namespace TrafficSimulation.CityGen;

/// <summary>
/// Which way traffic runs on a road (TER-4d): both ways, or one way only — with the road's own direction
/// or against it, in the order the <c>.town</c> file's road records carry it.
/// </summary>
/// <remarks>
/// <b>A one-way road is a narrower road and not a road with a lane painted out</b> (GEN-15): it is laid at
/// one lane's width, its carriageway is that lane, and the lane's own line is the middle of it. Which way
/// it runs is the road's, so nothing has to read it off the geometry — a road drawn one way and driven the
/// other is <see cref="AgainstTheRoad"/> rather than a road redrawn backwards.
/// </remarks>
internal enum RoadFlow : byte
{
    /// <summary>Two lanes, one each way, assigned by heading (TER-4a).</summary>
    BothWays,

    /// <summary>One lane, driven from the road's <c>From</c> junction towards its <c>To</c>.</summary>
    WithTheRoad,

    /// <summary>One lane, driven the other way.</summary>
    AgainstTheRoad,
}
