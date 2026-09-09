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

        /// <summary>
        /// Which stretches were laid on the condition that they lead somewhere — the lines a box offers,
        /// which are pavement only where the arms left the shell open
        /// (<see cref="DropTheLinesThatLeadNowhere"/>).
        /// </summary>
        readonly List<bool> _edgeConditional = [];

        /// <summary>What is still in the graph — everything, until a drop takes something out of it.</summary>
        readonly List<bool> _edgeAlive = [];

        // One array per edge while building, flattened once at the end: a stretch that a crossing splits
        // has its line replaced, and a flat store with offsets in it cannot be written to in place.
        readonly List<ArcSeg[]> _edgeArcs = [];

        public Vector2 PositionOf(int node) => _nodeM[node];

        /// <summary>
        /// One line, laid between the nodes its two ends stand at. <b>A line whose two ends are one node is
        /// not laid</b>: a stretch running from a node to itself is ground no walk can be stationed along
        /// and a corner nothing can be turned on (<c>WalkingNetwork</c> has none to lay between a line and
        /// itself). Both ends welding onto one node that was already there is how a line a third of a metre
        /// long becomes a loop.
        /// </summary>
        public int AddStrand(
            ReadOnlySpan<ArcSeg> arcs, float bandM, FootEdgeKind kind, bool onlyIfItLeadsSomewhere = false)
        {
            var from = NodeAt(arcs[0].StartM);
            var to = NodeAt(arcs[^1].EndM);
            return from == to ? NoStretch : AddPair(from, to, arcs, bandM, kind, onlyIfItLeadsSomewhere);
        }

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
                if (!_edgeAlive[edge]) continue;

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
        /// fillet's tangent point sits a millimetre off the kerb it was drawn to, and a movement's band runs
        /// a millimetre proud of the arm's. So the envelope dips a few millimetres inside the offset over
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
        /// <para>
        /// <b>And where the two lines graze rather than cross, one of them runs on past the other's cut</b>
        /// and its end lands <em>on</em> the line it should have handed over to. Such an end is that line's
        /// node too: the line is split under it and the two weld, so what is left over is a stub of one of
        /// them and not a second line laid along the other (TER-3c.6). Joined end to end instead, the join
        /// itself is the doubled line.
        /// </para>
        /// </remarks>
        public void Stitch(float acrossM, float bandM)
        {
            var ends = new List<int>();
            var ways = Ways();
            for (var node = 0; node < ways.Length; node++)
            {
                if (ways[node] == 1) Land(node);
            }

            ways = Ways();
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

        /// <summary>
        /// <b>Splits the line a loose end stands on under that end</b>, so the two become one node. Nothing
        /// is added: the split's own node welds onto the end, and whatever the end's stretch ran on past the
        /// crossing is a stub for the prune to take.
        /// </summary>
        void Land(int node)
        {
            var pointM = _nodeM[node];
            var best = -1;
            var bestAlongM = 0f;
            var bestM = weldM;

            for (var edge = 0; edge < _edgeFrom.Count; edge += 2)
            {
                if (!_edgeAlive[edge] || _edgeFrom[edge] == node || _edgeTo[edge] == node) continue;

                var lengthM = _edgeLengthM[edge];
                var fromEndM = (_nodeM[_edgeFrom[edge]] - pointM).Length();
                var toEndM = (_nodeM[_edgeTo[edge]] - pointM).Length();
                if (MathF.Min(fromEndM, toEndM) - lengthM > bestM) continue;

                var arcs = ArcsOf(edge);
                var alongM = Spline.ProjectM(arcs, pointM, lengthM * 0.5f, lengthM);
                var offM = (Spline.SampleAt(arcs, alongM).PositionM - pointM).Length();
                if (offM >= bestM) continue;

                bestM = offM;
                bestAlongM = alongM;
                best = edge;
            }

            // Landing on an end of it is the weld's own business, and it has already had its say.
            if (best < 0 || bestAlongM <= weldM || bestAlongM >= _edgeLengthM[best] - weldM) return;

            SplitAt(best, bestAlongM);
        }

        /// <summary>
        /// Whether some other stretch already runs between these two ends — for a join, a loop rather than
        /// a join; for a line the shell only wanted where it led somewhere, somewhere the walk goes anyway.
        /// </summary>
        bool AlreadyRun(int from, int to, int besides = NoStretch)
        {
            for (var edge = 0; edge < _edgeFrom.Count; edge++)
            {
                if (edge == besides || edge == besides + 1 || !_edgeAlive[edge]) continue;
                if (_edgeFrom[edge] == from && _edgeTo[edge] == to) return true;
            }

            return false;
        }

        /// <summary>
        /// No stretch: what a line that was not laid answers with, and what a caller asking about a pair of
        /// ends alone leaves out of the question.
        /// </summary>
        const int NoStretch = -2;

        /// <summary>
        /// <b>Drops the lines that were laid on the condition that they lead somewhere and do not.</b> A
        /// line a box offers is the shell where it closes a gap the arms left open, which is to say where
        /// the pavement carries on at both of its ends; standing on one end or on neither, it is a second
        /// line up the middle of a pavement that is already laid.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>And a line between two places the walk already runs between leads nowhere either</b>, however
        /// well joined its ends are. A movement straight through a place where a one-way street is merely
        /// cut is drawn on the street's own centreline at the street's own width, so the line that wraps it
        /// is the street's line to the last bit of the float — two stretches over one piece of ground,
        /// between one pair of nodes, which is a way a walk can be sent down and a corner nothing can be
        /// laid between.
        /// </para>
        /// <para>
        /// Repeated, because two of them can hold each other up: a pair that meets in the middle of a
        /// pavement leads somewhere at one end apiece until the first is dropped.
        /// </para>
        /// </remarks>
        public void DropTheLinesThatLeadNowhere()
        {
            bool cut;
            do
            {
                cut = false;
                var ways = Ways();
                for (var edge = 0; edge < _edgeAlive.Count; edge += 2)
                {
                    if (!_edgeAlive[edge] || !_edgeConditional[edge]) continue;
                    if (ways[_edgeFrom[edge]] > 1 && ways[_edgeTo[edge]] > 1
                        && !AlreadyRun(_edgeFrom[edge], _edgeTo[edge], besides: edge))
                    {
                        continue;
                    }

                    _edgeAlive[edge] = false;
                    _edgeAlive[edge + 1] = false;
                    cut = true;
                }
            }
            while (cut);
        }

        /// <summary>
        /// <b>Drops a stretch that is another one said twice</b>: a way out of a node that sets off along
        /// another way out of that same node and never leaves it (<see cref="SaidTwice"/>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two pieces of tarmac that lie along one another offer wrapping lines that lie along one another,
        /// and where neither is the inside of anything there is nothing to condition them on
        /// (<see cref="DropTheLinesThatLeadNowhere"/>) — so the pavement was laid twice over one piece of
        /// ground. What that costs is not a wasted stretch but a turn: the two are laid in whatever
        /// directions their pieces ran, so a walk down one and back up the other is a corner the town lays
        /// and draws — a loop hanging off the middle of a footway, turning a body round to send it back the
        /// way it came.
        /// </para>
        /// <para>
        /// <b>Both halves of the question, because either alone is wrong.</b> A pavement that closes on
        /// itself — round the head of a dead end, round a car park — leaves one node by two ways that are
        /// not doubled ground at all, so a shared node is not enough; and two lines that never stand a
        /// body's width apart are the two sides of a street too narrow to have any, until it is one node
        /// they both set off from, so the measurement is not enough either.
        /// </para>
        /// <para>
        /// <b>Asked of a shared node rather than of a shared pair of ends</b>, because the two are cut by
        /// what each was laid off and rarely stop in the same place: the loop that put this here was 3.8 m
        /// of pavement lying inside 4.6 m of it, with the leftover 0.7 m closing the ring.
        /// </para>
        /// </remarks>
        public void DropThePavementSaidTwice(float apartM)
        {
            var ways = new List<int>?[_nodeM.Count];
            for (var edge = 0; edge < _edgeAlive.Count; edge += 2)
            {
                if (!_edgeAlive[edge]) continue;

                (ways[_edgeFrom[edge]] ??= []).Add(edge);
                (ways[_edgeTo[edge]] ??= []).Add(edge);
            }

            for (var node = 0; node < ways.Length; node++)
            {
                var here = ways[node];
                if (here is null) continue;

                foreach (var keep in here)
                {
                    foreach (var drop in here)
                    {
                        if (drop == keep || !_edgeAlive[keep] || !_edgeAlive[drop]) continue;
                        if (_edgeKind[drop] != _edgeKind[keep] || _edgeLengthM[drop] > _edgeLengthM[keep]) continue;
                        if (!SaidTwice(keep, drop, node, apartM)) continue;

                        _edgeAlive[drop] = false;
                        _edgeAlive[drop + 1] = false;
                    }
                }
            }
        }

        /// <summary>
        /// Whether one way out of a node is another way out of it said twice: every metre of the shorter
        /// stands within <paramref name="apartM"/> of the longer's line, <b>and it ends further down that
        /// line than it set off</b>.
        /// </summary>
        /// <remarks>
        /// The second half is what tells a line laid over another from one that carries on out of the same
        /// node the other way: two short pieces of one footway meeting end to end each lie within a body's
        /// width of the other, since a body's width is longer than either of them, and only the direction
        /// they leave by says which is which. Walked at the same figure it is measured by, so nothing
        /// between two stations is missed.
        /// </remarks>
        bool SaidTwice(int keep, int drop, int node, float apartM)
        {
            var along = Leaving(keep, node);
            var alongLengthM = _edgeLengthM[keep];
            var doubled = Leaving(drop, node);
            var doubledLengthM = _edgeLengthM[drop];
            var stations = Math.Max(1, (int)MathF.Ceiling(doubledLengthM / apartM));

            var alongM = 0f;
            for (var station = 1; station <= stations; station++)
            {
                var atM = Spline.SampleAt(doubled, doubledLengthM * station / stations).PositionM;
                alongM = Spline.ProjectM(along, atM, alongLengthM * 0.5f, alongLengthM);
                if ((Spline.SampleAt(along, alongM).PositionM - atM).Length() > apartM) return false;
            }

            return alongM > 0f;
        }

        /// <summary>
        /// Drops the dead-end stubs nothing walks — under a stride long. Repeated until nothing is left to
        /// drop, because cutting one stub can leave the stretch behind it a stub in its turn.
        /// </summary>
        public void Prune(float stubM)
        {
            bool cut;
            do
            {
                cut = false;
                var ways = Ways();
                for (var edge = 0; edge < _edgeAlive.Count; edge += 2)
                {
                    if (!_edgeAlive[edge] || _edgeLengthM[edge] >= stubM) continue;
                    if (ways[_edgeFrom[edge]] != 1 && ways[_edgeTo[edge]] != 1) continue;

                    _edgeAlive[edge] = false;
                    _edgeAlive[edge + 1] = false;
                    cut = true;
                }
            }
            while (cut);
        }

        /// <summary>
        /// <b>Runs two stretches into one wherever the node between them forks nothing and the two are one
        /// line</b>, so that a node is a place a walk chooses between and never a seam in the construction.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The pavement is cut into pieces by what it is laid off — a piece per band, per fillet and per
        /// box, cut again wherever the one rule opens and closes (<see cref="Wrap"/>) — and most of those
        /// cuts fall in the middle of a footway nothing joins. Such a node stands for nothing: the walking
        /// side lays no corner at it (<c>WalkingNetwork.SamePlaceAt</c>) and gives up no ground for one, so
        /// what it costs is a node, a pair of edges, four turns nobody chooses between and a stretch's worth
        /// of every table laid per stretch.
        /// </para>
        /// <para>
        /// <b>Only where the joint is inside the rounding, and that is the point of the figures.</b> A
        /// chain walked as one line lies about where its own metres are by whatever its joints are open by
        /// (<c>Kerbs.Runs</c>), and a kink of one line closes at the centre opens by the kink times the
        /// offset on the lane laid outside it — so what may be run together is a joint that moves neither
        /// the line nor either lane further than offsetting the chain moved it anyway. Everything else is a
        /// corner the pavement really turns or a step the weld really left, and it keeps its node and its
        /// mitre.
        /// </para>
        /// <para>
        /// Repeated, because running two stretches together can leave the node at the far end of the second
        /// one forking nothing in its turn.
        /// </para>
        /// </remarks>
        public void RunOn(float sameM, float straightRad)
        {
            bool ran;
            do
            {
                ran = false;
                var ways = new int[_nodeM.Count];
                var first = new int[_nodeM.Count];
                var second = new int[_nodeM.Count];
                for (var edge = 0; edge < _edgeAlive.Count; edge += 2)
                {
                    if (!_edgeAlive[edge]) continue;

                    Stand(_edgeFrom[edge], edge, ways, first, second);
                    Stand(_edgeTo[edge], edge, ways, first, second);
                }

                for (var node = 0; node < ways.Length; node++)
                {
                    if (ways[node] != 2 || first[node] == second[node]) continue;
                    if (!OneLine(first[node], second[node], node, sameM, straightRad, out var openM)) continue;

                    // <b>Repeated only while something is really run together</b> (<see cref="Run"/>): a
                    // footway that closes on itself — an island's — comes down to two stretches between two
                    // nodes that are one line at both of them and that nothing may run together, so a sweep
                    // that counted the attempt never stopped sweeping.
                    ran |= Run(first[node], second[node], node, openM);
                }
            }
            while (ran);
        }

        static void Stand(int node, int edge, int[] ways, int[] first, int[] second)
        {
            if (ways[node] == 0) first[node] = edge;
            else if (ways[node] == 1) second[node] = edge;

            ways[node]++;
        }

        /// <summary>
        /// Whether the two stretches meeting at a node are one line there: the same ground, the same width,
        /// and a joint inside the figures. <b>Read off the pair as it stands now</b>, since an earlier join
        /// in the same sweep may have taken one of them or carried it away from this node.
        /// </summary>
        /// <remarks>
        /// <b>The joint is read in the line's own frame, and the two directions answer to different
        /// figures.</b> Across the line nothing may move: a step sideways is a step whatever it is called,
        /// and the grace is the rounding. Along the line the weld has already had its say — two ends it
        /// welded onto one node are one place, and where two grazing pieces of the shell overrun each other
        /// the leftover is the overlap and not a corner. So <paramref name="openM"/> comes back as what the
        /// joint is open by along itself, for <see cref="Run"/> to shut.
        /// </remarks>
        bool OneLine(int into, int onward, int node, float sameM, float straightRad, out float openM)
        {
            openM = 0f;
            if (!_edgeAlive[into] || !_edgeAlive[onward]) return false;
            if (!At(into, node) || !At(onward, node)) return false;
            if (_edgeKind[into] != _edgeKind[onward] || _edgeBandM[into] != _edgeBandM[onward]) return false;

            var last = Arriving(into, node)[^1];
            var starts = Leaving(onward, node)[0];
            var headingRad = last.HeadingAtRad(last.LengthM);
            if (MathF.Abs(Spline.WrapRad(starts.HeadingRad - headingRad)) > straightRad) return false;

            Heading.Frame(headingRad, out var ahead, out var right);
            var opening = starts.StartM - last.EndM;
            openM = Vector2.Dot(opening, ahead);
            return MathF.Abs(Vector2.Dot(opening, right)) <= sameM && MathF.Abs(openM) <= weldM;
        }

        bool At(int edge, int node) => _edgeFrom[edge] == node || _edgeTo[edge] == node;

        /// <summary>The stretch's arcs read the way they arrive at a node, and the way they leave it.</summary>
        ReadOnlySpan<ArcSeg> Arriving(int edge, int node) => _edgeArcs[_edgeTo[edge] == node ? edge : edge + 1];

        ReadOnlySpan<ArcSeg> Leaving(int edge, int node) => _edgeArcs[_edgeFrom[edge] == node ? edge : edge + 1];

        /// <summary>
        /// Lays the two as one stretch, <b>shut at the joint</b>. The first keeps the pair's own index and
        /// takes both sets of arcs; the second is dropped, and the node between them is left for
        /// <see cref="Lay"/> to leave out. <b>False where the two are one line and still may not be run
        /// together</b>, which is what tells <see cref="RunOn"/> that its sweep made no progress.
        /// </summary>
        bool Run(int into, int onward, int node, float openM)
        {
            var fromNode = _edgeTo[into] == node ? _edgeFrom[into] : _edgeTo[into];
            var toNode = _edgeFrom[onward] == node ? _edgeTo[onward] : _edgeFrom[onward];

            // Run together, a pavement that closes on itself is a stretch from a node to itself: ground no
            // walk can be stationed along, exactly as it is when a line is laid (<see cref="AddStrand"/>).
            if (fromNode == toNode) return false;

            var arriving = Arriving(into, node);
            var leaving = Leaving(onward, node);
            var arcs = new ArcSeg[arriving.Length + leaving.Length];
            var shut = Shut(arriving, openM, arcs);
            if (shut == 0) return false;

            leaving.CopyTo(arcs.AsSpan(shut));

            Rewrite(into, fromNode, toNode, arcs.AsSpan(0, shut + leaving.Length));
            _edgeAlive[onward] = false;
            _edgeAlive[onward + 1] = false;
            return true;
        }

        /// <summary>
        /// The arriving chain with its far end carried along itself to where the next one begins — run on
        /// past it where the joint was open, cut back where the two overran each other.
        /// </summary>
        /// <remarks>
        /// A gap is closed on the last piece, which carries the line's own curvature into it; an overlap is
        /// taken off by <see cref="Spline.SubChainInto"/>, since it can be longer than the piece that
        /// carries it. Left in, the joint is a step of up to the weld inside a line that claims to be one.
        /// </remarks>
        static int Shut(ReadOnlySpan<ArcSeg> arcs, float openM, Span<ArcSeg> into)
        {
            if (openM < 0f) return Spline.SubChainInto(arcs, 0f, Spline.TotalLengthM(arcs) + openM, into);

            arcs.CopyTo(into);
            into[arcs.Length - 1] = arcs[^1] with { LengthM = arcs[^1].LengthM + openM };
            return arcs.Length;
        }

        /// <summary>How many stretches still in the graph each node stands on.</summary>
        int[] Ways()
        {
            var ways = new int[_nodeM.Count];
            for (var edge = 0; edge < _edgeAlive.Count; edge += 2)
            {
                if (!_edgeAlive[edge]) continue;

                ways[_edgeFrom[edge]]++;
                ways[_edgeTo[edge]]++;
            }

            return ways;
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
            AddPair(middle, to, tail.AsSpan(0, tailCount), bandM, kind, _edgeConditional[edge]);
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

        int AddPair(
            int fromNode, int toNode, ReadOnlySpan<ArcSeg> arcs, float bandM, FootEdgeKind kind,
            bool conditional = false)
        {
            var forward = _edgeFrom.Count;
            var lengthM = Spline.TotalLengthM(arcs);
            var reversed = new ArcSeg[arcs.Length];
            Spline.ReverseInto(arcs, reversed);

            Add(fromNode, toNode, arcs.ToArray(), lengthM, bandM, kind, conditional);
            Add(toNode, fromNode, reversed, lengthM, bandM, kind, conditional);
            return forward;
        }

        void Add(
            int fromNode, int toNode, ArcSeg[] arcs, float lengthM, float bandM, FootEdgeKind kind,
            bool conditional)
        {
            _edgeFrom.Add(fromNode);
            _edgeTo.Add(toNode);
            _edgeLengthM.Add(lengthM);
            _edgeBandM.Add(bandM);
            _edgeKind.Add(kind);
            _edgeConditional.Add(conditional);
            _edgeAlive.Add(true);
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
        /// The graph as it stands, with what a drop or a join left out of it: <b>a node nothing stands on
        /// is not a node</b>, so the places are numbered from what is still there rather than from what was
        /// laid at some point.
        /// </summary>
        public FootGraph Lay(float nearestCellM)
        {
            var nodeM = new List<Vector2>();
            var nodeOf = new int[_nodeM.Count];
            Array.Fill(nodeOf, -1);
            for (var edge = 0; edge < _edgeAlive.Count; edge++)
            {
                if (!_edgeAlive[edge]) continue;

                nodeOf[_edgeFrom[edge]] = 0;
                nodeOf[_edgeTo[edge]] = 0;
            }

            for (var node = 0; node < nodeOf.Length; node++)
            {
                if (nodeOf[node] < 0) continue;

                nodeOf[node] = nodeM.Count;
                nodeM.Add(_nodeM[node]);
            }

            var edgeFrom = new List<int>();
            var edgeTo = new List<int>();
            var edgeLengthM = new List<float>();
            var edgeBandM = new List<float>();
            var edgeKind = new List<FootEdgeKind>();
            var edgeArcOffsets = new List<int> { 0 };
            var edgeArcs = new List<ArcSeg>();

            for (var edge = 0; edge < _edgeAlive.Count; edge++)
            {
                if (!_edgeAlive[edge]) continue;

                edgeFrom.Add(nodeOf[_edgeFrom[edge]]);
                edgeTo.Add(nodeOf[_edgeTo[edge]]);
                edgeLengthM.Add(_edgeLengthM[edge]);
                edgeBandM.Add(_edgeBandM[edge]);
                edgeKind.Add(_edgeKind[edge]);
                foreach (var arc in _edgeArcs[edge]) edgeArcs.Add(arc);
                edgeArcOffsets.Add(edgeArcs.Count);
            }

            var outOffsets = new int[nodeM.Count + 1];
            foreach (var node in edgeFrom) outOffsets[node + 1]++;
            for (var node = 1; node < outOffsets.Length; node++) outOffsets[node] += outOffsets[node - 1];

            var cursor = (int[])outOffsets.Clone();
            var outEdges = new int[edgeFrom.Count];
            for (var edge = 0; edge < edgeFrom.Count; edge++) outEdges[cursor[edgeFrom[edge]]++] = edge;

            return new FootGraph(
                [.. nodeM], [.. edgeFrom], [.. edgeTo], [.. edgeLengthM], [.. edgeBandM], [.. edgeKind],
                [.. edgeArcOffsets], [.. edgeArcs], outOffsets, outEdges, nearestCellM);
        }
    }
}

