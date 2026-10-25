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

    // What a towed car's own wheels carry (EVA-5, TowBar.Step).
    float onTheTrailerAxleShare;

    // How far ahead the wheel is aimed, as a time (DrivingFigures.LookaheadS).
    float lookaheadS;

    // The body row the first rail vehicle stands in (WorldLaying.AgentOfWagon): every rail vehicle's row is it and on.
    int firstWagonBody;

    // A rail vehicle's wheels (RailWheels.Step): how many segments the railway has, the passes the wheels are held to
    // their rails in, the pull back onto the line a second, steel on steel, the rolling resistance, and gravity.
    int railSegments;
    uint railPasses;
    float railOntoTheLine;
    float railFriction;
    float railRolling;
    float railGravityMps2;
} world;

#define WALKER_FIGURES world

#endif
