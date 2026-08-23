using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The routed driver: every car is given a route and drives it, and what it drives is a chain of turns
/// the road actually joins.
/// </summary>
[Collection(TrafficSimulation.Tests.Simulation.SolverCollection.Name)]
[Trait(Tier.Key, Tier.Town)]
public class CarRouteTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static TownWorld Open(string map) => new(Towns.Fresh(map), Config);

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    /// <summary>
    /// <b>A line is only ever laid over lanes the road joins</b>, whether the lane came out of a route or
    /// out of the tour that carries a car the search could not help. A chain with a break in it is a car
    /// told to drive across a block.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryChainIsAContiguousRunOfTurns(string map)
    {
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(600);

        var roads = world.Roads;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!world.Cars.Driven[car]) continue;

            var chain = world.Cars.ChainOf(car);
            for (var slot = 1; slot < world.Cars.Line[car].LaneCount; slot++)
            {
                Assert.NotNull(roads.TurnBetween(chain[slot - 1], chain[slot]));
            }
        }
    }

    /// <summary>
    /// <b>No route turns a car round.</b> The network prices a turn-around out of reach because the line
    /// between two opposing lanes is a semicircle no car can hold, and a route through one is a route the
    /// driver leaves its lane to drive.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoCarIsEverRoutedThroughATurnAround(string map)
    {
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(1_200);

        var roads = world.Roads;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var route = world.Cars.RouteOf(car);
            for (var slot = 1; slot < world.Cars.RouteCount[car]; slot++)
            {
                Assert.NotEqual(LaneTurn.TurnAround, roads.TurnBetween(route[slot - 1], route[slot]));
            }
        }
    }

    /// <summary>
    /// <b>The routes are driven and not merely planned.</b> A town whose cars never reach anywhere is a
    /// town whose router is answering a question nobody finishes — which is what a circling car looks
    /// like from outside, and is exactly the fault a radius-based arrival test hid.
    /// </summary>
    /// <remarks>
    /// What a car reaches is <em>the bay its driver reserved</em>, and it reaches it by coming to rest in
    /// it: a car has no goals of its own (CAR-8), so there is no other arrival to count.
    /// </remarks>
    [Fact]
    public void CarsReachTheBayTheyWereDrivenTo()
    {
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, Config);

        // Two minutes: a car on this map spends most of its leg stopped at a light, and the rolling
        // resistance the ground gained with the tyre model costs it about a fifth of its acceleration
        // out of every one of those stops.
        loop.Advance(7_200);

        Assert.True(world.Boardings > 0, "nobody got into a car in two minutes of the fixture map");
        Assert.True(world.BaysParkedIn > 0, "no car was parked in the bay it was driven to");
    }

    /// <summary>
    /// <b>A leg is routed a handful of times, not once per junction.</b> The route is searched for when a
    /// leg is drawn and again where it runs out; the lanes between two decisions are the network's own and
    /// are read rather than found. A car that re-derived its way at every junction would drive exactly the
    /// same and cost tens of searches a leg, which is the one thing no other reading here would show.
    /// </summary>
    /// <remarks>
    /// The bound is what a leg may honestly spend: two bays screened before one is claimed, the route laid
    /// once from where the car sets off, and what a retarget or a reroute costs on top of it. The shipped
    /// maps come to two searches a leg, and five on the one whose bays stand across water.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void ALegIsRoutedAHandfulOfTimes(string map)
    {
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(3_600);

        if (world.Boardings == 0) return;

        Assert.True(
            world.RouteSearches <= world.Boardings * MostSearchesPerLeg,
            $"{map}: {world.RouteSearches} searches of the driving network over {world.Boardings} legs");
    }

    /// <summary>What a leg may spend on finding its way before it is re-deriving rather than routing.</summary>
    const int MostSearchesPerLeg = 12;

    /// <summary>
    /// Every car that has been asked where it is going has somewhere to go, and it is a place in the
    /// town rather than nowhere.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A car on one lane has not been asked yet</b>, and that is not an omission: a line reaches as
    /// far as the car can stop from its own top speed, and the longest runs of the two cities are further
    /// than that — so a car standing on one is given its destination the first time it needs a second
    /// lane, and not before.
    /// </para>
    /// <para>
    /// <b>A map with nobody living on it is asked the other half of the same claim.</b> There is nowhere on
    /// the proving ground to go — no building, no bay and nobody to claim one — so its cars are carried by
    /// the tour rather than by a trip (CAR-1), and what has to hold is that not one of them was handed a
    /// destination anyway.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryCarThatHasBeenAskedIsGivenADestinationInTheTown(string map)
    {
        var plan = Towns.Of(map);
        using var world = Open(map);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(120);

        var nowhereToGo = plan.Buildings.Count == 0 && plan.ParkingLots.SpaceCount == 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!world.Cars.Driven[car] || world.Cars.Line[car].LaneCount < 2) continue;

            if (nowhereToGo)
            {
                Assert.False(
                    world.Cars.HasDestination[car], $"{map}: car {car} was sent somewhere in a town with nowhere to go");
                continue;
            }

            Assert.True(world.Cars.HasDestination[car], $"{map}: car {car} is driving with nowhere to go");

            var toM = world.Cars.DestinationM[car];
            Assert.InRange(toM.X, 0f, plan.WorldSizeM.X);
            Assert.InRange(toM.Y, 0f, plan.WorldSizeM.Y);
        }
    }
}
