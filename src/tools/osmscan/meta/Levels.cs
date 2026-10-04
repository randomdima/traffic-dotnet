using System.Globalization;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>What stands above what</b>: every road, railway and way for feet that is not on the ground — a bridge, a
/// viaduct, a tunnel, a passage under a building, a covered way, or any tagged layer — with the structure it is on
/// and the building over it; every place two of them cross without meeting, which is over which; and the ground's
/// height under every road node.
/// </summary>
/// <remarks>
/// <para>
/// <b>A level is OSM's <c>layer</c></b>, which orders what crosses and says nothing of height. Untagged, a bridge
/// is layer 1 and a tunnel −1 (Key:layer), and anything else 0.
/// </para>
/// <para>
/// <b>Two ways that cross with no node between them do not meet</b> (OSM's own rule): where their levels differ the
/// higher passes over, and where they are equal and neither is a bridge or tunnel, the crossing is flagged — a
/// junction or a level not mapped. Two that lie along each other and only weave over each other's line, as a tram
/// track down a street does, are not crossing.
/// </para>
/// <para>
/// <b>A height is the ground's</b>, off the terrain model (<see cref="Dem"/>; the surface model where the terrain
/// could not be had), at each road node. Across a bridge or tunnel the ground is the valley, the water or the hill
/// rather than the deck, so a structure's own nodes are laid on the straight between its ends, and say so.
/// </para>
/// </remarks>
internal static class Levels
{
    const double CellM = 40;

    public static Dictionary<long, LevelRecord> Lay(Town town, ZonesFound zones, BuildingsFound buildings, WalkFound walk, Flags flags, string into, List<Written> written)
    {
        var lines = new List<(long Way, string Class, string Kind, Dictionary<string, string> Tags, long[] Nodes, Pt[] LineM, int[] Geometry)>();
        for (var road = 0; road < town.Roads.Length; road++)
        {
            var way = town.Roads[road];
            var geometry = new int[way.Nodes.Length * 2];
            for (var at = 0; at < way.Nodes.Length; at++)
            {
                (geometry[2 * at], geometry[(2 * at) + 1]) = (town.Extract.Nodes.Lat[way.Nodes[at]], town.Extract.Nodes.Lon[way.Nodes[at]]);
            }

            lines.Add((way.Id, "road", way.Tags["highway"], way.Tags, [.. way.Nodes.Select(town.NodeId)], town.RoadLineM[road], geometry));
        }

        foreach (var rail in Element.Read(town.Fetched.Transit, 'w'))
        {
            if (rail.Tag("railway") is not { } kind || kind is "abandoned" or "razed" or "platform" || rail.Geometry.Length < 4) continue;

            var geometry = town.Surveyed(rail);
            lines.Add((rail.Id, "rail", kind, rail.Tags, rail.Nodes, town.Plane.Line(geometry), geometry));
        }

        foreach (var record in walk.Records)
        {
            if (record.Kind.StartsWith("area_", StringComparison.Ordinal) || record.Kind.EndsWith("_area", StringComparison.Ordinal) || record.Line.Length < 4) continue;

            lines.Add((record.Way, "walk", record.Kind, record.Tags, record.Nodes, town.Plane.Line(record.Line), record.Line));
        }

        var levels = new Dictionary<long, LevelRecord>();
        var structures = new List<LevelRecord>();
        foreach (var (way, kind, sub, tags, _, lineM, geometry) in lines)
        {
            var (layer, from) = Layer(tags);
            var structure = Structure(tags);
            var over = buildings.Over.GetValueOrDefault(way);
            var outline = structure is "bridge" or "viaduct" or "boardwalk" or "aqueduct"
                ? zones.Bridges.FirstOrDefault(bridge => bridge.Outline.Holds(Shape.Along(lineM, Shape.Length(lineM) / 2)))
                : default;
            if (layer == 0 && structure is null && over is null) continue;

            var record = new LevelRecord
            {
                Way = way,
                Class = kind,
                Kind = sub,
                Layer = layer,
                LayerFrom = from,
                Structure = structure,
                Name = tags.GetValueOrDefault("bridge:name") ?? tags.GetValueOrDefault("tunnel:name") ?? outline.Name,
                Outline = outline.Id,
                Under = over,
                LengthM = Math.Round(Shape.Length(lineM), 1),
                Line = kind == "road" ? null : geometry,
            };
            levels[way] = record;
            structures.Add(record);
        }

        var separations = Separations(town, lines, levels, flags);
        written.Add(Layers.Write(into, "levels",
            "Every road, railway and way for feet that is not on the ground: its layer and where that came from, the structure it is on, the outlined bridge holding it and the building over it.",
            ["survey", "osm-transit", "osm-walk", "osm-zones", "osm-buildings"], structures,
            new
            {
                byClass = structures.GroupBy(record => record.Class).ToDictionary(g => g.Key, g => g.Count()),
                structures = structures.GroupBy(record => $"{record.Class}:{record.Structure ?? "layer"}").OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                layers = structures.GroupBy(record => record.Layer).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(CultureInfo.InvariantCulture), g => g.Count()),
                bridgesOnOutlines = structures.Count(record => record.Outline is not null),
                underBuildings = structures.Count(record => record.Under is not null),
            }));
        written.Add(Layers.Write(into, "overpasses",
            "Every place two ways cross with no node between them: where, the two ways and their levels, which passes over, and whether OSM's tags explain it.",
            ["survey", "osm-transit", "osm-walk"], separations,
            new
            {
                pairs = separations.GroupBy(record => $"{record.UpperClass}/{record.LowerClass}").OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                unexplained = separations.Where(record => !record.Explained).GroupBy(record => $"{record.UpperClass}/{record.LowerClass}").ToDictionary(g => g.Key, g => g.Count()),
            }));
        return levels;
    }

    /// <summary>A way's layer and where it came from: its own tag, else 1 on a bridge, −1 in a tunnel, else 0.</summary>
    public static (int Layer, string From) Layer(Dictionary<string, string> tags)
    {
        if (tags.TryGetValue("layer", out var tagged) && int.TryParse(tagged.Split(';')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var layer)) return (layer, "tag");
        if (tags.GetValueOrDefault("bridge") is { } bridge && bridge != "no") return (1, "bridge");
        if (tags.GetValueOrDefault("tunnel") is { } tunnel && tunnel is not ("no" or "building_passage")) return (-1, "tunnel");
        return (0, "ground");
    }

    /// <summary>The structure a way is on or in, by its own tags: a bridge's kind, a tunnel's, a passage or a cover.</summary>
    public static string? Structure(Dictionary<string, string> tags)
    {
        if (tags.GetValueOrDefault("bridge") is { } bridge && bridge != "no") return bridge == "yes" ? "bridge" : bridge;
        if (tags.GetValueOrDefault("tunnel") is { } tunnel && tunnel != "no") return tunnel == "yes" ? "tunnel" : tunnel;
        if (tags.GetValueOrDefault("covered") is { } covered && covered != "no") return "covered";
        if (tags.GetValueOrDefault("indoor") == "yes") return "indoor";
        return null;
    }

    static List<OverpassRecord> Separations(
        Town town, List<(long Way, string Class, string Kind, Dictionary<string, string> Tags, long[] Nodes, Pt[] LineM, int[] Geometry)> lines,
        Dictionary<long, LevelRecord> levels, Flags flags)
    {
        var grid = new Grid(CellM);
        var segments = new List<(int Line, int At)>();
        for (var line = 0; line < lines.Count; line++)
        {
            var lineM = lines[line].LineM;
            for (var at = 0; at + 1 < lineM.Length; at++)
            {
                grid.Add(segments.Count, Box.Empty.With(lineM[at]).With(lineM[at + 1]));
                segments.Add((line, at));
            }
        }

        var found = new List<OverpassRecord>();
        var seen = new HashSet<(int, int)>();
        var near = new HashSet<int>();
        for (var segment = 0; segment < segments.Count; segment++)
        {
            var (line, at) = segments[segment];
            var a = lines[line];
            near.Clear();
            grid.Near(Box.Empty.With(a.LineM[at]).With(a.LineM[at + 1]), near);
            foreach (var other in near)
            {
                if (other <= segment) continue;

                var (otherLine, otherAt) = segments[other];
                if (otherLine == line || (a.Class != "road" && lines[otherLine].Class != "road")) continue;

                var b = lines[otherLine];
                if (!Shape.Crosses(a.LineM[at], a.LineM[at + 1], b.LineM[otherAt], b.LineM[otherAt + 1], out var crossing, out _, out _)) continue;
                if (Along(a.LineM[at + 1] - a.LineM[at], b.LineM[otherAt + 1] - b.LineM[otherAt])) continue;
                if (!seen.Add((Math.Min(line, otherLine), Math.Max(line, otherLine)))) continue;

                var (layerA, _) = Layer(a.Tags);
                var (layerB, _) = Layer(b.Tags);
                var structureA = Structure(a.Tags);
                var structureB = Structure(b.Tags);
                var (upper, lower) = layerA >= layerB ? (a, b) : (b, a);
                var explained = layerA != layerB || structureA is not null || structureB is not null;
                found.Add(new OverpassRecord
                {
                    At = town.Plane.Degrees(crossing),
                    Upper = upper.Way,
                    UpperClass = upper.Class,
                    UpperKind = upper.Kind,
                    UpperLayer = Math.Max(layerA, layerB),
                    Lower = lower.Way,
                    LowerClass = lower.Class,
                    LowerKind = lower.Kind,
                    LowerLayer = Math.Min(layerA, layerB),
                    Explained = explained,
                });
                if (!explained && Live(a) && Live(b))
                {
                    flags.Raise($"{a.Class}_{b.Class}_cross_unjoined", town, crossing,
                        $"{a.Kind} and {b.Kind} cross on one level with no node between them and no bridge or tunnel", $"w{a.Way}", $"w{b.Way}");
                }
            }
        }

        return found;

        // A way for feet crossing a road unjoined is common and harmless; a track out of use carries nothing.
        static bool Live((long Way, string Class, string Kind, Dictionary<string, string> Tags, long[] Nodes, Pt[] LineM, int[] Geometry) line) =>
            line.Class == "road" || (line.Class == "rail" && line.Kind is "rail" or "tram" or "light_rail" or "narrow_gauge");
    }

    /// <summary>
    /// Whether two runs lie along each other rather than across: a tram track down a street, or a pavement beside
    /// its road, weaves over the road's line where the two are drawn a few centimetres apart, and is not crossing it.
    /// </summary>
    static bool Along(Pt a, Pt b) => Math.Abs(Pt.Cross(a, b)) < Math.Sin(double.DegreesToRadians(AlongDeg)) * a.Length * b.Length;

    /// <summary>The widest angle two runs meet at and still lie along each other.</summary>
    const double AlongDeg = 15;

    /// <summary>
    /// The ground's height under every road node, by road: the model's, except along a bridge or tunnel, whose own
    /// nodes are laid on the straight between its ends; and each road's grade end to end and steepest over a span.
    /// </summary>
    public static Dictionary<int, (double[] HeightM, bool Laid, double GradePct, double SteepestPct)> Heights(Town town, Dem dem)
    {
        // Two of the model's posts: over a shorter span a roof or a tree beside the road reads as a climb.
        const double SpanM = 60;
        var heights = new Dictionary<int, (double[] HeightM, bool Laid, double GradePct, double SteepestPct)>();
        var ground = new double[town.NodeM.Length];
        Array.Fill(ground, double.NaN);
        for (var road = 0; road < town.Roads.Length; road++)
        {
            var way = town.Roads[road];
            var heightM = new double[way.Nodes.Length];
            for (var at = 0; at < heightM.Length; at++)
            {
                var node = way.Nodes[at];
                if (double.IsNaN(ground[node])) ground[node] = dem.HeightM(town.Extract.Nodes.Lat[node], town.Extract.Nodes.Lon[node]);
                heightM[at] = ground[node];
            }

            var alongM = new double[heightM.Length];
            for (var at = 1; at < alongM.Length; at++) alongM[at] = alongM[at - 1] + (town.RoadLineM[road][at] - town.RoadLineM[road][at - 1]).Length;

            var total = town.RoadLengthM[road];
            var laid = Structure(way.Tags) is { } structure && structure is not ("covered" or "building_passage" or "indoor");
            if (laid && heightM.Length > 2)
            {
                for (var at = 1; at + 1 < heightM.Length; at++) heightM[at] = heightM[0] + ((heightM[^1] - heightM[0]) * alongM[at] / Math.Max(1e-6, total));
            }

            var grade = total > 0 ? (heightM[^1] - heightM[0]) / total * 100 : 0;
            var steepest = 0.0;
            for (int from = 0, to = 0; from < heightM.Length; from++)
            {
                while (to < heightM.Length && alongM[to] - alongM[from] < SpanM) to++;
                if (to >= heightM.Length) break;

                steepest = Math.Max(steepest, Math.Abs(heightM[to] - heightM[from]) / (alongM[to] - alongM[from]) * 100);
            }

            heights[road] = (heightM, laid, grade, steepest);
        }

        return heights;
    }
}

internal sealed class LevelRecord
{
    public required long Way { get; init; }

    /// <summary><c>road</c>, <c>rail</c> or <c>walk</c>.</summary>
    public required string Class { get; init; }

    /// <summary>Its <c>highway</c> or <c>railway</c> value, or its kind as a way for feet.</summary>
    public required string Kind { get; init; }

    public required int Layer { get; init; }

    /// <summary><c>tag</c>, <c>bridge</c> or <c>tunnel</c> (OSM's default for one untagged), or <c>ground</c>.</summary>
    public required string LayerFrom { get; init; }

    /// <summary><c>bridge</c>, <c>viaduct</c>, <c>tunnel</c>, <c>building_passage</c>, <c>culvert</c>, <c>covered</c>, <c>indoor</c> and their like.</summary>
    public string? Structure { get; init; }

    public string? Name { get; init; }

    /// <summary>The outlined bridge (<c>man_made=bridge</c>) it runs on, by zone id.</summary>
    public string? Outline { get; init; }

    /// <summary>The building it passes under, by building id.</summary>
    public string? Under { get; init; }

    public required double LengthM { get; init; }

    /// <summary>Its line, lat, lon pairs in 1e-7°, for a way the survey does not hold.</summary>
    public int[]? Line { get; init; }
}

internal sealed class OverpassRecord
{
    /// <summary>Where the two cross, lat, lon in 1e-7°.</summary>
    public required int[] At { get; init; }

    public required long Upper { get; init; }

    public required string UpperClass { get; init; }

    public required string UpperKind { get; init; }

    public required int UpperLayer { get; init; }

    public required long Lower { get; init; }

    public required string LowerClass { get; init; }

    public required string LowerKind { get; init; }

    public required int LowerLayer { get; init; }

    /// <summary>Whether their tags account for their not meeting: levels that differ, or a bridge or tunnel.</summary>
    public required bool Explained { get; init; }
}
