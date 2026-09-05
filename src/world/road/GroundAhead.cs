using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>How far a body may drive down geometry of its own before the ground under it is somebody else's.</b>
/// The road's claims hold everything as a stretch of a way, and a manoeuvre's template is laid over no way
/// at all — so the ground under each point of it is looked up, and what the claims say about that ground is
/// the answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>The claims and not the geometry.</b> A cast finds a shape and cannot say whose the ground is; what a
/// car swinging out of a bay has to know is whether what it is about to occupy is inside somebody's road,
/// which is a fact no geometry carries — a body is visible to a cast and the claim in front of it is
/// not, so a swerve read off shapes alone lands in ground a car three seconds away is committed to.
/// </para>
/// <para>
/// <b>Walked at the body's own width and stepped rather than swept.</b> A template is a dozen metres of
/// bend at manoeuvring pace, so the step is a fraction of a body and the walk is a handful of samples —
/// against three ray chains and a tree descent apiece.
/// </para>
/// <para>
/// <b>A point over no lane is clear and not blocked.</b> A bay stands off the kerb and a recovery straight
/// runs over a verge; ground the network never had is ground nobody can have claimed, and the terrain rule
/// (<c>OnDrivableGround</c>) is what says whether a body may be there at all.
/// </para>
/// </remarks>
internal static class GroundAhead
{
    /// <summary>How finely a candidate is walked. A quarter of a body, which is the offset the follower is held to.</summary>
    const float StepM = 1f;

    /// <summary>
    /// How much of <paramref name="reachM"/> ahead of <paramref name="fromM"/> is nobody else's, walking the
    /// line from its near end — so the answer is the first stretch that is taken and never the nearest.
    /// </summary>
    /// <remarks>
    /// <b>The far end is walked and not left off it.</b> The step lands on it only where the reach is a whole
    /// number of steps, and the last metre of a template is ground like every metre before it — a body that
    /// comes to rest inside it is one nothing was ever asked about. Where that end is what is taken, the
    /// answer is the last step that was clear, which is as fine as a stepped walk can say.
    /// </remarks>
    public static float ClearM(
        RoadGraph roads, LaneOccupancy claims, scoped ReadOnlySpan<ArcSeg> line, float fromM, float reachM,
        float halfWidthM, int car)
    {
        Span<WayUnder> under = stackalloc WayUnder[roads.Ways.MostWaysUnderAPlace];
        var body = BodyFootprint.Round(halfWidthM);

        // One cursor for the whole walk: the distances only ever go forwards, and a template is a chain of
        // arcs that would otherwise be counted from its head at every sample.
        var cursor = default(SplineCursor);
        var alongM = 0f;
        for (; alongM < reachM; alongM += StepM)
        {
            var atM = Spline.SampleFrom(line, fromM + alongM, ref cursor).PositionM;
            if (TakenAt(roads, claims, atM, body, car, under, out _)) return alongM;
        }

        var endM = Spline.SampleFrom(line, fromM + reachM, ref cursor).PositionM;
        return TakenAt(roads, claims, endM, body, car, under, out _)
            ? MathF.Max(0f, alongM - StepM)
            : reachM;
    }

    /// <summary>
    /// Whether the ground at one place is inside somebody else's stretch of any way that place stands on.
    /// </summary>
    /// <remarks>
    /// <b>Every way under it and not the nearest lane alone</b> (<see cref="GroundUnder"/>). A car crossing a
    /// junction writes its road onto the <em>join</em> it is crossing on and onto no lane at all (TER-5c.1),
    /// so a template asking only the lane nearest each of its samples is a manoeuvre that cannot see a single
    /// car in the box it is swinging through.
    /// </remarks>
    public static bool TakenAt(
        RoadGraph roads, LaneOccupancy claims, Vector2 atM, float halfWidthM, int car, out LaneClaim found)
    {
        Span<WayUnder> under = stackalloc WayUnder[roads.Ways.MostWaysUnderAPlace];
        return TakenAt(roads, claims, atM, BodyFootprint.Round(halfWidthM), car, under, out found);
    }

    /// <summary>
    /// And the same question asked for a body rather than for a step of a walk — <b>the shape whose ground it
    /// actually is</b>, which is what a caller holding one pose rather than a line down to it wants.
    /// </summary>
    /// <remarks>
    /// <b>A round footprint is a step and a box is a body</b>, and the two do not cover the same ground: a box
    /// standing at an angle reaches a way at one corner, which is metres from where a circle at the same
    /// middle reaches it (<see cref="BodyFootprint.CoversOn"/>). Asking about the ground a body will be on
    /// with a circle where the body is a box is asking about a different piece of road.
    /// </remarks>
    public static bool TakenAt(
        RoadGraph roads, LaneOccupancy claims, Vector2 atM, in BodyFootprint body, int car,
        Span<WayUnder> under, out LaneClaim found)
    {
        found = LaneClaim.Nothing;

        // <b>Every way the sample overlaps</b>, which is the same walk a body is written onto
        // (<see cref="RoadGraph.WithinTheBand"/>) — a template asking where it may go asks whose the ground
        // is, and it must not ask of a narrower set of ways than the bodies were written to.
        var count = GroundUnder.At(roads.Ways, atM, body, crossesByM: 0f, under);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref under[index];
            if (claims.SpokenForByAnother(
                    way.Way, way.AlongM + way.BackM, way.AlongM + way.AheadM, car, out found))
            {
                return true;
            }
        }

        return false;
    }
}
