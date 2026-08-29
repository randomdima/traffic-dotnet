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

/// <summary>
/// One of an arm's two pictures (EVA-5): the file it is cut from, the size it is drawn at, and where along
/// it the hinge falls. <see cref="CarTowArm"/> is what the model reads.
/// </summary>
/// <remarks>
/// <b>Both figures are measured off the picture</b> and belong beside it for the reason a lamp lens does
/// (CAR-14a). The arm's own axis runs hinge to fork, so <c>hingeAtM</c> is negative for a picture that
/// carries its winch behind the hinge.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TowArmFile
{
    public required string Sprite { get; init; }

    public required Vector2 SizeM { get; init; }

    /// <summary>Where the hinge sits along the picture, from the middle of it.</summary>
    public required float HingeAtM { get; init; }
}

/// <summary>
/// <b>The arm a recovery vehicle pulls on</b> (EVA-5), as the file states it: the two pictures it is drawn
/// in, how far past the hinge the fork reaches when it is out, and where on the body the hinge is bolted.
/// <see cref="CarTowBeam"/> is what the model reads.
/// </summary>
/// <remarks>
/// <b>The reach is the extended picture's own measurement</b> — where an artist drew the fork, not a number
/// that could be chosen — and the collapsed picture states no reach because nothing is ever held on it.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TowBeamFile
{
    /// <summary>The arm reaching out, which is the picture a vehicle with something on the fork draws.</summary>
    public required TowArmFile Extended { get; init; }

    /// <summary>And the arm drawn in over its own deck, which is what an empty one draws.</summary>
    public required TowArmFile Collapsed { get; init; }

    /// <summary>And how far past the hinge the middle of the fork is, which is what the tow is held at.</summary>
    public required float ReachM { get; init; }

    /// <summary>Where the hinge is bolted on the body, <c>+x</c> being the nose.</summary>
    public required Vector2 PivotM { get; init; }
}

/// <summary>
/// How this car differs from the nominal one, as multipliers on the shared figures — <b>the file's shape,
/// where every one of the four is optional</b>. <see cref="CarHandling"/> is what the model reads.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class HandlingFile
{
    public float? MaxSpeed { get; init; }

    public float? Acceleration { get; init; }

    public float? Braking { get; init; }

    public float? Cornering { get; init; }
}

/// <summary>
/// One lamp lens as the variant's file states it: which fitting it is, and the section of the picture
/// it covers, in the body's own metres. <see cref="CarLens"/> is what the model reads.
/// </summary>
/// <remarks>
/// <c>fitting</c> is one of <c>rear</c>, <c>indicator</c>, <c>beaconRed</c> and <c>beaconBlue</c>; a
/// beacon end names the colour its art draws at rest, because that is the colour it goes back to when
/// the priority is given up.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class LampFile
{
    public required string Fitting { get; init; }

    public required Vector2 AtM { get; init; }

    public required Vector2 SizeM { get; init; }
}

/// <summary>The rounded box a car is collided as, in its own axes and measured off its picture.</summary>
/// <remarks>
/// <see cref="SizeM"/> is what the shape <em>reaches</em>, rounding included, so it is comparable with
/// the footprint beside it: the core the solver holds is this less the radius on both axes.
/// </remarks>
internal sealed class CollisionFile
{
    public required Vector2 SizeM { get; init; }

    public required float CornerRadiusM { get; init; }
}

/// <summary>One car variant as its file is written.</summary>
/// <remarks>
/// The shape is the file's and not the model's: an unmapped member is refused rather than dropped, so
/// every key a variant carries has to be declared for a misspelt one to be caught, whether or not the
/// simulation reads it. <see cref="CarVariant"/> is the subset the simulation runs on.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CarVariantFile
{
    public required string Id { get; init; }

    public required string Sprite { get; init; }

    public WreckFile? Wreck { get; init; }

    /// <summary>The arm this one tows on (EVA-5). A car that tows nothing names none.</summary>
    public TowBeamFile? Beam { get; init; }

    public required Vector2 FootprintM { get; init; }

    /// <summary>What this car weighs empty, which is the type's figure and not the nominal car's.</summary>
    public required float MassKg { get; init; }

    /// <summary>Where the front wheel centres stand along the body, <c>+x</c> being the nose.</summary>
    public required float FrontAxleM { get; init; }

    /// <summary>And the rear ones, which stand behind the middle of the body and so read negative.</summary>
    public required float RearAxleM { get; init; }

    /// <summary>
    /// The whole distance between the wheel centres on an axle, as <see cref="SimConfig.CarTrackM"/> is.
    /// <b>It is the width of the bodywork over the axles</b> (CAR-12): the wheel centres stand on this car's
    /// own flanks, as the nominal car's stand on the corners of its footprint, so half of each tyre is
    /// outside the panels and can be seen. It is measured off the picture with the mirrors ignored, which
    /// is why a police car — whose sheet is as wide as its mirrors — carries a narrower track than a van
    /// drawn to the same sheet.
    /// </summary>
    public required float TrackM { get; init; }

    public required int Drivetrain { get; init; }

    /// <summary>Whether this one is built not to break (PHY-4b). Absent is the ordinary car, which breaks.</summary>
    public bool Unbreakable { get; init; }

    /// <summary>
    /// <b>The shape this car is collided as</b> (CAR-12b): the largest rounded box that fits inside the
    /// picture, measured off it. A variant that names none is collided as the whole of its footprint with
    /// square corners, which is what every car was before the shape was fitted.
    /// </summary>
    public CollisionFile? CollisionM { get; init; }

    /// <summary>The car's outline in its own axes, anticlockwise. Authored art, read by no code here.</summary>
    public Vector2[] HullM { get; init; } = [];

    /// <summary>
    /// The lenses this car's art draws (CAR-14a), which are the only lamps it can show. A rectangle
    /// measured over a lamp the artist painted takes the section of it the fitting is — a front
    /// cluster's flank end for an indicator — and one is painted onto bodywork only for a nose that
    /// draws no lamp at all (CAR-14b).
    /// </summary>
    public LampFile[] Lamps { get; init; } = [];

    public HandlingFile? Handling { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(CarVariantFile))]
internal sealed partial class CarVariantJson : JsonSerializerContext;
