using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// The index's whole contract: <b>a superset, never a subset</b>, whatever bucket size it was laid
/// at. An index that drops one prop is a car that drives through a tree, and the failure is silent —
/// so the assertion is against brute force over the same set, at three bucket sizes, on a real town's
/// ninety-odd thousand props rather than on a made-up scatter.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class BucketGridTests
{
    /// <summary>Well under, about, and well over the spacing of a town's props.</summary>
    public static TheoryData<float> BucketSizes => [2f, 16f, 128f];

    [Theory]
    [MemberData(nameof(BucketSizes))]
    public void WhatIsNearAPointIsNeverMissed(float bucketSizeM)
    {
        var plan = Towns.Of(Towns.Fixture);
        var grid = BucketGrid.Build(plan.WorldSizeM, bucketSizeM, plan.Props.CentreM, plan.Props.RadiusM);
        var found = new int[plan.Props.Count];

        foreach (var reachM in (ReadOnlySpan<float>)[0f, 1f, 25f])
        {
            for (var probe = 0; probe < plan.Buildings.Count; probe++)
            {
                var pointM = plan.Buildings.CentreM[probe];
                var count = grid.Query(pointM, reachM, found);
                Assert.True(count <= found.Length, "the query wanted more room than the whole set");

                var reported = new HashSet<int>(found.AsSpan(0, count).ToArray());
                for (var prop = 0; prop < plan.Props.Count; prop++)
                {
                    var touchingM = reachM + plan.Props.RadiusM[prop];
                    if (Vector2.DistanceSquared(plan.Props.CentreM[prop], pointM) > touchingM * touchingM) continue;

                    Assert.Contains(prop, reported);
                }
            }
        }
    }

    /// <summary>
    /// Every item is in the index exactly once — which is what makes a query's result a set without
    /// anything having to deduplicate it, and what keeps one large item from costing a hundred slots.
    /// </summary>
    [Theory]
    [MemberData(nameof(BucketSizes))]
    public void EveryItemIsIndexedOnce(float bucketSizeM)
    {
        var plan = Towns.Of(Towns.Fixture);
        var grid = BucketGrid.Build(plan.WorldSizeM, bucketSizeM, plan.Props.CentreM, plan.Props.RadiusM);

        var everything = new int[plan.Props.Count];
        var count = grid.Query(plan.WorldSizeM * 0.5f, plan.WorldSizeM.Length(), everything);

        Assert.Equal(plan.Props.Count, count);
        Assert.Equal(plan.Props.Count, new HashSet<int>(everything).Count);
    }

    [Fact]
    public void AQueryOffTheEdgeIsAnsweredWithWhatIsInside()
    {
        var plan = Towns.Of(Towns.Fixture);
        var grid = BucketGrid.Build(plan.WorldSizeM, 16f, plan.Props.CentreM, plan.Props.RadiusM);
        var found = new int[plan.Props.Count];

        Assert.Equal(0, grid.Query(new Vector2(-10_000f, -10_000f), 10f, found));
        Assert.Equal(plan.Props.Count, grid.Query(new Vector2(-10_000f, -10_000f), 1e6f, found));
    }

    /// <summary>
    /// A widening search terminates, and terminates on the right answer: it stops as soon as the best
    /// it holds is nearer than the ring it would search next, so an empty quarter of the map costs
    /// rings and not a scan.
    /// </summary>
    [Fact]
    public void AWideningSearchTerminatesOnTheNearestItem()
    {
        var plan = Towns.Of(Towns.Fixture);
        var grid = BucketGrid.Build(plan.WorldSizeM, 16f, plan.Props.CentreM, plan.Props.RadiusM);

        foreach (var pointM in (ReadOnlySpan<Vector2>)
                 [new(0f, 0f), plan.WorldSizeM * 0.5f, plan.WorldSizeM, new(-500f, -500f)])
        {
            var nearest = grid.Nearest(pointM, out var distanceM);

            var brute = -1;
            var bruteDistanceM = float.PositiveInfinity;
            for (var prop = 0; prop < plan.Props.Count; prop++)
            {
                var candidateM = Vector2.Distance(plan.Props.CentreM[prop], pointM);
                if (candidateM >= bruteDistanceM) continue;

                bruteDistanceM = candidateM;
                brute = prop;
            }

            Assert.Equal(brute, nearest);
            Assert.Equal(bruteDistanceM, distanceM, tolerance: 1e-3f);
        }
    }

    [Fact]
    public void AnEmptyIndexAnswersNothingRatherThanSearchingForEver()
    {
        var grid = new BucketGrid(new Vector2(480f, 320f), 16f);
        grid.Rebuild([], [], count: 0);

        Assert.Equal(-1, grid.Nearest(new Vector2(240f, 160f), out var distanceM));
        Assert.Equal(float.PositiveInfinity, distanceM);
        Assert.Equal(0, grid.Query(new Vector2(240f, 160f), 100f, stackalloc int[8]));
    }

    /// <summary>
    /// A rebuild is what the proximity index does every tick, and the brief says nothing in it may
    /// survive one. Refilling the same arrays is how that is done without allocating.
    /// </summary>
    /// <remarks>
    /// <b>The warm-up is not padding.</b> This counts bytes on the running thread, so it counts
    /// everything the runtime allocates there too — and tiered compilation promotes a method around its
    /// thirtieth call, which allocates and lands wherever the suite's other tests happen to leave the
    /// count. Measured cold it reported a few hundred bytes about one run in six; past promotion it is
    /// exact. It is the same rule this engine's probes hold to, one level down.
    /// </remarks>
    [Fact]
    public void ARebuildReusesItsArraysAndAllocatesNothing()
    {
        var plan = Towns.Of(Towns.Fixture);
        var grid = BucketGrid.Build(plan.WorldSizeM, 16f, plan.Props.CentreM, plan.Props.RadiusM);
        for (var warm = 0; warm < 64; warm++) grid.Rebuild(plan.Props.CentreM, plan.Props.RadiusM, plan.Props.Count);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var rebuild = 0; rebuild < 16; rebuild++) grid.Rebuild(plan.Props.CentreM, plan.Props.RadiusM, plan.Props.Count);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
