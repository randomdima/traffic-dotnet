using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Foot;

/// <summary>The two bands either side of every carriageway, cut at the junctions the road passes through, and the pieces that carry them round a junction.</summary>
internal sealed partial class FootGraph
{
    /// <summary>One side of one road's pavement, as its own line and the spans of it that are not inside a junction.</summary>
    sealed class Strip
    {
        public required int Road { get; init; }

        public required ArcSeg[] Line { get; init; }

        public required float LengthM { get; init; }

        public required List<Span> Spans { get; init; }
    }

    /// <summary>
    /// A stretch of a strip, bounded at either end by the junction that interrupts it. The two distances
    /// start at the junction discs and are pushed out to the kerb corners as those are read.
    /// </summary>
    struct Span
    {
        public float FromM;
        public float ToM;
        public int FromJunction;
        public int ToJunction;
    }

    /// <summary>Where one pavement strip stops against one junction, and which arm of it the strip belongs to.</summary>
    readonly record struct BandEnd(int Junction, int Node, int Road, bool Arriving);

    /// <summary>Where one kerb corner meets the two pavements it joins: a place on a strip, at each end.</summary>
    readonly record struct KerbCorner(int Corner, int StripA, int SpanA, bool EndA, int StripB, int SpanB, bool EndB);

    /// <summary>
    /// The two bands either side of every carriageway, cut at every junction the road passes through by
    /// <b>the same arithmetic that cuts the lanes</b>. What comes out is not laid yet: the cuts stand at
    /// the junction discs, and a disc is nowhere near the kerb.
    /// </summary>
    static List<Strip> Strips(CityPlan plan, SimConfig config, float bandM)
    {
        var roads = plan.Roads;
        var discs = RoadCuts.JunctionIndex(plan, bandM * 0.5f);
        var cuts = new List<RoadCut>();
        var strips = new List<Strip>();

        var most = 2;
        for (var road = 0; road < roads.Count; road++)
        {
            most = Math.Max(most, roads.SegmentOffsets[road + 1] - roads.SegmentOffsets[road]);
        }

        var offset = new ArcSeg[most + 2];

        for (var road = 0; road < roads.Count; road++)
        {
            var centreline = roads.SegmentsOf(road);
            if (centreline.Length == 0) continue;

            // Half the carriageway plus half a walk: the middle of the band, which is where a pavement's
            // own line runs. Half a band in is the middle of the band, not near it.
            var acrossM = roads.WidthM[road] * 0.5f + bandM * 0.5f;
            foreach (var side in (ReadOnlySpan<float>)[config.RoadSideSign, -config.RoadSideSign])
            {
                Spline.OffsetInto(centreline, acrossM * side, offset);
                var line = offset.AsSpan(0, centreline.Length).ToArray();
                var lengthM = Spline.TotalLengthM(line);
                RoadCuts.Along(
                    plan, discs, line, lengthM, bandM * 0.5f, roads.FromJunction[road], roads.ToJunction[road], cuts);

                var spans = new List<Span>();
                for (var cut = 0; cut + 1 < cuts.Count; cut++)
                {
                    spans.Add(new Span
                    {
                        FromM = cuts[cut].ExitM,
                        ToM = MathF.Max(cuts[cut].ExitM, cuts[cut + 1].EnterM),
                        FromJunction = cuts[cut].Junction,
                        ToJunction = cuts[cut + 1].Junction,
                    });
                }

                strips.Add(new Strip { Road = road, Line = line, LengthM = lengthM, Spans = spans });
            }
        }

        return strips;
    }

    /// <summary>
    /// Reads every kerb corner the plan carries and <b>pushes the two strips it joins back to it</b>.
    /// This is where a junction's real kerb comes from: a disc is a driver's fiction sized for the ground
    /// two roads share, and the ground a walker may stand on ends at the fillet, which stands well outside
    /// it. The joint is the fillet's own arc read in by half a walk — the same distance the strip's line
    /// stands inside the arm's kerb, so the two meet at the tangent point and nothing has to be trimmed.
    /// </summary>
    static List<KerbCorner> Corners(CityPlan plan, List<Strip> strips, float bandM)
    {
        var corners = plan.JunctionCorners;
        var joined = new List<KerbCorner>();

        for (var corner = 0; corner < corners.Count; corner++)
        {
            var arcCentreM = corners.ArcCentreM[corner];
            var walkedM = corners.RadiusM[corner] - bandM * 0.5f;
            if (walkedM <= 0f) continue;

            var atA = Nearest(strips, arcCentreM + Toward(corners.TangentAM[corner], arcCentreM) * walkedM, bandM);
            var atB = Nearest(strips, arcCentreM + Toward(corners.TangentBM[corner], arcCentreM) * walkedM, bandM);
            if (atA.Strip < 0 || atB.Strip < 0) continue;

            Push(strips, atA);
            Push(strips, atB);
            joined.Add(new KerbCorner(corner, atA.Strip, atA.Span, atA.End, atB.Strip, atB.Span, atB.End));
        }

        return joined;
    }

    static Vector2 Toward(Vector2 pointM, Vector2 fromM) => Vector2.Normalize(pointM - fromM);

    /// <summary>Which end of which span of which strip a point stands at, or nothing within reach of one.</summary>
    readonly record struct StripPlace(int Strip, int Span, bool End, float AlongM);

    static StripPlace Nearest(List<Strip> strips, Vector2 pointM, float reachM)
    {
        var best = new StripPlace(-1, 0, false, 0f);
        var bestOffM = reachM;

        // The strip is chosen by how near its own line passes, and only then the end of a span on it —
        // the two questions in that order, because a span end on the wrong strip is nearer than the right
        // strip's own end surprisingly often at a junction where four arms meet.
        for (var strip = 0; strip < strips.Count; strip++)
        {
            var line = strips[strip].Line;
            var alongM = Spline.ProjectM(line, pointM, strips[strip].LengthM * 0.5f, strips[strip].LengthM);
            var offM = (Spline.SampleAt(line, alongM).PositionM - pointM).Length();
            if (offM >= bestOffM) continue;

            var bestPastM = float.MaxValue;
            for (var span = 0; span < strips[strip].Spans.Count; span++)
            {
                foreach (var end in (ReadOnlySpan<bool>)[false, true])
                {
                    var atM = end ? strips[strip].Spans[span].ToM : strips[strip].Spans[span].FromM;
                    var pastM = MathF.Abs(alongM - atM);
                    if (pastM >= bestPastM) continue;

                    bestPastM = pastM;
                    bestOffM = offM;
                    best = new StripPlace(strip, span, end, alongM);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Moves a span's end to the corner, and only ever <em>inward</em>: the kerb corner stands outside the
    /// disc the span was cut at, so a corner that would lengthen a stretch is one matched to the wrong end.
    /// </summary>
    static void Push(List<Strip> strips, StripPlace at)
    {
        var spans = strips[at.Strip].Spans;
        var span = spans[at.Span];
        if (at.End) span.ToM = MathF.Min(span.ToM, at.AlongM);
        else span.FromM = MathF.Max(span.FromM, at.AlongM);

        spans[at.Span] = span;
    }

    /// <summary>Lays every span that survived as a stretch of the graph, and reports where each one ended up.</summary>
    static Dictionary<(int Strip, int Span, bool End), BandEnd> Lay(Builder builder, List<Strip> strips, float bandM)
    {
        var ends = new Dictionary<(int, int, bool), BandEnd>();
        var stretch = new ArcSeg[64];

        for (var strip = 0; strip < strips.Count; strip++)
        {
            var spans = strips[strip].Spans;
            for (var span = 0; span < spans.Count; span++)
            {
                if (stretch.Length < strips[strip].Line.Length + 2) stretch = new ArcSeg[strips[strip].Line.Length + 2];

                var arcCount = Spline.SubChainInto(strips[strip].Line, spans[span].FromM, spans[span].ToM, stretch);
                if (arcCount == 0) continue;

                var edge = builder.AddStrand(stretch.AsSpan(0, arcCount), bandM, FootEdgeKind.Pavement);
                ends[(strip, span, false)] =
                    new BandEnd(spans[span].FromJunction, builder.FromNode(edge), strips[strip].Road, Arriving: false);
                ends[(strip, span, true)] =
                    new BandEnd(spans[span].ToJunction, builder.ToNode(edge), strips[strip].Road, Arriving: true);
            }
        }

        return ends;
    }

    /// <summary>
    /// The band round each kerb corner: the fillet's own arc read in by half a walk, which is what
    /// turning every corner on the curve of what it runs beside comes to at a junction.
    /// </summary>
    static void KerbCorners(
        CityPlan plan, Builder builder, List<KerbCorner> corners,
        Dictionary<(int Strip, int Span, bool End), BandEnd> ends, float bandM)
    {
        foreach (var corner in corners)
        {
            if (!ends.TryGetValue((corner.StripA, corner.SpanA, corner.EndA), out var from)) continue;
            if (!ends.TryGetValue((corner.StripB, corner.SpanB, corner.EndB), out var to)) continue;
            if (from.Node == to.Node) continue;

            var arcCentreM = plan.JunctionCorners.ArcCentreM[corner.Corner];
            var radiusM = plan.JunctionCorners.RadiusM[corner.Corner] - bandM * 0.5f;
            builder.AddArc(from.Node, to.Node, Around(builder, arcCentreM, radiusM, from.Node, to.Node), bandM, FootEdgeKind.JunctionCorner);
        }
    }

    /// <summary>
    /// The band between two arm-sides of one junction that <b>no kerb corner joins</b>, which is the one
    /// place this network was severed on ground a walker can plainly see is continuous.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A junction bites a strip with its disc, and the plan lays a fillet only where two kerbs actually
    /// turn.</b> Between two arms that run straight on — the far side of a T from its stem, the two halves
    /// of a road through an inline junction — there is no turn and so no fillet, and how much of the strip
    /// the disc took is then decided by nothing but the arm's own width: a strip stands half a carriageway
    /// and half a walk off the centreline, so a road narrower than its junction is bitten and a wider one
    /// is not. Where the bite is real and no fillet covers it, both sides of it are dead ends and a walker
    /// has to cross a road to get past a straight piece of pavement.
    /// </para>
    /// <para>
    /// <b>The ground decides, and it decides against far more pairs than it decides for.</b> Every other
    /// pair of arm-sides at a junction is across a carriageway from its partner — the two sides of one
    /// arm, the two halves of a dead end's mouth, the pair across the stem of a T — and the ground between
    /// those is road, paint or a lot, all of which this refuses. What is left is the pair that was one
    /// pavement before the disc bit it. The band is the straight between them: it is a step across a
    /// break rather than a corner, and the mitres round its two ends.
    /// </para>
    /// </remarks>
    static void ArmBands(
        CityPlan plan, TerrainGrid terrain, Builder builder,
        Dictionary<(int Strip, int Span, bool End), BandEnd> ends, float bandM)
    {
        foreach (var (junction, here) in EndsByJunction(ends))
        {
            // No wider than the bite the disc itself could take: a strip crosses the walked circle round
            // the junction on a chord, so twice that circle bounds it, and a band of slack on top covers
            // two arms that stand at different distances from their own centrelines. Longer than that is
            // not a bite being closed, it is two places joined because nothing happened to stand between
            // them — which is what the ground below is really relied on to refuse.
            var capM = 2f * (plan.Junctions.RadiusM[junction] + bandM);
            var gaps = new List<(float GapM, int From, int To)>();
            for (var a = 0; a < here.Count; a++)
            {
                for (var b = a + 1; b < here.Count; b++)
                {
                    var fromNode = here[a].Node;
                    var toNode = here[b].Node;
                    if (fromNode == toNode) continue;

                    var fromM = builder.PositionOf(fromNode);
                    var toM = builder.PositionOf(toNode);
                    var gapM = (toM - fromM).Length();
                    if (gapM > capM || !OnFoot(terrain, fromM, toM) || builder.Joined(fromNode, toNode)) continue;

                    gaps.Add((gapM, fromNode, toNode));
                }
            }

            // Nearest first, and one band per end: two ends that are each other's nearest are the pair the
            // disc severed. Ties go by node, so which bands a junction gets is a fact about the town and
            // not about the order its ends were collected in.
            gaps.Sort(static (left, right) =>
                left.GapM != right.GapM ? left.GapM.CompareTo(right.GapM)
                : left.From != right.From ? left.From.CompareTo(right.From)
                : left.To.CompareTo(right.To));

            var taken = new HashSet<int>();
            foreach (var (_, fromNode, toNode) in gaps)
            {
                if (taken.Contains(fromNode) || taken.Contains(toNode)) continue;

                var fromM = builder.PositionOf(fromNode);
                var runM = builder.PositionOf(toNode) - fromM;
                builder.AddArc(
                    fromNode, toNode, new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), runM.Length(), 0f), bandM,
                    FootEdgeKind.JunctionCorner);
                taken.Add(fromNode);
                taken.Add(toNode);
            }
        }
    }

    /// <summary>Whether every station between two points is ground this network may run over: walkable, and no part of it a carriageway, a crossing's paint or a lot.</summary>
    static bool OnFoot(TerrainGrid terrain, Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        var steps = Math.Max(2, (int)MathF.Ceiling(runM.Length() / (terrain.CellSizeM * 0.5f)));
        for (var step = 0; step <= steps; step++)
        {
            var at = terrain.At(fromM + (runM * step / steps));
            if (!at.Walkable || at.Drivable) return false;
        }

        return true;
    }

    /// <summary>Which band ends stand at each junction — what a band between two arms is chosen from.</summary>
    static Dictionary<int, List<BandEnd>> EndsByJunction(Dictionary<(int Strip, int Span, bool End), BandEnd> ends)
    {
        var byJunction = new Dictionary<int, List<BandEnd>>();
        foreach (var end in ends.Values)
        {
            if (!byJunction.TryGetValue(end.Junction, out var here)) byJunction[end.Junction] = here = [];
            here.Add(end);
        }

        return byJunction;
    }

    /// <summary>
    /// A dead end's head, where there is no kerb corner between two arms because there is only one arm.
    /// The band there is the head's own disc read <em>out</em> by half a walk, which is what a turning
    /// head is sized to hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Which junctions are dead ends is the plan's answer and not this derivation's.</b> Reading it off
    /// the band ends that happened to survive — two of them, both from one road — calls a crossroads whose
    /// other two arms collapsed to nothing a dead end, and lays a head band the long way round its disc
    /// and straight across the carriageway.
    /// </para>
    /// <para>
    /// <b>A head band is the one band laid off nothing but a radius</b>, and it is therefore the one that
    /// can be laid over ground somebody else has already stamped: a strip is laid off a road the plan
    /// carries and a kerb corner off a fillet it carries, but the ring round a head is only ever assumed
    /// to be pavement. So it is asked of the plan whether a paved area covers it, and where one does the
    /// band is refused outright rather than trimmed — a head opening onto a plaza is not a head there is a
    /// way round, and saying so leaves the two arms' pavements ending where the ground ends.
    /// </para>
    /// </remarks>
    static void HeadBands(
        CityPlan plan, Builder builder, Dictionary<(int Strip, int Span, bool End), BandEnd> ends, float bandM)
    {
        var armsPerJunction = RoadCuts.ArmsPerJunction(plan);
        foreach (var (junction, arms) in EndsByJunction(ends))
        {
            if (armsPerJunction[junction] != 1) continue;
            if (arms.Count != 2 || arms[0].Road != arms[1].Road) continue;
            if (builder.Joined(arms[0].Node, arms[1].Node)) continue;

            var centreM = plan.Junctions.CentreM[junction];
            var radiusM = plan.Junctions.RadiusM[junction] + bandM * 0.5f;

            // The long way round, which is the head. The two ends straddle the one arm's carriageway, so
            // the short way between them is straight across the mouth of the road.
            var head = Around(builder, centreM, radiusM, arms[0].Node, arms[1].Node, theLongWay: true);
            if (RunsOverAPavedArea(plan, head)) continue;

            builder.AddArc(arms[0].Node, arms[1].Node, head, bandM, FootEdgeKind.JunctionCorner);
        }
    }
}
