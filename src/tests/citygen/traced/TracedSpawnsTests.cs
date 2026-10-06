using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A traced town stands the people and cars its map asks for</b> (GEN-7, GEN-57, <see cref="TracedSpawns"/>): a
/// person at each door asked for and a car on each lane, as many as fit, and no car on top of one its bridges stand —
/// each asked of a few ways and houses laid by hand.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class TracedSpawnsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Where the tee's west arm runs, east to west along this line, with the houses on it.</summary>
    const float StreetY = 500f;

    /// <summary><b>A person stands at every door, and no more than one</b>: five asked of two houses stand two, one at each door.</summary>
    [Fact]
    public void AsManyPeopleStandAsThereAreDoorsOneADoor()
    {
        var plan = Laid(new TracedPopulation(People: 5, Cars: 0), Tee());

        Assert.Equal(plan.Buildings.EntryPointM.Order(ByPlace), Standing(plan, SpawnStage.Person).Order(ByPlace));
    }

    /// <summary><b>The cars asked for stand</b> where the lanes have room for every one of them: two asked of a tee stand two.</summary>
    [Fact]
    public void TheCarsAskedForStandWhereTheLanesHoldThem()
    {
        var plan = Laid(new TracedPopulation(People: 0, Cars: 2), Tee());

        Assert.Equal(2, Standing(plan, SpawnStage.Car).Length);
    }

    /// <summary>
    /// <b>Asked for more cars than its lanes hold, a town stands one a lane</b> (GEN-8): every car at the middle of a lane
    /// no other car stands on.
    /// </summary>
    [Fact]
    public void AskedPastItsLanesATownStandsOneALane()
    {
        var plan = Laid(new TracedPopulation(People: 0, Cars: 1000), Tee());
        var lanes = plan.Paving(Config).Lanes;

        var on = Standing(plan, SpawnStage.Car).Select(carM => Enumerable.Range(0, lanes.LaneCount).Single(lane =>
            Vector2.Distance(Spline.SampleAt(lanes.ArcsOf(lane), lanes.LaneLengthM[lane] * 0.5f).PositionM, carM) < LineTolerance.RoundingM)).ToArray();

        Assert.Equal(on.Length, on.Distinct().Count());
    }

    /// <summary>
    /// <b>No car is stood where a bridge's stands</b>: asked for a car on every lane of a street carried over another, none
    /// stands within a car's room of the cars at the bridge (<see cref="TracedBridgeCars"/>).
    /// </summary>
    [Fact]
    public void NoCarIsStoodWhereABridgesCarStands()
    {
        var plan = Laid(
            new TracedPopulation(People: 0, Cars: 1000),
            [100, 500, 400, 500, 600, 500, 900, 500, 500, 100, 500, 900],
            Street(0, 1), Street(1, 2) with { Bridge = true }, Street(2, 3), Street(4, 5));
        var bridges = TracedBridgeCars.Lay(plan.Roads, Config).PositionM;

        var nearestM = Standing(plan, SpawnStage.Car).Skip(bridges.Length).Min(carM => bridges.Min(bridgeM => Vector2.Distance(carM, bridgeM)));

        Assert.True(nearestM >= SpawnStage.RoomM(Config), $"a car stands {nearestM:F2} m from a bridge's");
    }

    static readonly Comparer<Vector2> ByPlace = Comparer<Vector2>.Create((one, other) =>
        one.X != other.X ? one.X.CompareTo(other.X) : one.Y.CompareTo(other.Y));

    static Vector2[] Standing(CityPlan plan, byte kind) =>
        [.. Enumerable.Range(0, plan.Spawns.Count).Where(spawn => plan.Spawns.Kind[spawn] == kind).Select(spawn => plan.Spawns.PositionM[spawn])];

    /// <summary>
    /// A tee at (500, 500): an arm west to (100, 500), one east to (900, 500) and one north to (500, 100), a lane each way,
    /// and two houses on the west arm's north side.
    /// </summary>
    static (float[] PointsM, SurveyWay[] Ways, Vector2[][] Houses) Tee()
    {
        var faceY = StreetY - Config.CityGen.TracedLaneWidthM - Config.WalkOuterM;
        return ([100, StreetY, 500, StreetY, 900, StreetY, 500, 100], [Street(0, 1), Street(1, 2), Street(1, 3)],
            [Box(200f, faceY - 2f), Box(300f, faceY - 2f)]);
    }

    static CityPlan Laid(TracedPopulation population, (float[] PointsM, SurveyWay[] Ways, Vector2[][] Houses) town) =>
        Laid(population, town.PointsM, town.Houses, town.Ways);

    static CityPlan Laid(TracedPopulation population, float[] pointsM, params SurveyWay[] ways) => Laid(population, pointsM, [], ways);

    static CityPlan Laid(TracedPopulation population, float[] pointsM, Vector2[][] houses, params SurveyWay[] ways)
    {
        var offsets = new List<int> { 0 };
        foreach (var house in houses) offsets.Add(offsets[^1] + house.Length);

        var survey = new Survey
        {
            Name = "Traced", Relation = 1, WidthM = 1000f, HeightM = 1000f, PointsM = pointsM, Ways = ways, Sea = [],
            Footprints = new CityPlan.FootprintArrays
            {
                RingOffsets = [.. Enumerable.Range(0, houses.Length + 1)],
                Rings = new CityPlan.RingArrays { Offsets = [.. offsets], PointM = [.. houses.SelectMany(house => house)] },
                Traced = new bool[houses.Length],
                HeightM = new float[houses.Length],
                Use = [.. houses.Select(_ => FootprintUse.House)],
            },
            Population = population,
        };

        return TracedPlan.Lay(survey, Config, BuildingSizes.None);
    }

    /// <summary>A residential way of a lane each way.</summary>
    static SurveyWay Street(params int[] points) => new()
    {
        Highway = "residential", LanesForward = 1, LanesBackward = 1, LanesShared = 0,
        CarriagewayM = 2f * OsmCarriageway.AssumedLaneWidthM, CentreOffsetM = 0f, Points = points,
    };

    /// <summary>A house of 12 by 8 m, its south wall at <paramref name="southY"/> and centred on <paramref name="x"/>.</summary>
    static Vector2[] Box(float x, float southY) =>
        [new(x - 6f, southY - 8f), new(x + 6f, southY - 8f), new(x + 6f, southY), new(x - 6f, southY)];
}
