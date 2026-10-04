using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Whether an enriched survey is one source to reference</b>: its layers read back off their files as a reader
/// reads them, and asked three things — does every id one layer gives for another resolve, and is every fact two
/// layers both carry the same in each (<see cref="Integrity"/>); was every OSM layer read off the survey's own moment
/// (<see cref="Snapshot"/>); and where the sources describe the same things, how far do they agree
/// (<see cref="Agreement"/>). Written to <c>consistency.md</c> beside the layers.
/// </summary>
/// <remarks>
/// <b>A broken reference, two layers disagreeing, or OSM read at two moments is the enrichment's own defect</b> and
/// fails the run. Two sources disagreeing — a timetable's stop set 30 m from OSM's, a footprint traced off older
/// imagery — is the sources', and a reading: the layers say which they took, and the quality layer names each case.
/// </remarks>
internal static class Audit
{
    public static int Run(string root, string map)
    {
        var clock = Stopwatch.StartNew();
        var data = new Dataset(root, map);
        var integrity = Integrity.Check(data);
        var snapshot = Snapshot.Check(data);
        var agreement = Agreement.Measure(data);
        var broken = integrity.Concat(snapshot).Where(finding => finding.Failed > 0).ToArray();

        var page = new StringBuilder();
        page.AppendLine($"# {map} — consistency").AppendLine();
        page.AppendLine("Written by `qq osm --meta-check` off the layers beside it, read back as a reader reads them. Generated; do not edit.").AppendLine();
        page.AppendLine(broken.Length == 0
            ? $"**One source: yes.** Every reference resolves, every fact two layers carry is the same in each, and every OSM layer stands at the survey's own moment ({data.Survey.Source.OsmBase})."
            : $"**One source: no.** {broken.Length} of {integrity.Count + snapshot.Count} checks broken: {string.Join("; ", broken.Select(finding => finding.Question))}.");
        page.AppendLine();

        Table(page, "## The layers hold together", "Every id one layer gives for another is an item of that other, and every fact two layers both carry is the same in each.", integrity);
        Table(page, "## One snapshot of OSM", "Every OSM layer read off one moment, the survey's: no node in two places, no element with two sets of tags.", snapshot);

        page.AppendLine("## Where the sources agree").AppendLine();
        page.AppendLine("Readings, not checks: where two sources describe the same thing, how far they agree, and which one the layers take. The quality layer names every disagreement by its elements.").AppendLine();
        foreach (var finding in agreement)
        {
            page.AppendLine($"### {finding.Question}").AppendLine();
            foreach (var reading in finding.Readings) page.AppendLine($"- {reading}");
            if (finding.Examples.Count > 0) page.AppendLine($"- e.g. {string.Join("; ", finding.Examples)}");
            page.AppendLine();
        }

        File.WriteAllText(Path.Combine(data.Folder, "consistency.md"), page.ToString());
        foreach (var finding in integrity.Concat(snapshot))
        {
            Console.WriteLine($"  {(finding.Failed == 0 ? "ok    " : "BROKEN")} {finding.Question}: {finding.Failed}/{finding.Asked}{(finding.Examples.Count > 0 ? $"  e.g. {finding.Examples[0]}" : "")}");
        }

        Console.WriteLine($"{Path.GetRelativePath(root, Path.Combine(data.Folder, "consistency.md"))}  {(broken.Length == 0 ? "one source" : $"{broken.Length} checks broken")}, {agreement.Count} readings in {clock.Elapsed.TotalSeconds:F0} s");
        return broken.Length == 0 ? 0 : 1;
    }

    static void Table(StringBuilder page, string heading, string about, List<Finding> findings)
    {
        page.AppendLine(heading).AppendLine().AppendLine(about).AppendLine();
        page.AppendLine("| Check | Asked | Broken | e.g. |").AppendLine("|---|---:|---:|---|");
        foreach (var finding in findings)
        {
            page.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"| {finding.Question} | {finding.Asked:N0} | {(finding.Failed == 0 ? "0" : $"**{finding.Failed:N0}**")} | {string.Join("; ", finding.Examples)} |"));
        }

        foreach (var reading in findings.SelectMany(finding => finding.Readings)) page.AppendLine().Append("- ").AppendLine(reading);
        page.AppendLine();
    }
}

/// <summary>One question asked of the data: of how many items, how many failed it, a few that did, and what was read.</summary>
internal sealed class Finding(string question)
{
    const int Shown = 5;

    public string Question { get; } = question;

    public int Asked { get; private set; }

    public int Failed { get; private set; }

    public List<string> Examples { get; } = [];

    public List<string> Readings { get; } = [];

    /// <summary>One item asked: held, or failed with what shows it.</summary>
    public void Ask(bool held, Func<string> example)
    {
        Asked++;
        if (held) return;

        Failed++;
        if (Examples.Count < Shown) Examples.Add(example());
    }

    public Finding Read(string reading)
    {
        Readings.Add(reading);
        return this;
    }
}

/// <summary>An enriched survey read back off its files: the survey, the manifest, every layer, and the sources kept.</summary>
internal sealed class Dataset
{
    public Dataset(string root, string map)
    {
        Root = root;
        Folder = Path.Combine(root, "towns", "traced", map);
        var surveyAt = Path.Combine(root, "towns", "traced", $"{map}.json");
        Survey = JsonSerializer.Deserialize(File.ReadAllBytes(surveyAt), OsmExtractJson.Default.OsmExtract) ?? throw new InvalidDataException($"{map}: no extract");
        Plane = new Plane(Survey.Frame);
        City = Scan.Cities.FirstOrDefault(city => city.Value.Map == map).Key ?? throw new ArgumentException($"no city is traced as {map}");
        using (var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder, "manifest.json")))) Manifest = manifest.RootElement.Clone();

        for (var node = 0; node < Survey.Nodes.Id.Length; node++) NodeIndex[Survey.Nodes.Id[node]] = node;
        foreach (var way in Survey.Ways.Where(way => way.Carriageway is not null)) SurveyRoads[way.Id] = way;
        foreach (var tagged in Survey.NodeTags) NodeTags[tagged.Node] = tagged.Tags;
        if (Corrections.Read(Path.Combine(root, "towns", "traced", $"{map}.osc"), root) is { } corrections)
        {
            CorrectedNodes = [.. corrections.Nodes.Keys];
            CorrectedWays = [.. corrections.Ways.Keys, .. corrections.DeletedWays];
        }

        Roads = Layer<RoadRecord>("roads");
        Junctions = Layer<JunctionRecord>("junctions");
        Crossings = Layer<CrossingRecord>("crossings");
        Walk = Layer<WalkRecord>("walk");
        Zones = Layer<ZoneRecord>("zones");
        Districts = Layer<ZoneRecord>("districts");
        Buildings = Layer<BuildingRecord>("buildings");
        Levels = Layer<LevelRecord>("levels");
        Overpasses = Layer<OverpassRecord>("overpasses");
        Routes = Layer<RouteRecord>("routes");
        Stops = Layer<StopRecord>("stops");
        Tracks = Layer<TrackRecord>("tracks");
        Places = Layer<PointRecord>("places");
        Furniture = Layer<PointRecord>("furniture");
        Barriers = Layer<PointRecord>("barriers");
        Unbuilt = Layer<PointRecord>("unbuilt");
        Detected = Layer<DetectedRecord>("detected");
        Seen = Layer<SeenRecord>("seen");
        Unmapped = Layer<UnmappedRecord>("unmapped");
        Quality = Layer<FlagRecord>("quality");
    }

    public string Root { get; }

    public string Folder { get; }

    public string City { get; }

    public OsmExtract Survey { get; }

    public Plane Plane { get; }

    public JsonElement Manifest { get; }

    public Dictionary<long, int> NodeIndex { get; } = [];

    /// <summary>The survey's road ways — those it gave lanes — by OSM id.</summary>
    public Dictionary<long, OsmWay> SurveyRoads { get; } = [];

    public Dictionary<int, Dictionary<string, string>> NodeTags { get; } = [];

    /// <summary>The nodes the survey's own corrections place or tag otherwise than OSM (<see cref="Corrections"/>), by OSM id.</summary>
    public HashSet<long> CorrectedNodes { get; } = [];

    /// <summary>The road ways the survey's own corrections lay otherwise than OSM, by OSM id.</summary>
    public HashSet<long> CorrectedWays { get; } = [];

    /// <summary>Each layer's head as written — the count it says, the items it holds — or null where there is no file.</summary>
    public Dictionary<string, (int Said, int Held)?> Heads { get; } = [];

    public List<RoadRecord> Roads { get; }

    public List<JunctionRecord> Junctions { get; }

    public List<CrossingRecord> Crossings { get; }

    public List<WalkRecord> Walk { get; }

    public List<ZoneRecord> Zones { get; }

    public List<ZoneRecord> Districts { get; }

    public List<BuildingRecord> Buildings { get; }

    public List<LevelRecord> Levels { get; }

    public List<OverpassRecord> Overpasses { get; }

    public List<RouteRecord> Routes { get; }

    public List<StopRecord> Stops { get; }

    public List<TrackRecord> Tracks { get; }

    public List<PointRecord> Places { get; }

    public List<PointRecord> Furniture { get; }

    public List<PointRecord> Barriers { get; }

    public List<PointRecord> Unbuilt { get; }

    public List<DetectedRecord> Detected { get; }

    public List<SeenRecord> Seen { get; }

    public List<UnmappedRecord> Unmapped { get; }

    public List<FlagRecord> Quality { get; }

    /// <summary>Every source the manifest names whose name passes a filter: its name, when it stands, and the paths it is kept at.</summary>
    public IEnumerable<(string Name, string StandsAt, string[] KeptAt)> Sources(Func<string, bool> which)
    {
        foreach (var source in Manifest.GetProperty("sources").EnumerateArray())
        {
            var name = source.GetProperty("name").GetString()!;
            if (!which(name)) continue;

            yield return (name, source.GetProperty("standsAt").GetString() ?? "",
                [.. source.GetProperty("kept").EnumerateArray().Select(kept => Path.Combine(Scan.Source(Root, Survey.Name), kept.GetString()!))]);
        }
    }

    /// <summary>Items by their key, the first of any a layer holds twice — which is the uniqueness check's to report, not a reader's to fall over.</summary>
    public static Dictionary<TKey, T> Index<T, TKey>(IEnumerable<T> items, Func<T, TKey> key) where TKey : notnull
    {
        var index = new Dictionary<TKey, T>();
        foreach (var item in items) index.TryAdd(key(item), item);
        return index;
    }

    /// <summary>A survey node's place as lat, lon in 1e-7°.</summary>
    public int[] At(long node) => NodeIndex.TryGetValue(node, out var at) ? [Survey.Nodes.Lat[at], Survey.Nodes.Lon[at]] : [];

    List<T> Layer<T>(string name)
    {
        var file = Layers.Read<T>(Folder, name);
        Heads[name] = file is null ? null : (file.Count, file.Items.Count);
        return file?.Items ?? [];
    }
}
