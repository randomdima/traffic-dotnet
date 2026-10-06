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

// What it may be braking at at all on this ground: the pedal's cap, or what the patch puts down (CarBuild).
float buildUtmostBrakingMps2(CarBuildRow car, float groundCoefficient)
{
    precise float patchMps2 = car.gripMps2 * groundCoefficient;
    return min(car.brakingMps2, patchMps2);
}

// Where the wheel is after a tick of winding it towards what was asked (CarBuild.WheelWoundTo, CAR-3a).
float buildWheelWoundTo(CarBuildRow car, float fromRad, float wantedRad, float dtS)
{
    precise float travelRad = car.steerRateRadPerS * dtS;
    precise float least = fromRad - travelRad;
    precise float most = fromRad + travelRad;
    return clamp(wantedRad, least, most);
}
