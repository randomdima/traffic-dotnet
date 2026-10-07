using System.Runtime.InteropServices;
using System.Numerics;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>A following car's whole tick on the device</b> (<c>world/town/kernels/car_tick.glsl</c>): the ground under it
/// (<c>car_ground.comp</c>), its line read, what it is told and the line driven (<c>car_tick.comp</c>), then its tyres
/// (<see cref="DeviceTyres"/>) — three kernels over one table, in one submit.
/// </summary>
/// <remarks>
/// <b>Run in shadow</b>: every tick is handed the host's own state for the cars it ticks, one to a slot
/// (<see cref="Lay"/>), so <c>TownWorld.TickCar</c> and the kernels are asked one question. What a slot is sent from and
/// read back into is mapped memory beside the device's own; <see cref="TickMs"/> and <see cref="TyresMs"/> are the
/// kernels alone.
/// </remarks>
internal sealed partial class DeviceCarTick : IDisposable
{
    /// <summary>What a car's tick carries from one tick to the next and reports (<c>CarTickRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct TickRow(
        float ProgressM, float CoveredSinceClaimM, float AlongMps, float OffLineM, float PlannedMps, float PaceMps, float AllowanceM,
        uint Hold, uint Flags)
    {
        public const uint OnACall = 1u, LineIsReverse = 2u, HasAPlace = 4u, HasAHold = 8u, Cut = 16u;
        public const uint Lost = 32u, AsksTheRoute = 64u, Shifted = 128u, Hazard = 256u;
    }

    /// <summary>What the last claims rebuild granted a car and what cut it, and the place it was sent to (<c>CarGrantRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct GrantRow(
        float AuthorityM, float HorizonM, float ClaimFromM, float CommittedToM, float GrantMarginM, uint GrantCutBy, int CutOn,
        float PlaceReachM, Vector2 PlaceM);

    /// <summary>How much of a car's line there is, and whether it ends where the leg does (<c>CarLineRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct LineRow(uint ArcCount, uint LaneCount, float LengthM, uint FinalApproach);

    /// <summary>What the driver was told (<c>DriveContextRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ContextRow(
        float GroundCoefficient, float AuthorityM, uint GrantCutBy, float PlaceStopM, float MarginM, uint WaitsToPass, float HorizonM,
        float PassAsideM, float BackUpM, uint Blocked, float WaitAtM, float Pad);

    /// <summary>The junction ahead (<c>BoxAheadRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct BoxRow(float ToTheBoxM, float LightAheadM, uint Flags, uint Pad)
    {
        public const uint Claimed = 1u, Turning = 2u, Inside = 4u, Committed = 8u;
    }

    /// <summary>The tick's figures, field for field with <c>car_tick.comp</c>'s, each the host's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Figures(
        uint CarCount, uint ArcsPerCar, uint MostLanes, uint FirstConnector, float RoundingM, float SlowerToPassMps,
        float QueueInCarLengths, float DtS, float LookaheadS, float GripMargin, float BrakingMargin, float StopSpeedMps,
        float WaitingToPassBrakingShare, float ReactionS, float CallPaceMps, float StandardGravityMps2, float WheelSpinFadeMps,
        float WheelSpinAllowanceMps, float MarkSlipMps, float MarkCorneringSlipMps, float PloughCrawlMps, float EngineBrakingMps2)
    {
        public static Figures Of(SimConfig config, int arcsPerCar, int firstConnector) => new(
            0u, (uint)arcsPerCar, (uint)LineAssembler.MostLanes, (uint)firstConnector, LineTolerance.RoundingM,
            config.Ambulance.SlowerToPassMps, config.Patience.LightQueueInCarLengths, config.TickSeconds, config.Driving.LookaheadS,
            config.Driving.GripMargin, config.Driving.BrakingMargin, config.Driving.StopSpeedMps,
            config.Driving.WaitingToPassBrakingShare, config.CarReactionS, config.Ambulance.CallPaceMps,
            config.Tyre.StandardGravityMps2, config.Tyre.WheelSpinFadeMps, config.Tyre.WheelSpinAllowanceMps, config.Marks.SlipMps,
            config.Marks.CorneringSlipMps, config.Marks.PloughCrawlMps, config.CarEngineBrakingMps2);
    }

    /// <summary>The stamps a tick is timed between.</summary>
    enum Stage { Began, Sent, Ticked, Tyred, Read }

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly DeviceTyres _tyres;
    readonly DeviceGround _ground;
    readonly Figures _figures;
    readonly int _arcsPerCar;
    readonly int _mostLanes;
    readonly List<GpuBuffer> _owned = [];
    readonly ComputeKernel _groundKernel, _tickKernel;
    readonly DescriptorSet _groundSet, _tickSet;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    readonly (GpuBuffer From, GpuBuffer To, int Stride)[] _sent;
    readonly (GpuBuffer From, GpuBuffer To, int Stride)[] _read;
    readonly GpuBuffer _ticksIn, _grantsIn, _cuttersIn, _boxesIn, _linesIn, _arcsIn, _entriesIn, _chainIn, _startsIn, _endsIn;
    readonly GpuBuffer _firstArcsIn, _breaksIn;
    readonly GpuBuffer _ticksOut, _linesOut, _chainOut, _startsOut, _endsOut, _contextsOut, _boxesOut, _commandsOut;

    /// <summary>Whether the town's car in each slot had already asked for its line to be grown before the tick (<see cref="Lay"/>).</summary>
    readonly bool[] _askedBefore;
    int _cars;

    /// <param name="tyres">The tyre pass whose table the tick writes the commands into, and whose cars it ticks.</param>
    /// <param name="arcsPerCar">The room each car's line has (<see cref="Agents.Car.Body.CarFleet.LineArcsOf"/>).</param>
    /// <param name="firstConnector">The way the town numbers its first connector (<c>TownWays.OfRoadConnector</c>).</param>
    /// <param name="claims">The claims as the last rebuild cut them: every slot's row, and each way's planned pieces in order.</param>
    public DeviceCarTick(
        Vk vk, SimConfig config, DeviceTyres tyres, DeviceGround ground, RoadGraph roads, int arcsPerCar, int firstConnector,
        (GpuBuffer Rows, GpuBuffer PlannedRuns, GpuBuffer PlannedOrder) claims)
    {
        _vk = vk;
        _config = config;
        _tyres = tyres;
        _ground = ground;
        _arcsPerCar = arcsPerCar;
        _mostLanes = LineAssembler.MostLanes;
        _figures = Figures.Of(config, arcsPerCar, firstConnector);
        var capacity = tyres.Capacity;
        var arcs = capacity * arcsPerCar;
        var lanes = capacity * _mostLanes;
        _askedBefore = new bool[capacity];

        var connectorAt = new uint[roads.LaneCount + 1];
        var lanePassable = new uint[roads.LaneCount];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var run = roads.ConnectorsFrom(lane);
            connectorAt[lane] = (uint)run.FirstId;
            connectorAt[lane + 1] = (uint)(run.FirstId + run.Count);
            lanePassable[lane] = Overtaking.IsALaneToPassFrom(roads, lane) ? 1u : 0u;
        }

        var connectorTo = new int[roads.ConnectorCount];
        var connectorRung = new uint[roads.ConnectorCount];
        var connectorLength = new float[roads.ConnectorCount];
        for (var connector = 0; connector < roads.ConnectorCount; connector++)
        {
            connectorTo[connector] = roads.ConnectorTo(connector);
            connectorRung[connector] = (uint)roads.FirmOnConnector(connector);
            connectorLength[connector] = roads.ConnectorLengthM(connector);
        }

        var lineArcs = Device<DeviceGround.ArcRow>(arcs);
        var entries = Device<float>(arcs);
        var chain = Device<int>(lanes);
        var starts = Device<float>(lanes);
        var ends = Device<float>(lanes);
        var firstArcs = Device<uint>(lanes);
        var breaks = Device<uint>(lanes);
        var lines = Device<LineRow>(capacity);
        var cutters = Device<DeviceClaims.ClaimRow>(capacity);
        var grants = Device<GrantRow>(capacity);
        var ticks = Device<TickRow>(capacity);
        var contexts = Device<ContextRow>(capacity);
        var boxes = Device<BoxRow>(capacity);

        _ticksIn = Staging<TickRow>(capacity);
        _grantsIn = Staging<GrantRow>(capacity);
        _cuttersIn = Staging<DeviceClaims.ClaimRow>(capacity);
        _boxesIn = Staging<BoxRow>(capacity);
        _linesIn = Staging<LineRow>(capacity);
        _arcsIn = Staging<DeviceGround.ArcRow>(arcs);
        _entriesIn = Staging<float>(arcs);
        _chainIn = Staging<int>(lanes);
        _startsIn = Staging<float>(lanes);
        _endsIn = Staging<float>(lanes);
        _firstArcsIn = Staging<uint>(lanes);
        _breaksIn = Staging<uint>(lanes);

        _ticksOut = Readback<TickRow>(capacity);
        _linesOut = Readback<LineRow>(capacity);
        _chainOut = Readback<int>(lanes);
        _startsOut = Readback<float>(lanes);
        _endsOut = Readback<float>(lanes);
        _contextsOut = Readback<ContextRow>(capacity);
        _boxesOut = Readback<BoxRow>(capacity);
        _commandsOut = Readback<DeviceTyres.CommandRow>(capacity);

        _sent =
        [
            (_ticksIn, ticks, Marshal.SizeOf<TickRow>()), (_grantsIn, grants, Marshal.SizeOf<GrantRow>()),
            (_cuttersIn, cutters, Marshal.SizeOf<DeviceClaims.ClaimRow>()), (_boxesIn, boxes, Marshal.SizeOf<BoxRow>()),
            (_linesIn, lines, Marshal.SizeOf<LineRow>()), (_arcsIn, lineArcs, arcsPerCar * Marshal.SizeOf<DeviceGround.ArcRow>()),
            (_entriesIn, entries, arcsPerCar * sizeof(float)), (_chainIn, chain, _mostLanes * sizeof(int)),
            (_startsIn, starts, _mostLanes * sizeof(float)), (_endsIn, ends, _mostLanes * sizeof(float)),
            (_firstArcsIn, firstArcs, _mostLanes * sizeof(uint)), (_breaksIn, breaks, _mostLanes * sizeof(uint)),
        ];
        _read =
        [
            (ticks, _ticksOut, Marshal.SizeOf<TickRow>()), (lines, _linesOut, Marshal.SizeOf<LineRow>()),
            (chain, _chainOut, _mostLanes * sizeof(int)), (starts, _startsOut, _mostLanes * sizeof(float)),
            (ends, _endsOut, _mostLanes * sizeof(float)), (contexts, _contextsOut, Marshal.SizeOf<ContextRow>()),
            (boxes, _boxesOut, Marshal.SizeOf<BoxRow>()), (tyres.Table.Commands, _commandsOut, Marshal.SizeOf<DeviceTyres.CommandRow>()),
        ];

        var table = tyres.Table;
        GpuBuffer[] onTheGround = [table.Poses, table.Cars, lines, tyres.Effects, .. ground.Tables];
        _groundKernel = new ComputeKernel(vk, "car_ground.comp", onTheGround.Length, (uint)Marshal.SizeOf<DeviceGround.AskPush>());
        _groundSet = _groundKernel.Bind(onTheGround);

        GpuBuffer[] ticked =
        [
            table.Builds, table.BuildOf, table.Poses, table.Commands, table.Cars, lineArcs, entries, chain, starts, ends, firstArcs,
            breaks, lines, Laid(roads.LaneLengthM), Laid(connectorAt), Laid(connectorTo), Laid(connectorRung), Laid(connectorLength),
            Laid(lanePassable), claims.Rows, claims.PlannedRuns, claims.PlannedOrder, cutters, grants, ticks, contexts, boxes,
        ];
        _tickKernel = new ComputeKernel(vk, "car_tick.comp", ticked.Length, (uint)Marshal.SizeOf<Figures>());
        _tickSet = _tickKernel.Bind(ticked);

        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
    }

    /// <summary>The most cars a tick is asked of: the tyre pass's.</summary>
    public int Capacity => _tyres.Capacity;

    /// <summary>The tyre pass the tick hands its commands to, whose rows a slot's pose, command and wheels are written into.</summary>
    public DeviceTyres Tyres => _tyres;

    public Span<TickRow> Ticks => _ticksIn.Span<TickRow>()[..Capacity];

    public Span<GrantRow> Grants => _grantsIn.Span<GrantRow>()[..Capacity];

    /// <summary>What cut each car's hold, where its tick row says something did.</summary>
    public Span<DeviceClaims.ClaimRow> Cutters => _cuttersIn.Span<DeviceClaims.ClaimRow>()[..Capacity];

    /// <summary>The junction ahead as the last tick left it, whose commitment a car that loses its line keeps.</summary>
    public Span<BoxRow> Boxes => _boxesIn.Span<BoxRow>()[..Capacity];

    public Span<LineRow> Lines => _linesIn.Span<LineRow>()[..Capacity];

    /// <summary>One slot's line arcs, <see cref="_arcsPerCar"/> to a slot.</summary>
    public Span<DeviceGround.ArcRow> LineArcsOf(int slot) => _arcsIn.Span<DeviceGround.ArcRow>().Slice(slot * _arcsPerCar, _arcsPerCar);

    public Span<float> EntriesOf(int slot) => _entriesIn.Span<float>().Slice(slot * _arcsPerCar, _arcsPerCar);

    public Span<int> ChainOf(int slot) => _chainIn.Span<int>().Slice(slot * _mostLanes, _mostLanes);

    public Span<float> StartsOf(int slot) => _startsIn.Span<float>().Slice(slot * _mostLanes, _mostLanes);

    public Span<float> EndsOf(int slot) => _endsIn.Span<float>().Slice(slot * _mostLanes, _mostLanes);

    public Span<uint> FirstArcsOf(int slot) => _firstArcsIn.Span<uint>().Slice(slot * _mostLanes, _mostLanes);

    public Span<uint> BreaksOf(int slot) => _breaksIn.Span<uint>().Slice(slot * _mostLanes, _mostLanes);

    public ReadOnlySpan<TickRow> TicksAfter => _ticksOut.Span<TickRow>()[.._cars];

    public ReadOnlySpan<LineRow> LinesAfter => _linesOut.Span<LineRow>()[.._cars];

    public ReadOnlySpan<int> ChainAfter(int slot) => _chainOut.Span<int>().Slice(slot * _mostLanes, _mostLanes);

    public ReadOnlySpan<float> StartsAfter(int slot) => _startsOut.Span<float>().Slice(slot * _mostLanes, _mostLanes);

    public ReadOnlySpan<float> EndsAfter(int slot) => _endsOut.Span<float>().Slice(slot * _mostLanes, _mostLanes);

    public ReadOnlySpan<ContextRow> ContextsAfter => _contextsOut.Span<ContextRow>()[.._cars];

    public ReadOnlySpan<BoxRow> BoxesAfter => _boxesOut.Span<BoxRow>()[.._cars];

    public ReadOnlySpan<DeviceTyres.CommandRow> CommandsAfter => _commandsOut.Span<DeviceTyres.CommandRow>()[.._cars];

    /// <summary>The device's own time for the last tick's ground and drive, in milliseconds, with nothing sent or read in it.</summary>
    public double TickMs => _timer.BetweenMs((int)Stage.Sent, (int)Stage.Ticked);

    /// <summary>The device's own time for the last tick's tyre pass, in milliseconds.</summary>
    public double TyresMs => _timer.BetweenMs((int)Stage.Ticked, (int)Stage.Tyred);

    /// <summary>The device's time spent taking the last tick's rows in and handing its answers back, in milliseconds.</summary>
    public double CopiesMs => _timer.BetweenMs((int)Stage.Began, (int)Stage.Sent) + _timer.BetweenMs((int)Stage.Tyred, (int)Stage.Read);

    /// <summary>
    /// One tick of the first <paramref name="cars"/> slots of what was written: sent, the ground, the drive and the tyres
    /// run, read back and waited for. One submit and one wait, whatever the count.
    /// </summary>
    public void Step(int cars)
    {
        if (cars > Capacity) throw new ArgumentOutOfRangeException(nameof(cars), $"The tick holds {Capacity} cars, not {cars}.");

        _cars = cars;
        var vk = _vk;
        var groups = (uint)Math.Max(1, (cars + 63) / 64);
        var commands = _batch.Begin();
        _timer.Reset(commands);
        _timer.Stamp(commands, (int)Stage.Began);
        _tyres.Send(commands, cars);
        foreach (var (from, to, stride) in _sent) vk.Copy(commands, from, to, (ulong)(cars * stride));
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Sent);

        _groundKernel.Dispatch(commands, _groundSet, groups, _ground.PushFor(cars));
        vk.BetweenKernels(commands);
        _tickKernel.Dispatch(commands, _tickSet, groups, _figures with { CarCount = (uint)cars });
        vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Ticked);

        _tyres.Run(commands, cars);
        vk.BeforeTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Tyred);

        _tyres.Read(commands);
        foreach (var (from, to, stride) in _read) vk.Copy(commands, from, to, (ulong)(cars * stride));
        _timer.Stamp(commands, (int)Stage.Read);
        _batch.SubmitAndWait();
        _timer.Read();
    }

    GpuBuffer Device<T>(int count) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None));

    GpuBuffer Staging<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferSrcBit, HostAccess.Writes));

    GpuBuffer Readback<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferDstBit, HostAccess.Reads));

    /// <summary>A device-local buffer holding these rows, sent once.</summary>
    GpuBuffer Laid<T>(T[] rows) where T : unmanaged
    {
        var buffer = Own(_vk.CreateBuffer(
            Bytes<T>(rows.Length), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));
        buffer.Upload<T>(rows.Length, into => rows.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
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
        _tickKernel.Dispose();
        _groundKernel.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
