using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The pavement's inner corners as they come out of the ground itself (TER-3c.4). What they are once
/// drawn is <c>GroundMeshTests</c>'s; what is asserted here is that each one is a corner at all.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class PavementCornerTests
{
    public static TheoryData<string> Maps => Towns.EveryShippedMap();

    [Theory]
    [MemberData(nameof(Maps))]
    public void EverySolvedCornerIsAWedgeWithTwoOutwardEdges(string map)
    {
        var config = SimConfig.Shipped();

        foreach (var corner in PavementCorners.Solve(Towns.Of(map), config))
        {
            Assert.Equal(1f, corner.NormalA.Length(), tolerance: 1e-3f);
            Assert.Equal(1f, corner.NormalB.Length(), tolerance: 1e-3f);
            Assert.InRange(corner.RadiusM, 1e-3f, config.Road.PavementCornerRadiusM);

            // Both edges face out of the same wedge, so the bisector the arc's centre stands on is a
            // direction and not a division by nothing.
            Assert.True(1f + Vector2.Dot(corner.NormalA, corner.NormalB) > 0.01f,
                $"{map}: the edges at {corner.CornerM} face each other");
            Assert.Equal(corner.RadiusM, Vector2.Distance(corner.ArcCentreM, corner.TangentAM), tolerance: 1e-2f);
            Assert.Equal(corner.RadiusM, Vector2.Distance(corner.ArcCentreM, corner.TangentBM), tolerance: 1e-2f);
        }
    }

    /// <summary>
    /// <b>The arc is bounded by how far the fillet would reach in</b> (TER-3c.4). A right angle turns on
    /// the full half-width and stands 0.83 m deep; a wedge sharp enough that the same arc would drive a
    /// spike further into the verge turns on less, so nothing ever reaches past half a walk.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void NoCornerReachesFurtherIntoTheVergeThanHalfTheWalk(string map)
    {
        var plan = Towns.Of(map);
        var config = SimConfig.Shipped();
        var walkM = plan.PavementWidthM > 0f ? plan.PavementWidthM : config.Road.PavementWidthM;

        foreach (var corner in PavementCorners.Solve(plan, config))
        {
            var deepM = Vector2.Distance(corner.ArcCentreM, corner.CornerM) - corner.RadiusM;
            Assert.InRange(deepM, 0f, (walkM * 0.5f) + 1e-2f);
        }
    }

    /// <summary>
    /// <b>A map that records no corners of its own is rounded the same as one that does</b>, which is the
    /// whole reason they are solved rather than read: the exam lattice carries an empty list and its
    /// junctions are corners all the same.
    /// </summary>
    [Fact]
    public void AMapCarryingNoCornersOfItsOwnStillHasThem()
    {
        var plan = Towns.Of("Exam");

        Assert.Equal(0, plan.PavementCorners.Count);
        Assert.NotEmpty(PavementCorners.Solve(plan, SimConfig.Shipped()));
    }

    /// <summary>A map laid without a pavement has no corners to turn, and asking costs nothing.</summary>
    [Fact]
    public void AMapWithNoPavementHasNoCorners()
    {
        var plan = Towns.Of("Track");

        Assert.Equal(0f, plan.PavementWidthM);
        Assert.Empty(PavementCorners.Solve(plan, SimConfig.Shipped()));
    }
}
