namespace TrafficSimulation.Multiplayer.Client;

/// <summary>
/// <b>How long a player's keys take to come back</b> (MP-3, MP-7): one input at a time is timed from the frame that read
/// its keys to the first snapshot saying the server had applied it or a later one, and on to the first frame posed at
/// that snapshot's tick — the first picture that can show what the keys did.
/// </summary>
/// <remarks>
/// <para>
/// <b>The first figure is the round trip and the server's hold together</b>: the frame the input waits to be pumped out,
/// the line both ways, the wait for a tick and then for a snapshot's tick, and the frame the snapshot waits to be read.
/// The second adds how far behind the server the picture is drawn. Neither can be split further without the server's
/// clock, which a snapshot does not carry.
/// </para>
/// <para>
/// Both are read off the frames' own clock, so each is good to a frame either end, and what the browser presents after
/// the frame is not in them.
/// </para>
/// </remarks>
internal sealed class InputLatency
{
    /// <summary>A sixteen-bit sequence at or past another, the short way round the wrap.</summary>
    const uint HalfWrap = 1u << 15;

    /// <summary>The input being timed, or nought for none.</summary>
    uint _timed;

    double _sentS;

    /// <summary>The server tick of the snapshot that answered the input timed, once one has.</summary>
    uint _answeredTick;

    bool _answered;

    LatencySpread _roundTrip = LatencySpread.None;
    LatencySpread _toPicture = LatencySpread.None;

    /// <summary>An input left at the local <paramref name="localS"/>; it is timed if none is being.</summary>
    public void Sent(uint sequence, double localS)
    {
        if (_timed != 0) return;

        _timed = sequence;
        _sentS = localS;
    }

    /// <summary>
    /// A snapshot of <paramref name="serverTick"/> read at the local <paramref name="localS"/>, saying the newest input
    /// applied by then was <paramref name="inputApplied"/> — the low sixteen bits of its sequence (<see cref="Wire.SnapshotHeader"/>).
    /// </summary>
    public void Answered(uint inputApplied, uint serverTick, double localS)
    {
        if (_timed == 0 || _answered || ((inputApplied - _timed) & 0xFFFFu) >= HalfWrap) return;

        _roundTrip = _roundTrip.With(localS - _sentS);
        _answeredTick = serverTick;
        _answered = true;
    }

    /// <summary>A frame posed at the fractional server <paramref name="tick"/>, at the local <paramref name="localS"/>.</summary>
    public void Drawn(double tick, double localS)
    {
        if (!_answered || tick < _answeredTick) return;

        _toPicture = _toPicture.With(localS - _sentS);
        _timed = 0;
        _answered = false;
    }

    /// <summary>What was timed since the last taken — keys to the snapshot answering them, and to the frame drawing it — and nothing held after.</summary>
    public (LatencySpread RoundTrip, LatencySpread ToPicture) Take()
    {
        var taken = (_roundTrip, _toPicture);
        (_roundTrip, _toPicture) = (LatencySpread.None, LatencySpread.None);
        return taken;
    }
}

/// <summary>How many times something was timed, and the least, the mean and the most of them, in seconds.</summary>
internal readonly record struct LatencySpread(int Count, double LeastS, double SumS, double MostS)
{
    public static LatencySpread None => new(0, double.PositiveInfinity, 0d, double.NegativeInfinity);

    public double MeanS => Count == 0 ? double.NaN : SumS / Count;

    public LatencySpread With(double s) => new(Count + 1, Math.Min(LeastS, s), SumS + s, Math.Max(MostS, s));
}
