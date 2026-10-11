// TownWorld.Legs on the device — a drive leg (CAR-15): one turn of the driver's head (DecideDriver), the next step of the
// leg where the line in hand is spent, and the clock that says a leg is getting nowhere and what is done about it
// (WatchTheProgress, TakesOnlyTheClock). Nothing here steers or brakes; what is decided is only which line a car is on.
//
// This is the leg's composition: it brings the line (town_driving.glsl), parking (town_parking.glsl) and the laying both
// share (town_laying.glsl, twice) with it, so a kernel includes this alone. DecideTheAction is
// town_decide_the_action.glsl's, which a kernel that decides a car includes too. TakesOnlyItsLook is not here: the look
// it is held to is the switching action's (Switching.Consider, SwitchLook).

#ifndef TOWN_LEGS_GLSL
#define TOWN_LEGS_GLSL

#include "world/town/kernels/town_driving.glsl"
#include "world/town/kernels/town_parking.glsl"

#define TOWN_LAY_LINE town_LayLine
#define TOWN_NEXT_LANE_ON_ROUTE town_NextLaneOnRoute
#define TOWN_PLAN_ROUTE town_PlanRoute
#define TOWN_TAKE_THE_LANE_UNDER_IT town_TakeTheLaneUnderIt
#define TOWN_TAKE_THE_ROAD town_TakeTheRoad
#define TOWN_LAY_THE_FIRST_LINE town_LayTheFirstLine
#define TOWN_RETARGET_FROM_THE_PLAN(car) town_RetargetTheBay(car, cars_PositionM(car), ParkingRegistryNoBay)
#include "world/town/kernels/town_laying.glsl"
#undef TOWN_LAY_LINE
#undef TOWN_NEXT_LANE_ON_ROUTE
#undef TOWN_PLAN_ROUTE
#undef TOWN_TAKE_THE_LANE_UNDER_IT
#undef TOWN_TAKE_THE_ROAD
#undef TOWN_LAY_THE_FIRST_LINE
#undef TOWN_RETARGET_FROM_THE_PLAN

#define TOWN_LAY_LINE town_LayLineRetargeted
#define TOWN_NEXT_LANE_ON_ROUTE town_NextLaneOnRouteRetargeted
#define TOWN_PLAN_ROUTE town_PlanRouteRetargeted
#define TOWN_TAKE_THE_LANE_UNDER_IT town_TakeTheLaneUnderItRetargeted
#define TOWN_TAKE_THE_ROAD town_TakeTheRoadRetargeted
#define TOWN_LAY_THE_FIRST_LINE town_LayTheFirstLineRetargeted
#define TOWN_RETARGET_FROM_THE_PLAN(car) TownRetargetNone
#include "world/town/kernels/town_laying.glsl"
#undef TOWN_LAY_LINE
#undef TOWN_NEXT_LANE_ON_ROUTE
#undef TOWN_PLAN_ROUTE
#undef TOWN_TAKE_THE_LANE_UNDER_IT
#undef TOWN_TAKE_THE_ROAD
#undef TOWN_LAY_THE_FIRST_LINE
#undef TOWN_RETARGET_FROM_THE_PLAN

// How far back a queue at a light reaches, for a car of this length: a light's hold ahead within a queue's length of it.
float town_QueueReachM(uint car)
{
    precise float reachM = cars_Build(car).lengthM * legsFigures.lightQueueInCarLengths;
    return reachM;
}

// The one wait that spends no clock: a light, which will change on its own (TLT-2a).
bool town_WaitingForAReasonItCanSee(uint car) { return cars_LightAheadM(car) <= town_QueueReachM(car); }

// The line has been driven to its end and the car has stopped there: a piece of a manoeuvre at its end, a line of the
// road within the car's own length of it, at rest.
bool town_TheLineIsSpent(uint car)
{
    CarLineRow line = cars_Line(car);
    CarBuildRow build = cars_Build(car);
    precise float leftM = line.lengthM - cars_ProgressM(car);
    float withinM = town_IsManoeuvring(car) ? buildFlankM(build) : build.lengthM;
    return line.arcCount > 0u && leftM <= withinM && abs(cars_AlongMps(car)) <= legsFigures.stopSpeedMps;
}

// The line laid again from the lane the body stands on, route and all — a step of the leg only where it got the car
// more road than it had. A dead end is refused before it is searched.
bool town_TakeTheRoadAgain(uint car)
{
    int lane = cars_LaneOf(car);
    if (lane < 0 || roads_LanesFromCount(lane) == 0) return false;

    precise float leftM = cars_Line(car).lengthM - cars_ProgressM(car);
    cars_ClearRoute(car);
    if (!town_TakeTheLaneUnderIt(car)) return false;

    precise float nowLeftM = cars_Line(car).lengthM - cars_ProgressM(car);
    return nowLeftM > leftM;
}

// The next line this leg is driven on, read off where the car stands: the manoeuvre's next piece, a car standing where it
// was going, a line waiting for its bay, or the road taken again (PER-25). Whether there was a next line at all.
bool town_TakeTheNextStepOfTheLeg(uint car)
{
    if (town_IsManoeuvring(car)) return manoeuvres_IsBegun(car) && town_TheManoeuvreIsDriven(car);
    if (cars_StopsAtItsPlaceOf(car)) return true;
    if (town_IsOnTheFinalApproach(car)) return false;

    return town_TakeTheRoadAgain(car);
}

// The car is in the bay and the leg is over: the place is this car's, and the car is stood down in it.
void town_ParkIt(uint car, int bay)
{
    if (bay >= 0) parking_Occupy(bay, car);

    atomicAdd(legsCounts.baysParkedIn, 1u);
    town_StandTheCarDown(car);
    town_BeginTheStand(car);
}

// How much of this leg is left to drive: what remains of the lane the car is on, with the ground back onto its line
// counted as ground still to be covered.
float town_RemainingOnTheDriveM(uint car)
{
    precise float offM = cars_OffLineM(car) - town_OffTheLineAllowanceM(car);
    float backOntoItM = max(0.0, offM);
    CarLineRow line = cars_Line(car);
    if (line.arcCount == 0u) return backOntoItM;

    float endsAtM = line.laneCount > 0u ? cars_LaneEndM(car, 0u) : line.lengthM;
    precise float aheadM = endsAtM - cars_ProgressM(car);
    precise float remainingM = backOntoItM + max(0.0, aheadM);
    return remainingM;
}

// The leg given up where the car stands (CAR-9a): its place the town's again, the line dropped, the car stood down.
void town_GiveUpTheLeg(uint car)
{
    parking_Release(car);
    atomicAdd(legsCounts.legsGivenUp, 1u);
    town_StandTheCarDown(car);
}

// The stretch the car is blocked entering: the lane after the one under it, or that one where the line goes no further.
int town_LaneAheadOnTheLine(uint car)
{
    uint lanes = cars_Line(car).laneCount;
    if (lanes == 0u) return CarFleetNoLane;

    uint ahead = 0u;
    while (ahead < lanes - 1u && cars_ProgressM(car) >= cars_LaneStartM(car, ahead + 1u)) ahead++;

    return ahead + 1u < lanes ? cars_Chain(car, ahead + 1u) : cars_Chain(car, ahead);
}

// The stretch this car is blocked entering priced up so others route round it, and the route dropped so the next line
// is drawn over the new answer. The mark expires and is never swept.
bool town_MarkTheWayBlocked(uint car)
{
    int lane = town_LaneAheadOnTheLine(car);
    if (lane < 0) return false;

    int link = driving_LinkOfLane(lane);
    if (link == TravelGraphNoLink) return false;

    routes_Mark(RouteDriving, link, legsFigures.carBlockedWayPriceM, legsFigures.carBlockedWayLifeS);
    cars_Reroutes(car)++;
    cars_ClearRoute(car);
    return town_TakeTheLaneUnderIt(car);
}

// Whether this leg is getting anywhere, and what is done when it is not: the road priced up and the route laid again,
// then another bay near where the car has got to, then the leg over. A retarget whose search has not answered is not
// a leg to give up: the car is decided again.
void town_WatchTheProgress(uint car, float elapsedS)
{
    float remainingM = town_RemainingOnTheDriveM(car);

    // A light buys the wait rather than the standing, so the clock is held where it is rather than given back.
    if (town_WaitingForAReasonItCanSee(car)) legProgress_Hold(cars_ClockClosestM(car), remainingM);
    else legProgress_Note(cars_ClockSinceS(car), cars_ClockClosestM(car), remainingM, cars_Build(car).widthM, elapsedS);

    precise float patienceS = legsFigures.carPatienceS * cars_FuseJitter(car);
    if (!legProgress_IsStuck(cars_ClockSinceS(car), patienceS)) return;

    if (int(cars_Reroutes(car)) < legsFigures.reroutesPerLeg && town_MarkTheWayBlocked(car))
    {
        atomicAdd(legsCounts.reroutesTaken, 1u);
        driveProgress_Restart(car);
        return;
    }

    // The road was not the problem, so the place might be: the destination's own last chance.
    uint retargeted = town_RetargetTheBay(car, cars_PositionM(car), town_BayAimedAt(car));
    if (retargeted == TownRetargetPending) return;
    if (retargeted == TownRetargetTaken)
    {
        atomicAdd(legsCounts.placesGivenUp, 1u);
        driveProgress_Restart(car);
        return;
    }

    town_GiveUpTheLeg(car);
}

// What a car's leg's clock is run by: its patience, how much nearer counts as nearer, and how far back a queue reaches.
void town_ClockTermsOf(uint car, out float patienceS, out float byM, out float queueReachM)
{
    precise float jitteredS = legsFigures.carPatienceS * cars_FuseJitter(car);
    patienceS = jitteredS;
    byM = cars_Build(car).widthM;
    queueReachM = town_QueueReachM(car);
}

// Whether a driven car's errand leaves it to its leg this decision: an owner's car its owner drives to a bay, or one its
// owner is not at the wheel of.
bool town_ItsErrandLeavesItToTheLeg(uint car)
{
    if (cars_Owner(car) == CarFleetNoOwner || town_WheelIsHeldOver(car)) return true;

    int owner = cars_Owner(car);
    return !town_IsAtTheWheel(owner) || (people_Stage(owner) != TripStageStandingBy && town_BayAimedAt(car) >= 0);
}

// No duty or order, an errand that leaves the car to its leg, a car driving its route with no line to grow and no bay to
// take up — whatever its action.
bool town_LeavesItToItsLeg(uint car)
{
    return !town_HoldAgents() && !town_IsUnderOrders(car) && cars_Ambulance(car) == 0u && !town_IsAnEvacuator(car)
        && !town_IsAPatrolCar(car) && cars_Driven(car) != 0u && cars_Broken(car) == 0u && town_ItsErrandLeavesItToTheLeg(car)
        && town_AsksTheRoute(car) == 0u && cars_StopsForBayOf(car) == CarFleetNoBay;
}

bool town_LeavesItsDecisionToItsLine(uint car) { return cars_Action(car) == CarActionFollow && town_LeavesItToItsLeg(car); }

bool town_LeavesItsSwitchToItsLine(uint car) { return cars_Action(car) == CarActionSwitch && town_LeavesItToItsLeg(car); }

// Whether this car's decision would do nothing but run its leg's clock — no line to grow, nothing it may get past that
// it has come up to, no lane beside it means to move across onto, its line not spent and a clock that will not run out
// on this decision. Asked of nothing it changes.
bool town_TakesOnlyTheClock(uint car, float sinceLastDecisionS)
{
    if (!town_LeavesItsDecisionToItsLine(car) || town_TheLineIsSpent(car)) return false;

    ClaimRow cutBy;
    int cutOn;
    if (overtaking_MayGetPastWhatCutIt(car, cutBy, cutOn) && overtaking_ComesUpTo(car, cars_AlongMps(car), cars_Context(car).authorityM))
    {
        return false;
    }

    bool inward;
    if (switching_Wants(car, inward)) return false;

    float patienceS;
    float byM;
    float queueReachM;
    town_ClockTermsOf(car, patienceS, byM, queueReachM);
    return !legProgress_WouldBeStuck(
        cars_ClockSinceS(car), cars_ClockClosestM(car), town_RemainingOnTheDriveM(car), byM, sinceLastDecisionS,
        cars_LightAheadM(car) <= queueReachM, patienceS);
}

// One turn of the driver's own head: the rest of the line laid, the action decided, the next step of the leg where the
// line in hand is spent, and the clock. A car nobody is in, a wreck and a car under a hand have no leg to decide.
void town_DecideDriver(uint car, float sinceLastDecisionS)
{
    if (cars_Driven(car) == 0u || cars_Broken(car) != 0u || cars_Action(car) == CarActionHand)
    {
        town_AsksTheRoute(car) = 0u;
        driveProgress_Restart(car);
        return;
    }

    town_LayTheRestOfTheLine(car);
    town_DecideTheAction(car, sinceLastDecisionS);

    if (town_TheLineIsSpent(car) && town_TakeTheNextStepOfTheLeg(car))
    {
        driveProgress_Restart(car);
        return;
    }

    town_WatchTheProgress(car, sinceLastDecisionS);
}

#endif
