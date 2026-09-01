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
/// <b>Every one of these is laid across a straight arm</b>, which is the reason a road's two ends are
/// straight pieces (<see cref="RoadStage"/>). An arm short enough that its own crossing would stand past the
/// middle of the road carries none: two junctions that close carry one box between them, and paint struck
/// halfway down a road nobody stops on is paint a walker is sent to and a driver never sees.
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

    /// <summary>One road as it leaves a junction: which road, which of its ends, and the way out along it.</summary>
    readonly record struct Arm(int Road, bool AtFromEnd, float BearingRad);

    /// <summary>
    /// The fillets a junction's corners are turned on, and <b>how far along each of its arms the junction's
    /// own ground therefore reaches</b> — which is what the paint on that arm is set back from, arm by arm
    /// and junction by junction.
    /// </summary>
    readonly record struct Mouths(CityPlan.JunctionCornerArrays Corners, float[][] ReachM);

    public static Laid Lay(
        TownLayout layout, ArcSeg[][] chains, CityPlan.JunctionArrays junctions, SimConfig config, float widthM)
    {
        var arms = ArmsOf(layout, chains, junctions.Count);
        var mouths = Corners(chains, junctions, arms, config, widthM);
        var through = Throughs(chains, arms, config, mouths.ReachM);

        // <b>A crossing is the width of the carriageway it crosses</b>, kerb to kerb, and it is drawn to
        // the width of the road it names rather than to a span of its own — so a zebra whose end bars
        // stand on the pavement cannot be laid at all. What the ground's own rounding is owed is the
        // classification's (<see cref="GroundPainter.Crossing"/>), which sweeps wider than the paint and
        // lays nothing off the carriageway.
        var crossings = Crossings(chains, arms, config, mouths.ReachM, through);
        var bars = Bars(chains, junctions, arms, config, widthM, mouths.ReachM, through);
        return new Laid(mouths.Corners, crossings, bars);
    }

    static List<Arm>[] ArmsOf(TownLayout layout, ArcSeg[][] chains, int junctions)
    {
        var arms = new List<Arm>[junctions];
        for (var junction = 0; junction < junctions; junction++) arms[junction] = [];

        for (var road = 0; road < chains.Length; road++)
        {
            if (chains[road].Length == 0) continue;

            var edge = layout.Edges[road];
            arms[edge.From].Add(new Arm(road, true, RoadStage.Facing(Spline.SampleAt(chains[road], 0f).Direction)));
            arms[edge.To].Add(new Arm(
                road, false,
                RoadStage.Facing(-Spline.SampleAt(chains[road], Spline.TotalLengthM(chains[road])).Direction)));
        }

        foreach (var junction in arms) junction.Sort((a, b) => Wrapped(a.BearingRad).CompareTo(Wrapped(b.BearingRad)));
        return arms;
    }

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
        ArcSeg[][] chains, CityPlan.JunctionArrays junctions, List<Arm>[] arms, SimConfig config, float widthM)
    {
        var cornerM = new List<Vector2>();
        var arcCentreM = new List<Vector2>();
        var radiusM = new List<float>();
        var tangentAM = new List<Vector2>();
        var tangentBM = new List<Vector2>();

        var halfM = widthM * 0.5f;
        var reachM = new float[arms.Length][];
        for (var junction = 0; junction < arms.Length; junction++)
        {
            // An arm no corner reaches — one of two that run apart, or of two all but straight through —
            // is reached only by the carriageway's own edge. <b>Or by the bend it leaves on</b>, where the
            // two arms of a node with no fork were swept into one curve (<see cref="RoadStage"/>): the paint
            // an arm carries is laid across a straight, and the straight there begins where the arc ends.
            reachM[junction] = new float[arms[junction].Count];
            for (var at = 0; at < arms[junction].Count; at++)
            {
                var arm = arms[junction][at];
                reachM[junction][at] = MathF.Max(halfM, Spline.BendAtTheEndM(chains[arm.Road], arm.AtFromEnd));
            }

            if (arms[junction].Count < 2) continue;

            for (var at = 0; at < arms[junction].Count; at++)
            {
                var next = (at + 1) % arms[junction].Count;
                var a = Heading.Unit(arms[junction][at].BearingRad);
                var b = Heading.Unit(arms[junction][next].BearingRad);
                var apartRad = Wrapped(arms[junction][next].BearingRad - arms[junction][at].BearingRad);

                // Two arms a straight line or more apart have no corner between them: their kerbs run away
                // from each other and the mouth's own edge is already tangent to both.
                if (apartRad >= MathF.PI) continue;

                var half = apartRad * 0.5f;

                // <b>How far the kerbs cross outside the mouth is whether there is a corner at all.</b> Two
                // arms all but straight through leave a spike narrower than the line the kerb is drawn as,
                // and a fillet turned on that is a shape nothing can see and no cell can hold.
                var spikeM = (halfM / MathF.Sin(half)) - halfM;
                if (spikeM < config.Road.PaintLineWidthM) continue;

                // The two kerbs bounding the wedge, and the arc tangent to both: the corner is where they
                // meet, the centre stands off it along the bisector, and each tangent runs back out along
                // its own kerb.
                var bisector = Vector2.Normalize(a + b);
                var filletM = config.JunctionFilletRadiusM(apartRad);

                var cornerAtM = junctions.CentreM[junction] + (bisector * (halfM / MathF.Sin(half)));
                cornerM.Add(cornerAtM);
                arcCentreM.Add(cornerAtM + (bisector * (filletM / MathF.Sin(half))));
                radiusM.Add(filletM);
                tangentAM.Add(cornerAtM + (a * (filletM / MathF.Tan(half))));
                tangentBM.Add(cornerAtM + (b * (filletM / MathF.Tan(half))));

                // Both arms of the corner are reached as far as that tangent, and each arm stands off
                // whichever of its own two corners reaches further.
                var tangentAlongM = config.JunctionArmReachM(apartRad);
                reachM[junction][at] = MathF.Max(reachM[junction][at], tangentAlongM);
                reachM[junction][next] = MathF.Max(reachM[junction][next], tangentAlongM);
            }
        }

        return new Mouths(
            new CityPlan.JunctionCornerArrays
            {
                CornerM = [.. cornerM], ArcCentreM = [.. arcCentreM], RadiusM = [.. radiusM],
                TangentAM = [.. tangentAM], TangentBM = [.. tangentBM],
            },
            reachM);
    }

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
    static CityPlan.CrosswalkArrays Crossings(
        ArcSeg[][] chains, List<Arm>[] arms, SimConfig config, float[][] reachM, int[] through)
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
                var alongM = reachM[at][index]
                             + (noFork ? ThroughCrossingSetbackM(config) : CrossingSetbackM(config));
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
    /// A bar on every arm of every junction that is lit or forks nothing, and on no other: a bar is a place
    /// to stop that a driver is told about before it has to, and there is nothing to tell one at a junction
    /// the ranking governs.
    /// </summary>
    /// <remarks>
    /// <b>A junction with no fork carries the two its own crossing wants</b> (TER-6), and they are the
    /// crossing's rather than the arms': one either side of the paint, each on the lane that runs over it
    /// from that side. Nothing there admits a conflicting movement, so nothing is lit (TLT-3) and the whole
    /// of what governs the paint is the walker's right of way — which these say where the stop for is made.
    /// </remarks>
    static CityPlan.StopLineArrays Bars(
        ArcSeg[][] chains, CityPlan.JunctionArrays junctions, List<Arm>[] arms, SimConfig config, float widthM,
        float[][] reachM, int[] through)
    {
        var centreM = new List<Vector2>();
        var approach = new List<Vector2>();
        var spanM = new List<float>();
        var thicknessM = new List<float>();
        var junction = new List<int>();
        var road = new List<int>();

        var setbackM = config.Road.CrossingSetbackM + config.Road.CrossingDepthM
                       + config.Road.StopBarSetbackM;
        for (var at = 0; at < arms.Length; at++)
        {
            var noFork = through[at] != NoArm;
            if (!noFork && !junctions.Lit[at]) continue;

            for (var index = 0; index < arms[at].Count; index++)
            {
                if (noFork && index != through[at]) continue;

                var arm = arms[at][index];

                // The lane arriving stops on the near side of the paint and the lane leaving on the far
                // side, which at a node with no fork is the same crossing barred from both of its sides.
                foreach (var side in noFork ? (ReadOnlySpan<float>)[1f, -1f] : [1f])
                {
                    var alongM = noFork
                        ? reachM[at][index] + ThroughCrossingSetbackM(config) + (side * BarOffTheCrossingM(config))
                        : reachM[at][index] + setbackM;
                    if (!OnTheArm(chains, arm, alongM, out var pointM, out var outward)) continue;

                    // Behind the paint and on the driver's own side of the centreline: a bar painted across
                    // the whole carriageway is one the oncoming traffic is also stopped at.
                    var travel = side > 0f ? -outward : outward;
                    centreM.Add(pointM + (Heading.RightOf(travel) * config.LaneOffsetM * config.RoadSideSign));
                    approach.Add(travel);
                    spanM.Add(widthM * 0.5f);
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

    /// <summary>How far out past whatever ground its junction reaches a crossing's own centre stands.</summary>
    static float CrossingSetbackM(SimConfig config) =>
        config.Road.CrossingSetbackM + (config.Road.CrossingDepthM * 0.5f);

    /// <summary>
    /// And the same at a junction with no fork, where <b>the bundle begins where the corner's ground ends</b>
    /// rather than a setback past it: what is behind the paint there is road and not a junction, so the first
    /// thing on it is the bar of the traffic leaving the corner, and the crossing stands that bar's own
    /// thickness and setback beyond.
    /// </summary>
    /// <remarks>
    /// It puts the far bar's outer edge exactly on <see cref="SimConfig.StraightStubM"/>, so the deepest
    /// bundle in the town still lies wholly on the straight a road leaves its junctions on (GEN-12).
    /// </remarks>
    static float ThroughCrossingSetbackM(SimConfig config) =>
        (config.Road.StopBarThicknessM * 0.5f) + config.Road.StopBarSetbackM + (config.Road.CrossingDepthM * 0.5f);

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
