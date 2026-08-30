using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// The person's whole movement model, checked against a fake walker — a pose in and numbers out, with
/// no solver in the room. That is the reason the follower is a function rather than a method on the
/// fleet, and it is what makes these the cheapest tests in the engine.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class WalkerFollowerTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const float MassKg = 80f;

    static float Dt => Config.TickSeconds;

    [Fact]
    public void TheImpulseIsNeverMoreThanTheFeetCanSpend()
    {
        // At rest, asked for full pace: the correction wanted is far more than a tick of grip affords.
        var step = WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, Vector2.Zero, aimM: new Vector2(100f, 0f), moving: true,
            terrainCoefficient: 1f, onFeet: true, MassKg, Dt);

        Assert.Equal(Config.PersonFootGripMps2 * MassKg * Dt, step.ImpulseNs.Length(), 3);
    }

    [Fact]
    public void AWalkerAlreadyAtItsPaceIsAskedForNothing()
    {
        var atPace = new Vector2(Config.PersonWalkSpeedMps, 0f);
        var step = WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, atPace, aimM: new Vector2(100f, 0f), moving: true,
            terrainCoefficient: 1f, onFeet: true, MassKg, Dt);

        // Not "small": the brief asks for nothing at all, and it is the rule that keeps several hundred
        // standing walkers out of the solver's write path.
        Assert.Equal(Vector2.Zero, step.ImpulseNs);
    }

    [Fact]
    public void StandingIsDeclaredAsZeroVelocityAndNotAsNoDeclaration()
    {
        var moving = new Vector2(Config.PersonWalkSpeedMps, 0f);
        var step = WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, moving, aimM: new Vector2(100f, 0f), moving: false,
            terrainCoefficient: 1f, onFeet: true, MassKg, Dt);

        Assert.Equal(Vector2.Zero, step.DesiredMps);
        Assert.True(step.ImpulseNs.X < 0f);
    }

    /// <summary>TER-2: the movement effect applies to every body on the terrain, and it scales the pace.</summary>
    [Theory]
    [InlineData(1.0f)]
    [InlineData(0.8f)]
    [InlineData(0.15f)]
    public void TheGroundScalesThePaceItDeclares(float coefficient)
    {
        var step = WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, Vector2.Zero, aimM: new Vector2(100f, 0f), moving: true,
            coefficient, onFeet: true, MassKg, Dt);

        Assert.Equal(Config.PersonWalkSpeedMps * coefficient, step.DesiredMps.Length(), 4);
    }

    /// <summary>And the same factor scales the grip: one factor, both figures.</summary>
    [Fact]
    public void TheGroundScalesTheGripAsWellAsThePace()
    {
        var onGrass = WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, Vector2.Zero, aimM: new Vector2(100f, 0f), moving: true,
            Config.Terrain.GrassCoefficient, onFeet: true, MassKg, Dt);

        Assert.Equal(Config.PersonFootGripMps2 * Config.Terrain.GrassCoefficient * MassKg * Dt, onGrass.ImpulseNs.Length(), 3);
    }

    /// <summary>
    /// The difference between being knocked over and being sent down the road: off its feet, a walker
    /// declares whatever its manoeuvre asks and almost none of it can be spent.
    /// </summary>
    [Fact]
    public void OffItsFeetAWalkerStillDeclaresAndCannotAct()
    {
        var offFeet = WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, Vector2.Zero, aimM: new Vector2(100f, 0f), moving: true,
            terrainCoefficient: 1f, onFeet: false, MassKg, Dt);

        Assert.Equal(Config.PersonWalkSpeedMps, offFeet.DesiredMps.Length(), 4);
        Assert.Equal(Config.PersonSlidingGripMps2 * MassKg * Dt, offFeet.ImpulseNs.Length(), 3);
    }

    [Fact]
    public void TheHeadingTurnsNoFasterThanTheTurnRate()
    {
        var mostRad = Config.PersonTurnRateDegPerS * MathF.PI / 180f * Dt;
        var step = WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, Vector2.Zero, aimM: new Vector2(-100f, 0f), moving: true,
            terrainCoefficient: 1f, onFeet: true, MassKg, Dt);

        Assert.Equal(mostRad, MathF.Abs(step.HeadingRad), 5);
    }

    /// <summary>A body that turns on the spot has no reason to take the long way round.</summary>
    [Fact]
    public void TheTurnTakesTheShortWayRound()
    {
        Assert.Equal(-0.1f, WalkerFollower.TurnToward(0.1f, -0.1f, 1f), 5);
        Assert.Equal(-MathF.PI + 0.1f, WalkerFollower.TurnToward(MathF.PI - 0.1f, -MathF.PI + 0.1f, 1f), 5);
    }

    [Fact]
    public void AnAimUnderTheBodyLeavesTheHeadingAlone()
    {
        var step = WalkerFollower.Step(
            Config, headingRad: 1.234f, Vector2.One, Vector2.Zero, aimM: Vector2.One, moving: false,
            terrainCoefficient: 1f, onFeet: true, MassKg, Dt);

        Assert.Equal(1.234f, step.HeadingRad, 5);
    }
}
