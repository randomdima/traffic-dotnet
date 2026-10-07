// One variant as a kernel reads it (CarBuild): what the tyres and the driver of that car are worth, field for field
// with app/devicetick/CarBuildRow.

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
    float maxSpeedMps;
    float reverseMaxMps;
    float brakingMps2;
    float maxSteerRad;
    float pedalRateMps3;
    float lookaheadFloorM;
    float lookaheadCeilingM;
    float projectionWindowM;
    float steerRateRadPerS;
    float turningRadiusM;
    float massKg;
    float sightM;
    float wheelWidthM;
    float lengthM;
    float noseAheadOfAxleM;
    float tailBehindAxleM;
};

// What it may be braking at at all: the pedal's cap, or what its own patch puts down (CarBuild.UtmostBrakingMps2).
float buildUtmostBrakingMps2(CarBuildRow car) { return min(car.brakingMps2, car.gripMps2); }

// Where the wheel is after a tick of winding it towards what was asked (CarBuild.WheelWoundTo, CAR-3a).
float buildWheelWoundTo(CarBuildRow car, float fromRad, float wantedRad, float dtS)
{
    precise float travelRad = car.steerRateRadPerS * dtS;
    precise float least = fromRad - travelRad;
    precise float most = fromRad + travelRad;
    return clamp(wantedRad, least, most);
}
