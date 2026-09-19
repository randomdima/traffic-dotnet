using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Foot;

/// <summary>Which of a pedestrian node's three ways a connection point belongs to.</summary>
internal enum FootConnectorKind : byte
{
    /// <summary>The way over the carriageway, which leaves the node square to the boundary.</summary>
    Crossing,

    /// <summary>The way down the road, which leaves the node along the boundary and away from the junction.</summary>
    Road,

    /// <summary>And the way round the junction, which leaves along the boundary the other way.</summary>
    Junction,
}

/// <summary>
/// <b>The points a pedestrian node hands its three ways over at</b> (WLK-9): a pair for the crossing, a pair
/// for the road and a pair for the junction, every one of them struck off the driven ground's own boundary
/// (<see cref="KerbLines"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>A pair and not a point, because every way is walked a lane each way.</b> The two points of a pair are
/// the two lanes' own lines — the middle of each half of the way's band (WLK-8), so a walking lane apart —
/// laid <em>across</em> the way they belong to: a crossing's pair lies along the boundary and sets off
/// square over the carriageway, and a road's or a junction's lies square to the boundary and sets off along
/// it.
/// </para>
/// <para>
/// <b>And two points standing on each other are one point</b> (WLK-12, <see cref="Shares"/>): where the
/// place a lane sets off from and the place it arrives at come within
/// <see cref="RoadFigures.FootConnectorMergeM"/> of one another there is nothing left of that lane, so both
/// nodes hand it over at the one place between them and nothing is laid down it.
/// </para>
/// <para>
/// <b>Everything is read off the boundary and nothing off the road's own line.</b> At a mouth the ground a
/// junction's movements are driven over reaches past the arm's edge, so a point placed square to a
/// centreline that runs straight on while the kerb turns into the corner lands inside the tarmac. The
/// boundary is where the tarmac really stops, and a step measured along it follows the corner round.
/// </para>
/// <para>
/// <b>What runs between two of them is <see cref="FootWays"/>'</b> (WLK-11): the points and which node the
/// way each of them belongs to reaches (<see cref="Reaches"/>) are the whole of what it is given, the line
/// itself being a stretch of the walk's own course (<see cref="WalkLines"/>) rather than anything struck
/// here.
/// </para>
/// </remarks>
internal sealed class FootConnectors
{
    /// <summary>How many lanes a pedestrian way is walked, which is what makes a connection a pair of points.</summary>
    public const int LanesPerWay = 2;

    /// <summary>The three ways a node hands over to (<see cref="FootConnectorKind"/>), before any is dropped.</summary>
    public const int Kinds = 3;

    /// <summary>The three ways a node hands over to (<see cref="FootConnectorKind"/>), a pair of points each.</summary>
    public const int PointsPerNode = Kinds * LanesPerWay;

    /// <summary>What a node standing nowhere answers for its place.</summary>
    public const int NoPlace = -1;

    /// <summary>A town with no pavement, which stands no pedestrian node and hands nothing over.</summary>
    public static readonly FootConnectors None = new([], [], [], [], [], [], [], [], []);

    readonly bool[] _stands;
    readonly Vector2[] _atM;
    readonly Vector2[] _kerbAtM;
    readonly int[] _placeOf;
    readonly Vector2[] _placeAtM;
    readonly Vector2[] _pointM;
    readonly bool[] _handsOver;
    readonly bool[] _shares;
    readonly int[] _reaches;

    FootConnectors(
        bool[] stands, Vector2[] atM, Vector2[] kerbAtM, int[] placeOf, Vector2[] placeAtM, Vector2[] pointM,
        bool[] handsOver, bool[] shares, int[] reaches)
    {
        _stands = stands;
        _atM = atM;
        _kerbAtM = kerbAtM;
        _placeOf = placeOf;
        _placeAtM = placeAtM;
        _pointM = pointM;
        _handsOver = handsOver;
        _shares = shares;
        _reaches = reaches;
    }

    public int NodeCount => _stands.Length;

    /// <summary>Whether this node was placed at all (WLK-2, <see cref="FootJunctions.StandsAt"/>).</summary>
    public bool StandsAt(int node) => _stands[node];

    /// <summary>
    /// <b>How many places the merge left</b> (WLK-3), which is what a walk has corners to arrive at: one per
    /// node that merged with nothing, and one per set of nodes that stood within
    /// <see cref="RoadFigures.FootNodeMergeM"/> of one another.
    /// </summary>
    public int PlaceCount => _placeAtM.Length;

    /// <summary>The place a node was merged into, or <see cref="NoPlace"/> where it stands nowhere.</summary>
    public int PlaceOf(int node) => _placeOf[node];

    /// <summary>
    /// <b>Where a place is: the middle of the nodes merged into it</b>, and a node's own place where nothing
    /// merged with it. It is the point a walk arrives at.
    /// </summary>
    public Vector2 PlaceAtM(int place) => _placeAtM[place];

    /// <summary>
    /// Where the node itself was struck, which <b>a merge does not move</b>: the corner a walk arrives at is
    /// the place (<see cref="PlaceAtM"/>), and this is the point off the road end that the boundary was read
    /// at — so everything this node hands over at still stands where that reading put it.
    /// </summary>
    public Vector2 AtM(int node) => _atM[node];

    /// <summary>
    /// <b>Whether this way is still handed over at</b> (WLK-3): a pair whose own way ran to a node that is
    /// now this same place joined a corner to itself, so it is dropped and its two points stand for nothing.
    /// </summary>
    public bool HandsOver(int node, FootConnectorKind kind) => _handsOver[(node * Kinds) + (int)kind];

    /// <summary>
    /// <b>Whether this point and the point the lane it belongs to arrives at are one point</b> (WLK-12): the
    /// two stand on each other, so there is no length of lane between them and a walk reaching one of them
    /// has reached the other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A point and not a pair, because a lane pinches out on its own.</b> The two lanes of a way stand a
    /// walking lane apart across the kerb, and at a corner the walk turns into rather than round, the outer
    /// lane's two ends meet while the inner lane's are still metres apart — the pavement narrowing to a point
    /// at the offset the outer lane is walked at and not at the inner one's. Weighed on the pair instead,
    /// every one of those reads as a way with something left of it and nothing ever welds.
    /// </para>
    /// <para>
    /// <b>It is not the pair <see cref="HandsOver"/> answers false for.</b> That pair's points stand for
    /// nothing, the merge having joined a corner to itself (WLK-3); a shared point is somewhere a walk
    /// arrives, and is the place the ways at either end of it meet.
    /// </para>
    /// </remarks>
    public bool Shares(int node, FootConnectorKind kind, int lane) => _shares[At(node, kind, lane)];

    /// <summary>
    /// <b>The node at the other end of one of this node's three ways</b>, or <see cref="FootJunctions.NoNode"/>
    /// where there is none — the crossing's is the node across the same carriageway, the road's the node at the
    /// far end of the same road on the same side of it, and the junction's the node round the corner.
    /// </summary>
    /// <remarks>
    /// <b>It is structural and is never read back off the points</b>: a skew corner puts the nearest pair of
    /// points on the wrong way, and two nodes a stride apart have no direction between them to read at all.
    /// The relation is its own inverse, which is what lets a way be laid once from either of its ends.
    /// </remarks>
    public int Reaches(int node, FootConnectorKind kind) => _reaches[(node * Kinds) + (int)kind];

    /// <summary>One of the node's six points: the <paramref name="lane"/>th of the pair belonging to one way.</summary>
    public Vector2 PointM(int node, FootConnectorKind kind, int lane) => _pointM[At(node, kind, lane)];

    static int At(int node, FootConnectorKind kind, int lane) =>
        (node * PointsPerNode) + ((int)kind * LanesPerWay) + lane;

    /// <summary>
    /// Where the boundary stands nearest this node — <b>the middle of its crossing's band</b>, the two
    /// points of that pair being the band's own lane lines half a lane either side of it.
    /// </summary>
    public Vector2 KerbAtM(int node) => _kerbAtM[node];

    /// <summary>
    /// <b>Where a zebra is wanted, one entry per road end</b> (WLK-10): the two kerbs an end's pair of
    /// nodes hands a crossing over at. An end whose nodes do not both stand, or whose crossings the merge
    /// dropped, asks for none.
    /// </summary>
    /// <remarks>
    /// <b>The answer goes down to the road tier as points</b> (<see cref="CrossedAtAnEnd"/>) rather than as
    /// anything of this slice's, because the paint is laid below the walk that places it
    /// ([slice-map.md](../../../../docs/slice-map.md)).
    /// </remarks>
    public CrossedAtAnEnd[] Crossed()
    {
        var asked = new CrossedAtAnEnd[NodeCount / 2];
        for (var end = 0; end < asked.Length; end++)
        {
            var near = FootJunctions.Node(end, -1);
            var far = FootJunctions.Node(end, +1);
            if (!Crosses(near) || !Crosses(far)) continue;

            asked[end] = new CrossedAtAnEnd(true, _kerbAtM[near], _kerbAtM[far]);
        }

        return asked;

        bool Crosses(int node) => _stands[node] && HandsOver(node, FootConnectorKind.Crossing);
    }

    /// <summary>
    /// Lays every pedestrian node in the town (<see cref="FootJunctions"/>) and strikes the six points each
    /// of them hands over at (WLK-9). Build-time only — it allocates freely.
    /// </summary>
    public static FootConnectors Lay(CityPlan plan, SimConfig config)
    {
        // A map laid without a pavement has no walking network at all, and saying so is better than
        // standing a node at every road end of it.
        if (plan.PavementWidthM <= 0f) return None;

        return Lay(FootJunctions.Lay(plan, config), WalkLines.Of(plan, config), config);
    }

    /// <summary>The same, off nodes and courses somebody already holds.</summary>
    public static FootConnectors Lay(FootJunctions nodes, WalkLines walks, SimConfig config)
    {
        var kerbs = walks.Boundary;
        var laneM = config.WalkingLaneWidthM;
        var setbackM = config.Road.FootConnectorAlongM;

        var stands = new bool[nodes.NodeCount];
        var atM = new Vector2[nodes.NodeCount];
        var kerbAtM = new Vector2[nodes.NodeCount];
        var pointM = new Vector2[nodes.NodeCount * PointsPerNode];

        for (var node = 0; node < nodes.NodeCount; node++)
        {
            var end = node / 2;
            var struckAtM = atM[node] = nodes.AtM(node);
            if (!nodes.StandsAt(end)) continue;

            // <b>The boundary is asked for at the node's own carriageway edge and not at the node</b>
            // (WLK-9): a node stands a figure clear of its own tarmac, and in the crotch of a fork that
            // figure reaches over the arm opposite — so the nearest boundary to it is that arm's kerb, and
            // the pair struck off it lays the crossing at a slant across the mouth and sets the walk off
            // down the wrong street. <b>The edge is a point of this road's own</b>, so the search settles
            // which kerb is meant before it settles where along it.
            if (!kerbs.Along(OwnEdgeM(nodes, config, node), 0f, out var at)) continue;

            kerbAtM[node] = at.PositionM;

            // Which way along the boundary is down the road: the kerb runs one way where the node stands and
            // the road leaves its junction the other, and only their agreement says which of the two sides
            // of the node is the road's and which is the junction's.
            var downTheRoad = Vector2.Dot(at.Direction, nodes.OutwardUnitAt(end)) >= 0f ? 1f : -1f;
            var offTheKerb = OffTheKerb(at, struckAtM);

            stands[node] = true;

            // The crossing leaves square to the boundary, so its two lanes stand along it — half a lane
            // either side of the place the node itself stands off, and on the boundary rather than beyond it.
            for (var lane = 0; lane < LanesPerWay; lane++)
            {
                var asideM = (lane - ((LanesPerWay - 1) * 0.5f)) * laneM * downTheRoad;
                if (!kerbs.Along(at.PositionM, asideM, out var across)) continue;

                pointM[At(node, FootConnectorKind.Crossing, lane)] = across.PositionM;
            }

            // And the other two leave along the boundary, so their lanes stand square to it — a setback
            // along the kerb from the node, and the band laid against the kerb rather than beyond it, so
            // each lane's own line is the middle of its half of it (WLK-8).
            foreach (var kind in (ReadOnlySpan<FootConnectorKind>)[FootConnectorKind.Road, FootConnectorKind.Junction])
            {
                var stepM = setbackM * downTheRoad * (kind == FootConnectorKind.Road ? 1f : -1f);
                if (!kerbs.Along(at.PositionM, stepM, out var along)) continue;

                var outward = CarriedAlong(along, offTheKerb);
                for (var lane = 0; lane < LanesPerWay; lane++)
                {
                    var struckM = along.PositionM + (outward * config.WalkingLaneAtM(lane));

                    // <b>And onto the course that lane is walked on</b> (WLK-9): the line is the whole
                    // shape's own offset and passes near this point rather than through it, so the point
                    // is moved to the course and not the course fitted to the point.
                    pointM[At(node, kind, lane)] =
                        walks.Course(lane).NearestTo(struckM, out var onTheCourse)
                            ? onTheCourse.PositionM
                            : struckM;
                }
            }
        }

        var (placeOf, placeAtM) = Places(nodes, stands);
        var (handsOver, reaches) = Kept(nodes, stands, placeOf);
        var shares = Shared(stands, reaches, handsOver, pointM, config.Road.FootConnectorMergeM);
        return new FootConnectors(
            stands, atM, kerbAtM, placeOf, placeAtM, pointM, handsOver, shares, reaches);
    }

    /// <summary>
    /// <b>The merge, numbered</b> (WLK-3): the place each node was merged into, and where each place stands —
    /// the middle of the nodes in it, which is a node's own place where nothing merged with it.
    /// </summary>
    /// <remarks>
    /// <b>Which nodes are one place is <see cref="FootJunctions"/>'s answer and is not taken again here.</b>
    /// The numbering is this table's own only because a node that stands nowhere has no place at all, and a
    /// place nothing stands in is not a corner a walk arrives at.
    /// </remarks>
    static (int[] PlaceOf, Vector2[] PlaceAtM) Places(FootJunctions nodes, bool[] stands)
    {
        var placeOf = new int[nodes.NodeCount];
        Array.Fill(placeOf, NoPlace);

        var numbered = new Dictionary<int, int>();
        var placeAtM = new List<Vector2>();
        for (var node = 0; node < nodes.NodeCount; node++)
        {
            if (!stands[node]) continue;

            var junction = nodes.JunctionOf(node);
            if (!numbered.TryGetValue(junction, out var place))
            {
                numbered[junction] = place = placeAtM.Count;
                placeAtM.Add(nodes.HubM(node));
            }

            placeOf[node] = place;
        }

        return (placeOf, [.. placeAtM]);
    }

    /// <summary>
    /// <b>Which of each node's three ways survives the merge</b> (WLK-3): every one whose own way ran
    /// somewhere else. A pair whose way ran to a node the merge has made <em>this same place</em> would join
    /// a corner to itself, so it is dropped — which is what leaves two nodes merged at the corner of a
    /// crossroads handing over to four ways and not six.
    /// </summary>
    /// <remarks>
    /// <b>Where each way runs is structural and is not read back off the points.</b> A crossing runs to the
    /// node across its own carriageway, a road walk to the node the street's own walk reaches next on the
    /// same physical side (<see cref="FootJunctions.DownTheRoad"/>) — the far end of the road, and the end
    /// past every cut junction between them, those standing no node at all (WLK-2) — and a junction walk to
    /// the node round the corner (<see cref="FootJunctions.WedgeNeighbour"/>). A way whose node stands
    /// nowhere is kept: what it runs to is missing, not merged.
    /// </remarks>
    static (bool[] HandsOver, int[] Reaches) Kept(FootJunctions nodes, bool[] stands, int[] placeOf)
    {
        var handsOver = new bool[nodes.NodeCount * Kinds];
        var reaches = new int[nodes.NodeCount * Kinds];
        Array.Fill(reaches, FootJunctions.NoNode);

        for (var node = 0; node < nodes.NodeCount; node++)
        {
            if (!stands[node]) continue;

            var end = node / 2;
            var hand = node % 2 == 1 ? +1 : -1;

            Keep(FootConnectorKind.Crossing, FootJunctions.Node(end, -hand));
            Keep(FootConnectorKind.Road, nodes.DownTheRoad(node));
            Keep(FootConnectorKind.Junction, nodes.WedgeNeighbour(node));

            void Keep(FootConnectorKind kind, int onto)
            {
                var joined = onto >= 0 && stands[onto] && placeOf[onto] == placeOf[node];
                handsOver[(node * Kinds) + (int)kind] = !joined;
                reaches[(node * Kinds) + (int)kind] = onto;
            }
        }

        return (handsOver, reaches);
    }

    /// <summary>
    /// <b>Which points stand on the point their lane arrives at, welded into one</b> (WLK-12): where the two
    /// are within <paramref name="withinM"/> of each other both are moved onto the place between them, and
    /// what was a stride of lane between two places becomes one place the ways either side of it meet at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Weighed and applied a lane at a time</b> (<see cref="Shares"/>). A pavement's two lanes stand a
    /// walking lane apart across the kerb, and where the walk turns into a corner rather than round one they
    /// pinch out at different places — so one lane really is nothing while the other still has metres of
    /// walk, and a reading taken on the pair finds neither.
    /// </para>
    /// <para>
    /// <b>Nothing cascades and nothing has to.</b> A point is the end of exactly one lane and the relation
    /// the lane reaches over is its own inverse (<see cref="Reaches"/>), so a weld joins two points and there
    /// is no chain of them to close — unlike the node merge above it (WLK-3), where a run of near
    /// neighbours really is one place.
    /// </para>
    /// </remarks>
    static bool[] Shared(
        bool[] stands, int[] reaches, bool[] handsOver, Vector2[] pointM, float withinM)
    {
        var shares = new bool[pointM.Length];
        for (var node = 0; node < stands.Length; node++)
        {
            if (!stands[node]) continue;

            for (var which = 0; which < Kinds; which++)
            {
                var kind = (FootConnectorKind)which;

                // Once per pair and from the lower-numbered end, the relation being its own inverse.
                var onto = reaches[(node * Kinds) + which];
                if (onto <= node || !stands[onto] || !handsOver[(node * Kinds) + which]) continue;

                for (var lane = 0; lane < LanesPerWay; lane++)
                {
                    var oneM = pointM[At(node, kind, lane)];
                    var otherM = pointM[At(onto, kind, lane)];
                    if (Vector2.Distance(oneM, otherM) > withinM) continue;

                    pointM[At(node, kind, lane)] = pointM[At(onto, kind, lane)] = (oneM + otherM) * 0.5f;
                    shares[At(node, kind, lane)] = true;
                    shares[At(onto, kind, lane)] = true;
                }
            }
        }

        return shares;
    }

    /// <summary>
    /// <b>How far one lane's point stands from the point that lane arrives at</b> — the length a walk down it
    /// would have, and what the weld is weighed on (WLK-12).
    /// </summary>
    public float ApartM(int node, FootConnectorKind kind, int lane)
    {
        var onto = Reaches(node, kind);
        return onto < 0 || !_stands[node] || !_stands[onto]
            ? float.PositiveInfinity
            : Vector2.Distance(_pointM[At(node, kind, lane)], _pointM[At(onto, kind, lane)]);
    }

    /// <summary>
    /// The way off the tarmac where the boundary is standing: square to the line, and pointed the side the
    /// node stands on. <b>A perpendicular of the boundary's own and never the run from the kerb to the
    /// node</b> — the two agree where the node stands square off the line and part company a setback along
    /// a corner, where what is wanted is still square to the kerb under the point.
    /// </summary>
    /// <summary>
    /// <b>The point on the node's own carriageway edge that its node stands off</b> (WLK-2): the same place
    /// on the road's line, the same hand, half the carriageway out instead of half the carriageway and the
    /// figure beyond it.
    /// </summary>
    /// <remarks>
    /// <b>It is a point of this road and not a place on the boundary</b>, and that is the whole of what it is
    /// for: asked where the <em>boundary</em> passes nearest it, a node in the crotch of a fork answers with
    /// the arm opposite, and this answers with its own street whatever stands over the node.
    /// </remarks>
    static Vector2 OwnEdgeM(FootJunctions nodes, SimConfig config, int node)
    {
        var end = node / 2;
        var onTheLineM = nodes.OnTheLineAt(end);
        var asideM = nodes.AsideM(end);
        return asideM <= 0f
            ? nodes.AtM(node)
            : onTheLineM
              + ((nodes.AtM(node) - onTheLineM) * ((asideM - config.Road.FootNodeAsideM) / asideM));
    }

    static Vector2 OffTheKerb(SplineSample at, Vector2 nodeM)
    {
        var square = Heading.RightOf(at.Direction);
        return Vector2.Dot(square, nodeM - at.PositionM) >= 0f ? square : -square;
    }

    /// <summary>
    /// The same at a place a setback along the line, where the node itself is no longer the side of it to
    /// read: <b>the boundary is a closed ring and a step along it can round a corner</b>, so a point a
    /// setback down the kerb of a narrow street can stand nearer the far side of the road than the node does.
    /// Which side is off the tarmac is the node's answer, carried along.
    /// </summary>
    static Vector2 CarriedAlong(SplineSample at, Vector2 offTheKerbAtTheNode)
    {
        var square = Heading.RightOf(at.Direction);
        return Vector2.Dot(square, offTheKerbAtTheNode) >= 0f ? square : -square;
    }
}
