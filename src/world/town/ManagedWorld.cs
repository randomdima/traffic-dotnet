using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The world layer in managed code</b> (<see cref="WorldLayer"/>): this project's own solver, the tyre model and a
/// walker's footing, stepped on the CPU — for a head with no compute and for the suite.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every figure it keeps is its own</b>: the body table it stood off the laying, the commands and aims it was
/// signalled, the tyres' carry, which car is on whose arm, the motion into the step.
/// </para>
/// <para>
/// <b>It steps beside the actors</b>, as the device's world does: <see cref="Begin"/> takes the signals and hands the
/// step to a thread of its own (<see cref="AsideThread"/>), and <see cref="Finish"/> waits for it — so the actors decide
/// the next tick off the view before it while the bodies move. The step is the same arithmetic in the same order on
/// either thread, so a town steps to the bit as it would at once. Nothing reads the solver while it steps: an instrument
/// handed it (<see cref="Solver"/>) and a retuning wait for the step first, <see cref="InBox"/> answers the whole roster,
/// the counts are the view's, and the marks the tyres lay are written on the ground by <see cref="Finish"/>, since the
/// ground is drawn from while the step runs. A browser has one thread and steps at once.
/// </para>
/// </remarks>
internal sealed class ManagedWorld : WorldLayer
{
    /// <summary>The step begun and not yet waited for, run beside the caller; none where the world steps at once.</summary>
    readonly AsideThread? _aside;

    float _stepDtS;
    WorldView? _stepInto;
    bool _stepReadsCovers;

    /// <summary>The marks the step in flight laid, in the order it laid them, for <see cref="Finish"/> to write on the ground.</summary>
    TyreMark[] _marksLaid = new TyreMark[64];

    int _marksLaidCount;

    readonly PhysicsWorld _physics;
    readonly RibbonAtlas _atlas;
    readonly float _heldAroundAWheelM;
    readonly AgentRoster _roster;

    readonly BodyId[] _carBody;

    /// <summary>Each car's build, copied off the laying's table of them (<see cref="_builds"/>) when laid and when retuned.</summary>
    readonly CarBuild[] _build;

    readonly CarBuilds _builds;
    readonly byte[] _variant;
    WheelGrounds _grounds;
    readonly DriveCommand[] _command;
    readonly bool[] _underAHand;
    readonly TyreCarries _tyres;
    readonly WheelScratch _wheels;
    readonly byte[] _wheelsOnAWay;
    readonly Vector2[] _accelerationMps2;

    /// <summary>
    /// What each car's covers were last read off, the read itself and where it is in <see cref="_carCovers"/> — so a car
    /// standing where it stood keeps them rather than being read again, the same answer to the bit.
    /// </summary>
    readonly CarStands[] _carStoodAt;
    readonly int[] _carCoverCount, _carCoversFrom;

    /// <summary>Every car's covers as last read, in car order, and the read before (swapped each read).</summary>
    WayCover[] _carCovers, _carCoversBefore;

    /// <summary>The same for each walker, read under its centre alone: where it stood (NaN for none), and its covers.</summary>
    readonly Vector2[] _walkerStoodAt;
    readonly int[] _walkerCoverCount, _walkerCoversFrom;
    WayCover[] _walkerCovers, _walkerCoversBefore;

    /// <summary>Counted on each time the builds are laid, which is part of what a car stands as (<see cref="CarStands"/>).</summary>
    int _buildsLaid = 1;

    /// <summary>What a car's covers are a function of, besides the atlas and the builds' figures.</summary>
    /// <remarks>Its equality written out so it inlines: it is asked of every car every step.</remarks>
    readonly record struct CarStands(Vector2 PositionM, Vector2 Forward, byte Channels, int BuildsLaid)
    {
        public bool Equals(CarStands other) =>
            PositionM.Equals(other.PositionM) && Forward.Equals(other.Forward) && Channels == other.Channels
            && BuildsLaid == other.BuildsLaid;

        public override int GetHashCode() => HashCode.Combine(PositionM, Forward, Channels, BuildsLaid);
    }

    /// <summary>
    /// What each tractor's arm holds (EVA-5): the load's agent or −1, by which end, and the link slot the tow is in; and
    /// which ends of each car a fork holds (<see cref="HeldAtTheNose"/>, <see cref="HeldAtTheTail"/>).
    /// </summary>
    readonly int[] _towLoad;
    readonly bool[] _towByTheTail;
    readonly int[] _towSlot;
    readonly byte[] _heldAt;

    const byte HeldAtTheNose = 1, HeldAtTheTail = 2;

    readonly BodyId[] _walkerBody;
    readonly Vector2[] _aimM;
    readonly ushort[] _walkFlags;
    readonly float[] _headingRad;
    readonly Vector2[] _declaredMps;
    readonly bool[] _inside;

    readonly RoadGraph _roads;

    /// <summary>What each car follows as last signalled, and where it was read along it (<see cref="LineReading"/>).</summary>
    readonly LineFollowed[] _followed;

    /// <summary>
    /// What each car's reading along its line and its aim were last worked out from, and what they came to — so a car read
    /// again off all of it as it was, to the bit, keeps the answer rather than walking its lanes again.
    /// </summary>
    /// <remarks>
    /// Nothing else is read: the lanes and joins walked are laid once with the town and never written again, and the
    /// build's figures change only with <see cref="_buildsLaid"/>.
    /// </remarks>
    readonly LineReadOff[] _lineReadOff;

    readonly LineReadAs[] _lineReadAs;

    /// <summary>What a car's line reading and aim are a function of, compared as bits.</summary>
    [StructLayout(LayoutKind.Sequential)]
    struct LineReadOff
    {
        public Vector2 PositionM, Forward;
        public int Lane, Join;
        public float StopsAtM, AroundM, LookaheadM;
        public int BuildsLaid;

        public static bool Same(in LineReadOff a, in LineReadOff b) =>
            MemoryMarshal.AsBytes(new ReadOnlySpan<LineReadOff>(in a))
                .SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<LineReadOff>(in b)));
    }

    readonly record struct LineReadAs(float ProgressM, float OffLineM, float LeadAtM, float AimRad);

    /// <summary>The motion each car and walker carried into the step: what a contact is read against (phase 5).</summary>
    readonly Vector2[] _carIntoMps, _walkerIntoMps;

    /// <summary>
    /// Each rail vehicle's body (TRN-7), the motion it carried into the step, the look it was put down in, its wheels and
    /// weight, the command it was last signalled and where each of its wheels stands on the railway (TRN-9).
    /// </summary>
    readonly BodyId[] _wagonBody;

    /// <summary>Each level crossing's arm's body (RAIL-10), by its approach's gear.</summary>
    readonly BodyId[] _barrierBody;

    readonly Vector2[] _wagonIntoMps;
    readonly ushort[] _wagonLook;
    readonly RailBuild[] _railBuild;
    readonly DriveCommand[] _railCommand;
    readonly RailCursor[] _railCursors;
    readonly WheelImpulse[] _railSpent = new WheelImpulse[TrainVehicle.MostBogies];

    /// <summary>The railway the rail vehicles' wheels stand on, and how each of its switches is set.</summary>
    readonly RailGraph _rails;

    readonly byte[] _switches;
    readonly WorldLaying _laying;

    /// <param name="stepsAside">Whether the step runs beside the caller (<see cref="StepsAsideWherever"/> where unsaid) or at once.</param>
    public ManagedWorld(WorldLaying laying, bool? stepsAside = null) : base(laying)
    {
        if (stepsAside ?? StepsAsideWherever) _aside = new AsideThread(StepTheBodies, "world step");
        _physics = laying.StandTheBodies();
        _atlas = laying.Atlas
                 ?? throw new InvalidOperationException("A managed world reads the host's atlas, and this town's was filed into another world.");
        _grounds = laying.Grounds;
        _heldAroundAWheelM = laying.Config.Road.HeldAroundAWheelM;
        _roster = laying.Roster;
        _builds = laying.Builds;

        var cars = laying.Cars;
        var count = cars.Length;
        _laying = laying;
        _carBody = new BodyId[count];
        _variant = new byte[count];
        _build = new CarBuild[count];
        _command = new DriveCommand[count];
        _underAHand = new bool[count];
        _tyres = new TyreCarries(count);
        _wheels = new WheelScratch(count);
        _wheelsOnAWay = new byte[count];
        _accelerationMps2 = new Vector2[count];
        _towLoad = new int[count];
        _towByTheTail = new bool[count];
        _towSlot = new int[count];
        _heldAt = new byte[count];
        _carIntoMps = new Vector2[count];
        _carStoodAt = new CarStands[count];
        _carCoverCount = new int[count];
        _carCoversFrom = new int[count];
        _carCovers = new WayCover[count * TyreModel.Wheels];
        _carCoversBefore = new WayCover[count * TyreModel.Wheels];
        _roads = laying.Roads;
        _followed = new LineFollowed[count];
        Array.Fill(_followed, LineFollowed.None);
        _lineReadOff = new LineReadOff[count];
        _lineReadAs = new LineReadAs[count];
        Array.Fill(_towLoad, -1);
        Array.Fill(_towSlot, -1);

        for (var car = 0; car < count; car++)
        {
            _carBody[car] = BodyOf(laying, _roster.AgentOfCar(car));
            _variant[car] = cars[car].Variant;
            _build[car] = _builds.Of(_variant[car]);
            _command[car] = DriveCommand.Parked;
            _tyres.Clear(car, cars[car].PositionM);
        }

        var walkers = laying.Walkers;
        var walkerCount = walkers.Length;
        _walkerBody = new BodyId[walkerCount];
        _aimM = new Vector2[walkerCount];
        _walkFlags = new ushort[walkerCount];
        _headingRad = new float[walkerCount];
        _declaredMps = new Vector2[walkerCount];
        _inside = new bool[walkerCount];
        _walkerIntoMps = new Vector2[walkerCount];
        _walkerStoodAt = new Vector2[walkerCount];
        Array.Fill(_walkerStoodAt, new Vector2(float.NaN));
        _walkerCoverCount = new int[walkerCount];
        _walkerCoversFrom = new int[walkerCount];
        // Two ways a walker, as the view keeps room for: one crossing a zebra stands on its paint and the lane under it.
        _walkerCovers = new WayCover[2 * walkerCount];
        _walkerCoversBefore = new WayCover[2 * walkerCount];
        for (var person = 0; person < walkerCount; person++)
        {
            _walkerBody[person] = BodyOf(laying, _roster.AgentOfPerson(person));
            _aimM[person] = walkers[person].PositionM;
            _headingRad[person] = walkers[person].HeadingRad;
            _inside[person] = walkers[person].Inside;
        }

        var wagons = laying.Wagons.Length;
        _wagonBody = new BodyId[wagons];
        _wagonIntoMps = new Vector2[wagons];
        _wagonLook = new ushort[wagons];
        _railBuild = new RailBuild[wagons];
        _railCommand = new DriveCommand[wagons];
        _railCursors = new RailCursor[wagons * TrainVehicle.MostBogies];
        Array.Fill(_railCursors, RailCursor.Nowhere);
        for (var wagon = 0; wagon < wagons; wagon++)
        {
            _wagonBody[wagon] = BodyOf(laying, laying.AgentOfWagon(wagon));
            _railBuild[wagon] = laying.Wagons[wagon].Build;
        }

        _barrierBody = new BodyId[laying.Barriers.Length];
        for (var gear = 0; gear < _barrierBody.Length; gear++) _barrierBody[gear] = BodyOf(laying, laying.AgentOfBarrier(gear));

        _rails = laying.Rails;
        _switches = new byte[Math.Max(1, _rails.SwitchCount)];

        WriteTheView(View);
        ReadCovers(View);
        CountTheBodies();
    }

    static BodyId BodyOf(WorldLaying laying, int agent) => new(laying.BodyOf(agent) + 1);

    /// <summary>
    /// Whether a world steps beside its caller where nothing says: wherever the runtime has a second thread to give it
    /// and a second processor to run it on.
    /// </summary>
    public static bool StepsAsideWherever => !OperatingSystem.IsBrowser() && Environment.ProcessorCount > 1;

    /// <summary>What the step has allocated on its own thread, all told (<see cref="AsideThread.AllocatedBytes"/>) — none where it steps at once.</summary>
    public override long AllocatedAsideBytes => _aside?.AllocatedBytes ?? 0;

    /// <summary>The step in flight waited for, so the solver may be read; its answer is still <see cref="Finish"/>'s to show.</summary>
    void Settle() => _aside?.Wait();

    public override PhysicsWorld Solver
    {
        get
        {
            Settle();
            return _physics;
        }
    }

    public override void FiguresChanged(in WheelGrounds grounds)
    {
        Settle();
        _grounds = grounds;
        for (var car = 0; car < _build.Length; car++) _build[car] = _builds.Of(_variant[car]);
        _buildsLaid++;
    }

    /// <summary>As of the step the <see cref="WorldLayer.View"/> is of, read off the solver as each is shown.</summary>
    public override int IntegratedBodyCount => _integratedSeen;

    /// <inheritdoc cref="IntegratedBodyCount"/>
    public override int FrozenBodyCount => _frozenSeen;

    int _integratedSeen, _frozenSeen;

    void CountTheBodies() => (_integratedSeen, _frozenSeen) = (_physics.IntegratedBodyCount, _physics.FrozenBodyCount);

    /// <remarks>
    /// <b>The whole roster while a step runs beside the caller</b>: the grids are the step's, and what asks — a frame's
    /// sprites — tests every body against its own view anyway, so it does not wait for the step.
    /// </remarks>
    public override int InBox(Vector2 leastM, Vector2 mostM, BodyKind kind, int mostCells, Span<int> into) =>
        _aside is { Running: true } ? -1 : _physics.InBox(leastM, mostM, kind, mostCells, into);

    protected override void TakeTheSignalsAndReadCovers(WorldView into)
    {
        Settle();
        foreach (ref readonly var signal in Signals.Written) Take(signal);

        WriteTheView(into);
        ReadCovers(into);
        CountTheBodies();
    }

    protected override void Finish(WorldView into)
    {
        Settle();
        CountTheBodies();
        foreach (ref readonly var mark in _marksLaid.AsSpan(0, _marksLaidCount))
        {
            Marks.Mark(mark.FromM, mark.ToM, mark.WidthM, mark.Intensity, mark.Ploughed);
        }

        _marksLaidCount = 0;
    }

    /// <summary>The signals taken here and now, and the step run beside the caller, or at once where the world has no thread for it.</summary>
    protected override void Begin(float dtS, WorldView into, bool readCovers)
    {
        foreach (ref readonly var signal in Signals.Written) Take(signal);

        (_stepDtS, _stepInto, _stepReadsCovers) = (dtS, into, readCovers);
        if (_aside is { } aside) aside.Run();
        else StepTheBodies();
    }

    /// <summary>Every body stepped off the signals taken, and the answer written into the view the step was begun for.</summary>
    void StepTheBodies()
    {
        var dtS = _stepDtS;
        var into = _stepInto!;

        // The motion the step begins from, before anything is spent on it: what phase 5 reads a contact's cause off, the
        // bodies holding the solver's answer to it by then.
        for (var car = 0; car < _carBody.Length; car++) _carIntoMps[car] = _physics.VelocityOf(_carBody[car]);
        for (var person = 0; person < _walkerBody.Length; person++)
        {
            _walkerIntoMps[person] = _physics.VelocityOf(_walkerBody[person]);
        }

        for (var wagon = 0; wagon < _wagonBody.Length; wagon++) _wagonIntoMps[wagon] = _physics.VelocityOf(_wagonBody[wagon]);

        for (var person = 0; person < _walkerBody.Length; person++) StepTheWalker(person, dtS);

        for (var car = 0; car < _carBody.Length; car++) StepTheTyres(car);

        for (var wagon = 0; wagon < _wagonBody.Length; wagon++) StepTheRailWheels(wagon);

        _physics.Step(dtS);
        SettleTheLoads(dtS);
        WriteTheView(into, stepped: true);
        WriteTheTouches(into);
        if (_stepReadsCovers) ReadCovers(into);
    }

    public override void Dispose() => _aside?.Dispose();

    void Take(in ControlSignal signal)
    {
        if (signal.Agent >= _roster.Count + _wagonBody.Length)
        {
            TakeForABarrier(signal);
            return;
        }

        if (signal.Agent >= _roster.Count)
        {
            TakeForAWagon(signal);
            return;
        }

        var isCar = _roster.IsCar(signal.Agent);
        var car = isCar ? _roster.CarIndex(signal.Agent) : -1;
        var person = isCar ? -1 : signal.Agent;
        var body = isCar ? _carBody[car] : _walkerBody[person];
        switch (signal.Action)
        {
            case ControlAction.Drive:
                _command[car] = signal.Command;
                _underAHand[car] = (signal.Flags & ControlSignal.UnderAHand) != 0;
                break;

            case ControlAction.Walk:
                _aimM[person] = new Vector2(signal.Params.X, signal.Params.Y);
                _walkFlags[person] = signal.Flags;
                break;

            case ControlAction.Place:
                _physics.Release(body, new Vector2(signal.Params.X, signal.Params.Y), signal.Params.Z);
                if (person >= 0)
                {
                    _inside[person] = false;
                    _headingRad[person] = signal.Params.Z;
                    _declaredMps[person] = Vector2.Zero;
                    _aimM[person] = new Vector2(signal.Params.X, signal.Params.Y);
                }

                break;

            case ControlAction.Contain:
                _physics.Contain(body);
                if (person >= 0)
                {
                    _inside[person] = true;
                    _declaredMps[person] = Vector2.Zero;
                }

                break;

            case ControlAction.Layer:
                _physics.PutOnLayer(body, (CollisionLayer)signal.Flags);
                break;

            case ControlAction.Channels:
                _physics.PutOnChannels(body, (byte)signal.Flags);
                break;

            case ControlAction.Tow:
                Tow(car, signal.Other, (signal.Flags & ControlSignal.ByTheTail) != 0);
                break;

            case ControlAction.ClearWheels:
                _tyres.Clear(car, _physics.PositionOf(body));
                _wheels.Clear(car);
                break;

            case ControlAction.Follow:
                _followed[car] = signal.Followed;
                break;
        }
    }

    /// <summary>What a level crossing's arm is asked (RAIL-10): to be put down across its lanes, or taken out of the world — and nothing else.</summary>
    void TakeForABarrier(in ControlSignal signal)
    {
        var body = _barrierBody[signal.Agent - _roster.Count - _wagonBody.Length];
        if (signal.Action == ControlAction.Place) _physics.Release(body, new Vector2(signal.Params.X, signal.Params.Y), signal.Params.Z);
        else if (signal.Action == ControlAction.Contain) _physics.Contain(body);
    }

    /// <summary>
    /// What a rail vehicle is asked (TRN-7, TRN-9): its pedals; to be put on its run-in moving at its pace, or taken
    /// out of the world; coupled behind the vehicle ahead or not; put on a bridge's channel; a switch set.
    /// </summary>
    void TakeForAWagon(in ControlSignal signal)
    {
        var wagon = signal.Agent - _roster.Count;
        var body = _wagonBody[wagon];
        switch (signal.Action)
        {
            case ControlAction.Drive:
                _railCommand[wagon] = signal.Command;
                break;

            case ControlAction.Place:
                var headingRad = signal.Params.Z;
                _physics.Release(body, new Vector2(signal.Params.X, signal.Params.Y), headingRad, Heading.Unit(headingRad) * signal.Params.W);
                _wagonLook[wagon] = signal.Flags;
                _railCursors.AsSpan(wagon * TrainVehicle.MostBogies, TrainVehicle.MostBogies).Fill(RailCursor.Nowhere);
                break;

            case ControlAction.Contain:
                _physics.Contain(body);
                break;

            case ControlAction.Channels:
                _physics.PutOnChannels(body, (byte)signal.Flags);
                break;

            case ControlAction.Couple:
                CoupleBehind(wagon, signal.Other);
                break;

            case ControlAction.Switch:
                _switches[signal.Other] = (byte)signal.Flags;
                break;
        }
    }

    /// <summary>
    /// <b>A rail vehicle coupled behind the one ahead of it</b>, or uncoupled (SOL-38): the solver's bar from its front
    /// body's end to the rear one of the vehicle ahead, buffers meeting (<see cref="WagonLaid.CoupledBehind"/>) — in its own slot.
    /// </summary>
    void CoupleBehind(int wagon, int aheadAgent)
    {
        var slot = _laying.LinkSlotOfWagon(wagon);
        if (aheadAgent < 0)
        {
            _physics.Unlink(slot);
            return;
        }

        var ahead = aheadAgent - _roster.Count;
        var (aheadM, behindM, restM) = _laying.Wagons[wagon].CoupledBehind(_laying.Wagons[ahead]);
        _physics.Link(slot, _wagonBody[ahead], aheadM, _wagonBody[wagon], behindM, restM);
    }

    /// <summary>
    /// <b>A rail vehicle's wheels</b> (<see cref="RailWheels.Step"/>, TRN-8): each bogie's impulse spent at its pivot, as
    /// a car's tyres are. Nothing for a vehicle out of the world.
    /// </summary>
    void StepTheRailWheels(int wagon)
    {
        var body = _wagonBody[wagon];
        if (!_physics.IsEnabled(body)) return;

        ref readonly var build = ref _railBuild[wagon];
        RailWheels.Step(
            Config, _stepDtS, _rails, _switches, build, _physics.PositionOf(body), _physics.RotationOf(body), _physics.VelocityOf(body),
            _physics.YawRateOf(body), _physics.InverseMassOf(body), _physics.InverseInertiaOf(body), _railCommand[wagon],
            _railCursors.AsSpan(wagon * TrainVehicle.MostBogies, TrainVehicle.MostBogies), _railSpent);
        for (var bogie = 0; bogie < build.Bogies; bogie++) _physics.ApplyImpulseAt(body, _railSpent[bogie].ImpulseNs, _railSpent[bogie].AtM);
    }

    /// <summary>
    /// <b>A tractor's arm put on a body by one end, or emptied</b> (EVA-5): the pin the solver holds between the tractor's
    /// hinge and the load's eye (<see cref="PhysicsWorld.Link"/>), in the slot of the end it holds, and — on a car — that
    /// end's pair lifted. Whatever the arm held before is let go of first.
    /// </summary>
    void Tow(int tractor, int loadAgent, bool byTheTail)
    {
        if (_towLoad[tractor] is var was and >= 0)
        {
            _physics.Unlink(_towSlot[tractor]);
            if (!_laying.IsAWagon(was)) _heldAt[_roster.CarIndex(was)] &= (byte)~HeldEnd(_towByTheTail[tractor]);
        }

        _towLoad[tractor] = loadAgent;
        _towByTheTail[tractor] = byTheTail;
        _towSlot[tractor] = -1;
        if (loadAgent < 0) return;

        var slot = _towSlot[tractor] = _laying.LinkSlotOfTow(loadAgent, byTheTail);
        var isAWagon = _laying.IsAWagon(loadAgent);
        if (!isAWagon) _heldAt[_roster.CarIndex(loadAgent)] |= HeldEnd(byTheTail);

        var (eyeM, _) = _laying.TowedAt(loadAgent, _build[tractor].TowReachM, byTheTail);
        var load = isAWagon ? _wagonBody[_laying.WagonOfAgent(loadAgent)] : _carBody[_roster.CarIndex(loadAgent)];
        _physics.Link(slot, _carBody[tractor], TowBar.HookOnTheBodyM(_build[tractor]), load, eyeM, 0f);

        static byte HeldEnd(bool byTheTail) => byTheTail ? HeldAtTheTail : HeldAtTheNose;
    }

    /// <summary>
    /// <b>A walker's footing</b> (<see cref="WalkerFollower.Step"/>): at its aim where it faces it, holding its stance where
    /// it stands where it does not, on its feet or sliding. Nothing for a body inside a container (PHY-7).
    /// </summary>
    void StepTheWalker(int person, float dtS)
    {
        if (_inside[person]) return;

        var body = _walkerBody[person];
        var flags = _walkFlags[person];
        var positionM = _physics.PositionOf(body);
        var step = WalkerFollower.Step(
            Config, _headingRad[person], positionM, _physics.VelocityOf(body), _declaredMps[person],
            (flags & ControlSignal.Faces) != 0 ? _aimM[person] : positionM, (flags & ControlSignal.Moving) != 0,
            (flags & ControlSignal.OnFeet) != 0, _physics.MassOf(body), dtS);
        _headingRad[person] = step.HeadingRad;
        _declaredMps[person] = step.DesiredMps;
        _physics.ApplyCentralImpulse(body, step.ImpulseNs);
    }

    CarPose PoseOf(int car)
    {
        var body = _carBody[car];
        return new CarPose(
            _physics.PositionOf(body), _physics.HeadingOf(body), _physics.RotationOf(body), _physics.VelocityOf(body),
            _physics.YawRateOf(body), _physics.MassOf(body), _accelerationMps2[car]);
    }

    /// <summary>
    /// <b>A car's four patches</b> (<see cref="TyrePass.Step"/>), and their impulses spent at the wheels — or, on somebody's
    /// arm, the two it rolls on (EVA-5). A car whose pass would change nothing spends nothing (<see cref="TyrePass.Settled"/>).
    /// </summary>
    void StepTheTyres(int car)
    {
        var body = _carBody[car];
        if (_heldAt[car] != 0)
        {
            TrailerWheels(car);
        }
        else
        {
            var carry = _tyres.Of(car);
            if (TyrePass.Settled(_physics.VelocityOf(body), _physics.YawRateOf(body), _command[car], carry))
            {
                _wheels.Clear(car);
                return;
            }

            // Room for every wheel's mark is made first, so the pass writes them where they are kept.
            if (_marksLaidCount + TyreModel.Wheels > _marksLaid.Length) Array.Resize(ref _marksLaid, _marksLaid.Length * 2);

            TyrePass.Step(
                Config, _grounds, _build[car], PoseOf(car), _command[car], _underAHand[car],
                1f / _physics.InverseInertiaOf(body), _wheelsOnAWay[car], carry, _wheels.GroundUnder(car),
                _wheels.ImpulsesOf(car), _wheels.ScrubOf(car), _marksLaid.AsSpan(_marksLaidCount, TyreModel.Wheels),
                out var laid);
            _marksLaidCount += laid;
        }

        var wheels = _wheels.ImpulsesOf(car);
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            _physics.ApplyImpulseAt(body, wheels[wheel].ImpulseNs, wheels[wheel].AtM);
        }
    }

    /// <summary>
    /// <b>The two wheels a towed car is left standing on</b> (EVA-5): the pair at the far end from the fork, the lifted
    /// pair cleared, and the two down rolling with the road under them — or none, on a car held at both ends.
    /// </summary>
    void TrailerWheels(int car)
    {
        var impulses = _wheels.ImpulsesOf(car);
        _wheels.ScrubOf(car).Clear();
        if (_heldAt[car] == (HeldAtTheNose | HeldAtTheTail))
        {
            impulses.Clear();
            return;
        }

        var byTheTail = _heldAt[car] == HeldAtTheTail;
        var down = TowBar.PairOnTheGround(byTheTail);
        var up = TowBar.PairInTheAir(byTheTail);
        impulses.Slice(up, TowBar.Wheels).Clear();

        var pose = PoseOf(car);
        var ground = _wheels.GroundUnder(car);
        Span<Vector2> atM = stackalloc Vector2[TowBar.Wheels];
        TowBar.AxleM(_build[car], pose, down, atM);
        for (var wheel = 0; wheel < TowBar.Wheels; wheel++)
        {
            ground[down + wheel] = _grounds.Under(_wheelsOnAWay[car], down + wheel);
        }

        TowBar.Step(
            _build[car], pose, atM, ground.Slice(down, TowBar.Wheels), Config.Evacuator.OnTheTrailerAxleShare,
            Config.TickSeconds, impulses.Slice(down, TowBar.Wheels));

        var alongMps = Vector2.Dot(pose.VelocityMps, pose.Forward);
        var spin = _tyres.SpinOf(car);
        for (var wheel = 0; wheel < TowBar.Wheels; wheel++) spin[down + wheel] = alongMps;
    }

    /// <summary>
    /// <b>What each car's four patches spent this step, along and across it</b>, lagged as a car settles onto its springs:
    /// bounded by construction, since a tyre cannot push harder than it grips.
    /// </summary>
    /// <remarks>
    /// A car frozen through the step spent nothing on it — an impulse would have woken it — so one whose load is already
    /// nought keeps it, and is passed over.
    /// </remarks>
    void SettleTheLoads(float dtS)
    {
        var settled = TownWorld.LoadSettledShare(Config, dtS);
        var leastMps2 = Config.Tyre.LeastSettledLoadMps2;
        for (var car = 0; car < _carBody.Length; car++)
        {
            var body = _carBody[car];
            if (_accelerationMps2[car] == Vector2.Zero && _physics.FrozenThroughLastStep(body)) continue;

            var wheels = _wheels.ImpulsesOf(car);
            var spentNs = Vector2.Zero;
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++) spentNs += wheels[wheel].ImpulseNs;
            var heldMps2 = spentNs / (_physics.MassOf(body) * dtS);
            var forward = _physics.RotationOf(body);
            var alongAndAcrossMps2 = new Vector2(
                Vector2.Dot(heldMps2, forward), Vector2.Dot(heldMps2, Heading.RightOf(forward)));
            _accelerationMps2[car] = TownWorld.AtLeastALoad(
                Blend.Lerp(_accelerationMps2[car], alongAndAcrossMps2, settled), leastMps2);
        }
    }

    /// <param name="stepped">
    /// Whether it is the step's own view, which reads each car on from where the last one read it; written again off the
    /// clock, it reads around the same place and so answers the same (<see cref="LineFollowed.AroundM"/>).
    /// </param>
    void WriteTheView(WorldView view, bool stepped = false)
    {
        var cars = view.Cars;
        var lookaheadS = Config.Driving.LookaheadS;
        for (var car = 0; car < _carBody.Length; car++)
        {
            var body = _carBody[car];
            ref var row = ref cars[car];
            ref readonly var build = ref _build[car];
            row.PositionM = _physics.PositionOf(body);
            row.VelocityMps = _physics.VelocityOf(body);
            row.HeadingRad = _physics.HeadingOf(body);
            row.YawRateRadPerS = _physics.YawRateOf(body);
            row.Flags = CarView.FlagsOf(
                _physics.IsFrozen(body), _physics.FrozenThroughLastStep(body), _wheelsOnAWay[car], _physics.ChannelsOf(body));
            row.OverlapM = _physics.OverlapOf(body);
            row.Forward = _physics.RotationOf(body);

            ref var followed = ref _followed[car];
            if (stepped && !float.IsNaN(followed.ProgressM)) followed.AroundM = followed.ProgressM;
            row.FollowSerial = followed.Serial;
            var readOff = new LineReadOff
            {
                PositionM = row.PositionM, Forward = row.Forward, Lane = followed.Lane, Join = followed.Join,
                StopsAtM = followed.StopsAtM, AroundM = followed.AroundM, BuildsLaid = _buildsLaid,
                LookaheadM = CarFollower.LookaheadM(build, MathF.Abs(Vector2.Dot(row.VelocityMps, row.Forward)), lookaheadS),
            };
            ref var readAs = ref _lineReadAs[car];
            if (!LineReadOff.Same(readOff, _lineReadOff[car]))
            {
                var rearAxleM = CarFollower.RearAxleM(build, row.PositionM, row.Forward);
                LineReading.Read(_roads, ref followed, rearAxleM, build.ProjectionWindowM);
                var leadM = LineReading.Lead(_roads, followed, readOff.LookaheadM, out var leadAtM);
                var aimRad = float.IsNaN(leadAtM) ? 0f : CarFollower.AimAt(build, rearAxleM, row.Forward, leadM);
                readAs = new LineReadAs(followed.ProgressM, followed.OffLineM, leadAtM, aimRad);
                _lineReadOff[car] = readOff;
            }

            (followed.ProgressM, followed.OffLineM) = (readAs.ProgressM, readAs.OffLineM);
            (row.ProgressM, row.OffLineM, row.LeadAtM, row.AimRad) = readAs;
        }

        var walkers = view.Walkers;
        for (var person = 0; person < _walkerBody.Length; person++)
        {
            var body = _walkerBody[person];
            ref var row = ref walkers[person];
            row.PositionM = _physics.PositionOf(body);
            row.VelocityMps = _physics.VelocityOf(body);
            row.HeadingRad = _headingRad[person];
            row.OverlapM = _physics.OverlapOf(body);
        }

        var wagons = view.Wagons;
        for (var wagon = 0; wagon < _wagonBody.Length; wagon++)
        {
            var body = _wagonBody[wagon];
            ref var row = ref wagons[wagon];
            row.PositionM = _physics.PositionOf(body);
            row.HeadingRad = _physics.HeadingOf(body);
            row.Flags = WagonView.FlagsOf((int)_wagonLook[wagon], _physics.IsEnabled(body), _physics.ChannelsOf(body));
            row.VelocityMps = _physics.VelocityOf(body);
            row.YawRateRadPerS = _physics.YawRateOf(body);
        }

        if (Volatile.Read(ref _keptAtView) is { } kept) KeepTheBodies(kept, view.Slot);
    }

    /// <summary>
    /// <b>The body table as each view was written</b>, by slot (<see cref="WorldView.Slot"/>), once an instrument asks for it
    /// (<see cref="KeepTheBodiesAtTheView"/>): the solver itself runs a step ahead of the view the host shows, and may be
    /// stepping beside it.
    /// </summary>
    KeptBodies[]? _keptAtView;

    /// <summary>How many views to come keep the whole table rather than its moving rows: one a slot, as it is first kept.</summary>
    int _keptOwedWhole;

    void KeepTheBodies(KeptBodies[] kept, int slot)
    {
        kept[slot].Keep(_physics.Table, _keptOwedWhole > 0 ? 0 : kept[slot].FurnitureCount);
        if (_keptOwedWhole > 0) Volatile.Write(ref _keptOwedWhole, _keptOwedWhole - 1);
    }

    /// <summary>
    /// The body table kept as each view is written (<see cref="BodiesAtTheView"/>): from the next view on, or at once where
    /// no step has begun, since the table then stands where the view does.
    /// </summary>
    public override void KeepTheBodiesAtTheView()
    {
        if (_keptAtView is not null) return;

        Settle();
        KeptBodies[] kept = [new KeptBodies(_physics, _laying.FurnitureCount), new KeptBodies(_physics, _laying.FurnitureCount)];
        _keptOwedWhole = kept.Length;
        if (Steps == 0)
        {
            foreach (var slot in kept) slot.Keep(_physics.Table, 0);
            _keptOwedWhole = 0;
        }

        Volatile.Write(ref _keptAtView, kept);
    }

    /// <summary>
    /// <b>The body table as the view the host shows was written</b> — every body of every kind — or nothing, until it has
    /// been kept whole for both views (<see cref="KeepTheBodiesAtTheView"/>).
    /// </summary>
    public KeptBodies? BodiesAtTheView => _keptAtView is { } kept && Volatile.Read(ref _keptOwedWhole) == 0 ? kept[View.Slot] : null;

    /// <summary>Every pair the step began touching, each with the speed it closed at off the motion carried into the step.</summary>
    void WriteTheTouches(WorldView view)
    {
        view.TouchCount = 0;
        foreach (var touch in _physics.BeganTouchingThisStep())
        {
            var closingMps = touch.ClosingMps
                             ?? Vector2.Dot(IntoTheStepMps(touch.First) - IntoTheStepMps(touch.Second), touch.Normal);
            view.AddTouch(touch with { ClosingMps = closingMps });
        }
    }

    Vector2 IntoTheStepMps(BodyTag tag) => tag.Kind switch
    {
        BodyKind.Person => _walkerIntoMps[tag.Index],
        BodyKind.Car => _carIntoMps[tag.Index],
        BodyKind.Train => _wagonIntoMps[tag.Index],
        _ => Vector2.Zero,
    };

    /// <summary>
    /// <b>Every body onto the ribbons it stands on</b> (TER-4c.2): a car under its four wheels on its own channels — which
    /// wheels stand on a way kept for its tyres — and put on the channels of the ways it stands over (PHY-1a); a walker
    /// under its centre as one wheel; a body inside a container on none.
    /// </summary>
    void ReadCovers(WorldView view)
    {
        Span<Vector2> wheelsM = stackalloc Vector2[TyreModel.Wheels];
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        (_carCovers, _carCoversBefore) = (_carCoversBefore, _carCovers);
        var written = 0;
        for (var car = 0; car < _carBody.Length; car++)
        {
            var body = _carBody[car];
            var stands = new CarStands(
                _physics.PositionOf(body), _physics.RotationOf(body), _physics.ChannelsOf(body), _buildsLaid);
            int count;
            if (_carStoodAt[car] == stands)
            {
                count = _carCoverCount[car];
                written = Append(ref _carCovers, written, _carCoversBefore.AsSpan(_carCoversFrom[car], count));
            }
            else
            {
                TyreModel.WheelPointsM(_build[car], PoseOf(car), wheelsM);
                count = _atlas.UnderWheels(wheelsM, _heldAroundAWheelM, under, out _wheelsOnAWay[car], stands.Channels);
                written = Append(ref _carCovers, written, under[..count]);
                _carStoodAt[car] = stands;
                _carCoverCount[car] = count;

                // Only a car read afresh can be put on other channels: one standing where it stood was put on these
                // ways' channels when it was read there, and a body put on others since does not stand where it stood.
                byte channels = 0;
                foreach (ref readonly var cover in under[..count]) channels |= _atlas.ChannelsOf(cover.Way);
                if (channels != 0 && channels != stands.Channels) _physics.PutOnChannels(body, channels);
            }

            var from = written - count;
            _carCoversFrom[car] = from;
            view.CarCoverRanges[car] = new CoverRange(from, count);
            ref var row = ref view.Cars[car];
            row.Flags = (row.Flags & ~0xFFFF00u)
                        | CarView.FlagsOf(false, false, _wheelsOnAWay[car], _physics.ChannelsOf(body));
        }

        if (view.CarCovers.Length < written) view.CarCovers = new WayCover[_carCovers.Length];
        _carCovers.AsSpan(0, written).CopyTo(view.CarCovers);
        view.CarCoverCount = written;
        (_walkerCovers, _walkerCoversBefore) = (_walkerCoversBefore, _walkerCovers);
        written = 0;
        var walkerRadiusM = Config.PersonRadiusM;
        for (var person = 0; person < _walkerBody.Length; person++)
        {
            var count = 0;
            if (_inside[person])
            {
                _walkerStoodAt[person] = new Vector2(float.NaN);
            }
            else if (_physics.PositionOf(_walkerBody[person]) is var atM && atM == _walkerStoodAt[person])
            {
                count = _walkerCoverCount[person];
                written = Append(ref _walkerCovers, written, _walkerCoversBefore.AsSpan(_walkerCoversFrom[person], count));
            }
            else
            {
                count = _atlas.UnderAWheel(atM, walkerRadiusM, under);
                written = Append(ref _walkerCovers, written, under[..count]);
                _walkerStoodAt[person] = atM;
                _walkerCoverCount[person] = count;
            }

            _walkerCoversFrom[person] = written - count;
            view.WalkerCoverRanges[person] = new CoverRange(written - count, count);
        }

        if (view.WalkerCovers.Length < written) view.WalkerCovers = new WayCover[_walkerCovers.Length];
        _walkerCovers.AsSpan(0, written).CopyTo(view.WalkerCovers);
        view.WalkerCoverCount = written;
        view.CoversRead = true;
    }

    static int Append(ref WayCover[] into, int at, ReadOnlySpan<WayCover> covers)
    {
        if (at + covers.Length > into.Length) Array.Resize(ref into, Math.Max(into.Length * 2, at + covers.Length));

        covers.CopyTo(into.AsSpan(at));
        return at + covers.Length;
    }
}
