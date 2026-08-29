using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Evacuator;

/// <summary>
/// <b>EVA-5's arithmetic, judged without a body</b>: where the coupling is anchored, what it spends, and
/// what the two wheels under a towed car do. Everything <see cref="TowBar"/> answers is a function of
/// numbers, which is the whole reason it is a component rather than a passage of the town.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class TowBarTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static CarBuild Nominal => CarBuild.Nominal(Config, drivenFrontShare: 1f);

    /// <summary>The one build in the catalogue that carries an arm, because the arm is its picture's (EVA-5).</summary>
    static CarBuild Evacuator =>
        CarBuild.Of(Config, CarCatalog.Shared.Variants[CarCatalog.Shared.Evacuator]);

    static CarPose Standing(float atX, float headingRad = 0f) =>
        new(new Vector2(atX, 0f), headingRad, Vector2.Zero, 0f, Config.Car.MassKg, Vector2.Zero);

    static HookEnd EndAt(Vector2 atM, Vector2 velocityMps, in CarPose pose, float massKg) => new(
        atM, velocityMps, atM - pose.PositionM, 1f / massKg,
        // The solver's own box inertia, so the arithmetic under test is priced the way the town prices it.
        12f / (massKg * ((Config.Car.LengthM * Config.Car.LengthM) + (Config.Car.WidthM * Config.Car.WidthM))));

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
        var daylightM = float.NaN;
        foreach (var variant in CarCatalog.Shared.Variants)
        {
            var towed = CarBuild.Of(Config, variant);
            var noseM = TowBar.ForkM(towed, Standing(0f), byTheTail: false);
            var tailM = TowBar.ForkM(towed, Standing(0f), byTheTail: true);
            Assert.Equal(Config.Evacuator.TowGripInsideTheEndM, towed.HalfLengthM - noseM.X, 4);
            Assert.Equal(Config.Evacuator.TowGripInsideTheEndM, towed.HalfLengthM + tailM.X, 4);

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
    /// <b>And the point it holds is ahead of the towed car and not on it</b> (EVA-5), which is what stops a
    /// tow crabbing: pulled at a point that far up its own axis, a wreck dragged out of line is turned back
    /// into it by the same impulse that is hauling it.
    /// </summary>
    [Fact]
    public void AWreckDraggedOutOfLineIsTurnedBackIntoIt()
    {
        var askewRad = 0.5f;
        var tractor = Standing(0f);
        var towed = new CarPose(
            new Vector2(-TowBar.SetDownBehindM(Evacuator, Nominal), 0f), askewRad, Vector2.Zero, 0f,
            Config.Car.MassKg, Vector2.Zero);

        var hookM = TowBar.HookM(Evacuator, tractor);
        var eyeM = TowBar.EyeM(Nominal, towed, Evacuator.TowReachM, byTheTail: false);
        var pullNs = TowBar.PullNs(
            EndAt(hookM, Vector2.Zero, tractor, 3200f), EndAt(eyeM, Vector2.Zero, towed, Config.Car.MassKg),
            Config.Evacuator.HitchSettleS, Config.Evacuator.HitchMostMps2, Config.Evacuator.HitchSideShare,
            Config.Car.MassKg, Config.TickSeconds);

        // The moment of that impulse about the wreck's own middle, against the way it is skewed.
        var armM = eyeM - towed.PositionM;
        var turningNms = (armM.X * pullNs.Y) - (armM.Y * pullNs.X);
        Assert.True(turningNms * askewRad < 0f, $"a wreck skewed {askewRad:F2} rad was turned {turningNms:F1} Nm further");
    }

    /// <summary>
    /// <b>An arm under no stretch, with neither end moving relative to the other, asks for nothing</b> — the
    /// state a steady tow spends its time in, and the one that must cost the pair no impulse at all.
    /// </summary>
    [Fact]
    public void AnArmUnderNoStretchSpendsNothing()
    {
        var movingMps = new Vector2(9f, 0f);
        var pose = Standing(0f);

        var pullNs = TowBar.PullNs(
            EndAt(new Vector2(3f, 7f), movingMps, pose, 3200f),
            EndAt(new Vector2(3f, 7f), movingMps, pose, Config.Car.MassKg),
            Config.Evacuator.HitchSettleS, Config.Evacuator.HitchMostMps2, Config.Evacuator.HitchSideShare,
            Config.Car.MassKg, Config.TickSeconds);

        Assert.Equal(0f, pullNs.Length(), 3);
    }

    /// <summary>
    /// EVA-5: a stretched bar pulls the eye <em>towards</em> the hook. The sign is the one thing about a
    /// coupling that cannot be got wrong quietly — reversed, it flings the load away and the tow is a
    /// catapult.
    /// </summary>
    [Fact]
    public void AStretchedBarPullsTheEyeTowardsTheHook()
    {
        var hookM = new Vector2(2f, 0f);
        var eyeM = Vector2.Zero;
        var pose = Standing(0f);

        var pullNs = TowBar.PullNs(
            EndAt(hookM, Vector2.Zero, pose, 3200f), EndAt(eyeM, Vector2.Zero, pose, Config.Car.MassKg),
            Config.Evacuator.HitchSettleS, Config.Evacuator.HitchMostMps2, Config.Evacuator.HitchSideShare,
            Config.Car.MassKg, Config.TickSeconds);

        Assert.True(pullNs.X > 0f, $"the bar pushed the eye away from the hook ({pullNs.X:F1} Ns)");
        Assert.True(MathF.Abs(pullNs.Y) < 1e-3f, "a bar stretched along one axis pulled across it");
    }

    /// <summary>
    /// And never harder than it may (EVA-5): a stretch of metres meets the ceiling rather than being paid
    /// in full, so a coupling handed an impossible correction cannot throw either body across the street.
    /// </summary>
    [Fact]
    public void TheBarIsCappedAtWhatItMaySpend()
    {
        var pose = Standing(0f);
        var pullNs = TowBar.PullNs(
            EndAt(new Vector2(40f, 0f), Vector2.Zero, pose, 3200f),
            EndAt(Vector2.Zero, Vector2.Zero, pose, Config.Car.MassKg),
            Config.Evacuator.HitchSettleS, Config.Evacuator.HitchMostMps2, Config.Evacuator.HitchSideShare,
            Config.Car.MassKg, Config.TickSeconds);

        var mostNs = Config.Evacuator.HitchMostMps2 * Config.Car.MassKg * Config.TickSeconds;
        Assert.True(pullNs.Length() <= mostNs + 1e-3f, $"the bar spent {pullNs.Length():F0} Ns of {mostNs:F0}");
    }

    /// <summary>
    /// <b>And far less across the bar than along it</b>, which is the figure that keeps a tow from being a
    /// jack-knife: the same stretch offered sideways buys a small fraction of the impulse.
    /// </summary>
    [Fact]
    public void TheBarSpendsFarLessAcrossItselfThanAlongItself()
    {
        var pose = Standing(0f);
        var eye = EndAt(Vector2.Zero, Vector2.Zero, pose, Config.Car.MassKg);

        var alongNs = TowBar.PullNs(
            EndAt(new Vector2(40f, 0f), Vector2.Zero, pose, 3200f), eye, Config.Evacuator.HitchSettleS,
            Config.Evacuator.HitchMostMps2, Config.Evacuator.HitchSideShare, Config.Car.MassKg, Config.TickSeconds);

        // The same impossible correction offered across the arm rather than along it. The arm's own
        // direction is the line between the two points, so a stretch is always "along" — what is measured
        // here is the sideways budget, reached by giving the eye a speed across that line.
        var acrossNs = TowBar.PullNs(
            EndAt(new Vector2(4f, 0f), Vector2.Zero, pose, 3200f),
            EndAt(Vector2.Zero, new Vector2(0f, 40f), pose, Config.Car.MassKg), Config.Evacuator.HitchSettleS,
            Config.Evacuator.HitchMostMps2, Config.Evacuator.HitchSideShare, Config.Car.MassKg, Config.TickSeconds);

        Assert.True(
            MathF.Abs(acrossNs.Y) < alongNs.Length() * 0.5f,
            $"the bar spent {MathF.Abs(acrossNs.Y):F0} Ns across itself against {alongNs.Length():F0} Ns along it");
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
