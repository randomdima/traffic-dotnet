using System.Diagnostics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
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
/// submit, into the tables the car tick reads. <b>Where the town has to lay them itself</b> — a hand or a truck holding
/// what it cannot stop short of, a manoeuvre begun at a bay, a tick held, more cars on a pass than there is room to ask
/// for — they are sent as cut (<see cref="DeviceClaims.LayAsCut"/>), as each was before, and counted.
/// </para>
/// <para>
/// <b>The bodies are the device's</b> (<see cref="DeviceTownBodies"/>): stepped off the impulses the tick left on the
/// device, the town's own written over them for what the town ticked itself. The poses the step leaves in the tyre pass's
/// table are what the next tick reads, the forward a body's own; the town lays only those of the cars whose bodies it
/// changed since (<see cref="DeviceTownBodies.PosesLeftInTheTable"/>).
/// </para>
/// <para>
/// <b>What the town's tick of a car it keeps does to its tyres stays the town's</b>: the device runs every car's tyres,
/// and for a car the town ticked itself the town's own answer stands and the device's is left unread.
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
    readonly GpuTimer _timer;
    readonly float _dtS;
    readonly int[] _taken;
    readonly bool[] _takenNow;

    /// <summary>Which cars the town's tick left to the device this tick, as <see cref="Takes"/> answered it.</summary>
    readonly bool[] _chosen;

    /// <summary>The cars whose wheels are being handed back, in the order they are copied.</summary>
    readonly int[] _leaving;

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
        _walkers = new DeviceWalkerTick(vk, config, town.People.Capacity);
        _bodies = new DeviceTownBodies(vk, config, town, _tyres, _walkers);
        _townClaims = new DeviceTownClaims(vk, config, town, _claims, _cars);
        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, 3);
        _dtS = config.TickSeconds;
        _taken = new int[cars.Capacity];
        _takenNow = new bool[cars.Capacity];
        _chosen = new bool[cars.Capacity];
        _leaving = new int[cars.Capacity];
    }

    /// <summary>How many cars the device ticked last tick.</summary>
    public int CarsTaken => _count;

    /// <summary>The device's own time for the last tick's kernels, the bodies' phase's among them, in milliseconds.</summary>
    public double KernelsMs => _cars.TickMs + _cars.TyresMs + _walkers.TickMs + _bodies.Phase.KernelsMs;

    /// <summary>The bodies' phase, for whoever reads its times.</summary>
    public DeviceTownBodies Bodies => _bodies;

    /// <summary>The device's time taking the last tick's car rows in and handing them back, in milliseconds.</summary>
    public double CopiesMs => _cars.SentMs + _cars.ReadMs;

    /// <summary>The device's time from the first command of the last tick's submit to its last, in milliseconds.</summary>
    public double SubmitMs => _timer.BetweenMs(0, 2);

    /// <summary>Of it, the device's time taking the town's claims in and listing them, in milliseconds.</summary>
    public double ClaimsMs => _timer.BetweenMs(0, 1);

    /// <summary>How many bytes of car rows the last tick sent.</summary>
    public ulong SentBytes { get; private set; }

    /// <summary>How many cars' wheels went back to the host in the bodies' submit, and how many in a submit of their own.</summary>
    public (long InTheStep, long Alone) HandedOver { get; private set; }

    /// <summary>How many submits of their own handed wheels back.</summary>
    public long HandOverSubmits { get; private set; }

    public bool Takes(int agent)
    {
        var town = _town;
        if (!_swept) HandBackTheLeavers(town);

        if (town.HoldAgents) return false;
        if (!town.Roster.IsCar(agent)) return !town.WalksUnderAHand(agent);

        var car = town.Roster.CarIndex(agent);
        var takes = _chosen[car] = DeviceCarTick.Takes(town, car);

        // The step a switching car asked for is answered by the car's own tick before it drives, which the device's is not.
        if (takes && town.Cars.Action[car] == CarAction.Switch) town.Switching.KeepOrWithdrawTheStep(car);

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
        _tyres.RecordHandOver(commands, leaving);
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

    /// <summary>Each car's wheels as the hand-over copied them back, written into the fleet; the device keeps them no longer.</summary>
    void TakeTheWheelsBack(CarFleet cars, ReadOnlySpan<int> leaving)
    {
        var wheels = _tyres.WheelsAfter;
        foreach (var car in leaving)
        {
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

    /// <summary>The host's time writing what the last tick sent, in milliseconds.</summary>
    public double PrepareMs { get; private set; }

    /// <summary>The host's time recording, submitting and waiting for the last tick, in milliseconds.</summary>
    public double WaitMs { get; private set; }

    /// <summary>The host's time writing what the last tick left back into the town, in milliseconds.</summary>
    public double TakeBackMs { get; private set; }

    /// <summary>
    /// <b>Whether the bodies are stepped in the agents' own submit</b> (<see cref="ITownDevice.StepsWithTheTick"/>): one
    /// submit a tick rather than two, the decisions reading the town the step left and what one does to a body stepped
    /// a tick later. Off, the bodies are phase 4's own submit, after the decisions.
    /// </summary>
    public bool StepsWithTheTick { get; init; } = true;

    public void TickTheRest(TownWorld town)
    {
        if (town.HoldAgents)
        {
            // Nothing is ticked here, and the bodies step on the town's own impulses.
            if (!StepsWithTheTick) return;

            _bodies.StepBodies(town, _dtS, default, walkersTicked: false);
            _swept = false;
            return;
        }

        var began = Stopwatch.GetTimestamp();
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
        _walkerCount = _walkers.Lay(town);
        if (stepping) _bodies.LayTheStep(town, _dtS, _taken.AsSpan(0, _count), walkersTicked: true);
        var prepared = Stopwatch.GetTimestamp();

        var commands = _batch.Begin();
        _timer.Reset(commands);
        _timer.Stamp(commands, 0);
        if (laidHere) _townClaims.RecordTheRest(commands);
        if (laidByTheTown) _claims.RecordAsCut(commands);
        _timer.Stamp(commands, 1);
        _cars.Record(commands, _count, town.Cars.Count);
        SentBytes = _cars.SentBytes;
        _cars.RecordRead(commands);
        _walkers.Record(commands, _walkerCount);
        _walkers.RecordRead(commands);
        if (stepping) _bodies.RecordTheStep(commands, town, _dtS);
        _timer.Stamp(commands, 2);
        _batch.SubmitAndWait();
        _timer.Read();
        _cars.ReadTimes();
        _walkers.ReadTimes();
        var waited = Stopwatch.GetTimestamp();

        TakeBackTheCars(town);
        TakeBackTheWalkers(town);
        if (stepping)
        {
            _bodies.TakeTheStepBack(town);
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

    public bool GrantedTheCars => _townClaims.GrantedTheCars;

    /// <summary>
    /// <b>Every body stepped on the device</b> (<see cref="DeviceTownBodies"/>): the wheels the tyre pass left for the cars
    /// ticked here and the impulses the walkers' tick left, where this tick's agents were ticked here — what the town
    /// ticked itself, and every agent of a tick held, laid from the town.
    /// </summary>
    public void StepBodies(TownWorld town, float dtS)
    {
        var leaving = Leaving(town, everyKept: false);
        _bodies.StepBodies(town, dtS, _ticked ? _taken.AsSpan(0, _count) : default, walkersTicked: _ticked, leaving);
        TakeTheWheelsBack(town.Cars, leaving);
        HandedOver = (HandedOver.InTheStep + leaving.Length, HandedOver.Alone);
        _ticked = false;
        _swept = false;
    }

    /// <summary>
    /// <b>Every car the device ticked, written back</b>: where it found itself on its line, what it was told, the junction
    /// ahead, its command and its tyres, and the marks they laid — the device's state remembered as the shadow's baseline —
    /// and then what its tick asks of the town, taken with the town's own code.
    /// </summary>
    void TakeBackTheCars(TownWorld town)
    {
        var cars = town.Cars;
        var taken = _taken.AsSpan(0, _count);
        _takenNow.AsSpan(0, cars.Count).Clear();
        var ticks = _cars.TicksAfter;
        foreach (var car in taken)
        {
            _takenNow[car] = true;
            if ((ticks[car].Flags & DeviceCarTick.TickRow.Shifted) != 0) town.ShiftedOnTheDevice(car);

            WriteTheTick(cars, car);
            WriteTheTyres(cars, car);
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
            var flags = ticks[car].Flags;
            if ((flags & DeviceCarTick.TickRow.Hazard) != 0) town.BrakedHardOnTheDevice();
            if ((flags & DeviceCarTick.TickRow.Lost) != 0)
            {
                town.LostItsLineOnTheDevice(car);
                continue;
            }

            if (cars.Action[car] == CarAction.Rejoin) town.RejoinedOnTheDevice(car);
            if ((flags & DeviceCarTick.TickRow.AsksTheRoute) != 0) town.AskedTheRouteOnTheDevice(car);
            if ((flags & DeviceCarTick.TickRow.SteppedOut) != 0) town.SteppedOutOnTheDevice(car);
        }
    }

    void WriteTheTick(CarFleet cars, int car)
    {
        var tick = _cars.TicksAfter[car];
        cars.ProgressM[car] = tick.ProgressM;
        cars.CoveredSinceClaimM[car] = tick.CoveredSinceClaimM;
        cars.AlongMps[car] = tick.AlongMps;
        cars.OffLineM[car] = tick.OffLineM;
        cars.PlannedMps[car] = tick.PlannedMps;
        cars.Hold[car] = (DrivingHold)tick.Hold;

        var command = _cars.CommandsAfter[car];
        cars.Command[car] = new DriveCommand(
            command.SteerRad, command.ThrottleMps2, command.BrakeMps2, (command.HandbrakeAndReverse & 1u) != 0,
            (command.HandbrakeAndReverse & 2u) != 0);

        var told = _cars.ContextsAfter[car];
        cars.Context[car] = new DriveContext(
            told.AuthorityM, (HeadwayKind)told.GrantCutBy, told.PlaceStopM, told.MarginM, told.WaitsToPass != 0u, told.HorizonM,
            told.PassAsideM, told.BackUpM, told.Blocked != 0u, told.WaitAtM);

        var box = _cars.BoxesAfter[car];
        cars.ToTheBoxM[car] = box.ToTheBoxM;
        cars.LightAheadM[car] = box.LightAheadM;
        cars.BoxIsOurs[car] = (box.Flags & DeviceCarTick.BoxRow.Claimed) != 0;
        cars.TurningAtTheBox[car] = (box.Flags & DeviceCarTick.BoxRow.Turning) != 0;
        cars.InsideTheBox[car] = (box.Flags & DeviceCarTick.BoxRow.Inside) != 0;
        cars.CommittedToTheBox[car] = (box.Flags & DeviceCarTick.BoxRow.Committed) != 0;
    }

    /// <summary>A car's own tyre row written back; its wheels are kept on the device until it leaves (<see cref="TakeTheWheelsBack"/>).</summary>
    void WriteTheTyres(CarFleet cars, int car)
    {
        var row = _tyres.CarsAfter[car];
        cars.SlipThrottle[car] = row.SlipThrottle;
        cars.DrivenSlipping[car] = (row.Flags & DeviceTyres.CarRow.DrivenSlipping) != 0;
        _tyres.KeptOnTheDevice[car] = true;
    }

    /// <summary>
    /// <b>Every walker the device ticked, written back</b>: the way it faces, the pace it declared and the impulse it left —
    /// but a casualty's, whose slide is the town's own bodies' phase's (<c>TownWorld.Settle</c>), written over the tick's
    /// row before the bodies are stepped.
    /// </summary>
    void TakeBackTheWalkers(TownWorld town)
    {
        var people = town.People;
        var rows = _walkers.WalkersAfter;
        var impulses = _walkers.ImpulsesAfter;
        for (var person = 0; person < _walkerCount; person++)
        {
            if (DeviceTownBodies.TheTownsImpulse(town, person)) continue;

            people.HeadingRad[person] = rows[person].HeadingRad;
            people.DeclaredMps[person] = rows[person].DeclaredMps;
            town.WalkerImpulseFor(person) = impulses[person];
        }
    }

    /// <remarks>Every wheel the device keeps is handed back first: the town may go on ticking its cars itself.</remarks>
    public void Dispose()
    {
        HandBackAlone(_town, Leaving(_town, everyKept: true));
        _timer.Dispose();
        _batch.Dispose();
        _townClaims.Dispose();
        _bodies.Dispose();
        _walkers.Dispose();
        _cars.Dispose();
        _claims.Dispose();
        _tyres.Dispose();
    }
}
