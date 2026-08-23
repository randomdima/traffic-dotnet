using System.Text.Json.Serialization;

namespace TrafficSimulation.World.Statics;

/// <summary>One prop look as its file is written: the image, what kind of thing it is, and the size it was drawn for.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class PropVariantFile
{
    public required string Id { get; init; }

    public required int Kind { get; init; }

    public required string Sprite { get; init; }

    public required float DiameterM { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(PropVariantFile))]
internal sealed partial class PropVariantJson : JsonSerializerContext;
