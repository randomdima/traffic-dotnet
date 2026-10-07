// The walkers as rows on the device, field for field with app/devicetick/DeviceWalkers' WalkerRow and LineWay.

// WalkingGround.MostWaysAlongAWalk; app/devicetick/DeviceWalkers refuses a host whose figure is another.
const int WalkerMostWays = 5;

// PersonFleet.NoWay, LaneOccupancy.NoWay and NoHold, PersonFleet.NoCar and NoCrossing: all −1.
const int WalkerNone = -1;

// RouteChain.NoWay.
const int WalkerRouteNoWay = int(0x80000000u);

// PersonAction.
const uint ActionStand = 0u;
const uint ActionWalk = 1u;
const uint ActionSidestep = 2u;
const uint ActionRejoin = 3u;
const uint ActionPost = 4u;
const uint ActionHand = 5u;

// TripStage.
const uint StageWalkingToTheCar = 2u;
const uint StageWalkingFromTheCar = 4u;
const uint StageOnDuty = 8u;

// ClaimPriority.Afoot.
const uint RungAfoot = 5u;

// WalkerRow.flags.
const uint WalkerWalking = 1u;
const uint WalkerWounded = 2u;
const uint WalkerInside = 4u;
const uint WalkerRunsOut = 8u;
const uint WalkerPassBegun = 16u;
const uint WalkerHeldAsDriving = 32u;

// The walker has committed to a straight at some time (WalkingGround's place walked to is a number). Carried as a flag
// rather than as a place that is not one: a device may assume no float it compares is a NaN.
const uint WalkerOnAStraight = 64u;

// One walker as the walkers' side of a rebuild reads and writes it: the fleet's fields (PersonFleet), the pass it is on
// (Sidestep) and how many runs its ground was swept in, where it decided to walk back to (WalkingBack), the straight it
// committed to (WalkingGround), the car whose officer it is (PatrolDuty), whose ground its body is laid as
// (WalkingGround.HeldAs), the hold its plan laid, and how fast it is going the way it faces (WalkingGround.AlongItsWalkMps).
struct WalkerRow
{
    vec2 positionM;
    vec2 declaredMps;
    vec2 destinationM;
    vec2 goalM;
    vec2 backToM;
    vec2 straightFromM;
    vec2 straightToM;
    float radiusM;
    float onWayM;
    float offWayM;
    float routeToM;
    float grantM;
    float passFromM;
    float passClearM;
    float passAsideM;
    int passSlot;
    int routeCount;
    int routeTaken;
    int onWay;
    int onCrossing;
    int car;
    int dutyCar;
    int heldAs;
    uint straightRuns;
    uint passRuns;
    uint flags;
    uint action;
    uint stage;
    int hold;
    float alongMps;
    uint pad;
};

// One of the town's ways under a stretch of a walk (LineWay).
struct WalkerWay
{
    int way;
    float fromM;
    float toM;
    float lineFromM;
};
