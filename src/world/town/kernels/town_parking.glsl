// TownWorld.Parking on the device — the town's side of a drive leg: where it begins (SetOff, LeaveTheBay, PlanTheWayOut),
// where on the carriageway a body stands as a line is laid from it (TheCarriagewayUnder, TheChainFrom), which bay it is
// aimed at and what it does about one it cannot reach (RetargetTheBay, TakeABayToTurnIn, FreeBayNear), where the line in
// hand stops for its bay or its place (TheBayTheLineStopsFor, ThePlaceTheLineStopsAtM), and what stands a car down at the
// end of it. LayTheFirstLine, TakeTheRoad and TakeTheLaneUnderIt are town_laying.glsl's. With them the two the leg begins
// and aims by that the C# keeps elsewhere: SendTo (TownWorld.Ambulance), and BayGoals and RouteExistsToTheBay
// (TownWorld.Trips), whose search is a request (route_asks.glsl) that answers pending until it lands. Include
// town_legs.glsl, which brings this with the rest of the leg.

#ifndef TOWN_PARKING_GLSL
#define TOWN_PARKING_GLSL

#include "world/town/kernels/town_driving.glsl"

// TownWorld.BaysConsideredPerLeg and BaysRoutedPerLeg: how many bays a search for one considers, nearest first, and how
// many of those are worth a route search each — bounds on the work.
const uint TownWorldBaysConsideredPerLeg = 4u;
const uint TownWorldBaysRoutedPerLeg = 2u;

// LineTolerance.RoundingM: a body this near a lane's end stands at the end.
const float LineToleranceRoundingM = 0.001;

#define PARKING_NEAR_ROOM TownWorldBaysConsideredPerLeg
#include "world/parking/kernels/parking_registry.glsl"

// The direction the body is pointing, which every line is read back through.
vec2 town_ForwardOf(uint car) { return trigUnit(cars_HeadingRad(car)); }

// The bay this leg is on its way to, or none — a claim in the register.
int town_BayAimedAt(uint car) { return parking_ClaimedBayOf(car); }

// The bay the line in hand finishes at: the one this leg is turning in while it has one (GEN-4l), the place it is going
// to otherwise.
int town_BayTheLineEndsIn(uint car)
{
    int turn = parking_TurnOf(car);
    return turn >= 0 ? turn : town_BayAimedAt(car);
}

bool town_TakeTheBay(uint car, int bay) { return parking_Claim(car, bay); }

void town_GiveUpTheBay(uint car) { parking_Release(car); }

// The turn given up: the bay back to the town, and the leg no longer coming back the other way.
void town_GiveUpTheTurn(uint car)
{
    parking_LeaveTheTurn(car);
    cars_TurnsBackOn(car) = CarFleetNoLane;
}

// Whether a metre of a lane is still in front of this car's rear axle — asked of the body and only of the lane it is on,
// every lane further down the chain being ahead of it by construction.
bool town_StandsShortOf(uint car, int lane, float atM)
{
    if (lane != cars_Chain(car, 0u)) return true;

    vec2 rearAxleM = carFollower_RearAxleM(cars_Build(car), cars_PositionM(car), town_ForwardOf(car));
    townArcsOfLanes();
    float offSq;
    return atM > splineProject(townLaneArcsFrom(lane), townLaneArcsTo(lane), rearAxleM, atM, roads_LaneLengthM(lane), offSq);
}

// Where on its last lane the line stops for the place this leg is aimed at in the road: the metre the route ends at,
// where it has run out on this lane and the metre is still in front of the car — or NaN.
float town_ThePlaceTheLineStopsAtM(uint car, int lastLane)
{
    if (town_BayTheLineEndsIn(car) >= 0 || cars_RouteEndsOn(car) != lastLane || cars_RouteTaken(car) < cars_RouteCount(car))
    {
        return townMapNaN();
    }

    float atM = cars_RouteEndsAtM(car);
    return town_StandsShortOf(car, lastLane, atM) ? atM : townMapNaN();
}

// The bay this line stops for, where the line being laid reaches it: worked off this lane, the route run out on it, and
// where the car waits for it still in front of the car.
int town_TheBayTheLineStopsFor(uint car, int lastLane)
{
    int bay = town_BayTheLineEndsIn(car);
    if (bay < 0 || cars_RouteTaken(car) < cars_RouteCount(car)) return CarFleetNoBay;
    if (townIsNaN(bayStreets_AtLaneM(bay, lastLane))) return CarFleetNoBay;

    return town_StandsShortOf(car, lastLane, parkingIn_StopForTheBayM(car, bay, lastLane)) ? bay : CarFleetNoBay;
}

// Whether the line in hand ends where the leg does — for its bay, or at its place in the road.
bool town_IsOnTheFinalApproach(uint car) { return cars_StopsForBayOf(car) != CarFleetNoBay || cars_StopsAtItsPlaceOf(car); }

// The car has driven its line out at the place its leg is aimed at in the road, and stands there.
bool town_StandsAtItsPlace(uint car) { return cars_StopsAtItsPlaceOf(car) && town_TheLineIsSpent(car); }

// A car park with nothing free to turn in priced up — by a car actually at the frontage.
void town_MarkTheFrontageFull(uint car, int fromLane)
{
    if (cars_LaneOf(car) != fromLane) return;

    int link = driving_LinkOfLane(fromLane);
    if (link != TravelGraphNoLink) routes_Mark(RouteDriving, link, legsFigures.carBlockedWayPriceM, legsFigures.carBlockedWayLifeS);
}

// A bay of this frontage to turn in (GEN-4l): free, worked off the lane running back as well, near the lane's end and
// still in front of the car — the one furthest along. None found is a frontage priced up, and the car drives on.
bool town_TakeABayToTurnIn(uint car, int fromLane, int backLane)
{
    if (parking_TurnOf(car) >= 0) return true;

    int best = BayStreetsNoBay;
    precise float withinM = roads_LaneLengthM(fromLane) - legsFigures.turnAtALotWithinM;
    float bestM = withinM;
    for (int at = bayStreets_FirstBayOfLane(fromLane); at < bayStreets_FirstBayOfLane(fromLane + 1); at++)
    {
        int bay = bayStreets_BayOffLane(at);
        float atM = bayStreets_AtLaneM(bay, fromLane);
        if (atM < bestM || !parking_IsFreeFor(int(car), bay) || townIsNaN(bayStreets_AtLaneM(bay, backLane))) continue;
        if (!town_StandsShortOf(car, fromLane, parkingIn_StopForTheBayM(car, bay, fromLane))) continue;

        best = bay;
        bestM = atM;
    }

    if (best != BayStreetsNoBay && parking_TakeTheTurn(car, best))
    {
        atomicAdd(legsCounts.turnsAtALotBegun, 1u);
        return true;
    }

    town_MarkTheFrontageFull(car, fromLane);
    return false;
}

// TownWorld.Trips' BayGoals: where a leg into a bay ends — the metre of each street lane its mouth stands abeam of
// (GEN-4f), into goals of room ROUTE_MOST_GOALS.
int town_BayGoals(int bay, out RouteGoal into[ROUTE_MOST_GOALS])
{
    for (int at = 0; at < ROUTE_MOST_GOALS; at++) into[at] = RouteGoal(TravelGraphNoLink, 0.0);
    if (bay < 0) return 0;

    int written = 0;
    for (uint slot = 0u; slot < BayStreetsMostLanes; slot++)
    {
        int lane = bayStreets_SlotLane(bay, slot);
        if (lane == RoadGraphNoLane) break;
        if (written == ROUTE_MOST_GOALS) break;

        int link = driving_LinkOfLane(lane);
        if (link == TravelGraphNoLink) continue;

        into[written++] = RouteGoal(link, driving_PlaceOfM(lane, bayStreets_AtLaneM(bay, lane)));
    }

    return written;
}

// What TownWorld.Trips' RouteExistsToTheBay comes to: no, yes, a search it needs asked (its request built), or that
// search asked and not yet answered.
const uint TownExistsNo = 0u;
const uint TownExistsYes = 1u;
const uint TownExistsAsk = 2u;
const uint TownExistsPending = 3u;

// RouteExistsToTheBay up to its search: no goals or no entry is no route, a goal on the link the car is on is one, and
// anything else is a search, from the far end of the lane, barred from the closed runs now (ClosedLinksNow).
uint town_RouteToTheBay(uint car, int fromLane, int bay, out RouteRequest ask)
{
    ask = town_DrivingRequest(town_ClosedLinksNow());
    RouteGoal goals[ROUTE_MOST_GOALS];
    int goalCount = town_BayGoals(bay, goals);
    if (goalCount == 0) return TownExistsNo;
    if (driving_LinkOfLane(fromLane) == TravelGraphNoLink) return TownExistsNo;

    RouteEntry entry = driving_EntryOnLane(fromLane, roads_LaneLengthM(fromLane));
    for (int slot = 0; slot < goalCount; slot++)
    {
        if (goals[slot].link == entry.link) return TownExistsYes;
    }

    ask.entries[0] = entry;
    ask.entryCount = 1u;
    ask.goalCount = uint(goalCount);
    for (int at = 0; at < goalCount; at++) ask.goals[at] = goals[at];
    return TownExistsAsk;
}

// RouteExistsToTheBay's search asked. An agent holds one ask, and a retarget asks for each bay it considers in turn: a
// held ask for a later bay of the same walk (`later`) is one this walk made on an earlier decision, once this bay had
// been answered no — so it is no again, rather than asked over the later one's answer.
uint town_SearchForTheBay(uint car, RouteRequest ask, bool hasLater, RouteRequest later)
{
    uint agent = townCounts.firstCarAgent + car;
    int held = routeAsks_Slot(agent);
    if (held >= 0 && !townAskedFromTheSameLinks(routes_Request(uint(held)), ask) && hasLater
        && townAskedFromTheSameLinks(routes_Request(uint(held)), later))
    {
        return TownExistsNo;
    }

    uint slot;
    if (town_AskTheDrivingNetwork(car, ask, slot) != RouteAskLanded) return TownExistsPending;

    atomicAdd(legsCounts.routeSearches, 1u);
    RouteResult answer = routes_Result(slot);
    routes_Release(agent);
    return answer.count > 0u && answer.goalSlot >= 0 ? TownExistsYes : TownExistsNo;
}

// From the car's side: a bay it cannot reach or drive into given up for the nearest one that can, near where it has got
// to — released before taking, and the same handful of searches a leg is drawn with. Pending where a search it needs has
// not yet answered, the car to be decided again.
uint town_RetargetTheBay(uint car, vec2 nearM, int avoidBay)
{
    town_GiveUpTheBay(car);

    // The place changed, so a turn the old route asked for is a turn nothing is asking for.
    town_GiveUpTheTurn(car);

    int candidates[TownWorldBaysConsideredPerLeg];
    int bays = parking_BaysNear(nearM, legsFigures.personWalkWorthM, candidates);
    int fromLane = cars_LaneOf(car);

    // The bays this walk searches for, in the order it does: the first BaysRoutedPerLeg it does not avoid.
    int routed[TownWorldBaysRoutedPerLeg];
    uint routedCount = 0u;
    for (int slot = 0; slot < bays && routedCount < TownWorldBaysRoutedPerLeg; slot++)
    {
        if (candidates[slot] != avoidBay) routed[routedCount++] = candidates[slot];
    }

    uint searched = 0u;
    for (int slot = 0; slot < bays && searched < TownWorldBaysRoutedPerLeg; slot++)
    {
        int bay = candidates[slot];
        if (bay == avoidBay) continue;

        if (fromLane >= 0)
        {
            searched++;
            RouteRequest ask;
            uint exists = town_RouteToTheBay(car, fromLane, bay, ask);
            if (exists == TownExistsAsk)
            {
                RouteRequest later;
                bool hasLater = searched < routedCount && town_RouteToTheBay(car, fromLane, routed[searched], later) == TownExistsAsk;
                exists = town_SearchForTheBay(car, ask, hasLater, later);
            }

            if (exists == TownExistsPending) return TownRetargetPending;
            if (exists == TownExistsNo) continue;
        }

        if (!town_TakeTheBay(car, bay)) continue;

        cars_HasDestination(car) = 1u;
        cars_DestinationM(car) = parking_CentreM(bay);
        cars_ClearRoute(car);

        // The line laid again from where the body is: the way out of a bay where it is still in one, the lane under it
        // otherwise — which plans to the new place.
        town_LayTheFirstLineRetargeted(car);
        return TownRetargetTaken;
    }

    return TownRetargetNone;
}

// The route out of a bay, laid before the car moves (GEN-4f): one search from every lane the bay is worked off — or from
// onlyLane — each entered where its mouth stands abeam. The lane that route sets off down, its route queued — or
// onlyLane, where there is nowhere to go, no way there, or no answer yet. A car turning in this bay (GEN-4l) is searched
// from the lane it turned for, the bay still its own while it plans.
int town_PlanTheWayOut(uint car, int bay, int onlyLane)
{
    int turnsOnto = parking_TurnOf(car) == bay ? cars_TurnsBackOn(car) : CarFleetNoLane;
    if (onlyLane == CarFleetNoLane) onlyLane = turnsOnto;

    cars_ClearRoute(car);
    if (cars_HasDestination(car) == 0u) return onlyLane;

    RouteRequest ask = town_DrivingRequest(town_ClosedLinksFor(car));
    RouteGoal goals[ROUTE_MOST_GOALS];
    int goalCount = town_RouteGoalsFor(car, goals);
    if (goalCount == 0) return onlyLane;

    int entries = 0;
    for (uint slot = 0u; slot < BayStreetsMostLanes; slot++)
    {
        int lane = bayStreets_SlotLane(bay, slot);
        if (lane == RoadGraphNoLane) break;
        if (onlyLane != CarFleetNoLane && lane != onlyLane) continue;
        if (driving_LinkOfLane(lane) == TravelGraphNoLink) continue;

        ask.entries[entries++] = driving_EntryOnLane(lane, bayStreets_AtLaneM(bay, lane));
    }

    if (entries == 0) return onlyLane;

    ask.entryCount = uint(entries);
    ask.goalCount = uint(goalCount);
    for (int at = 0; at < goalCount; at++) ask.goals[at] = goals[at];

    uint answered;
    if (town_AskTheDrivingNetwork(car, ask, answered) != RouteAskLanded) return onlyLane;

    atomicAdd(legsCounts.routeSearches, 1u);
    RouteResult answer = routes_Result(answered);
    uint agent = townCounts.firstCarAgent + car;
    if (answer.count == 0u || answer.goalSlot < 0)
    {
        routes_Release(agent);
        return onlyLane;
    }

    int firstLink = routes_ResultLink(answered, 0u);
    for (uint slot = 0u; slot < BayStreetsMostLanes; slot++)
    {
        int lane = bayStreets_SlotLane(bay, slot);
        if (lane == RoadGraphNoLane) break;
        if (driving_LinkOfLane(lane) != firstLink) continue;

        town_ExpandRoute(car, lane, answered, int(answer.count), routes_Request(answered).goals[answer.goalSlot], answer.stopsShort != 0u);
        routes_Release(agent);
        return lane;
    }

    routes_Release(agent);
    return onlyLane;
}

// Out of the bay the car stands in, route first (GEN-4f): the manoeuvre shaped onto the lane the route sets off down, and
// the route laid again from the other lane where that is the only one the car can be got out onto. False where it can
// be got out onto none.
bool town_LeaveTheBay(uint car, int bay)
{
    int lane = town_PlanTheWayOut(car, bay, CarFleetNoLane);
    if (!pullingOut_ShapeTheWayOut(car, bay, lane))
    {
        cars_ClearRoute(car);
        return false;
    }

    // No route from either lane is no route from one of them, and is not searched for again.
    if (lane != CarFleetNoLane && manoeuvres_Lane(car) != lane) town_PlanTheWayOut(car, bay, manoeuvres_Lane(car));

    return true;
}

// A drive leg begun for whoever has got in, aimed at the bay that leg claimed (PER-29): the one thing that takes a car
// back off the player without the reset (CTL-4).
void town_SetOff(uint car)
{
    carOrders_Release(car);

    cars_Driven(car) = 1u;
    cars_ClearRoute(car);
    town_GiveUpTheTurn(car);
    driveProgress_Restart(car);

    int bay = town_BayAimedAt(car);
    cars_HasDestination(car) = bay >= 0 ? 1u : 0u;
    cars_DestinationM(car) = bay >= 0 ? parking_CentreM(bay) : cars_PositionM(car);

    town_LayTheFirstLine(car);
}

// TownWorld.Ambulance's SendTo: a leg sent to a place, aimed at a bay or at none (AMB-5, EVA-3, SRV-6, CTL-8a).
void town_SendTo(uint car, vec2 toM, int bay)
{
    town_GiveUpTheBay(car);
    if (bay >= 0) town_TakeTheBay(car, bay);

    cars_Driven(car) = 1u;
    cars_ClearRoute(car);
    town_GiveUpTheTurn(car);
    driveProgress_Restart(car);
    cars_HasDestination(car) = 1u;
    cars_DestinationM(car) = toM;

    town_LayTheFirstLine(car);
}

// The chain's first lanes as a body standing on the carriageway has them, and how many; a chain begun afresh carries no
// grant.
uint town_TheChainFrom(uint car, TownStandingOn under)
{
    drivingGround_ForgetTheGrant(car);
    cars_Chain(car, 0u) = under.lane;
    if (under.onward == CarFleetNoLane) return 1u;

    cars_Chain(car, 1u) = under.onward;
    return 2u;
}

// The join between two lanes, taken over `best` where the body stands nearer its line than that and it runs the way the
// body points.
void town_OnTheJoin(int from, int onto, vec2 rearAxleM, vec2 forward, inout TownStandingOn best)
{
    int join = roads_ConnectorBetween(from, onto);
    if (join == RoadGraphNoConnector) return;

    uint first = townConnectorArcsFrom(join);
    uint end = townConnectorArcsTo(join);
    if (first == end) return;

    townArcsOfConnectors();
    float offSq;
    float onM = splineProject(first, end, rearAxleM, 0.0, roads_ConnectorLengthM(join), offSq);
    precise vec2 bestOff = best.positionM - rearAxleM;
    precise float bestSq = bestOff.x * bestOff.x + bestOff.y * bestOff.y;
    if (offSq >= bestSq) return;

    vec2 positionM;
    float headingRad;
    float curvature;
    splineSampleBent(first, end, onM, positionM, headingRad, curvature);
    vec2 direction = trigUnit(headingRad);
    precise float along = direction.x * forward.x + direction.y * forward.y;
    if (along <= 0.0) return;

    precise float alongM = roads_LaneLengthM(from) + onM;
    best = TownStandingOn(from, onto, alongM, positionM, headingRad, curvature);
}

// Where on the carriageway a body stands, as a line is laid from: the lane it is on and pointing along — direction
// decides, not distance — or, standing in a junction's box, the lane it came off and the one its join leads onto. Its
// level (PHY-1a) is the one whose lanes it may take.
TownStandingOn town_TheCarriagewayUnder(vec2 rearAxleM, vec2 forward, uint level)
{
    float alongM;
    int lane = roads_NearestStreetLaneOn(rearAxleM, level, alongM);
    if (lane < 0) return TownStandingOn(CarFleetNoLane, CarFleetNoLane, 0.0, vec2(0.0), 0.0, 0.0);

    vec2 positionM;
    float headingRad;
    float curvature;
    townArcsOfLanes();
    splineSampleBent(townLaneArcsFrom(lane), townLaneArcsTo(lane), alongM, positionM, headingRad, curvature);
    vec2 direction = trigUnit(headingRad);
    precise float along = direction.x * forward.x + direction.y * forward.y;
    if (along <= 0.0 && roads_LaneReverse(lane) >= 0)
    {
        lane = roads_LaneReverse(lane);
        precise float middleM = roads_LaneLengthM(lane) * 0.5;
        float offSq;
        alongM = splineProject(townLaneArcsFrom(lane), townLaneArcsTo(lane), rearAxleM, middleM, roads_LaneLengthM(lane), offSq);
    }

    splineSampleBent(townLaneArcsFrom(lane), townLaneArcsTo(lane), alongM, positionM, headingRad, curvature);
    TownStandingOn onLane = TownStandingOn(lane, CarFleetNoLane, alongM, positionM, headingRad, curvature);
    precise float endM = roads_LaneLengthM(lane) - LineToleranceRoundingM;
    if (alongM >= endM)
    {
        for (int at = 0; at < roads_LanesFromCount(lane); at++) town_OnTheJoin(lane, roads_LaneFrom(lane, at), rearAxleM, forward, onLane);
    }
    else if (alongM <= LineToleranceRoundingM)
    {
        int junction = roads_LaneFromJunction(lane);
        for (int at = roads_JunctionInOffsets(junction); at < roads_JunctionInOffsets(junction + 1); at++)
        {
            town_OnTheJoin(roads_JunctionInLane(at), lane, rearAxleM, forward, onLane);
        }
    }

    return onLane;
}

// What a car is told of a world that holds nothing for it (DriveContext.Clear) — written a field at a time, since a
// constructor of this many terms not all constant crashes glslang past a certain size of kernel.
DriveContextRow town_ClearContext()
{
    DriveContextRow clear;
    clear.authorityM = townMapInfinity();
    clear.grantCutBy = HeadwayKindNothing;
    clear.placeStopM = townMapInfinity();
    clear.marginM = 0.0;
    clear.waitsToPass = 0u;
    clear.horizonM = townMapInfinity();
    clear.passAsideM = 0.0;
    clear.backUpM = 0.0;
    clear.blocked = 0u;
    clear.waitAtM = townMapInfinity();
    clear.pad0 = 0.0;
    clear.pad1 = 0.0;
    return clear;
}

// A car with nothing left to do: no line, no route, no turn, no manoeuvre, no destination and no movement, handbrake on.
// Nobody is let out here (SRV-11, PER-29).
void town_StandTheCarDown(uint car)
{
    cars_Reroutes(car) = 0u;
    driveProgress_Restart(car);
    cars_SetLine(car, CarLineRow(0u, 0u, 0.0, 0u));
    cars_ClearRoute(car);
    town_GiveUpTheTurn(car);
    town_Enter(car, CarActionStand);
    manoeuvres_Clear(car);
    cars_HasDestination(car) = 0u;
    cars_Driven(car) = 0u;
    cars_Command(car) = TyreCommand(0.0, 0.0, 0.0, 1u);
    cars_Hold(car) = DrivingHoldNone;
    cars_Context(car) = town_ClearContext();
}

// Which side of one lane a point stands, at a metre of it: positive to the lane's right.
float town_SideOf(int lane, vec2 pointM, float atM)
{
    vec2 positionM;
    float headingRad;
    float curvature;
    splineSampleBent(townLaneArcsFrom(lane), townLaneArcsTo(lane), atM, positionM, headingRad, curvature);
    vec2 direction = trigUnit(headingRad);
    precise float side = ((pointM.x - positionM.x) * -direction.y) + ((pointM.y - positionM.y) * direction.x);
    return side;
}

// Whether a place stands on the same side of the carriageway as this bay does, both measured against the bay's own
// nearest lane, each at its own projection onto it (GEN-4b, GEN-4k).
bool town_StandOnTheSameSideOfTheRoad(vec2 bayM, vec2 placeM)
{
    float alongM;
    int lane = roads_NearestStreetLane(bayM, alongM);
    if (lane < 0) return false;

    townArcsOfLanes();
    float offSq;
    float placeAlongM = splineProject(townLaneArcsFrom(lane), townLaneArcsTo(lane), placeM, alongM, roads_LaneLengthM(lane), offSq);
    precise float sides = town_SideOf(lane, bayM, alongM) * town_SideOf(lane, placeM, placeAlongM);
    return sides > 0.0;
}

// The nearest free bay a car can be got into within withinM of a place, or -1 (AMB-2, SRV-2) — and the same refused
// any bay across the carriageway from sameSideAsM (GEN-4k), unless that is AnySide, infinitely far off every bay.
int town_FreeBayNearSide(vec2 ofM, float withinM, vec2 sameSideAsM)
{
    bool anySide = floatBitsToUint(sameSideAsM.x) == 0x7F800000u && floatBitsToUint(sameSideAsM.y) == 0x7F800000u;
    int candidates[TownWorldBaysConsideredPerLeg];
    int bays = parking_BaysNear(ofM, withinM, candidates);
    for (int slot = 0; slot < bays; slot++)
    {
        int bay = candidates[slot];
        if (!parking_CanBeReached(bay)) continue;
        if (!anySide && !town_StandOnTheSameSideOfTheRoad(parking_CentreM(bay), sameSideAsM)) continue;

        return bay;
    }

    return -1;
}

int town_FreeBayNear(vec2 ofM, float withinM) { return town_FreeBayNearSide(ofM, withinM, vec2(townMapInfinity())); }

#endif
