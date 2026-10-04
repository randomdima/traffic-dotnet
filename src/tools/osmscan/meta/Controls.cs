namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Every node that controls traffic or marks where it is crossed</b>: the survey's own tagged road nodes, and
/// those Overpass found off every road — a signal or sign drawn beside the carriageway — each snapped to the road
/// it stands beside where one is near enough.
/// </summary>
internal sealed class Controls
{
    /// <summary>The farthest a signal or sign drawn beside a road is snapped onto it: a pavement's width and a verge.</summary>
    public const double SnapM = 12;

    public Controls(Town town)
    {
        foreach (var (node, tags) in town.NodeTags)
        {
            All.Add(new ControlNode { Id = town.NodeId(node), Node = node, At = town.NodeM[node], Tags = tags });
        }

        if (town.Fetched.Control is not { } answer) return;

        foreach (var element in Element.Read(answer, 'n'))
        {
            if (town.NodeIndex.ContainsKey(element.Id)) continue;

            var at = town.Plane.At(element.Lat, element.Lon);
            var snapped = Regulates(element.Tags) ? town.NearestRoad(at, SnapM) : null;
            All.Add(new ControlNode
            {
                Id = element.Id,
                Node = null,
                At = at,
                Tags = element.Tags,
                Lat = element.Lat,
                Lon = element.Lon,
                SnappedRoad = snapped?.Road,
                SnappedAlongM = snapped?.AlongM ?? 0,
                SnappedOffM = snapped?.OffM ?? 0,
            });
        }
    }

    public List<ControlNode> All { get; } = [];

    /// <summary>Whether a node's tags put a car under a signal or sign.</summary>
    public static bool Regulates(Dictionary<string, string> tags) =>
        tags.GetValueOrDefault("highway") is "traffic_signals" or "stop" or "give_way" or "mini_roundabout";

    public static string? Signals(Dictionary<string, string> tags) =>
        tags.GetValueOrDefault("highway") == "traffic_signals" ? tags.GetValueOrDefault("traffic_signals") ?? "signal" : null;
}

/// <summary>One control node: its OSM id, its survey index where a road passes it, its place and tags, and the road it
/// was snapped to where it stands off every road — or a camera's sighting of one, tagged as OSM would tag it.</summary>
internal sealed class ControlNode
{
    /// <summary>Its OSM id; 0 for a sighting.</summary>
    public required long Id { get; init; }

    /// <summary>The sighting it is (<see cref="Sightings"/>), by its id; null for an OSM node.</summary>
    public string? Seen { get; init; }

    /// <summary>How evidence names it: <c>n&lt;id&gt;</c>, or the sighting's id.</summary>
    public string Label => Seen ?? $"n{Id}";

    /// <summary>The way along its road the traffic a sighting faces travels, read off its facing; null for an OSM node.</summary>
    public int? Toward { get; init; }

    /// <summary>Its index into the survey's nodes, or null for a node no road passes.</summary>
    public required int? Node { get; init; }

    public required Pt At { get; init; }

    public required Dictionary<string, string> Tags { get; init; }

    /// <summary>Its place in 1e-7°, for a node off the survey; a survey node's is the survey's.</summary>
    public int Lat { get; init; }

    public int Lon { get; init; }

    public int? SnappedRoad { get; init; }

    public double SnappedAlongM { get; init; }

    public double SnappedOffM { get; init; }

    public string? Tag(string key) => Tags.GetValueOrDefault(key);

    /// <summary>
    /// Which way along its road the node faces traffic, +1 along the way as drawn, −1 against it, 0 either: a
    /// sighting's as its facing reads, else its own <c>traffic_signals:direction</c> or <c>direction</c>, as forward
    /// or backward.
    /// </summary>
    public int Facing => Toward ?? (Tag("traffic_signals:direction") ?? Tag("direction")) switch
    {
        "forward" => +1,
        "backward" => -1,
        _ => 0,
    };
}
