using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What every town this build opens has to keep while it runs</b>, whichever map it is: nothing is
/// left inside anything else (`PHY-1`), and nobody goes on into ground it was refused (`TER-4c.1`).
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the soak's own two questions, asked of the run in front of you</b> — the arithmetic is
/// <see cref="SoakProbe"/>'s and is called from here rather than written again, so the probe's table and
/// this panel cannot disagree about what being stuck is.
/// </para>
/// <para>
/// <b>The readings are quoted and never gated</b>, because how far a town drives and what it costs its
/// people is a fact about that map. They are here because they are what makes the two claims worth
/// anything — a town where nothing ever moves overlaps by nothing and trespasses nowhere, and would keep
/// both while modelling none of it.
/// </para>
/// </remarks>
internal sealed class TownWatch : ScenarioWatch
{
    /// <summary>PHY-1 as this town keeps it, which the gate on every shipped map asserts by name.</summary>
    public const int NothingInsideAnything = 0;

    /// <summary>TER-4c.1 as this town keeps it: what a body was granted is the whole of where it may be.</summary>
    public const int NothingIsPastItsGrant = 1;

    const int WhatArrived = 0;
    const int WhatItCost = 1;

    static readonly string[] TheClaims =
    [
        "no body is left inside another",
        "nobody goes on into ground it was refused",
    ];

    static readonly string[] TheReadings =
    [
        "how far the town drove",
        "what the town cost its people",
    ];

    /// <summary>What the watch asks of a step.</summary>
    static readonly TownWatchTerms Terms = new(SoakProbe.OverlapAllowanceM, SoakProbe.PastTheGrantAllowanceM, StuckProbe.MovedM);

    readonly WatchRuns _runs;
    readonly float[] _overlapM;
    readonly float[] _pastM;
    readonly bool[] _afoot;
    readonly int _walkers;

    long _ticks;
    float _deepestM;
    int _deepestBody = -1;
    int _longestStuckTicks;
    int _stuckBody = -1;
    float _furthestPastM;
    int _furthestPastBody = -1;
    int _longestPastTicks;
    int _pastBody = -1;

    long _gaveUpBefore = -1;
    long _setDownBefore = -1;

    /// <summary>
    /// <b>The longest any one walker has gone on walking without getting anywhere</b> — the reading that
    /// says whether the give-up clock is doing its work. A body wedged where its straight back to the
    /// pavement runs through a wall reports the whole run here, and nothing else in the town notices it.
    /// </summary>
    int _longestStillTicks;

    /// <summary>How long one tick is, which is what turns a speed into the metres a car covered in it.</summary>
    readonly float _tickSeconds;

    public TownWatch(TownWorld world, SimConfig config)
        : base("the town", "what every town has to keep while it runs", TheClaims, TheReadings)
    {
        _tickSeconds = config.TickSeconds;
        _walkers = world.People.Count;
        Cars = world.Cars.Count;
        _runs = new WatchRuns(_walkers, Cars, Terms);
        _overlapM = new float[_runs.Bodies];
        _pastM = new float[_runs.Bodies];
        _afoot = new bool[_walkers];
    }

    /// <summary>The roster these figures were taken over, because a figure with no census beside it says nothing.</summary>
    public int Walkers => _walkers;

    public int Cars { get; }

    /// <summary>
    /// The deepest anything has ever been inside anything else, and which body that was. <b>A report on the
    /// unluckiest body</b>, and useful only beside the run below it: in a city there is always something,
    /// somewhere, in the tick between arriving at a body and being pushed off it.
    /// </summary>
    public float DeepestOverlapM => _deepestM;

    public int DeepestBody => _deepestBody;

    /// <summary>
    /// The longest any single body has stayed deeper than the allowance, which is the figure that separates
    /// a solver recovering from an approach from a body left inside another one.
    /// </summary>
    public int LongestStuckTicks => _longestStuckTicks;

    public int StuckBody => _stuckBody;

    /// <summary>
    /// The furthest anything has ever been past the ground it was granted, and which body that was. A
    /// peak is a body arriving at the edge of its own grant a tick late; a long run of them is a body driving
    /// on road that was somebody else's.
    /// </summary>
    public float FurthestPastTheGrantM => _furthestPastM;

    public int LongestPastTicks => _longestPastTicks;

    public int PastBody => _pastBody;

    /// <summary>
    /// <b>How far the town's cars have driven since this watch began</b>, which is the reading that says
    /// there was any traffic for the rest of it to be about.
    /// </summary>
    /// <remarks>
    /// <b>Ground covered and not arrivals</b>: no town this build lays has a bay (<c>docs/index.md#known-gaps</c>),
    /// so a car touring a lane arrives nowhere, and saying it arrived somewhere would be a second answer;
    /// what it does do is cover ground, and that is what is printed.
    /// </remarks>
    public float DrivenM { get; private set; }

    public long WalksGivenUp { get; private set; }

    /// <summary>And how many of those had to be lifted back onto the pavement to walk at all (PER-8).</summary>
    public long WalkersSetDown { get; private set; }

    public long Touches { get; private set; }

    /// <summary>And what the town cost: how many are on the ground and how many cars are wrecked, this instant.</summary>
    public int Down { get; private set; }

    public int Wrecked { get; private set; }

    public override ClaimVerdict Verdict(int claim) => claim switch
    {
        NothingInsideAnything when _ticks == 0 => ClaimVerdict.Waiting,
        NothingInsideAnything => _longestStuckTicks > SoakProbe.StuckAfterTicks
            ? ClaimVerdict.Broken
            : ClaimVerdict.Kept,

        NothingIsPastItsGrant when _ticks == 0 => ClaimVerdict.Waiting,
        NothingIsPastItsGrant => _longestPastTicks > SoakProbe.PastAfterTicks
            ? ClaimVerdict.Broken
            : ClaimVerdict.Kept,

        _ => ClaimVerdict.Waiting,
    };

    public override void Says(int claim, ref TextBuffer into)
    {
        switch (claim)
        {
            case NothingInsideAnything:
                into.Add("deepest ");
                into.Add(_deepestM * 1_000f, "F0");
                into.Add(" mm ");
                Named(ref into, _deepestBody);
                into.Add(", worst run ");
                into.Add(_longestStuckTicks);
                into.Add(" of ");
                into.Add(SoakProbe.StuckAfterTicks);
                into.Add(" ticks ");
                Named(ref into, _stuckBody);
                break;

            case NothingIsPastItsGrant:
                into.Add("deepest ");
                into.Add(_furthestPastM * 1_000f, "F0");
                into.Add(" mm ");
                Named(ref into, _furthestPastBody);
                into.Add(", longest drive on ");
                into.Add(_longestPastTicks);
                into.Add(" of ");
                into.Add(SoakProbe.PastAfterTicks);
                into.Add(" ticks ");
                Named(ref into, _pastBody);
                break;
        }
    }

    public override void Reads(int reading, ref TextBuffer into)
    {
        switch (reading)
        {
            case WhatArrived:
                into.Add((long)DrivenM);
                into.Add(" m driven, ");
                into.Add(WalksGivenUp);
                into.Add(" walks given up, ");
                into.Add(WalkersSetDown);
                into.Add(" set back on the pavement, longest ");
                into.Add(_longestStillTicks * _tickSeconds, "F0");
                into.Add(" s walking nowhere");
                break;

            case WhatItCost:
                into.Add(Down);
                into.Add(" of ");
                into.Add(_walkers);
                into.Add(" on the ground, ");
                into.Add(Wrecked);
                into.Add(" of ");
                into.Add(Cars);
                into.Add(" wrecked, ");
                into.Add(Touches);
                into.Add(" touches");
                break;
        }
    }

    /// <summary>
    /// <b>What this town has against one body, this instant</b> — how deep inside something it is and how
    /// far past the ground it was granted, each with the run of ticks it has held that for.
    /// </summary>
    /// <remarks>
    /// It is the same two sweeps the claims above are answered from, read at one body instead of over all
    /// of them. <b>A body that is inside nothing and where it was told to be has nothing said about it</b>:
    /// the label beside a unit is the unit's own state, and a line reading "0 mm" on every car in the town
    /// is a line nobody reads.
    /// </remarks>
    public override bool Notes(SelectionKind kind, int index, ref TextBuffer into)
    {
        var body = kind == SelectionKind.Person ? index : _walkers + index;
        if (body < 0 || body >= _runs.Bodies) return false;

        var note = _runs.NoteOf(body);

        var wrote = false;
        if (note.OverlapM > SoakProbe.OverlapAllowanceM)
        {
            into.Add(note.OverlapM * 1_000f, "F0");
            into.Add(" mm inside something, ");
            into.Add(note.StuckTicks);
            into.Add(" of ");
            into.Add(SoakProbe.StuckAfterTicks);
            into.Add(" ticks");
            wrote = true;
        }

        if (note.PastM <= SoakProbe.PastTheGrantAllowanceM) return wrote;

        if (wrote) into.Add("   ");
        into.Add(note.PastM * 1_000f, "F0");
        into.Add(" mm past its grant, ");
        into.Add(note.PastTicks);
        into.Add(" of ");
        into.Add(SoakProbe.PastAfterTicks);
        into.Add(" ticks");
        return true;
    }

    /// <summary>
    /// One tick. <b>The overlap is swept every tick and the rest is read off the town's own counters</b>:
    /// how long one body has been inside another is the whole of what tells a solver recovering from a
    /// body nothing pushed back out, and a sweep taken every other tick would count half of it.
    /// </summary>
    public override void Saw(TownWorld world)
    {
        // What the town had already given up on when this watch began, so what is quoted is what happened
        // while it was watching and not what the warm-up before it did.
        if (_gaveUpBefore < 0) _gaveUpBefore = world.WalksGivenUp;
        if (_setDownBefore < 0) _setDownBefore = world.WalkersSetDown;

        Take(Swept(world));

        WalksGivenUp = world.WalksGivenUp - _gaveUpBefore;
        WalkersSetDown = world.WalkersSetDown - _setDownBefore;
        Touches = world.Touches;
        Down = world.Down;
        Wrecked = world.Wrecked;
    }

    /// <summary>
    /// One step's sweep taken into what the watch has seen: which body it was and not only how deep — a walker a car swept
    /// and a car that drove into another are the same millimetres and different findings.
    /// </summary>
    void Take(in TownWatchedTick tick)
    {
        _ticks++;
        if (tick.DeepestOverlapM > _deepestM) (_deepestM, _deepestBody) = (tick.DeepestOverlapM, tick.DeepestBody);
        if (tick.LongestStuckTicks > _longestStuckTicks) (_longestStuckTicks, _stuckBody) = (tick.LongestStuckTicks, tick.StuckBody);
        if (tick.FurthestPastM > _furthestPastM) (_furthestPastM, _furthestPastBody) = (tick.FurthestPastM, tick.FurthestPastBody);
        if (tick.LongestPastTicks > _longestPastTicks) (_longestPastTicks, _pastBody) = (tick.LongestPastTicks, tick.PastBody);
        _longestStillTicks = Math.Max(_longestStillTicks, tick.LongestStillTicks);
        DrivenM += tick.DrivenM;
    }

    /// <summary>
    /// The town swept. <b>The claims are asked of the shapes and of the grants both</b>, and the two are not one
    /// reading: a car past its grant with nothing yet in the metres it took is inside nothing at all, and is the tick before
    /// the contact rather than the contact. <b>How long a walker has been walking and getting nowhere</b> is the one thing
    /// about the walking that nothing else in the town reports: every other figure here counts an event, and a body that is
    /// never given up and never arrives raises none of them.
    /// </summary>
    TownWatchedTick Swept(TownWorld world)
    {
        SoakProbe.SweepOverlaps(world, _overlapM);
        SoakProbe.SweepPastTheGrant(world, _pastM);
        var people = world.People;
        for (var person = 0; person < _walkers; person++)
        {
            _afoot[person] = people.Acts(person) && people.Walking[person] && !people.Inside[person].Any;
        }

        var drivenM = 0f;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (!world.Cars.Driven[car] || world.Cars.Broken[car]) continue;

            drivenM += world.Cars.VelocityMps[car].Length() * _tickSeconds;
        }

        return _runs.Step(_overlapM, _pastM, people.PositionM.AsSpan(0, _walkers), _afoot, drivenM);
    }

    /// <summary>What a body's place in the sweep is called: the roster it falls in and its own index there.</summary>
    void Named(ref TextBuffer into, int body)
    {
        if (body < 0)
        {
            into.Add("nobody");
            return;
        }

        into.Add(body < _walkers ? "walker " : "car ");
        into.Add(body < _walkers ? body : body - _walkers);
    }
}
