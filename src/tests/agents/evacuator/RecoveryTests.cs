using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Evacuator;

/// <summary>
/// The recovery asked of a town: the evacuator a map stands and the yard behind it (SRV-2, EVA-2), what a
/// wrecked car becomes (EVA-1), and that a recovery is taken, driven at and coupled (EVA-3, EVA-4, EVA-5).
/// </summary>
/// <remarks>
/// <b>Whether a recovery completes is a reading and not a bound</b>, and it is <c>--bench recovery</c>'s
/// (<see cref="Bench.RecoveryProbe"/>): a wreck a tow cannot get home through a dense city's geometry is a
/// fact about that city, and asserting it here would be tuning the towns until the instrument could no
/// longer report the thing it was written to find. What is asserted below is the machinery either side.
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class RecoveryTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// SRV-2 and EVA-2: every evacuator the town stood is parked in a bay held for it at its own depot,
    /// wearing the service variant, with a crew inside it and a yard of slots beside it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void EveryEvacuatorStandsAtItsDepotWithAYardBehindIt(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);

        var stood = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Recovery.Depot[car] < 0) continue;

            stood++;
            Assert.True(world.Depots.Holds(world.Recovery.Depot[car]), $"{map}: an evacuator belongs to no depot");
            Assert.Equal(CarCatalog.Shared.Evacuator, world.Cars.Variant[car]);
            Assert.Equal(RecoveryStage.Waiting, world.Recovery.Stage[car]);
            Assert.False(world.Cars.BlueLight[car], $"{map}: an evacuator started with its priority on");
            Assert.Equal(RecoveryDuty.Nothing, world.Recovery.Towing[car]);

            var bay = world.Recovery.HomeBay[car];
            Assert.Equal(bay, world.Parking.BayOf(car));
            Assert.Equal(car, world.Parking.HeldFor(bay));

            var crew = world.Containment.DriverOf(car);
            Assert.True(crew >= 0, $"{map}: an evacuator has nobody in it (CAR-1)");
            Assert.Equal(TripStage.OnDuty, world.People.Stage[crew]);
            Assert.Equal(ContainerKind.Car, world.People.Inside[crew].Kind);
        }

        Assert.Equal(world.Evacuators, stood);

        // EVA-2: a yard slot is held for whatever is brought to it and stands empty until something is.
        var slots = 0;
        for (var bay = 0; bay < world.Parking.BayCount; bay++)
        {
            if (world.Parking.HeldFor(bay) != ParkingRegistry.TheYard) continue;

            slots++;
            Assert.False(world.Parking.IsFree(bay), $"{map}: a yard slot reads free to the town");
            Assert.Equal(ParkingRegistry.Nobody, world.Parking.CarInBay(bay));
        }

        Assert.True(
            slots <= world.Depots.Count * Config.Evacuator.YardSlots,
            $"{map}: {slots} yard slots for {world.Depots.Count} depots");
        Assert.True(world.Evacuators == 0 || slots > 0, $"{map}: an evacuator stands at a depot with no yard");
    }

    /// <summary>
    /// EVA-1 and EVA-3: a car wrecked in the street is a call, and the town's evacuator takes it, drives at
    /// it with the priority up, and gets it onto the bar.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the fixture town</b>, whose one depot and one evacuator make the answer a fact about the
    /// machine rather than about which of a city's twenty wrecks was nearest.
    /// </remarks>
    [Fact]
    public void AWreckIsTakenAndPutOnTheBar()
    {
        using var world = new TownWorld(Towns.Of("Test"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var evacuator = TheEvacuator(world);
        Assert.True(evacuator >= 0, "the fixture town stood no evacuator");

        var wreck = NearestOrdinaryCar(world, world.Cars.PositionM[evacuator]);
        Assert.True(wreck >= 0, "the fixture town has no ordinary car to wreck");

        world.Apply(new BodyTag(BodyKind.Car, wreck), DamageOutcome.Broken);
        Assert.Equal(1, world.WrecksRaised);

        var hurried = false;
        for (var tick = 0; tick < 60 * 240 && world.WrecksHitched == 0; tick++)
        {
            loop.Advance(1);
            hurried |= world.Cars.BlueLight[evacuator];

            // EVA-4: the priority is the outbound leg and nothing else.
            Assert.True(
                !world.Cars.BlueLight[evacuator] || world.Recovery.Stage[evacuator] == RecoveryStage.Running,
                $"an evacuator carried the priority while {world.Recovery.Stage[evacuator]}");

            // CAR-14.6: and the amber bar is the recovery itself, every stage of it and no other tick.
            Assert.Equal(
                world.Recovery.Stage[evacuator] != RecoveryStage.Waiting, world.Cars.AtWork[evacuator]);
        }

        Assert.True(world.WrecksHitched > 0, "no evacuator got the wreck onto the bar in four minutes");
        Assert.True(hurried, "an evacuator answered a wreck without the priority (EVA-4)");
        Assert.Equal(evacuator, world.Recovery.OnTheHookOf[wreck]);
        Assert.Equal(wreck, world.Recovery.Towing[evacuator]);

        // EVA-5: on the arm it is the arm's own reach behind the truck and no longer standing in anybody's bay.
        var apartM = (world.Cars.PositionM[evacuator] - world.Cars.PositionM[wreck]).Length();
        var wantedM = TowBar.SetDownBehindM(world.Cars.BuildOf(evacuator), world.Cars.BuildOf(wreck));
        Assert.True(apartM < wantedM * 1.5f, $"the wreck was hooked on {apartM:F1} m behind a {wantedM:F1} m arm");

        // And the pair are two bodies the solver still holds apart: the arm's daylight is what stands between
        // them, not an exemption, so a truck that closes it hits what it is towing.
        var clearM = world.Cars.BuildOf(evacuator).HalfLengthM + world.Cars.BuildOf(wreck).HalfLengthM;
        Assert.True(apartM > clearM, $"the truck and its load stood {apartM:F1} m apart, inside the {clearM:F1} m they take");
        Assert.Equal(ParkingRegistry.NoBay, world.Parking.BayOf(wreck));
    }

    /// <summary>
    /// EVA-5 and CTL-7: <b>the arm is a lever, and the player pulls the same one the crew does</b>. Worked
    /// once it lets go of what it is holding; worked again with that car still standing in its reach it
    /// picks it back up — and it is the same coupling either way, because there is only one.
    /// </summary>
    [Fact]
    public void TheArmLetsGoAndPicksUpAgainWhenItIsWorkedByHand()
    {
        using var world = new TownWorld(Towns.Of("Test"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var evacuator = TheEvacuator(world);
        var wreck = NearestOrdinaryCar(world, world.Cars.PositionM[evacuator]);
        world.Apply(new BodyTag(BodyKind.Car, wreck), DamageOutcome.Broken);
        for (var tick = 0; tick < 60 * 240 && world.WrecksHitched == 0; tick++) loop.Advance(1);

        Assert.Equal(wreck, world.Recovery.Towing[evacuator]);

        // A car on the bar has its wheels straight, whichever way they were pointing when it stopped being
        // driven: the pair left on the ground has to roll.
        Assert.Equal(0f, world.Cars.Command[wreck].SteerRad, 5);

        world.Select(new Selection(SelectionKind.Car, evacuator));
        Assert.True(world.WorkTheAction(), "the arm refused to let go of what it was holding");
        Assert.Equal(RecoveryDuty.Nothing, world.Recovery.Towing[evacuator]);
        Assert.Equal(RecoveryDuty.Nothing, world.Recovery.OnTheHookOf[wreck]);

        // Standing exactly where the arm left it, so the very next pull of the lever has it again.
        Assert.True(world.WorkTheAction(), "the arm reached a car standing at its own fork and caught nothing");
        Assert.Equal(wreck, world.Recovery.Towing[evacuator]);
        Assert.Equal(evacuator, world.Recovery.OnTheHookOf[wreck]);

        // And it has it by the end the fork is actually under, which is the nose of a car set down in line.
        Assert.False(world.Recovery.HeldByTheTail[wreck], "a car in line behind the truck was caught by its tail");
    }

    /// <summary>And a car with no arm has no action to work, which is every other look in the catalogue.</summary>
    [Fact]
    public void ACarWithNoArmHasNothingToWork()
    {
        using var world = new TownWorld(Towns.Of("Test"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var evacuator = TheEvacuator(world);
        var ordinary = NearestOrdinaryCar(world, world.Cars.PositionM[evacuator]);

        world.Select(new Selection(SelectionKind.Car, ordinary));
        Assert.False(world.WorkTheAction(), "an ordinary car did something when its action was worked");

        world.SelectNone();
        Assert.False(world.WorkTheAction(), "an empty selection did something when its action was worked");
    }

    /// <summary>
    /// EVA-4: <b>the way back is traffic</b>. Once anything is on the bar the priority is out, and it stays
    /// out for the whole of the haul.
    /// </summary>
    [Fact]
    public void AHaulCarriesNoPriority()
    {
        using var world = new TownWorld(Towns.Of("Test"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var evacuator = TheEvacuator(world);
        var wreck = NearestOrdinaryCar(world, world.Cars.PositionM[evacuator]);
        world.Apply(new BodyTag(BodyKind.Car, wreck), DamageOutcome.Broken);

        var hauled = 0;
        for (var tick = 0; tick < 60 * 480; tick++)
        {
            loop.Advance(1);
            if (world.Recovery.Towing[evacuator] < 0) continue;

            hauled++;
            Assert.False(world.Cars.BlueLight[evacuator], "an evacuator hauled a wreck with the priority up");

            // CAR-14.6: the amber bar is the work, so it is up on exactly the leg the priority is not.
            Assert.True(world.Cars.AtWork[evacuator], "an evacuator hauled a wreck with its amber bar out");
        }

        Assert.True(hauled > 0, "nothing was ever on the bar");
    }

    /// <summary>
    /// EVA-7: a wreck standing in a yard slot is a car again once the workshop has had it long enough — and
    /// it is an <em>ordinary</em> car, which is what says a mended one can be driven away by whoever walks
    /// past (PER-4).
    /// </summary>
    /// <remarks>
    /// <b>The whole errand is run rather than the slot filled by hand</b>, because the interesting half of
    /// EVA-7 is what a restored car <em>is</em> to everything else in the town, and a car put in a slot by a
    /// test is a car nothing else in the town ever agreed to.
    /// </remarks>
    [Fact]
    public void AWreckTakenToTheYardIsACarAgain()
    {
        using var world = new TownWorld(Towns.Of("Test"), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var evacuator = TheEvacuator(world);
        var wreck = NearestOrdinaryCar(world, world.Cars.PositionM[evacuator]);
        world.Apply(new BodyTag(BodyKind.Car, wreck), DamageOutcome.Broken);

        for (var tick = 0; tick < 60 * 600 && world.WrecksRestored == 0; tick++) loop.Advance(1);

        Assert.True(world.WrecksYarded > 0, "the fixture town's evacuator never got a wreck into its yard (EVA-6)");
        Assert.True(world.WrecksRestored > 0, "a wreck stood in the yard and was never mended (EVA-7)");
        Assert.False(world.Cars.Broken[wreck], "a restored car is still in its terminal state");
        Assert.True(world.Parking.BayOf(wreck) >= 0, "a restored car is standing in no bay");
        Assert.False(world.Cars.Ambulance[wreck], "a restored car is still on a building's strength (EVA-7)");
        Assert.Equal(RecoveryDuty.Nothing, world.Recovery.OnTheHookOf[wreck]);
    }

    static int TheEvacuator(TownWorld world)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Recovery.Depot[car] >= 0) return car;
        }

        return -1;
    }

    /// <summary>The nearest ordinary parked car to a place: what a staged wreck is made of, and never a vehicle the town stood.</summary>
    static int NearestOrdinaryCar(TownWorld world, System.Numerics.Vector2 fromM)
    {
        var best = -1;
        var bestM = float.PositiveInfinity;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Broken[car] || CarCatalog.Shared.IsService(world.Cars.Variant[car])) continue;
            if (!world.Containment.IsFree(car)) continue;

            var farM = (world.Cars.PositionM[car] - fromM).Length();
            if (farM >= bestM) continue;

            best = car;
            bestM = farM;
        }

        return best;
    }
}
