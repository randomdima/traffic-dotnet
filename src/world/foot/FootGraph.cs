using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Foot;

/// <summary>What a stretch of the foot graph is, for whoever has to tell a kerb from a zebra.</summary>
internal enum FootEdgeKind : byte
{
    /// <summary>A band of pavement running beside a carriageway.</summary>
    Pavement,

    /// <summary>The band round a junction, joining the pavements of the arms that meet at it.</summary>
    JunctionCorner,

    /// <summary>The one kind of edge that touches a carriageway, which is what makes crossing at a crossing structural.</summary>
    Crossing,
}

/// <summary>
/// The fine walking graph: <b>every stretch of pavement, every band round a junction and every crossing,
/// derived from the plan and never re-traced</b>. Nothing here re-discovers where a kerb is —
/// a carriageway contributes the two bands either side of it, a junction contributes the band that joins
/// them, and a crossing contributes the one edge that touches a road.
/// </summary>
/// <remarks>
/// <para>
/// <b>The line is the pavement's own line, and there is only one of it.</b> A walk along a stretch is
/// that stretch's own curve — the kerb's points, at the kerb's own distance, with the kerb's own
/// curvature — and both directions hold it. <em>Half a band in is the middle of the band, not near it.</em>
/// A junction's band is the arc at the same distance outside its disc; a crossing is its own band's middle.
/// </para>
/// <para>
/// <b>A join is not a place.</b> Both edges end at the node they share and the line is read off that node,
/// so the point one leg finishes on is the point the next begins on: nothing to reconcile, nothing to trim,
/// no chain to relax. Nodes within a quarter-metre are welded, because a curve can otherwise end that far
/// the wrong side of its own node and a line laid through those stations steps backwards and crosses itself.
/// </para>
/// <para>
/// Directed edges are laid in pairs, so <c>edge ^ 1</c> is the same stretch walked the other way and the
/// contraction gets its reverse for nothing.
/// </para>
/// </remarks>
internal sealed partial class FootGraph : IFineGraph
{
    readonly Vector2[] _nodeM;
    readonly int[] _edgeFrom;
    readonly int[] _edgeTo;
    readonly float[] _edgeLengthM;
    readonly float[] _edgeBandM;
    readonly FootEdgeKind[] _edgeKind;
    readonly int[] _edgeArcOffsets;
    readonly ArcSeg[] _edgeArcs;
    readonly int[] _nodeOutOffsets;
    readonly int[] _nodeOutEdges;

    /// <summary>
    /// The forward stretches over a grid, which is the whole of what <see cref="NearestEdge"/> is. Laid
    /// with the graph because the graph is immutable and the tick asks the question.
    /// </summary>
    readonly ChainIndex _nearest;

    FootGraph(
        Vector2[] nodeM, int[] edgeFrom, int[] edgeTo, float[] edgeLengthM, float[] edgeBandM,
        FootEdgeKind[] edgeKind, int[] edgeArcOffsets, ArcSeg[] edgeArcs, int[] nodeOutOffsets, int[] nodeOutEdges,
        float nearestCellM)
    {
        _nodeM = nodeM;
        _edgeFrom = edgeFrom;
        _edgeTo = edgeTo;
        _edgeLengthM = edgeLengthM;
        _edgeBandM = edgeBandM;
        _edgeKind = edgeKind;
        _edgeArcOffsets = edgeArcOffsets;
        _edgeArcs = edgeArcs;
        _nodeOutOffsets = nodeOutOffsets;
        _nodeOutEdges = nodeOutEdges;

        // Only the forward half of each pair: the two directions of a stretch are the same line, so
        // indexing both would offer every answer twice and give the reverse edge a chance at a tie.
        var builder = new ChainIndex.Builder();
        for (var edge = 0; edge < _edgeFrom.Length; edge += 2) builder.Add(edge, ArcsOf(edge), _edgeLengthM[edge]);

        _nearest = builder.Seal(nearestCellM);
    }

    public int NodeCount => _nodeM.Length;

    public int EdgeCount => _edgeFrom.Length;

    public Vector2 AnchorM(int node) => _nodeM[node];

    public int FromNode(int edge) => _edgeFrom[edge];

    public int ToNode(int edge) => _edgeTo[edge];

    public float LengthM(int edge) => _edgeLengthM[edge];

    /// <summary>The two directions of one stretch are laid together, so the reverse is one bit away.</summary>
    public int Reverse(int edge) => edge ^ 1;

    public ReadOnlySpan<int> EdgesOut(int node) =>
        _nodeOutEdges.AsSpan(_nodeOutOffsets[node], _nodeOutOffsets[node + 1] - _nodeOutOffsets[node]);

    /// <summary>A walk is aimed at a place on the pavement and never at a node of it, so none of them is kept for its own sake.</summary>
    public bool AlwaysANode(int node) => false;

    /// <summary>How wide the ground this stretch runs down is, which is what a lane is a quarter of.</summary>
    public float BandM(int edge) => _edgeBandM[edge];

    public FootEdgeKind KindOf(int edge) => _edgeKind[edge];

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
    /// thousands of stretches a scan of all of them was the largest single cost on the walking side, and
    /// what answers now is <see cref="ChainIndex"/> — the same arithmetic over the handful of stretches
    /// that could possibly win.
    /// </remarks>
    public int NearestEdge(Vector2 pointM, out float alongM) => _nearest.Nearest(pointM, out alongM);

    /// <summary>
    /// Lays the whole graph off the plan. Build-time only: it allocates freely, and nothing it produces is
    /// written to again.
    /// </summary>
    public static FootGraph Build(CityPlan plan, SimConfig config)
    {
        var builder = new Builder(config.Network.FootGraphNodeWeldM);
        var bandM = plan.PavementWidthM;

        // A map laid without a pavement has no walking network at all, and saying so is better than
        // laying one down the middle of its roads.
        if (bandM > 0f)
        {
            var terrain = new TerrainGrid(plan, config);
            var strips = Strips(plan, config, bandM);
            var corners = Corners(plan, strips, bandM);
            var ends = Lay(builder, strips, bandM);
            KerbCorners(plan, builder, corners, ends, bandM);
            ArmBands(plan, terrain, builder, ends, bandM);
            HeadBands(plan, builder, ends, bandM);
            LotBands(plan, terrain, builder, bandM);
            Crossings(plan, builder, bandM);
        }

        return builder.Prune(config.Network.FootGraphStubPruneM, config.NearestChainCellM);
    }
}
