using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Runtime;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// The read-out's panel: that it is laid out where it says it is, that a section opens and shuts on
/// its own header, and that the panel takes the clicks that land on it rather than letting them
/// through to the town it is drawn over.
/// </summary>
[Collection(Simulation.SolverCollection.Name)]
[Trait(Tier.Key, Tier.Unit)]
public class FrameReadoutTests
{
    static readonly Vector2 UiPx = new(1600f, 900f);

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

    static int Draw(FrameReadout readout, TownWorld world, Vector2 pointerPx, in FrameFigures frame)
    {
        var draw = new ScreenDraw(new OverlayQuad[TownRenderer.OverlayCapacity]);
        readout.Draw(ref draw, UiPx, pointerPx, frame, crossings: 5, quads: 311, world, relaid: false);
        return draw.Written;
    }

    static TownWorld Town() => new(Towns.Fresh(Towns.Fixture), SimConfig.Shipped());

    /// <summary>
    /// <b>A section shuts on its own header and opens on it again</b>, which is the whole of the
    /// interaction: there is nothing else on the panel to press.
    /// </summary>
    [Fact]
    public void AHeaderShutsItsOwnSectionAndOpensItAgain()
    {
        using var world = Town();
        var readout = new FrameReadout();
        var figures = Measured();
        Draw(readout, world, -Vector2.One, figures);

        var header = readout.Box.AtPx + new Vector2(readout.Box.SizePx.X * 0.5f, Theme.PaddingPx + 2f);

        Assert.True(readout.IsOpen(FrameReadout.Frame));
        Assert.True(readout.Click(header));
        Assert.False(readout.IsOpen(FrameReadout.Frame));

        Draw(readout, world, -Vector2.One, figures);
        Assert.True(readout.Click(header));
        Assert.True(readout.IsOpen(FrameReadout.Frame));
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
        var readout = new FrameReadout();
        var figures = Measured();

        var whole = Draw(readout, world, -Vector2.One, figures);
        var tall = readout.Box.SizePx.Y;

        readout.Click(readout.Box.AtPx + new Vector2(readout.Box.SizePx.X * 0.5f, Theme.PaddingPx + 2f));
        var collapsed = Draw(readout, world, -Vector2.One, figures);

        Assert.True(collapsed < whole);
        Assert.True(readout.Box.SizePx.Y < tall);
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
        var readout = new FrameReadout();
        Draw(readout, world, -Vector2.One, Measured());

        Assert.True(readout.Click(readout.Box.AtPx + readout.Box.SizePx * 0.5f));
        Assert.False(readout.Click(readout.Box.AtPx - new Vector2(8f, 0f)));
    }

    /// <summary>
    /// The offscreen path times no frames, so the panel says so rather than printing the zero it would
    /// come to — and it draws none of the rows that would be zeros under it.
    /// </summary>
    [Fact]
    public void AnUnmeasuredFrameSaysSoInsteadOfPrintingZeroes()
    {
        using var world = Town();
        var readout = new FrameReadout();

        var measured = Draw(readout, world, -Vector2.One, Measured());
        var unmeasured = Draw(readout, world, -Vector2.One, new FrameFigures
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
    [InlineData(FrameReadout.Frame)]
    [InlineData(FrameReadout.Tick)]
    [InlineData(FrameReadout.Town)]
    public void ThePanelIsExactlyAsTallAsTheRowsItWrote(int section)
    {
        using var world = Town();
        var readout = new FrameReadout();
        var figures = Measured();

        Draw(readout, world, -Vector2.One, figures);
        Assert.Equal(FrameReadout.HeightFor(readout.Rows), readout.Box.SizePx.Y, 3);

        // And with that section the other way about, since each one is counted separately.
        readout.Click(readout.Box.AtPx + new Vector2(readout.Box.SizePx.X * 0.5f, HeaderY(readout, section)));
        Draw(readout, world, -Vector2.One, figures);
        Assert.Equal(FrameReadout.HeightFor(readout.Rows), readout.Box.SizePx.Y, 3);

        // Including the path that has no frame to rank, which draws a header and none of its rows.
        Draw(readout, world, -Vector2.One, new FrameFigures { Phases = new PhaseTimes { Ticks = 60 } });
        Assert.Equal(FrameReadout.HeightFor(readout.Rows), readout.Box.SizePx.Y, 3);
    }

    /// <summary>Where a section's own header stands, given what is open above it.</summary>
    static float HeaderY(FrameReadout readout, int section)
    {
        var rows = section switch
        {
            FrameReadout.Frame => 0,
            FrameReadout.Tick => 1 + (readout.IsOpen(FrameReadout.Frame) ? 10 : 0),
            _ => 2 + (readout.IsOpen(FrameReadout.Frame) ? 10 : 0) + (readout.IsOpen(FrameReadout.Tick) ? 10 : 0),
        };

        return Theme.PaddingPx + 2f + rows * (Theme.SmallTextPx + 4f);
    }

    /// <summary>
    /// Rule 2 is about the frame as well as the tick: a panel redrawn sixty times a second must not
    /// allocate a byte doing it, whatever its sections are set to.
    /// </summary>
    [Fact]
    public void DrawingThePanelAllocatesNothing()
    {
        using var world = Town();
        var readout = new FrameReadout();
        var figures = Measured();
        var quads = new OverlayQuad[TownRenderer.OverlayCapacity];

        // Once through first, so the JIT has compiled everything the measured pass runs.
        for (var pass = 0; pass < 2; pass++) Fill(readout, world, quads, figures);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 100; pass++) Fill(readout, world, quads, figures);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    static void Fill(FrameReadout readout, TownWorld world, OverlayQuad[] quads, in FrameFigures figures)
    {
        var draw = new ScreenDraw(quads);
        readout.Draw(ref draw, UiPx, UiPx * 0.5f, figures, crossings: 5, quads: 311, world, relaid: true);
    }
}
