using System.Globalization;
using System.Xml.Linq;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>Where OSM is wrong about a place, put right before its survey is written</b>: an osmChange file beside the
/// survey (<c>towns/traced/&lt;Map&gt;.osc</c>), applied over OSM's answer by <see cref="Scan"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>OSM's own edit format and OSM's own semantics</b> (OsmChange), so the file opens in JOSM over the place it
/// corrects: a node or way under <c>modify</c> replaces the one OSM holds — its place, its tags and its node list
/// whole — one under <c>create</c> is new at a negative id, and a way under <c>delete</c> is gone. It corrects
/// road ways and the nodes on them — an OSM node of a way the survey does not hold among them, where a corrected
/// road is made to pass it, as it passes a tram junction — and a node no way passes once it is applied is dropped.
/// </para>
/// <para>
/// <b>A correction of something the survey does not hold is refused</b>, so one OSM has since mended, split or
/// deleted is noticed at the next scan rather than laid over whatever replaced it.
/// </para>
/// </remarks>
internal sealed class Corrections
{
    internal sealed record Node(long Id, int Lat, int Lon, Dictionary<string, string> Tags);

    internal sealed record Way(long Id, long[] Nodes, Dictionary<string, string> Tags);

    public required string File { get; init; }

    /// <summary>Every node placed: OSM's moved or retagged, and those created.</summary>
    public required Dictionary<long, Node> Nodes { get; init; }

    /// <summary>Every road way laid: OSM's replaced, and those created.</summary>
    public required Dictionary<long, Way> Ways { get; init; }

    public required HashSet<long> DeletedWays { get; init; }

    const decimal UnitsPerDegree = 10_000_000m;

    /// <summary>The corrections in the file at <paramref name="path"/>, or null where there is none.</summary>
    public static Corrections? Read(string path, string root)
    {
        if (!System.IO.File.Exists(path)) return null;

        var file = Path.GetRelativePath(root, path);
        var change = XDocument.Load(path).Root;
        if (change?.Name != "osmChange") throw new InvalidDataException($"{file}: not an osmChange file.");

        var corrections = new Corrections { File = file, Nodes = [], Ways = [], DeletedWays = [] };
        foreach (var section in change.Elements())
        {
            var action = section.Name.LocalName;
            if (action is not ("create" or "modify" or "delete")) throw new InvalidDataException($"{file}: no osmChange action '{action}'.");

            foreach (var element in section.Elements())
            {
                var id = long.Parse(Attribute(element, "id", file), CultureInfo.InvariantCulture);
                if ((action == "create") != (id < 0))
                {
                    throw new InvalidDataException($"{file}: {element.Name} {id} under {action} — OSM creates at negative ids and changes at its own.");
                }

                switch (element.Name.LocalName, action)
                {
                    case ("way", "delete"):
                        corrections.DeletedWays.Add(id);
                        break;
                    case ("way", _):
                        corrections.Ways[id] = new Way(id,
                            [.. element.Elements("nd").Select(nd => long.Parse(Attribute(nd, "ref", file), CultureInfo.InvariantCulture))], Tags(element, file));
                        break;
                    case ("node", not "delete"):
                        corrections.Nodes[id] = new Node(id, Units(Attribute(element, "lat", file)), Units(Attribute(element, "lon", file)), Tags(element, file));
                        break;
                    default:
                        throw new InvalidDataException($"{file}: {element.Name} under {action} is not corrected — a node goes with the last way passing it.");
                }
            }
        }

        return corrections;
    }

    /// <summary>
    /// Lays the corrections over OSM's answer: each node's place and tags, and each road way, by id.
    /// </summary>
    public void Apply(Dictionary<long, (int Lat, int Lon)> placed, List<Scan.RawWay> roads, Dictionary<long, Dictionary<string, string>> nodeTags)
    {
        var passed = Ways.Values.SelectMany(way => way.Nodes).ToHashSet();
        foreach (var node in Nodes.Values)
        {
            if (node.Id > 0 && !placed.ContainsKey(node.Id) && !passed.Contains(node.Id))
            {
                throw new InvalidDataException($"{File}: node {node.Id} is on no road the survey holds, nor on one corrected.");
            }

            placed[node.Id] = (node.Lat, node.Lon);
            if (node.Tags.Count > 0) nodeTags[node.Id] = node.Tags;
            else nodeTags.Remove(node.Id);
        }

        var at = new Dictionary<long, int>(roads.Count);
        for (var road = 0; road < roads.Count; road++) at[roads[road].Id] = road;
        foreach (var way in Ways.Values)
        {
            foreach (var node in way.Nodes)
            {
                if (!placed.ContainsKey(node)) throw new InvalidDataException($"{File}: way {way.Id} passes node {node}, which neither OSM nor the corrections place.");
            }

            var raw = new Scan.RawWay(way.Id, way.Tags, way.Nodes);
            if (way.Id < 0) roads.Add(raw);
            else if (at.TryGetValue(way.Id, out var road)) roads[road] = raw;
            else throw new InvalidDataException($"{File}: way {way.Id} is no road the survey holds.");
        }

        foreach (var id in DeletedWays)
        {
            if (roads.RemoveAll(way => way.Id == id) == 0) throw new InvalidDataException($"{File}: way {id} is no road the survey holds.");
        }
    }

    static string Attribute(XElement element, string name, string file) =>
        element.Attribute(name)?.Value ?? throw new InvalidDataException($"{file}: {element.Name} with no {name}.");

    static Dictionary<string, string> Tags(XElement element, string file) =>
        element.Elements("tag").ToDictionary(tag => Attribute(tag, "k", file), tag => Attribute(tag, "v", file));

    /// <summary>A coordinate in OSM's integer 1e-7°, read exactly as the decimal it is written as.</summary>
    static int Units(string degrees) => (int)Math.Round(decimal.Parse(degrees, CultureInfo.InvariantCulture) * UnitsPerDegree);
}
