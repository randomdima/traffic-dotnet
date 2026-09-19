using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>A town's own boundary moved off itself</b> (<see cref="ArcOutset"/>). The construction's arithmetic
/// is asked of shapes with written-down answers in <c>ArcOutsetTests</c>; what a town adds is the one thing
/// a square cannot — <b>a hundred thousand stretches, most of them short, meeting at every angle</b>.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
[Collection(TownGeometryCollection.Name)]
public class PerimeterOutsetTests
{
    /// <summary>How far the boundary is moved, an example and a wide one: the move with corners to fail at.</summary>
    const float MovedM = 5f;

    /// <summary>
    /// <b>The boundary of a shape moved off itself is closed</b>, so every run the walk hands back is a ring
    /// and none is left with two ends. A run with ends is a crossing the construction did not find: the
    /// moved lines fold through one another all over a town, and every fold is two crossings that have to
    /// be solved and cut at.
    /// </summary>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void TheOutsetOfATownsBoundaryCloses(string map)
    {
        var config = SimConfig.Shipped();
        var shell = Towns.Of(map).Paving(config).Perimeter(config);
        var (rings, loose) = shell.Outset(MovedM, roundedM: 0f);

        var openM = 0f;
        foreach (var run in loose) openM += Spline.TotalLengthM(run);

        var ends = loose.Length == 0
            ? ""
            : $", the first {loose[0].Length} pieces from {loose[0][0].StartM} to {loose[0][^1].EndM}, "
              + $"a gap of {Vector2.Distance(loose[0][0].StartM, loose[0][^1].EndM):F3} m whose ends stand "
              + $"{Off(shell, loose[0][^1].EndM):F3} m and {Off(shell, loose[0][0].StartM):F3} m off the "
              + $"boundary against the {MovedM} m asked for";
        Assert.True(rings.Length > 0, $"{map}: the outset of {shell.Chains.Length} rings closed none");
        Assert.True(
            loose.Length == 0,
            $"{map}: the outset of {shell.Chains.Length} rings left {loose.Length} runs open, {openM:F1} m "
            + $"in all, against {rings.Length} rings closed{ends}");
    }

    /// <summary>
    /// <b>Every layer the town is drawn from closes against its boundary</b> (TER-7b,
    /// <see cref="GroundRings"/>): a layer is the boundary moved out by its own figure, and a closed shape
    /// moved off itself is closed. A run left with two ends is a crossing the move did not find — and it is
    /// a hole in the concrete a frame shows.
    /// </summary>
    [Theory]
    [InlineData(Towns.Fixture)]
    [InlineData(Towns.City)]
    public void EveryLayerTheTownIsDrawnFromClosesAgainstItsBoundary(string map)
    {
        var rings = Towns.Of(map).Paving(SimConfig.Shipped()).Rings(SimConfig.Shipped());

        foreach (var layer in rings.Layers)
        {
            Assert.True(
                layer.Loose.Length == 0,
                $"{map}: the {layer.Named} at {layer.OutwardM:F2} m left {layer.Loose.Length} runs open, "
                + $"{LengthM(layer.Loose):F1} m in all, against {layer.Rings.Length} rings closed");
        }
    }

    static float LengthM(ReadOnlySpan<ArcSeg[]> chains)
    {
        var lengthM = 0f;
        foreach (var chain in chains) lengthM += Spline.TotalLengthM(chain);

        return lengthM;
    }

    /// <summary>How far a place stands off the nearest piece of the boundary it was taken from.</summary>
    static float Off(BandShell shell, Vector2 pointM)
    {
        var offM = float.MaxValue;
        foreach (var ring in shell.Chains)
        {
            foreach (var piece in ring)
            {
                var alongM = Spline.ProjectM([piece], pointM, piece.LengthM * 0.5f, piece.LengthM);
                offM = MathF.Min(offM, Vector2.Distance(piece.PointAtM(alongM), pointM));
            }
        }

        return offM;
    }
}
