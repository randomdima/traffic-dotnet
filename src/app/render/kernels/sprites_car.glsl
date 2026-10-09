// One car as a frame's emit reads it (app/devicetick/DeviceSprites.CarRow, field for field): what sprites_buffers.glsl
// binds a buffer of, and what sprites_cars.comp lays off the car tick's own tables.

#ifndef SPRITES_CAR_GLSL
#define SPRITES_CAR_GLSL

// Its pose, the facts its lamps answer — how far its line bends ahead of it among them — and what it tows.
struct CarRow
{
    vec2 positionM;
    vec2 forward;
    float headingRad;
    float steerRad;
    float brakeMps2;
    float progressM;
    float alongMps;
    float contextPassAsideM;
    float toTheBoxM;
    float fuseJitter;
    float passEndsM;
    float passSteppedOutM;
    float passBackM;
    float passAsideM;
    uint variant;
    uint flags;
    int towing;
    float turnAheadRad;
};

const uint CarBroken = 1u;
const uint CarDriven = 2u;
const uint CarReverse = 4u;
const uint CarBlueLight = 8u;
const uint CarAtWork = 16u;
const uint CarTurningAtTheBox = 32u;
const uint CarLineIsReverse = 64u;
const uint CarPassBegun = 128u;
const uint CarHeldByTheTail = 256u;
const uint CarFromTheDevice = 512u;
const uint CarLevelShift = 16u;

#endif
