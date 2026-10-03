using System.Text.Json.Serialization;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// <b>A real place exactly as OpenStreetMap holds it</b>: every road in the rectangle round the roads inside its
/// boundary, the surfaces outlining them and the coastline over them, with every id, tag and coordinate OSM
/// stores (GEN-57) — and each road's lanes as OSM's own tagging rules mean them (<see cref="OsmCarriageway"/>).
/// Written by the scanner
/// (src/tools/osmscan/, run by <c>qq osm</c>) into <c>towns/traced/</c>, and read into this engine's metres
/// by <see cref="Survey.Of"/> when the map is opened.
/// </summary>
/// <remarks>
/// <b>Nothing in it is this engine's.</b> The frame and the lanes are OSM's conventions applied by the scanner,
/// so the engine interprets no tag of a lane; which classes are laid and the sea are read off it at load — and
/// a node or way in it is the one OSM has, under the same id, to be looked up or fetched again by.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class OsmExtract
{
    /// <summary>The catalogue name, which is also the file's name and what <c>--map</c> is given.</summary>
    public required string Name { get; init; }

    /// <summary>The line the start menu prints under the name.</summary>
    public required string Description { get; init; }

    /// <summary>Where the extract came from and on what licence, which travels with the data it covers.</summary>
    public required SurveySource Source { get; init; }

    public required OsmNodes Nodes { get; init; }

    /// <summary>The tags of every node on a way here that carries any, by its index into <see cref="Nodes"/>.</summary>
    public required OsmNodeTags[] NodeTags { get; init; }

    /// <summary>The flat frame every node is read into metres by.</summary>
    public required OsmFrame Frame { get; init; }

    /// <summary>Every road and coastline way, in OSM id order.</summary>
    public required OsmWay[] Ways { get; init; }

    /// <summary>
    /// Every surface OSM outlines a road's ground with (<c>area:highway</c>), in OSM id order: the closed ways
    /// that are the carriageway itself where a mapper drew it.
    /// </summary>
    public required OsmWay[] Areas { get; init; }

    /// <summary>Every turn restriction and lane connectivity relation over the roads, members by OSM id.</summary>
    public required OsmRelation[] Relations { get; init; }

    /// <summary>Refuses an extract that cannot describe a place, at the point it is read.</summary>
    public void Check(string what)
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException($"{what}: an extract with no name.");

        var count = Nodes.Id.Length;
        if (Nodes.Lat.Length != count || Nodes.Lon.Length != count)
        {
            throw new InvalidDataException($"{what}: {count} node ids over {Nodes.Lat.Length} latitudes and {Nodes.Lon.Length} longitudes.");
        }

        if (!(Frame.MarginM >= 0) || !(Frame.WidthM > 2 * Frame.MarginM) || !(Frame.HeightM > 2 * Frame.MarginM))
        {
            throw new InvalidDataException($"{what}: a frame of {Frame.WidthM} by {Frame.HeightM} m with a margin of {Frame.MarginM} m.");
        }

        foreach (var way in Ways.Concat(Areas))
        {
            if (way.Nodes.Length < 2) throw new InvalidDataException($"{what}: way {way.Id} of {way.Nodes.Length} nodes.");
            foreach (var node in way.Nodes)
            {
                if ((uint)node >= (uint)count) throw new InvalidDataException($"{what}: way {way.Id} passes node {node} of {count}.");
            }

        }

        foreach (var way in Ways)
        {
            if (way.Tags.ContainsKey("highway") && way.Tag("area") != "yes" && way.Carriageway is not { Lanes.Length: > 0 })
            {
                throw new InvalidDataException($"{what}: road way {way.Id} has no lanes.");
            }
        }

        foreach (var tagged in NodeTags)
        {
            if ((uint)tagged.Node >= (uint)count) throw new InvalidDataException($"{what}: tags for node {tagged.Node} of {count}.");
        }
    }
}

/// <summary>
/// Every node a way here passes, as three columns: its OSM id, and where it stands in OSM's own integer units
/// of <see cref="OsmNodes.UnitsPerDegree"/> — the figure OSM stores, so nothing was rounded on the way in.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class OsmNodes
{
    public const double UnitsPerDegree = 1e7;

    public required long[] Id { get; init; }

    public required int[] Lat { get; init; }

    public required int[] Lon { get; init; }

    public double LatDeg(int node) => Lat[node] / UnitsPerDegree;

    public double LonDeg(int node) => Lon[node] / UnitsPerDegree;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class OsmNodeTags
{
    /// <summary>The node's index into <see cref="OsmExtract.Nodes"/>.</summary>
    public required int Node { get; init; }

    public required Dictionary<string, string> Tags { get; init; }
}

/// <summary>One way as OSM draws it: its id, every tag it carries, and its nodes in its own order.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class OsmWay
{
    public required long Id { get; init; }

    public required Dictionary<string, string> Tags { get; init; }

    /// <summary>Indices into <see cref="OsmExtract.Nodes"/>, so two ways sharing a node share an index.</summary>
    public required int[] Nodes { get; init; }

    /// <summary>
    /// A road way's lanes as OSM means them, read off its tags by the scanner; null on any other way, and on a
    /// road drawn as an area.
    /// </summary>
    public OsmCarriageway? Carriageway { get; init; }

    public string? Tag(string key) => Tags.GetValueOrDefault(key);
}

/// <summary>
/// <b>The flat frame a traced map is laid in</b>: Transverse Mercator about one point, and the map as a
/// rectangle of whole metres round the place's own roads and a margin. x runs east and y south from the map's
/// north-west corner, so a node stands at (east − <see cref="WestM"/>, <see cref="HeightM"/> − (north −
/// <see cref="SouthM"/>)).
/// </summary>
/// <remarks>
/// <b>Every road in the rectangle is the place's</b>, inside its boundary or not, and a way running on past it
/// is held whole: the map lays what stands inside <see cref="MarginM"/> of its edge and cuts a way there.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class OsmFrame
{
    public required double Lat0Deg { get; init; }

    public required double Lon0Deg { get; init; }

    /// <summary>The map's west edge, metres east of <see cref="Lon0Deg"/>'s meridian.</summary>
    public required double WestM { get; init; }

    /// <summary>The map's south edge, metres north of <see cref="Lat0Deg"/>.</summary>
    public required double SouthM { get; init; }

    public required double WidthM { get; init; }

    public required double HeightM { get; init; }

    /// <summary>The ground the map keeps past the rectangle its roads are cut to, on every side (GEN-2b).</summary>
    public required double MarginM { get; init; }

    public TransverseMercator Projection() => new(Lat0Deg, Lon0Deg);

    /// <summary>Where a node stands on the map, x east and y south of its north-west corner.</summary>
    public (double X, double Y) Place(TransverseMercator projection, OsmNodes nodes, int node)
    {
        var (eastM, northM) = projection.Project(nodes.LatDeg(node), nodes.LonDeg(node));
        return (eastM - WestM, HeightM - (northM - SouthM));
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class OsmRelation
{
    public required long Id { get; init; }

    public required Dictionary<string, string> Tags { get; init; }

    public required OsmMember[] Members { get; init; }
}

/// <summary>One member of a relation: a <c>node</c>, <c>way</c> or <c>relation</c> by its OSM id, and its role.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class OsmMember
{
    public required string Type { get; init; }

    public required long Ref { get; init; }

    public required string Role { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SurveySource
{
    /// <summary>The OSM boundary relation the roads were taken inside.</summary>
    public required long Relation { get; init; }

    /// <summary>The moment the OSM database the extract was read from stood at.</summary>
    public required string OsmBase { get; init; }

    public required string Licence { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(OsmExtract))]
internal sealed partial class OsmExtractJson : JsonSerializerContext;
