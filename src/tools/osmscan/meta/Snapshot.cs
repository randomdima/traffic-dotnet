using System.Text.Json;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Whether every OSM layer was read off one moment, the survey's</b>: each family's moment against the survey's,
/// every node the families and the survey both hold in one place, every node both tag with one set of tags, and every
/// survey road a family also holds with the same nodes and tags.
/// </summary>
/// <remarks>
/// Overpass answers off the database as it stands when asked, so families asked hours apart stand hours apart, and an
/// edit made between them puts a node in two places or a crossing's tags in two states. A family asked as of the
/// survey's own moment (<c>[date:…]</c>, <see cref="Sources"/>) stands with it whenever it is asked.
/// <para>
/// A node or road the survey's own corrections put right (<see cref="Corrections"/>) differs from OSM on purpose, and
/// is not asked.
/// </para>
/// </remarks>
internal static class Snapshot
{
    public static List<Finding> Check(Dataset data)
    {
        var moments = new Finding("every OSM family stands at the survey's moment");
        var places = new Finding("every node the families and the survey both hold is in one place");
        var tagged = new Finding("every node the families and the survey both hold has one set of tags");
        var ways = new Finding("every survey road a family also holds has the same nodes and tags");
        var survey = data.Survey.Source.OsmBase;
        var placed = new HashSet<long>();
        var tagsSeen = new HashSet<long>();

        // The survey keeps the tags of a node on a road and of no other: a node only of a road surface or the coast
        // is held for its place, its tags unread.
        var onRoads = data.SurveyRoads.Values.SelectMany(road => road.Nodes).ToHashSet();
        foreach (var (name, standsAt, keptAt) in data.Sources(name => name.StartsWith("osm-", StringComparison.Ordinal)))
        {
            moments.Ask(standsAt == survey, () => $"{name} at {standsAt}");
            var answers = keptAt.Where(File.Exists).Select(path => JsonDocument.Parse(File.ReadAllBytes(path))).ToList();
            foreach (var element in Element.Read(answers))
            {
                if (element.Type == 'n')
                {
                    if (!data.NodeIndex.TryGetValue(element.Id, out var node) || data.CorrectedNodes.Contains(element.Id)) continue;

                    if ((element.Lat != 0 || element.Lon != 0) && placed.Add(element.Id)) places.Ask(Same(data, node, element.Lat, element.Lon), () => $"n{element.Id} in {name}");
                    if (element.Tags.Count > 0 && onRoads.Contains(node) && tagsSeen.Add(element.Id))
                    {
                        var held = data.NodeTags.GetValueOrDefault(node) ?? [];
                        tagged.Ask(Equal(held, element.Tags), () => $"n{element.Id} in {name}: {Differ(held, element.Tags)}");
                    }
                }
                else if (element.Type == 'w')
                {
                    if (element.Nodes.Length * 2 == element.Geometry.Length)
                    {
                        for (var at = 0; at < element.Nodes.Length; at++)
                        {
                            var id = element.Nodes[at];
                            if (!data.NodeIndex.TryGetValue(id, out var node) || data.CorrectedNodes.Contains(id) || !placed.Add(id)) continue;

                            places.Ask(Same(data, node, element.Geometry[2 * at], element.Geometry[(2 * at) + 1]), () => $"n{id} of w{element.Id} in {name}");
                        }
                    }

                    if (data.SurveyRoads.TryGetValue(element.Id, out var road) && element.Nodes.Length > 0 && !data.CorrectedWays.Contains(element.Id))
                    {
                        var nodes = road.Nodes.Select(node => data.Survey.Nodes.Id[node]);
                        ways.Ask(nodes.SequenceEqual(element.Nodes) && Equal(road.Tags, element.Tags), () => $"w{element.Id} in {name}: {Differ(road.Tags, element.Tags)}");
                    }
                }
            }

            foreach (var answer in answers) answer.Dispose();
        }

        return [moments, places, tagged, ways];
    }

    static bool Same(Dataset data, int node, int lat, int lon) => data.Survey.Nodes.Lat[node] == lat && data.Survey.Nodes.Lon[node] == lon;

    static bool Equal(Dictionary<string, string> a, Dictionary<string, string> b) => a.Count == b.Count && a.All(tag => b.TryGetValue(tag.Key, out var value) && value == tag.Value);

    /// <summary>The first tag two sets differ on, as the survey has it and as the family does.</summary>
    static string Differ(Dictionary<string, string> survey, Dictionary<string, string> family)
    {
        foreach (var key in survey.Keys.Union(family.Keys).Order(StringComparer.Ordinal))
        {
            var (was, now) = (survey.GetValueOrDefault(key), family.GetValueOrDefault(key));
            if (was != now) return $"{key} {was ?? "∅"} / {now ?? "∅"}";
        }

        return "nodes";
    }
}
