using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The frame's moving sprites emitted on the device</b> (<c>app/render/kernels/sprites_count.comp</c>,
/// <c>sprites_write.comp</c>): the walkers, every part of every car, the signal heads and the cars on the level above,
/// written into the over and above runs in <see cref="TownSprites.FillMoving"/>'s order and culled against the view as
/// it culls — drawn where they are written (<see cref="EmitForTheFrame"/>, <see cref="TownRenderer.DrawTheMovingFrom"/>),
/// or read back to be held to the host's fill (<see cref="Emit"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Count, scan, write.</b> One thread a unit counts what each part of it draws into a slot laid in draw order —
/// the walkers', each car part's a run apiece, the heads', the car parts again for the level above — a
/// <see cref="PrefixScan"/> turns the counts into where each part starts, and a second pass writes every part there
/// and the two draws' instance counts. The order is the host's whatever order the threads ran in; fixed slots a car
/// would leave the parts interleaved car by car where the host lays them part by part, and need a compaction scan all
/// the same.
/// </para>
/// <para>
/// <b>The host lays every unit's row from its own fleets</b> each frame, with how far a car's line bends ahead of it
/// (<see cref="CarLamps.TurnAheadRad"/>) in place of the line. <b>Bound to a car tick</b>, a car the tick still holds as
/// it left it (<see cref="DeviceCarTick.Takes"/>) takes what that tick wrote over the town's copy
/// (<c>sprites_cars.comp</c>) — so the town need not read every car back for the frame drawn after a tick. What looks are,
/// where the lenses are, the heads and the lights' tables are laid once. The view, the clock and the live rosters are a
/// frame block in a buffer rather than push constants, because the frame that will carry this pass is recorded once
/// (<see cref="CameraView"/>).
/// </para>
/// </remarks>
internal sealed class DeviceSprites : IDisposable
{
    /// <summary>One walker as the emit reads it (<c>PersonRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PersonRow(Vector2 PositionM, float HeadingRad, float DistanceWalkedM, uint Variant, uint Flags)
    {
        public const uint Inside = 1u;
        public const uint Down = 2u;
        public const uint Walking = 4u;
    }

    /// <summary>
    /// One car as the emit reads it (<c>CarRow</c>, <c>sprites_car.glsl</c>): its pose, what its lamps answer — how far
    /// its line bends ahead of it among them — and its tow.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CarRow(
        Vector2 PositionM, Vector2 Forward, float HeadingRad, float SteerRad, float BrakeMps2, float ProgressM, float AlongMps,
        float ContextPassAsideM, float ToTheBoxM, float FuseJitter, float PassEndsM, float PassSteppedOutM, float PassBackM,
        float PassAsideM, uint Variant, uint Flags, int Towing, float TurnAheadRad)
    {
        public const uint Broken = 1u;
        public const uint Driven = 2u;
        public const uint Reverse = 4u;
        public const uint BlueLight = 8u;
        public const uint AtWork = 16u;
        public const uint TurningAtTheBox = 32u;
        public const uint LineIsReverse = 64u;
        public const uint PassBegun = 128u;
        public const uint HeldByTheTail = 256u;

        /// <summary>The car tick holds it as it left it, and what that tick wrote is laid over the town's (<c>sprites_cars.comp</c>).</summary>
        public const uint FromTheDevice = 512u;

        public const int LevelShift = 16;
    }

    /// <summary>The figures <c>sprites_cars.comp</c> is pushed.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct LayPush(uint CarCount, uint ArcsPerCar, float TurnAheadM);

    /// <summary>One walker's look (<c>PersonLook</c>): its quad standing and lying down.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PersonLook(Vector2 StandingHalfM, Vector2 DownHalfM, float DownReachM, float HeightM);

    /// <summary>One car's look (<c>CarLook</c>): every part's quad and reach, and its arm's two pictures.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CarLook(
        Vector2 BodyHalfM, Vector2 WreckHalfM, float BodyReachM, float WreckReachM, Vector2 TyreHalfM, Vector2 FirstSteeredM,
        Vector2 SecondSteeredM, float TyreReachM, float WheelWidthM, float LampReachM, uint LensCount, float TowGripM,
        uint HasBeam, Vector2 PivotM, Vector2 CollapsedHalfM, Vector2 ExtendedHalfM, float CollapsedHingeAtM,
        float ExtendedHingeAtM, float CollapsedReachM, float ExtendedReachM, uint CollapsedSheet, uint ExtendedSheet);

    /// <summary>One lens of a look (<c>LensRow</c>): the fitting in the low byte, the colour its first cell burns in the next.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct LensRow(Vector2 AtBodyM, Vector2 FirstCellUv, Vector2 SecondCellUv, float GlowM, uint FittingAndFirstColour);

    /// <summary>One signal head (<c>HeadRow</c>), in the order its rows file it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct HeadRow(Vector2 CentreM, float HeadingRad, int Subject, uint ForCars, uint Pad = 0);

    /// <summary>The frame's own words (<c>Frame</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct FrameRow(
        Vector2 ViewCentreM, Vector2 ViewSpanM, float LeastPartM, float ElapsedS, uint PersonCount, uint CarCount, uint HandCount,
        int AlsoDrivenCar);

    /// <summary>What is fixed when the town is laid (<c>Figures</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Figures
    {
        public uint PersonCapacity;
        public uint CarCapacity;
        public uint HeadCount;
        public uint Levelled;
        public uint RoomOver;
        public uint RoomAbove;
        public uint MostLenses;
        public uint PersonSheets;
        public uint FirstDownSheet;
        public uint CarSheets;
        public uint FirstCarSheet;
        public uint RubberSheet;
        public uint LensSheet;
        public uint GlowSheet;
        public uint FirstHeadSheet;
        public uint GroundLevel;
        public uint AboveLevel;
        public uint LaneCount;
        public uint CrossingCount;
        public float WidestTyreM;
        public float WidestGlowM;
        public float FacingFromRad;
        public float FacingStepRad;
        public float PersonCellU;
        public float PersonCellV;
        public float LampBrakeMps2;
        public float StepBackLeadS;
        public float JunctionAheadM;
        public float IndicateRad;
        public float FlashHz;
        public float FlashOnShare;
        public float BeaconHz;
        public float HalfLensCellM;
        public float LensCellU;
        public float LensCellV;
        public float CarHeadHalfX;
        public float CarHeadHalfY;
        public float WalkHeadHalfX;
        public float WalkHeadHalfY;
        public float HeadReachM;
        public float CarHeadRedU;
        public float CarHeadAmberU;
        public float CarHeadGreenU;
        public float CarHeadCellU;
        public float WalkHeadRedU;
        public float WalkHeadAmberU;
        public float WalkHeadGreenU;
        public float WalkHeadCellU;
        public float CycleS;
        public float AmberTailS;
    }

    /// <summary>The stamps an emit is timed between.</summary>
    internal enum Stage
    {
        Began,
        Laid,
        Counted,
        Scanned,
        Written,
    }

    /// <summary>Where the scan keeps its grand total (<c>TotalAt</c>).</summary>
    const uint TotalAt = 0;

    const int Group = 64;

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly List<GpuBuffer> _owned = [];
    readonly int _units;
    readonly int _slots;
    readonly SpriteRoom _room;

    readonly GpuBuffer _frame, _people, _cars, _hands, _starts, _totals, _instances, _draws;
    readonly GpuBuffer _frameIn, _peopleIn, _carsIn, _handsIn;
    readonly GpuBuffer _drawsOut;

    /// <summary>Both runs as an emit read back left them, laid by the first that is: an emit drawn where it is reads none.</summary>
    GpuBuffer? _instancesOut;

    readonly ScanKernels _scans;
    readonly PrefixScan _scan;
    readonly ComputeKernel _count, _write;
    readonly DescriptorSet _countSet, _writeSet;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;

    /// <summary>The car tick a car is drawn off where it still holds it, the kernel laying it, and each car's room for its line there.</summary>
    readonly DeviceCarTick? _ticked;

    readonly ComputeKernel? _lay;
    readonly DescriptorSet _laySet;
    readonly int _arcsPerCar;

    int _peopleLaid;
    int _carsLaid;
    int _handsLaid;
    int _alsoDrivenCar = -1;

    /// <param name="cars">The fleet whose builds the looks are read off, and whose room the car rows are laid for.</param>
    /// <param name="personCapacity">The walkers' roster's room.</param>
    /// <param name="signals">The lights' own tables (<see cref="SignalService.Table"/>), which the heads' colours are read off.</param>
    /// <param name="levelled">Whether the town has a level above its ground (<see cref="TownWorld.Levelled"/>).</param>
    /// <param name="ticked">The car tick a car it still holds is drawn off (<see cref="DeviceCarTick.Drawn"/>), or none: every car as the town holds it.</param>
    public DeviceSprites(
        Vk vk, SimConfig config, TownSprites looks, SpriteRoom room, CarFleet cars, int personCapacity, SignalHeads heads,
        SignalTable signals, bool levelled, DeviceCarTick? ticked = null)
    {
        if (!vk.HasFloat64) throw new NotSupportedException($"{vk.DeviceName} has no doubles in its kernels, and a walker's cell is rounded in them.");

        _vk = vk;
        _config = config;
        _room = room;
        _ticked = ticked;
        _arcsPerCar = cars.Capacity > 0 ? cars.LineArcsOf(0).Length : 0;
        _units = personCapacity + cars.Capacity + heads.Count;
        _slots = personCapacity + (CarParts(levelled) * cars.Capacity) + heads.Count;
        if (_slots > ScanKernels.MostCounts) throw new ArgumentOutOfRangeException(nameof(cars), _slots, "more sprite slots than one scan covers");

        var figures = FiguresOf(config, looks, room, cars.Capacity, personCapacity, heads.Count, signals, levelled);

        GpuBuffer[] set =
        [
            _frame = Device<FrameRow>(1),
            Laid<Figures>([figures]),
            Laid<PersonLook>(PersonLooks(looks)),
            Laid<CarLook>(CarLooks(looks, cars)),
            Laid<LensRow>(Lenses(looks, config)),
            Laid<Vector4>(Tints(config)),
            _people = Device<PersonRow>(personCapacity),
            _cars = Device<CarRow>(cars.Capacity),
            _hands = Device<uint>(cars.Capacity),
            Laid<HeadRow>(Heads(heads)),
            Laid<int>(signals.LaneAxis),
            Laid<int>(signals.LaneJunction),
            Laid<float>(signals.OffsetS),
            Laid<int>(signals.CrossingAxis),
            Laid<int>(signals.CrossingJunction),
            _starts = Device<uint>(_slots),
            _totals = Device<uint>(4),
            Device<UInt2>(cars.Capacity),
            // A slot above at the least: a frame binds the run above at its own offset, which may not be the buffer's end.
            _instances = Device<SpriteInstance>(room.Over + Math.Max(1, room.Above), BufferUsageFlags.VertexBufferBit),
            _draws = Device<DrawIndirectCommand>(2, BufferUsageFlags.IndirectBufferBit),
        ];

        _frameIn = Staging<FrameRow>(1);
        _peopleIn = Staging<PersonRow>(personCapacity);
        _carsIn = Staging<CarRow>(cars.Capacity);
        _handsIn = Staging<uint>(cars.Capacity);
        _drawsOut = Readback<DrawIndirectCommand>(2);

        _scans = new ScanKernels(vk);
        _scan = new PrefixScan(vk, _scans, _starts, Device<uint>(ScanKernels.Block), _totals, TotalAt);
        _count = new ComputeKernel(vk, "sprites_count.comp", set.Length, pushBytes: 0);
        _write = new ComputeKernel(vk, "sprites_write.comp", set.Length, pushBytes: 0);
        _countSet = _count.Bind(set);
        _writeSet = _write.Bind(set);
        if (ticked?.Drawn is { } drawn)
        {
            _lay = new ComputeKernel(vk, "sprites_cars.comp", 7, (uint)Marshal.SizeOf<LayPush>());
            _laySet = _lay.Bind([_cars, drawn.Ticks, drawn.Commands, drawn.Briefs, drawn.Passes, drawn.Lines, drawn.Arcs]);
        }

        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
    }

    /// <summary>
    /// The emit for a running town: its fleets' room, its heads and lights, and whether it has a level above — and the
    /// car tick a car it still holds is drawn off, where the device ticks the town.
    /// </summary>
    public static DeviceSprites For(Vk vk, SimConfig config, TownWorld world, TownSprites looks, SpriteRoom room, DeviceCarTick? ticked = null) =>
        new(vk, config, looks, room, world.Cars, world.People.Capacity, world.Heads, world.Signals.Table, world.Levelled, ticked);

    /// <summary>
    /// The two runs as the last emit wrote them, on the device — the over run from the first instance and the run above
    /// from <see cref="AboveFrom"/> — for a frame to draw.
    /// </summary>
    public GpuBuffer Instances => _instances;

    /// <summary>The two draws the last emit wrote, the over run's and the run above's, for a frame's indirect draws.</summary>
    public GpuBuffer Draws => _draws;

    /// <summary>Where in <see cref="Instances"/> the run above starts: past the over run's room.</summary>
    public int AboveFrom => _room.Over;

    /// <summary>How many instances the last emit wrote into the over run, and into the run above.</summary>
    public int OverCount => (int)_drawsOut.Span<DrawIndirectCommand>()[0].InstanceCount;

    public int AboveCount => (int)_drawsOut.Span<DrawIndirectCommand>()[1].InstanceCount;

    /// <summary>The over run as the last emit that read it back wrote it.</summary>
    public ReadOnlySpan<SpriteInstance> Over => ReadBack.Span<SpriteInstance>()[..OverCount];

    /// <summary>And the run above, laid past the over run's room.</summary>
    public ReadOnlySpan<SpriteInstance> Above => ReadBack.Span<SpriteInstance>().Slice(_room.Over, AboveCount);

    GpuBuffer ReadBack => _instancesOut ?? throw new InvalidOperationException("No emit has read its runs back.");

    /// <summary>The device's time between two stages of the last emit, in milliseconds.</summary>
    public double BetweenMs(Stage from, Stage to) => _timer.BetweenMs((int)from, (int)to);

    /// <summary>A running town's walkers and cars laid as its frame will read them.</summary>
    public void Lay(TownWorld world)
    {
        LayPeople(world.People);
        LayCars(world.Cars, world.Recovery, world.HandDriven, world.HandDrivenCar);
    }

    /// <summary>Every walker of the roster as the emit reads it.</summary>
    public void LayPeople(PersonFleet people)
    {
        // The last frame's emit may still be copying out of the rows laid for it.
        _batch.Wait();
        var rows = _peopleIn.Span<PersonRow>();
        for (var person = 0; person < people.Count; person++)
        {
            var flags = (people.Inside[person].Any ? PersonRow.Inside : 0u) | (people.Wounded[person] ? PersonRow.Down : 0u)
                        | (people.Walking[person] ? PersonRow.Walking : 0u);
            rows[person] = new PersonRow(
                people.PositionM[person], people.HeadingRad[person], people.DistanceWalkedM[person], people.Variant[person], flags);
        }

        _peopleLaid = people.Count;
    }

    /// <summary>
    /// Every car of the fleet as the emit reads it, and the hands at the wheel — but where the car tick bound here still
    /// holds a car (<see cref="DeviceCarTick.Takes"/>), what its tick wrote, which the emit lays over the town's copy.
    /// </summary>
    public void LayCars(CarFleet cars, RecoveryDuty recovery, ReadOnlySpan<Selection> handDriven, int alsoDrivenCar)
    {
        _batch.Wait();
        var rows = _carsIn.Span<CarRow>();
        var aheadM = _config.Lamps.TurnAheadM;
        for (var car = 0; car < cars.Count; car++)
        {
            var fromTheDevice = _ticked?.Takes(car) ?? false;
            var pass = cars.Pass[car];
            var command = cars.Command[car];
            var flags = (cars.Broken[car] ? CarRow.Broken : 0u) | (cars.Driven[car] ? CarRow.Driven : 0u)
                        | (command.Reverse ? CarRow.Reverse : 0u) | (cars.BlueLight[car] ? CarRow.BlueLight : 0u)
                        | (cars.AtWork[car] ? CarRow.AtWork : 0u) | (cars.TurningAtTheBox[car] ? CarRow.TurningAtTheBox : 0u)
                        | (cars.LineIsReverse[car] ? CarRow.LineIsReverse : 0u) | (pass.Begun ? CarRow.PassBegun : 0u)
                        | (recovery.HeldByTheTail[car] ? CarRow.HeldByTheTail : 0u) | (fromTheDevice ? CarRow.FromTheDevice : 0u)
                        | ((uint)cars.LevelOf(car) << CarRow.LevelShift);
            var turnAheadRad = fromTheDevice ? 0f : CarLamps.TurnAheadRad(cars.LineOf(car), cars.ProgressM[car], aheadM);
            rows[car] = new CarRow(
                cars.PositionM[car], Heading.Unit(cars.HeadingRad[car]), cars.HeadingRad[car], command.SteerRad, command.BrakeMps2,
                cars.ProgressM[car], cars.AlongMps[car], cars.Context[car].PassAsideM, cars.ToTheBoxM[car], cars.FuseJitter[car],
                pass.EndsM, pass.SteppedOutM, pass.BackM, pass.AsideM, cars.Variant[car], flags, recovery.Towing[car], turnAheadRad);
        }

        var hands = _handsIn.Span<uint>();
        var held = 0;
        foreach (var unit in handDriven)
        {
            if (unit.Kind == SelectionKind.Car && held < hands.Length) hands[held++] = (uint)unit.Index;
        }

        _carsLaid = cars.Count;
        _handsLaid = held;
        _alsoDrivenCar = alsoDrivenCar;
    }

    /// <summary>
    /// <b>The two runs emitted for one view</b> — the one the fill is handed (<see cref="TownSprites.FillMoving"/>) — and
    /// read back with their counts.
    /// </summary>
    /// <param name="readBack">
    /// Whether the runs are copied back to be read: the whole of both rooms, which on a city is tens of megabytes and
    /// many times the emit itself — so an emit only timed is not.
    /// </param>
    public void Emit(Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre, float elapsedS, bool readBack = true)
    {
        var commands = Record(viewCentreM, viewSpanM, pixelsPerMetre, elapsedS);
        _vk.BeforeTransfer(commands);
        if (readBack)
        {
            _instancesOut ??= Own(_vk.CreateBuffer(_instances.SizeBytes, BufferUsageFlags.TransferDstBit, HostAccess.Reads));
            _vk.Copy(commands, _instances, _instancesOut, _instances.SizeBytes);
        }

        _vk.Copy(commands, _draws, _drawsOut, _draws.SizeBytes);
        _batch.SubmitAndWait();
        _timer.Read();
    }

    /// <summary>
    /// <b>The two runs emitted for the frame about to be drawn</b>, where they stay (<see cref="Instances"/>,
    /// <see cref="Draws"/>): submitted and not waited for, behind whatever the queue holds and ahead of whatever follows —
    /// the frame that draws them, and the next tick writing what this emit read.
    /// </summary>
    public void EmitForTheFrame(Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre, float elapsedS)
    {
        var commands = Record(viewCentreM, viewSpanM, pixelsPerMetre, elapsedS);
        _vk.Barrier(
            commands, PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageWriteBit, PipelineStageFlags2.AllCommandsBit,
            AccessFlags2.MemoryReadBit | AccessFlags2.MemoryWriteBit);
        _batch.Submit();
    }

    /// <summary>
    /// The emit recorded: the frame's words and the rows laid sent, the cars the bound tick holds laid off its tables,
    /// then count, scan and write — begun behind everything submitted before it, whose last frame may still be drawing the
    /// runs this one writes.
    /// </summary>
    CommandBuffer Record(Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre, float elapsedS)
    {
        _batch.Wait();
        _frameIn.Span<FrameRow>()[0] = new FrameRow(
            viewCentreM, viewSpanM, _config.View.CarPartLeastPx / pixelsPerMetre, elapsedS, (uint)_peopleLaid, (uint)_carsLaid,
            (uint)_handsLaid, _alsoDrivenCar);

        var vk = _vk;
        var commands = _batch.Begin();
        vk.AfterTheSubmitsBefore(commands);
        _timer.Reset(commands);
        vk.Copy(commands, _frameIn, _frame, Bytes<FrameRow>(1));
        vk.Copy(commands, _peopleIn, _people, Bytes<PersonRow>(_peopleLaid));
        vk.Copy(commands, _carsIn, _cars, Bytes<CarRow>(_carsLaid));
        vk.Copy(commands, _handsIn, _hands, Bytes<uint>(_handsLaid));
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        if (_lay is { } lay)
        {
            var push = new LayPush((uint)_carsLaid, (uint)_arcsPerCar, _config.Lamps.TurnAheadM);
            lay.Dispatch(commands, _laySet, (uint)Math.Max(1, (_carsLaid + Group - 1) / Group), push);
            vk.BetweenKernels(commands);
        }

        _timer.Stamp(commands, (int)Stage.Laid);
        var groups = (uint)Math.Max(1, (_units + Group - 1) / Group);
        _count.Dispatch(commands, _countSet, groups, default(NoPush));
        vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Counted);
        _scan.Record(commands, _slots);
        _timer.Stamp(commands, (int)Stage.Scanned);
        _write.Dispatch(commands, _writeSet, groups, default(NoPush));
        _timer.Stamp(commands, (int)Stage.Written);
        return commands;
    }

    /// <summary>A car's four parts, and four again where there is a level above to draw them in.</summary>
    static int CarParts(bool levelled) => levelled ? 8 : 4;

    static Figures FiguresOf(
        SimConfig config, TownSprites looks, SpriteRoom room, int carCapacity, int personCapacity, int headCount,
        SignalTable signals, bool levelled)
    {
        var lamps = config.Lamps;
        var lensCell = LampAtlas.CellSize(looks.Cars.SheetCount);
        var carHeadHalfM = new Vector2(config.Signals.CarHeadLengthM, config.Signals.CarHeadWidthM) * 0.5f;
        var walkHeadHalfM = new Vector2(config.Signals.WalkHeadWidthM, config.Signals.WalkHeadLengthM) * 0.5f;
        return new Figures
        {
            PersonCapacity = (uint)personCapacity,
            CarCapacity = (uint)carCapacity,
            HeadCount = (uint)headCount,
            Levelled = levelled ? 1u : 0u,
            RoomOver = (uint)room.Over,
            RoomAbove = (uint)room.Above,
            MostLenses = CarLamps.MostLenses,
            PersonSheets = (uint)looks.People.SheetCount,
            FirstDownSheet = (uint)looks.FirstDownSheet,
            CarSheets = (uint)looks.Cars.SheetCount,
            FirstCarSheet = (uint)looks.FirstCarSheet,
            RubberSheet = (uint)looks.RubberSheet,
            LensSheet = (uint)looks.LensSheet,
            GlowSheet = (uint)looks.LampGlowSheet,
            FirstHeadSheet = (uint)looks.FirstHeadSheet,
            GroundLevel = CityPlan.RoadArrays.Ground,
            AboveLevel = CityPlan.RoadArrays.Over,
            LaneCount = (uint)signals.LaneAxis.Length,
            CrossingCount = (uint)signals.CrossingAxis.Length,
            WidestTyreM = looks.WidestTyreM,
            WidestGlowM = looks.WidestGlowM,
            FacingFromRad = -MathF.PI * 0.5f,
            FacingStepRad = MathF.PI * 0.25f,
            PersonCellU = 1f / PersonCatalog.WalkColumns,
            PersonCellV = 1f / PersonCatalog.FacingRows,
            LampBrakeMps2 = lamps.BrakeMps2,
            StepBackLeadS = lamps.StepBackLeadS,
            JunctionAheadM = lamps.JunctionAheadM,
            IndicateRad = lamps.TurnDeg * MathF.PI / 180f,
            FlashHz = lamps.FlashHz,
            FlashOnShare = lamps.FlashOnShare,
            BeaconHz = lamps.BeaconHz,
            HalfLensCellM = LampAtlas.CellM * 0.5f,
            LensCellU = lensCell.X,
            LensCellV = lensCell.Y,
            CarHeadHalfX = carHeadHalfM.X,
            CarHeadHalfY = carHeadHalfM.Y,
            WalkHeadHalfX = walkHeadHalfM.X,
            WalkHeadHalfY = walkHeadHalfM.Y,
            HeadReachM = MathF.Max(carHeadHalfM.Length(), walkHeadHalfM.Length()),
            CarHeadRedU = SignalSprites.CellOf(true, SignalColour.Red).UvMin.X,
            CarHeadAmberU = SignalSprites.CellOf(true, SignalColour.Amber).UvMin.X,
            CarHeadGreenU = SignalSprites.CellOf(true, SignalColour.Green).UvMin.X,
            CarHeadCellU = SignalSprites.CellOf(true, SignalColour.Red).UvSize.X,
            WalkHeadRedU = SignalSprites.CellOf(false, SignalColour.Red).UvMin.X,
            WalkHeadAmberU = SignalSprites.CellOf(false, SignalColour.Amber).UvMin.X,
            WalkHeadGreenU = SignalSprites.CellOf(false, SignalColour.Green).UvMin.X,
            WalkHeadCellU = SignalSprites.CellOf(false, SignalColour.Red).UvSize.X,
            CycleS = config.Signals.CycleS,
            AmberTailS = config.Signals.AmberTailS,
        };
    }

    /// <summary>Each walker's look as <see cref="PersonSprites.Fill"/> sizes it, standing and down.</summary>
    static PersonLook[] PersonLooks(TownSprites looks)
    {
        var rows = new PersonLook[looks.People.SheetCount];
        for (var variant = 0; variant < rows.Length; variant++)
        {
            var heightM = looks.People.Variants[variant].HeightM;
            var standingHalfM = new Vector2(heightM * looks.Aspects[variant], heightM) * 0.5f;
            var downHalfM = new Vector2(heightM, heightM / looks.Aspects[looks.FirstDownSheet + variant]) * 0.5f;
            rows[variant] = new PersonLook(standingHalfM, downHalfM, downHalfM.Length(), heightM);
        }

        return rows;
    }

    /// <summary>Each car's look as <see cref="CarSprites"/> and <see cref="LampSprites"/> size and cull its parts.</summary>
    static CarLook[] CarLooks(TownSprites looks, CarFleet cars)
    {
        var catalogue = looks.Cars;
        var rows = new CarLook[catalogue.SheetCount];
        for (var variant = 0; variant < rows.Length; variant++)
        {
            ref readonly var build = ref cars.BuildOfVariant(variant);
            var artHalfM = new Vector2(build.LengthM, build.WidthM) * 0.5f;
            var bodyHalfM = artHalfM * looks.CarSheetScales[variant];
            var wreckHalfM = artHalfM * catalogue.Variants[variant].WreckScale * looks.CarSheetScales[catalogue.SheetCount + variant];
            var halfDiagonalM = new Vector2(build.LengthM, build.WidthM).Length() * 0.5f;

            var beam = catalogue.BeamOf(variant);
            var collapsed = beam?.Drawn(false) ?? default;
            var extended = beam?.Drawn(true) ?? default;
            rows[variant] = new CarLook(
                bodyHalfM, wreckHalfM, bodyHalfM.Length(), wreckHalfM.Length(),
                new Vector2(build.WheelLengthM, build.WheelWidthM) * 0.5f, TyreModel.WheelAtM(build, 0), TyreModel.WheelAtM(build, 1),
                halfDiagonalM + build.WheelLengthM, build.WheelWidthM, halfDiagonalM, (uint)catalogue.LensesOf(variant).Length,
                build.TowGripFromTheMiddleM, beam is null ? 0u : 1u, beam?.PivotM ?? Vector2.Zero, collapsed.SizeM * 0.5f,
                extended.SizeM * 0.5f, collapsed.HingeAtM, extended.HingeAtM, ArmReachM(collapsed), ArmReachM(extended),
                BeamSheet(looks, variant, towing: false), BeamSheet(looks, variant, towing: true));
        }

        return rows;
    }

    static float ArmReachM(CarTowArm arm) => (arm.SizeM * 0.5f).Length() + MathF.Abs(arm.HingeAtM);

    static uint BeamSheet(TownSprites looks, int variant, bool towing)
    {
        var slot = looks.Cars.BeamSlotOf(variant, towing);
        return slot == CarCatalog.NoBeam ? 0u : (uint)(looks.FirstBeamSheet + slot);
    }

    /// <summary>Every lens of every look, <see cref="CarLamps.MostLenses"/> rows a look: where it is, its two cells and its glow.</summary>
    static LensRow[] Lenses(TownSprites looks, SimConfig config)
    {
        var catalogue = looks.Cars;
        var rows = new LensRow[catalogue.SheetCount * CarLamps.MostLenses];
        for (var variant = 0; variant < catalogue.SheetCount; variant++)
        {
            var lenses = catalogue.LensesOf(variant);
            for (var lens = 0; lens < lenses.Length; lens++)
            {
                var fitting = lenses[lens].Fitting;
                rows[(variant * CarLamps.MostLenses) + lens] = new LensRow(
                    lenses[lens].AtBodyM, LampAtlas.CellAt(variant, lens, 0, catalogue.SheetCount),
                    LampAtlas.CellAt(variant, lens, 1, catalogue.SheetCount), LampSprites.GlowM(lenses[lens], config),
                    (uint)fitting | ((uint)LampAtlas.ColourAt(fitting, 0) << 8));
            }
        }

        return rows;
    }

    /// <summary>What each lamp's glow is tinted, by <see cref="CarLamp"/>.</summary>
    static Vector4[] Tints(SimConfig config)
    {
        var lamps = Enum.GetValues<CarLamp>();
        var tints = new Vector4[lamps.Length];
        foreach (var lamp in lamps) tints[(int)lamp] = LampAtlas.ColourOf(lamp) with { W = config.Lamps.GlowStrength };
        return tints;
    }

    /// <summary>The heads in the order their rows file them, which is the order <see cref="SignalSprites.Fill"/> meets them.</summary>
    static HeadRow[] Heads(SignalHeads heads)
    {
        var filed = heads.ByRow.Filed;
        var rows = new HeadRow[filed.Length];
        for (var at = 0; at < filed.Length; at++)
        {
            ref readonly var head = ref heads.Heads[filed[at]];
            rows[at] = new HeadRow(head.CentreM, head.HeadingRad, head.Subject, head.ForCars ? 1u : 0u);
        }

        return rows;
    }

    /// <summary>A device-local buffer holding these rows, sent once through a staging buffer.</summary>
    GpuBuffer Laid<T>(ReadOnlySpan<T> rows) where T : unmanaged
    {
        var buffer = Own(_vk.CreateBuffer(Bytes<T>(rows.Length), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));
        var copy = rows.ToArray();
        buffer.Upload<T>(copy.Length, into => copy.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    GpuBuffer Device<T>(int count, BufferUsageFlags also = 0) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit | also,
            HostAccess.None));

    GpuBuffer Staging<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferSrcBit, HostAccess.Writes));

    GpuBuffer Readback<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferDstBit, HostAccess.Reads));

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        // A frame drawing the runs may still be in flight, and its recording names these buffers.
        _vk.Api.DeviceWaitIdle(_vk.Device);
        _timer.Dispose();
        _batch.Dispose();
        _count.Dispose();
        _write.Dispose();
        _lay?.Dispose();
        _scans.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
