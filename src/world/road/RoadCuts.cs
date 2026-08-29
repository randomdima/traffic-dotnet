using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>Where one junction's disc takes a bite out of one chain: the node, and the two distances along the chain.</summary>
internal readonly record struct RoadCut(int Junction, float EnterM, float ExitM);

/// <summary>
/// Where a line running along a road is cut by the junctions that road passes through — <b>which is what
/// makes a lane the stretch between two junction discs rather than a whole road</b>, and what makes an
/// inline junction a place the graph has heard of.
/// </summary>
/// <remarks>
/// <b>Both networks are cut by this one piece of arithmetic</b>, which is why it stands apart from
/// <see cref="RoadGraph"/>: the carriageway's lanes against the discs themselves, and the pavement's
/// strips against those discs read out by half a walk. So a stretch of road and the two stretches of
/// pavement beside it are interrupted by the same junctions, and a change to how a junction bites can
/// only move all three together. What the padding is <em>not</em> is the kerb — <b>a junction's disc is
/// not its kerb</b>, and where a pavement actually stops is the fillet the plan carries, which stands
/// well outside the padded disc and which the walking network pushes its strips back to.
/// </remarks>
internal static class RoadCuts
{
    /// <summary>How finely a chain is walked when looking for the discs it passes through.</summary>
    /// <remarks>
    /// A quarter metre is the road tolerance, and the cut it produces is refined off it by
    /// bisection — so the error in where a stretch starts is a millimetre and not a sample.
    /// </remarks>
    const float SampleStepM = 0.25f;

    const int BisectionRounds = 12;

    public static BucketGrid JunctionIndex(CityPlan plan, float paddingM)
    {
        var junctions = plan.Junctions;
        var bucketM = 0f;
        foreach (var radiusM in junctions.RadiusM) bucketM = MathF.Max(bucketM, radiusM + paddingM);

        return BucketGrid.Build(plan.WorldSizeM, MathF.Max(bucketM, plan.CellSizeM), junctions.CentreM, junctions.RadiusM);
    }

    /// <summary>
    /// The discs one chain passes through, in the order it meets them, with the road's own two ends
    /// guaranteed to be the first and the last — <b>a road runs between two named intersections and
    /// touches no third</b> (TER-4), so anything found between them is a junction laid <em>on</em> the
    /// road and the two ends are what they were declared to be even where the geometry is untidy.
    /// </summary>
    /// <param name="paddingM">
    /// How much bigger than its own radius each disc is taken to be. Zero for a carriageway; half a walk
    /// for a pavement strip, whose stretch ends where it meets the walked circle round the junction and
    /// not where the kerb does.
    /// </param>
    /// <param name="alsoAt">
    /// Places on the chain a slice above has asked for a node of its own — the ends of a parking section
    /// (<see cref="ParkingSections"/>). <b>A cut and not a disc</b>: it takes no ground off the road, so the
    /// two stretches it makes meet at a point and the movement between them is a join of no length.
    /// </param>
    /// <param name="shortestStretchM">
    /// How much road one of those has to leave standing on either side of itself to be taken. A cut that
    /// leaves less is dropped and the section keeps the node its road already ends at, because a stretch
    /// too short to drive is worse than one node fewer.
    /// </param>
    public static void Along(
        CityPlan plan, BucketGrid discs, ReadOnlySpan<ArcSeg> chain, float lengthM, float paddingM,
        int fromJunction, int toJunction, List<RoadCut> into, ReadOnlySpan<SectionCut> alsoAt = default,
        float shortestStretchM = 0f)
    {
        into.Clear();

        // Every junction in the town: a query truncated to a fixed buffer is a superset silently made
        // a subset (BucketGrid), and the one thing this must not miss is a disc a road passes through.
        var nearby = new int[Math.Max(1, plan.Junctions.Count)];
        var junctions = plan.Junctions;

        var seen = new Dictionary<int, (float FirstM, float LastM)>();
        for (var stepM = 0f; stepM <= lengthM; stepM += SampleStepM)
        {
            var alongM = MathF.Min(stepM, lengthM);
            var pointM = Spline.SampleAt(chain, alongM).PositionM;
            var found = discs.Query(pointM, paddingM, nearby);
            for (var slot = 0; slot < found; slot++)
            {
                var junction = nearby[slot];
                if (!Inside(junctions, junction, paddingM, pointM)) continue;

                seen[junction] = seen.TryGetValue(junction, out var span)
                    ? (span.FirstM, alongM)
                    : (alongM, alongM);
            }
        }

        foreach (var (junction, span) in seen)
        {
            var enterM = Refine(junctions, junction, paddingM, chain, span.FirstM - SampleStepM, span.FirstM);
            var exitM = Refine(junctions, junction, paddingM, chain, span.LastM + SampleStepM, span.LastM);
            into.Add(new RoadCut(junction, enterM, exitM));
        }

        // The two named ends bound the run whatever the sampling found: a road whose first metre lies
        // outside its own disc still leaves that junction, and one that never enters the disc it ends
        // at still arrives there.
        into.RemoveAll(cut => cut.Junction == fromJunction || cut.Junction == toJunction);
        into.Sort(static (left, right) => left.EnterM.CompareTo(right.EnterM));
        into.Insert(0, new RoadCut(fromJunction, 0f, EndCutM(plan, chain, fromJunction, paddingM, 0f, lengthM, forward: true)));
        into.Add(new RoadCut(toJunction, EndCutM(plan, chain, toJunction, paddingM, lengthM, lengthM, forward: false), lengthM));

        // Taken in the order they stand, so a pair of them too close together drops the second and not
        // whichever the loop happened to reach first. <b>Where a cut may stand is the asker's</b> — it is
        // the only one that knows what it wanted the node for — so this is the backstop and not the rule:
        // one that would leave a stretch too short to drive goes without.
        foreach (var cut in alsoAt)
        {
            var slot = into.FindIndex(other => other.EnterM > cut.AlongM);
            if (slot <= 0) continue;

            if (cut.AlongM - into[slot - 1].ExitM < shortestStretchM) continue;
            if (into[slot].EnterM - cut.AlongM < shortestStretchM) continue;

            into.Insert(slot, new RoadCut(cut.Node, cut.AlongM, cut.AlongM));
        }
    }

    /// <summary>
    /// How many road arms meet at each junction — one for a road that starts or stops there, two for a
    /// road that runs through it. <b>A junction with exactly one arm is a dead end</b>, which is the only
    /// place a pavement runs round a head rather than round a kerb corner.
    /// </summary>
    /// <remarks>
    /// It is asked of the plan and never of what a derivation had left standing: a junction whose other
    /// arms are stretches too short to carry a line still has those arms, and a construction that counted
    /// what survived would lay a turning head across the mouth of a four-armed crossroads.
    /// </remarks>
    public static int[] ArmsPerJunction(CityPlan plan)
    {
        var roads = plan.Roads;
        var discs = JunctionIndex(plan, paddingM: 0f);
        var arms = new int[plan.Junctions.Count];
        var cuts = new List<RoadCut>();

        for (var road = 0; road < roads.Count; road++)
        {
            var centreline = roads.SegmentsOf(road);
            if (centreline.Length == 0) continue;

            Along(
                plan, discs, centreline, Spline.TotalLengthM(centreline), paddingM: 0f,
                roads.FromJunction[road], roads.ToJunction[road], cuts);
            for (var cut = 0; cut < cuts.Count; cut++)
            {
                arms[cuts[cut].Junction] += cut == 0 || cut == cuts.Count - 1 ? 1 : 2;
            }
        }

        return arms;
    }

    /// <summary>How far into its own end junction a chain reaches, which is where the stretch on it starts or stops.</summary>
    static float EndCutM(
        CityPlan plan, ReadOnlySpan<ArcSeg> chain, int junction, float paddingM, float fromM, float lengthM, bool forward)
    {
        var junctions = plan.Junctions;
        var stepM = forward ? SampleStepM : -SampleStepM;
        var alongM = fromM;
        while (alongM >= 0f && alongM <= lengthM && Inside(junctions, junction, paddingM, Spline.SampleAt(chain, alongM).PositionM))
        {
            alongM += stepM;
        }

        if (alongM == fromM) return fromM;

        return Math.Clamp(Refine(junctions, junction, paddingM, chain, alongM, alongM - stepM), 0f, lengthM);
    }

    static bool Inside(CityPlan.JunctionArrays junctions, int junction, float paddingM, Vector2 pointM)
    {
        var reachM = junctions.RadiusM[junction] + paddingM;
        return Vector2.DistanceSquared(junctions.CentreM[junction], pointM) <= reachM * reachM;
    }

    /// <summary>The crossing of the disc's edge, bisected between a distance known to be outside it and one known to be inside.</summary>
    static float Refine(
        CityPlan.JunctionArrays junctions, int junction, float paddingM, ReadOnlySpan<ArcSeg> chain,
        float outsideM, float insideM)
    {
        for (var round = 0; round < BisectionRounds; round++)
        {
            var middleM = (outsideM + insideM) * 0.5f;
            if (Inside(junctions, junction, paddingM, Spline.SampleAt(chain, middleM).PositionM)) insideM = middleM;
            else outsideM = middleM;
        }

        return insideM;
    }
}
