using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// What the parking slice is once the ways are the town's: <b>where a bay stands, where a walk to it is
/// aimed, and which bays a trip may choose from</b>. The booking is the register asserted here; the ground
/// a car takes getting to the bay is the road's book and is asserted with the rest of the road.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class ParkingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static ParkingRegistry RegistryOf(string map, out BayWays ways)
    {
        var plan = Towns.Of(map);
        var roads = RoadGraph.Build(plan, Config);
        ways = BayWays.Build(plan, roads, Config);
        return ParkingRegistry.Build(plan, ways, Config, cars: 8);
    }

    /// <summary>
    /// <b>A bay is booked by one leg at a time</b>: the second car to ask for it is refused, and the bay
    /// comes back the moment the first gives it up.
    /// </summary>
    [Fact]
    public void ABayIsBookedByOneLegAtATime()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        Assert.True(registry.IsFree(0));
        Assert.True(registry.Book(car: 1, bay: 0));
        Assert.False(registry.IsFree(0));
        Assert.False(registry.Book(car: 2, bay: 0));
        Assert.Equal(0, registry.BookingOf(1));

        registry.Release(1);
        Assert.True(registry.IsFree(0));
        Assert.True(registry.Book(car: 2, bay: 0));
    }

    /// <summary>
    /// <b>A car is aimed at one place at a time.</b> Booking a second bay gives the first one back, which is
    /// what a retarget is — a place held by a car that has gone elsewhere is a place removed from the town.
    /// </summary>
    [Fact]
    public void BookingASecondBayGivesTheFirstOneBack()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        Assert.True(registry.Book(car: 1, bay: 0));
        Assert.True(registry.Book(car: 1, bay: 1));

        Assert.True(registry.IsFree(0));
        Assert.Equal(1, registry.BookingOf(1));
    }

    /// <summary>
    /// <b>The booking becomes an occupancy</b> where the leg arrives: what holds the bay from then on is the
    /// body standing in it, and it is given back where the car is driven out.
    /// </summary>
    [Fact]
    public void ArrivingTurnsTheBookingIntoAnOccupancy()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        Assert.True(registry.Book(car: 1, bay: 0));
        registry.Occupy(bay: 0, car: 1);

        Assert.Equal(ParkingRegistry.NoBay, registry.BookingOf(1));
        Assert.Equal(0, registry.BayOf(1));
        Assert.False(registry.IsFree(0));
        Assert.False(registry.Book(car: 2, bay: 0));

        registry.Vacate(1);
        Assert.True(registry.IsFree(0));
    }

    /// <summary>
    /// The choice layer: the bays near a place come back <b>nearest first</b>, none of them further off
    /// than the walk that was asked for, and a bay somebody has booked is not one of them.
    /// </summary>
    [Fact]
    public void TheBaysNearAPlaceComeBackNearestFirstAndInsideTheWalk()
    {
        var registry = RegistryOf(Towns.Fixture, out _);
        var fromM = registry.CentreM(0);
        Span<int> found = stackalloc int[4];

        var count = registry.BaysNear(fromM, Config.PersonWalkWorthM, found);
        Assert.True(count > 0, "the fixture map has bays within a walk of its own first bay");

        var lastM = 0f;
        for (var slot = 0; slot < count; slot++)
        {
            var farM = (registry.CentreM(found[slot]) - fromM).Length();
            Assert.True(farM >= lastM, "the bays came back out of order");
            Assert.True(farM <= Config.PersonWalkWorthM);
            lastM = farM;
        }

        var taken = found[0];
        Assert.True(registry.Book(car: 3, bay: taken));

        var again = registry.BaysNear(fromM, Config.PersonWalkWorthM, found);
        for (var slot = 0; slot < again; slot++) Assert.NotEqual(taken, found[slot]);
    }

    /// <summary>
    /// <b>The index is the whole of the search.</b> Every bay inside the walk that the register says is free
    /// is one the query can reach, however the buckets fall — a bay dropped here is a place the town has
    /// and no trip can find.
    /// </summary>
    [Fact]
    public void TheIndexFindsEveryFreeBayInsideTheWalk()
    {
        var registry = RegistryOf(Towns.Fixture, out _);
        var fromM = registry.CentreM(0);
        var withinM = Config.PersonWalkWorthM;

        var expected = 0;
        for (var bay = 0; bay < registry.BayCount; bay++)
        {
            if (registry.IsFree(bay) && Vector2.Distance(registry.CentreM(bay), fromM) <= withinM) expected++;
        }

        Span<int> found = new int[registry.BayCount];
        Assert.Equal(expected, registry.BaysNear(fromM, withinM, found));
    }

    /// <summary>
    /// A bay's way in is <b>a fact about the bay</b> (GEN-4e): the ground off the driver's door of a car
    /// standing squarely in the middle of it (GEN-4i), and it does not move when anything else does.
    /// <b>Which flank that is is the standing's</b> (GEN-4j), and the two are the same distance out on
    /// opposite sides, because a car backed in is the same body turned about the middle of the space.
    /// </summary>
    [Fact]
    public void TheWayInStandsOffTheDriversDoorAndDoesNotMove()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        for (var bay = 0; bay < registry.BayCount; bay++)
        {
            var offsetM = registry.WayInM(bay, noseIn: true) - registry.CentreM(bay);
            Assert.Equal((Config.Car.WidthM * 0.5f) + Config.PersonDiameterM, offsetM.Length(), 3);

            var forward = new Vector2(MathF.Cos(registry.HeadingRad(bay)), MathF.Sin(registry.HeadingRad(bay)));
            Assert.True(MathF.Abs(Vector2.Dot(Vector2.Normalize(offsetM), forward)) < 1e-3f, "the door is off the flank");

            var backedInM = registry.WayInM(bay, noseIn: false) - registry.CentreM(bay);
            Assert.Equal(-offsetM.X, backedInM.X, 3);
            Assert.Equal(-offsetM.Y, backedInM.Y, 3);
        }
    }

    /// <summary>
    /// <b>A bay held for a turn is nobody else's</b> (GEN-4l), and the leg keeps the place it is going to
    /// while it turns: the two holds are separate registers because the destination has not changed — only
    /// the way round to it.
    /// </summary>
    [Fact]
    public void ATurnHoldsItsOwnBayAndKeepsTheLegsPlace()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        Assert.True(registry.Book(car: 1, bay: 0));
        Assert.True(registry.TakeTheTurn(car: 1, bay: 1));

        Assert.Equal(0, registry.BookingOf(1));
        Assert.Equal(1, registry.TurnOf(1));
        Assert.False(registry.IsFree(1));
        Assert.False(registry.IsFreeFor(car: 2, bay: 1));
        Assert.True(registry.IsFreeFor(car: 1, bay: 1));

        // Out of it, and the bay is the town's again — with the place still booked.
        registry.LeaveTheTurn(car: 1);
        Assert.True(registry.IsFree(1));
        Assert.Equal(ParkingRegistry.NoBay, registry.TurnOf(1));
        Assert.Equal(0, registry.BookingOf(1));
    }

    /// <summary>And a bay somebody else is turning in is one no leg may book, which is the same one question.</summary>
    [Fact]
    public void ABayBeingTurnedInIsRefusedToEverybodyElse()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        Assert.True(registry.TakeTheTurn(car: 1, bay: 2));
        Assert.False(registry.Book(car: 2, bay: 2));
        Assert.False(registry.TakeTheTurn(car: 2, bay: 2));

        registry.LeaveTheTurn(car: 1);
        Assert.True(registry.Book(car: 2, bay: 2));
    }

    /// <summary>
    /// <b>The way into a bay is never threaded onto a line behind the body that is to drive it</b> (GEN-4l).
    /// A way leaves its lane part-way along it, so a leg that has driven past that point has overshot its own
    /// turn-in; laid on regardless, the last dozen metres of the line run off the road behind the car, the
    /// follower calls the line lost (CAR-10a) and every re-laying hands back the same answer — a car standing
    /// still in a clear lane for the rest of the run.
    /// </summary>
    /// <remarks>
    /// <b>Asked as the line's own relation to the body</b>, because that is the state the fault leaves behind
    /// and the one a car driving into its bay is not in: a leg following the way in is on its line, and one
    /// holding a way it has driven past is a body's length or more off it. Asked of a city while it runs,
    /// since what it takes is a car that did not follow its own turn-in — a fact about traffic rather than one
    /// a fixture can be posed into.
    /// </remarks>
    [Theory]
    [InlineData("Odesa")]
    [InlineData("River")]
    public void NoCarHoldsALineIntoABayItHasDrivenPast(string map)
    {
        using var world = new TownWorld(Towns.Of(map), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var allowedM = Config.CarOffPathM * OffLineTolerance;

        for (var tick = 0; tick < TicksWatched; tick++)
        {
            loop.Advance();
            for (var car = 0; car < world.Cars.Count; car++)
            {
                if (world.Cars.TailWay[car] < 0 || world.Cars.Line[car].LaneCount == 0) continue;

                Assert.True(
                    world.Cars.OffLineM[car] <= allowedM,
                    $"car {car} is {world.Cars.OffLineM[car]:0.0} m off a line finishing at a bay it cannot reach");
            }
        }
    }

    /// <summary>The bar the road holds a car to before it calls the line lost (CAR-10a), which is where the fault shows.</summary>
    const float OffLineTolerance = 2f;

    /// <summary>Long enough for a leg to reach a bay's own frontage on either city, and short enough to be a town-tier test.</summary>
    const int TicksWatched = 3600;
}
