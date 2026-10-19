using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen.Traced;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>The ground's triangles filed by band for a frame to cull by (<see cref="GroundBands"/>).</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P7)]
public class GroundBandsTests
{
    /// <summary>
    /// <b>The level above is drawn in the order it is laid</b> (TER-7b): its deck, then its carriageway, kerb and paint,
    /// all in one part — so filed out of the mesh's order its decks are drawn over its own tarmac and lines. Asked of one
    /// street carried over another (<see cref="TracedPlanTests.OverAStreet"/>).
    /// </summary>
    [Fact]
    public void TheLevelAboveIsFiledInTheOrderItIsLaid() =>
        AssertFiledAsLaid(GroundMesh.Build(TracedPlanTests.OverAStreet(), SimConfig.Shipped()), GroundPart.Above);

    /// <summary>
    /// <b>The water is drawn in the order it is laid</b> (TER-7b): its shore, the shore's two edges, then the water — so
    /// filed out of the mesh's order the shore's triangles are drawn over the sea. Asked of a sloping coast
    /// (<see cref="TracedPlanTests.OnTheCoast"/>), whose triangles reach across as many bands again on one side as on
    /// the other.
    /// </summary>
    [Fact]
    public void TheWaterIsFiledInTheOrderItIsLaid() =>
        AssertFiledAsLaid(GroundMesh.Build(TracedPlanTests.OnTheCoast(), SimConfig.Shipped()), GroundPart.Water);

    static void AssertFiledAsLaid(GroundMesh mesh, GroundPart part)
    {
        GroundBands.Lay(mesh, out var filed);

        var laid = mesh.Parts[(int)part];
        Assert.True(laid.IndexCount > 0, $"the town staged has no {part}");
        Assert.Equal(
            mesh.Indices.Slice(laid.FirstIndex, laid.IndexCount).ToArray(),
            filed.AsSpan(laid.FirstIndex, laid.IndexCount).ToArray());
    }
}
