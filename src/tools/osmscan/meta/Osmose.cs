using System.Text.Json;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>What OSM's quality checker finds wrong or missing over the map</b> (Osmose), item by item: overlaps,
/// unconnected roads, broken restrictions, crossings and signals — and traffic signs Mapillary's cameras saw that
/// OSM does not map, with the tags they would add.
/// </summary>
/// <remarks>
/// The API answers at most 10 000 issues a call, which the map's unfiltered issues exceed, so each item is asked
/// for alone. Who mapped an element is in the answer and is not kept.
/// </remarks>
internal static class Osmose
{
    public const string Licence = "Osmose QA (osmose.openstreetmap.fr) over © OpenStreetMap contributors (ODbL); traffic signs detected by Mapillary (CC BY-SA 4.0)";

    /// <summary>
    /// The items read: Mapillary's unmapped signs (8300), crossings and signals (2090), overlaps (1070, 0), layers
    /// (4110), restrictions (3180), unconnected roads (1210), parking lanes (3161), public transport (1260, 2140, 9014);
    /// and of a road's own detail, its lanes and <c>turn:lanes</c> (3160), a road ending just short of another (1270),
    /// access a barrier or a tag contradicts (2130, 3220), tags that contradict each other or a road's speed
    /// (3032, 4030), a number that is not one (3091), and cycling infrastructure seen but not mapped (8470).
    /// </summary>
    static readonly int[] Items = [8300, 2090, 1070, 0, 4110, 3180, 1210, 3161, 1260, 2140, 9014, 3160, 1270, 2130, 3220, 3032, 4030, 3091, 8470];

    public static string[] Fetch(Sources sources) =>
        [.. Items.Select(item => sources.File($"osmose-{item}.json",
            $"https://osmose.openstreetmap.fr/api/0.3/issues.geojson?bbox={sources.Bbox()}&item={item}&limit=10000&full=true", Licence, Analysed))];

    /// <summary>When the analysis that raised an answer's issues ran, the latest of them.</summary>
    static string Analysed(string kept)
    {
        using var answer = JsonDocument.Parse(File.ReadAllBytes(kept));
        var latest = answer.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("properties").TryGetProperty("timestamp", out var stamp) ? stamp.GetString() ?? "" : "")
            .Max(StringComparer.Ordinal);
        return string.IsNullOrEmpty(latest) ? "no issues" : $"analysed {latest}";
    }

    public static List<OsmoseIssue> Read(string[] kept)
    {
        var read = new List<OsmoseIssue>();
        foreach (var path in kept)
        {
            using var answer = JsonDocument.Parse(File.ReadAllBytes(path));
            foreach (var feature in answer.RootElement.GetProperty("features").EnumerateArray())
            {
                var properties = feature.GetProperty("properties");
                var place = feature.GetProperty("geometry").GetProperty("coordinates");
                var elements = properties.TryGetProperty("elems", out var elems) && elems.ValueKind == JsonValueKind.Array
                    ? elems.EnumerateArray().Select(elem => $"{char.ToLowerInvariant(elem.GetProperty("type").GetString()![0])}{elem.GetProperty("id").GetInt64()}").ToArray()
                    : [];
                var proposed = new Dictionary<string, string>();
                if (properties.TryGetProperty("fixes", out var fixes) && fixes.ValueKind == JsonValueKind.Array && fixes.GetArrayLength() > 0
                    && fixes[0].ValueKind == JsonValueKind.Array && fixes[0].GetArrayLength() > 0 && fixes[0][0].TryGetProperty("create", out var create))
                {
                    foreach (var tag in create.EnumerateObject()) proposed[tag.Name] = tag.Value.ToString();
                }

                read.Add(new OsmoseIssue
                {
                    Uuid = properties.TryGetProperty("uuid", out var uuid) ? uuid.GetString() ?? "" : "",
                    Item = properties.GetProperty("item").GetInt32(),
                    Class = properties.GetProperty("class").GetInt32(),
                    Title = properties.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                    Subtitle = properties.TryGetProperty("subtitle", out var subtitle) && subtitle.ValueKind == JsonValueKind.String ? subtitle.GetString() : null,
                    Lat = (int)Math.Round(place[1].GetDouble() * 1e7),
                    Lon = (int)Math.Round(place[0].GetDouble() * 1e7),
                    Elements = elements,
                    Proposed = proposed,
                });
            }
        }

        return read;
    }
}

internal sealed class OsmoseIssue
{
    public required string Uuid { get; init; }

    public required int Item { get; init; }

    public required int Class { get; init; }

    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public required int Lat { get; init; }

    public required int Lon { get; init; }

    public required string[] Elements { get; init; }

    /// <summary>The tags Osmose proposes to add, for an unmapped sign the ones its detection means.</summary>
    public required Dictionary<string, string> Proposed { get; init; }

    /// <summary>
    /// Whether OSM's own data says a junction is signalled where no signal is mapped (2090/2); a sign Mapillary saw
    /// (8300) is a sighting (<see cref="Sightings"/>), laid as one.
    /// </summary>
    public bool SaysSignals => Item == 2090 && Class == 2;
}
