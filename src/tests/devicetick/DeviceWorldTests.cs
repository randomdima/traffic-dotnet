using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.Tests.World;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The world on the device is the managed world</b> (<see cref="DeviceWorld"/>, root <c>decision-log.md</c> 2026-10-11):
/// two towns of one plan, one handed to each, ticked side by side.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P2)]
public class DeviceWorldTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Long enough for the town's people to reach their cars and its cars to drive, park and meet each other.</summary>
    const int Ticks = 600;

    /// <summary>
    /// Every car and walker stands, moves and faces where the managed world has it, to the bit, every tick: the signals
    /// are the only thing the two are told, and what comes back is the only thing the town reads.
    /// </summary>
    [Fact]
    public void TheTownTheDeviceStepsIsTheTownTheManagedWorldSteps()
    {
        using var managed = new TownWorld(Towns.Built, Config);
        using var onTheDevice = new TownWorld(Towns.Built, Config);
        onTheDevice.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var managedLoop = new SimLoop<TownWorld>(managed, Config);
        var deviceLoop = new SimLoop<TownWorld>(onTheDevice, Config);

        var unlike = new List<string>();
        for (var tick = 1; tick <= Ticks && unlike.Count == 0; tick++)
        {
            managedLoop.Advance();
            deviceLoop.Advance();
            TownsInStep.Compare(managed, onTheDevice, tick, unlike);
        }

        Assert.True(unlike.Count == 0, string.Join('\n', unlike.Take(8)));
    }

    /// <summary>
    /// <b>Trains run on the device as in the managed world</b> (TRN-8, TRN-9, SOL-38): a town with a double track, a
    /// train put on each way's run-in and run across it on its own wheels and couplings — every rail vehicle where the
    /// managed world has it, to the bit, every tick.
    /// </summary>
    [Fact]
    public void TrainsRunOnTheDeviceAsInTheManagedWorld()
    {
        var plan = Towns.LayFresh(Towns.Brief(seed: 3, cars: 0, railTracks: 2));
        var config = new SimConfig { Train = new TrainFigures { FirstLeastS = 0.1f, FirstMostS = 0.5f } };
        using var managed = new TownWorld(plan, config);
        using var onTheDevice = new TownWorld(plan, config);
        onTheDevice.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var managedLoop = new SimLoop<TownWorld>(managed, config);
        var deviceLoop = new SimLoop<TownWorld>(onTheDevice, config);

        var unlike = new List<string>();
        for (var tick = 1; tick <= Ticks && unlike.Count == 0; tick++)
        {
            managedLoop.Advance();
            deviceLoop.Advance();
            TownsInStep.Compare(managed, onTheDevice, tick, unlike);
        }

        Assert.True(unlike.Count == 0, string.Join('\n', unlike.Take(8)));
        Assert.True(managed.Trains.Sent > 0, "no train was sent, so the test says nothing");
    }

    /// <summary>
    /// <b>A town laid with its world made on the device is the town handed over to it</b> (<see cref="DeviceWorldMaker"/>):
    /// its atlas filed by the device, and no managed world ever stood — and every car and walker where the town laid whole
    /// and handed over has it, every tick.
    /// </summary>
    /// <remarks>
    /// <b>And laid as a page lays it</b>, the world over a word standing in for the atlas while the device files it, and the
    /// first covers read once it has (<c>DeviceWorld.Ready</c>): the same town, every tick.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATownLaidOntoTheDeviceIsTheTownHandedToIt(bool atlasFinishedFirst)
    {
        using var handed = new TownWorld(Towns.Built, Config);
        handed.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        using var laidOnto = new TownWorld(Towns.Built, Config, worldMaker: new DeviceWorldMaker(device.Vk, atlasFinishedFirst));
        var handedLoop = new SimLoop<TownWorld>(handed, Config);
        var laidOntoLoop = new SimLoop<TownWorld>(laidOnto, Config);

        // Asked before the first tick as a window's loop asks it (Game.Advance), which is where a page finishes laying.
        for (var asked = 0; !laidOnto.World.Ready; asked++) Assert.True(asked < 4, "the world laid on the device never stood ready");

        var unlike = new List<string>();
        for (var tick = 1; tick <= Ticks && unlike.Count == 0; tick++)
        {
            handedLoop.Advance();
            laidOntoLoop.Advance();
            TownsInStep.Compare(handed, laidOnto, tick, unlike);
        }

        Assert.True(unlike.Count == 0, string.Join('\n', unlike.Take(8)));
    }

    /// <summary>
    /// <b>Tows pull on the device as in the managed world</b> (EVA-5, SOL-38): a car held by two arms from its two ends,
    /// carried on none of its wheels — or a truck on one arm that is itself towing, a chain — each set down a little off
    /// the line so the pins have something to pull, and every body where the managed world has it, to the bit.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TowsPullOnTheDeviceAsInTheManagedWorld(bool chain)
    {
        using var managed = new TownWorld(Towns.Built, Config);
        using var onTheDevice = new TownWorld(Towns.Built, Config);
        onTheDevice.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var managedLoop = new SimLoop<TownWorld>(managed, Config);
        var deviceLoop = new SimLoop<TownWorld>(onTheDevice, Config);
        managedLoop.Advance();
        deviceLoop.Advance();
        StageTheTows(managed, chain);
        StageTheTows(onTheDevice, chain);

        var unlike = new List<string>();
        for (var tick = 2; tick <= TowTicks && unlike.Count == 0; tick++)
        {
            managedLoop.Advance();
            deviceLoop.Advance();
            TownsInStep.Compare(managed, onTheDevice, tick, unlike);
        }

        Assert.True(unlike.Count == 0, string.Join('\n', unlike.Take(8)));
    }

    /// <summary>
    /// <b>A rail vehicle pulls an arm on the device as in the managed world</b> (EVA-5, TRN-8): a truck stood behind a
    /// running train facing away from it, its arm put on the last vehicle's tail, and dragged along the track — every body
    /// where the managed world has it, to the bit.
    /// </summary>
    [Fact]
    public void ARailVehicleOnAnArmPullsOnTheDeviceAsInTheManagedWorld()
    {
        var plan = Towns.LayFresh(Towns.Brief(seed: 3, cars: 0, buildings: Towns.BuildingsBuilt, railTracks: 2));
        var config = new SimConfig { Train = new TrainFigures { FirstLeastS = 0.1f, FirstMostS = 0.5f } };
        using var managed = new TownWorld(plan, config);
        using var onTheDevice = new TownWorld(plan, config);
        onTheDevice.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var managedLoop = new SimLoop<TownWorld>(managed, config);
        var deviceLoop = new SimLoop<TownWorld>(onTheDevice, config);

        var tick = 0;
        var unlike = new List<string>();
        for (; tick < 2_000 && LastWagonOut(managed) < 0; tick++)
        {
            managedLoop.Advance();
            deviceLoop.Advance();
        }

        var wagon = LastWagonOut(managed);
        Assert.True(wagon >= 0, "no train came out in the time");
        var truck = Enumerable.Range(0, managed.Cars.Count).FirstOrDefault(each => managed.Cars.BuildOf(each).TowReachM > 0f, -1);
        Assert.True(truck >= 0, "the town stood no vehicle with an arm");
        Assert.Equal(managed.Hitches.OfWagon(wagon), BehindTheWagon(managed, truck, wagon));
        Assert.Equal(managed.Hitches.OfWagon(wagon), BehindTheWagon(onTheDevice, truck, wagon));

        for (var end = tick + TowTicks; tick < end && unlike.Count == 0; tick++)
        {
            managedLoop.Advance();
            deviceLoop.Advance();
            TownsInStep.Compare(managed, onTheDevice, tick, unlike);
        }

        Assert.True(unlike.Count == 0, string.Join('\n', unlike.Take(8)));

        // And the arm still has it: the hinge stands at the wagon's eye nearer than the fork is inside its end, so the
        // fork is under the wagon however fast the train has dragged the truck.
        ref readonly var view = ref onTheDevice.World.View.Wagons[wagon];
        ref readonly var build = ref onTheDevice.Cars.BuildOf(truck);
        var hingeM = TowBar.HookM(build, onTheDevice.Cars.PositionM[truck], Heading.Unit(onTheDevice.Cars.HeadingRad[truck]));
        var gripM = TowBar.GripFromTheMiddleM(onTheDevice.Trains.VehicleOf(wagon).BodyM.X, config.Evacuator.TowGripInsideTheEndM);
        var eyeM = view.PositionM - (Heading.Unit(view.HeadingRad) * (gripM + build.TowReachM));
        Assert.True(view.VelocityMps != Vector2.Zero, "the train had stopped, so the tow says nothing");
        Assert.InRange((hingeM - eyeM).Length(), 0f, config.Evacuator.TowGripInsideTheEndM);

        static int LastWagonOut(TownWorld town)
        {
            for (var train = 0; train < town.Trains.Count; train++)
            {
                var vehicles = town.Trains.VehiclesOf(train);
                if (vehicles.Length > 0 && town.World.View.Wagons[vehicles[^1]].IsInWorld) return vehicles[^1];
            }

            return -1;
        }

        static int BehindTheWagon(TownWorld town, int truck, int wagon)
        {
            ref readonly var view = ref town.World.View.Wagons[wagon];
            var forward = Heading.Unit(view.HeadingRad);
            ref readonly var build = ref town.Cars.BuildOf(truck);
            var gripM = TowBar.GripFromTheMiddleM(town.Trains.VehicleOf(wagon).BodyM.X, town.Config.Evacuator.TowGripInsideTheEndM);
            var apartM = -build.TowHingeAheadOfCentreM + build.TowReachM + gripM;
            town.StandTheCarAt(truck, view.PositionM - (forward * apartM), view.HeadingRad + MathF.PI);
            Assert.True(town.WorkTheArm(truck));
            return town.Hitches.Load(truck);
        }
    }

    /// <summary>Long enough for a pin set down off its line to pull its pair into it and a train to drag a truck a street.</summary>
    const int TowTicks = 300;

    /// <summary>
    /// Two trucks and a car laid along the town's longest lane, the car a little to one side of the line: the first truck
    /// facing along the lane, and either the second facing back with its arm on the car's tail, or the second behind the
    /// first holding the car with its own arm, nose first under the first's.
    /// </summary>
    static void StageTheTows(TownWorld town, bool chain)
    {
        var cars = town.Cars;
        var trucks = Enumerable.Range(0, cars.Count).Where(each => cars.BuildOf(each).TowReachM > 0f).Take(2).ToArray();
        Assert.True(trucks.Length == 2, "the city stood fewer than two vehicles with an arm");
        var (first, second) = (trucks[0], trucks[1]);
        var load = Enumerable.Range(0, cars.Count).First(each => cars.BuildOf(each).TowReachM == 0f && !cars.Driven[each]);

        var roads = town.Roads;
        var lane = Enumerable.Range(0, roads.LaneCount).MaxBy(each => roads.LaneLengthM[each]);
        var on = Spline.SampleAt(roads.ArcsOf(lane), roads.LaneLengthM[lane] * 0.5f);
        var headingRad = MathF.Atan2(on.Direction.Y, on.Direction.X);
        var aside = new Vector2(-on.Direction.Y, on.Direction.X) * 0.3f;
        town.StandTheCarAt(first, on.PositionM, headingRad);
        if (chain)
        {
            var secondM = on.PositionM - (on.Direction * TowBar.SetDownBehindM(cars.BuildOf(first), cars.BuildOf(second)));
            town.StandTheCarAt(second, secondM, headingRad);
            var loadM = secondM - (on.Direction * TowBar.SetDownBehindM(cars.BuildOf(second), cars.BuildOf(load))) + aside;
            town.StandTheCarAt(load, loadM, headingRad);
            Assert.True(town.WorkTheArm(second));
            Assert.True(town.WorkTheArm(first));
            Assert.Equal((second, load), (town.Hitches.Load(first), town.Hitches.Load(second)));
            return;
        }

        var carM = on.PositionM - (on.Direction * TowBar.SetDownBehindM(cars.BuildOf(first), cars.BuildOf(load))) + aside;
        town.StandTheCarAt(load, carM, headingRad);
        town.StandTheCarAt(second, carM - (on.Direction * TowBar.SetDownBehindM(cars.BuildOf(second), cars.BuildOf(load))), headingRad + MathF.PI);
        Assert.True(town.WorkTheArm(first));
        Assert.True(town.WorkTheArm(second));
        Assert.Equal((first, second), (town.Hitches.HeldBy(load, byTheTail: false), town.Hitches.HeldBy(load, byTheTail: true)));
    }

    /// <summary>
    /// <b>A car is read along its line on the device where the managed world reads it</b> (<c>line_reading.glsl</c>,
    /// <see cref="LineReading"/>): every car of a town put down on a lane, on and off it, told to follow it — on into the
    /// join and the next lane, and cut short — and the same progress, distance off and point the wheel is aimed at read
    /// back, to the bit.
    /// </summary>
    [Fact]
    public void ACarIsReadAlongItsLineOnTheDeviceAsInTheManagedWorld()
    {
        using var town = new TownWorld(Towns.Of(Towns.City), Config);
        WorldLaying? laid = null;
        town.HandTheWorldTo(laying =>
        {
            laid = laying;
            return new DeviceWorld(device.Vk, laying);
        });
        using var managed = new ManagedWorld(laid!, stepsAside: false);
        var onTheDevice = town.World;
        var roads = laid!.Roads;

        var cars = laid.Cars.Length;
        for (var car = 0; car < cars; car++)
        {
            var lane = (int)((uint)(car * 7919) % (uint)roads.LaneCount);
            var onto = roads.LanesFrom(lane);
            var join = onto.Length > 0 && car % 3 != 0 ? roads.ConnectorBetween(lane, onto[car % onto.Length]) : RoadGraph.NoConnector;
            var lastLaneM = roads.LaneLengthM[join == RoadGraph.NoConnector ? lane : roads.ConnectorTo(join)];
            var stopsAtM = car % 4 == 0 ? lastLaneM * 0.5f : lastLaneM;
            var alongM = roads.LaneLengthM[lane] * (car % 10) / 9f;
            var on = Spline.SampleAt(roads.ArcsOf(lane), alongM);
            var atM = on.PositionM + (on.Right * ((car % 5) - 2) * 0.4f);
            var agent = laid.Roster.AgentOfCar(car);
            var followed = new LineFollowed(lane, join, stopsAtM, alongM + ((car % 7) - 3), serial: (uint)(car % 1000));
            foreach (var world in (ReadOnlySpan<WorldLayer>)[managed, onTheDevice])
            {
                world.Signals.Add(ControlSignal.Place(agent, atM, on.HeadingRad + ((car % 3) - 1) * 0.1f));
                world.Signals.Add(ControlSignal.Follow(agent, followed));
            }
        }

        managed.ReadTheCovers();
        onTheDevice.ReadTheCovers();

        var unlike = new List<string>();
        var read = 0;
        for (var car = 0; car < cars; car++)
        {
            var (one, other) = (managed.View.Cars[car], onTheDevice.View.Cars[car]);
            var reading = (Bits(one.ProgressM), Bits(one.OffLineM), one.FollowSerial, Bits(one.LeadAtM), Bits(one.AimRad));
            var otherReading = (
                Bits(other.ProgressM), Bits(other.OffLineM), other.FollowSerial, Bits(other.LeadAtM), Bits(other.AimRad));
            if (!reading.Equals(otherReading))
            {
                unlike.Add(
                    $"car {car}: {one.ProgressM} {one.OffLineM} {one.LeadAtM} {one.AimRad} against " +
                    $"{other.ProgressM} {other.OffLineM} {other.LeadAtM} {other.AimRad}");
            }
            read += float.IsNaN(one.ProgressM) ? 0 : 1;
        }

        Assert.True(unlike.Count == 0, $"{unlike.Count} of {cars}:\n" + string.Join('\n', unlike.Take(8)));
        Assert.Equal(cars, read);

        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    }

    /// <summary>
    /// <b>A batch is taken in the order it was signalled</b>, as the managed world takes it: of two commands to one car the
    /// second stands, and an aim given after a walker was put down walks it from there.
    /// </summary>
    [Fact]
    public void OfTwoSignalsToOneAgentTheLaterStandsOnTheDeviceAsInTheManagedWorld()
    {
        using var managed = new TownWorld(Towns.Built, Config);
        using var onTheDevice = new TownWorld(Towns.Built, Config);
        onTheDevice.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        const int person = 0;
        const int car = 0;
        var placedAtM = managed.World.View.Walkers[person].PositionM;
        var agentOfCar = managed.Roster.AgentOfCar(car);

        foreach (var world in (ReadOnlySpan<WorldLayer>)[managed.World, onTheDevice.World])
        {
            world.Signals.Add(ControlSignal.Drive(agentOfCar, new DriveCommand(0f, 3f, 0f, false, false), false));
            world.Signals.Add(ControlSignal.Drive(agentOfCar, DriveCommand.Parked, false));
            world.Signals.Add(ControlSignal.Place(person, placedAtM, 0f));
            world.Signals.Add(ControlSignal.Walk(person, placedAtM + new Vector2(3f, 0f), moving: true, onFeet: true, faces: true));
        }

        for (var step = 0; step < Steps; step++)
        {
            managed.World.Step(Config.TickSeconds);
            onTheDevice.World.Step(Config.TickSeconds);
        }

        Assert.NotEqual(placedAtM, managed.World.View.Walkers[person].PositionM);
        Assert.Equal(managed.World.View.Walkers[person].PositionM, onTheDevice.World.View.Walkers[person].PositionM);
        Assert.Equal(managed.World.View.Cars[car].PositionM, onTheDevice.World.View.Cars[car].PositionM);
    }

    /// <summary>Long enough for a walker to leave where it was put.</summary>
    const int Steps = 30;

    /// <summary>
    /// <b>Whether a car is frozen and was frozen through the step before, as the managed world says</b> in every view —
    /// which says which step it froze at: the step's own from the first on, and one written again off the clock after a signal thawed the car
    /// between steps — which the next step then reads as the car coming into it awake.
    /// </summary>
    [Fact]
    public void ACarIsFrozenThroughItsLastStepOnTheDeviceAsInTheManagedWorldInEveryView()
    {
        using var managed = new TownWorld(Towns.Built, Config);
        using var onTheDevice = new TownWorld(Towns.Built, Config);
        onTheDevice.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        var thawAt = Config.SolverRestTicks * 2;
        var unlike = new List<string>();
        for (var step = 1; step <= thawAt * 2 && unlike.Count == 0; step++)
        {
            managed.World.Step(Config.TickSeconds);
            onTheDevice.World.Step(Config.TickSeconds);
            CompareTheFreezing(managed.World.View, onTheDevice.World.View, $"step {step}", unlike);
            if (step != thawAt) continue;

            var car = FirstFrozen(managed.World.View);
            Assert.True(car >= 0, "no car froze to be thawed");
            var channels = (byte)(managed.World.View.Cars[car].Flags >> 16);
            foreach (var world in (ReadOnlySpan<WorldLayer>)[managed.World, onTheDevice.World])
            {
                world.Signals.Add(ControlSignal.Channels(managed.Roster.AgentOfCar(car), channels));
                world.ReadTheCovers();
            }

            CompareTheFreezing(managed.World.View, onTheDevice.World.View, $"off the clock after step {step}", unlike);
        }

        Assert.True(unlike.Count == 0, string.Join('\n', unlike.Take(8)));
    }

    static void CompareTheFreezing(WorldView managed, WorldView onTheDevice, string when, List<string> unlike)
    {
        const uint freezing = CarView.Frozen | CarView.FrozenThroughLastStep;
        for (var car = 0; car < managed.Cars.Length; car++)
        {
            var (one, other) = (managed.Cars[car].Flags & freezing, onTheDevice.Cars[car].Flags & freezing);
            if (one != other) unlike.Add($"{when} car {car}: flags {one} against {other}");
        }
    }

    static int FirstFrozen(WorldView view)
    {
        for (var car = 0; car < view.Cars.Length; car++)
        {
            if ((view.Cars[car].Flags & CarView.FrozenThroughLastStep) != 0) return car;
        }

        return -1;
    }

    /// <summary>
    /// <b>A car put down between steps is driven on from where it was put</b>, as in the managed world: its tyres read
    /// the pose the signal left, not the one the step before it did.
    /// </summary>
    [Fact]
    public void ACarPutDownBetweenStepsIsDrivenFromWhereItWasPutOnTheDeviceAsInTheManagedWorld()
    {
        using var managed = new TownWorld(Towns.Built, Config);
        using var onTheDevice = new TownWorld(Towns.Built, Config);
        onTheDevice.HandTheWorldTo(laying => new DeviceWorld(device.Vk, laying));
        const int car = 0;
        var agentOfCar = managed.Roster.AgentOfCar(car);
        var standing = managed.World.View.Cars[car];
        var putAtM = standing.PositionM + new Vector2(0.5f, 0.25f);
        foreach (var world in (ReadOnlySpan<WorldLayer>)[managed.World, onTheDevice.World])
        {
            world.Step(Config.TickSeconds);
            world.Signals.Add(ControlSignal.Place(agentOfCar, putAtM, standing.HeadingRad + 0.5f));
            world.ReadTheCovers();
            world.Signals.Add(ControlSignal.Drive(agentOfCar, new DriveCommand(0.2f, 3f, 0f, false, false), false));
            for (var step = 0; step < Steps; step++) world.Step(Config.TickSeconds);
        }

        var (one, other) = (managed.World.View.Cars[car], onTheDevice.World.View.Cars[car]);
        Assert.NotEqual(putAtM, one.PositionM);
        Assert.Equal((one.PositionM, one.VelocityMps, one.HeadingRad), (other.PositionM, other.VelocityMps, other.HeadingRad));
    }
}
