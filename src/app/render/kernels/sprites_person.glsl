// One walker as a frame's emit reads it (app/devicetick/DeviceSprites.PersonRow, field for field): what
// sprites_buffers.glsl binds a buffer of, and what sprites_world.comp lays off the world's own state.

#ifndef SPRITES_PERSON_GLSL
#define SPRITES_PERSON_GLSL

struct PersonRow
{
    vec2 positionM;
    float headingRad;
    float distanceWalkedM;
    uint variant;
    uint flags;
};

const uint PersonInside = 1u;
const uint PersonDown = 2u;
const uint PersonWalking = 4u;

#endif
