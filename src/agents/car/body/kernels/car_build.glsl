// One variant as a kernel reads it (CarBuild): what the tyres and the driver of that car are worth, field for field
// with app/devicetick/CarBuildRow.

#ifndef CAR_BUILD_GLSL
#define CAR_BUILD_GLSL

#include "agents/car/body/kernels/car_wheels.glsl"

// One wheel of a body (CarWheel): where it stands, +x the nose and +y the driver's right, its axle, and what it does.
struct CarWheelRow
{
    vec2 atM;
    uint axle;
    uint flags;
};

const uint WheelSteered = 1u;
const uint WheelHandbrake = 2u;
const uint WheelDriven = 4u;

struct CarBuildRow
{
    float wheelbaseM;
    float centreAheadOfAxleM;
    float halfTrackM;
    float cgHeightM;
    float frontWeightShare;
    float gripMps2;
    float wheelRotatingMassKg;
    float accelerationMps2;
    float brakingMps2;
    float maxSteerRad;
    float lookaheadFloorM;
    float lookaheadCeilingM;
    float projectionWindowM;
    float steerRateRadPerS;
    float massKg;
    float wheelWidthM;
    float lengthM;
    float widthM;
    float towHingeAheadOfCentreM;
    float towReachM;
    float towGripFromTheMiddleM;
    float inverseWheelRotatingMassPerKg;
    float wheelRateNPerM;
    float wheelDampingNsPerM;
    float cgAheadOfCentreM;
    float standingN;
    float standingNPerMAlong;
    float standingNPerMAcross;
    float inverseMassPerKg;
    float inversePitchInertiaPerKgM2;
    float inverseRollInertiaPerKgM2;
    float inverseMassCgHeightPerKgM;
    uint wheelCount;
    uint axleCount;
    CarWheelRow wheels[CarMostWheels];
    float axleDrive[CarMostWheels];
};

// Where a wheel stands in the body's own frame, +x forward and +y the driver's side (TyreModel.WheelAtM).
vec2 buildWheelAtM(CarBuildRow car, int wheel) { return car.wheels[wheel].atM; }

bool buildWheelIs(CarBuildRow car, int wheel, uint what) { return (car.wheels[wheel].flags & what) != 0u; }

// The axle a towed car rolls on, the far one from the fork (TowBar.AxleOnTheGround).
uint buildAxleOnTheGround(CarBuildRow car, bool byTheTail)
{
    int at = 0;
    for (int wheel = 1; wheel < int(car.wheelCount); wheel++)
    {
        precise float aheadM = car.wheels[wheel].atM.x - car.wheels[at].atM.x;
        if (byTheTail ? aheadM > 0.0 : aheadM < 0.0) at = wheel;
    }

    return car.wheels[at].axle;
}

// What it may be braking at at all: the pedal's cap, or what its own patch puts down (CarBuild.UtmostBrakingMps2).
float buildUtmostBrakingMps2(CarBuildRow car) { return min(car.brakingMps2, car.gripMps2); }

// Half its width (CarBuild.FlankM).
float buildFlankM(CarBuildRow car)
{
    precise float flankM = car.widthM * 0.5;
    return flankM;
}

// Where the wheel is after a tick of winding it towards what was asked (CarBuild.WheelWoundTo, CAR-3a).
float buildWheelWoundTo(CarBuildRow car, float fromRad, float wantedRad, float dtS)
{
    precise float travelRad = car.steerRateRadPerS * dtS;
    precise float least = fromRad - travelRad;
    precise float most = fromRad + travelRad;
    return clamp(wantedRad, least, most);
}

#endif
