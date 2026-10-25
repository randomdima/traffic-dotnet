using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>OBJ-8's shadows as the cloud stages read them (<see cref="CloudShadows.At"/>): a cover of the sky, and a clear one drawing none.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P9)]
public class CloudShadowTests
{
    static CloudShadow ShadowsUnder(float coverShare)
    {
        var config = new SimConfig { Clouds = new CloudFigures { CoverShare = coverShare } };
        var clouds = new Clouds(config.Clouds, new Wind(config.Wind, seed: 7), seed: 7);
        return CloudShadows.At(clouds, config, tick: 0);
    }

    [Fact]
    public void AClearSkyCastsNoShadow() => Assert.Equal(0f, ShadowsUnder(0f).Look.W);

    /// <summary>The field's threshold is what is cloud: a fuller sky's is lower, and shades ground a thinner one leaves in the sun.</summary>
    [Fact]
    public void AFullerSkyIsCloudWhereAThinnerOneIsNot() =>
        Assert.True(ShadowsUnder(0.6f).Look.Y < ShadowsUnder(0.2f).Look.Y);

    [Fact]
    public void ACoverIsAShare() => Assert.Throws<ArgumentException>(() => ShadowsUnder(1.5f));
}
