using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Town;
using Xunit;

namespace TrafficSimulation.Tests.Hud;

/// <summary>
/// The claims section of the status panel: that it is there on a scenario and on nothing else, that a
/// broken claim is on the always-on title rather than two collapses down, that the panel is as tall as
/// the rows it wrote and one width whatever is collapsed, and that drawing it allocates nothing.
/// </summary>
/// <remarks>
/// <b>What the claims themselves come to is not asked here.</b> That is the watch's own question and is
/// answered by the tier that runs the map (<c>JunctionExamTests</c>); this is
/// about the read-out, which is why a stub watch and the fixture map will do.
/// </remarks>
[Trait(Tier.Key, Tier.Unit)]
public class StatusPanelClaimsTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>A verdict the panel has to draw, handed to it rather than played out on a town.</summary>
    sealed class StubWatch(ClaimVerdict verdict)
        : ScenarioWatch("the stub", "what a watch looks like to a panel", ["a claim about the town"], ["a figure"])
    {
        public override ClaimVerdict Verdict(int claim) => verdict;

        public override void Says(int claim, ref TextBuffer into) => into.Add("the figures behind it");

        public override void Reads(int reading, ref TextBuffer into) => into.Add("a quoted figure");

        public override void Saw(TownWorld world)
        {
        }
    }

    static TownWorld Town() => new(Towns.Of(Towns.Fixture), Config);

    static FrameFigures Measured() => new()
    {
        FrameMs = 16d,
        CpuMs = 4d,
        Fps = 60d,
        Phases = new PhaseTimes { Ticks = 60, WholeTicks = 40 },
    };

    static int Draw(StatusPanel panel, TownWorld world, ReadOnlySpan<ScenarioWatch> claims)
    {
        var draw = new ScreenDraw(new OverlayQuad[TownRenderer.OverlayCapacity]);
        panel.Draw(
            ref draw, -Vector2.One, "Test", new RunState(), tick: 1234, Measured(), crossings: 5, counting: true,
            quads: 311, world, relaid: false, claims);
        return draw.Written;
    }

    static StatusPanel Opened()
    {
        var panel = new StatusPanel();
        panel.Show();
        return panel;
    }

    /// <summary>
    /// <b>A map that claims nothing draws no claims.</b> A place is a town somebody plays, and a
    /// laboratory read-out over a city is a read-out with no question behind it — so the section is not
    /// drawn, is not counted in the height, and does not widen the bar.
    /// </summary>
    [Fact]
    public void NothingClaimedIsNothingDrawn()
    {
        using var world = Town();
        var panel = Opened();

        var bare = Draw(panel, world, default);
        var bareRows = panel.Rows;
        var barePx = panel.Box.SizePx;

        var watched = Draw(panel, world, new ScenarioWatch[] { new StubWatch(ClaimVerdict.Kept) });

        Assert.True(watched > bare, "a watched map drew no more of the panel than an unwatched one");
        Assert.True(panel.Rows > bareRows, "the claims section wrote no rows");
        Assert.True(panel.Box.SizePx.Y > barePx.Y, "the claims section took no height");
        Assert.True(panel.Box.SizePx.X > barePx.X, "the claims rows were not budgeted for");
    }

    /// <summary>
    /// <b>The place map's own answer, at the other end of the gate.</b> The panel draws what it is handed
    /// and the catalogue decides what that is, so a shipped place that started reading as a scenario would
    /// put its test results back over a city and nothing else would say so.
    /// </summary>
    [Fact]
    public void OnlyAScenarioMapIsReadAsOne()
    {
        foreach (var entry in MapCatalogue.Catalogued.ToArray())
        {
            Assert.Equal(entry.Kind == MapKind.Scenario, MapCatalogue.IsScenario(entry.Name));
        }

        Assert.False(MapCatalogue.IsScenario("Odesa"));
        Assert.True(MapCatalogue.IsScenario("Test"));
    }

    /// <summary>
    /// <b>A broken claim is on the line that is always on screen.</b> The panel is shut by default and so
    /// is everything under it: a town that has broken one of its own claims has to say so without being
    /// asked, or the read-out is two collapses deep and nobody ever opens it (OBS-2i).
    /// </summary>
    [Fact]
    public void ABrokenClaimShowsOnTheShutBar()
    {
        using var world = Town();
        var panel = new StatusPanel();

        var kept = Draw(panel, world, new ScenarioWatch[] { new StubWatch(ClaimVerdict.Kept) });
        Assert.False(panel.Open);

        var broken = Draw(panel, world, new ScenarioWatch[] { new StubWatch(ClaimVerdict.Broken) });
        Assert.True(broken > kept, "a shut panel said nothing about a broken claim");
    }

    /// <summary>
    /// <b>Neither state moves when a claim breaks.</b> Both widths are budgets rather than measurements,
    /// so the bar somebody is reading does not grow the moment the thing it is about goes wrong.
    /// </summary>
    [Fact]
    public void TheBarIsOneWidthBrokenOrKept()
    {
        using var world = Town();
        var panel = new StatusPanel();

        Draw(panel, world, new ScenarioWatch[] { new StubWatch(ClaimVerdict.Waiting) });
        var barPx = panel.Box.SizePx.X;

        Draw(panel, world, new ScenarioWatch[] { new StubWatch(ClaimVerdict.Broken) });
        Assert.Equal(barPx, panel.Box.SizePx.X);

        // And the open panel is one width whether the claims are showing or collapsed.
        panel.Show();
        Draw(panel, world, new ScenarioWatch[] { new StubWatch(ClaimVerdict.Kept) });
        var bodyPx = panel.Box.SizePx.X;

        panel.Click(ClaimsHeaderPx(panel));
        Draw(panel, world, new ScenarioWatch[] { new StubWatch(ClaimVerdict.Kept) });
        Assert.False(panel.IsOpen(StatusPanel.Claims));
        Assert.Equal(bodyPx, panel.Box.SizePx.X);
    }

    /// <summary>
    /// <b>The panel is sized from a row count taken before any row is written</b>, and the claims are the
    /// section whose rows are a watch's and not the panel's: a watch that grew a claim without the count
    /// following it draws that row through the panel's own bottom edge.
    /// </summary>
    [Fact]
    public void ThePanelIsExactlyAsTallAsTheClaimsItWrote()
    {
        using var world = Town();
        var panel = Opened();
        var watching = Scenarios.For(world, Config);

        Draw(panel, world, watching);
        Assert.True(panel.IsOpen(StatusPanel.Claims));
        Assert.Equal(StatusPanel.HeightFor(panel.Rows), panel.Box.SizePx.Y, 3);

        Assert.True(panel.Click(ClaimsHeaderPx(panel)));
        Draw(panel, world, watching);
        Assert.False(panel.IsOpen(StatusPanel.Claims));
        Assert.Equal(StatusPanel.HeightFor(panel.Rows), panel.Box.SizePx.Y, 3);
    }

    /// <summary>
    /// Rule 2: the section is redrawn every frame of every scenario run, and every line on it is a claim's
    /// own text written into a buffer on the stack.
    /// </summary>
    [Fact]
    public void DrawingTheClaimsAllocatesNothing()
    {
        using var world = Town();
        var panel = Opened();
        var watching = Scenarios.For(world, Config);
        var quads = new OverlayQuad[TownRenderer.OverlayCapacity];
        var run = new RunState();
        var figures = Measured();

        for (var pass = 0; pass < 2; pass++) Fill(panel, world, quads, run, figures, watching);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var pass = 0; pass < 64; pass++) Fill(panel, world, quads, run, figures, watching);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    static void Fill(
        StatusPanel panel, TownWorld world, OverlayQuad[] quads, RunState run, in FrameFigures figures,
        ReadOnlySpan<ScenarioWatch> claims)
    {
        var draw = new ScreenDraw(quads);
        panel.Draw(
            ref draw, new Vector2(20f, 20f), "Test", run, tick: 1234, figures, crossings: 5,
            counting: true, quads: 311, world, relaid: true, claims);
    }

    /// <summary>Where the claims header stands: last of the sections, under whatever the ones above it are showing.</summary>
    static Vector2 ClaimsHeaderPx(StatusPanel panel)
    {
        var rows = 3
                   + (panel.IsOpen(StatusPanel.Frame) ? 10 : 0)
                   + (panel.IsOpen(StatusPanel.Tick) ? 10 : 0)
                   + (panel.IsOpen(StatusPanel.Town) ? 5 : 0);

        // One row under the title for the tick the run has reached, then the sections.
        return panel.Box.AtPx
               + new Vector2(
                   panel.Box.SizePx.X * 0.5f, StatusPanel.BodyTopPx + ((rows + 1) * (Theme.SmallTextPx + 4f)));
    }
}
