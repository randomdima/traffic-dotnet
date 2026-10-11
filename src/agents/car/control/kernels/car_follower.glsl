// What a leg reads of CarFollower (CarFollower.RearAxleM, ProgressM) over the resident town: where a body's rear axle is,
// and where along its own line that axle stands. The tick's own port of the follower is follower.glsl, under its kernel's
// bindings. The includer includes town_arcs.glsl and car_build.glsl first and defines the town's accessors.

#ifndef CAR_FOLLOWER_GLSL
#define CAR_FOLLOWER_GLSL

vec2 carFollower_RearAxleM(CarBuildRow car, vec2 positionM, vec2 forward)
{
    precise vec2 rear = positionM - forward * car.centreAheadOfAxleM;
    return rear;
}

// Searched in the build's window around where it last was, over the line the car holds.
float carFollower_ProgressM(uint car, CarBuildRow build, vec2 rearAxleM, float lastProgressM)
{
    townArcsOfLines();
    uint first = townLineArcsFrom(car);
    float offSq;
    return splineProject(first, first + cars_Line(car).arcCount, rearAxleM, lastProgressM, build.projectionWindowM, offSq);
}

#endif
