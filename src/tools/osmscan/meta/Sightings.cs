using System.Globalization;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Every sign, light and marking a camera saw over the map</b> — Mapillary's own detections (<see cref="Mapillary"/>)
/// and the ones Osmose relays of them that OSM lacks (item 8300) — each once: what it is, where, which way it faces,
/// when it was seen, the road it stands by, and what it was laid as.
/// </summary>
/// <remarks>
/// <para>
/// <b>OSM decides first.</b> A light, a stop or a give-way sign decides a junction's control only where OSM maps no
/// signal, sign, roundabout or priority road holding it (<see cref="Junctions"/>); a zebra or a pedestrians' light
/// becomes a crossing only where OSM maps none near it (<see cref="Crossings"/>). Everything else is weighed against
/// OSM and kept beside it.
/// </para>
/// <para>
/// <b>A sign faces the traffic it holds</b>, so that traffic heads the opposite way to the sign's face; a sighting
/// whose facing is known holds only the junction ahead of it. Osmose relays no facing, so its sighting holds the
/// nearer junction either way. One Osmose relays where Mapillary's own sighting of the same kind stands is that
/// sighting, read once.
/// </para>
/// </remarks>
internal static class Sightings
{
    /// <summary>How near two sightings of one kind stand to be one thing seen twice.</summary>
    const double SameM = 15;

    /// <summary>How far off a road a light or sign still stands by it: a mast over the kerb, placed to a few metres.</summary>
    const double SnapM = 15;

    /// <summary>How far from a junction a sighting is still that junction's.</summary>
    const double JunctionReachM = 30;

    /// <summary>How far a sighting may stand from what OSM maps for it — a signal, a crossing, a sign — and still be it.</summary>
    const double SignalReachM = 35, SignReachM = 30;

    public const double CrossingReachM = 15;

    /// <summary>How near the traffic's heading and a road's must be for a sign to face traffic along it; past the complement, against it.</summary>
    const double AlongDeg = 60;

    const string Osmose8300 = "Observed on ";

    public static List<Seen> Read(Town town, List<OsmoseIssue> osmose)
    {
        var read = new List<Seen>();
        if (town.Fetched.MapillaryAt is { } kept)
        {
            foreach (var sighting in Mapillary.Read(kept))
            {
                var at = town.Plane.At(sighting.Lat, sighting.Lon);
                if (!town.Plane.OnMap(at)) continue;

                var kind = Mapillary.Kind(sighting.Value, out var limitKmh);
                read.Add(new Seen($"mly{sighting.Id}", "mapillary", sighting.Value, kind, limitKmh, sighting.Lat, sighting.Lon, at, sighting.FacingDeg, sighting.First, sighting.Last));
            }
        }

        var own = read.GroupBy(seen => seen.Kind).ToDictionary(group => group.Key, group =>
        {
            var near = new Near();
            foreach (var seen in group) near.Add(seen.At, seen.Id);
            return near;
        });
        foreach (var issue in osmose.Where(issue => issue.Item == 8300))
        {
            var at = town.Plane.At(issue.Lat, issue.Lon);
            var (kind, limitKmh) = Kind(issue);
            if (!town.Plane.OnMap(at) || (own.TryGetValue(kind, out var mine) && mine.Nearest(at, SameM) is not null)) continue;

            var day = issue.Subtitle is { } said && said.StartsWith(Osmose8300, StringComparison.Ordinal) ? said[Osmose8300.Length..] : null;
            read.Add(new Seen($"osmose:{issue.Uuid}", "osmose", issue.Title, kind, limitKmh, issue.Lat, issue.Lon, at, null, day, day));
        }

        return read;
    }

    /// <summary>What a sign Osmose relays is, by the tags it would add, else by its title.</summary>
    static (string Kind, int? LimitKmh) Kind(OsmoseIssue issue)
    {
        if (issue.Proposed.GetValueOrDefault("maxspeed") is { } speed)
        {
            return ("speed_limit", int.TryParse(speed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) ? limit : null);
        }

        if (issue.Proposed.GetValueOrDefault("highway") is "traffic_signals") return ("traffic_light", null);
        if (issue.Proposed.GetValueOrDefault("highway") is "living_street") return ("living_street", null);

        var title = issue.Title.ToLowerInvariant();
        (string Said, string Kind)[] kinds =
            [("road bump", "bump"), ("one-directional", "one_way"), ("roundabout", "roundabout"), ("max height", "max_height"), ("max weight", "max_weight"), ("stop", "stop"), ("give way", "give_way")];
        foreach (var (said, kind) in kinds)
        {
            if (title.Contains(said, StringComparison.Ordinal)) return (kind, null);
        }

        return ("sign", null);
    }

    /// <summary>
    /// Every sighting that holds cars at a junction or marks a crossing, as a control node snapped to the road it stands
    /// by, tagged as OSM would tag it and facing the traffic it holds where its facing is known.
    /// </summary>
    public static List<ControlNode> Controls(Town town, List<Seen> seen)
    {
        var controls = new List<ControlNode>();
        foreach (var sighting in seen)
        {
            if (Tags(sighting.Kind) is not { } tags || town.NearestRoad(sighting.At, SnapM) is not { } road) continue;

            controls.Add(new ControlNode
            {
                Id = 0,
                Seen = sighting.Id,
                Node = null,
                At = sighting.At,
                Tags = tags,
                Lat = sighting.Lat,
                Lon = sighting.Lon,
                SnappedRoad = road.Road,
                SnappedAlongM = road.AlongM,
                SnappedOffM = road.OffM,
                Toward = Toward(town, road.Road, road.Segment, sighting.FacingDeg),
            });
        }

        return controls;

        static Dictionary<string, string>? Tags(string kind) => kind switch
        {
            "traffic_light" => new() { ["highway"] = "traffic_signals" },
            "pedestrian_light" => new() { ["highway"] = "crossing", ["crossing"] = "traffic_signals" },
            "zebra" => new() { ["highway"] = "crossing", ["crossing"] = "marked", ["crossing:markings"] = "zebra" },
            "stop" => new() { ["highway"] = "stop" },
            "give_way" => new() { ["highway"] = "give_way" },
            _ => null,
        };
    }

    /// <summary>
    /// Which way along its road the traffic a sighting faces travels — +1 along the way as drawn, −1 against it, 0
    /// where its facing is unknown or square to the road.
    /// </summary>
    static int Toward(Town town, int road, int segment, double? facingDeg)
    {
        if (facingDeg is not { } facing) return 0;

        var line = town.RoadLineM[road];
        var heading = (facing + 180) % 360;
        var turn = Math.Abs(((heading - Shape.BearingDeg(line[segment], line[segment + 1]) + 540) % 360) - 180);
        return turn <= AlongDeg ? +1 : turn >= 180 - AlongDeg ? -1 : 0;
    }

    /// <summary>
    /// Every sighting laid on the road it stands by and the junction it stands at, with what OSM maps near it for the
    /// same thing and what it was laid as; a light, a zebra, a stop or give-way sign OSM maps nothing for near it, and a
    /// speed sign its road is tagged otherwise, flagged.
    /// </summary>
    public static void Lay(
        Town town, List<Seen> seen, Controls controls, List<CrossingRecord> crossings, JunctionsFound junctions, Flags flags, string into, List<Written> written)
    {
        string[] readOff = [Mapillary.Name, "osmose", "survey", "osm-control", "osm-walk"];
        const string about = "Every traffic sign, traffic light and road marking a camera saw over the map — Mapillary's own detections and those Osmose relays — what it is, where, which way it faces, when it was seen, the road and junction it stands by, what OSM maps near it for the same thing, and the junction or crossing it was laid as where OSM maps none.";

        var signals = new Near();
        var signs = new Near();
        foreach (var node in controls.All)
        {
            if (node.Tag("highway") == "traffic_signals" || node.Tag("crossing") == "traffic_signals") signals.Add(node.At, "signals");
            if (node.Tag("highway") is { } sign && sign is "stop" or "give_way") signs.Add(node.At, sign);
        }

        var painted = new Near();
        foreach (var crossing in crossings.Where(crossing => crossing.Seen is null)) painted.Add(town.Plane.At(crossing.At[0], crossing.At[1]), crossing.Kind);

        var near = new Near();
        for (var node = 0; node < town.NodeM.Length; node++)
        {
            if (town.IsJunction(node)) near.Add(town.NodeM[node], town.NodeId(node).ToString(CultureInfo.InvariantCulture));
        }

        var heldBy = junctions.ByNode.Values.SelectMany(junction => (junction.Held ?? []).Where(held => held.Seen is not null).Select(held => (held.Seen!, junction.Node)))
            .ToDictionary(pair => pair.Item1, pair => pair.Node);
        var laidCrossings = crossings.Where(crossing => crossing.Seen is not null).Select(crossing => crossing.Seen!).ToHashSet();

        var records = new List<SeenRecord>(seen.Count);
        foreach (var sighting in seen)
        {
            var road = town.NearestRoad(sighting.At, SnapM);
            var way = road is { } beside ? town.Roads[beside.Road] : null;

            // Osmose relays only what OSM lacks, so its sightings disagree with OSM by being relayed, and are flagged as Osmose's own.
            var (osm, agrees) = sighting.Source == "osmose" ? (null, null) : Weigh(sighting.Kind, sighting.At, way, sighting.LimitKmh);
            records.Add(new SeenRecord
            {
                Id = sighting.Id,
                Source = sighting.Source,
                Value = sighting.Value,
                Kind = sighting.Kind,
                LimitKmh = sighting.LimitKmh,
                At = [sighting.Lat, sighting.Lon],
                FacingDeg = sighting.FacingDeg,
                FirstSeen = sighting.First,
                LastSeen = sighting.Last,
                Road = way?.Id,
                Junction = near.Nearest(sighting.At, JunctionReachM) is { } junction ? long.Parse(junction.What, CultureInfo.InvariantCulture) : null,
                Osm = osm,
                Agrees = agrees,
                HeldBy = heldBy.TryGetValue(sighting.Id, out var holder) ? holder : null,
                Crossing = laidCrossings.Contains(sighting.Id) ? true : null,
            });

            if (agrees != false) continue;

            var said = sighting.Kind switch
            {
                "speed_limit" => $"a {sighting.LimitKmh} km/h sign seen, its road tagged {osm}",
                "zebra" => $"a zebra seen, no OSM crossing within {CrossingReachM:F0} m",
                "stop" or "give_way" => osm is null
                    ? $"a {sighting.Kind.Replace('_', ' ')} sign seen, no OSM stop or give-way within {SignReachM:F0} m"
                    : $"a {sighting.Kind.Replace('_', ' ')} sign seen, OSM maps {osm.Replace('_', ' ')}",
                _ => $"a traffic light seen, no OSM signal within {SignalReachM:F0} m",
            };
            flags.Raise($"seen_{sighting.Kind}_disagrees", sighting.Lat, sighting.Lon, $"{said} (seen {sighting.Last ?? "undated"}, {sighting.Source})",
                way is null ? [sighting.Id] : [sighting.Id, $"w{way.Id}"]);
        }

        written.Add(Layers.Write(into, "seen", about, readOff, records,
            new
            {
                bySource = records.GroupBy(record => record.Source).ToDictionary(g => g.Key, g => g.Count()),
                byKind = records.GroupBy(record => record.Kind).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                agreeing = records.Where(record => record.Agrees is not null).GroupBy(record => record.Kind)
                    .ToDictionary(g => g.Key, g => $"{g.Count(record => record.Agrees == true)}/{g.Count()}"),
                heldByJunctions = records.Count(record => record.HeldBy is not null),
                laidAsCrossings = records.Count(record => record.Crossing == true),
                lastSeenByYear = records.Where(record => record.LastSeen is { Length: >= 4 }).GroupBy(record => record.LastSeen![..4]).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
            }));

        // What OSM maps near a sighting for the same thing, and whether it is that thing; null for a kind not weighed.
        (string? Osm, bool? Agrees) Weigh(string kind, Pt at, CityGen.Traced.OsmWay? way, int? limitKmh)
        {
            switch (kind)
            {
                case "traffic_light" or "pedestrian_light":
                    return signals.Nearest(at, SignalReachM) is { } signal ? (signal.What, true) : (null, false);
                case "zebra":
                    return painted.Nearest(at, CrossingReachM) is { } crossing ? (crossing.What, true) : (null, false);
                case "stop" or "give_way":
                    return signs.Nearest(at, SignReachM) is { } sign ? (sign.What, sign.What == kind) : (null, false);
                case "speed_limit" when way?.Tags.GetValueOrDefault("maxspeed") is { } tagged:
                    return (tagged, tagged == limitKmh?.ToString(CultureInfo.InvariantCulture));
                default:
                    return (null, null);
            }
        }
    }

    /// <summary>Places filed with what each is, to find the nearest within a reach.</summary>
    internal sealed class Near
    {
        readonly Grid _grid = new(50);
        readonly List<(Pt At, string What)> _items = [];
        readonly HashSet<int> _near = [];

        public void Add(Pt at, string what)
        {
            _grid.Add(_items.Count, Box.Empty.With(at));
            _items.Add((at, what));
        }

        public (string What, double OffM)? Nearest(Pt at, double reachM)
        {
            _near.Clear();
            _grid.Near(Box.Empty.With(at).Grown(reachM), _near);
            (string What, double OffM)? best = null;
            foreach (var item in _near)
            {
                var offM = (_items[item].At - at).Length;
                if (offM <= reachM && (best is null || offM < best.Value.OffM)) best = (_items[item].What, offM);
            }

            return best;
        }
    }
}

/// <summary>One sighting as read: its id, where it came from, Mapillary's class or Osmose's title, its kind, place, facing and days seen.</summary>
internal sealed record Seen(string Id, string Source, string Value, string Kind, int? LimitKmh, int Lat, int Lon, Pt At, double? FacingDeg, string? First, string? Last);

/// <summary>One sighting laid on the map, what OSM maps near it for the same thing, and what it was laid as.</summary>
internal sealed class SeenRecord
{
    /// <summary><c>mly&lt;id&gt;</c>, Mapillary's own id; <c>osmose:&lt;uuid&gt;</c> for one Osmose relays.</summary>
    public required string Id { get; init; }

    /// <summary><c>mapillary</c> or <c>osmose</c>.</summary>
    public required string Source { get; init; }

    /// <summary>Mapillary's class verbatim — <c>regulatory--stop--g1</c> — or Osmose's title.</summary>
    public required string Value { get; init; }

    /// <summary>
    /// <c>traffic_light</c>, <c>pedestrian_light</c>, <c>zebra</c>, <c>lane_arrow</c>, <c>stop_line</c>, <c>give_way_line</c>,
    /// <c>stop</c>, <c>give_way</c>, <c>priority_road</c>, <c>speed_limit</c>, <c>turn_ban</c>, <c>turn_only</c>,
    /// <c>no_entry</c>, <c>one_way</c>, <c>no_parking</c>, <c>crossing_sign</c>, <c>signals_ahead</c>, <c>bump</c>,
    /// <c>living_street</c>, <c>roundabout</c>, <c>max_height</c>, <c>max_weight</c>, <c>marking</c> or <c>sign</c>.
    /// </summary>
    public required string Kind { get; init; }

    public int? LimitKmh { get; init; }

    public required int[] At { get; init; }

    /// <summary>The compass bearing the sign faces, as Mapillary aligned it; none for one Osmose relays.</summary>
    public double? FacingDeg { get; init; }

    public string? FirstSeen { get; init; }

    public string? LastSeen { get; init; }

    /// <summary>The survey road it stands by, by OSM id.</summary>
    public long? Road { get; init; }

    /// <summary>The junction it stands nearest, within reach, by node id.</summary>
    public long? Junction { get; init; }

    /// <summary>What OSM maps near it for the same thing — a signal, a crossing's kind, a stop or give-way, its road's speed tag.</summary>
    public string? Osm { get; init; }

    /// <summary>Whether OSM maps the same thing near it; null for a kind not weighed against OSM.</summary>
    public bool? Agrees { get; init; }

    /// <summary>The junction that holds it among its controls (<see cref="JunctionRecord.Held"/>), by node id.</summary>
    public long? HeldBy { get; init; }

    /// <summary>Whether it was laid as a crossing OSM does not map.</summary>
    public bool? Crossing { get; init; }
}
