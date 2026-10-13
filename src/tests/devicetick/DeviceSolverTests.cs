using System.Numerics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The solver's step on the device, asked the CPU's question</b>: one table handed to both, and the device's
/// contacts and poses held to the CPU's own step.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceSolverTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// A yard of cars and walkers among furniture, dense enough that most of them touch something, all moving and none
    /// frozen — so the CPU's owners are every body, as the device's are.
    /// </summary>
    static PhysicsWorld Yard(int seed, int cars, int walkers, float sideM)
    {
        var random = new Random(seed);
        var world = new PhysicsWorld(Config);
        for (var box = 0; box < 6; box++)
        {
            world.AddStaticBox(
                new Vector2(random.NextSingle() * sideM, random.NextSingle() * sideM), new Vector2(6f, 3f),
                random.NextSingle() * MathF.PI, box % 2 == 0 ? 0f : 0.2f);
        }

        for (var tree = 0; tree < 8; tree++)
        {
            world.AddStaticDisc(new Vector2(random.NextSingle() * sideM, random.NextSingle() * sideM), 0.6f);
        }

        for (var car = 0; car < cars; car++)
        {
            var body = world.AddCar(
                new Vector2(random.NextSingle() * sideM, random.NextSingle() * sideM), (random.NextSingle() * 2f - 1f) * MathF.PI,
                new Vector2(2.0f, 0.75f), 0.15f, 1200f);
            world.ApplyCentralImpulse(body, new Vector2(random.NextSingle() * 2f - 1f, random.NextSingle() * 2f - 1f) * 9000f);
        }

        for (var walker = 0; walker < walkers; walker++)
        {
            var body = world.AddPerson(new Vector2(random.NextSingle() * sideM, random.NextSingle() * sideM));
            world.ApplyCentralImpulse(body, new Vector2(random.NextSingle() * 2f - 1f, random.NextSingle() * 2f - 1f) * 150f);
        }

        return world;
    }

    /// <summary>
    /// The device finds the CPU's contacts: the same pairs in the same order — by the lower body, the furniture second
    /// — with the same number of points and manifolds that agree to a tenth of a millimetre.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheDeviceFindsTheCpusContactsInTheCpusOrder(int seed)
    {
        var world = Yard(seed, cars: 60, walkers: 80, sideM: 60f);
        world.Step(Config.TickSeconds);
        using var solver = new DeviceSolver(device.Vk, Config, world.Table);

        solver.Step(world.Table, Config.TickSeconds, readBack: true);
        world.Step(Config.TickSeconds);

        var cpu = world.Contacts;
        Assert.False(solver.Overflowed);
        Assert.True(cpu.Count > 20, $"the yard should be crowded, found {cpu.Count} contacts");
        Assert.Equal(cpu.Count, solver.ContactCount);
        for (var contact = 0; contact < cpu.Count; contact++)
        {
            var on = solver.Contacts[contact];
            Assert.Equal((cpu.First[contact], cpu.Second[contact], cpu.Points[contact]), ((int)on.X, (int)on.Y, (int)on.Z));

            var normal = solver.ContactNormals[contact];
            Assert.Equal(cpu.Normal[contact].X, normal.X, 4);
            Assert.Equal(cpu.Normal[contact].Y, normal.Y, 4);
            Assert.Equal(cpu.SeparationM[contact * 2], normal.Z, 4);
            var points = solver.ContactPoints[contact];
            Assert.Equal(cpu.PointM[contact * 2].X, points.X, 4);
            Assert.Equal(cpu.PointM[contact * 2].Y, points.Y, 4);
            if (cpu.Points[contact] < 2) continue;

            Assert.Equal(cpu.SeparationM[contact * 2 + 1], normal.W, 4);
            Assert.Equal(cpu.PointM[contact * 2 + 1].X, points.Z, 4);
            Assert.Equal(cpu.PointM[contact * 2 + 1].Y, points.W, 4);
        }
    }

    /// <summary>
    /// <c>SOL-37</c>, <c>SOL-37a</c>: <b>where frozen bodies are met, the device finds the CPU's contacts pair for pair and in
    /// its order</b> — a frozen row woken whole from both ends, by a car below its indices and one above, with the wall the
    /// row's far end leans on, and nothing between two frozen cars nobody met.
    /// </summary>
    [Fact]
    public void WhereFrozenBodiesAreMetTheDeviceFindsTheCpusContacts()
    {
        var world = new PhysicsWorld(Config);
        var (lengthM, widthM) = (Config.Car.LengthM, Config.Car.WidthM);
        var below = world.AddNominalCar(new Vector2(2f * lengthM, -widthM), 0f);
        world.AddStaticBox(new Vector2(-0.5f * lengthM - 0.5f, 0f), new Vector2(1f, 4f), 0f);
        for (var car = 0; car < 6; car++) world.AddNominalCar(new Vector2(lengthM * car, 0f), 0f);
        world.AddNominalCar(new Vector2(0f, 50f), 0f);
        world.AddNominalCar(new Vector2(lengthM, 50f), 0f);
        for (var step = 0; step <= Config.SolverRestTicks; step++) world.Step(Config.TickSeconds);

        world.ApplyCentralImpulse(below, new Vector2(0f, Config.Car.MassKg * 0.5f));
        var above = world.AddNominalCar(new Vector2(5.5f * lengthM, widthM), 0f);
        world.ApplyCentralImpulse(above, new Vector2(Config.Car.MassKg * 0.5f, 0f));
        using var solver = new DeviceSolver(device.Vk, Config, world.Table);
        solver.Step(world.Table, Config.TickSeconds, readBack: true);
        world.Step(Config.TickSeconds);

        var cpu = world.Contacts;
        var expected = new List<(int, int)>();
        for (var contact = 0; contact < cpu.Count; contact++) expected.Add((cpu.First[contact], cpu.Second[contact]));
        var found = new List<(int, int)>();
        foreach (var contact in solver.Contacts) found.Add(((int)contact.X, (int)contact.Y));
        Assert.Equal(expected, found);
    }

    /// <summary>
    /// <b>Where no two contacts share a moving body the order they are solved in changes nothing</b>, so the coloured
    /// solve lands every body where the CPU's sequential one does, to a hundredth of a millimetre and a thousandth of a
    /// metre a second.
    /// </summary>
    [Fact]
    public void ContactsThatShareNoBodyLandWhereTheCpusDo()
    {
        var world = new PhysicsWorld(Config);
        for (var pair = 0; pair < 40; pair++)
        {
            var at = new Vector2(pair * 20f, 0f);
            var left = world.AddCar(at, 0.3f, new Vector2(2.0f, 0.75f), 0.15f, 1200f);
            var right = world.AddCar(at + new Vector2(4.0f, 0.4f), 2.9f, new Vector2(2.0f, 0.75f), 0.15f, 1200f);
            world.ApplyCentralImpulse(left, new Vector2(8000f, 500f));
            world.ApplyCentralImpulse(right, new Vector2(-7000f, 0f));
        }

        world.Step(Config.TickSeconds);
        using var solver = new DeviceSolver(device.Vk, Config, world.Table);
        solver.Step(world.Table, Config.TickSeconds, readBack: true);
        world.Step(Config.TickSeconds);

        var table = world.Table;
        Assert.True(world.Contacts.Count >= 40);
        foreach (var body in table.Moving)
        {
            var pose = solver.Pose[body];
            Assert.Equal(table.PositionM[body].X, pose.X, 5);
            Assert.Equal(table.PositionM[body].Y, pose.Y, 5);
            var motion = solver.Motion[body];
            Assert.Equal(table.VelocityMps[body].X, motion.X, 3);
            Assert.Equal(table.VelocityMps[body].Y, motion.Y, 3);
        }
    }

    /// <summary>
    /// <b>Solved in the CPU's own order, a crowded yard lands where the CPU's does</b> — every body within a tenth of a
    /// millimetre — so what the colours change is the order and nothing in the arithmetic.
    /// </summary>
    [Fact]
    public void InTheCpusOrderACrowdedYardLandsWhereTheCpusDoes()
    {
        var world = Yard(7, cars: 80, walkers: 120, sideM: 50f);
        world.Step(Config.TickSeconds);
        using var solver = new DeviceSolver(device.Vk, Config, world.Table) { SolveInContactOrder = true };
        solver.Step(world.Table, Config.TickSeconds, readBack: true);
        world.Step(Config.TickSeconds);

        var table = world.Table;
        foreach (var body in table.Moving)
        {
            var apartM = Vector2.Distance(table.PositionM[body], new Vector2(solver.Pose[body].X, solver.Pose[body].Y));
            Assert.True(apartM < 1e-4f, $"body {body} landed {apartM * 1000f:F3} mm from the CPU's");
        }
    }

    /// <summary>The colouring leaves nothing in a crowded yard for the one thread to run.</summary>
    [Fact]
    public void ACrowdedYardIsColouredWhole()
    {
        var world = Yard(7, cars: 80, walkers: 120, sideM: 50f);
        world.Step(Config.TickSeconds);
        using var solver = new DeviceSolver(device.Vk, Config, world.Table);
        solver.Step(world.Table, Config.TickSeconds, readBack: true);

        Assert.Equal(0, solver.LeftOver);
        Assert.InRange(solver.Colours, 1, 24);
    }
}
