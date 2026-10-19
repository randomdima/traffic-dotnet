using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// <b>The solver's link</b> (SOL-38), asked of the solver directly: a solid bar between a point on each of two bodies,
/// free to turn at both ends, and a pin at a rest length of nought.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class LinkTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static float HalfLengthM => Config.Car.LengthM * 0.5f;

    /// <summary>The point at a body's own nose and tail, on its centre line.</summary>
    static Vector2 Nose => new(HalfLengthM, 0f);

    static Vector2 Tail => new(-HalfLengthM, 0f);

    static Vector2 PointOf(PhysicsWorld world, BodyId body, Vector2 anchorM) =>
        world.PositionOf(body) + Shape.Rotate(world.RotationOf(body), anchorM);

    static void Advance(PhysicsWorld world, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++) world.Step(Config.TickSeconds);
    }

    /// <summary>
    /// <b>A bar holds its length pulled and pushed</b>: the leading body hauled forward and then shoved back, the
    /// one behind kept the bar's length off it the whole way, never further and never nearer.
    /// </summary>
    [Fact]
    public void ABarHoldsItsLengthPulledAndPushed()
    {
        const float RestM = 0.5f;
        var world = new PhysicsWorld(Config);
        world.FitLinks(1);
        var lead = world.AddNominalCar(Vector2.Zero, 0f);
        var behind = world.AddNominalCar(new Vector2(-Config.Car.LengthM - RestM, 0f), 0f);
        world.Link(0, lead, Tail, behind, Nose, RestM);

        var worstM = 0f;
        for (var tick = 0; tick < 240; tick++)
        {
            var pushNs = tick < 120 ? 400f : -400f;
            world.ApplyCentralImpulse(lead, new Vector2(pushNs, 0f));
            world.Step(Config.TickSeconds);
            var lengthM = Vector2.Distance(PointOf(world, lead, Tail), PointOf(world, behind, Nose));
            worstM = MathF.Max(worstM, MathF.Abs(lengthM - RestM));
        }

        Assert.True(worstM < 0.02f, $"the bar was {worstM:F3} m off its length");
        Assert.True(world.VelocityOf(behind).X < 0f, "the body behind was not pushed back with the one ahead");
    }

    /// <summary>
    /// <b>A bar spends nothing across itself</b>: a body moving square to the bar drags nothing sideways and turns
    /// nothing, so both ends are free to turn.
    /// </summary>
    [Fact]
    public void ABarSpendsNothingAcrossItself()
    {
        var world = new PhysicsWorld(Config);
        world.FitLinks(1);
        var first = world.AddNominalCar(Vector2.Zero, 0f);
        var second = world.AddNominalCar(new Vector2(10f, 0f), 0f);
        world.Link(0, first, Vector2.Zero, second, Vector2.Zero, 10f);
        world.ApplyCentralImpulse(first, new Vector2(0f, 3f * Config.Car.MassKg));

        Advance(world, 1);

        Assert.Equal(0f, world.VelocityOf(second).Y, 1e-4f);
        Assert.Equal(0f, world.YawRateOf(second), 1e-5f);
        Assert.Equal(0f, world.YawRateOf(first), 1e-5f);
    }

    /// <summary>
    /// <b>A pull reaches the tail of a chain in the step it is spent</b>, and the chain moves as one soon after: ten
    /// bodies on nine bars, the head given an impulse — the tail moving off in that step, and every one of them at the
    /// chain's common speed a tenth of a second on.
    /// </summary>
    /// <remarks>
    /// Not at once: the passes are Gauss–Seidel, and a chain of ten is not solved whole in sixteen of them
    /// (<c>docs/tech-debt.md</c>, a chain of links solved whole).
    /// </remarks>
    [Fact]
    public void AChainOfTenCarriesAPullFromItsHeadToItsTail()
    {
        const int Bodies = 10;
        const float RestM = 0.3f;
        var world = new PhysicsWorld(Config);
        world.FitLinks(Bodies - 1);
        var chain = new BodyId[Bodies];
        for (var at = 0; at < Bodies; at++) chain[at] = world.AddCar(new Vector2(-at * (Config.Car.LengthM + RestM), 0f), 0f, new Vector2(HalfLengthM, 1f), 0f, Config.Car.MassKg, onRails: true);
        for (var at = 0; at + 1 < Bodies; at++) world.Link(at, chain[at], Tail, chain[at + 1], Nose, RestM);

        world.ApplyCentralImpulse(chain[0], new Vector2(Bodies * Config.Car.MassKg * 5f, 0f));
        Advance(world, 1);
        Assert.True(world.VelocityOf(chain[^1]).X > 0.5f, $"the tail moved off at {world.VelocityOf(chain[^1]).X:F2} m/s");

        Advance(world, Config.Sim.TickRateHz / 10);
        foreach (var body in chain) Assert.InRange(world.VelocityOf(body).X, 4.9f, 5.1f);
    }

    /// <summary>
    /// <b>A pin keeps its two points on one</b> and leaves the pinned body free to swing about it: a body towed round
    /// a turn by a point ahead of it follows with the two points together.
    /// </summary>
    [Fact]
    public void APinKeepsItsTwoPointsOnOneRoundATurn()
    {
        var world = new PhysicsWorld(Config);
        world.FitLinks(1);
        var tractor = world.AddNominalCar(Vector2.Zero, 0f);
        var towed = world.AddNominalCar(new Vector2(-Config.Car.LengthM - 1f, 0f), 0f);
        var hookM = new Vector2(-HalfLengthM - 0.5f, 0f);
        var eyeM = new Vector2(HalfLengthM + 0.5f, 0f);
        world.Link(0, tractor, hookM, towed, eyeM, 0f);

        var worstM = 0f;
        for (var tick = 0; tick < 300; tick++)
        {
            var forward = world.RotationOf(tractor);
            world.ApplyCentralImpulse(tractor, forward * 200f);
            world.ApplyImpulseAt(tractor, Shape.LeftPerpendicular(forward) * 30f, world.PositionOf(tractor) + forward * HalfLengthM);
            world.Step(Config.TickSeconds);
            worstM = MathF.Max(worstM, Vector2.Distance(PointOf(world, tractor, hookM), PointOf(world, towed, eyeM)));
        }

        Assert.True(worstM < 0.02f, $"the pin's two points stood {worstM:F3} m apart");
        Assert.True(MathF.Abs(world.HeadingOf(tractor)) > 0.3f, "the tractor never turned");
    }

    /// <summary>
    /// <b>A linked body never comes to rest</b>, so a linked pair is never frozen apart (SOL-37b) — and a frozen
    /// body linked to another is woken.
    /// </summary>
    [Fact]
    public void ALinkedPairIsNeverFrozenAndALinkWakesAFrozenBody()
    {
        var world = new PhysicsWorld(Config);
        world.FitLinks(1);
        var first = world.AddNominalCar(Vector2.Zero, 0f);
        var second = world.AddNominalCar(new Vector2(-Config.Car.LengthM - 1f, 0f), 0f);
        Advance(world, (int)(Config.Solver.RestS * Config.Sim.TickRateHz) + 2);
        Assert.True(world.IsFrozen(first) && world.IsFrozen(second), "two bodies at rest never froze");

        world.Link(0, first, Tail, second, Nose, 1f);
        Advance(world, (int)(Config.Solver.RestS * Config.Sim.TickRateHz) * 3);

        Assert.False(world.IsFrozen(first));
        Assert.False(world.IsFrozen(second));
    }

    /// <summary><b>A link to a body out of the world holds nothing</b>: the one left in the world moves as if unlinked.</summary>
    [Fact]
    public void ALinkToABodyOutOfTheWorldHoldsNothing()
    {
        var world = new PhysicsWorld(Config);
        world.FitLinks(1);
        var first = world.AddNominalCar(Vector2.Zero, 0f);
        var second = world.AddNominalCar(new Vector2(-Config.Car.LengthM - 1f, 0f), 0f);
        world.Link(0, first, Tail, second, Nose, 1f);
        world.Contain(second);

        world.ApplyCentralImpulse(first, new Vector2(Config.Car.MassKg * 4f, 0f));
        Advance(world, 1);

        Assert.True(world.VelocityOf(first).X > 3.9f, $"the body in the world moved off at {world.VelocityOf(first).X:F2} m/s");
    }

    /// <summary><b>A rail vehicle is never damped</b> (SOL-38, <see cref="BodyFlags.Undamped"/>): moving free, it keeps its speed to the bit.</summary>
    [Fact]
    public void ARailVehicleIsNeverDamped()
    {
        var world = new PhysicsWorld(Config);
        var car = world.AddNominalCar(Vector2.Zero, 0f);
        var railed = world.AddCar(new Vector2(0f, 50f), 0f, new Vector2(10f, 1.5f), 0.1f, 40_000f, onRails: true);
        world.ApplyCentralImpulse(car, new Vector2(Config.Car.MassKg * 20f, 0f));
        world.ApplyCentralImpulse(railed, new Vector2(40_000f * 20f, 0f));

        Advance(world, 60);

        Assert.Equal(20f, world.VelocityOf(railed).X);
        Assert.True(world.VelocityOf(car).X < 20f, "the car was not damped either, so the test says nothing");
    }
}
