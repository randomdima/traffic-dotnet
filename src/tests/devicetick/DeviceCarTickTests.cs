using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Terrain;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A following car's whole tick on the device</b> (<see cref="DeviceCarTick"/>), asked the town's own question: every
/// tick of the suite's city, each car the device takes laid as the town's tick will read it, and what the two ticks did
/// held to each other.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceCarTickTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Ten seconds of the suite's city: queues, junctions, lights, and lines run onto their next lane.</summary>
    const int Ticks = 600;

    /// <summary>
    /// <b>Every following car's tick on the device does what the town's tick of it did</b> — its line read and shifted
    /// on, whether it lost the line or asked for more of it, what it was told and the junction ahead, its command, and the
    /// tyres that delivered it.
    /// </summary>
    /// <remarks>
    /// Ten seconds of the suite's city have 36 000 such ticks, and no line among them runs short onto a lane nothing
    /// leaves, so the road running out under a line is not asked here.
    /// </remarks>
    [Fact]
    public void EveryFollowingCarTicksAsTheTownTicksIt()
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
                count = 0;
                for (var car = 0; car < cars.Count; car++)
                {
                    if (!DeviceCarTick.Takes(town, car)) continue;

                    tick.Lay(count, town, car, cars.Variant[car]);

                    // Every road of the city is the same ground, so a coefficient left as the last tick's is the one
                    // read afresh; nothing but a reading can answer a car on its line here.
                    if (cars.Line[car].LaneCount > 0) tyres.Cars[count] = tyres.Cars[count] with { GroundCoefficient = float.NaN };
                    taken[count++] = car;
                }

                tick.Step(count);
                return;
            }

            for (var slot = 0; slot < count; slot++)
            {
                asked++;
                var why = tick.Disagreement(slot, town, taken[slot]);
                if (why != null) differ.Add($"tick {loop.Tick} car {taken[slot]}: {why}");
            }
        };

        for (var at = 0; at < Ticks; at++) loop.Advance();

        Assert.True(differ.Count == 0, $"{differ.Count} of {asked} ticks differ: {string.Join("; ", differ.Take(6))}");
    }
}
