using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// The index's whole contract: <b>the same chain the whole-network scan would have named, and the same
/// distance along it</b> — not a nearer one, not an equally near one, the same one. It replaced a scan
/// that decides which lane a car reacquires and which pavement a walker sets off down, so an index that
/// merely found <em>a</em> nearest chain would be a town that routes differently on a tie.
/// </summary>
/// <remarks>
/// Asserted against brute force over the same set, at three cell sizes, on a real town's lanes rather
/// than on a made-up scatter — and probed well outside the town as well as inside it, because a point
/// off the far edge of the grid is the one case the ring search cannot bound and has to fall back for.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class ChainIndexTests
{
    /// <summary>Well under, about, and well over a road's own width.</summary>
    public static TheoryData<float> CellSizes => [3f, 16f, 200f];

    [Theory]
    [MemberData(nameof(CellSizes))]
    public void NamesTheChainTheScanWouldHave(float cellSizeM)
    {
        var config = new SimConfig();
        var roads = RoadGraph.Build(Towns.Of(Towns.Fixture), config);

        var builder = new ChainIndex.Builder();
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            builder.Add(lane, roads.ArcsOf(lane), roads.LaneLengthM[lane]);
        }

        var index = builder.Seal(cellSizeM);
        Assert.Equal(roads.LaneCount, index.ChainCount);

        // An index of nothing agrees with a scan of nothing, so the population is asserted rather than
        // assumed: this test passing over an empty graph would say nothing at all.
        Assert.True(roads.LaneCount > 8, $"the fixture has {roads.LaneCount} lanes to choose between");

        var rng = new Random(1);
        var probed = 0;
        foreach (var pointM in Probes(roads, rng))
        {
            var wanted = Scan(roads, pointM, out var wantedAlongM);
            var got = index.Nearest(pointM, out var gotAlongM);

            Assert.Equal(wanted, got);
            Assert.Equal(wantedAlongM, gotAlongM);
            probed++;
        }

        Assert.True(probed > 8, $"only {probed} points were asked");
    }

    /// <summary>
    /// Points on the lanes, points a street away from them, and points right outside the town — the
    /// three cases being on the grid, one ring off it, and past its far corner.
    /// </summary>
    static IEnumerable<Vector2> Probes(RoadGraph roads, Random rng)
    {
        for (var lane = 0; lane < roads.LaneCount; lane += 3)
        {
            var alongM = (float)rng.NextDouble() * roads.LaneLengthM[lane];
            var onM = Spline.SampleAt(roads.ArcsOf(lane), alongM).PositionM;
            yield return onM;
            yield return onM + new Vector2((float)rng.NextDouble() * 60f - 30f, (float)rng.NextDouble() * 60f - 30f);
        }

        foreach (var farM in (Vector2[])[new(-5_000f, -5_000f), new(50_000f, 0f), new(0f, 50_000f), new(1e6f, 1e6f)])
        {
            yield return farM;
        }
    }

    /// <summary>The scan the index replaced, kept here as the thing it is measured against and nowhere else.</summary>
    static int Scan(RoadGraph roads, Vector2 pointM, out float progressM)
    {
        var best = -1;
        var bestDistanceSq = float.MaxValue;
        progressM = 0f;

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var arcs = roads.ArcsOf(lane);
            var alongM = Spline.ProjectM(arcs, pointM, roads.LaneLengthM[lane] * 0.5f, roads.LaneLengthM[lane]);
            var distanceSq = (Spline.SampleAt(arcs, alongM).PositionM - pointM).LengthSquared();
            if (distanceSq >= bestDistanceSq) continue;

            bestDistanceSq = distanceSq;
            progressM = alongM;
            best = lane;
        }

        return best;
    }
}
