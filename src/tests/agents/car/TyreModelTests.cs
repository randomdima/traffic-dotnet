using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The tyre model, asked against arithmetic with no solver in the room — which is the whole reason it
/// is a function of a pose rather than a method on a body.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class TyreModelTests
{
    static readonly SimConfig Figures = SimConfig.Shipped();

    /// <summary>The nominal car: the arithmetic here is the model's and not a variant's.</summary>
    static readonly CarBuild Car = CarBuild.Nominal(Figures, Figures.Car.DrivenFrontShare);

    static SurfaceUnderWheel Paved => new(
        Figures.Terrain.PavedCoefficient, Figures.PavedDragMps2,
        Figures.Marks.PowerM2S3 * Figures.Terrain.PavedMarkFactor, Ploughs: false);

    static SurfaceUnderWheel Turf => new(
        Figures.Terrain.GrassCoefficient, Figures.GrassDragMps2, Figures.Marks.PowerM2S3, Ploughs: true);

    static SurfaceUnderWheel[] AllOf(SurfaceUnderWheel surface) => [surface, surface, surface, surface];

    static CarPose Rolling(float alongMps, float acrossMps = 0f, float yawRateRadPerS = 0f) =>
        new(Vector2.Zero, 0f, new Vector2(alongMps, acrossMps), yawRateRadPerS, Figures.Car.MassKg, Vector2.Zero);

    /// <summary>
    /// A car whose wheels are turning with the road under them, which is what a car that drove up to
    /// this speed has. Four rims left at zero under a moving car are four locked wheels, and the model
    /// would be quite right to say so.
    /// </summary>
    static float[] SpinningWith(in CarPose pose)
    {
        var alongMps = Vector2.Dot(pose.VelocityMps, pose.Forward);
        return [alongMps, alongMps, alongMps, alongMps];
    }

    static WheelImpulse[] Step(in CarPose pose, in DriveCommand command) =>
        Step(pose, command, AllOf(Paved), SpinningWith(pose), out _);

    static WheelImpulse[] Step(
        in CarPose pose, in DriveCommand command, SurfaceUnderWheel[] ground, float[] spinMps, out TyreScrub[] scrub)
    {
        var wheels = new WheelImpulse[TyreModel.Wheels];
        scrub = new TyreScrub[TyreModel.Wheels];
        var atM = new Vector2[TyreModel.Wheels];
        TyreModel.WheelPointsM(Car,pose, atM);
        TyreModel.Step(
            Figures, Car, pose, command, float.PositiveInfinity, atM, ground, spinMps,
            Figures.TickSeconds, wheels, scrub);
        return wheels;
    }

    /// <summary>
    /// What the four patches together can hold in one direction while the body is pulling
    /// <paramref name="atMps2"/> that way — the whole car's capability, summed off the model itself rather
    /// than worked out beside it. Every wheel is slid hard enough to be on its own boundary, so what comes
    /// back is the budget and not a demand.
    /// </summary>
    /// <remarks>
    /// Run on tarmac with the <b>rolling resistance taken out of it</b>, which is the one thing in the
    /// returned impulse that is not the tyre: it is spent outside the traction budget and is the same
    /// figure whichever way the car is sliding, so leaving it in would flatter a ratio of two budgets.
    /// </remarks>
    static float HeldMps2(SimConfig figures, in CarBuild car, Vector2 atMps2, bool sideways)
    {
        var slidingMps = sideways ? new Vector2(0f, 40f) : new Vector2(40f, 0f);
        var pose = new CarPose(Vector2.Zero, 0f, slidingMps, 0f, figures.Car.MassKg, atMps2);
        var wheels = new WheelImpulse[TyreModel.Wheels];
        var scrub = new TyreScrub[TyreModel.Wheels];
        var atM = new Vector2[TyreModel.Wheels];
        var spinMps = new float[TyreModel.Wheels];
        var free = new SurfaceUnderWheel(figures.Terrain.PavedCoefficient, 0f, 0f, Ploughs: false);
        TyreModel.WheelPointsM(car, pose, atM);
        TyreModel.Step(
            figures, car, pose, DriveCommand.Locked, float.PositiveInfinity, atM, AllOf(free), spinMps,
            figures.TickSeconds, wheels, scrub);

        var heldNs = 0f;
        foreach (var wheel in wheels) heldNs += wheel.ImpulseNs.Length();
        return heldNs / (figures.Car.MassKg * figures.TickSeconds);
    }

    /// <summary>What the car settles at in one direction, the transfer being caused by the very figure it decides.</summary>
    static float SettlesAtMps2(SimConfig figures, in CarBuild car, bool sideways)
    {
        var heldMps2 = figures.TyreGripMps2;
        for (var pass = 0; pass < 8; pass++)
        {
            heldMps2 = HeldMps2(
                figures, car, sideways ? new Vector2(0f, heldMps2) : new Vector2(heldMps2, 0f), sideways);
        }

        return heldMps2;
    }

    /// <summary>
    /// CAR-3e: <b>one coefficient, at every load and in every direction.</b> A stop and a corner are worth
    /// the same, both are worth the coefficient, and a body that moves twice the load is worth the same
    /// again — because a patch is worth what it is carrying and a transfer only moves the carrying about.
    /// </summary>
    /// <remarks>
    /// <b>This is the gate on the rule, not on the numbers</b>, and the way it is failed is by somebody
    /// authoring a difference: a second coefficient along the roll, or a μ that falls with load. Both are
    /// real tyre behaviour and both are worth about a per cent at this scale, which is small enough to be
    /// where a fudge hides and too small to be seen from the height a town is watched at.
    /// </remarks>
    [Fact]
    public void OneCoefficientHoldsTheSameWhicheverWayTheLoadIsMoved()
    {
        var corneringMps2 = SettlesAtMps2(Figures, Car, sideways: true);
        var stoppingMps2 = SettlesAtMps2(Figures, Car, sideways: false);

        Assert.Equal(1f, stoppingMps2 / corneringMps2, 0.005f);
        Assert.Equal(Figures.TyreGripMps2, corneringMps2, 0.02f);

        // Twice the centre of gravity is twice the transfer, and a tall body that held less for it would be
        // a load sensitivity somewhere — which is the term this rule exists to keep out.
        var tall = CarBuild.Nominal(Figures, Figures.Car.DrivenFrontShare) with
        {
            CgHeightM = Car.CgHeightM * 2f,
        };
        Assert.Equal(stoppingMps2, SettlesAtMps2(Figures, tall, sideways: false), 0.02f);
        Assert.Equal(corneringMps2, SettlesAtMps2(Figures, tall, sideways: true), 0.02f);
    }

    /// <summary>
    /// CAR-4: <b>a stationary car cannot rotate</b>, in either gear. Nothing enforces it — it falls out
    /// of a model in which the only thing a steered wheel does is refuse to slide sideways, and a wheel
    /// that is not moving is not sliding.
    /// </summary>
    [Fact]
    public void AStationaryCarOnFullLockIsGivenNothingAtAll()
    {
        var wheels = Step(Rolling(0f), new DriveCommand(0.6f, 0f, 0f, false, false));

        foreach (var wheel in wheels) Assert.Equal(Vector2.Zero, wheel.ImpulseNs);
    }

    /// <summary>
    /// The ellipse is a <b>ceiling</b>: a wheel never spends more than the ground under it affords,
    /// however hard it is asked. The rolling resistance rides on top of it, being the one thing a tyre
    /// spends outside the budget.
    /// </summary>
    [Theory]
    [InlineData(30f, 0f)]
    [InlineData(30f, 12f)]
    [InlineData(2f, 8f)]
    public void NoWheelSpendsMoreThanTheGroundAffords(float alongMps, float acrossMps)
    {
        var pose = Rolling(alongMps, acrossMps);
        var wheels = Step(pose, new DriveCommand(0.4f, Figures.CarAccelerationMps2, 0f, false, false));

        Span<float> loads = stackalloc float[TyreModel.Wheels];
        TyreModel.Loads(Figures, Car, pose,loads);

        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var mostNs = (Figures.TyreGripMps2 + Figures.PavedDragMps2)
                         * Figures.Car.MassKg * loads[wheel] * Figures.TickSeconds;
            Assert.True(
                wheels[wheel].ImpulseNs.Length() <= mostNs * 1.001f,
                $"wheel {wheel} spent {wheels[wheel].ImpulseNs.Length():F1} Ns of {mostNs:F1}");
        }
    }

    /// <summary>A brake takes what the wheel is carrying and never more, so it cannot push a car backwards.</summary>
    [Fact]
    public void BrakingDoesNotDriveTheCarBackwards()
    {
        var wheels = Step(Rolling(0.05f), new DriveCommand(0f, 0f, Figures.CarBrakingMps2, false, false));

        var alongNs = 0f;
        foreach (var wheel in wheels) alongNs += wheel.ImpulseNs.X;

        // At a twentieth of a metre a second the whole braking pedal is worth more than the car has, and
        // the rolling resistance is the only thing that may be spent beside it.
        var mostNs = (0.05f + (Figures.PavedDragMps2 * Figures.TickSeconds)) * Figures.Car.MassKg;
        Assert.InRange(alongNs, -mostNs * 1.001f, 0f);
    }

    /// <summary>The handbrake locks the <b>rear</b> pair only, so the back drags while the front keeps rolling and steering.</summary>
    [Fact]
    public void TheHandbrakeLocksTheRearPairAndNothingElse()
    {
        var pose = Rolling(10f);
        var wheels = Step(pose, DriveCommand.Parked);

        Span<float> loads = stackalloc float[TyreModel.Wheels];
        TyreModel.Loads(Figures, Car, pose,loads);

        // A rolling front wheel spends its rolling resistance and nothing else; the locked rear pair is
        // spending the whole ellipse against the way the car is going.
        for (var wheel = 0; wheel < 2; wheel++)
        {
            var dragNs = Figures.PavedDragMps2 * Figures.Car.MassKg * loads[wheel] * Figures.TickSeconds;
            Assert.Equal(-dragNs, wheels[wheel].ImpulseNs.X, dragNs * 1e-3f);
        }

        Assert.True(wheels[2].ImpulseNs.X < wheels[0].ImpulseNs.X * 5f);
        Assert.True(wheels[3].ImpulseNs.X < wheels[1].ImpulseNs.X * 5f);
    }

    /// <summary>Every corner carries a quarter of the car at rest, and the four of them are the whole car whatever it is doing.</summary>
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(-9f, 0f)]
    [InlineData(6f, 4f)]
    public void TheLoadsAreAlwaysTheWholeCar(float alongMps2, float acrossMps2)
    {
        var pose = new CarPose(
            Vector2.Zero, 0f, Vector2.Zero, 0f, Figures.Car.MassKg, new Vector2(alongMps2, acrossMps2));

        Span<float> loads = stackalloc float[TyreModel.Wheels];
        TyreModel.Loads(Figures, Car, pose,loads);

        var total = 0f;
        foreach (var load in loads)
        {
            Assert.InRange(load, 0f, 1f);
            total += load;
        }

        Assert.Equal(1f, total, 1e-4f);
    }

    /// <summary>
    /// However hard the body is thrown about, <b>the four corners are still the whole car and none of them
    /// carries less than nothing</b>. A wheel asked for more transfer than it stands on lifts, which is a
    /// budget of zero and an impulse of zero; what it may never be is negative, since that is a tyre
    /// holding the road on from underneath and a friction pushing the way the wheel is already sliding.
    /// </summary>
    /// <remarks>
    /// Thrown about far harder than the tyres could manage, because the pose is set here rather than
    /// measured: the loads are read from what the patches themselves spent, so nothing this violent
    /// reaches them in a town.
    /// </remarks>
    [Fact]
    public void TheFourCornersAreTheWholeCarAndNoneOfThemIsNegative()
    {
        Span<float> loads = stackalloc float[TyreModel.Wheels];
        TyreModel.Loads(
            Figures, Car, new CarPose(Vector2.Zero, 0f, Vector2.Zero, 0f, Figures.Car.MassKg, new Vector2(-40f, 30f)), loads);

        var total = 0f;
        foreach (var load in loads)
        {
            Assert.True(load >= 0f, $"a corner was left carrying {load:F4} of the car");
            total += load;
        }

        Assert.Equal(1f, total, 1e-4f);
    }

    /// <summary>Braking moves load onto the front axle, which is what makes weight transfer a fact rather than a fudge.</summary>
    [Fact]
    public void BrakingLoadsTheFrontAxle()
    {
        Span<float> resting = stackalloc float[TyreModel.Wheels];
        Span<float> braking = stackalloc float[TyreModel.Wheels];
        TyreModel.Loads(
            Figures, Car, new CarPose(Vector2.Zero, 0f, Vector2.Zero, 0f, Figures.Car.MassKg, Vector2.Zero), resting);
        TyreModel.Loads(
            Figures, Car, new CarPose(Vector2.Zero, 0f, Vector2.Zero, 0f, Figures.Car.MassKg, new Vector2(-9f, 0f)), braking);

        Assert.Equal(0.5f, resting[0] + resting[1], 1e-4f);
        Assert.True(braking[0] + braking[1] > 0.5f);
        Assert.True(braking[2] + braking[3] < 0.5f);
    }

    /// <summary>Ackermann: the inner wheel turns further than the outer, because the two are turning one circle at two radii.</summary>
    [Fact]
    public void TheInnerFrontWheelTurnsFurtherThanTheOuter()
    {
        Span<float> steer = stackalloc float[TyreModel.Wheels];
        TyreModel.Ackermann(Car,0.4f, steer);

        Assert.True(steer[0] > steer[1], $"right {steer[0]:F3} should out-turn left {steer[1]:F3} on a right-hander");
        Assert.Equal(0f, steer[2]);
        Assert.Equal(0f, steer[3]);

        TyreModel.Ackermann(Car,-0.4f, steer);
        Assert.True(steer[1] < steer[0], "and the other way round on a left-hander");
    }

    /// <summary>
    /// <b>The friction circle has one radius</b>, read off the model itself: a locked wheel shoved along
    /// its roll and the same wheel shoved across it are both spending the whole budget, and it is the same
    /// budget. A second coefficient anywhere in the path shows up here as two.
    /// </summary>
    [Fact]
    public void TheCircleIsOneBudgetWhicheverWayTheWheelIsShoved()
    {
        var alongNs = Step(Rolling(30f), DriveCommand.Locked)[0].ImpulseNs.Length();
        var acrossNs = Step(Rolling(0f, 30f), DriveCommand.Locked)[0].ImpulseNs.Length();

        Span<float> loads = stackalloc float[TyreModel.Wheels];
        TyreModel.Loads(Figures, Car, Rolling(30f), loads);

        var budgetNs = Figures.TyreGripMps2 * Figures.Car.MassKg * loads[0] * Figures.TickSeconds;
        var dragNs = Figures.PavedDragMps2 * Figures.Car.MassKg * loads[0] * Figures.TickSeconds;

        Assert.Equal(budgetNs + dragNs, acrossNs, budgetNs * 1e-3f);
        Assert.Equal(budgetNs + dragNs, alongNs, budgetNs * 1e-3f);
    }

    /// <summary>
    /// <b>The drive a car gets is what its driven axle can put down</b>, and on the shipped figures the
    /// nominal car's whole pedal is exactly that and no more (CAR-45): it drives one axle, it stands evenly
    /// on two, and its engine is authored at what the rubber under that axle answers. A pedal and an axle
    /// meeting here is the whole of the rule — over it the excess would buy no acceleration at all, and
    /// under it the car would have no wheelspin in it anywhere.
    /// </summary>
    [Fact]
    public void TheDriveTheCarGetsIsWhatTheDrivenAxleCanPutDown()
    {
        var pose = Rolling(5f);
        var wheels = Step(pose, new DriveCommand(0f, Figures.CarAccelerationMps2, 0f, false, false));

        Span<float> loads = stackalloc float[TyreModel.Wheels];
        TyreModel.Loads(Figures, Car, pose,loads);

        var alongNs = 0f;
        foreach (var wheel in wheels) alongNs += wheel.ImpulseNs.X;

        var pedalNs = Figures.CarAccelerationMps2 * Figures.Car.MassKg * Figures.TickSeconds;
        var axleNs = Figures.TyreGripMps2
                     * Figures.Car.MassKg * (loads[0] + loads[1]) * Figures.TickSeconds;
        var dragNs = Figures.PavedDragMps2 * Figures.Car.MassKg * Figures.TickSeconds;

        Assert.True(alongNs > 0f);
        Assert.Equal(MathF.Min(pedalNs, axleNs) - dragNs, alongNs, axleNs * 0.05f);
        Assert.Equal(pedalNs, axleNs, pedalNs * 1e-3f);
    }

    /// <summary>Which end the drive is placed on is the variant's, and it is the axle that spends it.</summary>
    [Fact]
    public void ARearDrivenCarPushesWithItsRearAxle()
    {
        var pose = Rolling(5f);
        var wheels = new WheelImpulse[TyreModel.Wheels];
        var scrub = new TyreScrub[TyreModel.Wheels];
        var atM = new Vector2[TyreModel.Wheels];
        TyreModel.WheelPointsM(Car,pose, atM);
        TyreModel.Step(
            Figures, CarBuild.Nominal(Figures, drivenFrontShare: 0f), pose,
            new DriveCommand(0f, Figures.CarAccelerationMps2, 0f, false, false),
            float.PositiveInfinity, atM, AllOf(Paved), SpinningWith(pose), Figures.TickSeconds, wheels, scrub);

        Assert.True(wheels[2].ImpulseNs.X > 0f && wheels[3].ImpulseNs.X > 0f);
        Assert.True(wheels[0].ImpulseNs.X < 0f, "an undriven front wheel is only its rolling resistance");
    }

    /// <summary>Ground that is worth less holds a car less, which is TER-4/5 as the tyres see it.</summary>
    [Fact]
    public void SoftGroundHoldsACarLess()
    {
        var pose = Rolling(20f, 6f);
        var command = new DriveCommand(0f, 0f, Figures.CarBrakingMps2, false, false);
        var onTarmac = Step(pose, command, AllOf(Paved), SpinningWith(pose), out _);
        var onGrass = Step(
            pose, command, AllOf(Turf with { DragMps2 = Figures.PavedDragMps2 }), SpinningWith(pose), out _);

        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            Assert.True(onGrass[wheel].ImpulseNs.Length() < onTarmac[wheel].ImpulseNs.Length());
        }
    }

    /// <summary>Rolling resistance is the only thing slowing a car that is neither braking nor cornering.</summary>
    [Fact]
    public void ACoastingCarIsSlowedByTheGroundItRollsOver()
    {
        var pose = Rolling(15f);
        var wheels = Step(pose, DriveCommand.Idle);

        var alongNs = 0f;
        foreach (var wheel in wheels) alongNs += wheel.ImpulseNs.X;

        Assert.Equal(
            -Figures.PavedDragMps2 * Figures.Car.MassKg * Figures.TickSeconds, alongNs,
            Figures.Car.MassKg * Figures.TickSeconds * 1e-2f);
    }

    /// <summary>
    /// The rim is the whole of what a wheel spinning means: asked for more than the patch can put down,
    /// the tread runs faster than the road under it — and the gearing holds it to the allowance rather
    /// than letting it run away.
    /// </summary>
    [Fact]
    public void AWheelTheEngineOutrunsSpinsUpAndIsHeldToItsAllowance()
    {
        var spinMps = new float[TyreModel.Wheels];
        var command = new DriveCommand(0f, Figures.CarAccelerationMps2 * 4f, 0f, false, false);
        for (var tick = 0; tick < 30; tick++)
        {
            Step(Rolling(0.5f), command, AllOf(Paved), spinMps, out _);
        }

        Assert.True(spinMps[0] > 0.5f, $"a driven wheel asked for four times the pedal should be spinning, not {spinMps[0]:F2}");
        Assert.True(spinMps[0] <= 0.5f + TyreModel.SpinAllowanceMps(Figures, 0.5f) + 1e-3f);

        // The undriven pair is simply rolling, so it takes the road's speed as the tick's own impulses
        // leave it — which is a shade under the car's, the rolling resistance having been spent on it.
        Assert.Equal(0.5f, spinMps[2], 0.05f);
    }

    /// <summary>The allowance is the gearbox: the revs in hand to spin a wheel with are gone by road speed.</summary>
    [Fact]
    public void TheSpinAllowanceIsGoneByRoadSpeed()
    {
        Assert.Equal(Figures.Tyre.WheelSpinAllowanceMps, TyreModel.SpinAllowanceMps(Figures, 0f), 1e-3f);
        Assert.Equal(0f, TyreModel.SpinAllowanceMps(Figures, Figures.Tyre.WheelSpinFadeMps), 1e-3f);
        Assert.Equal(0f, TyreModel.SpinAllowanceMps(Figures, 40f), 1e-3f);
    }

    /// <summary>A wheel that is rolling is not slipping at all: it takes the road's own speed and stays there.</summary>
    [Fact]
    public void ARollingWheelKeepsUpWithTheRoad()
    {
        var spinMps = new float[TyreModel.Wheels];
        Array.Fill(spinMps, 12f);
        Step(Rolling(12f), new DriveCommand(0f, 1f, 0f, false, false), AllOf(Paved), spinMps, out var scrub);

        foreach (var speed in spinMps) Assert.Equal(12f, speed, 0.05f);
        foreach (var wheel in scrub) Assert.Equal(0f, wheel.SlidePowerM2S3, 1e-3f);
    }

    /// <summary>A locked wheel at speed drags over the road and writes on it, which is what a skid is.</summary>
    [Fact]
    public void ALockedWheelAtSpeedMarksTheRoad()
    {
        var pose = Rolling(25f);
        Step(pose, DriveCommand.Locked, AllOf(Paved), SpinningWith(pose), out var scrub);

        var travelM = TyreModel.ScrubTravelM(Figures, Figures.Marks.OnsetM, scrub[0].SlideSpeedMps, Figures.TickSeconds);
        Assert.True(scrub[0].SlideSpeedMps > Figures.Marks.SlipMps);
        Assert.True(TyreModel.GroundMarkIntensity(Figures, Paved, scrub[0], travelM) > 0f);
    }

    /// <summary>
    /// A corner is not a slide. A rolling tyre makes its cornering force by creeping across the ground,
    /// and a car holding a firm turn carries metres a second of that while leaving the road clean.
    /// </summary>
    [Fact]
    public void AnOrdinaryCornerLeavesTheRoadAsItFoundIt()
    {
        var pose = Rolling(15f, 3f);
        Step(pose, DriveCommand.Idle, AllOf(Paved), SpinningWith(pose), out var scrub);

        foreach (var wheel in scrub)
        {
            Assert.Equal(0f, TyreModel.GroundMarkIntensity(Figures, Paved, wheel, Figures.Marks.OnsetM));
        }
    }

    /// <summary>A parked car with its handbrake on does not scrub the road it is standing on.</summary>
    [Fact]
    public void AParkedCarDoesNotScrubTheRoad()
    {
        Step(Rolling(0f), DriveCommand.Parked, AllOf(Paved), new float[TyreModel.Wheels], out var scrub);

        foreach (var wheel in scrub)
        {
            Assert.Equal(0f, TyreModel.GroundMarkIntensity(Figures, Paved, wheel, Figures.Marks.OnsetM));
        }
    }

    /// <summary>
    /// Soft ground is different in kind and not in degree: a wheel merely rolling over turf displaces
    /// it, so a car that idles across a lawn leaves the same two tracks a fast one does. The same wheel
    /// on tarmac writes nothing at all.
    /// </summary>
    [Fact]
    public void SoftGroundKeepsATrackOfAWheelThatIsOnlyRollingOverIt()
    {
        var pose = Rolling(4f);
        Step(pose, DriveCommand.Idle, AllOf(Turf), SpinningWith(pose), out var onTurf);
        Step(pose, DriveCommand.Idle, AllOf(Paved), SpinningWith(pose), out var onTarmac);

        Assert.True(
            TyreModel.GroundMarkIntensity(Figures, Turf, onTurf[0], 0f) >= Figures.Marks.PloughFloor);
        Assert.Equal(0f, TyreModel.GroundMarkIntensity(Figures, Paved, onTarmac[0], 0f));
    }

    /// <summary>Standing on grass ploughs nothing: below the crawl the wheel is on the ground rather than crossing it.</summary>
    [Fact]
    public void AWheelStandingOnSoftGroundPloughsNothing()
    {
        Step(Rolling(0f), DriveCommand.Parked, AllOf(Turf), new float[TyreModel.Wheels], out var scrub);

        Assert.Equal(0f, TyreModel.GroundMarkIntensity(Figures, Turf, scrub[0], 0f));
    }

    /// <summary>
    /// Rubber is not laid in an instant: a scrub has to keep going to be worth anything, and one that
    /// hooks up again drains rather than banking what it had.
    /// </summary>
    [Fact]
    public void AScrubHasToKeepGoingToCount()
    {
        var travelM = TyreModel.ScrubTravelM(Figures, 0f, slideSpeedMps: 8f, Figures.TickSeconds);
        Assert.InRange(travelM, 0f, Figures.Marks.OnsetM);

        var bankedM = TyreModel.ScrubTravelM(Figures, Figures.Marks.OnsetM, slideSpeedMps: 40f, Figures.TickSeconds);
        Assert.Equal(Figures.Marks.OnsetM, bankedM);

        var drainedM = TyreModel.ScrubTravelM(Figures, Figures.Marks.OnsetM, slideSpeedMps: 0f, Figures.TickSeconds);
        Assert.True(drainedM < Figures.Marks.OnsetM);
    }

    /// <summary>The tread is wrapped into the picture's own pitch, so the pattern repeats seamlessly whichever way it runs.</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(30f)]
    [InlineData(-30f)]
    public void TheTreadScrollsWithinOnePitch(float spinMps)
    {
        var phaseM = 0f;
        for (var tick = 0; tick < 50; tick++)
        {
            phaseM = TyreModel.TreadPhaseM(phaseM, spinMps, Figures.Tyre.TreadPitchM, Figures.TickSeconds);
            Assert.InRange(phaseM, 0f, Figures.Tyre.TreadPitchM);
        }
    }
}
