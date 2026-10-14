using System.Diagnostics;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The plans asked</b> (TER-4c.1): a hold begun for one holder, its pieces asked as wishes in the order its line runs
/// over them, and what it came to once every hold is answered (<see cref="Answer"/>).
/// </summary>
internal sealed partial class LaneOccupancy
{
    /// <summary>How a wish's place on its holder's line is read (<see cref="Wish.LineM"/>).</summary>
    enum WishLine : byte
    {
        /// <summary>Its line runs up the way's metres.</summary>
        Up,

        /// <summary>Its line runs down the way's metres (TER-4c.7).</summary>
        Down,

        /// <summary>
        /// One place on its line whatever refuses it — a light's hold, or ground a body is swept over off its line.
        /// </summary>
        Fixed,
    }

    /// <summary>
    /// <b>One piece of ground a hold asks for</b>: a main piece followed by the secondary claims of the marks over it, or a
    /// secondary claim placed on its own.
    /// </summary>
    /// <remarks>
    /// <b>Fields, written where it lies</b> (<see cref="Add"/>): built on the stack and copied in, a wish cost
    /// <see cref="AskUp"/> half its time in stalled stores.
    /// </remarks>
    struct Wish
    {
        public ushort FromDm;
        public ushort ToDm;

        /// <summary>A mark's secondary claim: where on the main piece's way the mark begins, travelling it.</summary>
        public ushort AtDm;

        /// <summary>A main piece: how many secondary claims follow it.</summary>
        public byte Marks;

        /// <summary>
        /// The rung it is held at in a kept hold, whose committed line says how much of it is committed
        /// (<see cref="CommittedToDm"/>).
        /// </summary>
        public ClaimPriority Planned;

        public ClaimPriority Rung;
        public ClaimKind Kind;
        public WishLine Line;

        /// <summary>When it is answered: its rung, and a hold had whole after the plans of that rung.</summary>
        public byte Turn;

        public int Hold;

        /// <summary>
        /// <see cref="WishLine.Up"/> or <see cref="WishLine.Down"/>: where on the holder's line the main piece's way's
        /// nought falls — a mark's secondary claim carrying its main piece's, and cut where the mark begins on it.
        /// <see cref="WishLine.Fixed"/>: where on the line the hold is cut if this piece is refused.
        /// </summary>
        public float LineM;

        /// <summary>
        /// The way's row this rebuild, given it as it is asked (<see cref="Touch"/>) — and the way read back off it
        /// (<see cref="_touched"/>), so a wish is 24 bytes.
        /// </summary>
        public int Row;
    }

    /// <summary>
    /// <b>What one hold came to</b>, where its wishes are while it is answered, and the claims it holds in the order it
    /// asked them (<see cref="HoldState"/> says whose it is).
    /// </summary>
    struct HoldRow
    {
        public Refusal Cut;

        /// <summary>Where its wishes begin — one run of them, for a hold had whole.</summary>
        public int WishFrom;

        public int Wishes;
        public int FirstClaim;
        public int LastClaim;

        /// <summary>A kept hold: how far along its holder's line it is committed (<see cref="CommitTo"/>).</summary>
        public float CommittedLineM;
    }

    /// <summary>
    /// <b>What answering reads of a hold for every piece</b>: whose it is, whether it is had whole or not at all, and
    /// whether it is answered yet.
    /// </summary>
    /// <remarks>
    /// <b>Eight bytes, apart from <see cref="HoldRow"/></b>, so a laying's holds stay in cache while their pieces are
    /// taken: read off the 28-byte row, the hold was a fifth of <see cref="Take"/>'s time.
    /// </remarks>
    struct HoldState
    {
        public int Key;
        public bool Whole;

        /// <summary>A hold had whole, once it was answered.</summary>
        public bool Taken;

        /// <summary>Ended by a refusal, which its <see cref="HoldRow.Cut"/> is.</summary>
        public bool Cut;
    }

    Wish[] _wishes;
    int _wishCount;

    HoldRow[] _holds;
    HoldState[] _holdStates;
    int _holdCount;

    /// <summary>
    /// <b>A hold's life</b>: whether it holds anything at all, whether it is a plan committed by a line
    /// (<see cref="BeginPlan"/>), and the answer it was last answered or cut in (<see cref="_answers"/>).
    /// </summary>
    struct HoldLife
    {
        public bool Live;
        public bool ByLine;
        public int AnsweredAt;

        /// <summary>The turn the piece it was cut at was asked at, whether that piece ran down its way, and whether it was a secondary claim.</summary>
        public byte CutTurn;

        public bool CutAgainst;
        public bool CutSecondary;
    }

    HoldLife[] _holdLife;

    /// <summary>Holds let go of, begun again before the table grows.</summary>
    int[] _freeHolds;
    int _freeHoldCount;

    /// <summary>
    /// <b>A hold begun</b> for one holder: the ground it plans to use, asked a piece at a time (<see cref="Ask"/>,
    /// <see cref="AskDown"/>, <see cref="AskSwept"/>, <see cref="Place"/>) and answered with whatever else is asked with it
    /// (<see cref="Answer"/>). <b>Held until its holder lets go of it</b> (<see cref="Release"/>, root decision log,
    /// 2026-10-14) — or a stronger piece or a body cuts it.
    /// </summary>
    /// <param name="whole">
    /// <b>Had whole or not at all</b> (TER-4c.6): a pass or a manoeuvre not yet begun. Nothing of it is taken unless all of
    /// it can be, and it is answered after every hold of its rung asked with it that is not. Its pieces are asked in one
    /// run, at one rung.
    /// </param>
    public int BeginHold(int occupant, LaneRoster of, bool whole = false) => NewHold(occupant, of, whole, byLine: false);

    /// <summary>
    /// <b>A plan begun</b>: a hold whose committed ground is a line its holder moves (<see cref="CommitTo"/>), its claims
    /// held at the rung each was planned at, edited at its two ends as its holder goes (<see cref="TrimBehind"/>,
    /// <see cref="AskFurther"/>).
    /// </summary>
    public int BeginPlan(int occupant, LaneRoster of) => NewHold(occupant, of, whole: false, byLine: true);

    int NewHold(int occupant, LaneRoster of, bool whole, bool byLine)
    {
        Asking();
        int hold;
        if (_freeHoldCount > 0)
        {
            hold = _freeHolds[--_freeHoldCount];
        }
        else
        {
            if (_holdCount == _holds.Length)
            {
                Array.Resize(ref _holds, 2 * _holds.Length);
                Array.Resize(ref _holdStates, _holds.Length);
                Array.Resize(ref _holdLife, _holds.Length);
                Array.Resize(ref _freeHolds, _holds.Length);
            }

            hold = _holdCount++;
        }

        _holds[hold] = new HoldRow
        {
            Cut = Refusal.Whole, WishFrom = _wishCount, Wishes = 0, FirstClaim = NoSlot, LastClaim = NoSlot,
            CommittedLineM = float.NegativeInfinity,
        };
        _holdStates[hold] = new HoldState { Key = KeyOf(occupant, of), Whole = whole };
        _holdLife[hold] = new HoldLife { Live = true, ByLine = byLine, AnsweredAt = _answers };
        return hold;
    }

    /// <summary>Wishes asked after an answer begin a list of their own; those answered are read until then.</summary>
    void Asking()
    {
        if (!_answered) return;

        _wishCount = 0;
        _answered = false;
    }

    /// <summary>How many holds the table has been laid for: every one live now, and those let go of to be begun again.</summary>
    public int HoldCount => _holdCount;

    /// <summary>
    /// <b>Whether a hold of this holder's may be kept as it stands</b>: it is live, and nothing has changed where it was
    /// cut since (<see cref="IsWoken"/>).
    /// </summary>
    public bool CanKeep(int hold, int occupant, LaneRoster of) =>
        IsPlanOf(hold, occupant, of) && !IsWoken(hold);

    /// <summary>
    /// Whether a hold was cut at one of its marks — whose secondary claim, refused, is one a further ask from the cut
    /// would not ask again (<see cref="AskFurther"/>).
    /// </summary>
    public bool IsCutAtAMark(int hold) => _holds[hold].Cut.Refused && _holdLife[hold].CutSecondary;

    /// <summary>Whether a hold is a live plan of this holder's (<see cref="BeginPlan"/>).</summary>
    public bool IsPlanOf(int hold, int occupant, LaneRoster of) =>
        hold != NoHold && hold < _holdCount && _holdLife[hold].Live && _holdLife[hold].ByLine
        && _holdStates[hold].Key == KeyOf(occupant, of);

    /// <summary>
    /// <b>Whether what cut a hold has gone from where it cut it</b>, so asking again comes to more: nothing that would still
    /// refuse the piece it was cut at — a body, or a claim answered no later than it — stands on the decimetre it met,
    /// travelling the way it travelled.
    /// </summary>
    public bool IsWoken(int hold)
    {
        var cut = _holds[hold].Cut;
        if (!cut.Refused || cut.Way == NoWay) return false;

        var row = RowOf(cut.Way);
        if (row == NoRow) return true;

        ref readonly var life = ref _holdLife[hold];
        if (_claimsChangedAt[row] <= life.AnsweredAt) return false;

        var (fromDm, toDm) = life.CutAgainst
            ? ((ushort)Math.Max(0, cut.AtDm - 1), cut.AtDm)
            : (cut.AtDm, (ushort)Math.Min(ushort.MaxValue, cut.AtDm + 1));
        var key = _holdStates[hold].Key;
        var against = life.CutAgainst;
        if (MainIn(row, fromDm, toDm, key, against, life.CutTurn, out _, out _)) return false;
        if (life.CutSecondary) return true;

        return !BodyIn(row, fromDm, toDm, key, against, out _, out _)
               && !SecondaryIn(row, fromDm, toDm, key, against, life.CutTurn, out _, out _);
    }

    /// <summary>
    /// <b>Every claim a hold has, let go of</b>: each taken out of its way's run, and the hold given back, holding nothing
    /// and answered as nothing (<see cref="CutOf"/>). A hold already let go of is passed over.
    /// </summary>
    public void Release(int hold)
    {
        if (hold == NoHold || hold >= _holdCount || !_holdLife[hold].Live) return;

        ref var row = ref _holds[hold];
        for (var claim = row.FirstClaim; claim != NoSlot; claim = _owners[claim].HoldNext) LetGo(claim);

        row.FirstClaim = row.LastClaim = NoSlot;
        row.Cut = Refusal.Whole;
        _holdLife[hold].Live = false;
        _freeHolds[_freeHoldCount++] = hold;
    }

    /// <summary>
    /// <b>One piece of a hold's own line</b>: <c>[fromM, toM)</c> of one way, up its metres, asked at
    /// <paramref name="rung"/> — with the secondary claim every mark over it places over the other way's section
    /// (TER-5c.1), which ends the piece where the mark begins if it is refused.
    /// </summary>
    /// <remarks>
    /// <b>Begun past the holder's own body</b> where that stands over its start: a body the holder is level with cuts
    /// nothing, and ground it already stands on is no more its own for being asked.
    /// </remarks>
    /// <param name="lineFromM">Where <paramref name="fromM"/> falls on the holder's line.</param>
    /// <param name="further">
    /// <b>Asked past what a kept hold already holds</b> (<see cref="AskFurther"/>): no mark is asked that begins short of
    /// <paramref name="fromM"/>, since the hold already has it.
    /// </param>
    public void Ask(int hold, int way, float fromM, float toM, float lineFromM, ClaimPriority rung, bool further = false) =>
        AskUp(hold, way, fromM, toM, WishLine.Up, lineFromM - fromM, rung, rung, further);

    /// <summary>
    /// <see cref="Ask"/> of committed ground (<see cref="ClaimPriority.Committed"/>) in a kept hold, which is held at
    /// <paramref name="planned"/>, committed as far as its hold's committed line says (<see cref="CommitTo"/>).
    /// </summary>
    public void AskCommitted(
        int hold, int way, float fromM, float toM, float lineFromM, ClaimPriority planned, bool further = false) =>
        AskUp(hold, way, fromM, toM, WishLine.Up, lineFromM - fromM, ClaimPriority.Committed, planned, further);

    /// <summary>
    /// <b>A kept hold asked further</b>, from where it is cut or where all it asked for ends: what it came to forgotten
    /// until what it now asks is answered with every other piece (<see cref="Answer"/>), its claims kept as they are.
    /// </summary>
    public void AskFurther(int hold)
    {
        Asking();
        _holds[hold].Cut = Refusal.Whole;
        _holds[hold].WishFrom = _wishCount;
        _holds[hold].Wishes = 0;
        _holdStates[hold].Cut = false;
        _holdStates[hold].Taken = false;
    }

    /// <summary>
    /// <b>Ground a body is swept over off its own line</b> (TER-4c.6) — a pass's, a step's or a manoeuvre's: asked as
    /// <see cref="Ask"/> asks a piece, but <b>wherever in it the hold is refused, it ends at <paramref name="stopLineM"/></b>,
    /// since a way's metres there say nothing of the line's.
    /// </summary>
    public void AskSwept(int hold, int way, float fromM, float toM, float stopLineM, ClaimPriority rung) =>
        AskUp(hold, way, fromM, toM, WishLine.Fixed, stopLineM, rung, rung, further: false);

    void AskUp(
        int hold, int way, float fromM, float toM, WishLine line, float lineM, ClaimPriority rung, ClaimPriority planned,
        bool further)
    {
        if (hold == NoHold) return;

        var fromDm = DmOf(fromM);
        var toDm = DmOf(toM);
        ref var of = ref _holds[hold];
        var state = _holdStates[hold];
        var row = RowOf(way);
        if (OwnBodyAt(row, state.Key, fromDm, out _, out var ownToDm)) fromDm = ownToDm;

        if (toDm <= fromDm) return;

        if (row == NoRow) row = Touch(way);
        var main = Add(ref of, state.Whole, hold, row, fromDm, toDm, 0, rung, planned, ClaimKind.Main, line, lineM);
        var marks = 0;
        foreach (ref readonly var mark in MarksOf(way))
        {
            if (mark.MineFromDm >= toDm) break;
            if (further ? mark.MineFromDm < fromDm : mark.MineToDm <= fromDm) continue;

            Add(
                ref of, state.Whole, hold, Touch(mark.OnWay), mark.FromDm, mark.ToDm, Math.Max(fromDm, mark.MineFromDm),
                rung, planned, ClaimKind.Secondary, line, lineM);
            marks++;
        }

        _wishes[main].Marks = checked((byte)marks);
    }

    /// <summary>
    /// <b>One piece of a hold travelled down a way's metres</b> (TER-4c.7): <c>[fromM, toM)</c>, asked from
    /// <paramref name="toM"/> down — split at the marks as <see cref="Ask"/> splits a piece up the way, and ended short of
    /// the holder's own body where that stands over its top.
    /// </summary>
    /// <param name="lineAtToM">Where <paramref name="toM"/> falls on the holder's line.</param>
    public void AskDown(int hold, int way, float fromM, float toM, float lineAtToM, ClaimPriority rung)
    {
        if (hold == NoHold) return;

        var fromDm = DmOf(fromM);
        var toDm = DmOf(toM);
        ref var of = ref _holds[hold];
        var state = _holdStates[hold];
        var row = RowOf(way);
        if (toDm > 0 && OwnBodyAt(row, state.Key, (ushort)(toDm - 1), out var ownFromDm, out _)) toDm = ownFromDm;

        if (toDm <= fromDm) return;

        if (row == NoRow) row = Touch(way);
        var lineAtNoughtM = lineAtToM + toM;
        var main = Add(
            ref of, state.Whole, hold, row, fromDm, toDm, 0, rung, rung, ClaimKind.Main, WishLine.Down, lineAtNoughtM);
        var count = 0;
        var marks = MarksOf(way);
        for (var index = marks.Length - 1; index >= 0; index--)
        {
            ref readonly var mark = ref marks[index];
            if (mark.MineToDm <= fromDm || mark.MineFromDm >= toDm) continue;

            var markDm = Math.Min(toDm, mark.MineToDm);
            Add(
                ref of, state.Whole, hold, Touch(mark.OnWay), mark.FromDm, mark.ToDm, markDm, rung, rung,
                ClaimKind.Secondary, WishLine.Down, lineAtNoughtM);
            count++;
        }

        _wishes[main].Marks = checked((byte)count);
    }

    /// <summary>
    /// <b>A secondary claim placed where it stands</b> over <c>[fromM, toM)</c> of one way, and nothing else: no main
    /// claim and no mark followed. It is a light's hold (TLT-1) — ground held on the way it governs and on no other.
    /// </summary>
    /// <remarks>
    /// <b>How far it lies is the placer's to say</b>: a light's stops at the first body on its way
    /// (<see cref="FirstBody"/>).
    /// </remarks>
    public void Place(int hold, int way, float fromM, float toM, ClaimPriority rung)
    {
        if (hold == NoHold) return;

        var fromDm = DmOf(fromM);
        var toDm = DmOf(toM);
        if (toDm <= fromDm) return;

        Add(
            ref _holds[hold], _holdStates[hold].Whole, hold, Touch(way), fromDm, toDm, 0, rung, rung,
            ClaimKind.Secondary, WishLine.Fixed, fromM);
    }

    /// <summary>
    /// <b>What a hold came to</b> once every hold was answered (<see cref="Answer"/>): where on its holder's line it ends,
    /// and the place, rung and kind of what refused it — <see cref="Refusal.Whole"/> where it had all it asked for, or
    /// for no hold.
    /// </summary>
    public Refusal CutOf(int hold) => hold == NoHold || hold >= _holdCount ? Refusal.Whole : _holds[hold].Cut;

    /// <param name="of">The hold's row, <paramref name="hold"/>'s.</param>
    /// <param name="whole">Whether the hold is had whole (<see cref="HoldState.Whole"/>).</param>
    /// <param name="row">The way's row (<see cref="Touch"/>).</param>
    /// <returns>Where it was written.</returns>
    int Add(
        ref HoldRow of, bool whole, int hold, int row, ushort fromDm, ushort toDm, ushort atDm, ClaimPriority rung,
        ClaimPriority planned, ClaimKind kind, WishLine line, float lineM)
    {
        Asking();
        if (whole && (of.WishFrom + of.Wishes != _wishCount || (of.Wishes > 0 && _wishes[of.WishFrom].Rung != rung)))
        {
            throw new InvalidOperationException("a hold had whole is asked in one run and at one rung");
        }

        if (_wishCount == _wishes.Length) Array.Resize(ref _wishes, 2 * _wishes.Length);

        var at = _wishCount++;
        ref var wish = ref _wishes[at];
        wish.FromDm = fromDm;
        wish.ToDm = toDm;
        wish.AtDm = atDm;
        wish.Marks = 0;
        wish.Rung = rung;
        wish.Planned = planned;
        wish.Kind = kind;
        wish.Line = line;
        wish.Turn = (byte)((2 * (int)rung) + (whole ? 1 : 0));
        wish.Hold = hold;
        wish.LineM = lineM;
        wish.Row = row;
        of.Wishes++;
        return at;
    }

    /// <summary>How many pieces this rebuild asked for, for an instrument.</summary>
    public int WishCount => _wishCount;

    /// <summary>How many holds and main and secondary pieces one roster asked this rebuild, for an instrument.</summary>
    public (int Holds, int Mains, int Secondaries) AskedBy(LaneRoster roster)
    {
        var (holds, mains, seconds) = (0, 0, 0);
        for (var hold = 0; hold < _holdCount; hold++)
        {
            var key = _holdStates[hold].Key;
            if (_holdLife[hold].Live && key != NoKey && RosterOf(key) == roster) holds++;
        }

        foreach (ref readonly var wish in _wishes.AsSpan(0, _wishCount))
        {
            var key = _holdStates[wish.Hold].Key;
            if (key == NoKey || RosterOf(key) != roster) continue;

            if (wish.Kind == ClaimKind.Main) mains++;
            else seconds++;
        }

        return (holds, mains, seconds);
    }

    /// <summary>
    /// How many secondary pieces one roster asked this rebuild, into <paramref name="byKinds"/> at the kind of way of the
    /// main piece they follow times <see cref="WayKind"/>'s count plus the kind of way they lie on, for an instrument. One
    /// placed on its own is counted as following its own way's kind.
    /// </summary>
    public void SecondariesAskedAcross(LaneRoster roster, Span<int> byKinds)
    {
        var kinds = Enum.GetValues<WayKind>().Length;
        var (mainKind, marksLeft) = (0, 0);
        foreach (ref readonly var wish in _wishes.AsSpan(0, _wishCount))
        {
            var kind = (int)_ways.KindOf(_touched[wish.Row]);
            if (wish.Kind == ClaimKind.Main)
            {
                (mainKind, marksLeft) = (kind, wish.Marks);
                continue;
            }

            var fromKind = marksLeft > 0 ? mainKind : kind;
            if (marksLeft > 0) marksLeft--;

            var key = _holdStates[wish.Hold].Key;
            if (key != NoKey && RosterOf(key) == roster) byKinds[(fromKind * kinds) + kind]++;
        }
    }

    /// <summary>How many stretches of bodies one roster laid this rebuild, for an instrument.</summary>
    public int BodiesLaidBy(LaneRoster roster)
    {
        var count = 0;
        foreach (ref readonly var row in _rows.AsSpan(0, _touchedCount))
        {
            for (var at = row.Bodies; at != NoSlot; at = _slots[at].Next)
            {
                if (RosterOf(_slots[at].Key) == roster) count++;
            }
        }

        return count;
    }
}
