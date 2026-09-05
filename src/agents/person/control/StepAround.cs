using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Person.Control;

/// <summary>
/// <b>The one bound on where a step round a body may land</b> (PER-24): the carriageway, which is grazed
/// and never entered.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where the step goes is not here.</b> A step is the walk asked for again from an offset across the
/// walker's own way and granted or refused on the claims like any other ask
/// (<c>TownWorld.StepPastTheBody</c>), so which body is being got past, which side the step is to and how
/// far it reaches are all the grant's. What is left here is the question the claims cannot answer, because
/// it is about ground rather than about anybody on it.
/// </para>
/// <para>
/// <b>Ground the traffic is not on is a walker's to step onto</b>, walk or no walk — the verge, the
/// frontage, the far side of the pavement, the channel. A walker's own lane line runs about a body's width
/// from the edge of its band, so a step held inside the band is a step almost never taken.
/// </para>
/// </remarks>
internal static class StepAround
{
    /// <summary>
    /// <b>Whether a step may land here as far as the traffic is concerned</b>: outside the nearest lane's
    /// own band, or no further than <paramref name="grazeM"/> inside it. <b>A carriageway is grazed and
    /// never entered</b> — a body at the channel with the kerb under it is what a person does to get round
    /// something on a narrow pavement, and a body a stride further in is standing in a lane.
    /// </summary>
    /// <remarks>
    /// <b>The lane's own band, as everywhere else that asks this</b> (<see cref="Reel.InTheCarriageway"/>),
    /// and never the ground grid: a kerb line does not lie on a metre grid, so two samples either side of
    /// one are the same cell and the answer would turn on rounding rather than on where the body is.
    /// </remarks>
    public static bool IsClearOfTheTraffic(RoadGraph roads, Vector2 atM, float grazeM)
    {
        var lane = roads.NearestLane(atM, out var alongM);
        if (lane < 0) return true;

        var on = Spline.SampleAt(roads.ArcsOf(lane), alongM);
        return MathF.Abs(Vector2.Dot(atM - on.PositionM, on.Right)) > (roads.LaneWidthM[lane] * 0.5f) - grazeM;
    }
}
