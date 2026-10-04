using System.Diagnostics;
using System.Text.Json;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>A traced map's survey enriched with everything else known about the place</b>, written beside it into
/// <c>towns/traced/&lt;Map&gt;/</c>, one file a layer: what stands on the ground (buildings, zones, furniture,
/// places), how people walk it, how its junctions are controlled and turned, its crossings, its levels and
/// heights, its public transport, and what its sources disagree on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Data, and read by the engine only as imported</b> (<see cref="Import"/>): the folder is beside the map and not a
/// map, so the map list and the browser head never see it, and what a traced town is laid with is imported out of it
/// into the map's own file. A layer is never laid into a map that is there: the map is the master once imported. A
/// layer is laid against the survey it describes — a road by the
/// OSM id it has there, a node by its id — and in OSM's own units of 1e-7°, so it stays true whatever frame a
/// later survey is read in.
/// </para>
/// <para>
/// <b>Every figure is either OSM's or measured.</b> What a tag says is kept as the tag says it; what is worked out
/// — a junction's control, a sidewalk's road, a road's zone, a crossing's place along its road — names what it
/// was worked out from, so a reader can weigh it.
/// </para>
/// </remarks>
internal static class Enrichment
{
    public static int Run(string root, string city, bool refetch, bool fetchOnly, string[] skip)
    {
        if (!Scan.Cities.TryGetValue(city, out var place)) throw new ArgumentException($"no city '{city}': {string.Join(", ", Scan.Cities.Keys)}");

        var surveyAt = Path.Combine(root, "towns", "traced", $"{place.Map}.json");
        if (!File.Exists(surveyAt)) throw new InvalidOperationException($"{place.Map}: no survey at {surveyAt}; `qq osm` writes it");

        var extract = JsonSerializer.Deserialize(File.ReadAllBytes(surveyAt), OsmExtractJson.Default.OsmExtract)
                      ?? throw new InvalidDataException($"{place.Map}: no extract");
        var plane = new Plane(extract.Frame);
        var sources = new Sources(root, city, extract, plane, refetch);
        var clock = Stopwatch.StartNew();

        var fetched = Fetch(sources, skip);
        Console.WriteLine($"fetched {fetched.Count} sources in {clock.Elapsed.TotalSeconds:F0} s");
        if (fetchOnly) return 0;

        // The folder is written whole each run, so a layer no longer laid leaves no file behind to be read as current.
        var into = Path.Combine(root, "towns", "traced", place.Map);
        Directory.CreateDirectory(into);
        foreach (var stale in Directory.EnumerateFiles(into).Where(file => Path.GetExtension(file) is ".json" or ".md")) File.Delete(stale);

        var town = new Town(extract, plane, fetched, sources);
        var written = Layers.Lay(town, into);
        Report.Write(town, written, into, Path.GetRelativePath(root, into), clock.Elapsed);
        var audited = Audit.Run(root, place.Map);
        if (audited == 0) Console.WriteLine($"the layers hold; the map is the master and is not touched — `qq osm --import --force` imports it off them again, losing its edits");
        return audited;
    }

    /// <summary>Every source, each kept or fetched; one that cannot be had is said so and left out.</summary>
    static Fetched Fetch(Sources sources, string[] skip)
    {
        var fetched = new Fetched();
        Get("buildings", () => fetched.Buildings = sources.Buildings());
        Get("walk", () => fetched.Walk = sources.Walk());
        Get("zones", () => fetched.Zones = sources.Zones());
        Get("districts", () => fetched.Districts = sources.Districts());
        Get("transit", () => fetched.Transit = sources.Transit());
        Get("control", () => fetched.Control = sources.Control());
        Get("barriers", () => fetched.Barriers = sources.Barriers());
        Get("furniture", () => fetched.Furniture = sources.Furniture());
        Get("places", () => fetched.Places = sources.Places());
        Get("unbuilt", () => fetched.Unbuilt = sources.Unbuilt());
        Get("edits", () => fetched.Edits = sources.Edits());
        Get("notes", () => fetched.NotesAt = Notes.Fetch(sources));
        Get("ml-buildings", () => fetched.MlBuildingsAt = MlBuildings.Fetch(sources));
        Get("ml-roads", () => fetched.MlRoadsAt = MlRoads.Fetch(sources));
        Get("dem", () => fetched.DemAt = Dem.Fetch(sources));
        Get("terrain", () => fetched.TerrainAt = Dem.FetchTerrain(sources));
        Get("osmose", () => fetched.OsmoseAt = Osmose.Fetch(sources));
        Get("mapillary", () => fetched.MapillaryAt = Mapillary.Fetch(sources));
        Get("gtfs", () => fetched.GtfsAt = Gtfs.Fetch(sources));
        return fetched;

        void Get(string name, Action fetch)
        {
            if (skip.Contains(name))
            {
                fetched.Missing[name] = "skipped";
                return;
            }

            var clock = Stopwatch.StartNew();
            try
            {
                fetch();
                fetched.Count++;
                Console.WriteLine($"  {name,-13} {clock.Elapsed.TotalSeconds,5:F0} s");
            }
            catch (Exception failure) when (failure is InvalidOperationException or HttpRequestException or IOException or InvalidDataException)
            {
                fetched.Missing[name] = failure.Message;
                Console.WriteLine($"  {name,-13} not had: {failure.Message}");
            }
        }
    }
}

/// <summary>Every source as fetched; a family that could not be had is null and named in <see cref="Missing"/>.</summary>
internal sealed class Fetched
{
    public int Count { get; set; }

    public Dictionary<string, string> Missing { get; } = [];

    public List<JsonDocument>? Buildings { get; set; }

    public List<JsonDocument>? Walk { get; set; }

    public List<JsonDocument>? Zones { get; set; }

    public List<JsonDocument>? Districts { get; set; }

    public List<JsonDocument>? Transit { get; set; }

    public List<JsonDocument>? Control { get; set; }

    public List<JsonDocument>? Barriers { get; set; }

    public List<JsonDocument>? Furniture { get; set; }

    public List<JsonDocument>? Places { get; set; }

    public List<JsonDocument>? Unbuilt { get; set; }

    public List<JsonDocument>? Edits { get; set; }

    public string? NotesAt { get; set; }

    public string[]? MlBuildingsAt { get; set; }

    public string? MlRoadsAt { get; set; }

    public string? DemAt { get; set; }

    public string? TerrainAt { get; set; }

    public string[]? OsmoseAt { get; set; }

    public string? MapillaryAt { get; set; }

    public string? GtfsAt { get; set; }
}
