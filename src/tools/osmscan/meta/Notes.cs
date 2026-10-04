using System.Text.Json;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>What people on the ground have told OSM is wrong or missing</b>: every open map note over the map, off the OSM
/// API — its place, when it was opened and its first comment, and nothing of who wrote it.
/// </summary>
internal static class Notes
{
    public static string Fetch(Sources sources) =>
        sources.File("notes.json", $"https://api.openstreetmap.org/api/0.6/notes.json?bbox={sources.Bbox()}&limit=10000&closed=0", Sources.OsmLicence);

    public static List<NoteRecord> Read(string keptAt)
    {
        using var answer = JsonDocument.Parse(File.ReadAllBytes(keptAt));
        var read = new List<NoteRecord>();
        foreach (var feature in answer.RootElement.GetProperty("features").EnumerateArray())
        {
            var place = feature.GetProperty("geometry").GetProperty("coordinates");
            var properties = feature.GetProperty("properties");
            var comments = properties.GetProperty("comments");
            var first = comments.GetArrayLength() > 0 ? comments[0] : default;
            var text = first.ValueKind == JsonValueKind.Object && first.TryGetProperty("text", out var said) ? said.GetString() ?? "" : "";
            read.Add(new NoteRecord
            {
                Id = properties.GetProperty("id").GetInt64(),
                Lat = (int)Math.Round(place[1].GetDouble() * 1e7),
                Lon = (int)Math.Round(place[0].GetDouble() * 1e7),
                Opened = properties.GetProperty("date_created").GetString() ?? "",
                Comments = comments.GetArrayLength(),
                Text = text.Length > NoteRecord.TextLength ? text[..NoteRecord.TextLength] + "…" : text,
            });
        }

        return read;
    }
}
