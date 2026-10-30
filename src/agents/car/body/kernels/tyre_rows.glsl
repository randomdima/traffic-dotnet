// The rows a car's tyres are carried in on the device, field for field with app/devicetick/DeviceTyres: the pose they
// read, the command they deliver, the car's own figures and each wheel's carried state — shared by every kernel that
// reads or writes the tyres' table, whether or not it runs the tyre model.

// The body at the start of the tick (CarPose); forward is the body's own rotation, the one way the car points.
#ifndef TYRE_ROWS_GLSL
#define TYRE_ROWS_GLSL

#include "agents/car/body/kernels/car_wheels.glsl"

// A body on its springs (SprungBody): sunk, pitched nose down and rolled right side down off where it stands at rest, and
// how fast each is going.
struct SprungBody
{
    float heaveM;
    float pitchRad;
    float rollRad;
    float heaveMps;
    float pitchRadPerS;
    float rollRadPerS;
};

struct TyrePose
{
    vec2 positionM;
    vec2 velocityMps;
    vec2 forward;
    float headingRad;
    float yawRateRadPerS;
    float massKg;
    SprungBody sprung;
};

// The pedals and the wheel (DriveCommand): a brake of +inf locks every wheel.
struct TyreCommand
{
    float steerRad;
    float throttleMps2;
    float brakeMps2;
    uint handbrakeAndReverse;
};

// One car's own figures and flags (DeviceTyres.CarRow): hand 1, driven wheels slipping 2, at rest 4 (written), which
// of its wheels stood on a way when its body was last laid in bits 8–15, and which stand on nothing in 16–23, as the town
// last said (ControlAction.Hang, PHY-1b), and whether its body was on a channel above the ground's when last laid in bit
// 24 — the level it is drawn at, which its marks are laid on (DriftKnot.Raised).
struct TyreCarRow
{
    float inertiaKgM2;
    float slipThrottle;
    uint flags;
};

// One wheel's carried state (DeviceTyres.WheelRow).
struct TyreWheelRow
{
    vec2 markFromM;
    float spinMps;
    float scrubTravelM;
    float markIntensity;
    uint marking;
};

uint tyreWheelsOnAWayOf(TyreCarRow row) { return (row.flags >> 8) & 0xffu; }

uint tyreWheelsOverNothingOf(TyreCarRow row) { return (row.flags >> 16) & 0xffu; }

const uint TyreCarRaised = 1u << 24;

#endif
