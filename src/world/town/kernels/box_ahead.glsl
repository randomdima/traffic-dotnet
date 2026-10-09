// The junction ahead of a car as its tick reads it (TownWorld.ReadTheBoxAhead, drive_context.glsl), field for field
// with app/devicetick/DeviceCarTick.BoxRow — what a car's brief carries whole.

#ifndef BOX_AHEAD_GLSL
#define BOX_AHEAD_GLSL

// The junction ahead of a car (CarFleet.ToTheBoxM, BoxIsOurs, TurningAtTheBox, InsideTheBox, CommittedToTheBox,
// LightAheadM).
struct BoxAheadRow
{
    float toTheBoxM;
    float lightAheadM;
    uint flags;
    uint pad;
};

const uint BoxClaimed = 1u;
const uint BoxTurning = 2u;
const uint BoxInside = 4u;
const uint BoxCommitted = 8u;

#endif
