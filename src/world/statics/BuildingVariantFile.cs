using System.Numerics;
using System.Text.Json.Serialization;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Statics;

/// <summary>One rectangle of a roof's own walls, in the picture's axes and measured off the picture.</summary>
internal sealed class BuildingPartFile
{
    /// <summary>Its middle, from the middle of the footprint, <c>+y</c> being the door's side.</summary>
    public required Vector2 AtM { get; init; }

    public required Vector2 SizeM { get; init; }
}

/// <summary>
/// One roof as its file is written: the image, the footprint it was drawn at, and its walls — and for a prefab
/// (GEN-57), what it is drawn as and what the picture is to show.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BuildingVariantFile
{
    public required string Id { get; init; }

    /// <summary>The picture, beside this file; none for a prefab not yet drawn, which is drawn as a plain block of its look.</summary>
    public string? Sprite { get; init; }

    public required Vector2 FootprintM { get; init; }

    /// <summary>
    /// The radius its footprint's corners are rounded at (OBJ-2): a prefab is one rounded rectangle, drawn and collided
    /// as that — up to half its short side, which is a stadium or, square, a circle. Nought for a square-cornered roof.
    /// </summary>
    public float CornerRadiusM { get; init; }

    /// <summary>A prefab's look (<see cref="CityGen.BuildingLook"/>, lower-cased): what a traced footprint is fitted to it by.</summary>
    public string? Look { get; init; }

    /// <summary>What the picture is to show, word by word — the look, the storeys, the roof's shape and what it is laid in.</summary>
    public string[] Tags { get; init; } = [];

    /// <summary>The roof as seen from above, in a line, for whoever draws its picture.</summary>
    public string? Describe { get; init; }

    /// <summary>
    /// <b>The rectangles this roof is actually built of</b> (OBJ-5a) — one for a plain block, several for
    /// anything with a wing, a courtyard or a cut corner. A variant that names none is collided as the
    /// whole of its footprint.
    /// </summary>
    public BuildingPartFile[] PartsM { get; init; } = [];

    /// <summary>The fans its picture draws, cut off it (<c>qq props</c>) and drawn turning over it (OBJ-6); none on most.</summary>
    public DrawnFan[] Fans { get; init; } = [];

    /// <summary>
    /// Its picture's chimneys, which may smoke (OBJ-7), each where its stack's pots are — <c>X, Y</c> a pot, so a twin
    /// stack is four numbers — read off it by eye (<c>qq chimneys</c>), from the picture's middle with <c>+y</c> down it;
    /// none on most.
    /// </summary>
    public float[][] Chimneys { get; init; } = [];

    /// <summary>How wide its chimneys' pots are drawn on its picture, each, read off it with them; none for a house's (<c>ChimneyFigures.PotWidthM</c>).</summary>
    public float? PotWidthM { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(BuildingVariantFile))]
internal sealed partial class BuildingVariantJson : JsonSerializerContext;
