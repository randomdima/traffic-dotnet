// The world layer on the device (WorldLayer, app/devicetick/DeviceWorld): the push block its stepping kernels share,
// field for field with DeviceWorld.WorldPush, and the rows they keep (world_rows.glsl).

#ifndef WORLD_GLSL
#define WORLD_GLSL

#include "world/town/kernels/world_rows.glsl"

layout(push_constant) uniform World
{
    // The signals, cars or walkers a kernel is run over.
    uint count;
    uint mode;
    float dtS;

    // What a car's lagged acceleration closes on its new reading by in one step (TownWorld.LoadSettledShare).
    float loadSettledShare;

    // The least settled load that is any (TyreFigures.LeastSettledLoadMps2).
    float leastSettledLoadMps2;

    // The bits of a body's flags (BodyFlags): in the world, frozen, filed frozen.
    uint enabledBit;
    uint frozenBit;
    uint filedFrozenBit;

    // A walker's footing (WalkerFollower.Step).
    float walkSpeedMps;
    float turnRateDegPerS;
    float footGripMps2;
    float slidingGripMps2;
    float aimUnderfootM2;

    // The tow bar (EVA-5, TowBar).
    float hitchSettleS;
    float hitchMostMps2;
    float hitchSideShare;
    float onTheTrailerAxleShare;

    // How far ahead the wheel is aimed, as a time (DrivingFigures.LookaheadS).
    float lookaheadS;

    // The body row the first train's wagon stands in (WorldLaying.AgentOfWagon): every wagon's row is it and on.
    int firstWagonBody;
} world;

#define WALKER_FIGURES world

#endif
