// LaneTour on the device: at a lane's end, one of the turns the graph offers drawn from the car's own stream, weighted by
// what the router prices it at — never onto a bay, a roadside or a lane a closure holds (TER-5f, SRV-9, GEN-4h). The
// includer defines the town's accessors (roads_…, town_ClosedLane) and includes rng.glsl, rounded.glsl and
// town_map_queries.glsl first; the prices are its figures, handed in as the C# hands in its config.

#ifndef LANE_TOUR_GLSL
#define LANE_TOUR_GLSL

bool laneTour_IsOpen(int lane) { return roads_IsAStreetLane(lane) && town_ClosedLane(lane) == 0u; }

// The cheaper the turn, the likelier: nothing onto a lane the tour may not take, nor onto one nothing leaves.
float laneTour_Weight(int connector, float nearSideCarLengths, float acrossOncomingCarLengths)
{
    int onto = roads_ConnectorToLane(connector);
    if (!laneTour_IsOpen(onto)) return 0.0;
    if (roads_LanesFromCount(onto) == 0) return 0.0;

    uint turn = roads_ConnectorKind(connector);
    if (turn == LaneTurnStraight) return 1.0;

    precise float priced = 1.0 + (turn == LaneTurnNearSide ? nearSideCarLengths : acrossOncomingCarLengths);
    return roundedQuotient(1.0, priced);
}

// The nth of the movements onto a lane the tour may take.
int laneTour_TheOpenDrawn(int lane, int nth)
{
    for (int connector = roads_ConnectorAt(lane); connector < roads_ConnectorAt(lane + 1); connector++)
    {
        int onto = roads_ConnectorToLane(connector);
        if (!laneTour_IsOpen(onto)) continue;
        if (nth-- == 0) return onto;
    }

    return CarFleetNoLane;
}

int laneTour_NextLane(int lane, inout Rng draw, float nearSideCarLengths, float acrossOncomingCarLengths)
{
    float total = 0.0;
    int open = 0;
    for (int connector = roads_ConnectorAt(lane); connector < roads_ConnectorAt(lane + 1); connector++)
    {
        precise float summed = total + laneTour_Weight(connector, nearSideCarLengths, acrossOncomingCarLengths);
        total = summed;
        if (laneTour_IsOpen(roads_ConnectorToLane(connector))) open++;
    }

    if (open == 0) return CarFleetNoLane;
    if (total <= 0.0) return laneTour_TheOpenDrawn(lane, rngNextInt(draw, open));

    float drawn = rngNextFloat(draw, 0.0, total);
    for (int connector = roads_ConnectorAt(lane); connector < roads_ConnectorAt(lane + 1); connector++)
    {
        precise float left = drawn - laneTour_Weight(connector, nearSideCarLengths, acrossOncomingCarLengths);
        drawn = left;
        if (drawn <= 0.0) return roads_ConnectorToLane(connector);
    }

    return laneTour_TheOpenDrawn(lane, open - 1);
}

#endif
