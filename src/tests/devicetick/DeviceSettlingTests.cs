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

    /// <summary>A car along the row's last car's flank and away at a walking pace: touching it, and pressing on it nowhere.</summary>
    static void PassAlongTheLastCar(PhysicsWorld world)
    {
        var passing = world.AddNominalCar(new Vector2(Config.Car.LengthM * (Row - 0.5f), Config.Car.WidthM), 0f);
        world.ApplyCentralImpulse(passing, new Vector2(Config.Car.MassKg * 0.5f, 0f));
    }

    (int[] Woke, int[] Froze) Step(PhysicsWorld world)
    {
        using var solver = new DeviceSolver(device.Vk, Config, world.Table) { Settles = true };
        solver.LayTheRestCounts(world.Table);
        solver.Step(world.Table, StepSeconds, readBack: false);
        return (solver.Woke.ToArray(), solver.Froze.ToArray());
    }

    static void Advance(PhysicsWorld world, int steps)
    {
        for (var step = 0; step < steps; step++) world.Step(StepSeconds);
    }
}
