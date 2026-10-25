// One car as a frame's emit reads it (app/devicetick/DeviceSprites.CarRow, field for field): what sprites_buffers.glsl
// binds a buffer of, and what sprites_world.comp lays off the world's own state.

#ifndef SPRITES_CAR_GLSL
#define SPRITES_CAR_GLSL

// Its pose and wheel, what its lamps say (a CarLampSet), its look, what it tows (DeviceWorld.TowingCode), where along
// that the fork is, and how its body stands tilted on its springs as two halves (CarView.TiltHalves, CAR-56).
struct CarRow
{
    vec2 positionM;
    vec2 forward;
    float headingRad;
    float steerRad;
    uint lamps;
    uint variant;
    uint flags;
    int towing;
    float forkAlongM;
    uint tiltHalves;
};

const uint CarBroken = 1u;
const uint CarLevelShift = 16u;

#endif
