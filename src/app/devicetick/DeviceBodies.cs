using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>Every car's body laid onto the ways under it on the device</b> (<c>world/town/kernels/car_bodies.glsl</c>), beside
/// the host's: the cars as they stand when a rebuild begins sent across, each car's box read through the atlas
/// (<see cref="DeviceAtlas"/>) and its rows laid, one car a thread — read back to be held to the host's. One submit and
/// one wait.
/// </summary>
internal sealed class DeviceBodies : IDisposable
{
    /// <summary><c>atlas.glsl</c>'s own room under one body (<see cref="RibbonAtlas.MostWaysUnderABody"/>).</summary>
    const int MostWaysInTheKernel = 48;

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct BodyRow(
        Vector2 CentreM, Vector2 Forward, float HalfLengthM, float HalfWidthM, uint Channels, int Occupant, float AlongMps,
        uint Flags, float Pad0 = 0f, float Pad1 = 0f);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CarLineRow(uint ArcCount, uint LaneCount, float LengthM, uint FinalApproach);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct BodiesPush(DeviceAtlas.AskPush Ask, uint FirstConnector, uint MostLanes, uint GroundChannel);

    const uint Travelling = 1u;
    const uint RunsOnPastItself = 2u;
    const uint Still = 4u;
    const uint Blocked = 8u;

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly int _cars;
    readonly uint _firstConnector;
    readonly DeviceAtlas _atlas;
    readonly List<GpuBuffer> _owned = [];
    readonly GpuBuffer _bodies, _lines, _chain, _rows, _counts;
    readonly ComputeKernel _lay;
    readonly DescriptorSet _set;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;

    public DeviceBodies(Vk vk, SimConfig config, TownWorld world)
    {
        if (RibbonAtlas.MostWaysUnderABody != MostWaysInTheKernel)
        {
            throw new InvalidOperationException($"atlas.glsl is built for {MostWaysInTheKernel} ways under a body, and the town lays {RibbonAtlas.MostWaysUnderABody}.");
        }

        _vk = vk;
        _config = config;
        _cars = world.Cars.Capacity;
        _firstConnector = (uint)world.Ways.OfRoadConnector(0);
        _atlas = new DeviceAtlas(vk, world.Atlas.Table);

        var roads = world.Roads;
        var connectorAt = new uint[roads.LaneCount + 1];
        var connectorTo = new int[Math.Max(1, roads.ConnectorCount)];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var run = roads.ConnectorsFrom(lane);
            connectorAt[lane] = (uint)run.FirstId;
            connectorAt[lane + 1] = (uint)(run.FirstId + run.Count);
        }

        for (var connector = 0; connector < roads.ConnectorCount; connector++) connectorTo[connector] = roads.ConnectorTo(connector);

        _bodies = Mapped<BodyRow>(_cars, HostAccess.Writes);
        _lines = Mapped<CarLineRow>(_cars, HostAccess.Writes);
        _chain = Mapped<int>(_cars * LineAssembler.MostLanes, HostAccess.Writes);
        _rows = Mapped<DeviceClaims.ClaimRow>(_cars * MostWaysInTheKernel, HostAccess.Reads);
        _counts = Mapped<UInt4>(_cars, HostAccess.Reads);
        var connectorAtBuffer = Mapped<uint>(connectorAt.Length, HostAccess.Writes);
        connectorAtBuffer.Write<uint>(connectorAt);
        var connectorToBuffer = Mapped<int>(connectorTo.Length, HostAccess.Writes);
        connectorToBuffer.Write<int>(connectorTo);

        GpuBuffer[] set = [_bodies, _lines, _chain, connectorAtBuffer, connectorToBuffer, _rows, _counts, .. _atlas.Tables];
        _lay = new ComputeKernel(vk, "car_bodies.comp", set.Length, (uint)Marshal.SizeOf<BodiesPush>());
        _set = _lay.Bind(set);
        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, 2);
    }

    /// <summary>The device's time over the last laying, in milliseconds.</summary>
    public double LaidMs => _timer.BetweenMs(0, 1);

    /// <summary>One car's rows as the last laying laid them, in the order the atlas found its ways.</summary>
    public ReadOnlySpan<DeviceClaims.ClaimRow> RowsOf(int car) =>
        _rows.Span<DeviceClaims.ClaimRow>().Slice(car * MostWaysInTheKernel, (int)_counts.Span<UInt4>()[car].X);

    /// <summary>The channels a car is on once its body is laid: those of the ways under it, or its own over none.</summary>
    public byte ChannelsOf(int car) => (byte)_counts.Span<UInt4>()[car].Y;

    /// <summary>
    /// <b>The cars as they stand when a rebuild begins</b> (<see cref="ClaimsStage.Begun"/>), written into
    /// what the device reads: each box, on the host's own forward unit vector, and what it is laid as.
    /// </summary>
    public void Lay(TownWorld world)
    {
        var cars = world.Cars;
        var bodies = _bodies.Span<BodyRow>();
        var lines = _lines.Span<CarLineRow>();
        var chain = _chain.Span<int>();
        var lanes = LineAssembler.MostLanes;
        var stopMps = _config.Driving.StopSpeedMps;
        for (var car = 0; car < cars.Count; car++)
        {
            var laying = world.LayingOf(car);
            var halfM = cars.BuildOf(car).CollisionSizeM * 0.5f;
            var still = cars.VelocityMps[car].LengthSquared() <= stopMps * stopMps;
            var flags = (laying.Travelling ? Travelling : 0u) | (laying.RunsOnPastItself ? RunsOnPastItself : 0u)
                        | (still ? Still : 0u) | (laying.Blocked ? Blocked : 0u);
            bodies[car] = new BodyRow(
                cars.PositionM[car], Heading.Unit(cars.HeadingRad[car]), halfM.X, halfM.Y, cars.Channels[car], laying.Occupant,
                cars.AlongMps[laying.Occupant], flags);

            var line = cars.Line[car];
            lines[car] = new CarLineRow((uint)line.ArcCount, (uint)line.LaneCount, line.LengthM, 0u);
            for (var slot = 0; slot < line.LaneCount; slot++) chain[(car * lanes) + slot] = cars.ChainOf(car)[slot];
        }
    }

    /// <summary>Every car's body laid, and what each came to read back.</summary>
    public void LayTheBodies(int carCount)
    {
        var commands = _batch.Begin();
        _timer.Reset(commands);
        _timer.Stamp(commands, 0);
        _lay.Dispatch(
            commands, _set, (uint)Math.Max(1, (carCount + 63) / 64),
            new BodiesPush(_atlas.PushFor(carCount), _firstConnector, (uint)LineAssembler.MostLanes, CityGen.CityPlan.RoadArrays.GroundChannel));
        _timer.Stamp(commands, 1);
        _batch.SubmitAndWait();
        _timer.Read();
    }

    GpuBuffer Mapped<T>(int count, HostAccess access) where T : unmanaged => Own(
        _vk.CreateBuffer((ulong)(Math.Max(1, count) * Marshal.SizeOf<T>()), BufferUsageFlags.StorageBufferBit, access));

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _batch.Dispose();
        _lay.Dispose();
        _atlas.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
