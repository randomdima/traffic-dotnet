using System.Numerics;
using TrafficSimulation.CityGen;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>A long ring filed by cell says what its shape does</b> (<see cref="GroundShapes.EdgeCells"/>): every place half
/// a metre inside a circle of a hundred metres drawn through four hundred points is inside it and every place half a
/// metre outside is not, and a place is near it exactly where it stands within reach of the line.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class EdgeCellsTests
{
    const float RadiusM = 100f;
    const int Points = 400;

    static readonly Vector2 CentreM = new(500f, 300f);

    /// <summary>How far a chord of the ring stands in from the circle, at most.</summary>
    static readonly float SagM = RadiusM * (1f - MathF.Cos(MathF.PI / Points));

    [Fact]
    public void APlaceInsideTheRingIsInsideAndOneOutsideIsNot()
    {
        var (ring, cells) = Circle();

        for (var step = 0; step < 720; step++)
        {
            var towards = new Vector2(MathF.Cos(step * 0.5f * MathF.PI / 180f), MathF.Sin(step * 0.5f * MathF.PI / 180f));
            Assert.True(cells.Inside(ring, CentreM + (towards * (RadiusM - SagM - 0.5f))), $"{step * 0.5f}° inside");
            Assert.False(cells.Inside(ring, CentreM + (towards * (RadiusM + 0.5f))), $"{step * 0.5f}° outside");
        }

        Assert.True(cells.Inside(ring, CentreM));
        Assert.False(cells.Inside(ring, CentreM + new Vector2(0f, RadiusM * 3f)));
    }

    [Fact]
    public void APlaceIsNearTheRingWhereItStandsWithinReachOfIt()
    {
        var (ring, cells) = Circle();

        for (var step = 0; step < 360; step++)
        {
            var towards = new Vector2(MathF.Cos(step * MathF.PI / 180f), MathF.Sin(step * MathF.PI / 180f));
            Assert.True(cells.Near(ring, CentreM + (towards * (RadiusM + 2f)), 2.5f), $"{step}° within reach");
            Assert.False(cells.Near(ring, CentreM + (towards * (RadiusM + 4f)), 2.5f), $"{step}° out of reach");
        }
    }

    static (Vector2[] Ring, GroundShapes.EdgeCells Cells) Circle()
    {
        var ring = new Vector2[Points];
        for (var point = 0; point < Points; point++)
        {
            var angle = point * 2f * MathF.PI / Points;
            ring[point] = CentreM + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * RadiusM);
        }

        return (ring, new GroundShapes.EdgeCells(ring, CentreM - new Vector2(RadiusM), CentreM + new Vector2(RadiusM), 16f));
    }
}
