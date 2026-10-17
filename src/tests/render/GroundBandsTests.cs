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
    public void TheLevelAboveIsFiledInTheOrderItIsLaid()
    {
        var mesh = GroundMesh.Build(TracedPlanTests.OverAStreet(), SimConfig.Shipped());
        GroundBands.Lay(mesh, out var filed);

        var above = mesh.Parts[(int)GroundPart.Above];
        Assert.True(above.IndexCount > 0, "the town staged has nothing above its ground");
        Assert.Equal(
            mesh.Indices.Slice(above.FirstIndex, above.IndexCount).ToArray(),
            filed.AsSpan(above.FirstIndex, above.IndexCount).ToArray());
    }
}
