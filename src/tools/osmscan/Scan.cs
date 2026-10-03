using System.Text.Encodings.Web;
using System.Text.Json;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>One city taken off OpenStreetMap into its traced map's survey</b>, <c>towns/traced/&lt;Map&gt;.json</c>
/// (<see cref="OsmExtract"/>): the rectangle round every road way inside the city's boundary relation, and in it
/// every road way with all its tags, whether the boundary holds it or not, every tagged node on them, every turn
/// restriction and lane connectivity relation over them, every surface outlining a road (<c>area:highway</c>),
/// and the coastline — each node at OSM's own integer 1e-7°, so nothing is rounded on the way in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The boundary draws the rectangle and nothing else.</b> A city's limits are a line on a map and its suburbs'
/// streets run on across them, so a map of the city's own roads alone would leave grass where OSM has roads
/// inside its own edge. A way crossing the rectangle's edge is taken whole; the engine cuts it there.
/// </para>
/// <para>
/// Two things are added, both OSM's conventions and neither the engine's: <b>each road way's lanes</b> as its
/// tags mean them (<see cref="OsmCarriageway.Read"/>), and <b>the frame</b> every node is read into metres by
/// (<see cref="OsmFrame"/>) — Transverse Mercator about the middle of the city's roads, the map their extent and
/// <see cref="MarginM"/>.
/// </para>
/// <para>A node two ways place differently is refused rather than averaged.</para>
/// </remarks>
internal static class Scan
{
    sealed record City(string Map, long Relation, string Description);

    static readonly Dictionary<string, City> Cities = new()
    {
        ["odesa"] = new("OdesaOsm", 12888405, "Odesa's own streets and sea line, traced 1:1 off OpenStreetMap"),
    };

    /// <summary>
    /// Every <c>highway</c> value a vehicle is driven on: the road classes, their links and OSM's special road
    /// types. What is walked, ridden or not yet built is not a road and is left out.
    /// </summary>
    static readonly string[] Roads =
    [
        "motorway", "trunk", "primary", "secondary", "tertiary", "unclassified", "residential",
        "motorway_link", "trunk_link", "primary_link", "secondary_link", "tertiary_link",
        "living_street", "service", "road", "busway", "bus_guideway", "track", "raceway", "escape",
    ];

    /// <summary>
    /// How far past the roads' own extent coastline is taken, in degrees, so a coast that leaves the map between
    /// two of its nodes is still closed against the edge it crosses.
    /// </summary>
    const decimal CoastMarginDeg = 0.01m;

    /// <summary>How far the map runs past the rectangle its roads are cut to, so nothing is laid on its edge.</summary>
    const double MarginM = 60;

    const decimal UnitsPerDegree = 10_000_000m;

    public static int Run(string root, string city, bool refetch)
    {
        if (!Cities.TryGetValue(city, out var place)) throw new ArgumentException($"no city '{city}': {string.Join(", ", Cities.Keys)}");

        var kept = Path.Combine(root, ".tmp", "osm");
        Directory.CreateDirectory(kept);
        var pattern = $"[\"highway\"~\"^({string.Join('|', Roads)})$\"]";

        // The city's own roads, which say where the map is and nothing more.
        using var own = Overpass.Answer(Path.Combine(kept, $"{city}-city.json"),
            $"[out:json][timeout:600];area(id:{3600000000 + place.Relation})->.city;way(area.city){pattern};out geom;", refetch);
        var ownWays = Elements(own, "way");
        if (ownWays.Count == 0) throw new InvalidOperationException($"relation {place.Relation} holds no road");

        var (south, west, north, east) = (decimal.MaxValue, decimal.MaxValue, decimal.MinValue, decimal.MinValue);
        foreach (var way in ownWays)
        {
            foreach (var at in way.GetProperty("geometry").EnumerateArray())
            {
                south = Math.Min(south, at.GetProperty("lat").GetDecimal());
                north = Math.Max(north, at.GetProperty("lat").GetDecimal());
                west = Math.Min(west, at.GetProperty("lon").GetDecimal());
                east = Math.Max(east, at.GetProperty("lon").GetDecimal());
            }
        }

        var box = $"({south},{west},{north},{east})";
        using var roads = Overpass.Answer(Path.Combine(kept, $"{city}-region.json"),
            $"[out:json][timeout:900];way{pattern}{box}->.roads;" +
            ".roads out body geom;" +
            "node(w.roads)(if:count_tags() > 0);out body;" +
            "rel(bw.roads)[\"type\"~\"^(restriction|connectivity)\"];out body;",
            refetch);
        var roadWays = Elements(roads, "way");

        using var areas = Overpass.Answer(Path.Combine(kept, $"{city}-region-areas.json"),
            $"[out:json][timeout:300];way[\"area:highway\"]{box};out body geom;", refetch);
        var areaWays = Elements(areas, "way");

        using var coast = Overpass.Answer(Path.Combine(kept, $"{city}-coastline.json"),
            $"[out:json][timeout:180];way[\"natural\"=\"coastline\"]" +
            $"({south - CoastMarginDeg},{west - CoastMarginDeg},{north + CoastMarginDeg},{east + CoastMarginDeg});out body geom;",
            refetch);
        var coastWays = Elements(coast, "way");

        var units = Placed([.. roadWays, .. coastWays, .. areaWays]);
        var ids = units.Keys.Order().ToArray();
        var index = new Dictionary<long, int>(ids.Length);
        for (var at = 0; at < ids.Length; at++) index[ids[at]] = at;

        var nodes = new OsmNodes
        {
            Id = ids,
            Lat = [.. ids.Select(id => units[id].Lat)],
            Lon = [.. ids.Select(id => units[id].Lon)],
        };

        var extract = new OsmExtract
        {
            Name = place.Map,
            Description = place.Description,
            Source = new SurveySource
            {
                Relation = place.Relation,
                OsmBase = roads.RootElement.TryGetProperty("osm3s", out var osm3s) ? osm3s.GetProperty("timestamp_osm_base").GetString() ?? "" : "",
                Licence = "© OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright",
            },
            Nodes = nodes,
            NodeTags =
            [
                .. Elements(roads, "node")
                    .Where(node => node.TryGetProperty("tags", out _) && index.ContainsKey(node.GetProperty("id").GetInt64()))
                    .OrderBy(node => node.GetProperty("id").GetInt64())
                    .Select(node => new OsmNodeTags { Node = index[node.GetProperty("id").GetInt64()], Tags = Tags(node) }),
            ],
            Frame = Framed(ownWays),
            Ways = [.. roadWays.Concat(coastWays).Select(way => Way(way, index, lanes: true)).OrderBy(way => way.Id)],
            Areas = [.. areaWays.Select(way => Way(way, index, lanes: false)).OrderBy(way => way.Id)],
            Relations =
            [
                .. Elements(roads, "relation").OrderBy(relation => relation.GetProperty("id").GetInt64()).Select(relation => new OsmRelation
                {
                    Id = relation.GetProperty("id").GetInt64(),
                    Tags = Tags(relation),
                    Members =
                    [
                        .. relation.GetProperty("members").EnumerateArray().Select(member => new OsmMember
                        {
                            Type = member.GetProperty("type").GetString()!,
                            Ref = member.GetProperty("ref").GetInt64(),
                            Role = member.GetProperty("role").GetString() ?? "",
                        }),
                    ],
                }),
            ],
        };
        extract.Check(place.Map);

        var into = Path.Combine(root, "towns", "traced", $"{place.Map}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(into)!);
        var json = new OsmExtractJson(new JsonSerializerOptions(OsmExtractJson.Default.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(into, JsonSerializer.Serialize(extract, json.OsmExtract) + "\n");

        Report(extract, Path.GetRelativePath(root, into));
        return 0;
    }

    static List<JsonElement> Elements(JsonDocument answer, string type) =>
        [.. answer.RootElement.GetProperty("elements").EnumerateArray().Where(element => element.GetProperty("type").GetString() == type)];

    static Dictionary<string, string> Tags(JsonElement element)
    {
        var tags = new Dictionary<string, string>();
        if (!element.TryGetProperty("tags", out var tagged)) return tags;

        foreach (var tag in tagged.EnumerateObject()) tags[tag.Name] = tag.Value.GetString() ?? "";
        return tags;
    }

    /// <summary>
    /// Every node's place in OSM's integer units. Overpass prints the stored integer as seven decimals, so the
    /// decimal read back is exact.
    /// </summary>
    static Dictionary<long, (int Lat, int Lon)> Placed(List<JsonElement> ways)
    {
        var at = new Dictionary<long, (int Lat, int Lon)>();
        foreach (var way in ways)
        {
            var ids = way.GetProperty("nodes").EnumerateArray().Select(node => node.GetInt64()).ToArray();
            var places = way.GetProperty("geometry").EnumerateArray().ToArray();
            for (var node = 0; node < ids.Length; node++)
            {
                var units = ((int)(places[node].GetProperty("lat").GetDecimal() * UnitsPerDegree),
                             (int)(places[node].GetProperty("lon").GetDecimal() * UnitsPerDegree));
                if (at.TryGetValue(ids[node], out var was) && was != units)
                {
                    throw new InvalidDataException($"node {ids[node]} stands at {was} on one way and {units} on another");
                }

                at[ids[node]] = units;
            }
        }

        return at;
    }

    static OsmWay Way(JsonElement way, Dictionary<long, int> index, bool lanes)
    {
        var tags = Tags(way);
        return new OsmWay
        {
            Id = way.GetProperty("id").GetInt64(),
            Tags = tags,
            Nodes = [.. way.GetProperty("nodes").EnumerateArray().Select(node => index[node.GetInt64()])],
            Carriageway = lanes ? OsmCarriageway.Read(tags) : null,
        };
    }

    /// <summary>
    /// The frame: projected about the middle of the city's own roads' box of degrees, the map their extent and a
    /// margin.
    /// </summary>
    static OsmFrame Framed(List<JsonElement> ownWays)
    {
        var degrees = ownWays.SelectMany(way => way.GetProperty("geometry").EnumerateArray())
            .Select(at => (Lat: (double)at.GetProperty("lat").GetDecimal(), Lon: (double)at.GetProperty("lon").GetDecimal()))
            .ToArray();
        var lat0 = (degrees.Min(at => at.Lat) + degrees.Max(at => at.Lat)) * 0.5;
        var lon0 = (degrees.Min(at => at.Lon) + degrees.Max(at => at.Lon)) * 0.5;
        var projection = new TransverseMercator(lat0, lon0);
        var placed = degrees.Select(at => projection.Project(at.Lat, at.Lon)).ToArray();
        var westM = placed.Min(at => at.EastM) - MarginM;
        var southM = placed.Min(at => at.NorthM) - MarginM;
        return new OsmFrame
        {
            Lat0Deg = lat0,
            Lon0Deg = lon0,
            WestM = westM,
            SouthM = southM,
            WidthM = Math.Ceiling(placed.Max(at => at.EastM) + MarginM - westM),
            HeightM = Math.Ceiling(placed.Max(at => at.NorthM) + MarginM - southM),
            MarginM = MarginM,
        };
    }

    static void Report(OsmExtract extract, string into)
    {
        var roads = extract.Ways.Where(way => way.Carriageway is not null).ToArray();
        Console.WriteLine($"{into}  {new FileInfo(into).Length / 1024} KB  osm {extract.Source.OsmBase}  frame {extract.Frame.WidthM:F0} x {extract.Frame.HeightM:F0} m");
        Console.WriteLine($"nodes {extract.Nodes.Id.Length}  road ways {roads.Length}  roads drawn as areas {extract.Ways.Count(way => way.Tag("area") == "yes" && way.Tags.ContainsKey("highway"))}  " +
                          $"coastline ways {extract.Ways.Count(way => way.Tag("natural") == "coastline")}  road surfaces {extract.Areas.Length}  " +
                          $"tagged nodes {extract.NodeTags.Length}  relations {extract.Relations.Length}");
        Console.WriteLine("  " + string.Join("  ", roads.GroupBy(way => way.Tags["highway"]).OrderByDescending(group => group.Count()).Select(group => $"{group.Key} {group.Count()}")));
        Console.WriteLine("  relations: " + string.Join("  ", extract.Relations.GroupBy(relation => relation.Tags.GetValueOrDefault("restriction") ?? relation.Tags.GetValueOrDefault("type") ?? "?")
            .OrderByDescending(group => group.Count()).Select(group => $"{group.Key} {group.Count()}")));

        var lanes = roads.Select(way => way.Carriageway!).ToArray();
        Console.WriteLine($"  lanes: {lanes.Sum(carriageway => carriageway.Lanes.Length)} on {lanes.Length} ways; counts tagged on {lanes.Count(c => c.LanesFrom == OsmLanesFrom.Tagged)}, " +
                          $"widths tagged on {lanes.Count(c => c.WidthFrom != OsmWidthFrom.Assumed)}, placed off the middle on {lanes.Count(c => c.CentreOffsetM != 0f)}, " +
                          $"turns on {lanes.Count(c => c.Lanes.Any(lane => lane.Turn is not null))}, shared both ways on {lanes.Count(c => c.Lanes.Any(lane => lane.Way == OsmLaneWay.Both))}");

        var frame = extract.Frame;
        var projection = frame.Projection();
        var past = roads.Count(way => way.Nodes.Any(node =>
        {
            var (x, y) = frame.Place(projection, extract.Nodes, node);
            return x < frame.MarginM || y < frame.MarginM || x > frame.WidthM - frame.MarginM || y > frame.HeightM - frame.MarginM;
        }));
        Console.WriteLine($"  {past} road ways run on past the map's edge and are cut there");
    }
}
