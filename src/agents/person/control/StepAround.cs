using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Person.Control;

/// <summary>
/// <b>Where a walker aims to get past a body that is going nowhere</b> (PER-24): the aim it already had,
/// moved sideways by however much of the obstruction is in the way and no further.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a step and not a route.</b> The walked line is untouched, so nothing is planned, nothing is
/// remembered and the aim comes back to the line of its own accord the moment the body is abeam — which is
/// what "instantly" means here: the divergence lasts exactly as long as the thing that caused it.
/// </para>
/// <para>
/// <b>The side is the rule and the width is the geometry.</b> A step is to the walker's right, because a
/// town where everybody steps the same way is a town where two walkers meeting head on do not both step
/// into each other; how far is the two bodies' radii and the room between shoulders, which is the least
/// that gets past. The one thing that overrides the side is the ground (PER-7.2) — a step that lands off
/// the pavement, or in a bay the traffic uses, is a walker stepping into the road to get round a bin — and
/// the caller is what asks the terrain, since this holds nothing but the pose it is given.
/// </para>
/// <para>
/// <b>Measured against the aim and never against the body's heading.</b> What the step is a divergence
/// from is the walk, so a walker already turned by an earlier step reads the same obstruction as
/// progressively further out of its way and stops stepping when it is clear of it rather than when it is
/// pointed away from it.
/// </para>
/// </remarks>
internal static class StepAround
{
    /// <summary>
    /// Whether this body is in the way at all: in front of the walker, short of where it is aiming, and
    /// inside the room the walker needs across the line it is walking. <b>Anything else is already being
    /// passed</b>, and the aim it has is the aim it keeps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Short of the aim, because a body past it is not this stretch's problem</b>: the next point of the
    /// line is reached first and the question is asked again from there, with nothing standing this walker
    /// still in the meantime.
    /// </para>
    /// <para>
    /// <b>And never a body standing where the walk is going</b>, which is the one case a step cannot answer:
    /// the ground within the clearance of it is exactly the ground being stepped out of, so a walker aiming
    /// inside that circle is a walker whose aim it can never reach — it would come round the body and round
    /// it again for as long as the leg lasted. A paramedic walks <em>at</em> a casualty, and an officer
    /// closes a road from a place a stride from one.
    /// </para>
    /// </remarks>
    /// <param name="clearanceM">Middle to middle: the two radii and the room between shoulders.</param>
    public static bool IsInTheWay(Vector2 positionM, Vector2 aimM, Vector2 bodyM, float clearanceM)
    {
        if (!Frame(positionM, aimM, out var forward, out var right, out var toAimM)) return false;
        if ((bodyM - aimM).LengthSquared() < clearanceM * clearanceM) return false;

        var to = bodyM - positionM;
        var alongM = Vector2.Dot(to, forward);
        return alongM > 0f && alongM < toAimM && MathF.Abs(Vector2.Dot(to, right)) < clearanceM;
    }

    /// <summary>
    /// <b>Where to aim to pass it</b>: the clearance off the body's own middle, on the side asked for.
    /// Nothing is added for the distance to it — the point is beside the obstruction, so a walker aiming at
    /// it turns as far as it has to and no further, and closes back on its line as it comes past.
    /// </summary>
    public static Vector2 PassM(Vector2 positionM, Vector2 aimM, Vector2 bodyM, float clearanceM, bool onTheRight)
    {
        if (!Frame(positionM, aimM, out _, out var right, out _)) return aimM;

        return bodyM + (right * (onTheRight ? clearanceM : -clearanceM));
    }

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

    /// <summary>The frame of the walk this step is a divergence from, or false where there is no walk to diverge from.</summary>
    static bool Frame(Vector2 positionM, Vector2 aimM, out Vector2 forward, out Vector2 right, out float toAimM)
    {
        var run = aimM - positionM;
        toAimM = run.Length();
        if (toAimM < 1e-4f)
        {
            forward = Vector2.Zero;
            right = Vector2.Zero;
            return false;
        }

        forward = run / toAimM;
        right = Heading.RightOf(forward);
        return true;
    }
}
