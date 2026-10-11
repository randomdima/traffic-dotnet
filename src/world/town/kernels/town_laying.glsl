// A line laid from where a car stands, on the device: TownWorld.Driving's LayLine, NextLaneOnRoute and PlanRoute, and
// TownWorld.Parking's LayTheFirstLine, TakeTheRoad and TakeTheLaneUnderIt — line for line with the C#.
//
// Laid twice, because GLSL has no recursion and the C# reaches these six again from inside PlanRoute (RetargetTheBay →
// LayTheFirstLine → TakeTheRoad → TakeTheLaneUnderIt → LayLine). town_legs.glsl includes this once under the C#'s own
// names and once under theirs with Retargeted on the end, which is what RetargetTheBay lays its first line by. The
// second laying's PlanRoute retargets nothing: it is reached only once RetargetTheBay has claimed a bay, and a leg aimed
// at a bay does not retarget in the C# either. The includer defines TOWN_LAY_LINE, TOWN_NEXT_LANE_ON_ROUTE,
// TOWN_PLAN_ROUTE, TOWN_TAKE_THE_LANE_UNDER_IT, TOWN_TAKE_THE_ROAD, TOWN_LAY_THE_FIRST_LINE (the six names) and
// TOWN_RETARGET_FROM_THE_PLAN(car) (what PlanRoute's retarget answers), and includes town_driving.glsl and
// town_parking.glsl first.

bool TOWN_PLAN_ROUTE(uint car, int fromLane);

// The next lane the car's own route says to take, which the road joins to fromLane — or, where the route has run out, the
// route searched for once a line and the tour after that; none where the line stops here. A search asked and not yet
// answered stops the line where it is, and the car's next decision lays the rest (town_AsksTheRoute).
int TOWN_NEXT_LANE_ON_ROUTE(uint car, int fromLane, inout bool searched)
{
    int next = cars_PeekNextRouteLane(car);

    // A route that moves across here (CAR-53) is carried on down the lane the car is on, beside it.
    int besideAt;
    if (next >= 0 && roads_ConnectorBetween(fromLane, next) == RoadGraphNoConnector && roads_ReachesBySwitching(fromLane, next, besideAt))
    {
        return town_OnBesideTheRoute(car, fromLane, next);
    }

    // A queued lane the road does not join to the one under the car is a route from before a recovery: the whole of it goes.
    if (next >= 0 && roads_ConnectorBetween(fromLane, next) == RoadGraphNoConnector)
    {
        cars_ClearRoute(car);
        next = CarFleetNoLane;
    }

    if (next >= 0) return cars_TakeNextRouteLane(car);

    // A frontage this leg turns at (GEN-4l), or the lane the leg ends on: the queue stops here.
    if (town_TurnsBackHere(car, fromLane)) return CarFleetNoLane;
    if (town_TheLegEndsOn(car, fromLane)) return CarFleetNoLane;

    if (searched) return laneTour_NextLane(fromLane, cars_Draw(car), legsFigures.turnPriceNearSideCarLengths, legsFigures.turnPriceAcrossOncomingCarLengths);

    searched = true;
    if (TOWN_PLAN_ROUTE(car, fromLane))
    {
        town_AsksTheRoute(car) = 1u;
        return CarFleetNoLane;
    }

    next = cars_PeekNextRouteLane(car);
    if (next >= 0)
    {
        return roads_ConnectorBetween(fromLane, next) == RoadGraphNoConnector
            ? town_OnBesideTheRoute(car, fromLane, next)
            : cars_TakeNextRouteLane(car);
    }

    if (town_TurnsBackHere(car, fromLane)) return CarFleetNoLane;

    return town_TheLegEndsOn(car, fromLane)
        ? CarFleetNoLane
        : laneTour_NextLane(fromLane, cars_Draw(car), legsFigures.turnPriceNearSideCarLengths, legsFigures.turnPriceAcrossOncomingCarLengths);
}

// A route from the far end of fromLane to where the car is going, expanded into the lanes the line is laid over — true
// where it was asked and is not answered yet. A leg with no bay left claims another near where the car has got to
// (AMB-5, EVA-3: an errand's leg aimed at a place in the road claims none).
bool TOWN_PLAN_ROUTE(uint car, int fromLane)
{
    cars_ClearRoute(car);

    // The turn goes with the route that asked for it (GEN-4l), and is claimed again where the new route still turns there.
    town_GiveUpTheTurn(car);
    if (fromLane < 0 || cars_HasDestination(car) == 0u) return false;

    if (!town_IsAimedAtAPlaceInTheRoad(car) && town_BayAimedAt(car) < 0)
    {
        uint retargeted = TOWN_RETARGET_FROM_THE_PLAN(car);
        if (retargeted == TownRetargetPending) return true;
        if (retargeted == TownRetargetNone) return false;
    }

    uint found = town_TryPlan(car, fromLane);
    if (found == RouteFoundPending) return true;
    if (found == RouteFoundArrived) atomicAdd(legsCounts.routeArrivals, 1u);

    return false;
}

// The line drawn over as many lanes as it takes to see the car's own stopping distance ahead (CAR-11), drawing new lanes
// onto the end of the chain from `from`; spentM is how much of the chain already in hand is behind the car. Laid on a
// tick (fromTheQueue), it grows only off the route's queue; otherwise the route is asked whole.
void TOWN_LAY_LINE(uint car, uint from, float spentM, bool fromTheQueue)
{
    if (!fromTheQueue) town_AsksTheRoute(car) = 0u;

    uint wasLanes = cars_Line(car).laneCount;
    int was[LineAssemblerMostLanes];
    for (uint at = 0u; at < LineAssemblerMostLanes; at++) was[at] = at < wasLanes ? cars_Chain(car, at) : CarFleetNoLane;

    uint lanes = from;
    float reachM = town_SightM(car);
    float seenM = -spentM;
    bool searched = false;
    for (uint index = 0u; index < from; index++)
    {
        precise float laneSeenM = seenM + roads_LaneLengthM(cars_Chain(car, index));
        seenM = laneSeenM;
    }

    // The route is asked at the end of the line in hand whether or not another lane is wanted, and a lane it hands back
    // is taken.
    while (lanes < LineAssemblerMostLanes)
    {
        int next = fromTheQueue
            ? town_NextQueuedLane(car, cars_Chain(car, lanes - 1u))
            : TOWN_NEXT_LANE_ON_ROUTE(car, cars_Chain(car, lanes - 1u), searched);
        if (next < 0) break;

        cars_Chain(car, lanes++) = next;
        precise float laneSeenM = seenM + roads_LaneLengthM(next);
        seenM = laneSeenM;
        if (seenM >= reachM) break;
    }

    // A route is driven forwards: the gear belongs to the line.
    cars_LineIsReverse(car) = 0u;

    // A line whose last lane its bay is worked off stops where the car waits for its manoeuvre (GEN-4f); one whose last
    // lane holds the place the leg is aimed at in the road stops at that place.
    int lastLane = cars_Chain(car, lanes - 1u);
    int bay = town_TheBayTheLineStopsFor(car, lastLane);
    float placeM = bay == CarFleetNoBay ? town_ThePlaceTheLineStopsAtM(car, lastLane) : townMapNaN();
    cars_StopsForBay(car) = bay;
    cars_StopsAtItsPlace(car) = townIsNaN(placeM) ? 0u : 1u;
    float stopM = bay != CarFleetNoBay ? parkingIn_StopForTheBayM(car, bay, lastLane)
        : townIsNaN(placeM) ? townMapInfinity()
        : placeM;
    cars_SetLine(car, lineAssembler_Assemble(car, lanes, stopM));

    // What a plan reads of the line (TER-4c.1, S-2): the corners folded into each arc, and which joins break it.
    cornerLimits_Lay(car, cars_Line(car).arcCount, legsFigures.gripMargin, legsFigures.brakingMargin);
    for (uint slot = 0u; slot + 1u < cars_Line(car).laneCount; slot++)
    {
        int join = roads_ConnectorBetween(cars_Chain(car, slot), cars_Chain(car, slot + 1u));
        cars_JoinBreaks(car, slot) = join != RoadGraphNoConnector && roads_BreaksTheLine(join) != 0u ? 1u : 0u;
    }

    town_KeepTheGrantOnTheLanesKept(car, was, wasLanes);
}

// The lane the car stands on and points along taken as the front of a line, or the join it stands in and the lanes
// either side (GEN-53: never a bay); false where it stands on no lane.
bool TOWN_TAKE_THE_LANE_UNDER_IT(uint car)
{
    vec2 forward = town_ForwardOf(car);
    vec2 rearAxleM = carFollower_RearAxleM(cars_Build(car), cars_PositionM(car), forward);
    TownStandingOn under = town_TheCarriagewayUnder(rearAxleM, forward, cars_LevelOf(car));
    if (under.lane < 0) return false;

    // Where the body stands on the chain it is handed, before the line is laid over it: the route is searched from there.
    cars_ProgressM(car) = under.alongM;
    TOWN_LAY_LINE(car, town_TheChainFrom(car, under), 0.0, false);
    return cars_Line(car).arcCount > 0u;
}

// The lane under the car followed, or — with none to take — the car off its line, at rest until it has one (CAR-9).
bool TOWN_TAKE_THE_ROAD(uint car)
{
    bool taken = TOWN_TAKE_THE_LANE_UNDER_IT(car);
    town_Enter(car, taken ? CarActionFollow : CarActionRejoin);
    return taken;
}

// The line a leg begins on: the first piece of the manoeuvre out of the bay the car stands in, whether or not it was
// registered there (GEN-53), or the lane it stands on and the route from there.
void TOWN_LAY_THE_FIRST_LINE(uint car)
{
    manoeuvres_Clear(car);

    int standingIn = parking_BayOf(car);
    if (standingIn < 0) standingIn = parking_BayHolding(cars_PositionM(car));
    if (standingIn >= 0 && town_LeaveTheBay(car, standingIn))
    {
        town_Enter(car, CarActionUnpark);
        return;
    }

    TOWN_TAKE_THE_ROAD(car);
}
