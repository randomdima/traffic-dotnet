namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>Ground an action swept once and committed to</b> (TER-4c.8): the ways under a body stood at stations along the
/// action's own path, each holder's kept as runs — one way under consecutive stations, no longer than a span — that
/// say where along the path the body is over them. Asked for, laid and given back as the holder goes by reading the
/// runs, and never by sweeping again.
/// </summary>
/// <remarks>
/// <b>A run is the union of what its stations cover</b>, so it holds a little more than the body does between them
/// and never less; the span bounds how much ground behind the holder a run keeps until the holder is past all of it.
/// </remarks>
internal sealed class SweptGround
{
    readonly SweptRun[] _runs;
    readonly int[] _count;
    readonly float[] _stationAtM;
    readonly int _mostEach;

    /// <param name="mostRunsEach">How many runs one holder may keep — a bound on the table, and not a figure behaviour reads.</param>
    public SweptGround(int holders, int mostRunsEach)
    {
        _mostEach = mostRunsEach;
        _runs = new SweptRun[holders * mostRunsEach];
        _count = new int[holders];
        _stationAtM = new float[holders];
    }

    public void Clear(int holder)
    {
        _count[holder] = 0;
        _stationAtM[holder] = float.NegativeInfinity;
    }

    /// <summary>
    /// <b>The ways under the body at one more station of the path</b>, <paramref name="atM"/> along it and never before
    /// the last: each merged into the run its way had at the station before while that run spans less than
    /// <paramref name="spanM"/>, and a run of its own otherwise. False where the holder has no room left for one.
    /// </summary>
    public bool Station(int holder, float atM, float spanM, ReadOnlySpan<WayCover> under)
    {
        var first = holder * _mostEach;
        ref var count = ref _count[holder];
        var previousAtM = _stationAtM[holder];
        foreach (ref readonly var cover in under)
        {
            // Runs are kept in the order of their first stations, so none before one already too long can be grown.
            var open = -1;
            for (var at = count - 1; at >= 0; at--)
            {
                ref readonly var run = ref _runs[first + at];
                if (atM - run.FirstAtM > spanM) break;
                if (run.Way != cover.Way || run.LastAtM != previousAtM) continue;

                open = at;
                break;
            }

            if (open >= 0)
            {
                ref var run = ref _runs[first + open];
                run = new SweptRun(run.Way, MathF.Min(run.FromM, cover.FromM), MathF.Max(run.ToM, cover.ToM), run.FirstAtM, atM);
                continue;
            }

            if (count == _mostEach) return false;

            _runs[first + count++] = new SweptRun(cover.Way, cover.FromM, cover.ToM, atM, atM);
        }

        _stationAtM[holder] = atM;
        return true;
    }

    /// <summary>A holder's runs, in the order of their first stations.</summary>
    public ReadOnlySpan<SweptRun> Of(int holder) => _runs.AsSpan(holder * _mostEach, _count[holder]);

    /// <summary>The same runs measured from a path that begins <paramref name="shiftM"/> further on.</summary>
    public void Shift(int holder, float shiftM)
    {
        var runs = _runs.AsSpan(holder * _mostEach, _count[holder]);
        foreach (ref var run in runs) run = run with { FirstAtM = run.FirstAtM - shiftM, LastAtM = run.LastAtM - shiftM };

        _stationAtM[holder] -= shiftM;
    }
}

/// <summary>
/// One way under a swept body: the stretch of it, in the way's own metres, that the body covers at any station from
/// <paramref name="FirstAtM"/> to <paramref name="LastAtM"/> along the path it was swept down.
/// </summary>
internal readonly record struct SweptRun(int Way, float FromM, float ToM, float FirstAtM, float LastAtM)
{
    public WayCover Cover => new(Way, FromM, ToM);
}
