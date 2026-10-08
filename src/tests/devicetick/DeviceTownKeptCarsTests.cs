using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A car the town does not read after a tick is left on the device</b> (<see cref="DeviceTown.ReadsEveryCarBack"/> off):
/// its reading, command and tyres handed back only where its decision is due, its tick asks something of the town, or it
/// leaves the device's tick — and the town drives as one handed every car back with every tick.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P2)]
public class DeviceTownKeptCarsTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>Ten seconds: every car's decision due a hundred times, and every rebuild's claims cleared on the device.</summary>
    const int Ticks = 600;

    /// <summary>
    /// <b>Every tick, every car and walker stands, moves and faces to the bit as in the town handed every car back, and holds
    /// the same hold</b> — over a run in which cars were left on the device.
    /// </summary>
    [Fact]
    public void ACarLeftOnTheDeviceLeavesTheTownAsOneHandedEveryCarBack()
    {
        var config = SimConfig.Shipped();
        using var everyCar = new TownWorld(Towns.Built, config);
        using var kept = new TownWorld(Towns.Built, config);
        var everyLoop = new SimLoop<TownWorld>(everyCar, config);
        var keptLoop = new SimLoop<TownWorld>(kept, config);
        var warm = (int)(config.Building.DwellMaxS * config.Sim.TickRateHz) + 120;
        everyLoop.Advance(warm);
        keptLoop.Advance(warm);
        using var everyDevice = new DeviceTown(device.Vk, config, everyCar) { ReadsEveryCarBack = true };
        using var keptDevice = new DeviceTown(device.Vk, config, kept);
        everyCar.Device = everyDevice;
        kept.Device = keptDevice;

        var unlike = new List<string>();
        for (var tick = 0; tick < Ticks && unlike.Count == 0; tick++)
        {
            everyLoop.Advance();
            keptLoop.Advance();
            DeviceTownRunOnTests.Compare(everyCar, kept, tick, unlike);
        }

        everyCar.Device = kept.Device = null;
        Assert.True(keptDevice.CarsLeftOnTheDevice > 0, "no car was left on the device");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }
}
