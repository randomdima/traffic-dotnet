namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>Which ways' lists and which holds a layer holds</b>: every way's and every hold, as the host's own laying and a whole
/// mirror lay them — or, mirrored from a device that sends back only what is asked of it (<c>app/devicetick/DeviceRebuild</c>),
/// the lists of the ways readers asked about lately, every other way's planned list holding the lights' holds on it and
/// nothing else, and only the holds of the agents the town reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>A reader asks before it reads a way's lists</b> (<see cref="Knows"/>) — and asking marks the way asked about whether
/// it is known or not, so what readers keep asking about keeps being sent (<see cref="WaysAskedSince"/>). A question about
/// the lights alone needs no asking.
/// </para>
/// <para>
/// <b>A hold is read only by its own holder</b>, whose hold the town asks for before it reads it — so a hold read that this
/// layer was not sent is a defect, and throws.
/// </para>
/// </remarks>
internal sealed partial class LaneOccupancy
{
    /// <summary>Whether every hold is laid — or only those mirrored since the mirror began (<see cref="_holdKnownIn"/>).</summary>
    bool _knowsEveryHold = true;

    /// <summary>The mirror each hold was last written in (<see cref="Mirrors"/>), or never.</summary>
    long[] _holdKnownIn;

    /// <summary>Whether every hold is laid.</summary>
    public bool KnowsEveryHold => _knowsEveryHold;

    /// <summary>A hold about to be read, which this layer must hold.</summary>
    void ExpectTheHold(int hold)
    {
        if (!_knowsEveryHold && _holdKnownIn[hold] != Mirrors)
        {
            throw new InvalidOperationException($"Hold {hold} was read off a layer sent back without it.");
        }
    }

    /// <summary>Whether every way's lists are laid — or only those <see cref="_known"/> names.</summary>
    bool _knowsEveryWay = true;

    readonly bool[] _known;
    readonly int[] _knownWays;
    int _knownCount;

    /// <summary>The mirror each way was last asked about in (<see cref="Mirrors"/>), or never.</summary>
    readonly long[] _askedIn;

    /// <summary>How many mirrors this layer has begun (<see cref="BeginTheMirror"/>).</summary>
    public long Mirrors { get; private set; }

    /// <summary>Whether every way's lists are laid.</summary>
    public bool KnowsEveryWay => _knowsEveryWay;

    /// <summary>
    /// <b>Whether this layer holds a way's lists</b>, asked before reading them — and the way marked asked about this
    /// mirror, so a layer sent back in part is sent it next.
    /// </summary>
    public bool Knows(int way)
    {
        _askedIn[way] = Mirrors;
        return _knowsEveryWay || _known[way];
    }

    /// <summary>Whether this layer holds every one of these ways' lists, each asked about (<see cref="Knows(int)"/>).</summary>
    public bool Knows(ReadOnlySpan<LineWay> ways)
    {
        var known = true;
        foreach (ref readonly var way in ways) known &= Knows(way.Way);

        return known;
    }

    /// <summary>
    /// The ways asked about in the mirror <paramref name="mirror"/> or since (<see cref="Knows(int)"/>), ascending, into
    /// <paramref name="into"/>.
    /// </summary>
    /// <returns>How many were written, which <paramref name="into"/>'s length bounds.</returns>
    public int WaysAskedSince(long mirror, Span<int> into)
    {
        var written = 0;
        for (var way = 0; way < _askedIn.Length && written < into.Length; way++)
        {
            if (_askedIn[way] >= mirror) into[written++] = way;
        }

        return written;
    }

    /// <summary>
    /// <b>One way's lists held</b>, mirrored in a layer that holds only some (<see cref="BeginTheMirror"/> with
    /// <c>everyWay</c> false): its bodies and its planned pieces are what the device laid on it.
    /// </summary>
    public void KnowTheWay(int way)
    {
        if (_known[way]) return;

        _known[way] = true;
        _knownWays[_knownCount++] = way;
    }

    /// <summary>Every hold held, or only those mirrored from here on.</summary>
    void KnowEveryHold(bool everyHold) => _knowsEveryHold = everyHold;

    /// <summary>Every way's lists held, or only the ways <see cref="KnowTheWay"/> names from here on.</summary>
    void KnowEveryWay(bool everyWay)
    {
        _knowsEveryWay = everyWay;
        for (var at = 0; at < _knownCount; at++) _known[_knownWays[at]] = false;

        _knownCount = 0;
    }
}
