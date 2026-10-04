using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>What Mapillary's cameras saw along the streets</b>, off its API: every traffic sign, traffic light and road
/// marking its detections placed on the map, the way it faces and when it was first and last seen. Laid as sightings
/// (<see cref="Sightings"/>).
/// </summary>
/// <remarks>
/// <para>
/// The API answers at most <see cref="Most"/> features a box, a box under 0.01 square degrees, and no paging, so the
/// rectangle is asked in boxes and a box answering the most it may is asked again in quarters.
/// </para>
/// <para>
/// <b>It needs a client token</b>, read from <c>MAPILLARY_TOKEN</c> and sent as a header — never in a URL, so no
/// kept file, manifest or log holds it. Without one the source is not had; a token refused is reported and not
/// retried. Who took the pictures is never asked for.
/// </para>
/// </remarks>
internal static class Mapillary
{
    public const string Licence = "Mapillary map features, CC BY-SA 4.0 — https://www.mapillary.com/terms";

    public const string Name = "mapillary.jsonl";

    const string Url = "https://graph.mapillary.com/map_features";

    const string TokenVariable = "MAPILLARY_TOKEN";

    const string Fields = "id,object_value,object_type,geometry,aligned_direction,first_seen_at,last_seen_at";

    /// <summary>Every sign, every traffic light and every marking; benches, poles and hydrants are left to OSM.</summary>
    const string Values = "regulatory--*,warning--*,information--*,complementary--*,object--traffic-light--*,marking--*";

    const int Most = 2000;

    /// <summary>The first boxes' side, a quarter of the area the API allows; and the smallest a busy box is cut to.</summary>
    const double BoxDeg = 0.05, SmallestDeg = 0.003;

    public static string Fetch(Sources sources)
    {
        var token = Environment.GetEnvironmentVariable(TokenVariable);
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException($"no {TokenVariable}: Mapillary's detections need a client token");

        return sources.Made(Name, Url, Licence, Seen, into =>
        {
            using var kept = new StreamWriter(into, new UTF8Encoding(false), leaveOpen: true);
            var written = new HashSet<long>();
            for (var south = sources.SouthDeg; south < sources.NorthDeg; south += BoxDeg)
            {
                for (var west = sources.WestDeg; west < sources.EastDeg; west += BoxDeg)
                {
                    Box(west, south, Math.Min(west + BoxDeg, sources.EastDeg), Math.Min(south + BoxDeg, sources.NorthDeg));
                }
            }

            void Box(double west, double south, double east, double north)
            {
                var url = string.Create(CultureInfo.InvariantCulture,
                    $"{Url}?fields={Fields}&bbox={west:F6},{south:F6},{east:F6},{north:F6}&limit={Most}&object_values={Uri.EscapeDataString(Values)}");
                using var answer = JsonDocument.Parse(Http.Text(url, $"OAuth {token}"));
                var features = answer.RootElement.GetProperty("data");
                if (features.GetArrayLength() >= Most && east - west > SmallestDeg)
                {
                    var (midLon, midLat) = ((west + east) / 2, (south + north) / 2);
                    Box(west, south, midLon, midLat);
                    Box(midLon, south, east, midLat);
                    Box(west, midLat, midLon, north);
                    Box(midLon, midLat, east, north);
                    return;
                }

                foreach (var feature in features.EnumerateArray())
                {
                    var id = long.Parse(feature.GetProperty("id").GetString()!, CultureInfo.InvariantCulture);
                    if (!written.Add(id)) continue;

                    var place = feature.GetProperty("geometry").GetProperty("coordinates");
                    kept.WriteLine(JsonSerializer.Serialize(new Sighting
                    {
                        Id = id,
                        Value = feature.GetProperty("object_value").GetString()!,
                        Type = feature.TryGetProperty("object_type", out var type) ? type.GetString() : null,
                        Lat = (int)Math.Round(place[1].GetDouble() * 1e7),
                        Lon = (int)Math.Round(place[0].GetDouble() * 1e7),
                        FacingDeg = feature.TryGetProperty("aligned_direction", out var facing) && facing.ValueKind == JsonValueKind.Number ? Math.Round(facing.GetDouble()) : null,
                        First = Day(feature, "first_seen_at"),
                        Last = Day(feature, "last_seen_at"),
                    }, SightingJson.Default.Sighting));
                }
            }
        });

        static string? Day(JsonElement feature, string name) =>
            feature.TryGetProperty(name, out var value) && value.GetString() is { Length: >= 10 } stamp ? stamp[..10] : null;
    }

    /// <summary>When the sightings stand: the span of days they were last seen over.</summary>
    static string Seen(string kept)
    {
        var last = Read(kept).Select(sighting => sighting.Last).Where(day => day is not null).Order(StringComparer.Ordinal).ToList();
        return last.Count == 0 ? "no sightings" : $"last seen {last[0]} … {last[^1]}";
    }

    public static List<Sighting> Read(string kept) =>
        [.. File.ReadLines(kept).Where(line => line.Length > 0).Select(line => JsonSerializer.Deserialize(line, SightingJson.Default.Sighting)!)];

    /// <summary>
    /// What a detection is, for a road: a light, a marking, or a sign that controls a junction, a turn, a speed or
    /// a kerb; anything else is a <c>sign</c> or a <c>marking</c>. A speed sign gives its limit.
    /// </summary>
    public static string Kind(string value, out int? limitKmh)
    {
        limitKmh = null;
        if (value.StartsWith("object--traffic-light--pedestrians", StringComparison.Ordinal)) return "pedestrian_light";
        if (value.StartsWith("object--traffic-light--", StringComparison.Ordinal)) return "traffic_light";
        if (value.StartsWith("marking--discrete--crosswalk-zebra", StringComparison.Ordinal)) return "zebra";
        if (value.StartsWith("marking--discrete--arrow--", StringComparison.Ordinal)) return "lane_arrow";
        if (value.StartsWith("marking--discrete--stop-line", StringComparison.Ordinal)) return "stop_line";
        if (value.StartsWith("marking--discrete--give-way", StringComparison.Ordinal)) return "give_way_line";
        if (value.StartsWith("marking--", StringComparison.Ordinal)) return "marking";
        if (value.StartsWith("regulatory--stop--", StringComparison.Ordinal)) return "stop";
        if (value.StartsWith("regulatory--yield--", StringComparison.Ordinal)) return "give_way";
        if (value.Contains("--priority-road--", StringComparison.Ordinal)) return "priority_road";
        if (value.StartsWith("regulatory--maximum-speed-limit-", StringComparison.Ordinal))
        {
            var digits = new string([.. value["regulatory--maximum-speed-limit-".Length..].TakeWhile(char.IsAsciiDigit)]);
            if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit)) limitKmh = limit;
            return "speed_limit";
        }

        if (value.StartsWith("regulatory--no-left-turn", StringComparison.Ordinal) || value.StartsWith("regulatory--no-right-turn", StringComparison.Ordinal)
            || value.StartsWith("regulatory--no-u-turn", StringComparison.Ordinal) || value.StartsWith("regulatory--no-straight-through", StringComparison.Ordinal))
        {
            return "turn_ban";
        }

        if (value.StartsWith("regulatory--turn-", StringComparison.Ordinal) || value.StartsWith("regulatory--go-straight", StringComparison.Ordinal)
            || value.StartsWith("regulatory--keep-", StringComparison.Ordinal) || value.StartsWith("regulatory--pass-on-either-side", StringComparison.Ordinal))
        {
            return "turn_only";
        }

        if (value.StartsWith("regulatory--no-entry", StringComparison.Ordinal)) return "no_entry";
        if (value.StartsWith("regulatory--one-way", StringComparison.Ordinal)) return "one_way";
        if (value.StartsWith("regulatory--no-parking", StringComparison.Ordinal) || value.StartsWith("regulatory--no-stopping", StringComparison.Ordinal)) return "no_parking";
        if (value.Contains("--pedestrians-crossing--", StringComparison.Ordinal)) return "crossing_sign";
        if (value.StartsWith("warning--traffic-signals", StringComparison.Ordinal)) return "signals_ahead";
        return "sign";
    }
}

/// <summary>One detection as kept: Mapillary's id and class, its place in 1e-7°, the way it faces, and the days it was first and last seen.</summary>
internal sealed class Sighting
{
    public required long Id { get; init; }

    public required string Value { get; init; }

    public string? Type { get; init; }

    public required int Lat { get; init; }

    public required int Lon { get; init; }

    public double? FacingDeg { get; init; }

    public string? First { get; init; }

    public string? Last { get; init; }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(Sighting))]
internal sealed partial class SightingJson : System.Text.Json.Serialization.JsonSerializerContext;
