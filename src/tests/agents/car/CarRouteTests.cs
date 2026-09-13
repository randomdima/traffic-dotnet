using System.Collections.Concurrent;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
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
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class CarRouteTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static TownWorld Open(string map) => new(Towns.Of(map), Config);

    public static TheoryData<string> Maps => Towns.EveryTown();

    static readonly ConcurrentDictionary<string, TownWorld> Ran = new();

    /// <summary>
    /// <b>A minute of the town, taken once per map and read by every claim asked of every map.</b> All four
    /// are claims about the lines and routes a driven town is holding, and every one of them holds at any
    /// tick of it, so four runs of one map were the same minute driven four times over.
    /// </summary>
    static TownWorld Driven(string map) => Ran.GetOrAdd(map, opened =>
    {
        var world = Open(opened);
        new SimLoop<TownWorld>(world, Config).Advance(TicksDriven);
        return world;
    });

    /// <summary>A minute: what the search count below needs, and longer than any of the others asked for.</summary>
    const int TicksDriven = 3_600;

    /// <summary>
    /// <b>A line is only ever laid over lanes the road joins</b>, whether the lane came out of a route or
    /// out of the tour that carries a car the search could not help. A chain with a break in it is a car
    /// told to drive across a block.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryChainIsAContiguousRunOfTurns(string map)
    {
        var world = Driven(map);
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
    /// <b>Every pair of lanes in a queued route is a pair the road joins.</b> Nothing a car is handed to
    /// drive reverses the direction of travel (TER-5f): where a leg has to come back the other way the
    /// queue stops at the car park's frontage and the bay does the turning (GEN-4l), so a reversing pair
    /// never reaches the line assembler.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void EveryLanePairACarIsHandedIsOneTheRoadJoins(string map)
    {
        var world = Driven(map);
        var roads = world.Roads;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var route = world.Cars.RouteOf(car);
            for (var slot = 1; slot < world.Cars.RouteCount[car]; slot++)
            {
                Assert.NotNull(roads.TurnBetween(route[slot - 1], route[slot]));
            }
        }
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
        var world = Driven(map);
        if (world.Boardings == 0) return;

        Assert.True(
            world.RouteSearches <= world.Boardings * MostSearchesPerLeg,
            $"{map}: {world.RouteSearches} searches of the driving network over {world.Boardings} legs");
    }

    /// <summary>What a leg may spend on finding its way before it is re-deriving rather than routing.</summary>
    const int MostSearchesPerLeg = 12;

}
