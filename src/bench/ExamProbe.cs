using System.Numerics;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The driving exam, driven</b>: every card of <see cref="ExamCards"/> staged at once on the map laid
/// for it, every car ordered through the crossing its card names, and what each of them did read off the
/// bodies rather than off anything a driver said about itself.
/// </summary>
/// <remarks>
/// <b>The instrument and the test are one machine</b>: <c>--bench exam</c> prints the table and the town
/// suite asserts the same verdicts, so a card cannot pass in one and fail in the other. What each card
/// claims and what this build does instead is the card's own (<see cref="ExamCard.Finding"/>).
/// </remarks>
internal static class ExamProbe
{
    public static bool Run(SimConfig config)
    {
        using var world = new TownWorld(TownReader.ReadFile(ProjectPaths.TownFile(ExamPlan.Name)), config);
        var watch = new ExamWatch(config, world);
        var loop = new SimLoop<TownWorld>(world, config);
        for (var tick = 0; tick < ExamDrive.Ticks; tick++)
        {
            loop.Advance();
            watch.Saw(world);
        }

        var drive = watch.Drive;
        Console.WriteLine(
            $"driving exam — {ExamCards.Count} cards on {ExamPlan.Name}, {ExamDrive.Ticks} ticks "
            + $"({ExamDrive.Ticks / config.Sim.TickRateHz} s), {drive.Cars} cars ordered at once");
        Console.WriteLine();
        Console.WriteLine($"{"card",5}  {"movement",-12}{"claim",-18}{"verdict",-9} what happened");

        var passed = 0;
        var outstanding = 0;
        for (var card = 0; card < ExamCards.Count; card++)
        {
            var of = ExamCards.All[card];
            var wrong = drive.Verdict(card);
            var known = of.Finding.Length > 0;
            if (wrong is null) passed++;
            else if (known) outstanding++;

            var subject = drive.Of(card, 0);
            Console.WriteLine(
                $"{card,5}  {subject.Movement,-12}{of.Asks,-18}"
                + $"{(wrong is null ? "passed" : known ? "known" : "FAILED"),-9}{of.Name}");
            if (wrong is not null) Console.WriteLine($"{"",14}{wrong}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{passed} of {ExamCards.Count} cards driven as written, {outstanding} outstanding findings, "
            + $"{ExamCards.Count - passed - outstanding} failing.");

        foreach (var card in Outstanding()) Console.WriteLine($"  card {card}: {ExamCards.All[card].Finding}");

        // And the same run said as claims, which is what a caller reads to decide whether the exam passed.
        return ScenarioReport.Print(ExamPlan.Name, [watch], world.ElapsedS);
    }

    static IEnumerable<int> Outstanding()
    {
        for (var card = 0; card < ExamCards.Count; card++)
        {
            if (ExamCards.All[card].Finding.Length > 0) yield return card;
        }
    }
}

/// <summary>
/// The exam being driven on a town somebody else is ticking: every card ordered on the first tick, and
/// what each car did recorded tick by tick.
/// </summary>
/// <remarks>
/// <para>
/// <b>One run and thirty-six questions.</b> The cards stand a lattice apart and are ordered on the same
/// tick, so the whole exam is one town ticked once — the alternative, a town a card, is thirty-six towns
/// laid to ask thirty-six questions of one engine.
/// </para>
/// <para>
/// <b>The town is the caller's</b>, which is what lets the probe stand one of its own and the game hand
/// over the map somebody has just opened. Both then see the same cards driven the same way, because this
/// is the only thing that stages them.
/// </para>
/// <para>
/// <b>Everything is recorded as it happens and read afterwards.</b> A car whose order is finished is handed
/// back to the map, which would drive it again (CAR-1) — so a card is the window between the order and the
/// arrival, and a body that was still standing there afterwards is not the card.
/// </para>
/// </remarks>
internal sealed class ExamDrive
{
    /// <summary>
    /// A minute of town. A card is a stand back of a few tens of metres and one junction, which is a
    /// handful of seconds of driving — the rest is room for a car that has to wait for another, and for a
    /// light to come round.
    /// </summary>
    public const int Ticks = 3_600;

    readonly SimConfig _config;
    readonly TownWorld _world;
    readonly ExamLattice _lattice;
    readonly ExamDrove[] _drove;

    /// <summary>Which of a card's walker's two orders it is on: standing at its kerb, out on the paint, or gone over.</summary>
    readonly OnFoot[] _onFoot = new OnFoot[ExamCards.Count];

    /// <summary>Each card's verdict once it has one, and whether it was taken on a card that had been decided.</summary>
    readonly string?[] _wrong = new string?[ExamCards.Count];

    readonly bool[] _judged = new bool[ExamCards.Count];

    int _tick = -1;

    public ExamDrive(SimConfig config, TownWorld world)
    {
        _config = config;
        _lattice = ExamLattice.Of(config);
        _world = world;
        _drove = new ExamDrove[_lattice.Cars];

        Order();
        StandTheWalkers();
    }

    public int Cars => _drove.Length;

    public float CrossingPaceMps => _config.CarCrossingPaceMps;

    /// <summary>How many ticks of the town this has watched, which is what says a card has had its chance.</summary>
    public int Ticked => _tick + 1;

    /// <summary>One tick of the town, seen: every staged car read off its body, and every paced walker turned round.</summary>
    public void Saw() => Watch(++_tick);

    /// <summary>
    /// <b>Whether a card has been answered at all yet.</b> Every driver it stages has either arrived where
    /// it was sent or given up on the way, and until then the card is a question still being asked rather
    /// than one the engine got wrong.
    /// </summary>
    /// <remarks>
    /// <b>The exam's own window decides the rest.</b> A card can end with its cars neither arrived nor
    /// given up — two of them stopped on the box, each cut at the other's ground — and that is a finding
    /// rather than a question still open, so a card is decided once the minute it is given has run out.
    /// </remarks>
    public bool Decided(int card)
    {
        if (Ticked >= Ticks) return true;

        var of = ExamCards.All[card];
        for (var driver = 0; driver < of.Drivers.Length; driver++)
        {
            var drove = Of(card, driver);
            if (drove.ArrivedAt < 0 && drove.GaveUpAt < 0) return false;
        }

        return true;
    }

    public string Name(int card) => $"card {card} ({ExamCards.All[card].Name})";

    public ExamDrove Of(int card, int driver) => _drove[_lattice.CarOf(card, driver)];


    /// <summary>
    /// <b>What was wrong with the way a card was driven, or nothing</b> — judged once, on the tick the card
    /// is decided on, and kept.
    /// </summary>
    /// <remarks>
    /// <b>Kept because it is read every tick and not only at the end.</b> A watch on a running town asks
    /// every decided card where it stands on every tick of the frame it is drawn in, and a message composed
    /// afresh each time would be an allocation a second per failing card.
    /// </remarks>
    public string? Verdict(int card)
    {
        if (_judged[card]) return _wrong[card];

        _wrong[card] = Judge(card);
        _judged[card] = Decided(card);
        return _wrong[card];
    }

    /// <summary>
    /// <b>What was wrong with the way a card was driven, or nothing.</b> The standing claim under every
    /// card is that every car staged got where it was sent; the card's own claim is about its subject and
    /// about nothing else.
    /// </summary>
    string? Judge(int card)
    {
        var of = ExamCards.All[card];
        for (var driver = 0; driver < of.Drivers.Length; driver++)
        {
            var drove = Of(card, driver);
            if (drove.ArrivedAt < 0)
            {
                return $"{Name(card)}: driver {driver} ({drove.Movement}) never got to the place it was "
                       + $"ordered to on the {of.Drivers[driver].To} arm — {drove}";
            }

            // <b>And got there by the movement the card is about.</b> The lattice is a grid, so the place a
            // driver is sent to is also reachable round the block — an arrival on its own says the car got
            // there and not that it ever crossed the junction the card was written for.
            if (drove.ClearedAt < 0)
            {
                return $"{Name(card)}: driver {driver} ({drove.Movement}) got there without ever coming "
                       + $"through the box onto the {of.Drivers[driver].To} arm — {drove}";
            }
        }

        var subject = Of(card, 0);
        return of.Asks switch
        {
            ExamAsks.Unhindered when Held(subject) =>
                $"{Name(card)}: its movement takes ground off nothing there, and it was held for "
                + $"{subject.RestedBeforeTheBox} ticks short of the box — {subject}",

            ExamAsks.GivesWay => GaveWay(card, subject),
            ExamAsks.InTurn => WentInTurn(card, subject),

            ExamAsks.EntersOnGreen when subject.CrossedTheBarOnARed =>
                $"{Name(card)}: it crossed its own stop bar while the light was showing red — {subject}",

            ExamAsks.AtCrossingPace when subject.OnThePaintFor == 0 =>
                $"{Name(card)}: it never reached the paint — {subject}",

            // The half again a body still clearing the paint has: the pace is a target the tyres deliver on
            // the ground under them, and a car on a crossing may also be being pushed along it.
            ExamAsks.AtCrossingPace when subject.FastestOnThePaintMps > CrossingPaceMps * 1.5f =>
                $"{Name(card)}: it crossed the paint at {subject.FastestOnThePaintMps:F1} m/s against a "
                + $"crossing pace of {CrossingPaceMps:F1} — {subject}",

            // The same arm every other claim carries: a card nobody stood in front of asked the engine
            // nothing, and passing it would say the opposite (`GaveWay`'s "nothing to give way to").
            ExamAsks.StopsForThePaint when subject.SomebodyStoodOnItFor == 0 =>
                $"{Name(card)}: nobody ever stood on the paint it is asked to stop for — {subject}",

            ExamAsks.StopsForThePaint when subject.SharedThePaintFor > 0 =>
                $"{Name(card)}: it was on the paint for {subject.SharedThePaintFor} ticks with somebody on "
                + $"foot standing on it — {subject}",

            ExamAsks.TurnsRound when subject.CameBackAt < 0 =>
                $"{Name(card)}: it never came back down the road it drove in on — {subject}",

            _ => null,
        };
    }

    /// <summary>
    /// <b>Which of the two waits, and not which of them touches the box first</b> (TER-5e). A box admits
    /// more than one car at a time (TER-5c): what a right of way decides is whose claim is cut, and what
    /// that comes out as on the ground is the weaker movement held short while the stronger one is not.
    /// </summary>
    string? GaveWay(int card, ExamDrove subject)
    {
        var stronger = Of(card, 1);
        if (stronger.EnteredAt < 0) return $"{Name(card)}: nothing to give way to — {stronger}";

        if (Held(stronger))
        {
            return $"{Name(card)}: the car with the right of way was held for {stronger.RestedBeforeTheBox} "
                   + $"ticks by the one that should have given way — {stronger}";
        }

        return subject.EnteredAt > stronger.EnteredAt
            ? null
            : $"{Name(card)}: it was on the box at tick {subject.EnteredAt} and the car it gives way to at "
              + $"{stronger.EnteredAt} — {subject}";
    }

    string? WentInTurn(int card, ExamDrove subject)
    {
        var inFront = Of(card, 1);
        if (inFront.EnteredAt < 0) return $"{Name(card)}: nothing went in front of it — {inFront}";

        return subject.EnteredAt > inFront.EnteredAt
            ? null
            : $"{Name(card)}: it was on the box at tick {subject.EnteredAt} and the car in front of it at "
              + $"{inFront.EnteredAt} — {subject}";
    }

    /// <summary>
    /// Whether a car was <em>held</em> rather than momentarily below the speed a stop is read at. The bar
    /// is the staleness a driver's own decision is allowed (<see cref="SimFigures.AgentDecisionIntervalS"/>):
    /// shorter than that and nothing has decided anything about it yet.
    /// </summary>
    bool Held(ExamDrove drove) =>
        drove.RestedBeforeTheBox > _config.Sim.AgentDecisionIntervalS * _config.Sim.TickRateHz;

    /// <summary>
    /// Every car sent through its own card's crossing, on the first tick and all at once (CTL-8). <b>An
    /// order and not a route</b>: the place is a run on past the box on the arm the card names, and the
    /// only way from where the car stands to there is the movement the card is about.
    /// </summary>
    void Order()
    {
        for (var card = 0; card < ExamCards.Count; card++)
        {
            for (var driver = 0; driver < ExamCards.All[card].Drivers.Length; driver++)
            {
                var car = _lattice.CarOf(card, driver);
                _drove[car] = new ExamDrove(card, driver, ExamCards.All[card].Drivers[driver]);

                // <b>Stood down before it is sent.</b> A map with nowhere to be on it puts every car on a
                // tour when it is laid (CAR-1), and a leg planned over a line already laid through the
                // junction is planned from the far end of that line — a route round the block to a place
                // the car is about to drive past. The reset is what a hand taking the wheel does.
                _world.ReleaseOrderOfCar(car);
                if (!_world.OrderCar(car, _lattice.AimM(card, driver)))
                {
                    throw new InvalidOperationException(
                        $"{Name(card)}: driver {driver} would not take the order to {_lattice.AimM(card, driver)}");
                }
            }
        }
    }

    /// <summary>
    /// <b>Every body on foot given something to do, because a body left to itself does something else.</b>
    /// This map lays pavement, so a walker nobody is telling anything draws a destination anywhere on it
    /// (<c>TownWorld.WanderInstead</c>) and turns up in somebody else's card — the same thing
    /// <see cref="Hold"/> stops the cars doing.
    /// </summary>
    /// <remarks>
    /// A card about paint wants its body <em>on</em> the paint and every other card wants it out of the
    /// way, and an order to where the body already stands is how the second is asked for.
    /// </remarks>
    void StandTheWalkers()
    {
        for (var card = 0; card < ExamCards.Count; card++)
        {
            var walker = _lattice.WalkerOf(card);
            if (walker == ExamLattice.NoWalker) continue;

            if (ExamCards.All[card].Asks == ExamAsks.StopsForThePaint) SendOver(card, walker);
            else _world.Order(walker, _world.People.PositionM[walker]);
        }
    }

    /// <summary>
    /// <b>The body paced over its own paint until the car staged against it has answered</b> (TER-5e). A
    /// card about stopping is a car and a walker meeting on one crossing, and two bodies each deciding for
    /// themselves meet there only by luck.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Pacing and never standing in the road</b>: a body parked on the paint is a car that stops for it
    /// for good, which is a deadlock and not a crossing. So it walks kerb to kerb and turns round, and the
    /// same distance decides both ends of that — <see cref="StepsOutInFrontOfM"/>, the ground the subject
    /// covers while a body gets the whole way over.
    /// </para>
    /// <para>
    /// <b>Inside it the pacing stops</b>, one way or the other. A body already on the paint there is the
    /// card happening, so it finishes the crossing it is on and stays off; a body back at its kerb there
    /// stays at it, because stepping out a car's length in front of a moving one is not what any card
    /// claims and PER-15 is the rule that would be under test instead.
    /// </para>
    /// </remarks>
    void WalkTheCard(int card)
    {
        var walker = _lattice.WalkerOf(card);
        if (walker == ExamLattice.NoWalker || ExamCards.All[card].Asks != ExamAsks.StopsForThePaint) return;
        if (_onFoot[card] == OnFoot.Answered) return;

        var car = _lattice.CarOf(card, 0);
        var subject = _drove[car];
        if (!_lattice.Watched(card, out var crossing)) return;

        var closing = (_world.Cars.PositionM[car] - crossing.CentreM).Length() <= StepsOutInFrontOfM(car);
        if (closing && subject.MovedOff && subject.SomebodyIsOnItNow)
        {
            _onFoot[card] = OnFoot.Answered;
            return;
        }

        if (closing || !Reached(walker, card)) return;

        if (_onFoot[card] == OnFoot.GoingOver) SendBack(card, walker);
        else SendOver(card, walker);
    }

    /// <summary>
    /// How near the subject has to be for a body at the kerb to be stepping out <em>in front of</em> it: the
    /// ground it covers while a walker gets the whole way over, and the ground it would need to stop in.
    /// <b>It is the staging's own bound and not a rule</b> — what a driver owes a crossing is CAR-7b's and
    /// TER-5e's, and is what the card is asking about.
    /// </summary>
    float StepsOutInFrontOfM(int car)
    {
        var speedMps = _world.Cars.VelocityMps[car].Length();
        var overS = (2f * _lattice.KerbOffsetM / _config.PersonWalkSpeedMps) + _config.Sim.AgentDecisionIntervalS;
        return (speedMps * overS) + (speedMps * speedMps / (2f * _config.CarBrakingMps2));
    }

    void SendOver(int card, int walker)
    {
        _onFoot[card] = OnFoot.GoingOver;
        if (_lattice.Across(card, out var toM)) _world.Order(walker, toM);
    }

    void SendBack(int card, int walker)
    {
        _onFoot[card] = OnFoot.ComingBack;
        if (_lattice.Waiting(card, out var toM, out _)) _world.Order(walker, toM);
    }

    /// <summary>Whether the pacing body is at the kerb it was last sent to, which is when it is turned round.</summary>
    bool Reached(int walker, int card)
    {
        var toM = _onFoot[card] == OnFoot.GoingOver
            ? (_lattice.Across(card, out var farM) ? farM : Vector2.Zero)
            : (_lattice.Waiting(card, out var nearM, out _) ? nearM : Vector2.Zero);

        return (_world.People.PositionM[walker] - toM).Length() <= _world.People.RadiusM[walker];
    }

    void Watch(int tick)
    {
        for (var car = 0; car < _drove.Length; car++)
        {
            _drove[car].Read(tick, _world, _lattice, _config);
            Hold(car);
        }

        for (var card = 0; card < ExamCards.Count; card++) WalkTheCard(card);
    }

    /// <summary>
    /// <b>A car that has finished its card is sent back to the place it finished at.</b> This map has
    /// nowhere to be on it, so a car nobody is asking anything of drives itself (CAR-1,
    /// <c>TownWorld.DriveTheEmptyMap</c>) — and a car touring the lattice is traffic nobody staged
    /// arriving in somebody else's card.
    /// </summary>
    /// <remarks>
    /// <b>On the tick the order finishes and not once the car has moved.</b> A place behind a car that has
    /// already pulled away is a place the search reaches by driving round the block, which is the tour this
    /// exists to stop, dressed as an order.
    /// </remarks>
    void Hold(int car)
    {
        if (_world.OrderOf(car) != PlayerOrder.None) return;

        // The place it was sent to, or — for a car whose leg the ladder stood down somewhere else — where
        // it stands now. <b>A card that failed is not driven again</b>: what is being stopped is a car with
        // nothing to do touring the lattice into somebody else's card, and the card's own verdict was taken
        // when it gave up.
        var drove = _drove[car];
        _world.OrderCar(
            car,
            drove.ArrivedAt >= 0 ? _lattice.AimM(drove.Card, drove.Driver) : _world.Cars.PositionM[car]);
    }
}

/// <summary>Which way a card's walker is pacing its paint, or that it has stopped.</summary>
internal enum OnFoot : byte
{
    ComingBack,
    GoingOver,

    /// <summary>The card has been asked and answered, so the body walks off and stays off.</summary>
    Answered,
}

/// <summary>What one staged car did, read off its body every tick and never off what its driver intended.</summary>
internal sealed class ExamDrove
{
    readonly ExamDriver _drives;
    readonly Vector2 _from;
    readonly Vector2 _to;
    bool _approachWasRed;

    public ExamDrove(int card, int driver, ExamDriver drives)
    {
        Card = card;
        Driver = driver;
        _drives = drives;
        _from = ExamLattice.Bearing(drives.From);
        _to = ExamLattice.Bearing(drives.To);
    }

    /// <summary>The card this car was staged by, and which of that card's drivers it is.</summary>
    public int Card { get; }

    public int Driver { get; }

    /// <summary>The movement, as the two arms it joins.</summary>
    public string Movement => $"{_drives.From}→{_drives.To}";

    /// <summary>The first tick the body stood on the ground the junction reaches, or −1.</summary>
    public int EnteredAt { get; private set; } = -1;

    /// <summary>And the first tick after that it was off it again.</summary>
    public int LeftTheBoxAt { get; private set; } = -1;

    /// <summary>The first tick it stood clear of the box on the arm its card sends it out by.</summary>
    public int ClearedAt { get; private set; } = -1;

    /// <summary>How many ticks it stood still on its own approach before ever reaching the box.</summary>
    public int RestedBeforeTheBox { get; private set; }

    /// <summary>Whether it crossed its own stop bar while the light there was showing red.</summary>
    public bool CrossedTheBarOnARed { get; private set; }

    /// <summary>How many ticks its own body stood on the paint its card watches, and how fast it ever was there.</summary>
    public int OnThePaintFor { get; private set; }

    public float FastestOnThePaintMps { get; private set; }

    /// <summary>And how many of those it shared with somebody on foot.</summary>
    public int SharedThePaintFor { get; private set; }

    /// <summary>
    /// How many ticks somebody on foot stood on that paint before this car ever reached it, which is
    /// whether a card about stopping for one was staged at all. <b>Before and not after</b>: a body that
    /// steps on once the car has gone over is not the card's, and counting it would make an empty crossing
    /// read as one that was met.
    /// </summary>
    public int SomebodyStoodOnItFor { get; private set; }

    /// <summary>And whether one is standing there this tick, which is what the staging reads to know the card is happening.</summary>
    public bool SomebodyIsOnItNow { get; private set; }

    /// <summary>The first tick it was back down the arm it drove in on, facing the other way — `P-19` done.</summary>
    public int CameBackAt { get; private set; } = -1;

    /// <summary>The tick it stood at the place it was ordered to, which is the order carried out (CTL-8a).</summary>
    public int ArrivedAt { get; private set; } = -1;

    /// <summary>
    /// Whether it has ever been under way. Every car here starts stopped, so a stop that has not been
    /// preceded by a start is the order being taken up rather than anything on the road.
    /// </summary>
    public bool MovedOff { get; private set; }

    /// <summary>The tick the order stopped being one without the place having been reached, and what it was doing then.</summary>
    public int GaveUpAt { get; private set; } = -1;

    public string GaveUpDoing { get; private set; } = "—";

    /// <summary>Where it stood when the card stopped watching it, as an offset from the junction it was staged at.</summary>
    public Vector2 StoodAtM { get; private set; }

    public override string ToString() =>
        $"{Movement}: on the box {EnteredAt}–{LeftTheBoxAt}, clear at {ClearedAt}, arrived at {ArrivedAt}, "
        + $"{RestedBeforeTheBox} ticks at rest short of it, {OnThePaintFor} on the paint "
        + $"({SharedThePaintFor} of them shared, somebody on it for {SomebodyStoodOnItFor} before it), "
        + $"gave up at {GaveUpAt} doing {GaveUpDoing}, last seen {StoodAtM.X:F0},{StoodAtM.Y:F0} off the box";

    public void Read(int tick, TownWorld world, ExamLattice lattice, SimConfig config)
    {

        // <b>The card is over the moment the order is.</b> A car with nowhere left to be is handed back to
        // the map and driven again (CAR-1), so anything read after the arrival is that car's next errand
        // and not this card.
        if (ArrivedAt >= 0) return;

        var car = lattice.CarOf(Card, Driver);
        var atM = world.Cars.PositionM[car];
        var speedMps = world.Cars.VelocityMps[car].Length();
        var offM = atM - lattice.StageM(Card);

        // What the lane it is coming up on is being shown — the same table its driver reads, so what is
        // asserted about it cannot disagree with what it was told (TLT-3). <b>Read at the bar and frozen
        // there</b>: what a red forbids is going past the place it is shown at, and a car already committed
        // when the phase turned is clearing the box rather than running the light (TLT-4's amber tail).
        var lane = world.Cars.LaneOf(car);
        var redNow = lane >= 0 && world.Signals.ForApproach(lane, world.ElapsedS) == SignalColour.Red;
        if (Vector2.Dot(offM, _from) >= lattice.BarM) _approachWasRed = redNow;

        var onTheBox = offM.Length() <= lattice.ReachM;
        if (onTheBox && EnteredAt < 0)
        {
            EnteredAt = tick;
            CrossedTheBarOnARed = _approachWasRed;
        }
        else if (!onTheBox && EnteredAt >= 0 && LeftTheBoxAt < 0)
        {
            LeftTheBoxAt = tick;
        }

        // Clear of the box on the arm it was sent out by, far enough along it to be past the paint there.
        if (ClearedAt < 0 && EnteredAt >= 0 && Vector2.Dot(offM, _to) > lattice.ReachM) ClearedAt = tick;

        // And the order carried out, which is the whole of what the card asked for.
        if ((atM - lattice.AimM(Card, Driver)).Length() <= config.OrderedPlaceReachM
            && speedMps < config.Driving.StopSpeedMps)
        {
            ArrivedAt = tick;
            return;
        }

        // What it was doing when the order stopped being one — the entry that ended the leg somewhere the
        // card did not ask for, which is what a failure needs to name rather than leave to be guessed at.
        if (GaveUpAt < 0 && world.OrderOf(car) == PlayerOrder.None)
        {
            GaveUpAt = tick;
            GaveUpDoing = Maneuvers.Code(world.Cars.Doing[car]);
        }

        // At rest on its own approach, and only before it ever reached the box: a card about being held up
        // is about the wait in front of the junction and not about the queue after it. <b>And only once it
        // has moved at all</b> — every car here starts stopped, so the ticks it takes to pull away are the
        // order being taken up rather than the junction refusing it anything. <b>A red is not being held
        // up</b> either: a car standing at its own red is doing what the light told it, and counting that
        // as having given way to somebody would make every lit card read as one.
        MovedOff |= speedMps >= config.Driving.StopSpeedMps;
        if (MovedOff && !redNow && EnteredAt < 0 && speedMps < config.Driving.StopSpeedMps
            && Vector2.Dot(offM, _from) > 0f)
        {
            RestedBeforeTheBox++;
        }

        StoodAtM = offM;

        // Coming back the way it came is the heading reversed and the body back down its own arm (`P-19`).
        if (CameBackAt < 0 && Vector2.Dot(Heading.Unit(world.Cars.HeadingRad[car]), _from) > 0f
            && Vector2.Dot(offM, _from) > lattice.ReachM)
        {
            CameBackAt = tick;
        }

        if (!lattice.Watched(Card, out var crossing)) return;

        SomebodyIsOnItNow = SomebodyIsOn(world, crossing, config);
        if (OnThePaintFor == 0 && SomebodyIsOnItNow) SomebodyStoodOnItFor++;
        if (!OnThePaint(atM, crossing, config)) return;

        OnThePaintFor++;
        FastestOnThePaintMps = MathF.Max(FastestOnThePaintMps, speedMps);
        if (SomebodyIsOn(world, crossing, config)) SharedThePaintFor++;
    }

    /// <summary>Whether a body stands on the paint: within the depth along the road and the span across it.</summary>
    static bool OnThePaint(Vector2 atM, ExamCrossing crossing, SimConfig config)
    {
        var offM = atM - crossing.CentreM;
        return MathF.Abs(Vector2.Dot(offM, crossing.Axis)) <= ExamLattice.CrossingDepthM * 0.5f
               && MathF.Abs(Vector2.Dot(offM, Heading.RightOf(crossing.Axis))) <= config.RoadWidthM * 0.5f;
    }

    static bool SomebodyIsOn(TownWorld world, ExamCrossing crossing, SimConfig config)
    {
        for (var person = 0; person < world.People.Count; person++)
        {
            if (world.People.Inside[person].Any) continue;
            if (OnThePaint(world.People.PositionM[person], crossing, config)) return true;
        }

        return false;
    }
}
