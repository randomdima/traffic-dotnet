using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>Where one junction's reach takes a bite out of one chain: the node, and the two distances along the chain.</summary>
internal readonly record struct RoadCut(int Junction, float EnterM, float ExitM);

/// <summary>
/// Where a line running along a road is cut by the junctions that road passes through — <b>which is what
/// makes a lane the stretch between two junctions rather than a whole road</b>, and what makes an
/// inline junction a place the network has heard of.
/// </summary>
/// <remarks>
/// <para>
/// <b>The circle it bites with is a planning figure and not a piece of ground</b> (TER-5). A junction has
/// no shape: what a box is made of is the lines its own movements sweep (<see cref="LaneLines"/>), and this
/// is only how far back the arms are cut so those lines have room to be drawn. Nothing here says what is on
/// the ground anywhere.
/// </para>
/// <para>
/// <b>Both networks are cut by this one piece of arithmetic</b>: the carriageway's lanes against the radii
/// themselves, and the pavement's strips against those radii read out by half a walk. So a stretch of road
/// and the two stretches of pavement beside it are interrupted by the same junctions, and a change to how a
/// junction bites can only move all three together. What the padding is <em>not</em> is the kerb, and where
/// a pavement actually stops is the fillet the plan carries, which stands well outside the padded circle
/// and which the walking network pushes its strips back to.
/// </para>
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

    public static BucketGrid JunctionIndex(GroundPieces ground, float paddingM)
    {
        var junctions = ground.Junctions;
        var bucketM = 0f;
        foreach (var radiusM in junctions.RadiusM) bucketM = MathF.Max(bucketM, radiusM + paddingM);

        // A map with no junctions on it has nothing to bin, and one bucket over the whole town is the index
        // that says so. A bucket has a size, and there is no junction here to take one off.
        if (bucketM <= 0f) bucketM = MathF.Max(ground.WorldSizeM.X, ground.WorldSizeM.Y);

        return BucketGrid.Build(ground.WorldSizeM, bucketM, junctions.CentreM, junctions.RadiusM);
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
        GroundPieces ground, BucketGrid discs, ReadOnlySpan<ArcSeg> chain, float lengthM, float paddingM,
        int fromJunction, int toJunction, List<RoadCut> into, ReadOnlySpan<SectionCut> alsoAt = default,
        float shortestStretchM = 0f)
    {
        into.Clear();

        // Every junction in the town: a query truncated to a fixed buffer is a superset silently made
        // a subset (BucketGrid), and the one thing this must not miss is a disc a road passes through.
        var nearby = new int[Math.Max(1, ground.Junctions.Count)];
        var junctions = ground.Junctions;

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
        into.Insert(0, new RoadCut(fromJunction, 0f, EndCutM(ground, chain, fromJunction, paddingM, 0f, lengthM, forward: true)));
        into.Add(new RoadCut(toJunction, EndCutM(ground, chain, toJunction, paddingM, lengthM, lengthM, forward: false), lengthM));

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
    public static int[] ArmsPerJunction(GroundPieces ground)
    {
        var roads = ground.Roads;
        var discs = JunctionIndex(ground, paddingM: 0f);
        var arms = new int[ground.Junctions.Count];
        var cuts = new List<RoadCut>();

        for (var road = 0; road < roads.Count; road++)
        {
            var centreline = roads.SegmentsOf(road);
            if (centreline.Length == 0) continue;

            Along(
                ground, discs, centreline, Spline.TotalLengthM(centreline), paddingM: 0f,
                roads.FromJunction[road], roads.ToJunction[road], cuts);
            for (var cut = 0; cut < cuts.Count; cut++)
            {
                arms[cuts[cut].Junction] += cut == 0 || cut == cuts.Count - 1 ? 1 : 2;
            }
        }

        return arms;
    }

    /// <summary>
    /// <b>How far each junction's own ground reaches along the arms that meet it</b> (TER-5): the kerb fillet
    /// between an arm and its neighbour lets go of the kerb there, and the furthest of a junction's corners is
    /// how much road the box has taken. Never less than the disc it is drawn on, never more than the sharpest
    /// corner a junction may turn.
    /// </summary>
    /// <remarks>
    /// <b>Read off <see cref="SimConfig.JunctionArmReachM"/> and never re-derived</b> — it is the same answer
    /// the stage that laid the town set its crossings back from (TER-6). What wants it here is everything an
    /// arm has to keep off that ground with no paint to measure against: a lane line with no bar or crossing
    /// on its arm, and the node a car park asks to be cut at.
    /// </remarks>
    public static float[] ReachesM(GroundPieces ground, SimConfig config)
    {
        var bearings = new List<(float Rad, float HalfM, float StandsOffM)>[ground.Junctions.Count];
        var bendM = new float[bearings.Length];
        for (var junction = 0; junction < bearings.Length; junction++) bearings[junction] = [];

        for (var road = 0; road < ground.Roads.Count; road++)
        {
            var chain = ground.Roads.SegmentsOf(road);
            if (chain.Length == 0) continue;

            // <b>Each arm with its own half beside it, and with however far off the node it stands</b>:
            // where the kerbs of two arms cross is a fact about both their widths, and a one-way street is
            // half a road wide and stands on the half of that road it is driven (TER-4d).
            var halfM = ground.Roads.WidthM[road] * 0.5f;
            var from = Spline.SampleAt(chain, 0f);
            var to = Spline.SampleAt(chain, Spline.TotalLengthM(chain));
            var outOfFrom = from.Direction;
            var outOfTo = -to.Direction;
            bearings[ground.Roads.FromJunction[road]].Add((
                MathF.Atan2(outOfFrom.Y, outOfFrom.X), halfM,
                StandsOffM(from.PositionM - ground.Junctions.CentreM[ground.Roads.FromJunction[road]], outOfFrom)));
            bearings[ground.Roads.ToJunction[road]].Add((
                MathF.Atan2(outOfTo.Y, outOfTo.X), halfM,
                StandsOffM(to.PositionM - ground.Junctions.CentreM[ground.Roads.ToJunction[road]], outOfTo)));

            // A node with no fork has no corner to flare, and the ground it takes is the bend the two arms
            // were swept into instead (TER-5b) — which is as much of that arm as anything must stand off.
            bendM[ground.Roads.FromJunction[road]] =
                MathF.Max(bendM[ground.Roads.FromJunction[road]], Spline.BendAtTheEndM(chain, atStart: true));
            bendM[ground.Roads.ToJunction[road]] =
                MathF.Max(bendM[ground.Roads.ToJunction[road]], Spline.BendAtTheEndM(chain, atStart: false));
        }

        var reachM = new float[bearings.Length];
        for (var junction = 0; junction < bearings.Length; junction++)
        {
            var round = bearings[junction];
            reachM[junction] = round.Count == 2
                ? MathF.Max(ground.Junctions.RadiusM[junction], bendM[junction])
                : ground.Junctions.RadiusM[junction];

            if (round.Count < 2) continue;

            round.Sort();
            for (var at = 0; at < round.Count; at++)
            {
                var next = (at + 1) % round.Count;
                var apartRad = round[next].Rad - round[at].Rad;
                if (apartRad <= 0f) apartRad += MathF.Tau;

                // The neighbour lies to the right of the first arm of the pair and to the left of the
                // second, so a road standing off the node moves one kerb into the wedge and the other out.
                var kerbAtM = round[at].HalfM + round[at].StandsOffM;
                var kerbNextM = round[next].HalfM - round[next].StandsOffM;
                if (!config.JunctionTurnsACorner(apartRad, kerbAtM, kerbNextM)) continue;

                // The corner reaches each of its two arms differently where they are not the same width, and
                // what the junction has taken is the further of them.
                var cornerM = MathF.Max(
                    config.JunctionArmReachM(apartRad, kerbAtM, kerbNextM),
                    config.JunctionArmReachM(apartRad, kerbNextM, kerbAtM));
                reachM[junction] = MathF.Max(
                    reachM[junction], MathF.Min(config.JunctionArmReachMaxM, cornerM));
            }
        }

        return reachM;
    }

    /// <summary>
    /// <b>The junctions a road runs through as one line</b>, one answer per junction: two arms and no fork
    /// (TER-5b), and where two roads end there, the kerbs of the one stand where the kerbs of the other do.
    /// A disc laid on a road nothing ends at is such a place by definition.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the kerb corners and not of the node</b>: two ends at one place, facing opposite ways at
    /// one width, are four corners standing pairwise where the other end's stand, and the figure they are
    /// held to is the one that makes two pieces one line (<see cref="Kerbs.JoinedM"/>). A bend the roads
    /// were swept into meets exactly; a node left unswept because its deflection was under the sweep's
    /// notice creases the kerb by less than that; two arms of different widths, or a pair with room for
    /// nothing better than the fillet (GEN-12a), do not meet and keep their junction.
    /// </remarks>
    public static bool[] RunsThrough(GroundPieces ground)
    {
        var arms = ArmsPerJunction(ground);
        var ends = new List<(Vector2 PlaceM, Vector2 Outward, float HalfM)>[arms.Length];
        for (var junction = 0; junction < ends.Length; junction++) ends[junction] = [];

        var roads = ground.Roads;
        for (var road = 0; road < roads.Count; road++)
        {
            var arcs = roads.SegmentsOf(road);
            if (arcs.Length == 0) continue;

            var last = arcs[^1];
            var halfM = roads.WidthM[road] * 0.5f;
            ends[roads.FromJunction[road]].Add((arcs[0].StartM, arcs[0].StartUnit, halfM));
            ends[roads.ToJunction[road]].Add((last.EndM, -Heading.Unit(last.HeadingAtRad(last.LengthM)), halfM));
        }

        var through = new bool[arms.Length];
        for (var junction = 0; junction < through.Length; junction++)
        {
            if (arms[junction] != 2) continue;

            var at = ends[junction];
            if (at.Count == 0)
            {
                through[junction] = true;
                continue;
            }

            if (at.Count != 2) continue;

            var (placeA, outA, halfA) = at[0];
            var (placeB, outB, halfB) = at[1];
            var acrossA = Heading.RightOf(outA) * halfA;
            var acrossB = Heading.RightOf(outB) * halfB;
            through[junction] =
                Vector2.Distance(placeA + acrossA, placeB - acrossB) <= Kerbs.JoinedM
                && Vector2.Distance(placeA - acrossA, placeB + acrossB) <= Kerbs.JoinedM;
        }

        return through;
    }

    /// <summary>How far off the node an arm's own line stands, to the right of the way out along it.</summary>
    static float StandsOffM(Vector2 offTheNodeM, Vector2 outward) =>
        Vector2.Dot(offTheNodeM, Heading.RightOf(outward));

    /// <summary>How far into its own end junction a chain reaches, which is where the stretch on it starts or stops.</summary>
    static float EndCutM(
        GroundPieces ground, ReadOnlySpan<ArcSeg> chain, int junction, float paddingM, float fromM, float lengthM, bool forward)
    {
        var junctions = ground.Junctions;
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
