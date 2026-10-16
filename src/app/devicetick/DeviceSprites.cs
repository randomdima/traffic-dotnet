using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The frame's moving sprites emitted on the device off the world's own state</b> (<c>app/render/kernels/sprites_world.comp</c>,
/// <c>sprites_count.comp</c>, <c>sprites_write.comp</c>): the walkers, every part of every car, the signal heads and the
/// cars on the level above, written into the over and above runs in <see cref="TownSprites.FillMoving"/>'s order and culled
/// against the view as it culls — drawn where they are written, recorded into the frame that draws them
/// (<see cref="Record"/>, <see cref="TownRenderer.DrawTheMovingFrom"/>), or read back (<see cref="Emit"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing of an agent is sent to be drawn</b> (root <c>decision-log.md</c>, 2026-10-11): every row is laid on the
/// device off the world's bodies, the commands and what each car last signalled it shows (<see cref="DeviceWorld.Drawn"/>).
/// What a look is, where its lenses are, the heads and the lights' tables are laid once; the view and the clock are a frame
/// block in a buffer rather than push constants, because the frame that carries this pass is recorded once.
/// </para>
/// <para>
/// <b>Recorded once a swapchain image, in that image's own frame</b>: what a frame changes — the view, the clock, and
/// whether the rows are laid again — is that image's frame words (<see cref="WriteTheFrame"/>), so the emit costs a frame no
/// crossing and no submit of its own. <b>The rows are laid only when the world has moved</b> (a batch of signals taken,
/// <see cref="WorldLayer.SignalsTaken"/>): the lay's three dispatches read their workgroups out of the frame words, nought
/// on a frame no step or signal came between, and the rows laid for an earlier frame stand.
/// </para>
/// <para>
/// <b>Count, scan, write.</b> One thread a unit counts what each part of it draws into a slot laid in draw order — a cull
/// and nothing built — a <see cref="PrefixScan"/> turns the counts into where each part starts, and a second pass builds
/// every part counted there and the two draws' instance counts — so the order is the host's whatever order the threads ran in.
/// </para>
/// <para>
/// <b>The device must offer doubles</b>: a walker's facing, its stride's cell and a tow arm's bearing are rounded in them,
/// as the host's fill rounds them, and that is the whole of what they are for — the instances the host fills and these are
/// one to the bit (<c>--bench deviceworld</c>), which a device without them could not hold.
/// </para>
/// </remarks>
internal sealed class DeviceSprites : IMovingEmit, IDisposable
{
    /// <summary>
    /// A frame's words (<see cref="WriteTheFrame"/>): its <see cref="FrameRow"/>, copied to where the passes read it, and
    /// the lay's three dispatches' workgroups — cars, walkers, and the cars again for the tows — each nought where the
    /// world has not moved since the rows were last laid.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct FrameWords(FrameRow Frame, DispatchIndirectCommand LayCars, DispatchIndirectCommand LayPeople, DispatchIndirectCommand LayTows);

    static readonly ulong LayCarsAt = (ulong)Marshal.SizeOf<FrameRow>();
    static readonly ulong LayPeopleAt = LayCarsAt + (ulong)Marshal.SizeOf<DispatchIndirectCommand>();
    static readonly ulong LayTowsAt = LayPeopleAt + (ulong)Marshal.SizeOf<DispatchIndirectCommand>();

    /// <summary>One walker as the emit reads it (<c>PersonRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PersonRow(Vector2 PositionM, float HeadingRad, float DistanceWalkedM, uint Variant, uint Flags);

    /// <summary>One car as the emit reads it (<c>CarRow</c>, <c>sprites_car.glsl</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CarRow(
        Vector2 PositionM, Vector2 Forward, float HeadingRad, float SteerRad, float FuseJitter, uint Lamps, uint Variant, uint Flags,
        int Towing, float Pad);

    /// <summary>What a car looks like that is fixed when the town is laid (<c>CarLookRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CarLookRow(uint Variant, float FuseJitter);

    /// <summary>The push <c>sprites_world.comp</c> is run with.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct LayPush(uint Count, uint Mode);

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

    /// <summary>
    /// One lens of a look (<c>LensRow</c>): its two cells (<see cref="SpriteCell.Packed"/>), the fitting in the low byte,
    /// the colour its first cell burns in the next.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct LensRow(Vector2 AtBodyM, uint FirstCell, uint SecondCell, float GlowM, uint FittingAndFirstColour);

    /// <summary>One signal head (<c>HeadRow</c>), in the order its rows file it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct HeadRow(Vector2 CentreM, float HeadingRad, int Subject, uint ForCars, uint Pad = 0);

    /// <summary>The frame's own words (<c>Frame</c>): the view, the narrowest part, the clock, the rosters and the world's view the host is showing.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct FrameRow(
        Vector2 ViewCentreM, Vector2 ViewSpanM, float LeastPartM, float ElapsedS, uint PersonCount, uint CarCount, uint ViewSlot,
        uint Pad = 0);

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
        public uint PersonColumns;
        public uint PersonRows;
        public float FlashHz;
        public float FlashOnShare;
        public float BeaconHz;
        public float HalfLensCellM;
        public float CarHeadHalfX;
        public float CarHeadHalfY;
        public float WalkHeadHalfX;
        public float WalkHeadHalfY;
        public float HeadReachM;
        public uint CarHeadRed;
        public uint CarHeadAmber;
        public uint CarHeadGreen;
        public uint CarHeadFrames;
        public uint WalkHeadRed;
        public uint WalkHeadAmber;
        public uint WalkHeadGreen;
        public uint WalkHeadFrames;
        public float CycleS;
        public float AmberTailS;
    }

    /// <summary>Where the scan keeps its grand total (<c>TotalAt</c>).</summary>
    const uint TotalAt = 0;

    /// <summary>The binding of the counts the scan turns into starts (<c>Starts</c>), its totals the next.</summary>
    const int StartsAt = 14;

    const int Group = 64;

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly List<GpuBuffer> _owned = [];
    readonly int _units;
    readonly int _slots;
    readonly int _cars;
    readonly int _people;
    readonly SpriteRoom _room;

    readonly GpuBuffer _frame, _instances, _draws, _frameIn, _drawsOut;

    /// <summary>The world whose view the host is showing, which the rows are laid off (<see cref="WorldLayer.View"/>).</summary>
    readonly DeviceWorld _world;

    /// <summary>Each swapchain image's frame words (<see cref="FrameWords"/>), laid as its frame is first recorded.</summary>
    readonly List<GpuBuffer> _frames = [];

    /// <summary>The batch of signals the rows were last laid for (<see cref="WorldLayer.SignalsTaken"/>), or −1 before any.</summary>
    long _laidFor = -1;

    /// <summary>Both runs as an emit read back left them, laid by the first that is: an emit drawn where it is reads none.</summary>
    GpuBuffer? _instancesOut;

    readonly ScanKernels _scans;
    readonly PrefixScan _scan;
    readonly ComputeKernel _lay, _count, _write;
    readonly DescriptorSet _laySet, _countSet, _writeSet;
    readonly ComputeBatch _batch;

    /// <param name="levelled">Whether the town has a level above its ground (<see cref="TownWorld.Levelled"/>).</param>
    public DeviceSprites(
        Vk vk, SimConfig config, TownSprites looks, SpriteRoom room, CarFleet cars, PersonFleet people, SignalHeads heads,
        SignalTable signals, bool levelled, DeviceWorld world)
    {
        _vk = vk;
        _config = config;
        _room = room;
        _cars = cars.Count;
        _people = people.Count;
        _units = people.Capacity + cars.Capacity + heads.Count;
        _slots = people.Capacity + (CarParts(levelled) * cars.Capacity) + heads.Count;
        if (_slots > ScanKernels.MostCounts) throw new ArgumentOutOfRangeException(nameof(cars), _slots, "more sprite slots than one scan covers");

        var figures = FiguresOf(config, looks, room, cars.Capacity, people.Capacity, heads.Count, signals, levelled);
        var personRows = Device<PersonRow>(Math.Max(1, people.Capacity));
        var carRows = Device<CarRow>(Math.Max(1, cars.Capacity));
        GpuBuffer[] set =
        [
            _frame = Device<FrameRow>(1),
            Laid<Figures>([figures]),
            Laid<PersonLook>(PersonLooks(looks)),
            Laid<CarLook>(CarLooks(looks, cars)),
            Laid<LensRow>(Lenses(looks, config)),
            Laid<uint>(Tints(config)),
            personRows,
            carRows,
            Laid<HeadRow>(Heads(heads)),
            Laid<int>(signals.LaneAxis),
            Laid<int>(signals.LaneJunction),
            Laid<float>(signals.OffsetS),
            Laid<int>(signals.CrossingAxis),
            Laid<int>(signals.CrossingJunction),
            Device<uint>(_slots),
            Device<uint>(4),
            Device<UInt2>(Math.Max(1, cars.Capacity)),
            // A slot above at the least: a frame binds the run above at its own offset, which may not be the buffer's end.
            _instances = Device<SpriteInstance>(room.Over + Math.Max(1, room.Above), BufferUsageFlags.VertexBufferBit),
            _draws = Device<DrawIndirectCommand>(2, BufferUsageFlags.IndirectBufferBit),
        ];

        _frameIn = Own(_vk.CreateBuffer(
            Bytes<FrameWords>(1), BufferUsageFlags.TransferSrcBit | BufferUsageFlags.IndirectBufferBit, HostAccess.Writes));
        _drawsOut = Own(_vk.CreateBuffer(Bytes<DrawIndirectCommand>(2), BufferUsageFlags.TransferDstBit, HostAccess.Reads));

        _scans = new ScanKernels(vk);
        _scan = new PrefixScan(vk, _scans, set[StartsAt], Device<uint>(ScanKernels.Block), set[StartsAt + 1], TotalAt);
        _count = new ComputeKernel(vk, "sprites_count.comp", set.Length, pushBytes: 0);
        _write = new ComputeKernel(vk, "sprites_write.comp", set.Length, pushBytes: 0);
        _countSet = _count.Bind(set);
        _writeSet = _write.Bind(set);

        var carLooks = new CarLookRow[Math.Max(1, cars.Count)];
        for (var car = 0; car < cars.Count; car++) carLooks[car] = new CarLookRow(cars.Variant[car], cars.FuseJitter[car]);
        var personVariants = new uint[Math.Max(1, people.Count)];
        for (var person = 0; person < people.Count; person++) personVariants[person] = people.Variant[person];

        var drawn = world.Drawn;
        _world = world;
        _lay = new ComputeKernel(vk, "sprites_world.comp", 12, (uint)Marshal.SizeOf<LayPush>());
        _laySet = _lay.Bind(
        [
            carRows, personRows, drawn.Cars, drawn.Walkers, drawn.Commands, Laid<CarLookRow>(carLooks), Laid<uint>(personVariants),
            _frame, drawn.CarViews[0], drawn.CarViews[1], drawn.WalkerViews[0], drawn.WalkerViews[1],
        ]);
        _batch = new ComputeBatch(vk);
    }

    /// <summary>The emit for a running town whose world is on the device.</summary>
    public static DeviceSprites For(Vk vk, SimConfig config, TownWorld town, TownSprites looks, SpriteRoom room, DeviceWorld world) =>
        new(vk, config, looks, room, town.Cars, town.People, town.Heads, town.Signals.Table, town.Levelled, world);

    /// <summary>
    /// The two runs as the last emit wrote them, on the device — the over run from the first instance and the run above
    /// from <see cref="AboveFrom"/> — for a frame to draw.
    /// </summary>
    public GpuBuffer Instances => _instances;

    /// <summary>The two draws the last emit wrote, the over run's and the run above's, for a frame's indirect draws.</summary>
    public GpuBuffer Draws => _draws;

    /// <summary>Where in <see cref="Instances"/> the run above starts: past the over run's room.</summary>
    public int AboveFrom => _room.Over;

    /// <summary>What a frame's emit writes into mapped memory: its frame words.</summary>
    public long FrameBytes => (long)Bytes<FrameWords>(1);

    /// <summary>How many instances the last emit wrote into the over run, and into the run above.</summary>
    public int OverCount => (int)_drawsOut.Span<DrawIndirectCommand>()[0].InstanceCount;

    public int AboveCount => (int)_drawsOut.Span<DrawIndirectCommand>()[1].InstanceCount;

    /// <summary>The over run as the last emit that read it back wrote it.</summary>
    public ReadOnlySpan<SpriteInstance> Over => ReadBack.Span<SpriteInstance>()[..OverCount];

    /// <summary>And the run above, laid past the over run's room.</summary>
    public ReadOnlySpan<SpriteInstance> Above => ReadBack.Span<SpriteInstance>().Slice(_room.Over, AboveCount);

    GpuBuffer ReadBack => _instancesOut ?? throw new InvalidOperationException("No emit has read its runs back.");

    /// <summary>
    /// <b>The two runs emitted for one view</b> — the one the fill is handed (<see cref="TownSprites.FillMoving"/>) — and
    /// read back with their counts.
    /// </summary>
    public void Emit(Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre, float elapsedS)
    {
        _batch.Wait();
        _frameIn.Span<FrameWords>()[0] = WordsFor(viewCentreM, viewSpanM, pixelsPerMetre, elapsedS, lay: true);
        var commands = _batch.Begin();
        RecordTheEmit(commands, _frameIn);
        _vk.BeforeTransfer(commands);
        _instancesOut ??= Own(_vk.CreateBuffer(_instances.SizeBytes, BufferUsageFlags.TransferDstBit, HostAccess.Reads));
        _vk.Copy(commands, _instances, _instancesOut, _instances.SizeBytes);
        _vk.Copy(commands, _draws, _drawsOut, _draws.SizeBytes);
        _batch.SubmitAndWait();
    }

    /// <summary>
    /// <b>The emit for one swapchain image's frame</b>, recorded into its command buffer ahead of the pass that draws the two
    /// runs (<see cref="TownRenderer.DrawTheMovingFrom"/>): begun behind everything submitted before it — the world's last
    /// step, whose rows it reads, and the frame before, which may still be drawing the runs this one writes — and its writes
    /// made visible to the draws.
    /// </summary>
    public void Record(CommandBuffer commands, int image)
    {
        while (_frames.Count <= image)
        {
            var words = Own(_vk.CreateBuffer(
                Bytes<FrameWords>(1), BufferUsageFlags.TransferSrcBit | BufferUsageFlags.IndirectBufferBit, HostAccess.Streams));
            words.Span<FrameWords>()[0] = default;
            _frames.Add(words);
        }

        RecordTheEmit(commands, _frames[image]);
        _vk.Barrier(
            commands, PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageWriteBit,
            PipelineStageFlags2.DrawIndirectBit | PipelineStageFlags2.VertexAttributeInputBit,
            AccessFlags2.IndirectCommandReadBit | AccessFlags2.VertexAttributeReadBit);
    }

    /// <summary>
    /// <b>What image <paramref name="image"/>'s frame emits</b>, written into its frame words before the frame is submitted:
    /// the view and the clock, and the rows laid again only where the world has taken a batch of signals since they were
    /// last laid (<paramref name="signalsTaken"/>).
    /// </summary>
    public void WriteTheFrame(int image, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre, float elapsedS, long signalsTaken)
    {
        var lay = signalsTaken != _laidFor;
        _laidFor = signalsTaken;
        _frames[image].Span<FrameWords>()[0] = WordsFor(viewCentreM, viewSpanM, pixelsPerMetre, elapsedS, lay);
    }

    FrameWords WordsFor(Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre, float elapsedS, bool lay) => new(
        new FrameRow(
            viewCentreM, viewSpanM, _config.View.CarPartLeastPx / pixelsPerMetre, elapsedS, (uint)_people, (uint)_cars,
            (uint)_world.View.Slot),
        new DispatchIndirectCommand(lay ? Groups(_cars) : 0u, 1, 1), new DispatchIndirectCommand(lay ? Groups(_people) : 0u, 1, 1),
        new DispatchIndirectCommand(lay ? Groups(_cars) : 0u, 1, 1));

    /// <summary>
    /// The emit recorded off frame words <paramref name="words"/>: the frame's row sent, every row laid off the world as
    /// many workgroups as the words say, then count, scan and write.
    /// </summary>
    void RecordTheEmit(CommandBuffer commands, GpuBuffer words)
    {
        var vk = _vk;
        vk.AfterTheSubmitsBefore(commands);
        vk.Copy(commands, words, _frame, Bytes<FrameRow>(1));
        vk.AfterTransfer(commands);

        _lay.DispatchIndirect(commands, _laySet, words, LayCarsAt, new LayPush((uint)_cars, 0u));
        _lay.DispatchIndirect(commands, _laySet, words, LayPeopleAt, new LayPush((uint)_people, 1u));
        vk.BetweenKernels(commands);
        _lay.DispatchIndirect(commands, _laySet, words, LayTowsAt, new LayPush((uint)_cars, 2u));
        vk.BetweenKernels(commands);

        var groups = Groups(_units);
        _count.Dispatch(commands, _countSet, groups, default(NoPush));
        vk.BetweenKernels(commands);
        _scan.Record(commands, _slots);
        _write.Dispatch(commands, _writeSet, groups, default(NoPush));
    }

    static uint Groups(int count) => (uint)Math.Max(1, (count + Group - 1) / Group);

    /// <summary>A car's four parts, and four again where there is a level above to draw them in.</summary>
    static int CarParts(bool levelled) => levelled ? 8 : 4;

    static Figures FiguresOf(
        SimConfig config, TownSprites looks, SpriteRoom room, int carCapacity, int personCapacity, int headCount,
        SignalTable signals, bool levelled)
    {
        var lamps = config.Lamps;
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
            PersonColumns = PersonCatalog.WalkColumns,
            PersonRows = PersonCatalog.FacingRows,
            FlashHz = lamps.FlashHz,
            FlashOnShare = lamps.FlashOnShare,
            BeaconHz = lamps.BeaconHz,
            HalfLensCellM = LampAtlas.CellM * 0.5f,
            CarHeadHalfX = carHeadHalfM.X,
            CarHeadHalfY = carHeadHalfM.Y,
            WalkHeadHalfX = walkHeadHalfM.X,
            WalkHeadHalfY = walkHeadHalfM.Y,
            HeadReachM = MathF.Max(carHeadHalfM.Length(), walkHeadHalfM.Length()),
            CarHeadRed = (uint)SignalSprites.ColumnOf(true, SignalColour.Red),
            CarHeadAmber = (uint)SignalSprites.ColumnOf(true, SignalColour.Amber),
            CarHeadGreen = (uint)SignalSprites.ColumnOf(true, SignalColour.Green),
            CarHeadFrames = SignalSprites.CarFrames,
            WalkHeadRed = (uint)SignalSprites.ColumnOf(false, SignalColour.Red),
            WalkHeadAmber = (uint)SignalSprites.ColumnOf(false, SignalColour.Amber),
            WalkHeadGreen = (uint)SignalSprites.ColumnOf(false, SignalColour.Green),
            WalkHeadFrames = SignalSprites.WalkFrames,
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
                    lenses[lens].AtBodyM, LampSprites.LensCell(variant, lens, 0, catalogue.SheetCount).Packed,
                    LampSprites.LensCell(variant, lens, 1, catalogue.SheetCount).Packed, LampSprites.GlowM(lenses[lens], config),
                    (uint)fitting | ((uint)LampAtlas.ColourAt(fitting, 0) << 8));
            }
        }

        return rows;
    }

    /// <summary>What each lamp's glow is tinted, by <see cref="CarLamp"/>, packed as an instance carries it.</summary>
    static uint[] Tints(SimConfig config)
    {
        var lamps = Enum.GetValues<CarLamp>();
        var tints = new uint[lamps.Length];
        foreach (var lamp in lamps) tints[(int)lamp] = LampSprites.GlowTint(lamp, config);
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
        var buffer = Own(_vk.CreateBuffer(
            Bytes<T>(Math.Max(1, rows.Length)), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));
        var copy = rows.ToArray();
        buffer.Upload<T>(copy.Length, into => copy.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    GpuBuffer Device<T>(int count, BufferUsageFlags also = 0) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit | also,
            HostAccess.None));

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        // A frame drawing the runs may still be in flight, and its recording names these buffers.
        _vk.Api.DeviceWaitIdle(_vk.Device);
        _batch.Dispose();
        _lay.Dispose();
        _count.Dispose();
        _write.Dispose();
        _scans.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
