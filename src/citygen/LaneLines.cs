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
/// <b>A lane runs from one place a driver decides something to the next, and is cut nowhere else</b>
/// (TER-5h). It is laid in four steps and they are the whole of what happens here:
/// </para>
/// <list type="number">
/// <item>the roads are cut at every junction they run through rather than the two they name, so an inline
/// junction is a place the network has heard of — and <b>nothing else cuts one</b> (GEN-4h), a car park
/// along a frontage being bays hanging off a lane rather than a lane of its own;</item>
/// <item>each stretch is given the lanes its road's flow declares, at the share of the carriageway that
/// flow leaves it (TER-4d);</item>
/// <item>the lanes are cut back until every turn through them holds the junction's own corner (TER-5d), and
/// the connectors are drawn between the ends that leaves;</item>
/// <item><b>and every join that forks nothing is folded back into the lane it joins</b>
/// (<see cref="Welded"/>, TER-5h): one way in, one way out and no choice between them is not a junction,
/// whatever the plan called it, so the two stretches and the line across the box are one lane.</item>
/// </list>
/// <para>
/// <b>The fold is settled on the movements and never on the arms</b>, because the arms do not say it: a
/// node of two arms carrying a one-way street into a two-way one forks nothing either, and a node of four
/// that only ever offered one movement is a node the plan should not have laid. What the count of ways out
/// of a lane and ways into the next asks is exactly the question — <em>does a driver decide anything
/// here</em> — and it asks it of the table the router will read.
/// </para>
/// <para>
/// <b>Structure of arrays, laid once</b>, with every variable-length run — a lane's arcs, a lane's
/// connectors — a flat array and an offsets array beside it.
/// </para>
/// </remarks>
internal sealed partial class LaneLines
{
    /// <summary>Where a lane is asked for and the town has none — the reverse of a one-way stretch (TER-4d).</summary>
    public const int NoLane = -1;

    LaneLines(
        int junctionCount, int[] laneFromRoad, int[] laneToRoad, float[] laneWidthM, int[] laneFromJunction,
        int[] laneToJunction, bool[] laneForward, int[] laneReverse, float[] laneLengthM,
        float[] laneCutBackAtStartM, float[] laneCutBackAtEndM, int[] laneArcOffsets, ArcSeg[] laneArcs,
        int[] connectorAt, int[] connectorToLane, LaneTurn[] connectorKind, int[] connectorArcOffsets,
        ArcSeg[] connectorArcs, float[] connectorLengthM)
    {
        JunctionCount = junctionCount;
        LaneFromRoad = laneFromRoad;
        LaneToRoad = laneToRoad;
        LaneWidthM = laneWidthM;
        LaneFromJunction = laneFromJunction;
        LaneToJunction = laneToJunction;
        LaneForward = laneForward;
        LaneReverse = laneReverse;
        LaneLengthM = laneLengthM;
        LaneCutBackAtStartM = laneCutBackAtStartM;
        LaneCutBackAtEndM = laneCutBackAtEndM;
        LaneArcOffsets = laneArcOffsets;
        LaneArcs = laneArcs;
        ConnectorAt = connectorAt;
        ConnectorToLane = connectorToLane;
        ConnectorKind = connectorKind;
        ConnectorArcOffsets = connectorArcOffsets;
        ConnectorArcs = connectorArcs;
        ConnectorLengthM = connectorLengthM;

        LaneCutBackM = new float[laneFromRoad.Length];
        for (var lane = 0; lane < LaneCount; lane++)
        {
            LaneCutBackM[lane] = laneCutBackAtStartM[lane] + laneCutBackAtEndM[lane];
        }

        // Which lane a connector leaves is the run it stands in, so it is folded out once rather than
        // searched for: a caller holding an id asks both its ends the same way.
        ConnectorFromLane = new int[connectorToLane.Length];
        for (var lane = 0; lane < LaneCount; lane++)
        {
            for (var id = connectorAt[lane]; id < connectorAt[lane + 1]; id++) ConnectorFromLane[id] = lane;
        }
    }

    /// <summary>How many intersections the plan named, which is every place a lane can begin or end.</summary>
    public int JunctionCount { get; }

    /// <summary>
    /// <b>The road a lane sets off on</b>, which is not necessarily the one it arrives on
    /// (<see cref="LaneToRoad"/>): a lane folded through a node that forks nothing carries on onto whatever
    /// road was on the far side of it (TER-5h).
    /// </summary>
    public int[] LaneFromRoad { get; }

    /// <summary>And the road it arrives on, which is the one an arm of a junction it ends at belongs to.</summary>
    public int[] LaneToRoad { get; }

    /// <summary>How wide the ground this lane is driven on is — the share of its road's width it was given.</summary>
    public float[] LaneWidthM { get; }

    /// <summary>The plan's junction a lane sets off from.</summary>
    public int[] LaneFromJunction { get; }

    /// <summary>And the one it arrives at.</summary>
    public int[] LaneToJunction { get; }

    /// <summary>
    /// Whether the lane runs with the direction of the road it sets off on (<see cref="LaneFromRoad"/>),
    /// which is what says which side of that road's centreline it is on.
    /// </summary>
    public bool[] LaneForward { get; }

    /// <summary>The other lane of the same stretch, or <see cref="NoLane"/> where the stretch runs one way.</summary>
    public int[] LaneReverse { get; }

    /// <summary>The length of the line as driven, after the cut back.</summary>
    public float[] LaneLengthM { get; }

    /// <summary>How much of its stretch the lane gave up to the box at its start, and at its end.</summary>
    /// <remarks>
    /// Kept apart because the fold takes a lane's two ends from two different stretches (TER-5h), and what
    /// the boxes in between cost is no cost at all: those metres came back as the join line the lane now
    /// runs along.
    /// </remarks>
    public float[] LaneCutBackAtStartM { get; }

    /// <inheritdoc cref="LaneCutBackAtStartM"/>
    public float[] LaneCutBackAtEndM { get; }

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

    public int LaneCount => LaneFromRoad.Length;

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

    /// <summary>The plan's junction a movement crosses.</summary>
    public int JunctionOfConnector(int connector) => LaneToJunction[ConnectorFromLane[connector]];

    /// <summary>The line one connector is driven on, which is empty where the two lanes butt.</summary>
    public ReadOnlySpan<ArcSeg> ArcsOfConnector(int connector) =>
        ConnectorArcs.AsSpan(
            ConnectorArcOffsets[connector], ConnectorArcOffsets[connector + 1] - ConnectorArcOffsets[connector]);

    /// <summary>
    /// <b>Every lane and every connector in the town, laid off the plan's roads.</b> A lane is its road's
    /// own line moved to that lane's share of the carriageway, and it runs the whole of it: the road was
    /// laid to end on the connection points its two arms were drawn with (<c>RoadStage.Chain</c>), so the
    /// offset lands on them and there is nothing to cut back.
    /// </summary>
    public static LaneLines Of(GroundPieces ground, SimConfig config)
    {
        var roads = ground.Roads;
        var junctions = ground.Junctions;

        var laneRoad = new List<int>();
        var laneWidthM = new List<float>();
        var laneFromJunction = new List<int>();
        var laneToJunction = new List<int>();
        var laneForward = new List<bool>();
        var laneLengthM = new List<float>();
        var laneReverse = new List<int>();
        var laneArcOffsets = new List<int> { 0 };
        var laneArcs = new List<ArcSeg>();

        var scratch = new ArcSeg[MaxArcsPerStretch(roads)];
        var offset = new ArcSeg[scratch.Length];

        for (var road = 0; road < roads.Count; road++)
        {
            var centreline = roads.SegmentsOf(road);
            if (centreline.Length == 0) continue;

            // A road's own lane offset comes from the road's own declared width, because the catalogue's
            // figure is a default and everything derived from it follows the road's (TER-4). Each direction
            // has its share of the carriageway and a lane's line is the middle of that share, so one number
            // is both the offset and half a lane. <b>A one-way road's share is the whole of it</b>
            // (TER-4d): one lane, laid down the middle of a carriageway that was itself moved onto the half
            // its traffic drives (<c>RoadStage.OntoTheDrivenHalf</c>).
            var runsWithTheRoad = roads.Flow[road] != RoadFlow.AgainstTheRoad;
            var runsAgainstIt = roads.Flow[road] != RoadFlow.WithTheRoad;
            var halfLaneM = roads.WidthM[road] * 0.5f / roads.LanesOn(road);
            var laneOffsetM = roads.LanesOn(road) == 1 ? 0f : halfLaneM * config.RoadSideSign;

            var forward = laneRoad.Count;
            var backward = forward + (runsWithTheRoad ? 1 : 0);

            if (runsWithTheRoad)
            {
                Spline.OffsetInto(centreline, laneOffsetM, offset);
                AddLane(
                    road, halfLaneM, roads.FromJunction[road], roads.ToJunction[road], true,
                    offset.AsSpan(0, centreline.Length), runsAgainstIt ? backward : NoLane);
            }

            if (runsAgainstIt)
            {
                Spline.ReverseInto(centreline, scratch);
                Spline.OffsetInto(scratch.AsSpan(0, centreline.Length), laneOffsetM, offset);
                AddLane(
                    road, halfLaneM, roads.ToJunction[road], roads.FromJunction[road], false,
                    offset.AsSpan(0, centreline.Length), runsWithTheRoad ? forward : NoLane);
            }
        }

        var wholeOffsets = laneArcOffsets.ToArray();
        var wholeArcs = laneArcs.ToArray();
        var (outOffsets, outLanes) = Adjacency(junctions.Count, laneFromJunction);
        var (connectorAt, connectorToLane, connectorKind) = Connectors(
            config, laneToJunction, laneReverse, outOffsets, outLanes, wholeOffsets, wholeArcs);

        var (connectorArcOffsets, connectorArcs, connectorLengthM) = Movements(
            wholeOffsets, wholeArcs, laneLengthM, connectorAt, connectorToLane);

        (connectorAt, connectorToLane, connectorKind, connectorArcOffsets, connectorArcs, connectorLengthM) =
            WhatTheJunctionOffers(
                config, connectorAt, connectorToLane, connectorKind, connectorArcOffsets, connectorArcs,
                connectorLengthM);

        // <b>Nothing is cut back and nothing is folded</b> (§4.5, §6.4): a lane runs between the two points
        // it was drawn to run between, and the two fields that carried what a cut cost it come back nought.
        var noCutBackM = new float[laneRoad.Count];

        return new LaneLines(
            junctions.Count, [.. laneRoad], [.. laneRoad], [.. laneWidthM], [.. laneFromJunction],
            [.. laneToJunction], [.. laneForward], [.. laneReverse],
            [.. laneLengthM], noCutBackM, new float[laneRoad.Count], wholeOffsets, wholeArcs,
            connectorAt, connectorToLane, connectorKind,
            connectorArcOffsets, connectorArcs, connectorLengthM);

        void AddLane(
            int road, float halfLaneM, int fromJunction, int toJunction, bool forward,
            ReadOnlySpan<ArcSeg> arcs, int reverse)
        {
            laneRoad.Add(road);
            laneWidthM.Add(halfLaneM * 2f);
            laneFromJunction.Add(fromJunction);
            laneToJunction.Add(toJunction);
            laneForward.Add(forward);
            laneReverse.Add(reverse);

            foreach (var arc in arcs) laneArcs.Add(arc);

            laneArcOffsets.Add(laneArcs.Count);
            laneLengthM.Add(Spline.TotalLengthM(arcs));
        }
    }

    /// <summary>
    /// <b>The movements a junction actually offers</b> (§4.3): the ones a car at the junction's own design
    /// speed can hold. A pair the geometry cannot join inside that bound is a movement the junction does not
    /// make, and it is left out of the table rather than priced out of it — nothing downstream then carries
    /// a rule about a line no car drives.
    /// </summary>
    /// <remarks>
    /// <b>But a junction may not refuse its way out of being reachable</b> (GEN-5). A lane whose every
    /// movement is too tight keeps the loosest of them, and so does a lane every movement <em>onto</em>
    /// which is too tight: a car that arrives has to leave and a lane nothing reaches is a hole in the
    /// drivable region rather than a corner nobody takes.
    /// </remarks>
    static (int[] At, int[] ToLane, LaneTurn[] Kind, int[] ArcOffsets, ArcSeg[] Arcs, float[] LengthM)
        WhatTheJunctionOffers(
            SimConfig config, int[] at, int[] toLane, LaneTurn[] kind, int[] arcOffsets, ArcSeg[] arcs,
            float[] lengthM)
    {
        var floorM = config.JunctionCorneringRadiusM;
        var keptAt = new int[at.Length];
        var keptToLane = new List<int>(toLane.Length);
        var keptKind = new List<LaneTurn>(toLane.Length);
        var keptArcOffsets = new List<int> { 0 };
        var keptArcs = new List<ArcSeg>(arcs.Length);
        var keptLengthM = new List<float>(toLane.Length);

        // The loosest way out of each lane, and the loosest way onto it: both are kept whatever they come
        // to, because a lane has to be leavable and reachable.
        var loosestOut = new int[at.Length - 1];
        var loosestIn = new int[at.Length - 1];
        var outM = new float[at.Length - 1];
        var inM = new float[at.Length - 1];
        Array.Fill(loosestOut, -1);
        Array.Fill(loosestIn, -1);

        for (var lane = 0; lane + 1 < at.Length; lane++)
        {
            for (var connector = at[lane]; connector < at[lane + 1]; connector++)
            {
                var radiusM = TightestRadiusM(Run(arcs, arcOffsets, connector));
                if (radiusM > outM[lane])
                {
                    loosestOut[lane] = connector;
                    outM[lane] = radiusM;
                }

                var onto = toLane[connector];
                if (radiusM > inM[onto])
                {
                    loosestIn[onto] = connector;
                    inM[onto] = radiusM;
                }
            }
        }

        for (var lane = 0; lane + 1 < at.Length; lane++)
        {
            for (var connector = at[lane]; connector < at[lane + 1]; connector++)
            {
                var radiusM = TightestRadiusM(Run(arcs, arcOffsets, connector));
                if (radiusM < floorM
                    && connector != loosestOut[lane]
                    && connector != loosestIn[toLane[connector]])
                {
                    continue;
                }

                keptToLane.Add(toLane[connector]);
                keptKind.Add(kind[connector]);
                keptLengthM.Add(lengthM[connector]);
                foreach (var arc in Run(arcs, arcOffsets, connector)) keptArcs.Add(arc);

                keptArcOffsets.Add(keptArcs.Count);
            }

            keptAt[lane + 1] = keptToLane.Count;
        }

        return (keptAt, [.. keptToLane], [.. keptKind], [.. keptArcOffsets], [.. keptArcs], [.. keptLengthM]);

        static ReadOnlySpan<ArcSeg> Run(ArcSeg[] arcs, int[] offsets, int connector) =>
            arcs.AsSpan(offsets[connector], offsets[connector + 1] - offsets[connector]);
    }

    /// <summary>The lanes at each of the plan's junctions, by counting them into place.</summary>
    public static (int[] Offsets, int[] Lanes) Adjacency(int junctionCount, IReadOnlyList<int> laneJunction)
    {
        var offsets = new int[junctionCount + 1];
        foreach (var junction in laneJunction) offsets[junction + 1]++;

        for (var junction = 0; junction < junctionCount; junction++) offsets[junction + 1] += offsets[junction];

        var lanes = new int[laneJunction.Count];
        var cursor = new int[junctionCount];
        for (var junction = 0; junction < junctionCount; junction++) cursor[junction] = offsets[junction];

        for (var lane = 0; lane < laneJunction.Count; lane++) lanes[cursor[laneJunction[lane]]++] = lane;

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
    /// Every connector in the town, classified once. A lane's successors are the lanes leaving the node it
    /// arrives at, and the classification is the angle between the two lines where they meet — not the
    /// bearing of the roads, which says nothing about a street that bends through a junction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A lane leaving a node back onto the road this one came in on gets no connector at all</b>
    /// (TER-5f). It is the reverse of this lane, named rather than measured: the two are the two ways of one
    /// link, so the turn in the road is banned by construction and not by an angle. It is left out of the
    /// table rather than classified and priced out of it, so nothing downstream carries a rule about a
    /// movement no car makes. Coming back the way it went is a bay's (GEN-4l).
    /// </para>
    /// <para>
    /// <b>And a sharp turn is a sharp turn and not a reversal.</b> Two arms may be drawn as little as
    /// <c>ArmsApartMinDeg</c> apart and the bearings they end on are drawn either side of that
    /// (<see cref="ConnectionPoints"/>), so a movement between them turns through most of a half-circle —
    /// which is a corner a car takes slowly and what the design-speed bound is for, rather than a line to
    /// leave out. What is left out on the angle is only a pair that genuinely face each other.
    /// </para>
    /// </remarks>
    static (int[] At, int[] ToLane, LaneTurn[] Kind) Connectors(
        SimConfig config, List<int> laneToJunction, List<int> laneReverse, int[] outOffsets, int[] outLanes,
        int[] laneArcOffsets, ArcSeg[] laneArcs)
    {
        var laneCount = laneToJunction.Count;
        var offsets = new int[laneCount + 1];
        var toLane = new List<int>();
        var kind = new List<LaneTurn>();
        var straightRad = config.Road.TurnStraightToleranceDeg * MathF.PI / 180f;

        for (var lane = 0; lane < laneCount; lane++)
        {
            var arrivingRad = HeadingAt(laneArcOffsets, laneArcs, lane, atEnd: true);
            var node = laneToJunction[lane];
            foreach (var leaving in outLanes.AsSpan(outOffsets[node], outOffsets[node + 1] - outOffsets[node]))
            {
                if (leaving == laneReverse[lane]) continue;

                var leavingRad = HeadingAt(laneArcOffsets, laneArcs, leaving, atEnd: false);
                var turnRad = Spline.WrapRad(leavingRad - arrivingRad);
                if (MathF.PI - MathF.Abs(turnRad) <= LineTolerance.RoundingM) continue;

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
    /// millimetre a junction that takes no ground off its arms leaves between them, which is float noise and
    /// not a corner. Drawn rather than recognised, that noise is a biarc of two arcs a millimetre long whose
    /// curvature is enormous, and the two lanes would then set themselves back a metre and a quarter to
    /// flatten a corner that is not there.
    /// </summary>
    /// <remarks>
    /// <b>No town lays one</b> — a lane ends at a junction and a junction takes its disc's worth of ground
    /// (GEN-4h), so the census reports nought butting joins on every map. It is the tolerance a construction
    /// over floats owes itself and not a case the plan produces.
    /// </remarks>
    const float SameEndM = 1e-3f;

    /// <summary>
    /// Every movement's line in the town, drawn once: <b>from the point the lane it leaves ends on to the
    /// point the lane it arrives on begins at</b>, which are the two connection points themselves
    /// (TER-5d).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A junction is a set of connection points and the movements are what run between them.</b> Nothing
    /// is cut back to make room for one: a lane ends where it was drawn to end, and the ground past that
    /// point is the movements' alone. There is no figure a reader has to add to a lane's metres, and no
    /// ground carries both a lane and the line drawn across it.
    /// </para>
    /// <para>
    /// <b>A biarc between poses that face each other squarely is one arc</b>: the construction gives its two
    /// halves the same radius, and the joint between them is a point in the line nothing turns at
    /// (<see cref="Spline.JoinedInto"/>). Most of a town's movements are that pair.
    /// </para>
    /// </remarks>
    static (int[] ArcOffsets, ArcSeg[] Arcs, float[] LengthM) Movements(
        int[] laneArcOffsets, ArcSeg[] laneArcs, List<float> laneLengthM, int[] connectorAt,
        int[] connectorToLane)
    {
        var connectorCount = connectorToLane.Length;
        var arcOffsets = new int[connectorCount + 1];
        var arcs = new List<ArcSeg>();
        var lengthM = new float[connectorCount];
        var drawn = new ArcSeg[2];
        var joined = new ArcSeg[2];

        for (var lane = 0; lane < laneLengthM.Count; lane++)
        {
            for (var connector = connectorAt[lane]; connector < connectorAt[lane + 1]; connector++)
            {
                var onto = connectorToLane[connector];
                var from = Spline.SampleAt(ArcsOf(lane), laneLengthM[lane]);
                var to = Spline.SampleAt(ArcsOf(onto), 0f);
                var laid = TheSameEnd(from.PositionM, to.PositionM)
                    ? 0
                    : Spline.BiarcInto(from.PositionM, from.HeadingRad, to.PositionM, to.HeadingRad, drawn);

                laid = Spline.JoinedInto(drawn.AsSpan(0, laid), LineTolerance.RoundingM, joined);
                for (var arc = 0; arc < laid; arc++)
                {
                    arcs.Add(joined[arc]);
                    lengthM[connector] += joined[arc].LengthM;
                }

                arcOffsets[connector + 1] = arcs.Count;
            }
        }

        return (arcOffsets, [.. arcs], lengthM);

        ReadOnlySpan<ArcSeg> ArcsOf(int lane) =>
            laneArcs.AsSpan(laneArcOffsets[lane], laneArcOffsets[lane + 1] - laneArcOffsets[lane]);
    }

    /// <summary>Every lane cut back to the points its movements hand over at, and what that cost each end of them.</summary>
    readonly record struct Lanes(
        int[] ArcOffsets, ArcSeg[] Arcs, float[] LengthM, float[] ArrivingM, float[] LeavingM);

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
        var kept = new ArcSeg[mostArcs + 2];

        for (var lane = 0; lane < laneCount; lane++)
        {
            var whole = laneArcs.AsSpan(laneArcOffsets[lane], laneArcOffsets[lane + 1] - laneArcOffsets[lane]);
            var count = Spline.SubChainInto(whole, arrivingM[lane], laneLengthM[lane] - leavingM[lane], kept);
            for (var arc = 0; arc < count; arc++) arcs.Add(kept[arc]);

            arcOffsets[lane + 1] = arcs.Count;
            lengthM[lane] = Spline.TotalLengthM(kept.AsSpan(0, count));
        }

        return new Lanes(arcOffsets, [.. arcs], lengthM, arrivingM, leavingM);
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
