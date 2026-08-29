using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

[Trait(Tier.Key, Tier.Unit)]
public class WorldScaleTests
{
    [Fact]
    public void TheTwoConversionsAreEachOthersInverse()
    {
        var scale = new WorldScale(SimConfig.Shipped().View.ArtPixelsPerMetre);

        Assert.Equal(7.5f, scale.MetresFromArtPixels(scale.ArtPixelsFromMetres(7.5f)), tolerance: 1e-4f);
    }

    /// <summary>
    /// The camera's default span is what the shipped window opens on: the whole of the short side and no
    /// more, so the two figures are one framing rather than two settings that happen to sit beside each
    /// other.
    /// </summary>
    [Fact]
    public void TheCameraOpensOnTheShortSideOfTheShippedWindow()
    {
        var config = SimConfig.Shipped();

        var pixelsPerMetre = WorldScale.ScreenPixelsPerMetre(config.View.CameraDefaultViewM, config.View.WindowHeightPx);

        Assert.Equal(config.View.WindowHeightPx, pixelsPerMetre * config.View.CameraDefaultViewM, tolerance: 1e-3f);
    }

    /// <summary>
    /// The camera sizes itself from the interface's pixels, so a window with twice as many of them
    /// over the same span is at twice the scale — and a desktop that merely draws each of them on
    /// four of the display's changes nothing here, which is the whole point of the space.
    /// </summary>
    [Fact]
    public void TwiceTheInterfacePixelsOverTheSameSpanIsTwiceTheScale()
    {
        var logical = WorldScale.ScreenPixelsPerMetre(70f, 1080);
        var scaled = WorldScale.ScreenPixelsPerMetre(70f, 2160);

        Assert.Equal(logical * 2f, scaled, tolerance: 1e-4f);
    }
}
