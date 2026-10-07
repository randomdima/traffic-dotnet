using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>Every car's claim of a rebuild laid on the device</b> (<c>agents/car/actions/follow/kernels/plan.glsl</c>), beside
/// the host's: the cars as they stand before their claims are cleared sent across, every car's claim cleared and its plan
/// down its own line laid as rows, one car a thread — read back to be held to the host's. One submit and one wait.
/// </summary>
/// <remarks>
/// <b>The road and the marks are laid once</b>; a car's line crosses only as far as it is laid. A car on a pass is the
/// host's still: its plan begins past the pass.
/// </remarks>
internal sealed class DevicePlans : IDisposable
{
    /// <summary><c>plan_cars.comp</c>'s own bound on the ways under one plan (<see cref="DrivingGround.MostWaysAlongALine"/>).</summary>
    const int MostWaysInTheKernel = 23;

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct PlanPush(
        uint CarCount, uint ArcsPerCar, uint MostLanes, uint MostPieces, uint FirstConnector, float ReactionS, float StandOffM,
        float StopSpeedMps, float PlannedRunS, float PlanMostM, float PlanMostJoins, float CallReachShare, float BrakingMargin,
        float SlowerToPassMps);

    /// <summary>What a car's claim came to (<c>plan_rows.glsl</c>'s <c>PlanOutRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PlanOutRow(
        float ClaimFromM, float ClaimToM, float CommittedToM, float HorizonM, float PassesBelowMps, uint Pieces, uint LaidAHold,
        uint Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CarLineRow(uint ArcCount, uint LaneCount, float LengthM, uint FinalApproach);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SectionRow(int OnWay, float FromM, float ToM, float MineFromM, float MineToM);

    const uint Proposes = 1u;
    const uint OnACall = 2u;
    const uint LineIsReverse = 4u;

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly int _cars;
    readonly int _arcsPerCar;
    readonly int _mostPieces;
    readonly uint _firstConnector;
    readonly List<GpuBuffer> _owned = [];
    readonly GpuBuffer _carRows, _carFigures, _lines, _arcs, _entries, _chain, _starts, _ends, _breaks;
    readonly GpuBuffer _pieces, _results;

    /// <summary>What each laying sends, and the device-local buffers it lands in.</summary>
    readonly GpuBuffer[] _sent, _resident;

    readonly List<(GpuBuffer From, GpuBuffer To)> _once = [];
    readonly ComputeKernel _plan;
    readonly DescriptorSet _set;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;

    public DevicePlans(Vk vk, SimConfig config, TownWorld world)
    {
        if (DrivingGround.MostWaysAlongALine != MostWaysInTheKernel)
        {
            throw new InvalidOperationException($"plan_cars.comp is built for {MostWaysInTheKernel} ways along a line, and the town lays {DrivingGround.MostWaysAlongALine}.");
        }

        _vk = vk;
        _config = config;
        var cars = world.Cars;
        _cars = cars.Capacity;
        _arcsPerCar = cars.LineArcsOf(0).Length;
        var roads = world.Roads;
        var marks = world.Atlas.Marks;
        _mostPieces = (MostWaysInTheKernel + 1) * (1 + marks.MostCrossedByOne);
        _firstConnector = (uint)world.Ways.OfRoadConnector(0);

        var fleet = CarBuilds.OfTheFleet(config, CarCatalog.Shared);
        var builds = new CarBuildRow[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = CarBuildRow.Of(fleet.Of(variant));

        var connectorAt = new uint[roads.LaneCount + 1];
        var connectorTo = new int[Math.Max(1, roads.ConnectorCount)];
        var connectorRung = new uint[Math.Max(1, roads.ConnectorCount)];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var run = roads.ConnectorsFrom(lane);
            connectorAt[lane] = (uint)run.FirstId;
            connectorAt[lane + 1] = (uint)(run.FirstId + run.Count);
        }

        for (var connector = 0; connector < roads.ConnectorCount; connector++)
        {
            connectorTo[connector] = roads.ConnectorTo(connector);
            connectorRung[connector] = (uint)roads.FirmOnConnector(connector);
        }

        var wayCount = world.Ways.Count;
        var markAt = new uint[wayCount + 1];
        var sections = new List<SectionRow>();
        for (var way = 0; way < wayCount; way++)
        {
            markAt[way] = (uint)sections.Count;
            foreach (var section in marks.Of(way))
            {
                sections.Add(new SectionRow(section.OnWay, section.FromM, section.ToM, section.MineFromM, section.MineToM));
            }
        }

        markAt[wayCount] = (uint)sections.Count;
        if (sections.Count == 0) sections.Add(default);

        _carRows = Mapped<UInt4>(_cars);
        _carFigures = Mapped<Vector4>(_cars * 2);
        _lines = Mapped<CarLineRow>(_cars);
        _arcs = Mapped<DeviceGround.ArcRow>(_cars * _arcsPerCar);
        _entries = Mapped<float>(_cars * _arcsPerCar);
        _chain = Mapped<int>(_cars * LineAssembler.MostLanes);
        _starts = Mapped<float>(_cars * LineAssembler.MostLanes);
        _ends = Mapped<float>(_cars * LineAssembler.MostLanes);
        _breaks = Mapped<uint>(_cars * LineAssembler.MostLanes);
        _pieces = Read<DeviceClaims.ClaimRow>(_cars * _mostPieces);
        _results = Read<PlanOutRow>(_cars);
        _sent = [_carRows, _carFigures, _lines, _arcs, _entries, _chain, _starts, _ends, _breaks];
        _resident = new GpuBuffer[_sent.Length];
        for (var at = 0; at < _sent.Length; at++) _resident[at] = Resident(_sent[at].SizeBytes);

        GpuBuffer[] set =
        [
            Resident(Laid<CarBuildRow>(builds)), .. _resident, Resident(Laid<uint>(connectorAt)), Resident(Laid<int>(connectorTo)),
            Resident(Laid<uint>(connectorRung)), Resident(Laid<float>(roads.LaneLengthM)), Resident(Laid<uint>(markAt)),
            Resident(Laid<SectionRow>(CollectionsMarshal.AsSpan(sections))), _pieces, _results,
        ];
        _plan = new ComputeKernel(vk, "plan_cars.comp", set.Length, (uint)Marshal.SizeOf<PlanPush>());
        _set = _plan.Bind(set);
        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, 2);

        var commands = _batch.Begin();
        foreach (var (from, to) in _once) vk.Copy(commands, from, to, from.SizeBytes);
        _batch.SubmitAndWait();
    }

    /// <summary>What every car's claim came to on the last laying.</summary>
    public ReadOnlySpan<PlanOutRow> Results => _results.Span<PlanOutRow>()[.._cars];

    /// <summary>One car's pieces as the last laying laid them, in the host's order.</summary>
    public ReadOnlySpan<DeviceClaims.ClaimRow> PiecesOf(int car) =>
        _pieces.Span<DeviceClaims.ClaimRow>().Slice(car * _mostPieces, (int)Results[car].Pieces);

    /// <summary>The device's time over the last laying, in milliseconds.</summary>
    public double LaidMs => _timer.BetweenMs(0, 1);

    /// <summary>
    /// <b>The cars as they stand before their claims are laid</b> (<see cref="ClaimsStage.PlansDue"/>), written
    /// into what the device reads: each car's figures, whether it plans down its line, and its line as far as it is laid.
    /// </summary>
    public void Lay(TownWorld world)
    {
        var cars = world.Cars;
        var rows = _carRows.Span<UInt4>();
        var figures = _carFigures.Span<Vector4>();
        var lines = _lines.Span<CarLineRow>();
        var arcs = _arcs.Span<DeviceGround.ArcRow>();
        var entries = _entries.Span<float>();
        var chain = _chain.Span<int>();
        var starts = _starts.Span<float>();
        var ends = _ends.Span<float>();
        var breaks = _breaks.Span<uint>();
        var lanes = LineAssembler.MostLanes;
        for (var car = 0; car < cars.Count; car++)
        {
            var proposes = world.PlansItsLine(car) && !cars.Pass[car].Begun;
            var flags = (proposes ? Proposes : 0u) | (cars.BlueLight[car] ? OnACall : 0u) | (cars.LineIsReverse[car] ? LineIsReverse : 0u);
            rows[car] = new UInt4(cars.Variant[car], flags, 0u, 0u);
            figures[car * 2] = new Vector4(cars.ProgressM[car], cars.AlongMps[car], cars.PlannedMps[car], cars.GroundCoefficient[car]);
            figures[car * 2 + 1] = new Vector4(cars.GrantMarginM[car], proposes ? world.KeptOffWhatItMayPassM(car) : 0f, 0f, 0f);

            var line = cars.Line[car];
            lines[car] = new CarLineRow((uint)line.ArcCount, (uint)line.LaneCount, line.LengthM, 0u);
            var laid = cars.LineOf(car);
            for (var arc = 0; arc < laid.Length; arc++) arcs[(car * _arcsPerCar) + arc] = DeviceGround.ArcRow.Of(laid[arc]);
            cars.EntriesOf(car).CopyTo(entries.Slice(car * _arcsPerCar));
            for (var slot = 0; slot < line.LaneCount; slot++)
            {
                chain[(car * lanes) + slot] = cars.ChainOf(car)[slot];
                starts[(car * lanes) + slot] = cars.LaneStartsOf(car)[slot];
                ends[(car * lanes) + slot] = cars.LaneEndsOf(car)[slot];
                breaks[(car * lanes) + slot] = cars.JoinBreaksOf(car)[slot] ? 1u : 0u;
            }
        }
    }

    /// <summary>Every car's claim laid, and what it came to read back.</summary>
    public void Plan(int carCount)
    {
        var driving = _config.Driving;
        var push = new PlanPush(
            (uint)carCount, (uint)_arcsPerCar, (uint)LineAssembler.MostLanes, (uint)_mostPieces, _firstConnector, _config.CarReactionS,
            driving.StandOffM, driving.StopSpeedMps, driving.PlannedRunS, driving.PlanMostM, driving.PlanMostJoins,
            _config.Ambulance.CallReachShare, driving.BrakingMargin, _config.Ambulance.SlowerToPassMps);
        var commands = _batch.Begin();
        _timer.Reset(commands);
        for (var at = 0; at < _sent.Length; at++) _vk.Copy(commands, _sent[at], _resident[at], _sent[at].SizeBytes);
        _vk.AfterTransfer(commands);
        _timer.Stamp(commands, 0);
        _plan.Dispatch(commands, _set, (uint)Math.Max(1, (carCount + 63) / 64), push);
        _timer.Stamp(commands, 1);
        _batch.SubmitAndWait();
        _timer.Read();
    }

    GpuBuffer Mapped<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit, HostAccess.Writes));

    /// <summary>A device-local buffer the kernel reads, filled by a copy.</summary>
    GpuBuffer Resident(ulong bytes) =>
        Own(_vk.CreateBuffer(bytes, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));

    /// <summary>A device-local copy of a table laid once, copied when these are made.</summary>
    GpuBuffer Resident(GpuBuffer laid)
    {
        var resident = Resident(laid.SizeBytes);
        _once.Add((laid, resident));
        return resident;
    }

    GpuBuffer Read<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));

    GpuBuffer Laid<T>(ReadOnlySpan<T> rows) where T : unmanaged
    {
        var buffer = Mapped<T>(rows.Length);
        buffer.Write(rows);
        return buffer;
    }

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
        _plan.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
