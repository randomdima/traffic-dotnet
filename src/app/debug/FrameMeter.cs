using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// What one window of frames came to: the mean frame, the rate that is, the worst frame in it, where
/// that frame's time went, and where the tick's went under it.
/// </summary>
internal readonly record struct FrameFigures
{
    /// <summary>The mean frame in the window, or zero before the first one closed.</summary>
    public double FrameMs { get; init; }

    /// <summary>
    /// The share of that frame this build spent working: the town stepped, the buffers filled, the
    /// commands submitted. <b>The figure to quote</b>, because it is the only one the build owns —
    /// and the rows under it add up to it, with <see cref="OtherMs"/> carrying whatever they missed.
    /// </summary>
    public double CpuMs { get; init; }

    /// <summary>
    /// The share spent blocked on the presentation engine — waiting for an image to draw into and for
    /// the frame before last to be done with. Under FIFO it is the whole of the pacing and it is the
    /// compositor's number rather than this build's.
    /// </summary>
    public double BlockedMs { get; init; }

    public double Fps { get; init; }

    /// <summary>The worst single frame in the window — the figure the mean is there to hide and this one is there to keep.</summary>
    public double WorstMs { get; init; }

    public double PumpMs { get; init; }

    public double InputMs { get; init; }

    /// <summary>
    /// <b>Every tick the frame ran, together.</b> Not a tick's cost: that is <see cref="Phases"/>, and
    /// the two are bridged by <see cref="TicksPerFrame"/>.
    /// </summary>
    public double SimMs { get; init; }

    public double SpritesMs { get; init; }

    public double InterfaceMs { get; init; }

    public double SubmitMs { get; init; }

    /// <summary>
    /// How many fixed ticks a frame ran, over the window. <b>The one figure that lets a per-tick row
    /// be checked against a per-frame one</b>: a tick costing 0.7 ms at 3.0 ticks a frame is the 2.1 ms
    /// of <see cref="SimMs"/>, and if it is not then something is running ticks nobody counted.
    /// </summary>
    public double TicksPerFrame { get; init; }

    /// <summary>The five phases accumulated over the window, which <see cref="PhaseTimes.MillisecondsPer"/> reads as a mean per tick.</summary>
    public PhaseTimes Phases { get; init; }

    /// <summary>The world's own drill-down inside two of those phases, read per tick the same way.</summary>
    public TickParts Sub { get; init; }

    /// <summary>
    /// What this build spent that none of the named rows claimed: the loop's own arithmetic, the
    /// clock, and whatever the scheduler took the thread away for. <b>Printed rather than dropped</b>
    /// — a budget that quietly failed to add up is a budget nobody can use to decide anything.
    /// </summary>
    public double OtherMs =>
        Math.Max(0d, CpuMs - (PumpMs + InputMs + SimMs + SpritesMs + InterfaceMs + SubmitMs));
}

/// <summary>
/// The frame read-out's figures, averaged over a short window rather than taken off the frame just
/// drawn (OBS-2b).
/// </summary>
/// <remarks>
/// <para>
/// <b>A per-frame figure is not readable.</b> Vsync, the scheduler and the swapchain move the frame
/// cost by milliseconds between one frame and the next, so a number redrawn sixty times a second
/// changes every digit every time and answers nothing. A window long enough to hold tens of frames
/// settles it without waiting long enough to hide a change somebody just made.
/// </para>
/// <para>
/// <b>The window's worst frame is published beside its mean</b>, because averaging is exactly what
/// hides the spike somebody switched the read-out on to see. The mean says what the run costs, the
/// worst says whether it stutters, and neither one answers for the other.
/// </para>
/// <para>
/// <b>The frame's own parts are averaged here and the tick's phases are not.</b> A part is measured
/// once a frame, so a window's mean is its sum over the frames it held;
/// <see cref="PhaseTimes"/> already divides by the ticks it accumulated, so leaving the loop's
/// counters standing for a whole window and reading them at its end <em>is</em> that window's mean per
/// tick — which is why <see cref="Frame"/> returns when they are to be reset rather than resetting
/// anything itself.
/// </para>
/// </remarks>
internal sealed class FrameMeter
{
    /// <summary>
    /// How long a window is: long enough to hold tens of frames, short enough that a change made with
    /// the read-out open shows within a moment of making it.
    /// </summary>
    const double WindowMs = 500d;

    FrameParts _sum;
    int _frames;
    double _worstMs;
    bool _steady;

    public FrameFigures Figures { get; private set; }

    /// <summary>
    /// One frame's cost and where it went, with the phase counters as they now stand. Returns whether
    /// the window closed on this frame and the caller is to reset those counters — <b>true only of a
    /// window that ran ticks</b>, so a frozen town keeps the phase figures it last earned instead of
    /// showing zeros.
    /// </summary>
    /// <remarks>
    /// <b>The frame is split into work and waiting because only one of them is this build's.</b> A
    /// windowed run under FIFO spends whatever is left of the refresh interval blocked, so the wall
    /// clock is the display's number: three runs of the same town at the same framing differ by
    /// milliseconds in the total and repeat to the third decimal in the part that is the work.
    /// </remarks>
    public bool Frame(in FrameParts parts, in PhaseTimes phases, in TickParts sub)
    {
        // Never the first frame: it carries the swapchain's own first submit and the town's first
        // fill, and a window holding it would price those into the steady state.
        if (!_steady)
        {
            _steady = true;
            return false;
        }

        _frames++;
        _sum.Add(parts);
        _worstMs = Math.Max(_worstMs, parts.WholeMs);

        // The first window is closed on its first frame rather than half a second in: a read-out that
        // opens blank reads as an instrument that is not working.
        var opening = Figures.FrameMs <= 0d;
        if (!opening && _sum.WholeMs < WindowMs) return false;

        var per = 1d / _frames;
        var meanMs = _sum.WholeMs * per;
        var meanBlockedMs = _sum.BlockedMs * per;
        var ticked = phases.Ticks > 0;
        Figures = new FrameFigures
        {
            FrameMs = meanMs,
            CpuMs = Math.Max(0d, meanMs - meanBlockedMs),
            BlockedMs = meanBlockedMs,
            Fps = meanMs > 0d ? 1000d / meanMs : 0d,
            WorstMs = _worstMs,
            PumpMs = _sum.PumpMs * per,
            InputMs = _sum.InputMs * per,
            SimMs = _sum.SimMs * per,
            SpritesMs = _sum.SpritesMs * per,
            InterfaceMs = _sum.InterfaceMs * per,
            SubmitMs = _sum.SubmitMs * per,
            TicksPerFrame = _sum.SimTicks * per,
            Phases = ticked ? phases : Figures.Phases,
            Sub = ticked ? sub : Figures.Sub,
        };

        _sum = default;
        _frames = 0;
        _worstMs = 0d;
        return ticked;
    }
}
