// Every function the drive leg, the line and parking (town_legs.glsl, town_driving.glsl, town_parking.glsl) call, declared
// before any of them is defined: the town's own, which call each other across those files, and those another port
// defines in the file its C# owner maps to — a car's actions (agents/car/actions, Manoeuvres) and the errands' town
// partials. A kernel that calls one links its definition; a test kernel defines what it reaches from staged answers.

#ifndef TOWN_LEG_CALLS_GLSL
#define TOWN_LEG_CALLS_GLSL

#include "world/town/kernels/town_state.glsl"

// Manoeuvres (agents/car/control/Manoeuvres.cs).
bool manoeuvres_IsBegun(uint car);
void manoeuvres_Clear(uint car);

// The car's actions (agents/car/actions/…).
void overtaking_ShiftTheLine(uint car, int lane, float shiftM);
bool overtaking_MayGetPastWhatCutIt(uint car, out ClaimRow cutBy, out int cutOn);
bool overtaking_ComesUpTo(uint car, float alongMps, float toTheStopM);
void switching_ShiftTheLine(uint car, float shiftM);
bool switching_Wants(uint car, out bool inward);
bool pullingOut_ShapeTheWayOut(uint car, int bay, int lane);
bool pullingOut_Arrive(uint car);
bool parkingIn_Arrive(uint car);
float parkingIn_StopForTheBayM(uint car, int bay, int lane);
bool bayManoeuvring_TakeTheNextPiece(uint car);

// The errands' town partials: TownWorld.Ambulance (IsAimedAtAPlaceInTheRoad), Closure (TheEntranceItIsSentTo, and
// ClosedLinksFor and ClosedLinksNow as whether the closed runs bar a search at all), PlayerOrders (IsUnderOrders,
// _carOrders.Release), Recovery (IsAnEvacuator, OffTheLineAllowanceM), Patrol (IsAPatrolCar), Spawn (WheelIsHeldOver),
// OwnCars (IsAtTheWheel), Round (BeginTheStand), and the town's own HoldAgents.
bool town_IsAimedAtAPlaceInTheRoad(uint car);
bool town_TheEntranceItIsSentTo(uint car, out int lane, out float alongM);
bool town_ClosedLinksFor(uint car);
bool town_ClosedLinksNow();
bool town_IsUnderOrders(uint car);
void carOrders_Release(uint car);
bool town_IsAnEvacuator(uint car);
float town_OffTheLineAllowanceM(uint car);
bool town_IsAPatrolCar(uint car);
bool town_WheelIsHeldOver(uint car);
bool town_IsAtTheWheel(int person);
void town_BeginTheStand(uint car);
bool town_HoldAgents();

// Where on the carriageway a body stands, as a line is laid from (TownWorld.StandingOn): the lane a line over it begins
// on, the lane after the join it stands in or none, how far along that line it stands, and the line's sample there.
struct TownStandingOn
{
    int lane;
    int onward;
    float alongM;
    vec2 positionM;
    float headingRad;
    float curvature;
};

// TownWorld.RetargetTheBay's answer: no bay taken, one taken, or a search it needs asked and not yet answered.
const uint TownRetargetNone = 0u;
const uint TownRetargetTaken = 1u;
const uint TownRetargetPending = 2u;

// The leg's own, across its files (TownWorld.Legs, Driving, Parking, Actions; town_decide_the_action.glsl).
void town_DecideTheAction(uint car, float sinceLastDecisionS);
TownStandingOn town_TheCarriagewayUnder(vec2 rearAxleM, vec2 forward, uint level);
uint town_TheChainFrom(uint car, TownStandingOn under);
int town_BayGoals(int bay, out RouteGoal into[ROUTE_MOST_GOALS]);
bool town_LeaveTheBay(uint car, int bay);
void town_Enter(uint car, uint action);
bool town_IsManoeuvring(uint car);
bool town_TheManoeuvreIsDriven(uint car);
bool town_TheLineIsSpent(uint car);
void town_LayLine(uint car, uint from, float spentM, bool fromTheQueue);
bool town_TakeTheLaneUnderIt(uint car);
bool town_TakeTheRoad(uint car);
void town_LayTheFirstLine(uint car);
void town_LayLineRetargeted(uint car, uint from, float spentM, bool fromTheQueue);
bool town_TakeTheLaneUnderItRetargeted(uint car);
bool town_TakeTheRoadRetargeted(uint car);
void town_LayTheFirstLineRetargeted(uint car);
uint town_RetargetTheBay(uint car, vec2 nearM, int avoidBay);
int town_BayAimedAt(uint car);
bool town_TakeABayToTurnIn(uint car, int fromLane, int backLane);
int town_TheBayTheLineStopsFor(uint car, int lastLane);
float town_ThePlaceTheLineStopsAtM(uint car, int lastLane);
bool town_IsOnTheFinalApproach(uint car);
void town_GiveUpTheTurn(uint car);
void town_StandTheCarDown(uint car);
int town_PlanTheWayOut(uint car, int bay, int onlyLane);
vec2 town_ForwardOf(uint car);

#endif
