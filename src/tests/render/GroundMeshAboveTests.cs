using System.Numerics;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen.Traced;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// <b>A bridge over a road is drawn as a road of its own over the ground</b> (TER-7b): its deck, carriageway and paint
/// are the part drawn after the bodies on the ground (<see cref="GroundPart.Above"/>), and the ground's own parts lay
/// nothing of it — asked of one street carried over another (<see cref="TracedPlanTests.OverAStreet"/>).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class GroundMeshAboveTests
{
    static readonly GroundMesh Mesh = GroundMesh.Build(TracedPlanTests.OverAStreet(), SimConfig.Shipped());

    /// <summary>
    /// <b>A point on a bridge is drawn by the part above and by nothing of the ground's but the grass</b>: halfway
    /// between a bridgehead and the street it passes over, the ground under the deck is the grass it stands over.
    /// </summary>
    [Fact]
    public void APointOnABridgeIsDrawnAboveAndOnTheGroundIsGrass()
    {
        var onTheBridgeM = new Vector2(450f, 500f);

        Assert.True(Covers(GroundPart.Above, onTheBridgeM));
        for (var part = GroundPart.Walk; part < GroundPart.Above; part++)
        {
            Assert.False(Covers(part, onTheBridgeM), $"the ground's {part} is laid on the bridge");
        }
    }

    /// <summary>
    /// <b>The street passing under a bridge is drawn on the ground</b>, its carriageway running on under the deck rather
    /// than stopping at it.
    /// </summary>
    [Fact]
    public void TheStreetUnderABridgeIsDrawnOnTheGroundUnderTheDeck()
    {
        Assert.True(Covers(GroundPart.Carriageway, TracedPlanTests.OverAStreetCrossingM));
    }

    static bool Covers(GroundPart part, Vector2 pointM)
    {
        var tally = Mesh.Parts[(int)part];
        var vertices = Mesh.Vertices;
        var indices = Mesh.Indices.Slice(tally.FirstIndex, tally.IndexCount);
        for (var corner = 0; corner + 2 < indices.Length; corner += 3)
        {
            if (Inside(vertices[(int)indices[corner]].PositionM, vertices[(int)indices[corner + 1]].PositionM,
                    vertices[(int)indices[corner + 2]].PositionM, pointM))
            {
                return true;
            }
        }

        return false;
    }

    static bool Inside(Vector2 a, Vector2 b, Vector2 c, Vector2 p)
    {
        var ab = Cross(b - a, p - a);
        var bc = Cross(c - b, p - b);
        var ca = Cross(a - c, p - c);
        return (ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f);
    }

    static float Cross(Vector2 one, Vector2 other) => (one.X * other.Y) - (one.Y * other.X);
}
