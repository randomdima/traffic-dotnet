using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The walkers kept as the device's own</b> (<see cref="Keeps"/>), for a town whose walkers the device ticks too: one
/// table every stage of every rebuild runs over in turn — stationed, their bodies and passes laid, their walks planned and,
/// with the answers, their grants read and their aims set — read in where the bodies' step left each walker
/// (<c>walkers_kept_gather.comp</c>), with only what the town changed since written over it (<c>walkers_kept_patch.comp</c>),
/// and only what the stages changed handed back (<c>walkers_kept_out.comp</c>) for the town to take up as its own
/// (<see cref="TownWorld.KeptOnTheDevice"/>) — of a walker the town reads; any other is left on the device until it does
/// (<see cref="LeavesTheUnread"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>What the town holds of a walker is what the device left</b>, but where the town changed it since: every field a stage
/// writes is taken back whole, so a walker whose row the town would lay other than the device last left it is one the town
/// changed — and it is written over whole. Not compared: where the walker stands, its pace and how fast it goes the way it
/// faces, which are read in off the walker tick's table where that holds the town's (<see cref="DeviceWalkerTick.HoldsTheBodyOf"/>);
/// the hold its walk laid, which each rebuild numbers again; and the straight it walks, which the device's walks commit
/// to and the town's never do while the device keeps the walker.
/// </para>
/// <para>
/// <b>Numbers the device's</b> (root decision log, 2026-10-08): a walker's place on its way is the device's projection, and
/// how fast it goes the way it faces the device's arithmetic, where the town's own rebuild stationed it to the bit.
/// </para>
/// </remarks>
internal sealed partial class DeviceWalkers
{
    /// <summary>What the town changed of one kept walker (<c>walkers_kept_patch.comp</c>'s <c>KeptPatch</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct KeptPatch(WalkerRow Row, uint Walker, uint Kept);

    /// <summary>What a rebuild's stages made of one kept walker (<c>walkers_kept_out.comp</c>'s <c>WalkerOut</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkerOut(
        Vector2 DestinationM, Vector2 BackToM, float OnWayM, float OffWayM, float GrantM, float PassFromM, float PassClearM,
        float PassAsideM, int PassSlot, int RouteTaken, int OnWay, int OnCrossing, int HeldAs, uint Flags, uint Action, int Hold,
        uint Events, uint Walker);

    /// <summary>A kept walker handed back (<c>walkers_kept_out.comp</c>'s <c>HandedRow</c>): its row out, and its leg's clock.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct HandedRow(WalkerOut Made, WalkerClock Clock);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct KeptOutPush(uint WalkerCount, uint FirstWalkerUnit, uint Words);

    /// <summary><c>walkers_kept_out.comp</c>'s counts: rows out on the device, handed to the host, and walkers left on the device.</summary>
    const int CountOut = 0, CountHanded = 1, CountLeft = 2;

    GpuBuffer _keptTable = null!, _keptBefore = null!, _keptPatchesIn = null!, _keptPatches = null!;
    GpuBuffer _keptOutRows = null!, _keptCounts = null!, _keptCountsRead = null!;

    /// <summary>
    /// Which walkers the town reads and which it left on the device, written by the host; the walkers handed back, those
    /// left, and every walker's hold as the rebuild numbered it, in the host's memory — and, for walkers left on the device
    /// and taken back outside a rebuild, those named and what came of them.
    /// </summary>
    GpuBuffer _keptMarked = null!, _keptHanded = null!, _keptLeft = null!, _keptHolds = null!, _keptNamed = null!, _keptNamedBack = null!;

    ComputeKernel? _keptGather, _keptPatch, _keptOut, _keptAims, _keptHand;
    DescriptorSet _keptGatherSet, _keptPatchSet, _keptOutSet, _keptAimsSet, _keptHandSet;

    /// <summary>
    /// <b>Which walkers a rebuild changed that the town did not read</b>: what the town holds of each is what the device left
    /// before that rebuild, and the device's kept row is newer — taken back whole when the town reads it
    /// (<see cref="TakeBack"/>), and never written over meanwhile.
    /// </summary>
    bool[] _left = [];

    /// <summary>The marks <see cref="WantTheWalkers"/> builds, copied across whole.</summary>
    uint[] _marks = [];

    int Words => (_walkers + 31) / 32;

    /// <summary>Every kept walker's row as the device is known to hold it, and whether it is known at all.</summary>
    WalkerRow[] _known = [];

    bool[] _isKnown = [];

    int _keptPatchCount;

    /// <summary>Whether the device keeps every walker's side of the claims as its own (<see cref="DeviceWalkers"/>'s <c>keptFrom</c>).</summary>
    public bool Keeps { get; private set; }

    /// <summary>Every kept walker's row as the stages leave it, for a kernel that reads it in place.</summary>
    public GpuBuffer KeptTable => _keptTable;

    /// <summary>How many walkers the town changed since the last rebuild, written over the kept table by this one.</summary>
    public int KeptPatched => _keptPatchCount;

    /// <summary>How many walkers the last rebuild's stages changed, and how many of them it handed back.</summary>
    public (int Changed, int Handed) KeptChanged { get; private set; }

    /// <summary>How many walkers are left on the device, the town holding them as an older rebuild left them (<see cref="_left"/>).</summary>
    public int KeptLeft { get; private set; }

    /// <summary>
    /// <b>Whether a walker a rebuild changed that the town does not read is left on the device</b> (<see cref="_left"/>) —
    /// only where the device is asked every walker's decision put off first, and hands over the row of each it leaves to
    /// the town (<see cref="TakeUpTheDecided"/>). Off, every walker a rebuild changed is handed back.
    /// </summary>
    public bool LeavesTheUnread { get; set; }

    /// <summary>
    /// <b>Whether to count, after every rebuild is taken back, the walkers the town would still lay other than the device
    /// left them</b> (<see cref="KeptUnlike"/>) — a check of the taking back, at the cost of laying every walker again.
    /// </summary>
    public bool ChecksWhatWasTaken { get; set; }

    /// <summary>How many walkers the town held other than the device left them, right after a rebuild was taken back, over every rebuild checked.</summary>
    public long KeptUnlike { get; private set; }

    void KeepThem(GpuBuffer table)
    {
        Keeps = true;
        _keptTable = _on[(int)Piece.Station].Walkers;
        _keptBefore = Device<WalkerRow>(_walkers);
        _keptPatchesIn = Staging<KeptPatch>(_walkers);
        _keptPatches = Device<KeptPatch>(_walkers);
        _keptOutRows = Device<WalkerOut>(_walkers);
        _keptCounts = Device<uint>(3);
        _keptCountsRead = Readback<uint>(3);
        _keptMarked = Own(_vk.CreateBuffer(Bytes<uint>(2 * Words), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));
        _keptHanded = Own(_vk.CreateBuffer(Bytes<HandedRow>(_walkers), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _keptLeft = Own(_vk.CreateBuffer(Bytes<uint>(_walkers), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _keptHolds = Own(_vk.CreateBuffer(Bytes<int>(_walkers), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _keptNamed = Own(_vk.CreateBuffer(Bytes<uint>(_walkers), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));
        _keptNamedBack = Own(_vk.CreateBuffer(Bytes<HandedRow>(_walkers), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _keptGather = new ComputeKernel(_vk, "walkers_kept_gather.comp", 3, sizeof(uint));
        _keptGatherSet = _keptGather.Bind([_keptTable, _keptBefore, table]);
        _keptPatch = new ComputeKernel(_vk, "walkers_kept_patch.comp", 3, sizeof(uint));
        _keptPatchSet = _keptPatch.Bind([_keptPatches, _keptTable, _keptBefore]);
        _keptAims = new ComputeKernel(_vk, "walkers_kept_aims.comp", 3, sizeof(uint));
        _keptAimsSet = _keptAims.Bind([_keptOutRows, _keptCounts, table]);
        _known = new WalkerRow[_walkers];
        _isKnown = new bool[_walkers];
        _left = new bool[_walkers];
        _marks = new uint[2 * Words];
        DecideOverThem(table);
        _keptHand = new ComputeKernel(_vk, "walkers_kept_hand.comp", 4, sizeof(uint));
        _keptHandSet = _keptHand.Bind([_keptTable, _keptNamed, _walkerClocks!.Device, _keptNamedBack]);
    }

    /// <summary>
    /// Kept: the kernel that hands back what a rebuild's stages made of the walkers (<c>walkers_kept_out.comp</c>), bound
    /// over the rebuild's numbering of its units' holds once the rebuild has laid it, and over the claims' tally, which says
    /// whether the rebuild passed a room and hands back nothing.
    /// </summary>
    public void HandBackThrough(GpuBuffer unitHolds, GpuBuffer tally)
    {
        _keptOut = new ComputeKernel(_vk, "walkers_kept_out.comp", 11, (uint)Marshal.SizeOf<KeptOutPush>());
        _keptOutSet = _keptOut.Bind(
            [
                _keptTable, _keptBefore, _keptOutRows, _keptCounts, _keptMarked, _keptHanded, _keptLeft, unitHolds, _keptHolds,
                _walkerClocks!.Device, tally,
            ]);
    }

    /// <summary>
    /// Kept: every walker put back as a rebuild that passed a room read it in — before its stages, which wrote over the
    /// kept table, and its count of what is handed back cleared — for the rebuild to be laid again from it. Recorded; the
    /// caller barriers.
    /// </summary>
    /// <remarks>
    /// <b>The straights the stages committed stay</b>: laid again from the same rows at the same instant, every walker
    /// commits the very straight it did, over the very runs.
    /// </remarks>
    public void RecordTheKeptAgain(CommandBuffer commands)
    {
        _vk.Copy(commands, _keptBefore!, _keptTable!, Bytes<WalkerRow>(_walkerCount));
        _vk.Fill(commands, _keptCounts!, 0);
    }

    /// <summary>
    /// <b>Kept: what the town changed of its walkers since the device last left them</b>, staged to be written over the
    /// kept table — each walker whose row the town would lay other than the device is known to hold it, or whose body the
    /// walker tick's table does not hold where the town has it — and each walker's route and pass's ground where they changed.
    /// A walker new to the device is written whole, the straight the town walks it down among it.
    /// </summary>
    public void LayTheKept(TownWorld world, DeviceWalkerTick ticked)
    {
        var people = world.People;
        _walkerCount = people.Count;
        _keptPatchCount = 0;
        NameTheOfficers(world);

        var patches = _keptPatchesIn.Span<KeptPatch>();
        for (var person = 0; person < people.Count; person++)
        {
            _routes!.LayRows(person, people.RouteOf(person)[..people.RouteCount[person]]);
            _passes!.LayRows(person, world.Sidestepping.SweptOf(person));
            var row = RowOf(world, person);
            var kept = _isKnown[person];
            if (kept)
            {
                row.Hold = _known[person].Hold;
                if (SameToTheClaims(row, _known[person]) && (row.Is(WalkerRow.Inside) || ticked.HoldsTheBodyOf(people, person))) continue;
            }
            else
            {
                row.Hold = LaneOccupancy.NoHold;
                _straights!.LayRows(person, world.WalkingGround.StraightOf(person), again: true);
            }

            if (_left[person])
            {
                throw new InvalidOperationException(
                    $"Walker {person} would be written over the device's kept row off the town's copy, older than the device's.");
            }

            patches[_keptPatchCount++] = new KeptPatch(row, (uint)person, kept ? 1u : 0u);
            _known[person] = row;
            _isKnown[person] = true;
        }
    }

    /// <summary>
    /// Whether two rows of one walker are the same to the claims but for what the kept table reads in or keeps itself: where
    /// it stands, its pace and how fast it goes the way it faces, the hold its walk laid, the straight it walks, and what the
    /// last rebuild did to it.
    /// </summary>
    static bool SameToTheClaims(WalkerRow one, WalkerRow other)
    {
        Unread(ref one);
        Unread(ref other);
        return MemoryMarshal.AsBytes(new ReadOnlySpan<WalkerRow>(in one)).SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<WalkerRow>(in other)));

        static void Unread(ref WalkerRow row)
        {
            row.PositionM = row.DeclaredMps = row.StraightFromM = row.StraightToM = Vector2.Zero;
            row.AlongMps = 0f;
            row.Hold = 0;
            row.StraightRuns = row.Events = 0u;
            row.Flags &= ~WalkerRow.OnAStraight;
        }
    }

    /// <summary>
    /// Kept: what <see cref="LayTheKept"/> staged sent — the patches, and the routes, straights and passes' ground that
    /// changed — and the count of what is handed back cleared. The caller barriers.
    /// </summary>
    public void RecordTheKeptSend(CommandBuffer commands)
    {
        _vk.Copy(commands, _keptPatchesIn, _keptPatches, Bytes<KeptPatch>(_keptPatchCount));
        _routes!.Send(commands);
        _straights!.Send(commands);
        _passes!.Send(commands);
        _vk.Fill(commands, _keptCounts, 0);
    }

    /// <summary>How many bytes <see cref="RecordTheKeptSend"/> copies across.</summary>
    ulong KeptSentBytes =>
        ((ulong)_keptPatchCount * (ulong)Marshal.SizeOf<KeptPatch>()) + _routes!.MarkedBytes + _straights!.MarkedBytes + _passes!.MarkedBytes;

    /// <summary>
    /// Kept: every walker read in where the bodies' step left it, and what the town changed written over it, recorded and
    /// made visible to the stages.
    /// </summary>
    public void RecordTheKeptIn(CommandBuffer commands)
    {
        _keptGather!.Dispatch(commands, _keptGatherSet, Groups(_walkerCount), (uint)_walkerCount);
        _vk.BetweenKernels(commands);
        if (_keptPatchCount == 0) return;

        _keptPatch!.Dispatch(commands, _keptPatchSet, Groups(_keptPatchCount), (uint)_keptPatchCount);
        _vk.BetweenKernels(commands);
    }

    /// <summary>
    /// Kept: every walker the stages changed written out and counted, those the town reads and those it left on the device
    /// before now handed back, the rest named left, and every walker's hold numbered — recorded after
    /// <see cref="WantTheWalkers"/>; the caller barriers before reading the counts back.
    /// </summary>
    /// <param name="firstWalkerUnit">The unit the first walker's walk is, in the rebuild's numbering of its units' holds.</param>
    public void RecordTheKeptOut(CommandBuffer commands, int firstWalkerUnit) =>
        _keptOut!.Dispatch(
            commands, _keptOutSet, Groups(_walkerCount), new KeptOutPush((uint)_walkerCount, (uint)firstWalkerUnit, (uint)Words));

    /// <summary>
    /// <b>Which walkers the town reads as this rebuild leaves them</b>, marked for it to hand back: every one where
    /// <paramref name="every"/>, else those the town reads whenever (<see cref="TownWorld.ReadsTheWalker"/>) and those whose
    /// decision it takes this tick (<see cref="TownWorld.DecisionsPutOff"/>) — and which it left on the device before, which
    /// are handed back whether changed or not.
    /// </summary>
    public void WantTheWalkers(TownWorld town, bool every)
    {
        every |= !LeavesTheUnread;
        var words = Words;
        var marks = _marks.AsSpan();
        marks.Clear();
        var wanted = marks[..words];
        var left = marks[words..];
        for (var person = 0; person < _walkerCount; person++)
        {
            if (every || town.ReadsTheWalker(person)) wanted[person >> 5] |= 1u << (person & 31);
            if (_left[person]) left[person >> 5] |= 1u << (person & 31);
        }

        var firstCar = town.Roster.AgentOfCar(0);
        foreach (var agent in town.DecisionsPutOff)
        {
            if (agent < firstCar) wanted[agent >> 5] |= 1u << (agent & 31);
        }

        // Built here and copied across whole: the table is memory the host only writes, slow to read back a word at a time.
        marks.CopyTo(_keptMarked.Span<uint>());
    }

    /// <summary>
    /// Kept: each walker the last rebuild's stages changed aimed and walking as they left it, in the walker tick's table
    /// (<c>walkers_kept_aims.comp</c>) — recorded in the next tick's submit, after the walkers the town wrote whole over
    /// it, and before the tick; the caller barriers.
    /// </summary>
    public void RecordTheKeptAims(CommandBuffer commands) =>
        _keptAims!.Dispatch(commands, _keptAimsSet, Groups(_walkerCount), (uint)_walkerCount);

    /// <summary>Kept: how many walkers the stages changed, handed back and left, copied back — the rest is in the host's memory already.</summary>
    public void RecordReadTheKept(CommandBuffer commands) => _vk.Copy(commands, _keptCounts, _keptCountsRead, Bytes<uint>(3));

    /// <summary>How many bytes the last rebuild handed back of the walkers: the counts, the rows handed back, those left, and every hold.</summary>
    public ulong KeptReadBytes => Bytes<uint>(3) + Bytes<HandedRow>(KeptChanged.Handed) + Bytes<uint>(_leftNow) + Bytes<int>(_walkerCount);

    int _leftNow;

    /// <summary>
    /// <b>Kept: what the stages made of every walker they changed and the town reads taken up by the town</b>
    /// (<see cref="TakeBack"/>) — every other they changed left on the device (<see cref="_left"/>) — and every walker's
    /// hold as the rebuild numbered it (<see cref="TownWorld.WalkedOnTheDevice"/>).
    /// </summary>
    /// <param name="ticked">The walker tick whose table each changed walker's aim and walk are written into.</param>
    public void TakeTheKept(TownWorld town, DeviceWalkerTick ticked)
    {
        var counts = _keptCountsRead.Span<uint>();
        var handed = (int)Math.Min(counts[CountHanded], (uint)_walkerCount);
        _leftNow = (int)Math.Min(counts[CountLeft], (uint)_walkerCount);
        KeptChanged = ((int)counts[CountOut], handed);
        foreach (ref readonly var row in _keptHanded.Span<HandedRow>()[..handed]) TakeBack(town, ticked, row.Made, row.Clock);

        foreach (var person in _keptLeft.Span<uint>()[.._leftNow])
        {
            if (!_left[person]) KeptLeft++;
            _left[person] = true;
        }

        var holds = _keptHolds.Span<int>();
        for (var person = 0; person < _walkerCount; person++) town.WalkedOnTheDevice(person, holds[person], committedAStraight: false);

        if (ChecksWhatWasTaken) KeptUnlike += Unlike(town);
    }

    /// <summary>Whether the town holds this walker as an older rebuild left it, the device's kept row being newer (<see cref="_left"/>).</summary>
    public bool LeftOnTheDevice(int person) => _left[person];

    /// <summary>
    /// <b>A walker the town decides, taken back off the row its decision was asked of</b> (<c>walkers_decide.comp</c>), where
    /// a rebuild left it on the device — before the town reads it. Nothing for one the town holds.
    /// </summary>
    public void TakeUpTheDecided(TownWorld town, DeviceWalkerTick ticked, int person)
    {
        if (!_left[person]) return;

        ref readonly var clock = ref DecidedOn(person);
        TakeBack(town, ticked, DecidedRowOf(person), new WalkerClock(clock.SinceS, clock.ClosestM, clock.RouteTakenAt));
    }

    /// <summary>
    /// <b>Every walker left on the device taken back</b> as the kept table holds it, in one submit of its own — for a town
    /// about to read every walker: one laying its claims itself, or let go by the device.
    /// </summary>
    public void TakeBackTheLeft(TownWorld town, DeviceWalkerTick ticked)
    {
        if (!Keeps) return;

        var named = _keptNamed.Span<uint>();
        var count = 0;
        for (var person = 0; person < _walkerCount; person++)
        {
            if (_left[person]) named[count++] = (uint)person;
        }

        TakeBackTheNamed(town, ticked, count);
    }

    /// <summary>
    /// Every walker whose decision put off the town takes itself left on the device taken back, in one submit of its own —
    /// for a town taking the decisions without asking the device first.
    /// </summary>
    public void TakeBackTheDue(TownWorld town, DeviceWalkerTick ticked)
    {
        if (!Keeps) return;

        var named = _keptNamed.Span<uint>();
        var firstCar = town.Roster.AgentOfCar(0);
        var count = 0;
        foreach (var agent in town.DecisionsPutOff)
        {
            if (agent < firstCar && _left[agent]) named[count++] = (uint)agent;
        }

        TakeBackTheNamed(town, ticked, count);
    }

    /// <summary>One walker left on the device taken back, in a submit of its own — for a town about to change it. Nothing for one it holds.</summary>
    public void TakeBackTheLeft(TownWorld town, DeviceWalkerTick ticked, int person)
    {
        if (!Keeps || !_left[person]) return;

        _keptNamed.Span<uint>()[0] = (uint)person;
        TakeBackTheNamed(town, ticked, 1);
    }

    void TakeBackTheNamed(TownWorld town, DeviceWalkerTick ticked, int count)
    {
        if (count == 0) return;

        var commands = _batch.Begin();
        _vk.AfterTheSubmitsBefore(commands);
        _keptHand!.Dispatch(commands, _keptHandSet, Groups(count), (uint)count);
        _batch.SubmitAndWait();
        foreach (ref readonly var row in _keptNamedBack.Span<HandedRow>()[..count]) TakeBack(town, ticked, row.Made, row.Clock);
    }

    /// <summary>
    /// <b>One walker's kept row taken up by the town</b> (<see cref="TownWorld.KeptOnTheDevice"/>) and as what the device
    /// holds, its aim and walk as the walker tick's table holds them (<see cref="RecordTheKeptAims"/>). <b>Its walk's clock</b>
    /// restarts where its way taken is new to the town, as the town's own taking up restarts it — but of one left on the
    /// device, where its <paramref name="clock"/> has run on the way taken since, it is that clock, the device having
    /// restarted it alike (<c>walker_decide.glsl</c>'s <c>decideRestarted</c>).
    /// </summary>
    void TakeBack(TownWorld town, DeviceWalkerTick ticked, in WalkerOut made, in WalkerClock clock)
    {
        var person = (int)made.Walker;
        ref var known = ref _known[person];
        known.DestinationM = made.DestinationM;
        known.BackToM = made.BackToM;
        known.OnWayM = made.OnWayM;
        known.OffWayM = made.OffWayM;
        known.GrantM = made.GrantM;
        known.PassFromM = made.PassFromM;
        known.PassClearM = made.PassClearM;
        known.PassAsideM = made.PassAsideM;
        known.PassSlot = made.PassSlot;
        known.RouteTaken = made.RouteTaken;
        known.OnWay = made.OnWay;
        known.OnCrossing = made.OnCrossing;
        known.HeldAs = made.HeldAs;
        known.Flags = made.Flags;
        known.Action = made.Action;
        known.Hold = made.Hold;
        var ranOn = _left[person] && clock.RouteTakenAt == made.RouteTaken;
        town.KeptOnTheDevice(
            person,
            new KeptWalker(
                made.DestinationM, made.BackToM, made.OnWayM, made.OffWayM, made.GrantM,
                new Sidestep(made.PassSlot, made.PassFromM, made.PassClearM, made.PassAsideM, known.Is(WalkerRow.PassBegun)),
                made.RouteTaken, made.OnWay, made.OnCrossing, made.HeldAs,
                known.Is(WalkerRow.HeldAsDriving) ? LaneRoster.Driving : LaneRoster.Walking, known.Is(WalkerRow.Walking),
                (PersonAction)made.Action, (made.Events & WalkerRow.MadeItsPass) != 0, (made.Events & WalkerRow.WithdrewItsPass) != 0),
            restartsTheWalk: !ranOn);
        if (ranOn) town.WalkClocks.Take(person, clock.SinceS, clock.ClosestM);

        ticked.KnowTheAim(person, made.DestinationM, known.Is(WalkerRow.Walking));
        if (!_left[person]) return;

        _left[person] = false;
        KeptLeft--;
    }

    /// <summary>How many walkers the town would lay other than the device is known to hold them, but for what is never compared.</summary>
    int Unlike(TownWorld town)
    {
        NameTheOfficers(town);
        var unlike = 0;
        for (var person = 0; person < _walkerCount; person++)
        {
            var row = RowOf(town, person);
            if (!SameToTheClaims(row, _known[person])) unlike++;
        }

        return unlike;
    }
}
