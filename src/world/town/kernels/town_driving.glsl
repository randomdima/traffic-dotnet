// TownWorld.Driving on the device — the leg's half of it: the line a car holds over the next few lanes of its route and
// what grows it (NextQueuedLane, LayTheRestOfTheLine, OnBesideTheRoute, TurnsBackHere, KeepTheGrantOnTheLanesKept), the
// route asked for at the line's end (TryPlan, RouteGoalsFor, EntriesBeside, AlongTheEntryM, ExpandRoute, LayRouteLanes and
// the road's joins RouteChain.LayInto expands it over), and how a car takes up a line again (Reacquire,
// TakeTheLaneBeside, AdvanceLane). LayLine, NextLaneOnRoute and PlanRoute are town_laying.glsl's. The tick's half —
// TickCar, ReadTheLine, SetTheContext, Drive, Hold — is the car tick's (car_tick.glsl).
//
// A search is a request (route_asks.glsl): TryPlan asks the route service and answers pending until its answer lands on
// a later decision, which takes it as the C# takes its search's. TakeTheCarsPoseBack is the hybrid's handing back of a
// pose; the device holds every pose, so it is no call here. Include town_legs.glsl, which brings this with the rest of
// the leg.

#ifndef TOWN_DRIVING_GLSL
#define TOWN_DRIVING_GLSL

#include "world/town/kernels/town_leg_calls.glsl"
#include "world/town/kernels/town_legs_figures.glsl"
#include "world/town/kernels/town_actions.glsl"
#include "agents/car/body/kernels/car_fleet.glsl"
#include "agents/car/control/kernels/car_follower.glsl"
#include "agents/car/control/kernels/lane_tour.glsl"
#include "agents/car/actions/kernels/driving_ground.glsl"
#include "world/road/kernels/line_assembler.glsl"
#include "world/road/kernels/corner_limits.glsl"
#include "world/routing/kernels/leg_progress.glsl"
#include "world/routing/kernels/link_surcharges.glsl"

// TownWorld.RouteFound, and what a search asked and not yet answered comes to.
const uint RouteFoundRoute = 0u;
const uint RouteFoundArrived = 1u;
const uint RouteFoundNowhere = 2u;
const uint RouteFoundPending = 3u;

// TownWorld.OffLineTolerance: how many times the nominal off-path a body may stand from the lane it reacquires.
const float TownWorldOffLineTolerance = 2.0;

bool townIsNaN(float x) { return (floatBitsToUint(x) & 0x7fffffffu) > 0x7f800000u; }

// TownWorld._driveProgress: every driver's leg clock, as town_rows.glsl's cars_Clock… hold it (LegProgress).
void driveProgress_Restart(uint car) { legProgress_Restart(cars_ClockSinceS(car), cars_ClockClosestM(car)); }

// How far this car has to be able to see: its own stopping distance from its own top speed (CAR-11).
float town_SightM(uint car) { return cars_Build(car).sightM; }

// A request of the driving network with nothing in it yet, barred from the closed runs where this car's search is
// (SRV-10, ClosedLinksFor) and read at the surcharges' own clock.
RouteRequest town_DrivingRequest(bool closed)
{
    RouteRequest ask;
    ask.state = RouteSlotEmpty;
    ask.network = RouteDriving;
    ask.entryCount = 0u;
    ask.goalCount = 0u;
    ask.closedApply = closed ? 1u : 0u;
    ask.nowS = routes_Surcharges(RouteDriving).nowS;
    ask.owner = 0u;
    ask.stamp = 0u;
    for (int at = 0; at < ROUTE_MOST_ENTRIES; at++) ask.entries[at] = RouteEntry(TravelGraphNoLink, 0.0, 0.0, 0.0);
    for (int at = 0; at < ROUTE_MOST_GOALS; at++) ask.goals[at] = RouteGoal(TravelGraphNoLink, 0.0);
    return ask;
}

// Whether two driving asks are one search made from the same links: the same goals, the same links entered at the same
// price. A car moves along its own link between the decision that asks and the one that reads the answer, and the metres
// it has driven since change neither the links it sets off on nor where it is going — the answer is the one its ask got.
bool townAskedFromTheSameLinks(RouteRequest one, RouteRequest other)
{
    if (one.network != other.network || one.entryCount != other.entryCount || one.goalCount != other.goalCount
        || one.closedApply != other.closedApply)
    {
        return false;
    }

    for (uint at = 0u; at < one.entryCount; at++)
    {
        if (one.entries[at].link != other.entries[at].link || one.entries[at].enterM != other.entries[at].enterM) return false;
    }

    for (uint at = 0u; at < one.goalCount; at++)
    {
        if (one.goals[at] != other.goals[at]) return false;
    }

    return true;
}

// The route service asked for this car's search (TownWorld.SearchTheDrivingNetwork): landed where the ask held is this
// one from the same links and searched since, pending where it waits for its search or is asked now.
uint town_AskTheDrivingNetwork(uint car, RouteRequest ask, out uint slot)
{
    uint agent = townCounts.firstCarAgent + car;
    int held = routeAsks_Slot(agent);
    if (held >= 0 && townAskedFromTheSameLinks(routes_Request(uint(held)), ask))
    {
        slot = uint(held);
        return routes_Request(slot).state == RouteSlotDone ? RouteAskLanded : RouteAskPending;
    }

    return routes_Ask(agent, ask, slot);
}

// The lane a line carries on down where its route moves across from fromLane onto next (CAR-53), the queued lanes it
// passes beside given up — or none where it stops, which is where the car waits to move across.
int town_OnBesideTheRoute(uint car, int fromLane, int next)
{
    int passed;
    int onward = roads_OnBesideTheRoute(fromLane, next, cars_PeekRouteLaneAfterNext(car), passed);
    for (int lane = 0; lane < passed; lane++) cars_TakeNextRouteLane(car);

    return onward;
}

// The next lane a tick may lay without deciding anything: the route queue's where it gives one, and otherwise none, the
// car flagged for its next decision to lay the rest.
int town_NextQueuedLane(uint car, int fromLane)
{
    int next = cars_PeekNextRouteLane(car);
    if (next >= 0 && roads_ConnectorBetween(fromLane, next) != RoadGraphNoConnector) return cars_TakeNextRouteLane(car);

    int besideAt;
    if (next >= 0 && roads_ReachesBySwitching(fromLane, next, besideAt)) return town_OnBesideTheRoute(car, fromLane, next);

    town_AsksTheRoute(car) = 1u;
    return CarFleetNoLane;
}

// The rest of a line a tick could not lay, laid on the car's decision — only of a car driving the route's own line.
void town_LayTheRestOfTheLine(uint car)
{
    if (town_AsksTheRoute(car) == 0u) return;

    town_AsksTheRoute(car) = 0u;
    if (cars_Line(car).laneCount == 0u || town_IsManoeuvring(car)) return;

    uint action = cars_Action(car);
    if (!(action == CarActionFollow || action == CarActionOvertake || action == CarActionSwitch || action == CarActionBackUp
          || action == CarActionPark))
    {
        return;
    }

    town_LayLine(car, cars_Line(car).laneCount, cars_ProgressM(car), false);
}

// Whether the line being laid stops on this lane — for the leg's bay, or at its place in the road.
bool town_TheLegEndsOn(uint car, int lane)
{
    return town_TheBayTheLineStopsFor(car, lane) != CarFleetNoBay || !townIsNaN(town_ThePlaceTheLineStopsAtM(car, lane));
}

// Whether the leg comes back the other way from the end of this lane (GEN-4l) and has a bay to do it in — or a stretch
// with no way out of it at all, where the queue ends whether or not one was found.
bool town_TurnsBackHere(uint car, int fromLane)
{
    return cars_TurnsBackOn(car) >= 0 && roads_LaneReverse(fromLane) == cars_TurnsBackOn(car)
        && (town_TakeABayToTurnIn(car, fromLane, cars_TurnsBackOn(car)) || roads_LanesFromCount(fromLane) == 0);
}

// Where this leg is aimed, as the places on the network a search may finish at: the lane a police car closing it has to
// arrive on (SRV-9), the mouth of the leg's bay abeam on each lane it is worked off, or both directions of the stretch a
// place in the road stands on (AMB-5, EVA-3).
int town_RouteGoalsFor(uint car, out RouteGoal into[ROUTE_MOST_GOALS])
{
    for (int at = 0; at < ROUTE_MOST_GOALS; at++) into[at] = RouteGoal(TravelGraphNoLink, 0.0);

    int entrance;
    float alongM;
    if (town_TheEntranceItIsSentTo(car, entrance, alongM))
    {
        RouteGoal goal;
        int count = driving_GoalOnLane(entrance, alongM, goal);
        into[0] = goal;
        return count;
    }

    if (!town_IsAimedAtAPlaceInTheRoad(car)) return town_BayGoals(town_BayAimedAt(car), into);

    return driving_GoalsAt(cars_DestinationM(car), into);
}

// How far into the lane the search sets off from the body has got: where it stands on the lane under it, the lane's
// start for the one the join it stands in leads onto, and the far end of any lane further down the line.
float town_AlongTheEntryM(uint car, int fromLane)
{
    if (cars_Chain(car, 0u) == fromLane) return clamp(cars_ProgressM(car), 0.0, roads_LaneLengthM(fromLane));

    return cars_Chain(car, 1u) == fromLane && cars_ProgressM(car) >= roads_LaneLengthM(cars_Chain(car, 0u))
        ? 0.0
        : roads_LaneLengthM(fromLane);
}

// The lanes beside the one a search sets off from, as entries of their own each where the car stands abeam of it, at
// what moving across onto it costs (CAR-53), into ask's entries from `at` on; how many.
int town_EntriesBeside(int fromLane, float alongM, inout RouteRequest ask, int at)
{
    int count = 0;
    int room = ROUTE_MOST_ENTRIES - at;
    for (int side = 0; side < 2; side++)
    {
        bool inward = side == 0;
        for (int beside = roads_LaneBeside(fromLane, inward); beside != RoadGraphNoLane && count < room; beside = roads_LaneBeside(beside, inward))
        {
            precise float scaledM = alongM * roads_LaneLengthM(beside);
            float besideAlongM = roundedQuotient(scaledM, roads_LaneLengthM(fromLane));
            if (driving_LinkOfLane(beside) == TravelGraphNoLink) continue;

            RouteEntry entry = driving_EntryOnLane(beside, besideAlongM);
            entry.enterM = driving_SwitchM(fromLane, beside, legsFigures.nominalCarLengthM, legsFigures.laneSwitchPriceCarLengths);
            ask.entries[at + count++] = entry;
        }
    }

    return count;
}

// The road's joins as a route is expanded over them (TownWorld.RoadJoins): two lanes travelled one after the other where
// a connector runs between them or the car moves across (CAR-53), and the two sides of a car park's frontage — the lane
// the leg turns back on (GEN-4l) — where neither does.
struct TownRoadJoins
{
    int turnsBackOn;
};

int townRoadJoins_JoinedAt(inout TownRoadJoins joins, int link, int from)
{
    int first = drivingRuns_PieceOffsets(link);
    for (int slot = 0; slot < drivingRuns_PieceOffsets(link + 1) - first; slot++)
    {
        if (roads_ConnectorBetween(from, drivingRuns_Piece(first + slot)) != RoadGraphNoConnector) return slot;
    }

    return 0;
}

bool townRoadJoins_Reaches(inout TownRoadJoins joins, int from, int onto)
{
    if (roads_ConnectorBetween(from, onto) != RoadGraphNoConnector) return true;

    int besideAt;
    if (roads_ReachesBySwitching(from, onto, besideAt)) return true;

    joins.turnsBackOn = roads_LaneReverse(from) == onto ? onto : CarFleetNoLane;
    return false;
}

int townRoadChainPieceAt(int link, float alongM)
{
    float alongPieceM;
    return drivingRuns_PieceAt(link, alongM, alongPieceM);
}

// RouteChain.LayInto over the driving network's runs with the road's joins: a request slot's links into a car's queue.
#define ROUTE_CHAIN_LAY_INTO town_LayIntoTheRoute
#define ROUTE_CHAIN_JOINS TownRoadJoins
#define ROUTE_CHAIN_JOINED_AT(joins, link, from) townRoadJoins_JoinedAt(joins, link, from)
#define ROUTE_CHAIN_REACHES(joins, from, onto) townRoadJoins_Reaches(joins, from, onto)
#define ROUTE_CHAIN_BETWEEN(joins, from, onto) RouteChainNoWay
#define ROUTE_CHAIN_PIECES_FROM(link) drivingRuns_PieceOffsets(link)
#define ROUTE_CHAIN_PIECES_TO(link) drivingRuns_PieceOffsets((link) + 1)
#define ROUTE_CHAIN_PIECE(at) drivingRuns_Piece(at)
#define ROUTE_CHAIN_PIECE_AT(link, alongM) townRoadChainPieceAt(link, alongM)
#define ROUTE_CHAIN_LINK(links, at) routes_ResultLink(links, uint(at))
#define ROUTE_CHAIN_INTO(into, at) cars_RouteLane(into, uint(at))
#define ROUTE_CHAIN_ROOM CarFleetRouteLanesPerCar
#include "world/routing/kernels/route_chain.glsl"

// The lane beside `lane` that is a piece of `link`, or none.
int town_TheLaneBesideOn(int lane, int link)
{
    for (int side = 0; side < 2; side++)
    {
        bool inward = side == 0;
        for (int beside = roads_LaneBeside(lane, inward); beside != RoadGraphNoLane; beside = roads_LaneBeside(beside, inward))
        {
            if (driving_LinkOfLane(beside) == link) return beside;
        }
    }

    return RoadGraphNoLane;
}

// The run-links of a landed search (its request slot) as the lanes a line is laid over, into the car's own queue: the
// lanes of the first link past the one the car is on — or one beside it it moves across from — then whole links, then
// the last one's up to where the destination stands.
int town_LayRouteLanes(uint car, int fromLane, uint slot, int linkCount, RouteGoal goal, out int turnsBackOn, out bool ranOut)
{
    int firstSlot = -1;
    if (linkCount > 0)
    {
        int firstLink = routes_ResultLink(slot, 0u);
        if (driving_LinkOfLane(fromLane) == firstLink)
        {
            firstSlot = driving_SlotOfLane(fromLane) + 1;
        }
        else
        {
            int beside = town_TheLaneBesideOn(fromLane, firstLink);
            if (beside != RoadGraphNoLane) firstSlot = driving_SlotOfLane(beside);
        }
    }

    TownRoadJoins joins = TownRoadJoins(CarFleetNoLane);
    int written = town_LayIntoTheRoute(joins, slot, car, linkCount, fromLane, firstSlot, goal, ranOut);
    turnsBackOn = joins.turnsBackOn;
    return written;
}

// A landed search's lanes laid into this car's queue, and where that queue ends at the place it was searched for — the
// place a leg aimed at one in the road stops (ThePlaceTheLineStopsAtM). A search that handed over only the section in
// front of the car (stopsShort) is a queue that runs out, unless it stops first at a frontage it turns back on.
void town_ExpandRoute(uint car, int fromLane, uint slot, int linkCount, RouteGoal goal, bool stopsShort)
{
    int turnsBackOn;
    bool ranOut;
    int count = town_LayRouteLanes(car, fromLane, slot, linkCount, goal, turnsBackOn, ranOut);
    ranOut = ranOut || (stopsShort && turnsBackOn == CarFleetNoLane);
    cars_RouteCount(car) = count;
    cars_RouteTaken(car) = 0;
    cars_TurnsBackOn(car) = turnsBackOn;
    cars_RouteRunsOut(car) = ranOut ? 1u : 0u;
    cars_RouteChanged(car);

    float goalAlongM;
    int goalLane = drivingRuns_Piece(drivingRuns_PieceOffsets(goal.link) + drivingRuns_PieceAt(goal.link, goal.alongM, goalAlongM));
    int endsOn = count > 0 ? cars_RouteLane(car, uint(count - 1)) : fromLane;
    cars_RouteEndsOn(car) = !ranOut && turnsBackOn == CarFleetNoLane && endsOn == goalLane ? goalLane : CarFleetNoLane;
    cars_RouteEndsAtM(car) = goalAlongM;
}

// A route from the far end of fromLane to where the car is going, asked of the route service: Route or Arrived once its
// answer has landed and been laid into the queue, Nowhere where there is nothing to search for or the search found
// nothing, Pending until the search has answered.
uint town_TryPlan(uint car, int fromLane)
{
    RouteRequest ask = town_DrivingRequest(town_ClosedLinksFor(car));
    RouteGoal goals[ROUTE_MOST_GOALS];
    int goalCount = town_RouteGoalsFor(car, goals);
    if (goalCount == 0) return RouteFoundNowhere;

    float alongM = town_AlongTheEntryM(car, fromLane);
    if (driving_LinkOfLane(fromLane) == TravelGraphNoLink) return RouteFoundNowhere;

    ask.entries[0] = driving_EntryOnLane(fromLane, alongM);
    int entryCount = 1 + town_EntriesBeside(fromLane, alongM, ask, 1);
    ask.entryCount = uint(entryCount);
    ask.goalCount = uint(goalCount);
    for (int at = 0; at < goalCount; at++) ask.goals[at] = goals[at];

    // A place on a lane is arrived at and not got near, so a goal the car has driven past is searched for.
    uint slot;
    if (town_AskTheDrivingNetwork(car, ask, slot) != RouteAskLanded) return RouteFoundPending;

    atomicAdd(legsCounts.routeSearches, 1u);
    RouteResult answer = routes_Result(slot);
    uint agent = townCounts.firstCarAgent + car;
    if (answer.count == 0u || answer.goalSlot < 0)
    {
        routes_Release(agent);
        return RouteFoundNowhere;
    }

    town_ExpandRoute(car, fromLane, slot, int(answer.count), routes_Request(slot).goals[answer.goalSlot], answer.stopsShort != 0u);
    routes_Release(agent);

    // A route with nothing left in it is an arrival; one that stops at a frontage to turn (GEN-4l) has a turn ahead.
    return cars_RouteCount(car) > 0 || cars_TurnsBackOn(car) >= 0 ? RouteFoundRoute : RouteFoundArrived;
}

// A grant holds only down the lanes it was read down (TER-4c.8): where a line is laid again between two layings of the
// claims, it ends where the first lane laid differently begins — measured from the nose, walked back out by the ground
// covered since the claim.
void town_KeepTheGrantOnTheLanesKept(uint car, int was[LineAssemblerMostLanes], uint wasLanes)
{
    uint lanes = cars_Line(car).laneCount;
    uint slot = 0u;
    while (slot < lanes && slot < wasLanes && cars_Chain(car, slot) == was[slot]) slot++;
    if (slot >= wasLanes) return;

    float keptToM = slot < lanes ? cars_LaneStartM(car, slot) : cars_Line(car).lengthM;
    precise float noseM = cars_ProgressM(car) + drivingGround_LeadingEdgeAheadOfTheAxleM(car);
    precise float grantM = keptToM - noseM + cars_CoveredSinceClaimM(car);
    cars_AuthorityM(car) = min(cars_AuthorityM(car), grantM);
}

// A stopped car that has lost its line takes the lane it stands on, the way it points (CAR-9, PER-25) — never the same
// lanes again, which would be the route behind it searched for nothing. Whether a line was laid.
bool town_Reacquire(uint car, vec2 rearAxleM)
{
    vec2 forward = town_ForwardOf(car);
    TownStandingOn under = town_TheCarriagewayUnder(rearAxleM, forward, cars_LevelOf(car));
    if (under.lane < 0) return false;

    precise vec2 offM = under.positionM - rearAxleM;
    precise float toleranceM = legsFigures.carOffPathM * TownWorldOffLineTolerance;
    if (roundedLength(offM) > toleranceM) return false;

    vec2 direction = trigUnit(under.headingRad);
    precise float along = direction.x * forward.x + direction.y * forward.y;
    if (along <= 0.0) return false;

    cars_ProgressM(car) = under.alongM;
    uint lanes = cars_Line(car).laneCount;
    if (lanes > 0u && cars_Chain(car, 0u) == under.lane
        && (under.onward == CarFleetNoLane || (lanes > 1u && cars_Chain(car, 1u) == under.onward)))
    {
        return false;
    }

    town_LayLine(car, town_TheChainFrom(car, under), 0.0, false);
    atomicAdd(legsCounts.linesReacquired, 1u);
    return true;
}

// ICarTown.TakeTheLaneBeside: the car moved across onto `lane`, the old chain's lanes past the one it left back on the
// route so the new line runs on beside them, and its grant the ground held ahead of it.
void town_TakeTheLaneBeside(uint car, int lane, float heldAheadM)
{
    CarBuildRow build = cars_Build(car);
    cars_PutBackOnRoute(car, 1u, cars_Line(car).laneCount);

    vec2 rearAxleM = carFollower_RearAxleM(build, cars_PositionM(car), town_ForwardOf(car));
    precise float scaledM = cars_ProgressM(car) * roads_LaneLengthM(lane);
    float abeamM = roundedQuotient(scaledM, roads_LaneLengthM(cars_Chain(car, 0u)));
    precise float windowM = build.lengthM * legsFigures.projectionWindowInCarLengths;
    townArcsOfLanes();
    float offSq;
    float alongM = splineProject(townLaneArcsFrom(lane), townLaneArcsTo(lane), rearAxleM, abeamM, windowM, offSq);

    cars_Chain(car, 0u) = lane;
    cars_ProgressM(car) = alongM;
    driveProgress_Restart(car);
    town_LayLine(car, 1u, 0.0, true);
    cars_ProgressM(car) = carFollower_ProgressM(car, build, rearAxleM, alongM);
    precise float authorityM = heldAheadM + cars_CoveredSinceClaimM(car);
    cars_AuthorityM(car) = authorityM;
}

// The car has passed into the second lane of its chain and its line has been shifted on past the first: taking the next
// lane is progress (PER-25), and a pass and a step across follow the line into it (CAR-46, CAR-53).
void town_AdvanceLane(uint car, float shiftM)
{
    driveProgress_Restart(car);
    overtaking_ShiftTheLine(car, cars_Chain(car, 0u), shiftM);
    switching_ShiftTheLine(car, shiftM);
}

#endif
