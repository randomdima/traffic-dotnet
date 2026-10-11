// LineAssembler on the device (LineAssembler.Assemble, Spline.SubChainInto): a car's line over the lanes of its chain,
// the town's own join through each junction threaded between them, written into the car's own line tables — the host's
// line arc for arc. The includer defines the town's accessors (cars_…, roads_…, roads_ConnectorBetween) and includes
// town_arcs.glsl first.

#ifndef LINE_ASSEMBLER_GLSL
#define LINE_ASSEMBLER_GLSL

// LineAssembler.MostLanes: the most lanes a line is laid over — a bound on the work, which a local array of lanes is.
const uint LineAssemblerMostLanes = 12u;

// The stretch of a lane's line between two distances, written into the car's line from arc `into` with room for `room`
// (Spline.SubChainInto); how many arcs.
uint lineAssembler_SubChainInto(int lane, float fromM, float toM, uint car, uint into, uint room)
{
    precise float spanM = toM - fromM;
    if (spanM <= 0.0) return 0u;

    uint written = 0u;
    float startM = 0.0;
    for (int at = roads_LaneArcOffsets(lane); at < roads_LaneArcOffsets(lane + 1); at++)
    {
        Arc arc = townLaneArc(at);
        precise float endM = startM + arcLengthM(arc);
        float takeFromM = max(fromM, startM);
        float takeToM = min(toM, endM);
        if (takeToM > takeFromM && written < room)
        {
            precise float intoArcM = takeFromM - startM;
            precise float lengthM = takeToM - takeFromM;
            townSetLineArc(car, into + written, townArcOf(arcPointAt(arc, intoArcM), arcHeadingAt(arc, intoArcM), lengthM, arcCurvature(arc)));
            written++;
        }

        startM = endM;
    }

    return written;
}

// The line over the car's chain's first laneCount lanes, stopping lastLaneToM along the last: each lane's start, end and
// first arc written beside it. A lane the next one does not follow ends the line there. Its finalApproach is the
// laying's to say (cars_SetLine).
CarLineRow lineAssembler_Assemble(uint car, uint laneCount, float lastLaneToM)
{
    uint room = townCounts.arcsPerCar;
    uint written = 0u;
    float lengthM = 0.0;
    int arrivedOn = RoadGraphNoConnector;
    for (uint index = 0u; index < laneCount; index++)
    {
        int lane = cars_Chain(car, index);
        int leavingOn = index + 1u < laneCount ? roads_ConnectorBetween(lane, cars_Chain(car, index + 1u)) : RoadGraphNoConnector;
        if (arrivedOn != RoadGraphNoConnector)
        {
            for (int at = roads_ConnectorArcOffsets(arrivedOn); at < roads_ConnectorArcOffsets(arrivedOn + 1); at++)
            {
                cars_LineArcA(car, written) = roads_ConnectorArcA(at);
                cars_LineArcB(car, written) = roads_ConnectorArcB(at);
                written++;
            }

            precise float joinedM = lengthM + roads_ConnectorLengthM(arrivedOn);
            lengthM = joinedM;
        }

        // A lane is threaded whole (TER-5d): every join lands on its first point, and the join after it begins where its
        // line ends.
        float toM = roads_LaneLengthM(lane);
        if (index + 1u == laneCount) toM = min(toM, max(0.0, lastLaneToM));

        cars_LaneStartM(car, index) = lengthM;
        cars_LaneFirstArc(car, index) = written;
        uint laid = lineAssembler_SubChainInto(lane, 0.0, toM, car, written, room - written);
        for (uint arc = 0u; arc < laid; arc++)
        {
            precise float grownM = lengthM + cars_LineArcB(car, written + arc).y;
            lengthM = grownM;
        }

        written += laid;
        cars_LaneEndM(car, index) = lengthM;
        if (leavingOn == RoadGraphNoConnector && index + 1u < laneCount) return CarLineRow(written, index + 1u, lengthM, 0u);

        arrivedOn = leavingOn;
    }

    return CarLineRow(written, laneCount, lengthM, 0u);
}

#endif
