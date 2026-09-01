using System.Text.Json.Serialization;

namespace TrafficSimulation.World.Statics;

/// <summary>One prop look as its file is written: the image, the ground it belongs on, and the size it was drawn for.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class PropVariantFile
{
    public required string Id { get; init; }

    /// <summary>Which set the look is in, as <c>CityGen.PropKind</c> numbers them — a placement and not a picture (GEN-6b).</summary>
    public required int Kind { get; init; }

    public required string Sprite { get; init; }

    public required float DiameterM { get; init; }

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
