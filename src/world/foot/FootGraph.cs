using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Foot;

/// <summary>What a stretch of the foot graph is, for whoever has to tell a kerb from a zebra.</summary>
internal enum FootEdgeKind : byte
{
    /// <summary>
    /// A band of pavement: a walk beside a road, the walk round the wedge between two arms, or the
    /// connection a junction reaches one of its ways on. <b>They are one kind and not three</b> — the same
    /// concrete at the same width, and the only thing that differs is which figure placed it.
    /// </summary>
    Pavement,

    /// <summary>The one kind of edge that touches a carriageway, which is what makes crossing at a crossing structural.</summary>
    Crossing,
}

/// <summary>
/// The fine walking graph: <b>the pavement as the lanes it is walked down</b> (WLK-1,
/// <see cref="PavementLanes"/>). Nothing here re-discovers where a kerb is and nothing wraps a shape —
/// every lane is the driven ground's own boundary moved off itself by that lane's distance, cut at the
/// joints between the pieces the move came back with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every edge is a lane and every lane is walked one way</b> (WLK-8), so there is no reverse to get for
/// nothing: the lane beside this one is its own line a lane's width away, and a walker turns round by
/// arriving at a node and setting off down the one going back. <b>The line an edge holds is the line a body
/// is held on</b> — nothing is offset again between here and the walker.
/// </para>
/// <para>
/// <b>A join is not a place.</b> Both lanes end at the node they share and the line is read off that node,
/// so the point one leg finishes on is the point the next begins on: nothing to reconcile, nothing to trim,
/// no chain to relax. Nodes within a quarter-metre are welded.
/// </para>
/// <para>
/// <b>A node here is a seam, or a place a crossing parts a course.</b> The seams are the shape's own corners
/// rather than stations somebody chose, every one of them is of degree two, and the contraction folds them
/// into the runs they are part of. <b>The places a walk chooses between ways are the crossings' ends</b>
/// (WLK-15, <see cref="CrossingWays"/>): a course is parted there and the crossing's ways join it, which is
/// what joins one course to another.
/// </para>
/// </remarks>
internal sealed partial class FootGraph : IFineGraph, ILaneEnds
{
    readonly Vector2[] _nodeM;
    readonly int[] _edgeFrom;
    readonly int[] _edgeTo;
    readonly float[] _edgeLengthM;
    readonly float[] _edgeBandM;
    readonly FootEdgeKind[] _edgeKind;
    readonly byte[] _edgeChannels;
    readonly int[] _edgeArcOffsets;
    readonly ArcSeg[] _edgeArcs;
    readonly int[] _nodeOutOffsets;
    readonly int[] _nodeOutEdges;

    /// <summary>
    /// The lanes arriving at each node, in the same shape as the ones leaving. <b>Its own list and not the
    /// departures read backwards</b>: a lane is walked one way (WLK-8), so a node's two degrees are two
    /// facts.
    /// </summary>
    readonly int[] _nodeInOffsets;

    readonly int[] _nodeInEdges;

    /// <summary>
    /// The forward stretches over a grid, which is the whole of what <see cref="NearestEdge"/> is. Laid
    /// with the graph because the graph is immutable and the tick asks the question.
    /// </summary>
    readonly ChainIndex _nearest;

    FootGraph(
        Vector2[] nodeM, int[] edgeFrom, int[] edgeTo, float[] edgeLengthM, float[] edgeBandM,
        FootEdgeKind[] edgeKind, byte[] edgeChannels, int[] edgeArcOffsets, ArcSeg[] edgeArcs, int[] nodeOutOffsets,
        int[] nodeOutEdges, int[] nodeInOffsets, int[] nodeInEdges, GridLevel nearestLevel)
    {
        _nodeM = nodeM;
        _edgeFrom = edgeFrom;
        _edgeTo = edgeTo;
        _edgeLengthM = edgeLengthM;
        _edgeBandM = edgeBandM;
        _edgeKind = edgeKind;
        _edgeChannels = edgeChannels;
        _edgeArcOffsets = edgeArcOffsets;
        _edgeArcs = edgeArcs;
        _nodeOutOffsets = nodeOutOffsets;
        _nodeOutEdges = nodeOutEdges;

        _nodeInOffsets = nodeInOffsets;
        _nodeInEdges = nodeInEdges;

        // <b>Every lane, because every lane is its own line</b>: the two lanes of a pavement stand a lane
        // apart, so which of them a point is nearest is the answer the question wants rather than a tie.
        _nearest = ChainIndex.OfChains(_edgeArcs, _edgeArcOffsets, _edgeLengthM, nearestLevel);
    }

    public int NodeCount => _nodeM.Length;

    public int EdgeCount => _edgeFrom.Length;

    public Vector2 AnchorM(int node) => _nodeM[node];

    public int FromNode(int edge) => _edgeFrom[edge];

    public int ToNode(int edge) => _edgeTo[edge];

    public float LengthM(int edge) => _edgeLengthM[edge];

    /// <summary>
    /// <b>None</b> (WLK-8): a lane is walked one way, and the lane beside it is a line of its own a lane's
    /// width away rather than this one read backwards. A walker turns round by walking to a node and
    /// setting off down the lane going the other way, which is where the turn is a turn.
    /// </summary>
    public int Reverse(int edge) => -1;

    /// <summary><b>None</b>: a pavement's two lanes are walked opposite ways (WLK-8), so neither runs beside the other the same way.</summary>
    public int Beside(int edge, bool inward) => -1;

    public ReadOnlySpan<int> EdgesOut(int node) =>
        _nodeOutEdges.AsSpan(_nodeOutOffsets[node], _nodeOutOffsets[node + 1] - _nodeOutOffsets[node]);

    /// <summary>The lanes arriving at a node, which are their own list and not the departures turned round.</summary>
    public ReadOnlySpan<int> EdgesIn(int node) =>
        _nodeInEdges.AsSpan(_nodeInOffsets[node], _nodeInOffsets[node + 1] - _nodeInOffsets[node]);

    public int LaneCount => EdgeCount;

    /// <summary>
    /// The stretches a walker on this one may leave for at the end of it. <b>The way back is among them</b>,
    /// unlike a carriageway's (TER-5f): a walker turns round where it likes.
    /// </summary>
    public ReadOnlySpan<int> Onward(int lane) => EdgesOut(_edgeTo[lane]);

    public Vector2 StartsAtM(int lane) => _nodeM[_edgeFrom[lane]];

    public Vector2 EndsAtM(int lane) => _nodeM[_edgeTo[lane]];

    /// <summary>How wide the ground this stretch runs down is, which is what a lane is a quarter of.</summary>
    public float BandM(int edge) => _edgeBandM[edge];

    public FootEdgeKind KindOf(int edge) => _edgeKind[edge];

    /// <summary>
    /// <b>The collision channels a stretch is on</b> (PHY-1a): the ground's, a deck's for its own walk, and both for a joint
    /// between the two (WLK-1b, <see cref="DeckWalks"/>).
    /// </summary>
    public byte ChannelsOf(int edge) => _edgeChannels[edge];

    /// <summary>The stretch's own line, in the direction this edge is walked.</summary>
    public ReadOnlySpan<ArcSeg> ArcsOf(int edge) =>
        _edgeArcs.AsSpan(_edgeArcOffsets[edge], _edgeArcOffsets[edge + 1] - _edgeArcOffsets[edge]);

    /// <summary>
    /// The stretch whose own line passes nearest a point, and how far along it that is — the forward
    /// direction of it, since both directions are the same line.
    /// </summary>
    /// <remarks>
    /// <b>Every walk in the town begins and ends with two of these</b> (the entry and the goal), so it is
    /// asked from the tick and not only when a body is stood up. Over a town whose pavement runs to
    /// thousands of stretches a scan of all of them would be the largest single cost on the walking side, so
    /// <see cref="ChainIndex"/> answers — the same arithmetic over the handful of stretches that could
    /// possibly win.
    /// </remarks>
    public int NearestEdge(Vector2 pointM, out float alongM) => _nearest.Nearest(pointM, out alongM);

    /// <inheritdoc cref="NearestEdge(Vector2, out float)"/>
    /// <param name="scan">This caller's own working set over the stretches (<see cref="NewScan"/>), for an ask off its own thread.</param>
    public int NearestEdge(ChainIndex.Scan scan, Vector2 pointM, out float alongM) => _nearest.Nearest(scan, pointM, out alongM);


    /// <summary>
    /// <b>Every lane passing within <paramref name="radiusM"/> of a place</b>, and how far along each of them
    /// that place stands. It is how the lane beside one is found (<see cref="Reverse"/> being none): the two
    /// lanes of a pavement are a lane's width apart, so a body standing on either is within a pavement of both.
    /// </summary>
    /// <returns>How many there were, which may be more than the room given — the extras are not written.</returns>
    public int EdgesNear(Vector2 pointM, float radiusM, Span<int> into, Span<float> alongM) =>
        _nearest.Near(pointM, radiusM, into, alongM);

    /// <inheritdoc cref="EdgesNear(Vector2, float, Span{int}, Span{float})"/>
    /// <param name="scan">This caller's own working set (<see cref="NewScan"/>), for a query off its own thread.</param>
    public int EdgesNear(ChainIndex.Scan scan, Vector2 pointM, float radiusM, Span<int> into, Span<float> alongM) =>
        _nearest.Near(scan, pointM, radiusM, into, alongM);

    /// <summary>
    /// A scan of the index the stretches are found by, for a caller that means to ask
    /// <see cref="NearestEdge(ChainIndex.Scan, Vector2, out float)"/> or
    /// <see cref="EdgesNear(ChainIndex.Scan, Vector2, float, Span{int}, Span{float})"/> off its own thread.
    /// </summary>
    public ChainIndex.Scan NewScan() => _nearest.NewScan();

    /// <summary>
    /// <b>Lays the graph off the town's pavement</b> (WLK-1, <see cref="PavementLanes"/>): every lane of
    /// every course, as the pieces its own move came back with and parted where the crossings meet it, and
    /// the crossings' own ways (WLK-15).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The pieces are the shape's own and no seam is invented.</b> A course is a closed line with no end,
    /// so a graph over it has to be cut somewhere; what it is cut at is the joints between the arcs the move
    /// produced, which are the corners of the town rather than a station somebody chose, and the places a
    /// crossing parts it (<see cref="Parted"/>). A joint is a node of degree two, which the contraction folds
    /// into the run it is part of (<see cref="World.Routing.RunNetwork"/>, which promotes a place on a ring
    /// nothing splits).
    /// </para>
    /// <para>
    /// <b>Kept and handed out, it is still not safe</b>: the index it answers <see cref="NearestEdge"/>
    /// from carries a scratch of its own (<see cref="ChainIndex"/>), so two towns asking it at once is two
    /// walks over one working set.
    /// </para>
    /// </remarks>
    public static FootGraph Build(CityPlan plan, SimConfig config)
    {
        var pavement = PavementLanes.Of(plan, config);
        var crossings = CrossingWays.Of(plan, pavement, config);
        var (ground, decks) = DeckWalks.Of(plan, pavement.EndedAtTheCrossings(crossings, config), config);
        return Build(ground, crossings, config, decks);
    }

    /// <summary>
    /// The same, off a pavement and a set of crossings somebody already holds — the pavement ended at them
    /// (<see cref="PavementLanes.EndedAtTheCrossings"/>), or not where a reader asks for the walk as laid — and the walk
    /// across the decks over other roads, where there is any (WLK-1b).
    /// </summary>
    public static FootGraph Build(PavementLanes pavement, CrossingWays crossings, SimConfig config, DeckWalks? decks = null)
    {
        var weldM = config.Network.FootGraphNodeWeldM;
        var builder = new Builder(weldM, config.Grid);
        var bandM = config.WalkingLaneWidthM;
        Span<ArcSeg> one = stackalloc ArcSeg[1];
        var pieces = new List<ArcSeg>();
        AddTheLanes(pavement, crossings, CityPlan.RoadArrays.GroundChannel);

        // And the crossings' own ways, which begin and end at the places they parted (WLK-15): the same
        // points, so the weld joins them to the walk rather than standing a second node beside it.
        foreach (var way in crossings.Ways) builder.AddLane(way.Arcs, way.BandM, way.Kind);

        // And the turn across the walk where one ends (GEN-75), either way: the lane a walker arrives on there ends, and
        // the other takes them back.
        foreach (var (oneM, otherM) in pavement.Turns)
        {
            var acrossM = otherM - oneM;
            var headingRad = MathF.Atan2(acrossM.Y, acrossM.X);
            one[0] = new ArcSeg(oneM, headingRad, acrossM.Length(), 0f);
            builder.AddLane(one, bandM, FootEdgeKind.Pavement);
            one[0] = new ArcSeg(otherM, headingRad + MathF.PI, acrossM.Length(), 0f);
            builder.AddLane(one, bandM, FootEdgeKind.Pavement);
        }

        // And the decks' own walk on their own channel, and the joints up onto it and down off it (WLK-1b).
        if (decks is { } raised)
        {
            var deck = CityPlan.RoadArrays.ChannelOf(CityPlan.RoadArrays.Over);
            AddTheLanes(raised.Lanes, CrossingWays.None, deck);
            foreach (var joint in raised.Joints)
            {
                var alongM = joint.ToM - joint.FromM;
                one[0] = new ArcSeg(joint.FromM, MathF.Atan2(alongM.Y, alongM.X), alongM.Length(), 0f);
                var (fromChannels, toChannels) = joint.Up
                    ? (CityPlan.RoadArrays.GroundChannel, deck)
                    : (deck, CityPlan.RoadArrays.GroundChannel);
                builder.AddLane(one, bandM, FootEdgeKind.Pavement, fromChannels, toChannels);
            }
        }

        return builder.Lay();

        void AddTheLanes(PavementLanes lanes, CrossingWays parting, byte channels)
        {
            Span<ArcSeg> piece = stackalloc ArcSeg[1];
            Span<ArcSeg> turned = stackalloc ArcSeg[1];
            for (var lane = 0; lane < lanes.Count; lane++)
            {
                var withTheRing = lanes.RunsWithTheRing(lane);
                var rings = lanes.RingsOf(lane);
                for (var ring = 0; ring < rings.Length; ring++)
                {
                    Parted(rings[ring], parting.PartedOn(lane, ring), lanes.GapsOf(lane, ring), weldM, pieces);
                    for (var at = 0; at < pieces.Count; at++)
                    {
                        // Walked against its ring, a lane is the same pieces in the opposite order and each of
                        // them turned round: the line is the ground, and the direction is which way a body is
                        // held on it.
                        if (withTheRing)
                        {
                            piece[0] = pieces[at];
                            builder.AddLane(piece, bandM, FootEdgeKind.Pavement, channels, channels);
                            continue;
                        }

                        piece[0] = pieces[pieces.Count - 1 - at];
                        Spline.ReverseInto(piece, turned);
                        builder.AddLane(turned, bandM, FootEdgeKind.Pavement, channels, channels);
                    }
                }
            }
        }
    }

    /// <summary>
    /// <b>One ring of one lane of the pavement, cut where the crossings part it</b> (WLK-15,
    /// <see cref="CrossingWays.PartedOn"/>) — the ring's own pieces, and each piece carrying a place the
    /// walk meets it handed over as the two pieces it is.
    /// </summary>
    /// <remarks>
    /// <b>Nothing is moved and nothing is fitted</b>: a cut is a metre along the line the move laid, so the
    /// two pieces either side of it stand on exactly the ground the one piece did. <b>A place within a weld
    /// of a joint, or of a gap's end, is not cut at all</b> — the node is already there, and cutting would leave a
    /// stub shorter than the distance two nodes are one node at, which beside a gap is dropped with the ground it held.
    /// </remarks>
    /// <param name="gaps">
    /// The stretches of the ring nobody walks (<see cref="PavementLanes.GapsOf"/>, GEN-75): cut at their ends wherever
    /// those stand, no piece inside one handed over, and none beside one shorter than a weld — the turn there stands at
    /// the gap's end, and a sliver would be one node walked to itself.
    /// </param>
    static void Parted(
        ReadOnlySpan<ArcSeg> ring, ReadOnlySpan<float> crossedM, ReadOnlySpan<(float FromM, float ToM)> gaps, float weldM,
        List<ArcSeg> into)
    {
        into.Clear();

        var startM = 0f;
        var next = 0;
        var nextGap = 0;
        foreach (ref readonly var arc in ring)
        {
            var endM = startM + arc.LengthM;
            while (next < crossedM.Length && crossedM[next] < startM + weldM) next++;

            var fromM = 0f;
            while (true)
            {
                var crossAtM = next < crossedM.Length && crossedM[next] <= endM - weldM ? crossedM[next] : float.PositiveInfinity;
                if (!float.IsPositiveInfinity(crossAtM) && WithinAWeldOfAGapEnd(gaps, crossAtM, weldM))
                {
                    next++;
                    continue;
                }
                var gapAtM = GapEnd(gaps, nextGap, out var gapEndM) && gapEndM < endM ? gapEndM : float.PositiveInfinity;
                if (float.IsPositiveInfinity(crossAtM) && float.IsPositiveInfinity(gapAtM)) break;

                var atM = MathF.Min(crossAtM, gapAtM) - startM;
                if (gapAtM <= crossAtM) nextGap++;
                else next++;

                if (atM <= fromM) continue;

                Add(arc, fromM, atM, startM, gaps, weldM, into);
                fromM = atM;
            }

            Add(arc, fromM, arc.LengthM, startM, gaps, weldM, into);
            startM = endM;
        }

        static bool WithinAWeldOfAGapEnd(ReadOnlySpan<(float FromM, float ToM)> gaps, float atM, float weldM)
        {
            foreach (var gap in gaps)
            {
                if (MathF.Abs(atM - gap.FromM) < weldM || MathF.Abs(atM - gap.ToM) < weldM) return true;
            }

            return false;
        }

        // The ends of the gaps in order along the ring, each gap's start then its end.
        static bool GapEnd(ReadOnlySpan<(float FromM, float ToM)> gaps, int at, out float endM)
        {
            endM = at < 2 * gaps.Length ? (at % 2 == 0 ? gaps[at / 2].FromM : gaps[at / 2].ToM) : 0f;
            return at < 2 * gaps.Length;
        }

        static void Add(
            in ArcSeg arc, float fromM, float toM, float startM, ReadOnlySpan<(float FromM, float ToM)> gaps, float weldM,
            List<ArcSeg> into)
        {
            var (fromAtM, toAtM) = (startM + fromM, startM + toM);
            var middleM = (fromAtM + toAtM) * 0.5f;
            foreach (var gap in gaps)
            {
                if (middleM > gap.FromM && middleM < gap.ToM) return;

                var besideAGap = MathF.Abs(toAtM - gap.FromM) < LineTolerance.OnePlaceM || MathF.Abs(fromAtM - gap.ToM) < LineTolerance.OnePlaceM;
                if (besideAGap && toM - fromM < weldM) return;
            }

            into.Add(Piece(arc, fromM, toM));
        }
    }

    /// <summary>One stretch of one piece, as the piece of the same circle it is.</summary>
    static ArcSeg Piece(in ArcSeg arc, float fromM, float toM) =>
        new(arc.PointAtM(fromM), arc.HeadingAtRad(fromM), toM - fromM, arc.Curvature);
}
