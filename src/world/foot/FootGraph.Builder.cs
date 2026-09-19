using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Foot;

/// <summary>Collecting the nodes and pairs a build produces, and welding what lands on top of what is already there.</summary>
internal sealed partial class FootGraph
{
    /// <summary>
    /// Collects nodes and directed pairs, welding a node onto one already standing within the weld
    /// distance rather than laying a second one beside it.
    /// </summary>
    /// <remarks>
    /// <b>It drops nothing and joins nothing.</b> Every way is placed by name (WLK-1a) — the crossing over a
    /// carriageway, the walk down a road, the walk round a junction — so there are no loose ends to stitch,
    /// no stubs to prune and no line laid twice over one piece of ground to recognise. The weld is the whole
    /// of what reconciles the pieces, and it reconciles them at the points the nodes hand over at
    /// (<see cref="FootConnectors"/>). <b>Nothing feeds it at present</b>, the ways being a named gap.
    /// </remarks>
    sealed class Builder(float weldM)
    {
        readonly List<Vector2> _nodeM = [];
        readonly Dictionary<(int X, int Y), List<int>> _welds = [];
        readonly List<int> _edgeFrom = [];
        readonly List<int> _edgeTo = [];
        readonly List<float> _edgeLengthM = [];
        readonly List<float> _edgeBandM = [];
        readonly List<FootEdgeKind> _edgeKind = [];

        // One array per edge while building and flattened once at the end, a flat store with offsets in it
        // being the shape the graph keeps rather than the shape it is collected in.
        readonly List<ArcSeg[]> _edgeArcs = [];

        /// <summary>
        /// <b>One lane, walked one way, laid between the nodes its two ends stand at</b> (WLK-8). <b>A line
        /// whose two ends are one node is not laid</b>: a lane running from a node to itself is ground no
        /// walk can be stationed along and a corner nothing can be turned on. Both ends welding onto one
        /// node is how a lane shorter than the weld becomes a loop.
        /// </summary>
        /// <remarks>
        /// <b>There is no way back and that is the point</b> (<see cref="FootGraph.Reverse"/>): the two
        /// lanes of a pavement are two lines a lane's width apart, each walked its own way, and the one
        /// beside this is a lane in its own right rather than this one read backwards.
        /// </remarks>
        public void AddLane(ReadOnlySpan<ArcSeg> arcs, float bandM, FootEdgeKind kind)
        {
            if (arcs.Length == 0) return;

            var from = NodeAt(arcs[0].StartM);
            var to = NodeAt(arcs[^1].EndM);
            if (from == to) return;

            Add(from, to, arcs.ToArray(), Spline.TotalLengthM(arcs), bandM, kind);
        }

        void Add(int fromNode, int toNode, ArcSeg[] arcs, float lengthM, float bandM, FootEdgeKind kind)
        {
            _edgeFrom.Add(fromNode);
            _edgeTo.Add(toNode);
            _edgeLengthM.Add(lengthM);
            _edgeBandM.Add(bandM);
            _edgeKind.Add(kind);
            _edgeArcs.Add(arcs);
        }

        int NodeAt(Vector2 pointM)
        {
            var cell = Cell(pointM);
            for (var y = -1; y <= 1; y++)
            {
                for (var x = -1; x <= 1; x++)
                {
                    if (!_welds.TryGetValue((cell.X + x, cell.Y + y), out var here)) continue;

                    foreach (var node in here)
                    {
                        if ((_nodeM[node] - pointM).LengthSquared() <= weldM * weldM) return node;
                    }
                }
            }

            _nodeM.Add(pointM);
            if (!_welds.TryGetValue(cell, out var bucket)) _welds[cell] = bucket = [];

            bucket.Add(_nodeM.Count - 1);
            return _nodeM.Count - 1;
        }

        (int X, int Y) Cell(Vector2 pointM) =>
            ((int)MathF.Floor(pointM.X / weldM), (int)MathF.Floor(pointM.Y / weldM));

        /// <summary>
        /// The graph as it stands: <b>a node nothing stands on is not a node</b>, so the places are numbered
        /// from what a way really meets rather than from every point the weld was ever asked about.
        /// </summary>
        public FootGraph Lay(float nearestCellM)
        {
            var nodeM = new List<Vector2>();
            var nodeOf = new int[_nodeM.Count];
            Array.Fill(nodeOf, -1);
            for (var edge = 0; edge < _edgeFrom.Count; edge++)
            {
                nodeOf[_edgeFrom[edge]] = 0;
                nodeOf[_edgeTo[edge]] = 0;
            }

            for (var node = 0; node < nodeOf.Length; node++)
            {
                if (nodeOf[node] < 0) continue;

                nodeOf[node] = nodeM.Count;
                nodeM.Add(_nodeM[node]);
            }

            var edgeFrom = new int[_edgeFrom.Count];
            var edgeTo = new int[_edgeFrom.Count];
            var edgeArcOffsets = new int[_edgeFrom.Count + 1];
            var edgeArcs = new List<ArcSeg>();
            for (var edge = 0; edge < _edgeFrom.Count; edge++)
            {
                edgeFrom[edge] = nodeOf[_edgeFrom[edge]];
                edgeTo[edge] = nodeOf[_edgeTo[edge]];
                foreach (var arc in _edgeArcs[edge]) edgeArcs.Add(arc);

                edgeArcOffsets[edge + 1] = edgeArcs.Count;
            }

            var outOffsets = new int[nodeM.Count + 1];
            foreach (var node in edgeFrom) outOffsets[node + 1]++;
            for (var node = 1; node < outOffsets.Length; node++) outOffsets[node] += outOffsets[node - 1];

            var cursor = (int[])outOffsets.Clone();
            var outEdges = new int[edgeFrom.Length];
            for (var edge = 0; edge < edgeFrom.Length; edge++) outEdges[cursor[edgeFrom[edge]]++] = edge;

            // <b>Read off the arrivals and not off the departures</b>: a lane is walked one way, so the ones
            // arriving at a node are not the ones leaving it turned round and a node's two degrees can differ.
            var inOffsets = new int[nodeM.Count + 1];
            foreach (var node in edgeTo) inOffsets[node + 1]++;
            for (var node = 1; node < inOffsets.Length; node++) inOffsets[node] += inOffsets[node - 1];

            cursor = (int[])inOffsets.Clone();
            var inEdges = new int[edgeTo.Length];
            for (var edge = 0; edge < edgeTo.Length; edge++) inEdges[cursor[edgeTo[edge]]++] = edge;

            return new FootGraph(
                [.. nodeM], edgeFrom, edgeTo, [.. _edgeLengthM], [.. _edgeBandM], [.. _edgeKind],
                edgeArcOffsets, [.. edgeArcs], outOffsets, outEdges, inOffsets, inEdges, nearestCellM);
        }
    }
}
