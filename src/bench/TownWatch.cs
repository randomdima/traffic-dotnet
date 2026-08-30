using TrafficSimulation.App.Screen;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What every town this build opens has to keep while it runs</b>, whichever map it is: nothing is
/// left inside anything else (`PHY-1`), and no car stands still that nothing is timing.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the soak's own two questions, asked of the run in front of you</b> — the arithmetic is
/// <see cref="SoakProbe"/>'s and is called from here rather than written again, so the probe's table and
/// this panel cannot disagree about what being stuck is.
/// </para>
/// <para>
/// <b>The arrivals are quoted and never gated</b>, because what a town is able to arrive at is a fact
/// about that map: the proving grounds have nowhere to walk to and the crossings map has nobody to
/// drive. They are here because they are what makes the two claims worth anything — a town where
/// nothing ever moves overlaps by nothing and stands nobody unclocked, and would keep both while
/// modelling none of it.
/// </para>
/// </remarks>
internal sealed class TownWatch : ScenarioWatch
{
    /// <summary>PHY-1 as this town keeps it, which the gate on every shipped map asserts by name.</summary>
    public const int NothingInsideAnything = 0;

    /// <summary>TER-4c.1 as this town keeps it: what the book gave a body is the whole of where it may be.</summary>
    public const int NothingIsPastItsGrant = 1;

    const int NothingStandsUnclocked = 2;

    const int WhatArrived = 0;
    const int WhatItCost = 1;

    static readonly string[] TheClaims =
    [
        "no body is left inside another",
        "nobody goes on into ground the book refused it",
        "no car stands still with nothing running for it",
    ];

    static readonly string[] TheReadings =
    [
        "what got where it was going",
        "what the town cost its people",
    ];

    readonly float[] _overlapM;
    readonly int[] _stuckForTicks;
    readonly float[] _pastM;
    readonly float[] _wasPastM;
    readonly int[] _pastForTicks;
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

    long _walksBefore = -1;
    long _gaveUpBefore;
    long _drivesBefore;
    long _carTicks;
    long _stoodUnclocked;

    public TownWatch(TownWorld world)
        : base("the town", "what every town has to keep while it runs", TheClaims, TheReadings)
    {
        _walkers = world.People.Count;
        Cars = world.Cars.Count;
        _overlapM = new float[world.People.Count + world.Cars.Count];
        _stuckForTicks = new int[_overlapM.Length];
        _pastM = new float[_overlapM.Length];
        _wasPastM = new float[_overlapM.Length];
        _pastForTicks = new int[_overlapM.Length];
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
    /// The furthest anything has ever been past the ground the book granted it, and which body that was. A
    /// peak is a body arriving at the edge of its own grant a tick late; a long run of them is a body driving
    /// on road that was somebody else's.
    /// </summary>
    public float FurthestPastTheGrantM => _furthestPastM;

    public int FurthestPastBody => _furthestPastBody;

    public int LongestPastTicks => _longestPastTicks;

    public int PastBody => _pastBody;

    /// <summary>What arrived since this watch began, which is the half of the reading that makes the rest of it mean anything.</summary>
    public long WalksDone { get; private set; }

    public long WalksGivenUp { get; private set; }

    public long DrivesDone { get; private set; }

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

        NothingStandsUnclocked when _carTicks == 0 => ClaimVerdict.Waiting,
        NothingStandsUnclocked => _stoodUnclocked > 0 ? ClaimVerdict.Broken : ClaimVerdict.Kept,

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

            case NothingStandsUnclocked:
                into.Add(_stoodUnclocked);
                into.Add(" of ");
                into.Add(_carTicks);
                into.Add(" car-ticks stood with no clock");
                break;
        }
    }

    public override void Reads(int reading, ref TextBuffer into)
    {
        switch (reading)
        {
            case WhatArrived:
                into.Add(WalksDone);
                into.Add(" walks, ");
                into.Add(DrivesDone);
                into.Add(" drives, ");
                into.Add(WalksGivenUp);
                into.Add(" walks given up");
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
        if (body < 0 || body >= _overlapM.Length) return false;

        var wrote = false;
        if (_overlapM[body] > SoakProbe.OverlapAllowanceM)
        {
            into.Add(_overlapM[body] * 1_000f, "F0");
            into.Add(" mm inside something, ");
            into.Add(_stuckForTicks[body]);
            into.Add(" of ");
            into.Add(SoakProbe.StuckAfterTicks);
            into.Add(" ticks");
            wrote = true;
        }

        if (_pastM[body] <= SoakProbe.PastTheGrantAllowanceM) return wrote;

        if (wrote) into.Add("   ");
        into.Add(_pastM[body] * 1_000f, "F0");
        into.Add(" mm past its grant, ");
        into.Add(_pastForTicks[body]);
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
        // What the town had already arrived at when this watch began, so what is quoted is what happened
        // while it was watching and not what the warm-up before it did.
        if (_walksBefore < 0)
        {
            _walksBefore = world.WalkArrivals;
            _gaveUpBefore = world.WalksGivenUp;
            _drivesBefore = world.BaysParkedIn;
        }

        _ticks++;
        SoakProbe.SweepOverlaps(world, _overlapM);
        for (var body = 0; body < _overlapM.Length; body++)
        {
            // Which body it was and not only how deep: a walker a car swept and a car that drove into
            // another are the same millimetres and different findings.
            if (_overlapM[body] > _deepestM)
            {
                _deepestM = _overlapM[body];
                _deepestBody = body;
            }

            _stuckForTicks[body] = _overlapM[body] > SoakProbe.OverlapAllowanceM ? _stuckForTicks[body] + 1 : 0;
            if (_stuckForTicks[body] <= _longestStuckTicks) continue;

            _longestStuckTicks = _stuckForTicks[body];
            _stuckBody = body;
        }

        // And the same question asked of the book rather than of the shapes: whether a body is where it was
        // told it could be. The two are not one reading — a car past its grant with nothing yet in the metres
        // it took is inside nothing at all, and is the tick before the contact rather than the contact.
        //
        // <b>What is counted is going deeper and never being past</b>. A body stops where it was told to and
        // stays there, so a stride's worth of overshoot latches: read as a state it is a walker at a kerb
        // reported for the whole minute it waits, and the claim then says nothing about anybody driving on.
        for (var body = 0; body < _pastM.Length; body++) _wasPastM[body] = _pastM[body];

        SoakProbe.SweepPastTheGrant(world, _pastM);
        for (var body = 0; body < _pastM.Length; body++)
        {
            if (_pastM[body] > _furthestPastM)
            {
                _furthestPastM = _pastM[body];
                _furthestPastBody = body;
            }

            var deeper = _pastM[body] > SoakProbe.PastTheGrantAllowanceM
                         && _pastM[body] > _wasPastM[body] + SoakProbe.PastTheGrantAllowanceM;
            _pastForTicks[body] = deeper ? _pastForTicks[body] + 1 : 0;
            if (_pastForTicks[body] <= _longestPastTicks) continue;

            _longestPastTicks = _pastForTicks[body];
            _pastBody = body;
        }

        WalksDone = world.WalkArrivals - _walksBefore;
        WalksGivenUp = world.WalksGivenUp - _gaveUpBefore;
        DrivesDone = world.BaysParkedIn - _drivesBefore;
        Touches = world.Touches;
        _carTicks = world.Trace.CarTicks;
        _stoodUnclocked = world.Trace.StoodUnclocked;

        var down = 0;
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.Wounded[person]) down++;
        }

        var wrecked = 0;
        for (var car = 0; car < world.Cars.Count; car++)
        {
            if (world.Cars.Broken[car]) wrecked++;
        }

        Down = down;
        Wrecked = wrecked;
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
