using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Person.Control;

/// <summary>The lane a walker beside a road would step into, and where the middle of it is.</summary>
/// <param name="Lane">The lane itself, which is also its way in the road's own book.</param>
/// <param name="AlongM">How far along that lane the step lands.</param>
/// <param name="RoadM">And where that is, which is where the body would stand.</param>
internal readonly record struct PacedStep(int Lane, float AlongM, Vector2 RoadM);

/// <summary>
/// <b>A walker beside a road with nowhere to be paces across it</b>: out into the lane, a beat standing in
/// it, back to where it was put down, and again. What is here is the two places and the one question asked
/// before the first of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no crossing under it and that is the point.</b> `P-12` is about paint — a band of lane a
/// driver owes a stop short of, and a walker that waits for a phase before using it. A body that steps into
/// a lane where nothing is painted is owed nothing by anybody, and what has to stop for it is a driver
/// looking at what is in front of it and nothing else.
/// </para>
/// <para>
/// <b>What it waits for is ground nobody has taken</b>, and never a gap in the traffic. A reservation is the
/// road a driver is committed to — its own tail to where it plans to be able to stop — so a body put down
/// outside every one of them is a body every car on the road can still stop for, and a body put down inside
/// one is a body nothing could have. Waiting for an empty road instead would be a walker that only ever
/// stepped out when nothing was coming, which is a walker no driver is ever tested by.
/// </para>
/// </remarks>
internal static class StepOut
{
    /// <summary>
    /// The step this walker would take, or false where it is not standing beside a road at all. <b>The
    /// nearest lane and not the nearest road</b>: a lane runs one way, and the one whose own line passes
    /// nearest the body is the one the body is standing at the edge of.
    /// </summary>
    public static bool BesideARoad(SimConfig config, RoadGraph roads, Vector2 standingM, out PacedStep step)
    {
        step = default;
        var lane = roads.NearestLane(standingM, out var alongM);
        if (lane < 0) return false;

        var on = Spline.SampleAt(roads.ArcsOf(lane), alongM);
        if ((on.PositionM - standingM).Length() > config.RoadWidthM) return false;

        step = new PacedStep(lane, alongM, on.PositionM);
        return true;
    }

    /// <summary>
    /// Whether the ground this step lands on is nobody's, asked from <paramref name="fromM"/> — which is the
    /// whole of what holds a body on the pavement, so it steps out again the moment the road is clear of
    /// whatever it was last stopped for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing on the road at all is the clearest case there is</b> and lets the body straight out. It
    /// then stands in the lane for whatever arrives next, which is what a body pacing a road is for: waiting
    /// on the pavement for a car to appear first would put it in the lane at the one moment there is no
    /// longer room to walk into it.
    /// </para>
    /// <para>
    /// <b>Far enough is measured from the far edge of the road something has taken</b> — where its own driver
    /// is committed to being able to stop — and the clear ground past that has to be worth the walk across at
    /// the speed it is closing. The reaction is already inside the road a driver takes and is not counted
    /// twice. A queue closed up behind whatever stopped here has taken the ground right up to the body, which
    /// is what keeps it on the pavement until the last of them has gone past.
    /// </para>
    /// </remarks>
    public static bool RoomToStepOut(SimConfig config, LaneOccupancy roads, in PacedStep step, Vector2 fromM)
    {
        // Whatever stopped for this body last time is still standing where it stopped. It has not gone past
        // yet, and stepping out again in front of it is asking one driver the same question twice.
        if (StoodStillFor(config, roads, step)) return false;

        if (!roads.TakenUpTo(roads.WayOfLane(step.Lane), step.AlongM, out var taken)) return true;

        var clearM = step.AlongM - taken.ToM;
        if (clearM <= config.PersonDiameterM) return false;

        var acrossS = (step.RoadM - fromM).Length() / MathF.Max(0.1f, config.Person.WalkSpeedMps);
        return clearM > taken.AlongMps * acrossS;
    }

    /// <summary>
    /// Whether something has come to rest on the lane for a body standing on it. <b>A body paces a road to
    /// be stopped for</b>, and once it has been, standing there any longer is standing in front of a driver
    /// who has already answered.
    /// </summary>
    /// <remarks>
    /// Asked within a body and the gap one keeps, because that is where a driver stopping for this one
    /// comes to rest. Anything further back has stopped behind whatever is between, and that is its
    /// business rather than this body's.
    /// </remarks>
    public static bool StoodStillFor(SimConfig config, LaneOccupancy roads, in PacedStep step)
    {
        var reachM = config.Car.LengthM + config.CarBodyMarginM;
        return roads.BehindBody(
                   roads.WayOfLane(step.Lane), step.AlongM, step.AlongM - reachM, LaneOccupancy.Nobody, out var behind)
               && MathF.Abs(behind.AlongMps) <= config.Driving.StopSpeedMps;
    }
}
