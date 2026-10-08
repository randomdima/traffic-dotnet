using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.Tests.World;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A car whose decision put off a tick is its clock alone is decided on the device</b>
/// (<see cref="DeviceTown.DecidesTheCars"/>, <c>car_decide.glsl</c>): its clock run there, and only a decision that is more
/// handed back and taken by the town — which drives as one that decides every car itself.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P2)]
public class DeviceTownDecidesTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>Ten seconds: every car's decision due a hundred times, some of them a pass, a switch or a bay.</summary>
    const int Ticks = 600;

    /// <summary>
    /// <b>Every tick, every car and walker stands, moves and faces to the bit as in the town that decides every car itself,
    /// and holds the same hold</b> — over a run in which the device decided cars.
    /// </summary>
    [Fact]
    public void ACarDecidedOnTheDeviceLeavesTheTownAsOneThatDecidesItself()
    {
        var config = SimConfig.Shipped();
        using var itself = new TownWorld(Towns.Built, config);
        using var onTheDevice = new TownWorld(Towns.Built, config);
        var itselfLoop = new SimLoop<TownWorld>(itself, config);
        var deviceLoop = new SimLoop<TownWorld>(onTheDevice, config);
        var warm = (int)(config.Building.DwellMaxS * config.Sim.TickRateHz) + 120;
        itselfLoop.Advance(warm);
        deviceLoop.Advance(warm);
        using var itselfDevice = new DeviceTown(device.Vk, config, itself) { DecidesTheCars = false };
        using var decidingDevice = new DeviceTown(device.Vk, config, onTheDevice);
        itself.Device = itselfDevice;
        onTheDevice.Device = decidingDevice;

        var decidedFrom = onTheDevice.DecidedByTheClock;
        var unlike = new List<string>();
        for (var tick = 0; tick < Ticks && unlike.Count == 0; tick++)
        {
            itselfLoop.Advance();
            deviceLoop.Advance();
            TownsInStep.Compare(itself, onTheDevice, tick, unlike);
        }

        itself.Device = onTheDevice.Device = null;
        Assert.True(onTheDevice.DecidedByTheClock > decidedFrom, "no car was decided on the device");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }

    /// <summary>
    /// <b>Every tick, every car and walker stands, moves and faces to the bit as in the town that decides every walker
    /// itself, and holds the same hold</b> — over a run in which the device decided walkers.
    /// </summary>
    [Fact]
    public void AWalkerDecidedOnTheDeviceLeavesTheTownAsOneThatDecidesItself()
    {
        var config = SimConfig.Shipped();
        using var itself = new TownWorld(Towns.Built, config);
        using var onTheDevice = new TownWorld(Towns.Built, config);
        var itselfLoop = new SimLoop<TownWorld>(itself, config);
        var deviceLoop = new SimLoop<TownWorld>(onTheDevice, config);
        var warm = (int)(config.Building.DwellMaxS * config.Sim.TickRateHz) + 120;
        itselfLoop.Advance(warm);
        deviceLoop.Advance(warm);
        using var itselfDevice = new DeviceTown(device.Vk, config, itself) { DecidesTheWalkers = false };
        using var decidingDevice = new DeviceTown(device.Vk, config, onTheDevice);
        itself.Device = itselfDevice;
        onTheDevice.Device = decidingDevice;

        var decidedFrom = onTheDevice.WalkersDecidedByTheClock;
        var unlike = new List<string>();
        for (var tick = 0; tick < Ticks && unlike.Count == 0; tick++)
        {
            itselfLoop.Advance();
            deviceLoop.Advance();
            TownsInStep.Compare(itself, onTheDevice, tick, unlike);
        }

        itself.Device = onTheDevice.Device = null;
        Assert.True(onTheDevice.WalkersDecidedByTheClock > decidedFrom, "no walker was decided on the device");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }

    /// <summary>
    /// <b>Every decision the device asks finds what the town's own asking finds</b> (<see cref="TownWorld.TakesOnlyTheClock"/>,
    /// <see cref="TownWorld.WalkerTakesOnlyTheClock"/>)
    /// — its clock alone or more — read off what the device read it off.
    /// </summary>
    [Fact]
    public void EveryVerdictTheDeviceReachesIsTheTowns()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(Towns.Built, config);
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Advance((int)(config.Building.DwellMaxS * config.Sim.TickRateHz) + 120);
        using var town = new DeviceTown(device.Vk, config, world) { ChecksTheDecisions = true };
        world.Device = town;
        loop.Advance(Ticks);
        world.Device = null;

        Assert.True(town.DecisionsChecked > 0, "no verdict was checked");
        Assert.True(town.DecisionsUnlike == 0, $"{town.DecisionsUnlike} verdicts unlike: {string.Join("; ", town.DecisionsUnlikeSeen)}");
    }
}
