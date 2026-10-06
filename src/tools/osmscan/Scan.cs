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
/// Three things are added, all OSM's conventions and none the engine's: <b>each road way's lanes</b> as its
/// tags mean them (<see cref="OsmCarriageway.Read"/>), <b>where a car may turn</b> as the restriction and
/// connectivity relations mean it (<see cref="OsmTurns.Read"/>), and <b>the frame</b> every node is read into
/// metres by (<see cref="OsmFrame"/>) — Transverse Mercator about the middle of the city's roads, the map their
/// extent and <see cref="MarginM"/>.
/// </para>
/// <para>
/// <b>Where OSM is wrong about the place, the survey's own corrections put it right</b> (<see cref="Corrections"/>,
/// <c>towns/traced/&lt;Map&gt;.osc</c>): applied over OSM's answer before anything is read off it, so the lanes and
/// turns are read off the corrected ways as off any other.
/// </para>
/// <para>A node two ways place differently is refused rather than averaged.</para>
/// </remarks>
internal static class Scan
{
    internal sealed record City(string Map, long Relation, string Description);

    /// <summary>A way as OSM's answer holds it, before it is read: its id, its tags and its nodes' OSM ids.</summary>
    internal sealed record RawWay(long Id, Dictionary<string, string> Tags, long[] Nodes);

    internal static readonly Dictionary<string, City> Cities = new()
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

    /// <summary>
    /// <b>Where every answer a source gave for a map is kept, exactly as it came</b>: OSM's and every other source's,
    /// never edited — the place as it stood online, which the survey, the layers and the map are all read off.
    /// </summary>
    public static string Source(string root, string map) => Path.Combine(root, "towns", "traced", map, "source");

    /// <summary>The map the engine reads (<see cref="TownMap"/>): every map's file is <c>towns/&lt;Map&gt;.map</c>, a traced one's too.</summary>
    public static string MapFile(string root, string map) => Path.Combine(root, "towns", $"{map}.map");

    public static int Run(string root, string city, bool refetch)
    {
        if (!Cities.TryGetValue(city, out var place)) throw new ArgumentException($"no city '{city}': {string.Join(", ", Cities.Keys)}");

        var kept = Source(root, place.Map);
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
        List<RawWay> roadRaw = [.. roadWays.Select(Raw)];
        var nodeTags = Elements(roads, "node")
            .Where(node => node.TryGetProperty("tags", out _))
            .ToDictionary(node => node.GetProperty("id").GetInt64(), Tags);
        var corrections = Corrections.Read(Path.Combine(root, "towns", "traced", $"{place.Map}.osc"), root);
        corrections?.Apply(units, roadRaw, nodeTags);
        List<RawWay> coastRaw = [.. coastWays.Select(Raw)];
        List<RawWay> areaRaw = [.. areaWays.Select(Raw)];

        var passed = roadRaw.Concat(coastRaw).Concat(areaRaw).SelectMany(way => way.Nodes).ToHashSet();
        var ids = units.Keys.Where(passed.Contains).Order().ToArray();
        var index = new Dictionary<long, int>(ids.Length);
        for (var at = 0; at < ids.Length; at++) index[ids[at]] = at;

        var nodes = new OsmNodes
        {
            Id = ids,
            Lat = [.. ids.Select(id => units[id].Lat)],
            Lon = [.. ids.Select(id => units[id].Lon)],
        };

        OsmWay[] ways = [.. roadRaw.Concat(coastRaw).Select(way => Way(way, index, lanes: true)).OrderBy(way => way.Id)];
        OsmRelation[] relations =
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
        ];

        var unread = new List<(long Relation, string Why)>();
        var turns = OsmTurns.Read(relations, ways.Where(way => way.Carriageway is not null).ToDictionary(way => way.Id), index, unread);

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
                .. nodeTags
                    .Where(tagged => index.ContainsKey(tagged.Key))
                    .OrderBy(tagged => tagged.Key)
                    .Select(tagged => new OsmNodeTags { Node = index[tagged.Key], Tags = tagged.Value }),
            ],
            Frame = Framed(ownWays),
            Ways = ways,
            Areas = [.. areaRaw.Select(way => Way(way, index, lanes: false)).OrderBy(way => way.Id)],
            Relations = relations,
            Turns = turns,
        };
        extract.Check(place.Map);

        var into = Path.Combine(root, "towns", "traced", $"{place.Map}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(into)!);
        var json = new OsmExtractJson(new JsonSerializerOptions(OsmExtractJson.Default.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(into, JsonSerializer.Serialize(extract, json.OsmExtract) + "\n");

        Report(extract, Path.GetRelativePath(root, into), unread, corrections);
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

    static RawWay Raw(JsonElement way) =>
        new(way.GetProperty("id").GetInt64(), Tags(way), [.. way.GetProperty("nodes").EnumerateArray().Select(node => node.GetInt64())]);

    static OsmWay Way(RawWay way, Dictionary<long, int> index, bool lanes) => new()
    {
        Id = way.Id,
        Tags = way.Tags,
        Nodes = [.. way.Nodes.Select(node => index[node])],
        Carriageway = lanes ? OsmCarriageway.Read(way.Tags) : null,
    };

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

    static void Report(OsmExtract extract, string into, List<(long Relation, string Why)> unread, Corrections? corrections)
    {
        var roads = extract.Ways.Where(way => way.Carriageway is not null).ToArray();
        Console.WriteLine($"{into}  {new FileInfo(into).Length / 1024} KB  osm {extract.Source.OsmBase}  frame {extract.Frame.WidthM:F0} x {extract.Frame.HeightM:F0} m");
        if (corrections is not null)
        {
            Console.WriteLine($"  corrected off {corrections.File}: {corrections.Ways.Keys.Count(id => id > 0)} ways replaced, " +
                              $"{corrections.Ways.Keys.Count(id => id < 0)} added, {corrections.DeletedWays.Count} deleted, " +
                              $"{corrections.Nodes.Keys.Count(id => id > 0)} nodes moved or retagged, {corrections.Nodes.Keys.Count(id => id < 0)} added");
        }

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

        var turned = lanes.SelectMany(carriageway => carriageway.Lanes).Where(lane => lane.Turn is not null).ToArray();
        var unknown = turned.SelectMany(lane => lane.Turn!.Split(';')).Where(word => OsmTurns.Arrow(word.Trim()) is null)
            .GroupBy(word => word).OrderByDescending(group => group.Count()).Select(group => $"'{group.Key}' {group.Count()}").ToArray();
        Console.WriteLine($"  arrows on {turned.Count(lane => lane.Arrows != OsmArrows.None)} of {turned.Length} lanes with a turn entry" +
                          (unknown.Length > 0 ? $"; words OSM does not define: {string.Join(", ", unknown)}" : ""));

        var restrictions = extract.Turns.Restrictions;
        Console.WriteLine($"  turns: {restrictions.Length} restricted off {restrictions.Select(turn => turn.Relation).Distinct().Count()} relations " +
                          $"({restrictions.Count(turn => !turn.Only)} forbidden, {restrictions.Count(turn => turn.Only)} the only one allowed), " +
                          $"{extract.Turns.LaneLinks.Length} lane links off {extract.Turns.LaneLinks.Select(link => link.Relation).Distinct().Count()} relations");
        if (unread.Count > 0)
        {
            Console.WriteLine($"  {unread.Count} relations not read: " + string.Join("  ", unread.GroupBy(entry => entry.Why).OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key} {group.Count()} ({string.Join(", ", group.Take(3).Select(entry => entry.Relation))}{(group.Count() > 3 ? ", …" : "")})")));
        }

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
