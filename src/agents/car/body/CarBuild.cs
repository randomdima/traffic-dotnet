using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

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
    /// <see cref="CornerRadiusM"/> off each corner. It is inside the footprint rather than the footprint, so
    /// it is within <see cref="LengthM"/> by <see cref="WidthM"/> — which stay what the car is drawn, driven
    /// and parked at.
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

    /// <summary>
    /// <b>Its wheels</b> (CAR-57): where each stands, the axle it turns on, and whether it steers, is the handbrake's and
    /// is driven — <see cref="WheelCount"/> of them, and two axles of two where its file lays out no other.
    /// </summary>
    /// <remarks>Read one at a time by <see cref="WheelAt"/>, which hands the wheel over where it lies rather than the table.</remarks>
    public required CarWheelTable Wheels { get => _wheels; init => _wheels = value; }

    readonly CarWheelTable _wheels;

    /// <inheritdoc cref="Wheels"/>
    public required int WheelCount { get; init; }

    /// <summary>One of its wheels, where it lies (<see cref="Wheels"/>).</summary>
    [UnscopedRef]
    public ref readonly CarWheel WheelAt(int wheel) => ref _wheels[wheel];

    /// <summary>How many axles its wheels turn on, and what each is handed of the engine's drive and its hold (<see cref="AxleDriveTable"/>).</summary>
    public required int AxleCount { get; init; }

    /// <inheritdoc cref="AxleCount"/>
    public required AxleDriveTable AxleDrive { get => _axleDrive; init => _axleDrive = value; }

    readonly AxleDriveTable _axleDrive;

    /// <summary>What one axle is handed of the engine (<see cref="AxleDrive"/>).</summary>
    public float DriveOf(int axle) => _axleDrive[axle];

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

    /// <summary>
    /// <b>The per-tick arithmetic's own constants of this body</b>, laid with the build so a wheel loads them rather than
    /// working them out: one over <see cref="WheelRotatingMassKg"/>.
    /// </summary>
    /// <remarks>
    /// Derived when the build is resolved: a <c>with</c> that changed the rim would carry the old figure.
    /// </remarks>
    public required float InverseWheelRotatingMassPerKg { get; init; }

    /// <summary>
    /// <b>How stiff it is sprung and damped at each wheel</b> (CAR-56, <see cref="Suspension"/>): what a wheel's spring
    /// pushes back with per metre it is compressed, anti-roll bar and all, and what its damper does per metre a second.
    /// </summary>
    public required float WheelRateNPerM { get; init; }

    /// <inheritdoc cref="WheelRateNPerM"/>
    public required float WheelDampingNsPerM { get; init; }

    /// <summary>How far ahead of the middle of the body its centre of mass stands, which <see cref="FrontWeightShare"/> places.</summary>
    public required float CgAheadOfCentreM { get; init; }

    /// <summary>
    /// <b>What a wheel stands on at rest</b>, in newtons, as a plane of its place off the centre of mass
    /// (<see cref="Suspension.StandingShare"/>): at the centre, and per metre along and across.
    /// </summary>
    /// <remarks>
    /// Derived when the build is resolved off its weight and its wheels: a <c>with</c> that changed either would carry the
    /// old figure. So would <see cref="InverseMassPerKg"/> and the two inertias, which are its mass and a box of its
    /// length, width and twice its height.
    /// </remarks>
    public required float StandingN { get; init; }

    /// <inheritdoc cref="StandingN"/>
    public required float StandingNPerMAlong { get; init; }

    /// <inheritdoc cref="StandingN"/>
    public required float StandingNPerMAcross { get; init; }

    /// <inheritdoc cref="StandingN"/>
    public required float InverseMassPerKg { get; init; }

    /// <inheritdoc cref="StandingN"/>
    public required float InversePitchInertiaPerKgM2 { get; init; }

    /// <inheritdoc cref="StandingN"/>
    public required float InverseRollInertiaPerKgM2 { get; init; }

    /// <summary>One over its mass times the height of its centre of mass: what a roll moment its springs carry is worth across it.</summary>
    /// <remarks><inheritdoc cref="StandingN" path="/remarks"/></remarks>
    public required float InverseMassCgHeightPerKgM { get; init; }

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

    /// <summary>This car's own circle with the steering off its stop, which every manoeuvre it makes at a bay is shaped on (GEN-4f).</summary>
    public required float ParkingTemplateRadiusM { get; init; }

    /// <summary>And how much straight a manoeuvre ends on to put <em>this</em> body square in a bay or along a lane.</summary>
    public required float ParkingStraightensUpM { get; init; }

    /// <summary>How far ahead this car has to be able to see: its own stopping distance from its own top speed.</summary>
    public required float SightM { get; init; }

    public required float LookaheadFloorM { get; init; }

    public required float LookaheadCeilingM { get; init; }

    public required float ProjectionWindowM { get; init; }

    public float HalfLengthM => LengthM * 0.5f;

    /// <summary>How far it reaches to either side of the line it is driven along.</summary>
    public float FlankM => WidthM * 0.5f;

    /// <summary>How far the nose stands ahead of the axle its line is drawn for, and the tail behind it.</summary>
    public float NoseAheadOfAxleM => CentreAheadOfAxleM + HalfLengthM;

    public float TailBehindAxleM => HalfLengthM - CentreAheadOfAxleM;

    /// <summary>What it may be braking at at all: the pedal's cap, or what its own patch puts down.</summary>
    /// <remarks>
    /// <b>Its own rubber and no ground</b>: what a wheel stands on is its tyre's alone (TER-7b), and no figure the car
    /// plans by turns on it.
    /// </remarks>
    public float UtmostBrakingMps2 => MathF.Min(BrakingMps2, GripMps2);

    /// <summary>
    /// <b>What the car may actually brake at</b>: the pedal's own bound, or what its tyres can put down along the roll,
    /// whichever gives out first — and nearly all of that (<see cref="DrivingFigures.BrakingMargin"/>), because a stop is
    /// the one manoeuvre a driver aims the whole car at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a figure along the roll and takes no notice of what the wheel is doing</b>, and every stop is planned at
    /// it. The brake a driver actually applies in a bend is less (<c>CarFollower.BrakeCeilingMps2</c>), since the
    /// tyres answer to one ellipse: a stop planned through a corner is made over more ground than it was planned in, and
    /// what catches the difference is the hazard (<c>CarFollower.IsAHazard</c>).
    /// </para>
    /// <para>
    /// This and the three figures below are derived once when the build is resolved, so every reader has the same
    /// rounding — and, like the tyre constants, a <c>with</c> carries the old figure.
    /// </para>
    /// </remarks>
    public required float PlannedBrakingMps2 { get; init; }

    /// <summary>What the car plans a corner to hold: its tyres' grip, less the margin kept back (<see cref="DrivingFigures.GripMargin"/>).</summary>
    public required float PlannedCorneringMps2 { get; init; }

    /// <summary>
    /// <b>How far in front of itself the profile plans with the pedal off the throttle</b>
    /// (<c>CarFollower.LeadS</c>): the staleness of what the driver reads, and half the brake pedal's travel to
    /// <see cref="PlannedBrakingMps2"/> — the lead every committed stretch of ground is taken at.
    /// </summary>
    public required float RestingLeadS { get; init; }

    /// <summary>
    /// <b>How far ahead of its nose a light holding the road is one this car is queueing for</b>
    /// (<see cref="DrivePatienceFigures.LightQueueInCarLengths"/>) — the one wait that spends no clock (TLT-2a).
    /// </summary>
    public required float LightQueueReachM { get; init; }

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
    public float CorneringRadiusM(float atMps, float gripMargin) => atMps * atMps / (GripMps2 * gripMargin);

    /// <summary>
    /// One variant as the car it is: its own dimensions and mass, and the nominal figures scaled by what
    /// its file says it is worth against them.
    /// </summary>
    public static CarBuild Of(SimConfig config, in CarVariant variant)
    {
        // The balance is settled before the layout, because a car that drives all four wheels places its
        // drive by the load it will be spent against rather than evenly.
        var frontWeightShare = Math.Clamp(variant.FrontWeightShare ?? config.Car.StaticFrontShare, 0f, 1f);

        // Its own wheels where its file lays them out, the drive placed on its driven axles by what each stands on; or
        // its two axles' four, the drive placed as its drivetrain says.
        Span<CarWheel> wheels = stackalloc CarWheel[TyreModel.MostWheels];
        Span<float> axleDrive = stackalloc float[2];
        int wheelCount;
        if (variant.Wheels is { } laid)
        {
            laid.CopyTo(wheels);
            wheelCount = laid.Length;
            axleDrive = [];
        }
        else
        {
            var drivenFrontShare = variant.DrivenFrontShare(frontWeightShare);
            wheelCount = CarWheels.TwoAxles(variant.WheelbaseM, -variant.RearAxleM, variant.HalfTrackM, drivenFrontShare, wheels);
            axleDrive[0] = drivenFrontShare;
            axleDrive[1] = 1f - drivenFrontShare;
        }

        return Resolve(
            config, variant.FootprintM.X, variant.FootprintM.Y, variant.CollisionSizeM, variant.CornerRadiusM,
            variant.MassKg, variant.WheelbaseM, variant.HalfTrackM, -variant.RearAxleM, wheels[..wheelCount], axleDrive,
            variant.Handling, variant.Beam?.PivotM.X ?? 0f, variant.Beam?.ReachM ?? 0f,
            variant.MaxSteeringDeg, variant.TyreFriction, variant.CgHeightM, frontWeightShare,
            variant.WheelM, variant.WheelRotatingMassKg, variant.WheelRateNPerM, variant.WheelDampingNsPerM);
    }

    /// <summary>
    /// <b>The nominal car, with one variant's drive layout on it</b> — the crash sandbox's car and no town's
    /// (CAR-11a): its cars stand on the figures the town was sized against and differ in layout alone.
    /// </summary>
    public static CarBuild Nominal(SimConfig config, float drivenFrontShare)
    {
        Span<CarWheel> wheels = stackalloc CarWheel[TyreModel.MostWheels];
        var wheelCount = CarWheels.TwoAxles(
            config.Car.WheelbaseM, config.CarCentreAheadOfAxleM, config.CarTrackM * 0.5f, drivenFrontShare, wheels);
        ReadOnlySpan<float> axleDrive = [drivenFrontShare, 1f - drivenFrontShare];
        return Resolve(
            config, config.Car.LengthM, config.Car.WidthM,
            new Vector2(config.Car.LengthM, config.Car.WidthM), cornerRadiusM: 0f, config.Car.MassKg,
            config.Car.WheelbaseM, config.CarTrackM * 0.5f, config.CarCentreAheadOfAxleM, wheels[..wheelCount], axleDrive,
            CarHandling.Nominal, towHingeAheadOfCentreM: 0f, towReachM: 0f, maxSteeringDeg: null, tyreFriction: null,
            cgHeightM: null, Math.Clamp(config.Car.StaticFrontShare, 0f, 1f), wheelM: null, wheelRotatingMassKg: null,
            wheelRateNPerM: null, wheelDampingNsPerM: null);
    }

    /// <summary>
    /// <b>Where a steering angle stops describing a circle.</b> At a right angle the front wheels point
    /// across the car, <c>wheelbase/tan</c> is nothing, and past it the tangent changes sign and the radius
    /// comes back <em>negative</em> — a turning circle on the wrong side of a car that is no longer turning.
    /// </summary>
    /// <remarks>
    /// <b>Not a figure anybody chose but the bound the arithmetic has</b>, held a degree off so a radius
    /// stays a length rather than a point. Nothing in the fleet is near it — every authored lock is under
    /// 32° — and nothing a session can turn reaches it either, a steering lock being one car's own figure
    /// rather than a surface the panel may move (<see cref="TrimFigures"/>).
    /// </remarks>
    const float MostSteerRad = 89f * MathF.PI / 180f;

    static CarBuild Resolve(
        SimConfig config, float lengthM, float widthM, Vector2 collisionSizeM, float cornerRadiusM, float massKg,
        float wheelbaseM, float halfTrackM, float centreAheadOfAxleM, ReadOnlySpan<CarWheel> wheels,
        ReadOnlySpan<float> axleDrive, in CarHandling handling, float towHingeAheadOfCentreM, float towReachM, float? maxSteeringDeg,
        float? tyreFriction, float? cgHeightM, float frontShare, Vector2? wheelM,
        float? wheelRotatingMassKg, float? wheelRateNPerM, float? wheelDampingNsPerM)
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
        var turningRadiusM = wheelbaseM / Trig.Tan(maxSteerRad);
        var brakingMps2 = config.CarBrakingMps2 * handling.Braking;
        var friction = (tyreFriction ?? config.Tyre.Friction) * config.Trim.Friction;
        var gripMps2 = friction * config.Tyre.StandardGravityMps2;
        var maxSpeedMps = config.Car.MaxSpeedMps * handling.MaxSpeed;

        var rotatingMassKg = wheelRotatingMassKg ?? config.Tyre.WheelRotatingMassKg;
        var weightMps2 = config.Tyre.StandardGravityMps2;
        var cgM = cgHeightM ?? config.Car.CgHeightM;

        // On its springs: the centre of mass where the balance puts it between the axles, what each wheel stands on
        // there, and a box of the body's length, width and twice its height turning about it.
        var cgAheadOfCentreM = (frontShare * wheelbaseM) - centreAheadOfAxleM;
        Span<Vector2> wheelsM = stackalloc Vector2[wheels.Length];
        for (var wheel = 0; wheel < wheels.Length; wheel++) wheelsM[wheel] = wheels[wheel].AtM - new Vector2(cgAheadOfCentreM, 0f);

        var weightN = massKg * weightMps2;
        var standing = Suspension.StandingShare(wheelsM);
        var standingN = standing * weightN;
        var boxHeightM = 2f * cgM;

        // What each axle stands on at rest, and what of the engine it is handed: as stated, or else shared among the
        // driven axles by what each stands on, which is what driving them is for (CarVariant.DrivenFrontShare).
        var axles = CarWheels.AxlesOf(wheels);
        Span<float> axleStanding = stackalloc float[axles];
        axleStanding.Clear();
        var drivenStanding = 0f;
        for (var wheel = 0; wheel < wheels.Length; wheel++)
        {
            var share = standing.X + (standing.Y * wheelsM[wheel].X) + (standing.Z * wheelsM[wheel].Y);
            axleStanding[(int)wheels[wheel].Axle] += share;
            if (wheels[wheel].IsDriven) drivenStanding += share;
        }

        var drive = new AxleDriveTable();
        var wheelTable = new CarWheelTable();
        for (var wheel = 0; wheel < wheels.Length; wheel++)
        {
            wheelTable[wheel] = wheels[wheel];
            var axle = (int)wheels[wheel].Axle;
            if (axleDrive.IsEmpty && wheels[wheel].IsDriven) drive[axle] = axleStanding[axle] / drivenStanding;
        }

        for (var axle = 0; axle < axleDrive.Length; axle++) drive[axle] = axleDrive[axle];

        // The engine is authored as a headroom over the rubber and not as an acceleration, so that a pedal
        // stays a demand a tyre can be measured against when the compound underneath it moves (CAR-45).
        var drivenTractionMps2 = DrivenTraction(gripMps2, axleStanding, drive);
        var accelerationMps2 = drivenTractionMps2 * config.Car.DrivePedalInDrivenGrips * handling.Acceleration;
        var pedalRateMps3 = (accelerationMps2 + brakingMps2) / config.Driving.PedalTravelS;
        var plannedBrakingMps2 = MathF.Min(brakingMps2, gripMps2) * config.Driving.BrakingMargin;

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
            TowGripFromTheMiddleM = TowBar.GripFromTheMiddleM(lengthM, config.Evacuator.TowGripInsideTheEndM),
            CgHeightM = cgM,

            // A share is a share, and a car standing on more than all of itself is not a body the loads
            // can split.
            FrontWeightShare = frontShare,
            WheelCount = wheels.Length,
            Wheels = wheelTable,
            AxleCount = axles,
            AxleDrive = drive,
            MaxSpeedMps = maxSpeedMps,
            ReverseMaxMps = config.Car.ReverseMaxMps,
            AccelerationMps2 = accelerationMps2,
            DrivenTractionMps2 = drivenTractionMps2,
            BrakingMps2 = brakingMps2,
            GripMps2 = gripMps2,
            WheelLengthM = wheelM?.X ?? config.Tyre.WheelLengthM,
            WheelWidthM = wheelM?.Y ?? config.Tyre.WheelWidthM,
            WheelRotatingMassKg = rotatingMassKg,
            InverseWheelRotatingMassPerKg = 1f / rotatingMassKg,
            WheelRateNPerM = wheelRateNPerM ?? config.Car.WheelRateNPerM,
            WheelDampingNsPerM = wheelDampingNsPerM ?? config.Car.WheelDampingNsPerM,
            CgAheadOfCentreM = cgAheadOfCentreM,
            StandingN = standingN.X,
            StandingNPerMAlong = standingN.Y,
            StandingNPerMAcross = standingN.Z,
            InverseMassPerKg = 1f / massKg,
            InversePitchInertiaPerKgM2 = 12f / (massKg * ((lengthM * lengthM) + (boxHeightM * boxHeightM))),
            InverseRollInertiaPerKgM2 = 12f / (massKg * ((widthM * widthM) + (boxHeightM * boxHeightM))),
            InverseMassCgHeightPerKgM = 1f / (massKg * cgM),
            MaxSteerRad = maxSteerRad,
            PedalRateMps3 = pedalRateMps3,
            PlannedBrakingMps2 = plannedBrakingMps2,
            PlannedCorneringMps2 = gripMps2 * config.Driving.GripMargin,
            RestingLeadS = config.CarReactionS + (plannedBrakingMps2 / (2f * pedalRateMps3)),
            LightQueueReachM = lengthM * config.Patience.LightQueueInCarLengths,
            SteerRateRadPerS = 2f * maxSteerRad / config.Driving.WheelTravelS,
            TurningRadiusM = turningRadiusM,
            ParkingTemplateRadiusM = turningRadiusM * config.Car.ParkingTemplateArcMargin,
            ParkingStraightensUpM = lengthM * config.Road.ParkingStraightensUpInCarLengths,
            SightM = maxSpeedMps * maxSpeedMps
                / (2f * MathF.Min(brakingMps2, gripMps2) * config.Driving.GripMargin),
            LookaheadFloorM = lengthM * config.Driving.LookaheadFloorInCarLengths,
            LookaheadCeilingM = lengthM * config.Driving.LookaheadCeilingInCarLengths,
            ProjectionWindowM = lengthM * config.Driving.ProjectionWindowInCarLengths,
        };
    }

    /// <summary>
    /// <see cref="DrivenTractionMps2"/>: the pedal at which a driven axle's quotient — the drive it is
    /// placed by layout, over the load it stands on — first reaches what the patch holds.
    /// </summary>
    static float DrivenTraction(float gripMps2, ReadOnlySpan<float> axleStanding, in AxleDriveTable drive)
    {
        var mostMps2 = float.PositiveInfinity;
        for (var axle = 0; axle < axleStanding.Length; axle++)
        {
            if (drive[axle] > 0f) mostMps2 = MathF.Min(mostMps2, gripMps2 * axleStanding[axle] / drive[axle]);
        }

        // A car nothing drives has no pedal at all, and a car standing on nothing at the driven end has no
        // pedal it can use: either way the figure is a zero rather than an infinity for a caller to trip on.
        return float.IsFinite(mostMps2) ? mostMps2 : 0f;
    }
}

/// <summary>
/// The builds of every look in the catalogue, made once for a town and read by variant. <b>A build is a
/// fact about a look and not about a car</b>: one per look covers any number of cars, and a car is its
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
    /// And each of them as the nominal car driving through its own variant's end, which is the crash
    /// sandbox's fleet (CAR-11a) and no town's.
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
