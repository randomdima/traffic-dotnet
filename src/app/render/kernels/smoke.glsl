// The cars' smoke's tracks on the device (CAR-54, app/devicetick/DeviceSmoke): a point of a car's way and a car's track as
// a whole, field for field with DeviceSmoke.Point and DeviceSmoke.Head, and the way between two points. Laid by the step
// (smoke_track.comp) and read by the emit (sprites.glsl's spritesPuff).

#ifndef SMOKE_GLSL
#define SMOKE_GLSL

// Where the car was, which way its nose pointed, how it moved, whether it was running — one, or nought, and between them
// on the way from one to the other — the share of full throttle its driver asked of it, and when, on the step's clock.
struct SmokePoint
{
    vec2 positionM;
    vec2 forward;
    vec2 velocityMps;
    float running;
    float throttle;
    float timeS;
    float pad;
};

// The car as the last step left it, the newest of its points in its ring and how many of the ring are its way, when it
// last ran — a track not run on for a puff's life draws nothing — and the moment of the view the host shows, the step
// before (SimFigures.WorldStepsAhead), which the smoke is drawn at so its newest puff leaves the car where it is drawn.
struct SmokeHead
{
    SmokePoint live;
    uint newest;
    uint count;
    float ranS;
    float shownS;
};

// The way from one point to the next at a share of it, each figure in a straight line.
SmokePoint smokeBetween(SmokePoint from, SmokePoint to, float share)
{
    return SmokePoint(
        mix(from.positionM, to.positionM, share), mix(from.forward, to.forward, share),
        mix(from.velocityMps, to.velocityMps, share), mix(from.running, to.running, share),
        mix(from.throttle, to.throttle, share), mix(from.timeS, to.timeS, share), 0.0);
}

#endif
