using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A following car's whole tick on the device</b> (<see cref="DeviceCarTick"/>), asked the town's own question: every
/// tick of the suite's city, each car the device takes handed over, and what the two ticks did held to each other.
/// </summary>
/// <remarks>
/// Ten seconds of the suite's city have 36 000 such ticks, and no line among them runs short onto a lane nothing
/// leaves, so the road running out under a line is not asked here.
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceCarTickTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Ten seconds of the suite's city: queues, junctions, lights, and lines run onto their next lane.</summary>
    const int Ticks = 600;

    /// <summary>
    /// <b>Every following car's tick on the device does what the town's tick of it did</b>, laid whole as the town's tick
    /// reads it — its line read and shifted on, whether it lost the line or asked for more of it, what it was told and the
    /// junction ahead, its command, and the tyres that delivered it.
    /// </summary>
    [Fact]
    public void EveryFollowingCarTicksAsTheTownTicksIt()
    {
        var (differ, asked) = Shadow(resident: false);

        Assert.True(differ.Count == 0, $"{differ.Count} of {asked} ticks differ: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>
    /// <b>A car the device keeps ticks as the town ticks it</b>: told only what the town changed outside the tick — every
    /// pose, what it is told and was granted, a line a decision laid, a reading a rebuild set — and otherwise driving on
    /// what its own last tick left.
    /// </summary>
    [Fact]
    public void ACarTheDeviceKeepsTicksAsTheTownTicksIt()
    {
        var (differ, asked) = Shadow(resident: true);

        Assert.True(differ.Count == 0, $"{differ.Count} of {asked} ticks differ: {string.Join("; ", differ.Take(6))}");
    }

    (List<string> Differ, int Asked) Shadow(bool resident)
    {
        var plan = Towns.Of(Towns.City);
        using var world = new TownWorld(plan, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var cars = world.Cars;
        var claims = world.Occupancy;

        var shapes = new GroundShapes(plan.Paving(Config), Config);
        using var ground = new DeviceGround(device.Vk, shapes.Table);
        var builds = Enumerable.Range(0, CarCatalog.Shared.SheetCount).Select(variant => cars.BuildOfVariant(variant)).ToArray();
        using var tyres = new DeviceTyres(device.Vk, Config, ground, world.Terrain.EffectTable, builds, cars.Capacity);
        using var rows = device.Sized<DeviceClaims.ClaimRow>(claims.Capacity);
        using var plannedRuns = device.Sized<UInt2>(claims.WayCount);
        using var plannedOrder = device.Sized<uint>(claims.Capacity);
        using var tick = new DeviceCarTick(
            device.Vk, Config, tyres, ground, world.Roads, cars.LineArcsOf(0).Length, world.Ways.OfRoadConnector(0),
            (rows, plannedRuns, plannedOrder));

        var taken = new int[cars.Capacity];
        var count = 0;
        var listed = new int[claims.Capacity];
        var differ = new List<string>();
        var asked = 0;
        loop.WatchingTheAgents = (town, stage) =>
        {
            if (stage == AgentsStage.Due)
            {
                HostClaims.Lay(town.Occupancy, rows.Span<DeviceClaims.ClaimRow>(), plannedRuns.Span<UInt2>(), plannedOrder.Span<uint>(), listed);
                count = tick.Prepare(town, taken, resident);

                // Every road of the city is the same ground, so a coefficient left as the last tick's is the one read
                // afresh; nothing but a reading can answer a car on its line here.
                foreach (var car in taken.AsSpan(0, count))
                {
                    if (cars.Line[car].LaneCount > 0) tyres.Cars[car] = tyres.Cars[car] with { GroundCoefficient = float.NaN };
                }

                tick.Step(count, cars.Count);
                return;
            }

            foreach (var car in taken.AsSpan(0, count))
            {
                asked++;
                var why = tick.Disagreement(town, car);
                if (why != null) differ.Add($"tick {loop.Tick} car {car}: {why}");
            }

            tick.Settle(town, taken.AsSpan(0, count));
        };

        for (var at = 0; at < Ticks; at++) loop.Advance();

        return (differ, asked);
    }
}
