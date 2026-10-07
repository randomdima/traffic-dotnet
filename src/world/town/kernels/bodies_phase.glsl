// The bodies' phase on the device (TownWorld.StepBodies; app/devicetick/DeviceBodyPhase): the tick's impulses spent on
// the solver's bodies, the solver's step, then every car read back off its body into the car table. What its kernels
// share — the push block and the rows the host lays — field for field with DeviceBodyPhase's.

layout(push_constant) uniform Phase
{
    // The cars a kernel is run over, or the impulses laid.
    uint count;
    uint wheelsACar;
    float dtS;

    // What a car's lagged acceleration closes on its new reading by in one step (TownWorld.LoadSettledShare).
    float loadSettledShare;

    // The bit of a body's flags that says it is in the world (BodyFlags.Enabled).
    uint enabledBit;
    uint pad0;
    uint pad1;
    uint pad2;
};

// Which body a car is, and whether it rested parked on its last tick (DeviceBodyPhase.CarBodyRow).
struct CarBodyRow
{
    int body;
    uint flags;
};

const uint CarRestsParked = 1u;

// One impulse the host lays rather than the device yet produces (DeviceBodyPhase.LaidImpulseRow): at a point, or at
// the body's centre where `central` is set.
struct LaidImpulseRow
{
    vec2 impulseNs;
    vec2 atM;
    int body;
    uint central;
    uint pad0;
    uint pad1;
};
