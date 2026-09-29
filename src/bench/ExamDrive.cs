using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen.Exam;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The exam being driven on a town somebody else is ticking</b>: every card of <see cref="ExamCards"/>
/// staged on the map laid for it, every car ordered through the movement its card names, and what each of
/// them did read off the bodies rather than off anything a driver said about itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>One run and a hundred questions.</b> The cards stand a block apart and are ordered on the same tick, so
/// the whole exam is one town ticked once — the alternative, a town a card, is a hundred towns laid to ask a
/// hundred questions of one engine.
/// </para>
/// <para>
/// <b>Nothing here decides anything a driver decides.</b> What it does is what a hand at the orders does
/// (CTL-8): it sends a car to a place, holds a car where it stands by sending it there, puts a car on a call
/// (AMB-4) and sends a walker over a zebra. Every movement in between is the engine's own.
/// </para>
/// <para>
/// <b>Everything is recorded as it happens and judged afterwards.</b> A car whose order is finished is handed
/// back to the map, which would drive it again (CAR-1, <c>TownWorld.DriveTheEmptyMap</c>) — so every car the
/// exam is done with, and every car it has not sent yet, is held where it stands by an order to stand there,
/// and a car touring the lattice is never traffic nobody staged arriving in somebody else's card.
/// </para>
/// </remarks>
internal sealed class ExamDrive
{
    /// <summary>
    /// A minute and a half of town. A card is a stand back of a few tens of metres and one junction, which is
    /// a handful of seconds of driving — the rest is room for a car that has to wait for another, for a light
    /// to come round twice and for somebody on foot to get over.
    /// </summary>
    public const float WatchedS = 90f;

    readonly SimConfig _config;
    readonly TownWorld _world;
    readonly ExamLattice _lattice;
    readonly ExamCarLog[] _cars;
    readonly ExamWalkerLog[] _walkers;

    /// <summary>The zebra on every arm of every card's junction, indexed <c>card * 4 + arm</c> in the card's frame.</summary>
    readonly ExamBand[] _bands;

    /// <summary>Each card's verdict once it has one, and whether it was taken on a card that had been decided.</summary>
    readonly string?[] _wrong;

    readonly bool[] _judged;

    int _tick = -1;

    public ExamDrive(SimConfig config, TownWorld world)
    {
        _config = config;
        _world = world;
        _lattice = ExamLattice.Of(config);
        Ticks = (int)(WatchedS * config.Sim.TickRateHz);
        SampleTicks = Math.Max(1, (int)MathF.Round(config.Sim.AgentDecisionIntervalS * config.Sim.TickRateHz));

        var ends = world.Plan.Paving(config).RoadEnds(config);
        _bands = new ExamBand[_lattice.Cards * 4];
        for (var card = 0; card < _lattice.Cards; card++)
        {
            for (var arm = 0; arm < 4; arm++)
            {
                _bands[(card * 4) + arm] = _lattice.Zebra(card, (ExamArm)arm, ends, out var leftM, out var rightM)
                    ? ExamBand.Across(leftM, rightM, config.Road.CrossingDepthM)
                    : ExamBand.None;
            }
        }

        _wrong = new string?[_lattice.Cards];
        _judged = new bool[_lattice.Cards];
        _cars = new ExamCarLog[_lattice.Cars];
        _walkers = new ExamWalkerLog[_lattice.Walkers];

        var samples = (Ticks / SampleTicks) + 1;
        for (var card = 0; card < _lattice.Cards; card++)
        {
            var of = _lattice.Card(card);
            for (var driver = 0; driver < of.Drivers.Length; driver++)
            {
                _cars[_lattice.CarOf(card, driver)] = new ExamCarLog(
                    card, driver, of.Drivers[driver], of.Walkers.Length, samples, ApproachLane(card, driver));
            }

            for (var walker = 0; walker < of.Walkers.Length; walker++)
            {
                var walks = of.Walkers[walker];
                _lattice.Kerbs(card, walker, ends, out var fromM, out var inTheLaneM, out var toM);
                _walkers[_lattice.WalkerOf(card, walker)] = new ExamWalkerLog(
                    card, walker, walks, fromM, inTheLaneM, toM, walks.Strolls ? ExamBand.None : Band(card, walks.Crossing));
            }
        }

        for (var car = 0; car < _cars.Length; car++) Stage(car);
    }

    /// <summary>How long the exam is watched for, in ticks.</summary>
    public int Ticks { get; }

    /// <summary>How often a car's place is written down: once a decision, which is as often as anything about it can change on purpose.</summary>
    public int SampleTicks { get; }

    public ExamLattice Lattice => _lattice;

    public int Cars => _cars.Length;

    /// <summary>How many ticks of the town this has watched, which is what says a card has had its chance.</summary>
    public int Ticked => _tick + 1;

    public ExamCarLog Car(int card, int driver) => _cars[_lattice.CarOf(card, driver)];

    public ExamWalkerLog Walker(int card, int walker) => _walkers[_lattice.WalkerOf(card, walker)];

    ExamBand Band(int card, ExamArm arm) => _bands[(card * 4) + (int)arm];

    /// <summary>
    /// The lane a driver arrives at its junction on, which is what a light is read for — or −1 where its
    /// junction is not lit, or it arrives on nothing a light governs.
    /// </summary>
    int ApproachLane(int card, int driver)
    {
        var of = _lattice.Card(card);
        if (!of.Lit) return -1;

        var cell = _lattice.CellOf(card);
        var arm = _lattice.Arm(card, of.Drivers[driver].From);
        var road = _lattice.Ground.ArmRoad(cell, arm);
        var junction = _lattice.Ground.ArmJunction(cell, arm);
        foreach (var lane in _world.Roads.LanesIntoJunction(junction))
        {
            if (_world.Roads.LaneRoad[lane] == road) return lane;
        }

        return -1;
    }

    /// <summary>
    /// <b>A car stood down from whatever the map gave it and put where its card wants it</b>: sent at once if
    /// the card sends it at once, and otherwise held where it stands until its moment comes.
    /// </summary>
    /// <remarks>
    /// <b>Stood down before it is sent.</b> A map with nowhere to be on it puts every car on a tour when it is
    /// laid (CAR-1), and a leg planned over a line already laid through the junction is planned from the far end
    /// of that line. The reset is what a hand taking the wheel does.
    /// </remarks>
    void Stage(int car)
    {
        var log = _cars[car];
        _world.ReleaseOrderOfCar(car);
        if (!log.Drives.Parked && log.Drives.Start == ExamStart.AtOnce && log.Drives.DelayS <= 0f)
        {
            Send(car, tick: 0);
            return;
        }

        Order(car, _lattice.StandM(log.Card, log.Driver));
    }

    void Send(int car, int tick)
    {
        var log = _cars[car];
        log.StartedAt = tick;
        if (log.Drives.Emergency) _world.Cars.BlueLight[car] = true;

        Order(car, _lattice.AimM(log.Card, log.Driver));
    }

    void Order(int car, Vector2 toM)
    {
        if (_world.OrderCar(car, toM)) return;

        var log = _cars[car];
        throw new InvalidOperationException(
            $"\"{_lattice.Card(log.Card).Name}\": driver {log.Driver} would not take the order to {toM}.");
    }

    /// <summary>One tick of the town, seen: every staged car read off its body, sent when it is due and held when it is done.</summary>
    public void Saw()
    {
        var tick = ++_tick;
        var nowS = _world.ElapsedS;
        for (var car = 0; car < _cars.Length; car++)
        {
            var log = _cars[car];
            if (log.StartedAt < 0 && !log.Drives.Parked && Due(log, nowS)) Send(car, tick);

            Read(car, tick);
            Hold(car);
        }

        var sent = 0;
        for (var walker = 0; walker < _walkers.Length; walker++) Walk(walker, tick, ref sent);
    }

    /// <summary>
    /// Whether a car still standing is to be sent now: its delay run, and — for a card about a light — the
    /// tick its own approach turns to the colour the card is about, so the whole of that colour is ahead of it.
    /// </summary>
    bool Due(ExamCarLog log, float nowS)
    {
        if (log.Drives.Start == ExamStart.AtOnce) return nowS >= log.Drives.DelayS;
        if (log.ApproachLane < 0) return true;

        var colour = _world.Signals.ForApproach(log.ApproachLane, nowS);
        var wanted = log.Drives.Start == ExamStart.OnRed ? SignalColour.Red : SignalColour.Green;
        var turned = colour == wanted && log.WasShowing != wanted && log.WasShowing != ExamCarLog.NotYetRead;
        log.WasShowing = colour;
        if (turned) log.TurnedAtS = nowS;

        return log.TurnedAtS >= 0f && nowS >= log.TurnedAtS + log.Drives.DelayS;
    }

    /// <summary>
    /// <b>A car the exam is not sending anywhere is sent to where it stands</b>: one not sent yet, one that
    /// has arrived, one whose leg was given up, and an obstruction for the whole card. An order finished is a
    /// car handed back to the map, and the map would drive it (CAR-1).
    /// </summary>
    /// <remarks>
    /// <b>On the tick the order finishes and not once the car has moved.</b> A place behind a car that has
    /// already pulled away is a place the search reaches by driving round the block, which is the tour this
    /// exists to stop, dressed as an order.
    /// </remarks>
    void Hold(int car)
    {
        if (_world.OrderOf(car) != PlayerOrder.None || _world.Cars.Broken[car]) return;

        var log = _cars[car];
        var atM = log.StartedAt < 0 || log.Drives.Parked ? _lattice.StandM(log.Card, log.Driver)
            : log.ArrivedAt >= 0 ? _lattice.AimM(log.Card, log.Driver)
            : _world.Cars.PositionM[car];
        _world.OrderCar(car, atM);
    }

    void Read(int car, int tick)
    {
        var log = _cars[car];
        var cars = _world.Cars;
        var stageM = _lattice.StageM(log.Card);
        var atM = cars.PositionM[car];
        var forward = Heading.Unit(cars.HeadingRad[car]);
        var halfM = cars.BuildOf(car).HalfLengthM;
        var noseM = atM + (forward * halfM);
        var tailM = atM - (forward * halfM);
        var speedMps = cars.VelocityMps[car].Length();
        var boxM = _lattice.BoxRadiusM(log.Card);
        var to = _lattice.Outward(log.Card, log.Drives.To);
        var atRest = speedMps < _config.Driving.StopSpeedMps;

        log.Touched |= _world.PhysicsForInstruments.OverlapOf(cars.Body[car]) > SoakProbe.OverlapAllowanceM;
        log.Wrecked |= cars.Broken[car];
        if (log.Touched && log.TouchedAt < 0) log.TouchedAt = tick;

        // What its own approach is showing, read at the bar and frozen there: what a red forbids is going
        // past the place it is shown at, and a car already over it when the phase turned is clearing the box
        // rather than running the light (TLT-4's amber tail).
        var red = log.ApproachLane >= 0
                  && _world.Signals.ForApproach(log.ApproachLane, _world.ElapsedS) == SignalColour.Red;
        var pastTheBar = log.ApproachLane >= 0 && PastTheBar(log, noseM);
        if (pastTheBar && !log.WasPastTheBar && red && log.EnteredAt < 0) log.CrossedTheBarOnARed = true;
        log.WasPastTheBar = pastTheBar;

        if (tick % SampleTicks == 0 && log.Samples < log.X.Length)
        {
            log.X[log.Samples] = atM.X;
            log.Y[log.Samples] = atM.Y;
            log.Held[log.Samples] = log.MovedOff && atRest && !red && log.ClearedAt < 0 && log.StartedAt >= 0;
            log.Samples++;
        }

        // The card is over for this car the moment its order is: anything read after the arrival is the
        // hold the exam put it on, not the card.
        if (log.ArrivedAt >= 0 || log.Drives.Parked) return;

        var onTheBox = (noseM - stageM).Length() <= boxM;
        if (onTheBox && log.EnteredAt < 0) log.EnteredAt = tick;
        if (log.EnteredAt >= 0 && log.LeftAt < 0 && (tailM - stageM).Length() > boxM
            && (noseM - stageM).Length() > boxM)
        {
            log.LeftAt = tick;
        }

        // Clear of the box on the arm it was sent out by, far enough along it to be past the paint there.
        var offM = atM - stageM;
        if (log.ClearedAt < 0 && log.EnteredAt >= 0 && Vector2.Dot(offM, to) > boxM + halfM) log.ClearedAt = tick;

        if ((atM - _lattice.AimM(log.Card, log.Driver)).Length() <= _config.OrderedPlaceReachM && atRest
            && log.StartedAt >= 0)
        {
            log.ArrivedAt = tick;
            return;
        }

        if (log.StartedAt >= 0 && log.GaveUpAt < 0 && _world.OrderOf(car) == PlayerOrder.None)
        {
            log.GaveUpAt = tick;
            log.GaveUpHeld = cars.Hold[car];
        }

        // Moved off: every car here starts stopped, so the ticks it takes to pull away are the order being
        // taken up rather than anything on the road holding it — and <b>an order is taken up at a decision</b>,
        // so a car still settling onto its stand when it is sent brings itself to rest there first, on the order
        // it had. <b>Held is anywhere short of clearing the box</b> and not only short of its edge: a car
        // waiting at the mouth has its nose on the box, and counted from there it would read as never having
        // waited at all.
        log.MovedOff |= log.StartedAt >= 0 && tick > log.StartedAt + SampleTicks
                        && speedMps >= _config.Driving.StopSpeedMps;
        if (log.MovedOff && atRest && log.ClearedAt < 0)
        {
            log.StoodTicks++;
            if (!red)
            {
                // What held it is read on the tick it came to rest: by the last tick of a wait, whatever held it
                // has let go, and what is left binding is whatever the car pulls away against.
                if (!log.WasHeld)
                {
                    log.HeldFor = cars.Hold[car];
                    log.HeldIn = cars.Context[car];
                    var hold = _world.DriveHold(car);
                    log.HeldBy = LaneClaim.Nothing;
                    if (hold != LaneOccupancy.NoHold) _world.Occupancy.HoldEndsAtM(hold, out _, out log.HeldBy);
                }

                log.HeldTicks++;
            }
        }

        log.WasHeld = log.MovedOff && atRest && log.ClearedAt < 0 && !red;

        var of = _lattice.Card(log.Card);
        for (var walker = 0; walker < of.Walkers.Length; walker++)
        {
            if (of.Walkers[walker].Strolls) continue;

            var band = Band(log.Card, of.Walkers[walker].Crossing);
            if (!band.Covers(noseM, tailM)) continue;

            if (log.OnThePaintAt[walker] < 0) log.OnThePaintAt[walker] = tick;

            // <b>With the walker in its way, not merely on the same paint</b>: somebody a lane's width across
            // from the car's own middle is on the far half of the road, and a car going over behind them has
            // given them way.
            var person = _lattice.WalkerOf(log.Card, walker);
            var acrossM = band.AcrossM(_world.People.PositionM[person]) - band.AcrossM(atM);
            if (_walkers[person].OnThePaintNow && MathF.Abs(acrossM) < _config.LaneWidthM)
            {
                log.SharedThePaintFor[walker]++;
            }
        }
    }

    /// <summary>
    /// Whether a car's nose is over its own bar and past it — <b>the far edge of the paint</b>, which is what
    /// "stop behind the line" leaves a car short of. A nose brought to rest on the paint's near edge has
    /// stopped at the line, and reading it as crossed would call every stop a millimetre long a red run.
    /// </summary>
    bool PastTheBar(ExamCarLog log, Vector2 noseM)
    {
        var bars = _world.Bars;
        for (var bar = 0; bar < bars.Count; bar++)
        {
            if (bars.Lane[bar] != log.ApproachLane) continue;

            var farEdgeM = bars.CentreM[bar] + (bars.Approach[bar] * bars.ThicknessM[bar] * 0.5f);
            return Vector2.Dot(noseM - farEdgeM, bars.Approach[bar]) > 0f;
        }

        return false;
    }

    /// <summary>
    /// <b>One walker's card, walked</b>: sent over its zebra when its delay has run, and read — whether it is
    /// on the paint, and whether it got over.
    /// </summary>
    void Walk(int walker, int tick, ref int sent)
    {
        var log = _walkers[walker];

        // The people are laid after the cars and in the walkers' own order, so a walker's number is its body's.
        var person = walker;
        var people = _world.People;
        var atM = people.PositionM[person];

        // The interface takes as many orders a tick as a selection can hold (CTL-1b), so a walker past that is
        // sent on the next tick rather than dropped.
        if (log.StartedAt < 0 && WalkerDue(log) && sent < _config.View.SelectionMaxUnits)
        {
            _world.Order(person, log.HeadingForM);
            log.StartedAt = tick;
            sent++;
        }

        log.Down |= people.Wounded[person];
        log.Touched |= _world.PhysicsForInstruments.OverlapOf(people.Body[person]) > SoakProbe.OverlapAllowanceM;

        // <b>On the paint means crossing it</b>: a body standing at the edge of the paint before it sets off,
        // or at the far edge once it is over, is somebody at the kerb and not somebody the traffic is crossing.
        log.OnThePaintNow = log.StartedAt >= 0 && log.ArrivedAt < 0 && log.Band.Holds(atM, people.RadiusM[person]);
        if (log.OnThePaintNow && log.OnThePaintAt < 0) log.OnThePaintAt = tick;
        if (log.ArrivedAt >= 0 || log.StartedAt < 0 || (atM - log.HeadingForM).Length() > people.RadiusM[person])
        {
            return;
        }

        if (log.HeadingForM == log.ToM)
        {
            log.ArrivedAt = tick;
            return;
        }

        // Standing in the lane: for its pause, and then on over to the far side.
        if (log.PausedAtS < 0f) log.PausedAtS = _world.ElapsedS;
        if (_world.ElapsedS < log.PausedAtS + log.Walks.PauseS || sent >= _config.View.SelectionMaxUnits) return;

        log.HeadingForM = log.ToM;
        _world.Order(person, log.ToM);
        sent++;
    }

    /// <summary>
    /// Whether a walker still standing is to be sent: its delay run — from the start, or from the moment the
    /// car it waits for first came near enough to the paint, or to where a walk along the pavement sets off.
    /// </summary>
    bool WalkerDue(ExamWalkerLog log)
    {
        if (log.Walks.WaitsFor == ExamClaim.Nobody) return _world.ElapsedS >= log.Walks.DelayS;

        if (log.CarCameAtS < 0f)
        {
            var car = _lattice.CarOf(log.Card, log.Walks.WaitsFor);
            var cars = _world.Cars;
            var noseM = cars.PositionM[car] + (Heading.Unit(cars.HeadingRad[car]) * cars.BuildOf(car).HalfLengthM);
            var meetsM = log.Walks.Strolls ? log.FromM : log.Band.CentreM;
            if ((noseM - meetsM).Length() > log.Walks.WithinM) return false;

            log.CarCameAtS = _world.ElapsedS;
        }

        return _world.ElapsedS >= log.CarCameAtS + log.Walks.DelayS;
    }

    /// <summary>
    /// <b>Whether a card has been answered at all yet.</b> Every car it sends has either arrived or had its leg
    /// given up, and everybody it sends over has got over or is down — and until then it is a question still
    /// being asked rather than one the engine got wrong. The exam's own window decides the rest.
    /// </summary>
    public bool Decided(int card)
    {
        if (Ticked == 0) return false;
        if (Ticked >= Ticks) return true;

        var of = _lattice.Card(card);
        for (var driver = 0; driver < of.Drivers.Length; driver++)
        {
            var log = Car(card, driver);
            if (log.Drives.Parked) continue;
            if (log.ArrivedAt < 0 && log.GaveUpAt < 0 && !log.Wrecked) return false;
        }

        for (var walker = 0; walker < of.Walkers.Length; walker++)
        {
            var log = Walker(card, walker);
            if (log.ArrivedAt < 0 && !log.Down) return false;
        }

        return true;
    }

    public string Name(int card) => $"card {card} ({_lattice.Card(card).Name})";

    /// <summary>
    /// <b>What a plan was cut at, by whose card</b>: a body, a main claim or a secondary claim, of a driver or a
    /// walker named by the card that staged it — or that nothing cut it.
    /// </summary>
    public string Cut(in LaneClaim by)
    {
        if (!by.Found) return "nothing (its plan whole)";

        var what = by.HasBody ? "the body" : by.Secondary ? "the secondary claim" : "the main claim";
        var whose = by.Of switch
        {
            LaneRoster.Signal => $"light stretch {by.Occupant}",
            LaneRoster.Walking => by.Occupant < _walkers.Length
                ? $"walker {_walkers[by.Occupant].Walker} of card {_walkers[by.Occupant].Card}"
                : $"walker {by.Occupant}, staged by no card",
            _ => by.Occupant < _cars.Length
                ? $"driver {_cars[by.Occupant].Driver} of card {_cars[by.Occupant].Card}"
                : $"car {by.Occupant}, staged by no card",
        };
        return $"{what} of {whose} at {by.Priority}";
    }

    /// <summary>
    /// <b>What was wrong with the way a card went, or nothing</b> — judged once, on the first ask after the
    /// card is decided, and kept.
    /// </summary>
    /// <remarks>
    /// <b>Kept because it is read every frame and not only at the end.</b> A watch on a running town asks
    /// every decided card where it stands every time the panel is drawn, and a message composed afresh each
    /// time would be an allocation a frame per failing card.
    /// </remarks>
    public string? Verdict(int card)
    {
        if (_judged[card]) return _wrong[card];

        _wrong[card] = ExamJudge.Judge(this, _config, card);
        _judged[card] = Decided(card);
        return _wrong[card];
    }
}

/// <summary>
/// One zebra, as the harness reads it: the middle of the band, the way the traffic crosses it, and how deep
/// and how wide it is.
/// </summary>
internal readonly record struct ExamBand(Vector2 CentreM, Vector2 Along, float DepthM, float SpanM)
{
    public static ExamBand None => new(Vector2.Zero, Vector2.Zero, 0f, 0f);

    public bool Exists => SpanM > 0f;

    /// <summary>The band between two kerb points: square to the walk, a crossing's depth deep.</summary>
    public static ExamBand Across(Vector2 leftM, Vector2 rightM, float depthM)
    {
        var run = rightM - leftM;
        var spanM = run.Length();
        return new ExamBand((leftM + rightM) * 0.5f, Heading.RightOf(run / spanM), depthM, spanM);
    }

    /// <summary>Whether a body from its tail to its nose lies over the paint anywhere along its length.</summary>
    public bool Covers(Vector2 noseM, Vector2 tailM)
    {
        if (!Exists) return false;

        var noseAlongM = Vector2.Dot(noseM - CentreM, Along);
        var tailAlongM = Vector2.Dot(tailM - CentreM, Along);
        var across = Heading.RightOf(Along);
        var middleAcrossM = Vector2.Dot(((noseM + tailM) * 0.5f) - CentreM, across);
        return MathF.Min(noseAlongM, tailAlongM) <= DepthM * 0.5f
               && MathF.Max(noseAlongM, tailAlongM) >= -DepthM * 0.5f
               && MathF.Abs(middleAcrossM) <= SpanM * 0.5f;
    }

    /// <summary>How far across the road from the middle of the paint a point stands, towards its right-hand kerb.</summary>
    public float AcrossM(Vector2 atM) => Vector2.Dot(atM - CentreM, Heading.RightOf(Along));

    /// <summary>Whether a body on foot of this radius stands on the paint.</summary>
    public bool Holds(Vector2 atM, float radiusM)
    {
        if (!Exists) return false;

        var offM = atM - CentreM;
        return MathF.Abs(Vector2.Dot(offM, Along)) <= (DepthM * 0.5f) + radiusM
               && MathF.Abs(Vector2.Dot(offM, Heading.RightOf(Along))) <= SpanM * 0.5f;
    }
}

/// <summary>What one staged car did, read off its body every tick and never off what its driver intended.</summary>
internal sealed class ExamCarLog
{
    /// <summary>What <see cref="WasShowing"/> holds before a light has ever been read.</summary>
    public const SignalColour NotYetRead = (SignalColour)byte.MaxValue;

    public ExamCarLog(int card, int driver, ExamDriver drives, int walkers, int samples, int approachLane)
    {
        Card = card;
        Driver = driver;
        Drives = drives;
        ApproachLane = approachLane;
        OnThePaintAt = new int[walkers];
        SharedThePaintFor = new int[walkers];
        Array.Fill(OnThePaintAt, -1);
        X = new float[samples];
        Y = new float[samples];
        Held = new bool[samples];
    }

    public int Card { get; }

    public int Driver { get; }

    public ExamDriver Drives { get; }

    /// <summary>The lane it arrives at a lit junction on, or −1.</summary>
    public int ApproachLane { get; }

    public SignalColour WasShowing = NotYetRead;
    public float TurnedAtS = -1f;
    public bool WasPastTheBar;

    public int StartedAt = -1;
    public int EnteredAt = -1;
    public int LeftAt = -1;
    public int ClearedAt = -1;
    public int ArrivedAt = -1;
    public int GaveUpAt = -1;
    public DrivingHold GaveUpHeld;
    public int TouchedAt = -1;

    public bool MovedOff;
    public bool Touched;
    public bool Wrecked;
    public bool CrossedTheBarOnARed;

    /// <summary>Ticks at rest after it had moved off and before it was clear of the box, but for a red.</summary>
    public int HeldTicks;

    /// <summary>What its driver said held it, on the tick it last came to rest.</summary>
    public DrivingHold HeldFor;

    /// <summary>Whether it was held the tick before, which is what says a tick at rest is the start of a wait.</summary>
    public bool WasHeld;

    /// <summary>And what the driver had been told then: the headway, the stop, the crossing and the place.</summary>
    public DriveContext HeldIn = DriveContext.Clear;

    /// <summary>And what its plan was cut at then — <see cref="LaneClaim.Nothing"/> where its plan was whole.</summary>
    public LaneClaim HeldBy = LaneClaim.Nothing;

    /// <summary>And every tick at rest there, a red included.</summary>
    public int StoodTicks;

    /// <summary>The first tick its body lay over each of its card's walkers' paint, or −1.</summary>
    public int[] OnThePaintAt { get; }

    /// <summary>And how many ticks it lay there while that walker was on it.</summary>
    public int[] SharedThePaintFor { get; }

    /// <summary>Where it was, once a decision, and whether it was being held there.</summary>
    public float[] X { get; }

    public float[] Y { get; }

    public bool[] Held { get; }

    public int Samples;
}

/// <summary>What one staged walker did, read off its body every tick.</summary>
internal sealed class ExamWalkerLog
{
    public ExamWalkerLog(
        int card, int walker, ExamWalker walks, Vector2 fromM, Vector2 inTheLaneM, Vector2 toM, ExamBand band)
    {
        Card = card;
        Walker = walker;
        Walks = walks;
        FromM = fromM;
        ToM = toM;
        Band = band;
        HeadingForM = walks.PauseS > 0f ? inTheLaneM : toM;
    }

    public int Card { get; }

    public int Walker { get; }

    public ExamWalker Walks { get; }

    public Vector2 FromM { get; }

    public Vector2 ToM { get; }

    public ExamBand Band { get; }

    /// <summary>When the car it waits for first came near enough, or −1 before it has.</summary>
    public float CarCameAtS = -1f;

    /// <summary>Where on the paint it is walking to now: the middle of the lane it pauses in, or the far edge.</summary>
    public Vector2 HeadingForM;

    /// <summary>When it came to stand in the lane, or −1 before it has.</summary>
    public float PausedAtS = -1f;

    public int StartedAt = -1;
    public int OnThePaintAt = -1;
    public int ArrivedAt = -1;
    public bool OnThePaintNow;
    public bool Down;
    public bool Touched;
}
