namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// How work that need not happen every tick is spread over the ticks it may happen on. <b>Members fall
/// into groups by their own index, a run of <see cref="RunOfMembers"/> to a group and one group to a tick</b>,
/// so the town's thinking and its looking spread across the interval rather than spiking on the tick it comes
/// round on.
/// </summary>
/// <remarks>
/// <para>
/// The interval is seconds because what it bounds is <b>how far the world moves under a stale answer</b>
/// — 0.1 s is about a metre at town speed, against a 13.8 m mean following distance. An interval of 0
/// must reproduce the unstaggered town exactly.
/// </para>
/// <para>
/// <b>The stagger is the whole point of the type.</b> Bodies move every tick and procedures do not, but a
/// town where every car looks up on the same tick costs the same peak frame as one where none of them are
/// staggered at all — and the peak is what a frame budget is spent against.
/// </para>
/// <para>
/// <b>A turn is runs of neighbours, not every n-th member</b>: a member's rows sit beside its neighbours' in every
/// per-member array, and a turn that took every n-th would bring every cache line of every one of them in to read a
/// share of it. Runs rather than one block a group keep each tick's members spread over the roster, so a tick takes
/// its share of every kind of member the roster lays out in order.
/// </para>
/// </remarks>
internal readonly struct TickGroups
{
    /// <summary>How many neighbouring members take their turn together: a cache line of four-byte rows.</summary>
    public const int RunOfMembers = 16;

    public TickGroups(float intervalS, int tickRateHz)
    {
        if (tickRateHz <= 0) throw new ArgumentOutOfRangeException(nameof(tickRateHz));

        Groups = intervalS <= 0f ? 1 : Math.Max(1, (int)MathF.Round(intervalS * tickRateHz));
        IntervalS = Groups / (float)tickRateHz;
    }

    /// <summary>How many groups the members are spread over, which is also the interval in ticks.</summary>
    public int Groups { get; }

    /// <summary>How much world one turn is answerable for, which is what whoever takes it integrates over.</summary>
    public float IntervalS { get; }

    public bool EveryTick => Groups == 1;

    /// <summary>Whether this tick is this member's turn.</summary>
    public bool Turn(long tick, int member) => Groups == 1 || (tick + member / RunOfMembers) % Groups == 0;

    /// <summary>The members of <c>[0, <paramref name="count"/>)</c> whose turn this tick is, in index order, as <see cref="Turn"/> says.</summary>
    public InTurnMembers InTurn(long tick, int count)
    {
        var firstRun = (int)((Groups - (tick % Groups)) % Groups);
        return new InTurnMembers(firstRun * RunOfMembers, (Groups - 1) * RunOfMembers, count);
    }

    /// <summary>The members of one turn, walked run by run without allocating (<see cref="InTurn"/>).</summary>
    public struct InTurnMembers(int first, int skipped, int count)
    {
        int _current = first - 1;

        public readonly InTurnMembers GetEnumerator() => this;

        public readonly int Current => _current;

        public bool MoveNext()
        {
            if (++_current % RunOfMembers == 0 && _current != first) _current += skipped;
            return _current < count;
        }
    }
}
