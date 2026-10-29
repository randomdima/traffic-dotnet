using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>What a brief's town owes whatever seed it was laid from</b> (GEN-60…72, GEN-56): its districts, the roads between
/// them, its bridges and its connectors, asked over the suite's own city at four seeds.
/// </summary>
/// <remarks>
/// <b>Asked of what was authored as well as of the plan</b>: which district a place is in, what the barriers are and
/// where a river runs are the authoring's (<see cref="TownGen.Authored"/>), laid again here off the same brief — it is a
/// tenth of a second, and the same brief is the same town (GEN-60).
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class TownGenTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly ConcurrentDictionary<ulong, Lazy<TownGen.Authored>> Authored = new();

    static TownGen.Authored Of(ulong seed) => Authored.GetOrAdd(seed, at => new(() => TownGen.Author(Towns.Brief(at), Config))).Value;

    public static TheoryData<ulong> Seeds() => Towns.Seeds();

    /// <summary>
    /// <b>No district spans a river, a sea, an intercity road or a railway</b> (GEN-64): no cell of a district is wet or on
    /// the shore, on the ballast, or on an intercity road's carriageway and walk.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoDistrictSpansABarrier(ulong seed)
    {
        var town = Of(seed);
        var partition = town.Partition;
        var roads = town.Connectors.Roads;
        for (var cell = 0; cell < partition.Label.Length; cell++)
        {
            if (partition.Label[cell] < 0) continue;

            var atM = partition.Grid.CentreOf(cell);
            Assert.False(town.Ground.Waterside(atM), $"district {partition.Label[cell]} holds water or shore at {atM}");
            Assert.False(town.Ground.RailWithin(atM, Config.Rail.BallastWidthM * 0.5f), $"district {partition.Label[cell]} holds the railway at {atM}");
            foreach (var road in roads)
            {
                var keptM = (road.Line.Nearest(atM).AwayM);
                Assert.True(keptM > TownGen.CorridorM(Config, LayoutEdge.MostLanes), $"district {partition.Label[cell]} holds an intercity road at {atM}");
            }
        }
    }

    /// <summary>
    /// <b>A district is no bigger than a district gets, and no smaller than the least one</b> (GEN-65): a town grows by
    /// its districts, and a piece of land too small to be one is open land.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryDistrictIsTheSizeADistrictIs(ulong seed)
    {
        var partition = Of(seed).Partition;
        Assert.True(partition.Count > 1, "the town was cut into one district, so nothing was asked");
        for (var district = 0; district < partition.Count; district++)
        {
            Assert.InRange(partition.AreaM2[district], Config.CityGen.DistrictLeastM2, Config.CityGen.DistrictLargestM2);
        }
    }

    /// <summary>
    /// <b>No road carries more lanes than its class allows, and none more than four</b> (GEN-67): a district's own street
    /// one each way, a road between districts one or two — and every road the same each way but a one-way street. <b>An
    /// intercity road carries two each way and never fewer</b> (GEN-63), and so does a bridge carrying one, which no
    /// class says but its walk (<see cref="CityPlan.RoadArrays.IsWalked"/>).
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryRoadCarriesTheLanesItsClassAllows(ulong seed)
    {
        var roads = Towns.LaidFrom(seed).Roads;
        Assert.NotEmpty(roads.Class);
        var four = 0;
        for (var road = 0; road < roads.Count; road++)
        {
            var (with, against) = (roads.LanesWithTheRoad(road), roads.LanesAgainstTheRoad(road));
            Assert.InRange(with + against, 1, 2 * LayoutEdge.MostLanes);
            if (roads.Flow[road] == RoadFlow.BothWays) Assert.Equal(with, against);

            var most = roads.Class[road] is RoadClass.Street or RoadClass.CarPark or RoadClass.Roundabout ? 1 : LayoutEdge.MostLanes;
            Assert.InRange(Math.Max(with, against), 1, most);
            if (roads.Class[road] == RoadClass.Intercity || !roads.IsWalked(road))
            {
                Assert.True(
                    with == LayoutEdge.MostLanes && against == LayoutEdge.MostLanes,
                    $"intercity road {road} ({roads.Class[road]}) carries {with} and {against} lanes");
            }
            four += with + against == 4 ? 1 : 0;
        }

        Assert.True(four > 0, "no road of the town carries four lanes, so the intercity roads and the busiest district roads were not widened");
    }

    /// <summary>
    /// <b>An intercity road runs straight through every junction on it</b> (GEN-63): where two pieces of intercity road
    /// meet, they leave the junction on opposite bearings, each on a straight, so the box turns it by nothing and changes
    /// its curvature by nothing — <b>the core included</b>, which the first two roads to reach run through as one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AnIntercityRoadRunsStraightThroughItsJunctions(ulong seed)
    {
        var roads = Towns.LaidFrom(seed).Roads;
        var ends = new Dictionary<int, List<ArcSeg>>();
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads.IsWalked(road) || roads.SegmentsOf(road).Length == 0) continue;

            var arcs = roads.SegmentsOf(road);
            Leaving(roads.FromJunction[road], arcs[0]);
            var last = arcs[^1];
            Leaving(roads.ToJunction[road], new ArcSeg(last.EndM, last.HeadingAtRad(last.LengthM) + MathF.PI, last.LengthM, -last.Curvature));
        }

        foreach (var (junction, leaving) in ends)
        {
            if (leaving.Count != 2) continue;

            var turnedDeg = MathF.Abs(Spline.WrapRad(leaving[0].HeadingRad - leaving[1].HeadingRad - MathF.PI)) * 180f / MathF.PI;
            Assert.True(turnedDeg < StraightThroughDeg, $"an intercity road turns {turnedDeg:F1}° through junction {junction}");
            Assert.All(leaving, arc => Assert.Equal(0f, arc.Curvature));
        }

        void Leaving(int junction, ArcSeg arc)
        {
            if (!ends.TryGetValue(junction, out var list)) ends[junction] = list = [];
            list.Add(arc);
        }
    }

    /// <summary>How far off straight on through a junction a road may leave it, which is a float's worth of a turn.</summary>
    const float StraightThroughDeg = 0.5f;

    /// <summary><b>A district's own street never meets an intercity road</b> (GEN-68).</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoStreetMeetsAnIntercityRoad(ulong seed)
    {
        var plan = Towns.LaidFrom(seed);
        var roads = plan.Roads;
        var (street, intercity) = (new bool[plan.Junctions.Count], new bool[plan.Junctions.Count]);
        for (var road = 0; road < roads.Count; road++)
        {
            foreach (var junction in (ReadOnlySpan<int>)[roads.FromJunction[road], roads.ToJunction[road]])
            {
                street[junction] |= roads.Class[road] == RoadClass.Street;
                intercity[junction] |= roads.Class[road] == RoadClass.Intercity;
            }
        }

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            Assert.False(street[junction] && intercity[junction], $"a street meets an intercity road at junction {junction}, {plan.Junctions.CentreM[junction]}");
        }
    }

    /// <summary>
    /// <b>A bridge crosses its river short and square</b> (GEN-70): no further off the river's own normal than the bound,
    /// no longer than a deck is built, and over no sea.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryBridgeCrossesItsRiverShortAndSquare(ulong seed)
    {
        var town = Of(seed);
        var plan = Towns.LaidFrom(seed);
        var courses = town.Water.Courses;
        var skewMostRad = Config.CityGen.BridgeSkewMostDeg * MathF.PI / 180f;
        Assert.NotEmpty(plan.Bridges.Road);
        foreach (var road in plan.Bridges.Road)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            var (fromM, toM) = (arcs[0].StartM, arcs[^1].EndM);
            var middleM = (fromM + toM) * 0.5f;
            Assert.InRange(Vector2.Distance(fromM, toM), 0f, Config.CityGen.BridgeDeckLongestM);
            Assert.False(town.Water.InTheSea(middleM), $"bridge {road} at {middleM} crosses the sea");

            // The river's own way where the bridge crosses it: the nearest stretch of the nearest river's line.
            var (along, nearestM) = (Vector2.UnitX, float.PositiveInfinity);
            for (var course = 0; course < courses.Count; course++)
            {
                if (courses.Kind[course] != TownMap.WaterBody.River) continue;

                var line = new Polyline([.. courses.CourseOf(course)]);
                var (alongM, awayM) = line.Nearest(middleM);
                if (awayM < nearestM) (along, nearestM) = (line.Heading(alongM), awayM);
            }

            var span = Vector2.Normalize(toM - fromM);
            var skewRad = MathF.Asin(Math.Clamp(MathF.Abs(Vector2.Dot(span, along)), 0f, 1f));
            Assert.True(skewRad <= skewMostRad, $"bridge {road} at {middleM} runs {skewRad * 180f / MathF.PI:F1}° off its river's normal");
        }
    }

    /// <summary>
    /// <b>No road touches the water or its shore</b> (GEN-71) but a bridge and the road its bridgehead stands on: every
    /// other road's walk keeps off the shore by the strip every road keeps.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoRoadTouchesTheWater(ulong seed)
    {
        var town = Of(seed);
        var plan = Towns.LaidFrom(seed);
        var roads = plan.Roads;
        var bridgehead = new bool[plan.Junctions.Count];
        foreach (var road in plan.Bridges.Road) (bridgehead[roads.FromJunction[road]], bridgehead[roads.ToJunction[road]]) = (true, true);

        for (var road = 0; road < roads.Count; road++)
        {
            if (bridgehead[roads.FromJunction[road]] || bridgehead[roads.ToJunction[road]]) continue;

            var arcs = roads.SegmentsOf(road);
            var reachM = (roads.WidthM[road] * 0.5f) + Config.PavementWidthM;
            var lengthM = Spline.TotalLengthM(arcs);
            for (var alongM = 0f; alongM <= lengthM; alongM += 5f)
            {
                var at = Spline.SampleAt(arcs, alongM);
                foreach (var side in (ReadOnlySpan<float>)[-1f, 0f, 1f])
                {
                    var pointM = at.PositionM + (at.Right * (side * reachM));
                    Assert.False(town.Ground.Waterside(pointM), $"road {road} ({roads.Class[road]}) touches the water or its shore at {pointM}");
                }
            }
        }
    }

    /// <summary>
    /// <b>Every district is reached from the town's main roads and from each of its neighbours, separately</b> (GEN-69):
    /// its streets meet a road that is not a street; the roads that are not streets — between districts, to outside, the
    /// bridges and the rings — are one network with the intercity roads; and from its streets the streets of each district
    /// it shares an edge with are reached without an intercity road.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryDistrictIsReachedFromOutsideAndFromItsNeighbours(ulong seed)
    {
        var town = Of(seed);
        var plan = Towns.LaidFrom(seed);
        var roads = plan.Roads;
        var districtOf = new int[roads.Count];
        for (var road = 0; road < roads.Count; road++)
        {
            var arcs = roads.SegmentsOf(road);
            districtOf[road] = roads.Class[road] == RoadClass.Street ? plan.Districts.At(Spline.SampleAt(arcs, Spline.TotalLengthM(arcs) * 0.5f).PositionM) : -1;
        }

        var main = new bool[plan.Junctions.Count];
        var root = Clusters.Apart(plan.Junctions.Count);
        var mainRoads = 0;
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads.Class[road] is RoadClass.Street or RoadClass.CarPark) continue;

            mainRoads++;
            (main[roads.FromJunction[road]], main[roads.ToJunction[road]]) = (true, true);
            Clusters.Union(root, roads.FromJunction[road], roads.ToJunction[road]);
        }

        var pieces = Enumerable.Range(0, plan.Junctions.Count).Where(junction => main[junction]).Select(junction => Clusters.Find(root, junction)).Distinct().Count();
        Assert.True(pieces == 1, $"the {mainRoads} roads that are no district's own make {pieces} networks, not one");

        var streetsIn = new HashSet<int>(districtOf.Where(district => district >= 0));
        foreach (var district in streetsIn)
        {
            var meets = false;
            for (var road = 0; road < roads.Count && !meets; road++)
            {
                meets = districtOf[road] == district && (main[roads.FromJunction[road]] || main[roads.ToJunction[road]]);
            }

            Assert.True(meets, $"district {district}'s streets about {town.Partition.CentreM[district]} meet no road that is not a street");
        }

        foreach (var (a, b, _) in town.Partition.Seams())
        {
            if (!streetsIn.Contains(a) || !streetsIn.Contains(b)) continue;

            Assert.True(Reaches(plan, districtOf, a, b, avoidIntercity: true), $"district {a}'s streets reach district {b}'s only over an intercity road");
        }
    }

    /// <summary>
    /// Whether one district's streets reach a goal — another district's streets, or an intercity road where none is
    /// named — over that district's own streets and the roads that are no district's, and never another district's
    /// streets on the way.
    /// </summary>
    static bool Reaches(CityPlan plan, int[] districtOf, int from, int to, bool avoidIntercity)
    {
        var roads = plan.Roads;
        var reached = new bool[plan.Junctions.Count];
        var queue = new Queue<int>();
        for (var road = 0; road < roads.Count; road++)
        {
            if (districtOf[road] != from) continue;

            foreach (var junction in (ReadOnlySpan<int>)[roads.FromJunction[road], roads.ToJunction[road]])
            {
                if (reached[junction]) continue;

                reached[junction] = true;
                queue.Enqueue(junction);
            }
        }

        var atJunction = new List<int>[plan.Junctions.Count];
        for (var junction = 0; junction < atJunction.Length; junction++) atJunction[junction] = [];
        for (var road = 0; road < roads.Count; road++)
        {
            atJunction[roads.FromJunction[road]].Add(road);
            atJunction[roads.ToJunction[road]].Add(road);
        }

        while (queue.TryDequeue(out var junction))
        {
            foreach (var road in atJunction[junction])
            {
                if (roads.Class[road] == RoadClass.Intercity && !avoidIntercity && to < 0) return true;
                if (to >= 0 && districtOf[road] == to) return true;
                if (roads.Class[road] == RoadClass.Intercity && avoidIntercity) continue;
                if (roads.Class[road] == RoadClass.CarPark) continue;
                if (districtOf[road] >= 0 && districtOf[road] != from) continue;

                var far = roads.FromJunction[road] == junction ? roads.ToJunction[road] : roads.FromJunction[road];
                if (reached[far]) continue;

                reached[far] = true;
                queue.Enqueue(far);
            }
        }

        return false;
    }

    /// <summary>
    /// <b>Only an intercity road reaches the map's edge</b> (GEN-63): every junction standing on it is one a road runs off
    /// the map at, and the only road there is an intercity one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void OnlyAnIntercityRoadRunsOffTheMap(ulong seed)
    {
        var plan = Towns.LaidFrom(seed);
        var roads = plan.Roads;
        var runsOff = 0;
        for (var road = 0; road < roads.Count; road++)
        {
            foreach (var junction in (ReadOnlySpan<int>)[roads.FromJunction[road], roads.ToJunction[road]])
            {
                if (!plan.Junctions.RunsOff(junction)) continue;

                runsOff++;
                Assert.Equal(RoadClass.Intercity, roads.Class[road]);
            }
        }

        Assert.True(runsOff > 0, "no road of the town runs off the map, so its intercity roads reach nowhere");
    }

    /// <summary>
    /// <b>A stage's seed moves that stage and those after it, and nothing laid before</b> (GEN-60, GEN-11): the same brief
    /// with its streets' seed rerolled has the same water, connectors and districts, and other streets.
    /// </summary>
    [Fact]
    public void RerollingTheStreetsMovesNothingLaidBefore()
    {
        var brief = Towns.Brief(Towns.CitySeed);
        var rerolled = brief with { Seeds = brief.Seeds with { Streets = 99 } };
        var (one, other) = (TownGen.Author(brief, Config), TownGen.Author(rerolled, Config));

        Assert.Equal(one.Water.Courses.PointM, other.Water.Courses.PointM);
        Assert.Equal(one.Connectors.CoreM, other.Connectors.CoreM);
        Assert.Equal(one.Partition.Label, other.Partition.Label);
        Assert.NotEqual(Towns.LayFresh(brief).Digest(), Towns.LayFresh(rerolled).Digest());
    }

    /// <summary><b>A brief written and read back is the same brief, and lays the same town</b> (GEN-60): it is the whole town.</summary>
    [Fact]
    public void ABriefWrittenAndReadBackIsTheSameTown()
    {
        var brief = Towns.Brief(Towns.CitySeed) with { Sea = new SeaBrief { BearingDeg = 30f } };
        var back = TownBrief.Parse(brief.Json());

        Assert.Equal(brief.Json(), back.Json());
        Assert.Equal(TownGen.Author(brief, Config).Partition.Label, TownGen.Author(back, Config).Partition.Label);
    }
}
