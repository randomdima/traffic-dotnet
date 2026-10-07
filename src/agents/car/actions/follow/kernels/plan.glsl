// A car's claim of one rebuild, on the device (DrivingGround.ClearTheClaim, Following.Plan, WaysAlong, LevelTheRungs,
// AskOn; LaneOccupancy.Propose, PlaceTheMarks): the claim of the rebuild before forgotten, how far the car means to be
// able to stop, the ways of its own line under that, the rung each is held at, and every piece of it laid whole with
// its secondary claims through the marks — as rows in the claims' layout (claims_rows.glsl), in the host's order.
//
// Every quotient is the host's float (rounded.glsl): a last bit off moves where a plan ends, and with it who keeps the
// ground there.
//
// The including kernel includes arc.glsl, car_build.glsl, car_line.glsl, claims_rows.glsl, plan_rows.glsl,
// rounded.glsl and line_ways.glsl (with the buffers it reads: the line's own layout and the road's connectors), declares
// SPLINE_ARCS, LINE_ENTRIES and JOIN_BREAKS of the line, CONNECTOR_RUNG (uint[] a connector: its ClaimPriority), MARK_AT
// (uint[] a way, and one past the last), MARKS (CrossedSection[]) and PLAN_PIECES (ClaimRow[]), and supplies
// PLAN_FIGURES with the fields below's names.
//
// A car on a pass is not planned here yet: its plan begins past the pass and a body inside the pass is its own hold.

float planInfinity() { return uintBitsToFloat(0x7F800000u); }

// How much road a body doing this speed needs before it can be at rest (DrivingGround.StoppingM).
float planStoppingM(float alongMps, float brakingMps2)
{
    if (alongMps <= 0.0) return 0.0;

    precise float squared = alongMps * alongMps;
    precise float twice = 2.0 * brakingMps2;
    return roundedQuotient(squared, twice);
}

// Where a car means to be able to stop (Following.MeansToM).
float planMeansToM(float alongMps, float plannedMps, float accelerationMps2, float brakingMps2)
{
    float runS = PLAN_FIGURES.plannedRunS;
    if (plannedMps <= alongMps)
    {
        precise float heldM = plannedMps * runS;
        precise float meansM = heldM + planStoppingM(plannedMps, brakingMps2);
        return meansM;
    }

    precise float toClimbMps = plannedMps - alongMps;
    float climbS = min(runS, roundedQuotient(toClimbMps, accelerationMps2));
    precise float reachedMps = alongMps + accelerationMps2 * climbS;
    precise float coveredM = (alongMps * climbS) + (0.5 * accelerationMps2 * climbS * climbS) + (reachedMps * (runS - climbS));
    precise float meansM = coveredM + planStoppingM(reachedMps, brakingMps2);
    return meansM;
}

// How far along the line the corners let anything past fromM come to rest (CornerLimits.RestToM).
float planRestToM(uint car, uint arcCount, float fromM)
{
    uint first = car * PLAN_FIGURES.arcsPerCar;
    float startM = 0.0;
    for (uint arc = 0u; arc < arcCount; arc++)
    {
        if (startM > fromM)
        {
            precise float twice = 2.0 * PLAN_FIGURES.brakingMargin;
            precise float restM = startM + roundedQuotient(LINE_ENTRIES[first + arc], twice);
            return restM;
        }

        precise float nextM = startM + arcLengthM(SPLINE_ARCS[first + arc]);
        startM = nextM;
    }

    return planInfinity();
}

// How far a plan begun at fromM may reach (Following.HorizonToM).
float planHorizonToM(uint car, uint laneCount, float fromM, CarBuildRow build, float reach)
{
    precise float planM = min(PLAN_FIGURES.planMostM, build.sightM) * reach;
    precise float toM = fromM + planM;
    precise float joins = PLAN_FIGURES.planMostJoins * reach;
    int mostJoins = int(roundEven(joins));
    uint slots = car * PLAN_FIGURES.mostLanes;
    int passed = 0;
    for (uint slot = 0u; slot + 1u < laneCount && LANE_END[slots + slot] < toM; slot++)
    {
        if (LANE_END[slots + slot] <= fromM || JOIN_BREAKS[slots + slot] == 0u) continue;

        passed++;
        if (passed > mostJoins) return LANE_END[slots + slot];
    }

    return toM;
}

// The first join between two lanes of the line its nose is not yet out of (Following.TheNextBox).
bool planTheNextBox(uint car, uint laneCount, float noseM, out float mouthM, out float endsAtM)
{
    uint slots = car * PLAN_FIGURES.mostLanes;
    for (uint slot = 0u; slot + 1u < laneCount; slot++)
    {
        float startsM = LANE_START[slots + slot + 1u];
        if (noseM >= startsM || startsM <= LANE_END[slots + slot]) continue;

        mouthM = LANE_END[slots + slot];
        endsAtM = startsM;
        return true;
    }

    mouthM = 0.0;
    endsAtM = 0.0;
    return false;
}

// One piece of a plan as the terms it is asked on (Following.AskOn), as the row it lays (PlannedAsk.Laid): its holder's
// body reaches to where the piece begins.
ClaimRow planAsk(int car, int hold, LineWayRow way, uint rung, float claimFromM, float committedToM, float alongMps)
{
    precise float aheadM = way.lineFromM - claimFromM;
    precise float committedM = way.fromM + (committedToM - way.lineFromM);
    return ClaimRow(way.fromM, way.toM, alongMps, way.lineFromM, aheadM, committedM, way.fromM, car, hold, -1, way.way, rung);
}

// Every mark over [ask.fromM, ask.toM) of a main claim's way, the whole of its section of the other way laid as a
// secondary claim of the same hold (LaneOccupancy.PlaceTheMarks), at pieces[at..end); where the next goes.
uint planPlaceTheMarks(ClaimRow ask, uint at, uint end)
{
    for (uint mark = MARK_AT[ask.way]; mark < MARK_AT[ask.way + 1u]; mark++)
    {
        CrossedSection section = MARKS[mark];
        if (section.mineFromM >= ask.toM) break;
        if (section.mineToM <= ask.fromM) continue;

        float atM = max(ask.fromM, section.mineFromM);
        precise float pastM = max(0.0, atM - ask.fromM);
        precise float lineM = ask.lineFromM + pastM;
        precise float arrivalM = ask.aheadM + pastM;
        float committedToM = atM <= ask.committedToM ? planInfinity() : -planInfinity();
        if (at < end)
        {
            PLAN_PIECES[at++] = ClaimRow(
                section.fromM, section.toM, 0.0, lineM, arrivalM, committedToM, section.fromM, ask.occupant, ask.hold, -1,
                uint(section.onWay), (ask.flags & ClaimRungBits) | ClaimSecondary);
        }
    }

    return at;
}

// One car's claim of this rebuild (DrivingGround.ClearTheClaim, then Following.Plan for a car planning down its line),
// its pieces laid at PLAN_PIECES[car * PLAN_FIGURES.mostPieces ..]. hold is the hold the host numbered its plan, which
// is the hold the pieces carry.
PlanOutRow planTheCar(uint car, PlanCarRow row, CarBuildRow build, CarLineRow line, int hold)
{
    bool reverse = (row.flags & PlanCarLineIsReverse) != 0u;
    float leadingEdgeM = reverse ? build.tailBehindAxleM : build.noseAheadOfAxleM;
    precise float noseM = row.progressM + leadingEdgeM;
    float keptOffM = row.grantMarginM;
    bool onACall = (row.flags & PlanCarOnACall) != 0u;
    precise float passesBelowMps = row.plannedMps - PLAN_FIGURES.slowerToPassMps;
    PlanOutRow result = PlanOutRow(noseM, noseM, noseM, planInfinity(), onACall ? passesBelowMps : -planInfinity(), 0u, 0u, 0u);
    if ((row.flags & PlanCarProposes) == 0u) return result;

    float standOffM = PLAN_FIGURES.standOffM;
    precise float brakingMps2 = buildUtmostBrakingMps2(build) * PLAN_FIGURES.brakingMargin;
    float alongMps = max(0.0, row.alongMps);
    float plannedMps = row.plannedMps;
    precise float committedM = (alongMps * PLAN_FIGURES.reactionS) + planStoppingM(alongMps, brakingMps2);

    precise float pulledMps = alongMps + (build.accelerationMps2 * PLAN_FIGURES.reactionS);
    float reachableMps = min(plannedMps, pulledMps);
    precise float pullsAwayM = (reachableMps * PLAN_FIGURES.reactionS) + planStoppingM(reachableMps, brakingMps2) + max(standOffM, keptOffM);

    bool moving = alongMps > PLAN_FIGURES.stopSpeedMps;
    precise float meansM = moving ? planMeansToM(alongMps, plannedMps, build.accelerationMps2, brakingMps2) + standOffM : 0.0;
    float meantM = max(pullsAwayM, meansM);
    if (moving)
    {
        precise float cornersM = planRestToM(car, line.arcCount, noseM) - noseM + standOffM;
        meantM = min(meantM, cornersM);
    }

    float reach = onACall ? PLAN_FIGURES.callReachShare : 1.0;
    precise float reachedM = meantM * reach;
    float wantedM = max(committedM, reachedM);

    precise float lengthM = line.lengthM + leadingEdgeM;
    precise float wantedEndM = noseM + wantedM;
    float wantedToM = min(wantedEndM, lengthM);
    precise float committedToM = noseM + committedM;

    float planFromM = noseM;
    float planToM = min(max(committedToM, min(wantedToM, planHorizonToM(car, line.laneCount, planFromM, build, reach))), lengthM);

    float mouthM;
    float boxEndsAtM;
    if (planTheNextBox(car, line.laneCount, noseM, mouthM, boxEndsAtM) && noseM >= mouthM)
    {
        precise float outM = boxEndsAtM + build.lengthM + standOffM;
        planToM = max(planToM, min(outM, lengthM));
    }

    result.claimToM = planToM;
    result.committedToM = committedToM;
    if (planToM < wantedToM)
    {
        precise float horizonM = planToM - noseM;
        result.horizonM = horizonM;
    }

    if (planToM <= planFromM) return result;

    result.laidAHold = 1u;
    LineWayRow ways[LINE_MOST_WAYS];
    uint count = lineWaysAlong(car, line.laneCount, planFromM, planToM, ways);
    result.ways = count;

    uint rungs[LINE_MOST_WAYS];
    uint next = RungFirm;
    for (int index = int(count) - 1; index >= 0; index--)
    {
        if (ways[index].way >= PLAN_FIGURES.firstConnector) next = CONNECTOR_RUNG[ways[index].way - PLAN_FIGURES.firstConnector];
        rungs[index] = onACall ? RungSpecial : next;
    }

    for (uint index = 1u; index < count; index++) rungs[index] = max(rungs[index], rungs[index - 1u]);

    uint first = car * PLAN_FIGURES.mostPieces;
    uint at = first;
    uint end = first + PLAN_FIGURES.mostPieces;
    for (uint index = 0u; index < count; index++)
    {
        ClaimRow ask = planAsk(int(car), hold, ways[index], rungs[index], noseM, committedToM, row.alongMps);
        if (ask.toM <= ask.fromM) continue;

        at = planPlaceTheMarks(ask, at, end);
        if (at < end) PLAN_PIECES[at++] = ask;
    }

    result.pieces = at - first;
    return result;
}
