using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The town's walkers ticked on the device</b> (<c>world/town/kernels/walkers_tick.comp</c>): each walker's own tick and a
/// casualty's slide, over a walker table the device keeps from one tick to the next, leaving each walker's impulse where
/// the bodies' phase spends it and reads the walker back into the table after the step (<see cref="DeviceBodyPhase"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Run in shadow</b> (<see cref="Lay"/>, <see cref="Disagreement"/>): the town still ticks its walkers, and the
/// device is handed every walker as the town's tick reads it. <see cref="TickMs"/> is the kernel alone.
/// </para>
/// <para>
/// <b>Resident, for a town whose walkers the device ticks</b> (<see cref="LayWhatChanged"/>): the table is the device's, and
/// only a walker whose row or impulse the town holds other than the device is known to hold is written over it, whole
/// (<c>walkers_patch.comp</c>); what comes back is each walker's declared pace, impulse and heading
/// (<see cref="TicksAfter"/>), and where its body's step left it is learnt as the town takes it (<see cref="KnowTheBody"/>).
/// </para>
/// </remarks>
internal sealed partial class DeviceWalkerTick : IDisposable
{
    /// <summary>A walker as the device keeps it (<c>WalkerTableRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkerRow(
        Vector2 PositionM, Vector2 VelocityMps, Vector2 DeclaredMps, Vector2 AimM, float HeadingRad, float MassKg, uint Flags,
        float DistanceWalkedM)
    {
        public const uint Walking = 1u, Wounded = 2u, Inside = 4u, Paused = 8u;
    }

    /// <summary>What the town reads of a walker's tick (<c>walkers_tick.comp</c>'s <c>WalkerTickOut</c>), and which tick wrote it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct TickOut(Vector2 DeclaredMps, Vector2 ImpulseNs, float HeadingRad, uint Tick = 0u);

    /// <summary>One walker written whole over the device's table (<c>walkers_patch.comp</c>'s <c>WalkerPatch</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Patch(WalkerRow Row, Vector2 ImpulseNs, uint Walker, uint Pad = 0u);

    /// <summary>The kernel's figures, field for field with <c>walkers_tick.comp</c>'s, each the host's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Figures(
        uint WalkerCount, float DtS, float WalkSpeedMps, float TurnRateDegPerS, float FootGripMps2, float SlidingGripMps2, uint Tick)
    {
        public static Figures Of(SimConfig config) => new(
            0u, config.TickSeconds, config.PersonWalkSpeedMps, config.PersonTurnRateDegPerS, config.PersonFootGripMps2,
            config.PersonSlidingGripMps2, 0u);
    }

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly Figures _figures;
    readonly ResidentRows<WalkerRow> _walkers;
    readonly ResidentRows<Vector2> _impulses;
    readonly ResidentRows<int> _bodies;

    /// <summary>What each walker's tick wrote, in the host's own memory, where its tick wrote anything (<c>walkers_tick.comp</c>).</summary>
    readonly GpuBuffer _ticksOut;

    readonly ComputeKernel _kernel;
    readonly DescriptorSet _set;
    readonly GpuTimer _timer;
    int _count;

    /// <summary>The stamp the last tick wrote its rows with, never the zero a row never written holds.</summary>
    uint _tick;

    /// <summary>Resident: the patches staged and on the device, and the kernel that writes them.</summary>
    readonly GpuBuffer? _patchesIn, _patches;

    readonly ComputeKernel? _patchKernel;
    readonly DescriptorSet _patchSet;
    int _patchCount;

    /// <summary>Resident: every walker's row and impulse as the device is known to hold them, and whether it is known at all.</summary>
    readonly WalkerRow[] _known = [];

    readonly Vector2[] _knownImpulse = [];
    readonly bool[] _isKnown = [];

    /// <summary>Checked: every walker as the town laid it this tick, and the table as the tick read it, copied back.</summary>
    WalkerRow[]? _laidForCheck;

    GpuBuffer? _readForCheck;
    bool _checks;

    /// <param name="resident">Whether the table is the device's, written over only where the town changed a walker.</param>
    public DeviceWalkerTick(Vk vk, SimConfig config, int capacity, bool resident = false)
    {
        _vk = vk;
        _config = config;
        _figures = Figures.Of(config);
        Capacity = capacity;
        Resident = resident;
        _walkers = new ResidentRows<WalkerRow>(vk, capacity, 1, readsBack: true);
        _impulses = new ResidentRows<Vector2>(vk, capacity, 1, readsBack: true);
        _bodies = new ResidentRows<int>(vk, capacity, 1, hostOnly: true);
        _ticksOut = vk.CreateBuffer(
            (ulong)(Math.Max(1, capacity) * Marshal.SizeOf<TickOut>()), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _kernel = new ComputeKernel(vk, "walkers_tick.comp", 3, (uint)Marshal.SizeOf<Figures>());
        _set = _kernel.Bind([_walkers.Device, _impulses.Device, _ticksOut]);
        _timer = new GpuTimer(vk, 2);
        if (!resident) return;

        var bytes = (ulong)(Math.Max(1, capacity) * Marshal.SizeOf<Patch>());
        _patchesIn = vk.CreateBuffer(bytes, BufferUsageFlags.TransferSrcBit, HostAccess.Writes);
        _patches = vk.CreateBuffer(bytes, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        _patchKernel = new ComputeKernel(vk, "walkers_patch.comp", 3, sizeof(uint));
        _patchSet = _patchKernel.Bind([_patches, _walkers.Device, _impulses.Device]);
        _known = new WalkerRow[capacity];
        _knownImpulse = new Vector2[capacity];
        _isKnown = new bool[capacity];
        _townHolds = new AgentSet(capacity);
        _walked = new AgentSet(capacity);
        _heldSinceTheLaying = new AgentSet(capacity);
        _setSinceTheFrame = new AgentSet(capacity);
        _takenBack = new AgentSet(capacity);
        _walk = new int[capacity];
    }

    /// <summary>The most walkers the table holds.</summary>
    public int Capacity { get; }

    /// <summary>Whether the table is the device's, written over only where the town changed a walker (<see cref="LayWhatChanged"/>).</summary>
    public bool Resident { get; }

    /// <summary>How many walkers the last tick was run for.</summary>
    public int Count => _count;

    /// <summary>The walker table, for the bodies' phase that spends its impulses and reads it back.</summary>
    public GpuBuffer Table => _walkers.Device;

    /// <summary>Each walker's impulse, for the bodies' phase that spends it.</summary>
    public GpuBuffer ImpulseTable => _impulses.Device;

    /// <summary>Which body each walker is, for the bodies' phase.</summary>
    public GpuBuffer BodyTable => _bodies.Device;

    /// <summary>The walkers' tick's tables set on the resident town's root (<c>town_rows.glsl</c>): the table, impulses and bodies.</summary>
    public void Register(TownRoot root)
    {
        root.Set(TownRows.PeopleTable, _walkers.Device);
        root.Set(TownRows.PeopleImpulses, _impulses.Device);
        root.Set(TownRows.PeopleBodies, _bodies.Device);
    }

    /// <summary>Every walker as the last read left it — not resident.</summary>
    public ReadOnlySpan<WalkerRow> WalkersAfter => _walkers.AllRead[.._count];

    /// <summary>Every walker's impulse as the last read left it — not resident.</summary>
    public ReadOnlySpan<Vector2> ImpulsesAfter => _impulses.AllRead[.._count];

    /// <summary>
    /// Resident: what every walker's last tick declared, left and faced — where it wrote anything (<see cref="WroteLastTick"/>);
    /// a row it did not write holds what an earlier tick did.
    /// </summary>
    public ReadOnlySpan<TickOut> TicksAfter => _ticksOut.Span<TickOut>()[.._count];

    /// <summary>Whether the last tick wrote this row of <see cref="TicksAfter"/>: elsewhere it changed nothing the town holds.</summary>
    public bool WroteLastTick(in TickOut row) => row.Tick == _tick;

    /// <summary>The impulse a walker's last tick left on the device, however it was handed back.</summary>
    public Vector2 ImpulseAfter(int walker) => Resident ? _ticksOut.Span<TickOut>()[walker].ImpulseNs : _impulses.AllRead[walker];

    /// <summary>How many walkers the last tick was written over whole, resident.</summary>
    public int Patched => _patchCount;

    /// <summary>
    /// <b>Resident: whether to hold the table, as every tick reads it, to every walker as the town laid it that tick</b>
    /// (<see cref="TickReadUnlike"/>) — a check of what the device is known to hold, at the cost of the table copied back
    /// every tick.
    /// </summary>
    public bool ChecksWhatTheTickReads
    {
        get => _checks;
        set
        {
            _checks = value;
            if (!value || _readForCheck != null) return;

            _laidForCheck = new WalkerRow[Capacity];
            _readForCheck = _vk.CreateBuffer(
                (ulong)(Math.Max(1, Capacity) * Marshal.SizeOf<WalkerRow>()), BufferUsageFlags.TransferDstBit, HostAccess.Reads);
        }
    }

    /// <summary>How many walkers a tick read other than the town laid them, but for how far each has walked, over every tick checked.</summary>
    public long TickReadUnlike { get; private set; }

    /// <summary>The device's own time for the last tick's kernel, in milliseconds.</summary>
    public double TickMs => _timer.BetweenMs(0, 1);

    /// <summary>
    /// <b>Resident: every walker the town holds other than the device is known to</b> — where it stands and moves, its pace,
    /// aim, heading, mass and flags, or its impulse — staged to be written whole over the device's table, and which body each
    /// is, sent only where that changed.
    /// </summary>
    /// <returns>How many walkers the town has.</returns>
    public int LayWhatChanged(TownWorld town)
    {
        var people = town.People;
        var patches = _patchesIn!.Span<Patch>();
        var paused = town.HoldAgents;
        _patchCount = 0;

        // A walker added is the town's own; only one the town holds or touched can be other than the device is known to hold.
        for (var person = _walkersAdded; person < people.Count; person++) _townHolds.Add(person);
        _walkersAdded = people.Count;
        var every = VisitsEveryWalker || paused != _paused;
        VisitsEveryWalker = false;
        _paused = paused;
        var walk = _walk.AsSpan(0, every ? EveryWalker(people.Count) : _townHolds.Union(town.TouchedWalkers, people.Count, _walk));
        _walkedCount = walk.Length;
        town.TouchedWalkers.Clear();
        _walked.Clear();
        foreach (var person in walk)
        {
            _walked.Add(person);
            Set(person);
            var row = RowOf(people, person, paused);
            var impulseNs = town.WalkerImpulseNs(person);
            _bodies.Lay(person, people.Body[person].Index);
            if (_isKnown[person] && !(SameToTheTick(row, _known[person]) && impulseNs == _knownImpulse[person]))
            {
                // Written over the device's row whole, it is written where the device's last step left it.
                town.TakeThePoseBack(person);
                row = RowOf(people, person, paused);
            }

            if (_checks) _laidForCheck![person] = row;
            if (_isKnown[person] && SameToTheTick(row, _known[person]) && impulseNs == _knownImpulse[person]) continue;

            patches[_patchCount++] = new Patch(row, impulseNs, (uint)person);
            _known[person] = row;
            _knownImpulse[person] = impulseNs;
            _isKnown[person] = true;
        }

        if (ChecksTheVisits && !every) HoldTheUnwalked(town, paused);

        return people.Count;
    }

    /// <summary>
    /// Every walker <see cref="LayWhatChanged"/> did not walk held to what the device is known to hold of it, its impulse
    /// with it, and to a walker the town neither reads nor walks under a hand: one the town changed while the device kept
    /// it, unmarked (<see cref="TownWorld.TouchedWalkers"/>), throws.
    /// </summary>
    void HoldTheUnwalked(TownWorld town, bool paused)
    {
        var people = town.People;
        for (var person = 0; person < people.Count; person++)
        {
            if (_walked.Has(person)) continue;

            var row = RowOf(people, person, paused);
            var unlike =
                !_isKnown[person] ? "whether the device is known to hold it"
                : !SameButTheBody(row, _known[person]) ? $"its row, {row} against {_known[person]}"
                : town.WalkerImpulseNs(person) != _knownImpulse[person] ? "its impulse"
                : town.ReadsTheWalkersTick(person) ? "whether the town reads it"
                : town.WalksUnderAHand(person) ? "whether a hand walks it"
                : null;
            if (unlike is null) continue;

            throw new InvalidOperationException(
                $"Walker {person} was changed by the town while the device kept it, and not marked touched: {unlike}.");
        }
    }

    /// <summary>
    /// <b>Whether the next <see cref="LayWhatChanged"/> walks every walker</b>: set where what each is read for changed at
    /// once — a watch begun or let go — and cleared by it; the agents held or let go walk every walker too.
    /// </summary>
    public bool VisitsEveryWalker { get; set; } = true;

    /// <summary>
    /// <b>Whether to hold every walker <see cref="LayWhatChanged"/> did not walk to what the device is known to hold</b>, and
    /// throw where it differs (<see cref="HoldTheUnwalked"/>).
    /// </summary>
    public bool ChecksTheVisits { get; set; } = DeviceCarTick.ChecksTheVisitsInEveryTown;

    /// <summary>Which walkers the town holds as the device last left them — their pace, heading and impulse — a bit a walker.</summary>
    AgentSet _townHolds = null!;

    /// <summary>Which walkers the last <see cref="LayWhatChanged"/> walked, a bit a walker and in order, and how many.</summary>
    AgentSet _walked = null!;

    int[] _walk = [];
    int _walkedCount;

    /// <summary>How many walkers the table had when <see cref="LayWhatChanged"/> last took the added in, and whether the agents were held.</summary>
    int _walkersAdded;

    bool _paused;

    /// <summary>The walkers the last <see cref="LayWhatChanged"/> walked, in order: every walker whose row the town may have set.</summary>
    public ReadOnlySpan<int> Walked => _walk.AsSpan(0, _walkedCount);

    int EveryWalker(int count) => EveryWalker(count, _walk);

    static int EveryWalker(int count, Span<int> into)
    {
        for (var person = 0; person < count; person++) into[person] = person;
        return count;
    }

    /// <summary>Whether the town holds this walker's pace, heading and impulse as the device last left them.</summary>
    public bool TownHolds(int walker) => _townHolds.Has(walker);

    /// <summary>
    /// Whether the device is known to hold a walker as the town has it — nothing the town set of it waiting to be laid — so
    /// it may be left with the device.
    /// </summary>
    public bool HoldsAsTheTownHas(TownWorld town, int walker) =>
        _isKnown[walker] && SameButTheBody(RowOf(town.People, walker, town.HoldAgents), _known[walker])
        && town.WalkerImpulseNs(walker) == _knownImpulse[walker];

    /// <summary>
    /// Whether two rows are one to the tick but for where the walker stands and how it moves, which are its body's — taken
    /// back with its pose (<see cref="DeviceTownBodies.TakeThePoseBack"/>) and stale in the town where it is not read.
    /// </summary>
    static bool SameButTheBody(WalkerRow one, WalkerRow other) =>
        SameToTheTick(one with { PositionM = default, VelocityMps = default }, other with { PositionM = default, VelocityMps = default });

    /// <summary>A walker the town reads, or took back since: what the device holds of it is the town's again.</summary>
    public void TookBack(int walker) => Hold(walker);

    /// <summary>A walker the town took up anything of the claims' since the last laying, to be laid again with the next (<see cref="TheWalkersSetSinceTheLaying"/>).</summary>
    public void LayAgainWithTheClaims(int walker) => Set(walker);

    /// <summary>A walker the town holds from here on, and so one it may set anything of before the next laying.</summary>
    void Hold(int walker)
    {
        _townHolds.Add(walker);
        Set(walker);
    }

    /// <summary>A walker the town may have set anything of, for the next laying and the next frame.</summary>
    void Set(int walker)
    {
        _heldSinceTheLaying.Add(walker);
        _setSinceTheFrame.Add(walker);
    }

    /// <summary>
    /// <b>The walkers the town may have set anything of since the frame last asked</b> (<see cref="DeviceSprites.Lay"/>),
    /// added to <paramref name="into"/>: those walked or held since, and those it holds or touched now. The walked and held
    /// are forgotten once asked.
    /// </summary>
    public void TheWalkersSetSinceTheFrame(TownWorld town, AgentSet into)
    {
        into.UnionWith(_setSinceTheFrame);
        into.UnionWith(_townHolds);
        into.UnionWith(town.TouchedWalkers);
        _setSinceTheFrame.Clear();
    }

    /// <summary>The walkers walked or held since <see cref="TheWalkersSetSinceTheFrame"/> last asked.</summary>
    AgentSet _setSinceTheFrame = null!;

    /// <summary>A walker the town does not read after this tick: its pace, heading and impulse stay the device's.</summary>
    public void LeftOnTheDevice(int walker) => _townHolds.Remove(walker);

    /// <summary>
    /// The walkers the last <see cref="LayWhatChanged"/> walked, and those the town holds now — those whose tick the town
    /// may read, and whose body and impulse it may have set — or every walker, where the next walks every one; in order,
    /// written into <paramref name="into"/>.
    /// </summary>
    public Span<int> TheWalkersTheTownMayHaveSet(int count, Span<int> into)
    {
        if (VisitsEveryWalker) return into[..EveryWalker(count, into)];

        _takenBack.CopyFrom(_walked);
        _takenBack.UnionWith(_townHolds);
        return into[.._takenBack.CopyTo(count, into)];
    }

    /// <summary>The walkers the town holds as the device left them, in order, written into <paramref name="into"/> — whose poses it reads.</summary>
    public Span<int> TheWalkersTheTownHolds(int count, Span<int> into) => into[.._townHolds.CopyTo(count, into)];

    /// <summary>
    /// <b>The walkers the town may have set anything of since the last laying of the claims asked</b>, in order: those it
    /// walked or held since — if only between two walks, taken back for a decision and left again — those it holds or
    /// touched now, and those added since; or every walker, where the next walks every one. Every other is as that laying
    /// left it. The held and walked are forgotten once asked.
    /// </summary>
    public ReadOnlySpan<int> TheWalkersSetSinceTheLaying(TownWorld town, int count, Span<int> into)
    {
        if (VisitsEveryWalker) return into[..EveryWalker(count, into)];

        _takenBack.CopyFrom(_heldSinceTheLaying);
        _takenBack.UnionWith(_townHolds);
        var added = Math.Min(_walkersAdded, count);
        var named = _takenBack.Union(town.TouchedWalkers, added, into);
        for (var person = added; person < count; person++) into[named++] = person;
        _heldSinceTheLaying.Clear();
        return into[..named];
    }

    /// <summary>
    /// The walkers <see cref="LayWhatChanged"/> walked or the town held since <see cref="TheWalkersSetSinceTheLaying"/> last
    /// asked.
    /// </summary>
    AgentSet _heldSinceTheLaying = null!;

    AgentSet _takenBack = null!;

    /// <summary>
    /// Resident: what the tick handed back of one walker taken as what the device holds — its pace, impulse and heading —
    /// where its tick last wrote them.
    /// </summary>
    public void KnowTheTick(int walker)
    {
        ref readonly var tick = ref TicksAfter[walker];
        if (!WroteLastTick(tick)) return;

        _known[walker] = _known[walker] with { DeclaredMps = tick.DeclaredMps, HeadingRad = tick.HeadingRad };
        _knownImpulse[walker] = tick.ImpulseNs;
    }

    /// <summary>
    /// <b>One walker the device kept taken back as its ticks last left it</b>: the pace, heading and impulse its last tick
    /// to write any wrote — what the town read of every walker while every one came back — taken as what the device holds.
    /// Nothing for one the town holds.
    /// </summary>
    public void TakeTheTickBack(TownWorld town, int walker)
    {
        if (!Resident || walker >= _count || _townHolds.Has(walker)) return;

        Hold(walker);
        ref readonly var tick = ref TicksAfter[walker];
        if (tick.Tick == 0u) return;

        var people = town.People;
        people.HeadingRad[walker] = tick.HeadingRad;
        people.DeclaredMps[walker] = tick.DeclaredMps;
        town.WalkerImpulseFor(walker) = tick.ImpulseNs;
        _known[walker] = _known[walker] with { DeclaredMps = tick.DeclaredMps, HeadingRad = tick.HeadingRad };
        _knownImpulse[walker] = tick.ImpulseNs;
    }

    /// <summary>Every walker the device kept taken back as its ticks last left it (<see cref="TakeTheTickBack"/>), for a town about to set any.</summary>
    public void TakeEveryTickBack(TownWorld town)
    {
        for (var walker = 0; walker < _count; walker++) TakeTheTickBack(town, walker);
    }

    /// <summary>
    /// <b>Every walker the device kept read as its ticks last left it</b>, for a town about to read every one and set none:
    /// written into the town as <see cref="TakeTheTickBack"/> writes it, and left with the device all the same.
    /// </summary>
    public void ReadEveryTickBack(TownWorld town)
    {
        if (!Resident) return;

        var people = town.People;
        var ticks = TicksAfter;
        for (var walker = 0; walker < _count; walker++)
        {
            if (_townHolds.Has(walker) || ticks[walker].Tick == 0u) continue;

            ref readonly var tick = ref ticks[walker];
            people.HeadingRad[walker] = tick.HeadingRad;
            people.DeclaredMps[walker] = tick.DeclaredMps;
            town.WalkerImpulseFor(walker) = tick.ImpulseNs;
            _known[walker] = _known[walker] with { DeclaredMps = tick.DeclaredMps, HeadingRad = tick.HeadingRad };
            _knownImpulse[walker] = tick.ImpulseNs;
        }
    }

    /// <summary>Checked: what the tick read of every walker the town holds held to what it laid, over the last tick.</summary>
    public void CheckTheTick()
    {
        if (_checks) TickReadUnlike += UnlikeToTheTick();
    }

    /// <summary>How many walkers the town laid that the table held, as the last tick read it, other than laid.</summary>
    int UnlikeToTheTick()
    {
        var read = _readForCheck!.Span<WalkerRow>();
        var unlike = 0;
        foreach (var walker in Walked)
        {
            if (walker < _count && !SameToTheTick(read[walker], _laidForCheck![walker])) unlike++;
        }

        return unlike;
    }

    /// <summary>
    /// Resident: where the bodies' step left a walker the device's table holds in the world (<c>bodies_walkers_mirror.comp</c>),
    /// as the town has just taken it — taken as what the device holds, so it is not laid back over the table. A walker the
    /// town has not taken is known where it was last taken, as the town holds it.
    /// </summary>
    public void KnowTheBody(int walker, Vector2 positionM, Vector2 velocityMps)
    {
        if (!Resident || walker >= _count || (_known[walker].Flags & WalkerRow.Inside) != 0) return;

        _known[walker] = _known[walker] with { PositionM = positionM, VelocityMps = velocityMps };
    }

    /// <summary>
    /// Resident: a walker's aim and whether it walks, as a rebuild that keeps the walkers wrote them into the table
    /// (<c>walkers_kept_out.comp</c>), taken as what the device holds.
    /// </summary>
    public void KnowTheAim(int walker, Vector2 aimM, bool walking)
    {
        if (!Resident || !_isKnown[walker]) return;

        ref var known = ref _known[walker];
        known = known with { AimM = aimM, Flags = walking ? known.Flags | WalkerRow.Walking : known.Flags & ~WalkerRow.Walking };

        // Taken up after the tick was laid (DeviceRebuild.AnswersDue), and written over what was laid before it ran.
        if (!_checks) return;

        ref var laid = ref _laidForCheck![walker];
        laid = laid with { AimM = aimM, Flags = walking ? laid.Flags | WalkerRow.Walking : laid.Flags & ~WalkerRow.Walking };
    }

    /// <summary>
    /// Resident: whether the device's table is known to hold this walker's body as the town has it — where it stands, how it
    /// moves, the pace it declared and the way it faces — for whoever reads them off the table in the town's place.
    /// </summary>
    public bool HoldsTheBodyOf(Agents.Person.Body.PersonFleet people, int person)
    {
        if (!Resident || !_isKnown[person]) return false;

        ref readonly var known = ref _known[person];
        return known.PositionM == people.PositionM[person] && known.VelocityMps == people.VelocityMps[person]
               && known.DeclaredMps == people.DeclaredMps[person] && known.HeadingRad.Equals(people.HeadingRad[person]);
    }

    /// <summary>
    /// <b>One tick of the first <paramref name="walkers"/> walkers recorded into a caller's submit</b>: what was marked
    /// sent — or, resident, every walker staged written over the table — and the kernel run, leaving its writes for the caller
    /// to barrier before whatever reads them.
    /// </summary>
    public void Record(CommandBuffer commands, int walkers)
    {
        RecordWhatWasLaid(commands, walkers);
        RecordTheTick(commands);
    }

    /// <summary>
    /// The first half of <see cref="Record"/>: what was marked sent — or, resident, every walker staged written over the
    /// table — leaving the table for whatever else writes it before the tick (<see cref="RecordTheTick"/>) to barrier after.
    /// </summary>
    public void RecordWhatWasLaid(CommandBuffer commands, int walkers)
    {
        if (walkers > Capacity) throw new ArgumentOutOfRangeException(nameof(walkers), $"The table holds {Capacity} walkers, not {walkers}.");

        _count = walkers;
        if (Resident)
        {
            _vk.Copy(commands, _patchesIn!, _patches!, (ulong)(_patchCount * Marshal.SizeOf<Patch>()));
            _bodies.Send(commands);
            _vk.AfterTransfer(commands);
            if (_patchCount > 0)
            {
                _patchKernel!.Dispatch(commands, _patchSet, (uint)((_patchCount + 63) / 64), (uint)_patchCount);
                _vk.BetweenKernels(commands);
            }
        }
        else
        {
            _walkers.Send(commands);
            _impulses.Send(commands);
            _bodies.Send(commands);
            _vk.AfterTransfer(commands);
        }
    }

    /// <summary>The second half of <see cref="Record"/>: the kernel run over the table as it now stands.</summary>
    public void RecordTheTick(CommandBuffer commands)
    {
        var walkers = _count;
        if (Resident && _checks)
        {
            _vk.BeforeTransfer(commands);
            _vk.Copy(commands, _walkers.Device, _readForCheck!, (ulong)(walkers * Marshal.SizeOf<WalkerRow>()));
            _vk.AfterTransfer(commands);
        }

        _tick++;
        _timer.Reset(commands);
        _timer.Stamp(commands, 0);
        _kernel.Dispatch(
            commands, _set, (uint)Math.Max(1, (walkers + 63) / 64), _figures with { WalkerCount = (uint)walkers, Tick = _tick });
        _timer.Stamp(commands, 1);
    }

    /// <summary>
    /// What the walkers' tick and the bodies' phase after it left, recorded to be handed back — resident, nothing: the tick
    /// wrote what the town reads into the host's memory itself (<see cref="TicksAfter"/>).
    /// </summary>
    public void RecordRead(CommandBuffer commands)
    {
        _vk.BeforeTransfer(commands);
        if (Resident) return;

        _walkers.Read(commands, _count);
        _impulses.Read(commands, _count);
    }

    /// <summary>The device's stamps of the last tick taken in, once its submit has been waited for.</summary>
    public void ReadTimes() => _timer.Read();

    /// <summary>One walker as the town's tick reads it.</summary>
    static WalkerRow RowOf(Agents.Person.Body.PersonFleet people, int person, bool paused)
    {
        var flags = (people.Walking[person] ? WalkerRow.Walking : 0u) | (people.Wounded[person] ? WalkerRow.Wounded : 0u)
                    | (people.Inside[person].Any ? WalkerRow.Inside : 0u) | (paused ? WalkerRow.Paused : 0u);
        return new WalkerRow(
            people.PositionM[person], people.VelocityMps[person], people.DeclaredMps[person], people.DestinationM[person],
            people.HeadingRad[person], people.MassKg[person], flags, people.DistanceWalkedM[person]);
    }

    /// <summary>
    /// Whether two rows are one to the walker's tick, bit for bit: everything it reads — not how far the walker has walked,
    /// which the device and the town each add up their own way and the tick never reads.
    /// </summary>
    static bool SameToTheTick(in WalkerRow one, in WalkerRow other) =>
        MemoryMarshal.AsBytes(new ReadOnlySpan<WalkerRow>(in one))[..44].SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<WalkerRow>(in other))[..44]);

    public void Dispose()
    {
        _timer.Dispose();
        _kernel.Dispose();
        _patchKernel?.Dispose();
        _patches?.Dispose();
        _readForCheck?.Dispose();
        _patchesIn?.Dispose();
        _walkers.Dispose();
        _impulses.Dispose();
        _bodies.Dispose();
        _ticksOut.Dispose();
    }
}
