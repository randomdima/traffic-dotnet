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
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Physics;
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
/// <b>The host lays a unit's row from its own fleets</b>, with how far a car's line bends ahead of it
/// (<see cref="CarLamps.TurnAheadRad"/>) in place of the line, and sends the rows that changed. <b>Bound to a device
/// town</b>, a car the car tick still holds as it left it (<see cref="DeviceCarTick.Takes"/>) takes what that tick wrote
/// over the town's copy (<c>sprites_cars.comp</c>), and a car and a walker take where the bodies' step left them off the
/// tyre table and the walker table (<c>sprites_walkers.comp</c>) wherever the town has put neither down since — so the
/// town need not read them back for the frame drawn after a tick, and a frame lays only the units the town set since the
/// last (<see cref="Lay"/>). What looks are,
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

        /// <summary>The walker table holds it as the town has it, and its pose is read off that (<c>sprites_walkers.comp</c>).</summary>
        public const uint PoseFromTheDevice = 8u;
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

        /// <summary>Its pose is the one the bodies' step left in the tyre table, laid over the town's (<c>sprites_cars.comp</c>).</summary>
        public const uint PoseFromTheDevice = 1024u;

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

    readonly GpuBuffer _frame, _hands, _starts, _totals, _instances, _draws;
    readonly GpuBuffer _frameIn, _handsIn;
    readonly GpuBuffer _drawsOut;

    /// <summary>Every walker's and every car's row as the emit reads it, sent where one differs from the last laid.</summary>
    readonly ResidentRows<PersonRow> _people;

    readonly ResidentRows<CarRow> _cars;

    /// <summary>
    /// The walkers and cars the last <see cref="Lay"/> laid, and the cars it laid at a pose the town set since the step —
    /// laid again by the next, which may draw them off the tyre table.
    /// </summary>
    readonly AgentSet _peopleToLay, _carsToLay, _posedByTheTown;

    readonly int[] _laying;

    /// <summary>Whether the last <see cref="Lay"/> drew the cars at the poses the bodies' step left.</summary>
    bool _posedLast;

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

    /// <summary>The device town whose step left the poses a car and a walker are drawn at, its walker tick, and the kernel laying walkers.</summary>
    readonly DeviceTown? _town;

    readonly ComputeKernel? _layWalkers;
    readonly DescriptorSet _layWalkersSet;

    int _peopleLaid;
    int _carsLaid;
    int _handsLaid;
    int _alsoDrivenCar = -1;

    /// <param name="cars">The fleet whose builds the looks are read off, and whose room the car rows are laid for.</param>
    /// <param name="personCapacity">The walkers' roster's room.</param>
    /// <param name="signals">The lights' own tables (<see cref="SignalService.Table"/>), which the heads' colours are read off.</param>
    /// <param name="levelled">Whether the town has a level above its ground (<see cref="TownWorld.Levelled"/>).</param>
    /// <param name="town">
    /// The device town whose car tick a car it still holds is drawn off (<see cref="DeviceCarTick.Drawn"/>), and whose bodies'
    /// step a car and a walker are drawn where it left them — or none: every car and walker as the town holds it.
    /// </param>
    public DeviceSprites(
        Vk vk, SimConfig config, TownSprites looks, SpriteRoom room, CarFleet cars, int personCapacity, SignalHeads heads,
        SignalTable signals, bool levelled, DeviceTown? town = null)
    {
        var ticked = town?.CarTick;
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
        _people = new ResidentRows<PersonRow>(vk, personCapacity, 1, hostOnly: true);
        _cars = new ResidentRows<CarRow>(vk, cars.Capacity, 1, hostOnly: true);
        _peopleToLay = new AgentSet(personCapacity);
        _carsToLay = new AgentSet(cars.Capacity);
        _posedByTheTown = new AgentSet(cars.Capacity);
        _laying = new int[Math.Max(personCapacity, cars.Capacity)];

        GpuBuffer[] set =
        [
            _frame = Device<FrameRow>(1),
            Laid<Figures>([figures]),
            Laid<PersonLook>(PersonLooks(looks)),
            Laid<CarLook>(CarLooks(looks, cars)),
            Laid<LensRow>(Lenses(looks, config)),
            Laid<Vector4>(Tints(config)),
            _people.Device,
            _cars.Device,
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
            _lay = new ComputeKernel(vk, "sprites_cars.comp", 8, (uint)Marshal.SizeOf<LayPush>());
            _laySet = _lay.Bind(
                [_cars.Device, drawn.Ticks, drawn.Commands, drawn.Briefs, drawn.Passes, drawn.Lines, drawn.Arcs, ticked.Tyres.Table.Poses.Device]);
        }

        if (town != null)
        {
            _town = town;
            _layWalkers = new ComputeKernel(vk, "sprites_walkers.comp", 2, sizeof(uint));
            _layWalkersSet = _layWalkers.Bind([_people.Device, town.Walkers.Table]);
        }

        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
    }

    /// <summary>
    /// The emit for a running town: its fleets' room, its heads and lights, and whether it has a level above — and the
    /// device town whose tables what moves is drawn off, where the device ticks the town.
    /// </summary>
    public static DeviceSprites For(Vk vk, SimConfig config, TownWorld world, TownSprites looks, SpriteRoom room, DeviceTown? town = null) =>
        new(vk, config, looks, room, world.Cars, world.People.Capacity, world.Heads, world.Signals.Table, world.Levelled, town);

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

    /// <summary>
    /// <b>A running town's walkers and cars laid as its frame will read them</b> — where bound to a device town, a car at the
    /// pose its bodies' step left in the tyre table where the town has stepped and put it down nowhere since
    /// (<see cref="DeviceTownBodies.PosesLeftInTheTable"/>), and a walker at where the walker table holds it where that is
    /// known to be as the town has it (<see cref="DeviceWalkerTick.HoldsTheBodyOf"/>).
    /// </summary>
    /// <remarks>
    /// <b>Bound to a device town, only the rows the town may have set since the last laying are laid</b>
    /// (<see cref="DeviceCarTick.TheCarsSetSinceTheFrame"/>, <see cref="DeviceWalkerTick.TheWalkersSetSinceTheFrame"/>): a
    /// row leaves out what the emit lays over it off the device's tables, so a car or walker the device keeps holds only
    /// what the town alone sets, and stays as laid. Every row is laid where the cars start or stop being drawn off the tyre
    /// table, and by a laying asked to (<see cref="LaysEveryRow"/>). One laying a town asks what was set since it.
    /// </remarks>
    public void Lay(TownWorld world)
    {
        // The last frame's emit may still be copying out of the rows laid for it.
        _batch.Wait();
        var posedBy = _lay != null && _town != null && _town.Bodies.PosesLeftInTheTable(world) ? world.PhysicsForTheDevice : null;
        var every = LaysEveryRow || _lay == null || _town is not { Walkers.Resident: true } || (posedBy != null) != _posedLast;
        if (every)
        {
            LayPeople(world.People);
            LayCars(world.Cars, world.Recovery, world.HandDriven, world.HandDrivenCar, posedBy);
            UnitsLaid = (world.People.Count, world.Cars.Count);
        }
        else
        {
            UnitsLaid = LayWhatChanged(world, posedBy);
            LayTheHands(world.HandDriven, world.HandDrivenCar);
            if (ChecksTheRows) HoldTheUnlaid(world, posedBy);
        }

        _posedLast = posedBy != null;
    }

    /// <summary>Whether <see cref="Lay"/> lays every row whatever the town set — one laying held against another that does not.</summary>
    public bool LaysEveryRow { get; set; }

    /// <summary>How many walkers and cars the last <see cref="Lay"/> laid.</summary>
    public (int Walkers, int Cars) UnitsLaid { get; private set; }

    /// <summary>
    /// <b>Whether to hold every walker and car <see cref="Lay"/> did not lay to the row laying it would have</b>, and throw
    /// where it differs: one the town changed while the device kept it, and did not mark.
    /// </summary>
    public bool ChecksTheRows { get; set; } = DeviceCarTick.ChecksTheVisitsInEveryTown;

    /// <summary>Every walker of the roster as the emit reads it.</summary>
    public void LayPeople(PersonFleet people)
    {
        _batch.Wait();
        var walkers = _town?.Walkers;
        for (var person = 0; person < people.Count; person++) _people.Lay(person, PersonRowOf(people, walkers, person));
        _peopleLaid = people.Count;
    }

    /// <summary>
    /// Every car of the fleet as the emit reads it, and the hands at the wheel — but where the car tick bound here still
    /// holds a car (<see cref="DeviceCarTick.Takes"/>), what its tick wrote, which the emit lays over the town's copy.
    /// </summary>
    /// <param name="posedBy">
    /// The table of a town whose bodies' step left every car's pose in the tyre table, where each car the town has not put
    /// down since is drawn at that pose — or none.
    /// </param>
    public void LayCars(
        CarFleet cars, RecoveryDuty recovery, ReadOnlySpan<Selection> handDriven, int alsoDrivenCar, PhysicsWorld? posedBy = null)
    {
        _batch.Wait();
        _posedByTheTown.Clear();
        for (var car = 0; car < cars.Count; car++) LayCar(cars, recovery, car, posedBy);
        _carsLaid = cars.Count;
        LayTheHands(handDriven, alsoDrivenCar);
    }

    /// <summary>
    /// The walkers and cars the town may have set anything of since the last <see cref="Lay"/>, those added since, and the
    /// cars whose pose the town set since the step or did when they were last laid — each sent where its row changed.
    /// </summary>
    /// <returns>How many walkers and cars were laid.</returns>
    (int Walkers, int Cars) LayWhatChanged(TownWorld world, PhysicsWorld? posedBy)
    {
        var town = _town!;
        var people = world.People;
        _peopleToLay.Clear();
        town.Walkers.TheWalkersSetSinceTheFrame(world, _peopleToLay);
        for (var person = _peopleLaid; person < people.Count; person++) _peopleToLay.Add(person);
        var walkers = _laying.AsSpan(0, _peopleToLay.CopyTo(people.Count, _laying));
        foreach (var person in walkers) _people.Lay(person, PersonRowOf(people, town.Walkers, person));
        _peopleLaid = people.Count;

        var cars = world.Cars;
        var physics = world.PhysicsForTheDevice;
        _carsToLay.CopyFrom(_posedByTheTown);
        _posedByTheTown.Clear();
        town.CarTick.TheCarsSetSinceTheFrame(world, _carsToLay);
        foreach (var body in physics.Changes)
        {
            var tag = physics.TagOf(new BodyId(body + 1));
            if (tag.Kind == BodyKind.Car) _carsToLay.Add(tag.Index);
        }

        for (var car = _carsLaid; car < cars.Count; car++) _carsToLay.Add(car);
        var walkersLaid = walkers.Length;
        var laying = _laying.AsSpan(0, _carsToLay.CopyTo(cars.Count, _laying));
        foreach (var car in laying) LayCar(cars, world.Recovery, car, posedBy);
        _carsLaid = cars.Count;
        return (walkersLaid, laying.Length);
    }

    /// <summary>
    /// Checked: every walker and car the last <see cref="Lay"/> did not lay held to the row laying it would have — or it
    /// throws, naming one the town changed while the device kept it, and did not mark.
    /// </summary>
    void HoldTheUnlaid(TownWorld world, PhysicsWorld? posedBy)
    {
        var people = world.People;
        for (var person = 0; person < people.Count; person++)
        {
            if (_peopleToLay.Has(person)) continue;

            var row = PersonRowOf(people, _town!.Walkers, person);
            if (row == _people.LaidRow(person)) continue;

            throw new InvalidOperationException(
                $"Walker {person} was changed by the town while the device kept it, and not marked: laid {_people.LaidRow(person)}, now {row}.");
        }

        var cars = world.Cars;
        for (var car = 0; car < cars.Count; car++)
        {
            if (_carsToLay.Has(car)) continue;

            var row = CarRowOf(cars, world.Recovery, car, posedBy);
            if (row == _cars.LaidRow(car)) continue;

            throw new InvalidOperationException(
                $"Car {car} was changed by the town while the device kept it, and not marked: laid {_cars.LaidRow(car)}, now {row}.");
        }
    }

    /// <summary>
    /// One walker as the emit reads it — less where it stands, faces and has walked, and whether it walks, where those are
    /// read off the walker table (<c>sprites_walkers.comp</c>), and nothing but its being inside where it is, which draws
    /// nothing — so its row changes only with what is drawn of what the town alone sets.
    /// </summary>
    static PersonRow PersonRowOf(PersonFleet people, DeviceWalkerTick? walkers, int person)
    {
        if (people.Inside[person].Any) return new PersonRow(default, 0f, 0f, 0u, PersonRow.Inside);

        var flags = people.Wounded[person] ? PersonRow.Down : 0u;
        if (walkers != null && walkers.HoldsTheBodyOf(people, person))
        {
            return new PersonRow(default, 0f, 0f, people.Variant[person], flags | PersonRow.PoseFromTheDevice);
        }

        return new PersonRow(
            people.PositionM[person], people.HeadingRad[person], people.DistanceWalkedM[person], people.Variant[person],
            flags | (people.Walking[person] ? PersonRow.Walking : 0u));
    }

    /// <summary>One car's row laid, and noted where laid at a pose the town set since the step, which the next laying lays again.</summary>
    void LayCar(CarFleet cars, RecoveryDuty recovery, int car, PhysicsWorld? posedBy)
    {
        var row = CarRowOf(cars, recovery, car, posedBy);
        if (posedBy != null && (row.Flags & CarRow.PoseFromTheDevice) == 0) _posedByTheTown.Add(car);
        _cars.Lay(car, row);
    }

    /// <summary>
    /// One car as the emit reads it — less its pose where that is read off the tyre table, and less what its tick wrote
    /// where the car tick still holds it (<c>sprites_cars.comp</c>), so its row changes only with what the town alone sets.
    /// </summary>
    CarRow CarRowOf(CarFleet cars, RecoveryDuty recovery, int car, PhysicsWorld? posedBy)
    {
        var fromTheDevice = _ticked?.Takes(car) ?? false;
        var poseFromTheDevice = posedBy != null && (posedBy.ChangeOf(cars.Body[car].Index) & RowChange.Pose) == 0;
        var flags = (cars.Broken[car] ? CarRow.Broken : 0u) | (cars.Driven[car] ? CarRow.Driven : 0u)
                    | (cars.BlueLight[car] ? CarRow.BlueLight : 0u) | (cars.AtWork[car] ? CarRow.AtWork : 0u)
                    | (cars.LineIsReverse[car] ? CarRow.LineIsReverse : 0u) | (recovery.HeldByTheTail[car] ? CarRow.HeldByTheTail : 0u)
                    | (poseFromTheDevice ? CarRow.PoseFromTheDevice : 0u) | ((uint)cars.LevelOf(car) << CarRow.LevelShift);
        var (positionM, forward, headingRad) = poseFromTheDevice
            ? (Vector2.Zero, Vector2.Zero, 0f)
            : (cars.PositionM[car], Heading.Unit(cars.HeadingRad[car]), cars.HeadingRad[car]);
        if (fromTheDevice)
        {
            return new CarRow(
                positionM, forward, headingRad, 0f, 0f, 0f, 0f, 0f, 0f, cars.FuseJitter[car], 0f, 0f, 0f, 0f, cars.Variant[car],
                flags | CarRow.FromTheDevice, recovery.Towing[car], 0f);
        }

        var pass = cars.Pass[car];
        var command = cars.Command[car];
        flags |= (command.Reverse ? CarRow.Reverse : 0u) | (cars.TurningAtTheBox[car] ? CarRow.TurningAtTheBox : 0u)
                 | (pass.Begun ? CarRow.PassBegun : 0u);
        return new CarRow(
            positionM, forward, headingRad, command.SteerRad, command.BrakeMps2, cars.ProgressM[car], cars.AlongMps[car],
            cars.Context[car].PassAsideM, cars.ToTheBoxM[car], cars.FuseJitter[car], pass.EndsM, pass.SteppedOutM, pass.BackM,
            pass.AsideM, cars.Variant[car], flags, recovery.Towing[car],
            CarLamps.TurnAheadRad(cars.LineOf(car), cars.ProgressM[car], _config.Lamps.TurnAheadM));
    }

    /// <summary>The cars at a hand's wheel, and the one driven besides.</summary>
    void LayTheHands(ReadOnlySpan<Selection> handDriven, int alsoDrivenCar)
    {
        var hands = _handsIn.Span<uint>();
        var held = 0;
        foreach (var unit in handDriven)
        {
            if (unit.Kind == SelectionKind.Car && held < hands.Length) hands[held++] = (uint)unit.Index;
        }

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
        _people.Send(commands);
        _cars.Send(commands);
        vk.Copy(commands, _handsIn, _hands, Bytes<uint>(_handsLaid));
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        if (_lay is { } lay)
        {
            var push = new LayPush((uint)_carsLaid, (uint)_arcsPerCar, _config.Lamps.TurnAheadM);
            lay.Dispatch(commands, _laySet, (uint)Math.Max(1, (_carsLaid + Group - 1) / Group), push);
        }

        _layWalkers?.Dispatch(commands, _layWalkersSet, (uint)Math.Max(1, (_peopleLaid + Group - 1) / Group), (uint)_peopleLaid);
        if (_lay != null || _layWalkers != null) vk.BetweenKernels(commands);

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
        _layWalkers?.Dispose();
        _scans.Dispose();
        _people.Dispose();
        _cars.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
