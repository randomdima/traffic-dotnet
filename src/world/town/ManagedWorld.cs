using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The world layer in managed code</b> (<see cref="WorldLayer"/>): this project's own solver, the tyre model and a
/// walker's footing, stepped on the CPU — for a head with no compute and for the suite.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every figure it keeps is its own</b>: the body table it stood off the laying, the commands and aims it was
/// signalled, the tyres' carry, the tow bars, the motion into the step.
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
    readonly record struct CarStands(Vector2 PositionM, Vector2 Forward, byte Channels, int BuildsLaid);

    /// <summary>The car each tractor tows, the tractor each towed car hangs off, or −1; and which end it is held by (EVA-5).</summary>
    readonly int[] _towing, _onTheHookOf;
    readonly bool[] _byTheTail;
    int _onTheBar;

    readonly BodyId[] _walkerBody;
    readonly Vector2[] _aimM;
    readonly ushort[] _walkFlags;
    readonly float[] _headingRad;
    readonly Vector2[] _declaredMps;
    readonly bool[] _inside;

    readonly RoadGraph _roads;

    /// <summary>What each car follows as last signalled, and where it was read along it (<see cref="LineReading"/>).</summary>
    readonly LineFollowed[] _followed;

    /// <summary>The motion each car and walker carried into the step: what a contact is read against (phase 5).</summary>
    readonly Vector2[] _carIntoMps, _walkerIntoMps;

    /// <param name="stepsAside">Whether the step runs beside the caller (<see cref="StepsAsideWherever"/> where unsaid) or at once.</param>
    public ManagedWorld(WorldLaying laying, bool? stepsAside = null) : base(laying)
    {
        if (stepsAside ?? StepsAsideWherever) _aside = new AsideThread(StepTheBodies, "world step");
        _physics = laying.StandTheBodies();
        _atlas = laying.Atlas;
        _grounds = laying.Grounds;
        _heldAroundAWheelM = laying.Config.Road.HeldAroundAWheelM;
        _roster = laying.Roster;
        _builds = laying.Builds;

        var cars = laying.Cars;
        var count = cars.Length;
        _carBody = new BodyId[count];
        _variant = new byte[count];
        _build = new CarBuild[count];
        _command = new DriveCommand[count];
        _underAHand = new bool[count];
        _tyres = new TyreCarries(count);
        _wheels = new WheelScratch(count);
        _wheelsOnAWay = new byte[count];
        _accelerationMps2 = new Vector2[count];
        _towing = new int[count];
        _onTheHookOf = new int[count];
        _byTheTail = new bool[count];
        _carIntoMps = new Vector2[count];
        _carStoodAt = new CarStands[count];
        _carCoverCount = new int[count];
        _carCoversFrom = new int[count];
        _carCovers = new WayCover[count * TyreModel.Wheels];
        _carCoversBefore = new WayCover[count * TyreModel.Wheels];
        _roads = laying.Roads;
        _followed = new LineFollowed[count];
        Array.Fill(_followed, LineFollowed.None);
        Array.Fill(_towing, -1);
        Array.Fill(_onTheHookOf, -1);

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

        for (var person = 0; person < _walkerBody.Length; person++) StepTheWalker(person, dtS);

        for (var car = 0; car < _carBody.Length; car++) StepTheTyres(car);

        HaulOnTheBars(dtS);
        _physics.Step(dtS);
        SettleTheLoads(dtS);
        WriteTheView(into, stepped: true);
        WriteTheTouches(into);
        if (_stepReadsCovers) ReadCovers(into);
    }

    public override void Dispose() => _aside?.Dispose();

    void Take(in ControlSignal signal)
    {
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
                Couple(
                    car, signal.Tractor < 0 ? -1 : _roster.CarIndex(signal.Tractor), (signal.Flags & ControlSignal.ByTheTail) != 0);
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

    void Couple(int towed, int tractor, bool byTheTail)
    {
        var was = _onTheHookOf[towed];
        if (was >= 0)
        {
            _towing[was] = -1;
            _onTheBar--;
        }

        _onTheHookOf[towed] = tractor;
        _byTheTail[towed] = byTheTail;
        if (tractor < 0) return;

        _towing[tractor] = towed;
        _onTheBar++;
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
        if (_onTheHookOf[car] >= 0)
        {
            TrailerWheels(car);
        }
        else if (TyrePass.Settled(_physics.VelocityOf(body), _physics.YawRateOf(body), _command[car], _tyres.Of(car)))
        {
            _wheels.Clear(car);
            return;
        }
        else
        {
            Span<TyreMark> marks = stackalloc TyreMark[TyreModel.Wheels];
            TyrePass.Step(
                Config, _grounds, _build[car], PoseOf(car), _command[car], _underAHand[car],
                1f / _physics.InverseInertiaOf(body), _wheelsOnAWay[car], _tyres.Of(car), _wheels.GroundUnder(car),
                _wheels.ImpulsesOf(car), _wheels.ScrubOf(car), marks, out var laid);
            if (_marksLaidCount + laid > _marksLaid.Length) Array.Resize(ref _marksLaid, _marksLaid.Length * 2);

            marks[..laid].CopyTo(_marksLaid.AsSpan(_marksLaidCount));
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
    /// pair cleared, and the two down rolling with the road under them.
    /// </summary>
    void TrailerWheels(int car)
    {
        var down = TowBar.PairOnTheGround(_byTheTail[car]);
        var up = TowBar.PairInTheAir(_byTheTail[car]);
        var impulses = _wheels.ImpulsesOf(car);
        impulses.Slice(up, TowBar.Wheels).Clear();
        _wheels.ScrubOf(car).Clear();

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
    /// <b>Every tow bar, spent as the pair of impulses it is</b> (EVA-5), after the wheels and before the step: the bar
    /// answers the motion the tyres have just asked for, which is the whole of the coupling's stability.
    /// </summary>
    void HaulOnTheBars(float dtS)
    {
        if (_onTheBar == 0) return;

        for (var tractor = 0; tractor < _carBody.Length; tractor++)
        {
            var towed = _towing[tractor];
            if (towed < 0) continue;

            var tractorPose = PoseOf(tractor);
            var trailer = PoseOf(towed);
            var hookM = TowBar.HookM(_build[tractor], tractorPose);
            var eyeM = TowBar.EyeM(_build[towed], trailer, _build[tractor].TowReachM, _byTheTail[towed]);
            var pullNs = TowBar.PullNs(
                EndOfTheBar(_carBody[tractor], tractorPose, hookM), EndOfTheBar(_carBody[towed], trailer, eyeM),
                Config.Evacuator.HitchSettleS, Config.EvacuatorHitchMostMps2, Config.Evacuator.HitchSideShare,
                trailer.MassKg, dtS);
            _physics.ApplyImpulseAt(_carBody[towed], pullNs, eyeM);
            _physics.ApplyImpulseAt(_carBody[tractor], -pullNs, hookM);
        }
    }

    HookEnd EndOfTheBar(BodyId body, in CarPose pose, Vector2 atM)
    {
        var armM = atM - pose.PositionM;
        return new HookEnd(
            atM, pose.VelocityMps + (pose.YawRateRadPerS * new Vector2(-armM.Y, armM.X)), armM,
            _physics.InverseMassOf(body), _physics.InverseInertiaOf(body));
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
                Vector2.Lerp(_accelerationMps2[car], alongAndAcrossMps2, settled), leastMps2);
        }
    }

    /// <param name="stepped">
    /// Whether it is the step's own view, which reads each car on from where the last one read it; written again off the
    /// clock, it reads around the same place and so answers the same (<see cref="LineFollowed.AroundM"/>).
    /// </param>
    void WriteTheView(WorldView view, bool stepped = false)
    {
        var cars = view.Cars;
        for (var car = 0; car < _carBody.Length; car++)
        {
            var body = _carBody[car];
            ref var row = ref cars[car];
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
            var rearAxleM = CarFollower.RearAxleM(_build[car], row.PositionM, row.Forward);
            LineReading.Read(_roads, ref followed, rearAxleM, _build[car].ProjectionWindowM);
            row.ProgressM = followed.ProgressM;
            row.OffLineM = followed.OffLineM;
            row.FollowSerial = followed.Serial;

            var lookaheadM = CarFollower.LookaheadM(
                _build[car], MathF.Abs(Vector2.Dot(row.VelocityMps, row.Forward)), Config.Driving.LookaheadS);
            var leadM = LineReading.Lead(_roads, followed, lookaheadM, out row.LeadAtM);
            row.AimRad = float.IsNaN(row.LeadAtM) ? 0f : CarFollower.AimAt(_build[car], rearAxleM, row.Forward, leadM);
        }

        var walkers = view.Walkers;
        for (var person = 0; person < _walkerBody.Length; person++)
        {
            var body = _walkerBody[person];
            ref var row = ref walkers[person];
            row.PositionM = _physics.PositionOf(body);
            row.VelocityMps = _physics.VelocityOf(body);
            row.HeadingRad = _headingRad[person];
            row.Flags = _declaredMps[person] != Vector2.Zero ? WalkerView.Declares : 0u;
            row.OverlapM = _physics.OverlapOf(body);
        }
    }

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
            }

            var from = written - count;
            _carCoversFrom[car] = from;
            byte channels = 0;
            foreach (ref readonly var cover in _carCovers.AsSpan(from, count)) channels |= _atlas.ChannelsOf(cover.Way);
            if (channels != 0 && channels != _physics.ChannelsOf(body)) _physics.PutOnChannels(body, channels);

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
