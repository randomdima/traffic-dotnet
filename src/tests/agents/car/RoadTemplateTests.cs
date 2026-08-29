using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The one template a car drives on the road: `E-4`'s swerve. It is asserted on the property that makes
/// it usable at all — <b>where it ends and which way the car is pointing when it does</b> — because a
/// template that ends anywhere else hands `P-4` a car it has to recover rather than drive.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class RoadTemplateTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>The shape is asked of the nominal car, whose circle the town's own figures are quoted for.</summary>
    static readonly CarBuild Car = CarBuild.Nominal(Config, Config.Car.DrivenFrontShare);

    static readonly ArcSeg[] Into = new ArcSeg[RoadTemplates.MostSwerveArcs];

    /// <summary>A tenth of the car's own width: past that the line is not the one it left.</summary>
    static float ToleranceM => Config.Car.WidthM * 0.1f;

    /// <summary>The tightest a swerve is ever drawn — what a car at rest is laid one at.</summary>
    static float LockRadiusM => Config.ParkingTemplateRadiusM;

    /// <summary>The road under the shape, where every case but the bend below is asked on a straight one.</summary>
    const float OnAStraight = 0f;

    /// <summary>
    /// <b>A swerve comes back onto the line it left.</b> The whole shape is a detour in the driven line
    /// and never in the route, so it has to end where the car would have been, pointing the way it was
    /// pointing.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void TheSwerveEndsBackOnItsOwnLine(float side)
    {
        var offsetM = Config.Car.WidthM * side;
        var line = RoadTemplates.TryLaySwerve(Vector2.Zero, 0f, offsetM, passM: 6f, LockRadiusM, OnAStraight, Into);

        Assert.True(line.Any);
        Assert.Equal(0f, line.EndM.Y, ToleranceM);
        Assert.Equal(0f, WrappedRad(line.EndHeadingRad), 1e-2f);

        // And it goes somewhere: the straight between the two bends, plus what the four arcs spend.
        Assert.True(line.EndM.X > 6f, $"the swerve covered {line.EndM.X:F1} m, which is less than it was asked to pass");
    }

    /// <summary>And it actually goes out to the side it was asked for, half way along.</summary>
    [Fact]
    public void TheSwerveReachesTheOffsetItWasAskedFor()
    {
        var offsetM = Config.Car.WidthM;
        var line = RoadTemplates.TryLaySwerve(Vector2.Zero, 0f, offsetM, passM: 6f, LockRadiusM, OnAStraight, Into);
        var middle = Spline.SampleAt(Into.AsSpan(0, line.ArcCount), line.LengthM * 0.5f);

        Assert.Equal(offsetM, middle.PositionM.Y, ToleranceM);
    }

    /// <summary>Wider than two turning circles is not a swerve, and it is refused rather than approximated.</summary>
    [Fact]
    public void TheSwerveIsRefusedPastWhatTwoQuarterTurnsCanReach()
    {
        var tooFarM = LockRadiusM * 2f + 1f;
        Assert.False(
            RoadTemplates.TryLaySwerve(Vector2.Zero, 0f, tooFarM, passM: 6f, LockRadiusM, OnAStraight, Into).Any);
    }

    /// <summary>
    /// <b>The same swerve laid for a car that is moving is a longer, flatter shape</b>, which is the whole
    /// of what lets one be driven at road speed: the profile reads a template's arcs exactly as it reads a
    /// road's, so a swerve drawn at the steering lock is a 6 m/s manoeuvre whatever the road affords.
    /// </summary>
    [Fact]
    public void ASwerveDrawnForSpeedIsFlatterThanOneDrawnAtTheLock()
    {
        var fast = new ArcSeg[RoadTemplates.MostSwerveArcs];
        var atLock = RoadTemplates.TryLaySwerve(
            Vector2.Zero, 0f, Config.Car.WidthM, 6f, LockRadiusM, OnAStraight, Into);
        var atSpeed = RoadTemplates.TryLaySwerve(
            Vector2.Zero, 0f, Config.Car.WidthM, 6f, Config.CarCorneringRadiusM(20f, 1f), OnAStraight, fast);

        Assert.True(atLock.Any && atSpeed.Any);
        Assert.True(atSpeed.LengthM > atLock.LengthM);
        Assert.True(
            CarFollower.CornerMps(fast[0].Curvature, Config.Tyre.GripMps2 * Config.Driving.GripMargin) >= 20f,
            "a swerve laid for 20 m/s is one the corner term does not slow the car below 20 m/s to drive");
    }

    /// <summary>
    /// <b>A swerve on a bend stays beside the road rather than cutting across it.</b> The shape is drawn on
    /// top of the road's own curvature, so what it does is measured against the arc the car was on and not
    /// against the plane — and a shape drawn flat is a chord, which is a car off the carriageway at the far
    /// end of every bend it is laid on.
    /// </summary>
    [Fact]
    public void ASwerveOnABendIsMeasuredAgainstTheBendAndNotAgainstTheChord()
    {
        var offsetM = Config.Car.WidthM;
        var bend = 1f / 40f;
        var flat = new ArcSeg[RoadTemplates.MostSwerveArcs];
        var onTheBend = RoadTemplates.TryLaySwerve(Vector2.Zero, 0f, offsetM, 20f, LockRadiusM, bend, Into);
        var asAChord = RoadTemplates.TryLaySwerve(Vector2.Zero, 0f, offsetM, 20f, LockRadiusM, OnAStraight, flat);

        Assert.True(onTheBend.Any && asAChord.Any);

        // Where the road itself gets to over the same ground, against where each shape ends up.
        var road = new ArcSeg(Vector2.Zero, 0f, onTheBend.LengthM, bend);
        var beside = (onTheBend.EndM - road.EndM).Length();
        var across = (asAChord.EndM - road.EndM).Length();

        Assert.True(beside < Config.LaneOffsetM, $"the swerve ended {beside:F2} m off the lane it was laid beside");
        Assert.True(across > Config.LaneOffsetM * 2f, $"a flat swerve ended only {across:F2} m off, so this proves nothing");
    }

    static float WrappedRad(float rad) => MathF.Atan2(MathF.Sin(rad), MathF.Cos(rad));
}
