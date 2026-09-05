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
/// The street network as the router and the follower need it: <b>directed lanes, and the connectors
/// between them</b>. There is nothing else in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A lane is the stretch of road between two of the plan's cuts, not a whole road.</b> Lanes are cut at
/// <em>every</em> junction a road runs through rather than only the two it names, which is what makes an
/// inline junction — a place <em>on</em> a road, carrying a mid-block crossing (TER-5b) — somewhere the
/// graph has lanes ending. The ground inside a disc belongs to no lane: what a car drives across it is a
/// connector, because an intersection is a transition and never a destination (CAR-6.2a).
/// </para>
/// <para>
/// <b>The junctions are the plan's and the graph keeps no table of them</b> (<see cref="JunctionCount"/>).
/// Where lanes meet is <see cref="Places"/>, worked out from the connectors themselves — so nothing that
/// drives, routes or claims can ask what an intersection is, and a junction is the shape a set of crossed
/// lanes happens to make. What is offered back to the plan is the lanes one junction lowered into, for the
/// two slices whose subject is an intersection: the signals and the paint.
/// </para>
/// <para>
/// <b>Structure of arrays, laid once</b>, with every variable-length run — a lane's arcs, a lane's
/// connectors — a flat array and an offsets array beside it. Nothing here is rebuilt during a tick and
/// nothing here is queried by allocating.
/// </para>
/// <para>
/// <b>A crossing cuts nothing.</b> A zebra is a band of the same carriageway and nothing turns at one
/// (TER-6), so the graph never reads the crossing registry: what a car does at a crossing is a rule the
/// driver applies to the lane it is already on.
/// </para>
/// </remarks>
internal sealed class RoadGraph : ILaneEnds
{
    readonly int[] _laneArcOffsets;
    readonly ArcSeg[] _laneArcs;
    readonly int[] _junctionOutOffsets;
    readonly int[] _junctionOutLanes;
    readonly int[] _junctionInOffsets;
    readonly int[] _junctionInLanes;
    readonly int[] _connectorAt;
    readonly int[] _connectorToLane;
    readonly int[] _connectorFromLane;
    readonly LaneTurn[] _connectorKind;
    readonly ConnectorLines _connectorLines;

    /// <summary>The lanes over a grid, which is the whole of what <see cref="NearestLane"/> is.</summary>
    readonly ChainIndex _nearest;

    RoadGraph(
        int junctionCount, int[] laneRoad, float[] laneWidthM, int[] laneFromJunction,
        int[] laneToJunction, bool[] laneForward,
        float[] laneLengthM, int[] laneArcOffsets, ArcSeg[] laneArcs, float[] laneCutBackM, int[] laneReverse,
        bool[] laneEndsAtAPlace,
        int[] junctionOutOffsets, int[] junctionOutLanes, int[] junctionInOffsets, int[] junctionInLanes,
        int[] connectorAt, int[] connectorToLane, LaneTurn[] connectorKind, ConnectorLines connectorLines,
        LanePlaces places, WayCrossings crossings, float nearestCellM)
    {
        LaneCutBackM = laneCutBackM;
        _connectorLines = connectorLines;
        Crossings = crossings;
        JunctionCount = junctionCount;
        Places = places;
        LaneRoad = laneRoad;
        LaneWidthM = laneWidthM;
        LaneFromJunction = laneFromJunction;
        LaneToJunction = laneToJunction;
        LaneForward = laneForward;
        LaneLengthM = laneLengthM;
        LaneReverse = laneReverse;
        LaneEndsAtAPlace = laneEndsAtAPlace;
        _laneArcOffsets = laneArcOffsets;
        _laneArcs = laneArcs;
        _junctionOutOffsets = junctionOutOffsets;
        _junctionOutLanes = junctionOutLanes;
        _junctionInOffsets = junctionInOffsets;
        _junctionInLanes = junctionInLanes;
        _connectorAt = connectorAt;
        _connectorToLane = connectorToLane;
        _connectorKind = connectorKind;

        // Which lane a connector leaves is the run it stands in, so it is folded out once rather than
        // searched for: a caller holding an id asks both its ends the same way.
        _connectorFromLane = new int[connectorToLane.Length];
        for (var lane = 0; lane < laneRoad.Length; lane++)
        {
            for (var id = connectorAt[lane]; id < connectorAt[lane + 1]; id++) _connectorFromLane[id] = lane;
        }

        var builder = new ChainIndex.Builder();
        for (var lane = 0; lane < laneRoad.Length; lane++) builder.Add(lane, ArcsOf(lane), laneLengthM[lane]);

        _nearest = builder.Seal(nearestCellM);

        for (var place = 0; place < Places.Count; place++)
        {
            var connectors = 0;
            foreach (var lane in Places.LanesArriving(place)) connectors += ConnectorsFrom(lane).Count;

            MostConnectorsAtAPlace = Math.Max(MostConnectorsAtAPlace, connectors);
        }
    }

    /// <summary>
    /// <b>Where the carriageway's lanes meet</b>, worked out from the connectors (<see cref="LanePlaces"/>).
    /// It is not the plan's junctions and carries none of them: what the traffic runs on is lanes and the
    /// connectors between them, and a junction is the shape a set of crossed lanes makes.
    /// </summary>
    public LanePlaces Places { get; }

    /// <summary>
    /// The most movements any one place admits — which is how many connectors a body standing in a box can
    /// be lying under at once, and so how much room in the lane index one of them can want.
    /// </summary>
    public int MostConnectorsAtAPlace { get; }

    ReadOnlySpan<int> ILaneEnds.Onward(int lane) => LanesFrom(lane);

    int ILaneEnds.Reverse(int lane) => LaneReverse[lane];

    Vector2 ILaneEnds.StartsAtM(int lane) => StartOf(lane).PositionM;

    Vector2 ILaneEnds.EndsAtM(int lane) => EndOf(lane).PositionM;

    /// <summary>
    /// <b>How many intersections the plan named</b>, and the whole of what this graph knows about them.
    /// Nothing that drives reads it: it is here so that the slices whose subject <em>is</em> an intersection
    /// — the signals a junction's bundle governs (TLT-1), the bars and zebras laid on its arms (TER-6) —
    /// can find the lanes the plan's junction lowered into.
    /// </summary>
    /// <remarks>
    /// <b>A junction is a fact about the plan and never about the traffic.</b> What the town runs on is lanes
    /// and the connectors between them, and where those meet is <see cref="Places"/>, worked out from the
    /// connectors themselves. This is the way back to the record the ground was paved from, offered to the
    /// two slices that authored something per junction and to nothing else.
    /// </remarks>
    public int JunctionCount { get; }

    public int LaneCount => LaneRoad.Length;

    /// <summary>
    /// <b>Every lane connector in the town.</b> A connector is the movement between one lane's end and the
    /// next lane's start: its own line, its own length, its own turn and its own row in the table of what
    /// each way takes off the others. There is no second table of turns beside it — a turn is what a
    /// connector <em>is</em>.
    /// </summary>
    public int ConnectorCount => _connectorToLane.Length;

    public int[] LaneRoad { get; }

    /// <summary>
    /// How wide the lane is: the share of the carriageway its road declared that this direction has — half
    /// of it where the road runs both ways and the whole of it where it runs one (TER-4d). <b>The road's
    /// figure and not the catalogue's</b> (TER-4).
    /// </summary>
    public float[] LaneWidthM { get; }

    /// <summary>
    /// The plan's junction this lane sets off from, or <see cref="CityPlan.NoRecord"/> where it sets off from
    /// a cut a slice above asked for rather than an intersection (GEN-4h).
    /// </summary>
    public int[] LaneFromJunction { get; }

    /// <summary>And the one it arrives at, on the same terms.</summary>
    public int[] LaneToJunction { get; }

    /// <summary>Whether the lane runs with the road's own direction, which is what says which side of the centreline it sits on.</summary>
    public bool[] LaneForward { get; }

    /// <summary>The length of the line as <em>driven</em>: an offset arc is shorter inside a bend than the centreline it was taken from.</summary>
    public float[] LaneLengthM { get; }

    /// <summary>
    /// How much of its stretch this lane gave up to the junctions at its two ends, the two together
    /// (TER-5d). <b>Nothing that drives reads it</b> — a lane's line is what is left, and every movement
    /// hands over at its ends — so it is the town's own record of what the boxes cost it, for the census to
    /// report and for nothing to work out a second time.
    /// </summary>
    public float[] LaneCutBackM { get; }

    /// <summary>
    /// The other lane of the same stretch — the one a car that has turned in a bay comes back down
    /// (GEN-4l), and the one no turn at either end of it ever leads to (TER-5f). <b><see cref="NoLane"/> on
    /// a one-way stretch</b> (TER-4d), which has no other lane: there is nothing to come back down, nothing
    /// to cross to get round what is in the way, and nothing to park against on the far side.
    /// </summary>
    public int[] LaneReverse { get; }

    /// <summary>
    /// <b>Whether the lane runs out at a place a slice above asked for</b> rather than at an intersection —
    /// the end of a parking section (GEN-4h). A run of road is broken there whatever the degree of the
    /// place, because a leg aimed into a car park has to have somewhere to be routed to
    /// (<see cref="Routing.IFineGraph.EndsARun"/>).
    /// </summary>
    public bool[] LaneEndsAtAPlace { get; }

    /// <summary>The line the lane is driven on, in its own direction of travel, already offset to the driver's side.</summary>
    public ReadOnlySpan<ArcSeg> ArcsOf(int lane) =>
        _laneArcs.AsSpan(_laneArcOffsets[lane], _laneArcOffsets[lane + 1] - _laneArcOffsets[lane]);

    /// <summary>The lanes leaving one of the plan's junctions (<see cref="JunctionCount"/>).</summary>
    public ReadOnlySpan<int> LanesOutOfJunction(int junction) =>
        _junctionOutLanes.AsSpan(
            _junctionOutOffsets[junction], _junctionOutOffsets[junction + 1] - _junctionOutOffsets[junction]);

    /// <summary>And the lanes arriving at one.</summary>
    public ReadOnlySpan<int> LanesIntoJunction(int junction) =>
        _junctionInLanes.AsSpan(
            _junctionInOffsets[junction], _junctionInOffsets[junction + 1] - _junctionInOffsets[junction]);

    /// <summary>
    /// <b>The connectors a car on this lane may leave by</b>, as the run of ids they are
    /// (<see cref="ConnectorRun"/>). The run is empty where the lane runs out onto nothing.
    /// </summary>
    public ConnectorRun ConnectorsFrom(int lane) =>
        new(_connectorAt[lane], _connectorAt[lane + 1] - _connectorAt[lane]);

    /// <summary>
    /// The lanes those connectors arrive on, in the same order — <b>where a car on this lane may go</b>, for
    /// a caller that wants the destinations rather than the movements.
    /// </summary>
    public ReadOnlySpan<int> LanesFrom(int lane) =>
        _connectorToLane.AsSpan(_connectorAt[lane], _connectorAt[lane + 1] - _connectorAt[lane]);

    /// <summary>The lane a connector leaves.</summary>
    public int ConnectorFrom(int connector) => _connectorFromLane[connector];

    /// <summary>And the lane it arrives on.</summary>
    public int ConnectorTo(int connector) => _connectorToLane[connector];

    /// <summary>Which turn the connector makes, which is what it costs a router and what right of way it carries.</summary>
    public LaneTurn KindOf(int connector) => _connectorKind[connector];

    /// <summary>
    /// The way a connector is, in the numbering <see cref="Crossings"/> is laid in — the town's own
    /// (<see cref="TownWays"/>), whose first two blocks are this graph's lanes and then its connectors.
    /// </summary>
    public int WayOfConnector(int connector) => TownWays.WayOfRoadConnector(LaneCount, connector);

    /// <summary>And the trip back, for a caller holding a section's <see cref="CrossedSection.OnWay"/>.</summary>
    public int ConnectorOfWay(int way) => way - LaneCount;

    /// <summary>
    /// <b>The right of way a movement through a box has</b> (TER-5e) — a fact about the turn its connector
    /// makes, so it is read off the road like the connector itself and is never worked out from the car
    /// making it.
    /// </summary>
    /// <remarks>
    /// <b>Straighter is stronger, and it is one order rather than a table of pairs.</b> A stream that turns
    /// out of nobody's way is driven over by the turns that leave it (TER-4a); the near-side turn crosses
    /// nothing of its own carriageway and is ordinary traffic; and the turn across the oncoming stream, the
    /// last movement a box admits, gives way to both.
    /// </remarks>
    public RightOfWay RightOfWayOfConnector(int connector) => RightOfWayOf(_connectorKind[connector]);

    /// <summary>The same, for a caller holding the kind rather than the slot.</summary>
    public static RightOfWay RightOfWayOf(LaneTurn turn) => turn switch
    {
        LaneTurn.Straight => RightOfWay.StraightOn,
        LaneTurn.NearSide => RightOfWay.Traffic,
        _ => RightOfWay.TurningAcross,
    };

    /// <summary>
    /// The connector joining a pair of lanes, or <see cref="NoConnector"/> where the town joined them with
    /// none (TER-5f).
    /// </summary>
    public int ConnectorBetween(int fromLane, int toLane)
    {
        foreach (var connector in ConnectorsFrom(fromLane))
        {
            if (_connectorToLane[connector] == toLane) return connector;
        }

        return NoConnector;
    }

    /// <summary>
    /// <b>The ground each way through a junction takes off the others</b> (TER-5c), laid once with the town
    /// like the joins it is measured off, and <b>indexed the way the claims number ways</b>
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
        var connector = ConnectorBetween(fromLane, toLane);
        return connector < 0 ? null : _connectorKind[connector];
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
    public ReadOnlySpan<ArcSeg> ConnectorArcs(int connector) =>
        _connectorLines.Arcs.AsSpan(
            _connectorLines.ArcOffsets[connector],
            _connectorLines.ArcOffsets[connector + 1] - _connectorLines.ArcOffsets[connector]);

    public float ConnectorLengthM(int connector) => _connectorLines.LengthM[connector];

    /// <summary>
    /// The network in the words a walk over ground is written in (<see cref="IWayNetwork"/>) — a view and
    /// not a second structure, so it is taken wherever it is wanted rather than kept.
    /// </summary>
    public RoadWays Ways => new(this);

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
    /// Whether a body stands on one way, which is <b>whether its box reaches inside that way's band at all</b>
    /// — and, where it does, how far aside of the way's own line it stands.
    /// <b>The band the way is laid to and never a radius of the caller's choosing</b>: everything on the
    /// map is nearest some lane, and claiming that lane for a body on the pavement beside it would
    /// hold a street up for the traffic it is parked next to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Across the way and along it, and the second half is not redundant.</b> A projection onto a way is
    /// clamped to that way's own ends (<see cref="Spline.ProjectM"/>), so a body standing past one of them
    /// answers at the endpoint — and measured across the band alone, anything lined up with a way's end is
    /// standing on it however far up the road it really is. What that claimed was a body on joins it
    /// was nowhere near, which is a junction shut by a car in the next street. Inside the way the nearest
    /// point is square to the line and the second test costs nothing; it bites only where the clamp did.
    /// </para>
    /// <para>
    /// <b>Bare overlap and no verdict</b> (TER-4c.2). A body touching a way's band is on that way, and where
    /// across it stands is <see cref="BandReach.AcrossFromM"/> — a figure for whoever is trying to
    /// get past (<see cref="LaneOccupancy.StandsAside"/>) and never a reason to leave the fact out of the
    /// claims. Asked as a clear width instead, a car straddling the line between two lanes left enough of each
    /// of them clear to be written onto neither, and stood in the middle of a road that could not see it.
    /// </para>
    /// </remarks>
    /// <param name="body">
    /// The box the asking body stands in, read against this way's own line
    /// (<see cref="BodyFootprint.CoversOn"/>) — what of it is inside the band, and how far past the way's
    /// own ends it may stand and still be on it. <b>Both come out of the pose</b>: a body lying across a way
    /// reaches its length over the band and its width along it.
    /// </param>
    /// <param name="reach">
    /// What this way has of the body: the run of the line it covers, how far aside of that line it stands,
    /// and which way the line runs there — <b>all of it handed back rather than left to the caller</b>, since
    /// this test has reduced the angle already and a caller that asks <see cref="SplineSample.Direction"/>
    /// for it again reduces the same one a third time.
    /// </param>
    public static bool WithinTheBand(
        ReadOnlySpan<ArcSeg> arcs, float alongM, Vector2 atM, float bandM, in BodyFootprint body,
        float crossesByM, out BandReach reach)
    {
        reach = default;

        var on = Spline.SampleAt(arcs, alongM);
        var alongUnit = on.Direction;
        body.ReachOn(alongUnit, out var alongReachM, out var acrossReachM);

        var offsetM = atM - on.PositionM;
        var pastTheEndM = MathF.Abs(Vector2.Dot(offsetM, alongUnit));
        if (pastTheEndM > alongReachM) return false;
        // <b>Crossed and not touched</b>: the body's near edge has to be this far inside the band's own edge
        // before it is on the way at all, which is what keeps a wing mirror over the paint out of the next
        // lane's claims. Nought asks the bare question, which is what a walk over ground wants.
        if (MathF.Abs(Vector2.Dot(offsetM, Heading.RightOf(alongUnit))) - acrossReachM
            >= (bandM * 0.5f) - crossesByM)
        {
            return false;
        }

        if (!body.CoversOn(alongUnit, offsetM, bandM * 0.5f, out var backM, out var aheadM)) return false;

        // Where the box falls across the line, to the way's right — the span and not the clearance, because
        // whether one body is in another's way is a fact about the pair of them and a clearance can only be
        // asked by whatever travels the line itself.
        var acrossM = Vector2.Dot(offsetM, Heading.RightOf(alongUnit));
        reach = new BandReach(
            alongUnit, pastTheEndM, acrossM - acrossReachM, acrossM + acrossReachM, backM, aheadM);
        return true;
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
                plan, discs, centreline, lengthM, paddingM: 0f, roads.FromJunction[road], roads.ToJunction[road], cuts,
                sections.On(road), config.ParkingSectionShortestStretchM);

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

        // <b>The cuts are how the lanes were laid and are not a table the graph keeps</b>. They number the
        // plan's junctions first and the places a slice above asked for after them (GEN-4h), which is what
        // says whether a lane end is an intersection or a cut — and having said it, they are done with.
        var nodeCount = junctions.Count + sections.NodeCount;
        var wholeOffsets = laneArcOffsets.ToArray();
        var wholeArcs = laneArcs.ToArray();
        var wholeLengths = laneLengthM.ToArray();
        var (outOffsets, outLanes) = Adjacency(nodeCount, laneFromNode);
        var (inOffsets, inLanes) = Adjacency(nodeCount, laneToNode);
        var (connectorAt, connectorToLane, connectorKind) = Connectors(
            config, laneToNode, laneReverse, outOffsets, outLanes, wholeOffsets, wholeArcs);

        // <b>A lane ends where its movements hand over</b> (TER-5d): the cut back is settled over the whole
        // stretch and then taken off the line, so a lane's own last point is where every connector out of it
        // starts and the ground past it is the junction's alone.
        var (arrivingM, leavingM) = Setbacks(
            config, wholeOffsets, wholeArcs, wholeLengths, connectorAt, connectorToLane);
        var lanes = CutBackToTheConnectors(
            wholeOffsets, wholeArcs, wholeLengths, arrivingM, leavingM, scratch.Length);

        var connectorLines = LayConnectorLines(lanes, connectorAt, connectorToLane);
        var places = LanePlaces.Of(new Ends(lanes, connectorAt, connectorToLane, [.. laneReverse]));
        var crossings = LayCrossings(
            config, places, lanes.LengthM.Length, connectorAt, connectorToLane, connectorLines);

        var (junctionOutOffsets, junctionOutLanes) = Adjacency(junctions.Count, laneFromNode);
        var (junctionInOffsets, junctionInLanes) = Adjacency(junctions.Count, laneToNode);

        return new RoadGraph(
            junctions.Count, [.. laneRoad], [.. laneWidthM], AtAJunction(laneFromNode, junctions.Count),
            AtAJunction(laneToNode, junctions.Count), [.. laneForward],
            lanes.LengthM, lanes.ArcOffsets, lanes.Arcs, lanes.CutBackM, [.. laneReverse],
            [.. laneEndsAtAPlace],
            junctionOutOffsets, junctionOutLanes, junctionInOffsets, junctionInLanes,
            connectorAt, connectorToLane, connectorKind,
            connectorLines, places, crossings, config.NearestChainCellM);

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
    /// The lanes at each of the first <paramref name="nodeCount"/> cuts, by counting them into place. A lane
    /// at a cut past that count is left out, which is what makes this the plan's junctions alone when it is
    /// asked for those.
    /// </summary>
    static (int[] Offsets, int[] Lanes) Adjacency(int nodeCount, List<int> laneNode)
    {
        var offsets = new int[nodeCount + 1];
        var counted = 0;
        foreach (var node in laneNode)
        {
            if (node >= nodeCount) continue;

            offsets[node + 1]++;
            counted++;
        }

        for (var node = 1; node < offsets.Length; node++) offsets[node] += offsets[node - 1];

        var cursor = (int[])offsets.Clone();
        var lanes = new int[counted];
        for (var lane = 0; lane < laneNode.Count; lane++)
        {
            if (laneNode[lane] < nodeCount) lanes[cursor[laneNode[lane]]++] = lane;
        }

        return (offsets, lanes);
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
    /// The cut-back lanes read as <see cref="ILaneEnds"/>, so that where they meet can be worked out from
    /// the connectors before the graph they belong to exists.
    /// </summary>
    readonly struct Ends(Lanes lanes, int[] connectorAt, int[] connectorToLane, int[] laneReverse) : ILaneEnds
    {
        public int LaneCount => lanes.LengthM.Length;

        public int Reverse(int lane) => laneReverse[lane];

        public ReadOnlySpan<int> Onward(int lane) =>
            connectorToLane.AsSpan(connectorAt[lane], connectorAt[lane + 1] - connectorAt[lane]);

        public Vector2 StartsAtM(int lane) => Spline.SampleAt(ArcsOf(lane), 0f).PositionM;

        public Vector2 EndsAtM(int lane) => Spline.SampleAt(ArcsOf(lane), lanes.LengthM[lane]).PositionM;

        ReadOnlySpan<ArcSeg> ArcsOf(int lane) =>
            lanes.Arcs.AsSpan(lanes.ArcOffsets[lane], lanes.ArcOffsets[lane + 1] - lanes.ArcOffsets[lane]);
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
    /// Every connector's line in the town, drawn once: the arcs from the end of the lane it leaves to the
    /// start of the lane it arrives on.
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
    readonly record struct ConnectorLines(int[] ArcOffsets, ArcSeg[] Arcs, float[] LengthM);

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
    static ConnectorLines LayConnectorLines(Lanes lanes, int[] connectorAt, int[] connectorToLane)
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

        return new ConnectorLines(arcOffsets, [.. arcs], lengthM);

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


    /// <summary>
    /// <b>Which ground each movement through a junction takes off the others</b> (TER-5c), worked out once
    /// from the lines themselves: the stretch of every other connector at that place this one is driven over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the pairs at one place are ever compared, and nothing is settled without measuring.</b> A
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
        SimConfig config, LanePlaces places, int laneCount, int[] connectorAt, int[] connectorToLane,
        ConnectorLines lines)
    {
        var connectorCount = connectorToLane.Length;
        var wayCount = TownWays.WayOfRoadConnector(laneCount, connectorCount);
        var clearanceM = config.JunctionCrossingClearanceM;
        var found = new List<CrossedSection>[wayCount];
        var atThePlace = new List<int>();
        var lineA = new Vector2[LineOverlap.MostSamples];
        var lineB = new Vector2[LineOverlap.MostSamples];

        for (var place = 0; place < places.Count; place++)
        {
            atThePlace.Clear();
            foreach (var lane in places.LanesArriving(place))
            {
                for (var id = connectorAt[lane]; id < connectorAt[lane + 1]; id++) atThePlace.Add(id);
            }

            for (var first = 0; first < atThePlace.Count; first++)
            {
                for (var second = first + 1; second < atThePlace.Count; second++)
                {
                    Measure(atThePlace[first], atThePlace[second]);
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
            var alongA = SampleConnector(a, lineA, out var stepA);
            var alongB = SampleConnector(b, lineB, out var stepB);
            var sampledA = new SampledWay(alongA, 0f, stepA, lines.LengthM[a]);
            var sampledB = new SampledWay(alongB, 0f, stepB, lines.LengthM[b]);
            if (!LineOverlap.Measure(sampledA, sampledB, clearanceM, out var onA, out var onB)) return;

            var wayA = TownWays.WayOfRoadConnector(laneCount, a);
            var wayB = TownWays.WayOfRoadConnector(laneCount, b);
            (found[wayA] ??= []).Add(new CrossedSection(wayB, onB.FromM, onB.ToM, onA.FromM, onA.ToM));
            (found[wayB] ??= []).Add(new CrossedSection(wayA, onA.FromM, onA.ToM, onB.FromM, onB.ToM));
        }

        ReadOnlySpan<Vector2> SampleConnector(int connector, Vector2[] into, out float stepM)
        {
            var lengthM = lines.LengthM[connector];
            var arcs = lines.Arcs.AsSpan(
                lines.ArcOffsets[connector], lines.ArcOffsets[connector + 1] - lines.ArcOffsets[connector]);
            return into.AsSpan(0, LineOverlap.Sample(arcs, 0f, lengthM, lengthM, clearanceM, into, out stepM));
        }
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

    /// <summary>Two lanes no connector joins, which is every pair that does not meet at a node.</summary>
    public const int NoConnector = -1;

    /// <summary>Where a lane is asked for and the town has none — the reverse of a one-way stretch (TER-4d).</summary>
    public const int NoLane = -1;
}
