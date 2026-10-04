using System.Collections;
using System.Globalization;
using System.Text;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>What the enrichment found and what it could not</b>: <c>manifest.json</c>, naming every layer and every source
/// with where it was asked, when its data stood and its licence; and <c>coverage.md</c>, each layer's figures read
/// off it as written — both beside the layers.
/// </summary>
internal static class Report
{
    /// <summary>What no source here could give, and why, for a reader weighing the layers.</summary>
    static readonly string[] Unreachable =
    [
        "Odesa City Council's own geodata — signals and their timings, crossings, parking zones, the road register — is not published in a machine-readable form that can be reached: its organisation on data.gov.ua holds no datasets, its own site sits behind a browser check, and the cadastral map has been closed since 2022.",
        "Where public transport runs live, and so how fast a street is driven, is held by the operator and not published; the timetable is the only service figure.",
        "Microsoft's building model gives no heights in Ukraine; a building's height is OSM's or none.",
        "Neither height model is finer than 30 m: a ramp, a kerb or a grade over less than 60 m is not in them.",
        "A width read off imagery is the paved surface kerb to kerb, parking and gutters in it; how many lanes it is painted for, and how wide each, no open source gives.",
    ];

    public static void Write(Town town, List<Written> written, string into, string shown, TimeSpan took)
    {
        var sources = town.Sources.Fetched.GroupBy(source => source.Name).Select(group => new
        {
            name = group.Key,
            url = group.First().Url,
            standsAt = group.Min(source => source.StandsAt) == group.Max(source => source.StandsAt)
                ? group.First().StandsAt
                : $"{group.Min(source => source.StandsAt)} … {group.Max(source => source.StandsAt)}",
            licence = group.First().Licence,
            kept = group.Select(source => source.KeptAt).ToArray(),
        }).ToArray();

        var manifest = new
        {
            map = town.Extract.Name,
            survey = $"towns/traced/{town.Extract.Name}.json",
            surveyOsmBase = town.Extract.Source.OsmBase,
            osmAsOf = town.Sources.Moment,
            written =DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            places = "every lat, lon is in OSM's integer units of 1e-7 degrees; read into the map's metres by the survey's own frame",
            frame = town.Extract.Frame,
            rectangleDeg = new { south = town.Sources.SouthDeg, west = town.Sources.WestDeg, north = town.Sources.NorthDeg, east = town.Sources.EastDeg },
            sources,
            missing = town.Fetched.Missing,
            layers = written.Select(layer => new { layer.Layer, file = layer.Layer + ".json", layer.About, layer.ReadOff, layer.Count, layer.Bytes }).ToArray(),
        };
        File.WriteAllText(Path.Combine(into, "manifest.json"), Layers.Indented(manifest) + "\n");

        var page = new StringBuilder();
        page.AppendLine($"# {town.Extract.Name} — enriched survey").AppendLine();
        page.AppendLine($"Written by `qq osm --meta` beside `{manifest.survey}`. Every OSM layer is read as OSM stood at the survey's own moment, {manifest.osmAsOf}. ");
        page.AppendLine("Every place is lat, lon in OSM's 1e-7°; every road is the survey's way by its OSM id. Whether the layers hold together and how far their sources agree is `consistency.md`; `qq meta` answers questions of them by id, place, name or filter. Generated; do not edit.").AppendLine();
        page.AppendLine("## Sources").AppendLine();
        page.AppendLine("| Source | Data stands at | Licence |").AppendLine("|---|---|---|");
        foreach (var source in sources) page.AppendLine($"| [{source.name}]({source.url.Split('?')[0]}) | {source.standsAt} | {source.licence} |");
        if (town.Fetched.Missing.Count > 0)
        {
            page.AppendLine().AppendLine("Not had this run:").AppendLine();
            foreach (var (name, why) in town.Fetched.Missing) page.AppendLine($"- **{name}** — {why}");
        }

        page.AppendLine().AppendLine("Beyond reach of any source here:").AppendLine();
        foreach (var line in Unreachable) page.AppendLine($"- {line}");

        page.AppendLine().AppendLine("## Layers").AppendLine();
        foreach (var layer in written)
        {
            page.AppendLine($"### {layer.Layer} — {layer.Count:N0} items, {layer.Bytes / 1024:N0} KB").AppendLine();
            page.AppendLine(layer.About).AppendLine();
            if (layer.Summary is null) continue;

            if (layer.Summary is IDictionary counts)
            {
                foreach (var key in counts.Keys) page.AppendLine($"- **{key}**: {Said(counts[key])}");
            }
            else
            {
                foreach (var property in layer.Summary.GetType().GetProperties()) page.AppendLine($"- **{property.Name}**: {Said(property.GetValue(layer.Summary))}");
            }

            page.AppendLine();
        }

        File.WriteAllText(Path.Combine(into, "coverage.md"), page.ToString());

        foreach (var layer in written) Console.WriteLine($"  {layer.Layer,-12} {layer.Count,8} items  {layer.Bytes / 1024,7} KB");
        Console.WriteLine($"{shown}  {written.Count} layers, manifest.json and coverage.md in {took.TotalSeconds:F0} s");
    }

    static string Said(object? value) => value switch
    {
        null => "—",
        string text => text,
        IDictionary dictionary => string.Join(", ", dictionary.Keys.Cast<object>().Select(key => $"{key} {Said(dictionary[key])}")),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
