using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// The tick in shadow: what of the town the device has to be told before its tick, and what its tick did that the town's
/// did not.
/// </summary>
internal sealed partial class DeviceCarTick
{
    readonly float[] _spinMps = new float[TyreModel.Wheels];
    readonly float[] _scrubTravelM = new float[TyreModel.Wheels];
    readonly bool[] _marking = new bool[TyreModel.Wheels];
    readonly Vector2[] _markFromM = new Vector2[TyreModel.Wheels];
    readonly float[] _markIntensity = new float[TyreModel.Wheels];
    readonly SurfaceUnderWheel[] _surfaces = new SurfaceUnderWheel[TyreModel.Wheels];
    readonly WheelImpulse[] _impulses = new WheelImpulse[TyreModel.Wheels];
    readonly TyreScrub[] _scrub = new TyreScrub[TyreModel.Wheels];
    readonly TyreMark[] _marks = new TyreMark[TyreModel.Wheels];

    /// <summary>Whether the device ticked each car last tick, so that what its rows hold is what its own tick left.</summary>
    bool[] _held = [];

    /// <summary>How many lines the town had laid each car when the two ticks last left it, and when this one began.</summary>
    uint[] _lineLaidAt = [], _lineLaidDue = [];

    /// <summary>Where the town had each car on its line, and how far since its claim, when the two ticks last left it.</summary>
    float[] _progressAt = [], _coveredAt = [];

    /// <summary>What the town had each car granted and doing when the two ticks last left it.</summary>
    float[] _authorityAt = [];

    CarAction[] _actionAt = [];

    /// <summary>Which of each car's wheels its tyre row was last sent as standing on a way.</summary>
    byte[] _wheelsSent = [];

    /// <summary>Whether the town had already asked to grow each car's line before this tick.</summary>
    bool[] _askedBefore = [];

    /// <summary>The step each car is on going into this tick, as the device holds it.</summary>
    Overtake[] _passHeld = [];

    /// <summary>What each car was doing when this tick began.</summary>
    CarAction[] _actionDue = [];

    /// <summary>
    /// <b>Whether the town holds each car as the device last left it</b> (<see cref="ReadsEveryCar"/> off): handed back by its
    /// last tick, or by a hand-over since — or the town's own, not ticked here. A car the device ticked that the town did
    /// not read keeps its reading, command and tyres on the device, and nothing of the town's copy of them is ever laid.
    /// </summary>
    bool[] _current = [];

    /// <summary>Each car's command and tyres going into this tick, as the device holds them.</summary>
    DeviceTyres.CommandRow[] _commandBefore = [];
    DeviceTyres.CarRow[] _carBefore = [];
    DeviceTyres.WheelRow[] _wheelsBefore = [];

    void ShadowOf(int capacity)
    {
        _held = new bool[capacity];
        _lineLaidAt = new uint[capacity];
        _lineLaidDue = new uint[capacity];
        _progressAt = new float[capacity];
        _coveredAt = new float[capacity];
        _authorityAt = new float[capacity];
        _actionAt = new CarAction[capacity];
        _wheelsSent = new byte[capacity];
        _askedBefore = new bool[capacity];
        _passHeld = new Overtake[capacity];
        _actionDue = new CarAction[capacity];
        _cutOn = new int[capacity];
        _cutterOnTheDevice = new bool[capacity];
        _grantPending = new bool[capacity];
        _grantLeft = new bool[capacity];
        _commandBefore = new DeviceTyres.CommandRow[capacity];
        _carBefore = new DeviceTyres.CarRow[capacity];
        _wheelsBefore = new DeviceTyres.WheelRow[capacity * TyreModel.Wheels];
        _current = new bool[capacity];
        Array.Fill(_current, true);
        _townHolds = new AgentSet(capacity);
        _townHolds.AddFirst(capacity);
        _takenBits = new AgentSet(capacity);
        _visited = new AgentSet(capacity);
        _takenBack = new AgentSet(capacity);
        _visit = new int[capacity];
        _laidWhole = new bool[capacity];
    }

    /// <summary>Which cars <see cref="Prepare"/> laid whole off the town's copy since <see cref="WasLaidWhole"/> asked.</summary>
    bool[] _laidWhole = [];

    /// <summary>
    /// <b>Whether the town laid this car whole since last asked</b> — so the tick after is handed back
    /// (<see cref="ReadsEveryCar"/> off): what the device's tick alone writes of a car the town lays from its copy, as the
    /// copy had it. A car lost its line is laid with how far off it was, and the town's claims read whether it is
    /// back on (<c>TownWorld.IsUnderWay</c>), which only a tick handed back says.
    /// </summary>
    public bool WasLaidWhole(int car)
    {
        var laid = _laidWhole[car];
        _laidWhole[car] = false;
        return laid;
    }

    /// <summary>
    /// <b>Whether the device ticks this car</b>: driven, following its route's line on no pass, waiting to rejoin it,
    /// moving across onto the lane beside, or getting past something on the lane its pass was decided along, and on
    /// nobody's arm — the tick <c>car_tick.glsl</c> is. Asked before the town's own tick, of the state that tick reads.
    /// </summary>
    /// <remarks>
    /// <b>A rejoining car's tick is a following car's</b> (<c>Rejoining.Tick</c>): its line read, held where it is still
    /// off it, and followed where it is back on — the action it enters then being the host's to change. <b>An overtaking
    /// car off the lane of its pass</b> is the town's, whose own tick lets the pass go.
    /// </remarks>
    public static bool Takes(TownWorld town, int car)
    {
        var cars = town.Cars;
        var action = cars.Action[car];
        return cars.Driven[car] && !town.OnTheHook(car)
               && (action is CarAction.Follow or CarAction.Rejoin && !cars.Pass[car].Any || action == CarAction.Switch
                   || action == CarAction.Overtake && !town.Overtaking.IsOffTheLaneOfItsPass(car));
    }

    /// <summary>
    /// How many switching cars' steps the town's claims kept in the tick the device was asked of, and which the device —
    /// not yet answering a step itself — drove as still asked for (<see cref="Disagreement"/>).
    /// </summary>
    public int StepsKeptByTheTown { get; private set; }

    /// <summary>And how many stepped out onto the lane beside, which the town lays and the device drives on along the step.</summary>
    public int SteppedOut { get; private set; }

    /// <summary>
    /// <b>The town as its next tick reads it, told to the device</b>: every car's pose, and what each car the device takes
    /// is told and was granted. A car it did not take last tick is laid whole; one it did keeps what the device's own tick
    /// left, but for what the town changed since — a line laid, a reading set, a level crossed. A car it does not take is
    /// the town's, and its tyres run on the town's own state.
    /// </summary>
    /// <remarks>
    /// <b>Only a car the town holds or touched is walked</b> (<see cref="Walked"/>, <see cref="TownWorld.TouchedCars"/>):
    /// one the device keeps, which the town has set nothing of, is ticked as it was and laid nothing — every car is walked
    /// only where what each is told or read for changed at once (<see cref="VisitsEveryCar"/>).
    /// </remarks>
    /// <param name="taken">Filled with the cars the device takes, in the order the list sends them.</param>
    /// <param name="resident">
    /// Whether a car the device took last tick keeps what that tick left; otherwise every car it takes is laid whole every
    /// tick, so each tick is asked of exactly the state the town's reads.
    /// </param>
    /// <param name="claimsLaid">
    /// Whether the town's claims were laid since the last tick, so that every car's grant is sent; otherwise only a car whose
    /// grant the town set since — an action entered, a lane stepped onto — has it sent.
    /// </param>
    /// <param name="chosen">
    /// Which cars the town left to the device as its own tick reached each (<see cref="ITownDevice.Takes"/>) — asked again
    /// here, a car whose own tick moved it into one the device takes would be ticked twice. Empty, each car is asked here.
    /// </param>
    /// <param name="posesInTheTable">
    /// Whether the tyre pass's table already holds every car's pose as the tick is to read it — left by the bodies' step on
    /// the device, and the ones the town changed since laid (<see cref="DeviceTownBodies.PosesLeftInTheTable"/>) — so none
    /// is laid here.
    /// </param>
    /// <returns>How many cars the device takes.</returns>
    public int Prepare(
        TownWorld town, Span<int> taken, bool resident, bool claimsLaid = true, ReadOnlySpan<bool> chosen = default,
        bool posesInTheTable = false)
    {
        var cars = town.Cars;

        // A car's build is its variant's, set once as it was added; a car added is the town's own.
        for (var car = _buildsLaid; car < cars.Count; car++)
        {
            _tyres.Table.BuildOf.Lay(car, (uint)cars.Variant[car]);
            _townHolds.Add(car);
        }

        _buildsLaid = cars.Count;

        // Every car where the town lays what the device would keep — its poses, its grants, every car whole — or where
        // anything every car is told by changed; otherwise only those the town holds or touched.
        var every = !resident || !posesInTheTable || (claimsLaid && !GrantedOnTheDevice) || VisitsEveryCar;
        VisitsEveryCar = false;
        var visit = every ? EveryCar(cars.Count) : _visit.AsSpan(0, _townHolds.Union(town.TouchedCars, cars.Count, _visit));
        _walkedCount = visit.Length;
        town.TouchedCars.Clear();
        _visited.Clear();
        foreach (var car in visit)
        {
            _visited.Add(car);
            if (!posesInTheTable) LayThePose(town, car);
            _askedBefore[car] = town.AsksTheRoute(car);
            _lineLaidDue[car] = cars.LineLaid[car];
            if (chosen.IsEmpty ? !Takes(town, car) : !chosen[car])
            {
                if (_tyres.KeptOnTheDevice[car])
                {
                    throw new InvalidOperationException($"Car {car} left the device's tick with its wheels never handed back.");
                }

                ExpectTheTownHolds(car, "its tyres for the town's own tick");
                LayTheTyres(town, car);
                _held[car] = false;
                _takenBits.Remove(car);
                continue;
            }

            // A car whose action the town changed since the device last left it is the town's again, and laid whole.
            if (_held[car] && cars.Action[car] != _actionAt[car]) _held[car] = false;

            _takenBits.Add(car);
            _actionDue[car] = cars.Action[car];
            var granted = !resident || !_held[car] || claimsLaid || cars.AuthorityM[car] != _authorityAt[car];
            LayWhatItIsTold(town, car, granted, claimsLaid);
            if (!resident || !_held[car] || cars.Pass[car] != _passHeld[car]) LayThePass(cars, car);
            if (!resident || !_held[car])
            {
                ExpectTheTownHolds(car, "itself whole");
                _laidWhole[car] = true;
                LayTheTyres(town, car, wheels: !_tyres.KeptOnTheDevice[car]);
                LayTheLine(cars, car);
                LayTheReading(cars, car);
                LayTheBox(cars, car);
                LayTheClockOf(town, car);
            }
            else
            {
                if (town.DriveClocks.IsTouched(car)) LayTheClockOf(town, car);

                if (cars.LineLaid[car] != _lineLaidAt[car]) LayTheLine(cars, car);
                if (cars.ProgressM[car] != _progressAt[car] || cars.CoveredSinceClaimM[car] != _coveredAt[car])
                {
                    ExpectTheTownHolds(car, "its reading");
                    LayTheReading(cars, car);
                }

                if (cars.WheelsOnAWay[car] != _wheelsSent[car])
                {
                    ExpectTheTownHolds(car, "its tyre row");
                    LayTheCarRow(town, car);
                }
            }

            _held[car] = true;
        }

        if (ChecksTheVisits && !every) HoldTheUnvisited(town, chosen, claimsLaid);

        var count = _takenBits.CopyTo(cars.Count, taken);
        _taken.LayEvery(MemoryMarshal.Cast<int, uint>(taken[..count]));
        if (!posesInTheTable) _tyres.Table.Poses.Mark(0, cars.Count);
        return count;
    }

    /// <summary>
    /// <b>Whether the next <see cref="Prepare"/> walks every car</b>: set where what every car is told changed at once — a
    /// watch begun or let go, the agents held or let go, a scene opened — and cleared by it.
    /// </summary>
    public bool VisitsEveryCar { get; set; } = true;

    /// <summary>
    /// <b>Whether to hold every car <see cref="Prepare"/> did not walk to what a walk of it would have laid</b>, and throw
    /// where it differs: a car the town changed while the device kept it, and did not mark (<see cref="TownWorld.TouchedCars"/>).
    /// </summary>
    public bool ChecksTheVisits { get; set; } = ChecksTheVisitsInEveryTown;

    /// <summary>What <see cref="ChecksTheVisits"/> begins as in every car tick made after it is set — the suite's, on.</summary>
    internal static bool ChecksTheVisitsInEveryTown { get; set; }

    /// <summary>Which cars the town holds as the device last left them, a bit a car (<see cref="TownHolds"/>).</summary>
    AgentSet _townHolds = null!;

    /// <summary>Which cars the device ticks, and which the last <see cref="Prepare"/> walked.</summary>
    AgentSet _takenBits = null!, _visited = null!;

    /// <summary>The cars a <see cref="Prepare"/> walks, in order, and how many the last walked.</summary>
    int[] _visit = [];

    int _walkedCount;

    /// <summary>The cars the last <see cref="Prepare"/> walked, in order: every car whose figures the town may have set.</summary>
    public ReadOnlySpan<int> Walked => _visit.AsSpan(0, _walkedCount);

    /// <summary>Whether the last <see cref="Prepare"/> walked this car, so what the town holds of it is current there.</summary>
    public bool Visited(int car) => _visited.Has(car);

    Span<int> EveryCar(int count) => EveryCar(count, _visit);

    /// <summary>Every car below <paramref name="count"/>, in order, written into <paramref name="into"/>.</summary>
    public Span<int> EveryCar(int count, Span<int> into)
    {
        for (var car = 0; car < count; car++) into[car] = car;
        return into[..count];
    }

    /// <summary>
    /// Every car <see cref="Prepare"/> did not walk held to what walking it would have laid: taken as it was, doing what it
    /// did, on the line, pass and reading it was, told what it was, its clock untouched and not read by the town.
    /// </summary>
    void HoldTheUnvisited(TownWorld town, ReadOnlySpan<bool> chosen, bool claimsLaid)
    {
        var cars = town.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            if (_visited.Has(car)) continue;

            var takes = chosen.IsEmpty ? Takes(town, car) : chosen[car];
            var unlike =
                takes != _takenBits.Has(car) ? "whether the device ticks it"
                : !_held[car] ? "whether the device holds it"
                : cars.Action[car] != _actionAt[car] ? "what it is doing"
                : cars.LineLaid[car] != _lineLaidAt[car] ? "its line"
                : cars.Pass[car] != _passHeld[car] ? "its pass"
                : cars.ProgressM[car] != _progressAt[car] || cars.CoveredSinceClaimM[car] != _coveredAt[car] ? "its reading"
                : cars.WheelsOnAWay[car] != _wheelsSent[car] ? "which of its wheels stand on a way"
                : WouldLayTheGrant(town, car) ? "its grant"
                : town.DriveClocks.IsTouched(car) ? "its clock"
                : ToldOf(town, car) != _told.LaidRow(car) ? "what it is told"
                : town.ReadsTheTickOf(car) ? "whether the town reads it"
                : null;
            if (unlike is null) continue;

            throw new InvalidOperationException(
                $"Car {car} was changed by the town while the device kept it, and not marked touched: {unlike} (claims laid {claimsLaid}).");
        }
    }

    /// <summary>How many cars' builds are laid: every car's but those added since.</summary>
    int _buildsLaid;


    /// <summary>
    /// <b>Whether the town laid a car a line or set it doing something else since <see cref="Prepare"/> sent it</b> — a
    /// decision taken while the tick ran (<see cref="DeviceTown.DecidesATickBehind"/>) — so what the tick read of it is of a
    /// line or an action the town has moved on from.
    /// </summary>
    public bool TheTownMovedOnFrom(CarFleet cars, int car) => cars.LineLaid[car] != _lineLaidDue[car] || cars.Action[car] != _actionDue[car];

    /// <summary>A car the town moved on from laid whole the next time it is taken, its line among it.</summary>
    public void LayItWholeNext(int car) => _held[car] = false;

    /// <summary>A car the last tick handed back, or a hand-over since: the town holds it as the device left it (<see cref="_current"/>).</summary>
    public void TookBack(int car)
    {
        _current[car] = true;
        _townHolds.Add(car);
    }

    /// <summary>A car the last tick ticked that the town did not read: its reading, command and tyres are the device's (<see cref="_current"/>).</summary>
    public void LeftOnTheDevice(int car)
    {
        _current[car] = false;
        _townHolds.Remove(car);
    }

    /// <summary>
    /// The cars the town holds as the device left them or touched since <see cref="Prepare"/> — or every car, where the next
    /// walks every one — in order, written into <paramref name="into"/>: those whose figures the town may have set.
    /// </summary>
    public Span<int> TheTownMayHaveSet(TownWorld town, Span<int> into)
    {
        var count = town.Cars.Count;
        return VisitsEveryCar ? EveryCar(count, into) : into[.._townHolds.Union(town.TouchedCars, count, into)];
    }

    /// <summary>Whether the town holds a car as the device last left it (<see cref="_current"/>).</summary>
    public bool TownHolds(int car) => _current[car];

    /// <summary>
    /// Whether the device ticks this car this tick, once <see cref="Prepare"/> has taken it — and once that tick is taken
    /// back, whether the tables still hold the car as it left it: the town neither laid it a line nor set it doing
    /// something else since (<see cref="TheTownMovedOnFrom"/>).
    /// </summary>
    public bool Takes(int car) => _held[car];

    /// <summary>
    /// A car about to be laid off the town's copy of it, which must be what the device last left — or the town's own since:
    /// a copy older than the device's laid over it would set the car back.
    /// </summary>
    /// <param name="what">What of it would be laid, for the defect's report.</param>
    void ExpectTheTownHolds(int car, string what)
    {
        if (!_current[car])
        {
            throw new InvalidOperationException(
                $"Car {car} would have {what} laid off the town's copy, older than the device's (held {_held[car]}, doing " +
                $"{_actionAt[car]} when the ticks last left it, {_actionDue[car]} when this one began).");
        }
    }

    /// <summary>
    /// <b>A held car's claim cleared on the device</b> by the rebuild that asked it (<c>car_claims_gather.comp</c>): the
    /// ground it covered since is nothing there as in the town (<see cref="DrivingGround.ClearTheClaim"/>), and the town's
    /// clearing of it is no change to send.
    /// </summary>
    public void ClaimClearedOnTheDevice(int car) => _coveredAt[car] = 0f;

    /// <summary>
    /// What both ticks left, remembered for the next: the town's lines, readings and levels, and the device's command and
    /// tyres for every car it took.
    /// </summary>
    public void Settle(TownWorld town, ReadOnlySpan<int> taken)
    {
        SettleTheTown(town);
        SettleTheDevice(town.Cars, taken);
    }

    /// <summary>
    /// The town's half of <see cref="Settle"/>: its lines, readings and levels as its tick left them — taken before its
    /// decisions, so that what those change is what <see cref="Prepare"/> sends next.
    /// </summary>
    /// <remarks>
    /// <b>A car whose action the town's tick changed is laid whole next tick</b>: what the change did — the lane beside a
    /// switch stepped out onto, a line lost or rejoined — is the host's, and the device's tick made none of it.
    /// </remarks>
    public void SettleTheTown(TownWorld town)
    {
        for (var car = 0; car < town.Cars.Count; car++) SettleTheTown(town.Cars, car);
    }

    /// <summary><see cref="SettleTheTown(TownWorld)"/> of only these cars: every other the town has set nothing of since.</summary>
    public void SettleTheTown(CarFleet cars, ReadOnlySpan<int> settled)
    {
        foreach (var car in settled) SettleTheTown(cars, car);
    }

    void SettleTheTown(CarFleet cars, int car)
    {
        _lineLaidAt[car] = cars.LineLaid[car];
        _progressAt[car] = cars.ProgressM[car];
        _coveredAt[car] = cars.CoveredSinceClaimM[car];
        _authorityAt[car] = cars.AuthorityM[car];
        _actionAt[car] = cars.Action[car];
        if (_held[car] && cars.Action[car] != _actionDue[car]) _held[car] = false;
    }

    /// <summary>
    /// <b>The cars this tick took that the town may read or have set</b>, in order: those <see cref="Prepare"/> walked, those
    /// it holds now, and those whose tick asked something of it — or every one, where every car is handed back.
    /// </summary>
    public Span<int> TheTakenToTakeBack(int count, ReadOnlySpan<uint> events, Span<int> into)
    {
        if (ReadsEveryCar) return into[.._takenBits.CopyTo(count, into)];

        _takenBack.CopyFrom(_visited);
        _takenBack.UnionWith(_townHolds);
        foreach (var car in events) _takenBack.Add((int)car);
        _takenBack.IntersectWith(_takenBits);
        return into[.._takenBack.CopyTo(count, into)];
    }

    /// <summary>The cars <see cref="TheTakenToTakeBack"/> last named, a bit a car.</summary>
    AgentSet _takenBack = null!;

    /// <summary>
    /// The cars the last <see cref="Prepare"/> walked and those the town holds now — every car whose body, rest or wheels
    /// the town may have set — or every car, where the next walks every one; in order.
    /// </summary>
    public Span<int> TheCarsToLay(int count, Span<int> into)
    {
        if (VisitsEveryCar) return EveryCar(count, into);

        _takenBack.CopyFrom(_visited);
        _takenBack.UnionWith(_townHolds);
        return into[.._takenBack.CopyTo(count, into)];
    }

    /// <summary>The cars the town holds as the device left them, in order, written into <paramref name="into"/> — whose poses it reads.</summary>
    public Span<int> TheCarsTheTownHolds(int count, Span<int> into) => into[.._townHolds.CopyTo(count, into)];

    /// <summary>Whether this car is among those the device ticks (<see cref="Prepare"/>).</summary>
    public bool IsTaken(int car) => _takenBits.Has(car);

    /// <summary>Which cars the device ticks, a bit a car — for a caller that keeps what the last tick took.</summary>
    public AgentSet TakenCars => _takenBits;

    /// <summary>The device's half of <see cref="Settle"/>: its command and tyres for every car it took, as its tick left them.</summary>
    /// <remarks>
    /// <b>Unshadowed, the pass held is the town's</b> once the device's shift is written back
    /// (<see cref="Shadowed"/>): the device's own is not read, and the two are one function of one line.
    /// </remarks>
    public void SettleTheDevice(CarFleet cars, ReadOnlySpan<int> taken)
    {
        foreach (var car in taken)
        {
            _passHeld[car] = Shadowed ? PassesAfter[car].ToOvertake() : cars.Pass[car];

            // What the shadow holds the next tick to; a tick that hands back only what the town reads holds nothing to it.
            if (!ReadsEveryCar) continue;

            _commandBefore[car] = CommandsAfter[car];
            _carBefore[car] = _tyres.CarsAfter[car];
            if (_tyres.ReadsEveryWheel)
            {
                _tyres.WheelsAfter.Slice(car * TyreModel.Wheels, TyreModel.Wheels).CopyTo(_wheelsBefore.AsSpan(car * TyreModel.Wheels));
            }
        }
    }

    void LayThePose(TownWorld town, int car) => _tyres.Poses[car] = DeviceTyres.PoseRow.Of(town.Cars, car);

    void LayTheClockOf(TownWorld town, int car)
    {
        var (sinceS, closestM) = town.DriveClocks.Of(car);
        LayTheClock(car, sinceS, closestM);
    }

    /// <param name="wheels">Whether the host's copy of its wheels is laid — never where the device holds them newer.</param>
    void LayTheTyres(TownWorld town, int car, bool wheels = true)
    {
        var cars = town.Cars;
        var table = _tyres.Table;
        var command = DeviceTyres.CommandRow.Of(cars.Command[car]);
        table.Commands.Rows(car)[0] = command;
        table.Commands.Mark(car);
        _commandBefore[car] = command;
        LayTheCarRow(town, car);
        if (wheels) LayTheWheels(cars, car);
    }

    void LayTheWheels(CarFleet cars, int car)
    {
        var table = _tyres.Table;
        var wheels = table.Wheels.Rows(car);
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var at = (car * TyreModel.Wheels) + wheel;
            wheels[wheel] = new DeviceTyres.WheelRow(
                cars.MarkFromM[at], cars.WheelSpinMps[at], cars.ScrubTravelM[at], cars.MarkIntensity[at], cars.Marking[at] ? 1u : 0u);
        }

        wheels.CopyTo(_wheelsBefore.AsSpan(car * TyreModel.Wheels));
        table.Wheels.Mark(car);
    }

    /// <summary>
    /// Which of a car's wheels stand on a way, as a claims rebuild on the device wrote them into its tyre row
    /// (<c>rebuild_agents_out.comp</c>), taken as sent.
    /// </summary>
    public void KnowTheWheelsOnAWay(int car, byte wheels) => _wheelsSent[car] = wheels;

    void LayTheCarRow(TownWorld town, int car)
    {
        var cars = town.Cars;
        var row = new DeviceTyres.CarRow(
            1f / town.PhysicsForInstruments.InverseInertiaOf(cars.Body[car]), cars.SlipThrottle[car],
            DeviceTyres.CarRow.FlagsOf(cars.Action[car] == CarAction.Hand, cars.DrivenSlipping[car], cars.WheelsOnAWay[car]));
        _tyres.Table.Cars.Rows(car)[0] = row;
        _tyres.Table.Cars.Mark(car);
        _carBefore[car] = row;
        _wheelsSent[car] = cars.WheelsOnAWay[car];
    }

    /// <summary>
    /// What a car's decisions tell its tick, and — where the device's own rebuild does not write them
    /// (<see cref="GrantedOnTheDevice"/>) — what the town's claims granted it, what cut it and its hold bits.
    /// </summary>
    void LayWhatItIsTold(TownWorld town, int car, bool granted, bool claimsLaid)
    {
        var cars = town.Cars;
        if (granted && !GrantedOnTheDevice && !_grantPending[car])
        {
            if (_grantLeft[car])
            {
                throw new InvalidOperationException(
                    $"Car {car} would have its grant laid off the town's copy, which a rebuild on the device left behind.");
            }

            LayWhatItWasGranted(town, car, claimsLaid);
        }

        _told.Lay(car, ToldOf(town, car));
    }

    /// <summary>What a car's decisions tell its tick, as the town has them now.</summary>
    static ToldRow ToldOf(TownWorld town, int car)
    {
        var cars = town.Cars;
        var hasAPlace = town.SceneOf(car, out var placeM, out var reachM);
        var told = (cars.BlueLight[car] ? ToldRow.OnACall : 0u) | (cars.LineIsReverse[car] ? ToldRow.LineIsReverse : 0u)
                   | (hasAPlace ? ToldRow.HasAPlace : 0u);
        var asideM = 0f;
        var fromRestM = 0f;
        var stepsOutAtM = 0f;
        if (cars.Action[car] == CarAction.Switch)
        {
            told |= ToldRow.Switching;
            asideM = town.Switching.AsideM(car);
            if (town.Switching.WaitsShortOfTheEnd(car, out fromRestM)) told |= ToldRow.WaitsShortOfTheEnd;
        }
        else if (cars.Action[car] == CarAction.Overtake)
        {
            told |= ToldRow.Overtaking;
            asideM = town.Overtaking.AsideOfItsPassM(car);
            stepsOutAtM = town.Overtaking.StepsOutAtM(car);
        }

        return new ToldRow(told, cars.PaceMps[car], town.OffTheLineAllowanceM(car), asideM, fromRestM, reachM, placeM, stepsOutAtM, 0f);
    }

    /// <summary>
    /// <b>What the town's claims granted a car</b>, laid where the town changed it: read off the town's claims whole —
    /// grant, cutter and hold bits — where the town laid them itself or the table holds no cutter of this car's yet;
    /// otherwise only the grant, from the town's own figures and the way the last laying cut its hold on, the cutter and
    /// bits being what that laying left on the device (<see cref="TakeTheGrantOf"/>).
    /// </summary>
    void LayWhatItWasGranted(TownWorld town, int car, bool claimsLaid)
    {
        if (claimsLaid || !_cutterOnTheDevice[car])
        {
            var row = GrantOf(town, car, out var cutter, out var bits);
            _grants.Lay(car, row);
            _cutters.Lay(car, cutter);
            _holdBits.Lay(car, bits);
            _cutOn[car] = row.CutOn;
            _cutterOnTheDevice[car] = true;
            return;
        }

        _grants.Lay(car, TheTownsGrant(town.Cars, car));
    }

    /// <summary>A car's grant off the town's own figures, cut on the way the last laying cut its hold on.</summary>
    GrantRow TheTownsGrant(CarFleet cars, int car) => new(
        cars.AuthorityM[car], cars.HorizonM[car], cars.ClaimFromM[car], cars.CommittedToM[car], cars.GrantMarginM[car],
        (uint)cars.GrantCutBy[car], _cutOn[car], 0f, Vector2.Zero);

    /// <summary>
    /// Whether walking a car would lay its grant again between layings (<see cref="LayWhatItIsTold"/>): the town's figures
    /// changed since, and laid would send other than the table holds — or would throw, its grant being the device's.
    /// </summary>
    bool WouldLayTheGrant(TownWorld town, int car)
    {
        var cars = town.Cars;
        if (GrantedOnTheDevice || _grantPending[car] || cars.AuthorityM[car] == _authorityAt[car]) return false;

        var row = _cutterOnTheDevice[car] ? TheTownsGrant(cars, car) : GrantOf(town, car, out _, out _);
        return _grantLeft[car] || row != _grants.LaidRow(car);
    }

    /// <summary>
    /// <b>What a claims rebuild on the device wrote of a car's grant</b> (<c>rebuild_grant.comp</c>), taken as what the
    /// table holds: the town's own reading of the same claims, which is the device's to the bit
    /// (<see cref="ResidentRows{T}.Assume"/>) — so a grant laid after it is sent only where the town changed it.
    /// </summary>
    public void AssumeTheGrantOf(TownWorld town, int car)
    {
        var row = GrantOf(town, car, out var cutter, out var bits);
        _grants.Assume(car, row);
        _cutters.Assume(car, cutter);
        _holdBits.Assume(car, bits);
        _cutOn[car] = row.CutOn;
        _cutterOnTheDevice[car] = true;
        _grantPending[car] = false;
        _grantLeft[car] = false;
    }

    /// <summary>
    /// <b>What a claims rebuild on the device wrote of a car's grant, as it read back</b>: the grant row taken as the
    /// table's, and the cutter and hold bits it wrote left to the device unknown — sent whole again only where the town
    /// lays the claims itself.
    /// </summary>
    public void TakeTheGrantOf(int car, in GrantRow written)
    {
        _grants.Assume(car, written);
        _cutters.Forget(car);
        _holdBits.Forget(car);
        _cutOn[car] = written.CutOn;
        _cutterOnTheDevice[car] = true;
        _grantPending[car] = false;
        _grantLeft[car] = false;
    }

    /// <summary>
    /// <b>What a claims rebuild on the device wrote of a car's grant, left there unread</b> — a car the town does not hold
    /// as the device left it (<see cref="TownHolds"/>), whose grant comes back with its reading when it is handed back
    /// (<see cref="OutRow.Grant"/>). The town's own figures of it are its laying's, which cleared its claim: taken as the
    /// last laid, and never sent — a change the town makes to them before the grant is back is a defect, and throws.
    /// </summary>
    public void LeaveTheGrantOnTheDevice(CarFleet cars, int car)
    {
        _grants.Forget(car);
        _cutters.Forget(car);
        _holdBits.Forget(car);
        _cutterOnTheDevice[car] = true;
        _grantPending[car] = false;
        _grantLeft[car] = true;
        _authorityAt[car] = cars.AuthorityM[car];
    }

    /// <summary>Which cars' grants a rebuild on the device left there unread (<see cref="LeaveTheGrantOnTheDevice"/>).</summary>
    bool[] _grantLeft = [];

    /// <summary>
    /// <b>These cars' grants left to what a claims rebuild on the device wrote</b>, until its answers are read back
    /// (<see cref="TakeTheGrantOf"/>): the town's own figures of them are its laying's, which cleared every claim it laid,
    /// and are not laid over the device's meanwhile (<see cref="DeviceRebuild.AnswersDue"/>).
    /// </summary>
    public void GrantsArePending(ReadOnlySpan<int> cars)
    {
        foreach (var car in cars) _grantPending[car] = true;
    }

    /// <summary>Which cars' grants a rebuild on the device wrote that are not yet read back (<see cref="GrantsArePending"/>).</summary>
    bool[] _grantPending = [];

    /// <summary>The way each car's hold was cut on, as the last laying of the claims left it.</summary>
    int[] _cutOn = [];

    /// <summary>Whether the table holds a cutter and hold bits for each car, laid by the town or by a rebuild on the device.</summary>
    bool[] _cutterOnTheDevice = [];

    /// <summary>A car's grant, what cut it and its hold bits as the town's claims have them.</summary>
    static GrantRow GrantOf(TownWorld town, int car, out DeviceClaims.ClaimRow cutter, out uint bits)
    {
        var cars = town.Cars;
        var claims = town.Occupancy;
        var hold = town.PlanHoldOf(car);
        var cut = LaneClaim.Nothing;
        var cutOn = -1;
        if (hold != LaneOccupancy.NoHold)
        {
            claims.HoldEndsAtM(hold, out _, out cut);
            cutOn = claims.HoldCutOn(hold);
        }

        cutter = cut.Found ? DeviceClaims.ClaimRow.Of(cut, cutOn, false, cut.FromM) : default;
        bits = (hold != LaneOccupancy.NoHold ? ToldRow.HasAHold : 0u) | (cut.Found ? ToldRow.Cut : 0u);
        return new GrantRow(
            cars.AuthorityM[car], cars.HorizonM[car], cars.ClaimFromM[car], cars.CommittedToM[car], cars.GrantMarginM[car],
            (uint)cars.GrantCutBy[car], cutOn, 0f, Vector2.Zero);
    }

    void LayThePass(CarFleet cars, int car)
    {
        _passes.Rows(car)[0] = PassRow.Of(cars.Pass[car]);
        _passes.Mark(car);
        _passHeld[car] = cars.Pass[car];
    }

    void LayTheReading(CarFleet cars, int car)
    {
        _ticks.Rows(car)[0] = new TickRow(
            cars.ProgressM[car], cars.CoveredSinceClaimM[car], cars.AlongMps[car], cars.OffLineM[car], cars.PlannedMps[car],
            (uint)cars.Hold[car], 0u);
        _ticks.Mark(car);
    }

    void LayTheBox(CarFleet cars, int car)
    {
        _boxes.Rows(car)[0] = new BoxRow(cars.ToTheBoxM[car], cars.LightAheadM[car], BoxFlagsOf(cars, car), 0u);
        _boxes.Mark(car);
    }

    void LayTheLine(CarFleet cars, int car)
    {
        var line = cars.Line[car];
        _lines.Rows(car)[0] = LineOf(cars, car);
        var arcs = cars.LineOf(car);
        var entries = cars.EntriesOf(car);
        var arcRows = _arcs.Rows(car);
        var entryRows = _entries.Rows(car);
        for (var arc = 0; arc < line.ArcCount; arc++)
        {
            arcRows[arc] = ArcRow.Of(arcs[arc]);
            entryRows[arc] = entries[arc];
        }

        var chain = _chain.Rows(car);
        var starts = _starts.Rows(car);
        var ends = _ends.Rows(car);
        var firstArcs = _firstArcs.Rows(car);
        var breaks = _breaks.Rows(car);
        for (var lane = 0; lane < line.LaneCount; lane++)
        {
            chain[lane] = cars.ChainOf(car)[lane];
            starts[lane] = cars.LaneStartsOf(car)[lane];
            ends[lane] = cars.LaneEndsOf(car)[lane];
            firstArcs[lane] = (uint)cars.LaneFirstArcsOf(car)[lane];
            breaks[lane] = cars.JoinBreaksOf(car)[lane] ? 1u : 0u;
        }

        _lines.Mark(car);
        _arcs.MarkRows(car, line.ArcCount);
        _entries.MarkRows(car, line.ArcCount);
        _chain.MarkRows(car, line.LaneCount);
        _starts.MarkRows(car, line.LaneCount);
        _ends.MarkRows(car, line.LaneCount);
        _firstArcs.MarkRows(car, line.LaneCount);
        _breaks.MarkRows(car, line.LaneCount);
    }

    static LineRow LineOf(CarFleet cars, int car) => new(
        (uint)cars.Line[car].ArcCount, (uint)cars.Line[car].LaneCount, cars.Line[car].LengthM,
        LineReader.EndsWhereTheLegDoes(cars, car) ? 1u : 0u);

    static uint BoxFlagsOf(CarFleet cars, int car) =>
        (cars.BoxIsOurs[car] ? BoxRow.Claimed : 0u) | (cars.TurningAtTheBox[car] ? BoxRow.Turning : 0u)
        | (cars.InsideTheBox[car] ? BoxRow.Inside : 0u) | (cars.CommittedToTheBox[car] ? BoxRow.Committed : 0u);

    /// <summary>
    /// <b>What the device's tick of a car did that the town's tick of it did not</b>, after both: whether it lost its line,
    /// where it is on its line and the line itself, what it was told and the junction ahead, then its drive and its tyres —
    /// or null where they agree. Every figure and every outcome is held to the bit (<see cref="Same"/>).
    /// </summary>
    /// <remarks>
    /// <b>The drive and the tyres are held to the host's own functions asked of what the device handed them</b>
    /// (<see cref="Driven"/>, <see cref="Delivered"/>), not to the town's answer. The device finds a car on its line
    /// a tenth of a millimetre from where the town does, and a pedal asked to close a speed in one tick is that tenth
    /// over a stop and a tick — so held to the town, a car braking to a stop is a hundredth of a m/s² off.
    /// </remarks>
    public string? Disagreement(TownWorld town, int car)
    {
        var cars = town.Cars;
        var tick = TicksAfter[car];
        var lost = (tick.Flags & TickRow.Lost) != 0;
        if (lost != (cars.Action[car] == CarAction.Rejoin)) return $"lost: town {cars.Action[car]}, device {lost}";

        // A switch stepped out onto the lane beside is the town's to lay a line down, and the device drives on along the
        // step until it is told; a step the town's claims kept this tick the device does not yet answer itself.
        var steppedOut = (tick.Flags & TickRow.SteppedOut) != 0;
        var handedOver = _actionDue[car] == CarAction.Switch && cars.Action[car] == CarAction.Follow;
        if (steppedOut || handedOver)
        {
            if (steppedOut != handedOver) return $"stepped out: town {handedOver}, device {steppedOut}";

            SteppedOut++;
            return null;
        }

        var made = (tick.Flags & TickRow.PassMade) != 0;
        if (made != (_actionDue[car] == CarAction.Overtake && cars.Action[car] == CarAction.Follow))
        {
            return $"pass made: town {cars.Action[car]}, device {made}";
        }

        var pass = PassesAfter[car];
        if (cars.Pass[car].Begun && pass.Begun == 0u)
        {
            StepsKeptByTheTown++;
            return null;
        }

        if (cars.Pass[car].Any && pass.Lane != Overtake.NoLane
            && (pass.Lane != cars.Pass[car].Lane || pass.OutM != cars.Pass[car].OutM || pass.BackM != cars.Pass[car].BackM
                || pass.ClearsM != cars.Pass[car].ClearsM))
        {
            return $"pass: town {cars.Pass[car]}, device {pass.ToOvertake()}";
        }

        var shifted = (tick.Flags & TickRow.Shifted) != 0;
        if (shifted != (cars.LineLaid[car] != _lineLaidDue[car]))
        {
            return $"lane end: town at {cars.ProgressM[car]} of {cars.Line[car].LaneCount} lanes, device shifted {shifted} to {tick.ProgressM}";
        }

        // A car that lost its line has left the action its grant was laid for, and the town's Enter dropped the grant; the
        // device's tick leaves that to the action's change.
        if (!Same(cars.ProgressM[car], tick.ProgressM) || (!lost && !Same(cars.CoveredSinceClaimM[car], tick.CoveredSinceClaimM))
            || !Same(cars.AlongMps[car], tick.AlongMps) || !Same(cars.OffLineM[car], tick.OffLineM))
        {
            return $"reading: town progress {cars.ProgressM[car]} covered {cars.CoveredSinceClaimM[car]} along {cars.AlongMps[car]} " +
                   $"off {cars.OffLineM[car]}; device {tick}";
        }

        var asks = (tick.Flags & TickRow.AsksTheRoute) != 0;
        if (asks ? !town.AsksTheRoute(car) : town.AsksTheRoute(car) && !_askedBefore[car]) return $"asks the route: device {asks}";

        if (LinesAfter[car] != LineOf(cars, car)) return $"line: town {LineOf(cars, car)}, device {LinesAfter[car]}";
        for (var lane = 0; lane < cars.Line[car].LaneCount; lane++)
        {
            if (ChainAfter(car)[lane] != cars.ChainOf(car)[lane] || StartsAfter(car)[lane] != cars.LaneStartsOf(car)[lane]
                || EndsAfter(car)[lane] != cars.LaneEndsOf(car)[lane])
            {
                return $"lane {lane} of the line";
            }
        }

        return Told(cars, car, ContextsAfter[car], BoxesAfter[car])
               ?? (lost ? Stopped(cars, car) : Driven(cars, car))
               ?? Delivered(town, car, CommandsAfter[car]);
    }

    static string? Told(CarFleet cars, int car, in ContextRow context, in BoxRow box)
    {
        var host = cars.Context[car];
        var same = Same(host.AuthorityM, context.AuthorityM)
                   && (uint)host.GrantCutBy == context.GrantCutBy && Same(host.PlaceStopM, context.PlaceStopM)
                   && host.MarginM == context.MarginM && host.WaitsToPass == (context.WaitsToPass != 0u)
                   && Same(host.HorizonM, context.HorizonM) && host.PassAsideM == context.PassAsideM && host.BackUpM == context.BackUpM
                   && host.Blocked == (context.Blocked != 0u) && Same(host.WaitAtM, context.WaitAtM);
        if (!same) return $"told: town {host}, device {context}";

        return Same(cars.ToTheBoxM[car], box.ToTheBoxM) && Same(cars.LightAheadM[car], box.LightAheadM) && BoxFlagsOf(cars, car) == box.Flags
            ? null
            : $"box: town {cars.ToTheBoxM[car]} light {cars.LightAheadM[car]} flags {BoxFlagsOf(cars, car)}, device {box}";
    }

    /// <summary>A car that lost its line stops as the town stopped it, to the bit.</summary>
    string? Stopped(CarFleet cars, int car)
    {
        var tick = TicksAfter[car];
        return tick.Hold == (uint)cars.Hold[car] && CommandsAfter[car] == DeviceTyres.CommandRow.Of(cars.Command[car])
            ? null
            : $"stopped: town {cars.Hold[car]} {cars.Command[car]}, device {(DrivingHold)tick.Hold} {CommandsAfter[car]}";
    }

    /// <summary>
    /// <b>The device's drive held to <see cref="CarFollower.DriveTheLine"/> asked of the device's own reading</b>: where
    /// it found the car on its line and what it told it, over the line the town's tick left, from the command the device
    /// held going in.
    /// </summary>
    string? Driven(CarFleet cars, int car)
    {
        var tick = TicksAfter[car];
        var told = ContextsAfter[car];
        var context = new DriveContext(
            told.AuthorityM, (HeadwayKind)told.GrantCutBy, told.PlaceStopM, told.MarginM, told.WaitsToPass != 0u, told.HorizonM,
            told.PassAsideM, told.BackUpM, told.Blocked != 0u, told.WaitAtM);
        var pose = PoseOf(car);
        var drive = CarFollower.DriveTheLine(
            _config, cars.BuildOf(car), pose, cars.LineOf(car), cars.EntriesOf(car), tick.ProgressM, cars.Line[car].LengthM, context,
            pose.Forward, tick.AlongMps, reverse: false, CommandOf(_commandBefore[car]), PassesAfter[car].ToOvertake(),
            laneless: false, cars.BlueLight[car], cars.PaceMps[car]);

        var host = DeviceTyres.CommandRow.Of(drive.Command);
        var command = CommandsAfter[car];
        var same = Same(host.SteerRad, command.SteerRad) && Same(host.ThrottleMps2, command.ThrottleMps2)
                   && Same(host.BrakeMps2, command.BrakeMps2) && host.HandbrakeAndReverse == command.HandbrakeAndReverse
                   && (uint)drive.Hold == tick.Hold && Same(drive.PlannedMps, tick.PlannedMps)
                   && drive.Hazard == ((tick.Flags & TickRow.Hazard) != 0);
        return same ? null : $"drive: host {drive}, device {command} {(DrivingHold)tick.Hold} planned {tick.PlannedMps}";
    }

    CarPose PoseOf(int car)
    {
        var pose = _tyres.Poses[car];
        return new CarPose(pose.PositionM, pose.HeadingRad, pose.VelocityMps, pose.YawRateRadPerS, pose.MassKg, pose.AccelerationMps2);
    }

    static DriveCommand CommandOf(in DeviceTyres.CommandRow row) => new(
        row.SteerRad, row.ThrottleMps2, row.BrakeMps2, (row.HandbrakeAndReverse & 1u) != 0, (row.HandbrakeAndReverse & 2u) != 0);

    /// <summary>
    /// <b>The device's tyre pass held to the host's asked of the device's own command</b>, off the tyres the device held
    /// going in — so a command apart is the drive's to report (<see cref="Driven"/>), and not every wheel's after it.
    /// </summary>
    string? Delivered(TownWorld town, int car, in DeviceTyres.CommandRow onDevice)
    {
        var cars = town.Cars;
        var four = car * TyreModel.Wheels;
        var laid = _carBefore[car];
        var slipThrottle = laid.SlipThrottle;
        var slipping = (laid.Flags & DeviceTyres.CarRow.DrivenSlipping) != 0;
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var carried = _wheelsBefore[four + wheel];
            _spinMps[wheel] = Flushed(carried.SpinMps);
            _scrubTravelM[wheel] = Flushed(carried.ScrubTravelM);
            _marking[wheel] = carried.Marking != 0;
            _markFromM[wheel] = carried.MarkFromM;
            _markIntensity[wheel] = carried.MarkIntensity;
        }

        var after = _tyres.CarsAfter[car];
        var pose = PoseOf(car);
        var atRest = TyrePass.Step(
            _config, town.WheelGrounds, cars.BuildOf(car),
            pose with
            {
                VelocityMps = new Vector2(Flushed(pose.VelocityMps.X), Flushed(pose.VelocityMps.Y)),
                YawRateRadPerS = Flushed(pose.YawRateRadPerS),
            },
            CommandOf(onDevice), false, laid.InertiaKgM2, cars.WheelsOnAWay[car],
            new TyreCarry(_spinMps, _scrubTravelM, _marking, _markFromM, _markIntensity, ref slipThrottle, ref slipping), _surfaces,
            _impulses, _scrub, _marks, out _);
        if (atRest != ((after.Flags & DeviceTyres.CarRow.AtRest) != 0)) return $"at rest: host {atRest}, device {after}";
        if (!Same(slipThrottle, after.SlipThrottle)) return $"throttle: host {slipThrottle}, device {after}";

        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var carried = _tyres.WheelsAfter[four + wheel];
            var spent = _tyres.Impulses[four + wheel];
            var same = _scrub[wheel].Sliding == ((_tyres.Scrub[four + wheel].Flags & DeviceTyres.ScrubRow.Sliding) != 0)
                       && Same(_spinMps[wheel], carried.SpinMps) && Same(_scrubTravelM[wheel], carried.ScrubTravelM)
                       && _marking[wheel] == (carried.Marking != 0) && Same(_markIntensity[wheel], carried.MarkIntensity)
                       && Same(_markFromM[wheel].X, carried.MarkFromM.X) && Same(_markFromM[wheel].Y, carried.MarkFromM.Y)
                       && Same(_impulses[wheel].ImpulseNs.X, spent.X) && Same(_impulses[wheel].ImpulseNs.Y, spent.Y)
                       && Same(_impulses[wheel].AtM.X, spent.Z) && Same(_impulses[wheel].AtM.Y, spent.W);
            if (!same) return $"wheel {wheel}: host spin {_spinMps[wheel]} {_impulses[wheel]} {_scrub[wheel]}, device {carried} {spent}";
        }

        return slipping == ((after.Flags & DeviceTyres.CarRow.DrivenSlipping) != 0) ? null : $"slipping: host {slipping}, device {after}";
    }

    /// <summary>
    /// The host's figure to the bit, as the device keeps it: one the host leaves subnormal the device flushes to nought, as
    /// every kernel does.
    /// </summary>
    static bool Same(float host, float onDevice) => Flushed(host) == onDevice;

    /// <summary>
    /// A figure as a kernel reads it: <b>below the least normal float it is nought</b>, since a kernel's float flushes
    /// denormals unless it asks to keep them. A body coming to rest decays through them for a score of ticks — a velocity
    /// of 1e-38 m/s the host does not call rest and the device does.
    /// </summary>
    static float Flushed(float figure) => float.IsSubnormal(figure) ? 0f : figure;
}
