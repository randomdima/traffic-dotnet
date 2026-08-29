using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// The status panel: that its title is always on screen and never changes width, that the body opens
/// and shuts on that title, that a section opens and shuts on its own header, and that the panel takes
/// the clicks that land on it rather than letting them through to the town it is drawn over.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class StatusPanelTests
{
    const float TitleRowPx = Theme.TextPx + 10f;

    const float RowPitchPx = Theme.SmallTextPx + 4f;

    /// <summary>A window's worth of figures, so every row the panel can draw has something to draw.</summary>
    static FrameFigures Measured() => new()
    {
        FrameMs = 16d,
        CpuMs = 4d,
        BlockedMs = 12d,
        Fps = 60d,
        WorstMs = 21d,
        SimMs = 2d,
        SpritesMs = 1d,
        InterfaceMs = 0.5d,
        SubmitMs = 0.25d,
        TicksPerFrame = 1d,
        Phases = new PhaseTimes { Ticks = 60, AgentTicks = 10, BodyTicks = 20, WholeTicks = 40 },
        Sub = new TickParts { WalkerTicks = 6, CarTicks = 4, SolverTicks = 15 },
    };

    static int Draw(StatusPanel panel, TownWorld world, Vector2 pointerPx, in FrameFigures frame)
    {
        var draw = new ScreenDraw(new OverlayQuad[TownRenderer.OverlayCapacity]);
        panel.Draw(
            ref draw, pointerPx, "Test", new RunState(), tick: 1234, frame, crossings: 5, quads: 311, world,
            relaid: false);
        return draw.Written;
    }

    static TownWorld Town() => new(Towns.Of(Towns.Fixture), SimConfig.Shipped());

    /// <summary>The middle of the title band, which is what opens and shuts the body under it.</summary>
    static Vector2 TitlePx(StatusPanel panel) =>
        panel.Box.AtPx + new Vector2(panel.Box.SizePx.X * 0.5f, Theme.GapPx + TitleRowPx * 0.5f);

    /// <summary>
    /// <b>The title is furniture and the body is the instrument.</b> The panel starts shut, so a run
    /// nobody asked for figures still says its rate, its map and its pace — and stamps no timestamps
    /// gathering the rest (OBS-2b).
    /// </summary>
    [Fact]
    public void ItStartsShutAndOpensOnItsOwnTitle()
    {
        using var world = Town();
        var panel = new StatusPanel();
        var figures = Measured();

        var shut = Draw(panel, world, -Vector2.One, figures);
        Assert.False(panel.Open);
        Assert.Equal(0, panel.Rows);

        Assert.True(panel.Click(TitlePx(panel)));
        var open = Draw(panel, world, -Vector2.One, figures);
        Assert.True(panel.Open);
        Assert.True(open > shut);

        Assert.True(panel.Click(TitlePx(panel)));
        Draw(panel, world, -Vector2.One, figures);
        Assert.False(panel.Open);
    }

    /// <summary>
    /// <b>The title's width is a budget and not a measurement.</b> A bar that grew a character when the
    /// rate went from 9 to 10 fps, or when the pace went to <c>held</c>, is a bar that moves while it
    /// is being read.
    /// </summary>
    [Fact]
    public void TheShutBarIsOneWidthWhateverItSays()
    {
        using var world = Town();
        var panel = new StatusPanel();

        Draw(panel, world, -Vector2.One, Measured());
        var widthPx = panel.Box.SizePx.X;

        Draw(panel, world, -Vector2.One, new FrameFigures { FrameMs = 8d, Fps = 144d });
        Assert.Equal(widthPx, panel.Box.SizePx.X);

        var held = new RunState();
        held.AgentsHeld = true;
        var draw = new ScreenDraw(new OverlayQuad[TownRenderer.OverlayCapacity]);
        panel.Draw(
            ref draw, -Vector2.One, "Zebras", held, tick: 999999, Measured(), crossings: 5, quads: 311, world,
            relaid: false);
        Assert.Equal(widthPx, panel.Box.SizePx.X);
    }

    /// <summary>
    /// <b>The shut bar is the width of its own line, not of the body it hides</b>, and the open panel is
    /// the width of the widest row it writes. A bar sized on the body reached a third of the way across
    /// the town to say four words.
    /// </summary>
    [Fact]
    public void TheBarIsNarrowerThanTheBodyItOpens()
    {
        using var world = Town();
        var panel = new StatusPanel();
        var figures = Measured();

        Draw(panel, world, -Vector2.One, figures);
        var barPx = panel.Box.SizePx.X;

        panel.Click(TitlePx(panel));
        Draw(panel, world, -Vector2.One, figures);
        var bodyPx = panel.Box.SizePx.X;
        Assert.True(barPx < bodyPx, $"the shut bar is {barPx} against a body of {bodyPx}");

        // And the body is one width whatever its sections are set to: a panel that narrowed as a
        // section collapsed is a column of figures that walks sideways under the eye.
        panel.Click(HeaderPx(panel, StatusPanel.Town));
        Draw(panel, world, -Vector2.One, figures);
        Assert.Equal(bodyPx, panel.Box.SizePx.X);
    }

    /// <summary>
    /// <b>A section shuts on its own header and opens on it again</b>, which is the whole of the
    /// interaction: there is nothing else on the body to press.
    /// </summary>
    [Fact]
    public void AHeaderShutsItsOwnSectionAndOpensItAgain()
    {
        using var world = Town();
        var panel = Opened();
        var figures = Measured();
        Draw(panel, world, -Vector2.One, figures);

        Assert.True(panel.IsOpen(StatusPanel.Frame));
        Assert.True(panel.Click(HeaderPx(panel, StatusPanel.Frame)));
        Assert.False(panel.IsOpen(StatusPanel.Frame));

        Draw(panel, world, -Vector2.One, figures);
        Assert.True(panel.Click(HeaderPx(panel, StatusPanel.Frame)));
        Assert.True(panel.IsOpen(StatusPanel.Frame));
    }

    /// <summary>
    /// A collapsed section is a shorter panel and fewer quads. <b>Collapsing that cost the same is a
    /// panel that only looks smaller</b>, and the point of the sections is that the instrument gets
    /// cheaper as well as quieter.
    /// </summary>
    [Fact]
    public void CollapsingASectionDrawsLessOfIt()
    {
        using var world = Town();
        var panel = Opened();
        var figures = Measured();

        var whole = Draw(panel, world, -Vector2.One, figures);
        var tall = panel.Box.SizePx.Y;

        panel.Click(HeaderPx(panel, StatusPanel.Frame));
        var collapsed = Draw(panel, world, -Vector2.One, figures);

        Assert.True(collapsed < whole);
        Assert.True(panel.Box.SizePx.Y < tall);
    }

    /// <summary>
    /// <b>The panel takes every click that lands on it.</b> A read-out drawn over a car whose figures
    /// could be clicked through was a read-out that selected that car, which is a selection nobody
    /// asked for and could not see themselves making.
    /// </summary>
    [Fact]
    public void TheWholePanelTakesAClickAndTheTownDoesNot()
    {
        using var world = Town();
        var panel = Opened();
        Draw(panel, world, -Vector2.One, Measured());

        Assert.True(panel.Click(panel.Box.AtPx + panel.Box.SizePx * 0.5f));
        Assert.False(panel.Click(panel.Box.AtPx - new Vector2(8f, 0f)));
    }

    /// <summary>
    /// The offscreen path times no frames, so the panel says so rather than printing the zero it would
    /// come to — and it draws none of the rows that would be zeros under it.
    /// </summary>
    [Fact]
    public void AnUnmeasuredFrameSaysSoInsteadOfPrintingZeroes()
    {
        using var world = Town();
        var panel = Opened();

        var measured = Draw(panel, world, -Vector2.One, Measured());
        var unmeasured = Draw(panel, world, -Vector2.One, new FrameFigures
        {
            Phases = new PhaseTimes { Ticks = 60, WholeTicks = 40 },
        });

        Assert.True(unmeasured < measured);
    }

    /// <summary>
    /// <b>The panel is sized from a row count taken before any row is written</b>, so the two have to
    /// be held together by something: a section that grows a row without the count following it draws
    /// that row through the panel's own bottom edge, and nothing else would say so.
    /// </summary>
    [Theory]
    [InlineData(StatusPanel.Frame)]
    [InlineData(StatusPanel.Tick)]
    [InlineData(StatusPanel.Town)]
    public void ThePanelIsExactlyAsTallAsTheRowsItWrote(int section)
    {
        using var world = Town();
        var panel = Opened();
        var figures = Measured();

        Draw(panel, world, -Vector2.One, figures);
        Assert.Equal(StatusPanel.HeightFor(panel.Rows), panel.Box.SizePx.Y, 3);

        // And with that section the other way about, since each one is counted separately.
        panel.Click(HeaderPx(panel, section));
        Draw(panel, world, -Vector2.One, figures);
        Assert.Equal(StatusPanel.HeightFor(panel.Rows), panel.Box.SizePx.Y, 3);

        // Including the path that has no frame to rank, which draws a header and none of its rows.
        Draw(panel, world, -Vector2.One, new FrameFigures { Phases = new PhaseTimes { Ticks = 60 } });
        Assert.Equal(StatusPanel.HeightFor(panel.Rows), panel.Box.SizePx.Y, 3);
    }

    /// <summary>
    /// Rule 2 is about the frame as well as the tick: a panel redrawn sixty times a second must not
    /// allocate a byte doing it, whatever its sections are set to.
    /// </summary>
    [Fact]
    public void DrawingThePanelAllocatesNothing()
    {
        using var world = Town();
        var panel = Opened();
        var figures = Measured();
        var quads = new OverlayQuad[TownRenderer.OverlayCapacity];
        var run = new RunState();

        // Once through first, so the JIT has compiled everything the measured pass runs.
        for (var pass = 0; pass < 2; pass++) Fill(panel, world, quads, run, figures);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 100; pass++) Fill(panel, world, quads, run, figures);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    static void Fill(StatusPanel panel, TownWorld world, OverlayQuad[] quads, RunState run, in FrameFigures figures)
    {
        var draw = new ScreenDraw(quads);
        panel.Draw(
            ref draw, new Vector2(20f, 20f), "Test", run, tick: 1234, figures, crossings: 5, quads: 311, world,
            relaid: true);
    }

    static StatusPanel Opened()
    {
        var panel = new StatusPanel();
        panel.Show();
        return panel;
    }

    /// <summary>Where a section's own header stands, given the run row and what is open above it.</summary>
    static Vector2 HeaderPx(StatusPanel panel, int section)
    {
        var rows = section switch
        {
            StatusPanel.Frame => 0,
            StatusPanel.Tick => 1 + (panel.IsOpen(StatusPanel.Frame) ? 10 : 0),
            _ => 2 + (panel.IsOpen(StatusPanel.Frame) ? 10 : 0) + (panel.IsOpen(StatusPanel.Tick) ? 10 : 0),
        };

        // One row under the title for the tick the run has reached, then the sections.
        return panel.Box.AtPx
               + new Vector2(panel.Box.SizePx.X * 0.5f, StatusPanel.BodyTopPx + (rows + 1) * RowPitchPx);
    }
}
