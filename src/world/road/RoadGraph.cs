using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

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
/// <b>A lane runs between the two connection points of the run it is</b> (TER-5i), which is neither a
/// whole road nor one stretch of one. Roads are cut at <em>every</em> junction they run through rather than
/// only the two they name — which is what makes an inline junction, a place <em>on</em> a road carrying a
/// mid-block crossing (TER-5b), somewhere the graph can have lanes ending — and then every join that forks
/// nothing is folded back into the lane it joins, so a lane may run the length of several of the plan's
/// roads. The ground between two lane ends that are left belongs to no lane: what a car drives across it is
/// a connector, because an intersection is a transition and never a destination (CAR-6.2a).
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

    /// <summary>The lanes over a grid, which is the whole of what <see cref="NearestLane"/> is.</summary>
    readonly ChainIndex _nearest;

    RoadGraph(
        LaneLines lines, int[] junctionOutOffsets, int[] junctionOutLanes, int[] junctionInOffsets,
        int[] junctionInLanes, LanePlaces places, GridLevel nearestLevel)
    {
        _lines = lines;
        Places = places;
        _junctionOutOffsets = junctionOutOffsets;
        _junctionOutLanes = junctionOutLanes;
        _junctionInOffsets = junctionInOffsets;
        _junctionInLanes = junctionInLanes;

        var builder = new ChainIndex.Builder();
        for (var lane = 0; lane < lines.LaneCount; lane++)
        {
            builder.Add(lane, ArcsOf(lane), lines.LaneLengthM[lane]);
        }

        _nearest = builder.Seal(nearestLevel);

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

    /// <summary>
    /// <b>The road a lane is one way of</b> (TER-5i): it sets off on that road's own arm and arrives on the
    /// other, and never spans two — a run of roads through nodes nothing meets at is already one road before
    /// a lane is laid on it (GEN-51).
    /// </summary>
    public int[] LaneRoad => _lines.LaneRoad;

    /// <summary>
    /// How wide the lane is: the share of the carriageway its road declared that this direction has — half
    /// of it where the road runs both ways and the whole of it where it runs one (TER-4d). <b>The road's
    /// figure and not the catalogue's</b> (TER-4).
    /// </summary>
    public float[] LaneWidthM => _lines.LaneWidthM;

    /// <summary>The plan's junction this lane sets off from, which is the only thing a lane can start at.</summary>
    public int[] LaneFromJunction => _lines.LaneFromJunction;

    /// <summary>And the one it arrives at.</summary>
    public int[] LaneToJunction => _lines.LaneToJunction;

    /// <summary>
    /// Whether the lane runs with the direction of the road it is one way of (<see cref="LaneRoad"/>),
    /// which is what says which side of that road's centreline it sits on.
    /// </summary>
    public bool[] LaneForward => _lines.LaneForward;

    /// <summary>The length of the line as <em>driven</em>: an offset arc is shorter inside a bend than the centreline it was taken from.</summary>
    public float[] LaneLengthM => _lines.LaneLengthM;

    /// <summary>
    /// The other lane of the same stretch — the one a car that has turned in a bay comes back down
    /// (GEN-4l), and the one no turn at either end of it ever leads to (TER-5f). <b><see cref="NoLane"/> on
    /// a one-way stretch</b> (TER-4d), which has no other lane: there is nothing to come back down, nothing
    /// to cross to get round what is in the way, and nothing to park against on the far side.
    /// </summary>
    public int[] LaneReverse => _lines.LaneReverse;

    /// <summary>
    /// <b>Whether this lane and its reverse are one line rather than two halves of a carriageway</b>
    /// (GEN-4f): a bay's way is a car's width of ground driven in over and backed out over, so the ground
    /// under it carries both directions at once. <b>The town's own answer</b>
    /// (<see cref="LaneLines.LaneOverOneLine"/>) and not a distance measured between two lines.
    /// </summary>
    public bool[] LaneOverOneLine => _lines.LaneOverOneLine;

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
    public int ConnectorFrom(int connector) => _lines.ConnectorFromLane[connector];

    /// <summary>And the lane it arrives on.</summary>
    public int ConnectorTo(int connector) => _lines.ConnectorToLane[connector];

    /// <summary>Which turn the connector makes, which is what it costs a router and what right of way it carries.</summary>
    public LaneTurn KindOf(int connector) => _lines.ConnectorKind[connector];

    /// <summary>
    /// The way a connector is, in the town's own numbering (<see cref="TownWays"/>), whose first two blocks
    /// are this graph's lanes and then its connectors.
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
    /// <para>
    /// <b>Every movement hands over at a lane's start</b>, so there is no movement here that joins a stream
    /// already being driven where it lands: a node that would offer an approach one movement is a node the
    /// generator does not lay (GEN-18).
    /// </para>
    /// </remarks>
    public ClaimPriority FirmOnConnector(int connector) => FirmOn(_lines.ConnectorKind[connector]);

    /// <summary>
    /// The same, for a caller holding the kind rather than the slot — <b>the rung a movement is granted its
    /// ground at</b> (TER-5e), which is the whole of what "straighter is stronger" comes to.
    /// </summary>
    public static ClaimPriority FirmOn(LaneTurn turn) => turn switch
    {
        LaneTurn.Straight => ClaimPriority.FirmStraight,
        LaneTurn.NearSide => ClaimPriority.Firm,
        _ => ClaimPriority.FirmAcross,
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

    /// <summary>How wide the ground a movement is driven over is: the narrower of the two lanes it joins (TER-5d.1).</summary>
    public float ConnectorWidthM(int connector) => _lines.ConnectorWidthM(connector);

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
    /// The graph over the lines the plan laid with the town (<see cref="LaneLines"/>), which is the only
    /// place a lane or a connector is ever drawn.
    /// </summary>
    /// <remarks>
    /// <b>The plan's own lines and not a second laying of them</b> (<see cref="CityPlan.Paving"/>). They are a
    /// pure function of the plan, so a second laying could not disagree — but it is every lane, every movement
    /// and every run in the town drawn twice, and the ground and the graph then hold two copies of the same
    /// arcs for as long as the town is open.
    /// </remarks>
    public static RoadGraph Build(CityPlan plan, SimConfig config) =>
        Build(plan.Paving(config).Lanes, config);

    /// <summary>
    /// <b>The rules laid over lines that already exist</b>: where the lanes meet and the index a body is
    /// stood up against. Nothing here draws a line, so a graph and the ground under it cannot disagree about
    /// where the traffic runs.
    /// </summary>
    public static RoadGraph Build(LaneLines lines, SimConfig config)
    {
        var (junctionOutOffsets, junctionOutLanes) = LaneLines.Adjacency(lines.JunctionCount, lines.LaneFromJunction);
        var (junctionInOffsets, junctionInLanes) = LaneLines.Adjacency(lines.JunctionCount, lines.LaneToJunction);
        var places = LanePlaces.Of(new Ends(lines));

        return new RoadGraph(
            lines, junctionOutOffsets, junctionOutLanes, junctionInOffsets, junctionInLanes, places,
            config.Grid.Main);
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

    /// <summary>Two lanes no connector joins, which is every pair that does not meet at a node.</summary>
    public const int NoConnector = -1;

    /// <summary>Where a lane is asked for and the town has none — the reverse of a one-way stretch (TER-4d).</summary>
    public const int NoLane = LaneLines.NoLane;
}
