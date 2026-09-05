namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// <b>One stretch of one of the town's ways a car has claimed and is not on yet</b>, in that way's own
/// metres — the shape of every slot of <see cref="CarFleet.ClaimAhead"/>.
/// </summary>
/// <remarks>
/// It is the claim as the <em>car</em> carries it and not as the index holds it: the road's own row
/// (<c>LaneClaim</c>) is laid from this every tick and carries the rank, the priority and the roster with
/// it, none of which a car has any say in.
/// </remarks>
internal readonly record struct GroundClaim(int Way, float FromM, float ToM)
{
    public static GroundClaim Nothing => new(CarFleet.NoWay, 0f, 0f);

    public bool Any => Way != CarFleet.NoWay;
}
