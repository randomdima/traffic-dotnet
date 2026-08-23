using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The two templates a car drives on the road: `E-4`'s swerve and `P-11`'s counter-swing. Both are
/// asserted on the property that makes them usable at all — <b>where they end and which way the car is
/// pointing when they do</b> — because a template that ends anywhere else hands `P-4` a car it has to
/// recover rather than drive.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class RoadTemplateTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

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

    /// <summary>
    /// <b>The counter-swing ends antiparallel, at the lane separation it was given.</b> That is the whole
    /// contract: `P-4` takes the lane under the car afterwards, and a shape that ended anywhere but on the
    /// opposite lane's line would have it take the wrong one.
    /// </summary>
    [Fact]
    public void TheTurnAroundEndsOnTheOppositeLane()
    {
        var separationM = Config.LaneOffsetM * 2f;
        var line = RoadTemplates.TryLayTurnAround(
            Config, Vector2.Zero, 0f, new Vector2(0f, separationM), -Vector2.UnitX, Into);

        Assert.True(line.Any);
        Assert.Equal(separationM, line.EndM.Y, ToleranceM);
        Assert.Equal(MathF.PI, MathF.Abs(WrappedRad(line.EndHeadingRad)), 1e-2f);
    }

    /// <summary>
    /// The counter-swing pays for a narrow separation by reaching further along the arm, which is
    /// <b>the constraint worth checking</b> — and it is why the caller asks the terrain whether the shape
    /// fits rather than measuring the road's width.
    /// </summary>
    [Fact]
    public void TheNarrowerTheLanesTheFurtherAlongTheArmItReaches()
    {
        var near = RoadTemplates.TryLayTurnAround(
            Config, Vector2.Zero, 0f, new Vector2(0f, Config.LaneOffsetM * 2f), -Vector2.UnitX, Into);
        var wide = RoadTemplates.TryLayTurnAround(
            Config, Vector2.Zero, 0f, new Vector2(0f, Config.ParkingTemplateRadiusM * 2f), -Vector2.UnitX, Into);

        Assert.True(near.Any && wide.Any);
        Assert.True(
            near.EndM.X > wide.EndM.X,
            $"the narrow turn reached {near.EndM.X:F1} m along the arm against the wide one's {wide.EndM.X:F1} m");
    }

    /// <summary>A lane running any other way is not one this shape ends on, and the caller has picked the wrong one.</summary>
    [Fact]
    public void TheTurnAroundRefusesALaneThatIsNotTheOppositeOne()
    {
        Assert.False(RoadTemplates.TryLayTurnAround(
            Config, Vector2.Zero, 0f, new Vector2(0f, 3f), Vector2.UnitX, Into).Any);
    }

    /// <summary>Wider than the two arcs can span is refused, exactly as the swerve is.</summary>
    [Fact]
    public void TheTurnAroundIsRefusedPastWhatTwoArcsCanSpan()
    {
        var tooFarM = Config.ParkingTemplateRadiusM * 2f + 1f;
        Assert.False(RoadTemplates.TryLayTurnAround(
            Config, Vector2.Zero, 0f, new Vector2(0f, tooFarM), -Vector2.UnitX, Into).Any);
    }

    static float WrappedRad(float rad) => MathF.Atan2(MathF.Sin(rad), MathF.Cos(rad));
}
