using System.Text.Json;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>One OSM element as Overpass answered it</b>: its type and id, every tag, and whatever place the query asked
/// for — a node's own, a way's nodes and geometry, a relation's members with theirs, or a centre — each in OSM's
/// integer units of 1e-7°, so nothing is rounded on the way in.
/// </summary>
internal sealed class Element
{
    /// <summary><c>n</c>, <c>w</c> or <c>r</c>.</summary>
    public required char Type { get; init; }

    public required long Id { get; init; }

    public required Dictionary<string, string> Tags { get; init; }

    /// <summary>A node's place, or the centre of a way or relation asked for with <c>out center</c>.</summary>
    public int Lat { get; init; }

    public int Lon { get; init; }

    /// <summary>A way's node ids, where the query printed its body.</summary>
    public long[] Nodes { get; init; } = [];

    /// <summary>A way's places as lat, lon pairs, where the query printed its geometry.</summary>
    public int[] Geometry { get; init; } = [];

    public Member[] Members { get; init; } = [];

    /// <summary>The element's key in every layer: <c>n123</c>, <c>w123</c> or <c>r123</c>.</summary>
    public string Key => $"{Type}{Id}";

    public string? Tag(string key) => Tags.GetValueOrDefault(key);

    public bool Is(string key, string value) => Tags.TryGetValue(key, out var tagged) && tagged == value;

    public bool Closed => Geometry.Length >= 8 && Geometry[0] == Geometry[^2] && Geometry[1] == Geometry[^1];

    /// <summary>
    /// Every element of a family's answers, of one type or all, each once: an element astride two tiles is in both
    /// answers, and is the same element. None where the family could not be had.
    /// </summary>
    public static List<Element> Read(IEnumerable<JsonDocument>? answers, char? type = null)
    {
        var read = new List<Element>();
        if (answers is null) return read;

        var seen = new HashSet<(char, long)>();
        foreach (var answer in answers)
        {
            foreach (var element in answer.RootElement.GetProperty("elements").EnumerateArray())
            {
                var kind = element.GetProperty("type").GetString()![0];
                if ((type is not null && kind != type) || !seen.Add((kind, element.GetProperty("id").GetInt64()))) continue;

                read.Add(Of(element, kind));
            }
        }

        return read;
    }

    static Element Of(JsonElement element, char kind)
    {
        var (lat, lon) = (0, 0);
        if (element.TryGetProperty("lat", out var latAt)) (lat, lon) = (Units(latAt), Units(element.GetProperty("lon")));
        else if (element.TryGetProperty("center", out var centre)) (lat, lon) = (Units(centre.GetProperty("lat")), Units(centre.GetProperty("lon")));

        return new Element
        {
            Type = kind,
            Id = element.GetProperty("id").GetInt64(),
            Tags = TagsOf(element),
            Lat = lat,
            Lon = lon,
            Nodes = element.TryGetProperty("nodes", out var nodes) ? [.. nodes.EnumerateArray().Select(node => node.GetInt64())] : [],
            Geometry = element.TryGetProperty("geometry", out var geometry) ? Places(geometry) : [],
            Members = element.TryGetProperty("members", out var members)
                ? [.. members.EnumerateArray().Select(member => new Member
                {
                    Type = member.GetProperty("type").GetString()![0],
                    Ref = member.GetProperty("ref").GetInt64(),
                    Role = member.GetProperty("role").GetString() ?? "",
                    Geometry = member.TryGetProperty("geometry", out var placed) ? Places(placed) : [],
                    Lat = member.TryGetProperty("lat", out var memberLat) ? Units(memberLat) : 0,
                    Lon = member.TryGetProperty("lon", out var memberLon) ? Units(memberLon) : 0,
                })]
                : [],
        };
    }

    static Dictionary<string, string> TagsOf(JsonElement element)
    {
        var tags = new Dictionary<string, string>();
        if (!element.TryGetProperty("tags", out var tagged)) return tags;

        foreach (var tag in tagged.EnumerateObject()) tags[tag.Name] = tag.Value.GetString() ?? "";
        return tags;
    }

    /// <summary>
    /// A geometry's places as lat, lon pairs. A place Overpass could not give — a member node outside what it
    /// was asked for — is printed as null and left out.
    /// </summary>
    static int[] Places(JsonElement geometry)
    {
        var places = new List<int>(geometry.GetArrayLength() * 2);
        foreach (var at in geometry.EnumerateArray())
        {
            if (at.ValueKind != JsonValueKind.Object) continue;

            places.Add(Units(at.GetProperty("lat")));
            places.Add(Units(at.GetProperty("lon")));
        }

        return [.. places];
    }

    /// <summary>Overpass prints OSM's stored integer as seven decimals, so the decimal read back is exact.</summary>
    public static int Units(JsonElement degrees) => (int)Math.Round(degrees.GetDecimal() * 10_000_000m);
}

internal sealed class Member
{
    public required char Type { get; init; }

    public required long Ref { get; init; }

    public required string Role { get; init; }

    /// <summary>A way member's places as lat, lon pairs, where the query printed geometry.</summary>
    public int[] Geometry { get; init; } = [];

    public int Lat { get; init; }

    public int Lon { get; init; }
}
