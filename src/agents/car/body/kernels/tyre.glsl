// TyreModel on the device: one car's patches for one tick — an impulse and a place to spend it per wheel, what
// each did to the ground, and the rims wound on (TyreModel.Step, AckermannTurns, WheelPointsM, Rim, Scrub,
// RollingDrag, HoldStill), each wheel weighed by the share of the car it carries (suspension.glsl). Line for line with the host, `precise` where it rounds twice; its trigonometry is trig.glsl's
// and its quotients and roots rounded.glsl's.
//
// The including kernel supplies the tyre figures as TYRE_FIGURES' fields: wheelSpinFadeMps, wheelSpinAllowanceMps,
// markSlipMps, markCorneringSlipMps, ploughCrawlMps, engineBrakingMps2, workingSlipTangent, alongSlipEpsilonMps,
// liftedAxleLoadShare, ackermannLeastSteerRad, innerWheelLeastRadiusM (TyreFigures) — and includes car_build.glsl and
// tyre_rows.glsl first.

#include "core/geometry/kernels/trig.glsl"
#include "core/geometry/kernels/rounded.glsl"

const int TyreWheels = CarMostWheels;

// The ground under one patch (SurfaceUnderWheel).
struct TyreSurface
{
    float coefficient;
    float dragMps2;
    float markThresholdM2S3;
    uint ploughs;
};

// What one patch did to its ground (TyreScrub): slide power, plough power, slide speed, and ploughing | sliding << 1.
struct TyreScrub
{
    float slidePowerM2S3;
    float ploughPowerM2S3;
    float slideSpeedMps;
    uint flags;
};

bool tyreLocksEveryWheel(TyreCommand command) { return floatIsInfinite(command.brakeMps2) && command.brakeMps2 > 0.0; }
bool tyreHandbrake(TyreCommand command) { return (command.handbrakeAndReverse & 1u) != 0u; }
float tyreGearSign(TyreCommand command) { return (command.handbrakeAndReverse & 2u) != 0u ? -1.0 : 1.0; }

float tyreDot(vec2 a, vec2 b) { precise float d = a.x * b.x + a.y * b.y; return d; }
float tyreCross(vec2 a, vec2 b) { precise float c = (a.x * b.y) - (a.y * b.x); return c; }
float tyreLength(vec2 v) { return roundedLength(v); }

void tyreWheelPointsM(CarBuildRow car, TyrePose pose, out vec2 atM[TyreWheels])
{
    vec2 forward = pose.forward;
    vec2 right = vec2(-forward.y, forward.x);
    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        if (wheel >= int(car.wheelCount)) break;

        vec2 atBody = buildWheelAtM(car, wheel);
        precise vec2 at = pose.positionM + (forward * atBody.x) + (right * atBody.y);
        atM[wheel] = at;
    }
}

// The steered wheels' turns as the directions they turn the car's by, at each wheel's own index
// (TyreModel.AckermannTurns); only for a rack off nought.
void tyreAckermannTurns(CarBuildRow car, float steerRad, out vec2 into[TyreWheels])
{
    if (abs(steerRad) < TYRE_FIGURES.ackermannLeastSteerRad)
    {
        precise float rackSquared = 1.0 + (steerRad * steerRad);
        float rackM = roundedSqrt(rackSquared);
        vec2 rack = vec2(roundedQuotient(1.0, rackM), roundedQuotient(steerRad, rackM));
        for (int wheel = 0; wheel < TyreWheels; wheel++) into[wheel] = rack;
        return;
    }

    // The circle's centre on the line of the axle that does not steer, as far to the side the car turns (TyreModel.OffTheCentre).
    float radiusM = abs(roundedQuotient(car.wheelbaseM, trigTan(steerRad)));
    float s = sign(steerRad);
    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        into[wheel] = vec2(1.0, 0.0);
        if (wheel >= int(car.wheelCount) || !buildWheelIs(car, wheel, WheelSteered)) continue;

        vec2 atBody = buildWheelAtM(car, wheel);
        precise float offRaw = radiusM - (s * atBody.y);
        float offM = max(TYRE_FIGURES.innerWheelLeastRadiusM, offRaw);
        precise float aheadM = atBody.x + car.centreAheadOfAxleM;
        precise float squared = (offM * offM) + (aheadM * aheadM);
        float reachM = roundedSqrt(squared);
        precise float turnedAhead = s * aheadM;
        into[wheel] = vec2(roundedQuotient(offM, reachM), roundedQuotient(turnedAhead, reachM));
    }
}

// A direction turned by a turn, both unit vectors (TyreModel.Turned).
vec2 tyreTurned(vec2 direction, vec2 turn)
{
    precise vec2 turned = vec2((direction.x * turn.x) - (direction.y * turn.y), (direction.y * turn.x) + (direction.x * turn.y));
    return turned;
}

float tyreNoLargerThan(float value, float most) { return sign(value) * min(abs(value), abs(most)); }

float tyreSyncMassKg(CarBuildRow car, float loadKg)
{
    if (loadKg <= 0.0) return 0.0;

    precise float sum = car.inverseWheelRotatingMassPerKg + roundedQuotient(1.0, loadKg);
    return roundedQuotient(1.0, sum);
}

float tyreAxleShare(float wholeMps2, float share, float axleLoad)
{
    if (!(axleLoad > TYRE_FIGURES.liftedAxleLoadShare)) return 0.0;

    // A figure of nought is nought, its sign kept, without the rounded quotient: either the drive or the engine's hold is
    // nought on most ticks, and the quotient is dear.
    precise float placed = wholeMps2 * share;
    return wholeMps2 == 0.0 ? placed : roundedQuotient(placed, axleLoad);
}

float tyreSpinAllowanceMps(float alongMps)
{
    if (TYRE_FIGURES.wheelSpinFadeMps <= 0.0) return TYRE_FIGURES.wheelSpinAllowanceMps;

    precise float faded =
        TYRE_FIGURES.wheelSpinAllowanceMps * clamp(1.0 - roundedQuotient(abs(alongMps), TYRE_FIGURES.wheelSpinFadeMps), 0.0, 1.0);
    return faded;
}

float tyreEngineBrakingMps2(CarBuildRow car, TyreCommand command)
{
    if (tyreLocksEveryWheel(command) || car.accelerationMps2 <= 0.0) return 0.0;

    precise float held =
        TYRE_FIGURES.engineBrakingMps2 * (1.0 - clamp(roundedQuotient(command.throttleMps2, car.accelerationMps2), 0.0, 1.0));
    return held;
}

float tyreDriveLeftMps2(float gripMps2, float acrossMps2)
{
    if (gripMps2 <= 0.0) return 0.0;

    float spent = roundedQuotient(abs(acrossMps2), gripMps2);
    precise float left = 1.0 - (spent * spent);
    precise float drive = gripMps2 * roundedSqrt(max(0.0, left));
    return drive;
}

float tyreRim(
    CarBuildRow car, float spinMps, float alongMps, float alongAfterMps, float wantAlongNs, float askedAlongNs, bool sliding,
    float loadKg, float driveMps2, float brakeMps2, float spinAllowanceMps, float dtS)
{
    float rotatingKg = car.wheelRotatingMassKg;
    precise float pedalledMps = spinMps + (roundedQuotient(driveMps2 * loadKg, rotatingKg) * dtS);
    precise float freeMps = pedalledMps - roundedQuotient(wantAlongNs, rotatingKg);

    precise float either = (pedalledMps - alongAfterMps) * (freeMps - alongAfterMps);
    if (either < 0.0) freeMps = alongAfterMps;

    if (brakeMps2 > 0.0)
    {
        precise float braked = roundedQuotient(brakeMps2 * loadKg, rotatingKg) * dtS;
        freeMps -= sign(freeMps) * min(braked, abs(freeMps));
    }

    precise float capMps = abs(alongMps) + spinAllowanceMps;
    float spunMps = clamp(freeMps, -capMps, capMps);

    precise float holds = abs(wantAlongNs) + (TYRE_FIGURES.alongSlipEpsilonMps * loadKg);
    return !sliding || abs(askedAlongNs) <= holds ? alongAfterMps : spunMps;
}

TyreScrub tyreScrub(
    TyreSurface surface, float wantAlongNs, float wantAcrossNs, vec2 dragNs, vec2 velocityMps, float alongSlipMps,
    float acrossSlipMps, float spinMps, bool sliding, float loadKg, float dtS)
{
    precise float perKg = loadKg * dtS;
    if (perKg <= 0.0) return TyreScrub(0.0, 0.0, 0.0, 0u);

    precise float alongSlideMps = max(abs(alongSlipMps) - TYRE_FIGURES.markSlipMps, 0.0);
    precise float working = abs(spinMps) * TYRE_FIGURES.workingSlipTangent;
    float creepMps = max(min(TYRE_FIGURES.markCorneringSlipMps, working), TYRE_FIGURES.markSlipMps);
    precise float acrossSlideMps = max(abs(acrossSlipMps) - creepMps, 0.0);

    float ploughPowerM2S3 = 0.0;
    bool ploughing = false;
    if (surface.ploughs != 0u)
    {
        float patchSpeedMps = tyreLength(velocityMps);
        precise float power = roundedQuotient(tyreLength(dragNs), perKg) * patchSpeedMps;
        ploughPowerM2S3 = power;
        ploughing = patchSpeedMps > TYRE_FIGURES.ploughCrawlMps;
    }

    float slideSpeedMps = 0.0;
    if (alongSlideMps != 0.0 || acrossSlideMps != 0.0)
    {
        precise float squared = (alongSlideMps * alongSlideMps) + (acrossSlideMps * acrossSlideMps);
        slideSpeedMps = roundedSqrt(squared);
    }

    precise float slideWork = (abs(wantAlongNs) * alongSlideMps) + (abs(wantAcrossNs) * acrossSlideMps);
    float slidePower = roundedQuotient(slideWork, perKg);
    return TyreScrub(slidePower, ploughPowerM2S3, slideSpeedMps, (ploughing ? 1u : 0u) | (sliding ? 2u : 0u));
}

vec2 tyreRollingDrag(vec2 velocityMps, vec2 wheelForward, float alongMps, float loadKg, float dragMps2, bool locked, float dtS)
{
    if (dragMps2 <= 0.0) return vec2(0.0);

    if (!locked)
    {
        precise float held = min(dragMps2 * loadKg * dtS, abs(alongMps) * loadKg);
        precise vec2 drag = wheelForward * -sign(alongMps) * held;
        return drag;
    }

    float speedMps = tyreLength(velocityMps);
    if (speedMps <= 0.0) return vec2(0.0);

    precise float held = min(dragMps2 * loadKg * dtS, speedMps * loadKg);
    precise vec2 drag = roundedQuotient(-velocityMps, speedMps) * held;
    return drag;
}

// Whether a patch can put down this impulse in a tick without sliding (TyreModel.Grips).
bool tyreGrips(CarBuildRow car, TyrePose pose, TyreSurface surface, float loadFraction, vec2 impulseNs, float dtS)
{
    precise float most = car.gripMps2 * surface.coefficient * pose.massKg * loadFraction * dtS;
    return tyreLength(impulseNs) <= most;
}

// A car its locks hold, held still (TyreModel.HoldStill, CAR-52): the body's momentum and spin brought to rest at the
// wheels the handbrake holds, nothing at the others, every rim stopped. False, and nothing written, where any of them
// cannot grip, or where they stand on one line along the car.
bool tyreHoldStill(
    CarBuildRow car, TyrePose pose, float inertiaKgM2, float loadFraction[TyreWheels], vec2 atM[TyreWheels],
    TyreSurface ground[TyreWheels], float dtS, inout vec4 impulses[TyreWheels], inout float spinMps[TyreWheels])
{
    precise vec2 impulseNs = -pose.velocityMps * pose.massKg;
    precise float spinNms = -pose.yawRateRadPerS * inertiaKgM2;
    vec2 forward = pose.forward;
    int wheels = int(car.wheelCount);

    int held = 0;
    precise vec2 middleSumM = vec2(0.0);
    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        if (wheel >= wheels) break;
        if (!buildWheelIs(car, wheel, WheelHandbrake)) continue;

        middleSumM += atM[wheel] - pose.positionM;
        held++;
    }

    if (held == 0) return false;

    vec2 middleM = roundedQuotient(middleSumM, float(held));
    precise float leverSquaredM2 = 0.0;
    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        if (wheel >= wheels) break;
        if (!buildWheelIs(car, wheel, WheelHandbrake)) continue;

        precise vec2 armM = (atM[wheel] - pose.positionM) - middleM;
        float leverM = tyreCross(armM, forward);
        leverSquaredM2 += leverM * leverM;
    }

    if (leverSquaredM2 <= 0.0) return false;

    precise float turnBackNms = spinNms - tyreCross(middleM, impulseNs);
    float perLeverN = roundedQuotient(turnBackNms, leverSquaredM2);
    vec2 shareNs = roundedQuotient(impulseNs, float(held));
    vec2 heldNs[TyreWheels];
    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        heldNs[wheel] = vec2(0.0);
        if (wheel >= wheels || !buildWheelIs(car, wheel, WheelHandbrake)) continue;

        precise vec2 armM = (atM[wheel] - pose.positionM) - middleM;
        float leverM = tyreCross(armM, forward);
        precise vec2 pushedNs = shareNs + (forward * (perLeverN * leverM));
        heldNs[wheel] = pushedNs;
        if (!tyreGrips(car, pose, ground[wheel], loadFraction[wheel], heldNs[wheel], dtS)) return false;
    }

    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        if (wheel >= wheels) break;

        impulses[wheel] = vec4(heldNs[wheel], atM[wheel]);
        spinMps[wheel] = 0.0;
    }

    return true;
}

// One car's tyres for one tick (TyreModel.Step): impulses[wheel] = (impulse.xy, at.xy), scrub per wheel, spin wound on.
void tyreStep(
    CarBuildRow car, TyrePose pose, TyreCommand command, float axleDriveCeilingMps2, float loadFraction[TyreWheels],
    vec2 atM[TyreWheels], TyreSurface ground[TyreWheels], inout float spinMps[TyreWheels], float dtS,
    out vec4 impulses[TyreWheels], out TyreScrub scrub[TyreWheels])
{
    vec2 forward = pose.forward;
    int wheels = int(car.wheelCount);

    vec2 turnedBy[TyreWheels];
    bool steered = command.steerRad != 0.0;
    if (steered) tyreAckermannTurns(car, command.steerRad, turnedBy);

    // Drive placed by layout on each axle and divided by the load that axle stands on.
    float axleLoad[TyreWheels];
    for (int axle = 0; axle < TyreWheels; axle++) axleLoad[axle] = 0.0;
    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        if (wheel >= wheels) break;

        axleLoad[car.wheels[wheel].axle] += loadFraction[wheel];
    }

    precise float driveMps2 = command.throttleMps2 * tyreGearSign(command);
    float engineMps2 = tyreEngineBrakingMps2(car, command);
    float axleDriveMps2[TyreWheels];
    float axleEngineMps2[TyreWheels];
    for (int axle = 0; axle < TyreWheels; axle++)
    {
        axleDriveMps2[axle] = 0.0;
        axleEngineMps2[axle] = 0.0;
        if (axle >= int(car.axleCount)) continue;

        float share = car.axleDrive[axle];
        axleDriveMps2[axle] = tyreNoLargerThan(tyreAxleShare(driveMps2, share, axleLoad[axle]), axleDriveCeilingMps2);
        axleEngineMps2[axle] = tyreAxleShare(engineMps2, share, axleLoad[axle]);
    }

    float spinAllowanceMps = tyreSpinAllowanceMps(tyreDot(pose.velocityMps, forward));

    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        if (wheel >= wheels) break;

        uint axle = car.wheels[wheel].axle;
        precise vec2 fromCentreM = atM[wheel] - pose.positionM;
        precise vec2 velocityMps = pose.velocityMps + pose.yawRateRadPerS * vec2(-fromCentreM.y, fromCentreM.x);
        vec2 wheelForward = forward;
        if (steered && buildWheelIs(car, wheel, WheelSteered)) wheelForward = tyreTurned(forward, turnedBy[wheel]);

        vec2 wheelRight = vec2(-wheelForward.y, wheelForward.x);

        bool locked = tyreLocksEveryWheel(command) || (buildWheelIs(car, wheel, WheelHandbrake) && tyreHandbrake(command));
        TyreSurface surface = ground[wheel];
        precise float gripMps2 = car.gripMps2 * surface.coefficient;
        precise float loadKg = pose.massKg * loadFraction[wheel];

        float alongMps = tyreDot(velocityMps, wheelForward);
        float acrossMps = tyreDot(velocityMps, wheelRight);

        if (locked) spinMps[wheel] = 0.0;
        float spin = spinMps[wheel];
        precise float brakeMps2 = command.brakeMps2 + axleEngineMps2[axle];

        float acrossSlipAskMps = -acrossMps;
        precise float wantAcrossNs = -acrossMps * loadKg;
        float alongSlipAskMps;
        float wantAlongNs;
        float wheelDriveMps2 = axleDriveMps2[axle];
        if (locked)
        {
            alongSlipAskMps = -alongMps;
            precise float want = -alongMps * loadKg;
            wantAlongNs = want;
        }
        else
        {
            precise float pedalMps = wheelDriveMps2 * dtS - sign(alongMps) * min(brakeMps2 * dtS, abs(alongMps));
            precise float carriedMps = spin - alongMps;
            precise float carried = sign(carriedMps) * max(abs(carriedMps) - (gripMps2 * dtS), 0.0);
            precise float ask = carried + pedalMps;
            alongSlipAskMps = ask;
            precise float want = pedalMps * loadKg + (spin - alongMps) * tyreSyncMassKg(car, loadKg);
            wantAlongNs = want;
        }

        float askedAlongNs = wantAlongNs;
        precise float budgetNs = gripMps2 * loadKg * dtS;
        vec2 tyreNs = vec2(0.0);
        bool sliding = false;
        if (budgetNs > 0.0)
        {
            precise float perGripMps = gripMps2 * dtS;
            float alongShare = roundedQuotient(alongSlipAskMps, perGripMps);
            float acrossShare = roundedQuotient(acrossSlipAskMps, perGripMps);
            precise float squared = alongShare * alongShare + acrossShare * acrossShare;
            float demand = roundedSqrt(squared);
            sliding = demand > 1.0;
            if (sliding)
            {
                float alongBound = roundedQuotient(budgetNs * alongShare, demand);
                float acrossBound = roundedQuotient(budgetNs * acrossShare, demand);
                wantAlongNs = tyreNoLargerThan(alongBound, wantAlongNs);
                wantAcrossNs = tyreNoLargerThan(acrossBound, wantAcrossNs);
            }

            precise vec2 delivered = wheelForward * wantAlongNs + wheelRight * wantAcrossNs;
            tyreNs = delivered;
        }
        else
        {
            wantAlongNs = 0.0;
            wantAcrossNs = 0.0;
        }

        vec2 leftMps = velocityMps;
        if (loadKg > 0.0)
        {
            precise vec2 left = velocityMps + roundedQuotient(tyreNs, loadKg);
            leftMps = left;
        }

        vec2 dragNs = tyreRollingDrag(leftMps, wheelForward, tyreDot(leftMps, wheelForward), loadKg, surface.dragMps2, locked, dtS);

        float alongAfterMps = alongMps;
        float acrossAfterMps = acrossMps;
        if (loadKg > 0.0)
        {
            precise float alongNs = wantAlongNs + tyreDot(dragNs, wheelForward);
            precise float along = alongMps + roundedQuotient(alongNs, loadKg);
            precise float across = acrossMps + roundedQuotient(wantAcrossNs, loadKg);
            alongAfterMps = along;
            acrossAfterMps = across;
        }

        if (!locked && loadKg > 0.0)
        {
            spinMps[wheel] = tyreRim(
                car, spin, alongMps, alongAfterMps, wantAlongNs, askedAlongNs, sliding, loadKg, wheelDriveMps2, brakeMps2,
                spinAllowanceMps, dtS);
        }

        precise vec2 spent = tyreNs + dragNs;
        impulses[wheel] = vec4(spent, atM[wheel]);
        precise float alongSlip = spinMps[wheel] - alongAfterMps;
        scrub[wheel] = tyreScrub(
            surface, wantAlongNs, wantAcrossNs, dragNs, velocityMps, alongSlip, acrossAfterMps, spinMps[wheel], sliding, loadKg, dtS);
    }
}
