// TownWorld.TakesOnlyTheClock on the device, past what only the town knows of a car (CarDecideTermsRow): whether its
// decision would do anything but run its leg's clock — its line spent, something it may get past that it has come up to,
// a lane beside it it means to move across onto, or its clock run out on this decision — read off what its last tick left
// and what the claims the town's readers read granted it. Every figure a verdict turns on is the host's to the bit: a
// quotient as the host divides (rounded.glsl), and no product contracted into a sum.
//
// The including kernel includes car_tick.glsl and what it asks for, rounded.glsl, and names CAR_TICK_FIGURES's
// sinceLastDecisionS and slowerToSwitchMps.

// How much road a body doing this speed needs before it can be at rest (DrivingGround.StoppingM).
float decideStoppingM(float alongMps, float brakingMps2)
{
    if (alongMps <= 0.0) return 0.0;

    precise float squared = alongMps * alongMps;
    precise float twice = 2.0 * brakingMps2;
    return roundedQuotient(squared, twice);
}

// Whether a car has come to where it would begin slowing to wait for what stands this far ahead (Overtaking.ComesUpTo),
// its lead the one with no throttle to release (CarFollower.LeadS).
bool decideComesUpTo(CarBuildRow build, float alongMps, float toTheStopM)
{
    float speedMps = max(0.0, alongMps);
    float brakingMps2 = followBrakingMps2(build);
    precise float waitingMps2 = brakingMps2 * CAR_TICK_FIGURES.waitingToPassBrakingShare;
    precise float pedalTwice = 2.0 * build.pedalRateMps3;
    precise float leadS = CAR_TICK_FIGURES.reactionS + roundedQuotient(brakingMps2, pedalTwice);
    precise float slowsFromM = decideStoppingM(speedMps, waitingMps2) + speedMps * leadS + CAR_TICK_FIGURES.passSpareM;
    return toTheStopM <= slowsFromM;
}

// Which way along its own line a car takes on from the way its hold was cut on (DrivingGround.OnwardAlongTheLine).
int decideOnward(uint car, CarBuildRow build, CarLineRow line, CarTickRow tick, bool reverse, int cutOn)
{
    float leadingEdgeM = reverse ? build.tailBehindAxleM : build.noseAheadOfAxleM;
    precise float edgeM = tick.progressM + leadingEdgeM;
    int onward = LineNoWay;
    if (cutOn >= 0) lineIsOnItsLine(car, line.laneCount, line.lengthM > edgeM, uint(cutOn), onward);
    return onward;
}

// Whether what ended a car's grant holds it up (Switching.IsHeldUp): a body at rest it may get past, or traffic going
// slower than it means to by more than it switches for — once it has come to where it would begin slowing for it.
bool decideIsHeldUp(uint car, CarBuildRow build, CarLineRow line, CarTickRow tick, uint holdBits, ClaimRow cutBy, int cutOn,
    bool reverse, float grantLeftM)
{
    if ((holdBits & ContextHasAHold) == 0u || (holdBits & ContextCut) == 0u) return false;
    if (!claimHasBody(cutBy) || claimPassing(cutBy)) return false;

    int onward = decideOnward(car, build, line, tick, reverse, cutOn);
    bool makesIt = (cutBy.flags & ClaimOnItsLine) != 0u && cutBy.onward != LineNoWay
                   && (cutBy.onward == onward || cutBy.onward == LineRunsOn || onward == LineRunsOn);
    bool mayBePassed = (cutBy.flags & ClaimStill) != 0u && !makesIt;
    precise float slowerMps = tick.plannedMps - CAR_TICK_FIGURES.slowerToSwitchMps;
    bool slower = claimRoster(cutBy) == RosterDriving && cutBy.alongMps < slowerMps;
    return (mayBePassed || slower) && decideComesUpTo(build, tick.alongMps, grantLeftM);
}

// Whether a car following its route means to move across (Switching.Wants): what the road and the route answer, and,
// where that leaves it to the claims, whether it is held up.
bool decideWants(uint car, CarBuildRow build, CarLineRow line, CarTickRow tick, uint shape, uint holdBits, ClaimRow cutBy,
    int cutOn, bool reverse, float grantLeftM)
{
    if ((shape & DecideShapeStopsToMoveAcross) != 0u) return (shape & DecideShapeWants) != 0u;

    bool heldUp = (shape & DecideShapeAsks) != 0u && decideIsHeldUp(car, build, line, tick, holdBits, cutBy, cutOn, reverse, grantLeftM);
    return (shape & (heldUp ? DecideShapeWantsWhenHeldUp : DecideShapeWantsWhenFree)) != 0u;
}

// How much of a car's leg is left to drive (TownWorld.RemainingOnTheDriveM): what remains of the lane it is on, with the
// ground back onto its line counted.
float decideRemainingM(uint car, CarLineRow line, CarTickRow tick, float allowanceM)
{
    precise float offM = tick.offLineM - allowanceM;
    float backOntoItM = max(0.0, offM);
    if (line.arcCount == 0u) return backOntoItM;

    float endsAtM = line.laneCount > 0u ? LANE_END[car * LINE_FIGURES.mostLanes] : line.lengthM;
    precise float leftM = endsAtM - tick.progressM;
    precise float remainingM = backOntoItM + max(0.0, leftM);
    return remainingM;
}

// Whether a car's decision does anything but run its leg's clock (TownWorld.TakesOnlyTheClock), and its clock — how long
// it has got nowhere and the closest it has come (LegProgress) — run where it does not. A clock that would run out is
// left as it stood: the town's decision runs it.
bool decideMoreThanTheClock(
    uint car, CarBuildRow build, CarLineRow line, CarTickRow tick, CarToldRow told, CarBriefRow brief, uint holdBits,
    ClaimRow cutBy, int cutOn, CarDecideTermsRow terms, inout vec2 clock)
{
    if ((terms.flags & DecideByTheTown) != 0u || (tick.flags & TickEvents) != 0u) return true;

    // TownWorld.TheLineIsSpent, for a car on no manoeuvre.
    precise float toTheEndM = line.lengthM - tick.progressM;
    if (line.arcCount > 0u && toTheEndM <= build.lengthM && abs(tick.alongMps) <= CAR_TICK_FIGURES.stopSpeedMps) return true;

    // Overtaking.MayGetPastWhatCutIt, as the tick asks it, and ComesUpTo the grant its last tick left.
    bool reverse = (told.flags & ContextLineIsReverse) != 0u;
    float grantLeftM = brief.authorityM;
    uint flags = told.flags | (holdBits & (ContextHasAHold | ContextCut));
    ContextCarRow row = ContextCarRow(
        tick.progressM, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, tick.plannedMps, 0u, flags, cutOn, 0.0, vec2(0.0), 0.0, 0.0, vec2(0.0), 0.0,
        0.0);
    if (contextWaitsToPass(car, build, line, row, cutBy) && decideComesUpTo(build, tick.alongMps, grantLeftM)) return true;

    uint shape = terms.flags & (DecideShapeAsks | DecideShapeStopsToMoveAcross | DecideShapeWants | DecideShapeWantsWhenHeldUp | DecideShapeWantsWhenFree);
    if (decideWants(car, build, line, tick, shape, holdBits, cutBy, cutOn, reverse, grantLeftM)) return true;

    float remainingM = decideRemainingM(car, line, tick, told.allowanceM);
    float sinceS = clock.x;
    float closestM = clock.y;
    if (brief.lightAheadM <= terms.queueReachM)
    {
        if (sinceS >= terms.patienceS) return true;

        clock.y = min(closestM, remainingM);
        return false;
    }

    precise float nearerThanM = closestM - terms.byM;
    if (remainingM < nearerThanM)
    {
        clock = vec2(0.0, remainingM);
        return false;
    }

    precise float runS = sinceS + CAR_TICK_FIGURES.sinceLastDecisionS;
    if (runS >= terms.patienceS) return true;

    clock.x = runS;
    return false;
}
