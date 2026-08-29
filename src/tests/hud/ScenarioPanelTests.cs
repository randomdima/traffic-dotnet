using System.Numerics;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// The panel that says what the map on screen claims about itself: that it is as tall as the rows it
/// wrote, that the title opens and shuts the body, that it takes the clicks that land on it, and that
/// drawing it allocates nothing.
/// </summary>
/// <remarks>
/// <b>What the claims themselves come to is not asked here.</b> That is the watch's own question and is
/// answered by the tier that runs the map (<c>TrackFiguresTests</c>, <c>JunctionExamTests</c>); this is
/// about the panel, which is why the fixture map will do.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class ScenarioPanelTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Vector2 Window = new(1600f, 900f);

    static TownWorld Town() => new(Towns.Of(Towns.Fixture), Config);

    static int Draw(ScenarioPanel panel, ScenarioWatch[] watching, Vector2 pointerPx)
    {
        var draw = new ScreenDraw(new OverlayQuad[TownRenderer.OverlayCapacity]);
        panel.Draw(ref draw, Window, pointerPx, "Test", watching);
        return draw.Written;
    }

    /// <summary>
    /// <b>The panel is as tall as the rows it wrote.</b> It is sized from a count taken before a word of it
    /// goes down, so a watch that grew a row without the count following it draws that row through the
    /// panel's own bottom edge.
    /// </summary>
    [Fact]
    public void ThePanelIsTheHeightOfWhatItWrote()
    {
        using var world = Town();
        var watching = Scenarios.For(world, Config);
        var panel = new ScenarioPanel();

        Draw(panel, watching, -Vector2.One);
        Assert.Equal(ScenarioPanel.HeightFor(0), panel.Box.SizePx.Y, 0.01f);

        panel.Show();
        Draw(panel, watching, -Vector2.One);
        Assert.Equal(ScenarioPanel.HeightFor(panel.Rows), panel.Box.SizePx.Y, 0.01f);
    }

    /// <summary>
    /// <b>It stands along the bottom and keeps clear of the legend</b>, which is the one thing in that
    /// corner drawn straight on the town with nothing behind it.
    /// </summary>
    [Fact]
    public void ItStandsAtTheBottomInsideTheWindow()
    {
        using var world = Town();
        var panel = new ScenarioPanel();
        panel.Show();
        Draw(panel, Scenarios.For(world, Config), -Vector2.One);

        Assert.True(panel.Box.AtPx.X >= 0f, "the panel starts off the left edge");
        Assert.True(panel.Box.Bottom <= Window.Y, "the panel runs off the bottom of the window");
        Assert.True(panel.Box.Right < Window.X, "the panel runs into the corner the legend has");
        Assert.True(panel.Box.AtPx.Y > Window.Y * 0.5f, "the panel is not at the bottom at all");
    }

    /// <summary>
    /// <b>The title opens the body and shuts it again</b>, which is the whole of the interaction: there is
    /// nothing else on the panel to press.
    /// </summary>
    [Fact]
    public void TheTitleOpensTheBodyAndShutsItAgain()
    {
        using var world = Town();
        var watching = Scenarios.For(world, Config);
        var panel = new ScenarioPanel();

        var shut = Draw(panel, watching, -Vector2.One);
        Assert.False(panel.Open);
        Assert.Equal(0, panel.Rows);

        Assert.True(panel.Click(Middle(panel)));
        var open = Draw(panel, watching, -Vector2.One);

        Assert.True(panel.Open);
        Assert.True(panel.Rows > 0, "an open panel wrote no rows");
        Assert.True(open > shut, "opening the panel cost no more quads");

        Assert.True(panel.Click(Middle(panel)));
        Draw(panel, watching, -Vector2.One);
        Assert.False(panel.Open);
    }

    /// <summary>
    /// <b>A click that lands on the panel is the panel's.</b> A read-out whose rows could be clicked
    /// through was a read-out that selected whatever body was behind it.
    /// </summary>
    [Fact]
    public void ThePanelTakesEveryClickThatLandsOnIt()
    {
        using var world = Town();
        var panel = new ScenarioPanel();
        panel.Show();
        Draw(panel, Scenarios.For(world, Config), -Vector2.One);

        Assert.True(panel.Click(panel.Box.AtPx + (panel.Box.SizePx * 0.5f)));
        Assert.False(panel.Click(panel.Box.AtPx - new Vector2(0f, 8f)));
    }

    /// <summary>
    /// <b>A map with nothing to claim draws nothing at all</b>, and takes no clicks either — a box left
    /// standing over the town from a map that had claims would swallow the corner of every map opened
    /// after it.
    /// </summary>
    [Fact]
    public void NothingClaimedIsNothingDrawn()
    {
        var panel = new ScenarioPanel();
        panel.Show();

        Assert.Equal(0, Draw(panel, [], -Vector2.One));
        Assert.False(panel.Click(Vector2.Zero));
    }

    /// <summary>
    /// <b>Drawing it allocates nothing</b> (rule 2). It is drawn every frame of every run, and every line
    /// on it is a claim's own text written into a buffer on the stack.
    /// </summary>
    [Fact]
    public void DrawingThePanelAllocatesNothing()
    {
        using var world = Town();
        var watching = Scenarios.For(world, Config);
        var quads = new OverlayQuad[TownRenderer.OverlayCapacity];
        var panel = new ScenarioPanel();
        panel.Show();

        Fill(panel, watching, quads);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 32; frame++) Fill(panel, watching, quads);

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    static void Fill(ScenarioPanel panel, ScenarioWatch[] watching, OverlayQuad[] quads)
    {
        var draw = new ScreenDraw(quads);
        panel.Draw(ref draw, Window, -Vector2.One, "Test", watching);
    }

    static Vector2 Middle(ScenarioPanel panel) =>
        panel.Box.AtPx + new Vector2(panel.Box.SizePx.X * 0.5f, Theme.GapPx + (Theme.TextPx * 0.5f));
}
