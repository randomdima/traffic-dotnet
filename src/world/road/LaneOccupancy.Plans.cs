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
        public ushort Marks;

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
    /// <b>What one hold came to</b>, and where its wishes are (<see cref="HoldState"/> says whose it is).
    /// </summary>
    /// <param name="WishFrom">Where its wishes begin — one run of them, for a hold had whole.</param>
    record struct HoldRow(Refusal Cut, int WishFrom, int Wishes);

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
    /// <b>A hold begun</b> for one holder: the ground it plans to use, asked a piece at a time
    /// (<see cref="Ask"/>, <see cref="AskDown"/>, <see cref="AskSwept"/>, <see cref="Place"/>) and answered with every
    /// other (<see cref="Answer"/>).
    /// </summary>
    /// <param name="whole">
    /// <b>Had whole or not at all</b> (TER-4c.6): a pass or a manoeuvre not yet begun. Nothing of it is taken unless all of
    /// it can be, and it is answered after every hold of its rung that is not — so the traffic whose plan already reaches
    /// over it is let by first. Its pieces are asked in one run, at one rung.
    /// </param>
    public int BeginHold(int occupant, LaneRoster of, bool whole = false)
    {
        if (_holdCount == _holds.Length)
        {
            Array.Resize(ref _holds, 2 * _holds.Length);
            Array.Resize(ref _holdStates, _holds.Length);
        }

        _holds[_holdCount] = new HoldRow(Refusal.Whole, _wishCount, 0);
        _holdStates[_holdCount] = new HoldState { Key = KeyOf(occupant, of), Whole = whole };
        return _holdCount++;
    }

    /// <summary>How many holds this rebuild began.</summary>
    public int HoldCount => _holdCount;

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
    public void Ask(int hold, int way, float fromM, float toM, float lineFromM, ClaimPriority rung) =>
        AskUp(hold, way, fromM, toM, WishLine.Up, lineFromM - fromM, rung);

    /// <summary>
    /// <b>Ground a body is swept over off its own line</b> (TER-4c.6) — a pass's, a step's or a manoeuvre's: asked as
    /// <see cref="Ask"/> asks a piece, but <b>wherever in it the hold is refused, it ends at <paramref name="stopLineM"/></b>,
    /// since a way's metres there say nothing of the line's.
    /// </summary>
    public void AskSwept(int hold, int way, float fromM, float toM, float stopLineM, ClaimPriority rung) =>
        AskUp(hold, way, fromM, toM, WishLine.Fixed, stopLineM, rung);

    void AskUp(int hold, int way, float fromM, float toM, WishLine line, float lineM, ClaimPriority rung)
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
        var main = Add(ref of, state.Whole, hold, row, fromDm, toDm, 0, rung, ClaimKind.Main, line, lineM);
        var marks = 0;
        foreach (ref readonly var mark in MarksOf(way))
        {
            if (mark.MineFromDm >= toDm) break;
            if (mark.MineToDm <= fromDm) continue;

            Add(
                ref of, state.Whole, hold, Touch(mark.OnWay), mark.FromDm, mark.ToDm, Math.Max(fromDm, mark.MineFromDm),
                rung, ClaimKind.Secondary, line, lineM);
            marks++;
        }

        _wishes[main].Marks = (ushort)marks;
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
            ref of, state.Whole, hold, row, fromDm, toDm, 0, rung, ClaimKind.Main, WishLine.Down, lineAtNoughtM);
        var count = 0;
        var marks = MarksOf(way);
        for (var index = marks.Length - 1; index >= 0; index--)
        {
            ref readonly var mark = ref marks[index];
            if (mark.MineToDm <= fromDm || mark.MineFromDm >= toDm) continue;

            var markDm = Math.Min(toDm, mark.MineToDm);
            Add(
                ref of, state.Whole, hold, Touch(mark.OnWay), mark.FromDm, mark.ToDm, markDm, rung, ClaimKind.Secondary,
                WishLine.Down, lineAtNoughtM);
            count++;
        }

        _wishes[main].Marks = (ushort)count;
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
            ref _holds[hold], _holdStates[hold].Whole, hold, Touch(way), fromDm, toDm, 0, rung, ClaimKind.Secondary,
            WishLine.Fixed, fromM);
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
        ClaimKind kind, WishLine line, float lineM)
    {
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
        foreach (ref readonly var hold in _holdStates.AsSpan(0, _holdCount))
        {
            if (hold.Key != NoKey && RosterOf(hold.Key) == roster) holds++;
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
