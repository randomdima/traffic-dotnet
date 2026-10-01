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

    static float MostTurnRad => Config.PersonTurnRateDegPerS * MathF.PI / 180f * Dt;

    /// <summary>A walker at rest, facing +x, asked to get to a place — the case most claims below vary.</summary>
    static WalkerStep Step(Vector2 aimM, bool onFeet = true) =>
        WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, Vector2.Zero, Vector2.Zero, aimM, moving: true,
            onFeet, MassKg, Dt);

    /// <summary><b>PER-3: no acceleration of its own</b> — a walker at rest facing somewhere far is at its pace a tick later.</summary>
    [Fact]
    public void AWalkerFacingItsAimIsAtItsPaceATickLater()
    {
        var step = Step(aimM: new Vector2(100f, 0f));

        var velocityMps = step.ImpulseNs / MassKg;
        Assert.Equal(Config.PersonWalkSpeedMps, velocityMps.X, 4);
        Assert.Equal(0f, velocityMps.Y, 4);
    }

    /// <summary>
    /// <b>An aim nearer than a tick's walk is stood on at the end of the tick</b>, whichever way the body faces:
    /// it is what makes a walker arrive rather than overshoot and come back.
    /// </summary>
    [Fact]
    public void AnAimInsideATicksWalkIsReachedExactly()
    {
        var aimM = Vector2.Normalize(new Vector2(-0.6f, 1f)) * (Config.PersonStepM * 0.5f);
        var step = Step(aimM);

        Assert.Equal(aimM.X, step.ImpulseNs.X / MassKg * Dt, 5);
        Assert.Equal(aimM.Y, step.ImpulseNs.Y / MassKg * Dt, 5);
    }

    /// <summary><b>A walker not facing its aim turns on the spot</b>: it walks at nothing it is not facing.</summary>
    [Fact]
    public void AWalkerNotFacingItsAimStandsWhileItTurns()
    {
        var step = Step(aimM: new Vector2(-100f, 1f));

        Assert.Equal(Vector2.Zero, step.DesiredMps);
    }

    /// <summary>
    /// <b>The walk that used to circle its aim</b>: a body facing away from a point beside it, which at its pace
    /// and turn rate cannot be reached by turning while walking. It turns, walks, and stands on it.
    /// </summary>
    /// <remarks>
    /// Stepped on a fake body that keeps whatever it is given — the follower's own sums with no contact in
    /// them — for a second, which is several times what the turn and the walk together take.
    /// </remarks>
    [Fact]
    public void AWalkerFacingAwayFromAPointBesideItArrivesAndStands()
    {
        var aimM = new Vector2(-0.2f, 0.3f);
        var positionM = Vector2.Zero;
        var velocityMps = Vector2.Zero;
        var declaredMps = Vector2.Zero;
        var headingRad = 0f;

        for (var tick = 0; tick < Config.Sim.TickRateHz; tick++)
        {
            var step = WalkerFollower.Step(
                Config, headingRad, positionM, velocityMps, declaredMps, aimM, moving: true, onFeet: true, MassKg, Dt);
            headingRad = step.HeadingRad;
            declaredMps = step.DesiredMps;
            velocityMps += step.ImpulseNs / MassKg;
            positionM += velocityMps * Dt;
        }

        Assert.True(
            (positionM - aimM).Length() < 1e-4f && velocityMps.Length() < 1e-4f,
            $"a second on, the walker is {(positionM - aimM).Length():F3} m off its aim at {velocityMps.Length():F2} m/s");
    }

    /// <summary>
    /// <b>What a contact did to the body is taken back no faster than the feet grip</b>: a shove at ten times
    /// the pace costs the feet what one at the pace does, so a walker is carried by a shove and does not brace
    /// against a car.
    /// </summary>
    /// <remarks>
    /// <b>Two shoves against each other and not one against the figure it was computed from.</b> Asserting
    /// the impulse equals grip × mass × dt is the implementation written out twice (VER-12): it can only
    /// fail on the day somebody retunes the grip, and on that day it is edited rather than read.
    /// </remarks>
    [Fact]
    public void AShoveIsTakenBackNoFasterThanTheFeetGrip()
    {
        var shoved = Standing(new Vector2(0f, Config.PersonWalkSpeedMps));
        var thrown = Standing(new Vector2(0f, Config.PersonWalkSpeedMps * 10f));

        Assert.Equal(shoved.ImpulseNs.Length(), thrown.ImpulseNs.Length(), 3);
        Assert.True(shoved.ImpulseNs.Length() > 0f, "a walker shoved sideways spent nothing taking it back");

        // A walker that declared nothing and is moving: it is what a contact left it doing.
        static WalkerStep Standing(Vector2 velocityMps) =>
            WalkerFollower.Step(
                Config, headingRad: 0f, Vector2.Zero, velocityMps, Vector2.Zero, Vector2.Zero, moving: true,
                onFeet: true, MassKg, Dt);
    }

    [Fact]
    public void AWalkerAlreadyAtItsPaceIsAskedForNothing()
    {
        var atPace = new Vector2(Config.PersonWalkSpeedMps, 0f);
        var step = WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, atPace, atPace, aimM: new Vector2(100f, 0f), moving: true,
            onFeet: true, MassKg, Dt);

        // Not "small": the brief asks for nothing at all, and it is the rule that keeps several hundred
        // standing walkers out of the solver's write path.
        Assert.Equal(Vector2.Zero, step.ImpulseNs);
    }

    [Fact]
    public void StandingIsDeclaredAsZeroVelocityAndNotAsNoDeclaration()
    {
        var moving = new Vector2(Config.PersonWalkSpeedMps, 0f);
        var step = WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.Zero, moving, moving, aimM: new Vector2(100f, 0f), moving: false,
            onFeet: true, MassKg, Dt);

        Assert.Equal(Vector2.Zero, step.DesiredMps);
        Assert.True(step.ImpulseNs.X < 0f);
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
        var step = Step(aimM: new Vector2(-100f, 0f));

        Assert.Equal(MostTurnRad, MathF.Abs(step.HeadingRad), 5);
    }

    /// <summary>A body that turns on the spot has no reason to take the long way round.</summary>
    [Fact]
    public void TheTurnTakesTheShortWayRound()
    {
        Assert.Equal(-0.1f, WalkerFollower.TurnToward(0.1f, -0.1f, 1f), 5);
        Assert.Equal(-MathF.PI + 0.1f, WalkerFollower.TurnToward(MathF.PI - 0.1f, -MathF.PI + 0.1f, 1f), 5);
    }

    /// <summary>
    /// <b>An aim under the body is a stand</b>, and it is the walking side's own answer to being asked for
    /// nowhere to go: a body left declaring its last heading carries on walking out of the town while
    /// whatever had charge of it thinks it is standing still.
    /// </summary>
    [Fact]
    public void AnAimUnderTheBodyDeclaresAStandEvenWhileMoving()
    {
        var step = WalkerFollower.Step(
            Config, headingRad: 0f, Vector2.One, Vector2.Zero, Vector2.Zero, aimM: Vector2.One, moving: true,
            onFeet: true, MassKg, Dt);

        Assert.Equal(Vector2.Zero, step.DesiredMps);
    }

    [Fact]
    public void AnAimUnderTheBodyLeavesTheHeadingAlone()
    {
        var step = WalkerFollower.Step(
            Config, headingRad: 1.234f, Vector2.One, Vector2.Zero, Vector2.Zero, aimM: Vector2.One, moving: false,
            onFeet: true, MassKg, Dt);

        Assert.Equal(1.234f, step.HeadingRad, 5);
    }
}
