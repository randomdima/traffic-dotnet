using System.Numerics;
using System.Text.Json.Serialization;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Statics;

/// <summary>One rectangle of a roof's own walls, in the picture's axes and measured off the picture.</summary>
/// <param name="AtM">Its middle, from the middle of the footprint, <c>+y</c> being the door's side.</param>
internal sealed class BuildingPartFile
{
    public required Vector2 AtM { get; init; }

    public required Vector2 SizeM { get; init; }
}

/// <summary>One roof as its file is written: the image, the footprint it was drawn at, and its walls.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BuildingVariantFile
{
    public required string Id { get; init; }

    public required string Sprite { get; init; }

    public required Vector2 FootprintM { get; init; }

    /// <summary>
    /// <b>The rectangles this roof is actually built of</b> (OBJ-5a) — one for a plain block, several for
    /// anything with a wing, a courtyard or a cut corner. A variant that names none is collided as the
    /// whole of its footprint, which is what every building was before the parts existed.
    /// </summary>
    public BuildingPartFile[] PartsM { get; init; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(BuildingVariantFile))]
internal sealed partial class BuildingVariantJson : JsonSerializerContext;
