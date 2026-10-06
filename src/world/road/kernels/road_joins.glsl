// Which connector joins two lanes, on the device (RoadGraph.ConnectorBetween). The including kernel declares
// CONNECTOR_AT (uint[] a lane, and one past the last: where its connectors' run starts) and CONNECTOR_TO (int[] a
// connector: the lane it arrives on).

// The connector joining a pair of lanes, or -1.
int roadConnectorBetween(int fromLane, int toLane)
{
    for (uint connector = CONNECTOR_AT[fromLane]; connector < CONNECTOR_AT[fromLane + 1]; connector++)
    {
        if (CONNECTOR_TO[connector] == toLane) return int(connector);
    }

    return -1;
}
