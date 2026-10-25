using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen.Zones;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>A town stands the people and cars its map asks for</b> (GEN-7, <see cref="SpawnStage"/>): a person at each door
/// asked for and — where nobody has a bay — a car on each lane and more on the lanes with room, and no car on top of one its bridges
/// stand — each asked of a few roads and houses laid by hand.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class RosterTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Where the tee's west arm runs, east to west along this line, with the houses on it.</summary>
    const float StreetY = 500f;

    /// <summary><b>A person stands at every door, and no more than one</b>: a thousand asked of a street of houses stand one at each door.</summary>
    [Fact]
    public void AsManyPeopleStandAsThereAreDoorsOneADoor()
    {
        var plan = Laid(people: 1000, cars: 0, Tee());

        Assert.Equal(plan.Buildings.EntryPointM.Order(ByPlace), Standing(plan, SpawnStage.Person).Order(ByPlace));
    }

    /// <summary><b>The cars asked for stand</b> where the lanes have room for every one of them: two asked of a tee stand two.</summary>
    [Fact]
    public void TheCarsAskedForStandWhereTheLanesHoldThem()
    {
        var plan = Laid(people: 0, cars: 2, Tee());

        Assert.Equal(2, Standing(plan, SpawnStage.Car).Length);
    }

    /// <summary>
    /// <b>Asked for as many cars as it has lanes, a town stands one a lane</b> (GEN-7): every car at the middle of a lane
    /// no other car stands on.
    /// </summary>
    [Fact]
    public void AskedForItsLanesATownStandsOneALane()
    {
        var plan = Laid(people: 0, cars: StandableLanes().Count, Tee());
        var lanes = plan.Paving(Config).Lanes;

        var on = Standing(plan, SpawnStage.Car).Select(carM => Enumerable.Range(0, lanes.LaneCount).Single(lane =>
            Vector2.Distance(Spline.SampleAt(lanes.ArcsOf(lane), lanes.LaneLengthM[lane] * 0.5f).PositionM, carM) < LineTolerance.RoundingM)).ToArray();

        Assert.Equal(on.Length, on.Distinct().Count());
    }

    /// <summary><b>Asked past its lanes, a town stands the rest on the lanes with room</b> (GEN-7): two a lane asked of a tee stand.</summary>
    [Fact]
    public void AskedPastItsLanesTheCarsStandWhereTheLanesHoldThem()
    {
        var asked = 2 * StandableLanes().Count;

        Assert.Equal(asked, Standing(Laid(people: 0, cars: asked, Tee()), SpawnStage.Car).Length);
    }

    /// <summary>
    /// <b>Cars stood on one lane stand a car's room apart along it</b> (GEN-7, <see cref="SpawnStage.RoomM"/>): asked for a
    /// thousand, every pair of neighbours on each of a tee's lanes.
    /// </summary>
    [Fact]
    public void CarsOnOneLaneStandACarsRoomApart()
    {
        var plan = Laid(people: 0, cars: 1000, Tee());
        var lanes = plan.Paving(Config).Lanes;
        var standable = SpawnStage.TheLanes(lanes, Config);

        var gapsM = Standing(plan, SpawnStage.Car)
            .Select(carM => standable.Select(lane => (Lane: lane, AlongM: Spline.ProjectM(lanes.ArcsOf(lane), carM, 0f, lanes.LaneLengthM[lane], out var offSq), OffSq: offSq))
                .Single(on => on.OffSq < LineTolerance.RoundingM * LineTolerance.RoundingM))
            .GroupBy(on => on.Lane)
            .SelectMany(cars =>
            {
                var alongM = cars.Select(on => on.AlongM).Order().ToArray();
                return alongM.Zip(alongM.Skip(1), (nearM, farM) => farM - nearM);
            });

        Assert.All(gapsM, gapM => Assert.True(gapM >= SpawnStage.RoomM(Config), $"two cars stand {gapM:F2} m apart along a lane"));
    }

    /// <summary>
    /// <b>No car is stood where a bridge's stands</b>: asked for a car on every lane of a street carried over another, none
    /// stands within a car's room of the cars at the bridge (<see cref="TracedBridgeCars"/>).
    /// </summary>
    [Fact]
    public void NoCarIsStoodWhereABridgesCarStands()
    {
        var plan = Laid(
            people: 0, cars: 1000,
            [100, 500, 400, 500, 600, 500, 900, 500, 500, 100, 500, 900], [],
            Street(0, 1), Street(1, 2) with { Bridge = true }, Street(2, 3), Street(4, 5));
        var bridges = TracedBridgeCars.Lay(plan.Roads, Config).PositionM;

        var nearestM = Standing(plan, SpawnStage.Car).Skip(bridges.Length).Min(carM => bridges.Min(bridgeM => Vector2.Distance(carM, bridgeM)));

        Assert.True(nearestM >= SpawnStage.RoomM(Config), $"a car stands {nearestM:F2} m from a bridge's");
    }

    /// <summary>
    /// <b>No car is stood down a street that only ends</b> (<see cref="LaneLines.LanesDrivenOn"/>): asked for a car on every
    /// lane of a block with a dead-end spur off it, every car stands on a lane it can drive on for ever from, and the
    /// spur's lane toward its end is none of them.
    /// </summary>
    [Fact]
    public void NoCarIsStoodDownAStreetThatOnlyEnds()
    {
        var plan = Laid(
            people: 0, cars: 1000, [100, 500, 500, 500, 900, 500, 900, 900, 100, 900, 500, 100], [],
            Street(0, 1), Street(1, 2), Street(2, 3), Street(3, 4), Street(4, 0), Street(1, 5));
        var lanes = plan.Paving(Config).Lanes;
        var drivenOn = lanes.LanesDrivenOn(Config);

        var towardTheEnd = Enumerable.Range(0, lanes.LaneCount)
            .Single(lane => lanes.IsAStreetLane(lane) && lanes.ConnectorAt[lane + 1] == lanes.ConnectorAt[lane]);
        Assert.False(drivenOn[towardTheEnd]);
        Assert.All(Standing(plan, SpawnStage.Car), carM => Assert.True(drivenOn[NearestLane(lanes, carM)], $"a car stands at {carM} down the spur"));
    }

    static int NearestLane(LaneLines lanes, Vector2 pointM) =>
        Enumerable.Range(0, lanes.LaneCount).Where(lanes.IsAStreetLane).MinBy(lane =>
        {
            Spline.ProjectM(lanes.ArcsOf(lane), pointM, 0f, lanes.LaneLengthM[lane], out var offSq);
            return offSq;
        });

    static readonly Comparer<Vector2> ByPlace = Comparer<Vector2>.Create((one, other) =>
        one.X != other.X ? one.X.CompareTo(other.X) : one.Y.CompareTo(other.Y));

    /// <summary>The tee's lanes a car can be stood on (<see cref="SpawnStage.TheLanes"/>).</summary>
    static List<int> StandableLanes() => SpawnStage.TheLanes(Laid(people: 0, cars: 0, Tee()).Paving(Config).Lanes, Config);

    static Vector2[] Standing(CityPlan plan, byte kind) =>
        [.. Enumerable.Range(0, plan.Spawns.Count).Where(spawn => plan.Spawns.Kind[spawn] == kind).Select(spawn => plan.Spawns.PositionM[spawn])];

    /// <summary>
    /// A tee at (500, 500): an arm west to (100, 500), one east to (900, 500) and one north to (500, 100), a lane each way,
    /// and houses along the west arm's north side.
    /// </summary>
    static (float[] PointsM, SurveyWay[] Ways, Vector2[] Houses) Tee() =>
        ([100, StreetY, 500, StreetY, 900, StreetY, 500, 100], [Street(0, 1), Street(1, 2), Street(1, 3)],
         [new(150f, 200f), new(400f, 200f), new(400f, StreetY - 6f), new(150f, StreetY - 6f)]);

    static CityPlan Laid(int people, int cars, (float[] PointsM, SurveyWay[] Ways, Vector2[] Houses) town) =>
        Laid(people, cars, town.PointsM, town.Houses, town.Ways);

    /// <param name="houses">The outline of a terrace of houses, or none.</param>
    static CityPlan Laid(int people, int cars, float[] pointsM, Vector2[] houses, params SurveyWay[] ways)
    {
        (ZoneParam, float)[] counts = [(ZoneParam.People, people), (ZoneParam.Cars, cars)];
        var survey = new Survey
        {
            Name = "Traced", Seed = 1, WidthM = 1000f, HeightM = 1000f, PointsM = pointsM, Ways = ways, Sea = [],
            Zones = houses.Length == 0 ? Zoned.Town(1000f, 1000f, counts) : Zoned.Town(1000f, 1000f, counts, Zoned.Terrace(houses)),
        };

        return TownPlan.Lay(survey, Config, new ArtSizes(Houses, PropSizes.None));
    }

    /// <summary>One house prefab, of 12 by 8 m.</summary>
    static readonly BuildingSizes Houses = new(new Vector2[BuildingSizes.Uses], [new(12f, 8f)], [0.5f], [BuildingLook.House], []);

    /// <summary>A residential way of a lane each way.</summary>
    static SurveyWay Street(params int[] points) => new()
    {
        Highway = "residential", LanesForward = 1, LanesBackward = 1, LanesShared = 0,
        CarriagewayM = 2f * OsmCarriageway.AssumedLaneWidthM, CentreOffsetM = 0f, Points = points,
    };
}
