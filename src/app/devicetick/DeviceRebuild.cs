using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.App.DeviceTick.DeviceClaims;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>A whole claims rebuild on the device, in one submit</b> (<see cref="TownWorld"/>'s <c>RebuildLaneOccupancy</c>):
/// every producer's kernel run, what each laid numbered into the claims' own rows and holds exactly as the town numbers
/// its slots and holds, the plans answered and cut there, and every car's grant and every walker's answer read off them
/// where the car's tick and the walkers' read take them — chained device stage to device stage, with nothing read back
/// between.
/// </summary>
/// <remarks>
/// <para>
/// <b>The numbering is the host's laying order, counted, scanned and filed</b> (<c>world/town/kernels/rebuild_*.comp</c>):
/// every walker's body and then every car's (<see cref="DeviceWalkers"/>, <see cref="DeviceBodies"/>), a piece grown into
/// its holder's body already on the way where the host grows it; the passes; every light's stretch
/// (<c>signal_holds.comp</c>); the claims the host lays where they stand; every car's claim (<see cref="DevicePlans"/>)
/// and every walker's walk — then <see cref="DeviceClaims"/> lists, answers and cuts them, and the backing is laid after.
/// No count crosses: every kernel over the rows or the holds stops at, and is dispatched by, the claims' tally.
/// </para>
/// <para>
/// <b>What only the host lays is sent as host-laid units filed at their own place in that order</b>
/// (<see cref="HostUnit"/>): the passes of overtakes, switches, bays and sidesteps, the holds of a hand, a tow and a
/// manoeuvre, the plan of a car on a pass, and the backing — each with what its own laying cut of what was already down
/// (<see cref="RowEdit"/>, <see cref="HoldEdit"/>) — and, of every car, whether it was asked this laying and what a claim
/// the host laid left it granted (<see cref="CarClaimRow"/>).
/// </para>
/// <para>
/// <b>Each producer is still sent the town as it stands at its own stage</b> (<see cref="Watch"/>): the chain is from what
/// they lay onward, so what one stage of an agent's own state differs by never reaches another's.
/// </para>
/// <para>
/// <b>What a car's tick reads</b>: <see cref="Grants"/> in <c>car_tick.glsl</c>'s <c>CarGrantRow</c> layout (the place
/// and its reach are never written here), <see cref="Cutters"/> a <see cref="ClaimRow"/> a car, and <see cref="Told"/>
/// a <c>uint</c> a car carrying <c>CarToldRow</c>'s <c>HasAHold</c> and <c>Cut</c> bits — one row a car, written for
/// every car the laying asked and left as they were for every other; and <see cref="Claims"/>, the rows as cut with each
/// way's planned run.
/// </para>
/// </remarks>
internal sealed partial class DeviceRebuild : IDisposable
{
    /// <summary>One run the host lays itself (<c>rebuild_rows.glsl</c>'s <c>HostUnit</c>).</summary>
    /// <param name="HostSlotFirst">The slot the host numbered its first row — what its holds' rows are counted from.</param>
    /// <param name="HostHoldFirst">And the hold — what its rows' holds are counted from.</param>
    /// <param name="Numbering">Where it is filed: <see cref="Numbering.Passes"/>, <see cref="Numbering.Plans"/> or <see cref="Numbering.Backing"/>.</param>
    /// <param name="Unit">Among the plans, the unit it is filed as.</param>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct HostUnit(
        uint RowsAt, uint RowCount, uint HoldsAt, uint HoldCount, int HostSlotFirst, int HostHoldFirst, uint Numbering, uint Unit);

    /// <summary>Which numbering a host-laid unit is filed in (<c>rebuild_rows.glsl</c>).</summary>
    internal static class Numbering
    {
        public const uint Passes = 0u;
        public const uint Plans = 1u;
        public const uint Backing = 2u;
    }

    /// <summary>What the host tells the rebuild of one car (<c>rebuild_rows.glsl</c>'s <c>CarClaimRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal record struct CarClaimRow(
        uint Flags, int HostUnit, int HoldUnit, int HoldOffset, float AuthorityM, float HorizonM, float ClaimFromM, float CommittedToM,
        float GrantMarginM, uint GrantCutBy, float Pad0 = 0f, float Pad1 = 0f)
    {
        public const uint Asked = 1u;
        public const uint PlansOnTheDevice = 2u;
    }

    /// <summary>A row the host cut after it was laid (<c>rebuild_rows.glsl</c>'s <c>RowEdit</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct RowEdit(uint Slot, float ToM, uint Gone, uint Pad = 0u);

    /// <summary>A hold the host cut after it was laid (<c>rebuild_rows.glsl</c>'s <c>HoldEdit</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct HoldEdit(uint Hold, uint Laid, uint Pad0, uint Pad1, ClaimAnswer Answer, ClaimRow Cutter);

    /// <summary>The stamps a rebuild is timed between.</summary>
    internal enum Stage
    {
        Began,
        Sent,
        BodiesLaid,
        PlansLaid,
        Answered,
        Granted,
        Read,
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct BodiesPush(uint WalkerCount, uint CarCount, uint WayCount, uint CoverRoom, uint CarRoom);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct TallyPush(uint Step, uint HostRows, uint HostHolds, uint Pad = 0u);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct PlansPush(
        uint StretchCount, uint CarCount, uint WalkerCount, uint MostPieces, uint ProposedRoom, uint StandingUnit, float StandOffM,
        uint Pad = 0u);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct RangePush(uint RowFirst, uint RowCount, uint HoldFirst, uint HoldCount);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct UnitsPush(uint Count, uint FirstUnit);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SignalPush(uint StretchCount, uint LaneCount, uint CrossingCount, float CycleS, float AmberTailS, float TimeS);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct StretchRow(uint Way, float FromM, float ToM, int Subject, uint ForCars, float Pad0 = 0f, float Pad1 = 0f, float Pad2 = 0f);

    /// <summary><c>rebuild_tally.comp</c>'s steps, and the totals its scans leave.</summary>
    enum Step : uint
    {
        Candidates,
        Bodies,
        Plans,
        Backing,
    }

    enum Sum
    {
        Candidates,
        Bodies,
        PlannedRows,
        PlannedHolds,
        Ways,
        Count,
    }

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly DeviceClaims _claims;
    readonly DeviceBodies _bodies;
    readonly DevicePlans _plans;
    readonly DeviceWalkers _walkers;
    readonly List<GpuBuffer> _owned = [];
    readonly int _cars, _walkersRoom, _ways, _stretches, _units, _candidates, _holdRoom;
    readonly uint _laneCount, _crossingCount;

    readonly GpuBuffer _unitCandidates, _unitBodies, _candidateRefs, _wayCounts, _wayStarts, _wayCursor, _candidateOrder, _firsts;
    readonly GpuBuffer _unitRows, _unitHolds, _sums, _blockSums, _holdCutters, _held, _stretchPieces;
    readonly GpuBuffer _grants, _cutters, _told, _grantsOut, _cuttersOut, _toldOut;

    /// <summary>What the host sends each rebuild, staged and on the device.</summary>
    readonly Sent<ClaimRow> _hostRows;
    readonly Sent<HoldRow> _hostHolds;
    readonly Sent<HostUnit> _hostUnits;
    readonly Sent<uint> _hostRowUnit, _hostHoldUnit;
    readonly Sent<RowEdit> _rowEdits;
    readonly Sent<HoldEdit> _holdEdits;
    readonly Sent<CarClaimRow> _carClaims;

    /// <summary>Every car's grant, cutter and told bits as the town had them, sent once before the first rebuild.</summary>
    readonly GpuBuffer _primedGrants, _primedCutters, _primedTold;

    readonly ScanKernels _scans;
    readonly PrefixScan _scanCandidates, _scanWays, _scanBodies, _scanRows, _scanHolds;
    readonly Bound _bodiesCount, _bodiesGather, _bodiesFile, _bodiesSort, _bodiesMark, _bodiesTally, _bodiesLay;
    readonly Bound _tally, _count, _file, _host, _edits, _capture, _grant, _walkerAnswers, _signals;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;

    public DeviceRebuild(Vk vk, SimConfig config, TownWorld world)
    {
        _vk = vk;
        _config = config;
        var claims = world.Occupancy;
        _cars = world.Cars.Capacity;
        _walkersRoom = world.People.Capacity;
        _ways = claims.WayCount;
        _holdRoom = claims.HoldCapacity;
        var lights = world.SignalHolds.Table;
        _stretches = lights.Way.Length;
        _units = _stretches + 1 + _cars + _walkersRoom;

        _claims = new DeviceClaims(vk, _ways, claims.Capacity, _holdRoom, config.Road.MostSettlingPasses, _cars, sentFromTheHost: false);
        var tables = _claims.OnTheDevice;
        _bodies = new DeviceBodies(vk, config, world);
        _plans = new DevicePlans(vk, config, world);
        _walkers = new DeviceWalkers(vk, config, world, tables);
        _candidates = Math.Max(1, (_walkersRoom * _walkers.CoverRoom) + (_cars * DeviceBodies.Room));

        _unitCandidates = Device<uint>(_walkersRoom + _cars);
        _unitBodies = Device<uint>(_walkersRoom + _cars);
        _candidateRefs = Device<uint>(_candidates);
        _wayCounts = Device<uint>(_ways);
        _wayStarts = Device<uint>(_ways);
        _wayCursor = Device<uint>(_ways);
        _candidateOrder = Device<uint>(_candidates);
        _firsts = Device<uint>(_candidates);
        _unitRows = Device<uint>(_units);
        _unitHolds = Device<uint>(_units);
        _sums = Device<uint>((int)Sum.Count);
        _blockSums = Device<uint>(ScanKernels.Block);
        _holdCutters = Device<ClaimRow>(_holdRoom);
        _held = Device<System.Numerics.Vector4>(_stretches);
        _stretchPieces = Device<ClaimRow>(_stretches);

        _grants = Device<DeviceCarTick.GrantRow>(_cars);
        _cutters = Device<ClaimRow>(_cars);
        _told = Device<uint>(_cars);
        _grantsOut = Readback<DeviceCarTick.GrantRow>(_cars);
        _cuttersOut = Readback<ClaimRow>(_cars);
        _toldOut = Readback<uint>(_cars);

        var hostRoom = HostRowRoom(world);
        _hostRows = new Sent<ClaimRow>(this, hostRoom);
        _hostHolds = new Sent<HoldRow>(this, _holdRoom);
        _hostUnits = new Sent<HostUnit>(this, _cars + 3);
        _hostRowUnit = new Sent<uint>(this, hostRoom);
        _hostHoldUnit = new Sent<uint>(this, _holdRoom);
        _rowEdits = new Sent<RowEdit>(this, hostRoom);
        _holdEdits = new Sent<HoldEdit>(this, 2 * _holdRoom);
        _carClaims = new Sent<CarClaimRow>(this, _cars);
        _primedGrants = Staging<DeviceCarTick.GrantRow>(_cars);
        _primedCutters = Staging<ClaimRow>(_cars);
        _primedTold = Staging<uint>(_cars);

        _scans = new ScanKernels(vk);
        _scanCandidates = new PrefixScan(vk, _scans, _unitCandidates, _blockSums, _sums, (uint)Sum.Candidates);
        _scanWays = new PrefixScan(vk, _scans, _wayStarts, _blockSums, _sums, (uint)Sum.Ways);
        _scanBodies = new PrefixScan(vk, _scans, _unitBodies, _blockSums, _sums, (uint)Sum.Bodies);
        _scanRows = new PrefixScan(vk, _scans, _unitRows, _blockSums, _sums, (uint)Sum.PlannedRows);
        _scanHolds = new PrefixScan(vk, _scans, _unitHolds, _blockSums, _sums, (uint)Sum.PlannedHolds);

        var (walkerCovers, walkerProposed, walkerHolds, walkerCounts) = _walkers.OnTheDevice;
        var (carRows, carCounts) = _bodies.OnTheDevice;
        GpuBuffer[] bodies =
        [
            walkerCounts, walkerCovers, carCounts, carRows, _unitCandidates, _unitBodies, _candidateRefs, _wayCounts, _wayStarts,
            _wayCursor, _candidateOrder, _firsts, tables.Rows, tables.Tally,
        ];
        _bodiesCount = Bound.Of<BodiesPush>(vk, "rebuild_bodies_count.comp", bodies);
        _bodiesGather = Bound.Of<BodiesPush>(vk, "rebuild_bodies_gather.comp", bodies);
        _bodiesFile = Bound.Of<BodiesPush>(vk, "rebuild_bodies_file.comp", bodies);
        _bodiesSort = Bound.Of<BodiesPush>(vk, "rebuild_bodies_sort.comp", bodies);
        _bodiesMark = Bound.Of<BodiesPush>(vk, "rebuild_bodies_mark.comp", bodies);
        _bodiesTally = Bound.Of<BodiesPush>(vk, "rebuild_bodies_tally.comp", bodies);
        _bodiesLay = Bound.Of<BodiesPush>(vk, "rebuild_bodies_lay.comp", bodies);
        _tally = Bound.Of<TallyPush>(vk, "rebuild_tally.comp", [_sums, tables.Tally]);

        var (planCars, planFigures, planPieces, planResults) = _plans.OnTheDevice;
        GpuBuffer[] plans =
        [
            _held, _stretchPieces, _hostUnits.Device, _carClaims.Device, planCars, planFigures, planResults, planPieces, walkerCounts,
            walkerHolds, walkerProposed, _unitRows, _unitHolds, tables.Rows, tables.Holds, tables.Answers, tables.Shown, tables.Tally,
        ];
        _count = Bound.Of<PlansPush>(vk, "rebuild_count.comp", plans);
        _file = Bound.Of<PlansPush>(vk, "rebuild_file.comp", plans);
        _host = Bound.Of<RangePush>(
            vk, "rebuild_host.comp",
            [
                _hostRows.Device, _hostHolds.Device, _hostUnits.Device, _hostRowUnit.Device, _hostHoldUnit.Device, _unitRows, _unitHolds,
                tables.Rows, tables.Holds, tables.Answers, tables.Shown, tables.Tally,
            ]);
        _edits = Bound.Of<RangePush>(
            vk, "rebuild_edits.comp", [_rowEdits.Device, _holdEdits.Device, tables.Rows, tables.Holds, tables.Answers, _holdCutters]);
        _capture = Bound.Of<NoPush>(vk, "rebuild_cutters.comp", [tables.Rows, tables.Answers, _holdCutters, tables.Tally]);
        _grant = Bound.Of<UnitsPush>(
            vk, "rebuild_grant.comp", [_carClaims.Device, planResults, _unitHolds, tables.Answers, _holdCutters, _grants, _cutters, _told]);
        _walkerAnswers = Bound.Of<UnitsPush>(vk, "rebuild_walkers.comp", [walkerCounts, _unitHolds, tables.Answers, _walkers.AnswersRead]);

        var signals = world.Signals.Table;
        _laneCount = (uint)signals.LaneAxis.Length;
        _crossingCount = (uint)signals.CrossingAxis.Length;
        var stretchRows = new StretchRow[Math.Max(1, _stretches)];
        for (var at = 0; at < _stretches; at++)
        {
            stretchRows[at] = new StretchRow((uint)lights.Way[at], lights.FromM[at], lights.ToM[at], lights.Subject[at], lights.ForCars[at] ? 1u : 0u);
        }

        // A light's piece is named its hold where it is filed, so the hold the kernel is handed is never read.
        _signals = Bound.Of<SignalPush>(
            vk, "signal_holds.comp",
            [
                Laid(signals.LaneAxis), Laid(signals.LaneJunction), Laid(signals.OffsetS), Laid(signals.CrossingAxis),
                Laid(signals.CrossingJunction), tables.Rows, tables.BodyRuns, tables.Order, Laid<StretchRow>(stretchRows),
                Device<int>(_stretches), _held, _stretchPieces,
            ]);

        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
        ShadowOf();
    }

    /// <summary>How many rebuilds have been recorded and submitted.</summary>
    public long Rebuilds { get; private set; }

    /// <summary>
    /// What a car's tick binds in place of its own grant tables (<c>car_tick.comp</c>'s bindings 23 and 22, and the told
    /// row's claims bits): <c>CarGrantRow</c> a car, every field but <c>placeReachM</c> and <c>placeM</c> written for every
    /// car the laying asked; the <see cref="ClaimRow"/> that cut its hold; and a <c>uint</c> holding <c>HasAHold</c> (8)
    /// and <c>Cut</c> (16), to be merged into <c>CarToldRow.flags</c>.
    /// </summary>
    public GpuBuffer Grants => _grants;

    /// <inheritdoc cref="Grants"/>
    public GpuBuffer Cutters => _cutters;

    /// <inheritdoc cref="Grants"/>
    public GpuBuffer Told => _told;

    /// <summary>The claims as the rebuild cut them, for a kernel that reads them after (<see cref="DeviceCarTick"/>).</summary>
    public (GpuBuffer Rows, GpuBuffer PlannedRuns, GpuBuffer PlannedOrder) Claims => _claims.Cut;

    /// <summary>The device's claims, as the last rebuild read back left them.</summary>
    public DeviceClaims Answered => _claims;

    /// <summary>The walkers' side, as the last rebuild read back left it.</summary>
    public DeviceWalkers Walkers => _walkers;

    /// <summary>Every car's grant, cutter and told bits, as the last rebuild read back left them.</summary>
    public ReadOnlySpan<DeviceCarTick.GrantRow> GrantsRead => _grantsOut.Span<DeviceCarTick.GrantRow>()[.._carCount];

    /// <inheritdoc cref="GrantsRead"/>
    public ReadOnlySpan<ClaimRow> CuttersRead => _cuttersOut.Span<ClaimRow>()[.._carCount];

    /// <inheritdoc cref="GrantsRead"/>
    public ReadOnlySpan<uint> ToldRead => _toldOut.Span<uint>()[.._carCount];

    /// <summary>The device's time between two stamps of the last rebuild, in milliseconds.</summary>
    public double BetweenMs(Stage from, Stage to) => _timer.BetweenMs((int)from, (int)to);

    /// <summary>
    /// How many bytes the last rebuild sent before its kernels — the shadow's alone: the producers' inputs and the host's
    /// own units — and how many of them were the cars' lines.
    /// </summary>
    public (ulong All, ulong Lines) SentBytes => (
        _bodies.SentBytes + _plans.SentBytes + (4 * _walkers.SentBytesAStage) + Bytes<ClaimRow>(_hostRowCount) + Bytes<uint>(_hostRowCount)
        + Bytes<HoldRow>(_hostHoldCount) + Bytes<uint>(_hostHoldCount) + Bytes<HostUnit>(_hostUnitCount) + Bytes<RowEdit>(_rowEditCount)
        + Bytes<HoldEdit>(_holdEditCount) + Bytes<CarClaimRow>(_carCount),
        _plans.LinesSentBytes);

    /// <summary>
    /// <b>One rebuild, recorded and submitted once</b>: what the producers and the host's units were laid sent across, then
    /// every kernel of the chain over the device's own tables, then — the shadow's alone — the claims, the grants and the
    /// walkers read back. A copy a table and a fixed run of dispatches, whatever the town's size.
    /// </summary>
    void Record()
    {
        var commands = _batch.Begin();
        Record(commands, readBack: true);
        _batch.SubmitAndWait();
        ReadTimes();
    }

    /// <summary>
    /// <b>Whether a rebuild is laid and waits to be recorded into a tick's submit</b> (<see cref="Chained"/>): every stage of
    /// the town's own rebuild has been watched, and nothing of it has run on the device yet.
    /// </summary>
    public bool Pending { get; private set; }

    /// <summary>
    /// Whether a rebuild is recorded into the tick's own submit (<see cref="Record(CommandBuffer, bool)"/>) rather than
    /// submitted by itself once the town's grants are read.
    /// </summary>
    public bool Chained { get; set; }

    /// <summary>
    /// <b>One rebuild recorded into a caller's submit</b>: what the producers and the host's units were laid sent across, then
    /// every kernel of the chain, then — where <paramref name="readBack"/> — the claims, the grants and the walkers read back.
    /// Its writes are left for the caller to barrier before whatever reads them.
    /// </summary>
    public void Record(CommandBuffer commands, bool readBack)
    {
        var vk = _vk;
        Pending = false;
        _timer.Reset(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        _bodies.RecordSend(commands);
        _plans.RecordSend(commands);
        _walkers.RecordSend(commands, DeviceWalkers.Piece.Station);
        _walkers.RecordSend(commands, DeviceWalkers.Piece.Body);
        _walkers.RecordSend(commands, DeviceWalkers.Piece.Plan);
        _walkers.RecordSend(commands, DeviceWalkers.Piece.Read);
        _hostRows.Send(commands, _hostRowCount);
        _hostRowUnit.Send(commands, _hostRowCount);
        _hostHolds.Send(commands, _hostHoldCount);
        _hostHoldUnit.Send(commands, _hostHoldCount);
        _hostUnits.Send(commands, _hostUnitCount);
        _rowEdits.Send(commands, _rowEditCount);
        _holdEdits.Send(commands, _holdEditCount);
        _carClaims.Send(commands, _carCount);
        if (_priming)
        {
            vk.Copy(commands, _primedGrants, _grants, Bytes<DeviceCarTick.GrantRow>(_carCount));
            vk.Copy(commands, _primedCutters, _cutters, Bytes<ClaimRow>(_carCount));
            vk.Copy(commands, _primedTold, _told, Bytes<uint>(_carCount));
            _priming = false;
        }

        vk.Fill(commands, _wayCounts, 0);
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Sent);

        RecordTheBodies(commands);
        _timer.Stamp(commands, (int)Stage.BodiesLaid);
        RecordThePlans(commands);
        _timer.Stamp(commands, (int)Stage.PlansLaid);

        _claims.RecordAnswers(commands);
        _capture.DispatchIndirect(commands, _claims.OnTheDevice.Tally, Tally.HoldsGroups, default(NoPush));
        vk.BetweenKernels(commands);
        _claims.RecordCut(commands);
        _timer.Stamp(commands, (int)Stage.Answered);

        RecordTheBacking(commands);
        _grant.Dispatch(commands, _carCount, new UnitsPush((uint)_carCount, (uint)(_stretches + 1)));
        _walkerAnswers.Dispatch(commands, _walkerCount, new UnitsPush((uint)_walkerCount, (uint)(_stretches + 1 + _carCount)));
        vk.BetweenKernels(commands);
        _walkers.Record(commands, DeviceWalkers.Piece.Read);
        _timer.Stamp(commands, (int)Stage.Granted);

        // Every stamp is written, read back or not: a stamp never written is one its read waits on for good.
        if (!readBack)
        {
            _timer.Stamp(commands, (int)Stage.Read);
            return;
        }

        vk.BeforeTransfer(commands);
        _claims.RecordRead(commands, _rowsToRead, _holdsToRead);
        vk.Copy(commands, _grants, _grantsOut, Bytes<DeviceCarTick.GrantRow>(_carCount));
        vk.Copy(commands, _cutters, _cuttersOut, Bytes<ClaimRow>(_carCount));
        vk.Copy(commands, _told, _toldOut, Bytes<uint>(_carCount));
        _walkers.RecordRead(commands, DeviceWalkers.Piece.Read);
        _timer.Stamp(commands, (int)Stage.Read);
    }

    /// <summary>The device's stamps of the last rebuild taken in, once its submit has been waited for.</summary>
    public void ReadTimes()
    {
        _timer.Read();
        Rebuilds++;
    }

    /// <summary>
    /// <b>Every body laid and numbered</b>: the walkers stationed and their bodies laid, every car's body laid, every piece
    /// gathered in the host's order and grown into its holder's body on its way or numbered a body of its own, the
    /// host's passes filed after them, and every way's lists laid from them for what reads the bodies next.
    /// </summary>
    void RecordTheBodies(CommandBuffer commands)
    {
        var vk = _vk;
        var tally = _claims.OnTheDevice.Tally;
        _walkers.Record(commands, DeviceWalkers.Piece.Station);
        _walkers.Record(commands, DeviceWalkers.Piece.Body);
        _bodies.Record(commands);

        var units = _walkerCount + _carCount;
        var push = new BodiesPush((uint)_walkerCount, (uint)_carCount, (uint)_ways, (uint)_walkers.CoverRoom, (uint)DeviceBodies.Room);
        _bodiesCount.Dispatch(commands, units, push);
        vk.BetweenKernels(commands);
        _scanCandidates.Record(commands, units);
        RecordTheTally(commands, Step.Candidates, 0, 0);
        _bodiesGather.Dispatch(commands, units, push);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, _wayCounts, _wayStarts, _wayCounts.SizeBytes);
        vk.AfterTransfer(commands);
        _scanWays.Record(commands, _ways);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, _wayStarts, _wayCursor, _wayStarts.SizeBytes);
        vk.AfterTransfer(commands);
        _bodiesFile.DispatchIndirect(commands, tally, Tally.CandidatesGroups, push);
        vk.BetweenKernels(commands);
        _bodiesSort.Dispatch(commands, _ways, push);
        vk.BetweenKernels(commands);
        _bodiesMark.DispatchIndirect(commands, tally, Tally.CandidatesGroups, push);
        vk.BetweenKernels(commands);
        _bodiesTally.Dispatch(commands, units, push);
        vk.BetweenKernels(commands);
        _scanBodies.Record(commands, units);
        RecordTheTally(commands, Step.Bodies, _passes.RowCount, 0);
        _bodiesLay.Dispatch(commands, units, push);
        RecordTheHostUnits(commands, _passes);
        vk.BetweenKernels(commands);
        _claims.RecordLists(commands);
    }

    /// <summary>
    /// <b>Every hold before the answers laid and numbered</b>: every light's stretch held, every car's plan and every
    /// walker's walk laid, each unit counted in the host's order — lights, the claims laid where they stand, cars,
    /// walkers — and filed, the host's own units among them, what the host's own laying cut written over them, and the
    /// lists laid again over everything.
    /// </summary>
    void RecordThePlans(CommandBuffer commands)
    {
        var vk = _vk;
        var signals = _config.Signals;
        _signals.Dispatch(
            commands, _stretches, new SignalPush((uint)_stretches, _laneCount, _crossingCount, signals.CycleS, signals.AmberTailS, _timeS));
        _plans.Record(commands);
        _walkers.Record(commands, DeviceWalkers.Piece.Plan);

        var units = _stretches + 1 + _carCount + _walkerCount;
        var push = new PlansPush(
            (uint)_stretches, (uint)_carCount, (uint)_walkerCount, (uint)_plans.MostPieces, (uint)_walkers.ProposedRoom,
            StandingUnit, _config.Driving.StandOffM);
        _count.Dispatch(commands, units, push);
        vk.BetweenKernels(commands);
        _scanRows.Record(commands, units);
        _scanHolds.Record(commands, units);
        RecordTheTally(commands, Step.Plans, 0, 0);
        _file.Dispatch(commands, units, push);
        RecordTheHostUnits(commands, _planned);
        vk.BetweenKernels(commands);
        RecordTheEdits(commands, _laidEdits);
        _claims.RecordLists(commands);
    }

    /// <summary>The host's backing filed after every plan, with what it cut of them, and the tally moved past it.</summary>
    void RecordTheBacking(CommandBuffer commands)
    {
        RecordTheHostUnits(commands, _backing);
        _vk.BetweenKernels(commands);
        RecordTheEdits(commands, _answeredEdits);
        RecordTheTally(commands, Step.Backing, _backing.RowCount, _backing.HoldCount);
    }

    void RecordTheTally(CommandBuffer commands, Step step, int hostRows, int hostHolds)
    {
        _tally.Dispatch(commands, 1, new TallyPush((uint)step, (uint)hostRows, (uint)hostHolds));
        _vk.BetweenKernels(commands);
    }

    /// <summary>A run of the host's table filed, recorded — a dispatch of one empty group where it is empty, so a recording never changes shape.</summary>
    void RecordTheHostUnits(CommandBuffer commands, in HostRange range) =>
        _host.Dispatch(commands, range.RowCount + range.HoldCount, range.Push);

    void RecordTheEdits(CommandBuffer commands, in HostRange range)
    {
        _edits.Dispatch(commands, range.RowCount + range.HoldCount, range.Push);
        _vk.BetweenKernels(commands);
    }

    /// <summary>
    /// <b>How many rows the host may lay itself in one rebuild</b>: every car's body swept down a pass and every walker's
    /// sidestep, a hand's or a tow's hold, a car on a pass planning past it, and a backing — each within what the town's
    /// own table gives one car or one walker.
    /// </summary>
    static int HostRowRoom(TownWorld world) =>
        Math.Min(world.Occupancy.Capacity, (world.Cars.Capacity * HostRowsPerCar) + (world.People.Capacity * Sidestepping.MostRunsOfAPass) + 1);

    /// <summary>A generous share of a car's own room in the table for the rows only the host lays: its pass's sweep and its plan past it.</summary>
    const int HostRowsPerCar = 256;

    static uint Groups(int count) => (uint)Math.Max(1, (count + 63) / 64);

    /// <summary>A kernel and the one set it is bound over for good, sixty-four threads a group.</summary>
    readonly struct Bound(ComputeKernel kernel, DescriptorSet set)
    {
        public ComputeKernel Kernel { get; } = kernel;

        public static unsafe Bound Of<TPush>(Vk vk, string shader, GpuBuffer[] buffers) where TPush : unmanaged
        {
            var kernel = new ComputeKernel(vk, shader, buffers.Length, typeof(TPush) == typeof(NoPush) ? 0u : (uint)sizeof(TPush));
            return new Bound(kernel, kernel.Bind(buffers));
        }

        /// <summary>Over <paramref name="threads"/> threads.</summary>
        public void Dispatch<TPush>(CommandBuffer commands, int threads, in TPush push) where TPush : unmanaged =>
            Kernel.Dispatch(commands, set, Groups(threads), push);

        /// <summary>Over as many groups as the claims' tally says at <paramref name="word"/>.</summary>
        public void DispatchIndirect<TPush>(CommandBuffer commands, GpuBuffer tally, int word, in TPush push) where TPush : unmanaged =>
            Kernel.DispatchIndirect(commands, set, tally, Tally.BytesTo(word), push);
    }

    /// <summary>A run of the host's table — its rows, then its holds — or of its edits.</summary>
    readonly record struct HostRange(int RowFirst, int RowCount, int HoldFirst, int HoldCount)
    {
        public RangePush Push => new((uint)RowFirst, (uint)RowCount, (uint)HoldFirst, (uint)HoldCount);
    }

    GpuBuffer Device<T>(int count) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None));

    GpuBuffer Readback<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferDstBit, HostAccess.Reads));

    GpuBuffer Staging<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferSrcBit, HostAccess.Writes));

    /// <summary>A device-local table laid once.</summary>
    GpuBuffer Laid<T>(ReadOnlySpan<T> rows) where T : unmanaged
    {
        var buffer = Device<T>(rows.Length);
        var copy = rows.ToArray();
        buffer.Upload<T>(copy.Length, into => copy.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    static unsafe ulong Bytes<T>(int count) where T : unmanaged => (ulong)(Math.Max(1, count) * sizeof(T));

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    /// <summary>Rows the host writes into a staged copy each rebuild and sends to the device's own, as one copy.</summary>
    sealed class Sent<T> where T : unmanaged
    {
        public Sent(DeviceRebuild owner, int room)
        {
            _owner = owner;
            Staging = owner.Staging<T>(room);
            Device = owner.Device<T>(room);
        }

        readonly DeviceRebuild _owner;

        public GpuBuffer Staging { get; }

        public GpuBuffer Device { get; }

        public Span<T> Rows => Staging.Span<T>();

        /// <summary>The first <paramref name="count"/> rows copied across, recorded; nothing where there are none.</summary>
        public void Send(CommandBuffer commands, int count)
        {
            if (count > 0) _owner._vk.Copy(commands, Staging, Device, Bytes<T>(count));
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _batch.Dispose();
        _scans.Dispose();
        foreach (var bound in (ReadOnlySpan<Bound>)[
                     _bodiesCount, _bodiesGather, _bodiesFile, _bodiesSort, _bodiesMark, _bodiesTally, _bodiesLay, _tally, _count, _file,
                     _host, _edits, _capture, _grant, _walkerAnswers, _signals])
        {
            bound.Kernel.Dispose();
        }

        foreach (var buffer in _owned) buffer.Dispose();
        _walkers.Dispose();
        _plans.Dispose();
        _bodies.Dispose();
        _claims.Dispose();
    }
}
