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
    sealed class Builder(float weldM)
    {
        readonly List<Vector2> _nodeM = [];
        readonly Dictionary<(int X, int Y), List<int>> _welds = [];
        readonly List<int> _edgeFrom = [];
        readonly List<int> _edgeTo = [];
        readonly List<float> _edgeLengthM = [];
        readonly List<float> _edgeBandM = [];
        readonly List<FootEdgeKind> _edgeKind = [];
        // One array per edge while building, flattened once at the end: a stretch that a crossing splits
        // has its line replaced, and a flat store with offsets in it cannot be written to in place.
        readonly List<ArcSeg[]> _edgeArcs = [];

        public Vector2 PositionOf(int node) => _nodeM[node];

        public int AddStrand(ReadOnlySpan<ArcSeg> arcs, float bandM, FootEdgeKind kind) =>
            AddPair(NodeAt(arcs[0].StartM), NodeAt(arcs[^1].EndM), arcs, bandM, kind);

        public int AddArc(int fromNode, int toNode, ArcSeg arc, float bandM, FootEdgeKind kind) =>
            AddPair(fromNode, toNode, new ReadOnlySpan<ArcSeg>(in arc), bandM, kind);

        /// <summary>
        /// The stretch whose own line passes nearest a point, split there so the point becomes a node —
        /// which is what makes stepping off a kerb a split like any other, and what stops a crossing being
        /// spliced onto the end of a stretch it actually meets the middle of.
        /// </summary>
        public int SplitNearest(Vector2 pointM, float reachM)
        {
            var best = -1;
            var bestAlongM = 0f;
            var bestDistanceSq = reachM * reachM;

            for (var edge = 0; edge < _edgeFrom.Count; edge += 2)
            {
                var lengthM = _edgeLengthM[edge];

                // No station of a stretch is further from one of its own ends than the stretch is long,
                // so a stretch whose nearer end stands further off than that plus the reach cannot hold
                // the answer. It is the whole difference between a scan and a scan of the whole town.
                var fromEndM = (_nodeM[_edgeFrom[edge]] - pointM).Length();
                var toEndM = (_nodeM[_edgeTo[edge]] - pointM).Length();
                if (MathF.Min(fromEndM, toEndM) - lengthM > reachM) continue;

                var arcs = ArcsOf(edge);
                var alongM = Spline.ProjectM(arcs, pointM, lengthM * 0.5f, lengthM);
                var distanceSq = (Spline.SampleAt(arcs, alongM).PositionM - pointM).LengthSquared();
                if (distanceSq >= bestDistanceSq) continue;

                bestDistanceSq = distanceSq;
                bestAlongM = alongM;
                best = edge;
            }

            if (best < 0) return -1;
            if (bestAlongM <= weldM) return _edgeFrom[best];
            if (bestAlongM >= _edgeLengthM[best] - weldM) return _edgeTo[best];

            return SplitAt(best, bestAlongM);
        }

        /// <summary>
        /// <b>Joins the loose ends of the wrap that stand within <paramref name="acrossM"/> of one another</b>,
        /// nearest pair first, with the stretch of pavement that runs between them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What it closes is a notch and not a gap.</b> Two wrapping lines that hand over at a kerb
        /// fillet meet <em>tangentially</em>, and the two pieces they came off do not quite touch — a
        /// fillet's tangent point sits a millimetre off the kerb it was drawn to, a junction's disc stands a
        /// millimetre proud of the arm's band. So the envelope dips a few millimetres inside the offset over
        /// half a metre of itself, both lines are cut at that dip, and neither covers it: the pavement is
        /// interrupted by a notch of tarmac two centimetres deep, which is nothing a person walks round.
        /// </para>
        /// <para>
        /// <b>It cannot be answered by asking the tarmac more kindly.</b> A tolerance on the offset moves
        /// the cut by the square root of twice the radius times the tolerance — a metre of overshoot for a
        /// centimetre of grace — so the two lines then run <em>past</em> each other instead and the ends
        /// stand further apart than before. What is ill-conditioned is the crossing of two curves that graze;
        /// what is not is the distance between the two ends once they are cut.
        /// </para>
        /// </remarks>
        public void Stitch(float acrossM, float bandM)
        {
            var ends = new List<int>();
            var ways = new int[_nodeM.Count];
            for (var edge = 0; edge < _edgeFrom.Count; edge += 2)
            {
                ways[_edgeFrom[edge]]++;
                ways[_edgeTo[edge]]++;
            }

            for (var node = 0; node < ways.Length; node++)
            {
                if (ways[node] == 1) ends.Add(node);
            }

            var pairs = new List<(float M, int From, int To)>();
            for (var one = 0; one < ends.Count; one++)
            {
                for (var two = one + 1; two < ends.Count; two++)
                {
                    var apartM = (_nodeM[ends[one]] - _nodeM[ends[two]]).Length();
                    if (apartM <= acrossM) pairs.Add((apartM, ends[one], ends[two]));
                }
            }

            pairs.Sort((first, second) => first.M.CompareTo(second.M));

            // One join an end, nearest pair first: an end already carried on is no longer loose, and joined
            // to a second neighbour as well it is a place the walk can arrive at and leave by the same
            // stretch (<see cref="WalkingNetwork"/> has no corner to lay between a line and itself).
            var joined = new bool[_nodeM.Count];
            foreach (var (apartM, from, to) in pairs)
            {
                if (joined[from] || joined[to] || AlreadyRun(from, to)) continue;

                joined[from] = true;
                joined[to] = true;
                var lineM = _nodeM[to] - _nodeM[from];
                AddArc(
                    from, to, new ArcSeg(_nodeM[from], MathF.Atan2(lineM.Y, lineM.X), apartM, 0f), bandM,
                    FootEdgeKind.Pavement);
            }
        }

        /// <summary>Whether these two ends are already the two ends of one stretch, which is a loop and not a join.</summary>
        bool AlreadyRun(int from, int to)
        {
            for (var edge = 0; edge < _edgeFrom.Count; edge++)
            {
                if (_edgeFrom[edge] == from && _edgeTo[edge] == to) return true;
            }

            return false;
        }

        /// <summary>
        /// Drops the dead-end stubs nothing walks — under a stride long. Repeated until nothing is left to
        /// drop, because cutting one stub can leave the stretch behind it a stub in its turn.
        /// </summary>
        public FootGraph Prune(float stubM, float nearestCellM)
        {
            var alive = new bool[_edgeFrom.Count];
            Array.Fill(alive, true);

            bool cut;
            do
            {
                cut = false;
                var ways = new int[_nodeM.Count];
                for (var edge = 0; edge < alive.Length; edge += 2)
                {
                    if (!alive[edge]) continue;

                    ways[_edgeFrom[edge]]++;
                    ways[_edgeTo[edge]]++;
                }

                for (var edge = 0; edge < alive.Length; edge += 2)
                {
                    if (!alive[edge] || _edgeLengthM[edge] >= stubM) continue;
                    if (ways[_edgeFrom[edge]] != 1 && ways[_edgeTo[edge]] != 1) continue;

                    alive[edge] = false;
                    alive[edge + 1] = false;
                    cut = true;
                }
            }
            while (cut);

            return Lay(alive, nearestCellM);
        }

        ReadOnlySpan<ArcSeg> ArcsOf(int edge) => _edgeArcs[edge];

        /// <summary>
        /// Cuts a stretch in two at a place along it. The head keeps the pair's own index — everything
        /// already pointing at it goes on pointing at it — and the tail is a new pair.
        /// </summary>
        int SplitAt(int edge, float alongM)
        {
            var arcs = _edgeArcs[edge];
            var head = new ArcSeg[arcs.Length + 1];
            var tail = new ArcSeg[arcs.Length + 1];
            var headCount = Spline.SubChainInto(arcs, 0f, alongM, head);
            var tailCount = Spline.SubChainInto(arcs, alongM, _edgeLengthM[edge], tail);
            if (headCount == 0 || tailCount == 0) return _edgeFrom[edge];

            var to = _edgeTo[edge];
            var bandM = _edgeBandM[edge];
            var kind = _edgeKind[edge];
            var middle = NodeAt(head[headCount - 1].EndM);

            Rewrite(edge, _edgeFrom[edge], middle, head.AsSpan(0, headCount));
            AddPair(middle, to, tail.AsSpan(0, tailCount), bandM, kind);
            return middle;
        }

        void Rewrite(int edge, int fromNode, int toNode, ReadOnlySpan<ArcSeg> arcs)
        {
            var reversed = new ArcSeg[arcs.Length];
            Spline.ReverseInto(arcs, reversed);

            _edgeFrom[edge] = fromNode;
            _edgeTo[edge] = toNode;
            _edgeFrom[edge + 1] = toNode;
            _edgeTo[edge + 1] = fromNode;
            _edgeLengthM[edge] = Spline.TotalLengthM(arcs);
            _edgeLengthM[edge + 1] = _edgeLengthM[edge];
            _edgeArcs[edge] = arcs.ToArray();
            _edgeArcs[edge + 1] = reversed;
        }

        int AddPair(int fromNode, int toNode, ReadOnlySpan<ArcSeg> arcs, float bandM, FootEdgeKind kind)
        {
            var forward = _edgeFrom.Count;
            var lengthM = Spline.TotalLengthM(arcs);
            var reversed = new ArcSeg[arcs.Length];
            Spline.ReverseInto(arcs, reversed);

            Add(fromNode, toNode, arcs.ToArray(), lengthM, bandM, kind);
            Add(toNode, fromNode, reversed, lengthM, bandM, kind);
            return forward;
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

        FootGraph Lay(bool[] alive, float nearestCellM)
        {
            var edgeFrom = new List<int>();
            var edgeTo = new List<int>();
            var edgeLengthM = new List<float>();
            var edgeBandM = new List<float>();
            var edgeKind = new List<FootEdgeKind>();
            var edgeArcOffsets = new List<int> { 0 };
            var edgeArcs = new List<ArcSeg>();

            for (var edge = 0; edge < alive.Length; edge++)
            {
                if (!alive[edge]) continue;

                edgeFrom.Add(_edgeFrom[edge]);
                edgeTo.Add(_edgeTo[edge]);
                edgeLengthM.Add(_edgeLengthM[edge]);
                edgeBandM.Add(_edgeBandM[edge]);
                edgeKind.Add(_edgeKind[edge]);
                foreach (var arc in _edgeArcs[edge]) edgeArcs.Add(arc);
                edgeArcOffsets.Add(edgeArcs.Count);
            }

            var outOffsets = new int[_nodeM.Count + 1];
            foreach (var node in edgeFrom) outOffsets[node + 1]++;
            for (var node = 1; node < outOffsets.Length; node++) outOffsets[node] += outOffsets[node - 1];

            var cursor = (int[])outOffsets.Clone();
            var outEdges = new int[edgeFrom.Count];
            for (var edge = 0; edge < edgeFrom.Count; edge++) outEdges[cursor[edgeFrom[edge]]++] = edge;

            return new FootGraph(
                [.. _nodeM], [.. edgeFrom], [.. edgeTo], [.. edgeLengthM], [.. edgeBandM], [.. edgeKind],
                [.. edgeArcOffsets], [.. edgeArcs], outOffsets, outEdges, nearestCellM);
        }
    }
}

