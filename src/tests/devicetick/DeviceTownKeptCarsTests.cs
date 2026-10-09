using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.CityGen.Traced;
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
    /// <b>A car moving across with no step asked is left on the device as a following car is</b> — read only where its
    /// decision is due — and the town drives as one handed every car back with every tick, every car doing the same and
    /// on the same step: on a street of two lanes east and one west into a tee (<see cref="TracedPlanTests.TwoLanesIntoATee"/>), a car on the
    /// kerb lane sent up the arm only the inner lane turns onto, with a wreck on the inner lane just ahead of it standing
    /// on the ground its step would cover.
    /// </summary>
    [Fact]
    public void ASwitchingCarWithNoStepLeftOnTheDeviceLeavesTheTownAsOneHandedEveryCarBack()
    {
        var config = SimConfig.Shipped();
        using var everyCar = new TownWorld(TracedPlanTests.TwoLanesIntoATee(againstWest: 1), config);
        using var kept = new TownWorld(TracedPlanTests.TwoLanesIntoATee(againstWest: 1), config);
        var everyLoop = new SimLoop<TownWorld>(everyCar, config);
        var keptLoop = new SimLoop<TownWorld>(kept, config);
        var car = StageASwitchHeldBeside(everyCar);
        Assert.Equal(car, StageASwitchHeldBeside(kept));
        using var everyDevice = new DeviceTown(device.Vk, config, everyCar) { ReadsEveryCarBack = true };
        using var keptDevice = new DeviceTown(device.Vk, config, kept);
        everyCar.Device = everyDevice;
        kept.Device = keptDevice;

        var unlike = new List<string>();
        var leftSwitching = 0;
        for (var tick = 0; tick < SwitchTicks && unlike.Count == 0; tick++)
        {
            everyLoop.Advance();
            keptLoop.Advance();
            TownsInStep.Compare(everyCar, kept, tick, unlike);
            for (var other = 0; other < kept.Cars.Count; other++)
            {
                var (action, pass) = (everyCar.Cars.Action[other], everyCar.Cars.Pass[other]);
                if (action != kept.Cars.Action[other] || pass != kept.Cars.Pass[other])
                {
                    unlike.Add($"tick {tick} car {other}: {action} on {pass} against {kept.Cars.Action[other]} on {kept.Cars.Pass[other]}");
                }
            }

            if (kept.Cars.Action[car] == CarAction.Switch && !kept.Cars.Pass[car].Any && !keptDevice.CarTick.TownHolds(car)) leftSwitching++;
        }

        everyCar.Device = kept.Device = null;
        Assert.True(leftSwitching > 0, $"car {car} was never left on the device moving across with no step");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
    }

    /// <summary>
    /// The car the tee stands on its kerb lane running east, sent up the arm only the inner lane turns onto, and a wreck put
    /// down on the inner lane <see cref="WreckBesideAheadM"/> on from it.
    /// </summary>
    static int StageASwitchHeldBeside(TownWorld world)
    {
        var car = LaneSwitchInATownTests.TheCarOnTheStreet(world);
        var wreck = LaneSwitchInATownTests.TheCarOnTheStreet(world, eastward: false);
        var roads = world.Roads;
        var kerb = roads.NearestStreetLane(world.Cars.PositionM[car], out var alongM);
        var inner = roads.LaneBeside(kerb, inward: true);
        var at = Spline.SampleAt(roads.ArcsOf(inner), alongM + WreckBesideAheadM);
        OvertakeInATownTests.PutDown(world, wreck, at.PositionM, at.HeadingRad);
        Assert.True(world.OrderCar(car, LaneSwitchInATownTests.UpTheArmM), $"car {car} took no order up the arm");
        return car;
    }

    /// <summary>How far on from the car the wreck beside it stands: inside the ground of a step from rest.</summary>
    const float WreckBesideAheadM = 15f;

    /// <summary>Thirty seconds: up to the wreck, the looks while it holds the step's ground, and the step past it.</summary>
    const int SwitchTicks = 1_800;

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
