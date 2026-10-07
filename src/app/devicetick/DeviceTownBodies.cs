using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
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
/// and as many contacts as the step found — written into the fleets as the town's own phase writes them, and into the
/// solver's table, which goes on freezing what has settled and reports what began touching
/// (<see cref="PhysicsWorld.EndTheStepTaken"/>).
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
        _solver = new DeviceSolver(vk, config, physics.Table, room: physics.RoomForBodies);
        _phase = new DeviceBodyPhase(
            vk, config, _solver, tyres, town.People.Capacity + (2 * town.Cars.Capacity), town.People.Capacity);
        if (walkers != null) _phase.TakeTheWalkers(walkers);

        _walkerTick = walkers;
        _batch = new ComputeBatch(vk);
    }

    /// <summary>The solver's step on the device, for whoever asks it to solve in contact order or reads its counts.</summary>
    public DeviceSolver Solver => _solver;

    /// <summary>The phase's kernels, for whoever reads their times.</summary>
    public DeviceBodyPhase Phase => _phase;

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
    /// Of <see cref="HandBackMs"/>, the table's own working out: what the step woke, its freezing and what began touching
    /// (<see cref="PhysicsWorld.EndTheStepTaken"/>), in milliseconds.
    /// </summary>
    public double SettlingMs { get; private set; }

    /// <summary>How many bytes the last step handed back: the totals and the cars, every walker in the world, and the contacts.</summary>
    public ulong ReadBytes { get; private set; }

    /// <summary>Whether the last step laid the table whole.</summary>
    public bool LaidWhole { get; private set; }

    public bool Takes(int agent) => false;

    public void TickTheRest(TownWorld town)
    {
    }

    public bool StepsWithTheTick => false;

    public void StepBodies(TownWorld town, float dtS) => StepBodies(town, dtS, default, walkersTicked: false);

    /// <summary>
    /// <b>One step laid for a submit the agents' tick is recorded into first</b> (<see cref="RecordTheStep"/>): as
    /// <see cref="StepBodies(TownWorld, float, ReadOnlySpan{int}, bool, ReadOnlySpan{int})"/> lays it, but for the walkers'
    /// impulses, which that tick has not handed back yet — a walker's is its row there but where the town's own is the
    /// one that stands: a casualty's slide, a walker under a hand.
    /// </summary>
    public void LayTheStep(TownWorld town, float dtS, ReadOnlySpan<int> tyresStepped, bool walkersTicked)
    {
        var began = Stopwatch.GetTimestamp();
        Lay(town, dtS, tyresStepped, walkersTicked, tickedInThisSubmit: true);
        LayMs = Stopwatch.GetElapsedTime(began).TotalMilliseconds;
    }

    /// <summary>
    /// The step <see cref="LayTheStep"/> laid, recorded after the agents' tick in the caller's submit — whose tyre pass and
    /// walkers' tick the caller has barriered against a copy (<see cref="Vk.BeforeTransfer"/>).
    /// </summary>
    public void RecordTheStep(CommandBuffer commands, TownWorld town, float dtS) =>
        _phase.Record(
            commands, town.Cars.Count, town.People.Count, dtS,
            DeviceBodyPhase.Reads.Poses | DeviceBodyPhase.Reads.Walkers | DeviceBodyPhase.Reads.Contacts);

    /// <summary>What the recorded step left, handed back once the caller's submit has been waited for.</summary>
    public void TakeTheStepBack(TownWorld town)
    {
        var began = Stopwatch.GetTimestamp();
        _phase.ReadTimes();
        HandBack(town);
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
        _phase.Record(
            commands, town.Cars.Count, town.People.Count, dtS,
            DeviceBodyPhase.Reads.Poses | DeviceBodyPhase.Reads.Walkers | DeviceBodyPhase.Reads.Contacts);
        if (!handedOver.IsEmpty) _tyres.RecordHandOver(commands, handedOver);
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
    /// Whether the walkers' tick runs in the step's own submit, so its impulses are not handed back yet and the town's own
    /// are laid exactly where they stand — rather than wherever they differ from what the tick handed back.
    /// </param>
    void Lay(TownWorld town, float dtS, ReadOnlySpan<int> tyresStepped, bool walkersTicked, bool tickedInThisSubmit)
    {
        var physics = town.PhysicsForTheDevice;
        var table = physics.Table;
        var cars = town.Cars;
        LaidWhole = physics.StepsTaken != _stepsTaken || table.Count != _bodiesLaid;
        if (LaidWhole)
        {
            _solver.LayWhole(table);
            _bodiesLaid = table.Count;
            for (var car = 0; car < cars.Count; car++) _tyres.Poses[car] = PoseOf(cars, car);
            _tyres.Table.Poses.Mark(0, cars.Count);
        }
        else
        {
            _solver.LayRows(table, physics.Changes);
            LayThePlacedCars(town, physics);
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
        var ticked = walkersTicked && _walkerTick != null ? _walkerTick.ImpulsesAfter : default;
        for (var person = 0; person < people.Count; person++)
        {
            _phase.LayWalker(person, people.Body[person].Index);
            var impulseNs = town.WalkerImpulseNs(person);
            if (_walkerTick != null)
            {
                var townsOwn = tickedInThisSubmit
                    ? !walkersTicked || TheTownsImpulse(town, person)
                    : person >= ticked.Length || ticked[person] != impulseNs;
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
    /// The pose of every car whose body the town changed since the last step — frozen as the step ended, put down,
    /// reweighed — laid from the fleet into the tyre pass's table, for whatever reads it before the next step.
    /// </summary>
    public void LayThePosesTheTownChanged(TownWorld town) => LayThePlacedCars(town, town.PhysicsForTheDevice);

    /// <summary>The pose of every car whose body the town changed — put down, reweighed — laid again from the fleet, which the town wrote with it.</summary>
    void LayThePlacedCars(TownWorld town, PhysicsWorld physics)
    {
        var cars = town.Cars;
        foreach (var body in physics.Changes)
        {
            var tag = physics.TagOf(new BodyId(body + 1));
            if (tag.Kind != BodyKind.Car || tag.Index < 0 || tag.Index >= cars.Count) continue;

            _tyres.Poses[tag.Index] = PoseOf(cars, tag.Index);
            _tyres.Table.Poses.Mark(tag.Index);
        }
    }

    static DeviceTyres.PoseRow PoseOf(CarFleet cars, int car) => DeviceTyres.PoseRow.Of(new CarPose(
        cars.PositionM[car], cars.HeadingRad[car], cars.VelocityMps[car], cars.YawRateRadPerS[car], cars.MassKg[car],
        cars.AccelerationMps2[car]));

    /// <summary>
    /// <b>What the step left, handed back</b>: every car into the fleet as the town's own phase writes it, every walker
    /// in the world with how far that step took it, every body into the solver's table, and the contacts in the step's
    /// order — after which the table freezes what has settled and says what began touching.
    /// </summary>
    void HandBack(TownWorld town)
    {
        var physics = town.PhysicsForTheDevice;
        var began = Stopwatch.GetTimestamp();
        physics.BeginTheStepTaken();
        RosterMs = Stopwatch.GetElapsedTime(began).TotalMilliseconds;

        var cars = town.Cars;
        var poses = _phase.PosesAfter;
        for (var car = 0; car < cars.Count; car++)
        {
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

        var contacts = _solver.Contacts;
        var normals = _solver.ContactNormals;
        var points = _solver.ContactPoints;
        for (var contact = 0; contact < contacts.Length; contact++)
        {
            var pair = contacts[contact];
            var normal = normals[contact];
            var at = points[contact];
            var manifold = new Manifold
            {
                Normal = new Vector2(normal.X, normal.Y),
                Separation0 = normal.Z,
                Separation1 = normal.W,
                Point0 = new Vector2(at.X, at.Y),
                Point1 = new Vector2(at.Z, at.W),
                PointCount = (int)pair.Z,
            };
            physics.Touched((int)pair.X, (int)pair.Y, manifold);
        }

        var ending = Stopwatch.GetTimestamp();
        physics.EndTheStepTaken();
        SettlingMs = Stopwatch.GetElapsedTime(ending).TotalMilliseconds;
        _stepsTaken = physics.StepsTaken;
        _posesLeftFor = cars.Count;
        TheFrozenStill(town, physics);
        ReadBytes = _phase.ReadBytes + (ulong)(walkers * Unsafe.SizeOf<Vector4>())
                    + (ulong)(contacts.Length * (Unsafe.SizeOf<UInt4>() + (2 * Unsafe.SizeOf<Vector4>())));
    }

    /// <summary>
    /// <b>What froze in the step stands still in the fleets as in the table</b>: the town's own phase reads its fleets
    /// off the table after the freezing, so a body frozen at the end of a step is at exactly no motion there. The rows
    /// changed since the step was laid are exactly those it froze, and are sent with the next.
    /// </summary>
    static void TheFrozenStill(TownWorld town, PhysicsWorld physics)
    {
        foreach (var index in physics.Changes)
        {
            var body = new BodyId(index + 1);
            var tag = physics.TagOf(body);
            switch (tag.Kind)
            {
                case BodyKind.Car:
                    town.Cars.VelocityMps[tag.Index] = physics.VelocityOf(body);
                    town.Cars.YawRateRadPerS[tag.Index] = physics.YawRateOf(body);
                    break;
                case BodyKind.Person:
                    town.People.VelocityMps[tag.Index] = physics.VelocityOf(body);
                    break;
            }
        }
    }

    public void Dispose()
    {
        _batch.Dispose();
        _phase.Dispose();
        _solver.Dispose();
    }
}
