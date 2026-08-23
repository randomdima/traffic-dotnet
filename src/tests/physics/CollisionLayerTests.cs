using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// The one rule about layers: <b>two bodies interact when either scans the
/// other</b> — and the closure that turns a solver's <c>&amp;&amp;</c> into it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CollisionLayerTests
{
    /// <summary>
    /// Static geometry scans nothing at all, and a car still hits a building. Without the closure this
    /// is the case that fails, and it fails as a car driving through the town's walls.
    /// </summary>
    [Fact]
    public void SomethingScannedByNothingIsStillHit()
    {
        Assert.True(CollisionLayers.Interact(CollisionLayer.Car, CollisionLayer.Static));
        Assert.True(CollisionLayers.Interact(CollisionLayer.Static, CollisionLayer.Car));
        Assert.True(CollisionLayers.Interact(CollisionLayer.Person, CollisionLayer.Static));
    }

    [Fact]
    public void EveryPairOfLayersAgreesInBothDirections()
    {
        for (var first = 0; first < CollisionLayers.Count; first++)
        {
            for (var second = 0; second < CollisionLayers.Count; second++)
            {
                Assert.Equal(
                    CollisionLayers.Interact(CollisionLayers.Bit(first), CollisionLayers.Bit(second)),
                    CollisionLayers.Interact(CollisionLayers.Bit(second), CollisionLayers.Bit(first)));
            }
        }
    }

    /// <summary>
    /// The rule, against the solver's own test: Box2D asks <c>(catA &amp; maskB) &amp;&amp; (catB &amp;
    /// maskA)</c>, and over the symmetrised masks that has to answer exactly what "either scans the
    /// other" answers over the declaration.
    /// </summary>
    [Theory]
    [InlineData(0b001UL, 0b010UL)]
    [InlineData(0b000UL, 0b110UL)]
    [InlineData(0b100UL, 0b001UL)]
    [InlineData(0b011UL, 0b000UL)]
    public void TheSolversAndOverTheMasksIsTheBriefsOrOverTheScans(ulong firstScans, ulong secondScans)
    {
        ReadOnlySpan<CollisionLayer> scans = [(CollisionLayer)firstScans, (CollisionLayer)secondScans, CollisionLayer.None];
        var masks = CollisionLayers.Symmetrise(scans);

        for (var first = 0; first < scans.Length; first++)
        {
            for (var second = 0; second < scans.Length; second++)
            {
                var eitherScans = (scans[first] & CollisionLayers.Bit(second)) != 0
                                  || (scans[second] & CollisionLayers.Bit(first)) != 0;
                var solverWould = (masks[second] & CollisionLayers.Bit(first)) != 0
                                  && (masks[first] & CollisionLayers.Bit(second)) != 0;

                Assert.Equal(eitherScans, solverWould);
            }
        }
    }

    /// <summary>A layer nobody scans and that scans nobody interacts with nothing — the closure adds no bit of its own.</summary>
    [Fact]
    public void ClosureInventsNothing()
    {
        ReadOnlySpan<CollisionLayer> scans = [CollisionLayer.None, CollisionLayer.None, CollisionLayer.None];

        foreach (var mask in CollisionLayers.Symmetrise(scans)) Assert.Equal(CollisionLayer.None, mask);
    }
}
