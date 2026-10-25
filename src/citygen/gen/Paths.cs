using System.Numerics;
using TrafficSimulation.Core.Config;

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
    internal sealed record Path(Polyline Line, RoadClass Class, int Lanes, List<(float AlongM, int Node)> Nodes, List<(float FromM, float ToM, bool Bridge)> Closed)
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
    public int Add(Polyline line, RoadClass roadClass, int lanes, IEnumerable<Crossing> crossings)
    {
        var path = new Path(line, roadClass, lanes, [], []);
        var at = _paths.Count;
        _paths.Add(path);

        foreach (var crossing in crossings)
        {
            var (nearM, farM) = (line.Nearest(crossing.NearM).AlongM, line.Nearest(crossing.FarM).AlongM);
            var (fromM, toM) = nearM <= farM ? (nearM, farM) : (farM, nearM);
            path.Closed.Add((fromM, toM, crossing.Bridge));
            if (!crossing.Bridge) continue;

            Stand(at, fromM, line.At(fromM));
            Stand(at, toM, line.At(toM));        }

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

        foreach (var (fromM, toM, _) in at.Closed)
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

    /// <summary>Whether a node stands on an intercity road, which no district's own street may meet (GEN-68).</summary>
    public bool OnAnIntercity(int node)
    {
        foreach (var path in _paths)
        {
            if (path.Class == RoadClass.Intercity && path.Nodes.Exists(entry => entry.Node == node)) return true;
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
    /// path's own points between its two nodes, kept clear of both ends by their discs.
    /// </summary>
    public void Close()
    {
        foreach (var bridges in (ReadOnlySpan<bool>)[true, false])
        {
            foreach (var path in _paths)
            {
                path.Nodes.Sort((a, b) => a.AlongM.CompareTo(b.AlongM));                for (var at = 0; at + 1 < path.Nodes.Count; at++)
                {
                    var (fromM, from) = path.Nodes[at];
                    var (toM, to) = path.Nodes[at + 1];
                    if (from == to) continue;

                    var bridge = path.Closed.Exists(span => span.Bridge && MathF.Abs(span.FromM - fromM) < SameAlongM && MathF.Abs(span.ToM - toM) < SameAlongM);
                    if (bridge != bridges) continue;

                    if (bridge)
                    {
                        layout.Join(from, to, RoadClass.Bridge, lanes: path.Lanes);
                        continue;
                    }

                    var clearM = MathF.Max(layout.Sizes.StandoffM(layout.NodeM[from]), layout.Sizes.StandoffM(layout.NodeM[to])) * 2f
                                 + config.CityGen.LocalityM;
                    // A lead off a bridge the ground will not take leaves the road on the path's own line rather than unlaid.
                    if (layout.Join(from, to, path.Class, throughM: Through(path, fromM, toM, clearM), straight: true, lanes: path.Lanes) < 0)
                    {
                        layout.Join(from, to, path.Class, throughM: path.Line.Between(fromM, toM, clearM), straight: true, lanes: path.Lanes);
                    }
                }
            }
        }
    }

    /// <summary>
    /// The points a road between two of a path's nodes passes: the path's own, and first <b>a point on the line of a bridge
    /// either end lands</b>, so the road leaves the bridgehead on the span's line (GEN-14a) rather than for wherever the
    /// path turned after its approach.
    /// </summary>
    Vector2[] Through(Path path, float fromM, float toM, float clearM)
    {
        var through = new List<Vector2>(path.Line.Between(fromM, toM, clearM));
        if (Landing(path, fromM) is { } leaving) Lead(through, leaving, 0);
        if (Landing(path, toM) is { } arriving) Lead(through, arriving, through.Count);
        return [.. through];

        // At the end of the straight the path was routed off the bridge on (Crossing.NearApproachM), so on its own line.
        void Lead(List<Vector2> points, (Vector2 HeadM, Vector2 Outward) landing, int at)
        {
            var leadM = landing.HeadM + (landing.Outward * config.CityGen.BridgeApproachM);
            points.RemoveAll(pointM => Vector2.Distance(pointM, leadM) < config.CityGen.LocalityM);
            points.Insert(Math.Min(at, points.Count), leadM);
        }
    }

    /// <summary>Where a bridge on the path lands at a node standing this far along it, and which way off the span the land runs.</summary>
    (Vector2 HeadM, Vector2 Outward)? Landing(Path path, float alongM)
    {
        foreach (var (fromM, toM, bridge) in path.Closed)
        {
            if (!bridge) continue;

            var (headM, otherM) = MathF.Abs(toM - alongM) < SameAlongM ? (toM, fromM)
                : MathF.Abs(fromM - alongM) < SameAlongM ? (fromM, toM)
                : (float.NaN, float.NaN);
            if (float.IsNaN(headM)) continue;

            var head = NodeOn(path, headM);
            var other = NodeOn(path, otherM);
            if (head < 0 || other < 0) return null;

            return (NodeM(head), Vector2.Normalize(NodeM(head) - NodeM(other)));
        }

        return null;
    }

    static int NodeOn(Path path, float alongM)
    {
        foreach (var (nodeAlongM, node) in path.Nodes)
        {
            if (MathF.Abs(nodeAlongM - alongM) < SameAlongM) return node;
        }

        return -1;
    }

    /// <summary>
    /// <b>The ground a path keeps to itself</b>: its own carriageway and walk, and the corner a street turning off it
    /// needs — the ground a street's junction with it stands on, and where a district's own lattice stands no node.
    /// </summary>
    public float CorridorM(int path) => TownGen.CorridorM(config, _paths[path].Lanes);
}
