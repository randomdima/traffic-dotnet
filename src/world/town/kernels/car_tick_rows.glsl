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
// Switching.AsideM, WaitsShortOfTheEnd; Overtaking.AsideOfItsPassM, StepsOutAtM): flags in the bits ContextCarRow's flags
// have — but HasAHold and Cut, which are the claims' and come from the rebuild beside — and above them whether the car is
// moving across (CAR-53), whether its line stops where its route moves across and whether it is getting past something
// (CAR-46); the place it was sent to and how near it has to stand; and where an overtaking car's step out from rest
// begins along its line.
struct CarToldRow
{
    uint flags;
    float paceMps;
    float allowanceM;
    float passAsideM;
    float fromRestM;
    float placeReachM;
    vec2 placeM;
    float stepsOutAtM;
    float pad;
};

const uint ToldSwitching = 32u;
const uint ToldWaitsShortOfTheEnd = 64u;
const uint ToldOvertaking = 128u;

const uint TickLost = 32u;
const uint TickAsksTheRoute = 64u;
const uint TickShifted = 128u;
const uint TickHazard = 256u;
const uint TickSteppedOut = 512u;
const uint TickPassMade = 1024u;

// What a tick asks of the town (app/devicetick/DeviceCarTick.TickRow.Events): every flag but what the tick only reports.
const uint TickEvents = TickLost | TickAsksTheRoute | TickShifted | TickHazard | TickSteppedOut | TickPassMade;

// What the town reads of a car's tick besides its tick row (app/devicetick/DeviceCarTick.BriefRow, field for field): of
// the context only how far it was granted, how far aside its pass steps and whether it is blocked — the junction ahead
// whole — so the rest of the context need not come back.
struct CarBriefRow
{
    float authorityM;
    float passAsideM;
    float toTheBoxM;
    float lightAheadM;
    uint flags;
};

// CarBriefRow.flags: BoxAheadRow's, and the context's own.
const uint BriefBlocked = 16u;

// A car's grant as CarGrantRow holds it, but for the place, which is told: what the town holds of it only as a car it
// reads hands it back (app/devicetick/DeviceCarTick.GrantOut).
struct CarGrantOut
{
    float authorityM;
    float horizonM;
    float claimFromM;
    float committedToM;
    float grantMarginM;
    uint grantCutBy;
    int cutOn;
};

// What the town reads of a car's tick, written into its own memory where it reads it (app/devicetick/DeviceCarTick.OutRow,
// field for field): its tick row, command, brief and tyre row, its grant, its leg's clock (LegProgress), and the stamp of
// the tick that wrote it, never the zero of a row unwritten. The includer includes tyre_rows.glsl first.
struct CarOutRow
{
    CarTickRow tick;
    TyreCommand command;
    CarBriefRow brief;
    TyreCarRow car;
    CarGrantOut grant;
    float clockSinceS;
    float clockClosestM;
    uint stamp;
};

// What only the town knows of a car whose decision the device is asked whether it is its clock alone
// (app/devicetick/DeviceCarTick.DecideTermsRow, TownWorld.TakesOnlyTheClock): flags — whether the town decides it whatever
// the device finds, and what of Switching.Wants the road and the route alone answer (Switching.RoadShape) — its leg's
// patience, the width that counts as getting nearer, and how far back a queue at a light reaches.
struct CarDecideTermsRow
{
    uint flags;
    float patienceS;
    float byM;
    float queueReachM;
};

// CarDecideTermsRow.flags: Switching.RoadShape's in the low bits, then whether the town decides it whole — and of a car
// moving across whose look the device takes (DecideSwitching), which way its line stopping to move across has it move
// (DecideShapeInward), and which way its last decision had it mean to (DecideSwitchInward).
const uint DecideShapeAsks = 1u;
const uint DecideShapeStopsToMoveAcross = 2u;
const uint DecideShapeWants = 4u;
const uint DecideShapeWantsWhenHeldUp = 8u;
const uint DecideShapeWantsWhenFree = 16u;
const uint DecideByTheTown = 256u;
const uint DecideChecked = 512u;
const uint DecideSwitching = 1024u;
const uint DecideShapeInward = 2048u;
const uint DecideSwitchInward = 4096u;

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

CarGrantOut grantOut(CarGrantRow grant)
{
    return CarGrantOut(
        grant.authorityM, grant.horizonM, grant.claimFromM, grant.committedToM, grant.grantMarginM, grant.grantCutBy, grant.cutOn);
}

#endif
