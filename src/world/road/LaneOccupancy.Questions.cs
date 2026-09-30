namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The questions the reservations are asked</b>: what body is in front on one way, and everything on one
/// way for whoever draws or measures it — every one of them a walk of one way's own lists, and none of them a
/// decision (SIM-7). <b>A reader only ever reads the way it is on</b> (TER-4c.5): whatever holds ground that
/// way shares has placed a secondary claim on it.
/// </summary>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// <b>The nearest body in front</b>: of the bodies on one way that reach past <paramref name="fromM"/>
    /// and begin before <paramref name="untilM"/>, the one with the least near edge.
    /// </summary>
    /// <remarks>
    /// A body the asker is already inside answers at its own near edge rather than being skipped — a car
    /// inside somebody else is a contact and not a gap. <b>The asker's own body is never an answer</b>, and an
    /// occupant is always named with its roster.
    /// </remarks>
    public bool AheadBody(
        int way, float fromM, float untilM, int excluding, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM > untilM) break;
            if ((body.Occupant == excluding && body.Of == excludingOf) || body.ToM <= fromM) continue;

            found = body;
            return true;
        }

        found = LaneClaim.Nothing;
        return false;
    }

    /// <summary>
    /// <b>The nearest body in front of a holder going down a way against its metres</b> — a pass over the lane
    /// beside (TER-4c.6): of the bodies over <c>[fromM, untilM)</c>, the one reaching furthest.
    /// </summary>
    public bool AheadBodyAgainst(
        int way, float fromM, float untilM, int excluding, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving)
    {
        found = LaneClaim.Nothing;
        var reachM = float.NegativeInfinity;
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= untilM) break;
            if ((body.Occupant == excluding && body.Of == excludingOf) || body.ToM <= fromM) continue;
            if (body.ToM <= reachM) continue;

            found = body;
            reachM = body.ToM;
        }

        return found.Found;
    }

    /// <summary>
    /// <b>The nearest body travelling one way in front</b> (<see cref="LaneClaim.OnItsLine"/>): of the bodies going
    /// down the way that reach past <paramref name="fromM"/> and begin before <paramref name="untilM"/>, the one
    /// with the least near edge. A body only standing on the way — across it, or going nowhere down it — is no
    /// answer.
    /// </summary>
    public bool AheadTraveller(int way, float fromM, float untilM, out LaneClaim found)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= untilM) break;
            if (!body.OnItsLine || body.ToM <= fromM) continue;

            found = body;
            return true;
        }

        found = LaneClaim.Nothing;
        return false;
    }

    /// <summary>Whether any body other than the asker's stands over a stretch of one way.</summary>
    public bool AnyBodyOver(int way, float fromM, float toM, int excluding, LaneRoster excludingOf) =>
        AheadBody(way, fromM, toM, excluding, out _, excludingOf);

    /// <summary>The nearest body of one roster over a stretch of one way.</summary>
    public bool AheadBodyOf(LaneRoster of, int way, float fromM, float toM, out LaneClaim found)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= toM) break;
            if (body.Of != of || body.ToM <= fromM) continue;

            found = body;
            return true;
        }

        found = LaneClaim.Nothing;
        return false;
    }

    /// <summary>
    /// <b>Whether a stretch of one way is free for a pass</b> (TER-4c.6): no body over it but the asker's own,
    /// and — where <paramref name="plansToo"/> — no ground any other holder plans to use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is being passed plans nothing that counts against it</b>: a body at rest plans the room to pull
    /// away and no ground it cannot stop short of, and the pass, laid over that, cuts it (TER-4c.1). <b>Its body
    /// counts like any other</b>: one reaching into the lane beside is standing where the pass would go.
    /// </para>
    /// <para>
    /// <b>A caller may leave plans out on ground its pass only skirts</b> — the end of a zebra's paint at the
    /// kerb, which the traffic holds whole (TER-5c.3) wherever it is crossing.
    /// </para>
    /// </remarks>
    /// <param name="passed">The bodies the pass gets past, named by occupant and roster.</param>
    public bool IsFreeForAPass(
        int way, float fromM, float toM, int occupant, LaneRoster of, ReadOnlySpan<LaneClaim> passed,
        bool plansToo = true)
    {
        if (toM <= fromM) return true;

        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= toM) break;
            if (body.ToM <= fromM || (body.Occupant == occupant && body.Of == of)) continue;

            return false;
        }

        return !plansToo || IsUnplannedForAPass(way, fromM, toM, occupant, of, passed, PassTerms.Plain);
    }

    /// <summary>
    /// <b>Whether a car's pass is free over a way it holds more of than its body sweeps</b> (TER-4c.6) — a movement
    /// through a box, held whole: no body but the asker's where its body goes, no other pass over any of what it
    /// holds, and no ground another holder plans there but what it passes and what its terms take.
    /// </summary>
    /// <remarks>
    /// <b>A body is read where the pass's body goes, and a plan wherever the pass holds</b>: a movement is held whole
    /// so that nothing is let into it that the pass would then wait on, which is a question of plans. A body standing
    /// on the movement clear of where the pass's body goes is in nobody's way — and where it stands inside the box
    /// itself, it is what the pass is getting past.
    /// </remarks>
    public bool IsFreeForAPass(
        int way, float sweptFromM, float sweptToM, float heldFromM, float heldToM, int occupant, LaneRoster of,
        ReadOnlySpan<LaneClaim> passed, in PassTerms terms)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= heldToM) break;
            if (body.ToM <= heldFromM || (body.Occupant == occupant && body.Of == of) || terms.WaitsFor(body)) continue;
            if (body.Passing || (body.ToM > sweptFromM && body.FromM < sweptToM)) return false;
        }

        return IsUnplannedForAPass(way, heldFromM, heldToM, occupant, of, passed, terms);
    }

    /// <summary>
    /// <b>One holder's body on a way, as it stands this rebuild</b> — its first stretch there, never a pass — or false
    /// where it stands nowhere on it.
    /// </summary>
    public bool TheBodyOf(int way, int occupant, LaneRoster of, out LaneClaim body)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            body = _slots[at];
            if (body.Occupant == occupant && body.Of == of && !body.Passing) return true;
        }

        body = LaneClaim.Nothing;
        return false;
    }

    /// <summary>
    /// <b>Whether one holder's body stands on a way at all</b> — which of the town's lanes a scene lies across
    /// (SRV-9), read off the ground the body was laid on rather than measured again.
    /// </summary>
    public bool HasTheBodyOf(int way, int occupant, LaneRoster of)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            if (_slots[at].Occupant == occupant && _slots[at].Of == of) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>Whether anybody but two holders has a stretch of one way</b> — <see cref="IsTaken"/> asked for a body put
    /// down next to where it and the one putting it there already stand, whose own ground is no objection.
    /// </summary>
    public bool IsTakenByOthers(int way, float fromM, float toM, int occupant, int alsoOccupant, LaneRoster of)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= toM) break;
            if (body.ToM > fromM && !IsEither(body, occupant, alsoOccupant, of)) return true;
        }

        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var piece = ref _slots[at];
            if (piece.FromM >= toM) break;
            if (piece.ToM > fromM && !IsEither(piece, occupant, alsoOccupant, of)
                && piece.CommittedAt(MathF.Max(fromM, piece.FromM)))
            {
                return true;
            }
        }

        return false;

        static bool IsEither(in LaneClaim claim, int one, int other, LaneRoster of) =>
            claim.Of == of && (claim.Occupant == one || claim.Occupant == other);
    }

    /// <summary>
    /// <b>How far along one way a holder's own body reaches</b> — the far edge of its collider there, never of a pass —
    /// or negative infinity where it is not on the way at all. What a plan asked over that way is level with already
    /// (<see cref="Reach"/>'s <c>standsToM</c>).
    /// </summary>
    public float BodyReachesToM(int way, int occupant, LaneRoster of)
    {
        var toM = float.NegativeInfinity;
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.Occupant == occupant && body.Of == of && !body.Passing) toM = MathF.Max(toM, body.ToM);
        }

        return toM;
    }

    /// <summary>
    /// Whether no holder but the asker and what it passes plans any of a stretch of one way — nothing, that is, but
    /// what the pass's terms take.
    /// </summary>
    bool IsUnplannedForAPass(
        int way, float fromM, float toM, int occupant, LaneRoster of, ReadOnlySpan<LaneClaim> passed,
        in PassTerms terms)
    {
        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var piece = ref _slots[at];
            if (piece.FromM >= toM) break;
            if (piece.ToM <= fromM || (piece.Occupant == occupant && piece.Of == of)) continue;
            if (terms.Takes(piece, MathF.Max(fromM, piece.FromM), IsOneOf(piece, occupant, of, passed))) continue;

            return false;
        }

        return true;
    }

    /// <summary>
    /// <b>Where a holder travelling a way can come to rest on it</b>: the far end of its body there, or of the
    /// ground its plan on the way says it can no longer stop short of, whichever is further — past the way's own
    /// end where that ground runs on off it.
    /// </summary>
    public float StopsByM(int way, in LaneClaim body)
    {
        var stopsM = body.ToM;
        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var piece = ref _slots[at];
            if (piece.Occupant != body.Occupant || piece.Of != body.Of || piece.Secondary) continue;

            stopsM = MathF.Max(stopsM, piece.CommittedToM);
        }

        return stopsM;
    }

    /// <summary>
    /// <b>Whether a pass laid in this rebuild still has its stretch of one way to itself</b> (TER-4c.6) —
    /// asked by its holder once before it moves over, since a body may have stepped in since it was asked for
    /// and a second pass may have been asked for over the same ground on the same tick.
    /// </summary>
    /// <remarks>
    /// <b>Two passes asked on one tick are settled by roster and occupant, the lower keeping its own</b>: both
    /// holders read one layer, so the one comparison gives both of them one answer and exactly one withdraws.
    /// </remarks>
    public bool KeepsItsPass(int way, float fromM, float toM, int occupant, LaneRoster of) =>
        KeepsItsPass(way, fromM, toM, fromM, toM, occupant, of, PassTerms.Plain);

    /// <summary>
    /// And the same over a way the pass holds more of than its body sweeps — a movement through a box, held whole:
    /// a body where the pass's body goes but one its terms wait for, and another pass anywhere it holds
    /// (<see cref="IsFreeForAPass(int, float, float, float, float, int, LaneRoster, ReadOnlySpan{LaneClaim}, in PassTerms)"/>).
    /// </summary>
    public bool KeepsItsPass(
        int way, float sweptFromM, float sweptToM, float heldFromM, float heldToM, int occupant, LaneRoster of,
        in PassTerms terms)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= heldToM) break;
            if (body.ToM <= heldFromM || (body.Occupant == occupant && body.Of == of) || terms.WaitsFor(body)) continue;
            if (body.Passing)
            {
                if (body.Of > of || (body.Of == of && body.Occupant > occupant)) continue;

                return false;
            }

            if (body.ToM > sweptFromM && body.FromM < sweptToM) return false;
        }

        return true;
    }

    static bool IsOneOf(in LaneClaim claim, int occupant, LaneRoster of, ReadOnlySpan<LaneClaim> passed)
    {
        if (claim.Occupant == occupant && claim.Of == of) return true;

        foreach (ref readonly var body in passed)
        {
            if (claim.Occupant == body.Occupant && claim.Of == body.Of) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>Whether somebody has a stretch of one way</b>: a body standing over it, or ground a holder can no
    /// longer stop short of — which is what a place a body is put down on has to be clear of.
    /// </summary>
    public bool IsTaken(int way, float fromM, float toM)
    {
        if (AheadBody(way, fromM, toM, Nobody, out _)) return true;

        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var piece = ref _slots[at];
            if (piece.FromM >= toM) break;
            if (piece.ToM > fromM && piece.CommittedAt(MathF.Max(fromM, piece.FromM))) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>The nearest ground one roster holds in front</b>: of the planned pieces on one way held under
    /// <paramref name="of"/> that reach past <paramref name="fromM"/> and begin before <paramref name="untilM"/>,
    /// the one with the least near edge — which is how a driver asks whether a light holds the road ahead.
    /// </summary>
    public bool AheadPlanned(int way, float fromM, float untilM, LaneRoster of, out LaneClaim found)
    {
        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var piece = ref _slots[at];
            if (piece.FromM > untilM) break;
            if (piece.Of != of || piece.ToM <= fromM) continue;

            found = piece;
            return true;
        }

        found = LaneClaim.Nothing;
        return false;
    }

    /// <summary>
    /// <b>How far along one way a holder's own main claim reaches</b> from <paramref name="fromM"/>, in
    /// that way's metres — <paramref name="fromM"/> where it holds none of it there.
    /// </summary>
    public float PlannedToM(int way, float fromM, int occupant, LaneRoster of)
    {
        var reachM = fromM;
        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var piece = ref _slots[at];
            if (piece.Occupant != occupant || piece.Of != of || piece.Secondary) continue;
            if (piece.ToM <= reachM || piece.FromM > reachM) continue;

            reachM = piece.ToM;
        }

        return reachM;
    }

    /// <summary>
    /// <b>Everything on one way</b>, bodies first and then planned ground, each nearest first — for a test, an
    /// overlay and an instrument, and nothing on the hot path.
    /// </summary>
    public int CopyTo(int way, Span<LaneClaim> into)
    {
        var written = CopyBodiesTo(way, into);
        return written + CopyPlannedTo(way, into[written..]);
    }

    /// <summary>The bodies on one way, nearest first.</summary>
    public int CopyBodiesTo(int way, Span<LaneClaim> into) => CopyList(_bodies, way, into);

    /// <summary>The planned ground on one way, nearest first.</summary>
    public int CopyPlannedTo(int way, Span<LaneClaim> into) => CopyList(_planned, way, into);

    int CopyList(int[] heads, int way, Span<LaneClaim> into)
    {
        var written = 0;
        for (var at = heads[way]; at != NoSlot && written < into.Length; at = _next[at]) into[written++] = _slots[at];

        return written;
    }
}
