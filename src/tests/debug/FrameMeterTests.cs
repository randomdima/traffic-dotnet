using TrafficSimulation.App.Debug;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Debug;

/// <summary>
/// The frame read-out's figures are a window's, not a frame's: what it publishes, when it publishes
/// it, what it keeps hold of while a town is frozen, and — the claim the whole panel rests on — that
/// the rows it publishes add up to the frame they are rows of.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class FrameMeterTests
{
    static PhaseTimes OverTicks(long ticks) => new() { Ticks = ticks, AgentTicks = 1, WholeTicks = 1 };

    /// <summary>A frame that did nothing but take the time it took, which is all most of these need.</summary>
    static FrameParts Frame(double wholeMs, double blockedMs = 0d, int simTicks = 1) =>
        new(timed: false) { WholeMs = wholeMs, BlockedMs = blockedMs, SimTicks = simTicks };

    /// <summary>The first frame carries the swapchain's own first submit, and is dropped rather than averaged in.</summary>
    [Fact]
    public void TheFirstFrameIsNotMeasured()
    {
        var meter = new FrameMeter();

        meter.Frame(Frame(900d), OverTicks(1), default);

        Assert.Equal(0d, meter.Figures.FrameMs);
    }

    /// <summary>A read-out that opened blank would read as an instrument that is not working.</summary>
    [Fact]
    public void TheSecondFrameOpensTheReadOutOnItsOwn()
    {
        var meter = new FrameMeter();
        meter.Frame(Frame(16d), OverTicks(1), default);

        meter.Frame(Frame(20d), OverTicks(1), default);

        Assert.Equal(20d, meter.Figures.FrameMs, 6);
        Assert.Equal(50d, meter.Figures.Fps, 6);
    }

    /// <summary>Within a window nothing is republished, however much the frames themselves move.</summary>
    [Fact]
    public void TheFiguresHoldStillUntilTheWindowCloses()
    {
        var meter = new FrameMeter();
        meter.Frame(Frame(10d), OverTicks(1), default);
        meter.Frame(Frame(10d), OverTicks(1), default);

        Assert.False(meter.Frame(Frame(40d), OverTicks(1), default));
        Assert.Equal(10d, meter.Figures.FrameMs, 6);
    }

    /// <summary>The mean over the window, the rate it comes to, and the one frame the mean hides.</summary>
    [Fact]
    public void AClosedWindowPublishesItsMeanItsRateAndItsWorstFrame()
    {
        var meter = new FrameMeter();
        meter.Frame(Frame(10d), OverTicks(1), default);
        meter.Frame(Frame(10d), OverTicks(1), default);

        // 500 ms of frames: 24 of 20 ms and one of 40, which is 520 ms over 25 frames.
        for (var frame = 0; frame < 24; frame++) meter.Frame(Frame(20d), OverTicks(1), default);
        var closed = meter.Frame(Frame(40d), OverTicks(1), default);

        Assert.True(closed);
        Assert.Equal(520d / 25d, meter.Figures.FrameMs, 6);
        Assert.Equal(1000d / (520d / 25d), meter.Figures.Fps, 6);
        Assert.Equal(40d, meter.Figures.WorstMs, 6);
    }

    /// <summary>
    /// <b>The frame is split into the part this build spent working and the part it spent waiting</b>,
    /// because a windowed run under FIFO is paced by the display and only the first of the two is a
    /// figure about the engine.
    /// </summary>
    [Fact]
    public void TheFrameIsSplitIntoWorkAndWaiting()
    {
        var meter = new FrameMeter();
        meter.Frame(Frame(16d, blockedMs: 14d), OverTicks(1), default);

        meter.Frame(Frame(16d, blockedMs: 14d), OverTicks(1), default);

        Assert.Equal(16d, meter.Figures.FrameMs, 6);
        Assert.Equal(2d, meter.Figures.CpuMs, 6);
        Assert.Equal(14d, meter.Figures.BlockedMs, 6);
    }

    /// <summary>
    /// <b>The two rates are the two halves of that split.</b> A frame of 16 ms with 14 of them waiting
    /// is 62.5 a second drawn and 500 a second of work — the second being the headroom, and the whole
    /// reason both are on the header rather than one.
    /// </summary>
    [Fact]
    public void TheCeilingIsTheRateTheWorkAloneWouldAllow()
    {
        var meter = new FrameMeter();
        meter.Frame(Frame(16d, blockedMs: 14d), OverTicks(1), default);

        meter.Frame(Frame(16d, blockedMs: 14d), OverTicks(1), default);

        Assert.Equal(1000d / 16d, meter.Figures.Fps, 6);
        Assert.Equal(1000d / 2d, meter.Figures.CeilingFps, 6);
    }

    /// <summary>
    /// <b>A run nothing paces has run out of headroom, and says so by quoting one figure twice.</b>
    /// There is no wait to take off the frame, so the rate it draws at is the rate its work allows.
    /// </summary>
    [Fact]
    public void AnUnpacedRunDrawsAtItsOwnCeiling()
    {
        var meter = new FrameMeter();
        meter.Frame(Frame(8d), OverTicks(1), default);

        meter.Frame(Frame(8d), OverTicks(1), default);

        Assert.Equal(meter.Figures.Fps, meter.Figures.CeilingFps, 6);
    }

    /// <summary>
    /// <b>The claim the panel exists to make: the rows add up to the frame.</b> A read-out whose parts
    /// summed to something other than its total could not be used to decide which row was worth going
    /// and fixing, which is the only thing anybody opens it for.
    /// </summary>
    [Fact]
    public void TheRowsAddUpToTheFrame()
    {
        var meter = new FrameMeter();
        var frame = new FrameParts(timed: false)
        {
            WholeMs = 16d,
            BlockedMs = 9d,
            PumpMs = 0.5d,
            InputMs = 0.25d,
            SimMs = 3d,
            SpritesMs = 1d,
            InterfaceMs = 1.5d,
            SubmitMs = 0.25d,
            SimTicks = 1,
        };

        meter.Frame(frame, OverTicks(1), default);
        meter.Frame(frame, OverTicks(1), default);

        var figures = meter.Figures;
        Assert.Equal(figures.FrameMs, figures.CpuMs + figures.BlockedMs, 6);
        Assert.Equal(
            figures.CpuMs,
            figures.PumpMs + figures.InputMs + figures.SimMs + figures.SpritesMs + figures.InterfaceMs +
            figures.SubmitMs + figures.OtherMs, 6);

        // And the part no named row claimed is the part that is left, printed rather than dropped.
        Assert.Equal(0.5d, figures.OtherMs, 6);
    }

    /// <summary>
    /// <b>Ticks a frame is what bridges a per-frame row to a per-tick one</b>: without it the tick
    /// section's figures cannot be checked against the frame section's simulation row at all.
    /// </summary>
    [Fact]
    public void TheWindowSaysHowManyTicksAFrameRan()
    {
        var meter = new FrameMeter();

        // Dropped, then the opening window, which closes on its own first frame.
        meter.Frame(Frame(250d, simTicks: 3), OverTicks(3), default);
        meter.Frame(Frame(250d, simTicks: 3), OverTicks(3), default);
        Assert.Equal(3d, meter.Figures.TicksPerFrame, 6);

        // And a window of two frames, one of which the clock owed nothing.
        meter.Frame(Frame(250d, simTicks: 4), OverTicks(4), default);
        meter.Frame(Frame(250d, simTicks: 0), OverTicks(4), default);

        Assert.Equal(2d, meter.Figures.TicksPerFrame, 6);
    }

    /// <summary>
    /// The phases are the window's own, and the meter says so by asking for a reset — a window that
    /// ran no ticks asks for none, so a frozen town keeps the figures it last earned.
    /// </summary>
    [Fact]
    public void AFrozenTownKeepsTheLastPhasesItEarned()
    {
        var meter = new FrameMeter();
        meter.Frame(Frame(10d), OverTicks(1), default);
        meter.Frame(Frame(10d), OverTicks(4), new TickParts { SolverTicks = 7 });
        var earned = meter.Figures.Phases;
        var earnedSub = meter.Figures.Sub;

        var reset = false;
        for (var frame = 0; frame < 60 && !reset; frame++) reset = meter.Frame(Frame(10d), default, default);

        Assert.False(reset);
        Assert.Equal(earned, meter.Figures.Phases);
        Assert.Equal(earnedSub, meter.Figures.Sub);
    }
}
