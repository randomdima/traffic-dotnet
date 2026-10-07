using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The walkers' side of a claims rebuild on the device</b> (<c>world/town/kernels/walkers.glsl</c>), beside the
/// host's: the walkers as the town has them at one stage of a rebuild sent across, that stage's kernel run over them one
/// walker a thread — stationed on their walks, their bodies laid, their walks planned and claimed, or their grants read
/// and their aims set — and what it came to read back to be held to the town's.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pavement, its lines, the marks and the atlas are laid once.</b> Each stage has its own walkers on the device,
/// sent as the town has them at that stage (<see cref="Lay"/>), so every stage of one rebuild can be recorded into one
/// submit (<see cref="DeviceRebuild"/>) or each run on its own (<see cref="Run"/>).
/// </para>
/// <para>
/// <b>Run on its own, a stage that asks the claims</b> — whose body is where, whether a pass keeps its ground — lays
/// every way's body list from the town's own rows first, as the claims' lists are laid (<c>claims_lists.glsl</c>), and
/// that is timed apart from the walkers, and a walker's hold is numbered by the walker. <b>Chained</b>, it reads the
/// device's own claims (<see cref="DeviceClaims.OnTheDevice"/>), as numbered and listed by the rebuild.
/// </para>
/// <para>
/// <b>Resident, for a rebuild run in the town's place</b>, only the bodies and the walks are run: the walkers are sent
/// once stationed (<see cref="LayTheStationed"/>), the walks read them as sent but for the rows the town changed since
/// (<see cref="LayWhatChanged"/>), and each walker's route, straight and pass's ground are kept on the device and sent
/// only where they changed.
/// </para>
/// </remarks>
internal sealed class DeviceWalkers : IDisposable
{
    /// <summary>The stages a rebuild's walkers are run in, and the kernel of each.</summary>
    internal enum Piece
    {
        /// <summary>Every walker stationed on its walk (<c>walkers_station.comp</c>).</summary>
        Station,

        /// <summary>Every walker's body laid, and its pass's ground (<c>walkers_body.comp</c>).</summary>
        Body,

        /// <summary>Every walker's walk planned and claimed (<c>walkers_plan.comp</c>).</summary>
        Plan,

        /// <summary>Every walker's grant read, its pass considered and its aim set (<c>walkers_read.comp</c>).</summary>
        Read,
    }

    /// <summary>One walker as the kernels read and write it (<c>walker_rows.glsl</c>'s <c>WalkerRow</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WalkerRow
    {
        public Vector2 PositionM;
        public Vector2 VelocityMps;
        public Vector2 DeclaredMps;
        public Vector2 DestinationM;
        public Vector2 GoalM;
        public Vector2 BackToM;
        public Vector2 StraightFromM;
        public Vector2 StraightToM;
        public float HeadingRad;
        public float RadiusM;
        public float OnWayM;
        public float OffWayM;
        public float RouteToM;
        public float GrantM;
        public float PassFromM;
        public float PassClearM;
        public float PassAsideM;
        public int PassSlot;
        public int RouteCount;
        public int RouteTaken;
        public int OnWay;
        public int OnCrossing;
        public int Car;
        public int DutyCar;
        public int HeldAs;
        public uint StraightRuns;
        public uint PassRuns;
        public uint Flags;
        public uint Action;
        public uint Stage;
        public int Hold;
        public float AlongMps;

        public const uint Walking = 1u;
        public const uint Wounded = 2u;
        public const uint Inside = 4u;
        public const uint RunsOut = 8u;
        public const uint PassBegun = 16u;
        public const uint HeldAsDriving = 32u;
        public const uint OnAStraight = 64u;

        public readonly bool Is(uint flag) => (Flags & flag) != 0;
    }

    /// <summary>One way of the pavement as the kernels read it (<c>walking_rows.glsl</c>'s <c>WalkWayRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkWayRow(
        uint ArcFirst, uint ArcEnd, float LengthM, float WidthM, float JoinFromM, float JoinToM, int Zebra, uint Pad = 0u);

    /// <summary>One mark as the kernels read it (<c>claims_marks.glsl</c>'s <c>WayMark</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct MarkRow(int OnWay, float FromM, float ToM, float MineFromM, float MineToM);

    /// <summary>The push block every walkers' kernel reads (<c>walkers_buffers.glsl</c>): the walkers' figures, then the atlas's.</summary>
    [StructLayout(LayoutKind.Sequential)]
    struct Figures
    {
        public uint WalkerCount;
        public uint RouteRoom;
        public uint StraightRoom;
        public uint PassRoom;
        public uint CoverRoom;
        public uint ProposedRoom;
        public uint LaneCount;
        public int FirstFootwayWay;
        public int FirstMitreWay;
        public uint GroundChannel;
        public float StepM;
        public float StandstillGapM;
        public float WalkAheadM;
        public float OffLaneM;
        public float TouchM;
        public int CellsFromX;
        public int CellsFromY;
        public int CellsWidth;
        public int CellsHeight;
        public float PerCellM;
        public float CellM;
        public int Shift;
        public int Mask;
        public int StartShift;
        public int RowStartsShift;
        public int StartsPerCell;
        public float MetresPerOutside;
        public float AlongPerMetre;
        public uint HasChannels;
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ListsPush(uint WayCount, uint Pad);

    /// <summary><c>walker_rows.glsl</c>'s own bound on the ways under one walk (<see cref="WalkingGround.MostWaysAlongAWalk"/>).</summary>
    const int MostWaysInTheKernel = 5;

    /// <summary>A way's room in the kernels' atlas reads (<c>atlas.glsl</c>'s <c>AtlasMostWaysUnderABody</c>).</summary>
    const int MostWaysUnderABodyInTheKernel = 48;

    /// <summary>The stamps a stage run on its own is timed between: the walkers' kernel, and the lists laid before it where it reads the claims.</summary>
    enum Stamp
    {
        Began,
        Listed,
        Ran,
    }

    /// <summary>What one stage is sent and runs over: the walkers, their routes, their straights' and passes' ground.</summary>
    sealed class Sent(GpuBuffer walkers, GpuBuffer routes, GpuBuffer straights, GpuBuffer passes)
    {
        public readonly GpuBuffer Walkers = walkers, Routes = routes, Straights = straights, Passes = passes;
    }

    static readonly int Pieces = Enum.GetValues<Piece>().Length;

    readonly Vk _vk;
    readonly int _walkers;
    readonly int _ways;
    readonly int _coverRoom;
    readonly int _proposedRoom;
    readonly int[] _dutyCar;
    readonly List<GpuBuffer> _owned = [];
    readonly DeviceAtlas _atlas;
    Figures _figures;

    readonly Sent[] _in, _on;
    readonly GpuBuffer _answers, _covers, _wayRows, _proposed, _holds, _outCounts;

    /// <summary>Resident: each walker's route, straight and pass's ground as the device keeps them.</summary>
    readonly ResidentRows<int>? _routes;

    readonly ResidentRows<SweptRun>? _straights, _passes;

    /// <summary>
    /// Resident: every walker as stationed, kept on the device and sent only where it changed — written by no kernel, so
    /// each rebuild's bodies and walks are copied fresh from it.
    /// </summary>
    readonly ResidentRows<WalkerRow>? _stationed;

    /// <summary>Resident: the rows the town changed between stationing the walkers and their walks, staged.</summary>
    readonly GpuBuffer? _changedIn;

    /// <summary>Resident: where each changed row lands in the walks' own copy.</summary>
    readonly BufferCopy[] _changes = [];

    int _changeCount, _changedRows;

    /// <summary>Resident: whether a walker's straight is sent whatever was laid last, the device having committed one of its own.</summary>
    readonly bool[] _straightAgain = [];

    readonly GpuBuffer _walkersOut, _straightsOut, _coversOut, _waysOut, _proposedOut, _holdsOut, _countsOut;

    /// <summary>The claims a stage run on its own lays from the town's rows; none where the walkers are chained.</summary>
    readonly GpuBuffer? _rowsIn, _answersIn, _rows, _counts, _starts, _cursor, _order, _bodyRuns, _plannedRuns, _blockSums, _totals, _tally;

    readonly List<(GpuBuffer From, GpuBuffer To)> _once = [];
    readonly ComputeKernel? _count, _file, _sort;
    readonly DescriptorSet _countSet, _fileSet, _sortSet;
    readonly ScanKernels? _scans;
    readonly PrefixScan? _scan;
    readonly ComputeKernel[] _kernels;
    readonly DescriptorSet[] _sets;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    int _walkerCount;
    int _rowCount;

    /// <param name="claims">
    /// The device's own claims a chained rebuild numbers and lists (<see cref="DeviceClaims.OnTheDevice"/>), which the stages
    /// that ask the claims read — or none, for stages run on their own over the town's rows.
    /// </param>
    /// <param name="resident">Whether the walkers are kept for a rebuild run in the town's place, chained.</param>
    public DeviceWalkers(Vk vk, SimConfig config, TownWorld world, DeviceClaims.Tables? claims = null, bool resident = false)
    {
        if (WalkingGround.MostWaysAlongAWalk != MostWaysInTheKernel || RibbonAtlas.MostWaysUnderABody != MostWaysUnderABodyInTheKernel)
        {
            throw new InvalidOperationException("The walkers' kernels are built for another bound on a walk's ways or a body's.");
        }

        _vk = vk;
        var people = world.People;
        var ways = world.Ways;
        var walking = world.Walking;
        var marks = world.Atlas.Marks;
        _walkers = people.Capacity;
        _ways = ways.Count;
        var slots = world.Occupancy.Capacity;
        _coverRoom = WalkingGround.MostPiecesOfABody + Sidestepping.MostRunsOfAPass;
        _proposedRoom = MostWaysInTheKernel * (1 + marks.MostCrossedByOne);
        _dutyCar = new int[_walkers];
        _atlas = new DeviceAtlas(vk, world.Atlas.Table);

        var lanes = walking.Foot.EdgeCount;
        if (ways.FirstMitreWay - ways.FirstFootwayWay != lanes) throw new InvalidOperationException("The pavement's lanes are not the town's footways.");

        var arcs = new List<DeviceGround.ArcRow>();
        var walkWays = new WalkWayRow[Math.Max(1, lanes + walking.TurnCount)];
        var lines = world.WalkingGround.Lines;
        for (var edge = 0; edge < lanes; edge++)
        {
            var first = (uint)arcs.Count;
            foreach (var arc in walking.LaneOf(edge)) arcs.Add(DeviceGround.ArcRow.Of(arc));
            walkWays[edge] = new WalkWayRow(
                first, (uint)arcs.Count, walking.LaneLengthM(edge), walking.LaneWidthM(edge), 0f, 0f, lines.ZebraOf(ways.OfFootway(edge)));
        }

        for (var slot = 0; slot < walking.TurnCount; slot++)
        {
            var first = (uint)arcs.Count;
            foreach (var arc in walking.JoinArcs(slot)) arcs.Add(DeviceGround.ArcRow.Of(arc));
            walkWays[lanes + slot] = new WalkWayRow(
                first, (uint)arcs.Count, walking.JoinLengthM(slot), walking.LaneWidthM(walking.TurnToEdge(slot)), walking.JoinFromM(slot),
                walking.JoinToM(slot), RibbonMarks.NoZebra);
        }

        if (arcs.Count == 0) arcs.Add(default);

        var markAt = new uint[_ways + 1];
        var sections = new List<MarkRow>();
        for (var way = 0; way < _ways; way++)
        {
            markAt[way] = (uint)sections.Count;
            foreach (var section in marks.Of(way)) sections.Add(new MarkRow(section.OnWay, section.FromM, section.ToM, section.MineFromM, section.MineToM));
        }

        markAt[_ways] = (uint)sections.Count;
        if (sections.Count == 0) sections.Add(default);

        var atlas = _atlas.PushFor(0);
        _figures = new Figures
        {
            RouteRoom = PersonFleet.RouteWaysPerPerson,
            StraightRoom = WalkingGround.MostRunsOfAStraight,
            PassRoom = Sidestepping.MostRunsOfAPass,
            CoverRoom = (uint)_coverRoom,
            ProposedRoom = (uint)_proposedRoom,
            LaneCount = (uint)lanes,
            FirstFootwayWay = ways.FirstFootwayWay,
            FirstMitreWay = ways.FirstMitreWay,
            GroundChannel = CityGen.CityPlan.RoadArrays.GroundChannel,
            StepM = config.PersonStepM,
            StandstillGapM = config.PersonStandstillGapM,
            WalkAheadM = config.PersonWalkAheadM,
            OffLaneM = config.WalkerOffLaneM,
            TouchM = config.RibbonTouchM,
            CellsFromX = atlas.CellsFromX,
            CellsFromY = atlas.CellsFromY,
            CellsWidth = atlas.CellsWidth,
            CellsHeight = atlas.CellsHeight,
            PerCellM = atlas.PerCellM,
            CellM = atlas.CellM,
            Shift = atlas.Shift,
            Mask = atlas.Mask,
            StartShift = atlas.StartShift,
            RowStartsShift = atlas.RowStartsShift,
            StartsPerCell = atlas.StartsPerCell,
            MetresPerOutside = atlas.MetresPerOutside,
            AlongPerMetre = atlas.AlongPerMetre,
            HasChannels = atlas.HasChannels,
        };

        _on = new Sent[Pieces];
        if (resident)
        {
            _in = [];
            _routes = new ResidentRows<int>(vk, _walkers, PersonFleet.RouteWaysPerPerson, hostOnly: true);
            _straights = new ResidentRows<SweptRun>(vk, _walkers, WalkingGround.MostRunsOfAStraight, hostOnly: true);
            _passes = new ResidentRows<SweptRun>(vk, _walkers, Sidestepping.MostRunsOfAPass, hostOnly: true);
            _stationed = new ResidentRows<WalkerRow>(vk, _walkers, 1, hostOnly: true);
            _changedIn = Staging<WalkerRow>(_walkers);
            _changes = new BufferCopy[Math.Max(1, _walkers)];
            _straightAgain = new bool[_walkers];
            var bodies = Device<WalkerRow>(_walkers);
            var walks = Device<WalkerRow>(_walkers);
            for (var piece = 0; piece < Pieces; piece++)
            {
                _on[piece] = new Sent(piece == (int)Piece.Body ? bodies : walks, _routes.Device, _straights.Device, _passes.Device);
            }
        }
        else
        {
            _in = new Sent[Pieces];
            for (var piece = 0; piece < Pieces; piece++)
            {
                _in[piece] = new Sent(
                    Staging<WalkerRow>(_walkers), Staging<int>(_walkers * PersonFleet.RouteWaysPerPerson),
                    Staging<SweptRun>(_walkers * WalkingGround.MostRunsOfAStraight), Staging<SweptRun>(_walkers * Sidestepping.MostRunsOfAPass));
                _on[piece] = new Sent(
                    Device<WalkerRow>(_walkers), Device<int>(_walkers * PersonFleet.RouteWaysPerPerson),
                    Device<SweptRun>(_walkers * WalkingGround.MostRunsOfAStraight), Device<SweptRun>(_walkers * Sidestepping.MostRunsOfAPass));
            }
        }

        _answers = Device<DeviceClaims.ClaimAnswer>(_walkers);
        _covers = Device<DeviceClaims.ClaimRow>(_walkers * _coverRoom);
        _wayRows = Device<LineWay>(_walkers * MostWaysInTheKernel);
        _proposed = Device<DeviceClaims.ClaimRow>(_walkers * _proposedRoom);
        _holds = Device<DeviceClaims.HoldRow>(_walkers);
        _outCounts = Device<UInt4>(_walkers);

        _walkersOut = Readback<WalkerRow>(_walkers);
        _straightsOut = Readback<SweptRun>(_walkers * WalkingGround.MostRunsOfAStraight);
        _coversOut = Readback<DeviceClaims.ClaimRow>(_walkers * _coverRoom);
        _waysOut = Readback<LineWay>(_walkers * MostWaysInTheKernel);
        _proposedOut = Readback<DeviceClaims.ClaimRow>(_walkers * _proposedRoom);
        _holdsOut = Readback<DeviceClaims.HoldRow>(_walkers);
        _countsOut = Readback<UInt4>(_walkers);

        GpuBuffer claimRows, bodyRuns, order;
        if (claims is { } chained)
        {
            (claimRows, bodyRuns, order) = (chained.Rows, chained.BodyRuns, chained.Order);
        }
        else
        {
            _rowsIn = Staging<DeviceClaims.ClaimRow>(slots);
            _answersIn = Staging<DeviceClaims.ClaimAnswer>(_walkers);
            _rows = Device<DeviceClaims.ClaimRow>(slots);
            _counts = Device<uint>(2 * _ways);
            _starts = Device<uint>(2 * _ways);
            _cursor = Device<uint>(2 * _ways);
            _order = Device<uint>(slots);
            _bodyRuns = Device<UInt2>(_ways);
            _plannedRuns = Device<UInt2>(_ways);
            _blockSums = Device<uint>(ScanKernels.Block);
            _totals = Device<uint>(1);
            _tally = Own(vk.CreateBuffer(Bytes<uint>(DeviceClaims.Tally.Words), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));

            GpuBuffer[] lists = [_rows, _counts, _cursor, _order, _bodyRuns, _plannedRuns, _starts, _tally];
            var listsPush = (uint)Marshal.SizeOf<ListsPush>();
            _count = new ComputeKernel(vk, "claims_count.comp", lists.Length, listsPush);
            _file = new ComputeKernel(vk, "claims_file.comp", lists.Length, listsPush);
            _sort = new ComputeKernel(vk, "claims_sort.comp", lists.Length, listsPush);
            _countSet = _count.Bind(lists);
            _fileSet = _file.Bind(lists);
            _sortSet = _sort.Bind(lists);
            _scans = new ScanKernels(vk);
            _scan = new PrefixScan(vk, _scans, _starts, _blockSums, _totals, 0);
            (claimRows, bodyRuns, order) = (_rows, _bodyRuns, _order);
        }

        var walkWaysOnDevice = Resident(Laid<WalkWayRow>(walkWays));
        var arcsOnDevice = Resident(Laid<DeviceGround.ArcRow>(CollectionsMarshal.AsSpan(arcs)));
        var markAtOnDevice = Resident(Laid<uint>(markAt));
        var marksOnDevice = Resident(Laid<MarkRow>(CollectionsMarshal.AsSpan(sections)));
        var pushBytes = (uint)Marshal.SizeOf<Figures>();
        string[] shaders = ["walkers_station.comp", "walkers_body.comp", "walkers_plan.comp", "walkers_read.comp"];
        _kernels = new ComputeKernel[Pieces];
        _sets = new DescriptorSet[Pieces];
        for (var piece = 0; piece < Pieces; piece++)
        {
            var on = _on[piece];
            GpuBuffer[] set =
            [
                on.Walkers, on.Routes, on.Straights, on.Passes, walkWaysOnDevice, arcsOnDevice, markAtOnDevice, marksOnDevice, claimRows,
                bodyRuns, order, _answers, _covers, _wayRows, _proposed, _holds, _outCounts, .. _atlas.Tables,
            ];
            _kernels[piece] = new ComputeKernel(vk, shaders[piece], set.Length, pushBytes);
            _sets[piece] = _kernels[piece].Bind(set);
        }

        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stamp>().Length);

        var commands = _batch.Begin();
        foreach (var (from, to) in _once) vk.Copy(commands, from, to, from.SizeBytes);
        _batch.SubmitAndWait();
    }

    /// <summary>How many walkers the stages have room for.</summary>
    public int Capacity => _walkers;

    /// <summary>The room each walker's body and pass rows have in <see cref="Covers"/>.</summary>
    public int CoverRoom => _coverRoom;

    /// <summary>The room each walker's plan rows have in <see cref="Proposed"/>.</summary>
    public int ProposedRoom => _proposedRoom;

    /// <summary>
    /// On the device, as the last stage recorded left them: each walker's body rows then its pass's
    /// (<see cref="CoverRoom"/> a walker), its plan's rows (<see cref="ProposedRoom"/> a walker) and its hold, and how many
    /// of each (bodies and passes from <see cref="Piece.Body"/>; a hold laid, ways and plan rows from <see cref="Piece.Plan"/>).
    /// </summary>
    public (GpuBuffer Covers, GpuBuffer Proposed, GpuBuffer Holds, GpuBuffer Counts) OnTheDevice => (_covers, _proposed, _holds, _outCounts);

    /// <summary>What each walker's hold came to, a walker a row, as <see cref="Piece.Read"/> reads it — the chain's to write.</summary>
    public GpuBuffer AnswersRead => _answers;

    /// <summary>Every walker as the last stage read back left it.</summary>
    public ReadOnlySpan<WalkerRow> Walkers => _walkersOut.Span<WalkerRow>()[.._walkerCount];

    /// <summary>One walker's body as the last <see cref="Piece.Body"/> laid it, as rows in the order the town lays them.</summary>
    public ReadOnlySpan<DeviceClaims.ClaimRow> BodyOf(int walker) =>
        _coversOut.Span<DeviceClaims.ClaimRow>().Slice(walker * _coverRoom, (int)Counts[walker].X);

    /// <summary>And the ground its pass covers, as the passes the town lays.</summary>
    public ReadOnlySpan<DeviceClaims.ClaimRow> PassOf(int walker) =>
        _coversOut.Span<DeviceClaims.ClaimRow>().Slice((walker * _coverRoom) + (int)Counts[walker].X, (int)Counts[walker].Y);

    /// <summary>Whether the last <see cref="Piece.Plan"/> planned this walker's walk down any ways at all.</summary>
    public bool Planned(int walker) => (int)Counts[walker].Z >= 0;

    /// <summary>The ways the last <see cref="Piece.Plan"/> planned a walker's walk down.</summary>
    public ReadOnlySpan<LineWay> WaysOf(int walker) =>
        _waysOut.Span<LineWay>().Slice(walker * MostWaysInTheKernel, Math.Max(0, (int)Counts[walker].Z));

    /// <summary>Whether the last <see cref="Piece.Plan"/> laid this walker a hold, and the hold.</summary>
    public bool LaidAHold(int walker, out DeviceClaims.HoldRow hold)
    {
        hold = _holdsOut.Span<DeviceClaims.HoldRow>()[walker];
        return Counts[walker].X != 0;
    }

    /// <summary>The rows a walker's hold laid, in the order the town lays them.</summary>
    public ReadOnlySpan<DeviceClaims.ClaimRow> ProposedOf(int walker) =>
        _proposedOut.Span<DeviceClaims.ClaimRow>().Slice(walker * _proposedRoom, (int)Counts[walker].W);

    /// <summary>The ground of the straight a walker last committed to, as the last <see cref="Piece.Plan"/> left it.</summary>
    public ReadOnlySpan<SweptRun> StraightOf(int walker) =>
        _straightsOut.Span<SweptRun>().Slice(walker * WalkingGround.MostRunsOfAStraight, (int)Walkers[walker].StraightRuns);

    ReadOnlySpan<UInt4> Counts => _countsOut.Span<UInt4>();

    /// <summary>The device's time over the last stage's walkers' kernel run on its own, in milliseconds.</summary>
    public double RanMs => _timer.BetweenMs((int)Stamp.Listed, (int)Stamp.Ran);

    /// <summary>And over the claims' lists it laid first, nought where it read no claims.</summary>
    public double ListedMs => _timer.BetweenMs((int)Stamp.Began, (int)Stamp.Listed);

    /// <summary>
    /// <b>The walkers as the town has them now</b>, written into what <paramref name="piece"/> is sent: every walker's row,
    /// its route, the straight it committed to and its pass's ground.
    /// </summary>
    public void Lay(TownWorld world, Piece piece)
    {
        var people = world.People;
        _walkerCount = people.Count;
        NameTheOfficers(world);

        var sent = _in[(int)piece];
        var rows = sent.Walkers.Span<WalkerRow>();
        var routes = sent.Routes.Span<int>();
        var straights = sent.Straights.Span<SweptRun>();
        var passes = sent.Passes.Span<SweptRun>();
        for (var person = 0; person < people.Count; person++)
        {
            rows[person] = RowOf(world, person);
            people.RouteOf(person)[..people.RouteCount[person]].CopyTo(routes.Slice(person * PersonFleet.RouteWaysPerPerson));
            world.WalkingGround.StraightOf(person).CopyTo(straights.Slice(person * WalkingGround.MostRunsOfAStraight));
            world.Sidestepping.SweptOf(person).CopyTo(passes.Slice(person * Sidestepping.MostRunsOfAPass));
        }
    }

    /// <summary>
    /// <b>Resident: the walkers as the town has them once stationed</b> (<see cref="ClaimsStage.Stationed"/>), written into
    /// what the bodies and the walks are sent: every walker's row, route, straight and pass's ground where it differs from
    /// what the device keeps.
    /// </summary>
    public void LayTheStationed(TownWorld world)
    {
        var people = world.People;
        _walkerCount = people.Count;
        _changeCount = 0;
        _changedRows = 0;
        NameTheOfficers(world);

        for (var person = 0; person < people.Count; person++)
        {
            _stationed!.Lay(person, RowOf(world, person));
            _routes!.LayRows(person, people.RouteOf(person)[..people.RouteCount[person]]);
            _straights!.LayRows(person, world.WalkingGround.StraightOf(person), _straightAgain[person]);
            _straightAgain[person] = false;
            _passes!.LayRows(person, world.Sidestepping.SweptOf(person));
        }
    }

    /// <summary>
    /// <b>Resident: the walkers as the town has them once their passes are laid</b> (<see cref="ClaimsStage.WalksDue"/>):
    /// every row that changed since they were stationed — a pass given up, whose ground a walker's is laid as — written over
    /// the walks' own copy.
    /// </summary>
    public void LayWhatChanged(TownWorld world)
    {
        var rows = Kept(_changedIn).Span<WalkerRow>();
        var bytes = (ulong)Marshal.SizeOf<WalkerRow>();
        for (var person = 0; person < _walkerCount; person++)
        {
            var row = RowOf(world, person);
            if (MemoryMarshal.AsBytes(new ReadOnlySpan<WalkerRow>(in row))
                .SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<WalkerRow>(in _stationed!.LaidRow(person)))))
            {
                continue;
            }

            rows[person] = row;
            _changedRows++;
            var at = (ulong)person * bytes;
            if (_changeCount > 0 && _changes[_changeCount - 1].DstOffset + _changes[_changeCount - 1].Size == at)
            {
                _changes[_changeCount - 1].Size += bytes;
                continue;
            }

            _changes[_changeCount++] = new BufferCopy(at, at, bytes);
        }
    }

    /// <summary>
    /// Resident: what was laid sent — the rows as stationed that changed, then all of them copied on the device to the
    /// bodies' own and the walks', the rows changed since over the walks', and the routes, straights and passes' ground
    /// that changed. The caller barriers.
    /// </summary>
    public void RecordTheResidentSend(CommandBuffer commands)
    {
        var vk = _vk;
        _stationed!.Send(commands);
        _routes!.Send(commands);
        _straights!.Send(commands);
        _passes!.Send(commands);
        vk.AfterTransfer(commands);
        var bytes = Bytes<WalkerRow>(_walkerCount);
        vk.Copy(commands, _stationed.Device, _on[(int)Piece.Body].Walkers, bytes);
        vk.Copy(commands, _stationed.Device, _on[(int)Piece.Plan].Walkers, bytes);
        if (_changeCount == 0) return;

        vk.AfterTransfer(commands);
        vk.Copy(commands, Kept(_changedIn), _on[(int)Piece.Plan].Walkers, _changes.AsSpan(0, _changeCount));
    }

    /// <summary>
    /// How many bytes the resident send laid so far will copy across: the rows as stationed that changed, the rows changed
    /// since, and the routes, straights and passes' ground that changed.
    /// </summary>
    public ulong ResidentSentBytes =>
        _stationed!.MarkedBytes + ((ulong)_changedRows * (ulong)Marshal.SizeOf<WalkerRow>()) + _routes!.MarkedBytes
        + _straights!.MarkedBytes + _passes!.MarkedBytes;

    /// <summary>
    /// Resident: a walker the last walks committed to a straight of the device's own (<c>walkers_plan.comp</c>'s count), whose
    /// straight is sent next time whatever was laid last.
    /// </summary>
    public void SendTheStraightAgain(int walker) => _straightAgain[walker] = true;

    /// <summary>Which car, if any, each walker is the officer of (<see cref="WalkerRow.DutyCar"/>): the lowest numbered.</summary>
    void NameTheOfficers(TownWorld world)
    {
        Array.Fill(_dutyCar, PersonFleet.NoCar);
        var officers = world.OfficerOfEachCar;
        for (var car = officers.Length - 1; car >= 0; car--)
        {
            if (officers[car] >= 0 && officers[car] < _walkers) _dutyCar[officers[car]] = car;
        }
    }

    /// <summary>One walker as the town has it now, as the kernels read it.</summary>
    WalkerRow RowOf(TownWorld world, int person)
    {
        var people = world.People;
        var ground = world.WalkingGround;
        var heldAs = ground.HeldAs(person, out var roster);
        var straightTo = ground.StraightToM(person);
        var pass = people.Pass[person];
        return new WalkerRow
        {
            PositionM = people.PositionM[person],
            VelocityMps = people.VelocityMps[person],
            DeclaredMps = people.DeclaredMps[person],
            DestinationM = people.DestinationM[person],
            GoalM = people.GoalM[person],
            BackToM = world.WalkingBack.BackToM[person],
            StraightFromM = ground.StraightFromM(person),
            StraightToM = straightTo,
            HeadingRad = people.HeadingRad[person],
            RadiusM = people.RadiusM[person],
            OnWayM = people.OnWayM[person],
            OffWayM = people.OffWayM[person],
            RouteToM = people.RouteToM[person],
            GrantM = people.GrantM[person],
            PassFromM = pass.FromM,
            PassClearM = pass.ClearM,
            PassAsideM = pass.AsideM,
            PassSlot = pass.Slot,
            RouteCount = people.RouteCount[person],
            RouteTaken = people.RouteTaken[person],
            OnWay = people.OnWay[person],
            OnCrossing = people.OnCrossing[person],
            Car = people.Car[person],
            DutyCar = _dutyCar[person],
            HeldAs = heldAs,
            StraightRuns = (uint)ground.StraightOf(person).Length,
            PassRuns = (uint)world.Sidestepping.SweptOf(person).Length,
            Flags = (people.Walking[person] ? WalkerRow.Walking : 0u) | (people.Wounded[person] ? WalkerRow.Wounded : 0u)
                    | (people.Inside[person].Any ? WalkerRow.Inside : 0u) | (people.RouteRunsOut[person] ? WalkerRow.RunsOut : 0u)
                    | (pass.Begun ? WalkerRow.PassBegun : 0u) | (roster == LaneRoster.Driving ? WalkerRow.HeldAsDriving : 0u)
                    | (float.IsNaN(straightTo.X) ? 0u : WalkerRow.OnAStraight),
            Action = (uint)people.Action[person],
            Stage = (uint)people.Stage[person],
            Hold = world.WalkHold(person),
            AlongMps = ground.AlongItsWalkMps(person),
        };
    }

    static GpuBuffer Kept(GpuBuffer? buffer) =>
        buffer ?? throw new InvalidOperationException("These walkers are sent stage by stage, and keep nothing resident.");

    /// <summary>
    /// <b>The reservations as the town has them now</b>, written into what a stage run on its own is sent: every slot a
    /// row, a main claim of a plan with how far its holder's body reaches along its way (<see cref="DeviceClaims.Lay"/>'s rows).
    /// </summary>
    public void LayTheClaims(LaneOccupancy claims)
    {
        if (claims.WayCount != _ways) throw new InvalidOperationException("The claims are over another town's ways.");

        var rows = OwnClaims(_rowsIn).Span<DeviceClaims.ClaimRow>();
        for (var slot = 0; slot < claims.SlotCount; slot++) rows[slot] = DeviceClaims.ClaimRow.OfSlot(claims, slot);

        _rowCount = claims.SlotCount;
        OwnClaims(_tally).Span<uint>()[DeviceClaims.Tally.Rows] = (uint)_rowCount;
    }

    /// <summary>
    /// <b>What every walker's hold came to</b> (<see cref="LaneOccupancy.HoldEndsAtM"/>, <see cref="LaneOccupancy.HoldCutOn"/>),
    /// a walker a row, written into what a stage run on its own is sent.
    /// </summary>
    public void LayTheAnswers(TownWorld world)
    {
        var claims = world.Occupancy;
        var answers = OwnClaims(_answersIn).Span<DeviceClaims.ClaimAnswer>();
        for (var person = 0; person < world.People.Count; person++)
        {
            var hold = world.WalkHold(person);
            var endsAtM = claims.HoldEndsAtM(hold, out var marginM, out _);
            answers[person] = new DeviceClaims.ClaimAnswer(
                endsAtM, marginM, DeviceClaims.ClaimAnswer.NoRow, claims.HoldCutOn(hold), float.PositiveInfinity, 0f, 0f, 0f);
        }
    }

    /// <summary>
    /// <b>One stage run on its own</b>: what was laid for it sent across, the claims' body lists laid where the stage reads
    /// them, the stage's kernel run over every walker, and what it came to read back. One submit and one wait.
    /// </summary>
    public void Run(Piece piece)
    {
        var vk = _vk;
        var readsTheClaims = piece is Piece.Plan or Piece.Read;
        var commands = _batch.Begin();
        _timer.Reset(commands);
        RecordSend(commands, piece);
        if (piece == Piece.Read) vk.Copy(commands, OwnClaims(_answersIn), _answers, Bytes<DeviceClaims.ClaimAnswer>(_walkerCount));
        if (readsTheClaims)
        {
            vk.Copy(commands, OwnClaims(_rowsIn), OwnClaims(_rows), Bytes<DeviceClaims.ClaimRow>(_rowCount));
            vk.Fill(commands, OwnClaims(_counts), 0);
        }

        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stamp.Began);
        if (readsTheClaims) LayTheLists(commands);

        _timer.Stamp(commands, (int)Stamp.Listed);
        Record(commands, piece);
        vk.BeforeTransfer(commands);
        _timer.Stamp(commands, (int)Stamp.Ran);
        RecordRead(commands, piece);
        _batch.SubmitAndWait();
        _timer.Read();
    }

    /// <summary>What was laid for <paramref name="piece"/> copied to its walkers on the device, recorded; the caller barriers.</summary>
    public void RecordSend(CommandBuffer commands, Piece piece)
    {
        var vk = _vk;
        var (from, to) = (_in[(int)piece], _on[(int)piece]);
        vk.Copy(commands, from.Walkers, to.Walkers, Bytes<WalkerRow>(_walkerCount));
        vk.Copy(commands, from.Routes, to.Routes, Bytes<int>(_walkerCount * PersonFleet.RouteWaysPerPerson));
        vk.Copy(commands, from.Straights, to.Straights, Bytes<SweptRun>(_walkerCount * WalkingGround.MostRunsOfAStraight));
        vk.Copy(commands, from.Passes, to.Passes, Bytes<SweptRun>(_walkerCount * Sidestepping.MostRunsOfAPass));
    }

    /// <summary>How many bytes one stage's send copies (<see cref="RecordSend"/>).</summary>
    public ulong SentBytesAStage =>
        Bytes<WalkerRow>(_walkerCount) + Bytes<int>(_walkerCount * PersonFleet.RouteWaysPerPerson)
        + Bytes<SweptRun>(_walkerCount * WalkingGround.MostRunsOfAStraight) + Bytes<SweptRun>(_walkerCount * Sidestepping.MostRunsOfAPass);

    /// <summary><paramref name="piece"/>'s kernel over every walker its walkers were laid for, recorded and made visible to the next kernel.</summary>
    public void Record(CommandBuffer commands, Piece piece)
    {
        var figures = _figures with { WalkerCount = (uint)_walkerCount };
        _kernels[(int)piece].Dispatch(commands, _sets[(int)piece], Groups(_walkerCount), figures);
        _vk.BetweenKernels(commands);
    }

    /// <summary>What <paramref name="piece"/> left copied back, recorded after a transfer barrier.</summary>
    public void RecordRead(CommandBuffer commands, Piece piece)
    {
        var vk = _vk;
        vk.Copy(commands, _on[(int)piece].Walkers, _walkersOut, Bytes<WalkerRow>(_walkerCount));
        vk.Copy(commands, _outCounts, _countsOut, Bytes<UInt4>(_walkerCount));
        switch (piece)
        {
            case Piece.Body:
                vk.Copy(commands, _covers, _coversOut, Bytes<DeviceClaims.ClaimRow>(_walkerCount * _coverRoom));
                break;
            case Piece.Plan:
                vk.Copy(commands, _on[(int)piece].Straights, _straightsOut, Bytes<SweptRun>(_walkerCount * WalkingGround.MostRunsOfAStraight));
                vk.Copy(commands, _wayRows, _waysOut, Bytes<LineWay>(_walkerCount * MostWaysInTheKernel));
                vk.Copy(commands, _proposed, _proposedOut, Bytes<DeviceClaims.ClaimRow>(_walkerCount * _proposedRoom));
                vk.Copy(commands, _holds, _holdsOut, Bytes<DeviceClaims.HoldRow>(_walkerCount));
                break;
        }
    }

    /// <summary>Every way's body list laid from the rows, as the claims lay theirs (<c>claims_lists.glsl</c>).</summary>
    void LayTheLists(CommandBuffer commands)
    {
        var vk = _vk;
        var lists = new ListsPush((uint)_ways, 0u);
        _count!.Dispatch(commands, _countSet, Groups(_rowCount), lists);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, OwnClaims(_counts), OwnClaims(_starts), OwnClaims(_counts).SizeBytes);
        vk.AfterTransfer(commands);
        _scan!.Record(commands, 2 * _ways);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, OwnClaims(_starts), OwnClaims(_cursor), OwnClaims(_starts).SizeBytes);
        vk.AfterTransfer(commands);
        _file!.Dispatch(commands, _fileSet, Groups(_rowCount), lists);
        vk.BetweenKernels(commands);
        _sort!.Dispatch(commands, _sortSet, Groups(_ways), lists);
        vk.BetweenKernels(commands);
    }

    static GpuBuffer OwnClaims(GpuBuffer? buffer) =>
        buffer ?? throw new InvalidOperationException("These walkers read the device's own claims, and lay none of the town's.");

    static uint Groups(int count) => (uint)Math.Max(1, (count + 63) / 64);

    GpuBuffer Device<T>(int count) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None));

    GpuBuffer Staging<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferSrcBit, HostAccess.Writes));

    GpuBuffer Readback<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferDstBit, HostAccess.Reads));

    /// <summary>A device-local copy of a table laid once, copied when these are made.</summary>
    GpuBuffer Resident(GpuBuffer laid)
    {
        var resident = Own(_vk.CreateBuffer(laid.SizeBytes, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));
        _once.Add((laid, resident));
        return resident;
    }

    GpuBuffer Laid<T>(ReadOnlySpan<T> rows) where T : unmanaged
    {
        var buffer = Staging<T>(rows.Length);
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
        _scans?.Dispose();
        _count?.Dispose();
        _file?.Dispose();
        _sort?.Dispose();
        foreach (var kernel in _kernels) kernel.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
        _stationed?.Dispose();
        _routes?.Dispose();
        _straights?.Dispose();
        _passes?.Dispose();
        _atlas.Dispose();
    }
}
