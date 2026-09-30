namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>What kind of ground one of the town's ways is.</b> Every way is a lane somebody travels down; the
/// kind says which of the town's surfaces it is laid on and, through that, what travels there.
/// </summary>
/// <remarks>
/// <b>It is a property of the ground and never of the body standing on it</b> (TER-4c.2). A person in a
/// lane is a stretch of that lane and a car that has mounted a kerb is a stretch of the footway under it,
/// so nothing reads this to decide whether a reservation may be laid — only to say how wide the ground is.
/// </remarks>
internal enum WayKind : byte
{
    /// <summary>A lane of the carriageway — a bay's own among them (`GEN-53`).</summary>
    Lane,

    /// <summary>A lane connector: the movement across a junction, threaded between two lanes.</summary>
    Connector,

    /// <summary>One side of one stretch of pavement.</summary>
    Footway,

    /// <summary>The mitre between two stretches of pavement, which is a footway's join.</summary>
    Mitre,
}

/// <summary>
/// <b>Every way in the town, numbered once</b> — the carriageway's lanes, the connectors across its
/// junctions, the two sides of every pavement and the mitres between them. <b>One table and one
/// numbering</b>, so that a claim on any of them is comparable with a claim on any other and the ground a
/// body stands on can be written down without first asking what kind of body it is.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no second network.</b> A footway is not carriageway, but that is a difference in the ground
/// and not in the arrangement: they are all runs of arclength somebody travels down, told apart by
/// <see cref="KindOf"/>. Held as two tables instead, one piece of the world had two records that nothing
/// could compare — and a walker was granted ground a car was standing on because the two claims were counted
/// in different metres.
/// </para>
/// <para>
/// <b>The blocks are in this order and the order is load-bearing</b>: the carriageway's lanes are numbered
/// first so that a lane and its way are the same integer, and the connectors follow so that a connector's id
/// is an offset from the lane count. The pavement is last because nothing on the road needs to know it is
/// there. <b>A bay is a lane</b> (GEN-53), numbered with the rest.
/// </para>
/// <para>
/// <b>What is handed over is lengths and never networks</b> (SIM-7). The road is the lowest thing that can
/// hold this table, and the pavement is laid above it, so its ways arrive as runs of metres the caller
/// measured. Which of the town's features laid a way is that feature's own business; what is owned here is
/// the numbering.
/// </para>
/// </remarks>
internal sealed class TownWays
{
    readonly int _firstConnector;
    readonly int _firstFootway;
    readonly int _firstMitre;

    readonly float[] _lengthM;

    TownWays(
        int laneCount, int connectorCount, ReadOnlySpan<float> laneLengthM, ReadOnlySpan<float> connectorLengthM,
        ReadOnlySpan<float> footwayLengthM, ReadOnlySpan<float> mitreLengthM)
    {
        _firstConnector = laneCount;
        _firstFootway = laneCount + connectorCount;
        _firstMitre = _firstFootway + footwayLengthM.Length;

        _lengthM = new float[_firstMitre + mitreLengthM.Length];
        laneLengthM.CopyTo(_lengthM);
        connectorLengthM.CopyTo(_lengthM.AsSpan(_firstConnector));
        footwayLengthM.CopyTo(_lengthM.AsSpan(_firstFootway));
        mitreLengthM.CopyTo(_lengthM.AsSpan(_firstMitre));
    }

    /// <summary>
    /// <b>The whole town's ways</b>: the road's and the pavement's, in the one numbering everything
    /// downstream reads.
    /// </summary>
    public static TownWays Of(RoadGraph roads, ReadOnlySpan<float> footwayLengthM, ReadOnlySpan<float> mitreLengthM)
    {
        // Laid once with the town, so the array is the standing cost of having a table at all.
        var connectorLengthM = new float[roads.ConnectorCount];
        for (var id = 0; id < roads.ConnectorCount; id++) connectorLengthM[id] = roads.ConnectorLengthM(id);

        return new TownWays(
            roads.LaneCount, roads.ConnectorCount, roads.LaneLengthM, connectorLengthM, footwayLengthM,
            mitreLengthM);
    }

    /// <summary>
    /// <b>The road's ways alone</b> — for a fixture that stands a carriageway and nothing else. The town
    /// itself always has the pavement's two blocks, and they are empty here rather than absent, so that
    /// everything reading a way number reads the same numbering either way.
    /// </summary>
    public static TownWays OfTheRoad(RoadGraph roads) => Of(roads, [], []);

    /// <summary>
    /// <b>Where the joins begin, for a caller that knows how many lanes there are and holds no table</b> —
    /// the road graph laying its own table of crossings. A second statement of it is a second statement that
    /// can disagree.
    /// </summary>
    public static int WayOfRoadConnector(int laneCount, int connector) => laneCount + connector;

    /// <summary>Where the pavement's two blocks begin, for the network that has to name its own ways.</summary>
    public int FirstFootwayWay => _firstFootway;

    public int FirstMitreWay => _firstMitre;

    public int Count => _lengthM.Length;

    /// <summary>How many lanes the carriageway has, which is what makes a lane and its way the same integer.</summary>
    public int LaneCount => _firstConnector;

    public float LengthM(int way) => _lengthM[way];

    /// <summary>What kind of ground this way is laid on.</summary>
    public WayKind KindOf(int way) =>
        way < _firstConnector ? WayKind.Lane
        : way < _firstFootway ? WayKind.Connector
        : way < _firstMitre ? WayKind.Footway
        : WayKind.Mitre;

    /// <summary>Whether this way is one the town's traffic drives, as against one it walks.</summary>
    public bool IsDriven(int way) => way < _firstFootway;

    /// <summary>A carriageway lane's own way number. The lanes are numbered first, so the two are one integer.</summary>
    public int OfRoadLane(int lane) => lane;

    /// <summary>The way one of a junction's connectors is, named by the id the road graph gave it.</summary>
    public int OfRoadConnector(int connector) => _firstConnector + connector;

    /// <summary>One side of one stretch of pavement, named by its own directed edge of the foot graph.</summary>
    public int OfFootway(int edge) => _firstFootway + edge;

    /// <summary>And the mitre between two of them, named by the walking network's own turn slot.</summary>
    public int OfMitre(int slot) => _firstMitre + slot;

    /// <summary>The trip back, for a caller holding a way it already knows the kind of.</summary>
    public int RoadLaneOf(int way) => way;

    public int RoadConnectorOf(int way) => way - _firstConnector;

    public int FootwayOf(int way) => way - _firstFootway;

    public int MitreOf(int way) => way - _firstMitre;
}
