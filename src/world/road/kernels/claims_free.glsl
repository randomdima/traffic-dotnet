// Whether a pass's ground is free on the device (LaneOccupancy.IsFreeForAPass, on a plain pass's terms), and what refuses
// it where it is not: one way's bodies and planned pieces walked as claims_lists.glsl lays them, nearest first, but for
// what a cut took out of them since (ClaimGone) — which the host takes out of its way's list.
//
// The including kernel includes claims_rows.glsl and declares CLAIM_ROWS (ClaimRow[]), BODY_RUNS and PLANNED_RUNS (uvec2[]
// a way: first index into the order, count) and CLAIM_ORDER (uint[], row indices) first.

// The row that refuses a pass holding [heldFromM, heldToM) of one way, its body sweeping [sweptFromM, sweptToM) of it,
// or NoRow where the pass is free: no body over where the pass's body goes but the asker's own, no other pass over any of
// what it holds, and no ground another holder plans there — a plain pass taking no plan (PassTerms.Plain), and waiting for
// nobody on its paint, having none.
uint claimsRefusingAPass(
    uint way, float sweptFromM, float sweptToM, float heldFromM, float heldToM, int occupant, uint roster)
{
    uvec2 bodies = BODY_RUNS[way];
    for (uint at = bodies.x; at < bodies.x + bodies.y; at++)
    {
        ClaimRow body = CLAIM_ROWS[CLAIM_ORDER[at]];
        if (claimGone(body)) continue;
        if (body.fromM >= heldToM) break;

        uint bodyRoster = claimRoster(body);
        bool waitedFor = bodyRoster == RosterWalking && body.toM > 0.0 && body.fromM < 0.0;
        if (body.toM <= heldFromM || (body.occupant == occupant && bodyRoster == roster) || waitedFor) continue;
        if (claimPassing(body) || (body.toM > sweptFromM && body.fromM < sweptToM)) return CLAIM_ORDER[at];
    }

    uvec2 planned = PLANNED_RUNS[way];
    for (uint at = planned.x; at < planned.x + planned.y; at++)
    {
        ClaimRow piece = CLAIM_ROWS[CLAIM_ORDER[at]];
        if (claimGone(piece)) continue;
        if (piece.fromM >= heldToM) break;
        if (piece.toM <= heldFromM || (piece.occupant == occupant && claimRoster(piece) == roster)) continue;

        return CLAIM_ORDER[at];
    }

    return NoRow;
}
