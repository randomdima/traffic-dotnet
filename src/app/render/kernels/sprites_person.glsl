// One walker as a frame's emit reads it (app/devicetick/DeviceSprites.PersonRow, field for field): what
// sprites_buffers.glsl binds a buffer of, and what sprites_walkers.comp lays off the walker tick's own table.

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

// Where it stands, which way it faces and how far it has walked are the walker table's, over the town's copy.
const uint PersonPoseFromTheDevice = 8u;

#endif
