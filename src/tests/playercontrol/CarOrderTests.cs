using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.PlayerControl;

/// <summary>
/// <b>CTL-8: what a right-click on the town does to a car.</b> Four orders, told apart by what the
/// pointer was over and by nothing else — so what is asserted here is first that the click is read as
/// the right one, and then that the leg it begins is an ordinary leg of the town's own machinery.
/// </summary>
/// <remarks>
/// Staged on the fixture map because these are questions about a whole town — a road to align to, a car
/// park to aim at, and a car standing in it with nobody in it (CTL-8d).
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class CarOrderTests
{
    static readonly SimConfig Figures = SimConfig.Shipped();

    static TownWorld Open() => new(Towns.Of(Towns.Fixture), Figures, standStatics: false);

    /// <summary>A car standing in a bay with nobody in it, which is what a town looks like on its first tick.</summary>
    static int AnEmptyParkedCar(TownWorld world, int other = -1)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (car == other || world.Cars.Broken[car] || world.Cars.Driven[car]) continue;
            if (!world.Containment.IsFree(car) || world.Parking.BayOf(car) < 0) continue;

            return car;
        }

        Assert.Fail("the fixture map stands no empty parked car");
        return -1;
    }

    /// <summary>A place on the centreline of the lane beside a point, a set distance further along it.</summary>
    static Vector2 APlaceOnTheRoadNear(TownWorld world, Vector2 fromM, float aheadM)
    {
        var lane = world.Roads.NearestLane(fromM, out var alongM);
        Assert.True(lane >= 0, "no lane near the car");

        var arcs = world.Roads.ArcsOf(lane);
        var lengthM = world.Roads.LaneLengthM[lane];
        return Spline.SampleAt(arcs, Math.Clamp(alongM + aheadM, 0f, lengthM)).PositionM;
    }

    /// <summary>
    /// CTL-8a and CTL-8d: a click on the carriageway sends the car to that place and stands it along the
    /// lane that reaches it — and a car with nobody in it takes the order like any other.
    /// </summary>
    [Fact]
    public void AClickOnTheRoadDrivesAnEmptyCarThereAndStandsItInTheLane()
    {
        using var world = Open();
        var loop = new SimLoop<TownWorld>(world, Figures);

        var car = AnEmptyParkedCar(world);
        var toM = APlaceOnTheRoadNear(world, world.Cars.PositionM[car], 30f);
        Assert.True(world.Terrain.At(toM).Drivable, "the place picked is not on ground a car may drive on");

        Assert.True(world.OrderCar(car, toM));
        Assert.Equal(PlayerOrder.DriveThere, world.OrderOf(car));

        // CAR-1 makes a driverless car furniture because nothing is choosing for it; a hand giving it
        // goals is exactly that choice, so the leg begins.
        Assert.True(world.Cars.Driven[car], "an empty car under orders is being driven");

        loop.Advance(3600);

        Assert.True(
            (world.Cars.PositionM[car] - toM).Length() <= Figures.OrderedPlaceReachM,
            $"ordered to {toM}, ended at {world.Cars.PositionM[car]}");

        // Aligned to the lane, which is the line and never a correction applied after it: the car came to
        // rest driving the lane that reaches the place, so its heading is that lane's.
        var lane = world.Roads.NearestLane(toM, out var alongM);
        var along = Spline.SampleAt(world.Roads.ArcsOf(lane), alongM).Direction;
        var facing = Heading.Unit(world.Cars.HeadingRad[car]);
        Assert.True(MathF.Abs(Vector2.Dot(along, facing)) > 0.9f, "the car did not come to rest along the lane");

        // CTL-4: the order is carried out, the car stands down, and it idles awaiting the next one rather
        // than drawing a goal of its own.
        Assert.False(world.Cars.Driven[car]);
        Assert.Equal(PlayerOrder.None, world.OrderOf(car));
        Assert.True(world.IsUnderOrders(car));
    }

    /// <summary>
    /// CTL-8b: a click on a car park is an order to park in it — the bay clicked is booked and the leg to
    /// it is an ordinary drive leg, so the car sets off and the order is finished when that leg ends.
    /// </summary>
    /// <remarks>
    /// <b>Whether the bay is reached is not this test's question</b> and cannot be: a leg that ends in the
    /// ladder rather than in the bay is exactly what CTL-4 says an order that failed does, and what the
    /// town's parking is worth is [world/parking]'s to assert.
    /// </remarks>
    [Fact]
    public void AClickOnACarParkBooksThatBayAndSetsTheCarOff()
    {
        using var world = Open();
        var loop = new SimLoop<TownWorld>(world, Figures);

        var car = AnEmptyParkedCar(world);
        var toM = AFreeBayOtherThan(world, world.Parking.BayOf(car), out var bay);
        var from = world.Cars.PositionM[car];

        Assert.Equal(Ground.Parking, world.Terrain.GroundAt(toM));
        Assert.True(world.OrderCar(car, toM));
        Assert.Equal(PlayerOrder.ParkThere, world.OrderOf(car));
        Assert.Equal(bay, world.Parking.BookingOf(car));
        Assert.True(world.Cars.Driven[car]);

        loop.Advance(1800);

        Assert.True((world.Cars.PositionM[car] - from).Length() > Figures.Car.LengthM, "the car never set off");

        loop.Advance(10800);

        // CTL-4: however that leg ended, the order is over and the car idles awaiting the next one.
        Assert.False(world.Cars.Driven[car]);
        Assert.Equal(PlayerOrder.None, world.OrderOf(car));
        Assert.True(world.IsUnderOrders(car));
    }

    /// <summary>The centre of the free bay nearest the car that is not the one it is already standing in.</summary>
    static Vector2 AFreeBayOtherThan(TownWorld world, int standingIn, out int bay)
    {
        bay = -1;
        for (var candidate = 0; candidate < world.Parking.BayCount; candidate++)
        {
            if (candidate == standingIn || !world.Parking.IsFree(candidate)) continue;

            bay = candidate;
            return world.Parking.CentreM(candidate);
        }

        Assert.Fail("the fixture map has no second free bay");
        return default;
    }

    /// <summary>
    /// CTL-8c: a click on another car is an order to follow it — the leg is aimed a gap back along the
    /// road from that car rather than at a bay, and it is re-aimed as that car moves.
    /// </summary>
    /// <remarks>
    /// <b>What is asserted is the goal and not the chase</b>: whether the follower catches up is the
    /// road's answer and depends on the traffic between them, while what this order owes is that the place
    /// it is aimed at keeps station on the car in front (S-2a holds the gap once it is there).
    /// </remarks>
    [Fact]
    public void AClickOnAnotherCarFollowsItAndKeepsAimingAtIt()
    {
        using var world = Open();
        var loop = new SimLoop<TownWorld>(world, Figures);

        var lead = AnEmptyParkedCar(world);
        var car = AnEmptyParkedCar(world, lead);

        Assert.True(world.OrderCar(car, world.Cars.PositionM[lead]));
        Assert.Equal(PlayerOrder.FollowThatCar, world.OrderOf(car));
        Assert.Equal(lead, world.OrderedAfter(car));
        Assert.True(
            (world.Cars.DestinationM[car] - world.Cars.PositionM[lead]).Length() > Figures.Car.LengthM,
            "a leg aimed at where the leader is standing is a leg that ends inside it");

        // Send the leader away, so that what the follower is aimed at is somewhere the leader was not
        // when the order was given.
        var wasM = world.Cars.PositionM[lead];
        Assert.True(world.OrderCar(lead, APlaceOnTheRoadNear(world, wasM, 120f)));

        loop.Advance(1200);

        Assert.True((world.Cars.PositionM[lead] - wasM).Length() > Figures.Car.LengthM * 4f, "the leader never left");
        Assert.True((world.Cars.DestinationM[car] - Behind(world, lead)).Length() <= Figures.OrderedFollowRedrawM * 2f,
            $"still aimed at {world.Cars.DestinationM[car]} with the leader at {world.Cars.PositionM[lead]}");

        // A standing order, unlike the other three: it is still in hand however many legs have served it.
        Assert.Equal(PlayerOrder.FollowThatCar, world.OrderOf(car));
        Assert.True(world.Cars.Driven[car]);
    }

    /// <summary>The place a follower is sent: a gap back along the road the car in front is pointing down.</summary>
    static Vector2 Behind(TownWorld world, int lead) =>
        world.Cars.PositionM[lead] - (Heading.Unit(world.Cars.HeadingRad[lead]) * Figures.OrderedFollowGapM);

    /// <summary>
    /// CTL-8b: a click on ground no car can be driven to is a park near it and a walk the rest of the
    /// way — so the order is read as one, and the leg it begins is aimed at a bay.
    /// </summary>
    [Fact]
    public void AClickOffTheRoadParksNearestAndLeavesTheRestToBeWalked()
    {
        using var world = Open();

        var car = AnEmptyParkedCar(world);
        var toM = SomewhereNoCarCanGo(world, world.Cars.PositionM[car]);

        Assert.True(world.OrderCar(car, toM));
        Assert.Equal(PlayerOrder.ParkAndWalkThere, world.OrderOf(car));
        Assert.True(world.Parking.BookingOf(car) >= 0, "a park-and-walk order books the bay it is going to");
    }

    /// <summary>The nearest cell to a place that a car may not drive on — a pavement, a lawn, a building's own ground.</summary>
    static Vector2 SomewhereNoCarCanGo(TownWorld world, Vector2 fromM)
    {
        for (var stepM = 2f; stepM < 120f; stepM += 1f)
        {
            foreach (var way in new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY })
            {
                var atM = fromM + (way * stepM);
                if (world.Terrain.Contains(atM) && !world.Terrain.At(atM).Drivable) return atM;
            }
        }

        Assert.Fail("the fixture map is drivable everywhere near this car");
        return default;
    }

    /// <summary>CTL-4: the reset hands an ordered car back to whatever it would have been doing.</summary>
    [Fact]
    public void TheResetHandsAnOrderedCarBack()
    {
        using var world = Open();

        var car = AnEmptyParkedCar(world);
        world.Select(new Selection(SelectionKind.Car, car));
        Assert.True(world.OrderCar(car, APlaceOnTheRoadNear(world, world.Cars.PositionM[car], 20f)));
        Assert.True(world.IsUnderOrders(car));

        world.ReleaseHands();

        Assert.False(world.IsUnderOrders(car));
        Assert.Equal(PlayerOrder.None, world.OrderOf(car));

        // CAR-1: the hand was the whole of what was choosing for a car nobody is in, so it is stood down
        // rather than left to finish the last goal it was given.
        Assert.False(world.Cars.Driven[car]);
    }

    /// <summary>A wreck takes no orders, which is CTL-4's terminal rule said of a car.</summary>
    [Fact]
    public void AWreckTakesNoOrders()
    {
        using var world = Open();

        var car = AnEmptyParkedCar(world);
        world.Cars.Broken[car] = true;

        Assert.False(world.OrderCar(car, APlaceOnTheRoadNear(world, world.Cars.PositionM[car], 20f)));
        Assert.False(world.IsUnderOrders(car));
    }
}
