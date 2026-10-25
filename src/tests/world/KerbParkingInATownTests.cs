using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// <b>Parking at the kerb</b> (GEN-4n): a traced town's own cars stand along its roadsides, each in a place opened where
/// it stands and held by its body's ordinary claim on the roadside — on a block of four streets, each with a roadside
/// at both kerbs, and nobody living in it.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
public class KerbParkingInATownTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary><b>Every car the map stands is stood at a kerb</b>, in a kerb place of the register (GEN-7).</summary>
    [Fact]
    public void EveryCarTheMapStandsIsAtAKerb()
    {
        using var world = new TownWorld(TownPlan.Lay(Block(), Config, ArtSizes.None), Config);

        Assert.True(world.Cars.Count > 0);
        for (var car = 0; car < world.Cars.Count; car++)
        {
            var place = world.Parking.BayOf(car);
            Assert.True(place >= 0 && world.Parking.IsKerb(place), $"car {car} stands in place {place}, no kerb's");
        }
    }

    /// <summary>
    /// <b>A car at the kerb holds the roadside it stands on by its body</b>, as any body holds the way under it
    /// (TER-4c.2): no slot is held for it, only the ground its wheels are on.
    /// </summary>
    [Fact]
    public void ACarAtTheKerbHoldsTheRoadsideByItsBody()
    {
        using var world = new TownWorld(TownPlan.Lay(Block(), Config, ArtSizes.None), Config);
        new SimLoop<TownWorld>(world, Config).Advance(1);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            var roadside = world.Parking.KerbLaneOf(world.Parking.BayOf(car));
            Assert.True(world.Occupancy.HasTheBodyOf(world.Ways.OfRoadLane(roadside), car, LaneRoster.Driving), $"car {car} holds none of its roadside");
        }
    }

    /// <summary>
    /// <b>A car at the kerb stands within its roadside</b>, its body clear of the lane beside: the traffic queues on a
    /// body that stands in its lane.
    /// </summary>
    [Fact]
    public void ACarAtTheKerbStandsWithinItsRoadside()
    {
        using var world = new TownWorld(TownPlan.Lay(Block(), Config, ArtSizes.None), Config);

        for (var car = 0; car < world.Cars.Count; car++)
        {
            var roadside = world.Parking.KerbLaneOf(world.Parking.BayOf(car));
            var arcs = world.Roads.ArcsOf(roadside);
            Spline.ProjectM(arcs, world.Cars.PositionM[car], world.Roads.LaneLengthM[roadside] * 0.5f, world.Roads.LaneLengthM[roadside], out var offSq);
            var spareM = (world.Roads.LaneWidthM[roadside] - world.Cars.BuildOf(car).WidthM) * 0.5f;
            Assert.True(MathF.Sqrt(offSq) <= spareM + LineTolerance.RoundingM, $"car {car} stands {MathF.Sqrt(offSq):F2} m off its roadside's middle, {spareM:F2} m to spare");
        }
    }

    /// <summary>
    /// <b>Two cars at one kerb stand a gap apart</b> (<see cref="RoadFigures.KerbParkingGapM"/>): no place is opened over
    /// another, nor within a gap of it.
    /// </summary>
    [Fact]
    public void TwoCarsAtOneKerbStandAGapApart()
    {
        using var world = new TownWorld(TownPlan.Lay(Block(), Config, ArtSizes.None), Config);

        for (var one = 0; one < world.Cars.Count; one++)
        {
            for (var other = one + 1; other < world.Cars.Count; other++)
            {
                var (a, b) = (world.Parking.BayOf(one), world.Parking.BayOf(other));
                if (world.Parking.KerbLaneOf(a) != world.Parking.KerbLaneOf(b)) continue;

                var apartM = MathF.Max(world.Parking.KerbFromM(b) - world.Parking.KerbToM(a), world.Parking.KerbFromM(a) - world.Parking.KerbToM(b));
                Assert.True(apartM >= Config.Road.KerbParkingGapM - LineTolerance.RoundingM, $"cars {one} and {other} stand {apartM:F2} m apart at one kerb");
            }
        }
    }

    /// <summary>
    /// <b>A car parks between two cars at a kerb by backing in</b> (GEN-4n): in a gap its own length and a
    /// <see cref="RoadFigures.KerbParkingGapM"/> either side long, which a slide in forwards cannot be made into past the
    /// car behind.
    /// </summary>
    [Fact]
    public void ACarParallelParksBetweenTwoCarsAtAKerb()
    {
        using var world = new TownWorld(TownPlan.Lay(Block(), Config, ArtSizes.None), Config);
        var parked = ParkInAGap(world);

        Assert.True(parked.Parked, $"car {Sent} stands {Vector2.Distance(world.Cars.PositionM[Sent], parked.GapM):F2} m off the gap's middle, doing {world.Cars.Action[Sent]}");
        Assert.True(parked.BackedIn, $"car {Sent} parked in the gap, and not by backing in");
    }

    /// <summary>
    /// <b>A car parked between two cars at a kerb stands square in the middle of its place</b> (GEN-4i): within
    /// <see cref="DrivingFigures.ParkedSquareWithinDeg"/> of the kerb's line and
    /// <see cref="DrivingFigures.ParkedMiddleWithinM"/> of the place's middle — pulled forward out of the gap behind it was
    /// backed into, and straightened.
    /// </summary>
    [Fact]
    public void ACarParallelParkedStandsSquareInTheMiddleOfItsPlace()
    {
        using var world = new TownWorld(TownPlan.Lay(Block(), Config, ArtSizes.None), Config);
        Assert.True(ParkInAGap(world).Parked, $"car {Sent} did not park in the gap");

        var place = world.Parking.BayOf(Sent);
        var along = Heading.Unit(world.Parking.HeadingRad(place));
        var skewRad = MathF.Abs(MathF.Asin(Spline.Cross(along, world.Cars.Forward[Sent])));
        var offAlongM = MathF.Abs(Vector2.Dot(world.Cars.PositionM[Sent] - world.Parking.CentreM(place), along));
        Assert.True(skewRad <= Config.ParkedSquareWithinRad, $"car {Sent} stands {float.RadiansToDegrees(skewRad):F1}° off its place's line");
        Assert.True(offAlongM <= Config.Driving.ParkedMiddleWithinM, $"car {Sent} stands {offAlongM:F2} m along from its place's middle");
    }

    /// <summary>The car sent into the gap, and the two put down broken either side of it.</summary>
    const int Behind = 0, Ahead = 1, Sent = 2;

    /// <summary>What a gap is longer than the least a place is opened in, so the place is opened in it and not past it.</summary>
    const float GapSlackM = 0.1f;

    /// <summary>
    /// A gap at a kerb between two cars standing broken at it, as long as <see cref="Sent"/> and a gap either side, and that
    /// car sent to park in it: <see cref="Behind"/> broken where the map stood it, and <see cref="Ahead"/> parked ahead of it
    /// and broken there. Whether the car parked in the gap, where its middle is, and whether its way in was a back in.
    /// </summary>
    static (bool Parked, Vector2 GapM, bool BackedIn) ParkInAGap(TownWorld world)
    {
        var loop = new SimLoop<TownWorld>(world, Config);
        loop.Advance(OvertakeInATownTests.WarmUpTicks);

        var cars = world.Cars;
        var place = world.Parking.BayOf(Behind);
        var arcs = world.Roads.ArcsOf(world.Parking.KerbLaneOf(place));
        var behindAtM = (world.Parking.KerbFromM(place) + world.Parking.KerbToM(place)) * 0.5f;
        var toTheKerbM = cars.PositionM[Behind] - Spline.SampleAt(arcs, behindAtM).PositionM;
        OvertakeInATownTests.PutDown(world, Behind, cars.PositionM[Behind], cars.HeadingRad[Behind]);

        var gapM = cars.BuildOf(Sent).LengthM + (2f * Config.Road.KerbParkingGapM) + GapSlackM;
        var middleAtM = behindAtM + (cars.BuildOf(Behind).LengthM * 0.5f) + (gapM * 0.5f);
        var aheadAtM = middleAtM + (gapM * 0.5f) + (cars.BuildOf(Ahead).LengthM * 0.5f);
        Assert.True(world.OrderCar(Ahead, Spline.SampleAt(arcs, aheadAtM).PositionM + toTheKerbM), $"car {Ahead} could not be sent ahead of the gap");
        for (var tick = 0; tick < ParkingTicks && !StandsParked(world, Ahead); tick++) loop.Advance(1);
        Assert.True(StandsParked(world, Ahead), $"car {Ahead} did not park ahead of the gap");
        OvertakeInATownTests.PutDown(world, Ahead, cars.PositionM[Ahead], cars.HeadingRad[Ahead]);

        var middleM = Spline.SampleAt(arcs, middleAtM).PositionM + toTheKerbM;
        Assert.True(world.OrderCar(Sent, middleM), $"car {Sent} could not be sent to the gap");
        var backedIn = false;
        for (var tick = 0; tick < ParkingTicks && !StandsParked(world, Sent); tick++)
        {
            loop.Advance(1);
            if (world.Manoeuvres.Kind[Sent] == ManoeuvreKind.Park && world.Manoeuvres.IsBegun(Sent)) backedIn = world.Manoeuvres.Shape[Sent].AnyReverse;
        }

        var inTheGap = StandsParked(world, Sent) && Vector2.Distance(cars.PositionM[Sent], middleM) <= gapM * 0.5f;
        return (inTheGap, middleM, backedIn);
    }

    static bool StandsParked(TownWorld world, int car) =>
        world.Cars.Action[car] == CarAction.Stand && world.Parking.BayOf(car) >= 0 && world.Parking.IsKerb(world.Parking.BayOf(car));

    /// <summary>Three minutes: across the block, a wait for the street and the manoeuvre into the gap.</summary>
    const int ParkingTicks = 10_800;

    /// <summary>The side of the block, in metres, and where its corner stands.</summary>
    const float SideM = 300f, CornerM = 100f;

    /// <summary>
    /// A square block of four two-way streets, each a lane each way and a roadside at both kerbs, on a map that stands
    /// twenty cars and nobody in them.
    /// </summary>
    static TownMap Block()
    {
        var sizeM = new Vector2(500, 500);
        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(-1, ZoneKind.Town, [(ZoneParam.Cars, 20f)], [TownMap.WholeOutline(sizeM)]);
        var widthM = (2f * Config.CityGen.TracedLaneWidthM) + (2f * Config.CityGen.TracedRoadsideWidthM);
        return TownMap.Bare("Block", "a block with roadsides", 12888405, sizeM, ZoneKind.Town, []) with
        {
            PointM = [new(CornerM, CornerM), new(CornerM + SideM, CornerM), new(CornerM + SideM, CornerM + SideM), new(CornerM, CornerM + SideM)],
            Roads = [Street(1, 0, 1), Street(2, 1, 2), Street(3, 2, 3), Street(4, 3, 0)],
            Zones = zones.Arrays(),
        };

        TracedRoad Street(long id, int from, int to) => new()
        {
            OsmId = id, Highway = "residential", LanesForward = 1, LanesBackward = 1, LanesShared = 0, LanesTagged = true,
            WidthM = widthM, Mended = true, RoadsideAlong = true, RoadsideAgainst = true, Points = [from, to],
        };
    }
}
