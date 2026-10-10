using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.CityGen.Traced;
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
    /// <b>A car whose decision the device finds itself, touched by the town before the device found the turn it owes, is
    /// decided on that turn once</b> (<see cref="DeviceTown.DecidesItself"/>): every third tick a fifth of the cars marked
    /// touched, as the town marks a car it set anything of — the town as one that decides every car itself, and as many
    /// decisions taken.
    /// </summary>
    [Fact]
    public void ACarTouchedOnItsTurnIsDecidedOnItOnce()
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

        var (itselfFrom, deviceFrom) = (itself.CarDecisionsTaken, onTheDevice.CarDecisionsTaken);
        var unlike = new List<string>();
        for (var tick = 0; tick < Ticks && unlike.Count == 0; tick++)
        {
            itselfLoop.Advance();
            deviceLoop.Advance();
            TownsInStep.Compare(itself, onTheDevice, tick, unlike);
            if (tick % 3 != 0) continue;

            for (var car = tick % 5; car < onTheDevice.Cars.Count; car += 5) onTheDevice.TouchTheCar(car);
        }

        itself.Device = onTheDevice.Device = null;
        Assert.True(decidingDevice.DecisionsFound > 0, "no car's decision was found on the device");
        Assert.True(unlike.Count == 0, $"{unlike.Count} agents differ: {string.Join("; ", unlike.Take(6))}");
        Assert.Equal(itself.CarDecisionsTaken - itselfFrom, onTheDevice.CarDecisionsTaken - deviceFrom);
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

    /// <summary>
    /// <b>Every tick, every car stands, moves and faces to the bit as in the town that looks for room itself, holds the same
    /// hold, does the same on the same step, and means to move across the same way, as far across and looking as soon</b>
    /// — over a run in which the device took a switching car's looks (<see cref="DeviceTown.LooksOnTheDevice"/>): one held up
    /// by a wreck on its lane, a second on the lane beside holding its step's ground, taken away once the device has looked
    /// and found no room, so that its next look finds room and is the town's.
    /// </summary>
    [Fact]
    public void ASwitchingCarsLookOnTheDeviceLeavesTheTownAsOneThatLooksItself()
    {
        var config = SimConfig.Shipped();
        using var itself = new TownWorld(TracedPlanTests.TwoLanesIntoATee(againstWest: 1), config);
        using var onTheDevice = new TownWorld(TracedPlanTests.TwoLanesIntoATee(againstWest: 1), config);
        var itselfLoop = new SimLoop<TownWorld>(itself, config);
        var deviceLoop = new SimLoop<TownWorld>(onTheDevice, config);
        var (car, beside) = LaneSwitchInATownTests.StageAWreckOnEitherLane(itself);
        Assert.Equal((car, beside), LaneSwitchInATownTests.StageAWreckOnEitherLane(onTheDevice));
        using var itselfDevice = new DeviceTown(device.Vk, config, itself) { LooksOnTheDevice = false };
        using var lookingDevice = new DeviceTown(device.Vk, config, onTheDevice);
        itself.Device = itselfDevice;
        onTheDevice.Device = lookingDevice;

        var unlike = new List<string>();
        var held = true;
        for (var tick = 0; tick < SwitchTicks && unlike.Count == 0; tick++)
        {
            if (held && lookingDevice.LooksTaken.Looked > 0)
            {
                held = false;
                OvertakeInATownTests.PutDown(itself, beside, OvertakeInATownTests.FarOffTheTownM, 0f);
                OvertakeInATownTests.PutDown(onTheDevice, beside, OvertakeInATownTests.FarOffTheTownM, 0f);
            }

            itselfLoop.Advance();
            deviceLoop.Advance();
            TownsInStep.Compare(itself, onTheDevice, tick, unlike);
            for (var other = 0; other < itself.Cars.Count; other++)
            {
                var (one, two) = (itself.Switching, onTheDevice.Switching);
                var mine = (itself.Cars.Action[other], itself.Cars.Pass[other], one.Inward(other), one.AsideM(other), one.LooksInS(other));
                var theirs = (onTheDevice.Cars.Action[other], onTheDevice.Cars.Pass[other], two.Inward(other), two.AsideM(other), two.LooksInS(other));
                if (!mine.Equals(theirs)) unlike.Add($"tick {tick} car {other}: {mine} against {theirs}");
            }
        }

        itself.Device = onTheDevice.Device = null;
        Assert.True(lookingDevice.LooksTaken.Looked > 0, $"car {car} never looked for room on the device ({lookingDevice.LooksTaken})");
        Assert.True(onTheDevice.Switching.Made > 0, $"car {car} never moved across once the wreck beside it was taken away");
        Assert.True(unlike.Count == 0, $"{unlike.Count} cars differ: {string.Join("; ", unlike.Take(6))}");
    }

    /// <summary>
    /// <b>A switching car looking on the device for longer than the layer keeps a way nobody asks about is held to the town
    /// that looks itself</b>, as <see cref="ASwitchingCarsLookOnTheDeviceLeavesTheTownAsOneThatLooksItself"/> holds it — the
    /// wreck beside it never taken away: every way its looks read is marked asked, as the town's own look marks it, so the
    /// readers' layer goes on holding the ground it looks at; and its leg's clock, run with its looks, runs out as the town's
    /// does, on a patience short enough to run out inside the run.
    /// </summary>
    [Fact]
    public void ASwitchingCarLookingOnTheDeviceKeepsTheGroundItLooksAtKnown()
    {
        var config = new SimConfig
        {
            Patience = new DrivePatienceFigures { BlockedRoadInLightCycles = HeldPatienceS / SimConfig.Shipped().Signals.CycleS },
        };
        using var itself = new TownWorld(TracedPlanTests.TwoLanesIntoATee(againstWest: 1), config);
        using var onTheDevice = new TownWorld(TracedPlanTests.TwoLanesIntoATee(againstWest: 1), config);
        var itselfLoop = new SimLoop<TownWorld>(itself, config);
        var deviceLoop = new SimLoop<TownWorld>(onTheDevice, config);
        var (car, _) = LaneSwitchInATownTests.StageAWreckOnEitherLane(itself);
        LaneSwitchInATownTests.StageAWreckOnEitherLane(onTheDevice);
        using var itselfDevice = new DeviceTown(device.Vk, config, itself) { LooksOnTheDevice = false };
        using var lookingDevice = new DeviceTown(device.Vk, config, onTheDevice);
        itself.Device = itselfDevice;
        onTheDevice.Device = lookingDevice;

        var unlike = new List<string>();
        for (var tick = 0; tick < SwitchTicks && unlike.Count == 0; tick++)
        {
            itselfLoop.Advance();
            deviceLoop.Advance();
            var (one, two) = (itself.Switching, onTheDevice.Switching);
            var mine = (itself.Cars.Action[car], one.Inward(car), one.AsideM(car), one.LooksInS(car), one.LastLookOf(car),
                itself.Cars.LineLaid[car], itself.Cars.RouteLaid[car]);
            var theirs = (onTheDevice.Cars.Action[car], two.Inward(car), two.AsideM(car), two.LooksInS(car), two.LastLookOf(car),
                onTheDevice.Cars.LineLaid[car], onTheDevice.Cars.RouteLaid[car]);
            if (!mine.Equals(theirs)) unlike.Add($"tick {tick} car {car}: {mine} against {theirs}");
        }

        itself.Device = onTheDevice.Device = null;
        Assert.True(unlike.Count == 0, $"{unlike.Count} looks differ: {string.Join("; ", unlike.Take(6))}");
        var looked = lookingDevice.LooksTaken.Looked;
        Assert.True(
            looked * config.Driving.LaneSwitchAskEveryS > LookedPastTheLayerS,
            $"car {car} looked {looked} times on the device, not past what the layer keeps of a way nobody asks about");
    }

    /// <summary>
    /// How long the readers' layer goes on holding a way after it was last asked about, with room: twenty claims rebuilds
    /// (<see cref="DeviceRebuild"/>'s WaysAskedForMirrors) at a rebuild every three ticks of sixty a second.
    /// </summary>
    const float LookedPastTheLayerS = 2f;

    /// <summary>A patience a car held behind the wrecks runs out of well inside the run.</summary>
    const float HeldPatienceS = 8f;

    /// <summary>
    /// <b>Every switching car's look the device takes is the town's own</b> (<see cref="TownWorld.TakesOnlyItsLook"/>) — how it
    /// came out and every figure it left, to the bit — read off what the device read it off, over the same staging.
    /// </summary>
    [Fact]
    public void EverySwitchingCarsLookTheDeviceTakesIsTheTowns()
    {
        var config = SimConfig.Shipped();
        using var world = new TownWorld(TracedPlanTests.TwoLanesIntoATee(againstWest: 1), config);
        var loop = new SimLoop<TownWorld>(world, config);
        var (car, beside) = LaneSwitchInATownTests.StageAWreckOnEitherLane(world);
        using var town = new DeviceTown(device.Vk, config, world) { ChecksTheDecisions = true };
        world.Device = town;
        var held = true;
        for (var tick = 0; tick < SwitchTicks; tick++)
        {
            if (held && town.LooksTaken.Looked > 0)
            {
                held = false;
                OvertakeInATownTests.PutDown(world, beside, OvertakeInATownTests.FarOffTheTownM, 0f);
            }

            loop.Advance();
        }

        world.Device = null;
        Assert.True(town.LooksTaken.Looked > 0, $"car {car} never looked for room on the device ({town.LooksTaken})");
        Assert.True(world.Switching.Made > 0, $"car {car} never moved across once the wreck beside it was taken away");
        Assert.True(town.DecisionsUnlike == 0, $"{town.DecisionsUnlike} verdicts unlike: {string.Join("; ", town.DecisionsUnlikeSeen)}");
        Assert.True(town.LooksApart == 0, $"{town.LooksApart} looks left a figure up to {town.LooksMostApartM} m apart on a straight street");
    }

    /// <summary>Thirty seconds: up to the wrecks, a look while the one beside holds the step's ground, and the step once it is gone.</summary>
    const int SwitchTicks = 1_800;
}
