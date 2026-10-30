// Suspension on the device: a car's body on a damped spring at every wheel — what each wheel carries, and the body carried
// on by a step under what its tyres pushed it with (Suspension.Loads, Step). Line for line with the host, `precise` where
// it rounds twice and its quotients rounded.glsl's.
//
// The including kernel includes car_build.glsl and tyre_rows.glsl first.

#ifndef SUSPENSION_GLSL
#define SUSPENSION_GLSL

#include "core/geometry/kernels/rounded.glsl"

// The least any figure of the state is before it is nought: 2^-100, the float's bound and not a physical one (Suspension.LeastState).
const float SuspensionLeastState = 7.888609e-31;

// Where a wheel stands off the centre of mass, along the body and to its right (Suspension.FromCentreOfMassM).
vec2 suspensionFromCentreOfMassM(CarBuildRow car, int wheel)
{
    precise vec2 atM = buildWheelAtM(car, wheel) - vec2(car.cgAheadOfCentreM, 0.0);
    return atM;
}

// What a wheel stands on at rest, in newtons (Suspension.Standing).
float suspensionStanding(CarBuildRow car, vec2 atM)
{
    precise float standingN = car.standingN + (car.standingNPerMAlong * atM.x) + (car.standingNPerMAcross * atM.y);
    return standingN;
}

// What every spring pushes with past what it stood on, as a plane of a wheel's place (Suspension.Deviation).
vec3 suspensionDeviation(CarBuildRow car, SprungBody body)
{
    precise float atCentre = (car.wheelRateNPerM * body.heaveM) + (car.wheelDampingNsPerM * body.heaveMps);
    precise float perAlong = (car.wheelRateNPerM * body.pitchRad) + (car.wheelDampingNsPerM * body.pitchRadPerS);
    precise float perAcross = (car.wheelRateNPerM * body.rollRad) + (car.wheelDampingNsPerM * body.rollRadPerS);
    return vec3(atCentre, perAlong, perAcross);
}

// A wheel's spring push past what it stood on (Suspension.Along).
float suspensionAlong(vec3 plane, vec2 atM)
{
    precise float moreN = plane.x + (plane.y * atM.x) + (plane.z * atM.y);
    return moreN;
}

float suspensionNormal(float figure) { return abs(figure) < SuspensionLeastState ? 0.0 : figure; }

// Whether a wheel stands on something: its bit of `overNothing` clear (Suspension.Stands).
bool suspensionStands(uint overNothing, int wheel) { return ((overNothing >> uint(wheel)) & 1u) == 0u; }

// What each wheel carries as its share of the car, and the acceleration across it its springs are carrying (Suspension.Loads).
// A wheel standing on nothing carries nothing.
float suspensionLoads(CarBuildRow car, SprungBody body, uint overNothing, out float shareInto[CarMostWheels])
{
    vec3 plane = suspensionDeviation(car, body);
    precise float totalN = 0.0;
    precise float acrossNm = 0.0;
    for (int wheel = 0; wheel < CarMostWheels; wheel++)
    {
        shareInto[wheel] = 0.0;
        if (wheel >= int(car.wheelCount)) continue;

        vec2 atM = suspensionFromCentreOfMassM(car, wheel);
        float standingN = suspensionStanding(car, atM);
        precise float pushed = standingN + suspensionAlong(plane, atM);
        float pushN = suspensionStands(overNothing, wheel) ? max(0.0, pushed) : 0.0;
        shareInto[wheel] = pushN;
        totalN += pushN;
        acrossNm += (pushN - standingN) * atM.y;
    }

    float perN = totalN > 0.0 ? roundedQuotient(1.0, totalN) : 0.0;
    for (int wheel = 0; wheel < CarMostWheels; wheel++)
    {
        if (wheel >= int(car.wheelCount)) break;

        precise float share = shareInto[wheel] * perN;
        shareInto[wheel] = share;
    }

    precise float acrossMps2 = -acrossNm * car.inverseMassCgHeightPerKgM;
    return acrossMps2;
}

// Where the body's springs rest over wheels standing on nothing: leaning the most tilt over them, nose down and right side
// down (Suspension.LeanOver).
vec2 suspensionLeanOver(CarBuildRow car, uint overNothing, float mostTiltRad)
{
    precise float alongM = 0.0;
    precise float acrossM = 0.0;
    for (int wheel = 0; wheel < CarMostWheels; wheel++)
    {
        if (wheel >= int(car.wheelCount)) break;
        if (suspensionStands(overNothing, wheel)) continue;

        vec2 atM = suspensionFromCentreOfMassM(car, wheel);
        alongM += atM.x;
        acrossM += atM.y;
    }

    precise float pitchRad = sign(alongM) * mostTiltRad;
    precise float rollRad = sign(acrossM) * mostTiltRad;
    return vec2(pitchRad, rollRad);
}

// The body carried on by one step under what its tyres pushed it with at the ground, along it and to its right (Suspension.Step),
// its springs at rest leaning the most tilt over any wheel standing on nothing.
SprungBody suspensionStep(CarBuildRow car, SprungBody body, vec2 pushN, float dtS, uint overNothing, float mostTiltRad)
{
    vec2 lean = overNothing == 0u ? vec2(0.0) : suspensionLeanOver(car, overNothing, mostTiltRad);
    precise float pitchOffRad = body.pitchRad - lean.x;
    precise float rollOffRad = body.rollRad - lean.y;
    SprungBody fromRest = SprungBody(body.heaveM, pitchOffRad, rollOffRad, body.heaveMps, body.pitchRadPerS, body.rollRadPerS);
    vec3 plane = suspensionDeviation(car, fromRest);
    precise float heaveN = 0.0;
    precise float pitchNm = 0.0;
    precise float rollNm = 0.0;
    for (int wheel = 0; wheel < CarMostWheels; wheel++)
    {
        if (wheel >= int(car.wheelCount)) break;

        vec2 atM = suspensionFromCentreOfMassM(car, wheel);
        float standingN = suspensionStanding(car, atM);
        precise float pushed = standingN + suspensionAlong(plane, atM);
        precise float moreN = max(0.0, pushed) - standingN;
        heaveN += moreN;
        pitchNm += moreN * atM.x;
        rollNm += moreN * atM.y;
    }

    precise float heaveMps2 = -heaveN * car.inverseMassPerKg;
    precise float pitchRadPerS2 = ((-pushN.x * car.cgHeightM) - pitchNm) * car.inversePitchInertiaPerKgM2;
    precise float rollRadPerS2 = ((-pushN.y * car.cgHeightM) - rollNm) * car.inverseRollInertiaPerKgM2;

    precise float heaveMpsRaw = body.heaveMps + (heaveMps2 * dtS);
    precise float pitchRadPerSRaw = body.pitchRadPerS + (pitchRadPerS2 * dtS);
    precise float rollRadPerSRaw = body.rollRadPerS + (rollRadPerS2 * dtS);
    float heaveMps = suspensionNormal(heaveMpsRaw);
    float pitchRadPerS = suspensionNormal(pitchRadPerSRaw);
    float rollRadPerS = suspensionNormal(rollRadPerSRaw);

    precise float heaveMRaw = body.heaveM + (heaveMps * dtS);
    precise float pitchRadRaw = body.pitchRad + (pitchRadPerS * dtS);
    precise float rollRadRaw = body.rollRad + (rollRadPerS * dtS);
    return SprungBody(
        suspensionNormal(heaveMRaw), suspensionNormal(pitchRadRaw), suspensionNormal(rollRadRaw), heaveMps, pitchRadPerS,
        rollRadPerS);
}

#endif
