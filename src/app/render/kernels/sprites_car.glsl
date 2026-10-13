// One car as a frame's emit reads it (app/devicetick/DeviceSprites.CarRow, field for field): what sprites_buffers.glsl
// binds a buffer of, and what sprites_world.comp lays off the world's own state.

#ifndef SPRITES_CAR_GLSL
#define SPRITES_CAR_GLSL

// Its pose and wheel, what its lamps say (a CarLampSet), its look and what it tows.
struct CarRow
{
    vec2 positionM;
    vec2 forward;
    float headingRad;
    float steerRad;
    float fuseJitter;
    uint lamps;
    uint variant;
    uint flags;
    int towing;
    float pad;
};

const uint CarBroken = 1u;
const uint CarHeldByTheTail = 256u;
const uint CarLevelShift = 16u;

#endif
