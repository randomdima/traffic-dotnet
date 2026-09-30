using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World.Parking;

/// <summary>
/// <b>A manoeuvre at a bay is claimed whole or not begun</b> (GEN-4f), staged on the suite's own city: a car
/// standing nose in, sent on a leg, with a wreck standing on the street where its way out lands.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class BayManoeuvreTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A car does not start backing out of its bay while its way out cannot be had</b>: it stands in the space
    /// rather than reversing into the street as far as the wreck lets it.
    /// </summary>
    [Fact]
    public void ACarDoesNotStartBackingOutWhileItsWayOutIsStoodOn()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (car, way, _) = SendACarOutOverAWreck(world);

        var fastestMps = 0f;
        for (var tick = 0; tick < WatchedTicks; tick++)
        {
            loop.Advance(1);
            fastestMps = MathF.Max(fastestMps, world.Cars.VelocityMps[car].Length());
        }

        Assert.True(
            fastestMps <= Config.Driving.StopSpeedMps,
            $"car {car} rolled at up to {fastestMps:F2} m/s on way {way} with its landing stood on; " +
            $"it was held by {world.Cars.GrantCutBy[car]}");
    }

    /// <summary>
    /// <b>And backs out once its way out is clear</b>: the wait is for the ground and nothing else.
    /// </summary>
    [Fact]
    public void ACarBacksOutOnceItsWayOutIsClear()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (car, way, wreck) = SendACarOutOverAWreck(world);
        loop.Advance(WatchedTicks);
        PutDown(world, wreck, FarOffTheTownM, 0f);

        for (var tick = 0; tick < WatchedTicks && world.Cars.LineWayOf(car) == way; tick++) loop.Advance(1);

        Assert.True(
            world.Cars.LineWayOf(car) != way,
            $"car {car} is still on way {way} with nothing standing on it; it was held by {world.Cars.GrantCutBy[car]}");
    }

    /// <summary>
    /// A car standing nose in, sent somewhere across the town and taking its way out backwards, and another car
    /// put down broken where that way lands.
    /// </summary>
    /// <returns>The car, the way out it has taken, and the wreck.</returns>
    static (int Car, int Way, int Wreck) SendACarOutOverAWreck(TownWorld world)
    {
        var cars = world.Cars;
        var bays = world.BayWays;
        for (var car = 0; car < cars.Count; car++)
        {
            var bay = world.Parking.BayOf(car);
            if (bay < 0 || cars.Driven[car] || cars.Broken[car] || !BacksOut(bays, bay)) continue;
            if (!BayTemplate.StandsNoseIn(world.Parking.HeadingRad(bay), cars.HeadingRad[car])) continue;
            if (!world.OrderCar(car, TheFarthestBayFrom(world, cars.PositionM[car]))) continue;

            var way = cars.LineWayOf(car);
            if (way < 0 || !bays.IsDrivenInReverse(way)) continue;

            var wreck = car == 0 ? 1 : 0;
            var landing = Spline.SampleAt(bays.ArcsOf(way), bays.DrivenLengthM(way));
            PutDown(world, wreck, landing.PositionM, landing.HeadingRad);
            return (car, way, wreck);
        }

        Assert.Fail("the city had no car standing nose in a bay it backs out of");
        return default;
    }

    /// <summary>Whether a bay lays a way out driven backwards.</summary>
    static bool BacksOut(BayWays bays, int bay)
    {
        for (var slot = 0; slot < bays.WayCountOf(bay); slot++)
        {
            var way = bays.WayOf(bay, slot);
            if (!bays.IsEntry(way) && bays.IsDrivenInReverse(way)) return true;
        }

        return false;
    }

    /// <summary>The middle of the bay furthest from a place: somewhere a leg has to leave the street it starts on for.</summary>
    static Vector2 TheFarthestBayFrom(TownWorld world, Vector2 fromM)
    {
        var farthestM = fromM;
        for (var bay = 0; bay < world.BayWays.BayCount; bay++)
        {
            var atM = world.Parking.CentreM(bay);
            if (Vector2.DistanceSquared(atM, fromM) > Vector2.DistanceSquared(farthestM, fromM)) farthestM = atM;
        }

        return farthestM;
    }

    /// <summary>A car broken where it is put down: nothing drives it, and it holds its ground as a body (TER-4c.2).</summary>
    static void PutDown(TownWorld world, int car, Vector2 atM, float headingRad)
    {
        world.Cars.Driven[car] = false;
        world.Cars.Broken[car] = true;
        world.Cars.Command[car] = DriveCommand.Locked;
        world.Cars.WheelSpinOf(car).Clear();
        world.Cars.PositionM[car] = atM;
        world.Cars.HeadingRad[car] = headingRad;
        world.Cars.VelocityMps[car] = Vector2.Zero;
        world.PhysicsForInstruments.Release(world.Cars.Body[car], atM, headingRad);
    }

    /// <summary>Long enough for every car the city stands to be in its bay and at rest.</summary>
    const int WarmUpTicks = 120;

    /// <summary>Ten seconds: long enough to have backed out several times over.</summary>
    const int WatchedTicks = 600;

    /// <summary>Somewhere no way of any town reaches, to take a wreck out of the road.</summary>
    static readonly Vector2 FarOffTheTownM = new(-10_000f, -10_000f);
}
