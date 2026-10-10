using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>Phase 4 on the device, in the town's place</b> (<see cref="ITownDevice.StepBodies"/>): what the town changed in
/// its bodies sent, the tick's impulses spent — the tyre pass's and the walker tick's where they ran, the town's own
/// where they did not, and the tow bars — every body stepped, and what the step left handed back into the town's
/// fleets and its solver's table before anything reads them, with the pairs that began touching for phase 5. One
/// submit and one wait (<see cref="DeviceBodyPhase"/>, <see cref="DeviceSolver"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Sent</b>: the first step after the device is set, or after the town has stepped itself, lays the table whole and
/// every car's pose; after that only the rows the town changed (<see cref="PhysicsWorld.Changes"/>) and the poses of the
/// cars whose bodies those are, which body each car and walker is where that changed, and the wheels and pulls the town
/// still makes. <b>Handed back</b>: which frozen bodies the step took in and which it froze, which pairs began touching
/// (<see cref="DeviceSolver.Settles"/>), and the pose of every agent the town holds (<see cref="HandsBackEveryPose"/>) —
/// written into the fleets as the town's own phase writes them, and into the solver's table
/// (<see cref="PhysicsWorld.EndTheStepTaken"/>). <b>Every other pose stays on the device</b>, every car as the step read it
/// off its body and every walker off its own (<see cref="DeviceBodyPhase.Reads.Kept"/>), until it is gathered: with the
/// decisions that read it (<see cref="RecordTheDecided"/>), or alone when something asks (<see cref="TakeThePoseBack"/>). No
/// contact comes back: the rest counts and what was touching are the device's, and how deep each body is into what it
/// touches comes back only where asked (<see cref="ReadsTheOverlap"/>).
/// </para>
/// <para>
/// <b>Set as the town's device on its own, it takes no agent</b> and ticks nothing: the town ticks every agent and the
/// device steps the bodies, which is how its half is held to the town's.
/// </para>
/// </remarks>
internal sealed class DeviceTownBodies : ITownDevice, IDisposable
{
    readonly Vk _vk;
    readonly DeviceTyres _tyres;
    readonly DeviceSolver _solver;
    readonly DeviceBodyPhase _phase;
    readonly ComputeBatch _batch;
    readonly DeviceWalkerTick? _walkerTick;

    /// <summary>
    /// <b>What the town reads of a step's hand-back, gathered into its memory</b> off what the step kept on the device
    /// (<see cref="DeviceBodyPhase.Reads.Kept"/>): the agents it holds once the step is waited for, the decisions' agents in
    /// the head of the tick that takes them (<see cref="RecordTheDecided"/>), and any other the moment it is asked for
    /// (<see cref="PosesFetched"/>) — each row stamped with the step it is of (<see cref="_settledStamp"/>).
    /// </summary>
    readonly GatheredRows<DeviceTyres.PoseRow> _carsRead;

    readonly GatheredRows<DeviceBodyPhase.WalkedRow>? _walkersRead;

    /// <summary>The gathers' slots: the agents a hand-back reads, the decisions' agents, and one asked for alone.</summary>
    const int Held = 0, Decided = 1, Fetched = 2, Slots = 3;

    /// <summary>Each slot's gather bound to each side's hand-back, a side's slots together.</summary>
    readonly DescriptorSet[] _carSets, _walkerSets;

    /// <summary>The gathers of the lists of the decisions the device found more than their clock, bound to each side the first time.</summary>
    DescriptorSet[]? _carsFoundSets, _walkersFoundSets;

    readonly ComputeBatch _gathers;

    /// <summary>The stamp of the last step handed back, which every row gathered of it carries; none before the first.</summary>
    uint _stamp, _settledStamp;

    /// <summary>How many steps the town's table had been through when the device last stepped it, or none yet.</summary>
    int _stepsTaken = -1;

    /// <summary>
    /// The step each car's and each walker's pose in the fleets and the table was taken from (<see cref="_stepsTaken"/>
    /// then), so a pose is taken back once whatever asks for it.
    /// </summary>
    readonly int[] _carPoseAt;

    readonly int[] _walkerPoseAt;

    /// <summary>Which hand-back the last step taken back wrote (<see cref="DeviceBodyPhase.Side"/>), and of how many cars and walkers.</summary>
    int _settledSide = -1;

    int _settledCars;
    int _settledWalkers;

    /// <param name="tyres">The tyre pass whose table holds the cars' poses and the wheels it stepped; its capacity is the fleet's.</param>
    /// <param name="walkers">
    /// The walkers' tick, recorded in an earlier submit, whose impulses the walkers spend where it ticked them — or none,
    /// and every walker's impulse is the town's own.
    /// </param>
    public DeviceTownBodies(Vk vk, SimConfig config, TownWorld town, DeviceTyres tyres, DeviceWalkerTick? walkers = null)
    {
        _tyres = tyres;
        var physics = town.PhysicsForTheDevice;
        physics.KeepTheChanges();
        _solver = new DeviceSolver(vk, config, physics.Table, room: physics.RoomForBodies) { Settles = true };
        _phase = new DeviceBodyPhase(
            vk, config, _solver, tyres, town.People.Capacity + (2 * town.Cars.Capacity), town.People.Capacity);
        if (walkers != null) _phase.TakeTheWalkers(walkers);

        _vk = vk;
        const int SetsAGather = 2 * (Slots + 1);
        _carsRead = new GatheredRows<DeviceTyres.PoseRow>(vk, town.Cars.Capacity, Slots, SetsAGather);
        _carSets = BindTheSlots(_carsRead, _phase.PosesKept(0), _phase.PosesKept(1));
        _walkerSets = [];
        if (_phase.WalkersKept(0) is { } walkersKept)
        {
            _walkersRead = new GatheredRows<DeviceBodyPhase.WalkedRow>(vk, town.People.Capacity, Slots, SetsAGather);
            _walkerSets = BindTheSlots(_walkersRead, walkersKept, _phase.WalkersKept(1)!);
        }

        _gathers = new ComputeBatch(vk);
        _walkerTick = walkers;
        _batch = new ComputeBatch(vk);
        _carPoseAt = new int[town.Cars.Capacity];
        _walkerPoseAt = new int[town.People.Capacity];
        _cars = new int[town.Cars.Capacity];
        _walkers = new int[town.People.Capacity];
    }

    /// <summary>The cars and the walkers a step walks to lay or hand back, in order.</summary>
    readonly int[] _cars, _walkers;

    static Span<int> EveryAgent(int count, Span<int> into)
    {
        for (var agent = 0; agent < count; agent++) into[agent] = agent;
        return into[..count];
    }

    static DescriptorSet[] BindTheSlots<T>(GatheredRows<T> rows, GpuBuffer firstSide, GpuBuffer secondSide) where T : unmanaged
    {
        var sets = new DescriptorSet[2 * Slots];
        for (var slot = 0; slot < Slots; slot++)
        {
            sets[slot] = rows.BindNamed(firstSide, slot);
            sets[Slots + slot] = rows.BindNamed(secondSide, slot);
        }

        return sets;
    }

    /// <summary>A slot's cars and walkers gathered off one side's hand-back, recorded.</summary>
    void RecordTheGather(CommandBuffer commands, int slot, int side, uint stamp)
    {
        _carsRead.Record(commands, _carSets[(side * Slots) + slot], slot, stamp);
        _walkersRead?.Record(commands, _walkerSets[(side * Slots) + slot], slot, stamp);
    }

    /// <summary>A slot's gather in a submit of its own, waited for — behind whatever the queue already holds.</summary>
    void GatherNow(int slot, int side, uint stamp)
    {
        if (_carsRead.NamedIn(slot) == 0 && (_walkersRead?.NamedIn(slot) ?? 0) == 0) return;

        var commands = _gathers.Begin();
        _vk.AfterTheSubmitsBefore(commands);
        RecordTheGather(commands, slot, side, stamp);
        _gathers.SubmitAndWait();
    }

    /// <summary>
    /// <b>The poses the decisions put off read, gathered in the head of the tick that takes them</b> — off the last step's
    /// hand-back, before that tick's own step writes the other side: every car and every walker out of doors put off
    /// (<see cref="TownWorld.DecisionsPutOff"/>), and those the device found more than their clock, off the lists the
    /// decisions recorded before it wrote (<see cref="DeviceCarTick.FoundTables"/>, <see cref="DeviceWalkers.FoundTables"/>).
    /// </summary>
    /// <param name="carsFound">The cars' found lists, where the head finds the decisions due — or none.</param>
    /// <param name="walkersFound">The walkers', likewise.</param>
    public void RecordTheDecided(
        CommandBuffer head, TownWorld town, (GpuBuffer Counts, GpuBuffer More)? carsFound, (GpuBuffer Counts, GpuBuffer More)? walkersFound)
    {
        if (_settledSide < 0 || town.PhysicsForTheDevice.StepsTaken != _stepsTaken) return;

        var firstCar = town.Roster.AgentOfCar(0);
        foreach (var agent in town.DecisionsPutOff)
        {
            if (agent >= firstCar)
            {
                var car = town.Roster.CarIndex(agent);
                if (car < _settledCars) _carsRead.Name(Decided, car);
            }
            else if (agent < _settledWalkers && !town.People.Inside[agent].Any)
            {
                _walkersRead?.Name(Decided, agent);
            }
        }

        RecordTheGather(head, Decided, _settledSide, _settledStamp);
        if (carsFound is { } cars)
        {
            _carsFoundSets ??=
            [
                _carsRead.BindListed(_phase.PosesKept(0), cars.More, cars.Counts), _carsRead.BindListed(_phase.PosesKept(1), cars.More, cars.Counts),
            ];
            _carsRead.Record(head, _carsFoundSets[_settledSide], countAt: 0, _settledCars, _settledStamp);
        }

        if (walkersFound is { } walkers && _walkersRead is { } walkersRead)
        {
            _walkersFoundSets ??=
            [
                walkersRead.BindListed(_phase.WalkersKept(0)!, walkers.More, walkers.Counts),
                walkersRead.BindListed(_phase.WalkersKept(1)!, walkers.More, walkers.Counts),
            ];
            walkersRead.Record(head, _walkersFoundSets[_settledSide], countAt: 0, _settledWalkers, _settledStamp);
        }
    }

    /// <summary>
    /// <b>How many poses were asked for that no gather had named</b> (<see cref="TakeThePoseBack"/>), each gathered in a submit
    /// of its own and waited for — behind any tick or step the queue holds — over every step.
    /// </summary>
    public long PosesFetched { get; private set; }

    /// <summary>
    /// <b>Whether every step also copies every car's and walker's pose into the host's memory</b>
    /// (<see cref="DeviceBodyPhase.PosesAfter"/>, <see cref="DeviceBodyPhase.WalkersAfter"/>) — an instrument's, to hold one
    /// town's device against another's; the town reads none of it.
    /// </summary>
    public bool ReadsEveryPoseToo { get; set; }

    /// <summary>
    /// Whether every pose is written back after every step, where it is not only those the town holds
    /// (<see cref="DeviceCarTick.TownHolds"/>, <see cref="DeviceWalkerTick.TownHolds"/>) — as it is while the town holds its
    /// agents, watches every one or draws off its own copy. A pose left unread is taken back when something asks for it
    /// (<see cref="TakeThePoseBack"/>).
    /// </summary>
    public bool HandsBackEveryPose { get; set; }

    /// <summary>
    /// <b>Whether a pose left unread is written over with nought-over-nought</b> in the fleet, until it is taken back — an
    /// instrument, so that whatever reads a pose the town was not handed reads a figure that cannot pass unnoticed.
    /// </summary>
    public bool PoisonsWhatIsUnread { get; set; }

    bool EveryPoseRead(TownWorld town) =>
        HandsBackEveryPose || town.HoldAgents || town.WatchesEveryAgent || town.ReadsEveryCarAfterTheTick;

    /// <summary>The solver's step on the device, for whoever asks it to solve in contact order or reads its counts.</summary>
    public DeviceSolver Solver => _solver;

    /// <summary>
    /// The car tick whose hand-over the step's submit records for the cars leaving it (<see cref="DeviceCarTick.RecordTheHandOver"/>)
    /// — their rows the tick kept as well as their wheels — or none, and only their wheels are handed back.
    /// </summary>
    public DeviceCarTick? HandsOverThrough { get; init; }

    /// <summary>The phase's kernels, for whoever reads their times.</summary>
    public DeviceBodyPhase Phase => _phase;

    /// <summary>
    /// Whether every step hands back how deep each body was into what it touched (<see cref="PhysicsWorld.OverlapOf"/>) —
    /// for an instrument that asks it of a town the device steps; nothing in a tick does. A town watching every agent
    /// has it handed back whatever this says (<see cref="TownWorld.WatchesEveryAgent"/>).
    /// </summary>
    public bool ReadsTheOverlap { get; set; }

    /// <summary>Whether the step last recorded hands back the overlap.</summary>
    bool _overlapRead;

    DeviceBodyPhase.Reads WhatIsRead(TownWorld town)
    {
        _overlapRead = ReadsTheOverlap || town.WatchesEveryAgent;
        return DeviceBodyPhase.Reads.Kept | (ReadsEveryPoseToo ? DeviceBodyPhase.Reads.Poses | DeviceBodyPhase.Reads.Walkers : 0)
               | (_overlapRead ? DeviceBodyPhase.Reads.Overlap : 0);
    }

    /// <summary>The host's time laying the last step's rows, in milliseconds.</summary>
    public double LayMs { get; private set; }

    /// <summary>The host's time from the last step's submit to its answer, in milliseconds.</summary>
    public double WaitMs { get; private set; }

    /// <summary>The host's time writing the last step's answer into the fleets and the table, in milliseconds.</summary>
    public double HandBackMs { get; private set; }

    /// <summary>
    /// Of <see cref="HandBackMs"/>, the table's step begun: its awake roster taken in and its owners marked
    /// (<see cref="PhysicsWorld.BeginTheStepTaken"/>), in milliseconds. Its indexes are laid when a picture first asks.
    /// </summary>
    public double RosterMs { get; private set; }

    /// <summary>
    /// Of <see cref="HandBackMs"/>, the table taking what the step froze and what began touching
    /// (<see cref="PhysicsWorld.EndTheStepTaken"/>), in milliseconds.
    /// </summary>
    public double SettlingMs { get; private set; }

    /// <summary>
    /// How many bytes the last step handed back: the totals, the poses gathered by name since the step before — the town's
    /// held, the decisions' and those asked for alone — what woke, froze and began touching, and every pose and the overlap
    /// where they were asked for.
    /// </summary>
    public ulong ReadBytes { get; private set; }

    /// <summary>Whether the last step laid the table whole.</summary>
    public bool LaidWhole { get; private set; }

    public bool Takes(int agent) => false;

    public void TickTheRest(TownWorld town)
    {
    }

    public bool StepsWithTheTick => false;

    public bool DecidesATickBehind => false;

    public void StepBodies(TownWorld town, float dtS) => StepBodies(town, dtS, default, walkersTicked: false);

    /// <summary>
    /// <b>The poses the agents' tick reads, for a step submitted behind it</b>, laid before the tick's rows are sent: every
    /// car's where the step lays the table whole, else those whose bodies the town changed since the last step. The rest of
    /// the step is <see cref="LayTheStep"/>'s.
    /// </summary>
    public void LayThePosesForTheTick(TownWorld town)
    {
        var began = Stopwatch.GetTimestamp();
        var physics = town.PhysicsForTheDevice;
        _laysWhole = physics.StepsTaken != _stepsTaken;
        if (_laysWhole) LayEveryPose(town.Cars);
        else _tyres.LayThePlacedPoses(town.Cars, physics);

        LayMs = Stopwatch.GetElapsedTime(began).TotalMilliseconds;
    }

    /// <summary>Whether the step whose poses <see cref="LayThePosesForTheTick"/> laid lays the table whole.</summary>
    bool _laysWhole;

    /// <summary>
    /// <b>One step laid for a submit behind the agents' tick</b> (<see cref="RecordTheStep"/>), its poses laid before it
    /// (<see cref="LayThePosesForTheTick"/>): as <see cref="StepBodies(TownWorld, float, ReadOnlySpan{int}, bool, ReadOnlySpan{int})"/>
    /// lays it, but for the walkers' impulses, which that tick has not handed back yet — a walker's is its row there but
    /// where the town's own is the one that stands: a casualty's slide, a walker under a hand. Laid once the town has ticked
    /// what it ticks itself, every row it changed since the poses is laid with it.
    /// </summary>
    public void LayTheStep(TownWorld town, float dtS, ReadOnlySpan<int> tyresStepped, bool walkersTicked)
    {
        var began = Stopwatch.GetTimestamp();
        Lay(town, dtS, tyresStepped, walkersTicked, tickedInThisSubmit: true);
        LayMs += Stopwatch.GetElapsedTime(began).TotalMilliseconds;
    }

    /// <summary>
    /// The step <see cref="LayTheStep"/> laid, recorded after the agents' tick in the caller's submit — whose tyre pass and
    /// walkers' tick the caller has barriered against a copy (<see cref="Vk.BeforeTransfer"/>).
    /// </summary>
    public void RecordTheStep(CommandBuffer commands, TownWorld town, float dtS)
    {
        _phase.Record(commands, town.Cars.Count, town.People.Count, dtS, WhatIsRead(town));
        Watch?.Record(commands);
    }

    /// <summary>
    /// <b>A watch over every body kept on the device</b>, recorded behind every step and read once the step is waited for —
    /// or none.
    /// </summary>
    public DeviceTownWatch? Watch { get; set; }

    /// <summary>What the recorded step left, handed back once the caller's submit has been waited for.</summary>
    /// <param name="placedMeanwhile">
    /// The table's rows the town changed while the step ran (<see cref="PhysicsWorld.Changes"/>) — a body a decision put
    /// down or put away — whose own pose stands over what the step made of them, and is laid for the next.
    /// </param>
    public void TakeTheStepBack(TownWorld town, ReadOnlySpan<int> placedMeanwhile = default)
    {
        var began = Stopwatch.GetTimestamp();
        _phase.ReadTimes();
        Watch?.Read();
        HandBack(town, placedMeanwhile);
        WaitMs = 0;
        HandBackMs = Stopwatch.GetElapsedTime(began).TotalMilliseconds;
    }

    /// <summary>
    /// <b>One step of the town's bodies</b>, as <see cref="ITownDevice.StepBodies"/>.
    /// </summary>
    /// <param name="tyresStepped">
    /// The cars whose wheels the device's tyre pass left in its table this tick, ascending; every other car's are the
    /// town's own, and are laid from it. A car a decision put on somebody's arm since is one of them: the tick spent its
    /// own wheels, and the trailer's are the town's from the next.
    /// </param>
    /// <param name="walkersTicked">
    /// Whether the walkers' tick ran this tick and handed its impulses back (<see cref="DeviceWalkerTick.ImpulsesAfter"/>).
    /// A walker's impulse is its row there but where the town's own differs — a casualty's slide, a walker under a hand —
    /// which is written over it; where the tick did not run, every walker's is the town's.
    /// </param>
    /// <param name="handedOver">
    /// The cars whose wheels ride the step's submit back to the host (<see cref="DeviceTyres.RecordHandOver"/>).
    /// </param>
    public void StepBodies(
        TownWorld town, float dtS, ReadOnlySpan<int> tyresStepped, bool walkersTicked, ReadOnlySpan<int> handedOver = default)
    {
        var began = Stopwatch.GetTimestamp();
        Lay(town, dtS, tyresStepped, walkersTicked, tickedInThisSubmit: false);
        var laid = Stopwatch.GetTimestamp();

        var commands = _batch.Begin();
        _phase.Record(commands, town.Cars.Count, town.People.Count, dtS, WhatIsRead(town));
        Watch?.Record(commands);
        if (!handedOver.IsEmpty)
        {
            if (HandsOverThrough is { } cars) cars.RecordTheHandOver(commands, handedOver);
            else _tyres.RecordHandOver(commands, handedOver);
        }
        _batch.SubmitAndWait();
        _phase.ReadTimes();
        Watch?.Read();
        var waited = Stopwatch.GetTimestamp();

        HandBack(town);
        LayMs = Stopwatch.GetElapsedTime(began, laid).TotalMilliseconds;
        WaitMs = Stopwatch.GetElapsedTime(laid, waited).TotalMilliseconds;
        HandBackMs = Stopwatch.GetElapsedTime(waited).TotalMilliseconds;
    }

    /// <summary>
    /// What the town changed since the last step, and what it makes this tick: the table's rows, which body each car
    /// and walker is, the cars' poses where their bodies were placed or reweighed, and the impulses the device does not
    /// make itself.
    /// </summary>
    /// <param name="tickedInThisSubmit">
    /// Whether the agents' tick runs ahead of the step, its poses laid (<see cref="LayThePosesForTheTick"/>) and its walkers'
    /// impulses not handed back yet, so the town's own are laid exactly where they stand — rather than wherever they differ
    /// from what the tick handed back.
    /// </param>
    void Lay(TownWorld town, float dtS, ReadOnlySpan<int> tyresStepped, bool walkersTicked, bool tickedInThisSubmit)
    {
        var physics = town.PhysicsForTheDevice;
        var table = physics.Table;
        var cars = town.Cars;
        // Whole only where the table's are the figures that stand: before the first step, or after the town stepped itself.
        // A table grown since is laid the rows added (RowChange.Whole), over the device's own for every other body.
        LaidWhole = (tickedInThisSubmit && _laysWhole) || physics.StepsTaken != _stepsTaken;
        if (LaidWhole)
        {
            _solver.LayWhole(table);
            _solver.LayTheRestCounts(table);
            _solver.LayTheTouching(physics.TouchingKeys);
            if (!tickedInThisSubmit) LayEveryPose(cars);
        }
        else
        {
            _solver.LayRows(table, physics.Changes);
            if (!tickedInThisSubmit) _tyres.LayThePlacedPoses(cars, physics);
        }

        foreach (var body in physics.Changes)
        {
            if (physics.CountsRestAfresh(body)) _solver.CountAfresh(body);
        }

        physics.ForgetTheChanges();

        // A car the device keeps, which the town has set nothing of, is the body, the rest and the wheels it was: only one the
        // town holds or touched is laid (DeviceCarTick.TheCarsToLay) — every one, the table laid whole.
        var stepped = 0;
        var carsLaid = LaidWhole || HandsOverThrough is not { } carTick ? EveryAgent(cars.Count, _cars) : carTick.TheCarsToLay(cars.Count, _cars);
        foreach (var car in carsLaid)
        {
            _phase.LayCar(car, cars.Body[car].Index, town.RestsParked(car));
            while (stepped < tyresStepped.Length && tyresStepped[stepped] < car) stepped++;

            var tyres = stepped < tyresStepped.Length && tyresStepped[stepped] == car;
            if (!tyres) _phase.LayWheels(car, town.WheelImpulsesOf(car));
        }

        var people = town.People;
        var ticked = walkersTicked && _walkerTick != null ? _walkerTick.Count : 0;
        var walkersLaid = LaidWhole || !walkersTicked || _walkerTick is not { Resident: true } walkerTick
            ? EveryAgent(people.Count, _walkers)
            : walkerTick.TheWalkersTheTownMayHaveSet(people.Count, _walkers);
        foreach (var person in walkersLaid)
        {
            _phase.LayWalker(person, people.Body[person].Index);
            var impulseNs = town.WalkerImpulseNs(person);
            if (_walkerTick != null)
            {
                var townsOwn = tickedInThisSubmit
                    ? !walkersTicked || TheTownsImpulse(town, person)
                    : person >= ticked || (_walkerTick.TownHolds(person) && _walkerTick.ImpulseAfter(person) != impulseNs);
                if (townsOwn) _phase.LayWalkerImpulse(person, impulseNs);
                continue;
            }

            if (!people.Inside[person].Any && impulseNs != Vector2.Zero) _phase.LayCentralImpulse(people.Body[person].Index, impulseNs);
        }

        if (!town.AnyOnTheBar) return;

        for (var car = 0; car < cars.Count; car++)
        {
            if (!town.TheBarPulls(car, dtS, out var bar)) continue;

            _phase.LayImpulse(bar.Towed.Index, bar.PullNs, bar.EyeM);
            _phase.LayImpulse(bar.Tractor.Index, -bar.PullNs, bar.HookM);
        }
    }

    /// <summary>
    /// Whether a walker's impulse is the town's own whatever the walkers' tick made of it: a casualty's, which the town's
    /// own phase slides (<c>TownWorld.Settle</c>), and one under a hand, which the town ticks itself.
    /// </summary>
    internal static bool TheTownsImpulse(TownWorld town, int person) =>
        town.WalksUnderAHand(person) || (town.People.Wounded[person] && !town.People.Inside[person].Any);

    /// <summary>How many cars the last step here left their poses in the tyre pass's table for.</summary>
    int _posesLeftFor;

    /// <summary>
    /// <b>Whether the last step left every car's pose in the tyre pass's table</b> (<c>bodies_mirror.comp</c>) and the town
    /// has stepped none since: until the next step, a car's pose there is then the fleet's but where the town changed its
    /// body since (<see cref="LayThePosesTheTownChanged"/>) — and its forward is the body's own rather than the fleet's
    /// heading turned back into one.
    /// </summary>
    public bool PosesLeftInTheTable(TownWorld town) =>
        _stepsTaken == town.PhysicsForTheDevice.StepsTaken && town.Cars.Count <= _posesLeftFor;

    /// <summary>
    /// The pose of every car whose body the town changed since the last step — put down, pushed, reweighed — laid from
    /// the fleet into the tyre pass's table, for whatever reads it before the next step.
    /// </summary>
    public void LayThePosesTheTownChanged(TownWorld town) => _tyres.LayThePlacedPoses(town.Cars, town.PhysicsForTheDevice);

    void LayEveryPose(CarFleet cars)
    {
        for (var car = 0; car < cars.Count; car++) _tyres.Poses[car] = DeviceTyres.PoseRow.Of(cars, car);
        _tyres.Table.Poses.Mark(0, cars.Count);
    }

    /// <summary>
    /// <b>What the step left, handed back</b>: every car the town reads into the fleet as the town's own phase writes it,
    /// every walker it reads in the world with how far that step took it, those bodies into the solver's table, what the
    /// step took in, what it froze and which pairs began touching, in its order. The poses read are gathered off what the
    /// step kept on the device in a submit of their own; every other stays there until something asks for it
    /// (<see cref="TakeThePoseBack"/>).
    /// </summary>
    void HandBack(TownWorld town, ReadOnlySpan<int> placedMeanwhile = default)
    {
        var physics = town.PhysicsForTheDevice;
        var began = Stopwatch.GetTimestamp();
        physics.BeginTheStepTaken();
        RosterMs = Stopwatch.GetElapsedTime(began).TotalMilliseconds;

        if (_heldSent)
        {
            _gathers.Wait();
        }
        else
        {
            NameTheHeld(town, placedMeanwhile);
            GatherNow(Held, _phase.Side, _stamp);
        }

        _heldSent = false;
        var (carsRead, walkers) = (_heldCars, _heldWalkers);
        foreach (var car in _cars.AsSpan(0, carsRead)) TakeTheCar(town, car, Gathered(_phase.Side, car));
        foreach (var person in _walkers.AsSpan(0, walkers)) TakeTheWalker(town, person, GatheredWalker(_phase.Side, person));

        foreach (var body in _solver.Woke) physics.WokeInTheStep(body);

        var ending = Stopwatch.GetTimestamp();
        physics.EndTheStepTaken(_solver.Froze);
        var begun = _solver.Begun;
        foreach (ref readonly var pair in begun) physics.BeganInTheStep((int)pair.First, (int)pair.Second, pair.Normal, pair.ClosingMps);

        if (_overlapRead) physics.TakeTheOverlapM(MemoryMarshal.Cast<uint, float>(_solver.OverlapBits));

        SettlingMs = Stopwatch.GetElapsedTime(ending).TotalMilliseconds;
        _stepsTaken = physics.StepsTaken;
        _posesLeftFor = town.Cars.Count;
        (_settledSide, _settledCars, _settledWalkers, _settledStamp) = (_phase.Side, town.Cars.Count, _phase.WalkersHanded, _stamp);
        CarsRead = carsRead;
        WalkersRead = walkers;
        var gathered = _carsRead.GatheredBytes + (_walkersRead?.GatheredBytes ?? 0);
        _carsRead.GatheredBytes = 0;
        if (_walkersRead is { } walkersRead) walkersRead.GatheredBytes = 0;
        ReadBytes = _phase.ReadBytes + gathered + (ulong)((2 + _solver.Woke.Length + _solver.Froze.Length) * sizeof(uint))
                    + (ulong)(begun.Length * Unsafe.SizeOf<DeviceSolver.BegunRow>());
    }

    /// <summary>
    /// <b>The poses the step's hand-back reads, gathered behind the step</b> in a submit of their own sent while it still runs,
    /// so the host waits for both at once (<see cref="TakeTheStepBack"/>): the agents the town holds once its tick is taken
    /// back, which nothing may change before the step is.
    /// </summary>
    public void GatherTheHeld(TownWorld town, ReadOnlySpan<int> placedMeanwhile)
    {
        NameTheHeld(town, placedMeanwhile);
        _heldSent = true;
        if (_carsRead.NamedIn(Held) == 0 && (_walkersRead?.NamedIn(Held) ?? 0) == 0) return;

        var commands = _gathers.Begin();
        _vk.AfterTheSubmitsBefore(commands);
        RecordTheGather(commands, Held, _phase.Side, _stamp);
        _gathers.Submit();
    }

    /// <summary>Whether the hand-back's gather was sent ahead of it (<see cref="GatherTheHeld"/>).</summary>
    bool _heldSent;

    /// <summary>How many cars and walkers the hand-back reads, named first in <see cref="_cars"/> and <see cref="_walkers"/>.</summary>
    int _heldCars, _heldWalkers;

    /// <summary>
    /// The cars and walkers the step's hand-back reads, named to be gathered under the step's own stamp: every one the town
    /// holds (<see cref="DeviceCarTick.TownHolds"/>, <see cref="DeviceWalkerTick.TownHolds"/>), or every one where every
    /// pose is read; every other is left where the step kept it.
    /// </summary>
    void NameTheHeld(TownWorld town, ReadOnlySpan<int> placedMeanwhile)
    {
        var every = EveryPoseRead(town);
        var cars = town.Cars;
        var carsRead = 0;
        var carsHeld = every || PoisonsWhatIsUnread || HandsOverThrough is not { } carTick
            ? EveryAgent(cars.Count, _cars)
            : carTick.TheCarsTheTownHolds(cars.Count, _cars);
        foreach (var car in carsHeld)
        {
            if (!placedMeanwhile.IsEmpty && placedMeanwhile.Contains(cars.Body[car].Index)) continue;
            if (!every && HandsOverThrough is { } holds && !holds.TownHolds(car))
            {
                if (PoisonsWhatIsUnread) Poison(cars, car);
                continue;
            }

            _cars[carsRead++] = car;
            _carsRead.Name(Held, car);
        }

        var people = town.People;
        var walkersHanded = _phase.WalkersHanded;
        var walkers = 0;
        var walkersHeld = every || PoisonsWhatIsUnread || _walkerTick is not { Resident: true } walkerTick
            ? EveryAgent(walkersHanded, _walkers)
            : walkerTick.TheWalkersTheTownHolds(walkersHanded, _walkers);
        foreach (var person in walkersHeld)
        {
            if (people.Inside[person].Any) continue;
            if (!placedMeanwhile.IsEmpty && placedMeanwhile.Contains(people.Body[person].Index)) continue;
            if (!every && _walkerTick is { Resident: true } held && !held.TownHolds(person))
            {
                if (PoisonsWhatIsUnread) people.PositionM[person] = people.VelocityMps[person] = new Vector2(float.NaN);
                continue;
            }

            _walkers[walkers++] = person;
            _walkersRead!.Name(Held, person);
        }

        (_heldCars, _heldWalkers) = (carsRead, walkers);
        _stamp++;
    }

    /// <summary>How many cars' and walkers' poses the last step wrote into the fleets and the table (<see cref="HandsBackEveryPose"/>).</summary>
    public int CarsRead { get; private set; }

    /// <inheritdoc cref="CarsRead"/>
    public int WalkersRead { get; private set; }

    /// <summary>A car's pose as a step left it, written into the fleet as the town's own phase writes it and into the table.</summary>
    void TakeTheCar(TownWorld town, int car, in DeviceTyres.PoseRow row)
    {
        var cars = town.Cars;
        cars.PositionM[car] = row.PositionM;
        cars.HeadingRad[car] = row.HeadingRad;
        cars.VelocityMps[car] = row.VelocityMps;
        cars.YawRateRadPerS[car] = row.YawRateRadPerS;
        cars.AccelerationMps2[car] = row.AccelerationMps2;
        town.PhysicsForTheDevice.Moved(cars.Body[car], row.PositionM, row.Forward, row.HeadingRad, row.VelocityMps, row.YawRateRadPerS);
        _carPoseAt[car] = town.PhysicsForTheDevice.StepsTaken;
    }

    /// <summary>
    /// A walker as a step left its body, written into the fleet with how far it has walked and into the table — and taken
    /// as what the walkers' tick holds of it, so it is not laid back over the table.
    /// </summary>
    void TakeTheWalker(TownWorld town, int person, in DeviceBodyPhase.WalkedRow row)
    {
        var people = town.People;
        var wasM = people.PositionM[person];
        people.PositionM[person] = row.PositionM;
        people.VelocityMps[person] = row.VelocityMps;
        people.DistanceWalkedM[person] = _walkerTick != null ? row.DistanceWalkedM : people.DistanceWalkedM[person] + (row.PositionM - wasM).Length();
        town.PhysicsForTheDevice.Moved(people.Body[person], row.PositionM, row.VelocityMps);
        _walkerTick?.KnowTheBody(person, row.PositionM, row.VelocityMps);
        _walkerPoseAt[person] = town.PhysicsForTheDevice.StepsTaken;
    }

    /// <summary>
    /// <b>An agent's pose as the device's last step left it</b>, written into the fleet and the table where they hold an
    /// older one (<see cref="ITownDevice.TakeThePoseBack"/>) — off the hand-back that step kept, which the step after leaves
    /// alone, so it may be asked while that one runs: as a gather left it, or gathered alone where none named it
    /// (<see cref="PosesFetched"/>). A body the town put down since keeps its own.
    /// </summary>
    public void TakeThePoseBack(TownWorld town, int agent)
    {
        if (!Unread(town, agent)) return;

        if (town.Roster.IsCar(agent))
        {
            var car = town.Roster.CarIndex(agent);
            if (!_carsRead.Holds(car, _settledStamp)) Fetch(_carsRead, car);
            TakeTheCar(town, car, Gathered(_settledSide, car));
            return;
        }

        if (!_walkersRead!.Holds(agent, _settledStamp)) Fetch(_walkersRead, agent);
        TakeTheWalker(town, agent, GatheredWalker(_settledSide, agent));
    }

    /// <summary>A car's gathered pose, held to the whole read of the same side where every pose is read too (<see cref="GathersUnlike"/>).</summary>
    DeviceTyres.PoseRow Gathered(int side, int car)
    {
        var row = _carsRead.Row(car);
        if (ReadsEveryPoseToo && row != _phase.PosesOn(side, car + 1)[car]) GathersUnlike++;
        return row;
    }

    /// <inheritdoc cref="Gathered"/>
    DeviceBodyPhase.WalkedRow GatheredWalker(int side, int person)
    {
        var row = _walkersRead!.Row(person);
        if (ReadsEveryPoseToo && row != _phase.WalkersOn(side, person + 1)[person]) GathersUnlike++;
        return row;
    }

    /// <summary>
    /// <b>How many poses the town took off a gather other than the whole read of the same step holds them</b>, where every pose
    /// is read too (<see cref="ReadsEveryPoseToo"/>), over every step: what holds a gather to the side and step it stands for.
    /// </summary>
    public long GathersUnlike { get; private set; }

    /// <summary>These agents' poses taken back as <see cref="TakeThePoseBack"/> takes each, those no gather named gathered at once.</summary>
    public void TakeThePosesBack(TownWorld town, ReadOnlySpan<int> agents)
    {
        foreach (var agent in agents)
        {
            if (!Unread(town, agent)) continue;

            if (!town.Roster.IsCar(agent))
            {
                if (!_walkersRead!.Holds(agent, _settledStamp)) _walkersRead.Name(Fetched, agent);
            }
            else if (town.Roster.CarIndex(agent) is var car && !_carsRead.Holds(car, _settledStamp))
            {
                _carsRead.Name(Fetched, car);
            }
        }

        GatherNow(Fetched, _settledSide, _settledStamp);
        foreach (var agent in agents) TakeThePoseBack(town, agent);
    }

    /// <summary>
    /// Whether an agent's pose in the fleets and the table is older than the last step taken back left it, and that step's
    /// is the one to take: the town has stepped none since, has not put its body down since, and it is out of doors.
    /// </summary>
    bool Unread(TownWorld town, int agent)
    {
        var physics = town.PhysicsForTheDevice;
        if (_settledSide < 0 || physics.StepsTaken != _stepsTaken) return false;

        if (town.Roster.IsCar(agent))
        {
            var car = town.Roster.CarIndex(agent);
            return car < _settledCars && _carPoseAt[car] != _stepsTaken && !PutDownSince(physics, town.Cars.Body[car]);
        }

        var people = town.People;
        return agent < _settledWalkers && _walkerPoseAt[agent] != _stepsTaken && !people.Inside[agent].Any
               && !PutDownSince(physics, people.Body[agent]);
    }

    void Fetch<T>(GatheredRows<T> rows, int item) where T : unmanaged
    {
        rows.Name(Fetched, item);
        GatherNow(Fetched, _settledSide, _settledStamp);
        PosesFetched++;
    }

    /// <summary>
    /// Every agent's pose as the device's last step left it, written where the fleets and the table hold an older one —
    /// those no gather named gathered at once.
    /// </summary>
    public void TakeEveryPoseBack(TownWorld town)
    {
        if (_settledSide >= 0 && town.PhysicsForTheDevice.StepsTaken == _stepsTaken)
        {
            for (var car = 0; car < _settledCars; car++)
            {
                if (!_carsRead.Holds(car, _settledStamp)) _carsRead.Name(Fetched, car);
            }

            for (var person = 0; person < _settledWalkers; person++)
            {
                if (!_walkersRead!.Holds(person, _settledStamp)) _walkersRead.Name(Fetched, person);
            }

            GatherNow(Fetched, _settledSide, _settledStamp);
        }

        for (var car = 0; car < town.Cars.Count; car++) TakeThePoseBack(town, town.Roster.AgentOfCar(car));
        for (var person = 0; person < town.People.Count; person++) TakeThePoseBack(town, person);
    }

    static bool PutDownSince(PhysicsWorld physics, BodyId body) => (physics.ChangeOf(body.Index) & RowChange.Pose) != 0;

    static void Poison(CarFleet cars, int car)
    {
        cars.PositionM[car] = cars.VelocityMps[car] = cars.AccelerationMps2[car] = new Vector2(float.NaN);
        cars.HeadingRad[car] = cars.YawRateRadPerS[car] = float.NaN;
    }

    public void Dispose()
    {
        _batch.Dispose();
        _gathers.Dispose();
        _carsRead.Dispose();
        _walkersRead?.Dispose();
        _phase.Dispose();
        _solver.Dispose();
    }
}
