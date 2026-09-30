using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World.Parking;

/// <summary>
/// <b>A car gets into a bay and out of one by a manoeuvre of its own, begun only where all of its ground is
/// free</b> (GEN-4f), staged on the suite's own city.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class BayManoeuvreTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>A car sent from its bay leaves it facing the way the lane it lands on runs</b>: out of the space and onto
    /// its street, where it takes that lane and drives on down it.
    /// </summary>
    [Fact]
    public void ACarSentFromItsBayLeavesItFacingDownItsLane()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (car, bay) = SendACarOut(world, noseIn: true);
        for (var tick = 0; tick < LeavingTicks && world.Cars.Line[car].LaneCount == 0; tick++) loop.Advance(1);

        var cars = world.Cars;
        Assert.True(
            cars.Line[car].LaneCount > 0 && world.Parking.BayOf(car) != bay,
            $"car {car} has not left bay {bay} in {LeavingTicks} ticks: manoeuvre {world.Manoeuvres.Stage[car]}");

        var along = Spline.SampleAt(cars.LineOf(car), cars.ProgressM[car]).Direction;
        Assert.True(
            Vector2.Dot(Heading.Unit(cars.HeadingRad[car]), along) > 0f,
            $"car {car} left bay {bay} facing against lane {cars.LaneOf(car)}");
    }

    /// <summary>
    /// <b>A car does not start out of its bay while the ground its manoeuvre sweeps is stood on</b>: it stands in
    /// the space rather than reversing into the street as far as the wreck lets it.
    /// </summary>
    [Fact]
    public void ACarDoesNotStartOutOfItsBayWhileItsGroundIsStoodOn()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (car, _, _) = SendACarOutOverAWreck(world);

        var fastestMps = 0f;
        for (var tick = 0; tick < WatchedTicks; tick++)
        {
            loop.Advance(1);
            fastestMps = MathF.Max(fastestMps, world.Cars.VelocityMps[car].Length());
        }

        Assert.True(
            fastestMps <= Config.Driving.StopSpeedMps,
            $"car {car} rolled at up to {fastestMps:F2} m/s with its landing stood on; manoeuvre " +
            $"{world.Manoeuvres.Stage[car]}");
    }

    /// <summary>
    /// <b>Nor while the town's plans are being settled round it</b>: a car waiting for its manoeuvre's ground is held
    /// where it stands by the manoeuvre and not by a plan down its line, so answering the plans again hands it
    /// nothing to drive.
    /// </summary>
    /// <remarks>
    /// Staged with every other car sent across the town, and watched until plans have been settled, because they
    /// are settled again only in a rebuild where one cut another — which a town standing in its bays never has.
    /// </remarks>
    [Fact]
    public void ACarDoesNotStartOutOfItsBayWhileTheTownsPlansAreSettled()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (car, _, wreck) = SendACarOutOverAWreck(world);
        SendTheRestAcrossTheTown(world, car, wreck);
        var settledBefore = world.PlansLaidAgain;

        var fastestMps = 0f;
        var settledAt = -1;
        for (var tick = 0; tick < SettlingTicks && (settledAt < 0 || tick < settledAt + WatchedTicks); tick++)
        {
            loop.Advance(1);
            fastestMps = MathF.Max(fastestMps, world.Cars.VelocityMps[car].Length());
            if (settledAt < 0 && world.PlansLaidAgain > settledBefore) settledAt = tick;
        }

        Assert.True(settledAt >= 0, $"staging: no plan was settled in {SettlingTicks} ticks while the car waited");
        Assert.True(
            fastestMps <= Config.Driving.StopSpeedMps,
            $"car {car} rolled at up to {fastestMps:F2} m/s with its landing stood on; manoeuvre " +
            $"{world.Manoeuvres.Stage[car]}");
    }

    /// <summary><b>And begins it once that ground is clear</b>: the wait is for the ground and nothing else.</summary>
    [Fact]
    public void ACarBeginsItsWayOutOnceItsGroundIsClear()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (car, _, wreck) = SendACarOutOverAWreck(world);
        loop.Advance(WatchedTicks);
        PutDown(world, wreck, FarOffTheTownM, 0f);

        for (var tick = 0; tick < WatchedTicks && !world.Manoeuvres.IsBegun(car); tick++) loop.Advance(1);

        Assert.True(
            world.Manoeuvres.IsBegun(car) || world.Cars.Line[car].LaneCount > 0,
            $"car {car} has not begun its way out with nothing standing on it: manoeuvre {world.Manoeuvres.Stage[car]}");
    }

    /// <summary>
    /// <b>A car sent to a bay parks in it</b>: it drives to the street the bay stands off, manoeuvres in, and is
    /// stood down standing in that bay.
    /// </summary>
    [Fact]
    public void ACarSentToABayParksInIt()
    {
        using var world = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(WarmUpTicks);

        var (car, from) = SendACarOut(world, noseIn: null);
        var bay = TheNearestFreeBayTo(world, world.Parking.CentreM(from), from);
        Assert.True(world.OrderCar(car, world.Parking.CentreM(bay)), $"car {car} could not be sent to bay {bay}");

        for (var tick = 0; tick < ParkingTicks && world.Parking.BayOf(car) != bay; tick++) loop.Advance(1);

        Assert.True(
            world.Parking.BayOf(car) == bay,
            $"car {car} sent from bay {from} to bay {bay} stands in {world.Parking.BayOf(car)} after {ParkingTicks} " +
            $"ticks: driven {world.Cars.Driven[car]}, manoeuvre {world.Manoeuvres.Kind[car]} " +
            $"{world.Manoeuvres.Stage[car]}, line {world.Cars.Line[car].LaneCount} lanes, stops for bay " +
            $"{world.Cars.StopsForBayOf(car)}, aimed at {world.Parking.ClaimedBayOf(car)}, " +
            $"{Vector2.Distance(world.Cars.PositionM[car], world.Parking.CentreM(bay)):F1} m off it, held by " +
            $"{world.Cars.GrantCutBy[car]}");
        Assert.True(world.Parking.HoldsTheBody(bay, world.Cars.PositionM[car]), $"car {car} is registered in bay {bay} and stands outside it");
    }

    /// <summary>
    /// A car standing in a bay — nose in where <paramref name="noseIn"/> asks it — sent across the town, with the
    /// first piece of its way out as its line.
    /// </summary>
    static (int Car, int Bay) SendACarOut(TownWorld world, bool? noseIn)
    {
        var cars = world.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            var bay = world.Parking.BayOf(car);
            if (bay < 0 || cars.Driven[car] || cars.Broken[car]) continue;
            if (noseIn is { } wanted && BayTemplate.StandsNoseIn(world.Parking.HeadingRad(bay), cars.HeadingRad[car]) != wanted) continue;
            if (!world.OrderCar(car, TheFarthestBayFrom(world, cars.PositionM[car]))) continue;
            if (world.Manoeuvres.Kind[car] != ManoeuvreKind.Leave) continue;

            return (car, bay);
        }

        Assert.Fail("the city had no car standing in a bay that could be sent out of it");
        return default;
    }

    /// <summary>
    /// A car standing nose in, sent out, and another car put down broken where its way out lands on the street.
    /// </summary>
    static (int Car, int Bay, int Wreck) SendACarOutOverAWreck(TownWorld world)
    {
        var (car, bay) = SendACarOut(world, noseIn: true);
        var piece = world.Manoeuvres.PieceOf(car, 0);
        var landing = Spline.SampleAt(piece, Spline.TotalLengthM(piece));
        var wreck = car == 0 ? 1 : 0;
        PutDown(world, wreck, landing.PositionM, landing.HeadingRad);
        return (car, bay, wreck);
    }

    /// <summary>
    /// Every other car standing in a bay sent to the bay furthest from it at once, so the town's streets carry traffic
    /// crossing itself at its junctions.
    /// </summary>
    static void SendTheRestAcrossTheTown(TownWorld world, int car, int wreck)
    {
        var cars = world.Cars;
        for (var other = 0; other < cars.Count; other++)
        {
            if (other == car || other == wreck || cars.Driven[other] || cars.Broken[other]) continue;
            if (world.Parking.BayOf(other) < 0) continue;

            world.OrderCar(other, TheFarthestBayFrom(world, cars.PositionM[other]));
        }
    }

    /// <summary>The middle of the bay furthest from a place: somewhere a leg has to leave the street it starts on for.</summary>
    static Vector2 TheFarthestBayFrom(TownWorld world, Vector2 fromM)
    {
        var farthestM = fromM;
        for (var bay = 0; bay < world.Parking.BayCount; bay++)
        {
            var atM = world.Parking.CentreM(bay);
            if (Vector2.DistanceSquared(atM, fromM) > Vector2.DistanceSquared(farthestM, fromM)) farthestM = atM;
        }

        return farthestM;
    }

    /// <summary>The free bay nearest a place, other than one — a bay a leg has to drive a street or two to.</summary>
    static int TheNearestFreeBayTo(TownWorld world, Vector2 nearM, int other)
    {
        var nearest = -1;
        for (var bay = 0; bay < world.Parking.BayCount; bay++)
        {
            if (bay == other || !world.Parking.IsFree(bay)) continue;
            if (nearest < 0
                || Vector2.DistanceSquared(world.Parking.CentreM(bay), nearM)
                < Vector2.DistanceSquared(world.Parking.CentreM(nearest), nearM))
            {
                nearest = bay;
            }
        }

        Assert.True(nearest >= 0, "the city has no free bay");
        return nearest;
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

    /// <summary>Three minutes: long enough for the city's cars, all sent across it at once, to cut each other's plans.</summary>
    const int SettlingTicks = 10_800;

    /// <summary>Ten seconds: long enough to have begun a manoeuvre several times over.</summary>
    const int WatchedTicks = 600;

    /// <summary>Half a minute: long enough to wait out the street and drive the whole of a way out.</summary>
    const int LeavingTicks = 1800;

    /// <summary>Two minutes: a few streets, a wait for the street and the manoeuvre into the bay.</summary>
    const int ParkingTicks = 7200;

    /// <summary>Somewhere no way of any town reaches, to take a wreck out of the road.</summary>
    static readonly Vector2 FarOffTheTownM = new(-10_000f, -10_000f);
}
