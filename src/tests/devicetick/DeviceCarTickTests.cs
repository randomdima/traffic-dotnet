using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.Tests.World;
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
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var (differ, asked) = Shadow(world, new SimLoop<TownWorld>(world, Config), resident: false, Ticks);

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
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        var (differ, asked) = Shadow(world, new SimLoop<TownWorld>(world, Config), resident: true, Ticks);

        Assert.True(differ.Count == 0, $"{differ.Count} of {asked} ticks differ: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>
    /// <b>An overtaking car the device keeps ticks as the town ticks it</b> (CAR-46): coming up to a wreck in its lane and
    /// waiting towards where its step out begins, driving its pass over the lane beside, and following again once it is
    /// past — each held to the town's own tick of it.
    /// </summary>
    [Fact]
    public void ACarGettingPastAWreckTicksAsTheTownTicksIt()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(OvertakeInATownTests.WarmUpTicks);
        var (driver, _, _, _, _) = OvertakeInATownTests.StageAWreckAhead(world, alsoInTheLaneBeside: false);

        var (waited, drove, made) = (false, false, false);
        var (differ, asked) = Shadow(
            world, loop, resident: true, OvertakeInATownTests.WatchedTicks, (tick, taken) =>
            {
                if (!taken.Contains(driver)) return;

                var overtaking = world.Cars.Action[driver] == CarAction.Overtake;
                waited |= overtaking && !world.Cars.Pass[driver].Begun;
                drove |= overtaking && world.Cars.Pass[driver].Begun;
                made |= (tick.TicksAfter[driver].Flags & DeviceCarTick.TickRow.PassMade) != 0;
            }, onlyCar: driver);

        Assert.True(waited && drove && made, $"the device ticked car {driver} waiting {waited}, on its pass {drove}, past it {made}");
        Assert.True(differ.Count == 0, $"{differ.Count} of {asked} ticks differ: {string.Join("; ", differ.Take(6))}");
    }

    /// <param name="watch">Handed the tick and the cars it took once each tick has been held to the town's, before either settles.</param>
    /// <param name="onlyCar">The one car whose ticks are held to the town's, or every car the device takes.</param>
    (List<string> Differ, int Asked) Shadow(
        TownWorld world, SimLoop<TownWorld> loop, bool resident, int ticks, Action<DeviceCarTick, int[]>? watch = null,
        int onlyCar = -1)
    {
        var cars = world.Cars;
        var claims = world.Occupancy;

        var builds = Enumerable.Range(0, CarCatalog.Shared.SheetCount).Select(variant => cars.BuildOfVariant(variant)).ToArray();
        using var tyres = new DeviceTyres(device.Vk, Config, world.WheelGrounds, builds, cars.Capacity);
        using var rows = device.Sized<DeviceClaims.ClaimRow>(claims.Capacity);
        using var plannedRuns = device.Sized<UInt2>(claims.WayCount);
        using var plannedOrder = device.Sized<uint>(claims.Capacity);
        using var tick = new DeviceCarTick(
            device.Vk, Config, tyres, world.Roads, cars.LineArcsOf(0).Length, world.Ways.OfRoadConnector(0),
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
                tick.Step(count, cars.Count);
                return;
            }

            foreach (var car in taken.AsSpan(0, count))
            {
                if (onlyCar >= 0 && car != onlyCar) continue;

                asked++;
                var why = tick.Disagreement(town, car);
                if (why != null) differ.Add($"tick {loop.Tick} car {car}: {why}");
            }

            watch?.Invoke(tick, taken[..count]);
            tick.Settle(town, taken.AsSpan(0, count));
        };

        for (var at = 0; at < ticks; at++) loop.Advance();

        return (differ, asked);
    }
}
