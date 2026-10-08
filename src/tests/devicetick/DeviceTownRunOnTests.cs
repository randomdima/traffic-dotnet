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
/// <b>A claims rebuild that runs on while the town goes on</b> (<see cref="DeviceTown.RebuildsRunOn"/>) leaves the town as
/// one waited for: its answers taken up after the tick was laid, into the numbers of a laying put aside and under the
/// walkers that tick wrote whole, change nothing the town or the device does.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P2)]
public class DeviceTownRunOnTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>Ten seconds: a couple of hundred rebuilds, every one of them taken up after its tick where it ran on.</summary>
    const int Ticks = 600;

    /// <summary>
    /// <b>Every tick, every car and walker stands, moves and faces to the bit as in the town whose rebuilds were waited for,
    /// and holds the hold the same layer numbered for it</b> — over a run in which rebuilds ran on.
    /// </summary>
    [Fact]
    public void ARebuildThatRunsOnLeavesTheTownAsOneWaitedFor()
    {
        var config = SimConfig.Shipped();
        using var waitedFor = new TownWorld(Towns.Built, config);
        using var ranOn = new TownWorld(Towns.Built, config);
        var waitedLoop = new SimLoop<TownWorld>(waitedFor, config);
        var ranOnLoop = new SimLoop<TownWorld>(ranOn, config);
        var warm = (int)(config.Building.DwellMaxS * config.Sim.TickRateHz) + 120;
        waitedLoop.Advance(warm);
        ranOnLoop.Advance(warm);
        using var waitedDevice = new DeviceTown(device.Vk, config, waitedFor) { RebuildsRunOn = false };
        using var ranOnDevice = new DeviceTown(device.Vk, config, ranOn);
        waitedFor.Device = waitedDevice;
        ranOn.Device = ranOnDevice;

        var unlike = new List<string>();
        for (var tick = 0; tick < Ticks && unlike.Count == 0; tick++)
        {
            waitedLoop.Advance();
            ranOnLoop.Advance();
            TownsInStep.Compare(waitedFor, ranOn, tick, unlike);
        }

        waitedFor.Device = ranOn.Device = null;
        Assert.True(ranOnDevice.Claims.Rebuild.RanOn > 0, "no rebuild ran on");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }
}
