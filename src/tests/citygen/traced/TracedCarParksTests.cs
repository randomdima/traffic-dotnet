using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A traced map's car parks are laid as a mall's are</b> (GEN-53a, <see cref="TracedCarParks"/>): aisles a rank, a rank
/// and a walk apart inside the car park's outline, joined to the street beside it, and a rank of bays down both kerbs of
/// every aisle — each bay inside the outline.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class TracedCarParksTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>The aisles of a car park stand a pitch apart</b> (<see cref="SimConfig.CarParkAislePitchM"/>): an aisle, its
    /// two ranks and the walk between one rank and the next aisle's.
    /// </summary>
    [Fact]
    public void ACarParksAislesStandAPitchApart()
    {
        var map = Parked(Beside(streetAtM: 100f)).Map;

        var acrossM = Rungs(map).Order().ToList();
        Assert.True(acrossM.Count >= 2, $"{acrossM.Count} aisles");
        for (var at = 1; at < acrossM.Count; at++) Assert.Equal(Config.CarParkAislePitchM, acrossM[at] - acrossM[at - 1], 1);
    }

    /// <summary>
    /// <b>A car park is joined to the street beside it</b>: an aisle shares a point with the street, which is where the
    /// two meet.
    /// </summary>
    [Fact]
    public void ACarParkIsJoinedToTheStreetBesideIt()
    {
        var map = Parked(Beside(streetAtM: 100f)).Map;

        var street = map.Roads.Single(road => !road.Aisle);
        Assert.Contains(map.Roads.Where(road => road.Aisle).SelectMany(road => road.Points), street.Points.Contains);
    }

    /// <summary>
    /// <b>A car park no street comes within reach of is not laid</b> (<see cref="CityGenFigures.CarParkEntranceReachM"/>):
    /// aisles joined to nothing are a piece of town no car can reach.
    /// </summary>
    [Fact]
    public void ACarParkNoStreetReachesIsNotLaid()
    {
        var parking = Parked(Beside(streetAtM: Top - Config.CityGen.CarParkEntranceReachM - Config.CarParkAislePitchM - 10f));

        Assert.Equal((0, 1), (parking.Laid, parking.NoStreet));
        Assert.DoesNotContain(parking.Map.Roads, road => road.Aisle);
    }

    /// <summary>
    /// <b>An aisle is ranked down both kerbs</b> (GEN-53a): the car park on it has as many bays on its one side as on its
    /// other, each a lane from the next.
    /// </summary>
    [Fact]
    public void AnAisleIsRankedDownBothKerbs()
    {
        var plan = TownPlan.Lay(Parked(Beside(streetAtM: 100f)).Map, Config, ArtSizes.None);
        var parks = plan.CarParks;

        var widest = Enumerable.Range(0, parks.Street.Length).MaxBy(park => parks.BayOffsets[park + 1] - parks.BayOffsets[park]);
        var bays = Enumerable.Range(parks.BayOffsets[widest], parks.BayOffsets[widest + 1] - parks.BayOffsets[widest]).ToList();
        var right = bays.Where(bay => parks.Right[bay]).Select(bay => plan.Roads.SegmentsOf(parks.Road[bay])[0].StartM).ToList();
        var left = bays.Where(bay => !parks.Right[bay]).Select(bay => plan.Roads.SegmentsOf(parks.Road[bay])[0].StartM).ToList();
        Assert.Equal(right.Count, left.Count);
        Assert.True(right.Count >= Config.CityGen.BaysPerLotFewest, $"{right.Count} bays a side");
        for (var at = 1; at < right.Count; at++) Assert.Equal(Config.LaneWidthM, Vector2.Distance(right[at], right[at - 1]), 2);
    }

    /// <summary><b>Every bay stands inside its car park's outline</b>, its far end as much as its mouth.</summary>
    [Fact]
    public void EveryBayStandsInsideItsCarPark()
    {
        var plan = TownPlan.Lay(Parked(Beside(streetAtM: 100f)).Map, Config, ArtSizes.None);

        Assert.NotEmpty(plan.CarParks.Road);
        foreach (var bay in plan.CarParks.Road)
        {
            var line = plan.Roads.SegmentsOf(bay)[0];
            foreach (var endM in (ReadOnlySpan<Vector2>)[line.StartM, line.EndM])
            {
                Assert.InRange(endM.X, Left - LineTolerance.OnePlaceM, Left + WidthM + LineTolerance.OnePlaceM);
                Assert.InRange(endM.Y, Top - LineTolerance.OnePlaceM, Top + DepthM + LineTolerance.OnePlaceM);
            }
        }
    }

    const float Left = 100f, Top = 140f, WidthM = 200f, DepthM = 140f;

    /// <summary>
    /// A car park of <see cref="WidthM"/> by <see cref="DepthM"/> south of a street running east and west across the map at
    /// <paramref name="streetAtM"/> metres down it.
    /// </summary>
    static TownMap Beside(float streetAtM)
    {
        var sizeM = new Vector2(600, 500);
        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(-1, ZoneKind.Town, [], [TownMap.WholeOutline(sizeM)]);
        zones.Add(0, ZoneKind.Parking, [], [[new(Left, Top), new(Left + WidthM, Top), new(Left + WidthM, Top + DepthM), new(Left, Top + DepthM)]]);
        return TownMap.Bare("Lot", "a car park beside a street", 12888405, sizeM, ZoneKind.Town, []) with
        {
            PointM = [new(0, streetAtM), new(600, streetAtM)],
            Roads =
            [
                new TracedRoad
                {
                    OsmId = 1, Highway = "residential", LanesForward = 1, LanesBackward = 1, LanesShared = 0, LanesTagged = true,
                    WidthM = 2f * Config.CityGen.TracedLaneWidthM, Mended = true, Points = [0, 1],
                },
            ],
            Zones = zones.Arrays(),
        };
    }

    static TracedCarParks.Parking Parked(TownMap map) => TracedCarParks.Parked(map, Config);

    /// <summary>
    /// Where across the car park each of its aisles stands, along whichever way the ladder runs: the lot is square
    /// enough that the bay's depth decides which bearing ranks the most bays.
    /// </summary>
    static IEnumerable<double> Rungs(TownMap map)
    {
        var eastWest = Across(map, along: 0, spanM: WidthM).ToList();
        var northSouth = Across(map, along: 1, spanM: DepthM).ToList();
        return eastWest.Count >= northSouth.Count ? eastWest : northSouth;
    }

    /// <summary>Each aisle running the whole way along axis <paramref name="along"/> (0 x, 1 y), by where it stands across.</summary>
    static IEnumerable<double> Across(TownMap map, int along, float spanM)
    {
        foreach (var road in map.Roads.Where(road => road.Aisle))
        {
            var (first, last) = (map.PointM[road.Points[0]], map.PointM[road.Points[^1]]);
            var (fromAlong, fromAcross, toAlong, toAcross) =
                along == 0 ? (first.X, first.Y, last.X, last.Y) : (first.Y, first.X, last.Y, last.X);
            if (Math.Abs(fromAcross - toAcross) < LineTolerance.OnePlaceM && Math.Abs(fromAlong - toAlong) > spanM * 0.5)
                yield return fromAcross;
        }
    }
}
