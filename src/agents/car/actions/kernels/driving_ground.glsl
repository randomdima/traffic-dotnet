// What a leg asks of DrivingGround on the device: where a car's leading edge stands ahead of its rear axle along the line
// it drives, and its grant forgotten where it takes a line afresh. The fields are the includer's accessors
// (town_rows.glsl, town_cars.glsl).

#ifndef DRIVING_GROUND_GLSL
#define DRIVING_GROUND_GLSL

// Its nose forwards, its tail backwards: a line's metres run the way it is driven whichever gear it is taken in.
float drivingGround_LeadingEdgeAheadOfTheAxleM(uint car)
{
    return cars_LineIsReverse(car) != 0u ? cars_Build(car).tailBehindAxleM : cars_Build(car).noseAheadOfAxleM;
}

// What it was granted was read down the line it had, and is nothing on the one it takes — until the claims are next laid.
void drivingGround_ForgetTheGrant(uint car)
{
    cars_AuthorityM(car) = 0.0;
    cars_CoveredSinceClaimM(car) = 0.0;
    cars_HorizonM(car) = uintBitsToFloat(0x7F800000u);
}

#endif
