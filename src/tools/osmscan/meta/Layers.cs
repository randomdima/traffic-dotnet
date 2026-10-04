using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Lays every layer and writes each to a file of its own</b>: a head naming the layer, what it was read off and
/// how many items it holds, then its items one a line, so a file is read, grepped and diffed by item.
/// </summary>
internal static class Layers
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>Every layer, in the order each needs the ones before it; a layer whose source could not be had is laid empty.</summary>
    public static List<Written> Lay(Town town, string into)
    {
        var written = new List<Written>();
        var flags = new Flags();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        T Timed<T>(string what, Func<T> lay)
        {
            clock.Restart();
            var laid = lay();
            Console.WriteLine($"  laid {what,-10} {clock.Elapsed.TotalSeconds,5:F1} s");
            return laid;
        }

        var osmose = town.Fetched.OsmoseAt is { } issues ? Osmose.Read(issues) : [];
        // The ground under a road is the terrain model's; the surface model's, roofs and trees and all, only where the terrain could not be had.
        (Dem Model, string From)? ground = town.Fetched.TerrainAt is { } terrain ? (new Dem(terrain), Dem.TerrainName)
            : town.Fetched.DemAt is { } tiles ? (new Dem(tiles), "dem") : null;
        var controls = new Controls(town);
        var seen = Sightings.Read(town, osmose);
        var seenControls = Sightings.Controls(town, seen);
        var zones = Timed("zones", () => Zones.Lay(town, flags, into, written));
        var walk = Timed("walk", () => Walk.Lay(town, flags, into, written));
        var buildings = Timed("buildings", () => Buildings.Lay(town, zones, walk, flags, into, written));
        var crossings = Timed("crossings", () => Crossings.Lay(town, controls, seenControls, walk, flags, into, written));
        var junctions = Timed("junctions", () => Junctions.Lay(town, controls, seenControls, crossings, osmose, flags, into, written));
        var levels = Timed("levels", () => Levels.Lay(town, zones, buildings, walk, flags, into, written));
        var transit = Timed("transit", () => Transit.Lay(town, flags, into, written));
        Timed("points", () => { Points.Lay(town, into, written); return 0; });
        var detected = Timed("detected", () => MlRoads.Lay(town, zones, walk, flags, into, written));
        Timed("seen", () => { Sightings.Lay(town, seen, controls, crossings, junctions, flags, into, written); return 0; });
        Timed("roads", () => { Roads.Lay(town, zones, walk, buildings, levels, transit, crossings, detected, ground, flags, into, written); return 0; });
        written.Add(Flags.Lay(town, flags, osmose, into));
        return written;
    }

    /// <summary>One layer's file: its head, then its items one a line.</summary>
    public static Written Write<T>(string into, string layer, string about, IEnumerable<string> readOff, IReadOnlyCollection<T> items, object? summary = null)
    {
        var path = Path.Combine(into, layer + ".json");
        using (var file = new StreamWriter(path, false, new UTF8Encoding(false)))
        {
            file.Write("{\"layer\":");
            file.Write(JsonSerializer.Serialize(layer, Json));
            file.Write(",\"about\":");
            file.Write(JsonSerializer.Serialize(about, Json));
            file.Write(",\"readOff\":");
            file.Write(JsonSerializer.Serialize(readOff.ToArray(), Json));
            file.Write(",\"count\":");
            file.Write(items.Count);
            if (summary is not null)
            {
                file.Write(",\"summary\":");
                file.Write(JsonSerializer.Serialize(summary, summary.GetType(), Json));
            }

            file.Write(",\"items\":[");
            var first = true;
            foreach (var item in items)
            {
                file.Write(first ? "\n" : ",\n");
                file.Write(JsonSerializer.Serialize(item, Json));
                first = false;
            }

            file.Write("\n]}\n");
        }

        return new Written(layer, about, readOff.ToArray(), items.Count, new FileInfo(path).Length, summary);
    }

    /// <summary>One layer's file read back as a reader reads it, head and items; null where it was not written.</summary>
    public static LayerFile<T>? Read<T>(string into, string layer)
    {
        var path = Path.Combine(into, layer + ".json");
        if (!File.Exists(path)) return null;

        using var file = File.OpenRead(path);
        return JsonSerializer.Deserialize<LayerFile<T>>(file, Json);
    }

    public static string Serialize(object value) => JsonSerializer.Serialize(value, value.GetType(), Json);

    public static string Indented(object value) => JsonSerializer.Serialize(value, value.GetType(), new JsonSerializerOptions(Json) { WriteIndented = true });
}

/// <summary>One layer as written: its name, what it holds, what it was read off, how many items, its size and its summary.</summary>
internal sealed record Written(string Layer, string About, string[] ReadOff, int Count, long Bytes, object? Summary);

/// <summary>One layer's file as read back: its head, and its items.</summary>
internal sealed class LayerFile<T>
{
    public required string Layer { get; init; }

    public required int Count { get; init; }

    public JsonElement? Summary { get; init; }

    public required List<T> Items { get; init; }
}
