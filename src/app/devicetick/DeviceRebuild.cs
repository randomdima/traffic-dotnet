using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.App.DeviceTick.DeviceClaims;
using static TrafficSimulation.Runtime.GpuBuffer;

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
/// (<c>signal_holds.comp</c>); the claims laid where they stand, off what the host asked of each car
/// (<see cref="StandingRow"/>, <c>rebuild_standing.comp</c>); every car's claim (<see cref="DevicePlans"/>) and every
/// walker's walk — then <see cref="DeviceClaims"/> lists, answers and cuts them, and the backing every car asked for is
/// laid after (<see cref="BackingAskRow"/>, <c>rebuild_backing.comp</c>). No count crosses: every kernel over the rows or
/// the holds stops at, and is dispatched by, the claims' tally.
/// </para>
/// <para>
/// <b>What only the host lays is sent as host-laid units filed at their own place in that order</b>
/// (<see cref="HostUnit"/>): the passes of overtakes, switches, bays and sidesteps — and, of every car, whether it was
/// asked this laying and what the host left it granted where its claim is not the device's (<see cref="CarClaimRow"/>).
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
    /// <param name="Numbering">Where it is filed: <see cref="Numbering.Passes"/> or <see cref="Numbering.Plans"/>.</param>
    /// <param name="Unit">Among the plans, the unit it is filed as.</param>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct HostUnit(
        uint RowsAt, uint RowCount, uint HoldsAt, uint HoldCount, int HostSlotFirst, int HostHoldFirst, uint Numbering, uint Unit);

    /// <summary>Which numbering a host-laid unit is filed in (<c>rebuild_rows.glsl</c>).</summary>
    internal static class Numbering
    {
        public const uint Passes = 0u;
        public const uint Plans = 1u;
    }

    /// <summary>The ground one car asks to back up over (<see cref="BackingAsk"/>), as <c>rebuild_backing.comp</c> reads it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct BackingAskRow(
        int Car, uint Way, float FromM, float TailM, float CommittedFromM, float LineFromM, float AlongMps, uint Pad = 0u)
    {
        public static BackingAskRow Of(in BackingAsk ask) =>
            new(ask.Car, (uint)ask.Way, ask.FromM, ask.TailM, ask.CommittedFromM, ask.LineFromM, ask.AlongMps);
    }

    /// <summary>What the host tells the rebuild of one car (<c>rebuild_rows.glsl</c>'s <c>CarClaimRow</c>).</summary>
    /// <param name="AuthorityM">What a car whose claim is not the device's plan is left granted before its grant is read.</param>
    /// <param name="AskFirst">Where the run of <see cref="BodyAskRow"/>s a car on a pass looks for a body inside it begins.</param>
    [StructLayout(LayoutKind.Sequential)]
    internal record struct CarClaimRow(
        uint Flags, float AuthorityM, float HorizonM, float ClaimFromM, float CommittedToM, float GrantMarginM, uint GrantCutBy,
        int AskFirst = 0, uint AskCount = 0u)
    {
        public const uint Asked = 1u;
        public const uint PlansOnTheDevice = 2u;

        /// <summary>Its claim is laid where it stands (<see cref="StandingRow"/>), and its hold, where it has one, is that unit's.</summary>
        public const uint Stands = 4u;

        /// <summary>It plans past a pass begun, and is granted the whole of it where it plans nothing past it (<c>Following.Plan</c>).</summary>
        public const uint OnAPass = 8u;
    }

    /// <summary>
    /// <b>One car whose claim is laid where it stands</b> (<c>rebuild_standing.comp</c>'s <c>StandingRow</c>,
    /// <see cref="TownWorld.AsksWhereItStands"/>), in the town's order: how (<see cref="StandingClaim"/>), its run of the
    /// committed stretches or of the asks after a body, and where its claim was cleared to.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct StandingRow(
        uint Car, uint Kind, uint First, uint Count, float ClaimFromM, uint Pad0 = 0u, uint Pad1 = 0u, uint Pad2 = 0u);

    /// <summary>One stretch a car on a pass looks for a body in (<c>rebuild_rows.glsl</c>'s <c>BodyAskRow</c>, <see cref="BodyAsk"/>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct BodyAskRow(int Way, float FromM, float ToM, uint Flags, float InTheWayM, uint Pad = 0u)
    {
        public const uint OnFoot = 1u;
        public const uint FromTheBody = 2u;

        public static BodyAskRow Of(in BodyAsk ask) => new(
            ask.Way, ask.FromM, ask.ToM, (ask.OnFoot ? OnFoot : 0u) | (ask.FromTheBody ? FromTheBody : 0u), ask.InTheWayM);
    }

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
    readonly record struct TallyPush(uint Step, uint HostRows, uint WalkerPasses);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct PlansPush(uint StretchCount, uint CarCount, uint WalkerCount, uint MostPieces, float StandOffM);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct StandingPush(uint StandingCount, uint StretchCount, float StandOffM);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct RangePush(uint RowFirst, uint RowCount, uint HoldFirst, uint HoldCount);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct HostPush(RangePush Range, uint FirstCarUnit, uint CarCount);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct UnitsPush(uint Count, uint FirstUnit);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct BackingPush(uint AskCount, float BodyKeptOffM, uint HoldRoom);

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
    }

    enum Sum
    {
        Candidates,
        Bodies,
        PlannedRows,
        PlannedHolds,
        Ways,
        WalkerPasses,
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

    /// <summary>
    /// What the host asks of each car whose claim is laid where it stands, sent (<see cref="AskTheStanding"/>): a row a car;
    /// what a hand or a truck can no longer stop short of, a stretch a row as its main claim would lay it up to all it
    /// asks; and where a begun manoeuvre looks for a body.
    /// </summary>
    readonly Sent<StandingRow> _standing;

    readonly Sent<ClaimRow> _standingStretches;
    readonly Sent<BodyAskRow> _standingAsks;

    /// <summary>
    /// What <c>rebuild_standing.comp</c> laid where they stand, numbered from the unit's own first row and hold, for
    /// <c>rebuild_file.comp</c> to file: the rows, the holds, how many of each; the hold each car stands in, numbered so, or
    /// −1 — a car at a time, and in the host's memory a standing car at a time; and what it took of each light's stretch.
    /// </summary>
    readonly GpuBuffer _standingRows, _standingHolds, _standingCounts, _standingHoldOf, _standingSaid, _stretchCuts;

    /// <summary>
    /// The hold each car's claim laid, as the town numbers it (<see cref="TownWorld.HeldOnTheDevice"/>), or −1 — a car not
    /// asked left as its last laying left it (<c>rebuild_grant.comp</c>).
    /// </summary>
    readonly GpuBuffer _holdOf;

    /// <summary>Each way's lights' stretches, in their order (<c>rebuild_standing.comp</c>): where each way's run starts, and the run.</summary>
    readonly GpuBuffer _wayLightAt, _wayLights;

    readonly Bound _standingKernel;

    /// <summary>The standing cars this rebuild asked, the stretches and the asks they sent, and the rows they may lay at most.</summary>
    int _standingCount, _standingStretchCount, _standingAskCount, _standingMostRows;

    /// <summary>Which of the standing cars each car is this rebuild, or −1.</summary>
    readonly int[] _standingOf;

    /// <summary>What the host tells the rebuild of each car, kept on the device and sent only where it changed.</summary>
    readonly ResidentRows<CarClaimRow> _carClaims;

    ulong _carClaimsSent;

    /// <summary>Every car's grant, cutter and told bits as the town had them, sent once before the first rebuild.</summary>
    readonly GpuBuffer _primedGrants, _primedCutters, _primedTold;

    /// <summary>
    /// The runs of stretches each car on a pass looks for a body inside it in, sent (<see cref="BodyAskRow"/>), and what
    /// each found, a car a row (<c>rebuild_asks.comp</c>).
    /// </summary>
    readonly Sent<BodyAskRow> _asks;

    readonly GpuBuffer _askAnswers;

    /// <summary>How many asks this rebuild sends, and room for one car on a pass's asks as the town writes them.</summary>
    int _askCount;

    readonly BodyAsk[] _scratchAsks;

    /// <summary>
    /// The ground each car backing up asks for this laying, in the town's order, sent (<see cref="BackingAsk"/>); and how far
    /// back down its way each was laid (<c>rebuild_backing.comp</c>), an ask a float, read back.
    /// </summary>
    readonly Sent<BackingAskRow> _backingAsks;

    readonly BackingAsk[] _backingAsked;
    readonly GpuBuffer _reached, _reachedRead;
    int _backingCount;

    readonly ScanKernels _scans;
    readonly PrefixScan _scanCandidates, _scanWays, _scanBodies, _scanRows, _scanHolds;
    readonly Bound _bodiesCount, _bodiesGather, _bodiesFile, _bodiesSort, _bodiesMark, _bodiesTally, _bodiesLay;
    readonly Bound _tally, _count, _file, _host, _capture, _backing, _grant, _walkerAnswers, _signals, _asked;
    readonly ComputeBatch _batch;

    /// <summary>A rebuild in the town's sends, submitted ahead of the rest of it (<see cref="WaitForTheSends"/>).</summary>
    readonly ComputeBatch _sendBatch;

    readonly GpuTimer _timer;

    /// <summary>
    /// A rebuild held to the town's beside it (<see cref="Watch"/>) — or, given the claims and the car tick of a device
    /// that lays the claims in the town's place, the rebuild that lays them (<see cref="InTheTown"/>): numbered into those
    /// claims, the plans reading the car tick's own lines and every grant written into its own tables.
    /// </summary>
    /// <param name="walkers">
    /// The walker tick of a device that ticks the walkers too, whose table a rebuild in the town reads them in from and keeps
    /// them as its own (<see cref="DeviceWalkers.Keeps"/>) — or none.
    /// </param>
    /// <param name="rooms">
    /// In the town, the rooms a car's plan and a walk begin with (<see cref="RoomsToStart"/> where none), grown to fit what is
    /// laid; the rows' are the claims'. Held to the town's, every room is what either could ever lay.
    /// </param>
    public DeviceRebuild(
        Vk vk, SimConfig config, TownWorld world, DeviceClaims? claims = null, DeviceCarTick? cars = null, DeviceWalkerTick? walkers = null,
        Rooms? rooms = null)
    {
        if (walkers != null && cars == null) throw new ArgumentException("Only a rebuild laid in the town keeps the walkers.", nameof(walkers));

        _vk = vk;
        _config = config;
        var occupancy = world.Occupancy;
        _cars = world.Cars.Capacity;
        _walkersRoom = world.People.Capacity;
        _ways = occupancy.WayCount;
        _holdRoom = occupancy.HoldCapacity;
        var lights = world.SignalHolds.Table;
        _stretches = lights.Way.Length;
        _units = _stretches + 1 + _cars + _walkersRoom;
        _carTick = cars;
        _ownsTheClaims = claims is null;

        _claims = claims ?? new DeviceClaims(vk, _ways, occupancy.Capacity, _holdRoom, config.Road.MostSettlingPasses, _cars, sentFromTheHost: false);
        var tables = _claims.OnTheDevice;
        var start = cars != null ? rooms ?? RoomsToStart(world) : (Rooms?)null;
        _bodies = new DeviceBodies(vk, config, world, cars?.Lines);
        _plans = new DevicePlans(vk, config, world, cars?.Lines, start?.Pieces);
        _walkers = new DeviceWalkers(vk, config, world, tables, resident: cars != null, keptFrom: walkers?.Table, proposedRoom: start?.Proposed);
        _walkerTick = walkers;
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

        (_grants, _cutters, _told) = cars?.GrantTables ?? (Device<DeviceCarTick.GrantRow>(_cars), Device<ClaimRow>(_cars), Device<uint>(_cars));
        _grantsOut = Readback<DeviceCarTick.GrantRow>(_cars);
        _cuttersOut = Readback<ClaimRow>(_cars);
        _toldOut = Readback<uint>(_cars);

        var hostRoom = HostRowRoom(world);
        _hostRows = new Sent<ClaimRow>(this, HostRowsToStart, hostRoom);
        _hostHolds = new Sent<HoldRow>(this, _holdRoom);
        _hostUnits = new Sent<HostUnit>(this, _cars + 3);
        _hostRowUnit = new Sent<uint>(this, HostRowsToStart, hostRoom);
        _hostHoldUnit = new Sent<uint>(this, _holdRoom);
        _standing = new Sent<StandingRow>(this, StandingToStart, _cars);
        _standingStretches = new Sent<ClaimRow>(this, StandingToStart, _cars * RibbonAtlas.MostWaysUnderABody);
        _standingAsks = new Sent<BodyAskRow>(this, StandingToStart, _cars * BayManoeuvring.MostAsksOfAManoeuvre);
        _standingRows = Device<ClaimRow>(StandingToStart);
        _standingHolds = Device<HoldRow>(StandingToStart);
        _standingCounts = Device<uint>(2);
        _standingHoldOf = Device<int>(_cars);
        _holdOf = Device<int>(_cars);
        _standingSaid = Own(_vk.CreateBuffer(Bytes<int>(StandingToStart), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _stretchCuts = Device<UInt4>(Math.Max(1, _stretches));
        _standingOf = new int[_cars];
        _carClaims = new ResidentRows<CarClaimRow>(vk, _cars, 1, hostOnly: true);
        _primedGrants = Staging<DeviceCarTick.GrantRow>(_cars);
        _primedCutters = Staging<ClaimRow>(_cars);
        _primedTold = Staging<uint>(_cars);
        _asks = new Sent<BodyAskRow>(this, HostRowsToStart, AskRoom(world));
        _scratchAsks = new BodyAsk[Overtaking.MostAsksOfAPass(world.Atlas.Marks)];
        _askAnswers = Device<UInt4>(_cars);
        _backingAsks = new Sent<BackingAskRow>(this, Math.Min(_cars, HostRowsToStart), _cars);
        _backingAsked = new BackingAsk[_cars];
        _reached = Device<float>(_cars);
        _reachedRead = Readback<float>(_cars);

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

        _asked = Bound.Of<UnitsPush>(
            vk, "rebuild_asks.comp", [_carClaims.Device, _asks.Device, tables.Rows, tables.BodyRuns, tables.Order, _askAnswers]);

        var (planCars, planFigures, planPieces, planResults) = _plans.OnTheDevice;
        GpuBuffer[] plans =
        [
            _held, _stretchPieces, _stretchCuts, _carClaims.Device, planCars, planFigures, planResults, planPieces, walkerCounts,
            walkerHolds, walkerProposed, _unitRows, _unitHolds, tables.Rows, tables.Holds, tables.Answers, tables.Shown, tables.Tally,
            _askAnswers, _standingCounts, _standingRows, _standingHolds,
        ];
        _count = Bound.Of<PlansPush>(vk, "rebuild_count.comp", plans);
        _file = Bound.Of<PlansPush>(vk, "rebuild_file.comp", plans);
        _host = Bound.Of<HostPush>(
            vk, "rebuild_host.comp",
            [
                _hostRows.Device, _hostHolds.Device, _hostUnits.Device, _hostRowUnit.Device, _hostHoldUnit.Device, _unitRows, _unitHolds,
                tables.Rows, tables.Holds, tables.Answers, tables.Shown, tables.Tally,
            ]);
        _capture = Bound.Of<NoPush>(vk, "rebuild_cutters.comp", [tables.Rows, tables.Answers, _holdCutters, tables.Tally]);
        _grant = Bound.Of<UnitsPush>(
            vk, "rebuild_grant.comp",
            [
                _carClaims.Device, planResults, _unitHolds, tables.Answers, _holdCutters, _grants, _cutters, _told, _askAnswers, tables.Tally,
                _standingHoldOf, _holdOf,
            ]);
        _walkerAnswers = Bound.Of<UnitsPush>(vk, "rebuild_walkers.comp", [walkerCounts, _unitHolds, tables.Answers, _walkers.AnswersRead]);
        var (markAt, markSections) = _plans.Marks;
        _backing = Bound.Of<BackingPush>(
            vk, "rebuild_backing.comp",
            [
                _backingAsks.Device, tables.Rows, tables.Holds, tables.Answers, tables.Shown, _holdCutters, tables.BodyRuns,
                tables.PlannedRuns, tables.Order, markAt, markSections, _reached, tables.Tally,
            ]);

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

        (_wayLightAt, _wayLights) = LayTheWaysLights(lights.Way);
        _standingKernel = Bound.Of<StandingPush>(
            vk, "rebuild_standing.comp",
            [
                _standing.Device, _standingStretches.Device, _standingAsks.Device, tables.Rows, tables.BodyRuns, tables.Order, _held,
                _stretchPieces, _wayLightAt, _wayLights, markAt, markSections, _standingRows, _standingHolds, _standingCounts,
                _standingHoldOf, _standingSaid, _stretchCuts,
            ]);

        _batch = new ComputeBatch(vk);
        _sendBatch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
        ShadowOf();
        if (cars != null) InTheTownOf(world);
    }

    /// <summary>How many rebuilds have been recorded and submitted.</summary>
    public long Rebuilds { get; private set; }

    /// <summary>
    /// What a car's tick binds in place of its own grant tables (<c>car_tick.comp</c>'s bindings 22 and 21, and the told
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
        + Bytes<HoldRow>(_hostHoldCount) + Bytes<uint>(_hostHoldCount) + Bytes<HostUnit>(_hostUnitCount) + StandingSentBytes
        + _carClaimsSent,
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
        RecordTheStandingSent(commands);
        _asks.Send(commands, _askCount);
        _backingAsks.Send(commands, _backingCount);
        _carClaimsSent = _carClaims.MarkedBytes;
        _carClaims.Send(commands);
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
    /// host's passes filed after them — and the kept walkers' after those — and every way's lists laid from them for what
    /// reads the bodies next.
    /// </summary>
    void RecordTheBodies(CommandBuffer commands)
    {
        var vk = _vk;
        var tally = _claims.OnTheDevice.Tally;
        var keeps = _walkers.Keeps;
        vk.BeforeTransfer(commands);
        _claims.RecordTheRooms(commands, _plans.MostPieces, _walkers.ProposedRoom, beginning: true, _toldNoRoom);
        vk.AfterTransfer(commands);
        if (keeps) _walkers.RecordTheKeptIn(commands);
        if (_carTick is null || keeps) _walkers.Record(commands, DeviceWalkers.Piece.Station);

        _walkers.Record(commands, DeviceWalkers.Piece.Body);
        if (keeps) RecordTheWalkersPassesCounted(commands);
        _bodies.Record(commands);

        var units = _walkerCount + _carCount;
        var push = new BodiesPush((uint)_walkerCount, (uint)_carCount, (uint)_ways, (uint)_walkers.CoverRoom, (uint)DeviceBodies.Room);
        _bodiesCount.Dispatch(commands, units, push);
        vk.BetweenKernels(commands);
        _scanCandidates.Record(commands, units);
        RecordTheTally(commands, Step.Candidates);
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
        RecordTheTally(commands, Step.Bodies, _passes.RowCount);
        _bodiesLay.Dispatch(commands, units, push);
        RecordTheHostUnits(commands, _passes);
        if (keeps) RecordTheWalkersPassesFiled(commands);
        vk.BetweenKernels(commands);
        _claims.RecordLists(commands);
        _asked.Dispatch(commands, _carCount, new UnitsPush((uint)_carCount, 0u));
        vk.BetweenKernels(commands);
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
        var standOffM = _config.Driving.StandOffM;
        _signals.Dispatch(
            commands, _stretches, new SignalPush((uint)_stretches, _laneCount, _crossingCount, signals.CycleS, signals.AmberTailS, _timeS));
        vk.BetweenKernels(commands);
        _standingKernel.Dispatch(commands, 1, new StandingPush((uint)_standingCount, (uint)_stretches, standOffM));
        _plans.Record(commands);
        _walkers.Record(commands, DeviceWalkers.Piece.Plan);
        vk.BetweenKernels(commands);

        var units = _stretches + 1 + _carCount + _walkerCount;
        var push = new PlansPush((uint)_stretches, (uint)_carCount, (uint)_walkerCount, (uint)_plans.MostPieces, standOffM);
        _count.Dispatch(commands, units, push);
        vk.BetweenKernels(commands);
        _scanRows.Record(commands, units);
        _scanHolds.Record(commands, units);
        RecordTheTally(commands, Step.Plans);
        _file.Dispatch(commands, units, push);
        vk.BetweenKernels(commands);
        _claims.RecordLists(commands);
    }

    /// <summary>
    /// <b>The ground every car backing up asked for laid after the answers and the cut</b> (<c>rebuild_backing.comp</c>),
    /// numbered after everything else and the tally moved past it, and every way's lists laid again with it — nothing where
    /// no car asked.
    /// </summary>
    void RecordTheBacking(CommandBuffer commands)
    {
        if (_backingCount == 0) return;

        _backing.Dispatch(commands, 1, new BackingPush((uint)_backingCount, _config.Driving.PassSpareM, (uint)_holdRoom));
        _vk.BetweenKernels(commands);
        _claims.RecordLists(commands);
    }

    /// <summary>
    /// <b>Every car's ask to back up taken off the town</b>, in its order, and sent with the rebuild — or, held to the town's
    /// own laying, read and left there (<see cref="TownWorld.AsksToBackUp"/>).
    /// </summary>
    void AskTheBacking(TownWorld town, bool takes)
    {
        _backingCount = 0;
        foreach (var car in town.CarsAskedThisLaying)
        {
            var asks = takes ? town.TakeTheBackingAsk(car, out var ask) : town.AsksToBackUp(car, out ask);
            if (!asks) continue;

            _backingAsks.MakeRoomFor(_backingCount + 1);
            _backingAsks.Rows[_backingCount] = BackingAskRow.Of(ask);
            _backingAsked[_backingCount++] = ask;
        }
    }

    void RecordTheTally(CommandBuffer commands, Step step, int hostRows = 0)
    {
        _tally.Dispatch(commands, 1, new TallyPush((uint)step, (uint)hostRows, _walkers.Keeps ? 1u : 0u));
        _vk.BetweenKernels(commands);
    }

    /// <summary>A run of the host's table filed, recorded — a dispatch of one empty group where it is empty, so a recording never changes shape.</summary>
    void RecordTheHostUnits(CommandBuffer commands, in HostRange range) =>
        _host.Dispatch(commands, range.RowCount + range.HoldCount, new HostPush(range.Push, (uint)(_stretches + 1), (uint)_carCount));

    /// <summary>
    /// <b>How many rows the host may lay itself in one rebuild</b>: every car's body swept down a pass and every walker's
    /// sidestep — each within what the town's own table gives one car or one walker.
    /// </summary>
    static int HostRowRoom(TownWorld world) =>
        Math.Min(world.Occupancy.Capacity, (world.Cars.Capacity * HostRowsPerCar) + (world.People.Capacity * Sidestepping.MostRunsOfAPass) + 1);

    /// <summary>
    /// The standing cars, their asks and the rows they lay begin with room for, each grown to fit: a few hands, tows and
    /// manoeuvres a rebuild.
    /// </summary>
    const int StandingToStart = 64;

    /// <summary>
    /// <b>Every car of this laying whose claim is laid where it stands, asked of the town in its order</b>
    /// (<see cref="TownWorld.AsksWhereItStands"/>) to be sent: a row a car, and its committed stretches — each as its main
    /// claim would lay it up to all it asks, from where its holder stands — or where it looks for a body; and room made for
    /// what <c>rebuild_standing.comp</c> may lay of them: each stretch's claim and a claim of every mark over its way.
    /// </summary>
    [System.Runtime.CompilerServices.SkipLocalsInit]
    void AskTheStanding(TownWorld town)
    {
        Span<CommittedStretch> stretches = stackalloc CommittedStretch[RibbonAtlas.MostWaysUnderABody];
        Span<BodyAsk> asks = stackalloc BodyAsk[BayManoeuvring.MostAsksOfAManoeuvre];
        var marks = town.Occupancy.Marks;
        var claimFromM = town.Cars.ClaimFromM;
        (_standingCount, _standingStretchCount, _standingAskCount, _standingMostRows) = (0, 0, 0, 0);
        foreach (var car in town.CarsAskedThisLaying)
        {
            _standingOf[car] = -1;
            if (town.PlansItsLine(car)) continue;

            var kind = town.AsksWhereItStands(car, stretches, asks, out var count);
            if (kind == StandingClaim.None) continue;

            var first = kind == StandingClaim.Manoeuvre ? _standingAskCount : _standingStretchCount;
            if (kind == StandingClaim.Committed)
            {
                Fit(_standingStretches, _standingStretchCount + count);
                foreach (ref readonly var stretch in stretches[..count])
                {
                    _standingStretches.Rows[_standingStretchCount++] = ClaimRow.Of(
                        stretch.Ask.Laid(stretch.ToM), stretch.Way, gone: false, standsToM: stretch.Ask.FromM);
                    _standingMostRows += 1 + marks.Of(stretch.Way).Length;
                }
            }
            else if (kind == StandingClaim.Manoeuvre)
            {
                Fit(_standingAsks, _standingAskCount + count);
                foreach (ref readonly var ask in asks[..count]) _standingAsks.Rows[_standingAskCount++] = BodyAskRow.Of(ask);
            }

            Fit(_standing, _standingCount + 1);
            _standingOf[car] = _standingCount;
            _standing.Rows[_standingCount++] = new StandingRow((uint)car, (uint)kind, (uint)first, (uint)count, claimFromM[car]);
        }

        FitTheDevice(_standingRows, Bytes<ClaimRow>(_standingMostRows));
        FitTheDevice(_standingHolds, Bytes<HoldRow>(_standingCount));
        FitTheDevice(_standingSaid, Bytes<int>(_standingCount));

        static void Fit<T>(Sent<T> sent, int count) where T : unmanaged
        {
            if (!sent.MakeRoomFor(count)) throw new InvalidOperationException($"The cars standing asked past the {sent.Room} rows the rebuild has room for.");
        }
    }

    /// <summary>A table only the device writes grown to hold <paramref name="bytes"/>, the device waited for first — nothing where it does.</summary>
    void FitTheDevice(GpuBuffer buffer, ulong bytes)
    {
        if (bytes <= buffer.SizeBytes) return;

        _vk.WaitIdle();
        buffer.Grow(Math.Max(bytes, 2 * buffer.SizeBytes));
    }

    /// <summary>What the standing cars asked, copied across, recorded.</summary>
    void RecordTheStandingSent(CommandBuffer commands)
    {
        _standing.Send(commands, _standingCount);
        _standingStretches.Send(commands, _standingStretchCount);
        _standingAsks.Send(commands, _standingAskCount);
    }

    ulong StandingSentBytes =>
        Sent<StandingRow>.BytesOf(_standingCount) + Sent<ClaimRow>.BytesOf(_standingStretchCount) + Sent<BodyAskRow>.BytesOf(_standingAskCount);

    /// <summary>
    /// How many holds the last rebuild laid where they stand, read off the standing cars: one each of a hand's or a truck's,
    /// and of a manoeuvre's where it laid one.
    /// </summary>
    public int StandingHeld
    {
        get
        {
            var said = _standingSaid.Span<int>();
            var held = 0;
            for (var at = 0; at < _standingCount; at++)
            {
                if (said[at] >= 0 || _standing.Rows[at].Kind == (uint)StandingClaim.Committed) held++;
            }

            return held;
        }
    }

    /// <summary>
    /// Each way's lights' stretches, in their order, laid once: where each way's run begins in the second table — the
    /// way after it, where it ends — and the stretches.
    /// </summary>
    (GpuBuffer At, GpuBuffer Stretches) LayTheWaysLights(ReadOnlySpan<int> stretchWay)
    {
        var at = new uint[_ways + 1];
        foreach (var way in stretchWay) at[way + 1]++;
        for (var way = 0; way < _ways; way++) at[way + 1] += at[way];

        var filed = at.AsSpan(0, _ways).ToArray();
        var stretches = new uint[Math.Max(1, stretchWay.Length)];
        for (var stretch = 0; stretch < stretchWay.Length; stretch++) stretches[filed[stretchWay[stretch]]++] = (uint)stretch;

        return (Laid<uint>(at), Laid<uint>(stretches));
    }

    /// <summary>A generous share of a car's own room in the table for the rows only the host lays: its pass's sweep and its plan past it.</summary>
    const int HostRowsPerCar = 256;

    /// <summary>
    /// The rows the host's own laying, its cuts and its cars' asks begin with room for, each grown to fit
    /// (<see cref="Sent{T}.MakeRoomFor"/>): what a town lays itself in a rebuild is a few hands, tows and passes (OdesaOsm: at
    /// most 140 rows).
    /// </summary>
    const int HostRowsToStart = 1024;

    /// <summary>
    /// <b>About what one agent lays into a rebuild in the town</b>, its body's rows and its plan's together — what the rows'
    /// room is begun at before the town has laid any (OdesaOsm: about four).
    /// </summary>
    const int RowsAnAgentLays = 4;

    /// <summary>About the most one car's plan lays, which its room is begun at (OdesaOsm: 44 at most of 40 000).</summary>
    const int PiecesACarLays = 32;

    /// <summary>About the most one walker's walk lays, which its room is begun at (OdesaOsm: 35 at most of 40 000).</summary>
    const int RowsAWalkLays = 32;

    /// <summary>
    /// <b>How many times what a rebuild laid its room is kept at</b>: a room is grown before the next rebuild where the last
    /// one laid more than this share of it — to the power of two past this many times what it laid — so one rebuild laying
    /// twice the last, as a car going on a call does (<see cref="Core.Config.AmbulanceFigures.CallReachShare"/>), still fits.
    /// </summary>
    const int Headroom = 2;

    /// <summary>The room kept for <paramref name="laid"/>: <see cref="Headroom"/> times it, to the power of two past that.</summary>
    static int RoomFor(int laid) =>
        (int)Math.Min(int.MaxValue, BitOperations.RoundUpToPowerOf2((uint)Math.Max(1L, Math.Min(int.MaxValue, (long)laid * Headroom))));

    /// <summary>The rooms a rebuild laid in the town begins with: its rows, the pieces of a car's plan and the rows of a walk.</summary>
    internal readonly record struct Rooms(int Rows, int Pieces, int Proposed);

    /// <summary>The rooms as they stand now (<see cref="Rooms"/>), grown or not.</summary>
    public Rooms RoomsNow => new(_claims.RowRoom, _plans.MostPieces, _walkers.ProposedRoom);

    /// <summary>
    /// <b>The rooms a rebuild laid in the town begins with</b>: rows for <see cref="RowsAnAgentLays"/> each agent's, or what
    /// the town last laid where it laid more — never more than its table holds — and room for what a car's plan and a walk
    /// lay at most, each kept at <see cref="Headroom"/>.
    /// </summary>
    public static Rooms RoomsToStart(TownWorld world)
    {
        var claims = world.Occupancy;
        var agents = (long)world.Cars.Capacity + world.People.Capacity;
        var rows = Math.Min(claims.Capacity, RoomFor((int)Math.Min(int.MaxValue, Math.Max(agents * RowsAnAgentLays, claims.SlotCount))));
        return new Rooms(rows, RoomFor(PiecesACarLays), RoomFor(RowsAWalkLays));
    }

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

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    /// <summary>
    /// Rows the host writes into a staged copy each rebuild and sends to the device's own, as one copy — with room for as
    /// many as it was made with, or grown to fit (<see cref="MakeRoomFor"/>) where it was made with less than it could hold.
    /// </summary>
    sealed class Sent<T> where T : unmanaged
    {
        /// <param name="room">The rows there is room for to begin with.</param>
        /// <param name="most">The most rows there could ever be, past which a laying is a defect.</param>
        public Sent(DeviceRebuild owner, int room, int most)
        {
            _owner = owner;
            _most = most;
            Staging = owner.Staging<T>(Math.Min(room, most));
            Device = owner.Device<T>(Math.Min(room, most));
        }

        public Sent(DeviceRebuild owner, int room) : this(owner, room, room)
        {
        }

        readonly DeviceRebuild _owner;
        readonly int _most;

        public GpuBuffer Staging { get; }

        public GpuBuffer Device { get; }

        public Span<T> Rows => Staging.Span<T>();

        /// <summary>How many rows there is room for.</summary>
        public unsafe int Room => (int)(Staging.SizeBytes / (ulong)sizeof(T));

        /// <summary>
        /// <b>Room made for <paramref name="count"/> rows</b>, every row written so far kept — the device waited for first, as a
        /// staged copy and the table it is sent to grow together (<see cref="GpuBuffer.Grow"/>). False where that is past
        /// the most there could ever be.
        /// </summary>
        public bool MakeRoomFor(int count)
        {
            if (count <= Room) return true;
            if (count > _most) return false;

            var room = Math.Min(_most, RoomFor(count));
            _owner._vk.WaitIdle();
            Staging.Grow(BytesOf(room), keep: true);
            Device.Grow(BytesOf(room));
            return true;
        }

        /// <summary>The first <paramref name="count"/> rows copied across, recorded; nothing where there are none.</summary>
        public void Send(CommandBuffer commands, int count) => Send(commands, 0, count);

        /// <summary><paramref name="count"/> rows from <paramref name="first"/> copied across, recorded; nothing where there are none.</summary>
        public unsafe void Send(CommandBuffer commands, int first, int count)
        {
            if (count <= 0) return;

            var at = (ulong)first * (ulong)sizeof(T);
            _owner._vk.Copy(commands, Staging, Device, [new BufferCopy(at, at, BytesOf(count))]);
        }

        /// <summary>How many bytes <paramref name="count"/> rows are.</summary>
        public static unsafe ulong BytesOf(int count) => (ulong)count * (ulong)sizeof(T);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _batch.Dispose();
        _sendBatch.Dispose();
        _scans.Dispose();
        foreach (var bound in (ReadOnlySpan<Bound>)[
                     _bodiesCount, _bodiesGather, _bodiesFile, _bodiesSort, _bodiesMark, _bodiesTally, _bodiesLay, _tally, _count, _file,
                     _host, _capture, _backing, _grant, _walkerAnswers, _signals, _asked, _standingKernel])
        {
            bound.Kernel.Dispose();
        }

        _holdsOutKernel.Kernel?.Dispose();
        _agentsOutKernel.Kernel?.Dispose();
        _walkersReaders.Kernel?.Dispose();
        _walkerPasses.Kernel?.Dispose();
        _carsGather.Kernel?.Dispose();
        _waysOut.Kernel?.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
        _carClaims.Dispose();
        _carTerms?.Dispose();
        _walkers.Dispose();
        _plans.Dispose();
        _bodies.Dispose();
        if (_ownsTheClaims) _claims.Dispose();
    }
}
