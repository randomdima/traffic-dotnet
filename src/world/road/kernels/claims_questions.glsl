// Questions asked of one way's bodies on the device (LaneOccupancy.Questions), each a walk of the way's body list as
// claims_lists.glsl lays it — nearest first — and none of them a decision.
//
// The including kernel includes claims_rows.glsl and declares CLAIM_ROWS (ClaimRow[]), BODY_RUNS (uvec2[] a way: first
// index into the order, count) and BODY_ORDER (uint[], row indices) first.

float claimsQuestionsInfinity() { return uintBitsToFloat(0x7F800000u); }

// How far along one way a holder's own body reaches — never a pass of its — or minus infinity where it is not on the
// way at all (LaneOccupancy.BodyReachesToM).
float claimsBodyReachesToM(uint way, int occupant, uint roster)
{
    float toM = -claimsQuestionsInfinity();
    uvec2 run = BODY_RUNS[way];
    for (uint at = run.x; at < run.x + run.y; at++)
    {
        ClaimRow body = CLAIM_ROWS[BODY_ORDER[at]];
        if (body.occupant == occupant && claimRoster(body) == roster && !claimPassing(body)) toM = max(toM, body.toM);
    }

    return toM;
}

// The nearest body in front on one way, the asker's own aside: of those reaching past fromM and beginning before
// untilM, the one with the least near edge, as its row (LaneOccupancy.AheadBody).
bool claimsAheadBody(uint way, float fromM, float untilM, int excluding, uint excludingRoster, out uint found)
{
    uvec2 run = BODY_RUNS[way];
    for (uint at = run.x; at < run.x + run.y; at++)
    {
        uint row = BODY_ORDER[at];
        ClaimRow body = CLAIM_ROWS[row];
        if (body.fromM > untilM) break;
        if ((body.occupant == excluding && claimRoster(body) == excludingRoster) || body.toM <= fromM) continue;

        found = row;
        return true;
    }

    found = NoRow;
    return false;
}

// Whether a pass laid this rebuild still has its stretch of one way to itself, on a plain pass's terms: no body over it
// but the holder's own, and no other pass of a holder before it by roster and occupant (LaneOccupancy.KeepsItsPass with
// PassTerms.Plain, which waits for nobody on its paint, having none).
bool claimsKeepsItsPass(uint way, float fromM, float toM, int occupant, uint roster)
{
    uvec2 run = BODY_RUNS[way];
    for (uint at = run.x; at < run.x + run.y; at++)
    {
        ClaimRow body = CLAIM_ROWS[BODY_ORDER[at]];
        if (body.fromM >= toM) break;

        uint bodyRoster = claimRoster(body);
        bool waitedFor = bodyRoster == RosterWalking &&body.toM > 0.0 && body.fromM < 0.0;
        if (body.toM <= fromM || (body.occupant == occupant && bodyRoster == roster) || waitedFor) continue;
        if (claimPassing(body))
        {
            if (bodyRoster > roster || (bodyRoster == roster && body.occupant > occupant)) continue;

            return false;
        }

        if (body.toM > fromM && body.fromM < toM) return false;
    }

    return true;
}
