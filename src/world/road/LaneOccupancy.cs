namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>Which roster a holder is named in.</b> The road's claims are not one roster's — a person in a lane is on the road
/// like anything else — so a holder is an integer into one of the fleets, and <b>which one is carried rather than
/// inferred</b>.
/// </summary>
internal enum LaneRoster : byte
{
    Driving,

    Walking,

    /// <summary>
    /// <b>The lights</b> (TLT-1): a holder is one stretch a light holds, and never a body — a light has no collider and
    /// stands on no way.
    /// </summary>
    Signal,
}

/// <summary><b>What a claim is</b>: ground a body stands on, or ground somebody means to use.</summary>
internal enum ClaimKind : byte
{
    /// <summary>
    /// <b>Bodies standing on the way</b> (TER-4c.2): every body over one stretch, merged into one claim with all of them
    /// as its holders.
    /// </summary>
    Body,

    /// <summary>
    /// <b>Ground one holder means to use down its own line or walk</b> (TER-4c.1): one holder, overlapping no other main
    /// claim.
    /// </summary>
    Main,

    /// <summary>
    /// <b>The section of another way a mark links a main claim to, or a light's hold</b> (TER-5c.1, TLT-1): every one of
    /// one rung over a stretch, merged into one claim.
    /// </summary>
    Secondary,
}

/// <summary>
/// <b>One claim on one stretch of one way</b>, in decimetres of the way's own arclength from its start: what it is, how
/// strong, and how many laid it.
/// </summary>
/// <remarks>
/// <b>Half-open</b>, so one claim may end on the very decimetre the next begins. <b>Who laid it is for the instruments
/// alone</b> (<see cref="LaneOccupancy.HoldersOf"/>): a claim is what makes others act, and nothing that decides reads its
/// holders.
/// </remarks>
/// <param name="Rung"><see cref="ClaimPriority.Hard"/> for bodies; the rung it was taken at for anything else.</param>
/// <param name="Holders">How many laid it: one for a main claim.</param>
/// <param name="HoldersFrom">Where its holders begin in <see cref="LaneOccupancy.HoldersOf"/>'s table.</param>
internal readonly record struct Claim(
    ushort FromDm, ushort ToDm, ClaimPriority Rung, ClaimKind Kind, ushort Holders, int HoldersFrom)
{
    public float FromM => LaneOccupancy.MetresOf(FromDm);

    public float ToM => LaneOccupancy.MetresOf(ToDm);
}

/// <summary>
/// <b>What a hold came to</b>: where on its holder's line it ends, and the place, rung and kind of what refused it there —
/// never who.
/// </summary>
/// <param name="CutLineM">Where on the holder's line the hold ends; infinity where it had all it asked for.</param>
/// <param name="Way">The way it was refused on, or <see cref="LaneOccupancy.NoWay"/>.</param>
/// <param name="AtDm">Where on that way.</param>
internal readonly record struct Refusal(float CutLineM, int Way, ushort AtDm, ClaimPriority Rung, ClaimKind Kind)
{
    public static Refusal Whole => new(float.PositiveInfinity, LaneOccupancy.NoWay, 0, ClaimPriority.Hard, ClaimKind.Main);

    public bool Refused => !float.IsPositiveInfinity(CutLineM);

    public float AtM => LaneOccupancy.MetresOf(AtDm);

    /// <summary>Whether a light's hold refused it (TLT-1).</summary>
    public bool ByALight => Refused && Kind != ClaimKind.Body && Rung == ClaimPriority.Signal;
}

/// <summary>
/// <b>Who stands on each way and who means to use it, as runs of decimetres of the way's own arclength</b> — and, by
/// TER-4c, <b>the whole of what an agent looks at</b>. Three runs a way: the bodies, the main claims and the secondary
/// claims, each sorted by its near edge.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bodies are laid first</b> (TER-4c.2), merged where they overlap, and never compared, cut or taken.
/// </para>
/// <para>
/// <b>Every plan is then asked as wishes and taken in one pass, strongest rung first</b> (TER-4c.1, TER-5g): a hold's
/// pieces in the order its line runs over them, each main piece split where a mark falls on it into the ground before
/// the mark, the secondary claim over the other way's section, and the ground after (TER-5c.1). <b>A main piece is
/// refused at the first body, main claim or secondary claim of another holder on its way</b>, and keeps the ground short
/// of it; <b>a secondary claim is refused by any main claim over its section</b>, and shares ground with other
/// secondary claims. A refused hold ends there and asks nothing more (TER-5c.2).
/// </para>
/// <para>
/// <b>Exact in one pass</b>: whatever is down when a piece is asked is at least as strong as it, so nothing taken is ever
/// given back, and no answer turns on another being asked again. Of two of one rung, a plan is answered before a hold
/// had whole (<see cref="BeginHold"/>), and otherwise whichever was asked first keeps the ground. That needs a plan's
/// rungs never to strengthen along its line (<c>Following.LevelTheRungs</c>).
/// </para>
/// <para>
/// <b>A holder's own claims never refuse it</b>, and a piece begins past the holder's own body where that stands over
/// its start — a body cuts nothing it is level with. <b>Which holder is a claim's is read here and nowhere that
/// decides</b>: every claim is kept as its one holder laid it, and those of one rung that overlap are one claim where
/// they are read (<see cref="Bodies"/>, <see cref="Secondaries"/>, <see cref="FirstSecondary"/>).
/// </para>
/// <para>
/// <b>Each run is threaded through one table of slots in the order laid</b>, each slot naming the next by near edge: a
/// way's runs are a claim or two long, and threading them spares a pass counting each way's room and another placing
/// its claims in it (road decision log, 2026-10-13).
/// </para>
/// <para>
/// <b>Every edge is rounded up to the next decimetre</b>, so a claim covers up to a decimetre more than asked and one
/// metre always lands on one decimetre. The town's longest way has to fit in a <see cref="ushort"/> of them.
/// </para>
/// <para>
/// <b>It is laid afresh every rebuild and never written to during a decision</b>: an index rather than a register.
/// Nothing is released and nothing can leak. The tables grow to what the busiest laying asks and are kept.
/// </para>
/// </remarks>
internal sealed partial class LaneOccupancy
{
    public const int Nobody = -1;

    public const int NoHold = -1;

    public const int NoWay = -1;

    /// <summary>The holder of nothing, which matches no claim.</summary>
    const int NoKey = -1;

    const float DmPerM = 10f;

    /// <summary>
    /// The float error a decimetre carries to metres and back (0.3 m × 10 is 3.0000001): taken off before rounding up,
    /// so a metre read off a claim names the decimetre it came from.
    /// </summary>
    const float DmGrain = 1e-3f;

    /// <summary>One hold an agent, every one <see cref="NoHold"/> until its first laying.</summary>
    public static int[] NoHolds(int agents)
    {
        var holds = new int[agents];
        Array.Fill(holds, NoHold);
        return holds;
    }

    /// <summary>A metre of a way as decimetres from its start, rounded up.</summary>
    public static ushort DmOf(float m) => (ushort)Math.Clamp(MathF.Ceiling((m * DmPerM) - DmGrain), 0f, ushort.MaxValue);

    public static float MetresOf(ushort dm) => dm / DmPerM;

    /// <summary>One holder as one integer: its index in its roster, and the roster.</summary>
    public static int KeyOf(int occupant, LaneRoster of) => occupant < 0 ? NoKey : (occupant << 2) | (int)of;

    public static int OccupantOf(int key) => key >> 2;

    public static LaneRoster RosterOf(int key) => (LaneRoster)(key & 3);

    readonly TownWays _ways;

    /// <summary><b>Where a main piece's secondary claims go</b> (<see cref="RibbonAtlas.Marks"/>).</summary>
    readonly WayCrossings _marks;

    /// <summary>One mark in decimetres, both sides, rounded as a claim's edges are.</summary>
    readonly record struct MarkDm(int OnWay, ushort FromDm, ushort ToDm, ushort MineFromDm, ushort MineToDm);

    /// <summary>Every mark in decimetres, numbered as <see cref="_marks"/> numbers them — worked out once, as the marks are.</summary>
    readonly MarkDm[] _marksDm;

    ReadOnlySpan<MarkDm> MarksOf(int way) => _marksDm.AsSpan(_marks.FirstOf(way), _marks.Of(way).Length);

    /// <summary><b>One way laid on this rebuild</b>: the first slot of each of its runs.</summary>
    struct WayRow
    {
        public int Bodies;
        public int Mains;
        public int Seconds;
    }

    /// <summary>
    /// <b>One holder's claim in its way's run, linked to the next by near edge</b>: a body as it was laid, a main claim,
    /// or a secondary claim as it was taken. Which run it is threaded on says its kind.
    /// </summary>
    /// <remarks>
    /// <b>Fields, written where it lies</b> (<see cref="NewSlot"/>), as a <see cref="Wish"/> is. <b>Sixteen bytes</b>, so
    /// none straddles a cache line.
    /// </remarks>
    struct Slot
    {
        public ushort FromDm;
        public ushort ToDm;
        public ClaimPriority Rung;
        public int Key;
        public int Next;
    }

    const int NoSlot = -1;

    /// <summary>
    /// <b>Every main and secondary claim held</b>, each way's runs threaded through it — kept from one rebuild to the
    /// next by whichever hold has them, and given back to <see cref="_freeSlots"/> when it lets them go.
    /// </summary>
    Slot[] _slots;
    int _slotCount;

    /// <summary>What a held claim is, besides what a run reads of it: whose, its place in its hold and its run, and where it falls on its holder's line.</summary>
    /// <remarks>Apart from <see cref="Slot"/>, which every question walks, since only letting go and cutting read it.</remarks>
    struct SlotOwner
    {
        public int Hold;

        /// <summary>The hold's next claim, in the order it asked them.</summary>
        public int HoldNext;

        /// <summary>The claim before it in its way's run, or <see cref="NoSlot"/> where it is the first.</summary>
        public int Prev;

        public int Row;
        public ClaimKind Kind;
    }

    SlotOwner[] _owners;

    /// <summary>Slots let go of, taken again before the table grows.</summary>
    int[] _freeSlots;
    int _freeSlotCount;

    /// <summary>
    /// <b>Every body of this rebuild</b>, in the order laid, each way's run of them threaded through it — laid afresh
    /// every rebuild, as the world reads them afresh.
    /// </summary>
    Slot[] _bodies;
    int _bodyCount;

    /// <summary>A claim of one holder, linked to nothing yet.</summary>
    int NewSlot(ushort fromDm, ushort toDm, ClaimPriority rung, int key)
    {
        int at;
        if (_freeSlotCount > 0)
        {
            at = _freeSlots[--_freeSlotCount];
        }
        else
        {
            if (_slotCount == _slots.Length)
            {
                Array.Resize(ref _slots, 2 * _slots.Length);
                Array.Resize(ref _owners, _slots.Length);
            }

            at = _slotCount++;
        }

        ref var slot = ref _slots[at];
        slot.FromDm = fromDm;
        slot.ToDm = toDm;
        slot.Rung = rung;
        slot.Key = key;
        slot.Next = NoSlot;
        return at;
    }

    /// <summary>A body of one holder, linked to nothing yet.</summary>
    int NewBody(ushort fromDm, ushort toDm, int key)
    {
        if (_bodyCount == _bodies.Length) Array.Resize(ref _bodies, 2 * _bodies.Length);

        ref var body = ref _bodies[_bodyCount];
        body.FromDm = fromDm;
        body.ToDm = toDm;
        body.Rung = ClaimPriority.Hard;
        body.Key = key;
        body.Next = NoSlot;
        return _bodyCount++;
    }

    /// <summary>A body threaded into its way's run after every one beginning at or before its near edge.</summary>
    void InsertTheBody(ref int head, ushort fromDm, ushort toDm, int key)
    {
        var body = NewBody(fromDm, toDm, key);
        ref var link = ref head;
        while (link != NoSlot && _bodies[link].FromDm <= fromDm) link = ref _bodies[link].Next;

        _bodies[body].Next = link;
        link = body;
    }

    /// <summary>
    /// A held claim threaded into one of its way's runs after every one beginning at or before its near edge, and onto the
    /// end of its hold's own claims.
    /// </summary>
    int Hold(int row, ClaimKind kind, int hold, ushort fromDm, ushort toDm, ClaimPriority rung, int key)
    {
        var slot = NewSlot(fromDm, toDm, rung, key);
        ref var head = ref kind == ClaimKind.Main ? ref _rows[row].Mains : ref _rows[row].Seconds;
        var prev = NoSlot;
        var next = head;
        while (next != NoSlot && _slots[next].FromDm <= fromDm) (prev, next) = (next, _slots[next].Next);

        _slots[slot].Next = next;
        if (prev == NoSlot) head = slot;
        else _slots[prev].Next = slot;
        if (next != NoSlot) _owners[next].Prev = slot;

        ref var holdRow = ref _holds[hold];
        _owners[slot] = new SlotOwner { Hold = hold, HoldNext = NoSlot, Prev = prev, Row = row, Kind = kind };
        if (holdRow.LastClaim == NoSlot) holdRow.FirstClaim = slot;
        else _owners[holdRow.LastClaim].HoldNext = slot;
        holdRow.LastClaim = slot;
        return slot;
    }

    /// <summary>A held claim taken out of its way's run and given back — its hold's own list left to the caller.</summary>
    void LetGo(int slot)
    {
        ref readonly var owner = ref _owners[slot];
        var next = _slots[slot].Next;
        if (owner.Prev == NoSlot)
        {
            ref var head = ref owner.Kind == ClaimKind.Main ? ref _rows[owner.Row].Mains : ref _rows[owner.Row].Seconds;
            head = next;
        }
        else
        {
            _slots[owner.Prev].Next = next;
        }

        if (next != NoSlot) _owners[next].Prev = owner.Prev;

        if (_freeSlotCount == _freeSlots.Length) Array.Resize(ref _freeSlots, 2 * _freeSlots.Length);
        _freeSlots[_freeSlotCount++] = slot;
    }

    /// <summary>
    /// <b>The ways anything has been laid on, each once, and a row each</b> — numbered as they were first laid on and kept,
    /// so what a laying reads of a way is a few megabytes and not a row of every way in the town.
    /// </summary>
    int[] _touched;
    WayRow[] _rows;
    int _touchedCount;

    /// <summary>Each way's row (<see cref="_rows"/>), or <see cref="NoRow"/>.</summary>
    readonly int[] _rowOf;

    const int NoRow = -1;

    /// <summary>
    /// <b>How many rows of each table are first laid for a holder</b> — about twice what OdesaOsm's busiest laying uses
    /// (`--bench room`): 3.5 slots, 3.1 wishes, 1.2 ways and 0.9 of a driver's body stretch a holder. A start and not a
    /// bound.
    /// </summary>
    const int SlotsPerHolder = 6;

    /// <inheritdoc cref="SlotsPerHolder"/>
    const int WishesPerHolder = 6;

    /// <inheritdoc cref="SlotsPerHolder"/>
    const int WaysPerHolder = 2;

    /// <param name="ways">Every way in the town, in one numbering.</param>
    /// <param name="marks">
    /// <b>Which ways share ground</b> (<see cref="RibbonAtlas.Marks"/>); <see cref="WayCrossings.None"/> for a fixture
    /// whose ways touch nothing.
    /// </param>
    /// <param name="holders">
    /// How many holders the town has — drivers, walkers and lights — which the tables are first laid for, a few claims
    /// each. <b>A start and not a bound</b>: a laying that needs more grows them, and they are kept.
    /// </param>
    public LaneOccupancy(TownWays ways, WayCrossings marks, int holders = 16)
    {
        var room = Math.Max(16, holders);
        _slots = new Slot[SlotsPerHolder * room];
        _owners = new SlotOwner[_slots.Length];
        _freeSlots = new int[_slots.Length];
        _bodies = new Slot[WaysPerHolder * room];
        _own = new OwnStretch[WaysPerHolder * room];
        _ownHeads = new OwnHead[room];
        _holds = new HoldRow[2 * room];
        _holdStates = new HoldState[_holds.Length];
        _wishes = new Wish[WishesPerHolder * room];
        _order = new int[WishesPerHolder * room];
        for (var way = 0; way < ways.Count; way++)
        {
            if (ways.LengthM(way) * DmPerM > ushort.MaxValue)
            {
                throw new ArgumentException(
                    $"way {way} is {ways.LengthM(way):F1} m, past the {ushort.MaxValue / DmPerM:F1} m a claim can name",
                    nameof(ways));
            }
        }

        _ways = ways;
        _marks = marks;
        _marksDm = new MarkDm[marks.All.Length];
        for (var at = 0; at < _marksDm.Length; at++)
        {
            ref readonly var mark = ref marks.All[at];
            _marksDm[at] = new MarkDm(mark.OnWay, DmOf(mark.FromM), DmOf(mark.ToM), DmOf(mark.MineFromM), DmOf(mark.MineToM));
        }

        _rows = new WayRow[Math.Min(ways.Count, WaysPerHolder * room)];
        _touched = new int[_rows.Length];
        _rowOf = new int[ways.Count];
        Array.Fill(_rowOf, NoRow);
    }

    /// <summary>The numbering these claims are laid in.</summary>
    public TownWays Ways => _ways;

    public int WayCount => _ways.Count;

    /// <summary>The marks secondary claims are placed through.</summary>
    public WayCrossings Marks => _marks;

    /// <summary>
    /// The ways anything is laid on, in no order and each once. <b>A reader that wants every claim walks this and not
    /// the town.</b>
    /// </summary>
    public ReadOnlySpan<int> OccupiedWays => _touched.AsSpan(0, _touchedCount);

    public float WayLengthM(int way) => _ways.LengthM(way);

    /// <summary>
    /// <b>A rebuild begun</b>: the bodies laid last time dropped, to be laid again as the world now reads them, and every
    /// hold let go of.
    /// </summary>
    public void Begin()
    {
        foreach (ref var row in _rows.AsSpan(0, _touchedCount)) row.Bodies = NoSlot;

        _bodyCount = 0;
        _laidCount = 0;
        _ownCount = 0;
        _laying++;
        _bodiesClosed = false;
        LetGoOfEveryHold();
        _wishCount = 0;
        _answered = false;
    }

    /// <summary>
    /// <b>Each table's rows used this rebuild and the rows it has</b> — slots, wishes, holds, way rows and own body
    /// stretches — for an instrument weighing what they are laid for (<see cref="SlotsPerHolder"/>).
    /// </summary>
    public (string Table, int Used, int Rows, int RowBytes)[] TablesUsed() =>
    [
        ("slots", _slotCount - _freeSlotCount, _slots.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<Slot>()),
        ("slot owners", _slotCount - _freeSlotCount, _owners.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<SlotOwner>()),
        ("bodies", _bodyCount, _bodies.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<Slot>()),
        ("wishes", _wishCount, _wishes.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<Wish>()),
        ("order", _wishCount, _order.Length, sizeof(int)),
        ("holds", _holdCount, _holds.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<HoldRow>()),
        ("hold states", _holdCount, _holdStates.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<HoldState>()),
        ("way rows", _touchedCount, _rows.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<WayRow>()),
        ("own bodies", _ownCount, _own.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<OwnStretch>()),
        ("own heads", 0, _ownHeads.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<OwnHead>()),
        ("touched", _touchedCount, _touched.Length, sizeof(int)),
        ("row of a way", _rowOf.Length, _rowOf.Length, sizeof(int)),
        ("marks", _marksDm.Length, _marksDm.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<MarkDm>()),
    ];

    /// <summary>One way's row, given it at the first thing ever laid on it.</summary>
    int Touch(int way)
    {
        var row = _rowOf[way];
        if (row != NoRow) return row;

        if (_touchedCount == _rows.Length)
        {
            Array.Resize(ref _rows, Math.Min(_rowOf.Length, 2 * _rows.Length));
            Array.Resize(ref _touched, _rows.Length);
        }

        row = _touchedCount++;
        _rowOf[way] = row;
        _touched[row] = way;
        _rows[row] = new WayRow { Bodies = NoSlot, Mains = NoSlot, Seconds = NoSlot };
        return row;
    }

    /// <summary>One way's row, or <see cref="NoRow"/> where nothing has been laid on it.</summary>
    int RowOf(int way) => _rowOf[way];
}
