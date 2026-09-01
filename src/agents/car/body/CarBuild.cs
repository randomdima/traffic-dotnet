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

    /// <summary>
    /// <b>How much of this body stands on its front axle at rest</b> (CAR-11), before the pedals move any
    /// of it. It is the balance every other figure is spent against: the loaded axle is the one that can
    /// put power down and hold a corner, and the light end is the one that lets go.
    /// </summary>
    public required float FrontWeightShare { get; init; }

    public required float DrivenFrontShare { get; init; }

    public required float MaxSpeedMps { get; init; }

    public required float ReverseMaxMps { get; init; }

    /// <summary>
    /// What the pedal may ask for, which is <see cref="DrivenTractionMps2"/> times the headroom this car's
    /// engine has over its own rubber (CAR-45). What it actually pulls away at is the smaller of the two.
    /// </summary>
    public required float AccelerationMps2 { get; init; }

    /// <summary>
    /// <b>The most this car can pull away at</b>: what its driven axle puts down at the static load, which
    /// is what a car that has not transferred anything yet stands on (CAR-45).
    /// </summary>
    /// <remarks>
    /// Off the same division the model makes — drive is placed by layout and divided by the load of the axle
    /// it is placed on (<see cref="TyreModel"/>) — so the driven end of a car carrying half its weight puts
    /// down half its grip, and an axle asked for more than this slides instead of pushing. Where both axles
    /// are driven it is the worse of the two, since either sliding is the car sliding.
    /// </remarks>
    public required float DrivenTractionMps2 { get; init; }

    /// <summary>What the pedal may ask for. What actually stops the car is <see cref="UtmostBrakingMps2"/>.</summary>
    public required float BrakingMps2 { get; init; }

    /// <summary>
    /// What this car's tyres hold on clean ground — the one figure a corner is taken on and a stop is spent
    /// from, since the coefficient does not know which way the body is pointing.
    /// </summary>
    public required float GripMps2 { get; init; }

    /// <summary>
    /// <b>One of this car's tyres</b>, along its roll and across it — what the wheel is drawn at and how
    /// wide a mark it leaves. This body's own, because a tyre is bolted to a car and not to a town.
    /// </summary>
    public required float WheelLengthM { get; init; }

    public required float WheelWidthM { get; init; }

    /// <summary>
    /// And one of them as the straight-line mass it behaves like (J/r²), which is what decides how
    /// violently it spins up or locks against the corner it is carrying.
    /// </summary>
    public required float WheelRotatingMassKg { get; init; }

    public required float MaxSteerRad { get; init; }

    public required float PedalRateMps3 { get; init; }

    /// <summary>How fast this car's wheel travels, which is its own lock over the rack's own travel time (CAR-3a).</summary>
    public required float SteerRateRadPerS { get; init; }

    public required float TurningRadiusM { get; init; }

    /// <summary>
    /// <b>The circle this body turns kerb to kerb</b>, which is the figure a maker quotes and the one a
    /// person can measure off a car. It is a read-out and nothing decides on it: what is authored is the
    /// lock at the road wheel, and this is that lock met with this body's wheelbase and track.
    /// </summary>
    public float TurningCircleM
    {
        get
        {
            var outerM = TurningRadiusM + HalfTrackM;
            return 2f * MathF.Sqrt((outerM * outerM) + (WheelbaseM * WheelbaseM));
        }
    }

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

    public required float CrossingStandOffM { get; init; }

    public float HalfLengthM => LengthM * 0.5f;

    /// <summary>How far it reaches to either side of the line it is driven along.</summary>
    public float FlankM => WidthM * 0.5f;

    /// <summary>How far the nose stands ahead of the axle its line is drawn for, and the tail behind it.</summary>
    public float NoseAheadOfAxleM => CentreAheadOfAxleM + HalfLengthM;

    public float TailBehindAxleM => HalfLengthM - CentreAheadOfAxleM;

    /// <summary>What it may be braking at at all on this ground: the pedal's cap, or what the patch puts down.</summary>
    public float UtmostBrakingMps2(float groundCoefficient) =>
        MathF.Min(BrakingMps2, GripMps2 * groundCoefficient);

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
    public static CarBuild Of(SimConfig config, in CarVariant variant)
    {
        // The balance is settled before the layout, because a car that drives all four wheels places its
        // drive by the load it will be spent against rather than evenly.
        var frontWeightShare = Math.Clamp(variant.FrontWeightShare ?? config.Car.StaticFrontShare, 0f, 1f);
        return Resolve(
            config, variant.FootprintM.X, variant.FootprintM.Y, variant.CollisionSizeM, variant.CornerRadiusM,
            variant.MassKg, variant.WheelbaseM, variant.HalfTrackM, -variant.RearAxleM,
            variant.DrivenFrontShare(frontWeightShare),
            variant.Handling, variant.Beam?.PivotM.X ?? 0f, variant.Beam?.ReachM ?? 0f,
            variant.MaxSteeringDeg, variant.TyreFriction, variant.CgHeightM, frontWeightShare,
            variant.WheelM, variant.WheelRotatingMassKg);
    }

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
        CarHandling.Nominal, towHingeAheadOfCentreM: 0f, towReachM: 0f, maxSteeringDeg: null, tyreFriction: null,
        cgHeightM: null, Math.Clamp(config.Car.StaticFrontShare, 0f, 1f), wheelM: null, wheelRotatingMassKg: null);

    /// <summary>
    /// <b>Where a steering angle stops describing a circle.</b> At a right angle the front wheels point
    /// across the car, <c>wheelbase/tan</c> is nothing, and past it the tangent changes sign and the radius
    /// comes back <em>negative</em> — a turning circle on the wrong side of a car that is no longer turning.
    /// </summary>
    /// <remarks>
    /// <b>Not a figure anybody chose but the bound the arithmetic has</b>, held a degree off so a radius
    /// stays a length rather than a point. Nothing in the fleet is near it — every authored lock is under
    /// 32° — and the only thing that reaches it is the panel's own
    /// <see cref="TrimFigures.SteeringLock"/>, which runs to ten times shipped and used to take the whole
    /// parking geometry negative somewhere above two and a half.
    /// </remarks>
    const float MostSteerRad = 89f * MathF.PI / 180f;

    static CarBuild Resolve(
        SimConfig config, float lengthM, float widthM, Vector2 collisionSizeM, float cornerRadiusM, float massKg,
        float wheelbaseM, float halfTrackM, float centreAheadOfAxleM, float drivenFrontShare,
        in CarHandling handling, float towHingeAheadOfCentreM, float towReachM, float? maxSteeringDeg,
        float? tyreFriction, float? cgHeightM, float frontShare, Vector2? wheelM,
        float? wheelRotatingMassKg)
    {
        // Everything a reader would look up is worked out from a raw term and never the other way about:
        // the lock is the angle at the road wheel and the circle is what it comes to, the friction is a
        // coefficient and the grip is what it is worth.
        //
        // The one trim spent here is the road's (TrimFigures): the coefficient between rubber and tarmac is
        // a fact about a surface every body in the town stands on, so a panel may move it and every look
        // keeps its own compound underneath. What this car *is* — its lock, its height, its mass, its
        // engine — is its own file's and has no dial over it.
        var maxSteerRad = MathF.Min(
            (maxSteeringDeg ?? config.Car.MaxSteeringDeg) * MathF.PI / 180f, MostSteerRad);
        var turningRadiusM = wheelbaseM / MathF.Tan(maxSteerRad);
        var brakingMps2 = config.CarBrakingMps2 * handling.Braking;
        var friction = (tyreFriction ?? config.Tyre.Friction) * config.Trim.Friction;
        var gripMps2 = friction * config.Tyre.StandardGravityMps2;
        var maxSpeedMps = config.Car.MaxSpeedMps * handling.MaxSpeed;

        // The engine is authored as a headroom over the rubber and not as an acceleration, so that a pedal
        // stays a demand a tyre can be measured against when the compound underneath it moves (CAR-45).
        var drivenTractionMps2 = DrivenTraction(gripMps2, frontShare, drivenFrontShare);
        var accelerationMps2 = drivenTractionMps2 * config.Car.DrivePedalInDrivenGrips * handling.Acceleration;

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
            CgHeightM = cgHeightM ?? config.Car.CgHeightM,

            // A share is a share, and a car standing on more than all of itself is not a body the loads
            // can split.
            FrontWeightShare = frontShare,
            DrivenFrontShare = drivenFrontShare,
            MaxSpeedMps = maxSpeedMps,
            ReverseMaxMps = config.Car.ReverseMaxMps,
            AccelerationMps2 = accelerationMps2,
            DrivenTractionMps2 = drivenTractionMps2,
            BrakingMps2 = brakingMps2,
            GripMps2 = gripMps2,
            WheelLengthM = wheelM?.X ?? config.Tyre.WheelLengthM,
            WheelWidthM = wheelM?.Y ?? config.Tyre.WheelWidthM,
            WheelRotatingMassKg = wheelRotatingMassKg ?? config.Tyre.WheelRotatingMassKg,
            MaxSteerRad = maxSteerRad,
            PedalRateMps3 = (accelerationMps2 + brakingMps2) / config.Driving.PedalTravelS,
            SteerRateRadPerS = 2f * maxSteerRad / config.Driving.WheelTravelS,
            TurningRadiusM = turningRadiusM,
            ParkingTemplateRadiusM = turningRadiusM * config.Car.ParkingTemplateArcMargin,
            ParkingStraightensUpM = lengthM * config.Road.ParkingStraightensUpInCarLengths,
            SightM = maxSpeedMps * maxSpeedMps
                / (2f * MathF.Min(brakingMps2, gripMps2) * config.Driving.GripMargin),
            LookaheadFloorM = lengthM * config.Driving.LookaheadFloorInCarLengths,
            LookaheadCeilingM = lengthM * config.Driving.LookaheadCeilingInCarLengths,
            ProjectionWindowM = lengthM * config.Driving.ProjectionWindowInCarLengths,
            BodyMarginM = MathF.Max(widthM, lengthM * config.Driving.StandstillGapInCarLengths),
            TailMarginM = MathF.Max(widthM, lengthM * config.Driving.StandstillGapInCarLengths)
                * config.Driving.TailMarginShare,
            CrossingStandOffM = widthM * config.Driving.CrossingStandOffInCarWidths,
        };
    }

    /// <summary>
    /// <see cref="DrivenTractionMps2"/>: the pedal at which the driven axle's quotient — the drive it is
    /// placed by layout, over the load it stands on — first reaches what the patch holds.
    /// </summary>
    static float DrivenTraction(float gripMps2, float frontWeightShare, float drivenFrontShare)
    {
        var mostMps2 = float.PositiveInfinity;
        if (drivenFrontShare > 0f) mostMps2 = gripMps2 * frontWeightShare / drivenFrontShare;
        if (drivenFrontShare < 1f)
        {
            mostMps2 = MathF.Min(mostMps2, gripMps2 * (1f - frontWeightShare) / (1f - drivenFrontShare));
        }

        // A car nothing drives has no pedal at all, and a car standing on nothing at the driven end has no
        // pedal it can use: either way the figure is a zero rather than an infinity for a caller to trip on.
        return float.IsFinite(mostMps2) ? mostMps2 : 0f;
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
    readonly CarCatalog _catalogue;

    /// <summary>Whether these are the nominal car's, which is what a rebuild has to know to make them again.</summary>
    readonly bool _nominal;

    CarBuilds(CarCatalog catalogue, bool nominal)
    {
        _catalogue = catalogue;
        _nominal = nominal;
        _byVariant = new CarBuild[catalogue.SheetCount];
    }

    /// <summary>Each look as the car it is drawn as.</summary>
    public static CarBuilds OfTheFleet(SimConfig config, CarCatalog catalogue) => Made(config, catalogue, nominal: false);

    /// <summary>
    /// And each of them as the nominal car driving through its own variant's end, which is the proving
    /// ground's fleet (CAR-11a) and no town's.
    /// </summary>
    public static CarBuilds OfTheNominalCar(SimConfig config, CarCatalog catalogue) =>
        Made(config, catalogue, nominal: true);

    static CarBuilds Made(SimConfig config, CarCatalog catalogue, bool nominal)
    {
        var builds = new CarBuilds(catalogue, nominal);
        builds.Resolve(config);
        return builds;
    }

    /// <summary>
    /// <b>Every look made again from the figures as they stand now</b>, into the same array — so a fleet
    /// already holding this is holding the new figures rather than a stale copy.
    /// </summary>
    /// <remarks>
    /// It is the figures panel's and nothing else calls it: what a variant is worth does not change while
    /// a town is running unless somebody is turning it. Allocation-free by construction, which is what
    /// lets it be called on a town rather than only on the way to standing one up.
    /// </remarks>
    public void Resolve(SimConfig config)
    {
        for (var variant = 0; variant < _byVariant.Length; variant++)
        {
            _byVariant[variant] = _nominal
                // The nominal car's own balance, because the layout is all this borrows from the variant.
                ? CarBuild.Nominal(config, _catalogue.Variants[variant].DrivenFrontShare(config.Car.StaticFrontShare))
                : CarBuild.Of(config, _catalogue.Variants[variant]);
        }
    }

    public ref readonly CarBuild Of(int variant) => ref _byVariant[variant % _byVariant.Length];
}
