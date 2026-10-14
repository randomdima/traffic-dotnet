namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>A plan's own ground</b> (<see cref="BeginPlan"/>, root decision log, 2026-10-14): how far along its holder's line it
/// is committed (<see cref="CommitTo"/>), and what its holder has driven past let go of (<see cref="TrimBehind"/>).
/// </summary>
/// <remarks>
/// <b>Its claims are held at the rung each was planned at</b> (<see cref="Wish.Planned"/>), and how much of each is committed
/// is read off the plan's committed line where a stronger piece meets it (<see cref="CommittedToDm"/>) — so committing as
/// its holder moves writes one figure and no claim. <b>A plan's main claims run up its holder's line</b>
/// (<see cref="WishLine.Up"/>), each one's marks following it on its hold's list in the order they begin on its way.
/// </remarks>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// <b>A plan committed up to <paramref name="lineM"/> on its holder's line</b> (TER-5e): every claim of it before
    /// there, and every mark that begins before there, held as <see cref="ClaimPriority.Committed"/> ground, which nothing
    /// takes — and everything past it at the rung it was planned at.
    /// </summary>
    public void CommitTo(int hold, float lineM) => _holds[hold].CommittedLineM = lineM;

    /// <summary>
    /// <b>Where a held claim's committed ground ends</b>: it is committed from its near edge to there. All of it where it was
    /// taken at <see cref="ClaimPriority.Committed"/>; on a plan, as far as the plan's committed line — a mark all of
    /// it where it begins short of that, and none of it otherwise; and none of it anywhere else.
    /// </summary>
    ushort CommittedToDm(int slot)
    {
        ref readonly var claim = ref _slots[slot];
        if (claim.Rung == ClaimPriority.Committed) return claim.ToDm;

        ref readonly var owner = ref _owners[slot];
        if (!_holdLife[owner.Hold].ByLine || owner.Line != WishLine.Up) return claim.FromDm;

        var atDm = DmOf(_holds[owner.Hold].CommittedLineM - owner.LineM);
        if (owner.Kind == ClaimKind.Secondary) return owner.AtDm < atDm ? claim.ToDm : claim.FromDm;

        return Math.Clamp(atDm, claim.FromDm, claim.ToDm);
    }

    /// <summary>
    /// <b>What a plan's holder has passed, let go of</b>: every main claim of its line behind
    /// <paramref name="fromLineM"/> with its marks, the one it falls in begun there, and that one's marks that end by
    /// then — as a plan asked from there would have none of them.
    /// </summary>
    public void TrimBehind(int hold, float fromLineM)
    {
        ref var row = ref _holds[hold];
        var claim = row.FirstClaim;
        while (claim != NoSlot && IsMainUp(claim))
        {
            var fromDm = DmOf(fromLineM - _owners[claim].LineM);
            if (fromDm <= _slots[claim].FromDm) break;

            var lastMark = LastMarkOf(claim);
            var next = _owners[lastMark].HoldNext;
            if (fromDm >= _slots[claim].ToDm)
            {
                for (var at = claim; at != next;)
                {
                    var after = _owners[at].HoldNext;
                    LetGo(at);
                    at = after;
                }

                row.FirstClaim = claim = next;
                continue;
            }

            var prev = _owners[claim].Prev;
            Unthread(claim);
            _slots[claim].FromDm = fromDm;
            ThreadAfter(claim, prev);

            var kept = claim;
            var way = _touched[_owners[claim].Row];
            for (var mark = _owners[claim].HoldNext; mark != next;)
            {
                var after = _owners[mark].HoldNext;
                if (MarkEndDm(mark, way) <= fromDm)
                {
                    _owners[kept].HoldNext = after;
                    LetGo(mark);
                }
                else
                {
                    kept = mark;
                }

                mark = after;
            }

            if (next == NoSlot) row.LastClaim = kept;
            break;
        }

        if (row.FirstClaim == NoSlot) row.LastClaim = NoSlot;
    }

    bool IsMainUp(int slot) => _owners[slot].Kind == ClaimKind.Main && _owners[slot].Line == WishLine.Up;

    /// <summary>Whether a held claim is a secondary one, which on a plan's list is a mark of the main claim before it.</summary>
    bool IsAMark(int slot) => _owners[slot].Kind == ClaimKind.Secondary;

    /// <summary>A main claim's last mark on its hold's list, or the claim itself where it has none.</summary>
    int LastMarkOf(int main)
    {
        var last = main;
        for (var at = _owners[main].HoldNext; at != NoSlot && IsAMark(at); at = _owners[at].HoldNext) last = at;

        return last;
    }

    /// <summary>
    /// Where a mark's secondary claim ends on the way of the main claim it was taken with, read back off the marks of
    /// that way — or the furthest decimetre where none is that mark any more.
    /// </summary>
    ushort MarkEndDm(int mark, int mainWay)
    {
        var onWay = _touched[_owners[mark].Row];
        ref readonly var claim = ref _slots[mark];
        foreach (ref readonly var each in MarksOf(mainWay))
        {
            if (each.OnWay == onWay && each.FromDm == claim.FromDm && each.ToDm == claim.ToDm) return each.MineToDm;
        }

        return ushort.MaxValue;
    }
}
