using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Tools.OsmScan.Meta;
using Vector2 = System.Numerics.Vector2;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>A traced map's railway laid off OSM's main lines</b> (RAIL-2, TRN-1), every other edit made to it kept: the tracks the
/// enrichment holds (<c>towns/traced/&lt;Map&gt;/tracks.json</c>) — a main line's own, no yard, siding, spur or crossover —
/// joined where OSM joins them, and every route through the map from one place it leaves the map to another, run on past
/// both far enough for a whole train to stand off it. Run by <c>qq osm --rails</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A route is a track the trains run</b>: one is kept where most of it is not already another's, and routes that share
/// any of OSM's track are one line, run one train at a time (TRN-5). A route is run both ways.
/// </para>
/// <para>
/// <b>A bridge is its own level</b>: a chord of a way OSM draws as a bridge stands above whatever it passes over, and makes
/// no level crossing with it (<see cref="TownMap.RailArrays.Level"/>).
/// </para>
/// </remarks>
internal static class Rails
{
    /// <summary>The kinds of line a train is run on: a main line, a branch, and a line for freight alone.</summary>
    static readonly HashSet<string> Usages = ["main", "branch", "freight"];

    /// <summary>How much of a route has to be its own for it to be kept beside the routes before it.</summary>
    const double OwnShareLeast = 0.5;

    /// <summary>How far apart two places a route leaves the map at have to be for it to be a route through it, and not a turn back.</summary>
    const double ThroughLeastM = 1500;

    /// <param name="runOffM">How far past the map's edge a track runs on (<c>RailFigures.RunOffM</c>, which <c>qq osm</c> reads off the engine).</param>
    public static int Run(string root, string map, float runOffM)
    {
        var path = Scan.MapFile(root, map);
        var town = TownMap.Read(path);
        var tracks = Layers.Read<TrackRecord>(Path.Combine(root, "towns", "traced", map), "tracks")?.Items
                     ?? throw new InvalidDataException($"{map}: no tracks layer; run `qq osm --meta` first.");
        var plane = new Plane(town.Frame);
        var sizeM = new Pt(town.Frame.WidthM, town.Frame.HeightM);

        // Every main line's chords, node to node, and which nodes each node runs on to.
        var placeOf = new Dictionary<long, Pt>();
        var edges = new List<(long From, long To, double LengthM, bool Above)>();
        var next = new Dictionary<long, List<int>>();
        foreach (var track in tracks)
        {
            if (track.Railway != "rail" || track.Tags.ContainsKey("service") || !Usages.Contains(track.Tags.GetValueOrDefault("usage") ?? "")) continue;

            var line = plane.Line(track.Line);
            if (line.Length != track.Nodes.Length) continue;

            var above = track.Tags.GetValueOrDefault("bridge") is { } bridge && bridge != "no";
            for (var point = 0; point < line.Length; point++) placeOf[track.Nodes[point]] = line[point];
            for (var point = 0; point + 1 < line.Length; point++)
            {
                var (from, to) = (track.Nodes[point], track.Nodes[point + 1]);
                if (from == to) continue;

                edges.Add((from, to, (line[point + 1] - line[point]).Length, above));
                Add(next, from, edges.Count - 1);
                Add(next, to, edges.Count - 1);
            }
        }

        // Where the lines leave the map: a chord with one end on it and the other off it, its node off the map.
        var exits = new List<long>();
        foreach (var (from, to, _, _) in edges)
        {
            var (fromOn, toOn) = (OnMap(placeOf[from], sizeM), OnMap(placeOf[to], sizeM));
            if (fromOn != toOn) exits.Add(fromOn ? to : from);
        }

        exits = [.. exits.Distinct()];
        Console.WriteLine($"  {map}: {edges.Count} chords of main line, {exits.Count} places a line leaves the map");

        var candidates = new List<List<int>>();
        foreach (var start in exits)
        {
            var (cameBy, distanceM) = Paths(start, edges, next);
            foreach (var end in exits)
            {
                if (end == start || !cameBy.ContainsKey(end) || (placeOf[end] - placeOf[start]).Length < ThroughLeastM) continue;

                var route = Route(end, cameBy, edges);
                if (route.Any(edge => OnMap(placeOf[edges[edge].From], sizeM))) candidates.Add(route);
            }
        }

        // The longest first, each kept only where most of it is no route's already kept.
        var taken = new HashSet<int>();
        var kept = new List<List<int>>();
        foreach (var route in candidates.OrderByDescending(route => route.Sum(edge => edges[edge].LengthM)))
        {
            var ownM = route.Where(edge => !taken.Contains(edge)).Sum(edge => edges[edge].LengthM);
            if (ownM < OwnShareLeast * route.Sum(edge => edges[edge].LengthM)) continue;

            kept.Add(route);
            taken.UnionWith(route);
        }

        Console.WriteLine($"  {candidates.Count} routes through the map between them, {kept.Count} kept as mostly their own");

        // Routes sharing a chord are one line (TRN-5).
        var lineOf = Enumerable.Range(0, kept.Count).ToArray();
        for (var one = 0; one < kept.Count; one++)
        {
            for (var other = one + 1; other < kept.Count; other++)
            {
                if (kept[one].Intersect(kept[other]).Any()) Join(lineOf, one, other);
            }
        }

        var lineIds = kept.Select((_, route) => Find(lineOf, route)).Distinct().Select((root, id) => (root, id)).ToDictionary(p => p.root, p => p.id);
        var (offsets, points, levels, lines) = (new List<int> { 0 }, new List<Vector2>(), new List<byte>(), new List<int>());
        for (var route = 0; route < kept.Count; route++)
        {
            var nodes = Nodes(kept[route], edges, out var aboveFrom);
            var (first, last) = RunOff(nodes, placeOf, sizeM, runOffM);
            for (var at = first; at <= last; at++)
            {
                points.Add(new Vector2((float)placeOf[nodes[at]].X, (float)placeOf[nodes[at]].Y));
                levels.Add(at < last && aboveFrom[at] ? TownMap.RailArrays.Above : TownMap.RailArrays.AtGrade);
            }

            offsets.Add(points.Count);
            lines.Add(lineIds[Find(lineOf, route)]);
            Console.WriteLine($"    track {route}: line {lines[^1]}, {last - first + 1} points, {Length(nodes, placeOf, first, last) / 1000:F1} km");
        }

        var railed = town with
        {
            Rails = new TownMap.RailArrays
            {
                Line = [.. lines], Runs = [.. lines.Select(_ => TownMap.TrackRuns.Both)], PointOffsets = [.. offsets], PointM = [.. points],
                Level = [.. levels],
            },
        };
        railed.Write(path);
        Console.WriteLine($"  {map}: {kept.Count} tracks on {lineIds.Count} lines, into {path}");
        return 0;
    }

    static void Add(Dictionary<long, List<int>> next, long node, int edge)
    {
        if (!next.TryGetValue(node, out var list)) next[node] = list = [];
        list.Add(edge);
    }

    static bool OnMap(Pt at, Pt sizeM) => at.X >= 0 && at.Y >= 0 && at.X <= sizeM.X && at.Y <= sizeM.Y;

    /// <summary>The shortest way along the lines from one node to every node, as the chord each was reached by.</summary>
    static (Dictionary<long, int> CameBy, Dictionary<long, double> DistanceM) Paths(
        long start, List<(long From, long To, double LengthM, bool Above)> edges, Dictionary<long, List<int>> next)
    {
        var (cameBy, distanceM) = (new Dictionary<long, int> { [start] = -1 }, new Dictionary<long, double> { [start] = 0 });
        var queue = new PriorityQueue<long, double>();
        queue.Enqueue(start, 0);
        while (queue.TryDequeue(out var node, out var atM))
        {
            if (atM > distanceM[node]) continue;

            foreach (var edge in next[node])
            {
                var (from, to, lengthM, _) = edges[edge];
                var other = from == node ? to : from;
                if (distanceM.TryGetValue(other, out var knownM) && knownM <= atM + lengthM) continue;

                (distanceM[other], cameBy[other]) = (atM + lengthM, edge);
                queue.Enqueue(other, atM + lengthM);
            }
        }

        return (cameBy, distanceM);
    }

    /// <summary>The chords from the start the paths were taken from to a node, in order.</summary>
    static List<int> Route(long end, Dictionary<long, int> cameBy, List<(long From, long To, double LengthM, bool Above)> edges)
    {
        var route = new List<int>();
        for (var node = end; cameBy[node] >= 0;)
        {
            var edge = cameBy[node];
            route.Add(edge);
            node = edges[edge].From == node ? edges[edge].To : edges[edge].From;
        }

        route.Reverse();
        return route;
    }

    /// <summary>A route's nodes in order, and whether the chord from each stands on a bridge.</summary>
    static List<long> Nodes(List<int> route, List<(long From, long To, double LengthM, bool Above)> edges, out List<bool> aboveFrom)
    {
        var first = edges[route[0]];
        var second = route.Count > 1 ? edges[route[1]] : first;
        var start = first.From == second.From || first.From == second.To ? first.To : first.From;
        var (nodes, above) = (new List<long> { start }, new List<bool>());
        foreach (var edge in route)
        {
            var (from, to, _, onABridge) = edges[edge];
            nodes.Add(nodes[^1] == from ? to : from);
            above.Add(onABridge);
        }

        aboveFrom = above;
        return nodes;
    }

    /// <summary>
    /// <b>How much of a route is kept</b>: from the run-off short of where it first comes onto the map to the run-off past
    /// where it last leaves it, or to its own ends where it stops sooner.
    /// </summary>
    static (int First, int Last) RunOff(List<long> nodes, Dictionary<long, Pt> placeOf, Pt sizeM, float runOffM)
    {
        var (firstOn, lastOn) = (nodes.FindIndex(node => OnMap(placeOf[node], sizeM)), nodes.FindLastIndex(node => OnMap(placeOf[node], sizeM)));
        var first = firstOn;
        for (var outM = 0.0; first > 0 && outM < runOffM; first--) outM += (placeOf[nodes[first]] - placeOf[nodes[first - 1]]).Length;

        var last = lastOn;
        for (var outM = 0.0; last < nodes.Count - 1 && outM < runOffM; last++) outM += (placeOf[nodes[last + 1]] - placeOf[nodes[last]]).Length;

        return (first, last);
    }

    static double Length(List<long> nodes, Dictionary<long, Pt> placeOf, int first, int last)
    {
        var lengthM = 0.0;
        for (var at = first; at < last; at++) lengthM += (placeOf[nodes[at + 1]] - placeOf[nodes[at]]).Length;
        return lengthM;
    }

    static int Find(int[] root, int at)
    {
        while (root[at] != at) at = root[at] = root[root[at]];
        return at;
    }

    static void Join(int[] root, int one, int other) => root[Find(root, one)] = Find(root, other);
}
