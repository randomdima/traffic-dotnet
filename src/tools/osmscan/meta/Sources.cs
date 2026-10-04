using System.Globalization;
using System.Text.Json;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>What is asked of OSM beyond the roads</b>, one Overpass query a family, over the rectangle the survey's own
/// roads stand in and a margin, as OSM stood at the survey's own moment, each answer kept in
/// <c>towns/traced/&lt;Map&gt;/source/&lt;city&gt;-meta-&lt;family&gt;[-tile]-at-&lt;moment&gt;.json</c> (<see cref="Scan.Source"/>).
/// </summary>
/// <remarks>
/// <para>
/// The roads, their tagged nodes, their restriction and connectivity relations and the road surfaces are the
/// survey's already (<see cref="Scan"/>) and are not asked again: a layer is laid against the survey it describes.
/// </para>
/// <para>
/// <b>Every family is asked as of the survey's moment</b> (<c>[date:…]</c>, its <c>timestamp_osm_base</c>), so a
/// node the survey and a family both hold stands in one place with one set of tags however many hours apart they
/// were asked. An answer kept from a moment that is not the survey's is not read; a new survey asks afresh.
/// </para>
/// <para>
/// <b>A heavy family is asked a tile at a time</b>, so a busy server turning a query away costs one tile and not a
/// whole city's buildings, and no answer nears the server's memory allowance. An element astride two tiles is in
/// both answers, and read once (<see cref="Element.Read(IEnumerable{JsonDocument}, char?)"/>).
/// </para>
/// </remarks>
internal sealed class Sources
{
    /// <summary>
    /// Every query's settings, but its moment and its time. A tile is small enough for the server's own memory
    /// allowance, and asking for more only makes a busy server turn the query away sooner.
    /// </summary>
    const string Head = "[out:json]";

    /// <summary>
    /// The time a query is allowed, but a family that walks relations or every tagged node: as of a past moment the
    /// server reads each element's history, and the routes' members alone outrun the common allowance.
    /// </summary>
    const int TimeoutS = 300, LongTimeoutS = 900;

    /// <summary>
    /// The servers a family is asked of, and how often each. The main server turns away two queries in three
    /// when it is busy and then answers the third; the mirror the roads were first asked of has stopped answering
    /// at all, and a request to it hangs until the client gives up.
    /// </summary>
    static readonly string[] Servers = ["https://overpass-api.de/api/interpreter"];

    const int Tries = 30;

    /// <summary>
    /// How far past the survey's roads every family is asked for, so a building or a lot astride the map's edge is
    /// held whole.
    /// </summary>
    const double MarginDeg = 0.003;

    public const string OsmLicence = "© OpenStreetMap contributors, ODbL 1.0 — https://www.openstreetmap.org/copyright";

    readonly string _kept;
    readonly string _city;
    readonly bool _refetch;

    public Sources(string root, string city, OsmExtract extract, Plane plane, bool refetch)
    {
        (_kept, _city, _refetch) = (Scan.Source(root, extract.Name), city, refetch);
        Directory.CreateDirectory(_kept);
        Moment = extract.Source.OsmBase.Length > 0 ? extract.Source.OsmBase : throw new InvalidDataException("the survey says no OSM moment to ask its layers at");

        var (south, west, north, east) = (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
        var nodes = extract.Nodes;
        for (var node = 0; node < nodes.Id.Length; node++)
        {
            if (!plane.OnMap(plane.At(nodes.Lat[node], nodes.Lon[node]))) continue;

            (south, north) = (Math.Min(south, nodes.Lat[node]), Math.Max(north, nodes.Lat[node]));
            (west, east) = (Math.Min(west, nodes.Lon[node]), Math.Max(east, nodes.Lon[node]));
        }

        (SouthDeg, WestDeg) = ((south / OsmNodes.UnitsPerDegree) - MarginDeg, (west / OsmNodes.UnitsPerDegree) - MarginDeg);
        (NorthDeg, EastDeg) = ((north / OsmNodes.UnitsPerDegree) + MarginDeg, (east / OsmNodes.UnitsPerDegree) + MarginDeg);
    }

    public double SouthDeg { get; }

    public double WestDeg { get; }

    public double NorthDeg { get; }

    public double EastDeg { get; }

    /// <summary>The moment OSM is asked as of: the survey's own base.</summary>
    public string Moment { get; }

    /// <summary>Every source fetched, by what it is, with where it was asked and when its data stood.</summary>
    public List<SourceNote> Fetched { get; } = [];

    public string Kept(string name) => Path.Combine(_kept, $"{_city}-meta-{name}");

    /// <summary>Buildings and their parts: ways with their geometry, multipolygons with their members'.</summary>
    public List<JsonDocument> Buildings() => Ask("buildings", 3, 8, box =>
        $"(way[\"building\"]{box};way[\"building:part\"]{box};)->.w;.w out tags geom;" +
        $"(rel[\"building\"]{box};rel[\"building:part\"]{box};)->.r;.r out body geom;");

    /// <summary>Every way a pedestrian walks that is not a road, and every tagged node on them.</summary>
    public List<JsonDocument> Walk() => Ask("walk", 2, 4, box =>
        $"way[\"highway\"~\"^(footway|pedestrian|path|steps|cycleway|corridor|bridleway|platform|elevator)$\"]{box}->.w;" +
        ".w out body geom;node(w.w)(if:count_tags() > 0);out body;" +
        $"way[\"area:highway\"~\"^(footway|pedestrian|sidewalk|crossing|cycleway|traffic_island|path|steps)$\"]{box};out body geom;");

    /// <summary>Every surface that is a zone rather than a road, and every land use under the town.</summary>
    public List<JsonDocument> Zones() => Ask("zones", 2, 4, box =>
        "(" +
        $"nwr[\"amenity\"~\"^(parking|parking_space|parking_entrance|motorcycle_parking|fuel|car_wash|charging_station|bus_station|taxi|ferry_terminal|school|kindergarten|college|university|hospital|clinic|marketplace|townhall|police|fire_station|prison|grave_yard)$\"]{box};" +
        $"nwr[\"parking\"]{box};" +
        $"nwr[\"landuse\"]{box};" +
        $"nwr[\"leisure\"~\"^(park|garden|playground|pitch|stadium|sports_centre|track|nature_reserve|beach_resort|marina|dog_park|water_park|common|recreation_ground|golf_course)$\"]{box};" +
        $"nwr[\"place\"=\"square\"]{box};" +
        $"nwr[\"highway\"~\"^(pedestrian|services|rest_area|platform|footway)$\"][\"area\"=\"yes\"]{box};" +
        $"rel[\"highway\"][\"type\"=\"multipolygon\"]{box};" +
        $"nwr[\"man_made\"~\"^(bridge|pier|breakwater|groyne|quay|tunnel)$\"]{box};" +
        $"nwr[\"natural\"~\"^(water|beach|wood|scrub|grassland|heath|wetland|sand|bare_rock|cliff|bay|shingle)$\"]{box};" +
        $"nwr[\"waterway\"~\"^(riverbank|dock|canal|river|stream|ditch|drain)$\"]{box};" +
        $"nwr[\"aeroway\"~\"^(aerodrome|apron|runway|taxiway|terminal|helipad)$\"]{box};" +
        $"nwr[\"military\"]{box};nwr[\"industrial\"]{box};nwr[\"harbour\"]{box};" +
        $"nwr[\"tourism\"~\"^(zoo|theme_park|camp_site|caravan_site)$\"]{box};" +
        ");out body geom;");

    /// <summary>The town's districts and named places, for which part of it a thing stands in.</summary>
    public List<JsonDocument> Districts() => Ask("districts", 1, 1, box =>
        $"rel[\"boundary\"=\"administrative\"][\"admin_level\"~\"^(7|8|9|10)$\"]{box};out body geom;" +
        $"nwr[\"place\"~\"^(city|town|suburb|quarter|neighbourhood|village|hamlet|isolated_dwelling)$\"]{box};out tags center;");

    /// <summary>Public transport: its routes and their members, its track, and its stops and platforms.</summary>
    public List<JsonDocument> Transit() => Ask("transit", 1, 1, LongTimeoutS, box =>
        $"rel[\"type\"=\"route\"][\"route\"~\"^(tram|trolleybus|bus|minibus|share_taxi|train|light_rail|subway|ferry|funicular)$\"]{box}->.r;" +
        ".r out body;rel(br.r)[\"type\"=\"route_master\"];out body;" +
        $"way[\"railway\"~\"^(tram|rail|light_rail|narrow_gauge|funicular|subway|monorail|disused|abandoned|construction|preserved|miniature)$\"]{box}->.t;" +
        ".t out body geom;node(w.t)(if:count_tags() > 0);out body;" +
        $"(node[\"public_transport\"]{box};node[\"highway\"=\"bus_stop\"]{box};node[\"railway\"~\"^(tram_stop|station|halt|stop|platform|subway_entrance)$\"]{box};" +
        $"way[\"public_transport\"=\"platform\"]{box};way[\"railway\"=\"platform\"]{box};way[\"highway\"=\"platform\"]{box};);out body geom;" +
        $"rel[\"public_transport\"=\"stop_area\"]{box};out body;");

    /// <summary>
    /// Every node that controls or calms traffic, or marks a crossing, kerb, barrier or entrance — on a road or not —
    /// and every way that calms traffic or carries a sign.
    /// </summary>
    public List<JsonDocument> Control() => Ask("control", 1, 2, LongTimeoutS, box =>
        "(" +
        $"node[\"highway\"~\"^(traffic_signals|stop|give_way|crossing|speed_camera|traffic_mirror|turning_circle|turning_loop|mini_roundabout|motorway_junction|passing_place|milestone|elevator)$\"]{box};" +
        $"node[\"traffic_sign\"]{box};node[\"traffic_calming\"]{box};node[\"crossing\"]{box};node[\"railway\"~\"crossing\"]{box};" +
        $"node[\"kerb\"]{box};node[\"traffic_signals\"]{box};node[\"barrier\"]{box};node[\"entrance\"]{box};node[\"door\"]{box};" +
        ");out body;" +
        $"(way[\"traffic_calming\"]{box};way[\"traffic_sign\"]{box};);out body geom;" +
        $"rel[\"type\"=\"enforcement\"]{box};out body;");

    /// <summary>Every barrier OSM draws as a line: walls, fences, kerbs, rails and hedges a pedestrian cannot cross.</summary>
    public List<JsonDocument> Barriers() => Ask("barriers", 2, 4, box => $"way[\"barrier\"]{box};out body geom;");

    /// <summary>What stands along a street: trees, lamps, benches, shelters, cabinets, poles and their like.</summary>
    public List<JsonDocument> Furniture() => Ask("furniture", 2, 4, box =>
        "(" +
        $"node[\"natural\"~\"^(tree|shrub|rock|stone)$\"]{box};way[\"natural\"=\"tree_row\"]{box};node[\"highway\"=\"street_lamp\"]{box};" +
        $"node[\"amenity\"~\"^(bench|waste_basket|shelter|telephone|post_box|vending_machine|drinking_water|toilets|recycling|fountain|clock|bicycle_rental|atm|parcel_locker|bicycle_parking)$\"]{box};" +
        $"node[\"man_made\"~\"^(street_cabinet|utility_pole|mast|flagpole|surveillance|monitoring_station|manhole|tower|chimney)$\"]{box};" +
        $"node[\"power\"~\"^(pole|tower|transformer)$\"]{box};node[\"advertising\"]{box};node[\"emergency\"=\"fire_hydrant\"]{box};" +
        $"node[\"leisure\"~\"^(picnic_table|outdoor_seating)$\"]{box};node[\"tourism\"~\"^(artwork|information)$\"]{box};" +
        ");out body geom;");

    /// <summary>Every place a trip may be made to — shop, amenity, office, sight — each at one point.</summary>
    public List<JsonDocument> Places() => Ask("places", 2, 4, box =>
        "(" +
        $"nwr[\"shop\"]{box};nwr[\"amenity\"]{box};nwr[\"office\"]{box};nwr[\"tourism\"]{box};nwr[\"craft\"]{box};" +
        $"nwr[\"healthcare\"]{box};nwr[\"leisure\"]{box};nwr[\"historic\"]{box};nwr[\"club\"]{box};" +
        ");out tags center;");

    /// <summary>Roads not yet built, given up or only proposed, which a later survey may find as roads.</summary>
    public List<JsonDocument> Unbuilt() => Ask("unbuilt", 1, 1, box =>
        $"way[\"highway\"~\"^(construction|proposed|abandoned|disused|razed)$\"]{box};out body geom;");

    /// <summary>
    /// Every way for traffic or feet, by id, with its version and when it was last edited as of the moment — and
    /// nothing else, so who edited it is never asked for.
    /// </summary>
    public List<JsonDocument> Edits() => Ask("edits", 1, 2, box =>
        $"way[\"highway\"]{box};convert way ::id=id(),version=version(),edited=timestamp();out;");

    List<JsonDocument> Ask(string family, int columns, int rows, Func<string, string> query) => Ask(family, columns, rows, TimeoutS, query);

    /// <summary>One family asked over the rectangle cut into columns and rows of tiles, each tile's answer kept.</summary>
    List<JsonDocument> Ask(string family, int columns, int rows, int timeoutS, Func<string, string> query)
    {
        var answers = new List<JsonDocument>(columns * rows);
        var (stepLat, stepLon) = ((NorthDeg - SouthDeg) / rows, (EastDeg - WestDeg) / columns);
        var head = $"{Head}[timeout:{timeoutS}][date:\"{Moment}\"];";
        var stamp = Moment.Replace("-", "").Replace(":", "");
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var box = string.Create(CultureInfo.InvariantCulture,
                    $"({SouthDeg + (row * stepLat):F7},{WestDeg + (column * stepLon):F7},{SouthDeg + ((row + 1) * stepLat):F7},{WestDeg + ((column + 1) * stepLon):F7})");
                var name = columns * rows == 1 ? family : $"{family}-{(row * columns) + column}";
                var kept = Kept($"{name}-at-{stamp}.json");
                var answer = Overpass.Answer(kept, head + query(box), _refetch, Tries, Servers);
                answers.Add(answer);
                Fetched.Add(new SourceNote
                {
                    Name = $"osm-{family}",
                    Url = "https://overpass-api.de/api/interpreter",
                    Query = head + query(box),
                    StandsAt = Moment,
                    Licence = OsmLicence,
                    KeptAt = Path.GetFileName(kept),
                });
            }
        }

        return answers;
    }

    /// <summary>
    /// A file off any other source, kept beside the Overpass answers — whole, or only what a <paramref name="copy"/>
    /// keeps of it — with when its data stood as the file itself says where it says, else as fetched.
    /// </summary>
    public string File(string name, string url, string licence, Func<string, string>? standsAt = null, Action<Stream, Stream>? copy = null)
    {
        var kept = Http.Kept(Kept(name), url, _refetch, copy);
        Fetched.Add(new SourceNote
        {
            Name = name,
            Url = url,
            StandsAt = standsAt?.Invoke(kept) ?? $"as fetched {System.IO.File.GetLastWriteTimeUtc(kept).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}",
            Licence = licence,
            KeptAt = Path.GetFileName(kept),
        });
        return kept;
    }

    /// <summary>
    /// A file made of pieces asked of a source too large or too fenced to fetch whole, kept beside the Overpass
    /// answers, with when its data stood as the file made says.
    /// </summary>
    public string Made(string name, string url, string licence, Func<string, string> standsAt, Action<Stream> make)
    {
        var kept = Http.Made(Kept(name), _refetch, make);
        Fetched.Add(new SourceNote { Name = name, Url = url, StandsAt = standsAt(kept), Licence = licence, KeptAt = Path.GetFileName(kept) });
        return kept;
    }

    /// <summary>The rectangle as west, south, east, north, the order the OSM API and most others take it in.</summary>
    public string Bbox() => string.Create(CultureInfo.InvariantCulture, $"{WestDeg:F7},{SouthDeg:F7},{EastDeg:F7},{NorthDeg:F7}");
}

/// <summary>One source a layer was read off: what, where, when its data stood, on what licence, and where it is kept.</summary>
internal sealed class SourceNote
{
    public required string Name { get; init; }

    public required string Url { get; init; }

    public string? Query { get; init; }

    public required string StandsAt { get; init; }

    public required string Licence { get; init; }

    /// <summary>The file in the map's sources (<see cref="Scan.Source"/>) the answer is kept in.</summary>
    public required string KeptAt { get; init; }
}
