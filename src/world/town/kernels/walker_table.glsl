// The town's walkers as the device keeps them, one row a walker, field for field with app/devicetick/DeviceWalkerTick:
// where each stands and moves, what pace it declared, where it is aimed, which way it faces, its mass, what the host
// tells it and how far it has walked.

#ifndef WALKER_TABLE_GLSL
#define WALKER_TABLE_GLSL

struct WalkerTableRow
{
    vec2 positionM;
    vec2 velocityMps;
    vec2 declaredMps;
    vec2 aimM;
    float headingRad;
    float massKg;
    uint flags;
    float distanceWalkedM;
};

// WalkerTableRow.flags: what the host tells the walker's tick.
const uint WalkerTableWalking = 1u;
const uint WalkerTableWounded = 2u;
const uint WalkerTableInside = 4u;
const uint WalkerTablePaused = 8u;

#endif
