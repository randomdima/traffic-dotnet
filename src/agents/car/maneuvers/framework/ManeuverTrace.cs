namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>The instrument the catalogue is debugged with, built before the entries it watches</b>: which
/// manoeuvre every car was in, on every tick, and every hand-over between two of them.
/// </summary>
/// <remarks>
/// <para>
/// Taken before the catalogue was finished rather than after it, because three
/// faults are visible nowhere else, and each of them is one of this class's own counters:
/// <b>two manoeuvres passing a car back and forth in one spot</b>
/// (<see cref="ShuttlesBetween"/>), <b>an entry whose named successor is never reached</b>
/// (<see cref="EverEntered"/> over the whole catalogue), and <b>a car standing still that no clock is
/// running for</b> (<see cref="StoodUnclocked"/>) — which is the one the ladder's own watchdog cannot
/// find, because a car nothing is timing is a car it never hears about.
/// </para>
/// <para>
/// It is counters and never a log: a town of five hundred cars at sixty ticks a second would write a
/// megabyte a second, and the questions above are all answered by sums. Nothing here allocates after
/// construction, which is what lets it run in the ordinary tick rather than under a switch.
/// </para>
/// </remarks>
internal sealed class ManeuverTrace
{
    readonly long[] _ticksIn;
    readonly long[] _entries;
    readonly long[] _transitions;
    readonly long[] _shuttles;

    public ManeuverTrace()
    {
        var count = Maneuvers.Count;
        _ticksIn = new long[count];
        _entries = new long[count];
        _transitions = new long[count * count];
        _shuttles = new long[count * count];
    }

    /// <summary>Car-ticks spent standing still with no clock running and no procedure asking for it.</summary>
    public long StoodUnclocked { get; private set; }

    /// <summary>Car-ticks the whole trace covers, which is what every share is read against.</summary>
    public long CarTicks { get; private set; }

    /// <summary>One car-tick in one manoeuvre.</summary>
    /// <param name="clocked">
    /// Whether something is timing this car: a bound running down, a procedure that asked it to stand
    /// still, or motion. <b>False is a finding and not a state</b> — it is a car nothing will ever come
    /// back for.
    /// </param>
    public void Ticked(Maneuver doing, bool clocked)
    {
        CarTicks++;
        _ticksIn[(int)doing]++;
        if (!clocked) StoodUnclocked++;
    }

    /// <summary>
    /// One hand-over. <paramref name="inOneSpot"/> says the car has not covered a car length since it
    /// last entered <paramref name="to"/> — which, on a pair that has just swapped back, is the
    /// back-and-forth this trace exists to catch.
    /// </summary>
    public void Changed(Maneuver from, Maneuver to, bool inOneSpot)
    {
        _entries[(int)to]++;
        _transitions[((int)from * Maneuvers.Count) + (int)to]++;
        if (inOneSpot) _shuttles[((int)from * Maneuvers.Count) + (int)to]++;
    }

    public long TicksIn(Maneuver maneuver) => _ticksIn[(int)maneuver];

    public long Entries(Maneuver maneuver) => _entries[(int)maneuver];

    public long Transitions(Maneuver from, Maneuver to) => _transitions[((int)from * Maneuvers.Count) + (int)to];

    /// <summary>
    /// How often the pair handed a car back and forth without it going anywhere, counted in both
    /// directions: a manoeuvre that names a successor which names it straight back is a loop, however
    /// many successes it reports.
    /// </summary>
    public long ShuttlesBetween(Maneuver a, Maneuver b) =>
        _shuttles[((int)a * Maneuvers.Count) + (int)b] + _shuttles[((int)b * Maneuvers.Count) + (int)a];

    /// <summary>Whether anything ever reached this entry. An entry nothing reaches is either unbuilt or unenterable.</summary>
    public bool EverEntered(Maneuver maneuver) => _entries[(int)maneuver] > 0;

    /// <summary>The worst back-and-forth pair in the run, which is the one line of the summary worth reading first.</summary>
    public (Maneuver A, Maneuver B, long Count) WorstShuttle()
    {
        var worst = (A: Maneuver.None, B: Maneuver.None, Count: 0L);
        for (var from = 0; from < Maneuvers.Count; from++)
        {
            for (var to = from + 1; to < Maneuvers.Count; to++)
            {
                var pairs = ShuttlesBetween((Maneuver)from, (Maneuver)to);
                if (pairs <= worst.Count) continue;

                worst = ((Maneuver)from, (Maneuver)to, pairs);
            }
        }

        return worst;
    }

    public void Reset()
    {
        Array.Clear(_ticksIn);
        Array.Clear(_entries);
        Array.Clear(_transitions);
        Array.Clear(_shuttles);
        StoodUnclocked = 0;
        CarTicks = 0;
    }
}
