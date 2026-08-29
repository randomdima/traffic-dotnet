using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>How far a body may drive down geometry of its own before the ground under it is somebody else's.</b>
/// The road's book holds everything as a stretch of a way, and a manoeuvre's template is laid over no way
/// at all — so the ground under each point of it is looked up, and what the book says about that ground is
/// the answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>The book and not the geometry.</b> A cast finds a shape and cannot say whose the ground is; what a
/// car swinging out of a bay has to know is whether what it is about to occupy is inside somebody's road,
/// which is a fact no geometry carries — a body is visible to a cast and the reservation in front of it is
/// not, so a swerve read off shapes alone lands in ground a car three seconds away is committed to.
/// </para>
/// <para>
/// <b>Walked at the body's own width and stepped rather than swept.</b> A template is a dozen metres of
/// bend at manoeuvring pace, so the step is a fraction of a body and the walk is a handful of samples —
/// against three ray chains and a tree descent apiece.
/// </para>
/// <para>
/// <b>A point over no lane is clear and not blocked.</b> A bay stands off the kerb and a recovery straight
/// runs over a verge; ground the network never had is ground nobody can have reserved, and the terrain rule
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
    public static float ClearM(
        RoadGraph roads, LaneOccupancy book, scoped ReadOnlySpan<ArcSeg> line, float fromM, float reachM,
        float halfWidthM, int car)
    {
        Span<WayUnder> under = stackalloc WayUnder[GroundUnder.MostWaysUnderAPlace(roads)];

        // One cursor for the whole walk: the distances only ever go forwards, and a template is a chain of
        // arcs that would otherwise be counted from its head at every sample.
        var cursor = default(SplineCursor);
        for (var alongM = 0f; alongM < reachM; alongM += StepM)
        {
            var atM = Spline.SampleFrom(line, fromM + MathF.Min(alongM, reachM), ref cursor).PositionM;
            if (TakenAt(roads, book, atM, halfWidthM, car, under, out _)) return alongM;
        }

        return reachM;
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
        RoadGraph roads, LaneOccupancy book, Vector2 atM, float halfWidthM, int car, out LaneSlot found)
    {
        Span<WayUnder> under = stackalloc WayUnder[GroundUnder.MostWaysUnderAPlace(roads)];
        return TakenAt(roads, book, atM, halfWidthM, car, under, out found);
    }

    /// <summary>The same question asked with the caller's own room for the walk, which is what a sweep down a line wants.</summary>
    public static bool TakenAt(
        RoadGraph roads, LaneOccupancy book, Vector2 atM, float halfWidthM, int car, Span<WayUnder> under,
        out LaneSlot found)
    {
        found = LaneSlot.Nothing;

        var count = GroundUnder.At(roads, book, atM, halfWidthM, halfWidthM, under);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref under[index];
            if (book.SpokenForByAnother(
                    way.Way, way.AlongM - halfWidthM, way.AlongM + halfWidthM, car, out found))
            {
                return true;
            }
        }

        return false;
    }
}
