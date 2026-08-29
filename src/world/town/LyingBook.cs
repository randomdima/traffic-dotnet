using System.Numerics;

namespace TrafficSimulation.World.Town;

/// <summary>
/// Everything the lane index reads to work out where a body that is not driving lies — <b>the whole of
/// it, so that two equal states cannot mean two different stretches</b>.
/// </summary>
/// <remarks>
/// <see cref="Sweeping"/> and <see cref="CommittedToM"/> are the answer of
/// <c>TownWorld.WhereTheTemplateSweepEndsM</c> rather than the line it was read off: the line is a
/// hundred arcs and the sweep is the only thing about it this pass can see.
/// </remarks>
/// <param name="Towed">
/// Whether this body is on a tow bar (EVA-5), which is the one state that lays <em>no</em> stretch of its
/// own: the ground under a towed car is covered by the reservation of the vehicle pulling it, because the
/// two are one movement (TER-5c.2).
/// </param>
internal readonly record struct LyingState(
    Vector2 StandingM, Vector2 CommittedToM, Vector2 VelocityMps, int Bay, int MovementWay, byte Variant,
    bool Sweeping, bool Driven, bool Broken, bool Towed);

/// <summary>One stretch that pass laid, in the way's own metres — a row of the book, kept to be laid again.</summary>
/// <param name="StandsToM">
/// Where the body itself ends, as against how far the ground it has taken reaches
/// (<see cref="World.Road.LaneSlot.StandsToM"/>). A body that is not driving is still a body under way with
/// nowhere to go, so its stretch has the same three edges as any other and the two far ones meet only where
/// it is standing still.
/// </param>
internal readonly record struct LyingRow(int Way, float FromM, float StandsToM, float ToM, float AlongMps);

/// <summary>
/// <b>What each stationary body's stretches were, and the state they were worked out from.</b> A town's
/// parked cars are most of its fleet and none of them moves, so the geometry behind their stretches —
/// which lane each is nearest, which joins it lies under, how much of its bay's ways it holds — comes
/// out the same every tick for as long as they stand there.
/// </summary>
/// <remarks>
/// <para>
/// <b>The book itself is still laid from nothing every tick</b> (<c>LaneOccupancy.Begin</c>): what is
/// kept here is not a stretch of the book but the arithmetic that produced one, and the rows are handed
/// back to <c>LaneOccupancy.Add</c> exactly as the derivation would have handed them.
/// </para>
/// <para>
/// <b>A miss costs what the pass always cost and can never be wrong</b>, which is why the key is the
/// whole of the input rather than a flag somebody sets when a car moves: a state that differs by a
/// float is a state that is worked out again, and there is no invalidation to forget.
/// </para>
/// </remarks>
internal sealed class LyingBook
{
    const int NoCar = -1;

    /// <summary>A car whose rows may not be laid again: nothing has been worked out for it, or it overran.</summary>
    const int NotHeld = -1;

    readonly LyingState[] _state;
    readonly LyingRow[] _rows;
    readonly int[] _count;
    readonly int _mostRows;

    int _recording = NoCar;

    /// <param name="mostRows">
    /// How many stretches one stationary body may lay, which is the bound the road's own book is sized
    /// by. Overrunning it is not a defect here — the rows are dropped and the car works its geometry out
    /// again every tick — but it is a body the town has stopped saving anything on.
    /// </param>
    public LyingBook(int cars, int mostRows)
    {
        _mostRows = mostRows;
        _state = new LyingState[cars];
        _rows = new LyingRow[cars * mostRows];
        _count = new int[cars];
        Array.Fill(_count, NotHeld);
    }

    /// <summary>Whether the rows held for this car were worked out from exactly this state.</summary>
    public bool Holds(int car, in LyingState state) => _count[car] != NotHeld && _state[car] == state;

    /// <summary>The rows held, which is only ever asked of a car <see cref="Holds"/> has just answered for.</summary>
    public ReadOnlySpan<LyingRow> Of(int car) => _rows.AsSpan(car * _mostRows, _count[car]);

    /// <summary>Start taking down what this car lays, against the state it is being laid from.</summary>
    public void Begin(int car, in LyingState state)
    {
        _state[car] = state;
        _count[car] = 0;
        _recording = car;
    }

    public void Record(int car, in LyingRow row)
    {
        if (_recording != car || _count[car] == NotHeld) return;

        if (_count[car] == _mostRows)
        {
            _count[car] = NotHeld;
            return;
        }

        _rows[(car * _mostRows) + _count[car]++] = row;
    }

    public void End() => _recording = NoCar;
}
