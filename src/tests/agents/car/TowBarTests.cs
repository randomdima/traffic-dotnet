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
        new(new Vector2(atX, 0f), headingRad, Vector2.Zero, 0f, Config.Car.MassKg);

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

        // And what is left on the ground under it is the axle the fork is not holding up: by the tail, its frontmost.
        Assert.Equal(0, TowBar.AxleOnTheGround(Nominal, byTheTail: true));
        Assert.Equal(1, TowBar.AxleOnTheGround(Nominal, byTheTail: false));
    }

    /// <summary>
    /// EVA-5: <b>a towed car stands on the axle at its far end from the fork, and on no other wheel</b>: carried
    /// sideways, the wheels of that axle hold, and those up on the arm spend nothing at all.
    /// </summary>
    [Fact]
    public void TheTrailerStandsOnTheAxleAwayFromTheForkAlone()
    {
        var build = Nominal;
        var sliding = new CarPose(Vector2.Zero, 0f, new Vector2(10f, 4f), 0f, Config.Car.MassKg);
        var axle = TowBar.AxleOnTheGround(build, byTheTail: false);
        var into = Trailer(build, sliding, axle, 0.6f);

        for (var wheel = 0; wheel < build.WheelCount; wheel++)
        {
            var down = build.WheelAt(wheel).Axle == axle;
            Assert.True(down == (into[wheel].ImpulseNs != Vector2.Zero), $"wheel {wheel} on axle {build.WheelAt(wheel).Axle}");
        }
    }

    /// <summary>A towed car's wheels stepped on the axle down, each at where it stands (<see cref="TyreModel.WheelPointsM"/>).</summary>
    static WheelImpulse[] Trailer(in CarBuild build, in CarPose pose, int axle, float onTheAxleShare)
    {
        var atM = new Vector2[build.WheelCount];
        var into = new WheelImpulse[build.WheelCount];
        var ground = new SurfaceUnderWheel[build.WheelCount];
        Array.Fill(ground, new SurfaceUnderWheel(1f, 0f, 0f, false));
        TyreModel.WheelPointsM(build, pose, atM);
        TowBar.Step(build, pose, axle, atM, ground, onTheAxleShare, Config.TickSeconds, into);
        return into;
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
        var axle = TowBar.AxleOnTheGround(build, byTheTail: false);
        var down = RearRight;

        var rolling = new CarPose(Vector2.Zero, 0f, new Vector2(10f, 0f), 0f, Config.Car.MassKg);
        Assert.Equal(0f, Trailer(build, rolling, axle, 0.6f)[down].ImpulseNs.Length(), 3);

        var sliding = new CarPose(Vector2.Zero, 0f, new Vector2(10f, 4f), 0f, Config.Car.MassKg);
        var into = Trailer(build, sliding, axle, 0.6f);

        Assert.True(into[down].ImpulseNs.Y < 0f, "a wheel carried to its right did not push back to its left");
        Assert.Equal(0f, into[down].ImpulseNs.X, 3);
    }

    /// <summary>The nominal car's first wheel on its rear axle, which a car caught by the nose rolls on.</summary>
    const int RearRight = 2;

    /// <summary>
    /// And never harder than the ground affords: the lateral impulse is the friction budget of the load
    /// that pair is actually carrying, so lifting a car's nose onto the hook takes grip off its back wheels.
    /// </summary>
    [Fact]
    public void ATrailerWheelSpendsNoMoreThanItsOwnLoadAffords()
    {
        var build = Nominal;
        var shoved = new CarPose(Vector2.Zero, 0f, new Vector2(0f, 30f), 0f, Config.Car.MassKg);
        var axle = TowBar.AxleOnTheGround(build, byTheTail: false);

        const float axleShare = 0.6f;
        var into = Trailer(build, shoved, axle, axleShare);

        // The axle down is the rear pair, and each carries half of what is left on it.
        var loadKg = Config.Car.MassKg * axleShare * 0.5f;
        var mostNs = build.GripMps2 * loadKg * Config.TickSeconds;
        Assert.True(
            MathF.Abs(into[RearRight].ImpulseNs.Y) <= mostNs + 1e-3f,
            $"a trailer wheel put down {MathF.Abs(into[RearRight].ImpulseNs.Y):F0} Ns of an affordable {mostNs:F0}");
    }
}
