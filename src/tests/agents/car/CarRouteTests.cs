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
public class CarRouteTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static TownWorld Open(string map) => new(Towns.Of(map), Config);

    public static TheoryData<string> Maps => Towns.EveryShippedMap();

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
        // out of every one of those stops. It is the bound on how long a leg may take rather than a
        // window to watch to the end of — both halves are that something happened, so the tick the
        // second of them happens on is the answer.
        for (var tick = 0; tick < 7_200 && world.BaysParkedIn == 0; tick++) loop.Advance(1);

        Assert.True(world.Boardings > 0, "nobody got into a car in two minutes of the fixture map");
        Assert.True(world.BaysParkedIn > 0, "no car was parked in the bay it was driven to");
    }

    /// <summary>
    /// <b>A leg's line reaches the bay, and it reaches it over the bay's own way</b> (GEN-4f). The route
    /// used to stop three car lengths short and a manoeuvre laid the rest from wherever the car had got
    /// to; what the line ends at now is the pose the town drew the bay's way to, so the last dozen metres
    /// of a leg are in the book like every metre before them.
    /// </summary>
    /// <remarks>
    /// <b>A way the car reverses into is the one that ends at the mouth</b> (GEN-4j): a route is driven
    /// forwards, so such a way is not threaded onto the line — the line ends where the way begins, the car
    /// stops there, and `P-14` lays the town's own shape again from the pose it stopped in.
    /// <para>
    /// Asserted of every car found on its final approach over two minutes rather than of one — a claim
    /// about the assembler holds for every line it lays or it does not hold.
    /// </para>
    /// </remarks>
    [Fact]
    public void ALegsLineFinishesOnTheWayIntoItsBay()
    {
        using var world = Open(Towns.Fixture);
        var loop = new SimLoop<TownWorld>(world, Config);
        var ways = world.BayWays;
        var found = 0;

        for (var tick = 0; tick < 7_200; tick++)
        {
            loop.Advance(1);
            for (var car = 0; car < world.Cars.Count; car++)
            {
                // The way out of a bay is a line's own way too, and it finishes on the road rather than
                // at the bay — this is the claim about the leg's approach and not about the departure.
                var way = world.Cars.TailWayOf(car);
                if (way == CarFleet.NoWay || !ways.IsEntry(way)) continue;

                found++;
                var line = world.Cars.LineOf(car);
                var endsM = Spline.SampleAt(line, world.Cars.Line[car].LengthM).PositionM;
                var wayM = ways.ArcsOf(way);
                var wantedM = ways.IsDrivenInReverse(way)
                    ? wayM[0].StartM
                    : Spline.SampleAt(wayM, ways.LengthM(way)).PositionM;

                Assert.True(
                    (endsM - wantedM).Length() < 0.05f,
                    $"car {car} is on its final approach and its line ends {(endsM - wantedM).Length():F2} m "
                    + "from the pose the bay's own way "
                    + (ways.IsDrivenInReverse(way) ? "begins at" : "was drawn to"));
            }
        }

        Assert.True(found > 0, "no car reached its final approach in two minutes of the fixture map");
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

        // <b>Two seconds, and its own run.</b> What is asserted holds of the first legs a town lays and not
        // of every moment after them: a car whose trip has ended is carried on by the tour with nowhere to
        // go (CAR-1), which is that car driving and having no destination and is not this claim's business.
        using var world = Open(map);
        new SimLoop<TownWorld>(world, Config).Advance(120);

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
