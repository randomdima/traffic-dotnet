namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Where people walk</b>: every way OSM draws for feet — pavements drawn on their own, crossings, steps, paths,
/// pedestrian streets and squares — each with the road a pavement runs beside and on which side of it, so the
/// pavements a road has are known whether a mapper tagged them on the road or drew them apart.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pavement is matched to its road by where it runs</b>: points along it a few metres apart are each put to
/// the nearest road segment running the same way within reach, and the road and side most of them find is the
/// pavement's. A way tagged <c>footway=sidewalk</c> is a pavement by its tag; any other footway, path or cycleway
/// running beside one road for most of its length is one by its geometry, and says so.
/// </para>
/// <para>
/// A road's side is as it is drawn: <c>left</c> and <c>right</c> looking along the way from its first node, the
/// sides OSM's own <c>sidewalk:left</c> and <c>sidewalk:right</c> name.
/// </para>
/// </remarks>
internal static class Walk
{
    /// <summary>The farthest a pavement stands off its road's line: half a wide road, a verge and the pavement's own half.</summary>
    const double ReachM = 25;

    /// <summary>The most a pavement's run turns off its road's and still runs beside it.</summary>
    const double ParallelDeg = 25;

    /// <summary>How far apart a pavement is sampled.</summary>
    const double StepM = 5;

    /// <summary>The share of a way's samples that must find one road and side for an untagged way to be its pavement.</summary>
    const double BesideShare = 0.7;

    public static WalkFound Lay(Town town, Flags flags, string into, List<Written> written)
    {
        var elements = Element.Read(town.Fetched.Walk);
        var found = new WalkFound();
        foreach (var node in elements.Where(element => element.Type == 'n')) found.NodeTags[node.Id] = node.Tags;

        var records = new List<WalkRecord>();
        var coverage = new Dictionary<(int Road, int Side), List<(double From, double To)>>();
        foreach (var way in elements.Where(element => element.Type == 'w' && element.Geometry.Length >= 4))
        {
            var kind = Kind(way.Tags);
            var geometry = town.Surveyed(way);
            var line = town.Plane.Line(geometry);
            var lengthM = Shape.Length(line);
            string? sidewalkFrom = null;
            (int Road, int Side, double OffM, double Share)? beside = null;
            if (kind is "sidewalk" or "footway" or "path" or "cycleway")
            {
                beside = Beside(town, line, coverage, commit: false);
                if (kind == "sidewalk") sidewalkFrom = "tag";
                else if (beside is { Share: >= BesideShare }) (kind, sidewalkFrom) = ("sidewalk", "geometry");

                if (sidewalkFrom is not null) Beside(town, line, coverage, commit: true);
            }

            var record = new WalkRecord
            {
                Way = way.Id,
                Kind = kind,
                SidewalkFrom = sidewalkFrom,
                Road = sidewalkFrom is not null && beside is { } along ? town.Roads[along.Road].Id : null,
                Side = sidewalkFrom is not null && beside is { } on ? (on.Side > 0 ? "right" : "left") : null,
                OffM = sidewalkFrom is not null && beside is { } off ? Math.Round(off.OffM, 1) : null,
                Share = sidewalkFrom is not null && beside is { } share ? Math.Round(share.Share, 2) : null,
                LengthM = Math.Round(lengthM, 1),
                Nodes = way.Nodes,
                Line = geometry,
                Tags = way.Tags,
            };
            records.Add(record);
            if (kind == "crossing") found.CrossingWays.Add((way, line));
            if (kind == "sidewalk" && record.Road is null)
            {
                flags.Raise("sidewalk_beside_no_road", town, Shape.Along(line, lengthM / 2), "a pavement no road runs beside within reach", $"w{way.Id}");
            }
        }

        foreach (var ((road, side), runs) in coverage)
        {
            var covered = 0.0;
            var (from, to) = (double.MinValue, double.MinValue);
            foreach (var run in runs.OrderBy(run => run.From))
            {
                if (run.From > to)
                {
                    covered += Math.Max(0, to - from);
                    (from, to) = run;
                }
                else
                {
                    to = Math.Max(to, run.To);
                }
            }

            covered += Math.Max(0, to - from);
            found.Covered[(road, side)] = Math.Min(1, covered / Math.Max(1e-6, town.RoadLengthM[road]));
        }

        var components = Components(records);
        written.Add(Layers.Write(into, "walk",
            "Every way OSM draws for feet: its kind, for a pavement the road it runs beside, on which side as the road is drawn and how far off, its nodes for joining the network, its line in 1e-7° and its tags.",
            ["osm-walk"], records,
            new
            {
                kinds = records.GroupBy(record => record.Kind).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                kmByKind = records.GroupBy(record => record.Kind).OrderByDescending(g => g.Sum(r => r.LengthM))
                    .ToDictionary(g => g.Key, g => Math.Round(g.Sum(r => r.LengthM) / 1000, 1)),
                sidewalksByGeometry = records.Count(record => record.SidewalkFrom == "geometry"),
                sidewalksWithNoRoad = records.Count(record => record.Kind == "sidewalk" && record.Road is null),
                networkPieces = components.Count,
                largestPieceKm = components.Count > 0 ? Math.Round(components.Max() / 1000, 1) : 0,
            }));
        found.Records = records;
        return found;
    }

    /// <summary>What a way for feet is, by its tags.</summary>
    static string Kind(Dictionary<string, string> tags)
    {
        var highway = tags.GetValueOrDefault("highway");
        var sub = tags.GetValueOrDefault("footway") ?? tags.GetValueOrDefault("path") ?? tags.GetValueOrDefault("cycleway");
        if (highway is null && tags.TryGetValue("area:highway", out var area)) return $"area_{area}";
        if (sub == "crossing" || (highway == "footway" && tags.ContainsKey("crossing"))) return "crossing";
        if (sub == "sidewalk" || tags.GetValueOrDefault("is_sidepath") == "yes") return "sidewalk";
        if (sub == "traffic_island") return "island";
        return highway switch
        {
            "pedestrian" => tags.GetValueOrDefault("area") == "yes" ? "pedestrian_area" : "pedestrian_street",
            "steps" => "steps",
            "cycleway" => "cycleway",
            "path" or "bridleway" => "path",
            "corridor" => "corridor",
            "platform" => "platform",
            "elevator" => "elevator",
            "footway" when tags.GetValueOrDefault("area") == "yes" => "footway_area",
            _ => "footway",
        };
    }

    /// <summary>
    /// The road and side most of a line's samples run beside, how far off on average and what share of samples found
    /// it; with <paramref name="commit"/>, each sample's stretch of road is put down as covered on that side.
    /// </summary>
    static (int Road, int Side, double OffM, double Share)? Beside(
        Town town, Pt[] line, Dictionary<(int Road, int Side), List<(double From, double To)>> coverage, bool commit)
    {
        var lengthM = Shape.Length(line);
        var votes = new Dictionary<(int Road, int Side), (int Count, double OffM)>();
        var hits = new List<((int Road, int Side) Key, double AlongM)>();
        var samples = 0;
        for (var alongM = Math.Min(StepM / 2, lengthM / 2); alongM <= lengthM; alongM += StepM)
        {
            samples++;
            var at = Shape.Along(line, alongM);
            var ahead = Shape.Along(line, Math.Min(lengthM, alongM + 1)) - Shape.Along(line, Math.Max(0, alongM - 1));
            var near = new HashSet<int>();
            town.RoadGrid.Near(Box.Empty.With(at).Grown(ReachM), near);
            (int Road, int Side, double OffM, double AlongM)? best = null;
            foreach (var segment in near)
            {
                var (road, first) = town.Segments[segment];
                if (town.Highway(road) is "track" or "busway" or "raceway") continue;

                var (a, b) = (town.RoadLineM[road][first], town.RoadLineM[road][first + 1]);
                var run = b - a;
                if (run.Length < 0.5) continue;

                var cos = Math.Abs(Pt.Dot(run, ahead)) / (run.Length * Math.Max(1e-9, ahead.Length));
                if (cos < Math.Cos(double.DegreesToRadians(ParallelDeg))) continue;

                var (offM, segmentAlongM, _, _) = Shape.Nearest(at, [a, b]);
                if (offM > ReachM || offM < 0.3 || (best is { } held && offM >= held.OffM)) continue;

                best = (road, Math.Sign(Pt.Cross(run, at - a)), offM, town.AlongM(road, first) + segmentAlongM);
            }

            if (best is not { } found) continue;

            var key = (found.Road, found.Side);
            votes[key] = votes.TryGetValue(key, out var vote) ? (vote.Count + 1, vote.OffM + found.OffM) : (1, found.OffM);
            hits.Add((key, found.AlongM));
        }

        if (votes.Count == 0 || samples == 0) return null;

        var ((winner, side), (count, sumM)) = votes.MaxBy(vote => vote.Value.Count);
        if (commit)
        {
            foreach (var (key, alongM) in hits)
            {
                if (key != (winner, side)) continue;

                (coverage.TryGetValue(key, out var runs) ? runs : coverage[key] = []).Add((alongM - (StepM / 2), alongM + (StepM / 2)));
            }
        }

        return (winner, side, sumM / count, (double)count / samples);
    }

    /// <summary>The walk network's pieces, joined where ways share a node, as each piece's length in metres.</summary>
    static List<double> Components(List<WalkRecord> records)
    {
        var parent = new Dictionary<long, long>();
        long Root(long node)
        {
            if (!parent.TryGetValue(node, out var up)) return parent[node] = node;
            while (up != node)
            {
                var next = parent[up];
                parent[node] = next;
                (node, up) = (up, next);
            }

            return node;
        }

        foreach (var record in records)
        {
            for (var at = 1; at < record.Nodes.Length; at++) parent[Root(record.Nodes[at])] = Root(record.Nodes[0]);
        }

        return [.. records.Where(record => record.Nodes.Length > 0).GroupBy(record => Root(record.Nodes[0])).Select(group => group.Sum(record => record.LengthM))];
    }
}

/// <summary>What the walk layer found that later layers read.</summary>
internal sealed class WalkFound
{
    public List<WalkRecord> Records { get; set; } = [];

    /// <summary>Every tagged node on a way for feet — a kerb, a crossing's end, a barrier — by its OSM id.</summary>
    public Dictionary<long, Dictionary<string, string>> NodeTags { get; } = [];

    public List<(Element Way, Pt[] LineM)> CrossingWays { get; } = [];

    /// <summary>The share of a road's length a pavement drawn apart runs beside, by road and side (+1 right, −1 left).</summary>
    public Dictionary<(int Road, int Side), double> Covered { get; } = [];
}

internal sealed class WalkRecord
{
    public required long Way { get; init; }

    /// <summary>
    /// <c>sidewalk</c>, <c>crossing</c>, <c>island</c>, <c>footway</c>, <c>path</c>, <c>cycleway</c>, <c>steps</c>,
    /// <c>pedestrian_street</c>, <c>pedestrian_area</c>, <c>footway_area</c>, <c>corridor</c>, <c>platform</c>,
    /// <c>elevator</c>, or <c>area_*</c> for a surface outlined by <c>area:highway</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>For a pavement: <c>tag</c> where OSM says so, <c>geometry</c> where it runs beside one road.</summary>
    public string? SidewalkFrom { get; init; }

    /// <summary>The road way a pavement runs beside, by its OSM id.</summary>
    public long? Road { get; init; }

    /// <summary><c>left</c> or <c>right</c> of the road as it is drawn.</summary>
    public string? Side { get; init; }

    /// <summary>How far off the road's line it runs, on average.</summary>
    public double? OffM { get; init; }

    /// <summary>The share of its samples that found that road and side.</summary>
    public double? Share { get; init; }

    public required double LengthM { get; init; }

    public required long[] Nodes { get; init; }

    /// <summary>Lat, lon pairs in 1e-7°.</summary>
    public required int[] Line { get; init; }

    public required Dictionary<string, string> Tags { get; init; }
}
