using System.Diagnostics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// Wall time, scaled by the game's speed, turned into whole fixed ticks: the pacing a real-time run needs and a
/// headless run does not. The timestep never varies — a town watched faster runs more ticks, never longer ones.
/// </summary>
/// <remarks>
/// <para>
/// <b>The town keeps real time whatever the display's rate</b> (SIM-9): a frame owes every tick its span stood for and
/// draws the last, so a display at half the tick rate costs every other picture and never the town's pace.
/// </para>
/// <para>
/// <b>Only a tick slower than the time it stands for slows the town</b> (<see cref="MayRunAnother"/>). Catching up on
/// it costs as much time as it wins back: an eight-tick catch-up drew O30 at 7 fps against 54 fps at one tick a frame,
/// the town at 0.92 of real time either way. A frame still runs as many ticks as its speed asks however slow they are,
/// so a slow machine slows the town by what it is late and no more.
/// </para>
/// <para>
/// The time scale is capped because stretching the physics delta integrates the whole simulation coarsely and
/// manufactures collisions.
/// </para>
/// </remarks>
internal sealed class SimClock(SimConfig config)
{
    readonly double _tickSeconds = config.TickSeconds;
    readonly double _longestCaughtUpFrameS = config.Sim.LongestCaughtUpFrameS;
    readonly float _maxTimeScale = config.Sim.SoakMaxTimeScale;
    double _accumulator;
    long _stamp = Stopwatch.GetTimestamp();
    float _timeScale = 1f;

    /// <summary>The ticks a frame runs however long they take: one at real time, as many as the speed asks past it.</summary>
    public int TicksAtAnyCost => Math.Max(1, (int)MathF.Ceiling(_timeScale));

    /// <summary>
    /// The most ticks one call owes: a frame <see cref="SimFigures.LongestCaughtUpFrameS"/> long at the speed asked. A
    /// display slower than that, or a hitch, slows the town rather than being repaid as one frame many ticks long.
    /// </summary>
    public int MostTicksPerCall =>
        Math.Max(TicksAtAnyCost, (int)Math.Round(_longestCaughtUpFrameS * _timeScale / _tickSeconds));

    public float TimeScale
    {
        get => _timeScale;
        set => _timeScale = Math.Clamp(value, 0f, _maxTimeScale);
    }

    /// <summary>
    /// Ticks owed since the last call. The caller runs at most this many, and hands back what it did not run
    /// (<see cref="Unrun"/>, <see cref="Postpone"/>).
    /// </summary>
    public int TicksDue() => TicksDue(Stopwatch.GetTimestamp());

    /// <inheritdoc cref="TicksDue()"/>
    public int TicksDue(long nowStamp)
    {
        var elapsed = (nowStamp - _stamp) / (double)Stopwatch.Frequency;
        _stamp = nowStamp;

        _accumulator += elapsed * _timeScale;

        var due = (int)(_accumulator / _tickSeconds);
        if (due <= 0) return 0;

        var most = MostTicksPerCall;
        if (due > most)
        {
            _accumulator -= most * _tickSeconds;
            KeepOneTickAtMost();
            return most;
        }

        _accumulator -= due * _tickSeconds;
        return due;
    }

    /// <summary>
    /// Whether a frame that has run <paramref name="ran"/> ticks in <paramref name="spentS"/> of wall time runs another:
    /// always up to <see cref="TicksAtAnyCost"/>, and past it only while the ticks keep the pace asked.
    /// </summary>
    public bool MayRunAnother(int ran, double spentS) =>
        ran < TicksAtAnyCost || spentS * _timeScale <= ran * _tickSeconds;

    /// <summary>Ticks owed and refused by <see cref="MayRunAnother"/>, handed back.</summary>
    /// <remarks>
    /// <b>One is kept for the next call and the rest dropped.</b> A display beating against the tick's own period owes
    /// none on a frame a hair early and two on the one after; dropping the second ran a town with time to spare a tenth
    /// slow. Keeping them all would repay a slow tick's debt as a longer frame, which never wins the time back.
    /// </remarks>
    public void Unrun(int ticks)
    {
        if (ticks <= 0) return;

        _accumulator += ticks * _tickSeconds;
        KeepOneTickAtMost();
    }

    /// <summary>
    /// Ticks owed that the world could not take yet — a page's device answers between frames — kept for the call made once
    /// it can, up to <see cref="MostTicksPerCall"/>. Nothing about them was slow, so none is dropped that one call may run.
    /// </summary>
    public void Postpone(int ticks)
    {
        if (ticks <= 0) return;

        _accumulator = Math.Min(_accumulator + (ticks * _tickSeconds), MostTicksPerCall * _tickSeconds);
    }

    /// <summary>Forgets the time a stall spent outside the loop, so the first tick after it is an ordinary one.</summary>
    public void Resynchronise() => Resynchronise(Stopwatch.GetTimestamp());

    /// <inheritdoc cref="Resynchronise()"/>
    public void Resynchronise(long nowStamp)
    {
        _stamp = nowStamp;
        _accumulator = 0d;
    }

    void KeepOneTickAtMost() => _accumulator = Math.Min(_accumulator, _tickSeconds);
}
