using System.Diagnostics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The town's agents ticked on the device, in step</b> (<see cref="ITownDevice"/>): every car the car tick takes
/// (<see cref="DeviceCarTick.Takes"/>) and every walker but one under a hand ticked there with every car's tyres, in one
/// submit, and what they left written back into the town before any decision reads it — the tick's consequences taken
/// with the town's own code (<c>TownWorld.ShiftedOnTheDevice</c> and the rest).
/// </summary>
/// <remarks>
/// <para>
/// <b>The claims are laid here too</b> (<see cref="DeviceRebuild.InTheTown"/>): the town lays only what it alone lays, the
/// device every body, light, plan, walk and answer in one submit, and the claims are written back into the town before
/// it lays the backing and reads every grant; the backing and the cars' grants are recorded at the head of the tick's own
/// submit, into the tables the car tick reads. The walkers' side of them is the device's own, read in off the walker tick's
/// table (<see cref="DeviceWalkers.Keeps"/>). <b>Where the town has to lay them itself</b> — a hand or a truck holding
/// what it cannot stop short of, a manoeuvre begun at a bay, a tick held, more cars on a pass than there is room to ask
/// for — they are sent as cut (<see cref="DeviceClaims.LayAsCut"/>), as each was before, and counted.
/// </para>
/// <para>
/// <b>The bodies are the device's</b> (<see cref="DeviceTownBodies"/>): stepped off the impulses the tick left on the
/// device, the town's own written over them for what the town ticked itself, in a submit behind the tick's own that runs
/// while the host writes the cars and walkers back (<see cref="StepsWithTheTick"/>). The poses the step leaves in the tyre pass's
/// table are what the next tick reads, the forward a body's own; the town lays only those of the cars whose bodies it
/// changed since (<see cref="DeviceTownBodies.PosesLeftInTheTable"/>).
/// </para>
/// <para>
/// <b>What the town's tick of a car it keeps does to its tyres stays the town's</b>: the device runs every car's tyres,
/// and for a car the town ticked itself the town's own answer stands and the device's is left unread.
/// </para>
/// <para>
/// <b>The town decides a tick behind</b> (<see cref="DecidesATickBehind"/>): the decisions due on a tick are taken while the
/// next tick's submit runs, and that tick's answer leaves the town's own where a decision changed it meanwhile.
/// </para>
/// <para>
/// <b>A car's wheels stay on the device while the device ticks it</b> (<see cref="DeviceTyres.KeptOnTheDevice"/>): its
/// impulses are spent where the pass left them and its wheels' carried state is handed back only when it leaves the
/// device's tick — in the bodies' submit for a car the tick's decisions took off it, and in a submit of their own, before
/// any agent is ticked, for one that left after (a wreck, a hand, a held town). Nothing on the host reads a kept car's
/// wheels but its own tyre pass and a car laid whole.
/// </para>
/// </remarks>
internal sealed class DeviceTown : ITownDevice, ITownClaims, IDisposable
{
    readonly Vk _vk;
    readonly TownWorld _town;
    readonly DeviceTyres _tyres;
    readonly DeviceClaims _claims;
    readonly DeviceCarTick _cars;
    readonly DeviceWalkerTick _walkers;
    readonly DeviceTownBodies _bodies;
    readonly DeviceTownClaims _townClaims;
    readonly ComputeBatch _batch;

    /// <summary>The bodies' step, submitted behind the tick's own (<see cref="StepsWithTheTick"/>).</summary>
    readonly ComputeBatch _stepBatch;

    readonly GpuTimer _timer;
    readonly float _dtS;
    readonly int[] _taken;
    readonly bool[] _takenNow;

    /// <summary>Which cars the town's tick left to the device this tick, as <see cref="Takes"/> answered it.</summary>
    readonly bool[] _chosen;

    /// <summary>The cars whose wheels are being handed back, in the order they are copied.</summary>
    readonly int[] _leaving;

    /// <summary>Which cars the last tick took that the town moved on from while it ran, so what it read of them is not taken back.</summary>
    readonly bool[] _movedOn;

    /// <summary>Which cars the last tick took that it handed back (<see cref="ReadsEveryCarBack"/>): every one, or those the town reads.</summary>
    readonly bool[] _handedBack;

    int _count;
    int _walkerCount;

    /// <summary>Whether this tick's agents were ticked here, so the bodies' phase spends what their tick left.</summary>
    bool _ticked;

    /// <summary>Whether this tick's agents have been looked over for a car leaving the device with its wheels still there.</summary>
    bool _swept;

    public DeviceTown(Vk vk, SimConfig config, TownWorld town)
    {
        _vk = vk;
        _town = town;
        var cars = town.Cars;
        var claims = town.Occupancy;
        var builds = new CarBuild[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = cars.BuildOfVariant(variant);

        _tyres = new DeviceTyres(vk, config, town.WheelGrounds, builds, cars.Capacity) { ReadsEveryWheel = false };
        _claims = new DeviceClaims(vk, claims.WayCount, claims.Capacity, claims.HoldCapacity, config.Road.MostSettlingPasses, cars.Capacity);
        _cars = new DeviceCarTick(
            vk, config, _tyres, town.Roads, cars.LineArcsOf(0).Length, town.Ways.OfRoadConnector(0), _claims.Cut)
        {
            Shadowed = false,
        };
        _walkers = new DeviceWalkerTick(vk, config, town.People.Capacity, resident: true);
        _bodies = new DeviceTownBodies(vk, config, town, _tyres, _walkers) { HandsOverThrough = _cars };
        _townClaims = new DeviceTownClaims(vk, config, town, _claims, _cars, _walkers);
        _townClaims.Rebuild.MirrorsAtOnce = false;
        RebuildsRunOn = true;
        _batch = new ComputeBatch(vk);
        _stepBatch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, 3);
        _dtS = config.TickSeconds;
        _taken = new int[cars.Capacity];
        _takenNow = new bool[cars.Capacity];
        _chosen = new bool[cars.Capacity];
        _leaving = new int[cars.Capacity];
        _movedOn = new bool[cars.Capacity];
        _handedBack = new bool[cars.Capacity];
        _wanted = new uint[_cars.Wanted.Length];
        _cars.ReadsEveryCar = false;
        _listed = new bool[cars.Capacity];
        _decidedByTheTown = new bool[cars.Capacity];
        _byTheTown = new bool[cars.Capacity];
        _decisionIntervalS = new TickGroups(config.Sim.AgentDecisionIntervalS, config.Sim.TickRateHz).IntervalS;
        _decided = new int[cars.Capacity];
        _shapeOf = new uint[cars.Capacity];
        _shapeLaidAt = new (uint, uint)[cars.Capacity];
        _shapeKnown = new bool[cars.Capacity];
        town.DriveClocks.KeepTheTouched();
        town.CarsDecidedOnTheDevice = true;
    }

    /// <summary>
    /// <b>Whether the device decides the cars whose decisions put off a tick are their clock alone</b>
    /// (<see cref="TownWorld.TakesOnlyTheClock"/>, <c>car_decide.glsl</c>): asked at the head of the next tick, of what the
    /// last left, the clock run there, and only a decision that is more handed back and taken by the town — so a car's
    /// tick is not handed back for its decision. Off, the town decides every car with what every tick handed back.
    /// </summary>
    public bool DecidesTheCars
    {
        get => _town.CarsDecidedOnTheDevice;
        init => _town.CarsDecidedOnTheDevice = value;
    }

    /// <summary>Which cars' decisions this tick asks of the device, and the list of them laid.</summary>
    readonly bool[] _listed;

    /// <summary>Which cars the device ticks this tick whose decisions put off the town takes with what it holds.</summary>
    readonly bool[] _decidedByTheTown;

    readonly int[] _decided;
    int _decidedCount;

    /// <summary>
    /// What of each car's wish to move across its road and route alone answer (<see cref="Switching.ShapeOfTheRoad"/>), and
    /// the line and route it was read off (<see cref="CarFleet.LineLaid"/>, <see cref="CarFleet.RouteLaid"/>).
    /// </summary>
    readonly uint[] _shapeOf;

    readonly (uint Line, uint Route)[] _shapeLaidAt;
    readonly bool[] _shapeKnown;

    /// <summary>How many cars' decisions the device found were more than their clock, or the town took whole, over every tick.</summary>
    public long DecidedByTheTown { get; private set; }

    /// <summary>
    /// <b>The decisions put off last tick that this tick asks of the device</b>: of every car the device holds as its last
    /// tick left it, what only the town knows laid — the town deciding it whole where the car is not one whose decision is
    /// read off its line, or where the claims the town's readers read are not yet the device's (a rebuild laid this tick
    /// whose layer lands later).
    /// </summary>
    void LayTheDecisions(TownWorld town, bool readersBehind)
    {
        var cars = town.Cars;
        var firstCar = town.Roster.AgentOfCar(0);
        var count = 0;
        foreach (var agent in town.DecisionsPutOff)
        {
            if (agent < firstCar) continue;

            var car = town.Roster.CarIndex(agent);
            if (!_takenNow[car] || !_tyres.KeptOnTheDevice[car] || _cars.TownHolds(car))
            {
                // The town decides it with what it holds, and may move on from it: handed back as this tick leaves it.
                if (_cars.Takes(car)) _decidedByTheTown[car] = true;
                continue;
            }

            var byTheTown = readersBehind || !town.LeavesItsDecisionToItsLine(car);
            _byTheTown[car] = byTheTown;
            var (patienceS, byM, queueReachM) = town.ClockTermsOf(car);
            var flags = (byTheTown ? DeviceCarTick.DecideTermsRow.ByTheTown : ShapeOfTheRoad(town, car))
                        | (ChecksTheDecisions ? DeviceCarTick.DecideTermsRow.Checked : 0u);
            _cars.LayTheDecideTerms(car, new DeviceCarTick.DecideTermsRow(flags, patienceS, byM, queueReachM));
            _listed[car] = true;
            _decided[count++] = car;
        }

        _decidedCount = count;
        _cars.LayTheDecisions(_decided.AsSpan(0, count));
    }

    /// <summary>Which cars' decisions this tick the town takes whole whatever the device finds.</summary>
    readonly bool[] _byTheTown;

    /// <summary>
    /// <b>Whether to hold every verdict the device reaches to the town's own</b> (<see cref="TownWorld.TakesOnlyTheClock"/>,
    /// <see cref="DecisionsUnlike"/>) — a check of the kernel, at the cost of what every decision asked of it was read off
    /// handed back.
    /// </summary>
    public bool ChecksTheDecisions { get; set; }

    /// <summary>How many decisions the device found its clock alone where the town finds more, or the other way, over every check.</summary>
    public long DecisionsUnlike { get; private set; }

    /// <summary>How many of the device's verdicts were held to the town's own (<see cref="ChecksTheDecisions"/>).</summary>
    public long DecisionsChecked { get; private set; }

    /// <summary>The first decisions found unlike, for the report.</summary>
    public List<string> DecisionsUnlikeSeen { get; } = [];

    /// <summary>
    /// A verdict held to the town's own, the car written into the town as the device read it — the town a decision put off
    /// reads, a tick behind — and its clock as it stood before the device ran it.
    /// </summary>
    void CheckTheVerdict(int car, bool more)
    {
        var cars = _town.Cars;
        ref readonly var row = ref _cars.DecidedOn(car);
        WriteTheTyres(cars, car, row.Car);
        WriteTheTick(cars, car, row);
        _town.DriveClocks.Take(car, row.ClockSinceS, row.ClockClosestM);
        DecisionsChecked++;
        if (_town.TakesOnlyTheClock(car, _decisionIntervalS) != more) return;

        DecisionsUnlike++;
        if (DecisionsUnlikeSeen.Count < 8) DecisionsUnlikeSeen.Add($"car {car}: the device found {(more ? "more" : "its clock alone")}");
    }

    /// <summary>What a decision answers for, as the loop reckons it.</summary>
    readonly float _decisionIntervalS;

    /// <summary>What of a car's wish to move across its road and route alone answer, read again where its line or route changed.</summary>
    uint ShapeOfTheRoad(TownWorld town, int car)
    {
        var laidAt = (town.Cars.LineLaid[car], town.Cars.RouteLaid[car]);
        if (_shapeKnown[car] && _shapeLaidAt[car] == laidAt) return _shapeOf[car];

        _shapeOf[car] = (uint)town.Switching.ShapeOfTheRoad(car, out _);
        _shapeLaidAt[car] = laidAt;
        _shapeKnown[car] = true;
        return _shapeOf[car];
    }

    public bool DecidedOnTheDevice(int car)
    {
        if (!_listed[car]) return false;

        _listed[car] = false;
        var more = _cars.DecidedMoreThanTheClock(car);
        if (ChecksTheDecisions && !_byTheTown[car]) CheckTheVerdict(car, more);
        if (!more) return true;

        // More than the clock: the town decides it on what its last tick left, as the device read it.
        var cars = _town.Cars;
        ref readonly var row = ref _cars.DecidedOn(car);
        WriteTheTyres(cars, car, row.Car);
        WriteTheTick(cars, car, row);
        _town.DriveClocks.Take(car, row.ClockSinceS, row.ClockClosestM);
        _cars.TookBack(car);

        // And this tick handed it back as well (car_out.comp), which a decision that moved it on lays it whole from.
        _handedBack[car] = true;
        DecidedByTheTown++;
        return false;
    }

    /// <summary>
    /// <b>Whether every car the device ticked is handed back with every tick</b>, or only those the town reads before the
    /// device ticks them again (<see cref="TownWorld.ReadsTheTickOf"/>) and those whose tick asked something of it — every
    /// other car's reading, command and tyres kept on the device until it leaves the device's tick or the town reads every
    /// car (<see cref="TakeEveryCarBack"/>).
    /// </summary>
    public bool ReadsEveryCarBack
    {
        get => _cars.ReadsEveryCar;
        init => _cars.ReadsEveryCar = value;
    }

    /// <summary>How many cars the device ticked last tick.</summary>
    public int CarsTaken => _count;

    /// <summary>The device's own time for the last tick's kernels, the bodies' phase's among them, in milliseconds.</summary>
    public double KernelsMs => _cars.TickMs + _cars.TyresMs + _walkers.TickMs + _bodies.Phase.KernelsMs;

    /// <summary>The bodies' phase, for whoever reads its times.</summary>
    public DeviceTownBodies Bodies => _bodies;

    /// <summary>The walkers' tick, for whoever checks what it reads.</summary>
    public DeviceWalkerTick Walkers => _walkers;

    /// <summary>The device's time taking the last tick's car rows in and handing them back, in milliseconds.</summary>
    public double CopiesMs => _cars.SentMs + _cars.ReadMs;

    /// <summary>The device's time from the first command of the last tick's submit to its last, in milliseconds.</summary>
    public double SubmitMs => _timer.BetweenMs(0, 2);

    /// <summary>Of it, the device's time taking the town's claims in and listing them, in milliseconds.</summary>
    public double ClaimsMs => _timer.BetweenMs(0, 1);

    /// <summary>How many bytes of car rows the last tick sent.</summary>
    public ulong SentBytes { get; private set; }

    /// <summary>How many copy regions the last tick's own submit recorded (<see cref="Vk.RegionsCopied"/>).</summary>
    public int RegionsCopied { get; private set; }

    /// <summary>How many walkers the last tick wrote whole over the device's table, the town holding them otherwise.</summary>
    public int WalkersPatched => _walkers.Patched;

    /// <summary>How many cars' wheels went back to the host in the bodies' submit, and how many in a submit of their own.</summary>
    public (long InTheStep, long Alone) HandedOver { get; private set; }

    /// <summary>How many submits of their own handed wheels back.</summary>
    public long HandOverSubmits { get; private set; }

    /// <summary>How many driven cars the town ticked itself, over every tick.</summary>
    public long CarsDrivenByTheTown { get; private set; }

    public bool Takes(int agent)
    {
        var town = _town;
        if (!_swept) HandBackTheLeavers(town);

        if (town.HoldAgents) return false;
        if (!town.Roster.IsCar(agent)) return !town.WalksUnderAHand(agent);

        var car = town.Roster.CarIndex(agent);
        var takes = _chosen[car] = DeviceCarTick.Takes(town, car);

        // A switching car's step and an overtaking car's pass, asked for and laid since, are kept or withdrawn by the car's
        // own tick before it drives, which the device's does not.
        if (takes && town.Cars.Action[car] == CarAction.Switch) town.Switching.KeepOrWithdrawTheStep(car);
        if (takes && town.Cars.Action[car] == CarAction.Overtake) town.Overtaking.KeepOrWithdrawThePassOf(car);

        if (!takes && town.Cars.Driven[car]) CarsDrivenByTheTown++;
        if (!takes && _tyres.KeptOnTheDevice[car])
        {
            // It left during the agents' pass, after the sweep before it.
            _leaving[0] = car;
            HandBackAlone(town, _leaving.AsSpan(0, 1));
        }

        return takes;
    }

    /// <summary>Whether a car whose wheels the device keeps goes on being ticked there next, as the town stands now.</summary>
    bool StaysOnTheDevice(TownWorld town, int car) =>
        !town.HoldAgents && !town.IsTerminal(town.Roster.AgentOfCar(car)) && DeviceCarTick.Takes(town, car);

    /// <summary>
    /// <b>Before any agent is ticked, every car leaving the device with its wheels still there has them handed back</b> —
    /// what changed after the bodies' submit took the tick's leavers (<see cref="StepBodies"/>).
    /// </summary>
    void HandBackTheLeavers(TownWorld town)
    {
        _swept = true;
        HandBackAlone(town, Leaving(town, everyKept: false));
    }

    /// <summary>These cars' wheels handed back in one submit of their own; none, and nothing is submitted.</summary>
    void HandBackAlone(TownWorld town, ReadOnlySpan<int> leaving)
    {
        if (leaving.IsEmpty) return;

        var commands = _batch.Begin();
        _vk.BeforeTransfer(commands);
        _cars.RecordTheHandOver(commands, leaving);
        _batch.SubmitAndWait();
        TakeTheWheelsBack(town.Cars, leaving);
        HandedOver = (HandedOver.InTheStep, HandedOver.Alone + leaving.Length);
        HandOverSubmits++;
    }

    /// <summary>The cars whose wheels the device keeps and that will not be ticked there next — or, asked so, every one.</summary>
    Span<int> Leaving(TownWorld town, bool everyKept)
    {
        var kept = _tyres.KeptOnTheDevice;
        var count = 0;
        for (var car = 0; car < town.Cars.Count; car++)
        {
            if (kept[car] && (everyKept || !StaysOnTheDevice(town, car))) _leaving[count++] = car;
        }

        return _leaving.AsSpan(0, count);
    }

    /// <summary>
    /// Each car's wheels as the hand-over copied them back, written into the fleet; the device keeps them no longer — and
    /// a car its last tick did not hand back is taken back whole (<see cref="TakeBackOnTheSide"/>).
    /// </summary>
    void TakeTheWheelsBack(CarFleet cars, ReadOnlySpan<int> leaving)
    {
        var wheels = _tyres.WheelsAfter;
        foreach (var car in leaving)
        {
            TakeBackOnTheSide(cars, car);
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                var at = (car * TyreModel.Wheels) + wheel;
                ref readonly var carried = ref wheels[at];
                cars.WheelSpinMps[at] = carried.SpinMps;
                cars.ScrubTravelM[at] = carried.ScrubTravelM;
                cars.Marking[at] = carried.Marking != 0u;
                cars.MarkFromM[at] = carried.MarkFromM;
                cars.MarkIntensity[at] = carried.MarkIntensity;
            }

            _tyres.KeptOnTheDevice[car] = false;
        }
    }

    /// <summary>
    /// <b>A car its last tick left on the device, taken back as a hand-over copied it</b> (<see cref="DeviceCarTick.HandedOverRow"/>):
    /// its tyres, and its reading but where the town moved on from it since, whose own stands — as a tick's take-back leaves
    /// a car it hands back. Nothing for a car the town already holds as the device left it.
    /// </summary>
    void TakeBackOnTheSide(CarFleet cars, int car)
    {
        if (_cars.TownHolds(car)) return;

        var row = _cars.HandedOverRow(car);
        WriteTheTyres(cars, car, row.Car);
        TakeTheClock(_town, car, row);
        if (!_cars.TheTownMovedOnFrom(cars, car)) WriteTheTick(cars, car, row);
        _cars.TookBack(car);
    }

    /// <summary>
    /// A car's leg's clock as the device ran it (<see cref="DecidesTheCars"/>), but where the town changed it since — a
    /// decision of its own taken, a restart — whose clock is laid on the device next.
    /// </summary>
    void TakeTheClock(TownWorld town, int car, in DeviceCarTick.OutRow row)
    {
        if (DecidesTheCars && !town.DriveClocks.IsTouched(car)) town.DriveClocks.Take(car, row.ClockSinceS, row.ClockClosestM);
    }

    public void TakeEveryCarBack(TownWorld town)
    {
        var cars = town.Cars;
        var count = 0;
        for (var car = 0; car < cars.Count; car++)
        {
            if (!_cars.TownHolds(car)) _leaving[count++] = car;
        }

        if (count == 0) return;

        var left = _leaving.AsSpan(0, count);
        var commands = _batch.Begin();
        _vk.BeforeTransfer(commands);
        _cars.RecordTheHandOver(commands, left);
        _batch.SubmitAndWait();
        foreach (var car in left) TakeBackOnTheSide(cars, car);
        TakenBackWhole++;
    }

    /// <summary>How many cars a tick ticked and left on the device, the town not reading them (<see cref="ReadsEveryCarBack"/>), over every tick.</summary>
    public long CarsLeftOnTheDevice { get; private set; }

    /// <summary>How many times the town read every car, which every car the device kept was handed back for (<see cref="TakeEveryCarBack"/>).</summary>
    public long TakenBackWhole { get; private set; }

    /// <summary>The host's time writing what the last tick sent, in milliseconds.</summary>
    public double PrepareMs { get; private set; }

    /// <summary>The host's time recording, submitting and waiting for the last tick, in milliseconds.</summary>
    public double WaitMs { get; private set; }

    /// <summary>The host's time writing what the last tick left back into the town, in milliseconds.</summary>
    public double TakeBackMs { get; private set; }

    /// <summary>Of the wait, the host's time taking the decisions the tick before put off (<see cref="DecidesATickBehind"/>), in milliseconds.</summary>
    public double DecidedMs { get; private set; }

    /// <summary>Of the wait, the host's time with nothing left to do but wait for the device, in milliseconds.</summary>
    public double IdleMs { get; private set; }

    /// <summary>
    /// Of the writing back, the host's time waiting for the bodies' step once the cars and walkers were written back, in
    /// milliseconds (<see cref="StepsWithTheTick"/>).
    /// </summary>
    public double StepIdleMs { get; private set; }

    /// <summary>
    /// <b>Whether the bodies are stepped with the agents' tick</b> (<see cref="ITownDevice.StepsWithTheTick"/>): submitted
    /// behind it with no wait between, so the host writes the cars and walkers back while the step runs — the decisions
    /// reading the town the step left and what one does to a body stepped a tick later. Off, the bodies are phase 4's own
    /// submit, after the decisions.
    /// </summary>
    public bool StepsWithTheTick { get; init; } = true;

    /// <summary>
    /// <b>Whether the town decides a tick behind the device</b> (<see cref="ITownDevice.DecidesATickBehind"/>; owner, root
    /// decision log 2026-10-08): the last tick's decisions are taken while this tick's submit runs, so the host's time on
    /// them is the device's time on the tick.
    /// </summary>
    public bool DecidesATickBehind { get; init; } = true;

    /// <summary>
    /// <b>Whether a claims rebuild laid here runs on while the town goes on</b> (<see cref="DeviceRebuild.AnswersDue"/>): its
    /// answers written back once the tick's own submit is behind it, before the decisions read them. Off, every rebuild is
    /// waited for where its stages reach the plans.
    /// </summary>
    public bool RebuildsRunOn
    {
        get => _townClaims.Rebuild.AnswersAfterTheTick;
        init => _townClaims.Rebuild.AnswersAfterTheTick = value;
    }

    /// <summary>How many cars a tick handed back left to the town, which had changed their line or what they do since they were sent.</summary>
    public long CarsTheTownMovedOn { get; private set; }

    public void TickTheRest(TownWorld town)
    {
        if (town.HoldAgents)
        {
            _townClaims.TakeTheAnswers(town);
            town.TakeTheDecisionsPutOff();

            // Nothing is ticked here, and the bodies step on the town's own impulses.
            if (!StepsWithTheTick) return;

            _bodies.StepBodies(town, _dtS, default, walkersTicked: false);
            KnowTheStep();
            _townClaims.Rebuild.LandWhatWasSent(town);
            _townClaims.Rebuild.SendTheLayerBack();
            _swept = false;
            return;
        }

        var began = Stopwatch.GetTimestamp();

        // A rebuild running on (DeviceRebuild.AnswersDue) copied what it sent out of the tables laid here.
        _townClaims.Rebuild.WaitForTheSends();
        var laidHere = _townClaims.Pending;
        var laidByTheTown = _townClaims.TakeLaidByTheTown() | (town.ClaimsLaidThisTick && !laidHere);
        if (laidByTheTown) _claims.LayAsCut(town.Occupancy);
        var chosen = _chosen.AsSpan(0, town.Cars.Count);
        var stepping = StepsWithTheTick;

        // Stepped in this submit, the step lays every pose the town changed itself, before the tick's rows are sent.
        var posesInTheTable = stepping || _bodies.PosesLeftInTheTable(town);
        if (posesInTheTable && !stepping) _bodies.LayThePosesTheTownChanged(town);
        _count = _cars.Prepare(town, _taken, resident: true, laidByTheTown, chosen, posesInTheTable);
        chosen.Clear();

        // A rebuild laid this tick whose layer lands later leaves the town's readers on the claims before it.
        if (DecidesTheCars) LayTheDecisions(town, laidHere && _townClaims.Rebuild.LandsLater);
        if (!_cars.ReadsEveryCar) LayTheWanted(town);
        town.DriveClocks.ForgetTheTouched();
        _walkerCount = _walkers.LayWhatChanged(town);
        if (stepping) _bodies.LayTheStep(town, _dtS, _taken.AsSpan(0, _count), walkersTicked: true);
        var prepared = Stopwatch.GetTimestamp();

        var regionsFrom = Vk.RegionsCopied;
        var commands = _batch.Begin();
        _timer.Reset(commands);
        _timer.Stamp(commands, 0);
        if (laidHere) _townClaims.RecordTheRest(commands);
        if (laidByTheTown) _claims.RecordAsCut(commands);
        _timer.Stamp(commands, 1);
        _cars.Record(commands, _count, town.Cars.Count);
        SentBytes = _cars.SentBytes;
        _cars.RecordRead(commands);
        _walkers.RecordWhatWasLaid(commands, _walkerCount);
        if (laidHere)
        {
            _townClaims.Rebuild.RecordTheKeptAims(commands);
            _vk.BetweenKernels(commands);
        }

        _walkers.RecordTheTick(commands);
        _walkers.RecordRead(commands);
        _timer.Stamp(commands, 2);
        _batch.Submit();

        // The step in a submit of its own behind the tick's, so the cars and walkers are written back while it runs.
        if (stepping)
        {
            var step = _stepBatch.Begin();
            _vk.AfterTheSubmitsBefore(step);
            _bodies.RecordTheStep(step, town, _dtS);
            _stepBatch.Submit();
        }

        RegionsCopied = (int)(Vk.RegionsCopied - regionsFrom);

        // While the tick runs: this tick's rebuild's answers are written back where it ran on meanwhile, a layer the last
        // rebuild sent back is written into the town, and this tick's own rebuild's layer is sent back behind it, to be
        // copied in the host's time before the next tick's submit; and the last tick's decisions are taken, on the layer
        // just landed.
        _townClaims.TakeTheAnswers(town);
        _townClaims.Rebuild.LandWhatWasSent(town);
        _townClaims.Rebuild.SendTheLayerBack();
        var deciding = Stopwatch.GetTimestamp();
        var physics = town.PhysicsForTheDevice;
        var placedFrom = physics.Changes.Length;
        town.TakeTheWalkersDecisionsPutOff();
        var idle = Stopwatch.GetTimestamp();
        DecidedMs = Stopwatch.GetElapsedTime(deciding, idle).TotalMilliseconds;
        _batch.Wait();
        IdleMs = Stopwatch.GetElapsedTime(idle).TotalMilliseconds;
        _timer.Read();
        _cars.ReadTimes();
        _walkers.ReadTimes();

        // The cars' decisions once the tick is in, before what it left of them is written back: the town a tick old
        // either way, and a car a decision moved on from read as the tick had it.
        var carsDeciding = Stopwatch.GetTimestamp();
        town.TakeTheDecisionsPutOff();
        foreach (var car in _decided.AsSpan(0, _decidedCount))
        {
            if (_listed[car]) throw new InvalidOperationException($"Car {car}'s decision was asked of the device and never taken.");
        }

        _decidedCount = 0;
        var placedTo = physics.Changes.Length;
        var waited = Stopwatch.GetTimestamp();
        DecidedMs += Stopwatch.GetElapsedTime(carsDeciding, waited).TotalMilliseconds;

        TakeBackTheCars(town);
        TakeBackTheWalkers(town);
        if (stepping)
        {
            var steppedFrom = Stopwatch.GetTimestamp();
            _stepBatch.Wait();
            StepIdleMs = Stopwatch.GetElapsedTime(steppedFrom).TotalMilliseconds;
            _bodies.TakeTheStepBack(town, physics.Changes[placedFrom..placedTo]);
            KnowTheStep();
            _swept = false;
        }

        _ticked = !stepping;
        PrepareMs = Stopwatch.GetElapsedTime(began, prepared).TotalMilliseconds;
        WaitMs = Stopwatch.GetElapsedTime(prepared, waited).TotalMilliseconds;
        TakeBackMs = Stopwatch.GetElapsedTime(waited).TotalMilliseconds;
    }

    /// <summary>The claims laid here, for whoever reads how many and what each cost.</summary>
    public DeviceTownClaims Claims => _townClaims;

    public bool LaysTheClaims(TownWorld town) => _townClaims.LaysTheClaims(town);

    public void ClaimsAt(TownWorld town, ClaimsStage stage) => _townClaims.ClaimsAt(town, stage);

    public bool ReadTheGrants => _townClaims.ReadTheGrants;

    public bool KeepsTheWalkers => _townClaims.KeepsTheWalkers;

    public bool LandsLater => _townClaims.LandsLater;

    public void LandTheLastLaying(TownWorld town) => _townClaims.LandTheLastLaying(town);

    /// <summary>
    /// <b>Every body stepped on the device</b> (<see cref="DeviceTownBodies"/>): the wheels the tyre pass left for the cars
    /// ticked here and the impulses the walkers' tick left, where this tick's agents were ticked here — what the town
    /// ticked itself, and every agent of a tick held, laid from the town.
    /// </summary>
    public void StepBodies(TownWorld town, float dtS)
    {
        var leaving = Leaving(town, everyKept: false);
        _bodies.StepBodies(town, dtS, _ticked ? _taken.AsSpan(0, _count) : default, walkersTicked: _ticked, leaving);
        KnowTheStep();
        TakeTheWheelsBack(town.Cars, leaving);
        HandedOver = (HandedOver.InTheStep + leaving.Length, HandedOver.Alone);
        _ticked = false;
        _swept = false;
    }

    /// <summary>
    /// <b>Every car the device ticked, written back</b>: where it found itself on its line, what it was told — of it only
    /// what the town reads (<see cref="DeviceCarTick.BriefRow"/>), the rest of its context left as the town last set it — the
    /// junction ahead, its command and its tyres, and the marks they laid — the device's state remembered as the shadow's
    /// baseline — and then what its tick asks of the town, taken with the town's own code.
    /// </summary>
    void TakeBackTheCars(TownWorld town)
    {
        var cars = town.Cars;
        var taken = _taken.AsSpan(0, _count);
        _takenNow.AsSpan(0, cars.Count).Clear();
        var everyCar = _cars.ReadsEveryCar;
        var events = everyCar ? default : _cars.Events;
        var nextEvent = 0;
        foreach (var car in taken)
        {
            _takenNow[car] = true;
            _tyres.KeptOnTheDevice[car] = true;

            // A decision taken while the tick ran laid it a line or set it doing something else: what the tick read of the
            // line it was sent stands for nothing now, and the town's own is laid whole next tick.
            _movedOn[car] = _cars.TheTownMovedOnFrom(cars, car);
            if (_movedOn[car]) CarsTheTownMovedOn++;

            var asked = nextEvent < events.Length && events[nextEvent] == car;
            if (asked) nextEvent++;
            _handedBack[car] |= everyCar || asked;
            if (!_handedBack[car])
            {
                _cars.LeftOnTheDevice(car);
                CarsLeftOnTheDevice++;
                continue;
            }

            var row = RowOf(car);
            WriteTheTyres(cars, car, row.Car);
            TakeTheClock(town, car, row);
            _cars.TookBack(car);
            if (_movedOn[car]) continue;

            if ((row.Tick.Flags & DeviceCarTick.TickRow.Shifted) != 0) town.ShiftedOnTheDevice(car);

            WriteTheTick(cars, car, row);
        }

        foreach (var mark in _tyres.MarksLaid())
        {
            if (!_takenNow[(int)(mark.CarWheel / TyreModel.Wheels)]) continue;

            town.Marks.Mark(mark.FromM, mark.ToM, mark.WidthM, mark.Intensity, mark.Ploughed != 0u);
        }

        _cars.SettleTheTown(town);
        _cars.SettleTheDevice(cars, taken);
        foreach (var car in taken)
        {
            var handedBack = _handedBack[car];
            _handedBack[car] = false;
            if (_movedOn[car])
            {
                _cars.LayItWholeNext(car);
                continue;
            }

            if (!handedBack) continue;

            var flags = RowOf(car).Tick.Flags;
            if ((flags & DeviceCarTick.TickRow.Hazard) != 0) town.BrakedHardOnTheDevice();
            if ((flags & DeviceCarTick.TickRow.Lost) != 0)
            {
                town.LostItsLineOnTheDevice(car);
                continue;
            }

            if (cars.Action[car] == CarAction.Rejoin) town.RejoinedOnTheDevice(car);
            if ((flags & DeviceCarTick.TickRow.AsksTheRoute) != 0) town.AskedTheRouteOnTheDevice(car);
            if ((flags & DeviceCarTick.TickRow.SteppedOut) != 0) town.SteppedOutOnTheDevice(car);
            if ((flags & DeviceCarTick.TickRow.PassMade) != 0) town.MadeThePassOnTheDevice(car);
        }
    }

    /// <summary>
    /// <b>Which of the cars taken the town reads before the device ticks them again</b> (<see cref="TownWorld.ReadsTheTickOf"/>),
    /// marked for this tick to hand back — besides those whose tick asks something of the town, which it names itself.
    /// </summary>
    void LayTheWanted(TownWorld town)
    {
        // Built here and copied across whole: the table is memory the host only writes, slow to read back a word at a time.
        var wanted = _wanted.AsSpan(0, _cars.Wanted.Length);
        wanted.Clear();
        foreach (var car in _taken.AsSpan(0, _count))
        {
            var decidedByTheTown = _decidedByTheTown[car];
            _decidedByTheTown[car] = false;
            if (!_cars.WasLaidWhole(car) && !decidedByTheTown && !town.ReadsTheTickOf(car)) continue;

            wanted[car >> 5] |= 1u << (car & 31);
            _handedBack[car] = true;
        }

        wanted.CopyTo(_cars.Wanted);
    }

    /// <summary>Which cars the town reads after this tick, a bit a car, as <see cref="LayTheWanted"/> builds them.</summary>
    readonly uint[] _wanted;

    /// <summary>
    /// What the last tick handed back of a car: written into the host's memory where the town reads it, or copied back with
    /// every car's (<see cref="ReadsEveryCarBack"/>).
    /// </summary>
    DeviceCarTick.OutRow RowOf(int car)
    {
        if (!_cars.ReadsEveryCar) return _cars.RowOut(car);

        var clock = _cars.ClockAfter(car);
        return new DeviceCarTick.OutRow(
            _cars.TicksAfter[car], _cars.CommandsAfter[car], _cars.BriefsAfter[car], _tyres.CarsAfter[car], clock.X, clock.Y, 0u);
    }

    void WriteTheTick(CarFleet cars, int car, in DeviceCarTick.OutRow row)
    {
        var tick = row.Tick;
        cars.ProgressM[car] = tick.ProgressM;
        cars.CoveredSinceClaimM[car] = tick.CoveredSinceClaimM;
        cars.AlongMps[car] = tick.AlongMps;
        cars.OffLineM[car] = tick.OffLineM;
        cars.PlannedMps[car] = tick.PlannedMps;
        cars.Hold[car] = (DrivingHold)tick.Hold;

        var command = row.Command;
        cars.Command[car] = new DriveCommand(
            command.SteerRad, command.ThrottleMps2, command.BrakeMps2, (command.HandbrakeAndReverse & 1u) != 0,
            (command.HandbrakeAndReverse & 2u) != 0);

        var brief = row.Brief;
        cars.Context[car] = cars.Context[car] with
        {
            AuthorityM = brief.AuthorityM, PassAsideM = brief.PassAsideM, Blocked = (brief.Flags & DeviceCarTick.BriefRow.Blocked) != 0,
        };

        cars.ToTheBoxM[car] = brief.ToTheBoxM;
        cars.LightAheadM[car] = brief.LightAheadM;
        cars.BoxIsOurs[car] = (brief.Flags & DeviceCarTick.BoxRow.Claimed) != 0;
        cars.TurningAtTheBox[car] = (brief.Flags & DeviceCarTick.BoxRow.Turning) != 0;
        cars.InsideTheBox[car] = (brief.Flags & DeviceCarTick.BoxRow.Inside) != 0;
        cars.CommittedToTheBox[car] = (brief.Flags & DeviceCarTick.BoxRow.Committed) != 0;
    }

    /// <summary>A car's own tyre row written back; its wheels are kept on the device until it leaves (<see cref="TakeTheWheelsBack"/>).</summary>
    static void WriteTheTyres(CarFleet cars, int car, in DeviceTyres.CarRow row)
    {
        cars.SlipThrottle[car] = row.SlipThrottle;
        cars.DrivenSlipping[car] = (row.Flags & DeviceTyres.CarRow.DrivenSlipping) != 0;
    }

    /// <summary>
    /// <b>Every walker the device ticked, written back</b> where its tick wrote anything: the way it faces, the pace it
    /// declared and the impulse it left — but a casualty's, whose slide is the town's own bodies' phase's
    /// (<c>TownWorld.Settle</c>), written over the tick's row before the bodies are stepped.
    /// </summary>
    void TakeBackTheWalkers(TownWorld town)
    {
        var people = town.People;
        var ticks = _walkers.TicksAfter;
        for (var person = 0; person < _walkerCount; person++)
        {
            ref readonly var tick = ref ticks[person];
            if (!_walkers.WroteLastTick(tick) || DeviceTownBodies.TheTownsImpulse(town, person)) continue;

            people.HeadingRad[person] = tick.HeadingRad;
            people.DeclaredMps[person] = tick.DeclaredMps;
            town.WalkerImpulseFor(person) = tick.ImpulseNs;
        }

        _walkers.KnowTheTick();
    }

    /// <summary>Where the bodies' step just handed back left every walker, taken as what the device's walker table holds.</summary>
    void KnowTheStep() => _walkers.KnowTheStep(_bodies.Phase.WalkersAfter);

    /// <remarks>Every wheel the device keeps is handed back first: the town may go on ticking its cars itself.</remarks>
    public void Dispose()
    {
        _town.TakeTheDecisionsPutOff();
        _townClaims.LandTheLastLaying(_town);
        HandBackAlone(_town, Leaving(_town, everyKept: true));
        _timer.Dispose();
        _stepBatch.Dispose();
        _batch.Dispose();
        _townClaims.Dispose();
        _bodies.Dispose();
        _walkers.Dispose();
        _cars.Dispose();
        _claims.Dispose();
        _tyres.Dispose();
    }
}
