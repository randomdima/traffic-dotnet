// One piece of a plan laid whole as rows (LaneOccupancy.Propose, PlaceTheMarks): every mark over the main claim's
// metres placing the whole of its section of the other way as a secondary claim of the same hold, and then the main
// claim — the host's order — at CLAIMS_PROPOSED[into + written ..].
//
// The including kernel includes claims_rows.glsl and claims_marks.glsl and declares MARK_AT (uint[] a way, and one past
// the last), MARK_SECTIONS (WayMark[]), CLAIMS_PROPOSED (ClaimRow[]) and CLAIMS_PROPOSED_ROOM (how many rows a hold
// has room for from `into`) first. A hold past its room lays what fits and counts on, so it says how many it wanted.

// The terms one piece of a plan is asked on (PlannedAsk).
struct ClaimsAsk
{
    int hold;
    int occupant;
    uint roster;
    uint rung;
    float fromM;
    float lineFromM;
    float aheadM;
    float committedToM;
    float alongMps;
};

float claimsProposeInfinity() { return uintBitsToFloat(0x7F800000u); }

uint claimsAskFlags(ClaimsAsk ask) { return ask.rung | (ask.roster << ClaimRosterShift); }

// A secondary claim of the ask's hold over a mark's whole section, met at atM of the main claim's way
// (LaneOccupancy.PlaceTheMarks: LineAt, ArrivalAt, CannotStopShortOf). Read past from its own near edge.
ClaimRow claimsMarked(ClaimsAsk ask, WayMark mark, float atM)
{
    precise float pastM = max(0.0, atM - ask.fromM);
    precise float lineM = ask.lineFromM + pastM;
    precise float arrivalM = ask.aheadM + pastM;
    float committedToM = atM <= ask.committedToM ? claimsProposeInfinity() : -claimsProposeInfinity();
    return ClaimRow(
        mark.fromM, mark.toM, 0.0, lineM, arrivalM, committedToM, mark.fromM, ask.occupant, ask.hold, OnwardNoWay,
        uint(mark.onWay), claimsAskFlags(ask) | ClaimSecondary);
}

// The main claim the ask lays to toM (PlannedAsk.Laid), answered past standsToM.
ClaimRow claimsLaid(ClaimsAsk ask, uint way, float toM, float standsToM)
{
    return ClaimRow(
        ask.fromM, toM, ask.alongMps, ask.lineFromM, ask.aheadM, ask.committedToM, standsToM, ask.occupant, ask.hold,
        OnwardNoWay, way, claimsAskFlags(ask));
}

// One piece proposed over [ask.fromM, toM) of a way (LaneOccupancy.Propose); how many rows the hold has laid now. A
// piece of no length lays nothing.
uint claimsPropose(ClaimsAsk ask, uint way, float toM, float standsToM, uint into, uint written)
{
    if (ask.hold < 0 || toM <= ask.fromM) return written;

    for (uint at = MARK_AT[way]; at < MARK_AT[way + 1u]; at++)
    {
        WayMark mark = MARK_SECTIONS[at];
        if (mark.mineFromM >= toM) break;
        if (mark.mineToM <= ask.fromM) continue;

        if (written < CLAIMS_PROPOSED_ROOM) CLAIMS_PROPOSED[into + written] = claimsMarked(ask, mark, max(ask.fromM, mark.mineFromM));
        written++;
    }

    if (written < CLAIMS_PROPOSED_ROOM) CLAIMS_PROPOSED[into + written] = claimsLaid(ask, way, toM, standsToM);
    return written + 1u;
}
