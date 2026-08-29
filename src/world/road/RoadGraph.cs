using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

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
/// The street network as the router and the follower need it: <b>one node per junction, directed lane
/// edges between them, and a turn classification per pair of lanes at a node</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A lane is the stretch of road between two junction discs, not a whole road.</b> Lanes are cut at
/// <em>every</em> junction a road runs through rather than only the two it names, which is what makes
/// an inline junction — a place <em>on</em> a road, carrying a mid-block crossing (TER-5b) — a node the
/// graph has heard of. The ground inside a disc belongs to no lane: what a car drives across it is a
/// connector the assembler draws between the lane in and the lane out, because an intersection is a
/// transition and never a destination (CAR-6.2a).
/// </para>
/// <para>
/// <b>Structure of arrays, laid once</b>, with every variable-length run — a lane's arcs, a node's
/// lanes, a lane's turns — a flat array and an offsets array beside it. Nothing here is rebuilt during
/// a tick and nothing here is queried by allocating.
/// </para>
/// <para>
/// <b>A crossing adds no node.</b> A zebra is a band of the same carriageway and nothing turns at one
/// (TER-6), so the graph never reads the crossing registry: what a car does at a crossing is a rule the
/// driver applies to the lane it is already on.
/// </para>
/// </remarks>
internal sealed class RoadGraph
{
    readonly int[] _laneArcOffsets;
    readonly ArcSeg[] _laneArcs;
    readonly int[] _nodeOutOffsets;
    readonly int[] _nodeOutLanes;
    readonly int[] _nodeInOffsets;
    readonly int[] _nodeInLanes;
    readonly int[] _turnOffsets;
    readonly int[] _turnToLane;
    readonly LaneTurn[] _turnKind;
    readonly Joins _joins;

    /// <summary>The lanes over a grid, which is the whole of what <see cref="NearestLane"/> is.</summary>
    readonly ChainIndex _nearest;

    RoadGraph(
        int junctionCount, Vector2[] nodeCentreM, int[] laneRoad, float[] laneWidthM, int[] laneFromNode,
        int[] laneToNode, bool[] laneForward,
        float[] laneLengthM, int[] laneArcOffsets, ArcSeg[] laneArcs, int[] laneReverse,
        int[] nodeOutOffsets, int[] nodeOutLanes, int[] nodeInOffsets, int[] nodeInLanes,
        int[] turnOffsets, int[] turnToLane, LaneTurn[] turnKind, Joins joins,
        WayCrossings crossings, float nearestCellM)
    {
        _joins = joins;
        Crossings = crossings;
        JunctionCount = junctionCount;
        NodeCentreM = nodeCentreM;
        LaneRoad = laneRoad;
        LaneWidthM = laneWidthM;
        LaneFromNode = laneFromNode;
        LaneToNode = laneToNode;
        LaneForward = laneForward;
        LaneLengthM = laneLengthM;
        LaneReverse = laneReverse;
        _laneArcOffsets = laneArcOffsets;
        _laneArcs = laneArcs;
        _nodeOutOffsets = nodeOutOffsets;
        _nodeOutLanes = nodeOutLanes;
        _nodeInOffsets = nodeInOffsets;
        _nodeInLanes = nodeInLanes;
        _turnOffsets = turnOffsets;
        _turnToLane = turnToLane;
        _turnKind = turnKind;

        var builder = new ChainIndex.Builder();
        for (var lane = 0; lane < laneRoad.Length; lane++) builder.Add(lane, ArcsOf(lane), laneLengthM[lane]);

        _nearest = builder.Seal(nearestCellM);

        for (var node = 0; node < NodeCount; node++)
        {
            var turns = 0;
            foreach (var lane in LanesIn(node)) turns += _turnOffsets[lane + 1] - _turnOffsets[lane];

            MostTurnsAtANode = Math.Max(MostTurnsAtANode, turns);
        }
    }

    /// <summary>
    /// One per junction the plan named, then one per end of every parking section (GEN-4h). <b>The plan's
    /// own index is the junction's</b>: nothing is renumbered, and the nodes cut into the roads afterwards
    /// are numbered after all of them.
    /// </summary>
    public int NodeCount => NodeCentreM.Length;

    /// <summary>How many of them the plan named. The rest are places on a road rather than intersections.</summary>
    public int JunctionCount { get; }

    /// <summary>Where each node stands: the junction's own centre, or the point on the road a cut was taken at.</summary>
    public Vector2[] NodeCentreM { get; }

    /// <summary>
    /// Whether a node is a place a slice above asked for rather than an intersection — <b>the end of a
    /// parking section</b>. It carries no signal, no bar and no corner, and the movement across it is a join
    /// of no length; what it is for is that the frontage beyond it is a stretch of the network in its own
    /// right, so a leg aimed into a car park is routed to a node like every other leg.
    /// </summary>
    public bool IsAPlace(int node) => node >= JunctionCount;

    public int LaneCount => LaneRoad.Length;

    /// <summary>Every turn in the town, which is also every join: the two are the same table.</summary>
    public int TurnCount => _turnToLane.Length;

    /// <summary>
    /// The most movements any one of the town's junctions admits — which is how many joins a body standing
    /// in a box can be lying under at once, and so how much room in the lane index one of them can want.
    /// </summary>
    public int MostTurnsAtANode { get; }

    public int[] LaneRoad { get; }

    /// <summary>
    /// How wide the lane is: half the carriageway its road declared, which is the number the lane's own
    /// line was offset by half of. <b>The road's figure and not the catalogue's</b> (TER-4).
    /// </summary>
    public float[] LaneWidthM { get; }

    public int[] LaneFromNode { get; }

    public int[] LaneToNode { get; }

    /// <summary>Whether the lane runs with the road's own direction, which is what says which side of the centreline it sits on.</summary>
    public bool[] LaneForward { get; }

    /// <summary>The length of the line as <em>driven</em>: an offset arc is shorter inside a bend than the centreline it was taken from.</summary>
    public float[] LaneLengthM { get; }

    /// <summary>
    /// The other lane of the same stretch — the one a car that has turned in a bay comes back down
    /// (GEN-4l), and the one no turn at either end of it ever leads to (TER-5f).
    /// </summary>
    public int[] LaneReverse { get; }

    /// <summary>The line the lane is driven on, in its own direction of travel, already offset to the driver's side.</summary>
    public ReadOnlySpan<ArcSeg> ArcsOf(int lane) =>
        _laneArcs.AsSpan(_laneArcOffsets[lane], _laneArcOffsets[lane + 1] - _laneArcOffsets[lane]);

    /// <summary>The lanes leaving a node.</summary>
    public ReadOnlySpan<int> LanesOut(int node) =>
        _nodeOutLanes.AsSpan(_nodeOutOffsets[node], _nodeOutOffsets[node + 1] - _nodeOutOffsets[node]);

    /// <summary>The lanes arriving at a node.</summary>
    public ReadOnlySpan<int> LanesIn(int node) =>
        _nodeInLanes.AsSpan(_nodeInOffsets[node], _nodeInOffsets[node + 1] - _nodeInOffsets[node]);

    /// <summary>The lanes a car on this one may leave for, at the node this one ends at.</summary>
    public ReadOnlySpan<int> TurnsFrom(int lane) =>
        _turnToLane.AsSpan(_turnOffsets[lane], _turnOffsets[lane + 1] - _turnOffsets[lane]);

    /// <summary>The kinds of those turns, in the same order.</summary>
    public ReadOnlySpan<LaneTurn> TurnKindsFrom(int lane) =>
        _turnKind.AsSpan(_turnOffsets[lane], _turnOffsets[lane + 1] - _turnOffsets[lane]);

    /// <summary>Where the <paramref name="turn"/>th way out of a lane stands in the town's own turn table.</summary>
    public int TurnSlotAt(int lane, int turn) => _turnOffsets[lane] + turn;

    /// <summary>The lane a turn arrives on, for a caller holding a slot rather than the pair it joins.</summary>
    public int TurnToLane(int slot) => _turnToLane[slot];

    /// <summary>
    /// The way a join is, in the numbering <see cref="Crossings"/> is laid in — the book's own
    /// (<see cref="LaneOccupancy.WayOfTurn"/>), forwarded here because that is where the table is.
    /// </summary>
    public int WayOfTurn(int turnSlot) => LaneOccupancy.WayOfTurn(LaneCount, turnSlot);

    /// <summary>And the trip back, for a caller holding a section's <see cref="CrossedSection.OnWay"/>.</summary>
    public int TurnOfWay(int way) => LaneOccupancy.TurnOfWay(LaneCount, way);

    /// <summary>
    /// <b>The right of way a movement through a box has</b> (TER-5e) — a fact about the turn it makes, so it
    /// is read off the road like the turn itself and is never worked out from the car making it.
    /// </summary>
    /// <remarks>
    /// <b>Straighter is stronger, and it is one order rather than a table of pairs.</b> A stream that turns
    /// out of nobody's way is driven over by the turns that leave it (TER-4a); the near-side turn crosses
    /// nothing of its own carriageway and is ordinary traffic; and the turn across the oncoming stream, the
    /// last movement a box admits, gives way to both.
    /// </remarks>
    public RightOfWay RightOfWayOfTurn(int slot) => RightOfWayOf(_turnKind[slot]);

    /// <summary>The same, for a caller holding the kind rather than the slot.</summary>
    public static RightOfWay RightOfWayOf(LaneTurn turn) => turn switch
    {
        LaneTurn.Straight => RightOfWay.StraightOn,
        LaneTurn.NearSide => RightOfWay.Traffic,
        _ => RightOfWay.TurningAcross,
    };

    /// <summary>Where a pair of lanes stands in it, or <see cref="NoTurn"/> where they are not joined at all.</summary>
    public int TurnSlot(int fromLane, int toLane)
    {
        for (var slot = _turnOffsets[fromLane]; slot < _turnOffsets[fromLane + 1]; slot++)
        {
            if (_turnToLane[slot] == toLane) return slot;
        }

        return NoTurn;
    }

    /// <summary>
    /// <b>The ground each way through a junction takes off the others</b> (TER-5c), laid once with the town
    /// like the joins it is measured off, and <b>indexed the way the book numbers ways</b>
    /// (<see cref="LaneOccupancy.WayOfTurn"/>). It is a property of the movement and never of the
    /// intersection: a street bending through a box is driven over nothing and takes nothing.
    /// </summary>
    /// <remarks>
    /// The lanes' own rows are empty, because a lane hands over clear of the box it ends at (TER-5d) and
    /// nothing a junction admits is driven over one. They are in the table so that a way laid off a
    /// junction — the line into a parking space, which sweeps the oncoming lane's metres — can name the
    /// ground it takes in the same table and be read by the same walk.
    /// </remarks>
    public WayCrossings Crossings { get; }

    /// <summary>Which turn joins these two lanes, or <see langword="null"/> where they are not joined at all.</summary>
    public LaneTurn? TurnBetween(int fromLane, int toLane)
    {
        var slot = TurnSlot(fromLane, toLane);
        return slot < 0 ? null : _turnKind[slot];
    }

    /// <summary>
    /// <b>The line a car is driven across the junction on</b>, laid once with the town: from the pose the
    /// arriving lane is left at to the pose the leaving lane is joined at.
    /// </summary>
    /// <remarks>
    /// It is the one piece of a driven line no plan carries, and it is held here rather than drawn per
    /// car so that <em>every</em> reader of it — the assembler that hands a car its line, the overlay
    /// that draws the movements through a box — reads the same arcs. A second copy of this shape drifts
    /// from the first, and the picture then argues with the simulation instead of showing it.
    /// </remarks>
    public ReadOnlySpan<ArcSeg> JoinArcs(int slot) =>
        _joins.Arcs.AsSpan(_joins.ArcOffsets[slot], _joins.ArcOffsets[slot + 1] - _joins.ArcOffsets[slot]);

    /// <summary>How far short of the arriving lane's end the join leaves it.</summary>
    public float JoinFromM(int slot) => _joins.FromM[slot];

    /// <summary>And how far into the lane it leaves for the join arrives.</summary>
    public float JoinToM(int slot) => _joins.ToM[slot];

    public float JoinLengthM(int slot) => _joins.LengthM[slot];

    /// <summary>
    /// How far into a lane every movement into it arrives (TER-5d) — the lane's own share of the setback,
    /// which is what says where a lane's metres begin under a line assembled through the junction behind
    /// it.
    /// </summary>
    public float JoinedAtM(int lane) => _joins.JoinedAtM[lane];

    /// <summary>
    /// And how far short of its end every movement out of it leaves (TER-5d), so that
    /// <c>JoinedAtM … LaneLengthM − LeftAtM</c> is <b>the stretch of a lane anything actually drives</b>.
    /// Past either figure the lane's own line runs on into the box, under a movement rather than under
    /// itself — a reader that draws the whole lane draws that ground twice and leaves a spur nobody drives.
    /// </summary>
    public float LeftAtM(int lane) => _joins.LeftAtM[lane];

    /// <summary>The lane whose line passes nearest a point, and how far along it that is.</summary>
    /// <remarks>
    /// <b>It is not only asked of a car being stood up.</b> A car that has lost its line reacquires
    /// through here, a trip asks it for the lane a building's way out faces, and a bay is staged from
    /// it — all of which are decisions taken in a tick. Which is why it is <see cref="ChainIndex"/> and
    /// not a scan: the index is laid with the graph and cannot drift from it, because a graph laid with
    /// the town is never written to again.
    /// </remarks>
    public int NearestLane(Vector2 pointM, out float progressM) => _nearest.Nearest(pointM, out progressM);

    public SplineSample StartOf(int lane) => Spline.SampleAt(ArcsOf(lane), 0f);

    public SplineSample EndOf(int lane) => Spline.SampleAt(ArcsOf(lane), LaneLengthM[lane]);

    /// <summary>
    /// Whether a place stands inside one way's own band, and where on that way's line it falls.
    /// <b>The band the way is laid to and never a radius of the caller's choosing</b>: everything on the
    /// map is nearest some lane, and reading that lane's book for a body on the pavement beside it would
    /// hold a street up for the traffic it is parked next to.
    /// </summary>
    /// <remarks>
    /// <b>Across the way and along it, and the second half is not redundant.</b> A projection onto a way is
    /// clamped to that way's own ends (<see cref="Spline.ProjectM"/>), so a body standing past one of them
    /// answers at the endpoint — and measured across the band alone, anything lined up with a way's end is
    /// standing on it however far up the road it really is. What that put in the book was a body on joins it
    /// was nowhere near, which is a junction shut by a car in the next street. Inside the way the nearest
    /// point is square to the line and the second test costs nothing; it bites only where the clamp did.
    /// </remarks>
    /// <param name="reachM">How far past the band the body itself reaches — its own half-width or radius.</param>
    /// <param name="alongReachM">How far past a way's own end the body still lies on it — its own half-length or radius.</param>
    /// <param name="alongUnit">
    /// The way the line runs where <paramref name="alongM"/> falls on it. <b>Handed back rather than left
    /// to the caller</b>: this test measures across the band and along it, so it has already turned that
    /// angle into a direction, and a caller that asks <see cref="SplineSample.Direction"/> for it again
    /// reduces the same angle a third time.
    /// </param>
    public static bool WithinTheBand(
        ReadOnlySpan<ArcSeg> arcs, float alongM, Vector2 atM, float bandM, float reachM, float alongReachM,
        out Vector2 alongUnit)
    {
        var on = Spline.SampleAt(arcs, alongM);
        alongUnit = on.Direction;

        var offsetM = atM - on.PositionM;
        return MathF.Abs(Vector2.Dot(offsetM, Heading.RightOf(alongUnit))) <= (bandM * 0.5f) + reachM
               && MathF.Abs(Vector2.Dot(offsetM, alongUnit)) <= alongReachM;
    }

    public static RoadGraph Build(CityPlan plan, SimConfig config)
    {
        var roads = plan.Roads;
        var junctions = plan.Junctions;
        var discs = RoadCuts.JunctionIndex(plan, paddingM: 0f);
        var sections = ParkingSections.Lay(plan, config, junctions.Count);

        var laneRoad = new List<int>();
        var laneWidthM = new List<float>();
        var laneFromNode = new List<int>();
        var laneToNode = new List<int>();
        var laneForward = new List<bool>();
        var laneLengthM = new List<float>();
        var laneReverse = new List<int>();
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
                plan, discs, centreline, lengthM, paddingM: 0f, roads.FromJunction[road], roads.ToJunction[road], cuts,
                sections.On(road), config.ParkingSectionShortestStretchM);

            // A road's own lane offset comes from the road's own declared width, because the
            // catalogue's figure is a default and everything derived from it follows the road's
            // (TER-4). Each direction has half the carriageway and a lane's line is the middle of
            // its own half, so a quarter of a road is both the offset and half a lane — one number
            // and one site for the relation, which is why the lane's width is taken from it and not
            // worked out again.
            var halfLaneM = roads.WidthM[road] * 0.25f;
            var laneOffsetM = halfLaneM * config.RoadSideSign;

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
                var backward = forward + 1;

                Spline.OffsetInto(scratch.AsSpan(0, arcCount), laneOffsetM, offset);
                AddLane(
                    road, halfLaneM, cuts[cut].Junction, cuts[cut + 1].Junction, true, offset.AsSpan(0, arcCount),
                    backward);

                Spline.ReverseInto(scratch.AsSpan(0, arcCount), reversed);
                Spline.OffsetInto(reversed.AsSpan(0, arcCount), laneOffsetM, offset);
                AddLane(
                    road, halfLaneM, cuts[cut + 1].Junction, cuts[cut].Junction, false, offset.AsSpan(0, arcCount),
                    forward);
            }
        }

        var nodeCount = junctions.Count + sections.NodeCount;
        var nodeCentreM = new Vector2[nodeCount];
        junctions.CentreM.CopyTo(nodeCentreM, 0);
        sections.CentreM.CopyTo(nodeCentreM, junctions.Count);

        var arcOffsets = laneArcOffsets.ToArray();
        var arcs = laneArcs.ToArray();
        var laneLengths = laneLengthM.ToArray();
        var (outOffsets, outLanes) = Adjacency(nodeCount, laneFromNode);
        var (inOffsets, inLanes) = Adjacency(nodeCount, laneToNode);
        var (turnOffsets, turnToLane, turnKind) = Turns(
            config, laneToNode, laneReverse, outOffsets, outLanes, arcOffsets, arcs);

        var joins = LayJoins(config, arcOffsets, arcs, laneLengths, turnOffsets, turnToLane, turnKind);
        var crossings = LayCrossings(
            config, nodeCount, laneLengths.Length, inOffsets, inLanes, turnOffsets, turnToLane, joins);

        return new RoadGraph(
            junctions.Count, nodeCentreM, [.. laneRoad], [.. laneWidthM], [.. laneFromNode], [.. laneToNode],
            [.. laneForward],
            laneLengths, arcOffsets, arcs, [.. laneReverse],
            outOffsets, outLanes, inOffsets, inLanes, turnOffsets, turnToLane, turnKind, joins,
            crossings, config.NearestChainCellM);

        void AddLane(
            int road, float halfLaneM, int fromNode, int toNode, bool forward, ReadOnlySpan<ArcSeg> arcs, int reverse)
        {
            laneRoad.Add(road);
            laneWidthM.Add(halfLaneM * 2f);
            laneFromNode.Add(fromNode);
            laneToNode.Add(toNode);
            laneForward.Add(forward);
            laneReverse.Add(reverse);
            foreach (var arc in arcs) laneArcs.Add(arc);
            laneArcOffsets.Add(laneArcs.Count);
            laneLengthM.Add(Spline.TotalLengthM(arcs));
        }
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

    static (int[] Offsets, int[] Lanes) Adjacency(int nodeCount, List<int> laneNode)
    {
        var offsets = new int[nodeCount + 1];
        foreach (var node in laneNode) offsets[node + 1]++;
        for (var node = 1; node < offsets.Length; node++) offsets[node] += offsets[node - 1];

        var cursor = (int[])offsets.Clone();
        var lanes = new int[laneNode.Count];
        for (var lane = 0; lane < laneNode.Count; lane++) lanes[cursor[laneNode[lane]]++] = lane;

        return (offsets, lanes);
    }

    /// <summary>
    /// Every turn in the town, classified once. A lane's successors are the lanes leaving the node it
    /// arrives at, and the classification is the angle between the two lines where they meet — not the
    /// bearing of the roads, which says nothing about a street that bends through a junction.
    /// </summary>
    /// <remarks>
    /// <b>A lane leaving a node back the way this one came is not a successor at all</b> (TER-5f) — the
    /// reverse of this lane, and anything else facing it within the straight tolerance. It is left out of
    /// the table rather than classified and priced out of it, so nothing downstream carries a rule about a
    /// movement no car makes: no join is drawn for it, no ground is measured against it, no rank is given
    /// it, and the router cannot reach it. Coming back the way it went is a bay's (GEN-4l).
    /// </remarks>
    static (int[] Offsets, int[] ToLane, LaneTurn[] Kind) Turns(
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
    /// Every join in the town, drawn once: for each turn, the arcs across the box and how far into the
    /// two lanes they were taken.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The join is set back into both lanes only as far as it takes to reach the corner the junction
    /// was paved for.</b> A junction is not sized around a turning circle (TER-5): its arms are cut at
    /// the disc, and two of them at right angles leave a corner tighter than the steering lock affords,
    /// so a car following that line exactly ends up on the pavement. Taking the last of one lane and the
    /// first of the next into the turn is what gives the arc its radius, and the smallest setback whose
    /// tightest arc reaches the junction's own corner radius is what each turn asks for.
    /// </para>
    /// <para>
    /// <b>A lane end has one setback, and it is the widest its own turns asked for.</b> The alternative —
    /// each turn taking exactly what it needs — puts the boundary between a lane and the box in a
    /// different place for every movement out of it, so a lane has no one end, a straight and a turn hand
    /// over at two different points, and everything reading the pair has to say which movement it means.
    /// One point is worth the metres a straight gives up to it, and the straight it drives across the box
    /// instead is still a straight.
    /// </para>
    /// <para>
    /// <b>The radius asked for is the junction's and not the car's</b>: the wedge where two carriageways
    /// meet is paved back to an arc tangent to both, and that arc
    /// <em>is</em> the line a turning car takes. It is the wider of the two figures — 2.5 car widths
    /// against the steering lock's own circle — so a join drawn to it is one every car in the fleet has
    /// something in hand on.
    /// </para>
    /// <para>
    /// It is never more than half of the shorter of the two lanes, so a short stretch between two
    /// junctions is still a stretch and not two turns joined to each other — and so the two setbacks a
    /// lane carries can never cross over each other.
    /// </para>
    /// <para>
    /// <b>Every turn in the table is laid and every one of them reaches a rung</b>, because the one that
    /// never could is not in the table: a line between two opposing lanes is a semicircle at a lane's own
    /// spacing, tighter than the lock at any setback, and asking for the widest would have set every lane
    /// in the town back as far as the town allows. It is not a movement (TER-5f), so it is not a join.
    /// </para>
    /// </remarks>
    readonly record struct Joins(
        int[] ArcOffsets, ArcSeg[] Arcs, float[] FromM, float[] ToM, float[] LengthM, float[] JoinedAtM,
        float[] LeftAtM);

    /// <summary>How finely the ladder of setbacks is stepped before the widest one is taken.</summary>
    const int SetbackRungs = 8;

    /// <summary>
    /// How near two lane ends have to stand before the movement between them is no movement at all: the
    /// millimetre a place cut into a road (GEN-4h) leaves between its two lanes, which is float noise off
    /// two sub-chains of one curve and not a corner. Drawn rather than recognised, that noise is a biarc of
    /// two arcs a millimetre long whose curvature is enormous, and every lane in every car park would then
    /// set itself back a metre and a quarter to flatten a corner that is not there.
    /// </summary>
    const float SameEndM = 1e-3f;

    static Joins LayJoins(
        SimConfig config, int[] laneArcOffsets, ArcSeg[] laneArcs, float[] laneLengthM, int[] turnOffsets,
        int[] turnToLane, LaneTurn[] turnKind)
    {
        var turnCount = turnToLane.Length;
        var laneCount = laneLengthM.Length;
        var arcOffsets = new int[turnCount + 1];
        var arcs = new List<ArcSeg>();
        var fromM = new float[turnCount];
        var toM = new float[turnCount];
        var lengthM = new float[turnCount];
        var drawn = new ArcSeg[2];

        // One setback for the end of a lane and one for its start, widened a rung at a time until every
        // turn through them holds the corner, so that every movement out of a lane leaves it at the same
        // place and every movement into one arrives at the same place. Widening in rounds rather than
        // turn by turn is what makes the two agree: a setback taken for one turn changes the arc of every
        // other turn sharing that lane end, so what each one needs is only settled once they all are.
        var leavingM = new float[laneCount];
        var arrivingM = new float[laneCount];
        for (var round = 0; round <= SetbackRungs; round++)
        {
            var widened = false;
            for (var lane = 0; lane < laneCount; lane++)
            {
                for (var slot = turnOffsets[lane]; slot < turnOffsets[lane + 1]; slot++)
                {
                    var onto = turnToLane[slot];
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

        for (var lane = 0; lane < laneCount; lane++)
        {
            for (var slot = turnOffsets[lane]; slot < turnOffsets[lane + 1]; slot++)
            {
                var onto = turnToLane[slot];
                var leftM = leavingM[lane];
                var joinedM = arrivingM[onto];

                var from = Spline.SampleAt(ArcsOfBuilt(lane), laneLengthM[lane] - leftM);
                var to = Spline.SampleAt(ArcsOfBuilt(onto), joinedM);
                var laid = TheSameEnd(from.PositionM, to.PositionM)
                    ? 0
                    : Spline.BiarcInto(from.PositionM, from.HeadingRad, to.PositionM, to.HeadingRad, drawn);
                for (var arc = 0; arc < laid; arc++)
                {
                    arcs.Add(drawn[arc]);
                    lengthM[slot] += drawn[arc].LengthM;
                }

                fromM[slot] = leftM;
                toM[slot] = joinedM;
                arcOffsets[slot + 1] = arcs.Count;
            }
        }

        return new Joins(arcOffsets, [.. arcs], fromM, toM, lengthM, arrivingM, leavingM);

        ReadOnlySpan<ArcSeg> ArcsOfBuilt(int lane) =>
            laneArcs.AsSpan(laneArcOffsets[lane], laneArcOffsets[lane + 1] - laneArcOffsets[lane]);
    }

    /// <summary>
    /// Whether the line one turn would be drawn at these two setbacks reaches the junction's corner
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
    /// How far into a lane a turn may ever be set back: the corner the junction was paved for, and never
    /// more than half the shorter of the two lanes, so the two setbacks a lane carries cannot cross.
    /// </summary>
    static float CapM(SimConfig config, float[] laneLengthM, int lane, int onto) =>
        MathF.Min(config.IntersectionCornerRadiusM, MathF.Min(laneLengthM[lane], laneLengthM[onto]) * 0.5f);

    /// <summary>
    /// <b>Which ground each movement through a junction takes off the others</b> (TER-5c), worked out once
    /// from the lines themselves: the stretch of every other join at that node this one is driven over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the pairs at one node are ever compared, and nothing is settled without measuring.</b> A
    /// shared entry lane and a shared exit lane are cheap to recognise and neither is this rule's
    /// business — they are held apart by the road each car was granted.
    /// </para>
    /// <para>
    /// <b>The measurement is between the two lines and not between their crossing points</b>: two
    /// movements that pass within a car's width never touch each other's paint and still cannot both be
    /// made, so the question is how near the lines come rather than whether they intersect.
    /// </para>
    /// </remarks>
    static WayCrossings LayCrossings(
        SimConfig config, int nodeCount, int laneCount, int[] inOffsets, int[] inLanes, int[] turnOffsets,
        int[] turnToLane, Joins joins)
    {
        var turnCount = turnToLane.Length;
        var wayCount = LaneOccupancy.WayOfTurn(laneCount, turnCount);
        var clearanceM = config.JunctionCrossingClearanceM;
        var found = new List<CrossedSection>[wayCount];
        var atTheNode = new List<int>();
        var lineA = new Vector2[LineOverlap.MostSamples];
        var lineB = new Vector2[LineOverlap.MostSamples];

        for (var node = 0; node < nodeCount; node++)
        {
            atTheNode.Clear();
            foreach (var lane in inLanes.AsSpan(inOffsets[node], inOffsets[node + 1] - inOffsets[node]))
            {
                for (var slot = turnOffsets[lane]; slot < turnOffsets[lane + 1]; slot++) atTheNode.Add(slot);
            }

            for (var first = 0; first < atTheNode.Count; first++)
            {
                for (var second = first + 1; second < atTheNode.Count; second++)
                {
                    Measure(atTheNode[first], atTheNode[second]);
                }
            }
        }

        var offsets = new int[wayCount + 1];
        for (var way = 0; way < wayCount; way++) offsets[way + 1] = offsets[way] + (found[way]?.Count ?? 0);

        var sections = new CrossedSection[offsets[wayCount]];
        var most = 0;
        for (var way = 0; way < wayCount; way++)
        {
            found[way]?.CopyTo(sections, offsets[way]);
            most = Math.Max(most, offsets[way + 1] - offsets[way]);
        }

        return new WayCrossings(offsets, sections) { MostCrossedByOne = most };

        // <b>Both intervals go into both entries</b>: a car reads the far one to know what it takes and its
        // own to know when it is past it. The measurement itself is <see cref="LineOverlap"/>'s, which is
        // also what the ways laid off a junction are measured with.
        void Measure(int a, int b)
        {
            var alongA = SampleJoin(a, lineA, out var stepA);
            var alongB = SampleJoin(b, lineB, out var stepB);
            var sampledA = new SampledWay(alongA, 0f, stepA, joins.LengthM[a]);
            var sampledB = new SampledWay(alongB, 0f, stepB, joins.LengthM[b]);
            if (!LineOverlap.Measure(sampledA, sampledB, clearanceM, out var onA, out var onB)) return;

            var wayA = LaneOccupancy.WayOfTurn(laneCount, a);
            var wayB = LaneOccupancy.WayOfTurn(laneCount, b);
            (found[wayA] ??= []).Add(new CrossedSection(wayB, onB.FromM, onB.ToM, onA.FromM, onA.ToM));
            (found[wayB] ??= []).Add(new CrossedSection(wayA, onA.FromM, onA.ToM, onB.FromM, onB.ToM));
        }

        ReadOnlySpan<Vector2> SampleJoin(int slot, Vector2[] into, out float stepM)
        {
            var lengthM = joins.LengthM[slot];
            var arcs = joins.Arcs.AsSpan(joins.ArcOffsets[slot], joins.ArcOffsets[slot + 1] - joins.ArcOffsets[slot]);
            return into.AsSpan(0, LineOverlap.Sample(arcs, 0f, lengthM, lengthM, clearanceM, into, out stepM));
        }
    }

    /// <summary>Whether a lane ends where the next one starts, within <see cref="SameEndM"/>.</summary>
    static bool TheSameEnd(Vector2 fromM, Vector2 toM) =>
        (toM - fromM).LengthSquared() <= SameEndM * SameEndM;

    /// <summary>The tightest circle anywhere in a chain, which for a join is the whole question about it.</summary>
    static float TightestRadiusM(ReadOnlySpan<ArcSeg> arcs)
    {
        var bend = 0f;
        foreach (var arc in arcs) bend = MathF.Max(bend, MathF.Abs(arc.Curvature));

        return bend <= 1e-6f ? float.PositiveInfinity : 1f / bend;
    }

    /// <summary>Two lanes with no turn between them, which is every pair that does not meet at a node.</summary>
    public const int NoTurn = -1;
}
