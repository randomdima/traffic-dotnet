using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Person.Control;

/// <summary>What came of asking for the next lurch.</summary>
internal enum Lurch
{
    /// <summary>It is going, and <c>goalM</c> is where.</summary>
    Taken,

    /// <summary>Something is standing where it would have walked. It stays where it is and asks again in a moment.</summary>
    NoRoom,

    /// <summary>
    /// It is not near a road at all — thrown clear of one by a contact — and the ordinary wander carries it
    /// from there. A body merely knocked onto the verge staggers back into the road it came out of.
    /// </summary>
    NoRoad,
}

/// <summary>
/// <b>`PER-16` — a walker with nowhere to be that was put down <em>in</em> a carriageway reels along
/// it</b>: a few seconds of walking further down the lane it is on, thrown anywhere across the width of
/// that lane, over and over. What is here is the one place that lurch lands.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pose is the whole of what decides this, and no map is named anywhere</b> — the same rule that
/// makes a body put down <em>beside</em> a road one that paces across it (<see cref="StepOut"/>). A town
/// with somewhere to go has trips for its people and neither rule is reached.
/// </para>
/// <para>
/// <b>It keeps the way it is facing and does not turn round.</b> The two lanes of a carriageway run
/// opposite ways, so the nearest lane to a body that has wandered over the centreline is the one running
/// back the way it came — and a body that took that answer would reel a few metres one way, a few the
/// other, and stay where it was put down for ever. The lane it walks along is the one that agrees with the
/// body's own heading, which is stable across a stand because a body that stops is still facing the way it
/// was going.
/// </para>
/// <para>
/// <b>It is thrown across its own lane and never over the centreline.</b> The throw is measured off the
/// lane's own band, so a body cannot lurch onto ground the road does not have — and the lane running the
/// other way stays clear, which is the ground the traffic behind it gets past on (`E-4`). A drunk that
/// wandered over the centreline would be one nothing could lawfully pass on a road whose only other ground
/// is a verge, and a lap of them would be a lap nothing gets round.
/// </para>
/// <para>
/// <b>It asks nothing of the traffic, and the one thing it looks at is a body.</b> A reservation cut at this
/// body is a car committed to stopping short of it, so the road in front of it is its own to walk down — a
/// body that waited for a gap would be a body no driver is ever tested by. What it will not walk into is
/// something <em>standing</em> there, because that it walks into rather than the other way round and nobody
/// is holding off on its behalf.
/// </para>
/// </remarks>
internal static class Reel
{
    /// <summary>
    /// Whether this place is on the carriageway itself rather than beside it — <b>the fact that tells a
    /// drunk from a pacer</b>, asked of where the body was put down and never of where it has got to.
    /// </summary>
    /// <remarks>
    /// <b>The lane's own band</b>, as everywhere else that asks this question: a body on a verge is nearest
    /// some lane too, and a reach of anybody's choosing would make which rule a walker follows a matter of
    /// how wide that reach was.
    /// </remarks>
    public static bool InTheCarriageway(RoadGraph roads, Vector2 standingM)
    {
        var lane = roads.NearestLane(standingM, out var alongM);
        if (lane < 0) return false;

        var on = Spline.SampleAt(roads.ArcsOf(lane), alongM);
        return MathF.Abs(Vector2.Dot(standingM - on.PositionM, on.Right)) <= roads.LaneWidthM[lane] * 0.5f;
    }

    /// <summary>
    /// The next place this body lurches to, and whether it takes it at all.
    /// </summary>
    /// <param name="facing">Which way the body is pointing, which is what keeps the lurch going one way down the road.</param>
    public static Lurch NextLurch(
        SimConfig config, RoadGraph roads, LaneOccupancy book, int person, Vector2 fromM, Vector2 facing,
        float radiusM, ref Rng draw, out Vector2 goalM)
    {
        goalM = fromM;

        var lane = roads.NearestLane(fromM, out var alongM);
        if (lane < 0) return Lurch.NoRoad;

        var arcs = roads.ArcsOf(lane);
        var at = Spline.SampleAt(arcs, alongM);

        // <b>The nearest lane is nearest however far off it is</b>, so how far off has to be asked: a body
        // knocked onto the verge staggers back into the road it came out of, and one thrown clear of the
        // road altogether is somebody the ordinary wander carries. The reach is a road's width, which is the
        // same one a body beside a road is found by (<see cref="StepOut.BesideARoad"/>).
        if ((at.PositionM - fromM).Length() > config.RoadWidthM) return Lurch.NoRoad;

        if (Vector2.Dot(at.Direction, facing) < 0f)
        {
            var back = roads.LaneReverse[lane];
            if (back < 0) return Lurch.NoRoad;

            lane = back;
            alongM = Spline.ProjectM(roads.ArcsOf(lane), fromM, roads.LaneLengthM[lane] * 0.5f, roads.LaneLengthM[lane]);
            arcs = roads.ArcsOf(lane);
            at = Spline.SampleAt(arcs, alongM);
        }

        var acrossM = MathF.Max(0f, (roads.LaneWidthM[lane] * 0.5f) - radiusM);
        var strideM = MathF.Min(config.Person.WalkSpeedMps * config.Person.LurchS, ChordM(at.Curvature, acrossM));
        var claimM = radiusM * config.Person.RoadClaimMargin;

        // A body and not a reservation: a car that has stopped, a wreck, somebody else reeling down the same
        // lane.
        if (book.AheadBody(
                book.WayOfLane(lane), alongM, alongM + strideM + claimM, person, out _, LaneRoster.Walking))
        {
            return Lurch.NoRoom;
        }

        var on = Spline.SampleAt(arcs, MathF.Min(alongM + strideM, roads.LaneLengthM[lane]));
        goalM = on.PositionM + (on.Right * draw.NextFloat(-acrossM, acrossM));
        return Lurch.Taken;
    }

    /// <summary>
    /// <b>How long a lurch may be before the straight line to its far end leaves the lane</b>: a body walks
    /// at what is in front of it and not along the road, so a bend is cut across by whatever the chord sags
    /// away from the arc — <c>s²/8R</c> — and a lurch down a hairpin taken at its full stride puts the body
    /// on the wrong side of the road with a car coming round it.
    /// </summary>
    /// <remarks>
    /// <b>It is the corner formula and not a figure</b>: what a bend of radius R affords is
    /// <c>sqrt(8·R·sag)</c>, the same relation that says what a car may take that bend at. A straight
    /// affords the whole stride, which is what the infinity is.
    /// </remarks>
    static float ChordM(float curvature, float sagM)
    {
        var bend = MathF.Abs(curvature);
        return bend < 1e-4f ? float.PositiveInfinity : MathF.Sqrt(8f * sagM / bend);
    }
}
