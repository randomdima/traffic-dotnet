using System.Numerics;
using System.Text.Json.Serialization;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>The wreck a car becomes: the same car crumpled, cut from the same tile.</summary>
/// <remarks>
/// <c>Scale</c> is carried rather than assumed to be one — debris makes a wreck a little wider than the
/// car it was. A variant naming no wreck is drawn as the car it was, at the size it was.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WreckFile
{
    public required string Sprite { get; init; }

    public Vector2 Scale { get; init; } = Vector2.One;
}

/// <summary>How this car differs from the nominal one, as multipliers on the shared figures.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class HandlingFile
{
    public float? MaxSpeed { get; init; }

    public float? Acceleration { get; init; }

    public float? Braking { get; init; }

    public float? Cornering { get; init; }
}

/// <summary>One car variant as its file is written.</summary>
/// <remarks>
/// The shape is the file's and not the model's, which is why <see cref="HullM"/> and
/// <see cref="Handling"/> are here although nothing in this engine reads them yet: an unmapped member is
/// refused rather than dropped, so every key a variant carries has to be declared for a misspelt one to
/// be caught. <see cref="CarVariant"/> is the subset the simulation runs on.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CarVariantFile
{
    public required string Id { get; init; }

    public required string Sprite { get; init; }

    public WreckFile? Wreck { get; init; }

    public required Vector2 FootprintM { get; init; }

    public required float FrontAxleM { get; init; }

    public required float RearAxleM { get; init; }

    public required float TrackM { get; init; }

    public required int Drivetrain { get; init; }

    /// <summary>The car's outline in its own axes, anticlockwise. Authored art, read by no code here.</summary>
    public Vector2[] HullM { get; init; } = [];

    public HandlingFile? Handling { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(CarVariantFile))]
internal sealed partial class CarVariantJson : JsonSerializerContext;
