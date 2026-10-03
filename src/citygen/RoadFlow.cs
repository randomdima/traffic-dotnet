namespace TrafficSimulation.CityGen;

/// <summary>
/// Which way traffic runs on a road (TER-4d): both ways, or one way only — with the road's own direction
/// or against it.
/// </summary>
/// <remarks>
/// <b>A one-way road is a narrower road and not a road with a lane painted out</b> (GEN-15): it is laid at
/// its lanes' width, its carriageway is those lanes, and the road's own line is the middle of them. Which
/// way it runs is the road's, so nothing has to read it off the geometry — a road drawn one way and driven
/// the other is <see cref="AgainstTheRoad"/> rather than a road redrawn backwards.
/// </remarks>
internal enum RoadFlow : byte
{
    /// <summary>Lanes each way, assigned by heading (TER-4a).</summary>
    BothWays,

    /// <summary>Driven from the road's <c>From</c> junction towards its <c>To</c> only.</summary>
    WithTheRoad,

    /// <summary>Driven the other way only.</summary>
    AgainstTheRoad,
}

/// <summary>
/// How many lanes a road is driven in each way (<see cref="CityPlan.RoadArrays.Lanes"/>): with its own
/// direction, and against it.
/// </summary>
internal readonly record struct RoadLanes(byte With, byte Against)
{
    /// <summary>The same road read from its other end.</summary>
    public RoadLanes Turned => new(Against, With);

    public RoadFlow Flow => Against == 0 ? RoadFlow.WithTheRoad : With == 0 ? RoadFlow.AgainstTheRoad : RoadFlow.BothWays;
}
