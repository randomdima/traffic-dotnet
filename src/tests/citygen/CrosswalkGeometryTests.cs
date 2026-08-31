using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// Which way round a crossing's record is, pinned against the ground the town was laid with. The format
/// carries an axis, a depth and a span and says nothing about what any of them is across
/// and a reader that guessed would lay every foot crossing along the road it was
/// meant to cross.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
public class CrosswalkGeometryTests
{
    public static TheoryData<string> Maps => Towns.EveryTown();

    /// <summary>
    /// <b>The axis runs along the road, not across it</b> — a walker crosses square to the axis, over the
    /// span, and the depth is how much of the road's own length the paint takes up. A stride past either
    /// end of the span is ground to step off onto; a stride off either flank of the depth is still the
    /// road the crossing is painted across.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void TheAxisIsTheWayAcrossAndTheSpanIsHowFar(string map)
    {
        var plan = Towns.Of(map);
        var terrain = new TerrainGrid(plan, SimConfig.Shipped());
        var crossings = plan.Crosswalks;
        if (crossings.Count == 0) return;

        var strideM = plan.CellSizeM;
        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var centreM = crossings.CentreM[crossing];
            var axis = Vector2.Normalize(crossings.Axis[crossing]);
            var across = new Vector2(-axis.Y, axis.X);

            Assert.Equal(Ground.Crosswalk, terrain.GroundAt(centreM));

            var beyond = crossings.SpanM[crossing] * 0.5f + strideM;
            foreach (var end in (ReadOnlySpan<Vector2>)[centreM + across * beyond, centreM - across * beyond])
            {
                Assert.True(
                    terrain.At(end).Walkable && !terrain.At(end).Drivable,
                    $"{map}: crossing {crossing} ends on {terrain.GroundAt(end)}, which is not somewhere to step off onto");
            }

            var flank = crossings.DepthM[crossing] * 0.5f + strideM;
            foreach (var side in (ReadOnlySpan<Vector2>)[centreM + axis * flank, centreM - axis * flank])
            {
                Assert.True(
                    terrain.At(side).Drivable,
                    $"{map}: crossing {crossing} has {terrain.GroundAt(side)} beside it, not the road it crosses");
            }
        }
    }
}
