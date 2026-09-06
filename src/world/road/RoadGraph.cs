using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// The street network as the router and the follower need it: <b>directed lanes, and the connectors
/// between them</b>. There is nothing else in it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The lines themselves are the plan's</b> (<see cref="LaneLines"/>), laid when the town was generated
/// and read here rather than drawn again: what this adds is the rules over them — where they meet, what
/// each movement takes off the others, and the index a body is stood up against. The ground under those
/// same lines is what a car may drive on, which is why the surface and the network cannot disagree.
/// </para>
/// <para>
/// <b>A lane is the stretch of road between two of the plan's cuts, not a whole road.</b> Lanes are cut at
/// <em>every</em> junction a road runs through rather than only the two it names, which is what makes an
/// inline junction — a place <em>on</em> a road, carrying a mid-block crossing (TER-5b) — somewhere the
/// graph has lanes ending. The ground between two lane ends belongs to no lane: what a car drives across it
/// is a connector, because an intersection is a transition and never a destination (CAR-6.2a).
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
    readonly LaneLines _lines;
    readonly int[] _junctionOutOffsets;
    readonly int[] _junctionOutLanes;
    readonly int[] _junctionInOffsets;
    readonly int[] _junctionInLanes;
    readonly int[] _connectorFromLane;

    /// <summary>The lanes over a grid, which is the whole of what <see cref="NearestLane"/> is.</summary>
    readonly ChainIndex _nearest;

    RoadGraph(
        LaneLines lines, int[] junctionOutOffsets, int[] junctionOutLanes, int[] junctionInOffsets,
        int[] junctionInLanes, LanePlaces places, WayCrossings crossings, float nearestCellM)
    {
        _lines = lines;
        Crossings = crossings;
        Places = places;
        _junctionOutOffsets = junctionOutOffsets;
        _junctionOutLanes = junctionOutLanes;
        _junctionInOffsets = junctionInOffsets;
        _junctionInLanes = junctionInLanes;

        // Which lane a connector leaves is the run it stands in, so it is folded out once rather than
        // searched for: a caller holding an id asks both its ends the same way.
        _connectorFromLane = new int[lines.ConnectorCount];
        for (var lane = 0; lane < lines.LaneCount; lane++)
        {
            for (var id = lines.ConnectorAt[lane]; id < lines.ConnectorAt[lane + 1]; id++)
            {
                _connectorFromLane[id] = lane;
            }
        }

        var builder = new ChainIndex.Builder();
        for (var lane = 0; lane < lines.LaneCount; lane++)
        {
            builder.Add(lane, ArcsOf(lane), lines.LaneLengthM[lane]);
        }

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
    public int JunctionCount => _lines.JunctionCount;

    public int LaneCount => _lines.LaneCount;

    /// <summary>
    /// <b>Every lane connector in the town.</b> A connector is the movement between one lane's end and the
    /// next lane's start: its own line, its own length, its own turn and its own row in the table of what
    /// each way takes off the others. There is no second table of turns beside it — a turn is what a
    /// connector <em>is</em>.
    /// </summary>
    public int ConnectorCount => _lines.ConnectorCount;

    public int[] LaneRoad => _lines.LaneRoad;

    /// <summary>
    /// How wide the lane is: the share of the carriageway its road declared that this direction has — half
    /// of it where the road runs both ways and the whole of it where it runs one (TER-4d). <b>The road's
    /// figure and not the catalogue's</b> (TER-4).
    /// </summary>
    public float[] LaneWidthM => _lines.LaneWidthM;

    /// <summary>
    /// The plan's junction this lane sets off from, or <see cref="CityPlan.NoRecord"/> where it sets off from
    /// a cut a slice above asked for rather than an intersection (GEN-4h).
    /// </summary>
    public int[] LaneFromJunction => _lines.LaneFromJunction;

    /// <summary>And the one it arrives at, on the same terms.</summary>
    public int[] LaneToJunction => _lines.LaneToJunction;

    /// <summary>Whether the lane runs with the road's own direction, which is what says which side of the centreline it sits on.</summary>
    public bool[] LaneForward => _lines.LaneForward;

    /// <summary>The length of the line as <em>driven</em>: an offset arc is shorter inside a bend than the centreline it was taken from.</summary>
    public float[] LaneLengthM => _lines.LaneLengthM;

    /// <summary>
    /// How much of its stretch this lane gave up to the junctions at its two ends, the two together
    /// (TER-5d). <b>Nothing that drives reads it</b> — a lane's line is what is left, and every movement
    /// hands over at its ends — so it is the town's own record of what the boxes cost it, for the census to
    /// report and for nothing to work out a second time.
    /// </summary>
    public float[] LaneCutBackM => _lines.LaneCutBackM;

    /// <summary>
    /// The other lane of the same stretch — the one a car that has turned in a bay comes back down
    /// (GEN-4l), and the one no turn at either end of it ever leads to (TER-5f). <b><see cref="NoLane"/> on
    /// a one-way stretch</b> (TER-4d), which has no other lane: there is nothing to come back down, nothing
    /// to cross to get round what is in the way, and nothing to park against on the far side.
    /// </summary>
    public int[] LaneReverse => _lines.LaneReverse;

    /// <summary>
    /// <b>Whether the lane runs out at a place a slice above asked for</b> rather than at an intersection —
    /// the end of a parking section (GEN-4h). A run of road is broken there whatever the degree of the
    /// place, because a leg aimed into a car park has to have somewhere to be routed to
    /// (<see cref="Routing.IFineGraph.EndsARun"/>).
    /// </summary>
    public bool[] LaneEndsAtAPlace => _lines.LaneEndsAtAPlace;

    /// <summary>The line the lane is driven on, in its own direction of travel, already offset to the driver's side.</summary>
    public ReadOnlySpan<ArcSeg> ArcsOf(int lane) => _lines.ArcsOf(lane);

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
        new(_lines.ConnectorAt[lane], _lines.ConnectorAt[lane + 1] - _lines.ConnectorAt[lane]);

    /// <summary>
    /// The lanes those connectors arrive on, in the same order — <b>where a car on this lane may go</b>, for
    /// a caller that wants the destinations rather than the movements.
    /// </summary>
    public ReadOnlySpan<int> LanesFrom(int lane) =>
        _lines.ConnectorToLane.AsSpan(
            _lines.ConnectorAt[lane], _lines.ConnectorAt[lane + 1] - _lines.ConnectorAt[lane]);

    /// <summary>The lane a connector leaves.</summary>
    public int ConnectorFrom(int connector) => _connectorFromLane[connector];

    /// <summary>And the lane it arrives on.</summary>
    public int ConnectorTo(int connector) => _lines.ConnectorToLane[connector];

    /// <summary>Which turn the connector makes, which is what it costs a router and what right of way it carries.</summary>
    public LaneTurn KindOf(int connector) => _lines.ConnectorKind[connector];

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
    public RightOfWay RightOfWayOfConnector(int connector) => RightOfWayOf(_lines.ConnectorKind[connector]);

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
            if (_lines.ConnectorToLane[connector] == toLane) return connector;
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
        return connector < 0 ? null : _lines.ConnectorKind[connector];
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
    public ReadOnlySpan<ArcSeg> ConnectorArcs(int connector) => _lines.ArcsOfConnector(connector);

    public float ConnectorLengthM(int connector) => _lines.ConnectorLengthM[connector];

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

    /// <summary>
    /// The graph over the lines the plan laid with the town (<see cref="LaneLines"/>), which is the only
    /// place a lane or a connector is ever drawn.
    /// </summary>
    public static RoadGraph Build(CityPlan plan, SimConfig config) =>
        Build(LaneLines.Of(plan.Ground, config), config);

    /// <summary>
    /// <b>The rules laid over lines that already exist</b>: where the lanes meet, what each movement through
    /// a box takes off the others, and the index a body is stood up against. Nothing here draws a line, so a
    /// graph and the ground under it cannot disagree about where the traffic runs.
    /// </summary>
    public static RoadGraph Build(LaneLines lines, SimConfig config)
    {
        var (junctionOutOffsets, junctionOutLanes) = LaneLines.Adjacency(lines.JunctionCount, lines.LaneFromNode);
        var (junctionInOffsets, junctionInLanes) = LaneLines.Adjacency(lines.JunctionCount, lines.LaneToNode);
        var places = LanePlaces.Of(new Ends(lines));

        return new RoadGraph(
            lines, junctionOutOffsets, junctionOutLanes, junctionInOffsets, junctionInLanes, places,
            LayCrossings(config, places, lines), config.NearestChainCellM);
    }

    /// <summary>
    /// The lanes read as <see cref="ILaneEnds"/>, so that where they meet can be worked out from the
    /// connectors before the graph they belong to exists.
    /// </summary>
    readonly struct Ends(LaneLines lines) : ILaneEnds
    {
        public int LaneCount => lines.LaneCount;

        public int Reverse(int lane) => lines.LaneReverse[lane];

        public ReadOnlySpan<int> Onward(int lane) =>
            lines.ConnectorToLane.AsSpan(
                lines.ConnectorAt[lane], lines.ConnectorAt[lane + 1] - lines.ConnectorAt[lane]);

        public Vector2 StartsAtM(int lane) => Spline.SampleAt(lines.ArcsOf(lane), 0f).PositionM;

        public Vector2 EndsAtM(int lane) =>
            Spline.SampleAt(lines.ArcsOf(lane), lines.LaneLengthM[lane]).PositionM;
    }

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
    static WayCrossings LayCrossings(SimConfig config, LanePlaces places, LaneLines lines)
    {
        var laneCount = lines.LaneCount;
        var wayCount = TownWays.WayOfRoadConnector(laneCount, lines.ConnectorCount);
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
                for (var id = lines.ConnectorAt[lane]; id < lines.ConnectorAt[lane + 1]; id++) atThePlace.Add(id);
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
            var sampledA = new SampledWay(alongA, 0f, stepA, lines.ConnectorLengthM[a]);
            var sampledB = new SampledWay(alongB, 0f, stepB, lines.ConnectorLengthM[b]);
            if (!LineOverlap.Measure(sampledA, sampledB, clearanceM, out var onA, out var onB)) return;

            var wayA = TownWays.WayOfRoadConnector(laneCount, a);
            var wayB = TownWays.WayOfRoadConnector(laneCount, b);
            (found[wayA] ??= []).Add(new CrossedSection(wayB, onB.FromM, onB.ToM, onA.FromM, onA.ToM));
            (found[wayB] ??= []).Add(new CrossedSection(wayA, onA.FromM, onA.ToM, onB.FromM, onB.ToM));
        }

        ReadOnlySpan<Vector2> SampleConnector(int connector, Vector2[] into, out float stepM)
        {
            var lengthM = lines.ConnectorLengthM[connector];
            var arcs = lines.ArcsOfConnector(connector);
            return into.AsSpan(0, LineOverlap.Sample(arcs, 0f, lengthM, lengthM, clearanceM, into, out stepM));
        }
    }

    /// <summary>Two lanes no connector joins, which is every pair that does not meet at a node.</summary>
    public const int NoConnector = -1;

    /// <summary>Where a lane is asked for and the town has none — the reverse of a one-way stretch (TER-4d).</summary>
    public const int NoLane = LaneLines.NoLane;
}
