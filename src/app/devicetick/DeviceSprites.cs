using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Town;
using static TrafficSimulation.App.DeviceTick.DeviceHoldings;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The frame's moving sprites emitted on the device off the world's own state</b> (<c>app/render/kernels/sprites_world.comp</c>,
/// <c>sprites_count.comp</c>, <c>sprites_write.comp</c>): the walkers, every part of every car, the signal heads and the
/// cars on the level above, written into the over and above runs in <see cref="TownSprites.FillMoving"/>'s order and culled
/// against the view as it culls — drawn where they are written, recorded for the frame that draws them
/// (<see cref="Record"/>, <c>TownRenderer.DrawTheMovingFrom</c>), or read back (<c>Emit</c>, the desktop's instrument).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing of an agent is sent to be drawn</b> (root <c>decision-log.md</c>, 2026-10-11): every row is laid on the
/// device off the world's bodies, the commands and what each car last signalled it shows (<see cref="DeviceWorld.Drawn"/>).
/// What a look is, where its lenses are, the heads and the lights' tables are laid once, in one run of words
/// (<see cref="Tables"/>); the view and the clock are a frame block in a buffer rather than push constants, because the
/// frame that carries this pass is recorded once. <b>Sixteen storage buffers a pass</b>, which is all a page gives a
/// kernel: the same emit draws on both heads (WEB-12).
/// </para>
/// <para>
/// <b>Recorded once a slot</b> — a swapchain image's frame on the desktop, a page's batch ahead of its frame: what a frame
/// changes — the view, the clock, and whether the rows are laid again — is that slot's words (<see cref="WriteTheFrame"/>),
/// so the emit costs a frame no crossing of its own. <b>The rows are laid only when the world has moved</b> (a batch of
/// signals taken, <see cref="WorldLayer.SignalsTaken"/>): the lay's two dispatches read their workgroups out of the frame
/// words, nought on a frame no step or signal came between, and the rows laid for an earlier frame stand.
/// </para>
/// <para>
/// <b>Count, scan, write.</b> One thread a unit counts what each part of it draws into a slot laid in draw order — a cull
/// and nothing built — a <see cref="PrefixScan"/> turns the counts into where each part starts, and a second pass builds
/// every part counted there and the two draws' instance counts — so the order is the host's whatever order the threads ran in.
/// The walkers alone are built aside and put in the order they are drawn by a third (<c>sprites_order.comp</c>, PER-31),
/// a workgroup a walker counting those shown that rank before it: the square of the walkers on screen, and nothing sorted.
/// The instances the host fills and these are one to the bit (<c>--bench deviceworld</c>).
/// </para>
/// </remarks>
internal sealed partial class DeviceSprites : IMovingEmit, IDisposable
{
    /// <summary>
    /// A frame's words (<see cref="WriteTheFrame"/>): its <see cref="FrameRow"/>, copied to where the passes read it, and
    /// the lay's two dispatches' workgroups — cars and walkers — each nought where the world has not moved since the rows
    /// were last laid.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct FrameWords(FrameRow Frame, Groups3 LayCars, Groups3 LayPeople);

    /// <summary>A dispatch's workgroups, as an indirect dispatch reads them.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Groups3(uint X, uint Y = 1, uint Z = 1);

    /// <summary>One run's draw, as an indirect draw reads it (<c>SpriteDraw</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct DrawRow(uint VertexCount, uint InstanceCount, uint FirstVertex, uint FirstInstance);

    static readonly ulong LayCarsAt = Bytes<FrameRow>(1);
    static readonly ulong LayPeopleAt = LayCarsAt + (ulong)Unsafe.SizeOf<Groups3>();

    /// <summary>One walker as the emit reads it (<c>PersonRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PersonRow(Vector2 PositionM, float HeadingRad, float DistanceWalkedM, uint Variant, uint Flags);

    /// <summary>One car as the emit reads it (<c>CarRow</c>, <c>sprites_car.glsl</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CarRow(
        Vector2 PositionM, Vector2 Forward, float HeadingRad, float SteerRad, uint Lamps, uint Variant, uint Flags,
        int Towing, float ForkAlongM, uint TiltHalves);

    /// <summary>The push <c>sprites_world.comp</c> is run with.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct LayPush(uint Count, uint Mode);

    /// <summary>One walker's look (<c>PersonLook</c>): its quad, standing or down.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PersonLook(Vector2 HalfM, float HeightM);

    /// <summary>
    /// One car's look (<c>CarLook</c>): every part's quad and reach, where its wheels stand and which steer a bit each, and
    /// its arm's two pictures.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CarLook(
        Vector2 BodyHalfM, Vector2 WreckHalfM, float BodyReachM, float WreckReachM, Vector2 TyreHalfM,
        DeviceAtlas.WheelPoints WheelsM, uint WheelCount, uint SteeredWheels, float TyreReachM, float WheelWidthM,
        float LampReachM, uint LensCount, uint ExhaustCount,
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

    /// <summary>
    /// The frame's own words (<c>SpriteFrame</c>): the view, the narrowest part, the clock, the rosters, the world's view the
    /// host is showing, how slow a walker is drawn standing (<c>SimConfig.Person.DrawnStandingBelowMps</c>), and the air of
    /// the moments back from the town's (<see cref="Wind.Back"/>), which carries the smoke (CAR-54, OBJ-7).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct FrameRow(
        Vector2 ViewCentreM, Vector2 ViewSpanM, float LeastPartM, float ElapsedS, uint PersonCount, uint CarCount, uint ViewSlot,
        float StandingBelowMps, AirMoments Air);

    /// <summary>
    /// How many moments back the frame hands the air at (<c>AirMoments</c>), from now to the oldest any puff is drawn at
    /// (<see cref="AirStepS"/>).
    /// </summary>
    public const int AirMomentCount = 32;

    [InlineArray(AirMomentCount)]
    internal struct AirMoments
    {
        AirBack _first;
    }

    /// <summary>What is fixed when the town is laid (<c>SpriteFigures</c>), field for field, laid behind the frame's row.</summary>
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
        public uint CasualtyTint;
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
        public float CarTiltEyeHeightM;
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
        public uint WagonCount;
        public uint FirstTrainSheet;
        public uint GearCount;
        public uint BarrierSheet;
        public uint BarrierDriveSheet;
        public uint CrossingLightSheet;
        public uint CrossingLitSheet;
        public uint LightCount;
        public float BarrierWidthM;
        public float DriveHalfLengthM;
        public float DriveHalfWidthM;
        public float LightHalfLengthM;
        public float LightHalfWidthM;
        public uint Bridged;
        public uint BridgeChannel;
        public uint SmokeSheet;
        public uint MostExhausts;
        public float PuffEveryS;
        public float PuffPerEveryS;
        public uint PuffSplits;
        public float ScatterMps;
        public float DragS;
        public ExhaustLook Tailpipe;
        public ExhaustLook Stack;
        public uint ChimneyCount;
        public uint ChimneyPuffsEach;
        public float ChimneyEveryS;
        public float ChimneyPerEveryS;
        public float ChimneyLifeS;
        public float ChimneyRiseS;
        public float ChimneyPerPotWidthM;
        public float ChimneySpreadMps;
        public float ChimneyScatterMps;
        public float ChimneyDragS;
        public float ChimneyOpacity;
        public float ChimneyShade;
        public float ChimneyBreathShare;
        public float ChimneyPerBreathS;
        public float ChimneySmokingShare;
        public float ChimneyPerFireS;
        public float WidestChimneyPuffM;
        public float ChimneyReachM;
        public uint FirstWagonTrack;
        public float PerAirStepS;
    }

    /// <summary>One vehicle of the rolling stock as drawn (<c>TrainLook</c>): its half-size and its exhaust stacks (TRN-15).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct TrainLookRow(Vector2 HalfM, uint ExhaustCount, uint Pad, Vector2 FirstExhaustM, Vector2 SecondExhaustM)
    {
        public static TrainLookRow Of(in TrainVehicle vehicle)
        {
            var exhausts = vehicle.ExhaustsM;
            return new TrainLookRow(
                vehicle.FootprintM * 0.5f, (uint)exhausts.Length, 0u, exhausts.Length > 0 ? exhausts[0] : default,
                exhausts.Length > 1 ? exhausts[1] : default);
        }
    }

    /// <summary>One approach's crossing gear (<c>GearRow</c>, <see cref="CrossingGear"/>), laid once.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct GearRow(Vector2 PivotM, Vector2 DownAlong, float ArmHeadingRad, float ReachM);

    /// <summary>What one approach's gear shows a frame (<c>GearShownRow</c>, <see cref="CrossingSprites.ArmDrawn"/>, <see cref="CrossingGear.Lit"/>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct GearShownRow(Vector2 ArmFromM, Vector2 ArmToM, uint Lit, uint Pad = 0);

    /// <summary>One X light (<c>LightRow</c>, <see cref="CrossingLight"/>), its approach as its gear's row, laid once.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct LightRow(Vector2 CentreM, float HeadingRad, uint Gear);

    /// <summary>The tables laid once, in the order of the run's head (<c>sprites_buffers.glsl</c>'s <c>Table…</c>).</summary>
    enum Table
    {
        PersonLooks,
        CarLooks,
        Lenses,
        LampTints,
        Heads,
        LaneAxis,
        LaneJunction,
        Offsets,
        CrossingAxis,
        CrossingJunction,
        TrainLooks,
        GearRows,
        LightRows,
        Exhausts,
        Chimneys,
        Count,
    }

    /// <summary>Where the scan keeps its grand total (<c>TotalAt</c>).</summary>
    const uint TotalAt = 0;

    /// <summary>The binding of the counts the scan turns into starts (<c>Starts</c>), its totals the next.</summary>
    const int StartsAt = 4;

    /// <summary>
    /// The most workgroups one dispatch is sure of along x (Vulkan's least <c>maxComputeWorkGroupCount[0]</c>): the order
    /// pass runs one a walker of the roster's capacity.
    /// </summary>
    const int MostWorkgroups = 65_535;

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly DeviceHoldings _held;
    readonly int _units;
    readonly int _slots;
    readonly int _personCapacity;
    readonly int _cars;
    readonly int _people;
    readonly SpriteRoom _room;

    /// <summary>The frame's row with the figures laid behind it (<c>FrameAndFigures</c>).</summary>
    readonly GpuBuffer _frame;

    readonly GpuBuffer _instances, _draws;

    /// <summary>The world whose view the host is showing, which the rows are laid off (<see cref="WorldLayer.View"/>).</summary>
    readonly DeviceWorld _world;

    /// <summary>The air over the town, which each frame's words carry the moments back from its own of.</summary>
    readonly Wind _wind;

    readonly float _airStepS;

    /// <summary>What each slot's frame is steered by, written by the host as its frame is: its words, its gear and its rings.</summary>
    sealed class Slot
    {
        public required GpuBuffer Words, Gear, Rings;
    }

    /// <summary>Each slot's, laid the first time it is named.</summary>
    readonly List<Slot> _frames = [];

    /// <summary>The batch of signals the rows were last laid for (<see cref="WorldLayer.SignalsTaken"/>), or −1 before any.</summary>
    long _laidFor = -1;

    readonly ScanKernels _scans;
    readonly PrefixScan _scan;
    readonly ComputeKernel _lay, _count, _write, _order;
    readonly BoundSet _laySet, _countSet, _writeSet, _orderSet;

    /// <summary>The crossings' gear, and what each approach's shows a frame: a row each, written as the frame's words are.</summary>
    readonly CrossingGear _gear;

    readonly GpuBuffer _gearShown;

    /// <summary>Which units wear a ring (<see cref="SpriteRings"/>), copied in from the slot's as the frame's words are.</summary>
    readonly GpuBuffer _rings;

    /// <param name="levelled">Whether the town has a level above its ground (<see cref="TownWorld.Levelled"/>).</param>
    /// <param name="bridged">Whether any of its railway stands on a bridge, so a wagon may be drawn in the run above (RAIL-8).</param>
    public DeviceSprites(
        Vk vk, SimConfig config, TownSprites looks, SpriteRoom room, CarFleet cars, PersonFleet people, SignalHeads heads,
        SignalTable signals, bool levelled, bool bridged, CrossingGear gear, DeviceWorld world, Wind wind)
    {
        _vk = vk;
        _wind = wind;
        _airStepS = AirStepS(looks);
        _held = new DeviceHoldings(vk);
        _config = config;

        // The smoke's tracks (CAR-54, TRN-15) as the world's step lays them.
        var smoke = world.Smoke ?? throw new InvalidOperationException("The emit draws the smoke off tracks the world lays from DrawWith on.");

        // The puffs of smoke a car may have out (CAR-54), which no fill draws and so no fill's room holds: the over run's
        // for every car, and the run above's where it holds any car; a rail vehicle's (TRN-15) in whichever run it is
        // drawn in, as many stacks as the most any look opens, since which look a wagon wears is drawn as its train is sent.
        // And the chimneys' (OBJ-7), last of the over run, so a frame that would draw more than the room holds
        // cuts chimneys' puffs and nothing else — the last pots in the order they were laid, building by building, and not
        // the furthest from the view.
        var wagons = world.View.Wagons.Length;
        var puffs = cars.Capacity * looks.PuffsEach;
        var wagonPuffs = wagons * looks.Rolling.MostExhaustsOnALook * looks.Smoke.Stack.PuffsEach;
        var chimneys = looks.Chimneys;
        var chimneyPuffs = (int)Math.Min((long)chimneys.Length * looks.Chimney.PuffsEach, looks.Chimney.MostPuffsDrawn);
        _room = room = room with
        {
            Over = room.Over + puffs + wagonPuffs + chimneyPuffs,
            Above = room.Above > 0 ? room.Above + puffs + (bridged ? wagonPuffs : 0) : 0,
        };
        _cars = cars.Count;
        _people = people.Count;
        _personCapacity = people.Capacity;
        _gear = gear;
        var lights = gear.Lights;
        _units = people.Capacity + cars.Capacity + wagons + heads.Count + gear.Count + lights.Length + chimneys.Length;
        _slots = (levelled ? 2 : 1) * people.Capacity + (CarParts(levelled) * cars.Capacity) + (bridged ? 2 * 2 * wagons : 2 * wagons) + heads.Count
                 + (CrossingSprites.EachGear * gear.Count) + lights.Length + chimneys.Length;
        if (_slots > ScanKernels.MostCounts) throw new ArgumentOutOfRangeException(nameof(cars), _slots, "more sprite slots than one scan covers");
        if (people.Capacity > MostWorkgroups) throw new ArgumentOutOfRangeException(nameof(people), people.Capacity, "more walkers than the order pass has workgroups for");

        var (driveHalfM, lightHalfM) = (RailLooks.Shared.Drive.FootprintM * 0.5f, RailLooks.Shared.Light.FootprintM * 0.5f);
        var figures = FiguresOf(config, looks, room, cars.Capacity, people.Capacity, heads.Count, signals, levelled) with
        {
            WagonCount = (uint)wagons, FirstTrainSheet = (uint)looks.FirstTrainSheet, GearCount = (uint)gear.Count,
            BarrierSheet = (uint)looks.BarrierSheet, BarrierDriveSheet = (uint)looks.BarrierDriveSheet,
            CrossingLightSheet = (uint)looks.CrossingLightSheet, CrossingLitSheet = (uint)looks.CrossingLitSheet, LightCount = (uint)lights.Length,
            BarrierWidthM = config.Rail.BarrierWidthM, DriveHalfLengthM = driveHalfM.X, DriveHalfWidthM = driveHalfM.Y,
            LightHalfLengthM = lightHalfM.X, LightHalfWidthM = lightHalfM.Y,
            Bridged = bridged ? 1u : 0u, BridgeChannel = CityPlan.RailArrays.BridgeChannel,
            FirstWagonTrack = (uint)smoke.FirstWagon,
        };
        var gearRows = new GearRow[gear.Count];
        for (var each = 0; each < gear.Count; each++)
        {
            gearRows[each] = new GearRow(gear.PivotM[each], gear.DownAlong[each], gear.ArmHeadingRad[each], CrossingSprites.ReachM(gear, each, config));
        }

        var lightRows = new LightRow[lights.Length];
        for (var each = 0; each < lights.Length; each++) lightRows[each] = new LightRow(lights[each].CentreM, lights[each].HeadingRad, (uint)lights[each].Approach);

        var trainLooks = new TrainLookRow[looks.Rolling.Vehicles.Length];
        for (var vehicle = 0; vehicle < looks.Rolling.Vehicles.Length; vehicle++) trainLooks[vehicle] = TrainLookRow.Of(looks.Rolling.Vehicles[vehicle]);

        var tables = new WordTables();
        tables.Add<PersonLook>(Table.PersonLooks, PersonLooks(looks));
        tables.Add<CarLook>(Table.CarLooks, CarLooks(looks, cars));
        tables.Add<LensRow>(Table.Lenses, Lenses(looks, config));
        tables.Add<uint>(Table.LampTints, Tints());
        tables.Add<HeadRow>(Table.Heads, Heads(heads));
        tables.Add<int>(Table.LaneAxis, signals.LaneAxis);
        tables.Add<int>(Table.LaneJunction, signals.LaneJunction);
        tables.Add<float>(Table.Offsets, signals.OffsetS);
        tables.Add<int>(Table.CrossingAxis, signals.CrossingAxis);
        tables.Add<int>(Table.CrossingJunction, signals.CrossingJunction);
        tables.Add<TrainLookRow>(Table.TrainLooks, trainLooks);
        tables.Add<GearRow>(Table.GearRows, gearRows);
        tables.Add<LightRow>(Table.LightRows, lightRows);
        tables.Add<Vector2>(Table.Exhausts, Exhausts(looks));
        tables.Add<ChimneyRow>(Table.Chimneys, chimneys);

        // The frame's row and the figures behind it, sized to the block's alignment of eight, which a page asks the
        // binding to hold whole.
        var frameBytes = Bytes<FrameRow>(1) + Bytes<Figures>(1);
        _frame = _held.Device<uint>((int)((frameBytes + 15) / 16 * 4));
        _frame.Upload<Figures>([figures], Bytes<FrameRow>(1));

        _gearShown = _held.Device<GearShownRow>(Math.Max(1, gear.Count));
        var personRows = _held.Device<PersonRow>(Math.Max(1, people.Capacity));
        var carRows = _held.Device<CarRow>(Math.Max(1, cars.Capacity));
        _rings = _held.Device<uint>(SpriteRings.Length(people.Capacity, cars.Capacity, wagons));

        GpuBuffer[] set =
        [
            _frame,
            _held.Laid<uint>(tables.ToArray()),
            personRows,
            carRows,
            _held.Device<uint>(_slots),
            _held.Device<uint>(4),
            _held.Device<UInt2>(Math.Max(1, cars.Capacity)),
            // A slot above at the least: a frame binds the run above at its own offset, which may not be the buffer's end.
            _instances = _held.Device<SpriteInstance>(room.Over + Math.Max(1, room.Above), BufferUse.Vertex),
            _draws = _held.Device<DrawRow>(2, BufferUse.Indirect),
            world.Drawn.WagonViews[0],
            world.Drawn.WagonViews[1],
            _gearShown,
            smoke.Heads,
            smoke.Points,
            _rings,
            _held.Device<SpriteInstance>(Math.Max(1, people.Capacity)),
        ];

        _scans = new ScanKernels(vk);
        _scan = new PrefixScan(vk, _scans, set[StartsAt], _held.Device<uint>(ScanKernels.Block), set[StartsAt + 1], TotalAt);
        _count = _held.Kernel("sprites_count.comp", set.Length, pushBytes: 0);
        _write = _held.Kernel("sprites_write.comp", set.Length, pushBytes: 0);
        _order = _held.Kernel("sprites_order.comp", set.Length, pushBytes: 0);
        _countSet = _count.Bind(set);
        _writeSet = _write.Bind(set);
        _orderSet = _order.Bind(set);

        var carVariants = new uint[Math.Max(1, cars.Count)];
        for (var car = 0; car < cars.Count; car++) carVariants[car] = cars.Variant[car];
        var personVariants = new uint[Math.Max(1, people.Count)];
        for (var person = 0; person < people.Count; person++) personVariants[person] = people.Variant[person];

        var drawn = world.Drawn;
        _world = world;
        _lay = _held.Kernel("sprites_world.comp", 12, (uint)Marshal.SizeOf<LayPush>());
        _laySet = _lay.Bind(
        [
            carRows, personRows, drawn.Cars, drawn.Walkers, drawn.Commands, _held.Laid<uint>(carVariants),
            _held.Laid<uint>(personVariants), _frame, drawn.CarViews[0], drawn.CarViews[1], drawn.WalkerViews[0],
            drawn.WalkerViews[1],
        ]);
    }

    /// <summary>The emit for a running town whose world is on the device.</summary>
    public static DeviceSprites For(Vk vk, SimConfig config, TownWorld town, TownSprites looks, SpriteRoom room, DeviceWorld world) =>
        new(vk, config, looks, room, town.Cars, town.People, town.Heads, town.Signals.Table, town.Levelled,
            Array.Exists(town.Plan.Rails.Level, static level => level != CityGen.Map.TownMap.RailArrays.AtGrade), town.CrossingGear, world,
            town.Wind);

    /// <summary>
    /// The two runs as the last emit wrote them, on the device — the over run from the first instance and the run above
    /// from <see cref="AboveFrom"/> — for a frame to draw.
    /// </summary>
    public GpuBuffer Instances => _instances;

    /// <summary>The two draws the last emit wrote, the over run's and the run above's, for a frame's indirect draws.</summary>
    public GpuBuffer Draws => _draws;

    /// <summary>Where in <see cref="Instances"/> the run above starts: past the over run's room.</summary>
    public int AboveFrom => _room.Over;

    /// <summary>What a frame's emit has the host write: its frame words, its gear and its rings.</summary>
    public long FrameBytes => (long)(Bytes<FrameWords>(1) + _gearShown.SizeBytes + _rings.SizeBytes);

    /// <summary>
    /// <b>The emit for slot <paramref name="slot"/>'s frame</b>, recorded ahead of the pass that draws the two runs
    /// (<c>TownRenderer.DrawTheMovingFrom</c>): begun behind everything submitted before it — the world's last step, whose
    /// rows it reads, and the frame before, which may still be drawing the runs this one writes. The caller makes its writes
    /// visible to the draws.
    /// </summary>
    public void Record(Recording commands, int slot) => RecordTheEmit(commands, For(slot));

    /// <summary>
    /// <b>What slot <paramref name="slot"/>'s frame emits</b>, written into its words before the frame is submitted:
    /// the view, the clock and the rings, and the rows laid again only where the world has taken a batch of signals since
    /// they were last laid (<paramref name="signalsTaken"/>).
    /// </summary>
    public void WriteTheFrame(int slot, TownWorld town, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre, long signalsTaken)
    {
        var lay = signalsTaken != _laidFor;
        _laidFor = signalsTaken;
        Write(For(slot), town, viewCentreM, viewSpanM, pixelsPerMetre, lay);
    }

    void Write(Slot at, TownWorld town, Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre, bool lay)
    {
        at.Words.Span<FrameWords>()[0] = WordsFor(viewCentreM, viewSpanM, pixelsPerMetre, town.ElapsedS, lay);
        WriteTheGear(at.Gear, town.ElapsedS);
        SpriteRings.Mark(town, at.Rings.Span<uint>());
    }

    /// <summary>A slot's own words, laid the first time it is named.</summary>
    Slot For(int slot)
    {
        while (_frames.Count <= slot) _frames.Add(Lay());
        return _frames[slot];
    }

    Slot Lay()
    {
        var at = new Slot
        {
            Words = Streamed(Bytes<FrameWords>(1), BufferUse.Indirect),
            Gear = Streamed(_gearShown.SizeBytes),
            Rings = Streamed(_rings.SizeBytes),
        };
        at.Words.Span<FrameWords>()[0] = default;
        at.Gear.Span<GearShownRow>().Clear();
        at.Rings.Span<uint>().Clear();
        return at;
    }

    GpuBuffer Streamed(ulong bytes, BufferUse also = 0) =>
        _held.Own(_vk.CreateBuffer(bytes, BufferUse.TransferSrc | also, HostAccess.Streams));

    /// <summary>
    /// What every approach's crossing gear shows at the frame's moment (<see cref="CrossingSprites.ArmDrawn"/>,
    /// <see cref="CrossingGear.Lit"/>), written where the frame copies it from.
    /// </summary>
    void WriteTheGear(GpuBuffer into, float elapsedS)
    {
        var rows = into.Span<GearShownRow>();
        for (var each = 0; each < _gear.Count; each++)
        {
            var (fromM, toM) = CrossingSprites.ArmDrawn(_gear, each, elapsedS, _config);
            rows[each] = new GearShownRow(fromM, toM, _gear.Lit(each, elapsedS) ? 1u : 0u);
        }
    }

    FrameWords WordsFor(Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre, float elapsedS, bool lay)
    {
        var air = default(AirMoments);
        _wind.Back(elapsedS, _airStepS, air);
        return new FrameWords(
            new FrameRow(
                viewCentreM, viewSpanM, _config.View.CarPartLeastPx / pixelsPerMetre, elapsedS, (uint)_people, (uint)_cars,
                (uint)_world.View.Slot, _config.Person.DrawnStandingBelowMps, air),
            new Groups3(lay ? Groups(_cars) : 0u), new Groups3(lay ? Groups(_people) : 0u));
    }

    /// <summary>
    /// How far apart the frame's moments of the air are (<see cref="AirMomentCount"/>): the oldest is as old as any puff is
    /// drawn — a chimney's, a tailpipe's or a stack's.
    /// </summary>
    static float AirStepS(TownSprites looks)
    {
        var oldestS = MathF.Max(looks.Chimney.PuffsEach * looks.Chimney.EveryS, MathF.Max(looks.Smoke.Tailpipe.LongestS, looks.Smoke.Stack.LongestS));
        return oldestS / (AirMomentCount - 1);
    }

    /// <summary>
    /// The emit recorded off slot <paramref name="at"/>'s words: the frame's row, gear and rings sent, every row laid off the
    /// world as many workgroups as the words say, then count, scan, write and the walkers' order — each pass stamped where
    /// an instrument times it (<see cref="Stamp"/>).
    /// </summary>
    void RecordTheEmit(Recording commands, Slot at)
    {
        var vk = _vk;
        vk.AfterTheSubmitsBefore(commands);
        vk.Copy(commands, at.Words, _frame, Bytes<FrameRow>(1));
        vk.Copy(commands, at.Gear, _gearShown, _gearShown.SizeBytes);
        vk.Copy(commands, at.Rings, _rings, _rings.SizeBytes);
        vk.AfterTransfer(commands);
        Stamp(commands, 0);

        _lay.DispatchIndirect(commands, _laySet, at.Words, LayCarsAt, new LayPush((uint)_cars, 0u));
        _lay.DispatchIndirect(commands, _laySet, at.Words, LayPeopleAt, new LayPush((uint)_people, 1u));
        vk.BetweenKernels(commands);
        Stamp(commands, 1);

        var groups = Groups(_units);
        _count.Dispatch(commands, _countSet, groups, default(NoPush));
        vk.BetweenKernels(commands);
        Stamp(commands, 2);
        _scan.Record(commands, _slots);
        Stamp(commands, 3);
        _write.Dispatch(commands, _writeSet, groups, default(NoPush));
        vk.BetweenKernels(commands);
        Stamp(commands, 4);
        _order.Dispatch(commands, _orderSet, (uint)Math.Max(1, _personCapacity), default(NoPush));
        Stamp(commands, 5);
    }

    /// <summary>
    /// The device's time stamped after pass <paramref name="pass"/> of an emit an instrument times (<c>Emit</c>), the first
    /// clearing the rest: the desktop's alone, whose instrument waits for its read-back, which a page cannot.
    /// </summary>
    partial void Stamp(Recording commands, int pass);

    /// <summary>What the desktop's instrument laid of its own (<c>DeviceSprites.ReadBack.cs</c>).</summary>
    partial void DisposeTheReadBack();

    /// <summary>A car's five parts, and five again where there is a level above to draw them in.</summary>
    static int CarParts(bool levelled) => levelled ? 10 : 5;

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
            CasualtyTint = PersonSprites.CasualtyTint,
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
            BeaconHz = config.BeaconHz,
            HalfLensCellM = LampAtlas.CellM * 0.5f,
            CarTiltEyeHeightM = config.View.CarTiltEyeHeightM,
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
            SmokeSheet = (uint)looks.SmokeSheet,
            MostExhausts = CarCatalog.MostExhausts,
            PuffEveryS = looks.Smoke.EveryS,
            PuffPerEveryS = looks.Smoke.PerEveryS,
            PuffSplits = (uint)looks.Smoke.Splits,
            ScatterMps = looks.Smoke.ScatterMps,
            DragS = looks.Smoke.DragS,
            Tailpipe = looks.Smoke.Tailpipe,
            Stack = looks.Smoke.Stack,
            ChimneyCount = (uint)looks.Chimneys.Length,
            ChimneyPuffsEach = (uint)looks.Chimney.PuffsEach,
            ChimneyEveryS = looks.Chimney.EveryS,
            ChimneyPerEveryS = looks.Chimney.PerEveryS,
            ChimneyLifeS = looks.Chimney.LifeS,
            ChimneyRiseS = looks.Chimney.RiseS,
            ChimneyPerPotWidthM = looks.Chimney.PerPotWidthM,
            ChimneySpreadMps = looks.Chimney.SpreadMps,
            ChimneyScatterMps = looks.Chimney.ScatterMps,
            ChimneyDragS = looks.Chimney.DragS,
            ChimneyOpacity = looks.Chimney.Opacity,
            ChimneyShade = looks.Chimney.Shade,
            ChimneyBreathShare = looks.Chimney.BreathShare,
            ChimneyPerBreathS = looks.Chimney.PerBreathS,
            ChimneySmokingShare = looks.Chimney.SmokingShare,
            ChimneyPerFireS = looks.Chimney.PerFireS,
            WidestChimneyPuffM = looks.Chimney.WidestM,
            ChimneyReachM = looks.Chimney.ReachM,
            PerAirStepS = 1f / AirStepS(looks),
        };
    }

    /// <summary>Where every look's tailpipes end (CAR-54), <see cref="CarCatalog.MostExhausts"/> rows a look.</summary>
    static Vector2[] Exhausts(TownSprites looks)
    {
        var catalogue = looks.Cars;
        var rows = new Vector2[catalogue.SheetCount * CarCatalog.MostExhausts];
        for (var variant = 0; variant < catalogue.SheetCount; variant++)
        {
            catalogue.ExhaustsOf(variant).CopyTo(rows.AsSpan(variant * CarCatalog.MostExhausts));
        }

        return rows;
    }

    /// <summary>Each walker's look as <see cref="PersonSprites.Fill"/> sizes it.</summary>
    static PersonLook[] PersonLooks(TownSprites looks)
    {
        var rows = new PersonLook[looks.People.SheetCount];
        for (var variant = 0; variant < rows.Length; variant++)
        {
            var heightM = looks.People.Variants[variant].HeightM;
            rows[variant] = new PersonLook(new Vector2(heightM * looks.Aspects[variant], heightM) * 0.5f, heightM);
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
            var bodyHalfM = new Vector2(build.LengthM, build.WidthM) * 0.5f;
            var wreckHalfM = bodyHalfM * catalogue.Variants[variant].WreckScale;
            var halfDiagonalM = new Vector2(build.LengthM, build.WidthM).Length() * 0.5f;

            var beam = catalogue.BeamOf(variant);
            var collapsed = beam?.Drawn(false) ?? default;
            var extended = beam?.Drawn(true) ?? default;
            var wheelsM = new DeviceAtlas.WheelPoints();
            var steered = 0u;
            for (var wheel = 0; wheel < build.WheelCount; wheel++)
            {
                wheelsM[wheel] = build.WheelAt(wheel).AtM;
                if (build.WheelAt(wheel).IsSteered) steered |= 1u << wheel;
            }

            rows[variant] = new CarLook(
                bodyHalfM, wreckHalfM, bodyHalfM.Length(), wreckHalfM.Length(),
                new Vector2(build.WheelLengthM, build.WheelWidthM) * 0.5f, wheelsM, (uint)build.WheelCount, steered,
                halfDiagonalM + build.WheelLengthM, build.WheelWidthM, halfDiagonalM, (uint)catalogue.LensesOf(variant).Length,
                (uint)catalogue.ExhaustsOf(variant).Length, beam is null ? 0u : 1u, beam?.PivotM ?? Vector2.Zero, collapsed.SizeM * 0.5f,
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
                    lenses[lens].AtBodyM, LampSprites.LensCell(variant, lens, 0, catalogue.SheetCount, lenses).Packed,
                    LampSprites.LensCell(variant, lens, LampAtlas.StatesOf(fitting) - 1, catalogue.SheetCount, lenses).Packed,
                    LampSprites.GlowM(lenses[lens], config),
                    (uint)fitting | ((uint)LampAtlas.ColourAt(fitting, 0) << 8));
            }
        }

        return rows;
    }

    /// <summary>What each lamp's glow is tinted, by <see cref="CarLamp"/>, packed as an instance carries it.</summary>
    static uint[] Tints()
    {
        var lamps = Enum.GetValues<CarLamp>();
        var tints = new uint[lamps.Length];
        foreach (var lamp in lamps) tints[(int)lamp] = LampSprites.GlowTint(lamp);
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

    /// <summary>
    /// <b>Every table laid once, in one run of words</b> (<c>sprites_buffers.glsl</c>'s <c>Tables</c>): a head naming where
    /// each starts, then each table's rows as their bytes stand. One binding, where fifteen would take more storage buffers
    /// than a page gives a kernel.
    /// </summary>
    sealed class WordTables
    {
        readonly uint[] _head = new uint[(int)Table.Count];
        readonly List<uint> _words = [];

        public void Add<T>(Table table, ReadOnlySpan<T> rows) where T : unmanaged
        {
            _head[(int)table] = (uint)(_head.Length + _words.Count);
            _words.AddRange(MemoryMarshal.Cast<byte, uint>(MemoryMarshal.AsBytes(rows)));
        }

        public uint[] ToArray() => [.. _head, .. _words];
    }

    public void Dispose()
    {
        // A frame drawing the runs may still be in flight, and its recording names these buffers.
        _vk.WaitIdle();
        DisposeTheReadBack();
        _scans.Dispose();
        _held.Dispose();
    }
}
