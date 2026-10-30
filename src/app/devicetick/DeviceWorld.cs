using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using static TrafficSimulation.App.DeviceTick.DeviceHoldings;

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
/// (<see cref="Finish"/>), so the device steps while the host decides (<see cref="SimFigures.WorldStepsAhead"/>). Nothing the
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
internal sealed partial class DeviceWorld : WorldLayer
{
    /// <summary>A control signal as the device takes it (<c>WorldSignal</c>): the agent's body row, never the agent.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct SignalRow(int Body, uint ActionAndFlags, Vector2 P01, Vector2 P23, Vector2 P45);

    /// <summary>One car as the device keeps it beside its tyres (<c>WorldCar</c>).</summary>
    /// <param name="Towing">What its arm holds (<see cref="TowingCode"/>).</param>
    /// <param name="ForkAlongM">And where along that body's axis the fork is, from its middle (<see cref="TowBar.ForkAlongM"/>).</param>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CarRow(int Body, int Towing, uint Flags, float MassKg, float ForkAlongM);

    /// <summary>
    /// <b>What an arm holds, as a car's row names it</b> (<c>WorldCar.towing</c>): a car by its index, a rail vehicle as
    /// −2 less its index, nothing as −1 — so what draws the arm finds either without a second field.
    /// </summary>
    int TowingCode(int loadAgent) =>
        loadAgent < 0 ? -1 : _laying.IsAWagon(loadAgent) ? -2 - _laying.WagonOfAgent(loadAgent) : _laying.Roster.CarIndex(loadAgent);

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
        public uint EnabledBit;
        public uint FrozenBit;
        public uint FiledFrozenBit;
        public float WalkSpeedMps;
        public float TurnRateDegPerS;
        public float FootGripMps2;
        public float SlidingGripMps2;
        public float AimUnderfootM2;
        public float OnTheTrailerAxleShare;
        public float LookaheadS;
        public int FirstWagonBody;
        public int RailSegments;
        public uint RailPasses;
        public float RailOntoTheLine;
        public float RailFriction;
        public float RailRolling;
        public float RailGravityMps2;
        public float HandBoostSpeedShare;
        public float MostTiltOverNothingRad;
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

    /// <summary>Which car and which walker a body is, or −1 (<c>world_signals.comp</c>'s <c>agentOf</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal record struct AgentOf(int Car, int Walker);

    /// <summary>A tick's signals as written for the device: the commands, aims and shows from the front, the rest from the back.</summary>
    readonly record struct SignalCounts(int Commands, int Events)
    {
        public bool Any => Commands > 0 || Events > 0;
    }

    const int CarCoversEach = 16;
    const int WalkerCoversEach = 8;

    /// <summary>What the view hands back beside itself (<c>world_view.comp</c>): the covers' four counts.</summary>
    const int CountsOut = 4;

    readonly Vk _vk;
    readonly WorldLaying _laying;
    readonly int _cars, _walkers;
    readonly float _heldAroundAWheelM;
    readonly DeviceHoldings _held;

    readonly DeviceSolver _solver;
    readonly DeviceTyres _tyres;
    readonly DeviceAtlas _atlas;
    readonly DeviceLanes _lanes;
    readonly ComputeBatch _batch;

    /// <summary>What each car follows as last signalled, and where the view last read it (<see cref="LineFollowed"/>).</summary>
    readonly GpuBuffer _follows;

    readonly GpuBuffer _carRows, _walkerRows, _carRanges, _walkerRanges, _counts, _countsOut;
    GpuBuffer _signalsWritten, _signalsIn;

    /// <summary>
    /// The covers as the device gathers them, and the lists the host reads them from (<c>world_covers_out.comp</c>): a
    /// body's covers written straight across the bus, a run a thread, cost the step several times their copy in whole lines.
    /// </summary>
    GpuBuffer _carCoversGathered, _walkerCoversGathered, _carCovers, _walkerCovers;

    int _carRoom, _walkerRoom;

    readonly ComputeKernel _signalsKernel, _walkersKernel, _towKernel, _coversKernel, _coversOutKernel, _viewKernel, _walkersViewKernel;
    BoundSet _signalsSet, _coversSet, _coversOutSet;
    readonly BoundSet _walkersSet, _towSet;

    /// <summary>
    /// The view's two passes, cars' and walkers', each bound to each of the two views' rows (<see cref="WorldView.Slot"/>),
    /// which the host reads where they are written: the step in flight writes the one not shown.
    /// </summary>
    readonly BoundSet[] _viewSets = new BoundSet[2], _walkersViewSets = new BoundSet[2];

    /// <summary>The two views' rows, by slot, which the host reads where they are written.</summary>
    readonly GpuBuffer[] _carViews = new GpuBuffer[2];

    readonly GpuBuffer[] _walkerViews = new GpuBuffer[2];

    /// <summary>
    /// The trains' wagons (TRN-7): how many, the look each was last signalled to wear, and their view's pass and rows by
    /// slot, which the host reads where they are written as it reads the rest.
    /// </summary>
    readonly int _wagons;

    /// <summary>
    /// The look each wagon was last signalled to wear, written by the host as it files the signal (<see cref="RowOf"/>) and
    /// read by the wagons' view: <c>world_signals.comp</c> already binds the sixteen storage buffers a browser lets one
    /// stage have. A look changes only as its train is sent, while the wagon is out of the world and drawn by no view.
    /// </summary>
    readonly GpuBuffer _wagonLooks;
    readonly ComputeKernel _wagonsViewKernel;
    readonly BoundSet[] _wagonsViewSets = new BoundSet[2];
    readonly GpuBuffer[] _wagonViews = new GpuBuffer[2];

    /// <summary>The links the solve holds (SOL-38), each in its slot (<see cref="WorldLaying.LinkSlots"/>), as the signals leave them (<see cref="Tow"/>).</summary>
    readonly LinkSlots _links;

    /// <summary>The link slot each tractor's tow is held in, or −1 (EVA-5).</summary>
    readonly int[] _towSlot;

    /// <summary>The railway and the rail vehicles' wheels (<see cref="DeviceRail"/>).</summary>
    readonly DeviceRail _rail;

    /// <summary>What each body is (<see cref="BodyTag.Packed"/>), laid once off the laying's numbering.</summary>
    readonly GpuBuffer _tags;

    /// <summary>
    /// <b>Every body's pose as each view was taken</b>, by slot (<see cref="WorldView.Slot"/>), once an instrument asks for
    /// them (<see cref="KeepTheBodiesAtTheView"/>): the solver's own runs a step ahead of the view the host shows.
    /// </summary>
    GpuBuffer[]? _posesAtView;

    /// <summary>How many views to come copy the whole table rather than its moving rows: one a slot, as the poses are first kept.</summary>
    int _posesOwedWhole;

    WorldPush _push;

    /// <param name="atlas">
    /// The atlas, where the town filed it straight into the device as it was laid (<see cref="DeviceWorldMaker"/>); laid
    /// here off the host's otherwise.
    /// </param>
    public DeviceWorld(Vk vk, WorldLaying laying, DeviceAtlas? atlas = null) : base(laying)
    {
        _vk = vk;
        _held = new DeviceHoldings(vk);

        // The furniture and the atlas are read here, to stand the device's own, and never again: kept, they would be the
        // host's largest tables for the run (the root decision log, 2026-10-13).
        _laying = laying.WithoutTheGround();
        _cars = laying.Cars.Length;
        _walkers = laying.Walkers.Length;
        _wagons = laying.Wagons.Length;
        _last = new LastInBatch[laying.Agents];
        _links = new LinkSlots(laying.LinkSlots);
        _towSlot = new int[_cars];
        Array.Fill(_towSlot, -1);
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
        _atlas = atlas ?? new DeviceAtlas(
            vk, (laying.Atlas ?? throw new ArgumentException("A world laid with no atlas is handed the device's.", nameof(atlas))).Table);
        _lanes = new DeviceLanes(vk, laying.Roads);
        _batch = new ComputeBatch(vk);

        _carRows = _held.Laid<CarRow>(CarRowsOf());
        var followsNothing = new LineFollowed[_cars];
        Array.Fill(followsNothing, LineFollowed.None);
        _follows = _held.Laid<LineFollowed>(followsNothing);
        _walkerRows = _held.Laid<WalkerRow>(WalkerRowsOf(stood));
        var agentOf = new AgentOf[table.Count];
        Array.Fill(agentOf, new AgentOf(-1, -1));
        for (var car = 0; car < _cars; car++) agentOf[BodyOfCar(car)].Car = car;
        for (var person = 0; person < _walkers; person++) agentOf[laying.BodyOf(person)].Walker = person;
        var agentOfBuffer = _held.Laid<AgentOf>(agentOf);
        var tags = new int[table.Count];
        for (var body = 0; body < tags.Length; body++) tags[body] = laying.TagOf(body).Packed;
        _tags = _held.Laid<int>(tags);

        _carRanges = _held.Mapped<CoverRange>(_cars);
        _walkerRanges = _held.Mapped<CoverRange>(_walkers);
        _counts = _held.Device<uint>(4);
        _countsOut = _held.Mapped<uint>(CountsOut);
        _carRoom = Math.Max(Group, _cars * CarCoversEach);
        _walkerRoom = Math.Max(Group, _walkers * WalkerCoversEach);
        LayTheCovers();
        (_signalsWritten, _signalsIn) = SignalBuffers(Signals.Capacity);

        var (pose, heading, motion, mass, filter) = _solver.Bodies;
        var (shape, bounds, motionIntoTick) = _solver.Rows;
        var tyres = _tyres.Table;

        _wagonLooks = _held.Written<uint>(Math.Max(1, _wagons));
        _wagonLooks.Span<uint>().Clear();
        var pushBytes = (uint)Marshal.SizeOf<WorldPush>();
        _signalsKernel = _held.Kernel("world_signals.comp", 16, pushBytes);
        _signalsBound =
        [
            null!, agentOfBuffer, tyres.Commands.Device, tyres.Cars.Device, tyres.Wheels.Device, _carRows,
            _walkerRows, pose, heading, motion, motionIntoTick, shape, bounds, filter, tyres.Poses.Device, _follows,
        ];
        _signalsSet = BindTheSignals();

        _walkersKernel = _held.Kernel("world_walkers.comp", 4, pushBytes);
        _walkersSet = _walkersKernel.Bind([_walkerRows, pose, motion, mass]);

        _towKernel = _held.Kernel("world_tow.comp", 11, pushBytes);
        _towSet = _towKernel.Bind(
        [
            _carRows, tyres.Builds, tyres.BuildOf.Device, tyres.Poses.Device, tyres.Cars.Device, _tyres.WheelGrounds,
            _tyres.ImpulseTable, tyres.Wheels.Device, pose, motion, mass,
        ]);

        _coversKernel = _held.Kernel("world_covers.comp", 12 + _atlas.Tables.Length, (uint)Marshal.SizeOf<CoversPush>());
        _coversBound =
        [
            _carRows, _walkerRows, tyres.Builds, tyres.BuildOf.Device, pose, filter, tyres.Cars.Device, null!, null!,
            _counts, _carRanges, _walkerRanges, .. _atlas.Tables,
        ];
        _coversOutKernel = _held.Kernel("world_covers_out.comp", 5, (uint)Marshal.SizeOf<CoversOutPush>());
        _coversSet = BindTheCovers();

        _viewKernel = _held.Kernel("world_view.comp", 13 + _lanes.Tables.Length, pushBytes, sets: _viewSets.Length);
        _walkersViewKernel = _held.Kernel("world_view_walkers.comp", 9, pushBytes, sets: _walkersViewSets.Length);
        _wagonsViewKernel = _held.Kernel("world_view_wagons.comp", 6, pushBytes, sets: _wagonsViewSets.Length);
        _rail = new DeviceRail(_held, laying, pose, motion, mass, filter, pushBytes);
        for (var slot = 0; slot < _viewSets.Length; slot++)
        {
            var carViews = _carViews[slot] = _held.Mapped<CarView>(_cars);
            var walkerViews = _walkerViews[slot] = _held.Mapped<WalkerView>(_walkers);
            var wagonViews = _wagonViews[slot] = _held.Mapped<WagonView>(Math.Max(1, _wagons));
            ViewInSlot(slot).ReadOver(carViews.Span<CarView>(), walkerViews.Span<WalkerView>());
            ViewInSlot(slot).ReadWagonsOver(wagonViews.Span<WagonView>());
            _wagonsViewSets[slot] = _wagonsViewKernel.Bind([pose, heading, filter, _wagonLooks, wagonViews, motion]);
            _viewSets[slot] = _viewKernel.Bind(
            [
                _carRows, _tyres.ImpulseTable, tyres.Poses.Device, tyres.Cars.Device, pose, heading, motion, filter,
                _solver.Overlap, carViews, _follows, tyres.Builds, tyres.BuildOf.Device, .. _lanes.Tables,
            ]);
            _walkersViewSets[slot] = _walkersViewKernel.Bind(
                [_walkerRows, pose, motion, _solver.Overlap, walkerViews, _counts, _tyres.GroundMarkRing, _countsOut, filter]);
        }

        _push = new WorldPush
        {
            DtS = config.TickSeconds,
            EnabledBit = (uint)BodyFlags.Enabled,
            FrozenBit = (uint)BodyFlags.Frozen,
            FiledFrozenBit = (uint)BodyFlags.FiledFrozen,
            WalkSpeedMps = config.PersonWalkSpeedMps,
            TurnRateDegPerS = config.PersonTurnRateDegPerS,
            FootGripMps2 = config.PersonFootGripMps2,
            SlidingGripMps2 = config.PersonSlidingGripMps2,
            AimUnderfootM2 = config.Person.AimUnderfootM2,
            OnTheTrailerAxleShare = config.Evacuator.OnTheTrailerAxleShare,
            LookaheadS = config.Driving.LookaheadS,
            FirstWagonBody = laying.BodyOf(laying.AgentOfWagon(0)),
            RailSegments = laying.Rails.SegmentCount,
            RailPasses = (uint)config.Solver.VelocityIterations,
            RailOntoTheLine = 1f / config.Train.WheelOntoTheLineS,
            RailFriction = config.Train.WheelRailFriction,
            RailRolling = config.Train.RollingResistance,
            RailGravityMps2 = config.Tyre.StandardGravityMps2,
            HandBoostSpeedShare = config.Control.HandBoostSpeedShare,
            MostTiltOverNothingRad = config.CarTiltOverNothingMostRad,
        };

        // Every car read off its body as laid, and the first view and covers off the world as it was laid, before any step —
        // the covers once the atlas they are read off is the device's own (Ready).
        _coversOwed = !_atlas.Filed;
        using var carsKernel = new ComputeKernel(vk, "world_cars.comp", 6, pushBytes);
        var carsSet = carsKernel.Bind([_carRows, tyres.Poses.Device, pose, heading, motion, filter]);
        var commands = _batch.Begin();
        _solver.Send(commands);
        _tyres.SendMarked(commands);
        _vk.Fill(commands, _counts, 0);
        _vk.AfterTransfer(commands);
        carsKernel.Dispatch(commands, carsSet, Groups(_cars), _push with { Count = (uint)_cars });
        _vk.BetweenKernels(commands);
        if (!_coversOwed)
        {
            RecordTheCovers(commands);
            _vk.BetweenKernels(commands);
            RecordTheView(commands, View, coversRead: true);
        }

        _batch.SubmitAndWait();
        _firstViewOwed = !_coversOwed;
        TakeTheFirstView();
    }

    /// <summary>
    /// Whether the first covers are still to be read, off an atlas the device is still filing: a page lays its world over
    /// a word standing in for the tables, and reads the covers once they are laid (<see cref="DeviceAtlas.Advance"/>).
    /// </summary>
    bool _coversOwed;

    /// <summary>
    /// The covers set bound to the atlas's own tables, now they are laid, and the first covers and view read off the world as
    /// it was laid — the queue runs them after the atlas's filing.
    /// </summary>
    void ReadTheFirstCovers()
    {
        _coversBound[^1] = _atlas.Tables[0];
        _coversSet = BindTheCovers();
        var commands = _batch.Begin();
        _vk.Fill(commands, _counts, 0);
        _vk.AfterTransfer(commands);
        RecordTheCovers(commands);
        _vk.BetweenKernels(commands);
        RecordTheView(commands, View, coversRead: true);
        _batch.Submit();
        _firstViewOwed = true;
        _coversOwed = false;
    }

    /// <summary>Whether the step in flight reads the covers.</summary>
    bool _readingCovers;

    /// <summary>Whether the view the world was laid with is still to be taken, on a head whose answer comes later.</summary>
    bool _firstViewOwed;

    /// <inheritdoc/>
    /// <remarks>
    /// A page's batch lands between frames, and the first view it answers is taken here once it has — after the atlas the
    /// page is still filing is laid and its first covers read (<see cref="ReadTheFirstCovers"/>), and what the filing found
    /// is checked as it comes back.
    /// </remarks>
    public override bool Ready
    {
        get
        {
            if (!_batch.Readable) return false;

            if (_coversOwed)
            {
                if (_atlas.Advance()) ReadTheFirstCovers();
                return false;
            }

            _atlas.Check();
            TakeTheFirstView();
            return true;
        }
    }

    void TakeTheFirstView()
    {
        if (!_firstViewOwed || !_batch.Readable) return;

        _batch.Wait();
        TakeTheView(View, coversRead: true);
        _firstViewOwed = false;
    }

    readonly GpuBuffer[] _signalsBound;
    readonly GpuBuffer[] _coversBound;

    /// <summary>The signals' set over the buffers as they now are, rewritten in place once there is one.</summary>
    BoundSet BindTheSignals()
    {
        _signalsBound[0] = _signalsIn;
        return _signalsKernel.Bind(_signalsBound, _signalsSet);
    }

    /// <summary>The covers' set and their copy's over the lists as they now are, each rewritten in place once there is one.</summary>
    BoundSet BindTheCovers()
    {
        _coversBound[7] = _carCoversGathered;
        _coversBound[8] = _walkerCoversGathered;
        _coversOutSet = _coversOutKernel.Bind(
            [_counts, _carCoversGathered, _walkerCoversGathered, _carCovers, _walkerCovers], _coversOutSet);
        return _coversKernel.Bind(_coversBound, _coversSet);
    }

    /// <summary>Both lists of covers, the device's and the host's, at the rooms as they now stand.</summary>
    [MemberNotNull(nameof(_carCoversGathered), nameof(_walkerCoversGathered), nameof(_carCovers), nameof(_walkerCovers))]
    void LayTheCovers()
    {
        _carCoversGathered = _held.Device<WayCover>(_carRoom);
        _walkerCoversGathered = _held.Device<WayCover>(_walkerRoom);
        _carCovers = _held.Mapped<WayCover>(_carRoom);
        _walkerCovers = _held.Mapped<WayCover>(_walkerRoom);
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
                laid.PositionM, laid.HeadingRad, Vector2.Zero, 0f, stood.MassOf(body)));
            _tyres.Commands[car] = DeviceTyres.CommandRow.Of(DriveCommand.Parked);
            _tyres.Cars[car] = new DeviceTyres.CarRow(1f / stood.InverseInertiaOf(body), 1f, 0u);
            for (var wheel = 0; wheel < TyreModel.MostWheels; wheel++)
            {
                _tyres.Wheels[(car * TyreModel.MostWheels) + wheel] = new DeviceTyres.WheelRow(laid.PositionM, 0f, 0f, 0f, 0u);
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
            rows[car] = new CarRow(BodyOfCar(car), -1, 0u, _laying.Builds.Of(_laying.Cars[car].Variant).MassKg, 0f);
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
    /// <exception cref="NotSupportedException">On a head that cannot wait: off the clock the answer is wanted at once.</exception>
    protected override void TakeTheSignalsAndReadCovers(WorldView into)
    {
        if (!_batch.Waits) throw new NotSupportedException("The covers are read off the clock only where the host can wait for them.");

        var signals = WriteTheSignals();
        var commands = _batch.Begin();
        _solver.Send(commands);
        SendTheSignals(commands, signals);
        _vk.Fill(commands, _counts, 0);
        _vk.AfterTransfer(commands);
        if (RecordTheSignals(commands, signals)) _vk.BetweenKernels(commands);
        RecordTheCovers(commands);
        _vk.BetweenKernels(commands);
        RecordTheView(commands, into, coversRead: true);
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
        RecordTheView(commands, into, coversRead: true);
        _batch.SubmitAndWait();
        if (TakeTheView(into, coversRead: true)) ReadCovers(into);
    }

    /// <remarks>
    /// Covers that ran past their lists are read again into lists grown to hold them; on a head that cannot wait, the view
    /// goes without them (<see cref="WorldView.CoversRead"/>) and the next step reads into the grown lists.
    /// </remarks>
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

        if (TakeTheView(into, _readingCovers) && _batch.Waits) ReadCovers(into);
        TakeTheTouches(into);
    }

    /// <remarks>
    /// Nothing is copied back behind the step: the solver's totals come back from its last kernel, and the view's counts
    /// and the marks laid with the view.
    /// </remarks>
    protected override void Begin(float dtS, WorldView into, bool readCovers)
    {
        _rail.ForgetThePutDowns();
        var signals = WriteTheSignals();
        RestTheLinkedAfresh();
        _push.DtS = dtS;

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
        _towKernel.Dispatch(commands, _towSet, Groups(_cars), _push with { Count = (uint)_cars });
        _rail.Record(commands, _push);
        _vk.BetweenKernels(commands);
        _solver.Record(commands, dtS);
        _vk.BetweenKernels(commands);
        if (readCovers)
        {
            RecordTheCovers(commands);
            _vk.BetweenKernels(commands);
        }

        RecordTheView(commands, into, readCovers, stepped: true);
        if (Smoke is { } smoke)
        {
            _vk.BetweenKernels(commands);
            smoke.Record(commands, dtS);
        }

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
            _held.Release(_signalsWritten);
            _held.Release(_signalsIn);
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
            if (TakenOnTheHost(signal)) continue;

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

    /// <summary>
    /// <b>Whether a signal is the host's to take</b> rather than <c>world_signals.comp</c>'s: a rail vehicle's pedals, its
    /// coupling and a switch, which the host writes where the rail pass reads them (<see cref="TakeOnTheHost"/>).
    /// </summary>
    bool TakenOnTheHost(in ControlSignal signal)
    {
        if (signal.Agent < _laying.Roster.Count || signal.Action is not (ControlAction.Drive or ControlAction.Couple or ControlAction.Switch)) return false;

        TakeOnTheHost(signal);
        return true;
    }

    /// <summary>
    /// A rail vehicle's pedals, its coupling or a switch, taken where the rail pass and the solve read them, as
    /// <see cref="ManagedWorld"/> takes them in its own order.
    /// </summary>
    void TakeOnTheHost(in ControlSignal signal)
    {
        var wagon = signal.Agent - _laying.Roster.Count;
        switch (signal.Action)
        {
            case ControlAction.Drive:
                _rail.Drive(wagon, signal.Command);
                break;

            case ControlAction.Switch:
                _rail.Set(signal.Other, signal.Flags);
                break;

            default:
                var slot = _laying.LinkSlotOfWagon(wagon);
                if (signal.Other < 0)
                {
                    _links.Unlink(slot);
                    break;
                }

                var ahead = signal.Other - _laying.Roster.Count;
                var (aheadM, behindM, restM) = _laying.Wagons[wagon].CoupledBehind(_laying.Wagons[ahead]);
                _links.Link(slot, new LinkRow(_laying.BodyOf(signal.Other), _laying.BodyOf(signal.Agent), aheadM, behindM, restM));
                break;
        }
    }

    /// <summary>How many signal rows the buffers hold, the events counted from the last of them back.</summary>
    int RoomForSignals => (int)(_signalsIn.SizeBytes / (ulong)Unsafe.SizeOf<SignalRow>());

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
                if (signal.Agent >= _laying.Roster.Count && signal.Agent < _laying.Roster.Count + _wagons)
                {
                    _wagonLooks.Span<uint>()[signal.Agent - _laying.Roster.Count] = signal.Flags;
                    _rail.PutDown(signal.Agent - _laying.Roster.Count);
                }

                return new SignalRow(
                    body, actionAndFlags, p01, Shape.Rotation(signal.Params.Z), new Vector2(signal.Params.Z, signal.Params.W));

            case ControlAction.Contain:
                _solver.CountAfresh(body);
                break;

            case ControlAction.Layer:
                var layer = (CollisionLayer)signal.Flags;
                return new SignalRow(
                    body, actionAndFlags,
                    new Vector2(
                        BitConverter.UInt32BitsToSingle(BodyRows.LayerByte((ulong)layer)),
                        BitConverter.UInt32BitsToSingle(BodyRows.LayerByte(PhysicsWorld.MaskOf(layer)))),
                    default, default);

            case ControlAction.Tow:
                // world_signals.comp takes what the arm holds as a code (TowingCode), where along it the fork is, and the
                // load's body to thaw.
                var load = signal.Other;
                var forkAlongM = Tow(_laying.Roster.CarIndex(signal.Agent), load, (signal.Flags & ControlSignal.ByTheTail) != 0);
                return new SignalRow(
                    body, actionAndFlags, new Vector2(BitConverter.Int32BitsToSingle(TowingCode(load)), forkAlongM),
                    new Vector2(BitConverter.Int32BitsToSingle(load < 0 ? -1 : _laying.BodyOf(load)), 0f), default);
        }

        return new SignalRow(body, actionAndFlags, p01, p23, default);
    }

    /// <summary>
    /// <b>A tractor's arm as the signal leaves it</b> (EVA-5): whatever it held let go of, and the pin the solver holds
    /// between its hinge and the load's eye, in the slot of the end it holds, as <see cref="ManagedWorld"/> links it —
    /// laid for the solve before the step (<see cref="RestTheLinkedAfresh"/>). Where along the load the fork is.
    /// </summary>
    float Tow(int tractor, int loadAgent, bool byTheTail)
    {
        if (_towSlot[tractor] >= 0) _links.Unlink(_towSlot[tractor]);

        _towSlot[tractor] = -1;
        if (loadAgent < 0) return 0f;

        var tractorBuild = _laying.Builds.Of(_laying.Cars[tractor].Variant);
        var (eyeM, forkAlongM) = _laying.TowedAt(loadAgent, tractorBuild.TowReachM, byTheTail);
        var slot = _towSlot[tractor] = _laying.LinkSlotOfTow(loadAgent, byTheTail);
        _links.Link(slot, new LinkRow(BodyOfCar(tractor), _laying.BodyOf(loadAgent), TowBar.HookOnTheBodyM(tractorBuild), eyeM, 0f));
        return forkAlongM;
    }

    /// <summary>
    /// The links laid for the solve where they changed, and every linked body's rest counted afresh by the step, as
    /// <see cref="PhysicsWorld"/> counts it (SOL-38).
    /// </summary>
    void RestTheLinkedAfresh()
    {
        if (_links.Changed) _solver.LayTheLinks(_links.Live);
        foreach (ref readonly var link in _links.Live)
        {
            _solver.CountAfresh(link.First);
            _solver.CountAfresh(link.Second);
        }
    }

    /// <summary>The rows <see cref="WriteTheSignals"/> wrote copied to where the device reads them, recorded; the caller barriers after.</summary>
    void SendTheSignals(Recording commands, SignalCounts signals)
    {
        var rowBytes = (ulong)Unsafe.SizeOf<SignalRow>();
        var eventsFromBytes = (ulong)(RoomForSignals - signals.Events) * rowBytes;
        Span<CopyRun> runs = stackalloc CopyRun[2];
        runs[0] = new CopyRun(0, 0, (ulong)signals.Commands * rowBytes);
        runs[1] = new CopyRun(eventsFromBytes, eventsFromBytes, (ulong)signals.Events * rowBytes);
        var from = signals.Commands > 0 ? 0 : 1;
        var to = signals.Events > 0 ? 2 : 1;
        if (from < to) _vk.Copy(commands, _signalsWritten, _signalsIn, runs[from..to]);
    }

    /// <summary>The signals' kernels recorded, each only where it has any; whether any was.</summary>
    bool RecordTheSignals(Recording commands, SignalCounts signals)
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

    void RecordTheCovers(Recording commands) => _coversKernel.Dispatch(
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

    /// <summary>
    /// Every car's view and every walker's, in two dispatches that need nothing of each other, into the rows
    /// <paramref name="into"/> reads, and the counts beside it — and, beside them, the covers copied to the host's lists.
    /// </summary>
    /// <param name="coversRead">Whether the covers were read ahead of the view, and so are copied out with it.</param>
    /// <param name="stepped">
    /// Whether the pass is the step's own, keeping what the step settled; a view written again off the clock only reads
    /// (<c>world_view.comp</c>), so writing it twice moves nothing.
    /// </param>
    void RecordTheView(Recording commands, WorldView into, bool coversRead, bool stepped = false)
    {
        if (coversRead)
        {
            _coversOutKernel.Dispatch(
                commands, _coversOutSet, Groups(_cars + _walkers), new CoversOutPush((uint)_carRoom, (uint)_walkerRoom));
        }

        var mode = stepped ? ViewStepped : 0u;
        if (_cars > 0) _viewKernel.Dispatch(commands, _viewSets[into.Slot], Groups(_cars), _push with { Count = (uint)_cars, Mode = mode });
        _walkersViewKernel.Dispatch(
            commands, _walkersViewSets[into.Slot], Groups(_walkers), _push with { Count = (uint)_walkers, Mode = mode });
        if (_wagons > 0) _wagonsViewKernel.Dispatch(commands, _wagonsViewSets[into.Slot], Groups(_wagons), _push with { Count = (uint)_wagons });
        if (_posesAtView is { } poses) KeepThePoses(commands, poses[into.Slot]);
    }

    /// <summary>The poses the view was just taken at, copied where an instrument reads them by the view's slot: the moving rows, or the whole table while it is owed.</summary>
    void KeepThePoses(Recording commands, GpuBuffer into)
    {
        var pose = _solver.Bodies.Pose;
        var row = (ulong)Marshal.SizeOf<Vector4>();
        var from = _posesOwedWhole > 0 ? 0 : _solver.FirstMoving;
        if (_posesOwedWhole > 0) _posesOwedWhole--;

        var rows = (ulong)(_solver.BodyCount - from);
        if (rows == 0) return;

        _vk.BeforeTransfer(commands);
        _vk.Copy(commands, pose, into, [new CopyRun((ulong)from * row, (ulong)from * row, rows * row)]);
    }

    /// <summary>
    /// <b>Every body's pose kept as each view is taken</b>, from the next view on (<see cref="Bodies"/>): an instrument drawing
    /// over the bodies reads them where the view the host shows put them, and not a step ahead with the solver.
    /// </summary>
    public override void KeepTheBodiesAtTheView()
    {
        if (_posesAtView is not null) return;

        _posesAtView = [_held.Device<Vector4>(_solver.BodyCount), _held.Device<Vector4>(_solver.BodyCount)];
        _posesOwedWhole = _posesAtView.Length;
    }

    /// <summary>Whether every slot's poses have been laid whole since they were first kept, so an instrument may read either.</summary>
    public bool PosesKept => _posesAtView is not null && _posesOwedWhole == 0;

    /// <summary>
    /// <b>The body table as an instrument reads it</b>, every body of every kind in the one numbering: the poses at each
    /// view by slot (<see cref="KeepTheBodiesAtTheView"/>, empty until asked for), the shapes, the filters, the motion, what
    /// each body is, and the links with how many are live.
    /// </summary>
    public DeviceBodies Bodies => new(
        _posesAtView ?? [], _solver.Rows.Shape, _solver.Bodies.Filter, _solver.Bodies.Motion, _tags, _solver.Links.Rows,
        _solver.Links.Count, _solver.BodyCount, _solver.FirstMoving);

    /// <summary><c>world_covers_out.comp</c>'s push constants: each list's room.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CoversOutPush(uint CarRoom, uint WalkerRoom);

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
        foreach (var buffer in (ReadOnlySpan<GpuBuffer>)[_carCoversGathered, _walkerCoversGathered, _carCovers, _walkerCovers]) _held.Release(buffer);

        LayTheCovers();
        _coversSet = BindTheCovers();
    }

    /// <summary>Every pair the step began touching, by what each body is, with how each came into it.</summary>
    void TakeTheTouches(WorldView view)
    {
        view.TouchCount = 0;
        foreach (ref readonly var begun in _solver.Begun)
        {
            view.AddTouch(new Touch(
                _laying.TagOf((int)begun.First), _laying.TagOf((int)begun.Second), begun.Normal,
                new Approach(begun.FirstIntoMps, begun.SecondIntoMps)));
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

    /// <summary>Where the host writes a tick's signals, and the device-local copy the kernel reads, each holding <paramref name="count"/>.</summary>
    (GpuBuffer Written, GpuBuffer In) SignalBuffers(int count)
    {
        var room = Math.Max(Group, count);
        return (_held.Staging<SignalRow>(room), _held.Room<SignalRow>(room));
    }

    public override void Dispose()
    {
        _vk.WaitIdle();
        DisposeTheSprites();
        _held.DisposeTheKernels();
        _batch.Dispose();
        _atlas.Dispose();
        _lanes.Dispose();
        _tyres.Dispose();
        _solver.Dispose();
        _held.Dispose();
    }

    /// <summary>What draws off this world let go of, by the head that draws it (<c>DeviceWorld.Sprites.cs</c>).</summary>
    partial void DisposeTheSprites();

    /// <summary>
    /// The cars' and the rail vehicles' smoke's tracks (CAR-54, TRN-15), laid behind every step from
    /// <see cref="LayTheSmoke"/> on; none before.
    /// </summary>
    public DeviceSmoke? Smoke { get; private set; }

    /// <summary>
    /// The smoke's tracks laid behind every step from here on, for whatever draws the smoke off them — the emit
    /// (<c>DeviceWorld.Sprites.cs</c>). A world nothing draws smoke off lays none.
    /// </summary>
    public void LayTheSmoke() =>
        Smoke ??= new DeviceSmoke(
            _held, Config, _cars, _wagons, _wagons > 0 ? _laying.BodyOf(_laying.AgentOfWagon(0)) : 0, _carRows,
            _solver.Bodies.Pose, _solver.Bodies.Motion, _solver.Bodies.Filter, _tyres.Table.Commands.Device, _rail.Commands);
}
