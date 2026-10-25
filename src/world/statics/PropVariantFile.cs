using System.Text.Json.Serialization;

namespace TrafficSimulation.World.Statics;

/// <summary>One prop look as its file is written: the image, the ground it belongs on, and the size it was drawn at.</summary>
/// <remarks>
/// <b>A look is round or a box, and says which by stating exactly one of <see cref="DiameterM"/> and
/// <see cref="FootprintM"/></b> (GEN-6e), in true metres seen from above. Both or neither is refused as the look is
/// read (<c>PropCatalog</c>), because either would leave the town two answers or none about the ground the look covers.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class PropVariantFile
{
    public required string Id { get; init; }

    /// <summary>Which set the look is in, as <c>CityGen.PropKind</c> numbers them — a placement and not a picture (GEN-6b).</summary>
    public required int Kind { get; init; }

    public required string Sprite { get; init; }

    /// <summary>
    /// A round look's diameter: the picture's longest side, the other following its aspect, because a drawing is taken
    /// whole and is not always square.
    /// </summary>
    public float? DiameterM { get; init; }

    /// <summary>A box's width and depth, its front the depth's near edge where it <see cref="Turns"/>.</summary>
    public float[]? FootprintM { get; init; }

    /// <summary>
    /// <b>The diameter of the disc the prop stands as a body</b> where it stands one (GEN-6e) — a street tree's trunk under
    /// a crown far wider. Left out, the body is the look's girth. A picture may reach past its body; a body may not reach
    /// past its picture's girth, which is what the town clears ground for.
    /// </summary>
    public float? ColliderM { get; init; }

    /// <summary>
    /// The radius a box's corners are drawn rounded at. <b>The art tools' and not the town's</b>: a prop stands a disc
    /// (GEN-6e), so nothing here reads it.
    /// </summary>
    public float CornerRadiusM { get; init; }

    /// <summary>
    /// <b>How often the look is drawn against the others of its kind</b> (GEN-6e), as a weight and not a count: a look of
    /// share 2 is laid twice as often as one of share 1, and a kind whose looks all leave it out draws them evenly. Left out,
    /// it is 1. It says how common the thing is in a town — a litter bin against a builder's skip — and never how big:
    /// the size is <see cref="DiameterM"/>'s or <see cref="FootprintM"/>'s. More than nothing.
    /// </summary>
    public float Share { get; init; } = 1f;

    /// <summary>The art brief's own words for the look, which the engine never reads.</summary>
    public string? Describe { get; init; }

    /// <summary>
    /// Whether the picture has a front — a table, a planter, a skip. <b>Left out is upright</b>, which is
    /// what a tree seen from above is: turning one would only make the same look read as several
    /// (GEN-6b). No wild look may set it.
    /// </summary>
    public bool Turns { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(PropVariantFile))]
internal sealed partial class PropVariantJson : JsonSerializerContext;
