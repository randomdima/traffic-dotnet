using System.Numerics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>What the device's step froze and woke</b> (<see cref="DeviceSolver.Settles"/>): one step from a table the host
/// laid, where what is passed along the contacts has to run against their order — the far end of a row reached last.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceSettlingTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>A row long enough that one pass over its contacts in their order reaches only its near end.</summary>
    const int Row = 6;

    static float StepSeconds => Config.TickSeconds;

    /// <summary>
    /// <c>SOL-37a</c>: a frozen row whose last car a moving car meets is taken in whole on the step they meet — the car
    /// met, and every car touching it back to the first, though none of them is moved.
    /// </summary>
    [Fact]
    public void AFrozenRowMetAtItsLastCarIsTakenInWhole()
    {
        var world = new PhysicsWorld(Config);
        var row = new int[Row];
        for (var car = 0; car < Row; car++) row[car] = world.AddNominalCar(new Vector2(Config.Car.LengthM * car, 0f), 0f).Index;
        Advance(world, Config.SolverRestTicks);
        PassAlongTheLastCar(world);

        Assert.Equal(row, Step(world).Woke);
    }

    /// <summary>
    /// <c>SOL-37b</c>: a row whose rest time is up freezes not at all while its last car touches a car still moving.
    /// </summary>
    [Fact]
    public void ARowAtRestAgainstACarStillMovingFreezesNotAtAll()
    {
        var world = new PhysicsWorld(Config);
        for (var car = 0; car < Row; car++) world.AddNominalCar(new Vector2(Config.Car.LengthM * car, 0f), 0f);
        Advance(world, Config.SolverRestTicks - 1);
        PassAlongTheLastCar(world);

        Assert.Empty(Step(world).Froze);
    }

    /// <summary>
    /// <c>SOL-37c</c>: a pair that froze touching is held through a step that moves neither, and is the same touch when one
    /// of them is woken — it does not begin again.
    /// </summary>
    [Fact]
    public void ATouchThatFrozeDoesNotBeginAgainOnWaking()
    {
        var world = new PhysicsWorld(Config);
        var first = world.AddNominalCar(Vector2.Zero, 0f);
        world.AddNominalCar(new Vector2(Config.Car.LengthM, 0f), 0f);
        Advance(world, Config.SolverRestTicks + 1);

        using var solver = Settling(world);
        solver.LayTheTouching(world.TouchingKeys);
        solver.Step(world.Table, StepSeconds, readBack: false);

        // Pressed into the other, which keeps the pair touching on the step it is woken.
        world.ApplyCentralImpulse(first, new Vector2(Config.Car.MassKg * 0.01f, 0f));
        solver.Step(world.Table, StepSeconds, readBack: false);

        Assert.Empty(solver.Begun.ToArray());
    }

    /// <summary>
    /// <c>SOL-37c</c>: touches held through a step either side of one the step found — frozen pairs below and above a
    /// pressed one in index — are still the same touches when their cars are woken on the next.
    /// </summary>
    [Fact]
    public void TouchesHeldEitherSideOfAFoundOneDoNotBeginAgain()
    {
        var world = new PhysicsWorld(Config);
        var firsts = new BodyId[3];
        for (var pair = 0; pair < 3; pair++)
        {
            var atM = new Vector2(0f, 10f * pair);
            firsts[pair] = world.AddNominalCar(atM, 0f);
            world.AddNominalCar(atM + new Vector2(Config.Car.LengthM, 0f), 0f);
        }

        Advance(world, Config.SolverRestTicks + 1);
        using var solver = Settling(world);
        solver.LayTheTouching(world.TouchingKeys);
        var press = new Vector2(Config.Car.MassKg * 0.01f, 0f);
        world.ApplyCentralImpulse(firsts[1], press);
        solver.Step(world.Table, StepSeconds, readBack: false);

        foreach (var first in firsts) world.ApplyCentralImpulse(first, press);
        solver.Step(world.Table, StepSeconds, readBack: false);

        Assert.Empty(solver.Begun.ToArray());
    }

    /// <summary>A car along the row's last car's flank and away at a walking pace: touching it, and pressing on it nowhere.</summary>
    static void PassAlongTheLastCar(PhysicsWorld world)
    {
        var passing = world.AddNominalCar(new Vector2(Config.Car.LengthM * (Row - 0.5f), Config.Car.WidthM), 0f);
        world.ApplyCentralImpulse(passing, new Vector2(Config.Car.MassKg * 0.5f, 0f));
    }

    (int[] Woke, int[] Froze) Step(PhysicsWorld world)
    {
        using var solver = Settling(world);
        solver.Step(world.Table, StepSeconds, readBack: false);
        return (solver.Woke.ToArray(), solver.Froze.ToArray());
    }

    /// <summary>A device solver settling what it steps and listing what it settled, laid the table's rest counts.</summary>
    DeviceSolver Settling(PhysicsWorld world)
    {
        var solver = new DeviceSolver(device.Vk, Config, world.Table) { Settles = true, ListsWhatSettled = true };
        solver.LayTheRestCounts(world.Table);
        return solver;
    }

    static void Advance(PhysicsWorld world, int steps)
    {
        for (var step = 0; step < steps; step++) world.Step(StepSeconds);
    }
}
