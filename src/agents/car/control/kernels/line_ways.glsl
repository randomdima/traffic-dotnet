// The town's ways under a car's line, on the device (LineWays.Overlaps, DrivingGround.WaysAlong): the lanes of its chain
// and the joins between them, each with the metres of its own a stretch of the line covers.
//
// The including kernel includes road_joins.glsl, declares the line's own layout (line.glsl's LANE_CHAIN, LANE_START,
// LANE_END, LINE_WAYS_FIGURES.mostLanes a car) and LANE_LENGTH (float[] a lane), defines LINE_MOST_WAYS
// (DrivingGround.MostWaysAlongALine), and supplies LINE_WAYS_FIGURES' fields mostLanes and firstConnector (the way the
// town numbers its first connector).

// A stretch of a way under a line (LineWay).
struct LineWayRow
{
    uint way;
    float fromM;
    float toM;
    float lineFromM;
};

// Two stretches' overlap (LineWays.Overlaps).
bool lineOverlaps(float fromM, float toM, float leastM, float mostM, out float fromOut, out float toOut)
{
    fromOut = max(fromM, leastM);
    toOut = min(toM, mostM);
    return toOut > fromOut;
}

// The town's ways under a stretch of one car's line, nearest first (DrivingGround.WaysAlong), into ways[]; how many.
uint lineWaysAlong(uint car, uint laneCount, float fromLineM, float toLineM, out LineWayRow ways[LINE_MOST_WAYS])
{
    uint slots = car * LINE_WAYS_FIGURES.mostLanes;
    uint written = 0u;
    for (uint index = 0u; index < laneCount && written < LINE_MOST_WAYS; index++)
    {
        int lane = LANE_CHAIN[slots + index];
        int leavingOn = index + 1u < laneCount ? roadConnectorBetween(lane, LANE_CHAIN[slots + index + 1u]) : -1;
        float startsM = LANE_START[slots + index];
        precise float lastEndsM = startsM + LANE_LENGTH[lane];
        float endsM = index + 1u < laneCount ? LANE_END[slots + index] : lastEndsM;

        float fromM;
        float toM;
        if (lineOverlaps(fromLineM, toLineM, startsM, endsM, fromM, toM))
        {
            precise float wayFromM = fromM - startsM;
            precise float wayToM = toM - startsM;
            ways[written++] = LineWayRow(uint(lane), wayFromM, wayToM, fromM);
        }

        float laneEndsM = LANE_END[slots + index];
        if (leavingOn < 0 || laneEndsM >= toLineM) break;

        float nextStartsM = LANE_START[slots + index + 1u];
        if (written < LINE_MOST_WAYS && nextStartsM > laneEndsM && lineOverlaps(fromLineM, toLineM, laneEndsM, nextStartsM, fromM, toM))
        {
            precise float wayFromM = fromM - laneEndsM;
            precise float wayToM = toM - laneEndsM;
            ways[written++] = LineWayRow(LINE_WAYS_FIGURES.firstConnector + uint(leavingOn), wayFromM, wayToM, fromM);
        }
    }

    return written;
}

// Where a place in one way's own metres falls on the line that ran over it (LineWays.OnTheLineM).
float lineOnTheLineM(LineWayRow way, float wayM)
{
    precise float lineM = way.lineFromM + (wayM - way.fromM);
    return lineM;
}
