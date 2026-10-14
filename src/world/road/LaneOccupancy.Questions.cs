namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>One body as where it stood</b> — the way it was met on and its stretch of it — which is how a pass knows again what it
/// gets past (<see cref="LaneOccupancy.FirstBodySeen"/>).
/// </summary>
internal readonly record struct BodySeen(int Way, float FromM, float ToM)
{
    /// <summary>Whether this body stands over any of where <paramref name="seen"/> stood.</summary>
    public bool IsUnderAny(ReadOnlySpan<BodySeen> seen)
    {
        foreach (ref readonly var one in seen)
        {
            if (one.Way == Way && one.ToM > FromM && one.FromM < ToM) return true;
        }

        return false;
    }
}

/// <summary>
/// <b>The questions the claims are asked, by place</b>: what stands or is held over a stretch of one way — never who, and
/// none of them a decision (SIM-7). <b>A reader only ever reads the way it is on</b> (TER-4c.5): whatever holds ground
/// that way shares has placed a secondary claim on it.
/// </summary>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// <b>The nearest body over <c>[fromM, toM)</c> of one way that is not the asker's own</b>, as the metre it begins at,
    /// held to <paramref name="fromM"/> — a body the asker is already inside answers there, being a contact and not a gap.
    /// </summary>
    public bool FirstBody(int way, float fromM, float toM, int occupant, LaneRoster of, out float atM)
    {
        var found = BodyIn(RowOf(way), DmOf(fromM), DmOf(toM), KeyOf(occupant, of), false, out _, out var atDm);
        atM = found ? MetresOf(atDm) : float.PositiveInfinity;
        return found;
    }

    /// <summary>
    /// <b>The nearest body over <c>[fromM, toM)</c> of one way that is not the asker's own</b>, as the whole stretch it
    /// stands on — one body, and never the run it is merged into.
    /// </summary>
    public bool FirstBodySeen(int way, float fromM, float toM, int occupant, LaneRoster of, out BodySeen body)
    {
        var found = BodyIn(RowOf(way), DmOf(fromM), DmOf(toM), KeyOf(occupant, of), false, out var member, out _);
        body = found ? new BodySeen(way, MetresOf(_bodies[member].FromDm), MetresOf(_bodies[member].ToDm)) : default;
        return found;
    }

    /// <summary>Whether any body but the asker's stands over a stretch of one way.</summary>
    public bool AnyBodyOver(int way, float fromM, float toM, int occupant, LaneRoster of) =>
        FirstBody(way, fromM, toM, occupant, of, out _);

    /// <summary>
    /// Whether a secondary claim over <c>[fromDm, toDm)</c> at <paramref name="rung"/> or stronger, read merged, has a
    /// holder but the two named — a kept hold's mark read as committed where it is (<see cref="CommittedToDm"/>).
    /// </summary>
    bool HeldByOthers(int head, ushort fromDm, ushort toDm, int one, int other, ClaimPriority rung)
    {
        const byte Over = 1, ByOthers = 2;
        Span<ushort> reachDm = stackalloc ushort[Rungs];
        Span<byte> merged = stackalloc byte[Rungs];
        reachDm.Clear();

        // A merged claim over the stretch takes in whatever begins short of its reach, though past the stretch.
        var untilDm = toDm;
        for (var at = head; at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var claim = ref _slots[at];
            if (claim.FromDm >= untilDm) break;

            var of = (int)claim.Rung;
            if (claim.Rung > rung)
            {
                if (rung < ClaimPriority.Committed || CommittedToDm(at) <= claim.FromDm) continue;

                of = (int)ClaimPriority.Committed;
            }

            if (claim.FromDm >= reachDm[of]) merged[of] = 0;

            reachDm[of] = Math.Max(reachDm[of], claim.ToDm);
            if (claim.FromDm < toDm && claim.ToDm > fromDm) merged[of] |= Over;
            if (claim.Key != one && claim.Key != other) merged[of] |= ByOthers;
            if (merged[of] == (Over | ByOthers)) return true;
            if (merged[of] == Over) untilDm = Math.Max(untilDm, reachDm[of]);
        }

        return false;
    }

    /// <summary>
    /// <b>Whether one holder's body stands on a way at all</b> — which of the town's lanes a scene lies across (SRV-9),
    /// read off the ground the body was laid on rather than measured again.
    /// </summary>
    public bool HasTheBodyOf(int way, int occupant, LaneRoster of) => OwnBody(way, KeyOf(occupant, of), out _, out _);

    /// <summary>Where one holder's own body stands on a way — the stretch it laid there — or false where it stands nowhere on it.</summary>
    public bool TheBodyOf(int way, int occupant, LaneRoster of, out float fromM, out float toM)
    {
        var found = OwnBody(way, KeyOf(occupant, of), out var fromDm, out var toDm);
        (fromM, toM) = found ? (MetresOf(fromDm), MetresOf(toDm)) : (0f, 0f);
        return found;
    }

    /// <summary>
    /// <b>Whether somebody has a stretch of one way</b>: a body standing over it, or ground a holder can no longer stop
    /// short of — which is what a place a body is put down on has to be clear of.
    /// </summary>
    public bool IsTaken(int way, float fromM, float toM) => IsTakenByOthers(way, fromM, toM, Nobody, Nobody, LaneRoster.Driving);

    /// <summary>
    /// <b>Whether anybody but two holders has a stretch of one way</b> — <see cref="IsTaken"/> asked for a body put down
    /// next to where it and the one putting it there already stand, whose own ground is no objection.
    /// </summary>
    public bool IsTakenByOthers(int way, float fromM, float toM, int occupant, int alsoOccupant, LaneRoster of)
    {
        var fromDm = DmOf(fromM);
        var toDm = DmOf(toM);
        var one = KeyOf(occupant, of);
        var other = KeyOf(alsoOccupant, of);
        var row = RowOf(way);
        for (var at = BodiesOn(row); at != NoSlot; at = _bodies[at].Next)
        {
            ref readonly var member = ref _bodies[at];
            if (member.FromDm >= toDm) break;
            if (member.ToDm > fromDm && member.Key != one && member.Key != other) return true;
        }

        if (!_answered || row == NoRow) return false;

        for (var at = _rows[row].Mains; at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var main = ref _slots[at];
            if (main.FromDm >= toDm) break;
            if (main.ToDm > fromDm && main.Key != one && main.Key != other
                && CommittedToDm(at) > Math.Max(fromDm, main.FromDm))
            {
                return true;
            }
        }

        return HeldByOthers(_rows[row].Seconds, fromDm, toDm, one, other, ClaimPriority.Committed);
    }

    /// <summary>
    /// <b>The nearest secondary claim of one rung over <c>[fromM, toM)</c> of one way</b>, read merged, as the stretch it
    /// holds — which is how a driver asks whether a light holds the road ahead (<see cref="ClaimPriority.Signal"/>).
    /// </summary>
    public bool FirstSecondary(int way, float fromM, float toM, ClaimPriority rung, out float heldFromM, out float heldToM)
    {
        var fromDm = DmOf(fromM);
        var toDm = DmOf(toM);
        var row = RowOf(way);
        var (heldFromDm, heldToDm, found) = ((ushort)0, (ushort)0, false);
        for (var at = _answered && row != NoRow ? _rows[row].Seconds : NoSlot; at != NoSlot; at = _slots[at].Next)
        {
            ref readonly var second = ref _slots[at];
            if (second.FromDm >= (found ? heldToDm : toDm)) break;
            if (second.Rung != rung) continue;

            if (second.FromDm >= heldToDm) heldFromDm = second.FromDm;

            heldToDm = Math.Max(heldToDm, second.ToDm);
            found = heldToDm > fromDm;
        }

        (heldFromM, heldToM) = found ? (MetresOf(heldFromDm), MetresOf(heldToDm)) : (0f, 0f);
        return found;
    }
}
