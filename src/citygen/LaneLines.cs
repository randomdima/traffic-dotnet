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
/// <b>The lines a car is driven on, laid with the town</b>: every lane of every road, and every movement
/// between them. It is the town's driving geometry and the whole of it — <b>there is no junction here</b>,
/// only the lanes that meet at one and the lines drawn between their ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the town's tarmac.</b> What a car may drive on is the ground under these lines and nothing
/// else, so <see cref="GroundShapes"/> answers a point against them and the boundary is the merge of the
/// ribbons they lay (<see cref="LaneShell"/>). A junction has no shape of its own to be laid or asked
/// about: the ground inside one is the ground its movements take, which is why a box that is turned,
/// skewed, one-way or five-armed is drawn correctly without anything here knowing what shape it made
/// (TER-5).
/// </para>
/// <para>
/// <b>Laid at map generation, off the plan alone</b> — it is a pure function of the roads, the junctions
/// they end at, the world seed and the figures on <see cref="SimConfig"/>. Both readings of the town take
/// it: the surface the ground is drawn and answered from, and the network <c>World.Road.RoadGraph</c> puts
/// its rules on top of. Neither lays a second one.
/// </para>
/// <para>
/// <b>A lane is placed and not subtracted</b> (TER-5i, GEN-46). Every arm of every node was given a drawn
/// bearing and a standoff before anything was shaped, and the road was laid to arrive on them
/// (<c>RoadStage.Chain</c>, GEN-47) — so a lane is that road's own line moved to its share of the
/// carriageway (TER-4d), it runs the whole of it, and its two ends are the connection points themselves.
/// Nothing is cut back, nothing is folded, and there is no figure a reader has to add to a lane's metres.
/// <b>But for lane zero</b> (<see cref="IsRoadside"/>), a road's roadside, which joins nothing and runs on into each
/// box to the corner its kerb makes (<see cref="RoadFromM"/>).
/// </para>
/// <para>
/// <b>What is left to work out here is the junction</b>: which pairs of lane ends a car may be driven
/// between, what turn each of those is, and the line it is driven on (GEN-48).
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
        int junctionCount, int[] laneRoad, float[] laneWidthM, byte[] laneFromKerb, int[] laneFromJunction,
        int[] laneToJunction, bool[] laneForward, int[] laneReverse, bool[] laneOverOneLine, bool[] laneCrossesToPass,
        bool[] laneIsBay, byte[] laneLevel, float[] laneLengthM, int[] laneArcOffsets, ArcSeg[] laneArcs, int firstRoadside,
        float[] roadsideInTheBoxAtStartM, float[] roadsideInTheBoxAtEndM, RoadsideLanes.Taper[] tapers,
        int[] connectorAt, int[] connectorToLane, LaneTurn[] connectorKind,
        int[] connectorArcOffsets, ArcSeg[] connectorArcs, float[] connectorLengthM)
    {
        JunctionCount = junctionCount;
        FirstRoadside = firstRoadside;
        _roadsideInTheBoxAtStartM = roadsideInTheBoxAtStartM;
        _roadsideInTheBoxAtEndM = roadsideInTheBoxAtEndM;
        Tapers = tapers;
        LaneRoad = laneRoad;
        LaneWidthM = laneWidthM;
        LaneFromKerb = laneFromKerb;
        LaneFromJunction = laneFromJunction;
        LaneToJunction = laneToJunction;
        LaneForward = laneForward;
        LaneReverse = laneReverse;
        LaneOverOneLine = laneOverOneLine;
        LaneCrossesToPass = laneCrossesToPass;
        (LaneInward, LaneOutward) = Beside(laneRoad, laneForward, laneFromKerb, firstRoadside);
        LaneIsBay = laneIsBay;
        LaneLevel = laneLevel;
        LaneLengthM = laneLengthM;
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

        _bridgeheads = Bridgeheads(junctionCount, laneFromJunction, laneToJunction, laneLevel);
    }

    /// <summary>Which junctions a lane of each level ends at, or nothing where every lane is on the ground.</summary>
    readonly bool[] _bridgeheads;

    /// <summary>
    /// <b>Whether a junction is where the level above lands on the ground</b> — a bridgehead (PHY-1a): a lane of
    /// each level ends at it. The level above is cut square there and the ground runs on under it
    /// (<see cref="LaneShell"/>).
    /// </summary>
    public bool IsBridgehead(int junction) => _bridgeheads.Length > 0 && _bridgeheads[junction];

    /// <summary>Whether any lane of the town is driven on a level of its own.</summary>
    public bool Levelled => _bridgeheads.Length > 0;

    static bool[] Bridgeheads(int junctionCount, int[] fromJunction, int[] toJunction, byte[] level)
    {
        if (Array.TrueForAll(level, static each => each == CityPlan.RoadArrays.Ground)) return [];

        var levels = new int[junctionCount];
        for (var lane = 0; lane < level.Length; lane++)
        {
            levels[fromJunction[lane]] |= 1 << level[lane];
            levels[toJunction[lane]] |= 1 << level[lane];
        }

        var bridgeheads = new bool[junctionCount];
        for (var junction = 0; junction < junctionCount; junction++)
        {
            bridgeheads[junction] = BitOperations.PopCount((uint)levels[junction]) > 1;
        }

        return bridgeheads;
    }

    /// <summary>How many intersections the plan named, which is every place a lane can begin or end.</summary>
    public int JunctionCount { get; }

    /// <summary>
    /// <b>The road a lane is one way of</b> (TER-5i). A lane runs between the two connection points of one
    /// road and never spans two: a place a carriageway merely bends is one road at the layout already
    /// (GEN-51), so there is nothing left for a lane to carry on through.
    /// </summary>
    public int[] LaneRoad { get; }

    /// <summary>How wide the ground this lane is driven on is — the share of its road's width it was given.</summary>
    public float[] LaneWidthM { get; }

    /// <summary>
    /// <b>Where the lane stands among its road's lanes running its way</b>, counted in from the kerb its traffic
    /// keeps to (TER-4d): nought is the kerb lane, and the last is beside the line the two ways meet on. A roadside
    /// outside the kerb lane is nought as well (<see cref="IsRoadside"/>).
    /// </summary>
    public byte[] LaneFromKerb { get; }

    /// <summary>The plan's junction a lane sets off from.</summary>
    public int[] LaneFromJunction { get; }

    /// <summary>And the one it arrives at.</summary>
    public int[] LaneToJunction { get; }

    /// <summary>
    /// Whether the lane runs with the direction of the road it sets off on (<see cref="LaneRoad"/>),
    /// which is what says which side of that road's centreline it is on.
    /// </summary>
    public bool[] LaneForward { get; }

    /// <summary>
    /// <b>The lane the other way beside this one</b> — across the line the two ways of its stretch meet on — or
    /// <see cref="NoLane"/> where the stretch runs one way or this lane does not stand on that line. On a road
    /// of one lane each way it is the other lane of the stretch.
    /// </summary>
    public int[] LaneReverse { get; }

    /// <summary>
    /// <b>Whether this lane and its reverse were laid on one line</b> rather than either side of a
    /// carriageway (<see cref="CityPlan.RoadArrays.DrivenOverOneLine"/>, GEN-4f) — the road's own answer
    /// carried down, not an offset read back off the two lines.
    /// </summary>
    public bool[] LaneOverOneLine { get; }

    /// <summary>
    /// <b>Whether a car on this lane may cross the line its two ways meet on to get past</b> (CAR-6.2b): its road's
    /// <see cref="CityPlan.RoadArrays.LineCrossedToPass"/>, carried down.
    /// </summary>
    public bool[] LaneCrossesToPass { get; }

    /// <summary>
    /// <b>The lane beside this one running its way, toward the line its two ways meet on</b> — one further in from
    /// the kerb — or <see cref="NoLane"/> where this is the innermost of its way or a roadside.
    /// </summary>
    public int[] LaneInward { get; }

    /// <summary>And the one toward the kerb, or <see cref="NoLane"/> for the kerb lane and a roadside.</summary>
    public int[] LaneOutward { get; }

    /// <summary>
    /// Each lane's neighbours running its way, read off the order <see cref="Of"/> lays a road's lanes in: a way's lanes
    /// side by side from its kerb, one index apart.
    /// </summary>
    static (int[] Inward, int[] Outward) Beside(int[] laneRoad, bool[] laneForward, byte[] laneFromKerb, int firstRoadside)
    {
        var inward = new int[laneRoad.Length];
        var outward = new int[laneRoad.Length];
        Array.Fill(inward, NoLane);
        Array.Fill(outward, NoLane);
        for (var lane = 0; lane + 1 < firstRoadside; lane++)
        {
            var next = lane + 1;
            if (laneRoad[next] != laneRoad[lane] || laneForward[next] != laneForward[lane]
                || laneFromKerb[next] != laneFromKerb[lane] + 1)
            {
                continue;
            }

            inward[lane] = next;
            outward[next] = lane;
        }

        return (inward, outward);
    }

    /// <summary>
    /// <b>Whether this lane is a car park's bay</b> (<see cref="CityPlan.RoadArrays.IsABay"/>, GEN-53): laid over one
    /// line, and besides that joined to nothing and got into by a car's own manoeuvre. A traced road of one lane
    /// both ways share is laid over one line too and is no bay.
    /// </summary>
    public bool[] LaneIsBay { get; }

    /// <summary>
    /// <b>Where the roadsides begin</b> (<see cref="IsRoadside"/>): every lane from this one on is lane zero, and the
    /// town's lanes number the same as they would if it laid none.
    /// </summary>
    public int FirstRoadside { get; }

    /// <summary>
    /// <b>Whether this lane is a road's roadside</b> — lane zero (<see cref="RoadsideLanes"/>,
    /// <see cref="CityPlan.RoadArrays.RoadsideWithM"/>, GEN-57): the strip between a kerb and the lanes, laid as a
    /// lane so that it is ground as every lane is, and joined to nothing. No movement leaves it or arrives on it, no
    /// junction counts it among its lanes, and nobody is driven down it.
    /// </summary>
    /// <remarks>
    /// <b>It does not stop at the box</b>, as every other lane does: it runs on into it to the corner its kerb makes
    /// (<see cref="RoadFromM"/>).
    /// </remarks>
    public bool IsRoadside(int lane) => lane >= FirstRoadside;

    readonly float[] _roadsideInTheBoxAtStartM;
    readonly float[] _roadsideInTheBoxAtEndM;

    /// <summary>
    /// <b>Where along a lane its road's own stretch begins</b>: nought for every lane but a roadside, which runs on
    /// into the box it starts at (<see cref="IsRoadside"/>).
    /// </summary>
    public float RoadFromM(int lane) => IsRoadside(lane) ? _roadsideInTheBoxAtStartM[lane - FirstRoadside] : 0f;

    /// <summary>And where it ends: the lane's own end for every lane but a roadside, which runs on into the next box.</summary>
    public float RoadToM(int lane) =>
        LaneLengthM[lane] - (IsRoadside(lane) ? _roadsideInTheBoxAtEndM[lane - FirstRoadside] : 0f);

    /// <summary>
    /// <b>The ground a kerb is eased in over where a roadside is lost along the way</b> (<see cref="RoadsideLanes.Tapers"/>):
    /// no lane, so nothing here numbers, joins or drives it, and the shell is merged over it as over a lane
    /// (<see cref="LaneShell"/>).
    /// </summary>
    public RoadsideLanes.Taper[] Tapers { get; }

    /// <summary>The level the lane is driven on, which is its road's (<see cref="CityPlan.RoadArrays.Level"/>).</summary>
    public byte[] LaneLevel { get; }

    /// <summary>
    /// <b>The level a connector is driven on</b>: the two lanes' it joins where they share one, and the ground where
    /// it joins a bridge to the road it lands on — a bridgehead stands on the ground, and nothing passes under it.
    /// </summary>
    public byte ConnectorLevel(int connector)
    {
        var level = LaneLevel[ConnectorFromLane[connector]];
        return level == LaneLevel[ConnectorToLane[connector]] ? level : CityPlan.RoadArrays.Ground;
    }

    /// <summary>The length of the line as driven, which is its road's own length at this lane's offset.</summary>
    public float[] LaneLengthM { get; }

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

    /// <summary>The line one lane is driven on, in its own direction of travel, whole from one end to the other.</summary>
    public ReadOnlySpan<ArcSeg> ArcsOf(int lane) =>
        LaneArcs.AsSpan(LaneArcOffsets[lane], LaneArcOffsets[lane + 1] - LaneArcOffsets[lane]);

    /// <summary>
    /// <b>How wide the ground a movement is driven over is: the narrower of the two lanes it joins</b>
    /// (TER-5d.1). One figure, read by everything that has to know what a box is paved with — the tarmac's
    /// own shape (<see cref="LaneShell"/>), the ground under a point (<c>GroundShapes</c>) and the picture.
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
        var laneFromKerb = new List<byte>();
        var laneFromJunction = new List<int>();
        var laneToJunction = new List<int>();
        var laneForward = new List<bool>();
        var laneLengthM = new List<float>();
        var laneReverse = new List<int>();
        var laneOverOneLine = new List<bool>();
        var laneCrossesToPass = new List<bool>();
        var laneIsBay = new List<bool>();
        var laneLevel = new List<byte>();
        var laneArcOffsets = new List<int> { 0 };
        var laneArcs = new List<ArcSeg>();

        var scratch = new ArcSeg[MaxArcsPerStretch(roads)];
        var offset = new ArcSeg[scratch.Length];
        var firstLaneOf = new int[roads.Count];
        Array.Fill(firstLaneOf, NoLane);

        for (var road = 0; road < roads.Count; road++)
        {
            var centreline = roads.SegmentsOf(road);
            if (centreline.Length == 0) continue;

            firstLaneOf[road] = laneRoad.Count;

            // A road's own lane width comes from the road's own declared width, because the catalogue's
            // figure is a default and everything derived from it follows the road's (TER-4). Each lane stands
            // where its road says it does, counted in from the kerb its own traffic keeps to
            // (<see cref="CityPlan.RoadArrays.LaneOffsetM"/>, TER-4d). <b>A bay is driven both ways over one
            // line</b> (GEN-53, GEN-4f): a car's width of ground a car stands on whichever way round it
            // stands, so its two lanes are the line itself and not two halves of a carriageway — and so is a
            // traced road OSM draws as one lane both ways share (GEN-57).
            var with = roads.LanesWithTheRoad(road);
            var against = roads.LanesAgainstTheRoad(road);
            var laneM = roads.LaneWidthM(road);
            var overOneLine = roads.DrivenOverOneLine(road);

            // <b>The reverse of a lane is the lane the other way beside it</b>: the two either side of the line
            // the two ways meet on, which is every lane of a road of one lane each way.
            var innermostWith = laneRoad.Count + with - 1;
            var innermostAgainst = laneRoad.Count + with + against - 1;

            for (var fromKerb = 0; fromKerb < with; fromKerb++)
            {
                Spline.OffsetInto(centreline, roads.LaneOffsetM(road, fromKerb, withTheRoad: true) * config.RoadSideSign, offset);
                AddLane(
                    road, laneM, fromKerb, roads.FromJunction[road], roads.ToJunction[road], true,
                    offset.AsSpan(0, centreline.Length),
                    fromKerb == with - 1 && against > 0 ? innermostAgainst : NoLane, overOneLine);
            }

            if (against > 0) Spline.ReverseInto(centreline, scratch);
            for (var fromKerb = 0; fromKerb < against; fromKerb++)
            {
                Spline.OffsetInto(
                    scratch.AsSpan(0, centreline.Length), roads.LaneOffsetM(road, fromKerb, withTheRoad: false) * config.RoadSideSign,
                    offset);
                AddLane(
                    road, laneM, fromKerb, roads.ToJunction[road], roads.FromJunction[road], false,
                    offset.AsSpan(0, centreline.Length),
                    fromKerb == against - 1 && with > 0 ? innermostWith : NoLane, overOneLine);
            }
        }

        var wholeOffsets = laneArcOffsets.ToArray();
        var wholeArcs = laneArcs.ToArray();
        var (outOffsets, outLanes) = Adjacency(junctions.Count, laneFromJunction);
        var (connectorAt, connectorToLane, connectorKind) = Connectors(
            config, roads, laneRoad, laneForward, laneFromKerb, laneToJunction, outOffsets, outLanes,
            wholeOffsets, wholeArcs);

        var (connectorArcOffsets, connectorArcs, connectorLengthM) = Movements(
            config, roads, laneRoad, wholeOffsets, wholeArcs, laneLengthM, connectorAt, connectorToLane);

        (connectorAt, connectorToLane, connectorKind, connectorArcOffsets, connectorArcs, connectorLengthM) =
            WhatTheJunctionOffers(
                config, connectorAt, connectorToLane, connectorKind, connectorArcOffsets, connectorArcs,
                connectorLengthM);

        // <b>Lane zero is laid after every lane a movement joins</b> (<see cref="IsRoadside"/>): joined to nothing, it
        // takes no part in the junctions above, and the lanes before it number as they would without it.
        var firstRoadside = laneRoad.Count;
        var inTheBoxAtStartM = new List<float>();
        var inTheBoxAtEndM = new List<float>();
        RoadsideLanes.Taper[] tapers = [];
        if (roads.RoadsideWithM.Length > 0)
        {
            var roadsides = RoadsideLanes.Of(
                ground,
                new RoadsideLanes.Joined(
                    firstLaneOf, laneFromJunction, laneToJunction, laneWidthM, connectorAt, connectorToLane,
                    connectorArcOffsets, connectorArcs),
                config);
            var line = new ArcSeg[roadsides.MostPieces];
            for (var road = 0; road < roads.Count; road++)
            {
                if (roads.SegmentsOf(road).Length == 0) continue;

                foreach (var withTheRoad in (ReadOnlySpan<bool>)[true, false])
                {
                    var stripM = roads.RoadsideM(road, withTheRoad);
                    if (stripM <= 0f) continue;

                    var pieces = roadsides.LineInto(road, withTheRoad, line, out var startM, out var endM);
                    var (from, to) = withTheRoad
                        ? (roads.FromJunction[road], roads.ToJunction[road])
                        : (roads.ToJunction[road], roads.FromJunction[road]);
                    AddLane(road, stripM, 0, from, to, withTheRoad, line.AsSpan(0, pieces), NoLane, false);
                    inTheBoxAtStartM.Add(startM);
                    inTheBoxAtEndM.Add(endM);
                }
            }

            tapers = [.. roadsides.Tapers];
        }

        if (laneRoad.Count > firstRoadside)
        {
            wholeOffsets = laneArcOffsets.ToArray();
            wholeArcs = laneArcs.ToArray();
            var lastConnector = connectorAt[^1];
            connectorAt = [.. connectorAt, .. Enumerable.Repeat(lastConnector, laneRoad.Count - firstRoadside)];
        }

        return new LaneLines(
            junctions.Count, [.. laneRoad], [.. laneWidthM], [.. laneFromKerb], [.. laneFromJunction],
            [.. laneToJunction], [.. laneForward], [.. laneReverse], [.. laneOverOneLine], [.. laneCrossesToPass], [.. laneIsBay],
            [.. laneLevel], [.. laneLengthM], wholeOffsets, wholeArcs, firstRoadside, [.. inTheBoxAtStartM], [.. inTheBoxAtEndM],
            tapers, connectorAt, connectorToLane, connectorKind,
            connectorArcOffsets, connectorArcs, connectorLengthM);

        void AddLane(
            int road, float laneM, int fromKerb, int fromJunction, int toJunction, bool forward,
            ReadOnlySpan<ArcSeg> arcs, int reverse, bool overOneLine)
        {
            laneRoad.Add(road);
            laneWidthM.Add(laneM);
            laneFromKerb.Add((byte)fromKerb);
            laneFromJunction.Add(fromJunction);
            laneToJunction.Add(toJunction);
            laneForward.Add(forward);
            laneReverse.Add(reverse);
            laneOverOneLine.Add(overOneLine);
            laneCrossesToPass.Add(roads.LineCrossedToPass(road));
            laneIsBay.Add(roads.IsABay(road));
            laneLevel.Add(roads.LevelOf(road));

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

    /// <summary>
    /// How many arcs the longest road in the town is, which is how many a lane of it is: a lane is its road's
    /// own line offset piece for piece (<see cref="Spline.OffsetInto"/>) and nothing is cut off either end.
    /// </summary>
    static int MaxArcsPerStretch(CityPlan.RoadArrays roads)
    {
        var most = 1;
        for (var road = 0; road < roads.Count; road++)
        {
            most = Math.Max(most, roads.SegmentOffsets[road + 1] - roads.SegmentOffsets[road]);
        }

        return most;
    }

    /// <summary>
    /// Every connector in the town, classified once. A lane's successors are the lanes leaving the node it
    /// arrives at, and the classification is the angle between the two lines where they meet — not the
    /// bearing of the roads, which says nothing about a street that bends through a junction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The angle is taken against the arriving lane carried on along its own curve</b> across the gap the
    /// node leaves between the two lane ends. Two lanes stand a junction's standoff back from their node on
    /// each side, and a line that keeps its curve through that gap has turned by what the curve turns and no
    /// more — so <b>going round a roundabout is straight on</b> (GEN-19): the ring bends through every node by
    /// the arc the node takes out of it, which read end to end is a turn towards the island, weaker than every
    /// entry it meets, where circulating traffic is to hold for nothing. A straight lane carries on straight,
    /// so nothing else is read differently.
    /// </para>
    /// <para>
    /// <b>A lane leaving a node back onto the road this one came in on gets no connector at all</b>
    /// (TER-5f). It is the reverse of this lane, named rather than measured: the two are the two ways of one
    /// link, so the turn in the road is banned by construction and not by an angle. It is left out of the
    /// table rather than classified and priced out of it, so nothing downstream carries a rule about a
    /// movement no car makes. Coming back the way it went is a bay's (GEN-4l).
    /// </para>
    /// <para>
    /// <b>A bay joins nothing</b> (GEN-53): its two nodes are its own, and at each the only lane leaving is its
    /// own reverse. A car gets into one and out of it by a manoeuvre of its own (GEN-4f).
    /// </para>
    /// <para>
    /// <b>And a turn the plan forbids is not a turn the node makes</b> (<see cref="CityPlan.RoadArrays.BannedTurns"/>,
    /// TER-5j): it is left out before the arm's lanes are shared between the turns there are, so the lanes go to
    /// those. A turn whose lanes the plan names (<see cref="CityPlan.RoadArrays.LaneLinks"/>) joins those, and an
    /// arm with arrows painted on it is made from as they say (<see cref="LaneUse"/>).
    /// </para>
    /// <para>
    /// <b>A lane joins the lane of its own number, unless that strands one</b> (<see cref="LaneUse"/>, GEN-50): a lane
    /// with no movement out lane for lane, or a lane leaving the node that no movement reaches lane for lane, is
    /// joined by the lanes' spread instead — a lane lost or gained along a street, or onto a road wider than the
    /// turns into it.
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
        SimConfig config, CityPlan.RoadArrays roads, List<int> laneRoad, List<bool> laneForward,
        List<byte> laneFromKerb, List<int> laneToJunction, int[] outOffsets, int[] outLanes, int[] laneArcOffsets,
        ArcSeg[] laneArcs)
    {
        var laneCount = laneToJunction.Count;
        var candidates = new List<Candidate>();
        var leftLaneForLane = new bool[laneCount];
        var reachedLaneForLane = new bool[laneCount];
        var straightRad = config.Road.TurnStraightToleranceDeg * MathF.PI / 180f;
        var turns = new LaneTurn?[MostLanesAtANode(outOffsets)];
        var bearsToTheKerb = new bool[turns.Length];
        var banned = roads.BannedTurns.ToHashSet();
        var links = roads.LaneLinks.ToHashSet();
        var linked = roads.LaneLinks.Select(link => new RoadTurn(link.Junction, link.FromRoad, link.ToRoad)).ToHashSet();

        for (var lane = 0; lane < laneCount; lane++)
        {
            var arriving = laneArcs[laneArcOffsets[lane + 1] - 1];
            var arrivingRad = arriving.HeadingAtRad(arriving.LengthM);
            var node = laneToJunction[lane];
            var leavingLanes = outLanes.AsSpan(outOffsets[node], outOffsets[node + 1] - outOffsets[node]);
            var offered = new LaneUse.Offered();
            for (var slot = 0; slot < leavingLanes.Length; slot++)
            {
                var leaving = leavingLanes[slot];
                turns[slot] = null;
                if (laneRoad[leaving] == laneRoad[lane] && laneForward[leaving] != laneForward[lane]) continue;
                if (banned.Contains(new RoadTurn(node, laneRoad[lane], laneRoad[leaving]))) continue;

                var starts = laneArcs[laneArcOffsets[leaving]];
                if (MathF.PI - MathF.Abs(Spline.WrapRad(starts.HeadingRad - arrivingRad)) <= LineTolerance.RoundingM) continue;

                var carriedOnRad = arrivingRad + CarriedOnRad(arriving.Curvature, (starts.StartM - arriving.EndM).Length());
                var turnRad = Spline.WrapRad(starts.HeadingRad - carriedOnRad);
                var turn = MathF.Abs(turnRad) <= straightRad
                    ? LaneTurn.Straight
                    : MathF.Sign(turnRad) == MathF.Sign(config.RoadSideSign)
                        ? LaneTurn.NearSide
                        : LaneTurn.FarSide;
                turns[slot] = turn;
                bearsToTheKerb[slot] = turnRad * config.RoadSideSign >= 0f;
                offered = offered.With(turn);
            }

            var lanesHere = LanesOfItsWay(roads, laneRoad[lane], laneForward[lane]);
            var marked = roads.MarkedTurnsOf(laneRoad[lane], laneForward[lane]);
            for (var slot = 0; slot < leavingLanes.Length; slot++)
            {
                if (turns[slot] is not { } turn) continue;

                var leaving = leavingLanes[slot];
                var lanesThere = LanesOfItsWay(roads, laneRoad[leaving], laneForward[leaving]);

                // A turn whose lanes the survey names is made between those and no others.
                var named = linked.Contains(new RoadTurn(node, laneRoad[lane], laneRoad[leaving]));
                var laneForLane = named
                    ? links.Contains(new LaneLink(node, laneRoad[lane], laneFromKerb[lane], laneRoad[leaving], laneFromKerb[leaving]))
                    : LaneUse.Joins(
                        offered, turn, marked, laneFromKerb[lane], lanesHere, laneFromKerb[leaving], lanesThere,
                        bearsToTheKerb[slot]);
                var shared = !named
                    && LaneUse.Shares(offered, turn, marked, laneFromKerb[lane], lanesHere, laneFromKerb[leaving], lanesThere);
                if (!laneForLane && !shared) continue;

                candidates.Add(new Candidate(lane, leaving, turn, laneForLane));
                leftLaneForLane[lane] |= laneForLane;
                reachedLaneForLane[leaving] |= laneForLane;
            }
        }

        var offsets = new int[laneCount + 1];
        var toLane = new List<int>(candidates.Count);
        var kind = new List<LaneTurn>(candidates.Count);
        var next = 0;
        for (var lane = 0; lane < laneCount; lane++)
        {
            for (; next < candidates.Count && candidates[next].Lane == lane; next++)
            {
                var candidate = candidates[next];
                if (!candidate.LaneForLane && leftLaneForLane[lane] && reachedLaneForLane[candidate.Leaving]) continue;

                toLane.Add(candidate.Leaving);
                kind.Add(candidate.Kind);
            }

            offsets[lane + 1] = toLane.Count;
        }

        return (offsets, [.. toLane], [.. kind]);
    }

    /// <summary>
    /// A movement a lane may be joined by: lane for lane (<see cref="LaneUse.Joins"/>), or only as the lanes' spread
    /// (<see cref="LaneUse.Shares"/>), which is made where lane for lane strands one of its two lanes.
    /// </summary>
    readonly record struct Candidate(int Lane, int Leaving, LaneTurn Kind, bool LaneForLane);

    /// <summary>How many lanes a road is driven in the way one of its lanes runs.</summary>
    static int LanesOfItsWay(CityPlan.RoadArrays roads, int road, bool forward) =>
        forward ? roads.LanesWithTheRoad(road) : roads.LanesAgainstTheRoad(road);

    /// <summary>The most lanes that set off from any one node.</summary>
    static int MostLanesAtANode(int[] outOffsets)
    {
        var most = 0;
        for (var node = 0; node + 1 < outOffsets.Length; node++)
        {
            most = Math.Max(most, outOffsets[node + 1] - outOffsets[node]);
        }

        return most;
    }

    /// <summary>
    /// How far a heading turns carried on along a curve of this curvature to a point a chord this long away —
    /// twice the half-angle the chord subtends, and nothing on a straight.
    /// </summary>
    static float CarriedOnRad(float curvature, float chordM) =>
        2f * MathF.Asin(Math.Clamp(chordM * curvature * 0.5f, -1f, 1f));

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
        SimConfig config, CityPlan.RoadArrays roads, List<int> laneRoad, int[] laneArcOffsets,
        ArcSeg[] laneArcs, List<float> laneLengthM, int[] connectorAt, int[] connectorToLane)
    {
        var connectorCount = connectorToLane.Length;
        var arcOffsets = new int[connectorCount + 1];
        var arcs = new List<ArcSeg>();
        var lengthM = new float[connectorCount];
        var drawn = new ArcSeg[3];
        var joined = new ArcSeg[3];

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
