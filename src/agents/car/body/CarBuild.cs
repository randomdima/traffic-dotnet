using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// <b>The car a driver is actually driving</b>: its own body, its own axles and what its own tyres and
/// gearing are worth — one variant's figures, resolved against the nominal car's once and spent by
/// everything that decides for that car afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <b>The town is designed for the nominal car and driven by these.</b> A junction's radius, a lane's
/// width and a bay's size are the road's, sized against <see cref="CarFigures"/> and the same whoever
/// turns up (CAR-11); what a car does inside that road is its own — a heavier car stops later, a longer
/// one swings wider, and a car with a bigger turning circle needs more of the street to get into a space.
/// The line a car is given is a recommendation and the body on it is this.
/// </para>
/// <para>
/// <b>Everything derived is derived here, once.</b> A figure worked out per tick from a variant would be
/// the same figure worked out sixty times a second for a body whose dimensions never change; the build is
/// made when the car is stood up and read <c>in</c> from then on.
/// </para>
/// </remarks>
internal readonly record struct CarBuild
{
    public required float LengthM { get; init; }

    public required float WidthM { get; init; }

    /// <summary>
    /// <b>The shape this body is collided as</b> (CAR-12b), reaching this far along its own axes with
    /// <see cref="CornerRadiusM"/> off each corner. It is inside the picture rather than around it, so it
    /// is smaller than <see cref="LengthM"/> by <see cref="WidthM"/> — which stay what the car is drawn,
    /// driven and parked at.
    /// </summary>
    public required Vector2 CollisionSizeM { get; init; }

    public required float CornerRadiusM { get; init; }

    public required float MassKg { get; init; }

    /// <summary>Front axle to rear axle, which is what a steering angle is worth (CAR-4a).</summary>
    public required float WheelbaseM { get; init; }

    /// <summary>How far each wheel stands off the car's own middle, which is half its track.</summary>
    public required float HalfTrackM { get; init; }

    /// <summary>
    /// How far the middle of the body stands ahead of the rear axle — <b>the car's own, and not half a
    /// wheelbase</b>: where the axles sit under a body is a fact about that body, and a van with its rear
    /// axle far back carries most of itself in front of the point its line is drawn for.
    /// </summary>
    public required float CentreAheadOfAxleM { get; init; }

    /// <summary>
    /// <b>Where this body's tow beam is hinged</b> (EVA-5), ahead of the middle of it — negative for every
    /// recovery vehicle, since the arm is bolted to the deck behind the cab. Zero for a car that tows nothing.
    /// </summary>
    public required float TowHingeAheadOfCentreM { get; init; }

    /// <summary>
    /// And how far past that hinge the fork reaches, which is the length the coupling is held at. It is the
    /// picture's own measurement (<see cref="CarTowBeam.ReachM"/>), so the arm on screen and the arm in the
    /// arithmetic are one number.
    /// </summary>
    public required float TowReachM { get; init; }

    /// <summary>
    /// <b>How far from the middle of this car a tow takes hold of it</b> (EVA-5): a fixed distance inside
    /// whichever end the arm caught, so that end goes up on the fork and its pair of wheels leaves the
    /// ground. Every car in the catalogue is taken hold of the same distance in from either end, which is
    /// what an underlift does; which end it is, is the tow's own fact (<see cref="TowBar.EndSign"/>).
    /// </summary>
    public required float TowGripFromTheMiddleM { get; init; }

    public required float CgHeightM { get; init; }

    public required float DrivenFrontShare { get; init; }

    public required float MaxSpeedMps { get; init; }

    public required float ReverseMaxMps { get; init; }

    public required float AccelerationMps2 { get; init; }

    /// <summary>What the pedal may ask for. What actually stops the car is <see cref="UtmostBrakingMps2"/>.</summary>
    public required float BrakingMps2 { get; init; }

    /// <summary>What this car's tyres hold across the roll on clean ground — the one figure a corner is taken on.</summary>
    public required float GripMps2 { get; init; }

    /// <summary>And along it, which is what a stop is spent from.</summary>
    public required float LongGripMps2 { get; init; }

    public required float MaxSteerRad { get; init; }

    public required float PedalRateMps3 { get; init; }

    /// <summary>How fast this car's wheel travels, which is its own lock over the rack's own travel time (CAR-3a).</summary>
    public required float SteerRateRadPerS { get; init; }

    public required float TurningRadiusM { get; init; }

    /// <summary>The radius this car's own parking templates are drawn at: its circle, with the steering off its stop.</summary>
    public required float ParkingTemplateRadiusM { get; init; }

    /// <summary>And how much straight it ends on, which is what puts <em>this</em> body in the bay square.</summary>
    public required float ParkingStraightensUpM { get; init; }

    /// <summary>How far ahead this car has to be able to see: its own stopping distance from its own top speed.</summary>
    public required float SightM { get; init; }

    public required float LookaheadFloorM { get; init; }

    public required float LookaheadCeilingM { get; init; }

    public required float ProjectionWindowM { get; init; }

    /// <summary>The ground this body keeps around itself, which is its own width or its own standstill gap.</summary>
    public required float BodyMarginM { get; init; }

    public required float TailMarginM { get; init; }

    public required float CrossingPaceMps { get; init; }

    public required float CrossingStandOffM { get; init; }

    public float HalfLengthM => LengthM * 0.5f;

    /// <summary>How far it reaches to either side of the line it is driven along.</summary>
    public float FlankM => WidthM * 0.5f;

    /// <summary>How far the nose stands ahead of the axle its line is drawn for, and the tail behind it.</summary>
    public float NoseAheadOfAxleM => CentreAheadOfAxleM + HalfLengthM;

    public float TailBehindAxleM => HalfLengthM - CentreAheadOfAxleM;

    /// <summary>What it may be braking at at all on this ground: the pedal's cap, or what the patch puts down.</summary>
    public float UtmostBrakingMps2(float groundCoefficient) =>
        MathF.Min(BrakingMps2, LongGripMps2 * groundCoefficient);

    /// <summary>
    /// Where the wheel is after a tick of winding it from <paramref name="fromRad"/> towards
    /// <paramref name="wantedRad"/> (CAR-3a): the angle asked for, or as far towards it as the rack got.
    /// </summary>
    /// <remarks>
    /// <b>It is the body's and not the driver's</b>, so a hand on a key and a follower on a line are both
    /// held to it — the difference between them is what they ask for and never how fast it arrives.
    /// </remarks>
    public float WheelWoundTo(float fromRad, float wantedRad, float dtS)
    {
        var travelRad = SteerRateRadPerS * dtS;
        return Math.Clamp(wantedRad, fromRad - travelRad, fromRad + travelRad);
    }

    /// <summary>The widest a line has to be drawn for <em>this</em> car to hold this speed round it.</summary>
    public float CorneringRadiusM(float atMps, float groundCoefficient, float gripMargin) =>
        atMps * atMps / (GripMps2 * groundCoefficient * gripMargin);

    /// <summary>
    /// One variant as the car it is: its own dimensions and mass, and the nominal figures scaled by what
    /// its file says it is worth against them.
    /// </summary>
    public static CarBuild Of(SimConfig config, in CarVariant variant) => Resolve(
        config, variant.FootprintM.X, variant.FootprintM.Y, variant.CollisionSizeM, variant.CornerRadiusM,
        variant.MassKg, variant.WheelbaseM, variant.HalfTrackM, -variant.RearAxleM, variant.DrivenFrontShare,
        variant.Handling, variant.Beam?.PivotM.X ?? 0f, variant.Beam?.ReachM ?? 0f);

    /// <summary>
    /// <b>The nominal car, with one variant's drive layout on it</b> — the proving ground's car and nobody
    /// else's (CAR-11a). A lap whose cars differ in weight as well as in layout is three anecdotes rather
    /// than a comparison, so the instrument stands its cars on the figures the town was sized against and
    /// varies the one thing it is measuring.
    /// </summary>
    public static CarBuild Nominal(SimConfig config, float drivenFrontShare) => Resolve(
        config, config.Car.LengthM, config.Car.WidthM,
        new Vector2(config.Car.LengthM, config.Car.WidthM), cornerRadiusM: 0f, config.Car.MassKg,
        config.Car.WheelbaseM, config.CarTrackM * 0.5f, config.CarCentreAheadOfAxleM, drivenFrontShare,
        CarHandling.Nominal, towHingeAheadOfCentreM: 0f, towReachM: 0f);

    static CarBuild Resolve(
        SimConfig config, float lengthM, float widthM, Vector2 collisionSizeM, float cornerRadiusM, float massKg,
        float wheelbaseM, float halfTrackM, float centreAheadOfAxleM, float drivenFrontShare,
        in CarHandling handling, float towHingeAheadOfCentreM, float towReachM)
    {
        var maxSteerRad = config.Car.MaxSteeringDeg * MathF.PI / 180f;
        var turningRadiusM = wheelbaseM / MathF.Tan(maxSteerRad);
        var brakingMps2 = config.Car.BrakingMps2 * handling.Braking;
        var gripMps2 = config.Tyre.GripMps2 * handling.Cornering;
        var longGripMps2 = gripMps2 * config.Tyre.LongAxisFactor;
        var maxSpeedMps = config.Car.MaxSpeedMps * handling.MaxSpeed;
        var accelerationMps2 = config.Car.AccelerationMps2 * handling.Acceleration;

        return new CarBuild
        {
            LengthM = lengthM,
            WidthM = widthM,
            CollisionSizeM = collisionSizeM,
            CornerRadiusM = cornerRadiusM,
            MassKg = massKg,
            WheelbaseM = wheelbaseM,
            HalfTrackM = halfTrackM,
            CentreAheadOfAxleM = centreAheadOfAxleM,
            TowHingeAheadOfCentreM = towHingeAheadOfCentreM,
            TowReachM = towReachM,
            TowGripFromTheMiddleM = (lengthM * 0.5f) - config.Evacuator.TowGripInsideTheEndM,
            CgHeightM = config.Car.CgHeightM,
            DrivenFrontShare = drivenFrontShare,
            MaxSpeedMps = maxSpeedMps,
            ReverseMaxMps = config.Car.ReverseMaxMps,
            AccelerationMps2 = accelerationMps2,
            BrakingMps2 = brakingMps2,
            GripMps2 = gripMps2,
            LongGripMps2 = longGripMps2,
            MaxSteerRad = maxSteerRad,
            PedalRateMps3 = (accelerationMps2 + brakingMps2) / config.Driving.PedalTravelS,
            SteerRateRadPerS = 2f * maxSteerRad / config.Driving.WheelTravelS,
            TurningRadiusM = turningRadiusM,
            ParkingTemplateRadiusM = turningRadiusM * config.Car.ParkingTemplateArcMargin,
            ParkingStraightensUpM = lengthM * config.Road.ParkingStraightensUpInCarLengths,
            SightM = maxSpeedMps * maxSpeedMps
                / (2f * MathF.Min(brakingMps2, longGripMps2) * config.Driving.GripMargin),
            LookaheadFloorM = lengthM * config.Driving.LookaheadFloorInCarLengths,
            LookaheadCeilingM = lengthM * config.Driving.LookaheadCeilingInCarLengths,
            ProjectionWindowM = lengthM * config.Driving.ProjectionWindowInCarLengths,
            BodyMarginM = MathF.Max(widthM, lengthM * config.Driving.StandstillGapInCarLengths),
            TailMarginM = MathF.Max(widthM, lengthM * config.Driving.StandstillGapInCarLengths)
                * config.Driving.TailMarginShare,
            CrossingPaceMps = lengthM * config.Driving.CrossingPaceInCarLengthsPerS,
            CrossingStandOffM = widthM * config.Driving.CrossingStandOffInCarWidths,
        };
    }
}

/// <summary>
/// The builds of every look in the catalogue, made once for a town and read by variant. <b>A build is a
/// fact about a look and not about a car</b>: nineteen of them cover any number of cars, and a car is its
/// variant's index like it is for the sprite it is drawn with.
/// </summary>
internal sealed class CarBuilds
{
    readonly CarBuild[] _byVariant;

    CarBuilds(CarBuild[] byVariant) => _byVariant = byVariant;

    /// <summary>Each look as the car it is drawn as.</summary>
    public static CarBuilds OfTheFleet(SimConfig config, CarCatalog catalogue)
    {
        var builds = new CarBuild[catalogue.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++)
        {
            builds[variant] = CarBuild.Of(config, catalogue.Variants[variant]);
        }

        return new CarBuilds(builds);
    }

    /// <summary>
    /// And each of them as the nominal car driving through its own variant's end, which is the proving
    /// ground's fleet (CAR-11a) and no town's.
    /// </summary>
    public static CarBuilds OfTheNominalCar(SimConfig config, CarCatalog catalogue)
    {
        var builds = new CarBuild[catalogue.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++)
        {
            builds[variant] = CarBuild.Nominal(config, catalogue.Variants[variant].DrivenFrontShare);
        }

        return new CarBuilds(builds);
    }

    public ref readonly CarBuild Of(int variant) => ref _byVariant[variant % _byVariant.Length];
}
