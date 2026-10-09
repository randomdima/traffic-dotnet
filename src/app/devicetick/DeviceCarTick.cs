using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>A following car's whole tick on the device</b> (<c>world/town/kernels/car_tick.glsl</c>): its line read, what it is
/// told and the line driven (<c>car_tick.comp</c>), then its tyres (<see cref="DeviceTyres"/>) — two kernels over one
/// table, in one submit.
/// </summary>
/// <remarks>
/// <para>
/// <b>The table is the device's, indexed by the fleet's own car</b>, and kept from one tick to the next
/// (<see cref="ResidentRows{T}"/>): a car's line, where it is on it, its command, its tyres and the junction ahead are
/// what its last tick left. The host sends what it changed — every car's pose, what each taken car is told and was
/// granted, and a line, a reading or a whole car where something other than the tick changed it — and only that.
/// </para>
/// <para>
/// <b>Run in shadow</b> (<see cref="Prepare"/>, <see cref="Disagreement"/>): the town still ticks every car, and the
/// device's tick of the cars it takes is held to it. <see cref="TickMs"/> and <see cref="TyresMs"/> are the kernels alone.
/// </para>
/// </remarks>
internal sealed partial class DeviceCarTick : IDisposable
{
    /// <summary>What a car's tick carries from one tick to the next and reports, the device's own (<c>CarTickRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct TickRow(
        float ProgressM, float CoveredSinceClaimM, float AlongMps, float OffLineM, float PlannedMps, uint Hold, uint Flags)
    {
        public const uint Lost = 32u, AsksTheRoute = 64u, Shifted = 128u, Hazard = 256u, SteppedOut = 512u, PassMade = 1024u;

        /// <summary>What a tick asks of the town (<c>TickEvents</c>): a car whose tick raised any is handed back whoever reads it.</summary>
        public const uint Events = Lost | AsksTheRoute | Shifted | Hazard | SteppedOut | PassMade;
    }

    /// <summary>
    /// What the host tells a car's tick (<c>CarToldRow</c>): <see cref="HasAHold"/> and <see cref="Cut"/> are the claims',
    /// told apart in a word of their own (<see cref="DeviceRebuild.Told"/>, or the shadow's).
    /// </summary>
    /// <remarks>
    /// <see cref="PassAsideM"/> is a switch's step across or an overtaking car's pass from rest; <see cref="StepsOutAtM"/>
    /// where along its line that pass's step out begins (<c>Overtaking.StepsOutAtM</c>).
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ToldRow(
        uint Flags, float PaceMps, float AllowanceM, float PassAsideM, float FromRestM, float PlaceReachM, Vector2 PlaceM,
        float StepsOutAtM, float Pad)
    {
        public const uint OnACall = 1u, LineIsReverse = 2u, HasAPlace = 4u, HasAHold = 8u, Cut = 16u, Switching = 32u;
        public const uint WaitsShortOfTheEnd = 64u, Overtaking = 128u;
    }

    /// <summary>The step a car is on (<c>OvertakeRow</c>): <see cref="Overtake"/> field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PassRow(
        int Lane, float OutM, float OutStepM, float BackM, float BackStepM, float AsideM, float OutMps, float BackMps, float ClearsM,
        uint Begun, float Pad0, float Pad1)
    {
        public static PassRow Of(in Overtake pass) => new(
            pass.Lane, pass.OutM, pass.OutStepM, pass.BackM, pass.BackStepM, pass.AsideM, pass.OutMps, pass.BackMps, pass.ClearsM,
            pass.Begun ? 1u : 0u, 0f, 0f);

        public Overtake ToOvertake() => new(Lane, OutM, OutStepM, BackM, BackStepM, AsideM, OutMps, BackMps, ClearsM, Begun != 0u);
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
        float AuthorityM, uint GrantCutBy, float PlaceStopM, float MarginM, uint WaitsToPass, float HorizonM, float PassAsideM,
        float BackUpM, uint Blocked, float WaitAtM, float Pad0, float Pad1);

    /// <summary>The junction ahead (<c>BoxAheadRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct BoxRow(float ToTheBoxM, float LightAheadM, uint Flags, uint Pad)
    {
        public const uint Claimed = 1u, Turning = 2u, Inside = 4u, Committed = 8u;
    }

    /// <summary>
    /// What the town reads of a car's tick besides its <see cref="TickRow"/> (<c>CarBriefRow</c>): of the context only how
    /// far it was granted, how far aside its pass steps and whether it is blocked, and the junction ahead whole — its
    /// <see cref="BoxRow"/> flags with <see cref="Blocked"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct BriefRow(float AuthorityM, float PassAsideM, float ToTheBoxM, float LightAheadM, uint Flags)
    {
        public const uint Blocked = 16u;
    }

    /// <summary>A car's <see cref="GrantRow"/> but for the place, which is told (<c>CarGrantOut</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct GrantOut(
        float AuthorityM, float HorizonM, float ClaimFromM, float CommittedToM, float GrantMarginM, uint GrantCutBy, int CutOn)
    {
        public static GrantOut Of(in GrantRow row) =>
            new(row.AuthorityM, row.HorizonM, row.ClaimFromM, row.CommittedToM, row.GrantMarginM, row.GrantCutBy, row.CutOn);

        /// <remarks>The car tick's own grant table holds the place as zero (<see cref="DeviceCarTick.GrantOf"/>), and no kernel writes it.</remarks>
        public GrantRow ToGrantRow() => new(AuthorityM, HorizonM, ClaimFromM, CommittedToM, GrantMarginM, GrantCutBy, CutOn, 0f, Vector2.Zero);
    }

    /// <summary>
    /// What the town reads of one car's tick, written into the host's memory where it reads it (<c>car_out.comp</c>'s
    /// <c>CarOutRow</c>): its <see cref="TickRow"/>, command, <see cref="BriefRow"/> and tyre row, its grant as the tick
    /// read it, its leg's clock, and the stamp of the tick that wrote it.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct OutRow(
        TickRow Tick, DeviceTyres.CommandRow Command, BriefRow Brief, DeviceTyres.CarRow Car, GrantOut Grant, float ClockSinceS,
        float ClockClosestM, uint Stamp);

    /// <summary>
    /// What only the town knows of a car whose decision the device is asked whether it is its clock alone
    /// (<c>CarDecideTermsRow</c>): <see cref="Switching.RoadShape"/>, or <see cref="ByTheTown"/>; and what its clock is run by
    /// (<see cref="World.Town.TownWorld.ClockTermsOf"/>).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct DecideTermsRow(uint Flags, float PatienceS, float ByM, float QueueReachM)
    {
        /// <summary>The town decides it whole whatever the device finds.</summary>
        public const uint ByTheTown = 256u;

        /// <summary>What it was read off is handed back whatever the verdict, for the town to hold the verdict to its own.</summary>
        public const uint Checked = 512u;
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct OutPush(uint CarCount, uint Stamp, uint GrantsLeft);

    /// <summary>The tick's figures, field for field with <c>car_tick.comp</c>'s, each the host's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Figures(
        uint CarCount, uint ArcsPerCar, uint MostLanes, uint FirstConnector, float RoundingM, float SlowerToPassMps,
        float QueueInCarLengths, float DtS, float LookaheadS, float GripMargin, float BrakingMargin, float StopSpeedMps,
        float WaitingToPassBrakingShare, float ReactionS, float CallPaceMps, float StandardGravityMps2, float WheelSpinFadeMps,
        float WheelSpinAllowanceMps, float MarkSlipMps, float MarkCorneringSlipMps, float PloughCrawlMps, float EngineBrakingMps2,
        float PassSpareM, uint Mode, uint Stamp, float SinceLastDecisionS, float SlowerToSwitchMps)
    {
        public const uint Tick = 0u, Decide = 1u;

        /// <remarks>A decision answers for the decisions' interval as the loop reckons it (<see cref="TickGroups.IntervalS"/>).</remarks>
        public static Figures Of(SimConfig config, int arcsPerCar, int firstConnector) => new(
            0u, (uint)arcsPerCar, (uint)LineAssembler.MostLanes, (uint)firstConnector, LineTolerance.RoundingM,
            config.Ambulance.SlowerToPassMps, config.Patience.LightQueueInCarLengths, config.TickSeconds, config.Driving.LookaheadS,
            config.Driving.GripMargin, config.Driving.BrakingMargin, config.Driving.StopSpeedMps,
            config.Driving.WaitingToPassBrakingShare, config.CarReactionS, config.Ambulance.CallPaceMps,
            config.Tyre.StandardGravityMps2, config.Tyre.WheelSpinFadeMps, config.Tyre.WheelSpinAllowanceMps, config.Marks.SlipMps,
            config.Marks.CorneringSlipMps, config.Marks.PloughCrawlMps, config.CarEngineBrakingMps2, config.Driving.PassSpareM, Tick,
            0u, new TickGroups(config.Sim.AgentDecisionIntervalS, config.Sim.TickRateHz).IntervalS, config.Driving.SlowerToSwitchMps);
    }

    /// <summary>The stamps a tick is timed between.</summary>
    enum Stage { Began, Sent, Ticked, Tyred, Read }

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly DeviceTyres _tyres;
    readonly Figures _figures;
    readonly List<GpuBuffer> _laid = [];
    readonly ResidentRows<TickRow> _ticks;
    readonly ResidentRows<ToldRow> _told;
    readonly ResidentRows<GrantRow> _grants;
    readonly ResidentRows<DeviceClaims.ClaimRow> _cutters;
    readonly ResidentRows<BoxRow> _boxes;
    readonly ResidentRows<ContextRow> _contexts;
    readonly ResidentRows<LineRow> _lines;
    readonly ResidentRows<ArcRow> _arcs;
    readonly ResidentRows<float> _entries;
    readonly ResidentRows<int> _chain;
    readonly ResidentRows<float> _starts;
    readonly ResidentRows<float> _ends;
    readonly ResidentRows<uint> _firstArcs;
    readonly ResidentRows<uint> _breaks;
    readonly ResidentRows<uint> _taken;
    readonly ResidentRows<PassRow> _passes;
    readonly ResidentRows<uint> _holdBits;
    readonly ResidentRows<BriefRow> _briefs;
    readonly ComputeKernel _tickKernel;
    readonly DescriptorSet _tickSet;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    int _cars;
    int _takenCount;

    /// <summary>
    /// <see cref="ReadsEveryCar"/> off: which cars the host reads after a tick, a bit a car, written by the host and read
    /// by <c>car_out.comp</c>; the rows it writes, and the cars whose tick asked anything, in the host's own memory; and
    /// how many those were.
    /// </summary>
    readonly GpuBuffer _wanted, _rowsOut, _events, _eventCount, _eventCountOut;

    /// <summary>Every car's grant left as the last tick that took it with <see cref="HandsBackTheGrantsLeft"/> left it, in the host's memory.</summary>
    readonly GpuBuffer _grantsLeft;

    readonly ComputeKernel _outKernel;
    readonly DescriptorSet _outSet;
    readonly uint[] _eventsRead;
    readonly BufferCopy[] _handOverRuns;

    /// <summary>
    /// The decisions put off a tick the device is asked of (<see cref="LayTheDecisions"/>): the cars, what only the town
    /// knows of each, every car's leg's clock as the device runs it, and what it found — each verdict and, where a decision
    /// is more than the clock, what the town decides it on, in the host's memory — and which cars it found so this tick.
    /// </summary>
    readonly ResidentRows<uint> _decideList;

    readonly ResidentRows<DecideTermsRow> _decideTerms;
    readonly ResidentRows<Vector2> _clocks;
    readonly GpuBuffer _verdicts, _decidedAt, _decideOut;
    int _decideCount;

    /// <summary>
    /// Each car's grant, what cut it and its hold bits as the tick reads them, and as the town's readers read them
    /// (<see cref="ReadersKeepUp"/>) — copied from the first where the readers have the claims the tick has.
    /// </summary>
    readonly GpuBuffer _grantsBound, _cuttersBound, _holdBitsBound, _readersGrants, _readersCutters, _readersHoldBits;

    /// <summary>The stamp the last tick wrote its rows with, never the zero of a row never written.</summary>
    uint _stamp;

    /// <param name="tyres">The tyre pass whose table the tick writes the commands into; its capacity is the fleet's.</param>
    /// <param name="arcsPerCar">The room each car's line has (<see cref="Agents.Car.Body.CarFleet.LineArcsOf"/>).</param>
    /// <param name="firstConnector">The way the town numbers its first connector (<c>TownWays.OfRoadConnector</c>).</param>
    /// <param name="claims">The claims as the last rebuild cut them: every slot's row, and each way's planned pieces in order.</param>
    /// <param name="granted">
    /// Each car's grant, what cut it and its hold bits as the device's own rebuild writes them (<see cref="DeviceRebuild"/>),
    /// bound in place of the tables the host would lay — or none, and the host lays them.
    /// </param>
    public DeviceCarTick(
        Vk vk, SimConfig config, DeviceTyres tyres, RoadGraph roads, int arcsPerCar, int firstConnector,
        (GpuBuffer Rows, GpuBuffer PlannedRuns, GpuBuffer PlannedOrder) claims,
        (GpuBuffer Grants, GpuBuffer Cutters, GpuBuffer HoldBits)? granted = null)
    {
        GrantedOnTheDevice = granted.HasValue;
        _vk = vk;
        _config = config;
        _tyres = tyres;
        _figures = Figures.Of(config, arcsPerCar, firstConnector);
        var capacity = tyres.Capacity;
        var lanes = LineAssembler.MostLanes;

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

        _ticks = new ResidentRows<TickRow>(vk, capacity, 1, readsBack: true);
        _told = new ResidentRows<ToldRow>(vk, capacity, 1, hostOnly: true);
        // Not bridged: a rebuild on the device writes every asked car's grant before the host knows it (GrantsArePending).
        _grants = new ResidentRows<GrantRow>(vk, capacity, 1, readsBack: true, hostOnly: true, bridges: false);
        _cutters = new ResidentRows<DeviceClaims.ClaimRow>(vk, capacity, 1, hostOnly: true, bridges: false);
        _boxes = new ResidentRows<BoxRow>(vk, capacity, 1, readsBack: true);
        _contexts = new ResidentRows<ContextRow>(vk, capacity, 1, readsBack: true);
        _lines = new ResidentRows<LineRow>(vk, capacity, 1, readsBack: true);
        _arcs = new ResidentRows<ArcRow>(vk, capacity, arcsPerCar);
        _entries = new ResidentRows<float>(vk, capacity, arcsPerCar);
        _chain = new ResidentRows<int>(vk, capacity, lanes, readsBack: true);
        _starts = new ResidentRows<float>(vk, capacity, lanes, readsBack: true);
        _ends = new ResidentRows<float>(vk, capacity, lanes, readsBack: true);
        _firstArcs = new ResidentRows<uint>(vk, capacity, lanes);
        _breaks = new ResidentRows<uint>(vk, capacity, lanes);
        _taken = new ResidentRows<uint>(vk, capacity, 1, hostOnly: true);
        _passes = new ResidentRows<PassRow>(vk, capacity, 1, readsBack: true);
        _holdBits = new ResidentRows<uint>(vk, capacity, 1, hostOnly: true, bridges: false);
        _briefs = new ResidentRows<BriefRow>(vk, capacity, 1, readsBack: true);
        _decideList = new ResidentRows<uint>(vk, capacity, 1);
        _decideTerms = new ResidentRows<DecideTermsRow>(vk, capacity, 1, hostOnly: true);
        _clocks = new ResidentRows<Vector2>(vk, capacity, 1, readsBack: true);
        _verdicts = vk.CreateBuffer((ulong)(capacity * sizeof(uint)), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _decideOut = vk.CreateBuffer((ulong)(capacity * Marshal.SizeOf<OutRow>()), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _decidedAt = Laid(new uint[capacity]);
        _grantsBound = granted?.Grants ?? _grants.Device;
        _cuttersBound = granted?.Cutters ?? _cutters.Device;
        _holdBitsBound = granted?.HoldBits ?? _holdBits.Device;
        _readersGrants = Laid(new GrantRow[capacity]);
        _readersCutters = Laid(new DeviceClaims.ClaimRow[capacity]);
        _readersHoldBits = Laid(new uint[capacity]);

        var table = tyres.Table;
        GpuBuffer[] ticked =
        [
            table.Builds, table.BuildOf.Device, table.Poses.Device, table.Commands.Device, _arcs.Device,
            _entries.Device, _chain.Device, _starts.Device, _ends.Device, _firstArcs.Device, _breaks.Device, _lines.Device,
            Laid(roads.LaneLengthM), Laid(connectorAt), Laid(connectorTo), Laid(connectorRung), Laid(connectorLength),
            Laid(lanePassable), claims.Rows, claims.PlannedRuns, claims.PlannedOrder, granted?.Cutters ?? _cutters.Device,
            granted?.Grants ?? _grants.Device, _told.Device, _ticks.Device, _contexts.Device, _boxes.Device, _taken.Device,
            _passes.Device, granted?.HoldBits ?? _holdBits.Device, _briefs.Device, _decideList.Device, _decideTerms.Device,
            _clocks.Device, _verdicts, _decidedAt, _decideOut, table.Cars.Device, _readersGrants, _readersCutters, _readersHoldBits,
        ];
        _tickKernel = new ComputeKernel(vk, "car_tick.comp", ticked.Length, (uint)Marshal.SizeOf<Figures>());
        _tickSet = _tickKernel.Bind(ticked);

        _wanted = vk.CreateBuffer((ulong)(WantedWords * sizeof(uint)), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        _rowsOut = vk.CreateBuffer((ulong)(capacity * Marshal.SizeOf<OutRow>()), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _events = vk.CreateBuffer((ulong)(capacity * sizeof(uint)), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _eventCount = vk.CreateBuffer(
            sizeof(uint), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None);
        _eventCountOut = vk.CreateBuffer(sizeof(uint), BufferUsageFlags.TransferDstBit, HostAccess.Reads);
        _grantsLeft = vk.CreateBuffer((ulong)(capacity * sizeof(float)), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _outKernel = new ComputeKernel(vk, "car_out.comp", 13, (uint)Marshal.SizeOf<OutPush>());
        _outSet = _outKernel.Bind(
        [
            _taken.Device, _wanted, _ticks.Device, table.Commands.Device, _briefs.Device, table.Cars.Device, _rowsOut, _events, _eventCount,
            _clocks.Device, _decidedAt, _grantsBound, _grantsLeft,
        ]);
        _eventsRead = new uint[capacity];
        _handOverRuns = new BufferCopy[capacity];

        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
        ShadowOf(capacity);
    }

    /// <summary>The most cars the table holds: the tyre pass's.</summary>
    public int Capacity => _tyres.Capacity;

    /// <summary>Every car's line as the table keeps it (<c>CarLineRow</c>, its arcs and entries, and its lanes' chain, starts, ends and breaks).</summary>
    internal readonly record struct LineTables(
        GpuBuffer Lines, GpuBuffer Arcs, GpuBuffer Entries, GpuBuffer Chain, GpuBuffer Starts, GpuBuffer Ends, GpuBuffer Breaks);

    /// <summary>
    /// <b>Every car's line as the table keeps it</b>, for a kernel that reads lines in place (<see cref="DevicePlans"/>,
    /// <see cref="DeviceBodies"/>):
    /// current for a car only once <see cref="LayTheLineOf"/> has been asked of it and sent.
    /// </summary>
    public LineTables Lines => new(_lines.Device, _arcs.Device, _entries.Device, _chain.Device, _starts.Device, _ends.Device, _breaks.Device);

    /// <summary>
    /// <b>Each car's grant, what cut it and its hold bits as the tick reads them</b> — the tables the host lays where it
    /// changed them, which a claims rebuild on the device writes for every car it asks (<see cref="AssumeTheGrantOf"/>).
    /// </summary>
    public (GpuBuffer Grants, GpuBuffer Cutters, GpuBuffer HoldBits) GrantTables => (_grants.Device, _cutters.Device, _holdBits.Device);

    /// <summary>
    /// <b>A car's line made current in the table</b>, for a kernel that reads lines in place (<see cref="Lines"/>): laid
    /// whole where the table's copy may not be the town's — a car the last tick did not take, or whose line the town has
    /// changed since — and sent by <see cref="RecordTheLinesSent"/>.
    /// </summary>
    public void LayTheLineOf(CarFleet cars, int car)
    {
        if (_held[car] && cars.LineLaid[car] == _lineLaidAt[car]) return;

        LayTheLine(cars, car);
        _lineLaidAt[car] = cars.LineLaid[car];
    }

    /// <summary>
    /// <b>Whether the table holds a car as the device's own tick left it</b>, for a kernel that reads it in place
    /// (<see cref="ReadingTables"/>): the device ticked it last and the town has not changed what it is doing since — its
    /// reading laid where the town changed that since (a line laid, a reading set), and sent by
    /// <see cref="RecordTheReadingsSent"/>. A car it does not hold is the town's.
    /// </summary>
    public bool HoldsTheReadingOf(CarFleet cars, int car)
    {
        if (!_held[car] || cars.Action[car] != _actionAt[car]) return false;

        if (cars.ProgressM[car] != _progressAt[car] || cars.CoveredSinceClaimM[car] != _coveredAt[car])
        {
            ExpectTheTownHolds(car, "its reading for a rebuild");
            LayTheReading(cars, car);
            _progressAt[car] = cars.ProgressM[car];
            _coveredAt[car] = cars.CoveredSinceClaimM[car];
        }

        return true;
    }

    /// <summary>Every car's reading and the context its last tick told it, as the table keeps them (<see cref="HoldsTheReadingOf"/>).</summary>
    public (GpuBuffer Ticks, GpuBuffer Contexts) ReadingTables => (_ticks.Device, _contexts.Device);

    /// <summary>The readings <see cref="HoldsTheReadingOf"/> laid and the poses laid since the last tick, copied across, recorded; the caller barriers.</summary>
    public void RecordTheReadingsSent(CommandBuffer commands)
    {
        _ticks.Send(commands);
        _tyres.Table.Poses.Send(commands);
    }

    /// <summary>How many bytes <see cref="RecordTheReadingsSent"/> will copy.</summary>
    public ulong ReadingsMarkedBytes => _ticks.MarkedBytes + _tyres.Table.Poses.MarkedBytes;

    /// <summary>How many bytes of lines <see cref="RecordTheLinesSent"/> will copy.</summary>
    public ulong LinesMarkedBytes =>
        _lines.MarkedBytes + _arcs.MarkedBytes + _entries.MarkedBytes + _chain.MarkedBytes + _starts.MarkedBytes + _ends.MarkedBytes
        + _firstArcs.MarkedBytes + _breaks.MarkedBytes;

    /// <summary>The lines <see cref="LayTheLineOf"/> laid, copied across, recorded; the caller barriers.</summary>
    public void RecordTheLinesSent(CommandBuffer commands)
    {
        _lines.Send(commands);
        _arcs.Send(commands);
        _entries.Send(commands);
        _chain.Send(commands);
        _starts.Send(commands);
        _ends.Send(commands);
        _firstArcs.Send(commands);
        _breaks.Send(commands);
    }

    /// <summary>Whether each car's grant, cutter and hold bits are the device's own rebuild's rather than laid by the host.</summary>
    public bool GrantedOnTheDevice { get; }

    /// <summary>The tyre pass the tick hands its commands to, whose table holds each car's pose, command and wheels.</summary>
    public DeviceTyres Tyres => _tyres;

    /// <summary>
    /// Whether <see cref="RecordRead"/> hands back what only the shadow compares: the lines, their chains and
    /// lanes' ends, each car's pass, the scrub under its wheels, and every context and junction ahead whole — or, not
    /// shadowed, each car's <see cref="BriefRow"/> in place of the last two.
    /// </summary>
    /// <remarks>
    /// <b>A town whose tick the device runs reads none of them</b>: the town shifts its own line as the device did
    /// (<c>TownWorld.ShiftedOnTheDevice</c>), the scrub is scratch the tick that wrote it spends, and of a context the
    /// town reads only how far a car was granted, how far aside its pass steps and whether it is blocked. Over a bus of
    /// a few GB/s they are most of what a tick would otherwise hand back.
    /// </remarks>
    public bool Shadowed
    {
        get => _tyres.ReadsScrub;
        set => _tyres.ReadsScrub = value;
    }

    public ReadOnlySpan<TickRow> TicksAfter => _ticks.AllRead[.._cars];

    public ReadOnlySpan<LineRow> LinesAfter => _lines.AllRead[.._cars];

    public ReadOnlySpan<int> ChainAfter(int car) => _chain.RowsRead(car);

    public ReadOnlySpan<float> StartsAfter(int car) => _starts.RowsRead(car);

    public ReadOnlySpan<float> EndsAfter(int car) => _ends.RowsRead(car);

    /// <summary>Every car's context as the last tick told it — shadowed only (<see cref="Shadowed"/>).</summary>
    public ReadOnlySpan<ContextRow> ContextsAfter => _contexts.AllRead[.._cars];

    /// <summary>The junction ahead of every car as the last tick read it — shadowed only.</summary>
    public ReadOnlySpan<BoxRow> BoxesAfter => _boxes.AllRead[.._cars];

    /// <summary>What the town reads of every car's context and junction ahead, as the last tick left them — not shadowed.</summary>
    public ReadOnlySpan<BriefRow> BriefsAfter => _briefs.AllRead[.._cars];

    /// <summary>The step each car is on as the last tick left it, shifted on with its line.</summary>
    public ReadOnlySpan<PassRow> PassesAfter => _passes.AllRead[.._cars];

    public ReadOnlySpan<DeviceTyres.CommandRow> CommandsAfter => _tyres.CommandsAfter;

    /// <summary>A car's leg's clock as the device runs it (<see cref="LayTheDecisions"/>), as the last read of every car left it.</summary>
    public Vector2 ClockAfter(int car) => _clocks.AllRead[car];

    /// <summary>The device's own time for the last tick's drive, in milliseconds, with nothing sent or read in it.</summary>
    public double TickMs => _timer.BetweenMs((int)Stage.Sent, (int)Stage.Ticked);

    /// <summary>The device's own time for the last tick's tyre pass, in milliseconds.</summary>
    public double TyresMs => _timer.BetweenMs((int)Stage.Ticked, (int)Stage.Tyred);

    /// <summary>The device's time taking the last tick's rows in, in milliseconds.</summary>
    public double SentMs => _timer.BetweenMs((int)Stage.Began, (int)Stage.Sent);

    /// <summary>The device's time handing the last tick's answers back, in milliseconds — the shadow's alone.</summary>
    public double ReadMs => _timer.BetweenMs((int)Stage.Tyred, (int)Stage.Read);

    /// <summary>How many bytes the last tick took in, every table's.</summary>
    public ulong SentBytes { get; private set; }

    /// <summary>How many of them were lines' arcs and their entry figures.</summary>
    public ulong LinesSentBytes { get; private set; }

    /// <summary>
    /// <b>One tick</b>: what was marked sent, the drive run for the first <paramref name="taken"/> cars of
    /// the list, the tyres for the fleet's first <paramref name="cars"/>, and what they left read back and waited for.
    /// One submit and one wait, and a copy a table, whatever the count.
    /// </summary>
    public void Step(int taken, int cars)
    {
        var commands = _batch.Begin();
        Record(commands, taken, cars);
        RecordRead(commands);
        _batch.SubmitAndWait();
        ReadTimes();
    }

    /// <summary>
    /// <b>One tick recorded into a caller's submit</b>: what was marked sent, the drive for the first
    /// <paramref name="taken"/> cars of the list, and the tyres for the fleet's first <paramref name="cars"/> — leaving
    /// the tyre pass's writes for the caller to barrier before whatever reads them.
    /// </summary>
    public void Record(CommandBuffer commands, int taken, int cars)
    {
        RecordTheSends(commands, taken, cars);
        RecordTheTick(commands);
    }

    /// <summary>
    /// <b>The first half of <see cref="Record"/></b>: what was marked sent, and the decisions put off asked of what the last
    /// tick left (<see cref="LayTheDecisions"/>) — for a caller that reads the verdicts before the tick itself is in.
    /// </summary>
    public void RecordTheSends(CommandBuffer commands, int taken, int cars)
    {
        if (cars > Capacity) throw new ArgumentOutOfRangeException(nameof(cars), $"The tick holds {Capacity} cars, not {cars}.");

        _cars = cars;
        _takenCount = taken;
        _stamp++;
        var vk = _vk;
        _timer.Reset(commands);
        _timer.Stamp(commands, (int)Stage.Began);
        var table = _tyres.Table;
        SentBytes = table.BuildOf.MarkedBytes + table.Poses.MarkedBytes + table.Commands.MarkedBytes + table.Cars.MarkedBytes
                    + table.Wheels.MarkedBytes + _ticks.MarkedBytes + _told.MarkedBytes + _grants.MarkedBytes + _cutters.MarkedBytes
                    + _boxes.MarkedBytes + _lines.MarkedBytes + _arcs.MarkedBytes + _entries.MarkedBytes + _chain.MarkedBytes
                    + _starts.MarkedBytes + _ends.MarkedBytes + _firstArcs.MarkedBytes + _breaks.MarkedBytes + _taken.MarkedBytes
                    + _passes.MarkedBytes + _holdBits.MarkedBytes;
        LinesSentBytes = _arcs.MarkedBytes + _entries.MarkedBytes;
        _tyres.SendMarked(commands);
        vk.Fill(commands, _eventCount, 0);
        _ticks.Send(commands);
        _told.Send(commands);
        _grants.Send(commands);
        _cutters.Send(commands);
        _boxes.Send(commands);
        _lines.Send(commands);
        _arcs.Send(commands);
        _entries.Send(commands);
        _chain.Send(commands);
        _starts.Send(commands);
        _ends.Send(commands);
        _firstArcs.Send(commands);
        _breaks.Send(commands);
        _taken.Send(commands);
        _passes.Send(commands);
        _holdBits.Send(commands);
        _decideList.Send(commands);
        _decideTerms.Send(commands);
        _clocks.Send(commands);
        if (ReadersKeepUp)
        {
            // As the claims stand once every rebuild before this submit and every grant sent in it are in.
            vk.AfterTheSubmitsBefore(commands);
            vk.Copy(commands, _grantsBound, _readersGrants, (ulong)(Capacity * Marshal.SizeOf<GrantRow>()));
            vk.Copy(commands, _cuttersBound, _readersCutters, (ulong)(Capacity * Marshal.SizeOf<DeviceClaims.ClaimRow>()));
            vk.Copy(commands, _holdBitsBound, _readersHoldBits, (ulong)(Capacity * sizeof(uint)));
            ReadersKeptUp = true;
        }

        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Sent);

        // The decisions put off read what the last tick left, before this one writes over it.
        if (_decideCount > 0)
        {
            var decided = (uint)((_decideCount + 63) / 64);
            _tickKernel.Dispatch(
                commands, _tickSet, decided, _figures with { CarCount = (uint)_decideCount, Mode = Figures.Decide, Stamp = _stamp });
            vk.BetweenKernels(commands);
            _decideCount = 0;
        }
    }

    /// <summary>
    /// <b>The second half of <see cref="Record"/></b>: the drive and the tyres over what <see cref="RecordTheSends"/> sent —
    /// in the same submit, or in one behind it.
    /// </summary>
    public void RecordTheTick(CommandBuffer commands)
    {
        var groups = (uint)Math.Max(1, (_takenCount + 63) / 64);
        _tickKernel.Dispatch(commands, _tickSet, groups, _figures with { CarCount = (uint)_takenCount, Stamp = _stamp });
        _vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Ticked);

        _tyres.Run(commands, _cars);
        _timer.Stamp(commands, (int)Stage.Tyred);
    }

    /// <summary>
    /// What the last <see cref="Record"/> left, recorded to be handed back where <see cref="TicksAfter"/> and the rest read
    /// it — or, <see cref="ReadsEveryCar"/> off, where <see cref="RowOut"/> and <see cref="Events"/> read it.
    /// </summary>
    public void RecordRead(CommandBuffer commands)
    {
        if (!ReadsEveryCar)
        {
            RecordTheOut(commands);
            return;
        }

        var cars = _cars;
        _vk.BeforeTransfer(commands);
        _tyres.Read(commands);
        _ticks.Read(commands, cars);
        _clocks.Read(commands, cars);
        if (!Shadowed)
        {
            _briefs.Read(commands, cars);
        }
        else
        {
            _contexts.Read(commands, cars);
            _boxes.Read(commands, cars);
            _lines.Read(commands, cars);
            _chain.Read(commands, cars);
            _starts.Read(commands, cars);
            _ends.Read(commands, cars);
            _passes.Read(commands, cars);
        }

        _timer.Stamp(commands, (int)Stage.Read);
    }

    /// <summary>
    /// <b>Whether every car the device ticked is handed back with every tick</b> (<see cref="RecordRead"/>), or only those
    /// the host marks <see cref="Wanted"/> and those whose tick asked something of the town (<see cref="Events"/>) — each
    /// written by <c>car_out.comp</c> into the host's own memory where <see cref="RowOut"/> reads it, every other car's
    /// left on the device until it is handed over (<see cref="RecordTheHandOver"/>).
    /// </summary>
    public bool ReadsEveryCar { get; set; } = true;

    int WantedWords => (Capacity + 31) / 32;

    /// <summary>Which cars the host reads after the next tick, a bit a car — written whole before it is recorded.</summary>
    public Span<uint> Wanted => _wanted.Span<uint>()[..WantedWords];

    /// <summary>
    /// What the last tick wrote of a car the host reads (<see cref="ReadsEveryCar"/> off): only a car it marked
    /// <see cref="Wanted"/> or named in <see cref="Events"/> — any other's row is an earlier tick's.
    /// </summary>
    public ref readonly OutRow RowOut(int car)
    {
        ref readonly var row = ref _rowsOut.Span<OutRow>()[car];
        if (row.Stamp != _stamp) throw new InvalidOperationException($"Car {car} was read back past the tick that wrote it.");

        return ref row;
    }

    /// <summary>
    /// <b>Whether the next tick hands back every car's grant left</b> (<see cref="Agents.Car.Body.CarFleet.GrantLeftM"/>) where
    /// <see cref="GrantLeftAfter"/> reads it, besides the rows the town reads (<see cref="ReadsEveryCar"/> off).
    /// </summary>
    public bool HandsBackTheGrantsLeft { get; set; }

    /// <summary>A car's grant left as the last tick that took it with <see cref="HandsBackTheGrantsLeft"/> left it.</summary>
    public float GrantLeftAfter(int car) => _grantsLeft.Span<float>()[car];

    /// <summary>
    /// The cars whose last tick asked something of the town (<see cref="TickRow.Events"/>), in order — read once the tick's
    /// submit has been waited for (<see cref="ReadsEveryCar"/> off).
    /// </summary>
    public ReadOnlySpan<uint> Events
    {
        get
        {
            var count = (int)Math.Min(_eventCountOut.Span<uint>()[0], (uint)_eventsRead.Length);
            var events = _eventsRead.AsSpan(0, count);
            _events.Span<uint>()[..count].CopyTo(events);
            events.Sort();
            return events;
        }
    }

    /// <summary>
    /// What the town reads of the cars it marked <see cref="Wanted"/> and of those whose tick asked anything, written into the
    /// host's memory, recorded after the tyre pass; the rest left on the device.
    /// </summary>
    void RecordTheOut(CommandBuffer commands)
    {
        _vk.BetweenKernels(commands);
        var groups = (uint)Math.Max(1, (_takenCount + 63) / 64);
        _outKernel.Dispatch(commands, _outSet, groups, new OutPush((uint)_takenCount, _stamp, HandsBackTheGrantsLeft ? 1u : 0u));
        _vk.BeforeTransfer(commands);
        _vk.Copy(commands, _eventCount, _eventCountOut, sizeof(uint));
        _tyres.RecordReadTheMarkCount(commands);
        _timer.Stamp(commands, (int)Stage.Read);
    }

    /// <summary>
    /// <b>These cars handed back whole</b> where <see cref="HandedOverRow"/> reads them — their wheels, and, where the device
    /// keeps what the town does not read (<see cref="ReadsEveryCar"/> off), their reading, command, brief and tyre row as
    /// the last tick left them — recorded. The caller barriers before it.
    /// </summary>
    public void RecordTheHandOver(CommandBuffer commands, ReadOnlySpan<int> cars)
    {
        _tyres.RecordHandOver(commands, cars);
        if (ReadsEveryCar) return;

        _ticks.ReadItems(commands, cars, _handOverRuns);
        _briefs.ReadItems(commands, cars, _handOverRuns);
        _clocks.ReadItems(commands, cars, _handOverRuns);
        _grants.ReadItems(commands, cars, _handOverRuns);
        _tyres.RecordHandOverOfTheRows(commands, cars);
    }

    /// <summary>What <see cref="RecordTheHandOver"/> copied back of a car, once its submit has been waited for.</summary>
    public OutRow HandedOverRow(int car)
    {
        var clock = _clocks.AllRead[car];
        return new OutRow(
            _ticks.AllRead[car], _tyres.CommandRead(car), _briefs.AllRead[car], _tyres.CarRead(car), GrantOut.Of(_grants.AllRead[car]),
            clock.X, clock.Y, _stamp);
    }

    /// <summary>
    /// <b>The decisions put off a tick that the next tick asks of the device</b>, before it runs (<c>car_decide.glsl</c>):
    /// cars whose last tick the device holds, each with what only the town knows of it laid (<see cref="LayTheDecideTerms"/>)
    /// and its clock the device's own (<see cref="LayTheClock"/>).
    /// </summary>
    public void LayTheDecisions(ReadOnlySpan<int> cars)
    {
        var list = _decideList.All;
        for (var at = 0; at < cars.Length; at++) list[at] = (uint)cars[at];
        _decideList.Mark(0, cars.Length);
        _decideCount = cars.Length;
    }

    /// <summary>What only the town knows of a car whose decision the device is asked, laid where it changed.</summary>
    public void LayTheDecideTerms(int car, in DecideTermsRow terms) => _decideTerms.Lay(car, terms);

    /// <summary>A car's leg's clock as the town holds it, laid where the device runs it.</summary>
    public void LayTheClock(int car, float sinceS, float closestM)
    {
        _clocks.Rows(car)[0] = new Vector2(sinceS, closestM);
        _clocks.Mark(car);
    }

    /// <summary>
    /// <b>Whether a car's decision the device was asked of was more than its clock</b> (<see cref="LayTheDecisions"/>), read
    /// once the tick's submit has been waited for; where it was not, the device ran the clock.
    /// </summary>
    public bool DecidedMoreThanTheClock(int car)
    {
        var verdict = _verdicts.Span<uint>()[car];
        if (verdict >> 1 != _stamp) throw new InvalidOperationException($"Car {car}'s decision was read past the tick that answered it.");

        return (verdict & 1u) != 0u;
    }

    /// <summary>
    /// <b>Whether the town's readers read the claims the tick reads</b>, set before each <see cref="Record"/>: where they
    /// do, the claims a decision is asked of are those; where they do not — a rebuild laid on the tick whose layer lands
    /// later — the decisions are asked of the claims the readers last had.
    /// </summary>
    public bool ReadersKeepUp { get; set; } = true;

    /// <summary>Whether the readers' claims have been copied at all, so a decision may be asked of them.</summary>
    public bool ReadersKeptUp { get; private set; }

    /// <summary>What a car whose decision was more than its clock is decided on: what its last tick left, and its clock as it stood.</summary>
    public ref readonly OutRow DecidedOn(int car) => ref _decideOut.Span<OutRow>()[car];

    /// <summary>
    /// The stamp of the last tick recorded, which each car whose decision it found more than its clock is marked with in
    /// <see cref="DecidedAt"/> — for a kernel after it that reads which.
    /// </summary>
    public uint Stamp => _stamp;

    /// <inheritdoc cref="Stamp"/>
    public GpuBuffer DecidedAt => _decidedAt;

    /// <summary>The device's stamps of the last tick taken in, once its submit has been waited for.</summary>
    public void ReadTimes() => _timer.Read();

    /// <summary>A device-local buffer holding these rows, sent once.</summary>
    GpuBuffer Laid<T>(T[] rows) where T : unmanaged
    {
        var buffer = _vk.CreateBuffer(
            (ulong)(Math.Max(1, rows.Length) * Marshal.SizeOf<T>()), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None);
        _laid.Add(buffer);
        buffer.Upload<T>(rows.Length, into => rows.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _batch.Dispose();
        _tickKernel.Dispose();
        _outKernel.Dispose();
        foreach (var table in (ReadOnlySpan<IDisposable>)[
                     _ticks, _told, _grants, _cutters, _boxes, _contexts, _lines, _arcs, _entries, _chain, _starts, _ends, _firstArcs,
                     _breaks, _taken, _passes, _holdBits, _briefs, _wanted, _rowsOut, _events, _eventCount, _eventCountOut, _grantsLeft,
                     _decideList, _decideTerms, _clocks, _verdicts, _decideOut])
        {
            table.Dispose();
        }

        foreach (var buffer in _laid) buffer.Dispose();
    }
}
