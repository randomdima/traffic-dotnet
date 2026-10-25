using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>EVA-5's arithmetic, judged without a body</b>: where the coupling is anchored, and
/// what the two wheels under a towed car do. Everything <see cref="TowBar"/> answers is a function of
/// numbers, which is the whole reason it is a component rather than a passage of the town.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class TowBarTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static CarBuild Nominal => CarBuild.Nominal(Config, drivenFrontShare: 1f);

    /// <summary>The one build in the catalogue that carries an arm, because the arm is its picture's (EVA-5).</summary>
    static CarBuild Evacuator =>
        CarBuild.Of(Config, CarCatalog.Shared.Variants[CarCatalog.Shared.Evacuator]);

    static CarPose Standing(float atX, float headingRad = 0f) =>
        new(new Vector2(atX, 0f), headingRad, Vector2.Zero, 0f, Config.Car.MassKg, Vector2.Zero);

    /// <summary>
    /// EVA-5: <b>the coupling's two ends meet the moment the crew has winched a wreck onto the arm</b>, which
    /// is what says the placement and the coupling agree — a pair set down anywhere else is a tow under
    /// stretch before either body has moved, and the arm spends its first tick catching up.
    /// </summary>
    [Fact]
    public void TheArmIsUnderNoStretchWhereTheCrewSetTheWreckDown()
    {
        var tractor = Standing(0f);
        var towed = Standing(-TowBar.SetDownBehindM(Evacuator, Nominal));

        var hookM = TowBar.HookM(Evacuator, tractor);
        Assert.True(
            (hookM - TowBar.EyeM(Nominal, towed, Evacuator.TowReachM, byTheTail: false)).Length() < 1e-4f,
            "the arm stands out of line where the crew put the wreck");

        // And the fork stands just inside the wreck's own nose, a whole reach from the hinge — the picture
        // and the arithmetic pointing at the same place.
        Assert.Equal(Evacuator.TowReachM, (hookM - TowBar.ForkM(Nominal, towed, byTheTail: false)).Length(), 3);

        // And the two boxes are left clear of each other: a pair the solver has to push apart is a shove on
        // the vehicle that has a line to hold.
        var daylightM = (tractor.PositionM - towed.PositionM).Length() - Evacuator.HalfLengthM - Nominal.HalfLengthM;
        Assert.True(daylightM > 0.5f, $"the pair stands {daylightM:F2} m apart, which the corners will close");
    }

    /// <summary>
    /// EVA-5: <b>every car is taken hold of at the same distance inside the end that is caught</b>, whatever
    /// its axles are doing and whichever end it is — so the daylight between a truck and what it is pulling
    /// is the same for a coupé and a van, and the arm on screen is the same length over both.
    /// </summary>
    [Fact]
    public void EveryCarIsTakenHoldOfTheSameDistanceInsideWhicheverEndIsCaught()
    {
        // <b>The same inset and not a stated one.</b> What EVA-5 is about is that the figure does not vary
        // with the car or the end, so the first one seen is what the rest are held to — asserting each
        // against the figure it was laid from would be that figure written out twice (VER-12) and would go
        // red the day somebody retuned the arm rather than the day the arm stopped being even.
        var insetM = float.NaN;
        var daylightM = float.NaN;
        foreach (var variant in CarCatalog.Shared.Variants)
        {
            var towed = CarBuild.Of(Config, variant);
            var noseM = TowBar.ForkM(towed, Standing(0f), byTheTail: false);
            var tailM = TowBar.ForkM(towed, Standing(0f), byTheTail: true);
            if (float.IsNaN(insetM)) insetM = towed.HalfLengthM - noseM.X;
            Assert.Equal(insetM, towed.HalfLengthM - noseM.X, 4);
            Assert.Equal(insetM, towed.HalfLengthM + tailM.X, 4);

            var behindM = TowBar.SetDownBehindM(Evacuator, towed);
            var apartM = behindM - Evacuator.HalfLengthM - towed.HalfLengthM;
            if (float.IsNaN(daylightM)) daylightM = apartM;
            Assert.Equal(daylightM, apartM, 4);
        }
    }

    /// <summary>
    /// <b>And a car caught by the tail is the same tow mirrored</b> (EVA-5): the fork under its back end, the
    /// point held a reach beyond that, and the pair standing at the same distance — so a truck that backed
    /// onto a car nose-first pulls it exactly as well as one that backed onto its nose.
    /// </summary>
    [Fact]
    public void ACarCaughtByTheTailIsHeldTheSameWayRoundTheOtherWay()
    {
        // Facing the truck rather than away from it, which is what being caught by the tail means.
        var tractor = Standing(0f);
        var towed = Standing(-TowBar.SetDownBehindM(Evacuator, Nominal), MathF.PI);

        var hookM = TowBar.HookM(Evacuator, tractor);
        Assert.True(
            (hookM - TowBar.EyeM(Nominal, towed, Evacuator.TowReachM, byTheTail: true)).Length() < 1e-4f,
            "the arm stands out of line over a car it has by the tail");
        Assert.Equal(Evacuator.TowReachM, (hookM - TowBar.ForkM(Nominal, towed, byTheTail: true)).Length(), 3);

        // And what is left on the ground under it is the pair the fork is not holding up.
        Assert.Equal(TowBar.FrontPair, TowBar.PairOnTheGround(byTheTail: true));
        Assert.Equal(TowBar.RearPair, TowBar.PairOnTheGround(byTheTail: false));
        Assert.NotEqual(TowBar.PairInTheAir(byTheTail: true), TowBar.PairOnTheGround(byTheTail: true));
    }

    /// <summary>
    /// EVA-5: the two wheels a towed car stands on are its own back pair, at the very offsets the
    /// four-wheeled model puts them at — because they are the same two wheels.
    /// </summary>
    [Fact]
    public void TheTrailerStandsOnItsOwnBackPair()
    {
        var pose = Standing(0f);
        var build = Nominal;
        Span<Vector2> axleM = stackalloc Vector2[TowBar.Wheels];
        TowBar.AxleM(build, pose, TowBar.RearPair, axleM);

        for (var wheel = 0; wheel < TowBar.Wheels; wheel++)
        {
            var expected = TyreModel.WheelAtM(build, TowBar.RearPair + wheel);
            Assert.Equal(expected.X, axleM[wheel].X - pose.PositionM.X, 4);
            Assert.Equal(expected.Y, axleM[wheel].Y - pose.PositionM.Y, 4);
        }
    }

    /// <summary>
    /// <b>An unpowered, unbraked wheel holds sideways and gives the roll back the drag, and does nothing
    /// else</b> (EVA-5). A trailer rolling straight down clean tarmac with no drag under it spends nothing
    /// at all; one being carried sideways spends the whole of what it can to stop being.
    /// </summary>
    [Fact]
    public void TheTrailerWheelsHoldSidewaysAndOnlyRollAlong()
    {
        var build = Nominal;
        Span<Vector2> axleM = stackalloc Vector2[TowBar.Wheels];
        Span<WheelImpulse> into = stackalloc WheelImpulse[TowBar.Wheels];
        Span<SurfaceUnderWheel> ground = stackalloc SurfaceUnderWheel[TowBar.Wheels];
        ground.Fill(new SurfaceUnderWheel(1f, 0f, 0f, false));

        var rolling = new CarPose(
            Vector2.Zero, 0f, new Vector2(10f, 0f), 0f, Config.Car.MassKg, Vector2.Zero);
        TowBar.AxleM(build, rolling, TowBar.RearPair, axleM);
        TowBar.Step(build, rolling, axleM, ground, 0.6f, Config.TickSeconds, into);
        Assert.Equal(0f, into[0].ImpulseNs.Length(), 3);

        var sliding = new CarPose(
            Vector2.Zero, 0f, new Vector2(10f, 4f), 0f, Config.Car.MassKg, Vector2.Zero);
        TowBar.AxleM(build, sliding, TowBar.RearPair, axleM);
        TowBar.Step(build, sliding, axleM, ground, 0.6f, Config.TickSeconds, into);

        Assert.True(into[0].ImpulseNs.Y < 0f, "a wheel carried to its right did not push back to its left");
        Assert.Equal(0f, into[0].ImpulseNs.X, 3);
    }

    /// <summary>
    /// And never harder than the ground affords: the lateral impulse is the friction budget of the load
    /// that pair is actually carrying, so lifting a car's nose onto the hook takes grip off its back wheels.
    /// </summary>
    [Fact]
    public void ATrailerWheelSpendsNoMoreThanItsOwnLoadAffords()
    {
        var build = Nominal;
        Span<Vector2> axleM = stackalloc Vector2[TowBar.Wheels];
        Span<WheelImpulse> into = stackalloc WheelImpulse[TowBar.Wheels];
        Span<SurfaceUnderWheel> ground = stackalloc SurfaceUnderWheel[TowBar.Wheels];
        ground.Fill(new SurfaceUnderWheel(1f, 0f, 0f, false));

        var shoved = new CarPose(Vector2.Zero, 0f, new Vector2(0f, 30f), 0f, Config.Car.MassKg, Vector2.Zero);
        TowBar.AxleM(build, shoved, TowBar.RearPair, axleM);

        const float axleShare = 0.6f;
        TowBar.Step(build, shoved, axleM, ground, axleShare, Config.TickSeconds, into);

        var loadKg = Config.Car.MassKg * axleShare / TowBar.Wheels;
        var mostNs = build.GripMps2 * loadKg * Config.TickSeconds;
        Assert.True(
            MathF.Abs(into[0].ImpulseNs.Y) <= mostNs + 1e-3f,
            $"a trailer wheel put down {MathF.Abs(into[0].ImpulseNs.Y):F0} Ns of an affordable {mostNs:F0}");
    }
}
