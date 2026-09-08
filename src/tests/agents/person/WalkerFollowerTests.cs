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
[Trait(Priority.Key, Priority.P2)]
public class WalkerFollowerTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    const float MassKg = 80f;

    static float Dt => Config.TickSeconds;

    /// <summary>
    /// <b>The impulse saturates</b>: at rest and asked for full pace the correction wanted is already more
    /// than a tick of grip affords, so asking for ten times as much again buys nothing.
    /// </summary>
    /// <remarks>
    /// <b>Two steps against each other and not one against the figure it was computed from.</b> Asserting
    /// the impulse equals grip × mass × dt is the implementation written out twice (VER-12): it can only
    /// fail on the day somebody retunes the grip, and on that day it is edited rather than read. What
    /// saturation means is that the output stops following the input, and that is what is asked here.
    /// </remarks>
    [Fact]
    public void TheImpulseIsNeverMoreThanTheFeetCanSpend()
    {
        var far = Step(aimM: new Vector2(100f, 0f));
        var further = Step(aimM: new Vector2(1_000f, 0f));

        Assert.Equal(far.ImpulseNs.Length(), further.ImpulseNs.Length(), 3);
        Assert.True(far.ImpulseNs.Length() > 0f, "a walker asked for full pace from rest spent nothing");
    }

    /// <summary>A walker at rest on tarmac, asked to get to a place — the case every claim below varies.</summary>
    static WalkerStep Step(Vector2 aimM, float terrainCoefficient = 1f, bool onFeet = true) =>
        WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, Vector2.Zero, aimM, moving: true,
            terrainCoefficient, onFeet, MassKg, Dt);

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

    /// <summary>
    /// TER-2: the movement effect applies to every body on the terrain, and <b>it scales the pace</b> —
    /// ground worth a fifth declares a fifth of the pace the same walker declares on tarmac.
    /// </summary>
    [Theory]
    [InlineData(0.8f)]
    [InlineData(0.15f)]
    public void TheGroundScalesThePaceItDeclares(float coefficient)
    {
        var onTarmac = Step(aimM: new Vector2(100f, 0f)).DesiredMps.Length();

        Assert.Equal(onTarmac * coefficient, Step(new Vector2(100f, 0f), coefficient).DesiredMps.Length(), 4);
    }

    /// <summary>And the same factor scales the grip: one factor, both figures.</summary>
    [Theory]
    [InlineData(0.8f)]
    [InlineData(0.15f)]
    public void TheGroundScalesTheGripAsWellAsThePace(float coefficient)
    {
        var onTarmac = Step(aimM: new Vector2(100f, 0f)).ImpulseNs.Length();

        Assert.Equal(onTarmac * coefficient, Step(new Vector2(100f, 0f), coefficient).ImpulseNs.Length(), 3);
    }

    /// <summary>
    /// The difference between being knocked over and being sent down the road: off its feet, a walker
    /// <b>declares whatever its manoeuvre asks</b> — the same pace it would on its feet — <b>and spends less
    /// of it</b>, because what is under it is now rubber on tarmac rather than a foot pushing.
    /// </summary>
    /// <remarks>
    /// <b>Less, and not a stated fraction of it.</b> How much less is <c>Person.SlidingGripInFootGrips</c>
    /// and is a figure somebody may retune; that the declaration survives being knocked over while the
    /// spending does not is the behaviour, and it is what is asserted.
    /// </remarks>
    [Fact]
    public void OffItsFeetAWalkerStillDeclaresAndCannotAct()
    {
        var onFeet = Step(aimM: new Vector2(100f, 0f));
        var offFeet = Step(new Vector2(100f, 0f), onFeet: false);

        Assert.Equal(onFeet.DesiredMps.Length(), offFeet.DesiredMps.Length(), 4);
        Assert.True(
            offFeet.ImpulseNs.Length() < onFeet.ImpulseNs.Length(),
            $"a body off its feet spent {offFeet.ImpulseNs.Length():F1} Ns against {onFeet.ImpulseNs.Length():F1} "
            + "on them, so being knocked over cost it nothing");
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
