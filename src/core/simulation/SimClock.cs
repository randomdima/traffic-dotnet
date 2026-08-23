using System.Diagnostics;

namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// Wall time turned into whole fixed ticks: the pacing a real-time run needs and a headless run does
/// not. The timestep never varies — a frame that took too long owes several ticks, not one long one.
/// </summary>
/// <remarks>
/// Both bounds stop the clock manufacturing a fault the model does not have. Catch-up is capped, so a
/// stall drops the time it could not simulate instead of running a hundred ticks back to back. The
/// time scale is capped because stretching the physics delta integrates the whole simulation coarsely
/// and manufactures collisions.
/// </remarks>
internal sealed class SimClock(float tickSeconds, float maxTimeScale)
{
    readonly double _tickSeconds = tickSeconds;
    double _accumulator;
    long _stamp = Stopwatch.GetTimestamp();
    float _timeScale = 1f;

    public int MaxTicksPerCall { get; init; } = 8;

    public float TimeScale
    {
        get => _timeScale;
        set => _timeScale = Math.Clamp(value, 0f, maxTimeScale);
    }

    /// <summary>Where the render stands between the last tick and the next, for interpolation. 0 on the tick itself.</summary>
    public float Alpha => (float)(_accumulator / _tickSeconds);

    /// <summary>Ticks owed since the last call. The caller runs exactly this many and no more.</summary>
    public int TicksDue()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = (now - _stamp) / (double)Stopwatch.Frequency;
        _stamp = now;

        _accumulator += elapsed * _timeScale;

        var due = (int)(_accumulator / _tickSeconds);
        if (due <= 0) return 0;

        if (due > MaxTicksPerCall)
        {
            _accumulator = 0d;
            return MaxTicksPerCall;
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
