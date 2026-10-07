using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>Every car's body laid onto the ways under it on the device</b> (<c>world/town/kernels/car_bodies.glsl</c>), beside
/// the host's: the cars as they stand when a rebuild begins sent across, each car's wheels read through the atlas
/// (<see cref="DeviceAtlas"/>) and its rows laid, one car a thread — read back to be held to the host's
/// (<see cref="LayTheBodies"/>), or left on the device for a rebuild to number (<see cref="Record"/>).
/// </summary>
internal sealed class DeviceBodies : IDisposable
{
    /// <summary><c>atlas.glsl</c>'s own room under one body (<see cref="RibbonAtlas.MostWaysUnderABody"/>).</summary>
    const int MostWaysInTheKernel = 48;

    /// <summary>
    /// One car as its body is read (<c>CarBodyRow</c>): its centre on the host's own forward unit vector, so no wheel is
    /// the device's own trigonometry, and where its axles and track put its wheels (<see cref="TyreModel.WheelAtM"/>).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct BodyRow(
        Vector2 CentreM, Vector2 Forward, float FrontAxleM, float RearAxleM, float HalfTrackM, uint Channels, int Occupant,
        float AlongMps, uint Flags, uint Pad = 0u);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CarLineRow(uint ArcCount, uint LaneCount, float LengthM, uint FinalApproach);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct BodiesPush(
        DeviceAtlas.AskPush Ask, uint FirstConnector, uint MostLanes, uint GroundChannel, float AroundAWheelM);

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
    readonly GpuBuffer _bodiesIn, _linesIn, _chainIn, _bodies, _lines, _chain, _rows, _counts, _rowsOut, _countsOut;
    readonly ComputeKernel _lay;
    readonly DescriptorSet _set;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    int _carCount;

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

        _bodiesIn = Staging<BodyRow>(_cars);
        _linesIn = Staging<CarLineRow>(_cars);
        _chainIn = Staging<int>(_cars * LineAssembler.MostLanes);
        _bodies = Device<BodyRow>(_cars);
        _lines = Device<CarLineRow>(_cars);
        _chain = Device<int>(_cars * LineAssembler.MostLanes);
        _rows = Device<DeviceClaims.ClaimRow>(_cars * MostWaysInTheKernel);
        _counts = Device<UInt4>(_cars);
        _rowsOut = Readback<DeviceClaims.ClaimRow>(_cars * MostWaysInTheKernel);
        _countsOut = Readback<UInt4>(_cars);
        var connectorAtBuffer = Own(vk.CreateBuffer(Bytes<uint>(connectorAt.Length), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));
        connectorAtBuffer.Write<uint>(connectorAt);
        var connectorToBuffer = Own(vk.CreateBuffer(Bytes<int>(connectorTo.Length), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));
        connectorToBuffer.Write<int>(connectorTo);

        GpuBuffer[] set = [_bodies, _lines, _chain, connectorAtBuffer, connectorToBuffer, _rows, _counts, .. _atlas.Tables];
        _lay = new ComputeKernel(vk, "car_bodies.comp", set.Length, (uint)Marshal.SizeOf<BodiesPush>());
        _set = _lay.Bind(set);
        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, 2);
    }

    /// <summary>The room each car's rows have in <see cref="OnTheDevice"/>.</summary>
    public static int Room => MostWaysInTheKernel;

    /// <summary>
    /// On the device, as the last laying left them: each car's rows at <see cref="Room"/> a car, in the order the atlas
    /// found its ways, and how many there are, the channels it is on, how many ways were dropped and which of its wheels
    /// stand on one (<c>uvec4</c> a car).
    /// </summary>
    public (GpuBuffer Rows, GpuBuffer Counts) OnTheDevice => (_rows, _counts);

    /// <summary>The device's time over the last laying run on its own, in milliseconds.</summary>
    public double LaidMs => _timer.BetweenMs(0, 1);

    /// <summary>One car's rows as the last laying read back laid them, in the order the atlas found its ways.</summary>
    public ReadOnlySpan<DeviceClaims.ClaimRow> RowsOf(int car) =>
        _rowsOut.Span<DeviceClaims.ClaimRow>().Slice(car * MostWaysInTheKernel, (int)_countsOut.Span<UInt4>()[car].X);

    /// <summary>The channels a car is on once its body is laid: those of the ways under it, or its own over none.</summary>
    public byte ChannelsOf(int car) => (byte)_countsOut.Span<UInt4>()[car].Y;

    /// <summary>Which of a car's wheels stand on a way once its body is laid (<see cref="CarFleet.WheelsOnAWay"/>).</summary>
    public byte WheelsOnAWayOf(int car) => (byte)_countsOut.Span<UInt4>()[car].W;

    /// <summary>
    /// <b>The cars as they stand when a rebuild begins</b> (<see cref="ClaimsStage.Begun"/>), written into
    /// what the device is sent: each car's wheels under it, on the host's own forward unit vector, and what it is laid as.
    /// </summary>
    public void Lay(TownWorld world)
    {
        var cars = world.Cars;
        var bodies = _bodiesIn.Span<BodyRow>();
        var lines = _linesIn.Span<CarLineRow>();
        var chain = _chainIn.Span<int>();
        var lanes = LineAssembler.MostLanes;
        var stopMps = _config.Driving.StopSpeedMps;
        _carCount = cars.Count;
        for (var car = 0; car < cars.Count; car++)
        {
            var laying = world.LayingOf(car);
            ref readonly var build = ref cars.BuildOf(car);
            var still = cars.VelocityMps[car].LengthSquared() <= stopMps * stopMps;
            var flags = (laying.Travelling ? Travelling : 0u) | (laying.RunsOnPastItself ? RunsOnPastItself : 0u)
                        | (still ? Still : 0u) | (laying.Blocked ? Blocked : 0u);
            bodies[car] = new BodyRow(
                cars.PositionM[car], Heading.Unit(cars.HeadingRad[car]), TyreModel.WheelAtM(build, 0).X,
                TyreModel.WheelAtM(build, 2).X, build.HalfTrackM, cars.Channels[car], laying.Occupant, cars.AlongMps[laying.Occupant],
                flags);

            var line = cars.Line[car];
            lines[car] = new CarLineRow((uint)line.ArcCount, (uint)line.LaneCount, line.LengthM, 0u);
            for (var slot = 0; slot < line.LaneCount; slot++) chain[(car * lanes) + slot] = cars.ChainOf(car)[slot];
        }
    }

    /// <summary>Every car's body laid, and what each came to read back. One submit and one wait.</summary>
    public void LayTheBodies(int carCount)
    {
        _carCount = carCount;
        var commands = _batch.Begin();
        _timer.Reset(commands);
        RecordSend(commands);
        _vk.AfterTransfer(commands);
        _timer.Stamp(commands, 0);
        Record(commands);
        _timer.Stamp(commands, 1);
        _vk.BeforeTransfer(commands);
        _vk.Copy(commands, _rows, _rowsOut, Bytes<DeviceClaims.ClaimRow>(_carCount * MostWaysInTheKernel));
        _vk.Copy(commands, _counts, _countsOut, Bytes<UInt4>(_carCount));
        _batch.SubmitAndWait();
        _timer.Read();
    }

    /// <summary>What was laid copied to the device, recorded; the caller barriers.</summary>
    public void RecordSend(CommandBuffer commands)
    {
        _vk.Copy(commands, _bodiesIn, _bodies, Bytes<BodyRow>(_carCount));
        _vk.Copy(commands, _linesIn, _lines, Bytes<CarLineRow>(_carCount));
        _vk.Copy(commands, _chainIn, _chain, Bytes<int>(_carCount * LineAssembler.MostLanes));
    }

    /// <summary>How many bytes <see cref="RecordSend"/> copies.</summary>
    public ulong SentBytes => Bytes<BodyRow>(_carCount) + Bytes<CarLineRow>(_carCount) + Bytes<int>(_carCount * LineAssembler.MostLanes);

    /// <summary>Every car laid for laid onto the ways under it, recorded and made visible to the next kernel.</summary>
    public void Record(CommandBuffer commands)
    {
        _lay.Dispatch(
            commands, _set, (uint)Math.Max(1, (_carCount + 63) / 64),
            new BodiesPush(
                _atlas.PushFor(_carCount), _firstConnector, (uint)LineAssembler.MostLanes, CityGen.CityPlan.RoadArrays.GroundChannel,
                _config.Road.HeldAroundAWheelM));
        _vk.BetweenKernels(commands);
    }

    GpuBuffer Device<T>(int count) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None));

    GpuBuffer Staging<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferSrcBit, HostAccess.Writes));

    GpuBuffer Readback<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferDstBit, HostAccess.Reads));

    static unsafe ulong Bytes<T>(int count) where T : unmanaged => (ulong)(Math.Max(1, count) * sizeof(T));

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
