using System.Numerics;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// The proving ground's panel: that it is laid out where it says it is, that a section opens and shuts on
/// its own header, that the rows it writes are the rows it was sized for, and that it takes the clicks that
/// land on it rather than letting them through to the track it is drawn over.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class TrackPanelTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static TownWorld Track() => new(Towns.Of(TrackPlan.Name), Config);

    static int Draw(TrackPanel panel, TrackMetrics metrics, Vector2 pointerPx)
    {
        var draw = new ScreenDraw(new OverlayQuad[TownRenderer.OverlayCapacity]);
        panel.Draw(ref draw, pointerPx, topY: 120f, metrics);
        return draw.Written;
    }

    /// <summary>
    /// <b>The panel is as tall as the rows it wrote.</b> It is sized from a count taken before a word of it
    /// goes down, so a section that grew a row without the count following it draws that row through the
    /// panel's own bottom edge.
    /// </summary>
    [Fact]
    public void ThePanelIsTheHeightOfWhatItWrote()
    {
        using var world = Track();
        var metrics = new TrackMetrics(Config, world);
        var panel = new TrackPanel();

        Draw(panel, metrics, -Vector2.One);
        Assert.Equal(TrackPanel.HeightFor(panel.Rows), panel.Box.SizePx.Y, 0.01f);

        // And again with every section open, which is the tallest it ever is.
        for (var shape = 0; shape < metrics.Shapes; shape++)
        {
            if (!panel.IsOpen(shape)) panel.Click(HeaderOf(panel, shape));

            Draw(panel, metrics, -Vector2.One);
        }

        Assert.Equal(TrackPanel.HeightFor(panel.Rows), panel.Box.SizePx.Y, 0.01f);
    }

    /// <summary>
    /// <b>A section shuts on its own header and opens on it again</b>, which is the whole of the
    /// interaction: there is nothing else on the panel to press.
    /// </summary>
    [Fact]
    public void AHeaderShutsItsOwnSectionAndOpensItAgain()
    {
        using var world = Track();
        var metrics = new TrackMetrics(Config, world);
        var panel = new TrackPanel();
        Draw(panel, metrics, -Vector2.One);

        var wasOpen = panel.IsOpen(0);
        Assert.True(panel.Click(HeaderOf(panel, 0)));
        Assert.Equal(!wasOpen, panel.IsOpen(0));

        Draw(panel, metrics, -Vector2.One);
        Assert.True(panel.Click(HeaderOf(panel, 0)));
        Assert.Equal(wasOpen, panel.IsOpen(0));
    }

    /// <summary>
    /// A collapsed section is a shorter panel and fewer quads. <b>Collapsing that cost the same is a panel
    /// that only looks smaller</b>, and the point of the sections is that the instrument gets quieter and
    /// cheaper together.
    /// </summary>
    [Fact]
    public void OpeningASectionDrawsMoreOfIt()
    {
        using var world = Track();
        var metrics = new TrackMetrics(Config, world);
        var panel = new TrackPanel();

        var shut = Draw(panel, metrics, -Vector2.One);
        var shutRows = panel.Rows;

        Assert.True(panel.Click(HeaderOf(panel, metrics.Shapes - 1)));
        var open = Draw(panel, metrics, -Vector2.One);

        Assert.True(panel.Rows > shutRows, "opening a section wrote no more rows");
        Assert.True(open > shut, "opening a section cost no more quads");
    }

    /// <summary>
    /// <b>A click that lands on the panel is the panel's.</b> A read-out whose figures could be clicked
    /// through was a read-out that selected whatever car was behind it.
    /// </summary>
    [Fact]
    public void ThePanelTakesEveryClickThatLandsOnIt()
    {
        using var world = Track();
        var metrics = new TrackMetrics(Config, world);
        var panel = new TrackPanel();
        Draw(panel, metrics, -Vector2.One);

        Assert.True(panel.Click(panel.Box.AtPx + (panel.Box.SizePx * 0.5f)));
        Assert.False(panel.Click(panel.Box.AtPx + new Vector2(panel.Box.SizePx.X + 8f, 0f)));
    }

    /// <summary>The middle of a section's own header, as the last draw laid it.</summary>
    static Vector2 HeaderOf(TrackPanel panel, int shape) =>
        panel.HeaderOf(shape).AtPx + (panel.HeaderOf(shape).SizePx * 0.5f);
}
