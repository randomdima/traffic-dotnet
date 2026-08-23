using System.Numerics;
using System.Text.Json.Serialization;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Statics;

/// <summary>One roof as its file is written: the image, and the footprint it was drawn at.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BuildingVariantFile
{
    public required string Id { get; init; }

    public required string Sprite { get; init; }

    public required Vector2 FootprintM { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(BuildingVariantFile))]
internal sealed partial class BuildingVariantJson : JsonSerializerContext;
