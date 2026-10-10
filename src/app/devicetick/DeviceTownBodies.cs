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
/// still makes. <b>Handed back</b>: every car as the step read it off its body, every walker in the world off its own,
/// which frozen bodies it took in and which it froze, and which pairs began touching (<see cref="DeviceSolver.Settles"/>)
/// — written into the fleets as the town's own phase writes them, and into the solver's table
/// (<see cref="PhysicsWorld.EndTheStepTaken"/>). No contact comes back: the rest counts and what was touching are the
/// device's, and how deep each body is into what it touches comes back only where asked (<see cref="ReadsTheOverlap"/>).
/// </para>
/// <para>
/// <b>Set as the town's device on its own, it takes no agent</b> and ticks nothing: the town ticks every agent and the
/// device steps the bodies, which is how its half is held to the town's.
/// </para>
/// </remarks>
internal sealed class DeviceTownBodies : ITownDevice, IDisposable
{
    readonly DeviceTyres _tyres;
    readonly DeviceSolver _solver;
    readonly DeviceBodyPhase _phase;
    readonly ComputeBatch _batch;
    readonly DeviceWalkerTick? _walkerTick;

    /// <summary>How many steps the town's table had been through when the device last stepped it, or none yet.</summary>
    int _stepsTaken = -1;

    /// <summary>How many bodies the table held when it was last laid whole.</summary>
    int _bodiesLaid;

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

        _walkerTick = walkers;
        _batch = new ComputeBatch(vk);
    }

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
        return DeviceBodyPhase.Reads.Poses | DeviceBodyPhase.Reads.Walkers | (_overlapRead ? DeviceBodyPhase.Reads.Overlap : 0);
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
    /// How many bytes the last step handed back: the totals and the cars, every walker in the world, what woke, froze and
    /// began touching, and the overlap where it was asked for.
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
        _laysWhole = physics.StepsTaken != _stepsTaken || physics.Table.Count != _bodiesLaid;
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
    public void RecordTheStep(CommandBuffer commands, TownWorld town, float dtS) =>
        _phase.Record(commands, town.Cars.Count, town.People.Count, dtS, WhatIsRead(town));

    /// <summary>What the recorded step left, handed back once the caller's submit has been waited for.</summary>
    /// <param name="placedMeanwhile">
    /// The table's rows the town changed while the step ran (<see cref="PhysicsWorld.Changes"/>) — a body a decision put
    /// down or put away — whose own pose stands over what the step made of them, and is laid for the next.
    /// </param>
    public void TakeTheStepBack(TownWorld town, ReadOnlySpan<int> placedMeanwhile = default)
    {
        var began = Stopwatch.GetTimestamp();
        _phase.ReadTimes();
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
        if (!handedOver.IsEmpty)
        {
            if (HandsOverThrough is { } cars) cars.RecordTheHandOver(commands, handedOver);
            else _tyres.RecordHandOver(commands, handedOver);
        }
        _batch.SubmitAndWait();
        _phase.ReadTimes();
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
        LaidWhole = (tickedInThisSubmit && _laysWhole) || physics.StepsTaken != _stepsTaken || table.Count != _bodiesLaid;
        if (LaidWhole)
        {
            _solver.LayWhole(table);
            if (physics.StepsTaken != _stepsTaken)
            {
                _solver.LayTheRestCounts(table);
                _solver.LayTheTouching(physics.TouchingKeys);
            }

            _bodiesLaid = table.Count;
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

        var stepped = 0;
        for (var car = 0; car < cars.Count; car++)
        {
            _phase.LayCar(car, cars.Body[car].Index, town.RestsParked(car));
            while (stepped < tyresStepped.Length && tyresStepped[stepped] < car) stepped++;

            var tyres = stepped < tyresStepped.Length && tyresStepped[stepped] == car;
            if (!tyres) _phase.LayWheels(car, town.WheelImpulsesOf(car));
        }

        var people = town.People;
        var ticked = walkersTicked && _walkerTick != null ? _walkerTick.Count : 0;
        for (var person = 0; person < people.Count; person++)
        {
            _phase.LayWalker(person, people.Body[person].Index);
            var impulseNs = town.WalkerImpulseNs(person);
            if (_walkerTick != null)
            {
                var townsOwn = tickedInThisSubmit
                    ? !walkersTicked || TheTownsImpulse(town, person)
                    : person >= ticked || _walkerTick.ImpulseAfter(person) != impulseNs;
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
    /// <b>What the step left, handed back</b>: every car into the fleet as the town's own phase writes it, every walker
    /// in the world with how far that step took it, every body into the solver's table, what the step took in, what it
    /// froze and which pairs began touching, in its order.
    /// </summary>
    void HandBack(TownWorld town, ReadOnlySpan<int> placedMeanwhile = default)
    {
        var physics = town.PhysicsForTheDevice;
        var began = Stopwatch.GetTimestamp();
        physics.BeginTheStepTaken();
        RosterMs = Stopwatch.GetElapsedTime(began).TotalMilliseconds;

        var cars = town.Cars;
        var poses = _phase.PosesAfter;
        for (var car = 0; car < cars.Count; car++)
        {
            if (!placedMeanwhile.IsEmpty && placedMeanwhile.Contains(cars.Body[car].Index)) continue;

            ref readonly var row = ref poses[car];
            cars.PositionM[car] = row.PositionM;
            cars.HeadingRad[car] = row.HeadingRad;
            cars.VelocityMps[car] = row.VelocityMps;
            cars.YawRateRadPerS[car] = row.YawRateRadPerS;
            cars.AccelerationMps2[car] = row.AccelerationMps2;
            physics.Moved(cars.Body[car], row.PositionM, row.Forward, row.HeadingRad, row.VelocityMps, row.YawRateRadPerS);
        }

        var people = town.People;
        var walked = _phase.WalkersAfter;
        var walkers = 0;
        for (var person = 0; person < people.Count; person++)
        {
            if (people.Inside[person].Any) continue;
            if (!placedMeanwhile.IsEmpty && placedMeanwhile.Contains(people.Body[person].Index)) continue;

            var row = walked[person];
            var nowM = new Vector2(row.X, row.Y);
            var velocityMps = new Vector2(row.Z, row.W);
            var wasM = people.PositionM[person];
            people.PositionM[person] = nowM;
            people.VelocityMps[person] = velocityMps;
            people.DistanceWalkedM[person] += (nowM - wasM).Length();
            physics.Moved(people.Body[person], nowM, velocityMps);
            walkers++;
        }

        foreach (var body in _solver.Woke) physics.WokeInTheStep(body);

        var ending = Stopwatch.GetTimestamp();
        physics.EndTheStepTaken(_solver.Froze);
        var begun = _solver.Begun;
        foreach (var pair in begun)
        {
            physics.BeganInTheStep(
                (int)pair.X, (int)pair.Y, new Vector2(BitConverter.UInt32BitsToSingle(pair.Z), BitConverter.UInt32BitsToSingle(pair.W)));
        }

        if (_overlapRead) physics.TakeTheOverlapM(MemoryMarshal.Cast<uint, float>(_solver.OverlapBits));

        SettlingMs = Stopwatch.GetElapsedTime(ending).TotalMilliseconds;
        _stepsTaken = physics.StepsTaken;
        _posesLeftFor = cars.Count;
        ReadBytes = _phase.ReadBytes + (ulong)(walkers * Unsafe.SizeOf<Vector4>())
                    + (ulong)((2 + _solver.Woke.Length + _solver.Froze.Length) * sizeof(uint))
                    + (ulong)(begun.Length * Unsafe.SizeOf<UInt4>());
    }

    public void Dispose()
    {
        _batch.Dispose();
        _phase.Dispose();
        _solver.Dispose();
    }
}
