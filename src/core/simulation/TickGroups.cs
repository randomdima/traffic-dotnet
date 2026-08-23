namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// How work that need not happen every tick is spread over the ticks it may happen on. <b>Members fall
/// into groups by their own index, one group to a tick</b>, so the town's thinking and its looking spread
/// across the interval rather than spiking on the tick it comes round on.
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
/// </remarks>
internal readonly struct TickGroups
{
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
    public bool Turn(long tick, int member) => Groups == 1 || (tick + member) % Groups == 0;
}
