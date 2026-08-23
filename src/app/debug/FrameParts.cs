using System.Diagnostics;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// Where one frame's wall time went, in the order the shell spends it: the events pumped, the hands
/// read, the ticks the clock was owed, the instance buffer filled, the interface written, the frame
/// submitted, and whatever the presenter made it wait for.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists so that the read-out's total is the frame rate's own arithmetic.</b> A tick figure
/// answers what a tick costs and says nothing about a frame — a frame runs however many ticks the
/// clock was owed, which at pace 3 is three and on a stalled frame is none — so a read-out that
/// ranked the tick and stopped was a read-out whose rows could not be added up to the number at the
/// top of it. Every row here is a share of *one frame*, and what none of them claimed is printed as
/// <c>other</c> rather than left out.
/// </para>
/// <para>
/// <b>Milliseconds and not stopwatch ticks</b>, which is the opposite of
/// <see cref="Shared.Simulation.PhaseTimes"/> and for the opposite reason: a phase is tens of
/// microseconds accumulated over a whole window, where a frame's part is a millisecond-scale figure
/// taken once a frame. There is nothing here for the integer to protect.
/// </para>
/// <para>
/// <b>Nothing is stamped unless something is looking.</b> The read-out's own switch is what turns
/// this on, exactly as it turns the loop's phase timing on, so a run nobody asked for figures takes
/// no timestamps at all (OBS-2b).
/// </para>
/// </remarks>
internal struct FrameParts
{
    static readonly double MillisecondsPerTick = 1000d / Stopwatch.Frequency;

    readonly bool _timed;
    long _stamp;

    public FrameParts(bool timed)
    {
        _timed = timed;
        _stamp = timed ? Stopwatch.GetTimestamp() : 0L;
    }

    /// <summary>The window's events, and the swapchain rebuild a resize costs.</summary>
    public double PumpMs;

    /// <summary>The keys, the pointer and the camera they drive.</summary>
    public double InputMs;

    /// <summary>Every tick the clock was owed this frame — the whole of them, and not one of them.</summary>
    public double SimMs;

    /// <summary>The bodies on screen, written into the mapped instance buffer.</summary>
    public double SpritesMs;

    /// <summary>The interface: the debug layers, the panels, the legend and this read-out itself.</summary>
    public double InterfaceMs;

    /// <summary>The frame's own five calls, less what the first of them spent waiting.</summary>
    public double SubmitMs;

    /// <summary>What the presenter made the frame wait for, which is the renderer's own figure and not this build's.</summary>
    public double BlockedMs;

    /// <summary>The frame end to end, measured once around all of the above.</summary>
    public double WholeMs;

    /// <summary>How many fixed ticks the clock was owed this frame. Zero on a frozen town, three at pace 3.</summary>
    public int SimTicks;

    public readonly bool Timed => _timed;

    /// <summary>Everything since the last mark, into one of the fields above.</summary>
    public void Mark(ref double into)
    {
        if (!_timed) return;

        var now = Stopwatch.GetTimestamp();
        into += (now - _stamp) * MillisecondsPerTick;
        _stamp = now;
    }

    /// <summary>
    /// One frame's parts into a running total, which is what a window is. <b>Every row is summed and
    /// none is recomputed</b>, so a window's <c>other</c> is the mean of the frames' own and not a
    /// residual of residuals.
    /// </summary>
    public void Add(in FrameParts frame)
    {
        PumpMs += frame.PumpMs;
        InputMs += frame.InputMs;
        SimMs += frame.SimMs;
        SpritesMs += frame.SpritesMs;
        InterfaceMs += frame.InterfaceMs;
        SubmitMs += frame.SubmitMs;
        BlockedMs += frame.BlockedMs;
        WholeMs += frame.WholeMs;
        SimTicks += frame.SimTicks;
    }
}
