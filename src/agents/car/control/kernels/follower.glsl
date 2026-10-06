// CarFollower on the device: one tick of a car driving its line — the wheel by pure pursuit, the speed as the least of
// everything that limits it, one pedal or the other, and the margin spent where the held section cannot be stopped
// in (CarFollower.DriveTheLine, Steer, TargetSpeedMps, Pedals, IsAHazard). Line for line with the host, `precise`
// where it rounds twice; tan and atan are the device's.
//
// A pass the car is on (CAR-46) is not driven here yet: a car with one begun is the host's.
//
// The including kernel includes arc.glsl, car_build.glsl, tyre.glsl and arc_chain.glsl (its SPLINE_ARCS the lines)
// first, and supplies FOLLOWER_FIGURES' fields: lookaheadS, gripMargin, brakingMargin, stopSpeedMps,
// waitingToPassBrakingShare, reactionS, callPaceMps, dtS.

// What the driver has been told about the world this tick (DriveContext).
struct DriveContextRow
{
    float groundCoefficient;
    float authorityM;
    uint grantCutBy;
    float placeStopM;
    float marginM;
    uint waitsToPass;
    float horizonM;
    float passAsideM;
    float backUpM;
    uint blocked;
    float waitAtM;
    float pad;
};

// DrivingHold, by number.
const uint HoldNone = 0u;
const uint HoldCorner = 1u;
const uint HoldWheel = 2u;
const uint HoldLineEnd = 3u;
const uint HoldClaimed = 4u;
const uint HoldWaiting = 5u;
const uint HoldReach = 6u;
const uint HoldPlace = 7u;

// HeadwayKind.Light, by number.
const uint HeadwayLight = 4u;

float followInfinity() { return uintBitsToFloat(0x7F800000u); }

vec2 followRearAxleM(CarBuildRow car, vec2 positionM, vec2 forward)
{
    precise vec2 rear = positionM - forward * car.centreAheadOfAxleM;
    return rear;
}

float followLookaheadM(CarBuildRow car, float speedMps)
{
    precise float aheadM = speedMps * FOLLOWER_FIGURES.lookaheadS;
    return clamp(aheadM, car.lookaheadFloorM, car.lookaheadCeilingM);
}

float followPursuitBend(vec2 rearAxleM, vec2 forward, vec2 leadM)
{
    precise vec2 toLead = leadM - rearAxleM;
    float reachM = tyreLength(toLead);
    if (reachM < 1e-3) return 0.0;

    precise float bend = 2.0 * tyreCross(forward, toLead) / (reachM * reachM);
    return bend;
}

float followSteerFor(CarBuildRow car, float curvature)
{
    precise float turned = curvature * car.wheelbaseM;
    return clamp(atan(turned), -car.maxSteerRad, car.maxSteerRad);
}

float followSteer(CarBuildRow car, uint first, uint end, float progressM, vec2 rearAxleM, vec2 forward, float lookaheadM)
{
    precise float atM = progressM + lookaheadM;
    vec2 leadM;
    float headingRad;
    splineSample(first, end, atM, leadM, headingRad);
    return followSteerFor(car, followPursuitBend(rearAxleM, forward, leadM));
}

float followLeadS(CarBuildRow car, float brakingMps2, float throttleMps2)
{
    float onMps2 = max(0.0, throttleMps2);
    precise float releaseS = onMps2 / car.pedalRateMps3;
    precise float leadS = FOLLOWER_FIGURES.reactionS + (brakingMps2 / (2.0 * car.pedalRateMps3))
                          + (releaseS * (1.0 + (onMps2 / (2.0 * brakingMps2))));
    return leadS;
}

float followBrakingMps2(CarBuildRow car, float groundCoefficient)
{
    precise float braking = buildUtmostBrakingMps2(car, groundCoefficient) * FOLLOWER_FIGURES.brakingMargin;
    return braking;
}

float followCorneringMps2(CarBuildRow car, float groundCoefficient)
{
    precise float cornering = car.gripMps2 * groundCoefficient * FOLLOWER_FIGURES.gripMargin;
    return cornering;
}

float followApproachMps(float atMps, float distanceM, float brakingMps2)
{
    if (distanceM <= 0.0) return atMps;

    precise float squared = atMps * atMps + 2.0 * brakingMps2 * distanceM;
    return sqrt(squared);
}

float followCornerMps(float curvature, float lateralMps2)
{
    float bend = abs(curvature);
    return bend < 1e-4 ? followInfinity() : sqrt(lateralMps2 / bend);
}

void followBind(inout float targetMps, float limitMps, uint limit, inout uint hold)
{
    if (limitMps >= targetMps) return;

    targetMps = limitMps;
    hold = limit;
}

// The least of everything that limits a car, and what it would have asked for with the road to itself
// (CarFollower.TargetSpeedMps). entryAt is where the line's first arc's entry figure stands in FOLLOWER_ENTRIES.
float followTargetSpeedMps(
    CarBuildRow car, uint first, uint end, uint entryAt, float progressM, float lineLengthM, float steerRad, float alongMps,
    float lookaheadM, DriveContextRow context, float lastMps2, out uint hold, out float plannedMps)
{
    hold = HoldNone;
    float lateralMps2 = followCorneringMps2(car, context.groundCoefficient);
    float brakingMps2 = followBrakingMps2(car, context.groundCoefficient);
    precise float leadM = abs(alongMps) * followLeadS(car, brakingMps2, lastMps2);

    float targetMps = car.maxSpeedMps;
    float gripMps2 = buildUtmostBrakingMps2(car, context.groundCoefficient);
    precise float reachedM = progressM + leadM + lookaheadM;
    float startM = 0.0;
    for (uint arc = first; arc < end; arc++)
    {
        Arc piece = SPLINE_ARCS[arc];
        precise float endM = startM + arcLengthM(piece);
        if (endM >= progressM)
        {
            if (startM > reachedM)
            {
                precise float entryMps = sqrt(gripMps2 * FOLLOWER_ENTRIES[entryAt + (arc - first)]);
                precise float toItM = startM - reachedM;
                followBind(targetMps, followApproachMps(entryMps, toItM, brakingMps2), HoldCorner, hold);
                break;
            }

            followBind(targetMps, followCornerMps(arcCurvature(piece), lateralMps2), HoldCorner, hold);
        }

        startM = endM;
    }

    precise float toTheEndM = lineLengthM - progressM - leadM;
    followBind(targetMps, followApproachMps(0.0, toTheEndM, brakingMps2), HoldLineEnd, hold);
    precise float toThePlaceM = context.placeStopM - leadM;
    followBind(targetMps, followApproachMps(0.0, toThePlaceM, brakingMps2), HoldPlace, hold);

    plannedMps = max(0.0, targetMps);

    precise float wheelBend = tan(steerRad) / car.wheelbaseM;
    followBind(targetMps, followCornerMps(wheelBend, lateralMps2), HoldWheel, hold);
    precise float toTheHorizonM = context.horizonM - leadM;
    followBind(targetMps, followApproachMps(0.0, toTheHorizonM, brakingMps2), HoldReach, hold);
    precise float toTheGrantM = context.authorityM - leadM;
    followBind(
        targetMps, followApproachMps(0.0, toTheGrantM, brakingMps2), context.grantCutBy == HeadwayLight ? HoldWaiting : HoldClaimed,
        hold);

    if (context.waitsToPass != 0u)
    {
        precise float gently = brakingMps2 * FOLLOWER_FIGURES.waitingToPassBrakingShare;
        followBind(targetMps, followApproachMps(0.0, toTheGrantM, gently), HoldClaimed, hold);
    }

    precise float toTheWaitM = context.waitAtM - leadM;
    followBind(targetMps, followApproachMps(0.0, toTheWaitM, brakingMps2), HoldClaimed, hold);

    return max(0.0, targetMps);
}

float followBrakeCeilingMps2(CarBuildRow car, float steerRad, float alongMps, float groundCoefficient)
{
    precise float acrossMps2 = alongMps * alongMps * abs(tan(steerRad)) / car.wheelbaseM;
    precise float patchMps2 = car.gripMps2 * groundCoefficient;
    float leftMps2 = tyreDriveLeftMps2(patchMps2, acrossMps2);
    precise float ceiling = min(car.brakingMps2, leftMps2) * FOLLOWER_FIGURES.brakingMargin;
    return ceiling;
}

// One pedal or the other, never both, the foot travelling at the car's own rate (CarFollower.Pedals).
TyreCommand followPedals(CarBuildRow car, float steerRad, float targetMps, float alongMps, float groundCoefficient, float lastMps2)
{
    if (targetMps <= FOLLOWER_FIGURES.stopSpeedMps && abs(alongMps) <= FOLLOWER_FIGURES.stopSpeedMps)
    {
        return TyreCommand(steerRad, 0.0, 0.0, 1u);
    }

    precise float travelMps2 = car.pedalRateMps3 * FOLLOWER_FIGURES.dtS;
    precise float asked = (targetMps - alongMps) / FOLLOWER_FIGURES.dtS;
    precise float least = lastMps2 - travelMps2;
    precise float most = lastMps2 + travelMps2;
    float wantedMps2 = clamp(asked, least, most);
    return wantedMps2 >= 0.0
        ? TyreCommand(steerRad, min(wantedMps2, car.accelerationMps2), 0.0, 0u)
        : TyreCommand(steerRad, 0.0, min(-wantedMps2, followBrakeCeilingMps2(car, steerRad, alongMps, groundCoefficient)), 0u);
}

bool followIsAHazard(CarBuildRow car, float alongMps, DriveContextRow context)
{
    if (alongMps <= FOLLOWER_FIGURES.stopSpeedMps || (isinf(context.authorityM) && context.authorityM > 0.0)) return false;

    precise float heldM = context.authorityM + context.marginM;
    if (heldM <= 0.0) return true;

    precise float needs = alongMps * alongMps / (2.0 * heldM);
    return needs > buildUtmostBrakingMps2(car, context.groundCoefficient);
}

float followPedalMps2(TyreCommand command)
{
    precise float leaning = command.throttleMps2 - command.brakeMps2;
    return leaning;
}

// One tick of a car on its line (CarFollower.DriveTheLine), for a car on no pass.
TyreCommand followDriveTheLine(
    CarBuildRow car, TyrePose pose, uint first, uint end, uint entryAt, float progressM, float lengthM, DriveContextRow context,
    vec2 travel, float alongMps, bool reverse, TyreCommand last, bool laneless, bool blueLight, float paceMps,
    out uint hold, out float plannedMps, out bool hazard)
{
    vec2 rearAxleM = followRearAxleM(car, pose.positionM, pose.forward);
    float lookaheadM = followLookaheadM(car, abs(alongMps));

    float wasRad = reverse ? -last.steerRad : last.steerRad;
    float wantedRad = followSteer(car, first, end, progressM, rearAxleM, travel, lookaheadM);
    float steerRad = buildWheelWoundTo(car, wasRad, wantedRad, FOLLOWER_FIGURES.dtS);

    float lastMps2 = followPedalMps2(last);
    float targetMps = followTargetSpeedMps(
        car, first, end, entryAt, progressM, lengthM, steerRad, alongMps, lookaheadM, context, lastMps2, hold, plannedMps);

    float capMps = reverse || laneless ? car.reverseMaxMps : followInfinity();
    if (blueLight) capMps = min(capMps, FOLLOWER_FIGURES.callPaceMps);
    capMps = min(capMps, paceMps);
    targetMps = min(targetMps, capMps);

    TyreCommand command = followPedals(car, steerRad, targetMps, alongMps, context.groundCoefficient, lastMps2);
    if (reverse)
    {
        command.steerRad = -command.steerRad;
        command.handbrakeAndReverse |= 2u;
    }

    hazard = followIsAHazard(car, alongMps, context);
    if (hazard)
    {
        command.throttleMps2 = 0.0;
        command.brakeMps2 = car.brakingMps2;
        command.handbrakeAndReverse &= ~1u;
    }

    plannedMps = min(plannedMps, capMps);
    return command;
}
