using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>What a junction carries once its arms are known</b>: the kerb fillets between them, a crossing on
/// every arm (TER-6) — or one for both arms where the junction forks nothing — and a stop bar on every arm
/// of every junction that is lit or does not fork.
/// </summary>
/// <remarks>
/// <para>
/// <b>The paint is laid across a straight arm</b>, which is the reason a road's two ends are straight pieces
/// (<see cref="RoadStage"/>). An arm short enough that its own crossing would stand past the middle of the
/// road carries none: two junctions that close carry one box between them, and paint struck halfway down a
/// road nobody stops on is paint a walker is sent to and a driver never sees.
/// </para>
/// <para>
/// <b>The kerbs are not.</b> An arm may leave a junction on a bend — a piece of the orbital, and every piece
/// of a roundabout's ring (GEN-19) — so a corner is struck between the shapes the two kerbs are actually
/// drawn along and never between the lines their bearings make (<see cref="Kerb"/>). Struck on the lines, a
/// ring's entries were filleted to points a metre and a half off their own tarmac.
/// </para>
/// </remarks>
internal static class Furniture
{
    /// <summary>How much of a road may lie between a junction and the paint on its arm before there is no room for it.</summary>
    const float PaintWithinShare = 0.45f;

    /// <summary>Neither arm of a junction with no fork has the room for its bundle, so it carries no paint at all.</summary>
    const int NoArm = -1;

    internal readonly record struct Laid(
        CityPlan.JunctionCornerArrays Corners,
        CityPlan.CrosswalkArrays Crosswalks,
        CityPlan.StopLineArrays StopLines);

    /// <summary>
    /// One road as it leaves a junction: which road, which of its ends, the way out along it, how far
    /// the road's own line stands to the right of that way out — half a lane on a one-way street, which
    /// stands on the half of the carriageway it is driven (TER-4d), and nothing on any other road — and
    /// <b>how it bends there, reckoned along the way out</b> and so signed against that rather than against
    /// the way the road happens to be written.
    /// </summary>
    readonly record struct Arm(int Road, bool AtFromEnd, float BearingRad, float StandsOffM, float Curvature);

    /// <summary>
    /// <b>One arm's kerb on the side of the wedge it makes with its neighbour</b>, as the shape it is drawn
    /// along: the line of an arm that leaves straight, and the circle of one that leaves on a bend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A fillet is then one construction rather than three</b>: the arc tangent to both kerbs is centred
    /// where the two of them, each moved its own radius into the wedge, meet, and it touches each at that
    /// kerb's nearest point to the centre (<see cref="Offset"/>, <see cref="Touches"/>). Only
    /// <see cref="Meet"/> knows which of the two shapes a kerb is.
    /// </para>
    /// <para>
    /// <see cref="Side"/> is which way the wedge lies about the circle — inside it or outside — and is nil
    /// for a straight; <see cref="ArmRadiusM"/> is the arm's own centreline radius, which is what turns an
    /// angle about the circle into the distance along the road that the junction's ground reaches.
    /// </para>
    /// </remarks>
    readonly record struct Kerb(
        Vector2 AtM, Vector2 Along, Vector2 Into, Vector2 ArcCentreM, float ArcRadiusM, float Side,
        float ArmRadiusM)
    {
        public bool Curved => Side != 0f;
    }

    /// <summary>
    /// The fillets a junction's corners are turned on, and <b>how far along each of its arms the junction's
    /// own ground therefore reaches</b> — which is what the paint on that arm is set back from, arm by arm
    /// and junction by junction. <see cref="ReachM"/> is the further of the arm's two corners, which is
    /// where the junction's ground stops; <see cref="NearM"/> is the nearer, which is what the paint stands
    /// its stride past. One figure where the arm turns one corner or none.
    /// </summary>
    readonly record struct Mouths(CityPlan.JunctionCornerArrays Corners, float[][] ReachM, float[][] NearM);

    public static Laid Lay(
        TownLayout layout, ArcSeg[][] chains, CityPlan.JunctionArrays junctions, SimConfig config, float[] widthM)
    {
        var arms = ArmsOf(layout, chains, junctions.CentreM);
        var mouths = Corners(chains, junctions, arms, config, widthM);
        var through = Throughs(chains, arms, config, mouths.ReachM);

        // <b>A crossing is the width of the carriageway it crosses</b>, kerb to kerb, and it is drawn to
        // the width of the road it names rather than to a span of its own (TER-6) — so a zebra whose end
        // bars stand on the pavement cannot be laid at all, and the ground under the paint is a stretch of
        // that same road rather than a rectangle anybody has to reconcile with it.
        var crossings = Crossings(layout, chains, arms, config, mouths.ReachM, mouths.NearM, through);
        var bars = Bars(layout, chains, arms, config, widthM, mouths.ReachM, mouths.NearM, through);
        return new Laid(mouths.Corners, crossings, bars);
    }

    static List<Arm>[] ArmsOf(TownLayout layout, ArcSeg[][] chains, Vector2[] centreM)
    {
        var arms = new List<Arm>[centreM.Length];
        for (var junction = 0; junction < centreM.Length; junction++) arms[junction] = [];

        for (var road = 0; road < chains.Length; road++)
        {
            if (chains[road].Length == 0) continue;

            var edge = layout.Edges[road];
            var start = Spline.SampleAt(chains[road], 0f);
            var end = Spline.SampleAt(chains[road], Spline.TotalLengthM(chains[road]));

            // Signed against the way out and not against the way the road is written, so an arm that bends
            // the same way at both its ends carries the same figure at both of them.
            arms[edge.From].Add(new Arm(
                road, true, RoadStage.Facing(start.Direction),
                StandsOffM(start.PositionM - centreM[edge.From], start.Direction), chains[road][0].Curvature));
            arms[edge.To].Add(new Arm(
                road, false, RoadStage.Facing(-end.Direction),
                StandsOffM(end.PositionM - centreM[edge.To], -end.Direction), -chains[road][^1].Curvature));
        }

        foreach (var junction in arms) junction.Sort((a, b) => Wrapped(a.BearingRad).CompareTo(Wrapped(b.BearingRad)));
        return arms;
    }

    /// <summary>How far off the node an arm's own line stands, to the right of the way out along it.</summary>
    static float StandsOffM(Vector2 offTheNodeM, Vector2 outward) =>
        Vector2.Dot(offTheNodeM, Heading.RightOf(outward));

    /// <summary>
    /// The kerb fillet between each pair of arms that stand next to each other round a junction: the arc
    /// tangent to both carriageways, which is the ground a turning car takes (TER-5).
    /// </summary>
    /// <remarks>
    /// <b>Every corner a junction turns is turned</b>, and which pairs those are is read off the geometry
    /// rather than off the order the arms happen to be sorted in. Two kerbs meet at a corner only where
    /// the arms stand on the near side of a straight line: past that they run apart and the junction's
    /// own mouth is already tangent to both. What is left unfilleted is a spike of pavement standing in
    /// the carriageway — the ground a car turns across, classified as somewhere to walk.
    /// </remarks>
    static Mouths Corners(
        ArcSeg[][] chains, CityPlan.JunctionArrays junctions, List<Arm>[] arms, SimConfig config, float[] widthM)
    {
        var cornerM = new List<Vector2>();
        var arcCentreM = new List<Vector2>();
        var radiusM = new List<float>();
        var tangentAM = new List<Vector2>();
        var tangentBM = new List<Vector2>();

        var reachM = new float[arms.Length][];
        var nearM = new float[arms.Length][];
        for (var junction = 0; junction < arms.Length; junction++)
        {
            // An arm no corner reaches — one of two that run apart, or of two all but straight through —
            // is reached only by the mouth's own edge. <b>Or by the bend it leaves on</b>, where the
            // two arms of a node with no fork were swept into one curve (<see cref="RoadStage"/>, TER-5b):
            // the paint an arm carries is laid across a straight, and the straight there begins where the
            // arc ends. <b>A node with a fork takes no bend as its ground</b>: an arm that leaves a
            // junction on a long arc — a ring road's — is reached by its corners like any other, and its
            // paint stands on the arc, which at the radius a road is bent on reads square.
            reachM[junction] = new float[arms[junction].Count];
            nearM[junction] = new float[arms[junction].Count];
            for (var at = 0; at < arms[junction].Count; at++)
            {
                var arm = arms[junction][at];
                reachM[junction][at] = arms[junction].Count == 2
                    ? MathF.Max(junctions.RadiusM[junction], Spline.BendAtTheEndM(chains[arm.Road], arm.AtFromEnd))
                    : junctions.RadiusM[junction];
                nearM[junction][at] = float.PositiveInfinity;
            }

            if (arms[junction].Count < 2) continue;

            for (var at = 0; at < arms[junction].Count; at++)
            {
                var next = (at + 1) % arms[junction].Count;
                var apartRad = Wrapped(arms[junction][next].BearingRad - arms[junction][at].BearingRad);

                // <b>Each arm's kerb stands off the node by its own half and by however far its road
                // stands off there</b> (TER-4d) — the neighbour lies to the right of the first of the pair
                // and to the left of the second — so whether the two cross at all, and where, is asked of
                // the pair and not of one width the town shares.
                var kerbAM = (widthM[arms[junction][at].Road] * 0.5f) + arms[junction][at].StandsOffM;
                var kerbBM = (widthM[arms[junction][next].Road] * 0.5f) - arms[junction][next].StandsOffM;
                if (!config.JunctionTurnsACorner(apartRad, kerbAM, kerbBM)) continue;

                // <b>Whether the two kerbs cross is asked of the straight lines their bearings make</b>
                // (above) and <b>where they cross is asked of the shapes they are drawn along</b>: the
                // first is a question about the wedge, which a bend at the node does not change, and the
                // second is a question about the tarmac, which it does.
                var kerbA = KerbOf(junctions.CentreM[junction], arms[junction][at], widthM, 1f);
                var kerbB = KerbOf(junctions.CentreM[junction], arms[junction][next], widthM, -1f);
                if (!Meet(kerbA, kerbB, junctions.CentreM[junction], out var cornerAtM)) continue;

                // The arc tangent to both kerbs is centred where the two of them, each moved its own radius
                // into the wedge, meet — and it touches each at that kerb's own nearest point to the centre.
                var filletM = config.JunctionFilletRadiusM(apartRad);
                if (!Meet(Offset(kerbA, filletM), Offset(kerbB, filletM), cornerAtM, out var filletAtM)) continue;

                var touchesAM = Touches(kerbA, filletAtM);
                var touchesBM = Touches(kerbB, filletAtM);

                cornerM.Add(cornerAtM);
                arcCentreM.Add(filletAtM);
                radiusM.Add(filletM);
                tangentAM.Add(touchesAM);
                tangentBM.Add(touchesBM);

                // Each arm of the corner is reached as far as its own tangent point, and stands off
                // whichever of its own two corners reaches further; its paint stands its stride past
                // whichever reaches less far.
                var alongAM = AlongTheArmM(kerbA, touchesAM);
                var alongBM = AlongTheArmM(kerbB, touchesBM);
                reachM[junction][at] = MathF.Max(reachM[junction][at], alongAM);
                reachM[junction][next] = MathF.Max(reachM[junction][next], alongBM);
                nearM[junction][at] = MathF.Min(nearM[junction][at], alongAM);
                nearM[junction][next] = MathF.Min(nearM[junction][next], alongBM);
            }

            for (var at = 0; at < arms[junction].Count; at++)
            {
                nearM[junction][at] = float.IsPositiveInfinity(nearM[junction][at])
                    ? reachM[junction][at]
                    : MathF.Max(nearM[junction][at], junctions.RadiusM[junction]);
            }
        }

        return new Mouths(
            new CityPlan.JunctionCornerArrays
            {
                CornerM = [.. cornerM], ArcCentreM = [.. arcCentreM], RadiusM = [.. radiusM],
                TangentAM = [.. tangentAM], TangentBM = [.. tangentBM],
            },
            reachM, nearM);
    }

    /// <summary>
    /// One arm's kerb on the side its neighbour lies (<see cref="Kerb"/>). <paramref name="hand"/> is which
    /// of the pair this arm is: the neighbour lies to the right of the first and to the left of the second.
    /// </summary>
    static Kerb KerbOf(Vector2 centreM, Arm arm, float[] widthM, float hand)
    {
        var along = Heading.Unit(arm.BearingRad);
        var right = Heading.RightOf(along);
        var into = right * hand;
        var lineM = centreM + (right * arm.StandsOffM);
        var atM = lineM + (into * widthM[arm.Road] * 0.5f);
        if (arm.Curvature == 0f) return new Kerb(atM, along, into, Vector2.Zero, 0f, 0f, 0f);

        // The arm bends about a centre a radius to its right, so its kerb is the circle about that centre
        // through the kerb's own point — nearer where the wedge is on the inside of the bend and further
        // where it is on the outside, which is the same thing <see cref="Kerb.Side"/> then says.
        var arcCentreM = lineM + (right / arm.Curvature);
        var offTheKerbM = arcCentreM - atM;
        return new Kerb(
            atM, along, into, arcCentreM, offTheKerbM.Length(),
            MathF.Sign(Vector2.Dot(into, offTheKerbM)), MathF.Abs(1f / arm.Curvature));
    }

    /// <summary>The same kerb moved that far into the wedge, which is where a fillet of that radius is centred.</summary>
    static Kerb Offset(Kerb kerb, float byM) =>
        kerb.Curved
            ? kerb with { ArcRadiusM = kerb.ArcRadiusM - (kerb.Side * byM) }
            : kerb with { AtM = kerb.AtM + (kerb.Into * byM) };

    /// <summary>The point of one kerb nearest somewhere, which is where a fillet centred there touches it.</summary>
    static Vector2 Touches(Kerb kerb, Vector2 fromM) =>
        kerb.Curved
            ? kerb.ArcCentreM + (Vector2.Normalize(fromM - kerb.ArcCentreM) * kerb.ArcRadiusM)
            : kerb.AtM + (kerb.Along * Vector2.Dot(fromM - kerb.AtM, kerb.Along));

    /// <summary>
    /// How far along its own arm a point of one kerb stands, measured from the node: <b>the road's own
    /// length and not the kerb's</b>, because what is set back from a junction is set back along the
    /// carriageway. On a bend the two differ by the ratio of their radii, which is the whole reason the
    /// angle is what is measured.
    /// </summary>
    static float AlongTheArmM(Kerb kerb, Vector2 atM)
    {
        if (!kerb.Curved) return Vector2.Dot(atM - kerb.AtM, kerb.Along);

        var fromM = kerb.AtM - kerb.ArcCentreM;
        var toM = atM - kerb.ArcCentreM;
        return MathF.Abs(MathF.Atan2(Cross(fromM, toM), Vector2.Dot(fromM, toM))) * kerb.ArmRadiusM;
    }

    /// <summary>
    /// Where two kerbs cross, taking <b>the crossing nearest <paramref name="nearM"/></b> where there are
    /// two of them — a straight kerb cuts a ring's circle on the far side of it as well, and that crossing
    /// is a road's width from this junction rather than the ring's whole width.
    /// </summary>
    static bool Meet(Kerb a, Kerb b, Vector2 nearM, out Vector2 atM)
    {
        atM = Vector2.Zero;
        if (!a.Curved && !b.Curved)
        {
            var closing = Vector2.Dot(a.Along, b.Into);
            if (MathF.Abs(closing) < 1e-4f) return false;

            atM = a.AtM + (a.Along * (Vector2.Dot(b.AtM - a.AtM, b.Into) / closing));
            return true;
        }

        if (a.Curved && b.Curved) return TwoCircles(a, b, nearM, out atM);

        return b.Curved ? LineAndCircle(a, b, nearM, out atM) : LineAndCircle(b, a, nearM, out atM);
    }

    /// <summary>The straight kerb's own points at the circle's radius from its centre, nearer one first.</summary>
    static bool LineAndCircle(Kerb line, Kerb circle, Vector2 nearM, out Vector2 atM)
    {
        atM = Vector2.Zero;
        var offM = line.AtM - circle.ArcCentreM;
        var alongM = -Vector2.Dot(offM, line.Along);
        var squaredM = offM.LengthSquared() - (alongM * alongM);
        var halfChordSquaredM = (circle.ArcRadiusM * circle.ArcRadiusM) - squaredM;
        if (halfChordSquaredM < 0f) return false;

        var halfChordM = MathF.Sqrt(halfChordSquaredM);
        var oneM = line.AtM + (line.Along * (alongM - halfChordM));
        var otherM = line.AtM + (line.Along * (alongM + halfChordM));
        atM = Vector2.DistanceSquared(oneM, nearM) <= Vector2.DistanceSquared(otherM, nearM) ? oneM : otherM;
        return true;
    }

    /// <summary>And the two points both circles carry, which is the ordinary radical-line construction.</summary>
    static bool TwoCircles(Kerb a, Kerb b, Vector2 nearM, out Vector2 atM)
    {
        atM = Vector2.Zero;
        var betweenM = b.ArcCentreM - a.ArcCentreM;
        var apartM = betweenM.Length();
        if (apartM < 1e-4f) return false;

        var alongM = ((apartM * apartM) + (a.ArcRadiusM * a.ArcRadiusM) - (b.ArcRadiusM * b.ArcRadiusM))
                     / (2f * apartM);
        var halfChordSquaredM = (a.ArcRadiusM * a.ArcRadiusM) - (alongM * alongM);
        if (halfChordSquaredM < 0f) return false;

        var along = betweenM / apartM;
        var footM = a.ArcCentreM + (along * alongM);
        var acrossM = Heading.RightOf(along) * MathF.Sqrt(halfChordSquaredM);
        atM = Vector2.DistanceSquared(footM + acrossM, nearM) <= Vector2.DistanceSquared(footM - acrossM, nearM)
            ? footM + acrossM
            : footM - acrossM;
        return true;
    }

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    /// <summary>
    /// <b>Where the one crossing a junction with no fork carries stands</b> (TER-6): two arms admit nothing
    /// but driving through, so the node is somewhere to cross rather than somewhere to choose, and a zebra
    /// on each of its arms is the same stop asked for twice a few metres apart.
    /// </summary>
    /// <remarks>
    /// It stands on whichever arm has the most road left behind it — the arm whose paint stands furthest
    /// from whatever the far end of that road carries — and <b>the bundle begins where the corner's own
    /// ground lets go</b>, because nothing at such a node is a junction to be set back from: what is behind
    /// the paint is the same road bending.
    /// </remarks>
    static int[] Throughs(ArcSeg[][] chains, List<Arm>[] arms, SimConfig config, float[][] reachM)
    {
        var through = new int[arms.Length];
        Array.Fill(through, NoArm);

        for (var at = 0; at < arms.Length; at++)
        {
            if (arms[at].Count != 2) continue;

            var spareM = 0f;
            for (var index = 0; index < arms[at].Count; index++)
            {
                var arm = arms[at][index];
                var leftM = (Spline.TotalLengthM(chains[arm.Road]) * PaintWithinShare) - reachM[at][index]
                            - ThroughCrossingSetbackM(config) - BarOffTheCrossingM(config);
                if (leftM < 0f || (through[at] != NoArm && leftM <= spareM)) continue;

                through[at] = index;
                spareM = leftM;
            }
        }

        return through;
    }

    /// <summary>
    /// <b>A crossing on every arm of every junction</b> (TER-6) — the placement rule and not a chosen set: a
    /// block whose pavement has no way off it is a walking network of islands. <b>A junction with no fork
    /// carries one for both its arms</b>, laid where <see cref="Throughs"/> put it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stride is taken past the nearer of the arm's two corners, and the paint stands wholly past
    /// the further</b> (<see cref="CrossingAlongM"/>). Taken past the further corner, a skew junction's
    /// zebra stood a stride past its acute corner and three strides past its obtuse one — eighteen metres
    /// from the node, on an arm whose other kerb had run straight for ten.
    /// </para>
    /// <para>
    /// <b>A roundabout's circulating carriageway is the one arm that carries none</b> (GEN-19). A zebra on
    /// it is a walk laid across the traffic a roundabout exists to keep moving, and the ring's entries carry
    /// their own — which is where somebody crossing a roundabout crosses. What it leaves is an island
    /// nobody walks onto, which is what an island is.
    /// </para>
    /// </remarks>
    static CityPlan.CrosswalkArrays Crossings(
        TownLayout layout, ArcSeg[][] chains, List<Arm>[] arms, SimConfig config, float[][] reachM,
        float[][] nearM, int[] through)
    {
        var centreM = new List<Vector2>();
        var axis = new List<Vector2>();
        var depthM = new List<float>();
        var road = new List<int>();
        var junction = new List<int>();

        for (var at = 0; at < arms.Length; at++)
        {
            if (arms[at].Count < 2) continue;

            // A junction with no fork is one road to everything driving it, and one road is crossed once —
            // on the arm that had the room, and nowhere at all where neither of them did.
            var noFork = arms[at].Count == 2;
            if (noFork && through[at] == NoArm) continue;

            for (var index = 0; index < arms[at].Count; index++)
            {
                if (noFork && index != through[at]) continue;

                var arm = arms[at][index];
                if (layout.Edges[arm.Road].Class == RoadClass.Roundabout) continue;

                var alongM = noFork
                    ? reachM[at][index] + ThroughCrossingSetbackM(config)
                    : CrossingAlongM(config, nearM[at][index], reachM[at][index]);
                if (!OnTheArm(chains, arm, alongM, out var pointM, out var outward)) continue;

                centreM.Add(pointM);
                axis.Add(outward);
                depthM.Add(config.Road.CrossingDepthM);
                road.Add(arm.Road);
                junction.Add(at);
            }
        }

        return new CityPlan.CrosswalkArrays
        {
            CentreM = [.. centreM], Axis = [.. axis], DepthM = [.. depthM], Road = [.. road],
            Junction = [.. junction],
        };
    }

    /// <summary>
    /// A bar behind the crossing on every lane driving into every junction, lit or not: a bar is the place
    /// a driver holds short of the paint when the junction — its lights or its ranking — refuses it, and a
    /// lane with none holds at the box itself, over the zebra.
    /// </summary>
    /// <remarks>
    /// <b>A junction with no fork carries the two its own crossing wants</b> (TER-6), and they are the
    /// crossing's rather than the arms': one either side of the paint, each on the lane that runs over it
    /// from that side. Nothing there admits a conflicting movement, so nothing is lit (TLT-3) and the whole
    /// of what governs the paint is the walker's right of way — which these say where the stop for is made.
    /// <para>
    /// <b>A roundabout's circulating carriageway carries none either</b> (GEN-19). A bar is where a driver
    /// holds when the junction refuses them, and circulating traffic is never refused: the entries hold for
    /// it and it holds for nothing, so what the ring is left with is a road nobody stops on.
    /// </para>
    /// </remarks>
    static CityPlan.StopLineArrays Bars(
        TownLayout layout, ArcSeg[][] chains, List<Arm>[] arms, SimConfig config, float[] widthM,
        float[][] reachM, float[][] nearM, int[] through)
    {
        var centreM = new List<Vector2>();
        var approach = new List<Vector2>();
        var spanM = new List<float>();
        var thicknessM = new List<float>();
        var junction = new List<int>();
        var road = new List<int>();

        for (var at = 0; at < arms.Length; at++)
        {
            var noFork = through[at] != NoArm;
            if (!noFork && arms[at].Count < 3) continue;

            for (var index = 0; index < arms[at].Count; index++)
            {
                if (noFork && index != through[at]) continue;

                var arm = arms[at][index];
                if (layout.Edges[arm.Road].Class == RoadClass.Roundabout) continue;

                // The lane arriving stops on the near side of the paint and the lane leaving on the far
                // side, which at a node with no fork is the same crossing barred from both of its sides.
                foreach (var side in noFork ? (ReadOnlySpan<float>)[1f, -1f] : [1f])
                {
                    // <b>Nothing is stopped where nothing drives</b>: a one-way street bars the traffic
                    // coming to the junction and paints nothing across the lane leaving it (TER-4d).
                    if (!Driven(layout, arm, arrives: side > 0f)) continue;

                    var alongM = noFork
                        ? reachM[at][index] + ThroughCrossingSetbackM(config) + (side * BarOffTheCrossingM(config))
                        : CrossingAlongM(config, nearM[at][index], reachM[at][index]) + BarOffTheCrossingM(config);
                    if (!OnTheArm(chains, arm, alongM, out var pointM, out var outward)) continue;

                    // Behind the paint and on the driver's own side of the centreline: a bar painted across
                    // the whole carriageway is one the oncoming traffic is also stopped at. A one-way street
                    // has one lane and the bar is the whole of it, laid down the middle.
                    var travel = side > 0f ? -outward : outward;
                    var lanes = layout.Edges[arm.Road].Flow == RoadFlow.BothWays ? 2 : 1;
                    var offTheMiddleM = lanes == 2 ? config.LaneOffsetM * config.RoadSideSign : 0f;
                    centreM.Add(pointM + (Heading.RightOf(travel) * offTheMiddleM));
                    approach.Add(travel);
                    spanM.Add(widthM[arm.Road] / lanes);
                    thicknessM.Add(config.Road.StopBarThicknessM);
                    junction.Add(at);
                    road.Add(arm.Road);
                }
            }
        }

        return new CityPlan.StopLineArrays
        {
            CentreM = [.. centreM], Approach = [.. approach], SpanM = [.. spanM],
            ThicknessM = [.. thicknessM], Junction = [.. junction], Road = [.. road],
        };
    }

    /// <summary>
    /// Whether an arm carries traffic the way asked of it: <paramref name="arrives"/> for the lane coming to
    /// the junction the arm stands at, and the other way for the one leaving it. Both, on a road driven both
    /// ways; one of them on a one-way street (TER-4d).
    /// </summary>
    static bool Driven(TownLayout layout, Arm arm, bool arrives) => layout.Edges[arm.Road].Flow switch
    {
        RoadFlow.WithTheRoad => arm.AtFromEnd != arrives,
        RoadFlow.AgainstTheRoad => arm.AtFromEnd == arrives,
        _ => true,
    };

    /// <summary>
    /// How far out along an arm a crossing's own centre stands: its stride
    /// (<see cref="RoadFigures.CrossingSetbackM"/>) past the nearer of the arm's corners, and its whole
    /// depth past the further one — so the paint never lies on a fillet, and a skew junction's zebra is not
    /// three strides from the corner on the other kerb.
    /// </summary>
    static float CrossingAlongM(SimConfig config, float nearM, float farM) =>
        MathF.Max(nearM + config.Road.CrossingSetbackM, farM) + (config.Road.CrossingDepthM * 0.5f);

    /// <summary>
    /// And the same at a junction with no fork, where <b>what is behind the paint is road and not a
    /// junction</b>: the first thing on it is the bar of the traffic leaving the corner, and the crossing
    /// stands that bar's own thickness and setback beyond it.
    /// </summary>
    /// <remarks>
    /// <b>The bundle stands off the corner's ground by the same stride a crossing does off a junction's</b>
    /// (<see cref="RoadFigures.CrossingSetbackM"/>), and for the same reason: the paint has to lie on straight
    /// kerb rather than on the corner's own arc. Begun where the corner's ground ends instead, the first bar
    /// sat on the bend and the zebra behind it had a metre and a half of square road — the tightest crossings
    /// in either shipped town, and the only ones that read as laid on a curve.
    /// <para>
    /// It puts the far bar's outer edge exactly on <see cref="SimConfig.StraightStubM"/>, so the deepest
    /// bundle in the town still lies wholly on the straight a road leaves its junctions on (GEN-12).
    /// </para>
    /// </remarks>
    static float ThroughCrossingSetbackM(SimConfig config) =>
        config.Road.CrossingSetbackM + (config.Road.StopBarThicknessM * 0.5f) + config.Road.StopBarSetbackM
        + (config.Road.CrossingDepthM * 0.5f);

    /// <summary>How far off the crossing its bar stands, centre to centre, on whichever side is stopping at it.</summary>
    static float BarOffTheCrossingM(SimConfig config) =>
        (config.Road.CrossingDepthM * 0.5f) + config.Road.StopBarSetbackM;

    /// <summary>
    /// Where a given distance out along an arm falls, and which way the road runs there. <b>False where the
    /// arm is too short to carry it</b>, which is the whole of what stops two junctions a stride apart from
    /// painting over each other.
    /// </summary>
    static bool OnTheArm(ArcSeg[][] chains, Arm arm, float alongM, out Vector2 pointM, out Vector2 outward)
    {
        var chain = chains[arm.Road];
        var lengthM = Spline.TotalLengthM(chain);
        pointM = Vector2.Zero;
        outward = Vector2.UnitX;
        if (alongM > lengthM * PaintWithinShare) return false;

        var on = Spline.SampleAt(chain, arm.AtFromEnd ? alongM : lengthM - alongM);
        pointM = on.PositionM;
        outward = arm.AtFromEnd ? on.Direction : -on.Direction;
        return true;
    }

    static float Wrapped(float radians) => radians - (MathF.Tau * MathF.Floor(radians / MathF.Tau));
}
