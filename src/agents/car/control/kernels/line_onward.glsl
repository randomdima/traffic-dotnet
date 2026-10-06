// Whether a way is one a car's own line runs over, and which way the line takes after it, on the device
// (DrivingGround.IsOnItsLine). The including kernel includes road_joins.glsl, declares LANE_CHAIN (int[], the line's
// chain, LINE_ONWARD_FIGURES.mostLanes a car), and supplies LINE_ONWARD_FIGURES' fields mostLanes and firstConnector (the
// way the town numbers its first connector).

// LaneOccupancy.NoWay and RunsOn.
const int LineNoWay = -1;
const int LineRunsOn = -2;

// Whether the way is on the car's line, and the way after it there — a join's lane, the join out of a lane, RunsOn where
// the line runs on past the car down its last lane, NoWay where it ends there.
bool lineIsOnItsLine(uint car, uint laneCount, bool runsOnPastItself, uint way, out int onward)
{
    onward = LineNoWay;
    uint slots = car * LINE_ONWARD_FIGURES.mostLanes;
    for (uint index = 0u; index < laneCount; index++)
    {
        int lane = LANE_CHAIN[slots + index];
        int join = index + 1u == laneCount ? -1 : roadConnectorBetween(lane, LANE_CHAIN[slots + index + 1u]);
        int joinWay = join < 0 ? LineNoWay : int(LINE_ONWARD_FIGURES.firstConnector) + join;
        if (uint(lane) == way)
        {
            onward = joinWay != LineNoWay ? joinWay : runsOnPastItself ? LineRunsOn : LineNoWay;
            return true;
        }

        if (joinWay != LineNoWay && uint(joinWay) == way)
        {
            onward = LANE_CHAIN[slots + index + 1u];
            return true;
        }
    }

    return false;
}
