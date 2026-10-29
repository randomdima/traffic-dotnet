using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The roads that belong to the town rather than to a district</b> (GEN-63, GEN-67, GEN-69): the intercity roads, the
/// district roads along the edges districts share, and the links across a barrier — each a line laid before any street,
/// carrying the nodes the streets hang off it, and closed into roads last.
/// </summary>
/// <remarks>
/// <para>
/// <b>A node is a place on a path, and the roads are what is left between them once everything has been placed</b>
/// (GEN-10). A street hanging off a path puts a node on it (<see cref="NodeAt"/>); a path ending on another puts its end
/// node there; and each path is closed into roads only once nothing more can be put on it (<see cref="Close"/>).
/// </para>
/// <para>
/// <b>A road between two of a path's nodes is laid along the path</b>, passing the path's own points between them
/// (<see cref="LayoutEdge.ThroughM"/>), so the road is the line the path was laid as and the ground the town kept for it
/// rather than whatever two arms drawn at its ends would wander to.
/// </para>
/// <para>
/// <b>A crossing on a path is closed to every node but its own ends</b>: a bridge is a road of its own between its two
/// bridgeheads (GEN-14a), and a node on a level crossing would be a junction on the track.
/// </para>
/// </remarks>
internal sealed class Paths(TownLayout layout, SimConfig config)
{
    /// <summary>One path: its line, its class and lanes each way, the nodes on it, and the stretches of it no node may stand on.</summary>
    /// <param name="Closed">
    /// Each stretch no node stands on, and whether it is a span of its own between two nodes — a bridge, or an intercity
    /// road's span carried over a railway (<paramref name="Over"/> on the stretch) — rather than a level crossing.
    /// </param>
    /// <param name="Over">Whether the whole path is carried over what it crosses (<see cref="LayoutEdge.Over"/>).</param>
    /// <param name="OneWay">Whether it is driven only from its line's first point toward its last: a slip road.</param>
    internal sealed record Path(
        Polyline Line, RoadClass Class, int Lanes, List<(float AlongM, int Node)> Nodes,
        List<(float FromM, float ToM, bool Bridge, bool Over)> Closed, bool Over = false, bool OneWay = false)
    {
        public float CarriagewayM(float laneWidthM) => laneWidthM * Lanes * SimConfig.LanesPerCarriageway;
    }

    /// <summary>How near along a path two readings of one place stand: a node is filed at the distance its crossing was measured at.</summary>
    const float SameAlongM = 1f;

    readonly List<Path> _paths = [];

    /// <summary>The node standing at each place a path ends, so paths ending at one place share it.</summary>
    readonly Dictionary<Vector2, int> _endAt = [];

    public IReadOnlyList<Path> All => _paths;

    /// <summary>
    /// <b>One path laid</b>, with each crossing it takes closed to nodes — and a bridge's two ends stood as its
    /// bridgeheads — and a node at each of its ends. An end that cannot stand (on the water, or too near the railway) is
    /// none, and the path ends at the last node it carries.
    /// </summary>
    /// <param name="overRails">
    /// Whether each railway it crosses is crossed on a span carried over it rather than at a level crossing: an intercity
    /// road of a bypassed town, which nothing stops (GEN-68). A crossing that says so is carried over whatever it crosses
    /// (<see cref="Crossing.Over"/>).
    /// </param>
    /// <param name="over">Whether the whole path is carried over what it crosses (<see cref="Path.Over"/>).</param>
    /// <param name="oneWay">Whether it is driven only from its line's first point toward its last (<see cref="Path.OneWay"/>).</param>
    public int Add(
        Polyline line, RoadClass roadClass, int lanes, IEnumerable<Crossing> crossings, bool overRails = false, bool over = false,
        bool oneWay = false)
    {
        var path = new Path(line, roadClass, lanes, [], [], over, oneWay);
        var at = _paths.Count;
        _paths.Add(path);

        foreach (var crossing in crossings)
        {
            var (nearM, farM) = (line.Nearest(crossing.NearM).AlongM, line.Nearest(crossing.FarM).AlongM);
            var (fromM, toM) = nearM <= farM ? (nearM, farM) : (farM, nearM);
            var carried = crossing.Over || (overRails && !crossing.Bridge);
            path.Closed.Add((fromM, toM, crossing.Bridge || carried, carried));
            if (!crossing.Bridge && !carried) continue;

            Stand(at, fromM, line.At(fromM));
            Stand(at, toM, line.At(toM));
        }

        End(at, 0f, line.FirstM);
        End(at, line.LengthM, line.LastM);
        return at;
    }

    /// <summary>A node at one end of a path, shared with every other path ending at the very same place.</summary>
    void End(int path, float alongM, Vector2 atM)
    {
        if (!_endAt.TryGetValue(atM, out var node))
        {
            node = layout.AddNode(atM);
            if (node < 0) return;

            _endAt[atM] = node;
        }

        Widen(path, atM);
        _paths[path].Nodes.Add((alongM, node));
    }

    void Stand(int path, float alongM, Vector2 atM)
    {
        var node = layout.AddNode(atM);
        if (node < 0) return;

        _endAt[atM] = node;
        Widen(path, atM);
        _paths[path].Nodes.Add((alongM, node));
    }

    void Widen(int path, Vector2 atM) => layout.Sizes.Widen(atM, _paths[path].CarriagewayM(config.LaneWidthM));

    /// <summary>The node standing here stands its arms out at least this far (<see cref="NodeSizes.Reach"/>).</summary>
    public void Reach(Vector2 atM, float standoffM) => layout.Sizes.Reach(atM, standoffM);

    /// <summary>
    /// <b>A node on a path where something meets it</b>: the one already standing within <paramref name="weldM"/> along
    /// it, else a new one there — or none on a stretch closed to nodes, where the ground takes none, or within
    /// <paramref name="apartM"/> of a node already on it (GEN-68).
    /// </summary>
    public int NodeAt(int path, float alongM, float weldM, float apartM = 0f)
    {
        var at = _paths[path];
        foreach (var (nodeAlongM, node) in at.Nodes)
        {
            if (MathF.Abs(nodeAlongM - alongM) < weldM) return node;
        }

        foreach (var (nodeAlongM, _) in at.Nodes)
        {
            if (MathF.Abs(nodeAlongM - alongM) < apartM) return -1;
        }

        foreach (var (fromM, toM, _, _) in at.Closed)
        {
            if (alongM > fromM - weldM && alongM < toM + weldM) return -1;
        }

        var atM = at.Line.At(alongM);
        var placed = layout.AddNode(atM);
        if (placed < 0) return -1;

        // Filed as a place a path may end at, so a path laid to it later shares it rather than standing a second.
        _endAt[atM] = placed;
        Widen(path, atM);
        at.Nodes.Add((alongM, placed));
        return placed;
    }

    /// <summary>
    /// <b>A stretch of a path closed to nodes</b> where something stands beside it a node on it would be settled into
    /// (GEN-16): an interchange's terminal, or the end of a road carried over it (GEN-68).
    /// </summary>
    public void Keep(int path, float fromM, float toM) => _paths[path].Closed.Add((fromM, toM, false, false));

    /// <summary>
    /// Whether a stretch of a path lies wholly on it and clear of every stretch closed to nodes: the ground an interchange's
    /// slip roads leave it over (GEN-68).
    /// </summary>
    public bool Open(int path, float fromM, float toM)
    {
        var at = _paths[path];
        if (fromM <= 0f || toM >= at.Line.LengthM) return false;

        foreach (var (closedFromM, closedToM, _, _) in at.Closed)
        {
            if (closedFromM < toM && closedToM > fromM) return false;
        }

        return true;
    }

    /// <summary>Whether a node stands on an intercity road, which no district's own street may meet (GEN-68).</summary>
    public bool OnAnIntercity(int node)
    {
        foreach (var path in _paths)
        {
            if (path.Class == RoadClass.Intercity && path.Nodes.Exists(entry => entry.Node == node)) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a node is a bridgehead: an end of a span (GEN-14a), where the one road landing alone carries the span's line
    /// on and a street hung on it would be laid straight to somewhere else.
    /// </summary>
    public bool AtABridgehead(int node)
    {
        foreach (var path in _paths)
        {
            foreach (var (alongM, at) in path.Nodes)
            {
                if (at != node) continue;

                foreach (var (fromM, toM, bridge, _) in path.Closed)
                {
                    if (bridge && (alongM == fromM || alongM == toM)) return true;
                }
            }
        }

        return false;
    }

    /// <summary>Where a node stands, which a path laid to end on it ends at exactly.</summary>
    public Vector2 NodeM(int node) => layout.NodeM[node];

    /// <summary>The node at one end of a path, or −1 where that end could not stand.</summary>
    public int EndNode(int path, bool last)
    {
        var at = _paths[path];
        var endM = last ? at.Line.LengthM : 0f;
        foreach (var (alongM, node) in at.Nodes)
        {
            if (alongM == endM) return node;
        }

        return -1;
    }

    /// <summary>The node a path ends at within a reach of a place, or −1: where a road ending on a district's lattice stands in for its point.</summary>
    public int EndNear(int path, Vector2 atM, float withinM)
    {
        foreach (var last in (ReadOnlySpan<bool>)[false, true])
        {
            var end = EndNode(path, last);
            if (end >= 0 && Vector2.Distance(NodeM(end), atM) <= withinM) return end;
        }

        return -1;
    }

    /// <summary>A node standing elsewhere put on a path as well, where that path ends on it.</summary>
    public void Share(int path, float alongM, int node)
    {
        if (node < 0) return;

        Widen(path, layout.NodeM[node]);
        _paths[path].Nodes.Add((alongM, node));
    }

    /// <summary>Where the nodes went when they were settled (<see cref="TownLayout.SettleTheNodes"/>), so each path holds the node the town kept.</summary>
    public void TheNodesMoved(int[] moved)
    {
        foreach (var path in _paths)
        {
            for (var at = 0; at < path.Nodes.Count; at++) path.Nodes[at] = (path.Nodes[at].AlongM, moved[path.Nodes[at].Node]);
        }
    }

    /// <summary>
    /// <b>Every path closed into the roads between its nodes</b>, in order along it — <b>the bridges first</b>, so the road
    /// landing at each bridgehead finds the span it carries on and leaves on its line (GEN-14a) — each road passing the
    /// path's own points between its two nodes, kept clear of both ends by their discs. <b>The intercity roads and their
    /// interchanges before every other</b> (GEN-68): a road between districts arriving at a terminal on a bearing a slip road
    /// lies against is the one refused, and not the slip that half the interchange is left without.
    /// </summary>
    public void Close()
    {
        for (var path = 0; path < _paths.Count; path++) Spaced(path);

        var paths = new Dictionary<int, int>();
        foreach (var path in _paths)
        {
            foreach (var (_, node) in path.Nodes) paths[node] = paths.GetValueOrDefault(node) + 1;
        }

        foreach (var path in _paths)
        {
            path.Nodes.Sort((a, b) => a.AlongM.CompareTo(b.AlongM));
            for (var at = 0; at + 1 < path.Nodes.Count; at++)
            {
                if (SpanAt(path, at) is not { } span) continue;

                var (from, to) = (path.Nodes[at].Node, path.Nodes[at + 1].Node);

                // A bridgehead nothing else stands on is no junction (GEN-14a) — unless its span is refused, which leaves
                // it a place the path's own roads meet whatever they carry.
                var (fromJoint, toJoint) = (paths[from] == 1 && layout.Sizes.Joint(NodeM(from)), paths[to] == 1 && layout.Sizes.Joint(NodeM(to)));

                // A span over a railway is the road itself carried over.
                var curvature = SpanCurvature(path, at, span.Over ? path.Class : RoadClass.Bridge);
                var laid = span.Over
                    ? layout.Join(from, to, path.Class, curvature, straight: true, lanes: path.Lanes, over: true)
                    : layout.Join(from, to, RoadClass.Bridge, curvature, lanes: path.Lanes, unwalked: path.Class == RoadClass.Intercity);
                if (laid >= 0) continue;

                if (fromJoint) layout.Sizes.Unjoint(NodeM(from));
                if (toJoint) layout.Sizes.Unjoint(NodeM(to));
            }
        }

        foreach (var path in _paths.OrderBy(path => path.Class is RoadClass.Intercity or RoadClass.Slip || path.Over ? 0 : 1))
        {
            for (var at = 0; at + 1 < path.Nodes.Count;) at = ClosedFrom(path, at);
        }
    }

    /// <summary>
    /// <b>No two of a path's nodes further apart than a road is joined</b> (<see cref="CityGenFigures.JoinedRoadLongestM"/>):
    /// a road in across the country meets nothing for kilometres, and a lane is a whole road, which the ribbon atlas files
    /// only so long. A node is stood in each such gap at even spacing, moved along the path off a crossing's stretch.
    /// </summary>
    void Spaced(int path)
    {
        var at = _paths[path];
        var longestM = config.CityGen.JoinedRoadLongestM;
        var alongM = at.Nodes.Select(node => node.AlongM).Append(0f).Append(at.Line.LengthM).Order().ToList();
        for (var gap = 0; gap + 1 < alongM.Count; gap++)
        {
            var spanM = alongM[gap + 1] - alongM[gap];
            var cuts = (int)MathF.Ceiling(spanM / longestM) - 1;
            for (var cut = 1; cut <= cuts; cut++)
            {
                var placeM = alongM[gap] + (spanM * cut / (cuts + 1));
                for (var nudge = 0; nudge < SpacedNudges && NodeAt(path, placeM + (nudge * config.CityGen.LocalityM), SameAlongM) < 0; nudge++)
                {
                }
            }
        }
    }

    /// <summary>How many times a node spacing a path is moved on along it, a locality each, off ground that takes none.</summary>
    const int SpacedNudges = 8;

    /// <summary>
    /// <b>The road on from one of a path's nodes</b>, and the node it reaches: the next — or, where the ground refuses that
    /// road, the first after it the ground takes one to, so a node a street hung beside the path and the settling moved
    /// off its line is passed rather than the path broken there. Never past a bridge, which is a road of its own.
    /// </summary>
    int ClosedFrom(Path path, int at)
    {
        if (IsSpan(path, at)) return at + 1;

        var (fromM, from) = path.Nodes[at];
        for (var next = at + 1; next < path.Nodes.Count && !IsSpan(path, next - 1); next++)
        {
            var (toM, to) = path.Nodes[next];
            if (from == to || Laid(path, fromM, from, toM, to)) return next;
        }

        return at + 1;
    }

    /// <summary>
    /// <b>How a span between two of a path's nodes bends</b> (GEN-14a): the one arc through both that turns as the path
    /// turns between them, so the road runs on over it on the curve it was routed as — but never tighter than its class
    /// bends, which a span short across a corner of the path's line would ask, and straight where the path is to within
    /// the rounding (<see cref="RoadStage.CreaseRad"/>).
    /// </summary>
    float SpanCurvature(Path path, int at, RoadClass roadClass)
    {
        var (fromM, from) = path.Nodes[at];
        var (toM, to) = path.Nodes[at + 1];
        var chordM = Vector2.Distance(NodeM(from), NodeM(to));
        if (chordM <= 0f) return 0f;

        var (leaves, arrives) = (path.Line.Heading(fromM), path.Line.Heading(toM));
        var turnRad = Spline.WrapRad(Trig.Atan2(arrives.Y, arrives.X) - Trig.Atan2(leaves.Y, leaves.X));
        if (MathF.Abs(turnRad) <= RoadStage.CreaseRad(config)) return 0f;

        var mostCurvature = 1f / RoadLines.FloorRadiusM(config, roadClass);
        return Math.Clamp(2f * MathF.Sin(turnRad * 0.5f) / chordM, -mostCurvature, mostCurvature);
    }

    /// <summary>Whether the stretch of a path between two of its nodes is a span of its own.</summary>
    bool IsSpan(Path path, int at) => SpanAt(path, at) is not null;

    /// <summary>The span of its own the stretch of a path between two of its nodes is, or none.</summary>
    static (float FromM, float ToM, bool Bridge, bool Over)? SpanAt(Path path, int at)
    {
        var (fromM, toM) = (path.Nodes[at].AlongM, path.Nodes[at + 1].AlongM);
        foreach (var span in path.Closed)
        {
            if (span.Bridge && MathF.Abs(span.FromM - fromM) < SameAlongM && MathF.Abs(span.ToM - toM) < SameAlongM) return span;
        }

        return null;
    }

    /// <summary>One road laid along a path between two of its nodes, or false where the ground refuses it.</summary>
    bool Laid(Path path, float fromM, int from, float toM, int to)
    {
        var (fromStandoffM, toStandoffM) = (layout.Sizes.StandoffM(layout.NodeM[from]), layout.Sizes.StandoffM(layout.NodeM[to]));
        var clearM = (MathF.Max(fromStandoffM, toStandoffM) * 2f) + config.CityGen.LocalityM;

        // The nodes are in order along the path, so a one-way path is driven from the first of the two.
        var flow = path.OneWay ? RoadFlow.WithTheRoad : RoadFlow.BothWays;

        // A slip road's corner was placed by its interchange clear of each end's own box, and is its shape (GEN-68).
        if (path.Class == RoadClass.Slip)
        {
            var corners = path.Line.Between(fromM + fromStandoffM, toM - toStandoffM, 0f);
            return layout.Join(from, to, path.Class, throughM: corners, straight: true, lanes: path.Lanes, flow: flow) >= 0;
        }

        // A lead off a bridge the ground will not take leaves the road on the path's own line rather than unlaid.
        return layout.Join(from, to, path.Class, throughM: Through(path, fromM, from, toM, to, clearM), straight: true, lanes: path.Lanes, flow: flow, over: path.Over) >= 0
               || layout.Join(from, to, path.Class, throughM: path.Line.Between(fromM, toM, clearM), straight: true, lanes: path.Lanes, flow: flow, over: path.Over) >= 0;
    }

    /// <summary>
    /// The points a road between two of a path's nodes passes: the path's own, and first <b>a point on the line of a bridge
    /// either end lands</b>, so the road leaves the bridgehead on the span's line (GEN-14a) rather than for wherever the
    /// path turned after its approach — and <b>on an intercity road, a point on the path's own line through every junction
    /// on it</b> (GEN-63), so the two roads meeting there leave it on one bearing and it runs straight through the box.
    /// </summary>
    Vector2[] Through(Path path, float fromM, int from, float toM, int to, float clearM)
    {
        var through = new List<Vector2>(path.Line.Between(fromM, toM, clearM));
        var bridgeM = config.CityGen.BridgeApproachM;
        var junctionM = MathF.Min(bridgeM, (toM - fromM) * HalfAFreeRoad);
        if (Landing(path, fromM) is { } leaving) Lead(through, leaving, bridgeM, 0f, first: true);
        else if (path.Class == RoadClass.Intercity) Lead(through, (NodeM(from), path.Line.Heading(fromM)), junctionM, junctionM, first: true);
        if (Landing(path, toM) is { } arriving) Lead(through, arriving, bridgeM, 0f, first: false);
        else if (path.Class == RoadClass.Intercity) Lead(through, (NodeM(to), -path.Line.Heading(toM)), junctionM, junctionM, first: false);
        return [.. through];

        // At the end of a straight held off the node, on the path's own line: the path was routed off a bridge on one
        // (Crossing.NearApproachM), so what it passes there already stands on it; what it passes nearer a junction would
        // bend the road inside the straight, and is dropped.
        void Lead(List<Vector2> points, (Vector2 HeadM, Vector2 Outward) landing, float approachM, float clearOfM, bool first)
        {
            var leadM = landing.HeadM + (landing.Outward * approachM);
            points.RemoveAll(pointM =>
                Vector2.Distance(pointM, leadM) < config.CityGen.LocalityM || Vector2.Distance(pointM, landing.HeadM) < clearOfM);
            points.Insert(first ? 0 : points.Count, leadM);
        }
    }

    /// <summary>
    /// The most of a road between two nodes each end's straight takes, so the two never meet: a road shorter than two
    /// approaches holds each end's bearing for less than the whole of one.
    /// </summary>
    const float HalfAFreeRoad = 0.4f;

    /// <summary>
    /// Where a bridge on the path lands at a node standing this far along it, and which way off the span the land runs:
    /// on the span's own tangent there, carried on (<see cref="ConnectionPoints.CarriesOn"/>).
    /// </summary>
    (Vector2 HeadM, Vector2 Outward)? Landing(Path path, float alongM)
    {
        foreach (var (fromM, toM, bridge, over) in path.Closed)
        {
            if (!bridge) continue;

            var lastEnd = MathF.Abs(toM - alongM) < SameAlongM;
            if (!lastEnd && MathF.Abs(fromM - alongM) >= SameAlongM) continue;

            var at = NodeOn(path, fromM);
            if (at < 0 || at + 1 >= path.Nodes.Count || SpanAt(path, at) is null
                || NodeM(path.Nodes[at].Node) == NodeM(path.Nodes[at + 1].Node)) return null;

            var (head, other) = lastEnd ? (path.Nodes[at + 1].Node, path.Nodes[at].Node) : (path.Nodes[at].Node, path.Nodes[at + 1].Node);
            var curvature = SpanCurvature(path, at, over ? path.Class : RoadClass.Bridge);
            return (NodeM(head), -ConnectionPoints.Tangent(NodeM(head), NodeM(other), lastEnd ? -curvature : curvature));
        }

        return null;
    }

    static int NodeOn(Path path, float alongM)
    {
        for (var at = 0; at < path.Nodes.Count; at++)
        {
            if (MathF.Abs(path.Nodes[at].AlongM - alongM) < SameAlongM) return at;
        }

        return -1;
    }

    /// <summary>
    /// <b>The ground a path keeps to itself</b>: its own carriageway and walk, and the corner a street turning off it
    /// needs — the ground a street's junction with it stands on, and where a district's own lattice stands no node.
    /// </summary>
    public float CorridorM(int path) => TownGen.CorridorM(config, _paths[path].Lanes);
}
