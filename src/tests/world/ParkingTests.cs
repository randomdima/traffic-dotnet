using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The layer between the parking registry and the templates: which bay a leg claims, which lane it is
/// reached from, and whether the two templates describe a manoeuvre a car could actually make (VER-2).
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class ParkingTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static ParkingRegistry RegistryOf(string map, out RoadGraph roads)
    {
        var plan = Towns.Of(map);
        roads = RoadGraph.Build(plan, Config);
        return ParkingRegistry.Build(plan, roads, Config, cars: 8);
    }

    /// <summary>
    /// <b>VER-2, as far as this engine models it</b>: every bay of every shipped map can be driven into
    /// and backed out of. A bay that fails this is one no trip may claim, so a map with any of them is a
    /// map whose parking is quietly smaller than it looks.
    /// </summary>
    [Theory]
    [MemberData(nameof(Towns.EveryShippedMap), MemberType = typeof(Towns))]
    public void EveryBayCanBeEnteredAndLeft(string map)
    {
        var registry = RegistryOf(map, out _);

        for (var bay = 0; bay < registry.BayCount; bay++)
        {
            Assert.True(registry.CanBeEntered(bay), $"{map}: bay {bay} cannot be driven into from either lane");
            Assert.True(registry.CanBeLeft(bay), $"{map}: bay {bay} cannot be backed out of");
        }
    }

    /// <summary>
    /// The forward-in template ends <b>at the bay's own pose</b>, square to it, which is what makes the
    /// end of the line a place the car may be left rather than a place it happens to stop.
    /// </summary>
    [Fact]
    public void TheForwardTemplateEndsSquareInTheBay()
    {
        var registry = RegistryOf(Towns.Fixture, out var roads);
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayTemplate.MostArcs];

        for (var bay = 0; bay < registry.BayCount; bay++)
        {
            var lane = registry.EnterLane(bay);
            var staged = Spline.SampleAt(roads.ArcsOf(lane), registry.EnterAlongM(bay));
            var line = BayTemplate.TryLayEntry(
                Config, staged.PositionM, staged.HeadingRad, registry.CentreM(bay), registry.HeadingRad(bay), arcs);

            Assert.True(line.Any, $"bay {bay} was settled as enterable and then would not lay");

            var axleM = BayTemplate.RearAxleOfBayM(Config, registry.CentreM(bay), registry.HeadingRad(bay));
            Assert.True((line.EndM - axleM).Length() < 0.05f, $"bay {bay} ended {(line.EndM - axleM).Length():F2} m off");

            var facing = new Vector2(MathF.Cos(line.EndHeadingRad), MathF.Sin(line.EndHeadingRad));
            var bayFacing = new Vector2(MathF.Cos(registry.HeadingRad(bay)), MathF.Sin(registry.HeadingRad(bay)));
            Assert.True(Vector2.Dot(facing, bayFacing) > 0.999f, $"bay {bay} ended off square");
        }
    }

    /// <summary>
    /// And the reverse-out ends <b>on the lane, pointing along it</b> — the car's own heading, which
    /// while reversing is the opposite of the way the line is travelled.
    /// </summary>
    [Fact]
    public void TheReverseTemplateEndsOnTheLaneFacingTheWayItRuns()
    {
        var registry = RegistryOf(Towns.Fixture, out var roads);
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayTemplate.MostArcs];

        for (var bay = 0; bay < registry.BayCount; bay++)
        {
            var lane = registry.LeaveLane(bay);
            var abeam = Spline.SampleAt(roads.ArcsOf(lane), registry.LeaveAlongM(bay));
            var axleM = BayTemplate.RearAxleOfBayM(Config, registry.CentreM(bay), registry.HeadingRad(bay));
            var line = BayTemplate.TryLayExit(
                Config, axleM, registry.HeadingRad(bay), abeam.PositionM, abeam.Direction, Config.CarOffPathM, arcs);

            Assert.True(line.Any, $"bay {bay} was settled as leavable and then would not lay");

            // The end is on the lane's own line, give or take the overshoot a tight bay is allowed.
            var offM = Spline.ProjectM(roads.ArcsOf(lane), line.EndM, registry.LeaveAlongM(bay), Config.Car.LengthM * 4f);
            var onTheLaneM = (Spline.SampleAt(roads.ArcsOf(lane), offM).PositionM - line.EndM).Length();
            Assert.True(onTheLaneM <= Config.CarOffPathM, $"bay {bay} ended {onTheLaneM:F2} m off the lane");

            // Reversing, the rear axle travels against the car's heading: the car ends facing the lane.
            var travel = new Vector2(MathF.Cos(line.EndHeadingRad), MathF.Sin(line.EndHeadingRad));
            Assert.True(Vector2.Dot(-travel, abeam.Direction) > 0.99f, $"bay {bay} ended facing the wrong way");
        }
    }

    /// <summary>
    /// The registry's own contract: <b>a reservation is not an occupancy</b>, neither is free, and a bay
    /// is handed back rather than held when the leg that claimed it gives up.
    /// </summary>
    [Fact]
    public void AReservationIsNotAnOccupancyAndNeitherIsFree()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        Assert.True(registry.IsFree(0));
        Assert.True(registry.TryReserve(0, car: 1));
        Assert.False(registry.IsFree(0));
        Assert.False(registry.TryReserve(0, car: 2));
        Assert.Equal(0, registry.ReservationOf(1));
        Assert.Equal(ParkingRegistry.NoBay, registry.BayOf(1));

        registry.Occupy(0, car: 1);
        Assert.Equal(0, registry.BayOf(1));
        Assert.Equal(ParkingRegistry.NoBay, registry.ReservationOf(1));
        Assert.Equal(1, registry.OccupantOf(0));

        registry.Vacate(car: 1);
        Assert.True(registry.IsFree(0));
    }

    /// <summary>
    /// The choice layer: the bays near a place come back <b>nearest first</b>, none of them further off
    /// than the walk that was asked for, and a bay somebody else has is not one of them.
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

        // A bay that is spoken for is not offered, which is the whole reason a reservation exists.
        registry.TryReserve(found[0], car: 3);
        var again = registry.BaysNear(fromM, Config.PersonWalkWorthM, found);
        for (var slot = 0; slot < again; slot++) Assert.NotEqual(registry.ReservationOf(3), found[slot]);
    }

    /// <summary>
    /// A bay's way in is <b>a fact about the bay</b> (GEN-4e): the ground off the driver's door of a car
    /// standing squarely in it, and it does not move when anything else does.
    /// </summary>
    [Fact]
    public void TheWayInStandsOffTheDriversDoorAndDoesNotMove()
    {
        var registry = RegistryOf(Towns.Fixture, out _);

        for (var bay = 0; bay < registry.BayCount; bay++)
        {
            var offsetM = registry.WayInM(bay) - registry.CentreM(bay);
            Assert.Equal(Config.Car.WidthM * 0.5f + Config.PersonDiameterM, offsetM.Length(), 3);

            var forward = new Vector2(MathF.Cos(registry.HeadingRad(bay)), MathF.Sin(registry.HeadingRad(bay)));
            Assert.True(MathF.Abs(Vector2.Dot(Vector2.Normalize(offsetM), forward)) < 1e-3f, "the door is off the flank");
        }
    }
}
