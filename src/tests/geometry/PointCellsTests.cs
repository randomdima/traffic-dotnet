using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>Every answer is the one asking each point in turn would give</b> — which is the whole of what lets a
/// builder ask the index instead of the set, and have the town come out the same.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P3)]
public class PointCellsTests
{
    /// <summary>A town thirty kilometres across, so the far cells are ones a float is coarse in.</summary>
    const float AcrossM = 30000f;

    static readonly GridLevel Level = new WorldGrid(8f).Main;

    [Fact]
    public void WhetherAnyPointIsWithinReachIsWhatEveryPointSays()
    {
        var draw = new Rng(7, 1);
        var (cells, pointsM) = Scattered(ref draw, 1000);

        for (var ask = 0; ask < 300; ask++)
        {
            var atM = Somewhere(ref draw);
            var reachM = draw.NextFloat(1f, 400f);

            var any = false;
            foreach (var pointM in pointsM) any |= Vector2.DistanceSquared(pointM, atM) < reachM * reachM;

            Assert.Equal(any, cells.AnyWithin(atM, reachM));
        }
    }

    [Fact]
    public void TheNearestIsTheNearestOfEveryPoint()
    {
        var draw = new Rng(7, 2);
        var (cells, pointsM) = Scattered(ref draw, 300);

        for (var ask = 0; ask < 300; ask++)
        {
            var atM = Somewhere(ref draw);
            var nearestSq = float.PositiveInfinity;
            foreach (var pointM in pointsM) nearestSq = MathF.Min(nearestSq, Vector2.DistanceSquared(pointM, atM));

            Assert.Equal(nearestSq, cells.NearestSq(atM));
        }
    }

    /// <summary>A bound nearer than every point is the answer, and one further than the nearest is not.</summary>
    [Fact]
    public void ABoundStandsInForEveryPointBeyondIt()
    {
        var draw = new Rng(7, 3);
        var (cells, pointsM) = Scattered(ref draw, 300);

        for (var ask = 0; ask < 300; ask++)
        {
            var atM = Somewhere(ref draw);
            var nearestSq = float.PositiveInfinity;
            foreach (var pointM in pointsM) nearestSq = MathF.Min(nearestSq, Vector2.DistanceSquared(pointM, atM));

            Assert.Equal(nearestSq * 0.5f, cells.NearestSq(atM, nearestSq * 0.5f));
            Assert.Equal(nearestSq, cells.NearestSq(atM, nearestSq * 2f));
        }
    }

    static (PointCells Cells, List<Vector2> PointsM) Scattered(ref Rng draw, int count)
    {
        var cells = new PointCells(Level);
        var pointsM = new List<Vector2>(count);
        for (var point = 0; point < count; point++)
        {
            var atM = Somewhere(ref draw);
            pointsM.Add(atM);
            Assert.Equal(point, cells.Add(atM));
        }

        return (cells, pointsM);
    }

    static Vector2 Somewhere(ref Rng draw) => new(draw.NextFloat(0f, AcrossM), draw.NextFloat(0f, AcrossM));
}
