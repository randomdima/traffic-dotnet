using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// Which turn joins one lane to the next, priced for the router. A fact about the road and never about
/// the car on it, so it is filled once when the town is laid and read off thereafter.
/// </summary>
/// <remarks>
/// <b>There is no movement here that reverses the direction of travel</b> (TER-5f): a pair of lanes that
/// would face each other across a box is not joined at all, so no such turn is classified, laid, measured
/// or priced. Where a route has to come back the way it went, it does it in a bay (GEN-4l).
/// </remarks>
internal enum LaneTurn : byte
{
    Straight,

    /// <summary>The turn that crosses nothing: to the kerb side, which is the side traffic keeps.</summary>
    NearSide,

    /// <summary>The turn across the oncoming stream.</summary>
    FarSide,
}

/// <summary>
/// <b>The lines a car is driven on, laid with the town</b>: every lane of every road cut back to the points
/// its movements hand over at, and every connector between them. It is the town's driving geometry and the
/// whole of it — <b>there is no junction here</b>, only the lanes that meet at one and the lines drawn
/// between their ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the town's tarmac.</b> What a car may drive on is the ground under these lines and nothing
/// else, so <see cref="GroundShapes"/> answers a point against them and <see cref="Kerbs"/> is the union of
/// the bands they sweep. A junction has no shape of its own to be laid or asked about: the ground inside
/// one is the ground its connectors take, which is why a box that is turned, skewed, one-way or five-armed
/// is drawn correctly without anything here knowing what shape it made (TER-5).
/// </para>
/// <para>
/// <b>Laid at map generation, off the plan alone</b> — it is a pure function of the roads, the junctions
/// they are cut at and the figures on <see cref="SimConfig"/>. Both readings of the town take it: the
/// surface the ground is drawn and answered from, and the network <c>World.Road.RoadGraph</c> puts its
/// rules on top of. Neither lays a second one.
/// </para>
/// <para>
/// <b>A lane is the stretch of road between two cuts, not a whole road.</b> Roads are cut at <em>every</em>
/// junction they run through rather than the two they name, and at the places a car park asks for
/// (<see cref="ParkingSections"/>, GEN-4h) — so an inline junction is somewhere lanes end, and a frontage is
/// a stretch in its own right.
/// </para>
/// <para>
/// <b>Structure of arrays, laid once</b>, with every variable-length run — a lane's arcs, a lane's
/// connectors — a flat array and an offsets array beside it.
/// </para>
/// </remarks>
internal sealed class LaneLines
{
    /// <summary>Where a lane is asked for and the town has none — the reverse of a one-way stretch (TER-4d).</summary>
    public const int NoLane = -1;

    LaneLines(
        int junctionCount, int nodeCount, int[] laneRoad, float[] laneWidthM, int[] laneFromNode,
        int[] laneToNode, int[] laneFromJunction, int[] laneToJunction, bool[] laneForward, int[] laneReverse,
        bool[] laneEndsAtAPlace, float[] laneLengthM, float[] laneCutBackM, int[] laneArcOffsets,
        ArcSeg[] laneArcs, int[] connectorAt, int[] connectorToLane, LaneTurn[] connectorKind,
        int[] connectorArcOffsets, ArcSeg[] connectorArcs, float[] connectorLengthM)
    {
        JunctionCount = junctionCount;
        NodeCount = nodeCount;
        LaneRoad = laneRoad;
        LaneWidthM = laneWidthM;
        LaneFromNode = laneFromNode;
        LaneToNode = laneToNode;
        LaneFromJunction = laneFromJunction;
        LaneToJunction = laneToJunction;
        LaneForward = laneForward;
        LaneReverse = laneReverse;
        LaneEndsAtAPlace = laneEndsAtAPlace;
        LaneLengthM = laneLengthM;
        LaneCutBackM = laneCutBackM;
        LaneArcOffsets = laneArcOffsets;
        LaneArcs = laneArcs;
        ConnectorAt = connectorAt;
        ConnectorToLane = connectorToLane;
        ConnectorKind = connectorKind;
        ConnectorArcOffsets = connectorArcOffsets;
        ConnectorArcs = connectorArcs;
        ConnectorLengthM = connectorLengthM;

        // Which lane a connector leaves is the run it stands in, so it is folded out once rather than
        // searched for: a caller holding an id asks both its ends the same way.
        ConnectorFromLane = new int[connectorToLane.Length];
        for (var lane = 0; lane < LaneCount; lane++)
        {
            for (var id = connectorAt[lane]; id < connectorAt[lane + 1]; id++) ConnectorFromLane[id] = lane;
        }
    }

    /// <summary>How many intersections the plan named, which is the block the cut nodes start with.</summary>
    public int JunctionCount { get; }

    /// <summary>Every cut a lane can end at: the plan's junctions, then the places a car park asked for.</summary>
    public int NodeCount { get; }

    public int[] LaneRoad { get; }

    /// <summary>How wide the ground this lane is driven on is — the share of its road's width it was given.</summary>
    public float[] LaneWidthM { get; }

    public int[] LaneFromNode { get; }

    public int[] LaneToNode { get; }

    /// <summary>The plan's junction a lane sets off from, or <see cref="CityPlan.NoRecord"/> at a place (GEN-4h).</summary>
    public int[] LaneFromJunction { get; }

    /// <summary>And the one it arrives at, on the same terms.</summary>
    public int[] LaneToJunction { get; }

    /// <summary>Whether the lane runs with its road's own direction, which is what says which side it is on.</summary>
    public bool[] LaneForward { get; }

    /// <summary>The other lane of the same stretch, or <see cref="NoLane"/> where the stretch runs one way.</summary>
    public int[] LaneReverse { get; }

    /// <summary>Whether the lane runs out at a place a slice above asked for rather than at an intersection.</summary>
    public bool[] LaneEndsAtAPlace { get; }

    /// <summary>The length of the line as driven, after the cut back.</summary>
    public float[] LaneLengthM { get; }

    /// <summary>How much of its stretch the lane gave up to the boxes at its two ends.</summary>
    public float[] LaneCutBackM { get; }

    public int[] LaneArcOffsets { get; }

    public ArcSeg[] LaneArcs { get; }

    /// <summary>Count + 1 entries: the connectors out of lane i are <c>ConnectorAt[i]..ConnectorAt[i + 1]</c>.</summary>
    public int[] ConnectorAt { get; }

    public int[] ConnectorToLane { get; }

    /// <summary>And the lane it leaves, folded out of <see cref="ConnectorAt"/> once.</summary>
    public int[] ConnectorFromLane { get; }

    public LaneTurn[] ConnectorKind { get; }

    public int[] ConnectorArcOffsets { get; }

    public ArcSeg[] ConnectorArcs { get; }

    public float[] ConnectorLengthM { get; }

    public int LaneCount => LaneRoad.Length;

    public int ConnectorCount => ConnectorToLane.Length;

    /// <summary>The line one lane is driven on, in its own direction of travel, already cut back at both ends.</summary>
    public ReadOnlySpan<ArcSeg> ArcsOf(int lane) =>
        LaneArcs.AsSpan(LaneArcOffsets[lane], LaneArcOffsets[lane + 1] - LaneArcOffsets[lane]);

    /// <summary>
    /// <b>How wide the ground a movement is driven over is: the narrower of the two lanes it joins</b>
    /// (TER-5d.1). One figure, read by everything that has to know what a box is paved with — the tarmac's
    /// own shape (<c>Kerbs</c>), the ground under a point (<c>GroundShapes</c>) and the picture.
    /// </summary>
    /// <remarks>
    /// <b>The narrower, because a band is one width and the two ends are not.</b> Drawn at the arriving
    /// lane's width all the way back, a movement onto a wide road out of a narrow one reaches past the
    /// narrow one's own kerb at the mouth — half a metre of tarmac standing in the pavement, which cut the
    /// corner's wrapping line and left the pavement in two pieces either side of the junction.
    /// </remarks>
    public float ConnectorWidthM(int connector) =>
        MathF.Min(LaneWidthM[ConnectorFromLane[connector]], LaneWidthM[ConnectorToLane[connector]]);

    /// <summary>
    /// The plan's junction a movement crosses, or <see cref="CityPlan.NoRecord"/> where it joins two lanes
    /// at a place a car park cut into their road (GEN-4h).
    /// </summary>
    public int JunctionOfConnector(int connector) => LaneToJunction[ConnectorFromLane[connector]];

    /// <summary>The line one connector is driven on, which is empty where the two lanes butt.</summary>
    public ReadOnlySpan<ArcSeg> ArcsOfConnector(int connector) =>
        ConnectorArcs.AsSpan(
            ConnectorArcOffsets[connector], ConnectorArcOffsets[connector + 1] - ConnectorArcOffsets[connector]);

    /// <summary>
    /// <b>Every lane and every connector in the town, laid off the plan's roads.</b> The roads are cut at the
    /// junctions they pass through and at the nodes a car park asks for, each stretch is given the lanes its
    /// road's flow declares, the lanes are cut back until every turn through them holds the junction's corner,
    /// and the connectors are drawn between the ends that leaves.
    /// </summary>
    public static LaneLines Of(GroundPieces ground, SimConfig config)
    {
        var roads = ground.Roads;
        var junctions = ground.Junctions;
        var discs = RoadCuts.JunctionIndex(ground, paddingM: 0f);
        var sections = ParkingSections.Lay(ground, config, junctions.Count);

        var laneRoad = new List<int>();
        var laneWidthM = new List<float>();
        var laneFromNode = new List<int>();
        var laneToNode = new List<int>();
        var laneForward = new List<bool>();
        var laneLengthM = new List<float>();
        var laneReverse = new List<int>();
        var laneEndsAtAPlace = new List<bool>();
        var laneArcOffsets = new List<int> { 0 };
        var laneArcs = new List<ArcSeg>();

        var cuts = new List<RoadCut>();
        var scratch = new ArcSeg[MaxArcsPerStretch(roads)];
        var reversed = new ArcSeg[scratch.Length];
        var offset = new ArcSeg[scratch.Length];

        for (var road = 0; road < roads.Count; road++)
        {
            var centreline = roads.SegmentsOf(road);
            if (centreline.Length == 0) continue;

            var lengthM = Spline.TotalLengthM(centreline);
            RoadCuts.Along(
                ground, discs, centreline, lengthM, paddingM: 0f, roads.FromJunction[road], roads.ToJunction[road],
                cuts, sections.On(road), config.ParkingSectionShortestStretchM);

            // A road's own lane offset comes from the road's own declared width, because the
            // catalogue's figure is a default and everything derived from it follows the road's
            // (TER-4). Each direction has its share of the carriageway and a lane's line is the middle
            // of that share, so one number is both the offset and half a lane — one site for the
            // relation, which is why the lane's width is taken from it and not worked out again. <b>A
            // one-way road's share is the whole of it</b> (TER-4d): one lane, laid down the middle,
            // and the road is the narrower for it rather than the emptier. That middle is where a road of
            // two ways carries the same-way lane, because the narrow road stands on the half of the
            // carriageway it is driven and not in the middle of it.
            var runsWithTheRoad = roads.Flow[road] != RoadFlow.AgainstTheRoad;
            var runsAgainstIt = roads.Flow[road] != RoadFlow.WithTheRoad;
            var halfLaneM = roads.WidthM[road] * 0.5f / roads.LanesOn(road);
            var laneOffsetM = roads.LanesOn(road) == 1 ? 0f : halfLaneM * config.RoadSideSign;

            for (var cut = 0; cut + 1 < cuts.Count; cut++)
            {
                var fromM = cuts[cut].ExitM;
                var toM = MathF.Max(fromM, cuts[cut + 1].EnterM);
                // Nothing between two discs is nothing to drive on: where a road passes straight from
                // one junction into the next the two share their ground, and the connectors either
                // side of the pair are what a car crosses it on.
                var arcCount = Spline.SubChainInto(centreline, fromM, toM, scratch);
                if (arcCount == 0) continue;

                var forward = laneRoad.Count;
                var backward = forward + (runsWithTheRoad ? 1 : 0);

                if (runsWithTheRoad)
                {
                    Spline.OffsetInto(scratch.AsSpan(0, arcCount), laneOffsetM, offset);
                    AddLane(
                        road, halfLaneM, cuts[cut].Junction, cuts[cut + 1].Junction, true,
                        offset.AsSpan(0, arcCount), runsAgainstIt ? backward : NoLane);
                }

                if (runsAgainstIt)
                {
                    Spline.ReverseInto(scratch.AsSpan(0, arcCount), reversed);
                    Spline.OffsetInto(reversed.AsSpan(0, arcCount), laneOffsetM, offset);
                    AddLane(
                        road, halfLaneM, cuts[cut + 1].Junction, cuts[cut].Junction, false,
                        offset.AsSpan(0, arcCount), runsWithTheRoad ? forward : NoLane);
                }
            }
        }

        // <b>The cuts are how the lanes were laid and are not a table that is kept</b>. They number the
        // plan's junctions first and the places a slice above asked for after them (GEN-4h), which is what
        // says whether a lane end is an intersection or a cut — and having said it, they are done with.
        var nodeCount = junctions.Count + sections.NodeCount;
        var wholeOffsets = laneArcOffsets.ToArray();
        var wholeArcs = laneArcs.ToArray();
        var wholeLengths = laneLengthM.ToArray();
        var (outOffsets, outLanes) = Adjacency(nodeCount, laneFromNode);
        var (connectorAt, connectorToLane, connectorKind) = Connectors(
            config, laneToNode, laneReverse, outOffsets, outLanes, wholeOffsets, wholeArcs);

        // <b>A lane ends where its movements hand over</b> (TER-5d): the cut back is settled over the whole
        // stretch and then taken off the line, so a lane's own last point is where every connector out of it
        // starts and the ground past it is the connectors' alone.
        var (arrivingM, leavingM) = Setbacks(
            config, wholeOffsets, wholeArcs, wholeLengths, connectorAt, connectorToLane);
        var lanes = CutBackToTheConnectors(
            wholeOffsets, wholeArcs, wholeLengths, arrivingM, leavingM, scratch.Length);

        var (connectorArcOffsets, connectorArcs, connectorLengthM) = LayConnectorLines(
            lanes, connectorAt, connectorToLane);

        return new LaneLines(
            junctions.Count, nodeCount, [.. laneRoad], [.. laneWidthM], [.. laneFromNode], [.. laneToNode],
            AtAJunction(laneFromNode, junctions.Count), AtAJunction(laneToNode, junctions.Count),
            [.. laneForward], [.. laneReverse], [.. laneEndsAtAPlace],
            lanes.LengthM, lanes.CutBackM, lanes.ArcOffsets, lanes.Arcs,
            connectorAt, connectorToLane, connectorKind,
            connectorArcOffsets, connectorArcs, connectorLengthM);

        void AddLane(
            int road, float halfLaneM, int fromNode, int toNode, bool forward, ReadOnlySpan<ArcSeg> arcs, int reverse)
        {
            laneRoad.Add(road);
            laneWidthM.Add(halfLaneM * 2f);
            laneFromNode.Add(fromNode);
            laneToNode.Add(toNode);
            laneForward.Add(forward);
            laneReverse.Add(reverse);
            laneEndsAtAPlace.Add(toNode >= junctions.Count);
            foreach (var arc in arcs) laneArcs.Add(arc);
            laneArcOffsets.Add(laneArcs.Count);
            laneLengthM.Add(Spline.TotalLengthM(arcs));
        }
    }

    /// <summary>
    /// The lanes at each of the first <paramref name="nodeCount"/> cuts, by counting them into place. A lane
    /// at a cut past that count is left out, which is what makes this the plan's junctions alone when it is
    /// asked for those.
    /// </summary>
    public static (int[] Offsets, int[] Lanes) Adjacency(int nodeCount, IReadOnlyList<int> laneNode)
    {
        var offsets = new int[nodeCount + 1];
        var counted = 0;
        foreach (var node in laneNode)
        {
            if (node >= nodeCount) continue;

            offsets[node + 1]++;
            counted++;
        }

        for (var node = 0; node < nodeCount; node++) offsets[node + 1] += offsets[node];

        var lanes = new int[counted];
        var cursor = new int[nodeCount];
        for (var node = 0; node < nodeCount; node++) cursor[node] = offsets[node];

        for (var lane = 0; lane < laneNode.Count; lane++)
        {
            if (laneNode[lane] < nodeCount) lanes[cursor[laneNode[lane]]++] = lane;
        }

        return (offsets, lanes);
    }

    static int MaxArcsPerStretch(CityPlan.RoadArrays roads)
    {
        var most = 1;
        for (var road = 0; road < roads.Count; road++)
        {
            most = Math.Max(most, roads.SegmentOffsets[road + 1] - roads.SegmentOffsets[road]);
        }

        // A cut can fall inside a piece at either end, so a stretch holds at most every piece of its
        // road plus the two the cuts split.
        return most + 2;
    }

    /// <summary>
    /// A lane end named as the plan's junction, or <see cref="CityPlan.NoRecord"/> where the cut it stands at
    /// is a place a slice above asked for rather than an intersection (GEN-4h).
    /// </summary>
    static int[] AtAJunction(List<int> laneNode, int junctionCount)
    {
        var at = new int[laneNode.Count];
        for (var lane = 0; lane < laneNode.Count; lane++)
        {
            at[lane] = laneNode[lane] < junctionCount ? laneNode[lane] : CityPlan.NoRecord;
        }

        return at;
    }

    /// <summary>
    /// Every connector in the town, classified once. A lane's successors are the lanes leaving the node it
    /// arrives at, and the classification is the angle between the two lines where they meet — not the
    /// bearing of the roads, which says nothing about a street that bends through a junction.
    /// </summary>
    /// <remarks>
    /// <b>A lane leaving a node back the way this one came gets no connector at all</b> (TER-5f) — the
    /// reverse of this lane, and anything else facing it within the straight tolerance. It is left out of
    /// the table rather than classified and priced out of it, so nothing downstream carries a rule about a
    /// movement no car makes: no line is drawn for it, no ground is measured against it, no rank is given
    /// it, and the router cannot reach it. Coming back the way it went is a bay's (GEN-4l).
    /// </remarks>
    static (int[] At, int[] ToLane, LaneTurn[] Kind) Connectors(
        SimConfig config, List<int> laneToNode, List<int> laneReverse, int[] outOffsets, int[] outLanes,
        int[] laneArcOffsets, ArcSeg[] laneArcs)
    {
        var laneCount = laneToNode.Count;
        var offsets = new int[laneCount + 1];
        var toLane = new List<int>();
        var kind = new List<LaneTurn>();
        var straightRad = config.Road.TurnStraightToleranceDeg * MathF.PI / 180f;

        for (var lane = 0; lane < laneCount; lane++)
        {
            var arrivingRad = HeadingAt(laneArcOffsets, laneArcs, lane, atEnd: true);
            var node = laneToNode[lane];
            foreach (var leaving in outLanes.AsSpan(outOffsets[node], outOffsets[node + 1] - outOffsets[node]))
            {
                if (leaving == laneReverse[lane]) continue;

                var leavingRad = HeadingAt(laneArcOffsets, laneArcs, leaving, atEnd: false);
                var turnRad = Spline.WrapRad(leavingRad - arrivingRad);
                if (MathF.PI - MathF.Abs(turnRad) <= straightRad) continue;

                toLane.Add(leaving);
                kind.Add(MathF.Abs(turnRad) <= straightRad
                    ? LaneTurn.Straight
                    : MathF.Sign(turnRad) == MathF.Sign(config.RoadSideSign)
                        ? LaneTurn.NearSide
                        : LaneTurn.FarSide);
            }

            offsets[lane + 1] = toLane.Count;
        }

        return (offsets, [.. toLane], [.. kind]);
    }

    static float HeadingAt(int[] laneArcOffsets, ArcSeg[] laneArcs, int lane, bool atEnd)
    {
        if (atEnd)
        {
            var last = laneArcs[laneArcOffsets[lane + 1] - 1];
            return last.HeadingAtRad(last.LengthM);
        }

        return laneArcs[laneArcOffsets[lane]].HeadingRad;
    }

    /// <summary>
    /// How near two lane ends have to stand before the movement between them is no movement at all: the
    /// millimetre a place cut into a road (GEN-4h) leaves between its two lanes, which is float noise off
    /// two sub-chains of one curve and not a corner. Drawn rather than recognised, that noise is a biarc of
    /// two arcs a millimetre long whose curvature is enormous, and every lane in every car park would then
    /// set itself back a metre and a quarter to flatten a corner that is not there.
    /// </summary>
    const float SameEndM = 1e-3f;

    /// <summary>
    /// Every connector's line in the town, drawn once: <b>from the end of the lane it leaves to the start of
    /// the lane it arrives on</b>, which after the cut back are the two connection points themselves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A junction is a set of connection points and the connectors are what run between them</b>
    /// (TER-5d). A lane is cut back to the points its own movements hand over at
    /// (<see cref="CutBackToTheConnectors"/>), so its last point is where every movement out of it starts
    /// and its first point is where every movement into it lands. There is no figure a reader has to add to
    /// a lane's metres, and no ground carries both a lane and the line drawn across it.
    /// </para>
    /// <para>
    /// <b>A lane end has one point, whatever is driven off it.</b> The alternative — each connector taking
    /// exactly the run-in it needs — puts the boundary between a lane and the box in a different place for
    /// every movement out of it, so a lane has no one end and everything reading the pair has to say which
    /// movement it means. One point is worth the metres a straight gives up to it, and the straight it
    /// drives across the box instead is still a straight.
    /// </para>
    /// <para>
    /// <b>The radius asked for is the junction's and not the car's</b> (TER-5): the wedge where two
    /// carriageways meet is paved back to an arc tangent to both, and that arc <em>is</em> the line a
    /// turning car takes. It is the wider of the two figures — 2.5 car widths against the steering lock's
    /// own circle — so a connector drawn to it is one every car in the fleet has something in hand on.
    /// </para>
    /// <para>
    /// <b>Every connector is laid</b>, because the movement that could never be driven got none: a line
    /// between two opposing lanes is a semicircle at a lane's own spacing, tighter than the lock wherever it
    /// is drawn from. It is not a movement (TER-5f), so it is not a connector.
    /// </para>
    /// </remarks>
    static (int[] ArcOffsets, ArcSeg[] Arcs, float[] LengthM) LayConnectorLines(
        Lanes lanes, int[] connectorAt, int[] connectorToLane)
    {
        var connectorCount = connectorToLane.Length;
        var arcOffsets = new int[connectorCount + 1];
        var arcs = new List<ArcSeg>();
        var lengthM = new float[connectorCount];
        var drawn = new ArcSeg[2];

        for (var lane = 0; lane < lanes.LengthM.Length; lane++)
        {
            for (var connector = connectorAt[lane]; connector < connectorAt[lane + 1]; connector++)
            {
                var onto = connectorToLane[connector];
                var from = Spline.SampleAt(ArcsOfCut(lane), lanes.LengthM[lane]);
                var to = Spline.SampleAt(ArcsOfCut(onto), 0f);
                var laid = TheSameEnd(from.PositionM, to.PositionM)
                    ? 0
                    : Spline.BiarcInto(from.PositionM, from.HeadingRad, to.PositionM, to.HeadingRad, drawn);
                for (var arc = 0; arc < laid; arc++)
                {
                    arcs.Add(drawn[arc]);
                    lengthM[connector] += drawn[arc].LengthM;
                }

                arcOffsets[connector + 1] = arcs.Count;
            }
        }

        return (arcOffsets, [.. arcs], lengthM);

        ReadOnlySpan<ArcSeg> ArcsOfCut(int lane) =>
            lanes.Arcs.AsSpan(lanes.ArcOffsets[lane], lanes.ArcOffsets[lane + 1] - lanes.ArcOffsets[lane]);
    }

    /// <summary>Every lane cut back to the points its movements hand over at, and what that cost each of them.</summary>
    readonly record struct Lanes(int[] ArcOffsets, ArcSeg[] Arcs, float[] LengthM, float[] CutBackM);

    /// <summary>How finely the ladder of cut backs is stepped before the deepest one is taken.</summary>
    const int SetbackRungs = 8;

    /// <summary>
    /// How far into each end of each lane the corner reaches: one figure for the end of a lane and one for
    /// its start, widened a rung at a time until every turn through them holds the corner.
    /// </summary>
    /// <remarks>
    /// A junction is not sized around a turning circle (TER-5): its arms are cut at the disc, and two of
    /// them at right angles leave a corner tighter than the steering lock affords, so a car following that
    /// line exactly ends up on the pavement. Taking the last of one lane and the first of the next into the
    /// turn is what gives the arc its radius, and the smallest cut back whose tightest arc reaches the
    /// junction's own corner radius is what each turn asks for.
    /// <para>
    /// Widening in rounds rather than turn by turn is what makes a lane end one point: a cut taken for one
    /// turn changes the arc of every other turn sharing that end, so what each one needs is only settled
    /// once they all are.
    /// </para>
    /// </remarks>
    static (float[] ArrivingM, float[] LeavingM) Setbacks(
        SimConfig config, int[] laneArcOffsets, ArcSeg[] laneArcs, float[] laneLengthM, int[] connectorAt,
        int[] connectorToLane)
    {
        var laneCount = laneLengthM.Length;
        var drawn = new ArcSeg[2];
        var leavingM = new float[laneCount];
        var arrivingM = new float[laneCount];

        for (var round = 0; round <= SetbackRungs; round++)
        {
            var widened = false;
            for (var lane = 0; lane < laneCount; lane++)
            {
                for (var connector = connectorAt[lane]; connector < connectorAt[lane + 1]; connector++)
                {
                    var onto = connectorToLane[connector];
                    var capM = CapM(config, laneLengthM, lane, onto);
                    if (leavingM[lane] >= capM && arrivingM[onto] >= capM) continue;
                    if (HoldsTheCorner(config, laneArcOffsets, laneArcs, laneLengthM, lane, onto,
                            leavingM[lane], arrivingM[onto], drawn))
                    {
                        continue;
                    }

                    var rungM = capM / SetbackRungs;
                    leavingM[lane] = MathF.Min(capM, leavingM[lane] + rungM);
                    arrivingM[onto] = MathF.Min(capM, arrivingM[onto] + rungM);
                    widened = true;
                }
            }

            if (!widened) break;
        }

        return (arrivingM, leavingM);
    }

    /// <summary>
    /// <b>Every lane cut back to the two points its movements hand over at</b> (TER-5d), which is what makes
    /// those points the lane's own ends. What the boxes took is off the line rather than marked on it, so no
    /// reader has to know a lane's metres begin somewhere other than at nought, and no ground carries both a
    /// lane and the connector drawn across it.
    /// </summary>
    static Lanes CutBackToTheConnectors(
        int[] laneArcOffsets, ArcSeg[] laneArcs, float[] laneLengthM, float[] arrivingM, float[] leavingM,
        int mostArcs)
    {
        var laneCount = laneLengthM.Length;
        var arcOffsets = new int[laneCount + 1];
        var arcs = new List<ArcSeg>(laneArcs.Length);
        var lengthM = new float[laneCount];
        var cutBackM = new float[laneCount];
        var kept = new ArcSeg[mostArcs + 2];

        for (var lane = 0; lane < laneCount; lane++)
        {
            var whole = laneArcs.AsSpan(laneArcOffsets[lane], laneArcOffsets[lane + 1] - laneArcOffsets[lane]);
            var count = Spline.SubChainInto(whole, arrivingM[lane], laneLengthM[lane] - leavingM[lane], kept);
            for (var arc = 0; arc < count; arc++) arcs.Add(kept[arc]);

            arcOffsets[lane + 1] = arcs.Count;
            lengthM[lane] = Spline.TotalLengthM(kept.AsSpan(0, count));
            cutBackM[lane] = arrivingM[lane] + leavingM[lane];
        }

        return new Lanes(arcOffsets, [.. arcs], lengthM, cutBackM);
    }

    /// <summary>
    /// Whether the line one turn would be drawn at these two cut backs reaches the junction's corner
    /// radius — the question the widening asks of every turn through a lane end each round.
    /// </summary>
    static bool HoldsTheCorner(
        SimConfig config, int[] laneArcOffsets, ArcSeg[] laneArcs, float[] laneLengthM, int lane, int onto,
        float leavingM, float arrivingM, Span<ArcSeg> drawn)
    {
        var from = Spline.SampleAt(ArcsOf(lane), laneLengthM[lane] - leavingM);
        var to = Spline.SampleAt(ArcsOf(onto), arrivingM);
        if (TheSameEnd(from.PositionM, to.PositionM)) return true;

        var laid = Spline.BiarcInto(from.PositionM, from.HeadingRad, to.PositionM, to.HeadingRad, drawn);

        return laid == 0 || TightestRadiusM(drawn[..laid]) >= config.IntersectionCornerRadiusM;

        ReadOnlySpan<ArcSeg> ArcsOf(int of) =>
            laneArcs.AsSpan(laneArcOffsets[of], laneArcOffsets[of + 1] - laneArcOffsets[of]);
    }

    /// <summary>
    /// How much further into a lane the corner may ever be taken: the radius the junction was paved for, and
    /// never more than half of what the shorter of the two lanes can spare — because what is taken is cut
    /// off the lane, and a stretch has to be left standing for a car to be on.
    /// </summary>
    static float CapM(SimConfig config, float[] laneLengthM, int lane, int onto) =>
        MathF.Min(
            config.IntersectionCornerRadiusM,
            MathF.Max(0f, MathF.Min(laneLengthM[lane], laneLengthM[onto]) - config.LaneShortestStretchM) * 0.5f);

    /// <summary>Whether a lane ends where the next one starts, within <see cref="SameEndM"/>.</summary>
    static bool TheSameEnd(Vector2 fromM, Vector2 toM) =>
        (toM - fromM).LengthSquared() <= SameEndM * SameEndM;

    /// <summary>The tightest circle anywhere in a chain, which for a connector is the whole question about it.</summary>
    static float TightestRadiusM(ReadOnlySpan<ArcSeg> arcs)
    {
        var bend = 0f;
        foreach (var arc in arcs) bend = MathF.Max(bend, MathF.Abs(arc.Curvature));

        return bend <= 1e-6f ? float.PositiveInfinity : 1f / bend;
    }
}
