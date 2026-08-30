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
/// How this car's engine, brakes and gearing differ from the nominal one's, as multipliers on the shared
/// figures — <b>the file's shape, where every one of the three is optional</b>. <see cref="CarHandling"/>
/// is what the model reads.
/// </summary>
/// <remarks>
/// <b>What its tyres are worth is not here.</b> A coefficient of friction is a raw term and is stated as
/// one (<see cref="CarVariantFile.TyreFriction"/>); a multiplier on somebody else's grip would be a figure
/// nobody could check against a tyre.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class HandlingFile
{
    public float? MaxSpeed { get; init; }

    public float? Acceleration { get; init; }

    public float? Braking { get; init; }
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

    /// <summary>
    /// <b>How far this one's front wheels turn at the stop</b>, at the road wheel and not at the steering
    /// wheel. The raw term: the circle it turns is derived from it against this body's own wheelbase and
    /// track (<see cref="Agents.Car.Body.CarBuild.TurningCircleM"/>) and is a read-out, never an input.
    /// </summary>
    /// <remarks>
    /// <b>It is why a truck is not a hatchback with a heavier file.</b> Every body here is drawn on a
    /// short wheelbase — a picture at 96 px to the metre puts the arches where the art has them — so at
    /// one lock the whole fleet would turn inside four metres whatever it weighed. A variant that names
    /// none gets the nominal car's lock.
    /// </remarks>
    public float? MaxSteeringDeg { get; init; }

    /// <summary>
    /// <b>What this one's rubber holds on dry tarmac</b> — a coefficient of friction, and the raw term. What
    /// it may pull in m/s² is this times a weight and is worked out from it. Absent is the nominal tyre
    /// (<see cref="TyreFigures.Friction"/>).
    /// </summary>
    /// <remarks>
    /// One coefficient, whichever way this car is pointing and at whatever load: what varies between a
    /// supercar's rubber and a truck's is the compound. <b>It is what this car holds in total</b>, and what
    /// its own proportions decide is which wheel gives up first, never how much the four are worth.
    /// </remarks>
    public float? TyreFriction { get; init; }

    /// <summary>
    /// <b>The size of one of this one's tyres</b>, along its roll and across it. It is what the wheel is
    /// drawn at and the width of the mark it leaves, and it is what this body's <see cref="TrackM"/> was
    /// authored against (CAR-12, <see cref="TyreFigures.ShowsPastTheBodyworkShare"/>). Absent is the
    /// nominal tyre (<see cref="TyreFigures.WheelLengthM"/>).
    /// </summary>
    /// <remarks>
    /// A tyre belongs to the car it is bolted to and not to the town: a van's is not a coupé's, and the
    /// fleet running one size was the nominal car's figure standing where every body's own should be.
    /// <b>The tread pattern is not sized from here</b> — that is one picture the whole fleet shares, and
    /// its pitch is a fact about the sheet (<see cref="TyreFigures.TreadPitchM"/>).
    /// </remarks>
    public Vector2? WheelM { get; init; }

    /// <summary>
    /// <b>One of this one's wheels as the straight-line mass it behaves like</b> (J/r²) — what decides how
    /// violently it spins up or locks against the corner it is carrying. Absent is the nominal wheel
    /// (<see cref="TyreFigures.WheelRotatingMassKg"/>).
    /// </summary>
    public float? WheelRotatingMassKg { get; init; }

    /// <summary>
    /// How high this one carries its weight, which is <b>the whole of what makes a tall body handle like
    /// one</b>: the share of the load that leaves an axle under braking and a flank under cornering is
    /// <c>a·h/(base·g)</c>, so a van's is half again a coupe's on the same tyres. Absent is the nominal
    /// car's.
    /// </summary>
    public float? CgHeightM { get; init; }

    /// <summary>
    /// <b>How much of this one stands on its front axle at rest</b>, which is where the engine is and what
    /// is over it: a transverse front-engined hatchback carries about 0.63, a front-mid sports car about
    /// 0.52, a mid-engined supercar about 0.43, and a box body on a rear deck less still.
    /// </summary>
    /// <remarks>
    /// <b>It decides more about how wide a body runs than its grip or its lock does.</b> The axle carrying
    /// the weight is the one that can put power down and hold a corner, and the light end is the one that
    /// lets go first — so the same car at 0.62 and at 0.38 turns two circles that are not the same shape,
    /// in opposite directions in each gear. Absent is the even split, which is nobody's actual car.
    /// </remarks>
    public float? FrontWeightShare { get; init; }

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
