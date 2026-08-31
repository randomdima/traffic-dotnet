using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Person;

/// <summary>
/// The trip: what a person decides about one (PER-17, PER-10a) and whether a whole one completes on a
/// town (VER-8).
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class TripTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// PER-17 is <b>structural and not a weighted coin</b>: the same block is walked however far it is,
    /// and a trip that crosses a road is walked only while it is inside the walk-worth distance.
    /// </summary>
    [Fact]
    public void TheSameBlockIsWalkedAndAnythingFurtherIsWorthACar()
    {
        var farM = Config.PersonWalkWorthM * 2f;

        Assert.True(Trip.IsWorthWalking(Config, farM, crossesACarriageway: false));
        Assert.True(Trip.IsWorthWalking(Config, Config.PersonWalkWorthM * 0.5f, crossesACarriageway: true));
        Assert.False(Trip.IsWorthWalking(Config, farM, crossesACarriageway: true));

        // PER-10a's ceiling is the same figure, which is why there is one and not two.
        Assert.True(Trip.IsTooFarToWalk(Config, farM));
        Assert.False(Trip.IsTooFarToWalk(Config, Config.PersonWalkWorthM * 0.5f));
    }

    /// <summary>
    /// <b>VER-8: a whole trip, end to end.</b> Somebody leaves a building, walks to a car, drives it to
    /// the bay it reserved, gets out and walks in — and the counts move together, because a town where
    /// only the first of them moves is a town of people setting off and arriving nowhere.
    /// </summary>
    [Fact]
    public void AWholeTripCompletesUnattendedAndRepeatedly()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        // Twice the probe's own window, because a whole drive leg on this map no longer fits in one:
        // the ground gained a rolling resistance with the tyre model, and a fifth of a car's
        // acceleration goes to it at every one of the fixture map's many stops.
        loop.Advance(TripProbe.WarmupTicks + (TripProbe.MeasuredTicks * 2));

        Assert.True(world.TripsDrawn > 0, "nobody drew a destination");
        Assert.True(world.TripsWorthACar > 0, "no trip was judged worth a car");
        Assert.True(world.Boardings > 0, "nobody got into a car");
        Assert.True(world.BaysParkedIn > 0, "no car came to rest in the bay it was driven to");
        Assert.True(world.Alightings > 0, "nobody got out of a car");
        Assert.True(world.BuildingsEntered > 1, "a trip that never ends in a doorway is not a trip");
    }

    /// <summary>
    /// GEN-7 and CAR-1: <b>the town's cars start in its bays with nobody in them</b>, and every metre of
    /// traffic after that is somebody's trip rather than an arrangement.
    /// </summary>
    /// <remarks>
    /// <b>It is a rule about a town, and it is asked of the towns.</b> A map with nobody living on it —
    /// no building to go to, no bay to be claimed out of and nobody to claim one — has no trips for CAR-1
    /// to be about, and its cars are put on the road by the map itself rather than by anybody's leg. A
    /// proving ground is the only such map, and what it is for is the traffic it makes.
    /// <para>
    /// <b>A service vehicle is the one car this is not said of</b> (AMB-3, SRV-3): it starts parked like
    /// the rest, and with its crew already aboard, which is what makes it a car that acts without CAR-1
    /// needing an exception written into it. That it is never free is also what keeps it out of everybody
    /// else's trip (PER-4), so both halves are asserted here rather than assumed.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Towns.EveryTown), MemberType = typeof(Towns))]
    public void EveryCarStartsParkedWithNobodyInIt(string map)
    {
        var plan = Towns.Of(map);
        if (plan.Buildings.Count == 0 && plan.ParkingLots.SpaceCount == 0) return;

        using var world = new TownWorld(plan, Config);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            Assert.False(world.Cars.Driven[car], $"{map}: car {car} is driving before anybody has got into it");
            Assert.Equal(CarCatalog.Shared.IsService(world.Cars.Variant[car]), !world.Containment.IsFree(car));
            Assert.True(
                world.Parking.BayOf(car) >= 0,
                $"{map}: car {car} did not stand up in a bay the registry knows about");
        }
    }

    /// <summary>
    /// CTL-3: with a person selected, right-clicking a building is an order to walk there <em>and
    /// enter</em> — and every containment check binds unchanged on the way.
    /// </summary>
    [Fact]
    public void AContextOrderToABuildingIsWalkedToAndEntered()
    {
        using var world = new TownWorld(Towns.Of(Towns.Fixture), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1);

        // The building nearest a walker that is out on the pavement, so the order is one it can actually
        // carry out: an order to somebody indoors is taken up when the door puts them down (CTL-2).
        var plan = Towns.Of(Towns.Fixture);
        var walker = OutOfDoors.AWalker(world, loop, Config);
        var nearest = 0;
        var nearestM = float.MaxValue;
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var farM = (plan.Buildings.CentreM[building] - world.People.PositionM[walker]).Length();
            if (farM >= nearestM) continue;

            nearest = building;
            nearestM = farM;
        }

        world.Order(walker, plan.Buildings.CentreM[nearest]);
        loop.Advance(1);

        Assert.True(world.People.Manual[walker]);
        Assert.Equal(nearest, world.People.DestinationBuilding[walker]);
        Assert.Equal(TripStage.WalkingToTheDoor, world.People.Stage[walker]);

        // Either it gets inside, or it stands waiting for a place at a full one — both are the order
        // carried out. It is watched rather than asked at the end, because the dwell inside is bounded
        // and a walker that has been in and come out again reads from outside like one that never went.
        var carriedOut = false;
        for (var window = 0; window < 30 && !carriedOut; window++)
        {
            loop.Advance(60);
            carriedOut = world.People.Inside[walker] == new Contained(ContainerKind.Building, nearest)
                         || world.People.Stage[walker] is TripStage.WaitingForAPlace;
        }

        Assert.True(carriedOut, $"the ordered walker ended in {world.People.Stage[walker]}");

        // CTL-4: it stays in manual mode, so what it does when the order is done is idle awaiting the
        // next one rather than draw a destination of its own.
        Assert.True(world.People.Manual[walker]);
    }
}
