namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The claims as an instrument reads them</b>: a way's run of one kind as claims, those of one rung that overlap made
/// one, with all of their holders once each. Nothing that decides reads them.
/// </summary>
internal sealed partial class LaneOccupancy
{
    /// <summary>One kind's claims as last read, and their holders, which <see cref="HoldersOf"/> points into.</summary>
    sealed class ClaimView
    {
        public Claim[] Claims = new Claim[16];
        public int[] Holders = new int[16];
    }

    readonly ClaimView _bodyView = new();
    readonly ClaimView _mainView = new();
    readonly ClaimView _secondView = new();

    /// <summary>
    /// The body claims on one way, nearest first — every body laid, once the first question is asked — each the bodies
    /// running into one another, with all of them as its holders.
    /// </summary>
    /// <remarks><b>Read into one buffer a kind, which the next call overwrites.</b></remarks>
    public ReadOnlySpan<Claim> Bodies(int way)
    {
        CloseTheBodies();
        var row = RowOf(way);
        return row == NoRow ? [] : Read(_bodies, _rows[row].Bodies, ClaimKind.Body, _bodyView);
    }

    /// <summary>The main claims on one way, nearest first — none until the plans are answered. Read as <see cref="Bodies"/> are.</summary>
    public ReadOnlySpan<Claim> Mains(int way)
    {
        var row = RowOf(way);
        return _answered && row != NoRow ? Read(_slots, _rows[row].Mains, ClaimKind.Main, _mainView) : [];
    }

    /// <summary>
    /// The secondary claims on one way by near edge, those of one rung never overlapping — none until the plans are
    /// answered. Read as <see cref="Bodies"/> are.
    /// </summary>
    public ReadOnlySpan<Claim> Secondaries(int way)
    {
        var row = RowOf(way);
        return _answered && row != NoRow ? Read(_slots, _rows[row].Seconds, ClaimKind.Secondary, _secondView) : [];
    }

    /// <summary>The holders of one claim as its kind was last read.</summary>
    public ReadOnlySpan<int> HoldersOf(in Claim claim)
    {
        var view = claim.Kind switch
        {
            ClaimKind.Body => _bodyView,
            ClaimKind.Main => _mainView,
            _ => _secondView,
        };
        return view.Holders.AsSpan(claim.HoldersFrom, claim.Holders);
    }

    /// <summary>
    /// One run read into a view: of one rung, those overlapping made one claim, its holders in the order laid — a held
    /// claim read as the committed ground it begins with and the rest at its own rung (<see cref="CommittedToDm"/>).
    /// </summary>
    ReadOnlySpan<Claim> Read(Slot[] slots, int head, ClaimKind kind, ClaimView view)
    {
        Span<int> open = stackalloc int[Rungs];
        open.Fill(-1);
        var count = 0;
        for (var at = head; at != NoSlot; at = slots[at].Next)
        {
            ref readonly var slot = ref slots[at];
            var committedToDm = kind == ClaimKind.Body ? slot.FromDm : CommittedToDm(at);
            if (committedToDm > slot.FromDm) Open(open, view, ref count, at, slot.FromDm, committedToDm, ClaimPriority.Committed, kind);
            if (slot.ToDm > committedToDm) Open(open, view, ref count, at, committedToDm, slot.ToDm, slot.Rung, kind);
        }

        var holders = 0;
        foreach (ref var claim in view.Claims.AsSpan(0, count))
        {
            var from = holders;
            for (var at = claim.HoldersFrom; at != NoSlot && slots[at].FromDm < claim.ToDm; at = slots[at].Next)
            {
                var key = slots[at].Key;
                if (!IsOfRung(slots, at, kind, claim) || view.Holders.AsSpan(from, holders - from).Contains(key)) continue;

                if (holders == view.Holders.Length) Array.Resize(ref view.Holders, 2 * holders);

                view.Holders[holders++] = key;
            }

            claim = claim with { Holders = (ushort)(holders - from), HoldersFrom = from };
        }

        return view.Claims.AsSpan(0, count);
    }

    /// <summary>One stretch of a slot read into a view at one rung: run into the claim of that rung it overlaps, or a claim of its own.</summary>
    static void Open(
        Span<int> open, ClaimView view, ref int count, int at, ushort fromDm, ushort toDm, ClaimPriority rung, ClaimKind kind)
    {
        ref var into = ref open[(int)rung];
        if (into >= 0 && view.Claims[into].ToDm > fromDm)
        {
            if (toDm > view.Claims[into].ToDm) view.Claims[into] = view.Claims[into] with { ToDm = toDm };

            return;
        }

        if (count == view.Claims.Length) Array.Resize(ref view.Claims, 2 * count);

        // Where its holders begin is its first slot until they are named.
        into = count;
        view.Claims[count++] = new Claim(fromDm, toDm, rung, kind, 0, at);
    }

    /// <summary>Whether a slot holds some of a claim read at the claim's rung.</summary>
    bool IsOfRung(Slot[] slots, int at, ClaimKind kind, in Claim claim)
    {
        ref readonly var slot = ref slots[at];
        if (slot.ToDm <= claim.FromDm) return false;

        var committedToDm = kind == ClaimKind.Body ? slot.FromDm : CommittedToDm(at);
        return claim.Rung == ClaimPriority.Committed
            ? committedToDm > Math.Max(slot.FromDm, claim.FromDm)
            : slot.Rung == claim.Rung && slot.ToDm > Math.Max(committedToDm, claim.FromDm);
    }

    /// <summary>
    /// <b>The claim a refusal was met at</b>, for naming whose it was (<see cref="HoldersOf"/>): of its kind over its
    /// decimetre — and of its rung, for a secondary claim, those of two rungs lying over one another.
    /// </summary>
    public bool ClaimAt(in Refusal refusal, out Claim found)
    {
        found = default;
        if (refusal.Way == NoWay) return false;

        var claims = refusal.Kind switch
        {
            ClaimKind.Body => Bodies(refusal.Way),
            ClaimKind.Main => Mains(refusal.Way),
            _ => Secondaries(refusal.Way),
        };
        foreach (ref readonly var claim in claims)
        {
            if (claim.FromDm > refusal.AtDm || claim.ToDm < refusal.AtDm) continue;
            if (refusal.Kind == ClaimKind.Secondary && claim.Rung != refusal.Rung) continue;

            found = claim;
            return true;
        }

        return false;
    }

    /// <summary>
    /// <b>The first holder of the claim a refusal was met at</b> (<see cref="ClaimAt"/>), as one integer
    /// (<see cref="OccupantOf"/>, <see cref="RosterOf"/>) — or −1 where there is none.
    /// </summary>
    public int HolderAt(in Refusal refusal) =>
        ClaimAt(refusal, out var claim) && claim.Holders > 0 ? HoldersOf(claim)[0] : NoKey;

    /// <summary>How many claims of each kind the last rebuild laid, and how many holders they name between them.</summary>
    public (int Bodies, int Mains, int Secondaries, int Holders) Laid()
    {
        var (bodies, mains, seconds, holders) = (0, 0, 0, 0);
        foreach (var way in OccupiedWays)
        {
            bodies += Counted(Bodies(way), ref holders);
            mains += Counted(Mains(way), ref holders);
            seconds += Counted(Secondaries(way), ref holders);
        }

        return (bodies, mains, seconds, holders);
    }

    static int Counted(ReadOnlySpan<Claim> claims, ref int holders)
    {
        foreach (ref readonly var claim in claims) holders += claim.Holders;

        return claims.Length;
    }
}
