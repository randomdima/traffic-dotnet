using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Roads a machine traced off imagery</b> (Microsoft's Road Detections): every stretch it detected over the map,
/// with the width it read off the imagery.
/// </summary>
/// <remarks>
/// <para>
/// The release is one zip a UN subregion holding one tab-separated row a stretch — the country's alpha-3 code, then a
/// GeoJSON feature — in no order of country or of place, so the whole region is inflated once as it comes and only
/// the rows over the map are kept (some 1.4 GB read for a few MB kept). A stretch is a short straight of a few
/// points; its width is a string.
/// </para>
/// <para>
/// <b>The width is the paved surface the model saw</b>, which is not a carriageway's: a dual carriageway is seen as
/// one road across both and its median, and cars parked along a kerb are in it.
/// </para>
/// </remarks>
internal static class MlRoads
{
    public const string Licence = "Microsoft Road Detections, ODbL 1.0 — https://github.com/microsoft/RoadDetections";

    const string Region = "https://usaminedroads.z19.web.core.windows.net/drops/2025.04.28/Eastern_Europe.zip";

    const string Country = "UKR";

    /// <summary>When the stretches stand: the release's date, which is not the imagery's — that is older and not given.</summary>
    const string Release = "release 2025-04-28, traced off imagery of years before";

    /// <summary>How far apart a stretch is sampled.</summary>
    const double StepM = 10;

    /// <summary>
    /// How far past half its width a stretch's line may stand off an OSM way and still be that way: the imagery's own
    /// offset from OSM, and a line read down the middle of a surface rather than drawn.
    /// </summary>
    const double SlackM = 5;

    /// <summary>How far past half its width a second carriageway may stand off a stretch's line and still be under its surface.</summary>
    const double UnderSlackM = 2;

    /// <summary>The widest angle a stretch and a way meet at and still run along each other.</summary>
    const double AlongDeg = 25;

    /// <summary>
    /// The share of a stretch's samples that find no OSM way of any kind for it to be unmapped; and the length an
    /// unmapped road must run, its stretches joined, to be flagged — shorter is a yard's corner or the model's noise.
    /// </summary>
    const double UnmappedShare = 0.8, UnmappedM = 40;

    const string About =
        "Every stretch of road Microsoft's model traced off imagery over the map: its line, the width it read, the survey roads it was laid along, or what else of OSM's it lies on — a way for feet, a track, a taxiway, a paved lot — or that OSM has nothing there.";

    static readonly string[] ReadOff = ["ml-roads.geojsonl", "survey", "osm-walk", "osm-transit", "osm-unbuilt", "osm-zones"];

    /// <summary>
    /// The most a surface read off imagery may stand wider than a road's lanes make it — a parking lane each side and
    /// the gutters — and still agree; and the most narrower, a lane.
    /// </summary>
    public const double WiderM = 8, NarrowerM = 3;

    /// <summary>The share of a road a stretch must run along for its width to be weighed against the road's lanes.</summary>
    public const double WeighedShare = 0.5;

    public static string Fetch(Sources sources) =>
        sources.File("ml-roads.geojsonl", Region, Licence, _ => Release, (zip, into) => Clip(zip, into, sources));

    /// <summary>
    /// Every stretch laid along the survey roads it runs down, each road given the width its stretches read; a stretch
    /// along no road is matched to whatever else of OSM's it lies on, and one on nothing is flagged as a road OSM lacks.
    /// </summary>
    /// <remarks>
    /// A stretch is sampled every <see cref="StepM"/> and each sample laid on the nearest survey road running along it
    /// within half its width and <see cref="SlackM"/>, and on any other carriageway along it under the surface it read:
    /// the two ways of a dual carriageway are both under one stretch, and each says it was read across two.
    /// </remarks>
    public static Dictionary<long, DetectedWidth> Lay(Town town, ZonesFound zones, WalkFound walk, Flags flags, string into, List<Written> written)
    {
        var widths = new Dictionary<long, DetectedWidth>();
        if (town.Fetched.MlRoadsAt is not { } kept)
        {
            written.Add(Layers.Write(into, "detected", About, ReadOff, Array.Empty<DetectedRecord>()));
            written.Add(Layers.Write(into, "unmapped", "No road detections this run.", ReadOff, Array.Empty<UnmappedRecord>()));
            return widths;
        }

        var others = new Lines();
        foreach (var record in walk.Records.Where(record => record.Line.Length >= 4)) others.Add("walk", town.Plane.Line(record.Line));
        foreach (var rail in Element.Read(town.Fetched.Transit, 'w').Where(way => way.Tag("railway") is not null && way.Geometry.Length >= 4)) others.Add("rail", town.Plane.Line(rail.Geometry));
        foreach (var way in Element.Read(town.Fetched.Unbuilt, 'w').Where(way => way.Geometry.Length >= 4)) others.Add("unbuilt", town.Plane.Line(way.Geometry));
        foreach (var way in Element.Read(town.Fetched.Zones, 'w').Where(way => way.Tag("aeroway") is "runway" or "taxiway" or "apron" && way.Geometry.Length >= 4))
        {
            others.Add("aeroway", town.Plane.Line(way.Geometry));
        }
        var pavedGrid = new Grid(200);
        for (var at = 0; at < zones.Paved.Count; at++) pavedGrid.Add(at, zones.Paved[at].Bounds);

        var samples = new Dictionary<int, List<(double WidthM, int Across)>>();
        var found = new List<(int[] Degrees, Pt[] Line, double WidthM, double LengthM, string Status, SortedSet<long> LaidOn)>();
        var records = new List<DetectedRecord>();
        var near = new HashSet<int>();
        var stretches = Read(kept, town.Sources);
        for (var stretch = 0; stretch < stretches.Count; stretch++)
        {
            var (degrees, widthM) = stretches[stretch];
            var line = town.Plane.Line(degrees);
            var lengthM = Shape.Length(line);
            var reachM = (widthM / 2) + SlackM;
            var laidOn = new SortedSet<long>();
            var said = new Dictionary<string, int>();
            for (var alongM = Math.Min(StepM / 2, lengthM / 2); alongM <= lengthM; alongM += StepM)
            {
                var place = Shape.Along(line, alongM);
                var run = Shape.Along(line, Math.Min(lengthM, alongM + 1)) - Shape.Along(line, Math.Max(0, alongM - 1));
                var under = Under(town, place, run, reachM, near);
                string status;
                if (under.Count > 0)
                {
                    var spanned = under.Where((road, k) => k == 0 || road.OffM <= (widthM / 2) + UnderSlackM).ToArray();
                    foreach (var (road, _) in spanned)
                    {
                        (samples.TryGetValue(road, out var list) ? list : samples[road] = []).Add((widthM, spanned.Length));
                        laidOn.Add(town.Roads[road].Id);
                    }

                    status = "road";
                }
                else
                {
                    near.Clear();
                    pavedGrid.Near(Box.Empty.With(place), near);
                    status = others.Nearest(place, reachM)
                             ?? (near.Any(lot => zones.Paved[lot].Holds(place)) ? "paved"
                                 : town.NearestRoad(place, reachM) is not null ? "road_across" : "none");
                }

                said[status] = said.GetValueOrDefault(status) + 1;
            }

            var samplesTaken = said.Values.Sum();
            var most = said.Count == 0 ? "none" : said.MaxBy(pair => pair.Value).Key;
            var unmapped = samplesTaken > 0 && said.GetValueOrDefault("none") >= UnmappedShare * samplesTaken;
            var kind = said.ContainsKey("road") && said["road"] * 2 >= samplesTaken ? "road" : unmapped ? "unmapped" : most == "none" ? "mixed" : most;
            found.Add((degrees, line, widthM, lengthM, kind, laidOn));
        }

        var networks = Networks(town, found);
        for (var stretch = 0; stretch < found.Count; stretch++)
        {
            var (degrees, _, widthM, lengthM, kind, laidOn) = found[stretch];
            records.Add(new DetectedRecord
            {
                Id = $"mlr{stretch}",
                Status = kind,
                WidthM = Math.Round(widthM, 1),
                LengthM = Math.Round(lengthM, 1),
                Roads = laidOn.Count > 0 ? [.. laidOn] : null,
                Network = networks.Of.TryGetValue(stretch, out var network) ? network : null,
                Line = degrees,
            });
        }

        foreach (var network in networks.Records.Where(network => network.LengthM >= UnmappedM))
        {
            var longest = network.Lines.MaxBy(line => line.Length)!;
            var middle = town.Plane.Line(longest);
            flags.Raise("road_not_in_osm", town, Shape.Along(middle, Shape.Length(middle) / 2),
                string.Create(CultureInfo.InvariantCulture,
                    $"a road {network.WidthM:F1} m wide traced off imagery, {network.LengthM:F0} m in {network.Stretches.Length} stretches, with no OSM way beside it{(network.Meets is { } meets ? $"; it meets {string.Join(", ", meets.Select(way => $"w{way}"))}" : "")}"),
                [network.Id, .. (network.Meets ?? []).Select(way => $"w{way}")]);
        }

        foreach (var (road, list) in samples)
        {
            widths[town.Roads[road].Id] = new DetectedWidth
            {
                WidthM = Math.Round(Median(list.Select(sample => sample.WidthM)), 1),
                Share = Math.Round(Math.Min(1, list.Count * StepM / Math.Max(StepM, town.RoadLengthM[road])), 2),
                Across = (int)Median(list.Select(sample => (double)sample.Across)),
            };
        }

        written.Add(Layers.Write(into, "detected", About, ReadOff, records,
            new
            {
                byStatus = records.GroupBy(record => record.Status).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                kmByStatus = records.GroupBy(record => record.Status).OrderByDescending(g => g.Sum(r => r.LengthM)).ToDictionary(g => g.Key, g => Math.Round(g.Sum(r => r.LengthM) / 1000, 1)),
                roadsWithWidth = widths.Count,
                roadsReadAcrossTwo = widths.Values.Count(width => width.Across >= 2),
            }));
        written.Add(Layers.Write(into, "unmapped",
            "Every road the imagery shows that OSM has no way of any kind for: its unmapped stretches joined where they meet, its lines, length and width, and the survey roads its free ends meet.",
            ReadOff, networks.Records,
            new
            {
                networks = networks.Records.Count,
                flagged = networks.Records.Count(network => network.LengthM >= UnmappedM),
                km = Math.Round(networks.Records.Sum(network => network.LengthM) / 1000, 1),
                meetingTheSurvey = networks.Records.Count(network => network.Meets is not null),
            }));
        return widths;
    }

    static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted[sorted.Length / 2];
    }

    /// <summary>
    /// The unmapped stretches joined into networks where their ends meet — the model writes a road as stretches end to
    /// end — each with the survey roads its free ends meet: a stretch laid on survey roads sharing the end, else the
    /// survey road within its reach of it.
    /// </summary>
    static (List<UnmappedRecord> Records, Dictionary<int, string> Of) Networks(
        Town town, List<(int[] Degrees, Pt[] Line, double WidthM, double LengthM, string Status, SortedSet<long> LaidOn)> found)
    {
        static (int, int) First(int[] line) => (line[0], line[1]);
        static (int, int) Last(int[] line) => (line[^2], line[^1]);

        var ends = new Dictionary<(int, int), List<int>>();
        for (var stretch = 0; stretch < found.Count; stretch++)
        {
            foreach (var end in (ReadOnlySpan<(int, int)>)[First(found[stretch].Degrees), Last(found[stretch].Degrees)])
            {
                (ends.TryGetValue(end, out var list) ? list : ends[end] = []).Add(stretch);
            }
        }

        var parent = Enumerable.Range(0, found.Count).ToArray();
        int Root(int at) => parent[at] == at ? at : parent[at] = Root(parent[at]);
        foreach (var sharing in ends.Values)
        {
            var unmapped = sharing.Where(stretch => found[stretch].Status == "unmapped").ToArray();
            foreach (var other in unmapped.Skip(1)) parent[Root(other)] = Root(unmapped[0]);
        }

        var records = new List<UnmappedRecord>();
        var of = new Dictionary<int, string>();
        foreach (var group in Enumerable.Range(0, found.Count).Where(stretch => found[stretch].Status == "unmapped").GroupBy(Root).OrderBy(group => group.Key))
        {
            var id = $"mlw{records.Count}";
            var members = group.ToArray();
            var meets = new SortedSet<long>();
            foreach (var stretch in members)
            {
                foreach (var end in (ReadOnlySpan<(int, int)>)[First(found[stretch].Degrees), Last(found[stretch].Degrees)])
                {
                    var sharing = ends[end];
                    if (sharing.Any(other => other != stretch && found[other].Status == "unmapped")) continue;

                    var laid = sharing.Where(other => found[other].Status == "road").SelectMany(other => found[other].LaidOn).ToArray();
                    if (laid.Length > 0) meets.UnionWith(laid);
                    else if (town.NearestRoad(town.Plane.At(end.Item1, end.Item2), (found[stretch].WidthM / 2) + SlackM) is { } road) meets.Add(town.Roads[road.Road].Id);
                }

                of[stretch] = id;
            }

            records.Add(new UnmappedRecord
            {
                Id = id,
                Stretches = [.. members.Select(stretch => $"mlr{stretch}")],
                LengthM = Math.Round(members.Sum(stretch => found[stretch].LengthM), 1),
                WidthM = Math.Round(Median(members.Select(stretch => found[stretch].WidthM)), 1),
                Meets = meets.Count > 0 ? [.. meets] : null,
                Lines = [.. members.Select(stretch => found[stretch].Degrees)],
            });
        }

        return (records, of);
    }

    /// <summary>Every survey road running along a sample within a reach, by road, nearest first.</summary>
    static List<(int Road, double OffM)> Under(Town town, Pt place, Pt run, double reachM, HashSet<int> near)
    {
        near.Clear();
        town.RoadGrid.Near(Box.Empty.With(place).Grown(reachM), near);
        var nearest = new Dictionary<int, double>();
        var along = Math.Sin(double.DegreesToRadians(AlongDeg));
        foreach (var segment in near)
        {
            var (road, first) = town.Segments[segment];
            var line = town.RoadLineM[road];
            var (offM, _, _, _) = Shape.Nearest(place, line.AsSpan(first, 2));
            var way = line[first + 1] - line[first];
            if (offM > reachM || Math.Abs(Pt.Cross(way, run)) > along * way.Length * run.Length) continue;
            if (!nearest.TryGetValue(road, out var held) || offM < held) nearest[road] = offM;
        }

        return [.. nearest.Select(pair => (pair.Key, pair.Value)).OrderBy(pair => pair.Value)];
    }

    /// <summary>Every line of OSM's that is not a survey road, filed by segment, to say what of OSM's a stretch lies on.</summary>
    sealed class Lines
    {
        readonly Grid _grid = new(40);
        readonly List<(string Kind, Pt From, Pt To)> _segments = [];
        readonly HashSet<int> _near = [];

        public void Add(string kind, Pt[] line)
        {
            for (var at = 0; at + 1 < line.Length; at++)
            {
                _grid.Add(_segments.Count, Box.Empty.With(line[at]).With(line[at + 1]));
                _segments.Add((kind, line[at], line[at + 1]));
            }
        }

        /// <summary>The kind of the nearest line within a reach; null where none is.</summary>
        public string? Nearest(Pt at, double reachM)
        {
            _near.Clear();
            _grid.Near(Box.Empty.With(at).Grown(reachM), _near);
            (string Kind, double OffM)? best = null;
            foreach (var segment in _near)
            {
                var (kind, from, to) = _segments[segment];
                var (offM, _, _, _) = Shape.Nearest(at, [from, to]);
                if (offM <= reachM && (best is null || offM < best.Value.OffM)) best = (kind, offM);
            }

            return best?.Kind;
        }
    }

    /// <summary>The zip's first entry inflated as it streams in, and every row of the country over the rectangle kept.</summary>
    static void Clip(Stream zip, Stream into, Sources sources)
    {
        Span<byte> head = stackalloc byte[30];
        zip.ReadExactly(head);
        if (BinaryPrimitives.ReadUInt32LittleEndian(head) != 0x04034B50 || BinaryPrimitives.ReadUInt16LittleEndian(head[8..]) != 8)
        {
            throw new InvalidDataException("not a zip whose first entry is deflated");
        }

        zip.ReadExactly(new byte[BinaryPrimitives.ReadUInt16LittleEndian(head[26..]) + BinaryPrimitives.ReadUInt16LittleEndian(head[28..])]);
        using var rows = new StreamReader(new DeflateStream(zip, CompressionMode.Decompress, leaveOpen: true), Encoding.UTF8);
        using var kept = new StreamWriter(into, new UTF8Encoding(false), leaveOpen: true);
        const string prefix = Country + "\t";
        for (var row = rows.ReadLine(); row is not null; row = rows.ReadLine())
        {
            if (!row.StartsWith(prefix, StringComparison.Ordinal)) continue;

            var feature = row[prefix.Length..].TrimEnd('\r');
            if (Stretch(feature, sources) is not null) kept.WriteLine(feature);
        }
    }

    /// <summary>Every stretch kept: its line as lat, lon pairs in 1e-7°, and its width.</summary>
    public static List<(int[] Line, double WidthM)> Read(string keptAt, Sources sources)
    {
        var read = new List<(int[] Line, double WidthM)>();
        foreach (var feature in File.ReadLines(keptAt))
        {
            if (Stretch(feature, sources) is { } stretch) read.Add(stretch);
        }

        return read;
    }

    /// <summary>One row's stretch where any of its points is over the rectangle and it says a width; else null.</summary>
    static (int[] Line, double WidthM)? Stretch(string feature, Sources sources)
    {
        using var parsed = JsonDocument.Parse(feature);
        var geometry = parsed.RootElement.GetProperty("geometry");
        if (geometry.GetProperty("type").GetString() != "LineString") return null;

        var points = geometry.GetProperty("coordinates");
        var line = new int[points.GetArrayLength() * 2];
        var over = false;
        var at = 0;
        foreach (var point in points.EnumerateArray())
        {
            var (lon, lat) = (point[0].GetDouble(), point[1].GetDouble());
            over |= lat >= sources.SouthDeg && lat <= sources.NorthDeg && lon >= sources.WestDeg && lon <= sources.EastDeg;
            line[at++] = (int)Math.Round(lat * 1e7);
            line[at++] = (int)Math.Round(lon * 1e7);
        }

        return over && line.Length >= 4
                    && parsed.RootElement.GetProperty("properties").TryGetProperty("WidthMeters", out var width)
                    && double.TryParse(width.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var widthM) && widthM > 0
            ? (line, widthM)
            : null;
    }
}

/// <summary>One stretch of road traced off imagery, and what of OSM's it was found to be.</summary>
internal sealed class DetectedRecord
{
    /// <summary><c>mlr&lt;n&gt;</c>, by its row in the release's rows over the map; a later release renumbers them.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// <c>road</c> (laid along survey roads), <c>walk</c>, <c>rail</c>, <c>unbuilt</c>, <c>aeroway</c>, <c>paved</c> (inside a lot),
    /// <c>road_across</c> (by a road it does not run along), <c>unmapped</c> (nothing of OSM's there) or <c>mixed</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>The paved width the model read, across every carriageway and kerbside lane under it.</summary>
    public required double WidthM { get; init; }

    public required double LengthM { get; init; }

    /// <summary>The survey roads it was laid along, by OSM id.</summary>
    public long[]? Roads { get; init; }

    /// <summary>For an unmapped stretch, the unmapped road it is part of (<see cref="UnmappedRecord"/>).</summary>
    public string? Network { get; init; }

    /// <summary>Lat, lon pairs in 1e-7°.</summary>
    public required int[] Line { get; init; }
}

/// <summary>A road the imagery shows that OSM lacks: its unmapped stretches joined where they meet.</summary>
internal sealed class UnmappedRecord
{
    /// <summary><c>mlw&lt;n&gt;</c>; a later release renumbers them.</summary>
    public required string Id { get; init; }

    /// <summary>Its stretches in the detected layer, by id.</summary>
    public required string[] Stretches { get; init; }

    public required double LengthM { get; init; }

    /// <summary>The median of its stretches' widths.</summary>
    public required double WidthM { get; init; }

    /// <summary>The survey roads its free ends meet, by OSM id; null for one standing alone.</summary>
    public long[]? Meets { get; init; }

    /// <summary>Each stretch's line, lat, lon pairs in 1e-7°.</summary>
    public required int[][] Lines { get; init; }
}

/// <summary>A road's width as the stretches traced off imagery along it read it.</summary>
internal sealed class DetectedWidth
{
    /// <summary>The median of its stretches' widths: the whole paved surface, kerb to kerb.</summary>
    public required double WidthM { get; init; }

    /// <summary>The share of its length a stretch was laid along.</summary>
    public required double Share { get; init; }

    /// <summary>How many OSM carriageways the surface spanned, at the median: 2 for each way of a dual carriageway.</summary>
    public required int Across { get; init; }
}
