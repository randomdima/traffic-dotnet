using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// <c>SOL-37</c>: a body at rest is frozen out of the step, and anything that reaches it brings it back in time to
/// be pushed — asked of the solver directly, one case a clause.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P0)]
public class FreezingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static float StepSeconds => Config.TickSeconds;

    /// <summary><c>SOL-37</c>: a body freezes on the step its rest time is up, and not on the one before.</summary>
    [Fact]
    public void ABodyAtRestFreezesWhenItsRestTimeIsUp()
    {
        var world = new PhysicsWorld(Config);
        var car = world.AddNominalCar(Vector2.Zero, 0f);

        Advance(world, Config.SolverRestTicks - 1);
        Assert.False(world.IsFrozen(car));

        world.Step(StepSeconds);
        Assert.True(world.IsFrozen(car));
    }

    /// <summary><c>SOL-37</c>: a frozen body is left out of the step — nothing integrates it.</summary>
    [Fact]
    public void AFrozenBodyIsNotIntegrated()
    {
        var world = new PhysicsWorld(Config);
        world.AddNominalCar(Vector2.Zero, 0f);
        Advance(world, Config.SolverRestTicks);

        world.Step(StepSeconds);

        Assert.Equal(1, world.FrozenBodyCount);
        Assert.Equal(0, world.IntegratedBodyCount);
    }

    /// <summary><c>SOL-37a</c>: an impulse wakes a frozen body, and it moves on the step the impulse was spent before.</summary>
    [Fact]
    public void AnImpulseWakesAFrozenBodyInTimeToMoveIt()
    {
        var world = new PhysicsWorld(Config);
        var car = world.AddNominalCar(Vector2.Zero, 0f);
        Advance(world, Config.SolverRestTicks);

        world.ApplyCentralImpulse(car, new Vector2(Config.Car.MassKg, 0f));
        world.Step(StepSeconds);

        Assert.False(world.IsFrozen(car));
        Assert.True(world.PositionOf(car).X > 0f, "a frozen car spent an impulse and stood still");
    }

    /// <summary>
    /// <c>SOL-37a</c>: a car driven into a frozen row pushes the whole row on the step it arrives — the car it
    /// strikes, and the one that car leans on — rather than shoving the first into the second.
    /// </summary>
    [Fact]
    public void AFrozenRowIsPushedOnTheStepItIsStruck()
    {
        var world = new PhysicsWorld(Config);
        var lengthM = Config.Car.LengthM;
        var struck = world.AddNominalCar(new Vector2(lengthM, 0f), 0f);
        var leant = world.AddNominalCar(new Vector2(lengthM * 2f, 0f), 0f);
        Advance(world, Config.SolverRestTicks);
        Assert.True(world.IsFrozen(struck) && world.IsFrozen(leant));

        // Stood nose to tail with the row and rolling into it, so the step it is added on is the step it strikes.
        var striker = world.AddNominalCar(Vector2.Zero, 0f);
        world.ApplyCentralImpulse(striker, new Vector2(Config.Car.MassKg * 3f, 0f));
        world.Step(StepSeconds);

        Assert.True(world.VelocityOf(leant).X > 0f, "the far end of a frozen row was not pushed when the near end was struck");
    }

    /// <summary>
    /// <c>SOL-37b</c>: a body at rest against one the step is still moving is not frozen, however long it has been
    /// at rest itself.
    /// </summary>
    [Fact]
    public void ABodyTouchingOneStillMovingStaysAwake()
    {
        var world = new PhysicsWorld(Config);
        var standing = world.AddNominalCar(Vector2.Zero, 0f);
        var passing = world.AddNominalCar(new Vector2(-Config.Car.LengthM * 0.5f, Config.Car.WidthM), 0f);

        // Along the standing car's flank at a walking pace, touching it and pressing on it nowhere.
        world.ApplyCentralImpulse(passing, new Vector2(Config.Car.MassKg * 0.5f, 0f));
        Advance(world, Config.SolverRestTicks * 2);

        Assert.False(world.IsFrozen(standing));
    }

    /// <summary>
    /// <c>SOL-37c</c>: a pair that froze touching is the same touch when one of them is woken — it does not begin
    /// again.
    /// </summary>
    [Fact]
    public void ATouchThatFrozeDoesNotBeginAgainOnWaking()
    {
        var world = new PhysicsWorld(Config);
        var first = world.AddNominalCar(Vector2.Zero, 0f);
        world.AddNominalCar(new Vector2(Config.Car.LengthM, 0f), 0f);
        world.Step(StepSeconds);
        Advance(world, Config.SolverRestTicks);
        Assert.True(world.IsFrozen(first));

        // Pressed into the other, which keeps the pair touching on the step it is woken.
        world.ApplyCentralImpulse(first, new Vector2(Config.Car.MassKg * 0.01f, 0f));
        world.Step(StepSeconds);

        Assert.Empty(Began(world));
    }

    /// <summary>
    /// <c>SOL-37</c>: a body frozen at exact rest is one no step can tell from a body that never froze — the same
    /// town to the bit, through a row struck and pushed.
    /// </summary>
    [Fact]
    public void FreezingAtExactRestChangesNothing()
    {
        var never = new SimConfig { Solver = new SolverFigures { RestSpeedMps = 0f, RestS = 1e6f } };
        var exact = new SimConfig { Solver = new SolverFigures { RestSpeedMps = 0f } };

        Assert.Equal(Run(never), Run(exact));

        static Vector4[] Run(SimConfig config)
        {
            // A row of props against the cars' flank, so the cars rest on the town's furniture as well as on each other.
            const float PropRadiusM = 0.4f;
            var propYM = -((config.Car.WidthM * 0.5f) + PropRadiusM);
            var world = new PhysicsWorld(config);
            for (var prop = 0; prop < 12; prop++) world.AddStaticDisc(new Vector2(prop * 1.5f, propYM), PropRadiusM);

            var fleet = new BodyId[8];
            for (var car = 0; car < fleet.Length; car++)
            {
                fleet[car] = world.AddNominalCar(new Vector2(car * config.Car.LengthM, 0f), 0f);
            }

            world.SettleStatics();
            for (var step = 0; step < 240; step++)
            {
                if (step == 60) world.ApplyCentralImpulse(fleet[^1], new Vector2(-config.Car.MassKg * 4f, 0f));

                world.Step(config.TickSeconds);
            }

            var read = new Vector4[fleet.Length];
            for (var car = 0; car < fleet.Length; car++)
            {
                read[car] = new Vector4(world.PositionOf(fleet[car]), world.VelocityOf(fleet[car]).X, world.HeadingOf(fleet[car]));
            }

            return read;
        }
    }

    static List<Touch> Began(PhysicsWorld world)
    {
        var began = new List<Touch>();
        foreach (var touch in world.BeganTouchingThisStep()) began.Add(touch);
        return began;
    }

    static void Advance(PhysicsWorld world, int steps)
    {
        for (var step = 0; step < steps; step++) world.Step(StepSeconds);
    }
}
