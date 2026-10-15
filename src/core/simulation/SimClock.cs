using System.Diagnostics;

namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// Wall time, scaled by the game's speed, turned into whole fixed ticks: the pacing a real-time run needs and a
/// headless run does not. The timestep never varies — a town watched faster runs more ticks, never longer ones.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every physics tick is drawn</b> (SIM-9): at real time a call owes one tick at most, and a frame draws after its
/// ticks. A tick slower than the time it stands for slows the town rather than batching ticks into fewer frames — a
/// catch-up turns a tick a tenth over its budget into a frame eight ticks long, and never wins the time back.
/// </para>
/// <para>
/// The time scale is capped because stretching the physics delta integrates the whole simulation coarsely and
/// manufactures collisions.
/// </para>
/// </remarks>
internal sealed class SimClock(float tickSeconds, float maxTimeScale)
{
    readonly double _tickSeconds = tickSeconds;
    double _accumulator;
    long _stamp = Stopwatch.GetTimestamp();
    float _timeScale = 1f;

    /// <summary>
    /// The most ticks one call owes: one at real time, and as many as the time scale asks for a frame past it — a town
    /// watched faster is drawn at the display's rate, and the ticks between two frames are not drawn.
    /// </summary>
    public int MostTicksPerCall => Math.Max(1, (int)MathF.Ceiling(_timeScale));

    public float TimeScale
    {
        get => _timeScale;
        set => _timeScale = Math.Clamp(value, 0f, maxTimeScale);
    }

    /// <summary>Ticks owed since the last call. The caller runs exactly this many and no more.</summary>
    public int TicksDue()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = (now - _stamp) / (double)Stopwatch.Frequency;
        _stamp = now;

        _accumulator += elapsed * _timeScale;

        var due = (int)(_accumulator / _tickSeconds);
        if (due <= 0) return 0;

        var most = MostTicksPerCall;
        if (due > most)
        {
            // One tick owed past the cap is kept for the next call and the rest dropped. A display beating against the
            // tick's own period owes none on a frame a hair early and two on the one after it: dropped there, a town with
            // time to spare ran a tenth slow. Kept, a late frame slows the town by what it is late and no more.
            _accumulator = Math.Min(_accumulator - (most * _tickSeconds), _tickSeconds);
            return most;
        }

        _accumulator -= due * _tickSeconds;
        return due;
    }

    /// <summary>Forgets the time a stall spent outside the loop, so the first tick after it is an ordinary one.</summary>
    public void Resynchronise()
    {
        _stamp = Stopwatch.GetTimestamp();
        _accumulator = 0d;
    }
}
