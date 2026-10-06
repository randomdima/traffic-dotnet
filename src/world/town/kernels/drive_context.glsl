// What a car following its route's line is told about the world this tick, on the device (TownWorld.SetTheContext,
// ReadTheBoxAhead, LaneAheadSlot, LightAheadM, ToThePlaceOnTheLineM; Following.Tick through
// Overtaking.MayGetPastWhatCutIt): the junction ahead read off its own line, how far off a light holds its road, where
// the place it was sent to falls on its line, whether what ended its grant is something it may get past, and the drive
// context its profile is answered against (follower.glsl's DriveContextRow).
//
// The including kernel includes arc.glsl, car_build.glsl, car_line.glsl, claims_rows.glsl, road_joins.glsl,
// line_onward.glsl, line_ways.glsl, arc_chain.glsl (SPLINE_ARCS the lines, LINE_WAYS_FIGURES.arcsPerCar a car) and
// follower.glsl's DriveContextRow, declares CLAIM_ROWS, PLANNED_RUNS and PLANNED_ORDER as claims.glsl reads them (the
// claims after their cut), CONNECTOR_RUNG and CONNECTOR_LENGTH (a connector's ClaimPriority and length) and
// LANE_PASSABLE (uint a lane: whether a car on it has a lane running back beside it to pass on), and supplies
// CONTEXT_FIGURES' fields arcsPerCar, slowerToPassMps and queueInCarLengths.

// ClaimPriority.FirmStraight.
const uint RungFirmStraight = 6u;

// What the context reads of one car this tick: where its line has it, what its claim granted it and was cut by, the
// speed it was planning for before this tick's drive, and the place it was sent to.
struct ContextCarRow
{
    float progressM;
    float coveredSinceClaimM;
    float authorityM;
    float horizonM;
    float claimFromM;
    float committedToM;
    float grantMarginM;
    float groundCoefficient;
    float plannedMps;
    uint grantCutBy;
    uint flags;
    int cutOn;
    vec2 placeM;
    float placeReachM;
    float pad;
    vec2 positionM;
    float pad1;
    float pad2;
};

// ContextCarRow.flags.
const uint ContextOnACall = 1u;
const uint ContextLineIsReverse = 2u;
const uint ContextHasAPlace = 4u;
const uint ContextHasAHold = 8u;
const uint ContextCut = 16u;

// The junction ahead of a car (CarFleet.ToTheBoxM, BoxIsOurs, TurningAtTheBox, InsideTheBox, CommittedToTheBox,
// LightAheadM).
struct BoxAheadRow
{
    float toTheBoxM;
    float lightAheadM;
    uint flags;
    uint pad;
};

const uint BoxClaimed = 1u;
const uint BoxTurning = 2u;
const uint BoxInside = 4u;
const uint BoxCommitted = 8u;

float contextInfinity() { return uintBitsToFloat(0x7F800000u); }

// Which lane of the chain the car's own line is running down (TownWorld.LaneAheadSlot).
uint contextLaneAheadSlot(uint car, uint laneCount, float progressM)
{
    uint slots = car * LINE_WAYS_FIGURES.mostLanes;
    uint ahead = 0u;
    while (ahead + 1u < laneCount && progressM >= LANE_START[slots + ahead + 1u]) ahead++;
    return ahead;
}

// The nearest ground one roster holds in front on a way (LaneOccupancy.AheadPlanned): a row, or NoRow.
uint contextAheadPlanned(uint way, float fromM, float untilM, uint roster)
{
    uvec2 run = PLANNED_RUNS[way];
    for (uint at = run.x; at < run.x + run.y; at++)
    {
        uint row = PLANNED_ORDER[at];
        ClaimRow piece = CLAIM_ROWS[row];
        if (claimGone(piece)) continue;
        if (piece.fromM > untilM) break;
        if (claimRoster(piece) != roster || piece.toM <= fromM) continue;

        return row;
    }

    return NoRow;
}

// How far in front of a car's nose a light holds its road, out to a queue's length of it (TownWorld.LightAheadM).
float contextLightAheadM(uint car, uint laneCount, float noseM, float queueM)
{
    LineWayRow ways[LINE_MOST_WAYS];
    precise float untilM = noseM + queueM;
    uint count = lineWaysAlong(car, laneCount, noseM, untilM, ways);
    for (uint index = 0u; index < count; index++)
    {
        uint row = contextAheadPlanned(ways[index].way, ways[index].fromM, ways[index].toM, RosterSignal);
        if (row == NoRow) continue;

        precise float aheadM = lineOnTheLineM(ways[index], CLAIM_ROWS[row].fromM) - noseM;
        return max(0.0, aheadM);
    }

    return contextInfinity();
}

// The junction ahead of a car (TownWorld.ReadTheBoxAhead, but the red bar's crossing, which is an instrument's).
BoxAheadRow contextReadTheBoxAhead(uint car, CarBuildRow build, CarLineRow line, ContextCarRow row)
{
    uint slots = car * LINE_WAYS_FIGURES.mostLanes;
    precise float noseM = row.progressM + build.noseAheadOfAxleM;
    precise float queueM = build.lengthM * CONTEXT_FIGURES.queueInCarLengths;
    BoxAheadRow box = BoxAheadRow(contextInfinity(), contextLightAheadM(car, line.laneCount, noseM, queueM), 0u, 0u);

    uint ahead = contextLaneAheadSlot(car, line.laneCount, row.progressM);
    int movement = ahead + 1u < line.laneCount ? roadConnectorBetween(LANE_CHAIN[slots + ahead], LANE_CHAIN[slots + ahead + 1u]) : -1;
    if (movement >= 0 && CONNECTOR_RUNG[movement] != RungFirmStraight) box.flags |= BoxTurning;
    if (movement < 0 || CONNECTOR_LENGTH[movement] <= 0.0) return box;

    float endsM = LANE_END[slots + ahead];
    if (row.progressM >= endsM)
    {
        box.flags |= BoxInside | BoxCommitted | BoxClaimed;
        box.toTheBoxM = 0.0;
        return box;
    }

    precise float toTheBoxM = endsM - noseM;
    box.toTheBoxM = toTheBoxM;
    precise float committedM = row.committedToM - row.claimFromM;
    precise float committedReachM = noseM - row.coveredSinceClaimM + committedM;
    if (committedReachM > endsM) box.flags |= BoxCommitted;

    precise float grantLeftM = row.authorityM - row.coveredSinceClaimM;
    precise float horizonLeftM = row.horizonM - row.coveredSinceClaimM;
    precise float grantedReachM = noseM + min(grantLeftM, horizonLeftM);
    if (grantedReachM > endsM) box.flags |= BoxClaimed;
    return box;
}

// How far ahead of the rear axle a place stands along the line the car is driving, or infinity where the line does not
// come past it (TownWorld.ToThePlaceOnTheLineM).
float contextToThePlaceOnTheLineM(uint car, CarBuildRow build, CarLineRow line, ContextCarRow row)
{
    if (line.arcCount == 0u) return contextInfinity();

    uint first = car * CONTEXT_FIGURES.arcsPerCar;
    float windowM = build.sightM;
    precise float aroundM = row.progressM + windowM;
    float offSq;
    float atM = splineProject(first, first + line.arcCount, row.placeM, aroundM, windowM, offSq);
    vec2 onM;
    float headingRad;
    splineSample(first, first + line.arcCount, atM, onM, headingRad);
    precise vec2 off = onM - row.placeM;
    precise float offM = sqrt(off.x * off.x + off.y * off.y);
    if (offM > row.placeReachM) return contextInfinity();

    precise float aheadM = atM - row.progressM;
    if (atM < row.progressM)
    {
        precise vec2 apart = row.positionM - row.placeM;
        precise float apartM = sqrt(apart.x * apart.x + apart.y * apart.y);
        return apartM <= row.placeReachM ? aheadM : contextInfinity();
    }

    return aheadM;
}

// Whether a body that ended a car's grant is one it may get past (Overtaking.MayGetPast, LaneClaim.MayBePassedBy and
// MakesTheMovementOf): at rest, not a pass, and not making the car's own movement — or, on a call, anything at rest or
// any traffic going slower than it means to.
bool contextMayGetPast(ClaimRow body, int onward, bool onACall, float plannedMps)
{
    if (!claimHasBody(body) || claimPassing(body)) return false;

    bool still = (body.flags & ClaimStill) != 0u;
    bool mayBePassed;
    if (onACall)
    {
        mayBePassed = still || claimRoster(body) == RosterDriving;
    }
    else
    {
        bool makesIt = (body.flags & ClaimOnItsLine) != 0u && body.onward != LineNoWay
                       && (body.onward == onward || body.onward == LineRunsOn || onward == LineRunsOn);
        mayBePassed = still && !makesIt;
    }

    precise float slowerMps = plannedMps - CONTEXT_FIGURES.slowerToPassMps;
    return mayBePassed && (still || body.alongMps < slowerMps);
}

// Whether a car may get past what ended its grant (Overtaking.MayGetPastWhatCutIt): a hold cut by a body it may pass,
// in a lane with a lane running back beside it, on the route's line driven forwards. cutBy is read where row says the
// hold was cut at all.
bool contextWaitsToPass(uint car, CarBuildRow build, CarLineRow line, ContextCarRow row, ClaimRow cutBy)
{
    if ((row.flags & ContextHasAHold) == 0u || (row.flags & ContextCut) == 0u) return false;

    bool reverse = (row.flags & ContextLineIsReverse) != 0u;
    bool laneToPassOn = line.laneCount > 0u && !reverse && LANE_PASSABLE[LANE_CHAIN[car * LINE_WAYS_FIGURES.mostLanes]] != 0u;
    if (!laneToPassOn) return false;

    float leadingEdgeM = reverse ? build.tailBehindAxleM : build.noseAheadOfAxleM;
    precise float edgeM = row.progressM + leadingEdgeM;
    int onward = LineNoWay;
    if (row.cutOn >= 0) lineIsOnItsLine(car, line.laneCount, line.lengthM > edgeM, uint(row.cutOn), onward);
    return contextMayGetPast(cutBy, onward, (row.flags & ContextOnACall) != 0u, row.plannedMps);
}

// What a car following its line is told this tick (TownWorld.SetTheContext): its ground, what is granted and what cut
// it, the place it was sent to, whether it waits to pass, and the end of its plan where that was held short.
DriveContextRow contextSetTheContext(uint car, CarBuildRow build, CarLineRow line, ContextCarRow row, ClaimRow cutBy)
{
    float toTheSceneM = (row.flags & ContextHasAPlace) != 0u ? contextToThePlaceOnTheLineM(car, build, line, row) : contextInfinity();
    precise float grantLeftM = row.authorityM - row.coveredSinceClaimM;
    precise float horizonLeftM = row.horizonM - row.coveredSinceClaimM;
    bool waitsToPass = contextWaitsToPass(car, build, line, row, cutBy);
    return DriveContextRow(
        row.groundCoefficient, grantLeftM, row.grantCutBy, toTheSceneM, row.grantMarginM, waitsToPass ? 1u : 0u, horizonLeftM,
        0.0, 0.0, 0u, contextInfinity(), 0.0);
}
