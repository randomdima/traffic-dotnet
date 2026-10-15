using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The world layer on the device</b> (<see cref="WorldLayer"/>; root <c>decision-log.md</c>, 2026-10-11): the solver
/// (<see cref="DeviceSolver"/>), the tyres (<see cref="DeviceTyres"/>), a walker's footing, the tow bars, the lane
/// ribbons each body stands on and where each car stands along the lanes it follows (<see cref="DeviceLanes"/>), all
/// kept on the device and stepped from what the last step left. A tick crosses the signals in and the shallow state
/// out, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>One submit and one wait a tick, a tick apart</b> (<c>world/town/kernels/world_*.comp</c>): the signals taken, the
/// tyre pass beside every walker's footing, the tow bars' trailing wheels and every wheel spent, the bars' pull where a
/// bar is up, the solver's step, the covers where the claims want them, and the view and its counts written straight
/// into memory the host reads. The step is submitted by <see cref="Begin"/> and waited for by the next tick's
/// (<see cref="Finish"/>), so the device steps while the host decides (<see cref="WorldLayer.StepsAhead"/>). Nothing the
/// host writes for the device is written while a step is in flight.
/// </para>
/// <para>
/// <b>A car's tyres read the pose the last step's view left</b> (<c>world_view.comp</c>), which the signals keep where
/// they move a car's body (<c>world_signals.comp</c>), so no pass reads every car off its body before the tyres; the
/// first pose is read off the table as it is laid (<c>world_cars.comp</c>).
/// </para>
/// <para>
/// <b>The body table is stood once off the laying</b> (<see cref="WorldLaying.StandTheBodies"/>), laid into the device and
/// let go of. What a signal needs worked out on the host is arithmetic — the body an agent is, a placed body's rotation,
/// a layer's mask — and what a begun touch is, is the laying's numbering (<see cref="WorldLaying.TagOf"/>).
/// </para>
/// </remarks>
internal sealed class DeviceWorld : WorldLayer
{
    /// <summary>A control signal as the device takes it (<c>WorldSignal</c>): the agent's body row, never the agent.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct SignalRow(int Body, uint ActionAndFlags, Vector2 P01, Vector2 P23, Vector2 P45);

    /// <summary>One car as the device keeps it beside its tyres (<c>WorldCar</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CarRow(int Body, int Tractor, uint Flags, float MassKg);

    /// <summary>One walker as the device keeps it (<c>WorldWalker</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkerRow(
        Vector2 AimM, Vector2 DeclaredMps, float HeadingRad, float MassKg, float RadiusM, int Body, uint Flags, float DistanceWalkedM);

    /// <summary>The stepping kernels' push constants (<c>world.glsl</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WorldPush
    {
        public uint Count;
        public uint Mode;
        public float DtS;
        public float LoadSettledShare;
        public float LeastSettledLoadMps2;
        public uint EnabledBit;
        public uint FrozenBit;
        public uint FiledFrozenBit;
        public float WalkSpeedMps;
        public float TurnRateDegPerS;
        public float FootGripMps2;
        public float SlidingGripMps2;
        public float AimUnderfootM2;
        public float HitchSettleS;
        public float HitchMostMps2;
        public float HitchSideShare;
        public float OnTheTrailerAxleShare;
        public uint CarCount;
        public float LookaheadS;
    }

    /// <summary>The covers kernel's push constants (<c>world_covers.comp</c>): the atlas's, then the bodies'.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct CoversPush
    {
        public DeviceAtlas.AskPush Atlas;
        public uint CarCount;
        public uint WalkerCount;
        public float HeldAroundAWheelM;
        public uint CarRoom;
        public uint WalkerRoom;
        public uint EnabledBit;
    }

    /// <summary>A tick's signals as written for the device: the commands, aims and shows from the front, the rest from the back.</summary>
    readonly record struct SignalCounts(int Commands, int Events)
    {
        public bool Any => Commands > 0 || Events > 0;
    }

    const int Group = 64;
    const int CarCoversEach = 16;
    const int WalkerCoversEach = 8;

    /// <summary>What the view hands back beside itself (<c>world_view.comp</c>): the covers' four counts.</summary>
    const int CountsOut = 4;

    readonly Vk _vk;
    readonly WorldLaying _laying;
    readonly int _cars, _walkers;
    readonly float _heldAroundAWheelM;
    readonly List<GpuBuffer> _owned = [];
    readonly List<ComputeKernel> _kernels = [];

    readonly DeviceSolver _solver;
    readonly DeviceTyres _tyres;
    readonly DeviceAtlas _atlas;
    readonly DeviceLanes _lanes;
    readonly ComputeBatch _batch;

    /// <summary>What each car follows as last signalled, and where the view last read it (<see cref="LineFollowed"/>).</summary>
    readonly GpuBuffer _follows;

    readonly GpuBuffer _carRows, _walkerRows, _carRanges, _walkerRanges, _counts, _countsOut;
    GpuBuffer _signalsWritten, _signalsIn;
    GpuBuffer _carCovers, _walkerCovers;
    int _carRoom, _walkerRoom;

    readonly ComputeKernel _signalsKernel, _walkersKernel, _towKernel, _coversKernel, _viewKernel;
    DescriptorSet _signalsSet, _coversSet;
    readonly DescriptorSet _walkersSet, _towSet;

    /// <summary>
    /// The view kernel bound to each of the two views' rows (<see cref="WorldView.Slot"/>), which the host reads where
    /// they are written: the step in flight writes the one not shown.
    /// </summary>
    readonly DescriptorSet[] _viewSets = new DescriptorSet[2];

    /// <summary>The two views' rows, by slot, which the host reads where they are written.</summary>
    readonly GpuBuffer[] _carViews = new GpuBuffer[2];

    readonly GpuBuffer[] _walkerViews = new GpuBuffer[2];

    /// <summary>Whether each car hangs off a tow bar as last signalled (<see cref="ControlAction.Tow"/>), and how many do.</summary>
    readonly bool[] _towed;

    int _bars;

    WorldPush _push;

    public DeviceWorld(Vk vk, WorldLaying laying) : base(laying)
    {
        _vk = vk;

        // The furniture and the atlas are read here, to stand the device's own, and never again: kept, they would be the
        // host's largest tables for the run (the root decision log, 2026-10-13).
        _laying = laying.WithoutTheGround();
        _cars = laying.Cars.Length;
        _walkers = laying.Walkers.Length;
        _last = new LastInBatch[laying.Roster.Count];
        _towed = new bool[_cars];
        var config = laying.Config;
        _heldAroundAWheelM = config.Road.HeldAroundAWheelM;

        var stood = laying.StandTheBodies();
        var table = stood.Table;
        _solver = new DeviceSolver(vk, config, table) { Settles = true };
        _solver.LayWhole(table);
        _solver.LayTheRestCounts(table);
        _solver.LayTheTouching(stood.TouchingKeys);

        _tyres = new DeviceTyres(vk, config, laying.Grounds, BuildsOf(laying.Builds), Math.Max(1, _cars));
        LayTheTyres(stood);
        _atlas = new DeviceAtlas(vk, laying.Atlas.Table);
        _lanes = new DeviceLanes(vk, laying.Roads);
        _batch = new ComputeBatch(vk);

        _carRows = Laid(CarRowsOf());
        var followsNothing = new LineFollowed[_cars];
        Array.Fill(followsNothing, LineFollowed.None);
        _follows = Laid(followsNothing);
        _walkerRows = Laid(WalkerRowsOf(stood));
        var bodies = table.Count;
        var carOf = new int[bodies];
        var walkerOf = new int[bodies];
        Array.Fill(carOf, -1);
        Array.Fill(walkerOf, -1);
        for (var car = 0; car < _cars; car++) carOf[BodyOfCar(car)] = car;
        for (var person = 0; person < _walkers; person++) walkerOf[laying.BodyOf(person)] = person;
        var carOfBuffer = Laid(carOf);
        var walkerOfBuffer = Laid(walkerOf);

        _carRanges = Mapped<CoverRange>(_cars);
        _walkerRanges = Mapped<CoverRange>(_walkers);
        _counts = Device<uint>(4);
        _countsOut = Mapped<uint>(CountsOut);
        _carRoom = Math.Max(Group, _cars * CarCoversEach);
        _walkerRoom = Math.Max(Group, _walkers * WalkerCoversEach);
        _carCovers = Mapped<WayCover>(_carRoom);
        _walkerCovers = Mapped<WayCover>(_walkerRoom);
        (_signalsWritten, _signalsIn) = SignalBuffers(Signals.Capacity);

        var (pose, heading, motion, mass, filter) = _solver.Bodies;
        var (shape, bounds, motionIntoTick) = _solver.Rows;
        var tyres = _tyres.Table;

        var pushBytes = (uint)Marshal.SizeOf<WorldPush>();
        _signalsKernel = Kernel("world_signals.comp", 17, pushBytes);
        _signalsBound =
        [
            null!, carOfBuffer, walkerOfBuffer, tyres.Commands.Device, tyres.Cars.Device, tyres.Wheels.Device, _carRows,
            _walkerRows, pose, heading, motion, motionIntoTick, shape, bounds, filter, tyres.Poses.Device, _follows,
        ];
        _signalsSet = BindTheSignals();

        _walkersKernel = Kernel("world_walkers.comp", 4, pushBytes);
        _walkersSet = _walkersKernel.Bind([_walkerRows, pose, motion, mass]);

        _towKernel = Kernel("world_tow.comp", 11, pushBytes);
        _towSet = _towKernel.Bind(
        [
            _carRows, tyres.Builds, tyres.BuildOf.Device, tyres.Poses.Device, tyres.Cars.Device, _tyres.WheelGrounds,
            _tyres.ImpulseTable, tyres.Wheels.Device, pose, motion, mass,
        ]);

        _coversKernel = Kernel("world_covers.comp", 12 + _atlas.Tables.Length, (uint)Marshal.SizeOf<CoversPush>());
        _coversBound =
        [
            _carRows, _walkerRows, tyres.Builds, tyres.BuildOf.Device, pose, filter, tyres.Cars.Device, null!, null!,
            _counts, _carRanges, _walkerRanges, .. _atlas.Tables,
        ];
        _coversSet = BindTheCovers();

        _viewKernel = Kernel("world_view.comp", 18 + _lanes.Tables.Length, pushBytes, sets: _viewSets.Length);
        for (var slot = 0; slot < _viewSets.Length; slot++)
        {
            var carViews = _carViews[slot] = Mapped<CarView>(_cars);
            var walkerViews = _walkerViews[slot] = Mapped<WalkerView>(_walkers);
            ViewInSlot(slot).ReadOver(carViews.Span<CarView>(), walkerViews.Span<WalkerView>());
            _viewSets[slot] = _viewKernel.Bind(
            [
                _carRows, _walkerRows, _tyres.ImpulseTable, tyres.Poses.Device, tyres.Cars.Device, pose, heading, motion,
                filter, _solver.Overlap, carViews, walkerViews, _counts, _tyres.GroundMarkRing, _countsOut, _follows,
                tyres.Builds, tyres.BuildOf.Device, .. _lanes.Tables,
            ]);
        }

        _push = new WorldPush
        {
            DtS = config.TickSeconds,
            LoadSettledShare = TownWorld.LoadSettledShare(config, config.TickSeconds),
            LeastSettledLoadMps2 = config.Tyre.LeastSettledLoadMps2,
            EnabledBit = (uint)BodyFlags.Enabled,
            FrozenBit = (uint)BodyFlags.Frozen,
            FiledFrozenBit = (uint)BodyFlags.FiledFrozen,
            WalkSpeedMps = config.PersonWalkSpeedMps,
            TurnRateDegPerS = config.PersonTurnRateDegPerS,
            FootGripMps2 = config.PersonFootGripMps2,
            SlidingGripMps2 = config.PersonSlidingGripMps2,
            AimUnderfootM2 = config.Person.AimUnderfootM2,
            HitchSettleS = config.Evacuator.HitchSettleS,
            HitchMostMps2 = config.EvacuatorHitchMostMps2,
            HitchSideShare = config.Evacuator.HitchSideShare,
            OnTheTrailerAxleShare = config.Evacuator.OnTheTrailerAxleShare,
            CarCount = (uint)_cars,
            LookaheadS = config.Driving.LookaheadS,
        };

        // Every car read off its body as laid, and the first view and covers off the world as it was laid, before any step.
        using var carsKernel = new ComputeKernel(vk, "world_cars.comp", 6, pushBytes);
        var carsSet = carsKernel.Bind([_carRows, tyres.Poses.Device, pose, heading, motion, filter]);
        var commands = _batch.Begin();
        _solver.Send(commands);
        _tyres.SendMarked(commands);
        _vk.Fill(commands, _counts, 0);
        _vk.AfterTransfer(commands);
        carsKernel.Dispatch(commands, carsSet, Groups(_cars), _push with { Count = (uint)_cars });
        _vk.BetweenKernels(commands);
        RecordTheCovers(commands);
        _vk.BetweenKernels(commands);
        RecordTheView(commands, View);
        _batch.SubmitAndWait();
        TakeTheView(View, coversRead: true);
    }

    /// <summary>Whether the step in flight reads the covers.</summary>
    bool _readingCovers;

    readonly GpuBuffer[] _signalsBound;
    readonly GpuBuffer[] _coversBound;

    /// <summary>The signals' set over the buffers as they now are, rewritten in place once there is one.</summary>
    DescriptorSet BindTheSignals()
    {
        _signalsBound[0] = _signalsIn;
        return _signalsKernel.Bind(_signalsBound, _signalsSet);
    }

    DescriptorSet BindTheCovers()
    {
        _coversBound[7] = _carCovers;
        _coversBound[8] = _walkerCovers;
        return _coversKernel.Bind(_coversBound, _coversSet);
    }

    int BodyOfCar(int car) => _laying.BodyOf(_laying.Roster.AgentOfCar(car));

    static CarBuild[] BuildsOf(CarBuilds table)
    {
        var builds = new CarBuild[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = table.Of(variant);
        return builds;
    }

    /// <summary>Every car's tyres laid as the town stands them: its build, at rest where it was put, parked, nothing carried.</summary>
    void LayTheTyres(PhysicsWorld stood)
    {
        for (var car = 0; car < _cars; car++)
        {
            ref readonly var laid = ref _laying.Cars[car];
            var body = new BodyId(BodyOfCar(car) + 1);
            _tyres.BuildOf[car] = laid.Variant;
            _tyres.Poses[car] = DeviceTyres.PoseRow.Of(new CarPose(
                laid.PositionM, laid.HeadingRad, Vector2.Zero, 0f, stood.MassOf(body), Vector2.Zero));
            _tyres.Commands[car] = DeviceTyres.CommandRow.Of(DriveCommand.Parked);
            _tyres.Cars[car] = new DeviceTyres.CarRow(1f / stood.InverseInertiaOf(body), 1f, 0u);
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                _tyres.Wheels[(car * TyreModel.Wheels) + wheel] = new DeviceTyres.WheelRow(laid.PositionM, 0f, 0f, 0f, 0u);
            }
        }

        var table = _tyres.Table;
        table.BuildOf.Mark(0, _cars);
        table.Poses.Mark(0, _cars);
        table.Commands.Mark(0, _cars);
        table.Cars.Mark(0, _cars);
        table.Wheels.Mark(0, _cars);
    }

    CarRow[] CarRowsOf()
    {
        var rows = new CarRow[Math.Max(1, _cars)];
        for (var car = 0; car < _cars; car++)
        {
            rows[car] = new CarRow(BodyOfCar(car), -1, 0u, _laying.Builds.Of(_laying.Cars[car].Variant).MassKg);
        }

        return rows;
    }

    WalkerRow[] WalkerRowsOf(PhysicsWorld stood)
    {
        var rows = new WalkerRow[Math.Max(1, _walkers)];
        var radiusM = Config.PersonRadiusM;
        for (var person = 0; person < _walkers; person++)
        {
            ref readonly var laid = ref _laying.Walkers[person];
            var body = _laying.BodyOf(person);
            rows[person] = new WalkerRow(
                laid.PositionM, Vector2.Zero, laid.HeadingRad, stood.MassOf(new BodyId(body + 1)), radiusM, body,
                laid.Inside ? WalkerInside : 0u, 0f);
        }

        return rows;
    }

    /// <summary>A walker inside a container (<c>WalkerInside</c>).</summary>
    const uint WalkerInside = 8u;

    /// <summary>The signals, the covers and the view in one submit and one wait.</summary>
    protected override void TakeTheSignalsAndReadCovers(WorldView into)
    {
        var signals = WriteTheSignals();
        var commands = _batch.Begin();
        _solver.Send(commands);
        SendTheSignals(commands, signals);
        _vk.Fill(commands, _counts, 0);
        _vk.AfterTransfer(commands);
        if (RecordTheSignals(commands, signals)) _vk.BetweenKernels(commands);
        RecordTheCovers(commands);
        _vk.BetweenKernels(commands);
        RecordTheView(commands, into);
        _batch.SubmitAndWait();
        if (TakeTheView(into, coversRead: true)) ReadCovers(into);
    }

    /// <summary>The covers read again, into lists grown to hold them, and the view with them.</summary>
    void ReadCovers(WorldView into)
    {
        var commands = _batch.Begin();
        _vk.Fill(commands, _counts, 0);
        _vk.AfterTransfer(commands);
        RecordTheCovers(commands);
        _vk.BetweenKernels(commands);
        RecordTheView(commands, into);
        _batch.SubmitAndWait();
        if (TakeTheView(into, coversRead: true)) ReadCovers(into);
    }

    /// <exception cref="InvalidOperationException">The step found more candidate pairs or contacts than the solver holds, and so lost some.</exception>
    protected override void Finish(WorldView into)
    {
        _batch.Wait();
        if (_solver.Overflowed)
        {
            throw new InvalidOperationException(
                $"A step found more candidate pairs or contacts than the {_solver.CandidateCapacity} and {_solver.ContactCapacity} " +
                "the device holds, and lost some.");
        }

        if (TakeTheView(into, _readingCovers)) ReadCovers(into);
        TakeTheTouches(into);
    }

    /// <remarks>
    /// Nothing is copied back behind the step: the solver's totals come back from its last kernel, and the view's counts
    /// and the marks laid with the view.
    /// </remarks>
    protected override void Begin(float dtS, WorldView into, bool readCovers)
    {
        var signals = WriteTheSignals();
        if (dtS != _push.DtS)
        {
            _push.DtS = dtS;
            _push.LoadSettledShare = TownWorld.LoadSettledShare(Config, dtS);
        }

        var commands = _batch.Begin();
        _solver.Send(commands);
        _tyres.SendMarked(commands);
        SendTheSignals(commands, signals);
        if (readCovers) _vk.Fill(commands, _counts, 0);
        _vk.AfterTransfer(commands);

        if (RecordTheSignals(commands, signals)) _vk.BetweenKernels(commands);
        _tyres.Run(commands, _cars);
        _walkersKernel.Dispatch(commands, _walkersSet, Groups(_walkers), _push with { Count = (uint)_walkers });
        _vk.BetweenKernels(commands);
        _towKernel.Dispatch(commands, _towSet, Groups(_cars), _push with { Count = (uint)_cars, Mode = 0 });
        _vk.BetweenKernels(commands);
        if (_bars > 0)
        {
            _towKernel.Dispatch(commands, _towSet, Groups(_cars), _push with { Count = (uint)_cars, Mode = 1 });
            _vk.BetweenKernels(commands);
        }

        _solver.Record(commands, dtS);
        _vk.BetweenKernels(commands);
        if (readCovers)
        {
            RecordTheCovers(commands);
            _vk.BetweenKernels(commands);
        }

        RecordTheView(commands, into, stepped: true);
        _batch.Submit();
        _readingCovers = readCovers;
    }

    /// <summary>
    /// Where in the batch being written an agent's last command, aim, show and placing stand, or −1; stale unless
    /// <see cref="Batch"/> is the batch's (<see cref="WriteTheSignals"/>).
    /// </summary>
    struct LastInBatch
    {
        public long Batch;
        public int Drive, Walk, Show, Place, Follow;
    }

    readonly LastInBatch[] _last;
    long _batchWritten;

    /// <summary>A row's mark that the device takes it in order with the events (<c>SignalInOrder</c>).</summary>
    const uint SignalInOrder = 0x8000u;

    /// <summary>
    /// This tick's signals written where the device copies them from, each with what only the host's table can work out,
    /// and every body one puts down or contains counted afresh — a body refiltered keeps its rest count, as
    /// <see cref="PhysicsWorld.PutOnLayer"/> keeps it.
    /// </summary>
    /// <remarks>
    /// The device takes the commands, aims and shows a thread each and the events after them in order
    /// (<c>world_signals.comp</c>), where <see cref="ManagedWorld"/> takes every signal in order. So only an agent's last
    /// of each crosses, from the front of the buffer, and the events from the back; an aim given after the agent's last
    /// placing goes with the events, marked to be taken in order with them — a placing aims a walker where it is put, and
    /// the last of the two to be signalled stands.
    /// </remarks>
    SignalCounts WriteTheSignals()
    {
        var written = Signals.Written;
        if (written.Length > RoomForSignals)
        {
            foreach (var buffer in (ReadOnlySpan<GpuBuffer>)[_signalsWritten, _signalsIn])
            {
                _owned.Remove(buffer);
                buffer.Dispose();
            }

            (_signalsWritten, _signalsIn) = SignalBuffers(written.Length * 2);
            _signalsSet = BindTheSignals();
        }

        var batch = ++_batchWritten;
        for (var at = 0; at < written.Length; at++)
        {
            ref var last = ref LastOf(written[at].Agent, batch);
            switch (written[at].Action)
            {
                case ControlAction.Drive: last.Drive = at; break;
                case ControlAction.Walk: last.Walk = at; break;
                case ControlAction.Show: last.Show = at; break;
                case ControlAction.Place: last.Place = at; break;
                case ControlAction.Follow: last.Follow = at; break;
            }
        }

        var rows = _signalsWritten.Span<SignalRow>();
        var back = RoomForSignals - 1;
        var commands = 0;
        var events = 0;
        for (var at = 0; at < written.Length; at++)
        {
            ref readonly var signal = ref written[at];
            ref readonly var last = ref _last[signal.Agent];
            var superseded = signal.Action switch
            {
                ControlAction.Drive => last.Drive != at,
                ControlAction.Walk => last.Walk != at,
                ControlAction.Show => last.Show != at,
                ControlAction.Follow => last.Follow != at,
                _ => false,
            };
            if (superseded) continue;

            var row = RowOf(signal);
            var isCommand = signal.Action is ControlAction.Drive or ControlAction.Walk or ControlAction.Show or ControlAction.Follow;
            if (signal.Action == ControlAction.Walk && last.Place >= 0 && last.Place < at)
            {
                row = row with { ActionAndFlags = row.ActionAndFlags | SignalInOrder };
                isCommand = false;
            }

            if (isCommand) rows[commands++] = row;
            else rows[back - events++] = row;
        }

        return new SignalCounts(commands, events);
    }

    /// <summary>How many signal rows the buffers hold, the events counted from the last of them back.</summary>
    int RoomForSignals => (int)(_signalsIn.SizeBytes / (ulong)Marshal.SizeOf<SignalRow>());

    ref LastInBatch LastOf(int agent, long batch)
    {
        ref var last = ref _last[agent];
        if (last.Batch != batch) last = new LastInBatch { Batch = batch, Drive = -1, Walk = -1, Show = -1, Place = -1, Follow = -1 };
        return ref last;
    }

    SignalRow RowOf(in ControlSignal signal)
    {
        var body = _laying.BodyOf(signal.Agent);
        var actionAndFlags = (uint)signal.Action | ((uint)signal.Flags << 16);
        var p01 = new Vector2(signal.Params.X, signal.Params.Y);
        var p23 = new Vector2(signal.Params.Z, signal.Params.W);
        switch (signal.Action)
        {
            case ControlAction.Place:
                _solver.CountAfresh(body);
                return new SignalRow(body, actionAndFlags, p01, Shape.Rotation(signal.Params.Z), new Vector2(signal.Params.Z, 0f));

            case ControlAction.Contain:
                _solver.CountAfresh(body);
                break;

            case ControlAction.Layer:
                var layer = (CollisionLayer)signal.Flags;
                return new SignalRow(
                    body, actionAndFlags,
                    new Vector2(
                        BitConverter.UInt32BitsToSingle(DeviceSolver.LayerByte((ulong)layer)),
                        BitConverter.UInt32BitsToSingle(DeviceSolver.LayerByte(PhysicsWorld.MaskOf(layer)))),
                    default, default);

            case ControlAction.Tow:
                // world_signals.comp takes the tractor as its car index.
                var tractor = signal.Tractor < 0 ? -1 : _laying.Roster.CarIndex(signal.Tractor);
                CountTheBar(_laying.Roster.CarIndex(signal.Agent), tractor >= 0);
                return new SignalRow(body, actionAndFlags, new Vector2(BitConverter.Int32BitsToSingle(tractor), 0f), default, default);
        }

        return new SignalRow(body, actionAndFlags, p01, p23, default);
    }

    /// <summary>A car's bar as the signal leaves it, kept count of so a step with none up runs no pull (<c>world_tow.comp</c>).</summary>
    void CountTheBar(int car, bool towed)
    {
        if (_towed[car] == towed) return;

        _towed[car] = towed;
        _bars += towed ? 1 : -1;
    }

    /// <summary>The rows <see cref="WriteTheSignals"/> wrote copied to where the device reads them, recorded; the caller barriers after.</summary>
    void SendTheSignals(CommandBuffer commands, SignalCounts signals)
    {
        var rowBytes = (ulong)Marshal.SizeOf<SignalRow>();
        var eventsFromBytes = (ulong)(RoomForSignals - signals.Events) * rowBytes;
        Span<BufferCopy> runs = stackalloc BufferCopy[2];
        runs[0] = new BufferCopy(0, 0, (ulong)signals.Commands * rowBytes);
        runs[1] = new BufferCopy(eventsFromBytes, eventsFromBytes, (ulong)signals.Events * rowBytes);
        var from = signals.Commands > 0 ? 0 : 1;
        var to = signals.Events > 0 ? 2 : 1;
        if (from < to) _vk.Copy(commands, _signalsWritten, _signalsIn, runs[from..to]);
    }

    /// <summary>The signals' kernels recorded, each only where it has any; whether any was.</summary>
    bool RecordTheSignals(CommandBuffer commands, SignalCounts signals)
    {
        if (signals.Commands > 0)
        {
            _signalsKernel.Dispatch(
                commands, _signalsSet, Groups(signals.Commands), _push with { Count = (uint)signals.Commands, Mode = 0 });
        }

        if (signals.Events > 0)
        {
            if (signals.Commands > 0) _vk.BetweenKernels(commands);
            _signalsKernel.Dispatch(commands, _signalsSet, 1, _push with { Count = (uint)signals.Events, Mode = 1 });
        }

        return signals.Any;
    }

    void RecordTheCovers(CommandBuffer commands) => _coversKernel.Dispatch(
        commands, _coversSet, Groups(_cars + _walkers),
        new CoversPush
        {
            Atlas = _atlas.PushFor(_cars + _walkers),
            CarCount = (uint)_cars,
            WalkerCount = (uint)_walkers,
            HeldAroundAWheelM = _heldAroundAWheelM,
            CarRoom = (uint)_carRoom,
            WalkerRoom = (uint)_walkerRoom,
            EnabledBit = (uint)BodyFlags.Enabled,
        });

    /// <summary>Every car's and then every walker's view, in one dispatch, into the rows <paramref name="into"/> reads, and the counts beside it.</summary>
    /// <param name="stepped">
    /// Whether the pass is the step's own, keeping what the step settled; a view written again off the clock only reads
    /// (<c>world_view.comp</c>), so writing it twice moves nothing.
    /// </param>
    void RecordTheView(CommandBuffer commands, WorldView into, bool stepped = false) => _viewKernel.Dispatch(
        commands, _viewSets[into.Slot], Groups(_cars + _walkers),
        _push with { Count = (uint)(_cars + _walkers), Mode = stepped ? ViewStepped : 0u });

    /// <summary><c>world_view.comp</c>'s mode bit for the step's own pass (<c>ViewStepped</c>).</summary>
    const uint ViewStepped = 2u;

    /// <summary>
    /// The covers the device read taken into <paramref name="view"/>, where they were read — its rows it reads where the
    /// device wrote them (<see cref="WorldView.ReadOver"/>); whether a list ran past its room — grown then, and to be read
    /// again.
    /// </summary>
    bool TakeTheView(WorldView view, bool coversRead)
    {
        if (!coversRead) return false;

        var counts = _countsOut.Span<uint>();
        if (counts[2] > 0 || counts[3] > 0)
        {
            GrowTheCovers((int)counts[0], (int)counts[1]);
            return true;
        }

        view.CarCoverCount = (int)counts[0];
        view.WalkerCoverCount = (int)counts[1];
        if (view.CarCovers.Length < view.CarCoverCount) view.CarCovers = new WayCover[_carRoom];
        if (view.WalkerCovers.Length < view.WalkerCoverCount) view.WalkerCovers = new WayCover[_walkerRoom];
        _carCovers.Span<WayCover>()[..view.CarCoverCount].CopyTo(view.CarCovers);
        _walkerCovers.Span<WayCover>()[..view.WalkerCoverCount].CopyTo(view.WalkerCovers);
        _carRanges.Span<CoverRange>()[.._cars].CopyTo(view.CarCoverRanges);
        _walkerRanges.Span<CoverRange>()[.._walkers].CopyTo(view.WalkerCoverRanges);
        view.CoversRead = true;
        return false;
    }

    void GrowTheCovers(int carCovers, int walkerCovers)
    {
        _carRoom = Math.Max(_carRoom, carCovers * 2);
        _walkerRoom = Math.Max(_walkerRoom, walkerCovers * 2);
        foreach (var buffer in (ReadOnlySpan<GpuBuffer>)[_carCovers, _walkerCovers])
        {
            _owned.Remove(buffer);
            buffer.Dispose();
        }

        _carCovers = Mapped<WayCover>(_carRoom);
        _walkerCovers = Mapped<WayCover>(_walkerRoom);
        _coversSet = BindTheCovers();
    }

    /// <summary>Every pair the step began touching, by what each body is, with the speed it closed at.</summary>
    void TakeTheTouches(WorldView view)
    {
        view.TouchCount = 0;
        foreach (ref readonly var begun in _solver.Begun)
        {
            view.AddTouch(new Touch(
                _laying.TagOf((int)begun.First), _laying.TagOf((int)begun.Second), begun.Normal, begun.ClosingMps));
        }
    }

    /// <summary>
    /// <b>The ground's marks, on the device where the tyres lay them</b> (<see cref="DeviceTyres.GroundMarks"/>), and the
    /// words a frame draws them by: nothing of them is read back, so the world layer's own ring (<see cref="WorldLayer.Marks"/>)
    /// stays empty in a town stepped here.
    /// </summary>
    public (GpuBuffer Rows, GpuBuffer Ring) GroundMarks => (_tyres.GroundMarks, _tyres.GroundMarkRing);

    /// <remarks>The step in flight waited for first: the retuned rows are written where the device copies them from.</remarks>
    public override void FiguresChanged(in WheelGrounds grounds)
    {
        _batch.Wait();
        _tyres.Retune(grounds, BuildsOf(_laying.Builds));
    }

    public override int InBox(Vector2 leastM, Vector2 mostM, BodyKind kind, int mostCells, Span<int> into) => -1;

    public override int IntegratedBodyCount => _solver.Owners;

    /// <summary>What the solver's step holds of the device's buffers (<see cref="DeviceSolver.BytesHeld"/>) — an instrument's.</summary>
    public long SolverBytes => _solver.BytesHeld;

    public override int FrozenBodyCount => 0;

    /// <summary>What moves, drawn off the device's own state; none until <see cref="DrawWith"/> lays it.</summary>
    public DeviceSprites? Sprites { get; private set; }

    /// <summary>What moves drawn off this world's own state from here on, by the emit laid for it.</summary>
    public DeviceSprites DrawWith(TownWorld town, TownSprites looks, SpriteRoom room)
    {
        Sprites?.Dispose();
        return Sprites = DeviceSprites.For(_vk, Config, town, looks, room, this);
    }

    /// <summary>
    /// <b>What the emit reads of the world</b> (<c>sprites_world.comp</c>): each car's and walker's own row, the cars'
    /// commands, and the two views' rows by slot (<see cref="WorldView.Slot"/>) — where every body stands and points, and a
    /// car's channels, as the view the host is showing has them.
    /// </summary>
    public (GpuBuffer Cars, GpuBuffer Walkers, GpuBuffer Commands, GpuBuffer[] CarViews, GpuBuffer[] WalkerViews) Drawn =>
        (_carRows, _walkerRows, _tyres.Table.Commands.Device, _carViews, _walkerViews);

    static uint Groups(int count) => (uint)Math.Max(1, (count + Group - 1) / Group);

    ComputeKernel Kernel(string shader, int buffers, uint pushBytes, int sets = 1)
    {
        var kernel = new ComputeKernel(_vk, shader, buffers, pushBytes, sets);
        _kernels.Add(kernel);
        return kernel;
    }

    /// <summary>Where the host writes a tick's signals, and the device-local copy the kernel reads, each holding <paramref name="count"/>.</summary>
    (GpuBuffer Written, GpuBuffer In) SignalBuffers(int count)
    {
        var bytes = Bytes<SignalRow>(Math.Max(Group, count));
        return (
            Own(_vk.CreateBuffer(bytes, BufferUsageFlags.TransferSrcBit, HostAccess.Writes)),
            Own(_vk.CreateBuffer(bytes, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None)));
    }

    GpuBuffer Mapped<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(Math.Max(1, count)), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));

    GpuBuffer Device<T>(int count) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None));

    GpuBuffer Laid<T>(T[] rows) where T : unmanaged
    {
        var buffer = Own(_vk.CreateBuffer(
            Bytes<T>(Math.Max(1, rows.Length)), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None));
        buffer.Upload<T>(rows.Length, into => rows.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    public override void Dispose()
    {
        _vk.Api.DeviceWaitIdle(_vk.Device);
        Sprites?.Dispose();
        foreach (var kernel in _kernels) kernel.Dispose();
        _batch.Dispose();
        _atlas.Dispose();
        _lanes.Dispose();
        _tyres.Dispose();
        _solver.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
