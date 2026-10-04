using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen.Traced;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>A car moving across onto the lane beside running its way</b> (CAR-53), staged on a street of two lanes into a tee
/// (<see cref="TracedPlanTests.TwoLanesIntoATee"/>): the car the town stands on its kerb lane, sent where only the inner
/// lane goes, put on the inner lane and sent on, or held up by a wreck.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P2)]
public class LaneSwitchInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A car turns off the lane beside the one it is on by moving across onto it first</b>: sent up an arm turned
    /// onto from the inner lane alone, the car on the kerb lane gets up it — the street runs on to a dead end, so there
    /// is no way round.
    /// </summary>
    [Fact]
    public void ACarMovesAcrossOntoTheLaneItsTurnIsMadeFrom()
    {
        using var world = new TownWorld(TracedPlanTests.TwoLanesIntoATee(), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var car = TheCarOnTheStreet(world);
        Assert.True(world.OrderCar(car, UpTheArmM), $"car {car} took no order up the arm");

        for (var tick = 0; tick < WatchedTicks && !IsUpTheArm(world, car); tick++) loop.Advance(1);

        Assert.True(
            IsUpTheArm(world, car),
            $"car {car} never got up the arm; it is on lane {world.Cars.LaneOf(car)}, {world.Cars.Action[car]}, "
            + $"held by {world.Cars.Hold[car]} at {world.Cars.PositionM[car]}");
    }

    /// <summary>
    /// <b>A car keeps to the kerb lane where nothing has it in another</b>: put on the inner lane and sent along the
    /// street past the tee, to where it runs on in one lane, it moves across onto the kerb lane on the way.
    /// </summary>
    [Fact]
    public void ACarOnTheInnerLaneMovesAcrossToTheKerb()
    {
        using var world = new TownWorld(TracedPlanTests.TwoLanesIntoATee(), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var car = TheCarOnTheStreet(world);
        PutOnTheLaneBeside(world, car);
        Assert.True(world.OrderCar(car, OnTheOneLaneM), $"car {car} took no order along the street");

        var onTheKerb = false;
        for (var tick = 0; tick < WatchedTicks && !onTheKerb && world.Cars.PositionM[car].X < TracedPlanTests.TeeOneLaneM.X; tick++)
        {
            loop.Advance(1);
            var lane = world.Cars.LaneOf(car);
            onTheKerb = lane >= 0 && world.Roads.LaneFromKerb[lane] == 0 && world.Roads.LaneBeside(lane, inward: true) >= 0;
        }

        Assert.True(onTheKerb, $"car {car} came to {world.Cars.PositionM[car]} on lane {world.Cars.LaneOf(car)} and never onto a kerb lane");
    }

    /// <summary>
    /// <b>A car gets past what stands in its lane over a lane of its own way, and never over the oncoming one</b>, on a
    /// road of more than a lane each way (CAR-6.2b): with a wreck on the kerb lane ahead of it on a street of two lanes east
    /// and one west, the car sent along the street gets past it without once crossing to the lane running west.
    /// </summary>
    [Fact]
    public void ACarGetsPastAWreckOverTheLaneBesideAndNotTheOncomingOne()
    {
        using var world = new TownWorld(TracedPlanTests.TwoLanesIntoATee(againstWest: 1), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var car = TheCarOnTheStreet(world);
        var wreck = TheCarOnTheStreet(world, eastward: false);
        var kerbLane = world.Roads.NearestStreetLane(world.Cars.PositionM[car], out _);
        var at = Spline.SampleAt(world.Roads.ArcsOf(kerbLane), world.Roads.LaneLengthM[kerbLane] * WreckAlongShare);
        OvertakeInATownTests.PutDown(world, wreck, at.PositionM, at.HeadingRad);
        Assert.True(world.OrderCar(car, OnTheOneLaneM), $"car {car} took no order along the street");

        var crossed = false;
        var pastM = at.PositionM.X + world.Cars.BuildOf(wreck).LengthM;
        for (var tick = 0; tick < WatchedTicks && world.Cars.PositionM[car].X < pastM; tick++)
        {
            loop.Advance(1);
            crossed |= world.Cars.Action[car] == CarAction.Overtake;
        }

        Assert.True(
            world.Cars.PositionM[car].X >= pastM && !crossed,
            $"car {car} came to {world.Cars.PositionM[car]} past a wreck at {at.PositionM}, crossing the line {crossed}");
    }

    /// <summary>
    /// <b>A car moves across to get past traffic going slower than it means to</b>, on a road of more than a lane each
    /// way: a car held to a crawl ahead of it on the kerb lane, both sent along the street, is got past.
    /// </summary>
    [Fact]
    public void ACarGetsPastSlowerTrafficOverTheLaneBeside()
    {
        using var world = new TownWorld(TracedPlanTests.TwoLanesIntoATee(againstWest: 1), Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        var car = TheCarOnTheStreet(world);
        var slow = TheCarOnTheStreet(world, eastward: false);
        var kerbLane = world.Roads.NearestStreetLane(world.Cars.PositionM[car], out _);
        var at = Spline.SampleAt(world.Roads.ArcsOf(kerbLane), world.Roads.LaneLengthM[kerbLane] * SlowAlongShare);
        world.Cars.PositionM[slow] = at.PositionM;
        world.Cars.HeadingRad[slow] = at.HeadingRad;
        world.PhysicsForInstruments.Release(world.Cars.Body[slow], at.PositionM, at.HeadingRad);
        world.Cars.PaceMps[slow] = CrawlMps;
        Assert.True(world.OrderCar(slow, OnTheOneLaneM) && world.OrderCar(car, OnTheOneLaneM), "the two cars took no orders along the street");

        var cars = world.Cars;
        for (var tick = 0; tick < WatchedTicks && cars.PositionM[car].X <= cars.PositionM[slow].X
                           && cars.PositionM[slow].X < TracedPlanTests.TeeOneLaneM.X; tick++)
        {
            loop.Advance(1);
        }

        Assert.True(
            cars.PositionM[car].X > cars.PositionM[slow].X,
            $"car {car} at {cars.PositionM[car]} never got past car {slow} crawling at {cars.PositionM[slow]}");
    }

    /// <summary>The car the town stands on the street short of the bridge over it, running east — or west, where asked.</summary>
    static int TheCarOnTheStreet(TownWorld world, bool eastward = true)
    {
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var atM = world.Cars.PositionM[car];
            if (world.Cars.Level[car] == CityPlan.RoadArrays.Ground && MathF.Abs(atM.Y - TracedPlanTests.TeeOneLaneM.Y) < OnTheStreetM
                && MathF.Cos(world.Cars.HeadingRad[car]) > 0f == eastward)
            {
                return car;
            }
        }

        throw new InvalidOperationException("the town stood no car on the street that way");
    }

    /// <summary>A car moved across onto the lane beside the one it stands on, toward the line, where it stands abeam.</summary>
    static void PutOnTheLaneBeside(TownWorld world, int car)
    {
        var roads = world.Roads;
        var lane = roads.NearestStreetLane(world.Cars.PositionM[car], out var alongM);
        var at = Spline.SampleAt(roads.ArcsOf(roads.LaneBeside(lane, inward: true)), alongM);
        world.Cars.PositionM[car] = at.PositionM;
        world.Cars.HeadingRad[car] = at.HeadingRad;
        world.PhysicsForInstruments.Release(world.Cars.Body[car], at.PositionM, at.HeadingRad);
    }

    /// <summary>Whether a car is on the north arm's lane running away from the tee: the one lane of the town that runs north.</summary>
    static bool IsUpTheArm(TownWorld world, int car)
    {
        var lane = world.Cars.LaneOf(car);
        return lane >= 0
               && world.Roads.StartOf(lane).PositionM.Y - world.Roads.EndOf(lane).PositionM.Y > OnTheStreetM
               && MathF.Abs(world.Roads.EndOf(lane).PositionM.X - TracedPlanTests.TeeNorthM.X) < OnTheStreetM;
    }

    /// <summary>Where the car is sent: a place up the north arm.</summary>
    static readonly Vector2 UpTheArmM = new(900f, 300f);

    /// <summary>Or a place on the street where it runs on in one lane.</summary>
    static readonly Vector2 OnTheOneLaneM = new(1500f, 500f);

    /// <summary>How far down the kerb lane a wreck is put down: well ahead of the car, and well short of the tee.</summary>
    const float WreckAlongShare = 0.5f;

    /// <summary>How far down it a slow car is put: ahead of the car, with the street before the tee to get past it in.</summary>
    const float SlowAlongShare = 0.3f;

    /// <summary>A walking pace, far under the street's.</summary>
    const float CrawlMps = 2f;

    /// <summary>How far off a street's own line a car on it stands: inside its carriageway.</summary>
    const float OnTheStreetM = 10f;

    /// <summary>Two minutes: a kilometre of street at a town's pace, with a wait for the lane beside.</summary>
    const int WatchedTicks = 7_200;
}
