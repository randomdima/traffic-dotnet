using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Debug;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// <b>The construction the turn-circle layer draws</b> (OBS-2j), which is the one thing this overlay works
/// out for itself rather than reading off a producer — so it is the one thing in it that can be wrong on
/// its own, and the only place in the codebase where a turn centre is computed at all.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class TurnCircleTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly CarBuild Car = CarBuild.Nominal(Config, drivenFrontShare: 0f);

    /// <summary>A quarter of the way to the stop, which is a circle a street's worth of car could be on.</summary>
    const float PartLockRad = 0.15f;

    /// <summary>
    /// <b>The centre stands square out from the rear axle</b>, which is the whole of the rule: every wheel
    /// rolls about a point on its own axle's line, and the rear pair share one.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(1.1f)]
    [InlineData(-2.4f)]
    public void TheCentreIsSquareOutFromTheRearAxle(float headingRad)
    {
        var atM = new Vector2(30f, -12f);
        Assert.True(TurnCircle.Of(Car, atM, headingRad, PartLockRad, out var turn));

        Heading.Frame(headingRad, out var forward, out _);
        var rearAxleM = atM - (forward * Car.CentreAheadOfAxleM);

        // Square out: the centre is off the axle line by nothing at all along the car's own heading.
        Assert.Equal(0f, Vector2.Dot(turn.CentreM - rearAxleM, forward), 3);
        Assert.Equal((turn.CentreM - rearAxleM).Length(), turn.RearAxleRadiusM, 3);
    }

    /// <summary>
    /// <b>And it stands at the radius the wheelbase and the steering angle give it</b> — the bicycle
    /// model's own figure, which the crossing of the two axles has to agree with or one of them is wrong.
    /// </summary>
    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.3f)]
    [InlineData(-0.5f)]
    public void TheRadiusIsTheWheelbaseOverTheTangentOfTheLock(float steerRad)
    {
        Assert.True(TurnCircle.Of(Car, Vector2.Zero, 0.4f, steerRad, out var turn));

        Assert.Equal(MathF.Abs(Car.WheelbaseM / MathF.Tan(steerRad)), turn.RearAxleRadiusM, 2);

        // What is drawn is the arc of the nearest rear wheel, which is half a track inside that.
        Assert.Equal(turn.RearAxleRadiusM - Car.HalfTrackM, turn.RadiusM, 2);
    }

    /// <summary>
    /// <b>The inside of the turn is the side the wheel is turned to.</b> A picture that put the centre on
    /// the wrong side would be a circle drawn where the car has just come from.
    /// </summary>
    [Theory]
    [InlineData(PartLockRad, 1f)]
    [InlineData(-PartLockRad, -1f)]
    public void TheCentreIsOnTheSideTheWheelIsTurnedTo(float steerRad, float side)
    {
        Assert.True(TurnCircle.Of(Car, Vector2.Zero, 0f, steerRad, out var turn));

        Heading.Frame(0f, out _, out var right);
        Assert.True(
            Vector2.Dot(turn.CentreM, right) * side > 0f,
            $"a lock of {steerRad:F2} rad puts the centre at {turn.CentreM.X:F2},{turn.CentreM.Y:F2}");

        // And the near rear wheel is the one on that side, which is the wheel the arc is drawn to.
        Assert.True(Vector2.Dot(turn.RearInnerM, right) * side > 0f);
    }

    /// <summary>
    /// <b>Straight wheels have no centre to draw.</b> It is off the far side of the town and the arc
    /// through the car is a line, so the layer draws nothing rather than a strip across the map.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(1e-5f)]
    public void StraightWheelsHaveNoCircle(float steerRad) =>
        Assert.False(TurnCircle.Of(Car, Vector2.Zero, 0f, steerRad, out _));

    /// <summary>And neither has a wheel turned so little that the circle is wider than anything is drawn.</summary>
    [Fact]
    public void NorHasAWheelTurnedTooLittleToBeACircle()
    {
        var justOffStraightRad = MathF.Atan(Car.WheelbaseM / (TurnCircle.WidestM * 2f));

        Assert.False(TurnCircle.Of(Car, Vector2.Zero, 0f, justOffStraightRad, out _));
    }

    /// <summary>
    /// <b>Every look turns its own circle</b> (CAR-11): a long car with a wide track is on a wider arc at
    /// the same angle than a short one, because both are its own axles' arithmetic and neither is the
    /// nominal car's.
    /// </summary>
    [Fact]
    public void EachLookTurnsItsOwnCircle()
    {
        var builds = CarBuilds.OfTheFleet(Config, CarCatalog.Shared);
        var tightestM = float.MaxValue;
        var widestM = 0f;

        for (var variant = 0; variant < CarCatalog.Shared.Count; variant++)
        {
            ref readonly var build = ref builds.Of(variant);
            Assert.True(TurnCircle.Of(build, Vector2.Zero, 0f, build.MaxSteerRad, out var turn));

            Assert.Equal(MathF.Abs(build.WheelbaseM / MathF.Tan(build.MaxSteerRad)), turn.RearAxleRadiusM, 2);
            tightestM = MathF.Min(tightestM, turn.RearAxleRadiusM);
            widestM = MathF.Max(widestM, turn.RearAxleRadiusM);
        }

        Assert.True(widestM > tightestM, $"every look on the fleet turns the same {widestM:F2} m circle");
    }
}
