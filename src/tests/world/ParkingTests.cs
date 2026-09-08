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
/// aimed, and which bays a trip may choose from</b>. The claim on a bay is the register asserted here; the
/// ground a car takes getting to the bay is the road's and is asserted with the rest of the road.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
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
    /// <b>A bay is claimed by one leg at a time</b>: the second car to ask for it is refused, and the bay
    /// comes back the moment the first gives it up.
    /// </summary>
    [Fact]
    public void ABayIsClaimedByOneLegAtATime()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        Assert.True(registry.IsFree(0));
        Assert.True(registry.Claim(car: 1, bay: 0));
        Assert.False(registry.IsFree(0));
        Assert.False(registry.Claim(car: 2, bay: 0));
        Assert.Equal(0, registry.ClaimedBayOf(1));

        registry.Release(1);
        Assert.True(registry.IsFree(0));
        Assert.True(registry.Claim(car: 2, bay: 0));
    }

    /// <summary>
    /// <b>A car is aimed at one place at a time.</b> Claiming a second bay gives the first one back, which is
    /// what a retarget is — a place held by a car that has gone elsewhere is a place removed from the town.
    /// </summary>
    [Fact]
    public void ClaimingASecondBayGivesTheFirstOneBack()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        Assert.True(registry.Claim(car: 1, bay: 0));
        Assert.True(registry.Claim(car: 1, bay: 1));

        Assert.True(registry.IsFree(0));
        Assert.Equal(1, registry.ClaimedBayOf(1));
    }

    /// <summary>
    /// <b>The claim becomes an occupancy</b> where the leg arrives: what holds the bay from then on is the
    /// body standing in it, and it is given back where the car is driven out.
    /// </summary>
    [Fact]
    public void ArrivingTurnsTheClaimIntoAnOccupancy()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        Assert.True(registry.Claim(car: 1, bay: 0));
        registry.Occupy(bay: 0, car: 1);

        Assert.Equal(ParkingRegistry.NoBay, registry.ClaimedBayOf(1));
        Assert.Equal(0, registry.BayOf(1));
        Assert.False(registry.IsFree(0));
        Assert.False(registry.Claim(car: 2, bay: 0));

        registry.Vacate(1);
        Assert.True(registry.IsFree(0));
    }

    /// <summary>
    /// The choice layer: the bays near a place come back <b>nearest first</b>, none of them further off
    /// than the walk that was asked for, and a bay somebody has claimed is not one of them.
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
        Assert.True(registry.Claim(car: 3, bay: taken));

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

        // <b>The same stand-off at every bay, and not the expression it was laid from.</b> How far off the
        // flank a door is is half a car and a body; writing that out again would be the derivation twice
        // (VER-12), where what GEN-4e is about is that the figure is a fact about a bay rather than about
        // whatever is standing in it.
        var standOffM = float.NaN;
        for (var bay = 0; bay < registry.BayCount; bay++)
        {
            var offsetM = registry.WayInM(bay, noseIn: true) - registry.CentreM(bay);
            if (float.IsNaN(standOffM)) standOffM = offsetM.Length();
            Assert.Equal(standOffM, offsetM.Length(), 3);

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

        Assert.True(registry.Claim(car: 1, bay: 0));
        Assert.True(registry.TakeTheTurn(car: 1, bay: 1));

        Assert.Equal(0, registry.ClaimedBayOf(1));
        Assert.Equal(1, registry.TurnOf(1));
        Assert.False(registry.IsFree(1));
        Assert.False(registry.IsFreeFor(car: 2, bay: 1));
        Assert.True(registry.IsFreeFor(car: 1, bay: 1));

        // Out of it, and the bay is the town's again — with the place still claimed.
        registry.LeaveTheTurn(car: 1);
        Assert.True(registry.IsFree(1));
        Assert.Equal(ParkingRegistry.NoBay, registry.TurnOf(1));
        Assert.Equal(0, registry.ClaimedBayOf(1));
    }

    /// <summary>And a bay somebody else is turning in is one no leg may claim, which is the same one question.</summary>
    [Fact]
    public void ABayBeingTurnedInIsRefusedToEverybodyElse()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        Assert.True(registry.TakeTheTurn(car: 1, bay: 2));
        Assert.False(registry.Claim(car: 2, bay: 2));
        Assert.False(registry.TakeTheTurn(car: 2, bay: 2));

        registry.LeaveTheTurn(car: 1);
        Assert.True(registry.Claim(car: 2, bay: 2));
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
    [InlineData(Towns.City)]
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

    /// <summary>
    /// <b>A car standing in a bay holds that bay's ways and none of the street</b> (TER-4c.2, GEN-4i). It is
    /// laid from its own box like every other body in the town, and a bay's mouth stands off the carriageway,
    /// so what it covers is the bay's own ground: a parked car that cut the lane it was parked beside would
    /// stop the traffic there for as long as it stood.
    /// </summary>
    [Fact]
    public void ACarStandingInABayHoldsTheBayAndNoneOfTheStreet()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);
        new SimLoop<TownWorld>(world, Config).Advance(1);

        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        var stood = 0;
        for (var bay = 0; bay < world.BayWays.BayCount; bay++)
        {
            var car = world.Parking.CarInBay(bay);
            if (car == ParkingRegistry.Nobody || world.Cars.Driven[car]) continue;

            stood++;
            Assert.True(
                HoldsAnyOf(world, world.BayWays.WaysOf(bay), car, LaneRoster.Driving, slots),
                $"car {car} is standing in bay {bay} and holds no metre of any way that bay is worked off");
        }

        Assert.True(stood > 0, "no car in Odesa is standing in a bay");

        for (var lane = 0; lane < world.Roads.LaneCount; lane++)
        {
            var count = world.Occupancy.CopyTo(world.Ways.OfRoadLane(lane), slots);
            for (var at = 0; at < count; at++)
            {
                var held = slots[at];
                if (held.Of != LaneRoster.Driving) continue;

                var bay = world.Parking.CarInBay(world.Parking.BayOf(held.Occupant));
                Assert.False(
                    bay == held.Occupant && !world.Cars.Driven[held.Occupant],
                    $"car {held.Occupant} is standing in a bay and holds {held.FromM:0.00}–{held.ToM:0.00} m "
                    + $"of lane {lane}");
            }
        }
    }

    /// <summary>
    /// <b>And a person standing in a bay holds it on exactly those terms</b> (TER-4c.2, GEN-4f). A bay is a
    /// lane, so what is on it is a fact about the ground and never about which roster the body standing there
    /// is in — walked only over the carriageway, a body in a space claimed nothing any driver aiming at that
    /// space reads, and the town went on offering the space to park in.
    /// </summary>
    /// <remarks>
    /// <b>Stood at the back of the space, which is where the hole was.</b> A bay's way used to stop at the
    /// pose a car comes to rest in, so the deepest metres of the space — most of it, and all of the ground a
    /// nose-in car's own bonnet stands over — were on no way at all; the way now runs on to the end of the
    /// space (<see cref="BayWays.LengthM"/>), and a body anywhere in it claims it.
    /// </remarks>
    [Fact]
    public void APersonStandingInABayHoldsThatBaysWays()
    {
        using var world = new TownWorld(Towns.Of(Towns.City), Config);

        // Long enough for the town to have put somebody on the street: at the first tick the whole roster is
        // still indoors, and a body inside a container is no body at all (PHY-7).
        new SimLoop<TownWorld>(world, Config).Advance(600);

        var bay = ABayNobodyIsIn(world);
        Assert.True(bay >= 0, "no bay in Odesa is standing empty");

        // Somebody actually in the world, off its own walk: a body inside a container is no body at all
        // (PHY-7), and one walking a crossing takes that crossing's bands instead of the ground under it.
        var person = SomebodyOutside(world);
        Assert.True(person >= 0, "a town of walkers had nobody standing outside a building");

        var headingRad = world.Parking.HeadingRad(bay);
        world.People.Walking[person] = false;
        world.People.PositionM[person] = world.Parking.CentreM(bay)
                                         + (Heading.Unit(headingRad) * (Config.ParkingSpaceLengthM * 0.5f))
                                         - (Heading.Unit(headingRad) * world.People.RadiusM[person]);
        world.People.VelocityMps[person] = Vector2.Zero;
        world.RebuildProximityIndex();

        Span<LaneClaim> slots = stackalloc LaneClaim[64];
        Assert.True(
            HoldsAnyOf(world, world.BayWays.WaysOf(bay), person, LaneRoster.Walking, slots),
            $"person {person} is standing in bay {bay} and holds no metre of any way it is worked off");
    }

    /// <summary>A bay with no car registered in it, so what is claimed of it afterwards is the walker's.</summary>
    static int ABayNobodyIsIn(TownWorld world)
    {
        for (var bay = 0; bay < world.BayWays.BayCount; bay++)
        {
            if (world.Parking.CarInBay(bay) == ParkingRegistry.Nobody) return bay;
        }

        return -1;
    }

    static int SomebodyOutside(TownWorld world)
    {
        for (var person = 0; person < world.People.Count; person++)
        {
            if (!world.People.Inside[person].Any) return person;
        }

        return -1;
    }

    /// <summary>Whether one occupant has a stretch on any of these ways, which is the claims' own answer and not a size.</summary>
    static bool HoldsAnyOf(
        TownWorld world, ReadOnlySpan<int> ways, int occupant, LaneRoster of, Span<LaneClaim> slots)
    {
        foreach (var way in ways)
        {
            var count = world.Occupancy.CopyTo(way, slots);
            for (var at = 0; at < count; at++)
            {
                if (slots[at].Occupant == occupant && slots[at].Of == of) return true;
            }
        }

        return false;
    }

    /// <summary>The bar the road holds a car to before it calls the line lost (CAR-10a), which is where the fault shows.</summary>
    const float OffLineTolerance = 2f;

    /// <summary>Long enough for a leg to reach a bay's own frontage on either city, and short enough to be a town-tier test.</summary>
    const int TicksWatched = 3600;
}
