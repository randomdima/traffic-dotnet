// The rows the world layer on the device keeps and hands back (app/devicetick/DeviceWorld), field for field with
// DeviceWorld's: the control signals as the device takes them, its own rows of each car and walker, and the shallow
// state it hands back.

#ifndef WORLD_ROWS_GLSL
#define WORLD_ROWS_GLSL

// One control signal as the device takes it (DeviceWorld.SignalRow): the body, what it asks in the low half and its
// flags in the high, and its figures — a body's own, worked out by the host where only the host's table has them (a
// placed body's rotation, a layer's mask).
struct WorldSignal
{
    int body;
    uint actionAndFlags;
    vec2 p01;
    vec2 p23;
    vec2 p45;
};

const uint SignalDrive = 1u;
const uint SignalWalk = 2u;
const uint SignalPlace = 3u;
const uint SignalContain = 4u;
const uint SignalLayer = 5u;
const uint SignalChannels = 6u;
const uint SignalTow = 7u;
const uint SignalClearWheels = 8u;
const uint SignalShow = 9u;
const uint SignalFollow = 10u;

// ControlSignal's flags.
const uint DriveHandbrake = 1u;
const uint DriveReverse = 2u;
const uint DriveUnderAHand = 4u;
const uint WalkMoving = 1u;
const uint WalkOnFeet = 2u;
const uint WalkFaces = 4u;
const uint TowByTheTail = 1u;

// The host's mark on a command or an aim to be taken in order with the events (DeviceWorld.SignalInOrder).
const uint SignalInOrder = 0x8000u;

uint signalAction(WorldSignal s) { return s.actionAndFlags & 0x7fffu; }
bool signalInOrder(WorldSignal s) { return (s.actionAndFlags & SignalInOrder) != 0u; }
uint signalFlags(WorldSignal s) { return s.actionAndFlags >> 16; }

// One car as the world keeps it beside its tyres (DeviceWorld.CarRow): its body, the tractor whose bar it hangs off or
// −1, and what it weighs as the tyres read it.
struct WorldCar
{
    int body;
    int tractor;
    uint flags;
    float massKg;
};

const uint CarByTheTail = 1u;

// Whether its body came into the last step frozen, which "frozen through the step" is read against after it; and whether
// it comes into the next one frozen — frozen as the last step left it and thawed by no signal since (world_view.comp).
const uint CarFrozenBefore = 2u;
const uint CarFrozenNext = 4u;

// What the car shows as last signalled (ControlAction.Show): its lamps, a CarLampSet, from bit 8, and whether it is a wreck.
const uint CarShowsShift = 8u;
const uint CarShowsBroken = 1u << 16;
const uint CarShows = 0x1ff00u;

// One walker as the world keeps it (DeviceWorld.WalkerRow): what it was told, its footing's own state, and how far it has
// walked, which its gait is drawn off.
struct WorldWalker
{
    vec2 aimM;
    vec2 declaredMps;
    float headingRad;
    float massKg;
    float radiusM;
    int body;
    uint flags;
    float distanceWalkedM;
};

// Walk flags in the low bits, as signalled; inside a container in the next.
const uint WalkerInside = 8u;

// A car's shallow state handed back (CarView).
struct CarViewRow
{
    vec2 positionM;
    vec2 velocityMps;
    float headingRad;
    float yawRateRadPerS;
    uint flags;
    float overlapM;
    vec2 forward;
    float progressM;
    float offLineM;
    uint followSerial;
    float leadAtM;
    float aimRad;
    uint pad;
};

// Its words, as world_view.comp writes the rows out.
const uint CarViewWords = 16u;

const uint ViewFrozen = 1u;
const uint ViewFrozenThroughLastStep = 2u;

// world_view.comp's mode bit: the pass is the step's own, and keeps what the step settled (DeviceWorld.ViewStepped).
const uint ViewStepped = 2u;

// A walker's shallow state handed back (WalkerView).
struct WalkerViewRow
{
    vec2 positionM;
    vec2 velocityMps;
    float headingRad;
    float overlapM;
};

#endif
