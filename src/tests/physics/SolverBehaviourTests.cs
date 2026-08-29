using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// What the solver must <em>do</em>, asked of it directly rather than through a town: the requirement
/// list <c>SOL-1</c>…<c>SOL-36</c>, one case each for the ones a rig can answer.
/// </summary>
/// <remarks>
/// The staged crash table, the drive and soak probes and the crossing gate are where the model is judged
/// against the reference build's picture. These are the properties underneath that — the ones whose
/// failure would show up there as a town that wrecks itself rather than as anything a reader could name.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class SolverBehaviourTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static float StepSeconds => Config.TickSeconds;

    /// <summary><c>SOL-11</c>: the world is a plane seen from above, so a body nothing acts on goes nowhere.</summary>
    [Fact]
    public void NothingFalls()
    {
        var world = new PhysicsWorld(Config);
        var car = world.AddNominalCar(new Vector2(10f, 10f), 0.4f);

        Advance(world, 600);

        Assert.Equal(new Vector2(10f, 10f), world.PositionOf(car));
        Assert.Equal(0.4f, world.HeadingOf(car), 1e-6f);
    }

    /// <summary>
    /// <c>SOL-14</c>: an impulse off the centre spins the body it hits, unless that body's rotation is
    /// locked. A car turns because its tyres turn it; a walker is never spun, and its heading is intent.
    /// </summary>
    [Fact]
    public void AnImpulseOffTheCentreSpinsACarAndNeverAWalker()
    {
        var world = new PhysicsWorld(Config);
        var car = world.AddNominalCar(Vector2.Zero, 0f);
        var walker = world.AddPerson(new Vector2(50f, 0f));

        var atCarM = new Vector2(Config.Car.LengthM * 0.5f, Config.Car.WidthM * 0.5f);
        world.ApplyImpulseAt(car, new Vector2(0f, 500f), atCarM);
        world.ApplyImpulseAt(walker, new Vector2(0f, 500f), new Vector2(50f + 0.4f, 0.4f));
        Advance(world, 1);

        Assert.True(MathF.Abs(world.YawRateOf(car)) > 0.01f, "a car struck off its centre did not turn");
        Assert.Equal(0f, world.YawRateOf(walker));

        // Both were still pushed: PHY-9 says being shoved is always possible, locked rotation or not.
        Assert.True(world.VelocityOf(walker).Y > 1f);
    }

    /// <summary>
    /// <c>SOL-12</c>: an overlap is pushed out without the push becoming motion. <b>The pair must end at
    /// rest</b> — a correction folded into the real velocity is energy the collision never had, and it is
    /// what makes a resting pair breathe and a queue jitter.
    /// </summary>
    [Fact]
    public void AnOverlapIsPushedOutAndTheBodiesDoNotKeepMoving()
    {
        var world = new PhysicsWorld(Config);
        var first = world.AddNominalCar(Vector2.Zero, 0f);
        var second = world.AddNominalCar(new Vector2(Config.Car.LengthM - 0.5f, 0f), 0f);

        Advance(world, 120);

        var apartM = world.PositionOf(second).X - world.PositionOf(first).X;
        Assert.True(
            apartM > Config.Car.LengthM - Config.Solver.AllowedPenetrationM * 2f,
            $"the pair was left {apartM:F4} m apart, inside the {Config.Car.LengthM:F1} m they need");

        Assert.True(world.VelocityOf(first).Length() < 0.01f, $"the first body kept {world.VelocityOf(first).Length():F4} m/s");
        Assert.True(world.VelocityOf(second).Length() < 0.01f, $"the second body kept {world.VelocityOf(second).Length():F4} m/s");
    }

    /// <summary><c>SOL-13</c>: no bounce. A body driven into a wall stops against it and is not returned.</summary>
    [Fact]
    public void NothingBounces()
    {
        var world = new PhysicsWorld(Config);
        var car = world.AddNominalCar(Vector2.Zero, 0f);
        world.AddStaticBox(new Vector2(20f, 0f), new Vector2(2f, 20f), 0f);
        world.SettleStatics();

        world.ApplyCentralImpulse(car, new Vector2(Config.Car.MassKg * 12f, 0f));
        Advance(world, 180);

        Assert.True(world.VelocityOf(car).X > -0.05f, $"the car came back off the wall at {world.VelocityOf(car).X:F3} m/s");
        Assert.True(world.PositionOf(car).X < 20f, "the car is on the far side of a wall it cannot pass");
    }

    /// <summary>
    /// <c>SOL-6</c>: it is the <em>beginning</em> of a touch that is reported. A pair resting against each
    /// other in a queue is one report however long it leans there, which is the whole of what
    /// "once per touch" needs.
    /// </summary>
    [Fact]
    public void ATouchIsReportedOnceHoweverLongItLasts()
    {
        var world = new PhysicsWorld(Config);
        world.AddNominalCar(Vector2.Zero, 0f);
        world.AddNominalCar(new Vector2(Config.Car.LengthM, 0f), 0f);

        var began = 0;
        for (var step = 0; step < 300; step++)
        {
            world.Step(StepSeconds);
            foreach (var _ in world.BeganTouchingThisStep()) began++;
        }

        Assert.Equal(1, began);
    }

    /// <summary>
    /// <c>SOL-4</c>: a body taken out of the world and put back keeps its identity — its slot, its tag and
    /// its roster place — and while it is out it has no shape and no layer (PHY-7).
    /// </summary>
    [Fact]
    public void AContainedBodyLeavesTheWorldAndComesBackTheSame()
    {
        var world = new PhysicsWorld(Config);
        var walker = world.AddPerson(new Vector2(5f, 0f));
        world.Tag(walker, new BodyTag(BodyKind.Person, 7));

        Assert.True(world.CastRay(Vector2.Zero, new Vector2(20f, 0f), BodyId.None, statics: false, out var before));
        Assert.Equal(new BodyTag(BodyKind.Person, 7), before.Tag);

        world.Contain(walker);
        Assert.False(world.CastRay(Vector2.Zero, new Vector2(20f, 0f), BodyId.None, statics: false, out _));
        Assert.Equal(0, world.IntegratedBodyCount);
        Assert.Equal(1, world.DynamicBodyCount);

        world.Release(walker, new Vector2(12f, 0f), 1.2f);
        Assert.True(world.CastRay(Vector2.Zero, new Vector2(20f, 0f), BodyId.None, statics: false, out var after));
        Assert.Equal(new BodyTag(BodyKind.Person, 7), after.Tag);
        Assert.Equal(1, world.IntegratedBodyCount);
        Assert.Equal(new Vector2(12f, 0f), world.PositionOf(walker));
    }

    /// <summary><c>SOL-10</c>: a figure with no census is not a figure, so the world can say what it is carrying.</summary>
    [Fact]
    public void TheWorldCountsWhatItCarries()
    {
        var world = new PhysicsWorld(Config);
        for (var prop = 0; prop < 40; prop++) world.AddStaticDisc(new Vector2(prop * 5f, 30f), 0.5f);

        var walker = world.AddPerson(Vector2.Zero);
        world.AddNominalCar(new Vector2(20f, 0f), 0f);
        world.SettleStatics();
        world.Step(StepSeconds);

        Assert.Equal(40, world.StaticBodyCount);
        Assert.Equal(2, world.DynamicBodyCount);
        Assert.Equal(2, world.IntegratedBodyCount);

        world.Contain(walker);
        world.Step(StepSeconds);
        Assert.Equal(2, world.DynamicBodyCount);
        Assert.Equal(1, world.IntegratedBodyCount);
    }

    /// <summary><c>SOL-8</c>: whether anything static stands inside an axis-aligned box, which is what a driven line asks once.</summary>
    [Fact]
    public void TheStaticQueryFindsWhatStandsInTheBox()
    {
        var world = new PhysicsWorld(Config);
        world.AddStaticDisc(new Vector2(10f, 10f), 0.6f);
        world.AddStaticBox(new Vector2(40f, 0f), new Vector2(6f, 3f), 0.5f);
        world.AddNominalCar(new Vector2(70f, 0f), 0f);
        world.SettleStatics();

        Assert.True(world.StaticInBox(new Vector2(9f, 9f), new Vector2(11f, 11f)));
        Assert.True(world.StaticInBox(new Vector2(38f, -2f), new Vector2(42f, 2f)));

        // Empty ground, and ground with only a dynamic body on it: a car is not the town's furniture.
        Assert.False(world.StaticInBox(new Vector2(20f, 20f), new Vector2(25f, 25f)));
        Assert.False(world.StaticInBox(new Vector2(68f, -2f), new Vector2(72f, 2f)));
    }

    /// <summary><c>SOL-9</c>: how deep a body is into everything touching it, which is what measures PHY-1.</summary>
    [Fact]
    public void OverlapIsHowDeepTheBodyIs()
    {
        var world = new PhysicsWorld(Config);
        var first = world.AddNominalCar(Vector2.Zero, 0f);
        var apart = world.AddNominalCar(new Vector2(60f, 0f), 0f);
        world.AddNominalCar(new Vector2(Config.Car.LengthM - 0.4f, 0f), 0f);

        world.Step(StepSeconds);

        Assert.Equal(0.4f, world.OverlapOf(first), 1e-3f);
        Assert.Equal(0f, world.OverlapOf(apart));
    }

    /// <summary>
    /// <c>SOL-35</c>: the same start and the same steps give the same world, to the bit. Every ordering
    /// inside the solver is derived from body indices, so nothing about discovery order can reach the
    /// answer.
    /// </summary>
    [Fact]
    public void TheSameStepsFromTheSameStartGiveTheSameWorld()
    {
        var first = Run();
        var second = Run();

        Assert.Equal(first.Length, second.Length);
        for (var body = 0; body < first.Length; body++) Assert.Equal(first[body], second[body]);

        static Vector3[] Run()
        {
            var world = new PhysicsWorld(Config);
            var fleet = new BodyId[24];
            for (var car = 0; car < fleet.Length; car++)
            {
                fleet[car] = world.AddNominalCar(new Vector2(car % 6 * 3.6f, car / 6 * 2.2f), car * 0.13f);
            }

            for (var prop = 0; prop < 30; prop++) world.AddStaticDisc(new Vector2(prop * 1.7f, 6f), 0.5f);

            world.SettleStatics();
            for (var step = 0; step < 240; step++)
            {
                for (var car = 0; car < fleet.Length; car++)
                {
                    var push = new Vector2(MathF.Cos(car + step * 0.01f), MathF.Sin(car * 2f)) * 400f;
                    world.ApplyImpulseAt(fleet[car], push, world.PositionOf(fleet[car]) + new Vector2(1f, 0.5f));
                }

                world.Step(Config.TickSeconds);
            }

            var read = new Vector3[fleet.Length];
            for (var car = 0; car < fleet.Length; car++)
            {
                read[car] = new Vector3(world.PositionOf(fleet[car]), world.HeadingOf(fleet[car]));
            }

            return read;
        }
    }

    /// <summary>
    /// <c>PHY-5b</c>: a body put on the downed layer is driven through, walked through and cast through,
    /// and the only thing left that can stop it is the town's furniture.
    /// </summary>
    [Fact]
    public void ADownedBodyIsPassedThroughByEverythingButTheGround()
    {
        var world = new PhysicsWorld(Config);
        var walker = world.AddPerson(new Vector2(10f, 0f));
        world.Tag(walker, new BodyTag(BodyKind.Person, 3));
        var car = world.AddNominalCar(Vector2.Zero, 0f);
        world.AddStaticBox(new Vector2(10f, 4f), new Vector2(4f, 1f), 0f);
        world.SettleStatics();

        world.PutOnLayer(walker, CollisionLayer.Downed);

        // A driver looking down the road no longer finds it, which is the same filter said as a query.
        Assert.False(world.CastRay(Vector2.Zero, new Vector2(20f, 0f), BodyId.None, statics: false, out _));

        // Driven over: the car keeps the speed it arrived with and the body is left where it lay.
        world.ApplyCentralImpulse(car, new Vector2(Config.Car.MassKg * 8f, 0f));
        Advance(world, 120);

        Assert.True(world.PositionOf(car).X > 12f, "a car was stopped by a body it should have passed over");
        Assert.Equal(new Vector2(10f, 0f), world.PositionOf(walker));

        // And the wall still holds it: the one thing the downed layer keeps scanning.
        world.ApplyCentralImpulse(walker, new Vector2(0f, Config.Person.MassKg * 6f));
        Advance(world, 120);

        Assert.True(world.PositionOf(walker).Y < 3.5f, "a body in the road slid through a building");
    }

    static void Advance(PhysicsWorld world, int steps)
    {
        for (var step = 0; step < steps; step++) world.Step(StepSeconds);
    }
}
