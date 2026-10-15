namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>Every plan answered in one pass</b> (TER-4c.1, TER-4c.3): the wishes taken strongest rung first and, within a
/// rung, plans before holds had whole and otherwise in the order they were asked — each against what is already down
/// on its way, which is never weaker than it.
/// </summary>
internal sealed partial class LaneOccupancy
{
    const int Rungs = (int)ClaimPriority.Backing + 1;

    /// <summary>Two turns a rung: its plans, then its holds had whole.</summary>
    const int Turns = 2 * Rungs;

    int[] _order;
    readonly int[] _turnStarts = new int[Turns + 1];

    bool _answered;

    /// <summary>
    /// <b>Everything asked since the last answer, answered</b>: each piece taken where nothing already down refuses it, and
    /// every hold asked ended at the first piece refused (<see cref="CutOf"/>).
    /// </summary>
    public void Answer()
    {
        Asking();
        TakeThemInOrder();
        _answered = true;
        _answers++;
    }

    /// <summary>
    /// The wishes sorted by turn, stably — so each hold's pieces of one rung stay in the order its line runs. A main
    /// piece's marks follow it and are not sorted apart.
    /// </summary>
    void TakeThemInOrder()
    {
        if (_order.Length < _wishCount) _order = new int[Math.Max(_wishCount, 2 * _order.Length)];

        Array.Clear(_turnStarts);
        var leads = 0;
        for (var index = 0; index < _wishCount; index += 1 + _wishes[index].Marks)
        {
            _turnStarts[_wishes[index].Turn + 1]++;
            leads++;
        }

        for (var turn = 1; turn <= Turns; turn++) _turnStarts[turn] += _turnStarts[turn - 1];

        for (var index = 0; index < _wishCount; index += 1 + _wishes[index].Marks)
        {
            _order[_turnStarts[_wishes[index].Turn]++] = index;
        }

        foreach (var index in _order.AsSpan(0, leads)) Take(index);
    }

    /// <summary>
    /// <b>One wish taken</b>, unless its hold was already ended: a hold had whole all at once, a secondary claim whole or
    /// not at all, and a main piece with its marks up to whatever refuses the piece or the first mark refused — the hold
    /// then ending there.
    /// </summary>
    void Take(int index)
    {
        ref readonly var wish = ref _wishes[index];
        ref var hold = ref _holdStates[wish.Hold];
        if (hold.Cut || hold.Taken) return;

        if (hold.Whole)
        {
            TakeWhole(wish.Hold, ref hold);
            return;
        }

        var key = hold.Key;
        var refused = Refuses(wish, key, out var refusal);
        if (wish.Kind == ClaimKind.Secondary)
        {
            if (refused)
            {
                Cut(wish, ref hold, refusal);
            }
            else
            {
                TakeOver(wish, wish.FromDm, wish.ToDm, key);
                Insert(wish, wish.FromDm, wish.ToDm, key);
            }

            return;
        }

        // A mark is reached where the piece is had up to where it begins; refused, the piece ends there.
        var against = wish.Line == WishLine.Down;
        var reachDm = refused ? refusal.AtDm : against ? wish.FromDm : wish.ToDm;
        var refusedAt = index;
        var marksTaken = 0;
        foreach (ref readonly var mark in _wishes.AsSpan(index + 1, wish.Marks))
        {
            if (against ? mark.AtDm < reachDm : mark.AtDm > reachDm) break;

            if (Refuses(mark, key, out var markRefusal))
            {
                (refused, refusal, reachDm, refusedAt) = (true, markRefusal, mark.AtDm, index + 1 + marksTaken);
                break;
            }

            marksTaken++;
        }

        var (fromDm, toDm) = against ? (reachDm, wish.ToDm) : (wish.FromDm, reachDm);
        var main = NoSlot;
        if (toDm > fromDm)
        {
            TakeOver(wish, fromDm, toDm, key);
            main = Insert(wish, fromDm, toDm, key);
        }

        // A piece refused where it begins has nothing of it had, and no mark with it.
        if (main == NoSlot) marksTaken = 0;

        foreach (ref readonly var mark in _wishes.AsSpan(index + 1, marksTaken))
        {
            TakeOver(mark, mark.FromDm, mark.ToDm, key);
            Insert(mark, mark.FromDm, mark.ToDm, key, main);
        }

        if (refused) Cut(_wishes[refusedAt], ref hold, refusal);
    }

    /// <summary>A hold ended at the piece refused, with what it would take to wake it (<see cref="IsWoken"/>).</summary>
    void Cut(in Wish refused, ref HoldState state, in Refusal refusal)
    {
        _holds[refused.Hold].Cut = refusal;
        state.Cut = true;
        Ended(refused.Hold, refused.Turn, refused.Kind, refused.Line);
    }

    void Ended(int hold, byte turn, ClaimKind kind, WishLine line)
    {
        ref var life = ref _holdLife[hold];
        life.AnsweredAt = _answers;
        life.CutTurn = turn;
        life.CutSecondary = kind == ClaimKind.Secondary;
        life.CutAgainst = kind == ClaimKind.Main && line == WishLine.Down;
    }

    /// <summary>
    /// <b>A hold had whole, answered at once</b>: every piece of it taken where none is refused, and none where one is —
    /// the hold ending at the first, in the order it was asked.
    /// </summary>
    void TakeWhole(int hold, ref HoldState state)
    {
        state.Taken = true;
        var wishes = _wishes.AsSpan(_holds[hold].WishFrom, _holds[hold].Wishes);
        foreach (ref readonly var wish in wishes)
        {
            if (!Refuses(wish, state.Key, out var refusal)) continue;

            Cut(wish, ref state, refusal);
            return;
        }

        var main = NoSlot;
        foreach (ref readonly var wish in wishes)
        {
            TakeOver(wish, wish.FromDm, wish.ToDm, state.Key);
            var slot = Insert(wish, wish.FromDm, wish.ToDm, state.Key, wish.Kind == ClaimKind.Secondary ? main : NoSlot);
            if (wish.Kind == ClaimKind.Main) main = slot;
        }
    }

    /// <summary>
    /// <b>What refuses a piece first</b>, travelling the way it is travelled: for a main piece a body, a main claim or a
    /// secondary claim of anybody but its own holder — of two at one decimetre, a body before a claim — and for a
    /// secondary claim any main claim over its section. <b>A claim held at a later turn refuses nothing</b>: the piece
    /// takes it (<see cref="TakeOver"/>).
    /// </summary>
    bool Refuses(in Wish wish, int key, out Refusal refusal)
    {
        var row = wish.Row;
        if (wish.Kind == ClaimKind.Secondary)
        {
            var held = MainIn(row, wish.FromDm, wish.ToDm, key, false, wish.Turn, out var heldDm, out var heldRung);
            refusal = held
                ? new Refusal(CutLineM(wish, wish.AtDm), _touched[row], heldDm, heldRung, ClaimKind.Main)
                : Refusal.Whole;
            return held;
        }

        var against = wish.Line == WishLine.Down;
        var found = BodyIn(row, wish.FromDm, wish.ToDm, key, against, out _, out var atDm);
        var rung = ClaimPriority.Hard;
        var kind = ClaimKind.Body;
        if (MainIn(row, wish.FromDm, wish.ToDm, key, against, wish.Turn, out var mainDm, out var mainRung)
            && (!found || Nearer(mainDm, atDm, against)))
        {
            (found, atDm, rung, kind) = (true, mainDm, mainRung, ClaimKind.Main);
        }

        if (SecondaryIn(row, wish.FromDm, wish.ToDm, key, against, wish.Turn, out var secondDm, out var secondRung)
            && (!found || Nearer(secondDm, atDm, against)))
        {
            (found, atDm, rung, kind) = (true, secondDm, secondRung, ClaimKind.Secondary);
        }

        refusal = found ? new Refusal(CutLineM(wish, atDm), _touched[row], atDm, rung, kind) : Refusal.Whole;
        return found;
    }

    /// <summary>
    /// Where on its holder's line a wish is cut, refused at one decimetre of its main piece's way — a mark's secondary
    /// claim at where the mark begins on it (<see cref="Wish.AtDm"/>).
    /// </summary>
    static float CutLineM(in Wish wish, ushort atDm) => CutLineM(wish.Line, wish.LineM, atDm);

    static float CutLineM(WishLine line, float lineM, ushort atDm) => line switch
    {
        WishLine.Up => lineM + MetresOf(atDm),
        WishLine.Down => lineM - MetresOf(atDm),
        _ => lineM,
    };

    /// <summary>Whether one decimetre is met before another by a piece travelling the way it is travelled.</summary>
    static bool Nearer(ushort dm, ushort thanDm, bool against) => against ? dm > thanDm : dm < thanDm;

    /// <summary>When a held claim is answered: its rung, and after the plans of it where its hold is had whole (<see cref="Wish.Turn"/>).</summary>
    byte TurnOf(int slot) => _slots[slot].Turn;

    /// <summary>
    /// The first decimetre of <c>[fromDm, toDm)</c> a main claim of anybody but the holder lies over, travelling the way it is
    /// travelled — of those answered no later than <paramref name="turn"/>, or over ground committed
    /// (<see cref="CommittedToDm"/>).
    /// </summary>
    bool MainIn(
        int row, ushort fromDm, ushort toDm, int key, bool against, byte turn, out ushort atDm, out ClaimPriority rung)
    {
        atDm = 0;
        rung = ClaimPriority.Hard;
        var found = false;
        for (var at = row == NoRow ? NoSlot : _rows[row].Mains; at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var main = ref _slots[at];
            if (main.FromDm >= toDm) break;
            if (main.ToDm <= fromDm || main.Key == key) continue;

            var (heldToDm, heldRung) = (main.ToDm, main.Rung);
            if (TurnOf(at) > turn)
            {
                heldToDm = CommittedToDm(at);
                if (heldToDm <= Math.Max(fromDm, main.FromDm)) continue;

                heldRung = ClaimPriority.Committed;
            }

            if (!against)
            {
                atDm = Math.Max(fromDm, main.FromDm);
                rung = heldRung;
                return true;
            }

            atDm = Math.Min(toDm, heldToDm);
            rung = heldRung;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// The same of the secondary claims taken so far — which may lie over one another, so every one beginning short of
    /// <paramref name="toDm"/> is read.
    /// </summary>
    bool SecondaryIn(
        int row, ushort fromDm, ushort toDm, int key, bool against, byte turn, out ushort atDm, out ClaimPriority rung)
    {
        atDm = 0;
        rung = ClaimPriority.Hard;
        var found = false;
        for (var at = row == NoRow ? NoSlot : _rows[row].Seconds; at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var second = ref _slots[at];
            if (second.FromDm >= toDm) break;
            if (second.ToDm <= fromDm || second.Key == key) continue;

            var heldRung = second.Rung;
            if (TurnOf(at) > turn)
            {
                if (CommittedToDm(at) <= second.FromDm) continue;

                heldRung = ClaimPriority.Committed;
            }

            if (!against)
            {
                atDm = Math.Max(fromDm, second.FromDm);
                rung = heldRung;
                return true;
            }

            var reachDm = Math.Min(toDm, second.ToDm);
            if (found && reachDm <= atDm) continue;

            atDm = reachDm;
            rung = heldRung;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// <b>A claim taken into its way's run of its kind</b>, and held by its hold. Main claims of one holder may lie over
    /// one another — a plan and a pass of one car being one holder's ground — and another's never do, since it would have
    /// refused them.
    /// </summary>
    /// <param name="main">For a mark's secondary claim, the main claim taken with it.</param>
    int Insert(in Wish wish, ushort fromDm, ushort toDm, int key, int main = NoSlot) => Hold(wish, fromDm, toDm, key, main);

    /// <summary>
    /// <b>Held ground a stronger piece takes</b> (the owner's ruling, root decision log): every claim of another holder
    /// over <c>[fromDm, toDm)</c> answered at a later turn than the piece — a main claim, or for a main piece a secondary
    /// one too — cut from there on as that holder would have been refused had it asked after (<see cref="CutHeld"/>).
    /// Claims taken this rebuild never are, being taken in turn; only a hold kept from an earlier one can be.
    /// </summary>
    void TakeOver(in Wish wish, ushort fromDm, ushort toDm, int key)
    {
        while (LaterIn(_rows[wish.Row].Mains, fromDm, toDm, key, wish.Turn) is var main && main != NoSlot)
        {
            CutHeld(main, fromDm, toDm, wish.Rung, wish.Kind);
        }

        if (wish.Kind == ClaimKind.Secondary) return;

        while (LaterIn(_rows[wish.Row].Seconds, fromDm, toDm, key, wish.Turn) is var second && second != NoSlot)
        {
            CutHeld(second, fromDm, toDm, wish.Rung, wish.Kind);
        }
    }

    /// <summary>
    /// The first claim of a run over <c>[fromDm, toDm)</c>, of anybody but the holder, answered after
    /// <paramref name="turn"/> — none of whose ground there is committed, a piece taken having been refused by any that is.
    /// </summary>
    int LaterIn(int head, ushort fromDm, ushort toDm, int key, byte turn)
    {
        for (var at = head; at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var claim = ref _slots[at];
            if (claim.FromDm >= toDm) break;
            if (claim.ToDm > fromDm && claim.Key != key && TurnOf(at) > turn) return at;
        }

        return NoSlot;
    }

    /// <summary>
    /// <b>A held hold cut where a stronger piece or a body takes one of its claims</b>, as it would have been refused there
    /// asking after it: a main claim ended where the ground taken begins, travelling it the way its holder does; a mark's
    /// secondary claim, or one placed on its own, let go of, and a mark's main claim ended where the mark begins; and
    /// everything the hold asked after that let go of. What it came to is that refusal (<see cref="CutOf"/>), and its
    /// holder is woken by whatever changes there next (<see cref="IsWoken"/>).
    /// </summary>
    /// <param name="byRung">The rung of what takes it — <see cref="ClaimPriority.Hard"/> for a body.</param>
    /// <param name="byKind">What takes it.</param>
    void CutHeld(int slot, ushort byFromDm, ushort byToDm, ClaimPriority byRung, ClaimKind byKind)
    {
        ref readonly var owner = ref _owners[slot];
        var hold = owner.Hold;
        var against = owner.Line == WishLine.Down;
        var (cutTurn, cutKind, cutLine) = (TurnOf(slot), owner.Kind, owner.Line);
        ref readonly var claim = ref _slots[slot];
        var metDm = owner.Kind == ClaimKind.Main
            ? against ? Math.Min(claim.ToDm, byToDm) : Math.Max(claim.FromDm, byFromDm)
            : Math.Max(claim.FromDm, byFromDm);
        var cutLineM = owner.Kind == ClaimKind.Main
            ? CutLineM(owner.Line, owner.LineM, metDm)
            : CutLineM(owner.Line, owner.LineM, owner.AtDm);
        var refusal = new Refusal(cutLineM, _touched[owner.Row], metDm, byRung, byKind);

        // Where the hold now ends: in the main claim the cut one is, or is a mark of, travelled as far as the cut.
        var (shortened, endDm, from) = _holdStates[hold].Whole ? (NoSlot, (ushort)0, _holds[hold].FirstClaim)
            : owner.Kind == ClaimKind.Main ? (slot, metDm, slot)
            : owner.Main != NoSlot ? (owner.Main, owner.AtDm, owner.Main)
            : (NoSlot, (ushort)0, slot);

        ref var row = ref _holds[hold];
        var last = NoSlot;
        for (var at = row.FirstClaim; at != from; at = _owners[at].HoldNext) last = at;

        // The shortened main claim, then the marks taken with it short of where it now ends; nothing past the cut claim, and
        // no mark of a main claim nothing is left of — a plan trimmed behind its holder keeps the marks it still stands
        // in, which begin short of its main claim's near edge, and none of them is ground it plans from there.
        var mainLeft = false;
        var past = false;
        for (var at = from; at != NoSlot;)
        {
            var next = _owners[at].HoldNext;
            past |= at == slot && at != shortened;
            if (!past && at == shortened)
            {
                mainLeft = Shorten(at, endDm);
                if (mainLeft) last = Link(hold, last, at);
                else LetGo(at);
            }
            else if (!past && mainLeft && _owners[at].Main == shortened
                     && (against ? _owners[at].AtDm >= endDm : _owners[at].AtDm <= endDm))
            {
                last = Link(hold, last, at);
            }
            else
            {
                LetGo(at);
            }

            at = next;
        }

        row.LastClaim = last;
        if (last == NoSlot) row.FirstClaim = NoSlot;
        else _owners[last].HoldNext = NoSlot;

        row.Cut = refusal;
        _holdStates[hold].Cut = true;
        Ended(hold, cutTurn, cutKind, cutLine);
    }

    /// <summary>A claim kept on its hold's list, after <paramref name="last"/>; the new last.</summary>
    int Link(int hold, int last, int slot)
    {
        if (last == NoSlot) _holds[hold].FirstClaim = slot;
        else _owners[last].HoldNext = slot;

        return slot;
    }

    /// <summary>
    /// A main claim ended at <paramref name="endDm"/>, travelling it the way its holder does: whether anything of it is
    /// left, threaded back into its run where its near edge now is.
    /// </summary>
    bool Shorten(int slot, ushort endDm)
    {
        ref var claim = ref _slots[slot];
        var against = _owners[slot].Line == WishLine.Down;
        var (fromDm, toDm) = against ? (Math.Max(claim.FromDm, endDm), claim.ToDm) : (claim.FromDm, Math.Min(claim.ToDm, endDm));
        if (toDm <= fromDm) return false;

        _claimsChangedAt[_owners[slot].Row] = _answers;
        if (fromDm == claim.FromDm)
        {
            claim.ToDm = toDm;
            return true;
        }

        Unthread(slot);
        claim.FromDm = fromDm;
        claim.ToDm = toDm;
        Thread(slot);
        return true;
    }

    /// <summary>A held claim taken out of its run without letting go of it.</summary>
    void Unthread(int slot)
    {
        ref readonly var owner = ref _owners[slot];
        _claimsChangedAt[owner.Row] = _answers;
        var next = _slots[slot].Next;
        if (owner.Prev == NoSlot) HeadOf(owner.Row, owner.Kind) = next;
        else
        {
            _slots[owner.Prev].Next = next;
        }

        if (next != NoSlot) _owners[next].Prev = owner.Prev;
    }

    /// <summary>A held claim threaded back into its run after every one beginning at or before its near edge.</summary>
    void Thread(int slot) => ThreadAfter(slot, NoSlot);

    /// <summary>
    /// <see cref="Thread"/>, looked for from <paramref name="after"/> on — a claim of the run beginning no later than this
    /// one, or <see cref="NoSlot"/> for its first.
    /// </summary>
    void ThreadAfter(int slot, int after)
    {
        ref var owner = ref _owners[slot];
        _claimsChangedAt[owner.Row] = _answers;
        ref var head = ref HeadOf(owner.Row, owner.Kind);
        var prev = after;
        var next = after == NoSlot ? head : _slots[after].Next;
        while (next != NoSlot && _slots[next].FromDm <= _slots[slot].FromDm) (prev, next) = (next, _slots[next].Next);

        _slots[slot].Next = next;
        owner.Prev = prev;
        if (prev == NoSlot) head = slot;
        else _slots[prev].Next = slot;
        if (next != NoSlot) _owners[next].Prev = slot;
    }
}
