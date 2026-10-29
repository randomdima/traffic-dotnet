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

    /// <summary>
    /// <b>The nearest point is the one a scan keeping only a strictly nearer one names</b> — of two as near, the one added
    /// first, which a set laid with every point twice over asks at every turn.
    /// </summary>
    [Fact]
    public void TheNearestPointIsTheFirstOfThoseAsNearAsAScanFindsThem()
    {
        var draw = new Rng(11, 1);
        var (_, pointsM) = Scattered(ref draw, 400);
        var cells = new PointCells(Level);
        var twice = pointsM.Concat(pointsM).ToArray();
        foreach (var pointM in twice) cells.Add(pointM);

        for (var ask = 0; ask < 300; ask++)
        {
            var atM = ask % 3 == 0 ? pointsM[ask % pointsM.Count] : Somewhere(ref draw);

            var best = -1;
            var bestSq = float.MaxValue;
            for (var point = 0; point < twice.Length; point++)
            {
                var distanceSq = Vector2.DistanceSquared(twice[point], atM);
                if (distanceSq >= bestSq) continue;

                (best, bestSq) = (point, distanceSq);
            }

            Assert.Equal(best, cells.Nearest(atM));
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

    /// <summary>
    /// <b>Handed the nearest of the points before some count, the answer is the nearest of every point</b> —
    /// however many came after. Asked over the whole town and over a corner of it a few cells across, so the newer
    /// points are both asked in turn and found by the rings.
    /// </summary>
    [Theory]
    [InlineData(AcrossM)]
    [InlineData(AcrossM / 1000f)]
    public void TheNearestOfThoseBeforeAndThoseSinceIsTheNearestOfEveryPoint(float acrossM)
    {
        var draw = new Rng(7, 4);
        var cells = new PointCells(Level);
        var pointsM = new List<Vector2>();
        for (var point = 0; point < 300; point++)
        {
            var pointM = Within(ref draw, acrossM);
            pointsM.Add(pointM);
            cells.Add(pointM);
        }

        for (var ask = 0; ask < 300; ask++)
        {
            var atM = Within(ref draw, acrossM);
            var since = draw.NextInt(pointsM.Count + 1);
            var beforeSq = float.PositiveInfinity;
            for (var point = 0; point < since; point++) beforeSq = MathF.Min(beforeSq, Vector2.DistanceSquared(pointsM[point], atM));

            var nearestSq = beforeSq;
            for (var point = since; point < pointsM.Count; point++) nearestSq = MathF.Min(nearestSq, Vector2.DistanceSquared(pointsM[point], atM));

            Assert.Equal(nearestSq, cells.NearestSq(atM, beforeSq, since));
        }
    }

    /// <summary>
    /// <b>An extent named changes no answer</b>: a set filed over a window of part of the town, with points both in it and
    /// out of it, answers every question as the set filed without one.
    /// </summary>
    [Fact]
    public void ASetFiledOverAWindowAnswersAsOneFiledWithout()
    {
        var draw = new Rng(11, 1);
        var (plain, pointsM) = Scattered(ref draw, 600);
        var windowed = new PointCells(Level, new Vector2(AcrossM * 0.25f), new Vector2(AcrossM * 0.75f));
        foreach (var pointM in pointsM) windowed.Add(pointM);

        var (plainNear, windowedNear) = (new List<int>(), new List<int>());
        for (var ask = 0; ask < 300; ask++)
        {
            var atM = Somewhere(ref draw);
            var reachM = draw.NextFloat(1f, 2000f);

            Assert.Equal(plain.AnyWithin(atM, reachM), windowed.AnyWithin(atM, reachM));
            Assert.Equal(plain.NearestSq(atM), windowed.NearestSq(atM));
            Assert.Equal(plain.Nearest(atM), windowed.Nearest(atM));
            plain.Within(atM, reachM, plainNear);
            windowed.Within(atM, reachM, windowedNear);
            Assert.Equal(plainNear, windowedNear);
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

    static Vector2 Somewhere(ref Rng draw) => Within(ref draw, AcrossM);

    static Vector2 Within(ref Rng draw, float acrossM) => new(draw.NextFloat(0f, acrossM), draw.NextFloat(0f, acrossM));
}
