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
/// device, the town's own written over them for what the town ticked itself.
/// </para>
/// <para>
/// <b>What the town's tick of a car it keeps does to its tyres stays the town's</b>: the device runs every car's tyres,
/// and for a car the town ticked itself the town's own answer stands and the device's is left unread.
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
    readonly int[] _taken;
    readonly bool[] _takenNow;

    /// <summary>Which cars the town's tick left to the device this tick, as <see cref="Takes"/> answered it.</summary>
    readonly bool[] _chosen;
    int _count;
    int _walkerCount;

    /// <summary>Whether this tick's agents were ticked here, so the bodies' phase spends what their tick left.</summary>
    bool _ticked;

    public DeviceTown(Vk vk, SimConfig config, TownWorld town)
    {
        _vk = vk;
        _town = town;
        var cars = town.Cars;
        var claims = town.Occupancy;
        var builds = new CarBuild[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = cars.BuildOfVariant(variant);

        _tyres = new DeviceTyres(vk, config, town.WheelGrounds, builds, cars.Capacity);
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
        _taken = new int[cars.Capacity];
        _takenNow = new bool[cars.Capacity];
        _chosen = new bool[cars.Capacity];
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

    public bool Takes(int agent)
    {
        var town = _town;
        if (town.HoldAgents) return false;
        if (!town.Roster.IsCar(agent)) return !town.WalksUnderAHand(agent);

        var car = town.Roster.CarIndex(agent);
        return _chosen[car] = DeviceCarTick.Takes(town, car);
    }

    /// <summary>The host's time writing what the last tick sent, in milliseconds.</summary>
    public double PrepareMs { get; private set; }

    /// <summary>The host's time recording, submitting and waiting for the last tick, in milliseconds.</summary>
    public double WaitMs { get; private set; }

    /// <summary>The host's time writing what the last tick left back into the town, in milliseconds.</summary>
    public double TakeBackMs { get; private set; }

    public void TickTheRest(TownWorld town)
    {
        if (town.HoldAgents) return;

        var began = Stopwatch.GetTimestamp();
        var laidHere = _townClaims.Pending;
        var laidByTheTown = _townClaims.TakeLaidByTheTown() | (town.ClaimsLaidThisTick && !laidHere);
        if (laidByTheTown) _claims.LayAsCut(town.Occupancy);
        var chosen = _chosen.AsSpan(0, town.Cars.Count);
        _count = _cars.Prepare(town, _taken, resident: true, laidByTheTown, chosen);
        chosen.Clear();
        _walkerCount = _walkers.Lay(town);
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
        _timer.Stamp(commands, 2);
        _batch.SubmitAndWait();
        _timer.Read();
        _cars.ReadTimes();
        _walkers.ReadTimes();
        var waited = Stopwatch.GetTimestamp();

        TakeBackTheCars(town);
        TakeBackTheWalkers(town);
        _ticked = true;
        PrepareMs = Stopwatch.GetElapsedTime(began, prepared).TotalMilliseconds;
        WaitMs = Stopwatch.GetElapsedTime(prepared, waited).TotalMilliseconds;
        TakeBackMs = Stopwatch.GetElapsedTime(waited).TotalMilliseconds;
    }

    /// <summary>The claims laid here, for whoever reads how many and what each cost.</summary>
    public DeviceTownClaims Claims => _townClaims;

    public bool LaysTheClaims(TownWorld town) => _townClaims.LaysTheClaims(town);

    public void ClaimsAt(TownWorld town, ClaimsStage stage) => _townClaims.ClaimsAt(town, stage);

    /// <summary>
    /// <b>Every body stepped on the device</b> (<see cref="DeviceTownBodies"/>): the wheels the tyre pass left for the cars
    /// ticked here and the impulses the walkers' tick left, where this tick's agents were ticked here — what the town
    /// ticked itself, and every agent of a tick held, laid from the town.
    /// </summary>
    public void StepBodies(TownWorld town, float dtS)
    {
        _bodies.StepBodies(town, dtS, _ticked ? _taken.AsSpan(0, _count) : default, walkersTicked: _ticked);
        _ticked = false;
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
            WriteTheTyres(town, car);
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

    void WriteTheTyres(TownWorld town, int car)
    {
        var cars = town.Cars;
        var row = _tyres.CarsAfter[car];
        cars.SlipThrottle[car] = row.SlipThrottle;
        cars.DrivenSlipping[car] = (row.Flags & DeviceTyres.CarRow.DrivenSlipping) != 0;

        var impulses = town.WheelImpulsesFor(car);
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var at = (car * TyreModel.Wheels) + wheel;
            var carried = _tyres.WheelsAfter[at];
            cars.WheelSpinMps[at] = carried.SpinMps;
            cars.ScrubTravelM[at] = carried.ScrubTravelM;
            cars.Marking[at] = carried.Marking != 0u;
            cars.MarkFromM[at] = carried.MarkFromM;
            cars.MarkIntensity[at] = carried.MarkIntensity;

            var spent = _tyres.Impulses[at];
            impulses[wheel] = new WheelImpulse(new(spent.X, spent.Y), new(spent.Z, spent.W));
        }
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
            if (town.WalksUnderAHand(person) || (people.Wounded[person] && !people.Inside[person].Any)) continue;

            people.HeadingRad[person] = rows[person].HeadingRad;
            people.DeclaredMps[person] = rows[person].DeclaredMps;
            town.WalkerImpulseFor(person) = impulses[person];
        }
    }

    public void Dispose()
    {
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
