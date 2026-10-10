using System.Diagnostics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
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
/// device every body, light, plan, walk, answer and backing in one submit, and every grant into the tables the car tick
/// reads, and the claims are written back into the town. The walkers' side of them is the device's own, read in off the walker tick's
/// table (<see cref="DeviceWalkers.Keeps"/>). <b>Where the town has to lay them itself</b> — a hand or a truck holding
/// what it cannot stop short of, a manoeuvre begun at a bay, a tick held, more cars on a pass than there is room to ask
/// for — they are sent as cut (<see cref="DeviceClaims.LayAsCut"/>), as each was before, and counted. A rebuild laid here
/// runs on while the town goes on (<see cref="RebuildsRunOn"/>), the cars the town ticks itself put off until it answers.
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

    /// <summary>The head of the tick — what is sent, and the decisions put off asked — submitted ahead of the rest.</summary>
    readonly ComputeBatch _headBatch;

    readonly GpuTimer _timer;
    readonly float _dtS;
    readonly int[] _taken;

    /// <summary>Which cars the last tick took, a bit a car — what a decision put off from it reads them as.</summary>
    readonly AgentSet _takenNow;

    /// <summary>The cars a take-back walks, and those whose staying on the device the town may have changed.</summary>
    readonly int[] _takeBack, _maySet;

    /// <summary>Which cars the town's tick left to the device this tick, as <see cref="Takes"/> answered it.</summary>
    readonly bool[] _chosen;

    /// <summary>The cars the town ticks itself, put off this tick while a claims rebuild runs on (<see cref="Takes"/>).</summary>
    readonly int[] _putOff;

    int _putOffCount;

    /// <summary>The cars whose wheels are being handed back, in the order they are copied.</summary>
    readonly int[] _leaving;

    /// <summary>Which cars the last tick took that the town moved on from while it ran, so what it read of them is not taken back.</summary>
    readonly bool[] _movedOn;

    /// <summary>Which cars the last tick took that it handed back (<see cref="ReadsEveryCarBack"/>): every one, or those the town reads.</summary>
    readonly bool[] _handedBack;

    /// <summary>Which cars the last tick that took them handed their grant left back (<see cref="DeviceCarTick.HandsBackTheGrantsLeft"/>).</summary>
    readonly bool[] _grantLeftHandedBack;

    int _count;
    int _walkerCount;

    /// <summary>Whether this tick's agents were ticked here, so the bodies' phase spends what their tick left.</summary>
    bool _ticked;

    /// <summary>Whether this tick's agents have been looked over for a car leaving the device with its wheels still there.</summary>
    bool _swept;

    /// <param name="rooms">
    /// The rooms the claims laid here begin with — its rows, a car's plan and a walk — grown to fit whatever is laid
    /// (<see cref="DeviceRebuild.RoomsToStart"/> where none).
    /// </param>
    public DeviceTown(Vk vk, SimConfig config, TownWorld town, DeviceRebuild.Rooms? rooms = null)
    {
        _vk = vk;
        _town = town;
        var cars = town.Cars;
        var claims = town.Occupancy;
        var builds = new CarBuild[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = cars.BuildOfVariant(variant);

        var start = rooms ?? DeviceRebuild.RoomsToStart(town);
        _tyres = new DeviceTyres(vk, config, town.WheelGrounds, builds, cars.Capacity) { ReadsEveryWheel = false };
        _claims = new DeviceClaims(vk, claims.WayCount, start.Rows, claims.HoldCapacity, config.Road.MostSettlingPasses, cars.Capacity);
        _cars = new DeviceCarTick(
            vk, config, _tyres, town.Roads, cars.LineArcsOf(0).Length, town.Ways.OfRoadConnector(0), _claims.Cut)
        {
            Shadowed = false,
        };
        _walkers = new DeviceWalkerTick(vk, config, town.People.Capacity, resident: true);
        _walkersBack = new int[town.People.Capacity];
        _bodies = new DeviceTownBodies(vk, config, town, _tyres, _walkers)
        {
            HandsOverThrough = _cars,
        };
        _townClaims = new DeviceTownClaims(vk, config, town, _claims, _cars, _walkers, start);
        _townClaims.Rebuild.MirrorsAtOnce = false;
        _looks = new DeviceSwitchLook(vk, config, town, _cars.Decided, _townClaims.Rebuild);
        _cars.Looks = _looks;
        RebuildsRunOn = true;
        _batch = new ComputeBatch(vk);
        _stepBatch = new ComputeBatch(vk);
        _headBatch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, 3);
        _dtS = config.TickSeconds;
        _taken = new int[cars.Capacity];
        _takenNow = new AgentSet(cars.Capacity);
        _takeBack = new int[cars.Capacity];
        _maySet = new int[cars.Capacity];
        _chosen = new bool[cars.Capacity];
        _putOff = new int[cars.Capacity];
        _leaving = new int[cars.Capacity];
        _movedOn = new bool[cars.Capacity];
        _handedBack = new bool[cars.Capacity];
        _grantLeftHandedBack = new bool[cars.Capacity];
        _wanted = new uint[_cars.Wanted.Length];
        _cars.ReadsEveryCar = false;
        _listed = new bool[cars.Capacity];
        _decidedByTheTown = new bool[cars.Capacity];
        _byTheTown = new bool[cars.Capacity];
        _decisionIntervalS = new TickGroups(config.Sim.AgentDecisionIntervalS, config.Sim.TickRateHz).IntervalS;
        _decided = new int[cars.Capacity];
        _shapeOf = new uint[cars.Capacity];
        _shapeInward = new bool[cars.Capacity];
        _shapeLaidAt = new (uint, uint)[cars.Capacity];
        _shapeKnown = new bool[cars.Capacity];
        _looking = new bool[cars.Capacity];
        _found = new bool[cars.Capacity];
        _letGo = new int[cars.Capacity];
        _townTicks = new int[cars.Capacity];
        _asked = new AgentSet(cars.Capacity);
        town.DriveClocks.KeepTheTouched();
        town.CarsDecidedOnTheDevice = true;

        // Every walker's clock the device's from here on, as the town has it now.
        var people = town.People;
        _walkerListed = new bool[people.Capacity];
        _walkerByTheTown = new bool[people.Capacity];
        _walkersDue = new uint[people.Capacity];
        town.WalkClocks.KeepTheTouched();
        for (var person = 0; person < people.Count; person++)
        {
            var (sinceS, closestM) = town.WalkClocks.Of(person);
            KeptWalkers.LayTheClock(person, sinceS, closestM, people.RouteTaken[person]);
        }
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

    /// <summary>
    /// <b>Whether the device decides the walkers whose decisions put off a tick are their clock alone</b>
    /// (<see cref="TownWorld.WalkerTakesOnlyTheClock"/>, <c>walkers_decide.comp</c>), as it does the cars
    /// (<see cref="DecidesTheCars"/>). Off, the town decides every walker.
    /// </summary>
    public bool DecidesTheWalkers { get; init; } = true;

    /// <summary>The walkers' side of the claims the device keeps, whose table the walkers' decisions are asked of.</summary>
    DeviceWalkers KeptWalkers => _townClaims.Rebuild.Walkers;

    /// <summary>Which walkers' decisions this tick asks of the device, which of them the town decides whole, and the list laid.</summary>
    readonly bool[] _walkerListed, _walkerByTheTown;

    readonly uint[] _walkersDue;
    int _walkersDueCount;

    /// <summary>Whether the device's copy of every walker's hold is the readers', and whether the town laid the claims itself since the device last did.</summary>
    bool _walkerReadersKnown, _townLaidSinceTheDevice;

    /// <summary>How many walkers' decisions the device found were more than their clock, or the town took whole, over every tick.</summary>
    public long WalkersDecidedByTheTown { get; private set; }

    /// <summary>
    /// <b>The walkers' decisions put off last tick that this tick asks of the device</b>: every one out of doors, on its feet
    /// and off duty — whose decision may run its clock — the town deciding it whole where the kept table does not hold it as
    /// the town does (<see cref="DeviceWalkers.HoldsAsTheTownHas"/>), or the device has no copy of the holds the readers read.
    /// </summary>
    void LayTheWalkerDecisions(TownWorld town)
    {
        var people = town.People;
        var firstCar = town.Roster.AgentOfCar(0);
        var kept = KeptWalkers;
        var count = 0;
        var rebuild = _townClaims.Rebuild;
        foreach (var person in town.DecisionsPutOff)
        {
            if (person >= firstCar) break;

            var hold = _asksTheHolds ? town.WalkHold(person, rebuild.LandsThisTick) : LaneOccupancy.NoHold;
            if (people.Inside[person].Any || people.Wounded[person] || people.Stage[person] == TripStage.OnDuty)
            {
                rebuild.AskTheHold(hold);
                continue;
            }

            var byTheTown = !_walkerReadersKnown || !kept.HoldsAsTheTownHas(town, person);
            _walkerByTheTown[person] = byTheTown;
            _walkerListed[person] = true;
            _walkersDue[count++] = (uint)person | (byTheTown ? DeviceWalkers.ByTheTown : 0u);
            if (ChecksTheDecisions) rebuild.AskTheHold(hold);
            else rebuild.AskTheWalkersHoldIfDecided(hold, person);
        }

        _walkersDueCount = count;
        var (wayInReachM, walksBeforeTheNextM, byM, patienceS, lostOffM) = town.WalkerClockTerms;
        kept.LayTheDecisions(
            _walkersDue.AsSpan(0, count),
            new DeviceWalkers.DecideFigures(
                0u, 0u, wayInReachM, walksBeforeTheNextM, byM, patienceS, _decisionIntervalS, lostOffM, ChecksTheDecisions ? 1u : 0u, 0u,
                0u, 0u));
    }

    /// <summary>Every walker's clock the town changed since it last laid them, laid where the device runs it, with its route slot.</summary>
    void LayTheWalkersClocks(TownWorld town)
    {
        var clocks = town.WalkClocks;
        foreach (var person in clocks.Touched)
        {
            if (!clocks.IsTouched(person)) continue;

            var (sinceS, closestM) = clocks.Of(person);
            KeptWalkers.LayTheClock(person, sinceS, closestM, town.People.RouteTaken[person]);
            clocks.Untouch(person);
        }

        clocks.ForgetTheTouched();
    }

    public bool WalkerDecidedOnTheDevice(int person)
    {
        if (!_walkerListed[person]) return false;

        _walkerListed[person] = false;
        var kept = KeptWalkers;
        var more = kept.DecidedMoreThanTheClock(person);
        var checks = ChecksTheDecisions && !_walkerByTheTown[person];

        // What the town reads of it is what the device decided it on, where a rebuild left it there, and where its last
        // step left it.
        if (more || checks)
        {
            kept.TakeUpTheDecided(_town, _walkers, person);
            _walkers.TakeTheTickBack(_town, person);
            _bodies.TakeThePoseBack(_town, person);
        }
        if (checks) CheckTheWalkersVerdict(person, more);

        // Its clock restarted where the rebuild walked it on, as the town's taking that up did, and run there.
        if (!more)
        {
            _town.WalkClocks.Untouch(person);
            return true;
        }

        // The town's own clock where it restarted it since: the device restarted it alike.
        if (!_town.WalkClocks.IsTouched(person))
        {
            ref readonly var clock = ref kept.DecidedOn(person);
            _town.WalkClocks.Take(person, clock.SinceS, clock.ClosestM);
        }

        WalkersDecidedByTheTown++;
        return false;
    }

    /// <summary>A walker's verdict held to the town's own, its clock as the device asked it with.</summary>
    void CheckTheWalkersVerdict(int person, bool more)
    {
        if (!_town.WalkClocks.IsTouched(person))
        {
            ref readonly var clock = ref KeptWalkers.DecidedOn(person);
            _town.WalkClocks.Take(person, clock.SinceS, clock.ClosestM);
        }

        DecisionsChecked++;
        if (_town.WalkerTakesOnlyTheClock(person, _decisionIntervalS) != more) return;

        DecisionsUnlike++;
        if (DecisionsUnlikeSeen.Count < 8) DecisionsUnlikeSeen.Add($"walker {person}: the device found {(more ? "more" : "its clock alone")}");
    }

    /// <summary>Which cars' decisions this tick asks of the device, and the list of them laid.</summary>
    readonly bool[] _listed;

    /// <summary>Which cars the device ticks this tick whose decisions put off the town takes with what it holds.</summary>
    readonly bool[] _decidedByTheTown;

    readonly int[] _decided;
    int _decidedCount;

    /// <summary>
    /// What of each car's wish to move across its road and route alone answer (<see cref="Switching.ShapeOfTheRoad"/>), which
    /// way its line stopping to move across has it move, and the line and route it was read off (<see cref="CarFleet.LineLaid"/>,
    /// <see cref="CarFleet.RouteLaid"/>).
    /// </summary>
    readonly uint[] _shapeOf;

    readonly bool[] _shapeInward;
    readonly (uint Line, uint Route)[] _shapeLaidAt;
    readonly bool[] _shapeKnown;

    /// <summary>How many cars' decisions the device found were more than their clock, or the town took whole, over every tick.</summary>
    public long DecidedByTheTown { get; private set; }

    /// <summary>
    /// <b>Whether a switching car's look for room is asked of the device</b> with its decision (<see cref="DeviceSwitchLook"/>),
    /// where the device holds the layer the town's readers read. Off, every switching car's decision is the town's.
    /// </summary>
    public bool LooksOnTheDevice { get; init; } = true;

    readonly DeviceSwitchLook _looks;

    /// <summary>The switching cars' looks on the device, for whoever reads their times.</summary>
    public DeviceSwitchLook Looks => _looks;

    /// <summary>Which cars' decisions this tick asks of the device with their look for room.</summary>
    readonly bool[] _looking;

    /// <summary>
    /// How many switching cars' decisions the device took with their look, over every tick, by how the look came out: not
    /// looking yet, looked and found no room, or found ground the layer does not hold yet.
    /// </summary>
    public (long Waits, long Looked, long Unknown) LooksTaken { get; private set; }

    /// <summary>
    /// <b>The decisions put off last tick that this tick asks of the device</b>: of every car the device holds as its last
    /// tick left it, what only the town knows laid — the town deciding it whole where the car is not one whose decision is
    /// read off its line, or every car where the device has no copy of the claims the town's readers read
    /// (<see cref="DeviceCarTick.ReadersKeptUp"/>). <b>A switching car's look for room is asked with it</b>
    /// (<see cref="LooksOnTheDevice"/>) where the claims the device holds are the readers': no rebuild laid on this tick, and
    /// the last laid here rather than by the town.
    /// </summary>
    void LayTheDecisions(TownWorld town, bool readersUnknown, bool laidByTheTown)
    {
        var cars = town.Cars;
        var firstCar = town.Roster.AgentOfCar(0);
        var rebuild = _townClaims.Rebuild;
        var looks = LooksOnTheDevice && _cars.ReadersKeepUp && rebuild.LaidAny && rebuild.LandsLater && !_townLaidSinceTheDevice
                    && !laidByTheTown;
        var count = 0;
        foreach (var agent in town.DecisionsPutOff)
        {
            if (agent < firstCar) continue;

            var car = town.Roster.CarIndex(agent);
            var hold = _asksTheHolds ? town.PlanHoldOf(car, rebuild.LandsThisTick) : LaneOccupancy.NoHold;
            if (!_takenNow.Has(car) || !_tyres.KeptOnTheDevice[car] || _cars.TownHolds(car))
            {
                // The town decides it with what it holds, and may move on from it: handed back as this tick leaves it.
                if (_cars.Takes(car)) _decidedByTheTown[car] = true;
                rebuild.AskTheHold(hold);
                continue;
            }

            if (ChecksTheDecisions) rebuild.AskTheHold(hold);
            else rebuild.AskTheHoldIfDecided(hold, car);

            var switching = looks && town.LeavesItsSwitchToItsLine(car);
            var byTheTown = readersUnknown || !(switching || town.LeavesItsDecisionToItsLine(car));
            _byTheTown[car] = byTheTown;
            _looking[car] = switching && !byTheTown;
            var (patienceS, byM, queueReachM) = town.ClockTermsOf(car);
            var flags = (byTheTown ? DeviceCarTick.DecideTermsRow.ByTheTown : ShapeOfTheRoad(town, car))
                        | (ChecksTheDecisions ? DeviceCarTick.DecideTermsRow.Checked : 0u);
            if (_looking[car]) flags |= LayTheSwitch(town, car);

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
    public bool ChecksTheDecisions { get; init; }

    /// <summary>How many decisions the device found its clock alone where the town finds more, or the other way, over every check.</summary>
    public long DecisionsUnlike { get; private set; }

    /// <summary>How many of the device's verdicts were held to the town's own (<see cref="ChecksTheDecisions"/>).</summary>
    public long DecisionsChecked { get; private set; }

    /// <summary>The first decisions found unlike, for the report.</summary>
    public List<string> DecisionsUnlikeSeen { get; } = [];

    /// <summary>
    /// A verdict held to the town's own, the car written into the town as the device read it — the town a decision put off
    /// reads, a tick behind — and its clock as it stood before the device ran it; and of a switching car whose look the
    /// device took, the look too, figure for figure — but where the device left it to the town for want of room.
    /// </summary>
    void CheckTheVerdict(int car, bool more, bool looking)
    {
        var cars = _town.Cars;
        ref readonly var row = ref _cars.DecidedOn(car);
        WriteTheTyres(cars, car, row.Car);
        WriteTheTick(cars, car, row);
        _cars.SettleTheTown(cars, [car]);
        _town.DriveClocks.Take(car, row.ClockSinceS, row.ClockClosestM);
        DecisionsChecked++;
        if (!looking)
        {
            if (_town.TakesOnlyTheClock(car, _decisionIntervalS) != more) return;

            DecisionsUnlike++;
            if (DecisionsUnlikeSeen.Count < 8) DecisionsUnlikeSeen.Add($"car {car}: the device found {(more ? "more" : "its clock alone")}");
            return;
        }

        if (more && _looks.Deferred(car)) return;

        var alone = _town.TakesOnlyItsLook(car, _decisionIntervalS, out var townLook);
        if (more && !alone) return;

        var deviceLook = more ? default : _looks.LookOf(car, _town.Switching.Inward(car), out _);
        if (!more && alone && deviceLook.Outcome == townLook.Outcome)
        {
            var apartM = FiguresApartM(townLook, deviceLook);
            if (apartM == 0f) return;

            LooksApart++;
            LooksMostApartM = MathF.Max(LooksMostApartM, apartM);
            return;
        }

        DecisionsUnlike++;
        if (DecisionsUnlikeSeen.Count < 8)
        {
            DecisionsUnlikeSeen.Add(
                $"switching car {car}: the device found {(more ? "more" : $"its look alone, {deviceLook}, {_looks.WhyOf(car)}")}; " +
                $"the town {(alone ? "its look alone" : "more")}, {townLook}{TheTownsClaimOf(car)}");
        }
    }

    /// <summary>Where the device's look was refused by a claim, that holder's claims on the same way as the town's readers hold them.</summary>
    string TheTownsClaimOf(int car)
    {
        if (!_looks.RefusedBy(car, out var way, out var occupant, out var roster)) return "";

        var claims = _town.ReadersOccupancy;
        var held = new List<string>();
        Span<int> slots = stackalloc int[64];
        foreach (var bodies in (ReadOnlySpan<bool>)[true, false])
        {
            foreach (var slot in slots[..claims.ListOf(way, bodies, slots)])
            {
                var claim = claims.Slots[slot];
                if (claim.Occupant == occupant && claim.Of == roster)
                {
                    held.Add($"{(bodies ? "body" : "plan")} [{claim.FromM}, {claim.ToM}){(claim.Passing ? " passing" : "")}");
                }
            }
        }

        return $"; the town's readers hold of {roster} {occupant} on way {way}: {(held.Count == 0 ? "nothing" : string.Join(", ", held))}";
    }

    /// <summary>
    /// How many switching cars' looks the device took came out as the town's own did but left it a figure other than the
    /// town's, and the most apart any stood (<see cref="LooksMostApartM"/>): how far across the lane beside stands is read off
    /// the road's arcs, and the step from rest and its room are drawn off that — a figure the device reads a float or two
    /// from the host's, by a term not yet traced (tech debt).
    /// </summary>
    public long LooksApart { get; private set; }

    /// <inheritdoc cref="LooksApart"/>
    public float LooksMostApartM { get; private set; }

    /// <summary>How far apart the figures of two looks that came out alike stand, the most of them: nought for the same to the bit.</summary>
    static float FiguresApartM(in SwitchLook one, in SwitchLook other)
    {
        if (!Same(one.LooksInS, other.LooksInS)) return float.PositiveInfinity;

        return MathF.Max(MathF.Max(ApartM(one.AsideM, other.AsideM), ApartM(one.FromRestM, other.FromRestM)), ApartM(one.RoomToM, other.RoomToM));

        static float ApartM(float one, float other) => Same(one, other) ? 0f : MathF.Max(MathF.Abs(one - other), float.Epsilon);
    }

    static bool Same(float one, float other) => BitConverter.SingleToInt32Bits(one) == BitConverter.SingleToInt32Bits(other);

    /// <summary>
    /// <b>A switching car's look the device took, taken</b> (<see cref="Switching.TakeTheLook"/>): how far across it stands,
    /// when it looks again and what its look drew — and the ways the look asked the layer about marked asked.
    /// </summary>
    void TakeTheLook(int car)
    {
        var switching = _town.Switching;
        var look = _looks.LookOf(car, switching.Inward(car), out var askedAbout);
        switching.TakeTheLook(car, look, askedAbout);
        var (waits, looked, unknown) = LooksTaken;
        LooksTaken = look.Outcome switch
        {
            SwitchOutcome.Looked => (waits, looked + 1, unknown),
            SwitchOutcome.Unknown => (waits, looked, unknown + 1),
            _ => (waits + 1, looked, unknown),
        };
    }

    /// <summary>What a decision answers for, as the loop reckons it.</summary>
    readonly float _decisionIntervalS;

    /// <summary>What of a car's wish to move across its road and route alone answer, read again where its line or route changed.</summary>
    uint ShapeOfTheRoad(TownWorld town, int car)
    {
        var laidAt = (town.Cars.LineLaid[car], town.Cars.RouteLaid[car]);
        if (_shapeKnown[car] && _shapeLaidAt[car] == laidAt) return _shapeOf[car];

        _shapeOf[car] = (uint)town.Switching.ShapeOfTheRoad(car, out _shapeInward[car]);
        _shapeLaidAt[car] = laidAt;
        _shapeKnown[car] = true;
        return _shapeOf[car];
    }

    /// <summary>
    /// A switching car's look asked of the device with its decision: the car as its last decision left it laid, and the terms
    /// that say so — which way its line stopping has it move (read with <see cref="ShapeOfTheRoad"/>), and which way it meant to.
    /// </summary>
    uint LayTheSwitch(TownWorld town, int car)
    {
        var switching = town.Switching;
        var (fromRestM, roomToM) = switching.LastLookOf(car);
        _looks.Lay(car, new DeviceSwitchLook.InRow(town.Cars.Channels[car], switching.LooksInS(car), fromRestM, roomToM));
        return DeviceCarTick.DecideTermsRow.Switching | (_shapeInward[car] ? DeviceCarTick.DecideTermsRow.ShapeInward : 0u)
               | (switching.Inward(car) ? DeviceCarTick.DecideTermsRow.SwitchInward : 0u);
    }

    public bool DecidedOnTheDevice(int car)
    {
        if (_found[car])
        {
            _found[car] = false;
            HandTheDecisionOver(car);
            return false;
        }

        if (!_listed[car]) return false;

        _listed[car] = false;
        var looking = _looking[car];
        _looking[car] = false;
        var more = _cars.DecidedMoreThanTheClock(car);
        if (ChecksTheDecisions && !_byTheTown[car]) CheckTheVerdict(car, more, looking);
        if (!more)
        {
            if (looking) TakeTheLook(car);
            return true;
        }

        HandTheDecisionOver(car);
        return false;
    }

    /// <summary>
    /// A decision the device found more than the clock: the town decides it on what the car's last tick left, as the device
    /// read it, and where its last step left it.
    /// </summary>
    void HandTheDecisionOver(int car)
    {
        var cars = _town.Cars;
        ref readonly var row = ref _cars.DecidedOn(car);
        WriteTheTyres(cars, car, row.Car);
        WriteTheTick(cars, car, row);
        _town.DriveClocks.Take(car, row.ClockSinceS, row.ClockClosestM);
        _cars.TookBack(car);
        _bodies.TakeThePoseBack(_town, _town.Roster.AgentOfCar(car));

        // And this tick handed it back as well (car_out.comp), which a decision that moved it on lays it whole from.
        _handedBack[car] = true;
        DecidedByTheTown++;
    }

    public bool DecidesItself(int car) => _cars.DecidesItself(car);

    /// <summary>
    /// <b>Whether the device finds this car's decision itself</b> (<see cref="DeviceCarTick.FindTheDecisionsDue"/>): one the
    /// device ticked last tick and keeps, that the town neither holds nor touched since, that is not done for, and whose
    /// decision only its line and clock answer (<see cref="TownWorld.LeavesItsDecisionToItsLine"/>) — so it is put off as the
    /// town would put it off, and the town is handed it only where it is more than its clock. Never where the verdicts are
    /// checked, or the town decides as it goes.
    /// </summary>
    bool FindsItsOwnDecision(TownWorld town, int car) => WhyTheTownDecides(town, car) is null;

    /// <summary>What of <see cref="FindsItsOwnDecision"/> leaves the car's decision the town's, or null where none does.</summary>
    string? WhyTheTownDecides(TownWorld town, int car) =>
        !DecidesTheCars || !DecidesATickBehind || ChecksTheDecisions ? "the device does not find decisions"
        : !_takenNow.Has(car) || !_tyres.KeptOnTheDevice[car] ? "the device did not keep it"
        : _cars.TownHolds(car) ? "the town holds it"
        : town.TouchedCars.Has(car) ? "the town touched it"
        : town.IsTerminal(town.Roster.AgentOfCar(car)) ? "it is done for"
        : !town.LeavesItsDecisionToItsLine(car) ? "what decides it is not its line alone"
        : null;

    /// <summary>
    /// <b>Whether the cars the device finds the decisions of were let off the decisions' turn the town took last</b>, and
    /// which turn: the town's loop skipped them on it (<see cref="TownWorld.DecideAgent"/>), and the device finds them with
    /// the next tick's sends — so a car let go of before then is put off for the town (<see cref="LetGoOfTheDecision"/>).
    /// None while the agents are held, whose decisions put off are nothing.
    /// </summary>
    long _owedTick = NoneOwed;

    const long NoneOwed = -1;

    /// <summary>
    /// <b>This car's decision the town's again</b>: no longer found on the device, and — where its turn was the one the device
    /// owes — named in <paramref name="putOff"/> for the town to put off.
    /// </summary>
    /// <returns>How many it named: one or none.</returns>
    int LetGoOfTheDecision(TownWorld town, int car, Span<int> putOff)
    {
        if (!_cars.DecidesItself(car)) return 0;

        _cars.LetItDecideItself(car, false);
        var agent = town.Roster.AgentOfCar(car);
        if (_owedTick == NoneOwed || !town.DecisionClock.Turn(_owedTick, agent)) return 0;

        putOff[0] = agent;
        return 1;
    }

    /// <summary>
    /// <b>Every car the host walked let go of</b>, its decision the town's from here on (<see cref="LetGoOfTheDecision"/>) —
    /// whatever the town set of it may have changed what decides it — and, checked, every other the device finds held to
    /// still being one it would (<see cref="DeviceCarTick.ChecksTheVisits"/>).
    /// </summary>
    void LetGoOfTheWalked(TownWorld town)
    {
        var count = 0;
        foreach (var car in _cars.Walked) count += LetGoOfTheDecision(town, car, _letGo.AsSpan(count));
        town.PutOffTheDecisions(_letGo.AsSpan(0, count));
        if (!_cars.ChecksTheVisits) return;

        for (var car = 0; car < town.Cars.Count; car++)
        {
            if (_cars.Visited(car) || !_cars.DecidesItself(car) || WhyTheTownDecides(town, car) is not { } why) continue;

            throw new InvalidOperationException(
                $"Car {car}'s decision was left to the device by a town that changed it since, and did not mark it touched: {why}.");
        }
    }

    /// <summary>Every car's decision the town's from here on, those the device owes put off (<see cref="LetGoOfTheDecision"/>).</summary>
    void LetGoOfEveryDecision(TownWorld town)
    {
        var count = 0;
        for (var car = 0; car < town.Cars.Count; car++) count += LetGoOfTheDecision(town, car, _letGo.AsSpan(count));
        town.PutOffTheDecisions(_letGo.AsSpan(0, count));
    }

    /// <summary>
    /// Of the cars this tick took that the town may read or have set, which the device finds the decisions of from the next
    /// turn on (<see cref="FindsItsOwnDecision"/>), each with what only the town knows of it laid.
    /// </summary>
    void LetTheDecisionsBeFound(TownWorld town, ReadOnlySpan<int> taken)
    {
        foreach (var car in taken)
        {
            var finds = FindsItsOwnDecision(town, car);
            _cars.LetItDecideItself(car, finds);
            if (!finds) continue;

            var (patienceS, byM, queueReachM) = town.ClockTermsOf(car);
            _cars.LayTheDecideTerms(car, new DeviceCarTick.DecideTermsRow(ShapeOfTheRoad(town, car), patienceS, byM, queueReachM));
        }
    }

    /// <summary>
    /// <b>What the device found of the decisions it owed</b>, once the head of the tick is waited for: each car more than its
    /// clock put off for the town in its turn's order, its hold as the readers hold it written into their layer where the
    /// tick asked for the holds — every other its clock alone, run there and counted.
    /// </summary>
    void TakeWhatWasFound(TownWorld town)
    {
        var found = _cars.FoundMore(out var clockAlone);
        town.DecidedByTheClockOnTheDevice(clockAlone);
        DecisionsFound += clockAlone + found.Length;
        if (found.IsEmpty) return;

        if (_asksTheHolds) _townClaims.Rebuild.MirrorTheFoundHolds(town, found, checks: _cars.ChecksTheVisits);
        var agents = _letGo.AsSpan(0, found.Length);
        for (var at = 0; at < found.Length; at++)
        {
            _found[found[at]] = true;
            agents[at] = town.Roster.AgentOfCar(found[at]);
        }

        town.PutOffTheDecisions(agents);
        _foundCount = found.Length;
    }

    /// <summary>How many cars' decisions the device found itself (<see cref="DecidesItself"/>), over every tick.</summary>
    public long DecisionsFound { get; private set; }

    /// <summary>Which cars the device found more than their clock this tick, until the town decides them; and how many.</summary>
    readonly bool[] _found;

    int _foundCount;

    /// <summary>Room for the agents a letting go or a finding puts off.</summary>
    readonly int[] _letGo;

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

    /// <summary>The cars' tick, for a frame that draws them off its tables (<see cref="DeviceCarTick.Drawn"/>).</summary>
    public DeviceCarTick CarTick => _cars;

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

    /// <summary>How many cars the town ticked itself once a claims rebuild that ran on had its answers written back, over every tick.</summary>
    public long CarsPutOff { get; private set; }

    public bool NamesTheCars => true;

    /// <remarks>
    /// Those the town holds or touched, and those added since the last tick laid — every other car the device took last
    /// tick, and the town set nothing of since, so it takes it again (<see cref="DeviceCarTick.ChecksTheVisits"/>). Every car
    /// where the next tick walks every one.
    /// </remarks>
    public ReadOnlySpan<int> CarsTheTownTicks(TownWorld town)
    {
        if (!_swept) HandBackTheLeavers(town);

        var cars = _cars.TheCarsTheTownTicks(town, _townTicks);
        _asked.Clear();
        foreach (var car in cars) _asked.Add(car);
        _named = true;
        return cars;
    }

    public ReadOnlySpan<int> CarsTheTownDecides(TownWorld town) => _cars.TheCarsNotDecidingThemselves(town.Cars.Count, _townTicks);

    /// <summary>Room for the cars the town ticks or decides (<see cref="CarsTheTownTicks"/>, <see cref="CarsTheTownDecides"/>).</summary>
    readonly int[] _townTicks;

    /// <summary>
    /// Which cars this tick's agents' pass was named (<see cref="CarsTheTownTicks"/>), whose answers <see cref="_chosen"/>
    /// holds — none where one not done for was not asked (<see cref="Takes"/>) — and whether it was named any, rather than
    /// asking every agent.
    /// </summary>
    readonly AgentSet _asked;

    bool _named;

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

        if (takes || !_townClaims.Rebuild.AnswersDue) return takes;

        // Its own tick reads what the rebuild running on answered it (TickThePutOff).
        _putOff[_putOffCount++] = car;
        return true;
    }

    /// <summary>
    /// <b>The cars the town ticks itself, put off while a claims rebuild ran on</b> (<see cref="Takes"/>), ticked by the town
    /// once its answers are written back — on them and on the layer before it, as a rebuild waited for leaves them to the
    /// town's turn.
    /// </summary>
    void TickThePutOff(TownWorld town)
    {
        foreach (var car in _putOff.AsSpan(0, _putOffCount)) town.TickTheCar(car);
        CarsPutOff += _putOffCount;
        _putOffCount = 0;
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

    /// <summary>
    /// The cars whose wheels the device keeps and that will not be ticked there next — of those the town holds or touched,
    /// the only ones whose staying it can have changed; or, asked so, every one.
    /// </summary>
    Span<int> Leaving(TownWorld town, bool everyKept)
    {
        var kept = _tyres.KeptOnTheDevice;
        var count = 0;
        var asked = everyKept || town.HoldAgents ? _cars.EveryCar(town.Cars.Count, _maySet) : _cars.TheTownMayHaveSet(town, _maySet);
        foreach (var car in asked)
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

        _bodies.TakeThePoseBack(_town, _town.Roster.AgentOfCar(car));
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

    public void TakeTheWalkerBack(TownWorld town, int person)
    {
        KeptWalkers.TakeBackTheLeft(town, _walkers, person);
        _walkers.TakeTheTickBack(town, person);
        _bodies.TakeThePoseBack(town, person);
    }

    /// <remarks>
    /// The town's own where it holds the car, or moved on from what the device read of it — a wreck, whose grant went with
    /// its action — which a hand-back leaves standing (<see cref="TakeBackOnTheSide"/>).
    /// </remarks>
    public float GrantLeftM(TownWorld town, int car)
    {
        var cars = town.Cars;
        if (_cars.TownHolds(car) || _cars.TheTownMovedOnFrom(cars, car)) return cars.GrantLeftM(car);
        if (!_grantLeftHandedBack[car])
        {
            throw new InvalidOperationException($"Car {car}'s grant left was not handed back: the town did not watch every agent when it was ticked.");
        }

        return _cars.GrantLeftAfter(car);
    }

    public void TakeEveryCarBack(TownWorld town)
    {
        KeptWalkers.TakeBackTheLeft(town, _walkers);
        _walkers.TakeEveryTickBack(town);
        _bodies.TakeEveryPoseBack(town);
        var cars = town.Cars;
        var count = 0;
        for (var car = 0; car < cars.Count; car++)
        {
            if (!_cars.TownHolds(car)) _leaving[count++] = car;
        }

        if (count == 0) return;

        TakeBack(cars, _leaving.AsSpan(0, count));
        TakenBackWhole++;
    }

    public void TakeTheCarBack(TownWorld town, int car)
    {
        if (_cars.TownHolds(car)) return;

        _leaving[0] = car;
        TakeBack(town.Cars, _leaving.AsSpan(0, 1));
    }

    /// <summary>These cars, each left on the device by its last tick, handed over in a submit of their own and taken back as it left them.</summary>
    void TakeBack(CarFleet cars, ReadOnlySpan<int> left)
    {
        var commands = _batch.Begin();
        _vk.BeforeTransfer(commands);
        _cars.RecordTheHandOver(commands, left);
        _batch.SubmitAndWait();
        foreach (var car in left) TakeBackOnTheSide(cars, car);
    }

    /// <summary>How many cars a tick ticked and left on the device, the town not reading them (<see cref="ReadsEveryCarBack"/>), over every tick.</summary>
    public long CarsLeftOnTheDevice { get; private set; }

    /// <summary>
    /// How many cars the host walked to lay what each tick sends, over every tick: those it holds or touched
    /// (<see cref="DeviceCarTick.Walked"/>), or every car.
    /// </summary>
    public long CarsWalked { get; private set; }

    /// <summary>And how many walkers (<see cref="DeviceWalkerTick.Walked"/>).</summary>
    public long WalkersWalked { get; private set; }

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
    /// answers written back once the tick's own submit is behind it, and the cars the town ticks itself ticked on them then
    /// (<see cref="Takes"/>), before the bodies' step is laid and the decisions read them. Off, every rebuild is waited for
    /// where its stages reach the plans.
    /// </summary>
    public bool RebuildsRunOn
    {
        get => _townClaims.Rebuild.AnswersAfterTheTick;
        init => _townClaims.Rebuild.AnswersAfterTheTick = value;
    }

    /// <summary>How many cars a tick handed back left to the town, which had changed their line or what they do since they were sent.</summary>
    public long CarsTheTownMovedOn { get; private set; }

    /// <summary>Whether the town watched every agent as the last tick was laid, so a watch begun or let go walks every car.</summary>
    bool _watchedEveryAgent;

    /// <inheritdoc cref="DeviceCarTick.ChecksTheVisits"/>
    public bool ChecksTheVisits
    {
        get => _cars.ChecksTheVisits;
        init => _cars.ChecksTheVisits = value;
    }

    public void TickTheRest(TownWorld town)
    {
        if (town.HoldAgents)
        {
            // The decisions owed are taken on a held tick, which takes none; the turn about to be taken is owed.
            _owedTick = NoneOwed;
            TickTheRestHeld(town);
            _owedTick = town.AgentsTick;
            return;
        }

        TickTheRestOnTheDevice(town);
        _owedTick = town.AgentsTick;
    }

    void TickTheRestHeld(TownWorld town)
    {
        _townClaims.TakeTheAnswers(town);
        KeptWalkers.TakeBackTheDue(town, _walkers);
        AskTheHoldsOfTheDecisions(town);
        town.TakeTheDecisionsPutOff();

        // Nothing is ticked here, and the bodies step on the town's own impulses.
        if (!StepsWithTheTick) return;

        _bodies.StepBodies(town, _dtS, default, walkersTicked: false);
        _townClaims.Rebuild.LandWhatWasSent(town);
        _townClaims.Rebuild.SendTheLayerBack();
        _swept = false;
    }

    void TickTheRestOnTheDevice(TownWorld town)
    {
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

        // Every car is walked where what each is read or told for changed at once, or the town decides or reads them all.
        if (!DecidesTheCars || laidByTheTown || town.ReadsEveryCarAfterTheTick || town.WatchesEveryAgent != _watchedEveryAgent)
        {
            _cars.VisitsEveryCar = true;
        }

        if (town.WatchesEveryAgent != _watchedEveryAgent) _walkers.VisitsEveryWalker = true;
        _watchedEveryAgent = town.WatchesEveryAgent;
        _count = _cars.Prepare(town, _taken, resident: true, laidByTheTown, chosen, posesInTheTable, _named ? _asked : null);
        CarsWalked += _cars.Walked.Length;
        chosen.Clear();
        _named = false;
        LetGoOfTheWalked(town);
        AskTheHoldsOfWhatTheTownDrives(town);

        // A rebuild laid this tick whose layer lands later leaves the town's readers on the claims before it, which the
        // decisions are asked of — or, where the device has never had them, the town decides every car.
        _cars.ReadersKeepUp = !(laidHere && _townClaims.Rebuild.LandsLater);
        var readersUnknown = !_cars.ReadersKeepUp && !_cars.ReadersKeptUp;
        if (DecidesTheCars) LayTheDecisions(town, readersUnknown, laidByTheTown);
        if (_owedTick != NoneOwed && _cars.DecidingItself > 0)
        {
            _cars.FindTheDecisionsDue(town.DecisionClock, _owedTick, town.Roster.AgentOfCar(0), readersUnknown);
        }

        _owedTick = NoneOwed;
        if (!_cars.ReadsEveryCar) LayTheWanted(town);
        _cars.HandsBackTheGrantsLeft = town.WatchesEveryAgent;
        town.DriveClocks.ForgetTheTouched();
        _walkerCount = _walkers.LayWhatChanged(town);
        WalkersWalked += _walkers.Walked.Length;

        // The walkers' holds as the readers read them are copied this tick where the readers keep up with the claims, off
        // the last rebuild laid here — which a laying of the town's own since leaves behind.
        if (laidHere) _townLaidSinceTheDevice = false;
        if (laidByTheTown) _townLaidSinceTheDevice = true;
        var readersCopied = _cars.ReadersKeepUp && _townClaims.Rebuild.LaidAny && !_townLaidSinceTheDevice;
        if (_cars.ReadersKeepUp) _walkerReadersKnown = readersCopied;
        if (DecidesTheWalkers) LayTheWalkerDecisions(town);
        LayTheWalkersClocks(town);
        if (stepping) _bodies.LayThePosesForTheTick(town);
        var prepared = Stopwatch.GetTimestamp();

        // The head of the tick in a submit of its own — what is sent, and the decisions put off asked of what the last tick
        // left — so the decisions are taken while the rest of it runs.
        var regionsFrom = Vk.RegionsCopied;
        var head = _headBatch.Begin();
        _timer.Reset(head);
        _timer.Stamp(head, 0);
        if (laidHere) _townClaims.RecordTheTickBehind(head);
        if (laidByTheTown) _claims.RecordAsCut(head);
        _timer.Stamp(head, 1);
        KeptWalkers.RecordTheClocksSent(head);
        _cars.RecordTheSends(head, _count, town.Cars.Count);
        SentBytes = _cars.SentBytes;
        _walkers.RecordWhatWasLaid(head, _walkerCount);
        if (laidHere) _townClaims.Rebuild.RecordTheKeptAims(head);
        _vk.BetweenKernels(head);

        // The walkers' decisions put off read each one as the town has it now, its hold as the readers do.
        if (readersCopied && _townClaims.Rebuild.RecordTheWalkersReaders(head)) _vk.BetweenKernels(head);
        KeptWalkers.RecordTheDecisions(head);
        _townClaims.Rebuild.RecordTheHoldsAsked(head, _cars.Stamp, KeptWalkers.DecideStamp, ofTheFound: _asksTheHolds && _cars.FindsTheDecisionsDue);
        _headBatch.Submit();

        var commands = _batch.Begin();
        _vk.AfterTheSubmitsBefore(commands);
        _cars.RecordTheTick(commands);
        _cars.RecordRead(commands);
        _walkers.RecordTheTick(commands);
        _walkers.RecordRead(commands);
        _timer.Stamp(commands, 2);
        _batch.Submit();

        // While the tick runs: this tick's rebuild's answers are written back where it ran on meanwhile, and the cars the
        // town ticks itself, put off for them, are ticked on them.
        _townClaims.TakeTheAnswers(town);
        TickThePutOff(town);

        // The step in a submit of its own behind the tick's, laid with what the town ticked, so the cars and walkers are
        // written back while it runs.
        if (stepping)
        {
            _bodies.LayTheStep(town, _dtS, _taken.AsSpan(0, _count), walkersTicked: true);
            var step = _stepBatch.Begin();
            _vk.AfterTheSubmitsBefore(step);
            _bodies.RecordTheStep(step, town, _dtS);
            _stepBatch.Submit();
        }

        RegionsCopied = (int)(Vk.RegionsCopied - regionsFrom);

        // And a layer the last rebuild sent back is written into the town, and this tick's own rebuild's layer is sent back
        // behind it, to be copied in the host's time before the next tick's submit; and the last tick's decisions are
        // taken, on the layer just landed.
        _townClaims.Rebuild.LandWhatWasSent(town);
        _townClaims.Rebuild.SendTheLayerBack();
        var physics = town.PhysicsForTheDevice;
        var placedFrom = physics.Changes.Length;

        // The walkers' decisions the town takes itself while the device runs; asked of the device, once its verdicts are in.
        var decidingWalkers = Stopwatch.GetTimestamp();
        if (!DecidesTheWalkers) town.TakeTheWalkersDecisionsPutOff();
        var idle = Stopwatch.GetTimestamp();
        _headBatch.Wait();
        IdleMs = Stopwatch.GetElapsedTime(idle).TotalMilliseconds;
        _looks.ReadTimes();
        _townClaims.Rebuild.MirrorTheHoldsAsked(town.ReadersOccupancy);

        // The decisions once the device's verdicts are in, while the tick runs, and before what it left of the cars is
        // written back: the town a tick old either way, and a car a decision moved on from read as the tick had it.
        var deciding = Stopwatch.GetTimestamp();
        TakeWhatWasFound(town);
        town.TakeTheDecisionsPutOff();
        foreach (var car in _decided.AsSpan(0, _decidedCount))
        {
            if (_listed[car]) throw new InvalidOperationException($"Car {car}'s decision was asked of the device and never taken.");
        }

        foreach (var agent in _letGo.AsSpan(0, _foundCount))
        {
            var car = town.Roster.CarIndex(agent);
            if (_found[car]) throw new InvalidOperationException($"Car {car}'s decision was found more than its clock and never taken.");
        }

        _foundCount = 0;

        foreach (var entry in _walkersDue.AsSpan(0, _walkersDueCount))
        {
            var person = (int)(entry & ~DeviceWalkers.ByTheTown);
            if (_walkerListed[person]) throw new InvalidOperationException($"Walker {person}'s decision was asked of the device and never taken.");
        }

        _decidedCount = _walkersDueCount = 0;
        var placedTo = physics.Changes.Length;
        var decided = Stopwatch.GetTimestamp();
        DecidedMs = Stopwatch.GetElapsedTime(decidingWalkers, idle).TotalMilliseconds + Stopwatch.GetElapsedTime(deciding, decided).TotalMilliseconds;
        _batch.Wait();
        IdleMs += Stopwatch.GetElapsedTime(decided).TotalMilliseconds;
        _timer.Read();
        _cars.ReadTimes();
        _walkers.ReadTimes();
        var waited = Stopwatch.GetTimestamp();

        TakeBackTheCars(town);
        TakeBackTheWalkers(town);
        if (stepping)
        {
            var steppedFrom = Stopwatch.GetTimestamp();
            _stepBatch.Wait();
            StepIdleMs = Stopwatch.GetElapsedTime(steppedFrom).TotalMilliseconds;
            _bodies.TakeTheStepBack(town, physics.Changes[placedFrom..placedTo]);
            _swept = false;
        }

        _ticked = !stepping;
        PrepareMs = Stopwatch.GetElapsedTime(began, prepared).TotalMilliseconds;
        WaitMs = Stopwatch.GetElapsedTime(prepared, waited).TotalMilliseconds;
        TakeBackMs = Stopwatch.GetElapsedTime(waited).TotalMilliseconds;
    }

    /// <summary>Whether this tick asks for the holds the town reads (<see cref="DeviceRebuild.AsksForTheHolds"/>).</summary>
    bool _asksTheHolds;

    /// <summary>
    /// <b>Whether this tick asks for the holds the town reads</b> — every layer landing sending back only those
    /// (<see cref="DeviceRebuild.HoldsAsAsked"/>) where the town takes every decision once the device's verdicts are in — and,
    /// where it does, the hold of every driven car the device does not take, which the town's own tick reads next
    /// (<c>Following.Tick</c>). The decisions' holds are asked as they are laid.
    /// </summary>
    void AskTheHoldsOfWhatTheTownDrives(TownWorld town)
    {
        var rebuild = _townClaims.Rebuild;
        rebuild.HoldsAsAsked = DecidesTheCars && DecidesTheWalkers;
        KeptWalkers.LeavesTheUnread = DecidesTheWalkers;
        _asksTheHolds = rebuild.AsksForTheHolds(town.ReadersOccupancy);
        if (!_asksTheHolds) return;

        var cars = town.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            if (cars.Driven[car] && !_cars.Takes(car)) rebuild.AskTheHold(town.PlanHoldOf(car, rebuild.LandsThisTick));
        }
    }

    /// <summary>
    /// <b>While the agents are held, the holds of every decision put off asked for and waited for</b>, before the town takes
    /// them — the layer sent back landed first, which the holds asked for are read off.
    /// </summary>
    void AskTheHoldsOfTheDecisions(TownWorld town)
    {
        var rebuild = _townClaims.Rebuild;
        rebuild.HoldsAsAsked = DecidesTheCars && DecidesTheWalkers;
        if (!rebuild.AsksForTheHolds(town.ReadersOccupancy)) return;

        rebuild.LandWhatWasSent(town);
        var firstCar = town.Roster.AgentOfCar(0);
        foreach (var agent in town.DecisionsPutOff)
        {
            rebuild.AskTheHold(agent < firstCar ? town.WalkHold(agent) : town.PlanHoldOf(town.Roster.CarIndex(agent)));
        }

        var commands = _batch.Begin();
        rebuild.RecordTheHoldsAsked(commands, _cars.Stamp, KeptWalkers.DecideStamp, ofTheFound: false);
        _batch.SubmitAndWait();
        rebuild.MirrorTheHoldsAsked(town.ReadersOccupancy);
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
        _takenNow.CopyFrom(_cars.TakenCars);
        var everyCar = _cars.ReadsEveryCar;
        var events = everyCar ? default : _cars.Events;

        // A car the town neither holds nor touched, whose tick asked nothing of it, is the device's as it was: nothing of it
        // is taken back or set here.
        var taken = _cars.TheTakenToTakeBack(cars.Count, events, _takeBack);
        var nextEvent = 0;
        var handedBackCount = 0;
        foreach (var car in taken)
        {
            _tyres.KeptOnTheDevice[car] = true;
            _grantLeftHandedBack[car] = _cars.HandsBackTheGrantsLeft;

            // A decision taken while the tick ran laid it a line or set it doing something else: what the tick read of the
            // line it was sent stands for nothing now, and the town's own is laid whole next tick.
            _movedOn[car] = _cars.TheTownMovedOnFrom(cars, car);
            if (_movedOn[car]) CarsTheTownMovedOn++;

            var asked = nextEvent < events.Length && events[nextEvent] == car;
            if (asked) nextEvent++;
            if (asked && !_handedBack[car]) HandedBackFor = HandedBackFor with { Asked = HandedBackFor.Asked + 1 };
            _handedBack[car] |= everyCar || asked;
            if (!_handedBack[car])
            {
                _cars.LeftOnTheDevice(car);
                continue;
            }

            handedBackCount++;
            var row = RowOf(car);
            WriteTheTyres(cars, car, row.Car);
            TakeTheClock(town, car, row);
            _cars.TookBack(car);
            if (_movedOn[car]) continue;

            if ((row.Tick.Flags & DeviceCarTick.TickRow.Shifted) != 0) town.ShiftedOnTheDevice(car);

            WriteTheTick(cars, car, row);
        }

        CarsLeftOnTheDevice += _count - handedBackCount;
        foreach (var mark in _tyres.MarksLaid())
        {
            if (!_takenNow.Has((int)(mark.CarWheel / TyreModel.Wheels))) continue;

            town.Marks.Mark(mark.FromM, mark.ToM, mark.WidthM, mark.Intensity, mark.Ploughed != 0u);
        }

        _cars.SettleTheTown(cars, taken);
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

        // Every other car the device took is as it was, and so is whether it finds its decision.
        LetTheDecisionsBeFound(town, taken);
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
        var why = HandedBackFor;

        // Only a car the town holds or touched can be one it decides, laid whole or reads (DeviceCarTick.ChecksTheVisits).
        foreach (var car in _cars.Walked)
        {
            if (!_cars.IsTaken(car)) continue;

            var decidedByTheTown = _decidedByTheTown[car];
            _decidedByTheTown[car] = false;
            var whole = _cars.WasLaidWhole(car);
            if (!whole && !decidedByTheTown && !town.ReadsTheTickOf(car)) continue;

            wanted[car >> 5] |= 1u << (car & 31);
            _handedBack[car] = true;
            if (whole) why.Whole++;
            else if (decidedByTheTown) why.Decided++;
            else if (town.Cars.Action[car] == CarAction.Switch) why.Switching++;
            else if (town.Cars.Action[car] == CarAction.Overtake) why.Overtaking++;
            else if (town.Cars.Action[car] == CarAction.Rejoin) why.Rejoining++;
            else why.Read++;
        }

        HandedBackFor = why;

        wanted.CopyTo(_cars.Wanted);
    }

    /// <summary>Which cars the town reads after this tick, a bit a car, as <see cref="LayTheWanted"/> builds them.</summary>
    readonly uint[] _wanted;

    /// <summary>
    /// How many cars' ticks were handed back for the town to read, over every tick, by why (<see cref="LayTheWanted"/>): laid
    /// whole, decided by the town, read while switching, overtaking or rejoining, read for anything else — and how many
    /// besides whose own tick asked something of the town.
    /// </summary>
    public HandedBackCounts HandedBackFor { get; private set; }

    /// <inheritdoc cref="HandedBackFor"/>
    public record struct HandedBackCounts(
        long Whole, long Decided, long Switching, long Overtaking, long Rejoining, long Read, long Asked);

    /// <summary>
    /// What the last tick handed back of a car: written into the host's memory where the town reads it, or copied back with
    /// every car's (<see cref="ReadsEveryCarBack"/>) — but its grant, which every rebuild handed back then.
    /// </summary>
    DeviceCarTick.OutRow RowOf(int car)
    {
        if (!_cars.ReadsEveryCar) return _cars.RowOut(car);

        var clock = _cars.ClockAfter(car);
        return new DeviceCarTick.OutRow(
            _cars.TicksAfter[car], _cars.CommandsAfter[car], _cars.BriefsAfter[car], _tyres.CarsAfter[car], default, clock.X, clock.Y, 0u);
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

        // Every rebuild hands back every grant where every car is (DeviceRebuild.AnswersOnlyWhatTheTownHolds).
        if (_cars.ReadsEveryCar) return;

        var grant = row.Grant;
        cars.AuthorityM[car] = grant.AuthorityM;
        cars.HorizonM[car] = grant.HorizonM;
        cars.ClaimFromM[car] = grant.ClaimFromM;
        cars.CommittedToM[car] = grant.CommittedToM;
        cars.GrantMarginM[car] = grant.GrantMarginM;
        cars.GrantCutBy[car] = (HeadwayKind)grant.GrantCutBy;
        _cars.TakeTheGrantOf(car, grant.ToGrantRow());
    }

    /// <summary>
    /// A car's own tyre row written back, and — where the rebuild hands back only the cars the town holds — which of its
    /// wheels stand on a way; its wheels are kept on the device until it leaves (<see cref="TakeTheWheelsBack"/>).
    /// </summary>
    void WriteTheTyres(CarFleet cars, int car, in DeviceTyres.CarRow row)
    {
        cars.SlipThrottle[car] = row.SlipThrottle;
        cars.DrivenSlipping[car] = (row.Flags & DeviceTyres.CarRow.DrivenSlipping) != 0;
        if (_cars.ReadsEveryCar) return;

        cars.WheelsOnAWay[car] = row.WheelsOnAWay;
        _cars.KnowTheWheelsOnAWay(car, row.WheelsOnAWay);
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
        var everyWalker = !DecidesTheWalkers || town.ReadsEveryCarAfterTheTick;

        // Of a walker the town neither holds nor touched nothing changed but what the device's tick wrote, which it keeps; of
        // the rest, one the town does not read and has set nothing of since it was laid is left with it (TakeTheWalkerBack).
        foreach (var person in _walkers.TheWalkersTheTownMayHaveSet(_walkerCount, _walkersBack))
        {
            if (!everyWalker && !town.ReadsTheWalker(person) && !town.WalksUnderAHand(person) && _walkers.HoldsAsTheTownHas(town, person))
            {
                _walkers.LeftOnTheDevice(person);
                continue;
            }

            ref readonly var tick = ref ticks[person];
            if (!_walkers.WroteLastTick(tick) || DeviceTownBodies.TheTownsImpulse(town, person)) continue;

            people.HeadingRad[person] = tick.HeadingRad;
            people.DeclaredMps[person] = tick.DeclaredMps;
            town.WalkerImpulseFor(person) = tick.ImpulseNs;
            _walkers.KnowTheTick(person);
        }

        _walkers.CheckTheTick();
    }

    /// <summary>The walkers a take-back walks.</summary>
    readonly int[] _walkersBack;

    /// <remarks>A walker's pace, heading and impulse are taken back with its pose (<see cref="DeviceWalkerTick.TakeTheTickBack"/>).</remarks>
    public void TakeThePoseBack(TownWorld town, int agent)
    {
        if (!town.Roster.IsCar(agent)) _walkers.TakeTheTickBack(town, agent);
        _bodies.TakeThePoseBack(town, agent);
    }

    public void TakeEveryPoseBack(TownWorld town)
    {
        _walkers.TakeEveryTickBack(town);
        _bodies.TakeEveryPoseBack(town);
    }

    /// <remarks>Every decision the device finds is the town's from here on, those it owes put off.</remarks>
    public void LetGo(TownWorld town)
    {
        LetGoOfEveryDecision(town);
        TakeEveryPoseBack(town);
    }

    /// <remarks>
    /// The last laying landed with every hold, and every car the device keeps handed back whole, before the decisions put
    /// off are taken: the town goes on ticking its agents itself, and reads any of them.
    /// </remarks>
    /// <remarks>
    /// <b>Whatever a tick left in flight is waited out first</b> — one that threw between its submit and its wait — and the
    /// device is idle before anything is freed, whatever the taking back threw.
    /// </remarks>
    public void Dispose()
    {
        _headBatch.Wait();
        _batch.Wait();
        _stepBatch.Wait();
        try
        {
            _walkers.TakeEveryTickBack(_town);
            _bodies.TakeEveryPoseBack(_town);
            _townClaims.LandTheLastLaying(_town);
            _townClaims.Rebuild.LandEveryHold(_town.ReadersOccupancy);
            HandBackAlone(_town, Leaving(_town, everyKept: true));
            KeptWalkers.TakeBackTheLeft(_town, _walkers);
            LetGoOfEveryDecision(_town);
            _town.TakeTheDecisionsPutOff();
        }
        finally
        {
            _vk.WaitIdle();
            DisposeTheTables();
        }
    }

    void DisposeTheTables()
    {
        _timer.Dispose();
        _stepBatch.Dispose();
        _headBatch.Dispose();
        _batch.Dispose();
        _looks.Dispose();
        _townClaims.Dispose();
        _bodies.Dispose();
        _walkers.Dispose();
        _cars.Dispose();
        _claims.Dispose();
        _tyres.Dispose();
    }
}
