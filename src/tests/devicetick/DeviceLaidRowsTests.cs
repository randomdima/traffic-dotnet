using System.Numerics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A row the town changed, laid on the device</b> (<see cref="DeviceSolver.LayRows"/>): only what the town changed is
/// written, over a body the device has stepped on from where the table last saw it.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P4)]
public class DeviceLaidRowsTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static float StepSeconds => Config.TickSeconds;

    /// <summary>How long the device drives the car on its own: long enough that damping parts its motion from the table's.</summary>
    const int DrivenSteps = 60;

    /// <summary>A car put on other channels stands where the device's step left it, not where the table last had it.</summary>
    [Fact]
    public void ACarPutOnOtherChannelsStandsWhereTheDeviceLeftIt()
    {
        var (world, car, solver) = ACarTheDeviceDroveOn();
        using var _ = solver;
        var leftAt = solver.Pose[car.Index];

        world.PutOnChannels(car, 2);
        Lay(world, solver);

        Assert.Equal(leftAt, solver.Pose[car.Index]);
    }

    /// <summary>A car pushed moves as the device had it moving, plus what the push added.</summary>
    [Fact]
    public void ACarPushedMovesAsTheDeviceHadItPlusThePush()
    {
        var (world, car, solver) = ACarTheDeviceDroveOn();
        using var _ = solver;
        var movingMps = new Vector2(solver.Motion[car.Index].X, solver.Motion[car.Index].Y);
        var impulseNs = new Vector2(0f, Config.Car.MassKg);

        world.ApplyCentralImpulse(car, impulseNs);
        Lay(world, solver);

        Assert.Equal(movingMps + (impulseNs * world.InverseMassOf(car)), new Vector2(solver.Motion[car.Index].X, solver.Motion[car.Index].Y));
    }

    /// <summary>
    /// A car rolling across an empty table, laid whole on the device and stepped there on its own — the table's own row
    /// left where it was laid.
    /// </summary>
    (PhysicsWorld World, BodyId Car, DeviceSolver Solver) ACarTheDeviceDroveOn()
    {
        var world = new PhysicsWorld(Config);
        world.KeepTheChanges();
        var car = world.AddNominalCar(Vector2.Zero, 0f);
        world.ApplyCentralImpulse(car, new Vector2(Config.Car.MassKg * 10f, 0f));

        var solver = new DeviceSolver(device.Vk, Config, world.Table, room: world.RoomForBodies);
        solver.LayWhole(world.Table);
        world.ForgetTheChanges();
        for (var step = 0; step < DrivenSteps; step++) Run(solver, step: true);

        return (world, car, solver);
    }

    /// <summary>The rows the town changed laid and read back, without a step.</summary>
    void Lay(PhysicsWorld world, DeviceSolver solver)
    {
        solver.LayRows(world.Table, world.Changes);
        world.ForgetTheChanges();
        Run(solver, step: false);
    }

    void Run(DeviceSolver solver, bool step)
    {
        var vk = device.Vk;
        using var batch = new ComputeBatch(vk);
        var commands = batch.Begin();
        solver.Send(commands);
        vk.AfterTransfer(commands);
        if (solver.RecordLaidRows(commands)) vk.BetweenKernels(commands);
        if (step) solver.Record(commands, StepSeconds);
        vk.BeforeTransfer(commands);
        solver.RecordReadBack(commands, everything: true);
        batch.SubmitAndWait();
    }
}
