using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Gen;

[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P8)]
public class LitJunctionsTests
{
    /// <summary>
    /// <b>A junction is lit more often the more arms it has</b> (TLT-3): the draw is weighted by the movements each
    /// admits, so of as many tees as crossroads a larger share of the crossroads is lit. Asked of the draw alone, since a
    /// town lights only where its roads between districts meet (GEN-68) and a small one has a handful of those.
    /// </summary>
    [Fact]
    public void ACrossroadsIsLitMoreOftenThanATee()
    {
        const int Each = 200;
        var arms = new int[Each * 2];
        for (var junction = 0; junction < arms.Length; junction++) arms[junction] = junction < Each ? 3 : 4;

        var draw = new Rng(20261026, 0);
        var lit = LitJunctions.Picked(arms, new bool[arms.Length], 0.5f, ref draw);

        var teesLit = lit.AsSpan(0, Each).Count(true);
        var crossroadsLit = lit.AsSpan(Each).Count(true);
        Assert.True(crossroadsLit > teesLit, $"{crossroadsLit} of {Each} crossroads lit against {teesLit} of {Each} tees");
    }
}
