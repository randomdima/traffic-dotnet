using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.Tests.World;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A car the town does not read after a tick is left on the device</b> (<see cref="DeviceTown.ReadsEveryCarBack"/> off):
/// its reading, command and tyres handed back only where its decision is due, its tick asks something of the town, or it
/// leaves the device's tick — and the town drives as one handed every car back with every tick, and a watch reads every car
/// as that town holds it.
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
            TownsInStep.Compare(everyCar, kept, tick, unlike);
        }

        everyCar.Device = kept.Device = null;
        Assert.True(keptDevice.CarsLeftOnTheDevice > 0, "no car was left on the device");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }

    /// <summary>
    /// <b>Every tick, a watch reads every car's grant left to the bit as the town handed every car back holds it</b>
    /// (<see cref="TownWorld.GrantLeftM"/>) — over a run in which cars were left on the device, and one of them wrecked
    /// where the contacts would wreck it, its grant gone with its action before the device hands it back.
    /// </summary>
    [Fact]
    public void AWatchReadsTheGrantLeftOfACarLeftOnTheDeviceAsTheTownHandedEveryCarBack()
    {
        var config = SimConfig.Shipped();
        using var everyCar = new TownWorld(Towns.Built, config);
        using var watched = new TownWorld(Towns.Built, config) { WatchesEveryAgent = true };
        var everyLoop = new SimLoop<TownWorld>(everyCar, config);
        var watchedLoop = new SimLoop<TownWorld>(watched, config);
        var warm = (int)(config.Building.DwellMaxS * config.Sim.TickRateHz) + 120;
        everyLoop.Advance(warm);
        watchedLoop.Advance(warm);
        using var everyDevice = new DeviceTown(device.Vk, config, everyCar) { ReadsEveryCarBack = true };
        using var watchedDevice = new DeviceTown(device.Vk, config, watched);
        everyCar.Device = everyDevice;
        watched.Device = watchedDevice;

        var unlike = new List<string>();
        var wrecked = -1;
        for (var tick = 0; tick < Ticks && unlike.Count == 0; tick++)
        {
            everyLoop.Advance();
            watchedLoop.Advance();
            if (tick == Ticks / 2)
            {
                wrecked = LeftOnTheDeviceWithAGrant(watched);
                var tag = new BodyTag(BodyKind.Car, wrecked);
                everyCar.Apply(tag, DamageOutcome.Broken);
                watched.Apply(tag, DamageOutcome.Broken);
            }

            for (var car = 0; car < everyCar.Cars.Count; car++)
            {
                var handedM = everyCar.Cars.GrantLeftM(car);
                var readM = watched.GrantLeftM(car);
                if (BitConverter.SingleToInt32Bits(handedM) != BitConverter.SingleToInt32Bits(readM))
                {
                    unlike.Add($"tick {tick} car {car}: handed back {handedM}, read {readM}");
                }
            }
        }

        everyCar.Device = watched.Device = null;
        Assert.True(watchedDevice.CarsLeftOnTheDevice > 0, "no car was left on the device");
        Assert.True(wrecked >= 0, "no car was wrecked");
        Assert.True(unlike.Count == 0, $"{unlike.Count} grants differ: {string.Join("; ", unlike.Take(6))}");
    }

    /// <summary>A driven car whose grant left the town's own copy no longer holds: one its last tick left on the device.</summary>
    static int LeftOnTheDeviceWithAGrant(TownWorld town)
    {
        var cars = town.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            var townsM = cars.GrantLeftM(car);
            if (cars.Driven[car] && float.IsFinite(townsM) && townsM != town.GrantLeftM(car)) return car;
        }

        Assert.Fail("no driven car was left on the device with a grant the town does not hold");
        return -1;
    }
}
