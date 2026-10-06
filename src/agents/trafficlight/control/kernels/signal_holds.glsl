// Every light's hold laid on the device (SignalHolds.Lay, HoldTheStretch; LaneOccupancy.AheadTraveller, Place): a
// stretch showing anything but green held from its bar to the first body travelling its way, as one secondary claim of a
// hold of its own (TLT-1), in the claims' layout (claims_rows.glsl).
//
// The including kernel includes signal.glsl and claims_rows.glsl and declares CLAIM_ROWS, BODY_RUNS and BODY_ORDER as
// claims.glsl reads them.

// ClaimPriority.Signal.
const uint RungSignal = 4u;

// One stretch a light holds (SignalHolds): the way, from its bar to the way's end, and what shows it its colour.
struct StretchHoldRow
{
    uint way;
    float fromM;
    float toM;
    int subject;
    uint forCars;
    float pad0;
    float pad1;
    float pad2;
};

// The nearest body travelling a way in front (LaneOccupancy.AheadTraveller): of those going down it that reach past
// fromM and begin before untilM, the one with the least near edge — a row, or NoRow.
uint signalAheadTraveller(uint way, float fromM, float untilM)
{
    uvec2 run = BODY_RUNS[way];
    for (uint at = run.x; at < run.x + run.y; at++)
    {
        uint row = BODY_ORDER[at];
        ClaimRow body = CLAIM_ROWS[row];
        if (body.fromM >= untilM) break;
        if ((body.flags & ClaimOnItsLine) == 0u || body.toM <= fromM) continue;

        return row;
    }

    return NoRow;
}

// One stretch at one moment (SignalHolds.Lay, HoldTheStretch): whether it is held, the piece it places — where there is
// any ground left to place it on — and what its hold was laid to end at: `cut` the line metre (infinity where it was
// had whole) and the body that cut it.
bool signalHoldTheStretch(
    uint stretch, StretchHoldRow held, float timeS, int hold, out ClaimRow piece, out bool placed, out float cut, out uint cutBy)
{
    piece = ClaimRow(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0, -1, -1, 0u, 0u);
    placed = false;
    cut = 0.0;
    cutBy = NoRow;
    if (signalForStretch(held.subject, held.forCars != 0u, timeS) == SignalGreen) return false;

    cutBy = signalAheadTraveller(held.way, held.fromM, held.toM);
    float reachM = cutBy == NoRow ? held.toM : max(held.fromM, CLAIM_ROWS[cutBy].fromM);
    precise float shortM = reachM - held.fromM;
    cut = reachM < held.toM ? shortM : uintBitsToFloat(0x7F800000u);
    placed = reachM > held.fromM;
    piece = ClaimRow(
        held.fromM, reachM, 0.0, 0.0, 0.0, -uintBitsToFloat(0x7F800000u), held.fromM, int(stretch), hold, -1, held.way,
        RungSignal | (RosterSignal << ClaimRosterShift) | ClaimSecondary);
    return true;
}
