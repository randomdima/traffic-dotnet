// The people's fields no row of town_rows.glsl holds, by id (288–383) and by their C# names: people_TimerS(p) is
// PersonFleet.TimerS[p]. Laid once off the town (app/devicetick/TownState.People). A walker's field a row holds is reached
// there — Walking, Wounded, RouteRunsOut and Inside.Any are people_WalkFlags(p)'s bits (walker_rows.glsl), and the pass
// it is on (PersonFleet.Pass) is its walk row's, named below.

#ifndef TOWN_PEOPLE_GLSL
#define TOWN_PEOPLE_GLSL

#include "world/town/kernels/town_tables.glsl"

// PersonAction.
const uint PersonActionStand = 0u;
const uint PersonActionWalk = 1u;
const uint PersonActionSidestep = 2u;
const uint PersonActionRejoin = 3u;
const uint PersonActionPost = 4u;
const uint PersonActionHand = 5u;
const uint PersonActionInside = 6u;
const uint PersonActionDown = 7u;

// TripStage.
const uint TripStageStandingBy = 0u;
const uint TripStageWalkingToTheDoor = 1u;
const uint TripStageWalkingToTheCar = 2u;
const uint TripStageDriving = 3u;
const uint TripStageWalkingFromTheCar = 4u;
const uint TripStageWaitingForAPlace = 5u;
const uint TripStageDwelling = 6u;
const uint TripStageUnderOrders = 7u;
const uint TripStageOnDuty = 8u;

// ContainerKind; PersonFleet.NoBuilding, NoCar, NoWay, NoCrossing; how much of a route a walker carries at once.
const uint ContainerKindNone = 0u;
const uint ContainerKindBuilding = 1u;
const uint ContainerKindCar = 2u;
const int PersonFleetNoBuilding = -1;
const int PersonFleetNoCar = -1;
const int PersonFleetNoWay = -1;
const int PersonFleetNoCrossing = -1;
const uint PersonFleetRouteWaysPerPerson = 64u;

// Where somebody is (Contained): inside nothing, a building or a car, and which.
struct Contained
{
    uint kind;
    int index;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer ContainedRows { Contained r[]; };

const uint PeopleDrawId = 288u;
const uint PeopleManualId = 289u;
const uint PeopleRecklessId = 290u;
const uint PeopleVariantId = 291u;
const uint PeopleDestinationBuildingId = 292u;
const uint PeopleTimerSId = 293u;
const uint PeopleInsideId = 294u;
const uint TownWoundedWordsId = 295u;

#define people_Draw(p) TOWN(Rngs, PeopleDrawId).r[p]
#define people_Manual(p) TOWN(Uints, PeopleManualId).v[p]
#define people_Reckless(p) TOWN(Uints, PeopleRecklessId).v[p]
#define people_Variant(p) TOWN(Uints, PeopleVariantId).v[p]
#define people_DestinationBuilding(p) TOWN(Ints, PeopleDestinationBuildingId).v[p]
#define people_TimerS(p) TOWN(Floats, PeopleTimerSId).v[p]

// PersonFleet.Inside, the one array Containers' _personIsIn is too: what its walk row's Inside flag says of it.
#define people_Inside(p) TOWN(ContainedRows, PeopleInsideId).r[p]

// PersonFleet.Pass (Sidestep) as its walk row holds it: Slot, FromM, ClearM, AsideM, and Begun as WalkerPassBegun.
#define people_PassSlot(p) people_Walk(p).passSlot
#define people_PassFromM(p) people_Walk(p).passFromM
#define people_PassClearM(p) people_Walk(p).passClearM
#define people_PassAsideM(p) people_Walk(p).passAsideM

// TownWorld._wounded: the walkers wounded as a bitset (AgentSet, a word 32 walkers: bit p & 31 of word p >> 5).
#define town_WoundedWord(w) TOWN(Uints, TownWoundedWordsId).v[w]

#endif
