using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The bodies' phase on the device</b> (<see cref="DeviceBodyPhase"/>), asked the town's own question: every tick of
/// the suite's city, the bodies as the town's step finds them and the impulses its tick wrote handed over, and what the
/// two phases did held to each other.
/// </summary>
/// <remarks>
/// <b>Two towns, because neither asks everything</b>: on <see cref="Towns.City"/> every car tours from its first tick and
/// nobody walks; on <see cref="Towns.Built"/> people walk and every car stands in a car park for the first seconds.
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceBodyPhaseTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Five seconds of a town: cars pulling away, braking into queues and turning, or walkers leaving their doors.</summary>
    const int Ticks = 300;

    /// <summary>
    /// <b>The tick's impulses land on the device's bodies as on the town's, to the bit</b> — every driven car's wheels at
    /// their points, a parked car's not at all, every walker's at its centre — since the device makes the same products
    /// and sums in the same order.
    /// </summary>
    [Theory]
    [InlineData(nameof(Towns.City))]
    [InlineData(nameof(Towns.Built))]
    public void TheTicksImpulsesLandAsOnTheTownToTheBit(string town)
    {
        var differ = new List<string>();
        var asked = 0;
        Shadow(
            town == nameof(Towns.Built) ? Towns.Built : Towns.Of(Towns.City),
            solveInContactOrder: false,
            beforeTheStep: (DeviceBodyPhase phase, in BodyTable table) =>
            {
                for (var body = 0; body < table.Count; body++)
                {
                    asked++;
                    var why = phase.Applied(table, body);
                    if (why != null) differ.Add(why);
                }
            },
            afterTheStep: null);

        Assert.True(differ.Count == 0, $"{differ.Count} of {asked} bodies differ: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>
    /// <b>Every car the device reads back off its body is where the town's step left it</b>, within the solver's own
    /// agreement when both solve in contact order — where it is, how it moves, turns and points — and settling onto its
    /// springs as the town's does.
    /// </summary>
    [Fact]
    public void EveryCarIsReadBackWhereTheTownsStepLeftIt()
    {
        var differ = new List<string>();
        var asked = 0;
        Shadow(
            Towns.Of(Towns.City),
            solveInContactOrder: true,
            beforeTheStep: null,
            afterTheStep: (phase, town, tick) =>
            {
                for (var car = 0; car < town.Cars.Count; car++)
                {
                    asked++;
                    var why = phase.Disagreement(town, car);
                    if (why != null) differ.Add($"tick {tick} car {car}: {why}");
                }
            });

        Assert.True(differ.Count == 0, $"{differ.Count} of {asked} car-ticks differ: {string.Join("; ", differ.Take(6))}");
    }

    delegate void BeforeTheStep(DeviceBodyPhase phase, in BodyTable table);

    void Shadow(
        CityPlan plan, bool solveInContactOrder, BeforeTheStep? beforeTheStep, Action<DeviceBodyPhase, TownWorld, long>? afterTheStep)
    {
        using var world = new TownWorld(plan, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var cars = world.Cars;
        var physics = world.PhysicsForInstruments;

        var builds = Enumerable.Range(0, CarCatalog.Shared.SheetCount).Select(variant => cars.BuildOfVariant(variant)).ToArray();
        using var tyres = new DeviceTyres(device.Vk, Config, world.WheelGrounds, builds, cars.Capacity);
        using var solver = new DeviceSolver(device.Vk, Config, physics.Table) { SolveInContactOrder = solveInContactOrder };
        using var phase = new DeviceBodyPhase(device.Vk, Config, solver, tyres, world.People.Capacity + (2 * cars.Capacity));

        world.BeforeTheImpulses = town =>
        {
            phase.Lay(town, Config.TickSeconds, everyWheel: true);
            phase.Step(town.Cars.Count, Config.TickSeconds, readBack: true);
        };
        if (beforeTheStep != null) world.BeforeTheSolverSteps = before => beforeTheStep(phase, before.Table);

        for (var at = 0; at < Ticks; at++)
        {
            loop.Advance();
            afterTheStep?.Invoke(phase, world, loop.Tick);
        }
    }
}
