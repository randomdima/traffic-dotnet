// One variant as a kernel reads it (CarBuild): what the tyres and the driver of that car are worth, field for field
// with app/devicetick/CarBuildRow.

#ifndef CAR_BUILD_GLSL
#define CAR_BUILD_GLSL

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
    float drivenFrontShare;
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
    float wheelbaseWeightM2PerS2;
    float trackWeightM2PerS2;
};

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
