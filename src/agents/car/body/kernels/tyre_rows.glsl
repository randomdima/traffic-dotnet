// The rows a car's tyres are carried in on the device, field for field with app/devicetick/DeviceTyres: the pose they
// read, the command they deliver, the car's own figures and each wheel's carried state — shared by every kernel that
// reads or writes the tyres' table, whether or not it runs the tyre model.

// The body at the start of the tick (CarPose); forward is the body's own rotation, the one way the car points.
struct TyrePose
{
    vec2 positionM;
    vec2 velocityMps;
    vec2 forward;
    vec2 accelerationMps2;
    float headingRad;
    float yawRateRadPerS;
    float massKg;
    float pad;
};

// The pedals and the wheel (DriveCommand): a brake of +inf locks every wheel.
struct TyreCommand
{
    float steerRad;
    float throttleMps2;
    float brakeMps2;
    uint handbrakeAndReverse;
};

// One car's own figures and flags (DeviceTyres.CarRow): hand 1, driven wheels slipping 2, at rest 4 (written), and which
// of its wheels stood on a way when its body was last laid in bits 8–11.
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

uint tyreWheelsOnAWayOf(TyreCarRow row) { return (row.flags >> 8) & 0xfu; }
