namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Every place a road is crossed on foot or by rail</b>: the crossing node on the road, the crossing way a mapper
/// drew across it where there is one, what kind it is — signals, zebra, painted, unmarked — where along its road it
/// stands and how far from the nearest junction.
/// </summary>
/// <remarks>
/// <para>
/// <b>A crossing is where its way meets the road</b>: OSM joins a <c>footway=crossing</c> way to the road at a
/// <c>highway=crossing</c> node, which is the crossing's place. A crossing way that crosses a road without a node
/// there is placed where the two lines cross and flagged; a crossing node with no way is a crossing all the same.
/// </para>
/// <para>
/// <b>Its kind is read off the node, else the way</b>, in every tagging OSM has had: <c>crossing=traffic_signals</c>,
/// <c>crossing:markings=zebra</c>, the older <c>crossing=zebra</c> and <c>crossing_ref=zebra</c>, iD's
/// <c>crossing=marked</c> and <c>uncontrolled</c> (painted, how not said), <c>unmarked</c>, <c>informal</c> and
/// <c>no</c>. A painted crossing in Ukraine is a zebra (marking 1.14.1), so <c>marked</c> is one in all but name.
/// </para>
/// </remarks>
internal static class Crossings
{
    /// <summary>The farthest from a crossing its nearest junction is looked for along its road.</summary>
    const double JunctionReachM = 120;

    /// <summary>The nearest a crossing node off every road stands to one and is still flagged as meant for it.</summary>
    const double BesideM = 3;

    public static List<CrossingRecord> Lay(Town town, Controls controls, List<ControlNode> seen, WalkFound walk, Flags flags, string into, List<Written> written)
    {
        var waysByNode = new Dictionary<long, List<(Element Way, Pt[] Line)>>();
        foreach (var (way, line) in walk.CrossingWays)
        {
            foreach (var id in way.Nodes) (waysByNode.TryGetValue(id, out var list) ? list : waysByNode[id] = []).Add((way, line));
        }

        var joined = new HashSet<long>();
        var records = new List<CrossingRecord>();
        foreach (var (node, tags) in town.NodeTags)
        {
            var type = Type(tags);
            if (type is null || town.Uses[node] is not { } uses) continue;

            var (road, at) = uses.MaxBy(use => Junctions.Rank(town.Highway(use.Road)));
            var id = town.NodeId(node);
            var ways = waysByNode.GetValueOrDefault(id) ?? [];
            foreach (var (way, _) in ways) joined.Add(way.Id);

            var crossingWay = ways.Count > 0 ? ways[0].Way : null;
            var (kind, painted, conflict) = type == "pedestrian" ? Read(tags, crossingWay?.Tags) : (type, null, null);
            if (conflict is not null)
            {
                flags.Raise("crossing_node_and_way_disagree", town, town.NodeM[node], $"{conflict}; the node's is taken", $"n{id}", $"w{crossingWay!.Id}");
            }

            var (junction, junctionM) = NearestJunction(town, node);
            records.Add(new CrossingRecord
            {
                Node = id,
                At = [town.Extract.Nodes.Lat[node], town.Extract.Nodes.Lon[node]],
                Type = type,
                Kind = kind,
                Painted = painted,
                Source = crossingWay is null ? "node" : "node+way",
                Road = town.Roads[road].Id,
                Highway = town.Highway(road),
                Name = town.Roads[road].Tag("name"),
                AlongM = Math.Round(town.AlongM(road, at), 1),
                CarriagewayM = Math.Round(town.Roads[road].Carriageway!.WidthM, 1),
                Lanes = town.Roads[road].Carriageway!.Lanes.Length,
                Junction = junction is { } j ? town.NodeId(j) : null,
                JunctionM = junctionM is { } m ? Math.Round(m, 1) : null,
                AtJunction = junctionM is <= Junctions.CrossingReachM,
                Way = crossingWay?.Id,
                LengthM = ways.Count > 0 ? Math.Round(Shape.Length(ways[0].Line), 1) : null,
                Line = crossingWay?.Geometry,
                Kerbs = crossingWay is null ? null : Kerbs(crossingWay, walk),
                Island = tags.GetValueOrDefault("crossing:island") == "yes" || crossingWay?.Tag("crossing:island") == "yes" ? true : null,
                Raised = tags.GetValueOrDefault("traffic_calming") is "table" or "hump" || crossingWay?.Tag("traffic_calming") == "table" ? true : null,
                Tags = tags,
            });
        }

        foreach (var (way, line) in walk.CrossingWays)
        {
            if (joined.Contains(way.Id)) continue;

            var met = false;
            for (var segment = 0; segment + 1 < line.Length; segment++)
            {
                var near = new HashSet<int>();
                town.RoadGrid.Near(Box.Empty.With(line[segment]).With(line[segment + 1]), near);
                foreach (var candidate in near)
                {
                    var (road, first) = town.Segments[candidate];
                    var (a, b) = (town.RoadLineM[road][first], town.RoadLineM[road][first + 1]);
                    if (!Shape.Crosses(line[segment], line[segment + 1], a, b, out var at, out _, out var along)) continue;

                    met = true;
                    var alongM = town.AlongM(road, first) + (along * (b - a).Length);
                    var (junction, junctionM) = NearestJunction(town, town.Roads[road].Nodes[along < 0.5 ? first : first + 1]);
                    records.Add(new CrossingRecord
                    {
                        Node = null,
                        At = town.Plane.Degrees(at),
                        Type = "pedestrian",
                        Kind = Kind(way.Tags),
                        Painted = Painted(Kind(way.Tags), way.Tag("crossing:markings")),
                        Source = "way_inferred",
                        Road = town.Roads[road].Id,
                        Highway = town.Highway(road),
                        Name = town.Roads[road].Tag("name"),
                        AlongM = Math.Round(alongM, 1),
                        CarriagewayM = Math.Round(town.Roads[road].Carriageway!.WidthM, 1),
                        Lanes = town.Roads[road].Carriageway!.Lanes.Length,
                        Junction = junction is { } j ? town.NodeId(j) : null,
                        JunctionM = junctionM is { } m ? Math.Round(m, 1) : null,
                        AtJunction = junctionM is <= Junctions.CrossingReachM,
                        Way = way.Id,
                        LengthM = Math.Round(Shape.Length(line), 1),
                        Line = way.Geometry,
                        Kerbs = Kerbs(way, walk),
                        Tags = way.Tags,
                    });
                    flags.Raise("crossing_way_not_joined", town, at, $"a crossing way crosses {town.Highway(road)} w{town.Roads[road].Id} with no node there", $"w{way.Id}", $"w{town.Roads[road].Id}");
                }
            }

            if (!met) flags.Raise("crossing_way_over_no_road", town, Shape.Along(line, Shape.Length(line) / 2), "a crossing way meeting no road", $"w{way.Id}");
        }

        // A zebra or pedestrians' light a camera saw is a crossing where OSM maps none near it, and none seen is already one.
        var mapped = new Sightings.Near();
        foreach (var record in records) mapped.Add(town.Plane.At(record.At[0], record.At[1]), record.Kind);
        foreach (var control in seen.Where(control => control.Tag("highway") == "crossing" && control.SnappedRoad is not null))
        {
            if (mapped.Nearest(control.At, Sightings.CrossingReachM) is not null) continue;

            var road = control.SnappedRoad!.Value;
            var placed = town.NearestRoad(control.At, control.SnappedOffM + 1, candidate => candidate == road)!.Value;
            var line = town.RoadLineM[road];
            var along = (placed.AlongM - town.AlongM(road, placed.Segment)) / Math.Max(1e-6, (line[placed.Segment + 1] - line[placed.Segment]).Length);
            var (junction, junctionM) = NearestJunction(town, town.Roads[road].Nodes[along < 0.5 ? placed.Segment : placed.Segment + 1]);
            var kind = Kind(control.Tags);
            records.Add(new CrossingRecord
            {
                Node = null,
                Seen = control.Seen,
                At = [control.Lat, control.Lon],
                Type = "pedestrian",
                Kind = kind,
                Painted = Painted(kind, control.Tag("crossing:markings")),
                Source = "seen",
                Road = town.Roads[road].Id,
                Highway = town.Highway(road),
                Name = town.Roads[road].Tag("name"),
                AlongM = Math.Round(placed.AlongM, 1),
                CarriagewayM = Math.Round(town.Roads[road].Carriageway!.WidthM, 1),
                Lanes = town.Roads[road].Carriageway!.Lanes.Length,
                Junction = junction is { } j ? town.NodeId(j) : null,
                JunctionM = junctionM is { } m ? Math.Round(m, 1) : null,
                AtJunction = junctionM is <= Junctions.CrossingReachM,
                Tags = control.Tags,
            });
            mapped.Add(control.At, kind);
        }

        foreach (var control in controls.All)
        {
            if (control.Node is not null || control.Tag("highway") != "crossing") continue;

            if (town.NearestRoad(control.At, BesideM) is { } near)
            {
                flags.Raise("crossing_node_off_road", town, control.At, $"a crossing node {near.OffM:F1} m off w{town.Roads[near.Road].Id} and not on it", $"n{control.Id}", $"w{town.Roads[near.Road].Id}");
            }
        }

        written.Add(Layers.Write(into, "crossings",
            "Every place a road is crossed on foot or by rail: its node, the crossing way drawn across where there is one — or the camera's sighting of it where OSM maps none — its kind, the road it crosses and where along it, and the nearest junction.",
            ["survey", "osm-walk", "osm-control", Mapillary.Name], records,
            new
            {
                types = records.GroupBy(record => record.Type).ToDictionary(g => g.Key, g => g.Count()),
                pedestrianKinds = records.Where(record => record.Type == "pedestrian").GroupBy(record => record.Kind).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                painted = records.Where(record => record.Type == "pedestrian").GroupBy(record => record.Painted switch { true => "yes", false => "no", null => "unsaid" })
                    .ToDictionary(g => g.Key, g => g.Count()),
                sources = records.GroupBy(record => record.Source).ToDictionary(g => g.Key, g => g.Count()),
                atJunction = records.Count(record => record.AtJunction),
                midBlock = records.Count(record => !record.AtJunction),
            }));
        return records;
    }

    /// <summary><c>pedestrian</c>, <c>rail</c> or <c>tram</c> for a node a road is crossed at, null for any other.</summary>
    static string? Type(Dictionary<string, string> tags)
    {
        switch (tags.GetValueOrDefault("railway"))
        {
            case "level_crossing":
                return "rail";
            case "tram_level_crossing":
                return "tram";
        }

        var highway = tags.GetValueOrDefault("highway");
        return highway == "crossing" || (highway == "traffic_signals" && tags.ContainsKey("crossing")) ? "pedestrian" : null;
    }

    /// <summary>
    /// What a crossing is, by every tagging OSM has had for it: <c>signals</c>, <c>zebra</c>, <c>marked</c> (painted,
    /// how not said), <c>unmarked</c>, <c>informal</c>, <c>no</c>, or <c>unknown</c> where a crossing says nothing more.
    /// </summary>
    public static string Kind(Dictionary<string, string> tags)
    {
        var crossing = tags.GetValueOrDefault("crossing");
        var markings = tags.GetValueOrDefault("crossing:markings");
        if (crossing == "traffic_signals" || tags.GetValueOrDefault("crossing:signals") == "yes" || tags.GetValueOrDefault("highway") == "traffic_signals") return "signals";
        if (crossing == "no") return "no";
        if (crossing == "informal") return "informal";
        if (markings is not null && markings.StartsWith("zebra", StringComparison.Ordinal)) return "zebra";
        if (crossing == "zebra" || tags.GetValueOrDefault("crossing_ref") == "zebra") return "zebra";
        if (crossing == "unmarked" || markings == "no") return "unmarked";
        if (crossing is "marked" or "uncontrolled" || (markings is not null && markings != "no")) return "marked";
        return "unknown";
    }

    /// <summary>
    /// A crossing's kind and paint off its node and the way drawn across it, which OSM tags apart and which can say
    /// different things: the node's kind where it says one, else the way's; painted as the node says, else as the way
    /// does. A signalled crossing whose way says it is painted is a painted signalled crossing, and a zebra a painted
    /// crossing named, so neither is a disagreement; any other two kinds are, and the node's is taken.
    /// </summary>
    public static (string Kind, bool? Painted, string? Conflict) Read(Dictionary<string, string> node, Dictionary<string, string>? way)
    {
        var onNode = Kind(node);
        var onWay = way is null ? "unknown" : Kind(way);
        var painted = Painted(onNode, node.GetValueOrDefault("crossing:markings"));
        if (way is not null) painted ??= Painted(onWay, way.GetValueOrDefault("crossing:markings"));

        var agree = onNode == "unknown" || onWay == "unknown" || onNode == onWay || (Paint(onNode) || onNode == "signals") && (Paint(onWay) || onWay == "signals");
        return (onNode != "unknown" ? onNode : onWay, painted, agree ? null : $"its node says {onNode}, its way {onWay}");

        static bool Paint(string kind) => kind is "zebra" or "marked";
    }

    /// <summary>
    /// Whether a crossing is painted on the road: a zebra or a painted one is, an unmarked one is not, a signalled
    /// one is where its <c>crossing:markings</c> says, and anything else is not known.
    /// </summary>
    static bool? Painted(string kind, string? markings) => kind switch
    {
        "zebra" or "marked" => true,
        "unmarked" or "no" or "informal" => false,
        _ => markings is null ? null : markings != "no",
    };

    /// <summary>The kerb at each end of a crossing way, where its end node says: <c>lowered</c>, <c>raised</c>, <c>flush</c>.</summary>
    static string?[]? Kerbs(Element way, WalkFound walk)
    {
        if (way.Nodes.Length < 2) return null;

        string?[] kerbs = [walk.NodeTags.GetValueOrDefault(way.Nodes[0])?.GetValueOrDefault("kerb"), walk.NodeTags.GetValueOrDefault(way.Nodes[^1])?.GetValueOrDefault("kerb")];
        return kerbs.Any(kerb => kerb is not null) ? kerbs : null;
    }

    /// <summary>The junction nearest a node along its roads, and how far; the node itself where it is one.</summary>
    static (int? Node, double? DistanceM) NearestJunction(Town town, int node)
    {
        if (town.IsJunction(node)) return (node, 0);

        (int? Node, double? DistanceM) best = (null, null);
        foreach (var arm in town.Arms(node))
        {
            foreach (var (met, atM, _) in arm.Walk(town, JunctionReachM))
            {
                if (!town.IsJunction(met)) continue;
                if (best.DistanceM is null || atM < best.DistanceM) best = (met, atM);
                break;
            }
        }

        return best;
    }
}

internal sealed class CrossingRecord
{
    /// <summary>The crossing node's OSM id; null where it was placed where its way crosses a road, or seen.</summary>
    public long? Node { get; init; }

    /// <summary>The camera's sighting it was laid from (<see cref="SeenRecord"/>), where OSM maps no crossing near it.</summary>
    public string? Seen { get; init; }

    /// <summary>Lat, lon in 1e-7°.</summary>
    public required int[] At { get; init; }

    /// <summary><c>pedestrian</c>, <c>rail</c> (a road over a railway) or <c>tram</c> (a road over tram track).</summary>
    public required string Type { get; init; }

    /// <summary>For a pedestrian crossing <c>signals</c>, <c>zebra</c>, <c>marked</c>, <c>unmarked</c>, <c>informal</c>,
    /// <c>no</c> or <c>unknown</c>; else its type.</summary>
    public required string Kind { get; init; }

    /// <summary>Whether it is painted on the road — a zebra in Ukraine (marking 1.14.1) — where its tags say.</summary>
    public bool? Painted { get; init; }

    /// <summary><c>node</c>, <c>node+way</c>, <c>way_inferred</c> where a crossing way crosses a road with no node, or <c>seen</c>.</summary>
    public required string Source { get; init; }

    public required long Road { get; init; }

    public required string Highway { get; init; }

    public string? Name { get; init; }

    /// <summary>How far along its road from the way's first node.</summary>
    public required double AlongM { get; init; }

    /// <summary>The carriageway's width as OSM's lanes make it.</summary>
    public required double CarriagewayM { get; init; }

    public required int Lanes { get; init; }

    public long? Junction { get; init; }

    public double? JunctionM { get; init; }

    /// <summary>Whether it is on a junction's arm, within <see cref="Junctions.CrossingReachM"/> of it.</summary>
    public required bool AtJunction { get; init; }

    /// <summary>The crossing way drawn across the road, by its OSM id.</summary>
    public long? Way { get; init; }

    public double? LengthM { get; init; }

    /// <summary>The crossing way's line, lat, lon pairs in 1e-7°.</summary>
    public int[]? Line { get; init; }

    /// <summary>The kerb at the crossing way's first and last node.</summary>
    public string?[]? Kerbs { get; init; }

    public bool? Island { get; init; }

    public bool? Raised { get; init; }

    public required Dictionary<string, string> Tags { get; init; }
}
