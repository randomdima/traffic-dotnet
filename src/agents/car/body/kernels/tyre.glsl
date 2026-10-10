// TyreModel on the device: one car's four patches for one tick — an impulse and a place to spend it per wheel, what
// each did to the ground, and the four rims wound on (TyreModel.Step, Loads, Ackermann, WheelPointsM, Rim, Scrub,
// RollingDrag, HoldStill). Line for line with the host, `precise` where it rounds twice; its trigonometry is trig.glsl's
// and its quotients and roots rounded.glsl's.
//
// The including kernel supplies the tyre figures as TYRE_FIGURES' fields: standardGravityMps2, wheelSpinFadeMps,
// wheelSpinAllowanceMps, markSlipMps, markCorneringSlipMps, ploughCrawlMps, engineBrakingMps2 — and includes
// car_build.glsl and tyre_rows.glsl first.

#include "core/geometry/kernels/trig.glsl"
#include "core/geometry/kernels/rounded.glsl"

const int TyreWheels = 4;
const int TyreRearRight = 2;
const int TyreRearLeft = 3;
const float TyreWorkingSlipTangent = 0.3;
const float TyreAlongSlipEpsilonMps = 0.01;

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

bool tyreLocksEveryWheel(TyreCommand command) { return isinf(command.brakeMps2) && command.brakeMps2 > 0.0; }
bool tyreHandbrake(TyreCommand command) { return (command.handbrakeAndReverse & 1u) != 0u; }
float tyreGearSign(TyreCommand command) { return (command.handbrakeAndReverse & 2u) != 0u ? -1.0 : 1.0; }

float tyreDot(vec2 a, vec2 b) { precise float d = a.x * b.x + a.y * b.y; return d; }
float tyreCross(vec2 a, vec2 b) { precise float c = (a.x * b.y) - (a.y * b.x); return c; }
float tyreLength(vec2 v) { return roundedLength(v); }

vec2 tyreWheelAtM(CarBuildRow car, int wheel)
{
    precise float front = car.wheelbaseM - car.centreAheadOfAxleM;
    return vec2(wheel < 2 ? front : -car.centreAheadOfAxleM, wheel % 2 == 0 ? car.halfTrackM : -car.halfTrackM);
}

void tyreWheelPointsM(CarBuildRow car, TyrePose pose, out vec2 atM[4])
{
    vec2 forward = pose.forward;
    vec2 right = vec2(-forward.y, forward.x);
    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        vec2 atBody = tyreWheelAtM(car, wheel);
        precise vec2 at = pose.positionM + (forward * atBody.x) + (right * atBody.y);
        atM[wheel] = at;
    }
}

void tyreLoads(CarBuildRow car, TyrePose pose, out float into[4])
{
    float weight = TYRE_FIGURES.standardGravityMps2;
    float alongShare = roundedQuotient(pose.accelerationMps2.x * car.cgHeightM, car.wheelbaseM * weight);
    float acrossShare = roundedQuotient(pose.accelerationMps2.y * car.cgHeightM, car.halfTrackM * 2.0 * weight);
    precise float front = clamp(car.frontWeightShare - alongShare, 0.0, 1.0);
    precise float toTheSide = clamp(0.5 - acrossShare, 0.0, 1.0);
    precise float l0 = front * toTheSide;
    precise float l1 = front * (1.0 - toTheSide);
    precise float l2 = (1.0 - front) * toTheSide;
    precise float l3 = (1.0 - front) * (1.0 - toTheSide);
    into[0] = l0;
    into[1] = l1;
    into[2] = l2;
    into[3] = l3;
}

void tyreAckermann(CarBuildRow car, float steerRad, out float into[4])
{
    into[2] = 0.0;
    into[3] = 0.0;
    if (abs(steerRad) < 1e-4)
    {
        into[0] = steerRad;
        into[1] = steerRad;
        return;
    }

    float radiusM = abs(roundedQuotient(car.wheelbaseM, trigTan(steerRad)));
    float innerRad = trigAtan(roundedQuotient(car.wheelbaseM, max(0.01, radiusM - car.halfTrackM)));
    precise float outerRadius = radiusM + car.halfTrackM;
    float outerRad = trigAtan(roundedQuotient(car.wheelbaseM, outerRadius));
    float s = sign(steerRad);
    into[0] = s * (s > 0.0 ? innerRad : outerRad);
    into[1] = s * (s > 0.0 ? outerRad : innerRad);
}

float tyreNoLargerThan(float value, float most) { return sign(value) * min(abs(value), abs(most)); }

float tyreSyncMassKg(CarBuildRow car, float loadKg)
{
    if (loadKg <= 0.0) return 0.0;

    precise float sum = roundedQuotient(1.0, car.wheelRotatingMassKg) + roundedQuotient(1.0, loadKg);
    return roundedQuotient(1.0, sum);
}

float tyreAxleShare(float wholeMps2, float share, float axleLoad)
{
    float r = roundedQuotient(wholeMps2 * share, axleLoad);
    return axleLoad > 1e-3 ? r : 0.0;
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

    precise float holds = abs(wantAlongNs) + (TyreAlongSlipEpsilonMps * loadKg);
    return !sliding || abs(askedAlongNs) <= holds ? alongAfterMps : spunMps;
}

TyreScrub tyreScrub(
    TyreSurface surface, float wantAlongNs, float wantAcrossNs, vec2 dragNs, vec2 velocityMps, float alongSlipMps,
    float acrossSlipMps, float spinMps, bool sliding, float loadKg, float dtS)
{
    precise float perKg = loadKg * dtS;
    if (perKg <= 0.0) return TyreScrub(0.0, 0.0, 0.0, 0u);

    precise float alongSlideMps = max(abs(alongSlipMps) - TYRE_FIGURES.markSlipMps, 0.0);
    precise float working = abs(spinMps) * TyreWorkingSlipTangent;
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
// rear pair, nothing at the front, every rim stopped. False, and nothing written, where either rear patch cannot grip.
bool tyreHoldStill(
    CarBuildRow car, TyrePose pose, float inertiaKgM2, vec2 atM[4], TyreSurface ground[4], float dtS, inout vec4 impulses[4],
    inout float spinMps[4])
{
    precise vec2 impulseNs = -pose.velocityMps * pose.massKg;
    precise float spinNms = -pose.yawRateRadPerS * inertiaKgM2;

    precise vec2 rightM = atM[TyreRearRight] - pose.positionM;
    precise vec2 leftM = atM[TyreRearLeft] - pose.positionM;
    vec2 forward = pose.forward;
    precise vec2 middleM = (rightM + leftM) * 0.5;
    precise vec2 acrossM = rightM - leftM;
    precise float couple = spinNms - tyreCross(middleM, impulseNs);
    float coupleNs = roundedQuotient(couple, tyreCross(acrossM, forward));
    precise vec2 rightNs = (impulseNs * 0.5) + (forward * coupleNs);
    precise vec2 leftNs = (impulseNs * 0.5) - (forward * coupleNs);

    float loadFraction[4];
    tyreLoads(car, pose, loadFraction);
    if (!tyreGrips(car, pose, ground[TyreRearRight], loadFraction[TyreRearRight], rightNs, dtS)
        || !tyreGrips(car, pose, ground[TyreRearLeft], loadFraction[TyreRearLeft], leftNs, dtS))
    {
        return false;
    }

    impulses[0] = vec4(0.0, 0.0, atM[0]);
    impulses[1] = vec4(0.0, 0.0, atM[1]);
    impulses[TyreRearRight] = vec4(rightNs, atM[TyreRearRight]);
    impulses[TyreRearLeft] = vec4(leftNs, atM[TyreRearLeft]);
    for (int wheel = 0; wheel < TyreWheels; wheel++) spinMps[wheel] = 0.0;
    return true;
}

// One car's tyres for one tick (TyreModel.Step): impulses[wheel] = (impulse.xy, at.xy), scrub per wheel, spin wound on.
void tyreStep(
    CarBuildRow car, TyrePose pose, TyreCommand command, float axleDriveCeilingMps2, vec2 atM[4], TyreSurface ground[4],
    inout float spinMps[4], float dtS, out vec4 impulses[4], out TyreScrub scrub[4])
{
    vec2 forward = pose.forward;
    float drivenFrontShare = car.drivenFrontShare;

    float loadFraction[4];
    tyreLoads(car, pose, loadFraction);

    float steerRad[4];
    tyreAckermann(car, command.steerRad, steerRad);

    precise float frontLoad = loadFraction[0] + loadFraction[1];
    precise float driveMps2 = command.throttleMps2 * tyreGearSign(command);
    precise float rearShare = 1.0 - drivenFrontShare;
    precise float rearLoad = 1.0 - frontLoad;
    float frontDriveMps2 = tyreNoLargerThan(tyreAxleShare(driveMps2, drivenFrontShare, frontLoad), axleDriveCeilingMps2);
    float rearDriveMps2 = tyreNoLargerThan(tyreAxleShare(driveMps2, rearShare, rearLoad), axleDriveCeilingMps2);
    float spinAllowanceMps = tyreSpinAllowanceMps(tyreDot(pose.velocityMps, forward));

    float engineMps2 = tyreEngineBrakingMps2(car, command);
    float frontEngineMps2 = tyreAxleShare(engineMps2, drivenFrontShare, frontLoad);
    float rearEngineMps2 = tyreAxleShare(engineMps2, rearShare, rearLoad);

    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        precise vec2 fromCentreM = atM[wheel] - pose.positionM;
        precise vec2 velocityMps = pose.velocityMps + pose.yawRateRadPerS * vec2(-fromCentreM.y, fromCentreM.x);
        vec2 wheelForward = forward;
        if (steerRad[wheel] != 0.0)
        {
            precise float turned = pose.headingRad + steerRad[wheel];
            wheelForward = trigUnit(turned);
        }

        vec2 wheelRight = vec2(-wheelForward.y, wheelForward.x);

        bool rear = wheel >= 2;
        bool locked = tyreLocksEveryWheel(command) || (rear && tyreHandbrake(command));
        TyreSurface surface = ground[wheel];
        precise float gripMps2 = car.gripMps2 * surface.coefficient;
        precise float loadKg = pose.massKg * loadFraction[wheel];

        float alongMps = tyreDot(velocityMps, wheelForward);
        float acrossMps = tyreDot(velocityMps, wheelRight);

        if (locked) spinMps[wheel] = 0.0;
        float spin = spinMps[wheel];
        precise float brakeMps2 = command.brakeMps2 + (rear ? rearEngineMps2 : frontEngineMps2);

        float acrossSlipAskMps = -acrossMps;
        precise float wantAcrossNs = -acrossMps * loadKg;
        float alongSlipAskMps;
        float wantAlongNs;
        float axleDriveMps2 = rear ? rearDriveMps2 : frontDriveMps2;
        if (locked)
        {
            alongSlipAskMps = -alongMps;
            precise float want = -alongMps * loadKg;
            wantAlongNs = want;
        }
        else
        {
            precise float pedalMps = axleDriveMps2 * dtS - sign(alongMps) * min(brakeMps2 * dtS, abs(alongMps));
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

        vec2 dragNs = tyreRollingDrag(velocityMps, wheelForward, alongMps, loadKg, surface.dragMps2, locked, dtS);

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
                car, spin, alongMps, alongAfterMps, wantAlongNs, askedAlongNs, sliding, loadKg, axleDriveMps2, brakeMps2,
                spinAllowanceMps, dtS);
        }

        precise vec2 spent = tyreNs + dragNs;
        impulses[wheel] = vec4(spent, atM[wheel]);
        precise float alongSlip = spinMps[wheel] - alongAfterMps;
        scrub[wheel] = tyreScrub(
            surface, wantAlongNs, wantAcrossNs, dragNs, velocityMps, alongSlip, acrossAfterMps, spinMps[wheel], sliding, loadKg, dtS);
    }
}
