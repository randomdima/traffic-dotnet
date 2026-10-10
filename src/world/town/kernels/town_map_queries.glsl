// What the town's decisions ask of the static map (town_map.glsl, town_places.glsl), line for line with the C# and named
// alike: roads_ConnectorBetween is RoadGraph.ConnectorBetween, driving_PlaceOfM DrivingNetwork.PlaceOfM. A quotient or a
// root is rounded.glsl's, so it is the host's to the bit; the including kernel gets doubles with it.
//
// The queries that answer a route's entry or goal (DrivingNetwork.EntryOnLane, GoalOnLane; WalkingNetwork.EntryOnEdge)
// answer the routing module's RouteEntry and RouteGoal, and are here only where the includer has included
// world/routing/kernels/route_tables.glsl first. What is asked of a spatial index of lanes or edges (RoadGraph.NearestLane,
// NearestStreetLane, LanesAround; FootGraph.NearestEdge, EdgesNear, and with them DrivingNetwork.GoalsAt and
// WalkingNetwork.EntriesNear, GoalsAt, AsideM) is not: no ChainIndex is on the device yet.

#ifndef TOWN_MAP_QUERIES_GLSL
#define TOWN_MAP_QUERIES_GLSL

#include "world/town/kernels/town_map.glsl"
#include "world/town/kernels/town_places.glsl"
#include "core/geometry/kernels/rounded.glsl"

// WalkingNetwork.LaneMOf's floor on an edge's length, a literal in the C#.
const float WalkingNetworkLeastEdgeM = 1e-4;

float townMapNaN() { return uintBitsToFloat(0x7FC00000u); }
float townMapInfinity() { return uintBitsToFloat(0x7F800000u); }

// RoadGraph.

bool roads_IsABayArm(int lane) { return roads_LaneIsBay(lane) != 0u; }

bool roads_IsARoadside(int lane) { return lane >= roads_FirstRoadside; }

bool roads_IsAStreetLane(int lane) { return !roads_IsABayArm(lane) && !roads_IsARoadside(lane); }

int roads_LaneBeside(int lane, bool inward) { return inward ? roads_LaneInward(lane) : roads_LaneOutward(lane); }

bool roads_AreSideBySide(int lane, int other)
{
    return lane != other && roads_LaneRoad(lane) == roads_LaneRoad(other) && roads_LaneForward(lane) == roads_LaneForward(other)
        && roads_IsAStreetLane(lane) && roads_IsAStreetLane(other);
}

int roads_ConnectorBetween(int fromLane, int toLane)
{
    for (int connector = roads_ConnectorAt(fromLane); connector < roads_ConnectorAt(fromLane + 1); connector++)
    {
        if (roads_ConnectorToLane(connector) == toLane) return connector;
    }

    return RoadGraphNoConnector;
}

// Inward first, then outward: which of two lanes beside it joins is the first found that way.
bool roads_ReachesBySwitching(int from, int onto, out int besideAt)
{
    besideAt = RoadGraphNoLane;
    if (!roads_IsAStreetLane(from) || !roads_IsAStreetLane(onto)) return false;
    if (roads_AreSideBySide(from, onto))
    {
        besideAt = onto;
        return true;
    }

    for (int side = 0; side < 2; side++)
    {
        bool inward = side == 0;
        for (int beside = roads_LaneBeside(from, inward); beside != RoadGraphNoLane; beside = roads_LaneBeside(beside, inward))
        {
            if (roads_ConnectorBetween(beside, onto) == RoadGraphNoConnector) continue;

            besideAt = beside;
            return true;
        }
    }

    return false;
}

int roads_LanesOver(int from, int beside) { return int(roads_LaneFromKerb(beside)) - int(roads_LaneFromKerb(from)); }

// The first of the nearest in connector order.
int roads_TheLaneOnBeside(int from, int route)
{
    int found = RoadGraphNoLane;
    int nearest = int(0x7FFFFFFFu);
    for (int connector = roads_ConnectorAt(from); connector < roads_ConnectorAt(from + 1); connector++)
    {
        int onward = roads_ConnectorToLane(connector);
        if (!roads_AreSideBySide(onward, route)) continue;

        int lanesOver = abs(roads_LanesOver(onward, route));
        if (lanesOver >= nearest) continue;

        nearest = lanesOver;
        found = onward;
    }

    return found;
}

int roads_OnBesideTheRoute(int last, int next, int after, out int passed)
{
    passed = 0;
    if (!roads_AreSideBySide(last, next)) return roads_TheLaneOnBeside(last, next);

    if (after == RoadGraphNoLane) return RoadGraphNoLane;

    if (roads_ConnectorBetween(last, after) != RoadGraphNoConnector)
    {
        passed = 2;
        return after;
    }

    int beside = roads_TheLaneOnBeside(last, after);
    if (beside != RoadGraphNoLane) passed = 1;

    return beside;
}

uint roads_FirmOn(uint turn)
{
    if (turn == LaneTurnStraight) return ClaimPriorityFirmStraight;
    if (turn == LaneTurnNearSide) return ClaimPriorityFirm;
    return ClaimPriorityFirmAcross;
}

uint roads_FirmOnConnector(int connector) { return roads_FirmOn(roads_ConnectorKind(connector)); }

float roads_ConnectorWidthM(int connector)
{
    return min(roads_LaneWidthM(roads_ConnectorFromLane(connector)), roads_LaneWidthM(roads_ConnectorToLane(connector)));
}

uint roads_ConnectorChannels(int connector)
{
    return (1u << roads_LaneLevel(roads_ConnectorFromLane(connector))) | (1u << roads_LaneLevel(roads_ConnectorToLane(connector)));
}

uint roads_ConnectorLevel(int connector)
{
    uint level = roads_LaneLevel(roads_ConnectorFromLane(connector));
    return level == roads_LaneLevel(roads_ConnectorToLane(connector)) ? level : RoadArraysGround;
}

int roads_WayOfConnector(int connector) { return roads_LaneCount + connector; }

int roads_ConnectorOfWay(int way) { return way - roads_LaneCount; }

// Overtaking.IsALaneToPassFrom: a lane with another the other way, not one line with it, whose line may be crossed.
bool overtaking_IsALaneToPassFrom(int lane)
{
    return roads_LaneReverse(lane) >= 0 && roads_LaneOverOneLine(lane) == 0u && roads_LaneCrossesToPass(lane) != 0u;
}

// RunNetwork, either network's: where along its link a piece's metre stands, and which piece a link's metre is on.

float drivingRuns_PlaceOfM(int link, int slot, float alongPieceM)
{
    precise float atM = drivingRuns_StationM(drivingRuns_PieceOffsets(link) + slot) + alongPieceM;
    return atM;
}

int drivingRuns_PieceAt(int link, float alongLinkM, out float alongPieceM)
{
    int first = drivingRuns_PieceOffsets(link);
    int low = 0;
    int high = drivingRuns_PieceOffsets(link + 1) - first - 1;
    while (low < high)
    {
        int middle = (low + high + 1) / 2;
        if (drivingRuns_StationM(first + middle) <= alongLinkM) low = middle;
        else high = middle - 1;
    }

    precise float pieceM = alongLinkM - drivingRuns_StationM(first + low);
    alongPieceM = pieceM;
    return low;
}

float walkingRuns_PlaceOfM(int link, int slot, float alongPieceM)
{
    precise float atM = walkingRuns_StationM(walkingRuns_PieceOffsets(link) + slot) + alongPieceM;
    return atM;
}

int walkingRuns_PieceAt(int link, float alongLinkM, out float alongPieceM)
{
    int first = walkingRuns_PieceOffsets(link);
    int low = 0;
    int high = walkingRuns_PieceOffsets(link + 1) - first - 1;
    while (low < high)
    {
        int middle = (low + high + 1) / 2;
        if (walkingRuns_StationM(first + middle) <= alongLinkM) low = middle;
        else high = middle - 1;
    }

    precise float pieceM = alongLinkM - walkingRuns_StationM(first + low);
    alongPieceM = pieceM;
    return low;
}

// DrivingNetwork.

float driving_PlaceOfM(int lane, float alongLaneM)
{
    return drivingRuns_PlaceOfM(driving_LinkOfLane(lane), driving_SlotOfLane(lane), alongLaneM);
}

// WalkingNetwork.

float walking_PlaceOfM(int edge, float alongEdgeM)
{
    return walkingRuns_PlaceOfM(walking_LinkOfEdge(edge), walking_SlotOfEdge(edge), alongEdgeM);
}

int walking_TurnSlot(int fromEdge, int toEdge)
{
    for (int slot = walking_TurnOffsets(fromEdge); slot < walking_TurnOffsets(fromEdge + 1); slot++)
    {
        if (walking_TurnToEdge(slot) == toEdge) return slot;
    }

    return WalkingNetworkNoTurn;
}

int walking_TurnSlotAt(int edge, int turn) { return walking_TurnOffsets(edge) + turn; }

bool walking_IsACorner(int way) { return way < 0; }

int walking_CornerOf(int way) { return ~way; }

int walking_WayOfCorner(int turn) { return ~turn; }

float walking_LaneWidthM(int edge) { return foot_BandM(edge); }

float walking_WayLengthM(int way)
{
    return walking_IsACorner(way) ? walking_JoinLengthM(walking_CornerOf(way)) : walking_LaneLengthM(way);
}

float walking_WayWidthM(int way)
{
    return walking_LaneWidthM(walking_IsACorner(way) ? walking_TurnToEdge(walking_CornerOf(way)) : way);
}

void walking_SpanOfWay(int before, int way, int after, out float fromM, out float toM)
{
    fromM = 0.0;
    toM = walking_WayLengthM(way);
    if (walking_IsACorner(way)) return;

    if (before != WalkingNetworkNoLane && walking_IsACorner(before)) fromM = walking_JoinToM(walking_CornerOf(before));
    if (after != WalkingNetworkNoLane && walking_IsACorner(after))
    {
        precise float shortM = toM - walking_JoinFromM(walking_CornerOf(after));
        toM = min(toM, shortM);
    }

    toM = max(fromM, toM);
}

float walking_LaneMOf(int edge, float alongEdgeM)
{
    float edgeLengthM = max(WalkingNetworkLeastEdgeM, foot_LengthM(edge));
    float headM = walking_HeadLengthM(edge);
    precise float alongTheStretchM = walking_LaneLengthM(edge) - walking_TailLengthM(edge) + headM;
    precise float laneM = (roundedQuotient(alongEdgeM, edgeLengthM) * alongTheStretchM) - headM;
    return max(0.0, laneM);
}

// WalkingNetwork.FootJoins.Between: the corner turned from one edge onto another, or RouteChainNoWay.
int walking_FootJoinsBetween(int from, int onto)
{
    if (from < 0) return RouteChainNoWay;

    int turn = walking_TurnSlot(from, onto);
    return turn == WalkingNetworkNoTurn || turn == walking_TailOf(from) ? RouteChainNoWay : walking_WayOfCorner(turn);
}

// TownWays.

uint ways_KindOf(int way)
{
    if (way < ways_FirstConnector) return WayKindLane;
    if (way < ways_FirstFootwayWay) return WayKindConnector;
    if (way < ways_FirstMitreWay) return WayKindFootway;
    return WayKindMitre;
}

bool ways_IsDriven(int way) { return way < ways_FirstFootwayWay; }

int ways_OfRoadLane(int lane) { return lane; }

int ways_OfRoadConnector(int connector) { return ways_FirstConnector + connector; }

int ways_OfFootway(int edge) { return ways_FirstFootwayWay + edge; }

int ways_OfMitre(int slot) { return ways_FirstMitreWay + slot; }

int ways_RoadLaneOf(int way) { return way; }

int ways_RoadConnectorOf(int way) { return way - ways_FirstConnector; }

int ways_FootwayOf(int way) { return way - ways_FirstFootwayWay; }

int ways_MitreOf(int way) { return way - ways_FirstMitreWay; }

// WayLines: the zebra a way is, and the channels it is on.

int wayLines_ZebraOf(int way)
{
    if (ways_KindOf(way) != WayKindFootway) return RibbonMarksNoZebra;

    int crossing = crossingEdges_CrossingOf(ways_FootwayOf(way));
    return crossing == CityPlanNoRecord ? RibbonMarksNoZebra : crossing;
}

bool wayLines_IsTheCrossing(int way) { return wayLines_ZebraOf(way) != RibbonMarksNoZebra; }

uint wayLines_ChannelsOf(int way)
{
    uint kind = ways_KindOf(way);
    if (kind == WayKindLane) return 1u << roads_LaneLevel(ways_RoadLaneOf(way));
    if (kind == WayKindConnector) return roads_ConnectorChannels(ways_RoadConnectorOf(way));
    return 1u << RoadArraysGround;
}

// BayStreets.

float bayStreets_AtLaneM(int bay, int lane)
{
    for (uint slot = 0u; slot < BayStreetsMostLanes; slot++)
    {
        if (bayStreets_SlotLane(bay, slot) == lane) return bayStreets_SlotAtLaneM(bay, slot);
    }

    return townMapNaN();
}

bool bayStreets_CanBeReached(int bay) { return bayStreets_SlotLane(bay, 0u) != RoadGraphNoLane; }

int bayStreets_BayOnArm(int lane) { return lane >= 0 && lane < roads_LaneCount ? bayStreets_BayOnLane(lane) : BayStreetsNoBay; }

// The bay off a lane furthest along it within reach of its end.
int bayStreets_TheBayToTurnIn(int lane, float withinM)
{
    int best = BayStreetsNoBay;
    precise float bestM = roads_LaneLengthM(lane) - withinM;
    for (int at = bayStreets_FirstBayOfLane(lane); at < bayStreets_FirstBayOfLane(lane + 1); at++)
    {
        int bay = bayStreets_BayOffLane(at);
        float atM = bayStreets_AtLaneM(bay, lane);
        if (atM < bestM) continue;

        best = bay;
        bestM = atM;
    }

    return best;
}

// BucketGrid.Nearest over a grid laid as town_places.glsl lays one, its items' centres the table centresId names: the
// item nearest a point by its centre, ring by ring out from the point's cell, and -1 where the grid files none.
int bucketGrid_CellOf(BucketGridRow grid, float atM)
{
    precise float scaled = atM * grid.perCellM;
    return int(floor(scaled));
}

int bucketGrid_Nearest(BucketGridRow grid, uint startsId, uint countsId, uint itemsId, uint centresId, vec2 pointM, out float distanceM)
{
    int toX = grid.fromX + grid.width - 1;
    int toY = grid.fromY + grid.height - 1;
    int originX = clamp(bucketGrid_CellOf(grid, pointM.x), grid.fromX, toX);
    int originY = clamp(bucketGrid_CellOf(grid, pointM.y), grid.fromY, toY);
    int lastRing = max(grid.width, grid.height);

    int best = -1;
    float bestDistanceSquared = townMapInfinity();
    for (int ring = 0; ring <= lastRing; ring++)
    {
        for (int y = clamp(originY - ring, grid.fromY, toY); y <= clamp(originY + ring, grid.fromY, toY); y++)
        {
            for (int x = clamp(originX - ring, grid.fromX, toX); x <= clamp(originX + ring, grid.fromX, toX); x++)
            {
                if (ring > 0 && abs(x - originX) != ring && abs(y - originY) != ring) continue;

                int bucket = ((y - grid.fromY) * grid.width) + (x - grid.fromX);
                int start = TOWN(Ints, startsId).v[bucket];
                int end = start + TOWN(Ints, countsId).v[bucket];
                for (int slot = start; slot < end; slot++)
                {
                    int item = TOWN(Ints, itemsId).v[slot];
                    vec2 centreM = TOWN(Vec2s, centresId).v[item];
                    precise float dx = centreM.x - pointM.x;
                    precise float dy = centreM.y - pointM.y;
                    precise float distanceSquared = (dx * dx) + (dy * dy);
                    if (distanceSquared >= bestDistanceSquared) continue;

                    bestDistanceSquared = distanceSquared;
                    best = item;
                }
            }
        }

        precise float searchedM = float(ring) * grid.cellM;
        precise float reachM = searchedM + grid.maxRadiusM;
        precise float reachSquared = reachM * reachM;
        if (best >= 0 && bestDistanceSquared <= reachSquared) break;
    }

    distanceM = best >= 0 ? roundedSqrt(bestDistanceSquared) : townMapInfinity();
    return best;
}

#ifdef ROUTE_TABLES_GLSL

RouteEntry driving_EntryOnLane(int lane, float alongLaneM)
{
    int link = driving_LinkOfLane(lane);
    float alongM = driving_PlaceOfM(lane, alongLaneM);
    precise float remainingM = drivingRuns_LengthM(link) - alongM;
    return RouteEntry(link, alongM, remainingM, 0.0);
}

// Into a goal of room one; no goal where the lane is on no link.
int driving_GoalOnLane(int lane, float alongLaneM, out RouteGoal goal)
{
    goal = RouteGoal(TravelGraphNoLink, 0.0);
    if (driving_LinkOfLane(lane) == TravelGraphNoLink) return 0;

    goal = RouteGoal(driving_LinkOfLane(lane), driving_PlaceOfM(lane, alongLaneM));
    return 1;
}

RouteEntry walking_EntryOnEdge(int edge, float alongEdgeM)
{
    int link = walking_LinkOfEdge(edge);
    float alongM = walking_PlaceOfM(edge, alongEdgeM);
    precise float remainingM = walkingRuns_LengthM(link) - alongM;
    return RouteEntry(link, alongM, remainingM, 0.0);
}

#endif

#endif
