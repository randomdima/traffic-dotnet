using System.Globalization;
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
        using (var file = File.Create(path))
        using (var json = new Utf8JsonWriter(file, new JsonWriterOptions { Encoder = Json.Encoder }))
        {
            // Each value is a JSON document of its own to the writer, flushed and reset before the text between them.
            void Value<TValue>(TValue value, Type? type = null)
            {
                JsonSerializer.Serialize(json, value, type ?? typeof(TValue), Json);
                json.Flush();
                json.Reset();
            }

            file.Write("{\"layer\":"u8);
            Value(layer);
            file.Write(",\"about\":"u8);
            Value(about);
            file.Write(",\"readOff\":"u8);
            Value(readOff.ToArray());
            file.Write(",\"count\":"u8);
            file.Write(Encoding.UTF8.GetBytes(items.Count.ToString(CultureInfo.InvariantCulture)));
            if (summary is not null)
            {
                file.Write(",\"summary\":"u8);
                Value(summary, summary.GetType());
            }

            file.Write(",\"items\":["u8);
            var first = true;
            foreach (var item in items)
            {
                file.Write(first ? "\n"u8 : ",\n"u8);
                Value(item);
                first = false;
            }

            file.Write("\n]}\n"u8);
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

    public static string Indented(object value) => JsonSerializer.Serialize(value, value.GetType(), IndentedJson);

    static readonly JsonSerializerOptions IndentedJson = new(Json) { WriteIndented = true };
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
