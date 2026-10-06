// The reservations on the device (LaneOccupancy): every claim a row, every way's bodies and planned pieces as runs of
// row indices nearest first, and every hold's pieces a run of rows in the order its holder's line runs over them.
// The plans are answered in passes as the host answers them (LaneOccupancy.Answers.cs): each hold against the others
// as they were shown the pass before.
//
// The including kernel includes claims_rows.glsl and declares, before this is included: CLAIM_ROWS (ClaimRow[]),
// BODY_RUNS and PLANNED_RUNS (uvec2[] a way: first index into the order, count), BODY_ORDER and PLANNED_ORDER (uint[],
// row indices), HOLD_ROWS (HoldRow[]), SHOWN_TO (float[] a hold), and CLAIMS_FIGURES.plansHidden — the first pass,
// read against the bodies alone.

float claimsInfinity() { return uintBitsToFloat(0x7F800000u); }

ClaimAnswer claimsWhole() { return ClaimAnswer(claimsInfinity(), 0.0, NoRow, -1, claimsInfinity(), 0.0, 0.0, 0.0); }

// Whether a reservation is the asker's own (LaneOccupancy.Owns).
bool claimsOwns(ClaimRow ask, ClaimRow claim)
{
    return claim.occupant == ask.occupant && claimRoster(claim) == claimRoster(ask);
}

// Whether one holder's body is on a stretch of one way (LaneOccupancy.StandsOn).
bool claimsStandsOn(uint way, int occupant, uint roster, float fromM, float toM)
{
    uvec2 run = BODY_RUNS[way];
    for (uint at = run.x; at < run.x + run.y; at++)
    {
        ClaimRow body = CLAIM_ROWS[BODY_ORDER[at]];
        if (body.fromM >= toM) break;
        if (body.occupant == occupant && claimRoster(body) == roster && !claimPassing(body) && body.toM > fromM) return true;
    }

    return false;
}

// Whether a planned piece is shown to the others this pass, and how far along its way (LaneOccupancy.Shows).
bool claimsShows(ClaimRow piece, out float toM)
{
    toM = piece.toM;
    float shownM = SHOWN_TO[piece.hold];
    if (piece.lineFromM >= shownM) return false;
    if (!claimSecondary(piece))
    {
        precise float endsM = piece.fromM + (shownM - piece.lineFromM);
        toM = min(toM, endsM);
    }

    return true;
}

bool claimsAnswered(int hold) { return (HOLD_ROWS[hold].flags & HoldAnswered) != 0u; }

// Whether an asker is held off a piece however the two compare: one laid where it stands (LaneOccupancy.HeldAgainst).
bool claimsHeldAgainst(ClaimRow ask, ClaimRow other)
{
    return claimsAnswered(ask.hold) && !claimsAnswered(other.hold);
}

// How far short of what refused it a holder comes to rest (Overtaking.KeptOffM): the room to step out round a body
// it may get past — a body going nowhere, or for a call anything at rest or slow it may pass — and its stand-off
// otherwise (LaneClaim.GoesNowhere, MayBePassedBy, Overtaking.MayGetPast).
float claimsKeptOffM(uint hold, ClaimRow asker, uint cutBy)
{
    HoldRow held = HOLD_ROWS[hold];
    ClaimRow by = CLAIM_ROWS[cutBy];
    bool still = (by.flags & ClaimStill) != 0u;
    bool body = claimHasBody(by) && !claimPassing(by);
    bool roomFor;
    if ((held.flags & HoldOnACall) != 0u)
    {
        bool traffic = claimRoster(by) == RosterDriving;
        roomFor = body && (still || traffic) && (still || by.alongMps < held.passesBelowMps);
    }
    else
    {
        roomFor = body && still && !((by.flags & ClaimOnItsLine) != 0u && by.onward != OnwardNoWay);
    }

    return roomFor ? held.keptOffWhatItMayPassM : held.standingMarginM;
}

// Which of two planned reservations meeting on one piece of ground keeps it — true where the asker does
// (LaneOccupancy.Beats). The asker is a main claim, read as the terms it was asked on (PlannedAsk).
bool claimsBeats(ClaimRow ask, float atM, bool askStands, ClaimRow other, bool otherStands)
{
    bool askCommitted = atM <= ask.committedToM;
    bool otherCommitted = !claimHasBody(other) && atM <= other.committedToM;
    if (askCommitted != otherCommitted) return askCommitted;
    if (askStands != otherStands) return askStands;
    if (!askCommitted && claimRung(ask) != claimRung(other)) return claimRung(ask) < claimRung(other);

    precise float askArrivalM = ask.aheadM + max(0.0, atM - ask.fromM);
    precise float otherArrivalM = other.aheadM + (claimSecondary(other) ? 0.0 : max(0.0, atM - other.fromM));
    if (askArrivalM != otherArrivalM) return askArrivalM < otherArrivalM;

    return claimRoster(ask) != claimRoster(other) ? claimRoster(ask) < claimRoster(other) : ask.occupant < other.occupant;
}

// How far along its way a main claim can be had against the bodies and the plans as shown (LaneOccupancy.Reach).
float claimsReach(ClaimRow ask, out uint cutBy)
{
    uint way = ask.way;
    float fromM = ask.fromM;
    float toM = ask.toM;
    float limitM = toM;
    cutBy = NoRow;

    uvec2 bodies = BODY_RUNS[way];
    for (uint at = bodies.x; at < bodies.x + bodies.y; at++)
    {
        uint row = BODY_ORDER[at];
        ClaimRow body = CLAIM_ROWS[row];
        if (body.fromM >= limitM) break;
        if (claimsOwns(ask, body) || body.toM <= ask.standsToM) continue;
        if (claimPassing(body) && claimsStandsOn(way, ask.occupant, claimRoster(ask), body.fromM, body.toM)) continue;

        limitM = max(fromM, body.fromM);
        cutBy = row;
        break;
    }

    if (CLAIMS_FIGURES.plansHidden != 0u) return limitM;

    uvec2 planned = PLANNED_RUNS[way];
    for (uint at = planned.x; at < planned.x + planned.y; at++)
    {
        uint row = PLANNED_ORDER[at];
        ClaimRow other = CLAIM_ROWS[row];
        float atM = max(fromM, other.fromM);
        if (atM >= limitM) break;

        float otherToM;
        if (claimGone(other) || !claimsShows(other, otherToM) || otherToM <= fromM || claimsOwns(ask, other)) continue;

        float overToM = min(toM, otherToM);
        if (!claimsHeldAgainst(ask, other) && claimsBeats(
                ask, atM, claimsStandsOn(way, ask.occupant, claimRoster(ask), atM, overToM), other,
                claimsStandsOn(way, other.occupant, claimRoster(other), atM, overToM)))
        {
            continue;
        }

        limitM = atM;
        cutBy = row;
        break;
    }

    return limitM;
}

// Whether a main claim of another hold over a secondary claim keeps the ground (LaneOccupancy.TakenBy).
bool claimsTakenBy(ClaimRow secondary, out uint taker)
{
    uint way = secondary.way;
    uvec2 planned = PLANNED_RUNS[way];
    for (uint at = planned.x; at < planned.x + planned.y; at++)
    {
        uint row = PLANNED_ORDER[at];
        ClaimRow main = CLAIM_ROWS[row];
        if (main.fromM >= secondary.toM) break;
        if (claimGone(main) || claimSecondary(main) || claimsOwns(secondary, main)) continue;

        float mainToM;
        if (!claimsShows(main, mainToM) || mainToM <= secondary.fromM) continue;

        float atM = max(main.fromM, secondary.fromM);
        float overToM = min(mainToM, secondary.toM);
        if (claimsAnswered(main.hold) && !claimsBeats(
                main, atM, claimsStandsOn(way, main.occupant, claimRoster(main), atM, overToM), secondary,
                claimsStandsOn(way, secondary.occupant, claimRoster(secondary), atM, overToM)))
        {
            continue;
        }

        taker = row;
        return true;
    }

    taker = NoRow;
    return false;
}

// What one hold's own main claims reach this pass, in the order its line runs over them — its holder's answer
// (Following.AnswerTheDrive, WalkingGround.Answer) — or, for a hold placed rather than asked, where it was laid to end.
ClaimAnswer claimsMainAnswer(uint hold)
{
    HoldRow held = HOLD_ROWS[hold];
    bool asked = false;
    for (uint row = held.firstRow; row < held.endRow; row++)
    {
        ClaimRow piece = CLAIM_ROWS[row];
        if (claimSecondary(piece)) continue;

        asked = true;
        uint cutBy;
        float reachM = claimsReach(piece, cutBy);
        if (reachM >= piece.toM) continue;

        precise float cutLineM = piece.lineFromM + (reachM - piece.fromM);
        return ClaimAnswer(cutLineM, claimsKeptOffM(hold, piece, cutBy), cutBy, int(piece.way), reachM, 0.0, 0.0, 0.0);
    }

    return asked
        ? claimsWhole()
        : ClaimAnswer(held.laidCutLineM, held.laidMarginM, held.laidCutBy, held.laidCutOn, claimsInfinity(), 0.0, 0.0, 0.0);
}

// Where a hold's secondary claims are taken: the nearest along its holder's line (LaneOccupancy.SecondaryCut).
ClaimAnswer claimsSecondaryCut(uint hold)
{
    HoldRow held = HOLD_ROWS[hold];
    ClaimAnswer cut = claimsWhole();
    if (CLAIMS_FIGURES.plansHidden != 0u) return cut;

    for (uint row = held.firstRow; row < held.endRow; row++)
    {
        ClaimRow piece = CLAIM_ROWS[row];
        if (claimGone(piece) || !claimSecondary(piece) || piece.lineFromM >= cut.cutLineM) continue;

        uint taker;
        if (claimsTakenBy(piece, taker))
        {
            cut = ClaimAnswer(piece.lineFromM, held.standingMarginM, taker, int(piece.way), claimsInfinity(), 0.0, 0.0, 0.0);
        }
    }

    return cut;
}

// One hold answered this pass: its main claims as its holder answers them and its secondary claims as the main claims
// over them leave them, the nearer of the two (LaneOccupancy.ShowTheAnswers).
ClaimAnswer claimsAnswer(uint hold)
{
    ClaimAnswer main = claimsMainAnswer(hold);
    ClaimAnswer taken = claimsSecondaryCut(hold);
    return taken.cutLineM < main.cutLineM ? taken : main;
}

// HeadwayKind, by number.
const uint HeadwayNothing = 0u;
const uint HeadwayQueue = 1u;
const uint HeadwayObstruction = 2u;
const uint HeadwayClaimed = 3u;
const uint HeadwayLight = 4u;
const uint HeadwayWalker = 5u;
const uint HeadwayPassing = 6u;

// LaneRoster's walkers and lights.
const uint RosterWalking = 1u;
const uint RosterSignal = 2u;

// What a claim that ended a grant is to the driver it ended (DrivingGround.KindOf).
uint claimsKindOf(uint row)
{
    if (row == NoRow) return HeadwayNothing;

    ClaimRow claim = CLAIM_ROWS[row];
    if (!claimHasBody(claim)) return claimRoster(claim) == RosterSignal ? HeadwayLight : HeadwayClaimed;
    if (claimPassing(claim)) return HeadwayPassing;
    if (claimRoster(claim) == RosterWalking) return HeadwayWalker;
    return (claim.flags & ClaimOnItsLine) != 0u ? HeadwayQueue : HeadwayObstruction;
}

// What a car got off its plan (DrivingGround.ReadTheGrant): how far past its nose the plan survived less what it keeps off
// what cut it — infinity where nothing did — the margin, and what cut it. A hold nothing cut leaves the margin and the
// cutter as the claim's clearing left them.
vec4 claimsReadTheGrant(ClaimAnswer answer, float claimFromM, float clearedMarginM, uint clearedKind)
{
    if (isinf(answer.cutLineM) && answer.cutLineM > 0.0) return vec4(answer.cutLineM, clearedMarginM, float(clearedKind), 0.0);

    precise float authorityM = answer.cutLineM - answer.marginM - claimFromM;
    uint kind = answer.cutBy == NoRow ? HeadwayClaimed : claimsKindOf(answer.cutBy);
    return vec4(authorityM, answer.marginM, float(kind), 0.0);
}

// A hold cut where its answer says: every piece at or past the metre taken out, and the main claim running over it
// ending there — on the way it was refused on, at the very metre it was refused at (LaneOccupancy.CutTo).
void claimsCutTo(uint hold, ClaimAnswer answer)
{
    float lineM = answer.cutLineM;
    if (isinf(lineM) && lineM > 0.0) return;

    HoldRow held = HOLD_ROWS[hold];
    for (uint row = held.firstRow; row < held.endRow; row++)
    {
        ClaimRow piece = CLAIM_ROWS[row];
        if (claimGone(piece)) continue;
        if (piece.lineFromM >= lineM)
        {
            CLAIM_ROWS[row].flags = piece.flags | ClaimGone;
            continue;
        }

        precise float endsLineM = piece.lineFromM + (piece.toM - piece.fromM);
        if (claimSecondary(piece) || endsLineM <= lineM) continue;

        bool refusedHere = int(piece.way) == answer.cutOn && answer.cutWayM > piece.fromM && answer.cutWayM < piece.toM;
        precise float carriedM = piece.fromM + (lineM - piece.lineFromM);
        float toM = refusedHere ? answer.cutWayM : carriedM;
        if (toM <= piece.fromM) CLAIM_ROWS[row].flags = piece.flags | ClaimGone;
        else CLAIM_ROWS[row].toM = toM;
    }
}
