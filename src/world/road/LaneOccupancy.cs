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
/// <b>Nothing is laid for the town</b> (root decision log, 2026-10-14): every holder keeps what it holds until it lets go
/// of it, and edits it on its own turn. <b>A body is its holder's own ground</b> (TER-4c.2), laid as it moves
/// (<see cref="PlaceBody"/>), never compared, cut or taken, and cutting any plan of another's it comes to stand on.
/// </para>
/// <para>
/// <b>What is asked is taken in one pass, strongest rung first</b> (TER-4c.1, TER-5g): a hold's
/// pieces in the order its line runs over them, each main piece split where a mark falls on it into the ground before
/// the mark, the secondary claim over the other way's section, and the ground after (TER-5c.1). <b>A main piece is
/// refused at the first body, main claim or secondary claim of another holder on its way</b>, and keeps the ground short
/// of it; <b>a secondary claim is refused by any main claim over its section</b>, and shares ground with other
/// secondary claims. A refused hold ends there and asks nothing more (TER-5c.2).
/// </para>
/// <para>
/// <b>Exact in one pass</b>: of what is asked together, whatever is taken first is at least as strong as what follows, and
/// what is already held and weaker is cut where a stronger piece takes it (<see cref="TakeOver"/>). Of two of one rung,
/// a plan is answered before a hold had whole (<see cref="BeginHold"/>), and otherwise whichever holds it keeps it. That
/// needs a plan's rungs never to strengthen along its line (<c>Following.LevelTheRungs</c>).
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
/// <b>A register and not an index</b>: what is held is held until its holder lets go of it, so a holder that stops
/// claiming lets go of what it holds or it stays. The tables grow to what the town holds and are kept.
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

    /// <summary>One hold an agent, every one <see cref="NoHold"/> until its first turn.</summary>
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

    /// <summary><b>One way anything has been laid on</b>: the first slot of each of its runs.</summary>
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

        /// <summary>A held claim's turn (<see cref="Wish.Turn"/>), in what would be the row's padding.</summary>
        public byte Turn;

        public int Key;
        public int Next;
    }

    const int NoSlot = -1;

    /// <summary>
    /// <b>Every claim held, bodies among them</b>, each way's runs threaded through it — kept by whichever hold has them
    /// until it lets them go, and given back to <see cref="_freeSlots"/> then.
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

        /// <summary>How its place on its holder's line is read, as its wish's was (<see cref="Wish.Line"/>, <see cref="Wish.LineM"/>).</summary>
        public WishLine Line;

        /// <summary>The rung it is held at once its holder can stop short of it again (<see cref="Wish.Planned"/>).</summary>
        public ClaimPriority Planned;

        public float LineM;

        /// <summary>A mark's secondary claim: where on its main claim's way the mark begins (<see cref="Wish.AtDm"/>).</summary>
        public ushort AtDm;

        /// <summary>A mark's secondary claim: the main claim it was taken with; <see cref="NoSlot"/> for anything else.</summary>
        public int Main;
    }

    SlotOwner[] _owners;

    /// <summary>Slots let go of, taken again before the table grows.</summary>
    int[] _freeSlots;
    int _freeSlotCount;

    /// <summary>
    /// <b>The answer each row last had anything laid, taken, cut or let go of on</b> (<see cref="_answers"/>) — so a hold
    /// cut on a row nothing has changed on since is not woken (<see cref="IsWoken"/>).
    /// </summary>
    int[] _claimsChangedAt;

    /// <summary>How many times what was asked has been answered (<see cref="Answer"/>): the clock a change is stamped by.</summary>
    int _answers = 1;

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

    /// <summary>The first slot of one of a row's runs: its bodies, main claims or secondary claims.</summary>
    ref int HeadOf(int row, ClaimKind kind)
    {
        ref var of = ref _rows[row];
        if (kind == ClaimKind.Body) return ref of.Bodies;

        return ref kind == ClaimKind.Main ? ref of.Mains : ref of.Seconds;
    }

    /// <summary>A held claim put on the end of its hold's own list.</summary>
    void OnTheHold(int hold, int slot)
    {
        ref var holdRow = ref _holds[hold];
        if (holdRow.LastClaim == NoSlot) holdRow.FirstClaim = slot;
        else _owners[holdRow.LastClaim].HoldNext = slot;

        holdRow.LastClaim = slot;
    }

    /// <summary>
    /// A held claim threaded into one of its way's runs after every one beginning at or before its near edge, and onto the
    /// end of its hold's own claims, with its wish's place on the holder's line.
    /// </summary>
    /// <param name="main">For a mark's secondary claim, the main claim taken with it; otherwise <see cref="NoSlot"/>.</param>
    int Hold(in Wish wish, ushort fromDm, ushort toDm, int key, int main)
    {
        if (wish.Kind == ClaimKind.Main && OnTheEndOf(wish, fromDm) is var end && end != NoSlot)
        {
            _slots[end].ToDm = toDm;
            _claimsChangedAt[wish.Row] = _answers;
            return end;
        }

        // A plan's claims are held at their planned rung, its committed line saying how much of each is committed.
        var byLine = _holdLife[wish.Hold].ByLine;
        var slot = NewSlot(fromDm, toDm, byLine ? wish.Planned : wish.Rung, key);
        _slots[slot].Turn = byLine ? (byte)(2 * (int)wish.Planned) : wish.Turn;
        _owners[slot] = new SlotOwner
        {
            Hold = wish.Hold, HoldNext = NoSlot, Prev = NoSlot, Row = wish.Row, Kind = wish.Kind, Line = wish.Line,
            Planned = wish.Planned, LineM = wish.LineM, AtDm = wish.AtDm, Main = main,
        };
        Thread(slot);
        OnTheHold(wish.Hold, slot);
        return slot;
    }

    /// <summary>
    /// <b>The main claim of a plan a piece taken carries on</b>: its last, where the piece is taken up its line from
    /// where that one ends, on the same way and planned alike — so a plan asked further a little at a time
    /// (<see cref="AskFurther"/>), and its committed piece and the rest of a way, stay one claim. <see cref="NoSlot"/>
    /// where there is none.
    /// </summary>
    int OnTheEndOf(in Wish wish, ushort fromDm)
    {
        ref readonly var holdRow = ref _holds[wish.Hold];
        if (!_holdLife[wish.Hold].ByLine || holdRow.LastClaim == NoSlot || wish.Line != WishLine.Up) return NoSlot;

        var last = holdRow.LastClaim;
        var end = _owners[last].Kind == ClaimKind.Main ? last : _owners[last].Main;
        if (end == NoSlot) return NoSlot;

        ref readonly var owner = ref _owners[end];
        ref readonly var claim = ref _slots[end];
        return owner.Line == WishLine.Up && owner.Row == wish.Row && owner.LineM == wish.LineM
               && owner.Planned == wish.Planned && claim.Rung == wish.Planned && claim.ToDm == fromDm
            ? end
            : NoSlot;
    }

    /// <summary>A held claim taken out of its way's run and given back — its hold's own list left to the caller.</summary>
    void LetGo(int slot)
    {
        Unthread(slot);
        if (_owners[slot].Kind == ClaimKind.Body) _bodiesLaid--;

        _owners[slot].Hold = NoHold;
        if (_freeSlotCount == _freeSlots.Length) Array.Resize(ref _freeSlots, 2 * _freeSlots.Length);
        _freeSlots[_freeSlotCount++] = slot;
    }

    /// <summary>
    /// <b>The ways anything has been laid on, each once, and a row each</b> — numbered as they were first laid on and kept,
    /// so what answering reads of the ways is a few megabytes and not a row of every way in the town.
    /// </summary>
    int[] _touched;
    WayRow[] _rows;
    int _touchedCount;

    /// <summary>Each way's row (<see cref="_rows"/>), or <see cref="NoRow"/>.</summary>
    readonly int[] _rowOf;

    const int NoRow = -1;

    /// <summary>
    /// <b>How many rows of each table are first laid for a holder</b> — about twice what OdesaOsm's claims used at their
    /// busiest (`--bench room`, 2026-10-13): 3.5 slots and 3.1 wishes a holder. A start and not a bound.
    /// </summary>
    const int SlotsPerHolder = 6;

    /// <inheritdoc cref="SlotsPerHolder"/>
    const int WishesPerHolder = 6;

    /// <summary>
    /// <b>How many ways' rows are first laid for a holder</b>: a way keeps its row from the first thing laid on it, so
    /// this is what the town touches over its life — 3.2 a holder on OdesaOsm (260 000 of its 1.1 million ways). A start
    /// and not a bound.
    /// </summary>
    const int WaysPerHolder = 4;

    /// <param name="ways">Every way in the town, in one numbering.</param>
    /// <param name="marks">
    /// <b>Which ways share ground</b> (<see cref="RibbonAtlas.Marks"/>); <see cref="WayCrossings.None"/> for a fixture
    /// whose ways touch nothing.
    /// </param>
    /// <param name="holders">
    /// How many holders the town has — drivers, walkers and lights — which the tables are first laid for, a few claims
    /// each. <b>A start and not a bound</b>: a turn that needs more grows them, and they are kept.
    /// </param>
    public LaneOccupancy(TownWays ways, WayCrossings marks, int holders = 16)
    {
        var room = Math.Max(16, holders);
        _slots = new Slot[SlotsPerHolder * room];
        _owners = new SlotOwner[_slots.Length];
        _freeSlots = new int[_slots.Length];
        _bodyHoldOf = new int[4 * room];
        Array.Fill(_bodyHoldOf, NoHold);
        _holds = new HoldRow[2 * room];
        _holdStates = new HoldState[_holds.Length];
        _holdLife = new HoldLife[_holds.Length];
        _freeHolds = new int[_holds.Length];
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
        _claimsChangedAt = new int[_rows.Length];
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
    /// <b>Each table's rows used and the rows it has</b> — slots, wishes, holds and way rows — for an instrument weighing
    /// what they are laid for (<see cref="SlotsPerHolder"/>).
    /// </summary>
    public (string Table, int Used, int Rows, int RowBytes)[] TablesUsed() =>
    [
        ("slots", _slotCount - _freeSlotCount, _slots.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<Slot>()),
        ("slot owners", _slotCount - _freeSlotCount, _owners.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<SlotOwner>()),
        ("wishes", _wishCount, _wishes.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<Wish>()),
        ("order", _wishCount, _order.Length, sizeof(int)),
        ("holds", _holdCount - _freeHoldCount, _holds.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<HoldRow>()),
        ("hold states", _holdCount - _freeHoldCount, _holdStates.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<HoldState>()),
        ("body holds", _bodyHoldOf.Length, _bodyHoldOf.Length, sizeof(int)),
        ("way rows", _touchedCount, _rows.Length, System.Runtime.CompilerServices.Unsafe.SizeOf<WayRow>()),
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
            Array.Resize(ref _claimsChangedAt, _rows.Length);
        }

        row = _touchedCount++;
        _rowOf[way] = row;
        _touched[row] = way;
        _rows[row] = new WayRow { Bodies = NoSlot, Mains = NoSlot, Seconds = NoSlot };
        _claimsChangedAt[row] = _answers;
        return row;
    }

    /// <summary>One way's row, or <see cref="NoRow"/> where nothing has been laid on it.</summary>
    int RowOf(int way) => _rowOf[way];
}
