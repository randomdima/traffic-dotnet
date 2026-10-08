// The rows a car's tick on the device carries from one tick to the next and is told (car_tick.glsl), field for field
// with app/devicetick/DeviceCarTick.

#ifndef CAR_TICK_ROWS_GLSL
#define CAR_TICK_ROWS_GLSL

// What a car's tick carries from one tick to the next and reports, the device's own (CarFleet.ProgressM,
// CoveredSinceClaimM, AlongMps, OffLineM, PlannedMps, Hold): flags is what the tick found.
struct CarTickRow
{
    float progressM;
    float coveredSinceClaimM;
    float alongMps;
    float offLineM;
    float plannedMps;
    uint hold;
    uint flags;
};

// What the host tells a car's tick, its decisions' (CarFleet.PaceMps, TownWorld.OffTheLineAllowanceM, SceneOf;
// Switching.AsideM, WaitsShortOfTheEnd): flags in the bits ContextCarRow's flags have — but HasAHold and Cut, which are
// the claims' and come from the rebuild beside — and above them whether the car is moving across (CAR-53) and whether
// its line stops where its route moves across; and the place it was sent to and how near it has to stand.
struct CarToldRow
{
    uint flags;
    float paceMps;
    float allowanceM;
    float passAsideM;
    float fromRestM;
    float placeReachM;
    vec2 placeM;
};

const uint ToldSwitching = 32u;
const uint ToldWaitsShortOfTheEnd = 64u;

const uint TickLost = 32u;
const uint TickAsksTheRoute = 64u;
const uint TickShifted = 128u;
const uint TickHazard = 256u;
const uint TickSteppedOut = 512u;

// What the last claims rebuild granted a car and what cut it (CarFleet.AuthorityM, HorizonM, ClaimFromM, CommittedToM,
// GrantMarginM, GrantCutBy; LaneOccupancy.HoldCutOn). The place fields are unread: the place is told (CarToldRow).
struct CarGrantRow
{
    float authorityM;
    float horizonM;
    float claimFromM;
    float committedToM;
    float grantMarginM;
    uint grantCutBy;
    int cutOn;
    float placeReachM;
    vec2 placeM;
};

#endif
