using System.Diagnostics;

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
    /// <b>Every hold answered</b>: each piece asked taken where nothing already down refuses it, and every hold ended at
    /// the first piece refused (<see cref="CutOf"/>). Once a rebuild, after the last piece is asked.
    /// </summary>
    public void Answer()
    {
        CloseTheBodies();
        TakeThemInOrder();
        _answered = true;
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
            if (refused) Cut(wish.Hold, ref hold, refusal);
            else Insert(wish, wish.FromDm, wish.ToDm, key);

            return;
        }

        // A mark is reached where the piece is had up to where it begins; refused, the piece ends there.
        var against = wish.Line == WishLine.Down;
        var reachDm = refused ? refusal.AtDm : against ? wish.FromDm : wish.ToDm;
        foreach (ref readonly var mark in _wishes.AsSpan(index + 1, wish.Marks))
        {
            if (against ? mark.AtDm < reachDm : mark.AtDm > reachDm) break;

            if (Refuses(mark, key, out var markRefusal))
            {
                (refused, refusal, reachDm) = (true, markRefusal, mark.AtDm);
                break;
            }

            Insert(mark, mark.FromDm, mark.ToDm, key);
        }

        var (fromDm, toDm) = against ? (reachDm, wish.ToDm) : (wish.FromDm, reachDm);
        if (toDm > fromDm) Insert(wish, fromDm, toDm, key);
        if (refused) Cut(wish.Hold, ref hold, refusal);
    }

    void Cut(int hold, ref HoldState state, in Refusal refusal)
    {
        _holds[hold].Cut = refusal;
        state.Cut = true;
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

            Cut(hold, ref state, refusal);
            return;
        }

        foreach (ref readonly var wish in wishes) Insert(wish, wish.FromDm, wish.ToDm, state.Key);
    }

    /// <summary>
    /// <b>What refuses a piece first</b>, travelling the way it is travelled: for a main piece a body, a main claim or a
    /// secondary claim of anybody but its own holder — of two at one decimetre, a body before a claim — and for a
    /// secondary claim any main claim over its section.
    /// </summary>
    bool Refuses(in Wish wish, int key, out Refusal refusal)
    {
        var row = wish.Row;
        if (wish.Kind == ClaimKind.Secondary)
        {
            var held = MainIn(row, wish.FromDm, wish.ToDm, key, false, out var heldDm, out var heldRung);
            refusal = held
                ? new Refusal(CutLineM(wish, wish.AtDm), _touched[row], heldDm, heldRung, ClaimKind.Main)
                : Refusal.Whole;
            return held;
        }

        var against = wish.Line == WishLine.Down;
        var found = BodyIn(row, wish.FromDm, wish.ToDm, key, against, out _, out var atDm);
        var rung = ClaimPriority.Hard;
        var kind = ClaimKind.Body;
        if (MainIn(row, wish.FromDm, wish.ToDm, key, against, out var mainDm, out var mainRung)
            && (!found || Nearer(mainDm, atDm, against)))
        {
            (found, atDm, rung, kind) = (true, mainDm, mainRung, ClaimKind.Main);
        }

        if (SecondaryIn(row, wish.FromDm, wish.ToDm, key, against, out var secondDm, out var secondRung)
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
    static float CutLineM(in Wish wish, ushort atDm) => wish.Line switch
    {
        WishLine.Up => wish.LineM + MetresOf(atDm),
        WishLine.Down => wish.LineM - MetresOf(atDm),
        _ => wish.LineM,
    };

    /// <summary>Whether one decimetre is met before another by a piece travelling the way it is travelled.</summary>
    static bool Nearer(ushort dm, ushort thanDm, bool against) => against ? dm > thanDm : dm < thanDm;

    /// <summary>The first decimetre of <c>[fromDm, toDm)</c> a main claim of anybody but the holder lies over, travelling the way it is travelled.</summary>
    bool MainIn(int row, ushort fromDm, ushort toDm, int key, bool against, out ushort atDm, out ClaimPriority rung)
    {
        atDm = 0;
        rung = ClaimPriority.Hard;
        var found = false;
        for (var at = row == NoRow ? NoSlot : _rows[row].Mains; at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var main = ref _slots[at];
            if (main.FromDm >= toDm) break;
            if (main.ToDm <= fromDm || main.Key == key) continue;

            if (!against)
            {
                atDm = Math.Max(fromDm, main.FromDm);
                rung = main.Rung;
                return true;
            }

            atDm = Math.Min(toDm, main.ToDm);
            rung = main.Rung;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// The same of the secondary claims taken so far — which may lie over one another, so every one beginning short of
    /// <paramref name="toDm"/> is read.
    /// </summary>
    bool SecondaryIn(int row, ushort fromDm, ushort toDm, int key, bool against, out ushort atDm, out ClaimPriority rung)
    {
        atDm = 0;
        rung = ClaimPriority.Hard;
        var found = false;
        for (var at = row == NoRow ? NoSlot : _rows[row].Seconds; at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var second = ref _slots[at];
            if (second.FromDm >= toDm) break;
            if (second.ToDm <= fromDm || second.Key == key) continue;

            if (!against)
            {
                atDm = Math.Max(fromDm, second.FromDm);
                rung = second.Rung;
                return true;
            }

            var reachDm = Math.Min(toDm, second.ToDm);
            if (found && reachDm <= atDm) continue;

            atDm = reachDm;
            rung = second.Rung;
            found = true;
        }

        return found;
    }

    /// <summary>A claim taken into its way's run of its kind.</summary>
    void Insert(in Wish wish, ushort fromDm, ushort toDm, int key)
    {
        if (wish.Kind == ClaimKind.Main)
        {
            InsertMain(wish.Row, fromDm, toDm, wish.Rung, key);
            return;
        }

        InsertAfterTies(ref _rows[wish.Row].Seconds, fromDm, toDm, wish.Rung, key);
    }

    /// <summary>
    /// <b>A main claim taken in, with every main claim it lies over</b> — all of them its own holder's, since another's
    /// would have refused it: two holds of one holder, a plan and a pass, being one holder's ground. So a run of main
    /// claims never overlaps itself, and what it lies over is found where its near edge is.
    /// </summary>
    void InsertMain(int row, ushort fromDm, ushort toDm, ClaimPriority rung, int key)
    {
        // Grown before the walk, which holds a reference into the table.
        if (_slotCount == _slots.Length) Array.Resize(ref _slots, 2 * _slots.Length);

        ref var link = ref _rows[row].Mains;
        while (link != NoSlot && _slots[link].ToDm <= fromDm) link = ref _slots[link].Next;

        if (link == NoSlot || _slots[link].FromDm >= toDm)
        {
            var slot = NewSlot(fromDm, toDm, rung, key);
            _slots[slot].Next = link;
            link = slot;
            return;
        }

        ref var first = ref _slots[link];
        var next = link;
        for (; next != NoSlot && _slots[next].FromDm < toDm; next = _slots[next].Next)
        {
            ref readonly var own = ref _slots[next];
            Debug.Assert(own.Key == key, "a main claim taken over another holder's");
            (fromDm, toDm) = (Math.Min(fromDm, own.FromDm), Math.Max(toDm, own.ToDm));
            rung = own.Rung < rung ? own.Rung : rung;
        }

        first.FromDm = fromDm;
        first.ToDm = toDm;
        first.Rung = rung;
        first.Next = next;
    }

}
