// CarFleet's methods over the resident town's tables, named as the C# names them: cars_ClearRoute(car) is
// CarFleet.ClearRoute(car). The fields are the includer's accessors (town_rows.glsl, town_cars.glsl); a route queue is
// CarFleetRouteLanesPerCar a car and a line's lanes townCounts.mostLanes a car.

#ifndef CAR_FLEET_GLSL
#define CAR_FLEET_GLSL

// The highest of the car's channels' levels (CarFleet.LevelOf: BitOperations.Log2, which answers nought for none).
uint cars_LevelOf(uint car) { return uint(max(findMSB(cars_Channels(car)), 0)); }

// The bay the line in hand stops short of — none for a line of no lanes, whatever was last written.
int cars_StopsForBayOf(uint car) { return cars_Line(car).laneCount > 0u ? cars_StopsForBay(car) : CarFleetNoBay; }

bool cars_StopsAtItsPlaceOf(uint car) { return cars_Line(car).laneCount > 0u && cars_StopsAtItsPlace(car) != 0u; }

// The first lane of the chain, or none for a car on none.
int cars_LaneOf(uint car) { return cars_Line(car).laneCount > 0u ? cars_Chain(car, 0u) : CarFleetNoLane; }

float cars_GrantLeftM(uint car)
{
    precise float leftM = cars_AuthorityM(car) - cars_CoveredSinceClaimM(car);
    return leftM;
}

void cars_RouteChanged(uint car) { cars_RouteLaid(car)++; }

int cars_PeekNextRouteLane(uint car)
{
    return cars_RouteTaken(car) >= cars_RouteCount(car) ? CarFleetNoLane : cars_RouteLane(car, uint(cars_RouteTaken(car)));
}

int cars_TakeNextRouteLane(uint car)
{
    if (cars_RouteTaken(car) >= cars_RouteCount(car)) return CarFleetNoLane;

    cars_RouteChanged(car);
    int lane = cars_RouteLane(car, uint(cars_RouteTaken(car)));
    cars_RouteTaken(car)++;
    return lane;
}

int cars_PeekRouteLaneAfterNext(uint car)
{
    return cars_RouteTaken(car) + 1 >= cars_RouteCount(car) ? CarFleetNoLane : cars_RouteLane(car, uint(cars_RouteTaken(car) + 1));
}

// The chain's lanes from slot `from` up to `to` handed back to the front of the queue, in the order they were; what does
// not fit is dropped off the far end, and the queue runs out there (CarFleet.PutBackOnRoute, the lanes its caller hands
// it being these).
void cars_PutBackOnRoute(uint car, uint from, uint to)
{
    int lanes = int(to - from);
    int taken = cars_RouteTaken(car);
    int left = cars_RouteCount(car) - taken;
    int kept = min(left, int(CarFleetRouteLanesPerCar) - lanes);
    if (kept < left)
    {
        cars_RouteRunsOut(car) = 1u;
        cars_RouteEndsOn(car) = CarFleetNoLane;
    }

    // Span.CopyTo moves an overlapping run as if through a copy: from the back where it moves on.
    if (lanes > taken)
    {
        for (int at = kept - 1; at >= 0; at--) cars_RouteLane(car, uint(lanes + at)) = cars_RouteLane(car, uint(taken + at));
    }
    else
    {
        for (int at = 0; at < kept; at++) cars_RouteLane(car, uint(lanes + at)) = cars_RouteLane(car, uint(taken + at));
    }

    for (int at = 0; at < lanes; at++) cars_RouteLane(car, uint(at)) = cars_Chain(car, from + uint(at));
    cars_RouteTaken(car) = 0;
    cars_RouteCount(car) = lanes + kept;
    cars_RouteChanged(car);
}

// The queue dropped, and the turn at the end of it with it.
void cars_ClearRoute(uint car)
{
    cars_RouteCount(car) = 0;
    cars_RouteTaken(car) = 0;
    cars_RouteRunsOut(car) = 0u;
    cars_RouteEndsOn(car) = CarFleetNoLane;
    cars_TurnsBackOn(car) = CarFleetNoLane;
    cars_RouteChanged(car);
}

// This car's line, its tables already written, taken as laid. What the tick reads of where it ends (CarLineRow's
// finalApproach, LineReader.EndsWhereTheLegDoes) is read off the stop it was laid for.
void cars_SetLine(uint car, CarLineRow line)
{
    bool endsWhereTheLegDoes = line.laneCount > 0u && (cars_StopsForBay(car) != CarFleetNoBay || cars_StopsAtItsPlace(car) != 0u);
    line.finalApproach = endsWhereTheLegDoes ? 1u : 0u;
    cars_Line(car) = line;
    cars_LineLaid(car)++;
}

// The line shifted on past its first lane, everything else kept as laid; how far its metres moved back (CarFleet.ShiftTheLine).
float cars_ShiftTheLine(uint car)
{
    CarLineRow line = cars_Line(car);
    uint lanes = line.laneCount;
    float shiftM = cars_LaneStartM(car, 1u);
    uint shiftArcs = cars_LaneFirstArc(car, 1u);
    for (uint at = shiftArcs; at < line.arcCount; at++)
    {
        cars_LineArcA(car, at - shiftArcs) = cars_LineArcA(car, at);
        cars_LineArcB(car, at - shiftArcs) = cars_LineArcB(car, at);
        cars_LineEntry(car, at - shiftArcs) = cars_LineEntry(car, at);
    }

    for (uint slot = 0u; slot + 1u < lanes; slot++)
    {
        cars_Chain(car, slot) = cars_Chain(car, slot + 1u);
        precise float startM = cars_LaneStartM(car, slot + 1u) - shiftM;
        precise float endM = cars_LaneEndM(car, slot + 1u) - shiftM;
        cars_LaneStartM(car, slot) = startM;
        cars_LaneEndM(car, slot) = endM;
        cars_LaneFirstArc(car, slot) = cars_LaneFirstArc(car, slot + 1u) - shiftArcs;
        cars_JoinBreaks(car, slot) = cars_JoinBreaks(car, slot + 1u);
    }

    precise float lengthM = line.lengthM - shiftM;
    cars_SetLine(car, CarLineRow(line.arcCount - shiftArcs, lanes - 1u, lengthM, 0u));
    return shiftM;
}

#endif
