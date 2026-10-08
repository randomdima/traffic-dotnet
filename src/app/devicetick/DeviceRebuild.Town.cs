using System.Diagnostics;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using static TrafficSimulation.App.DeviceTick.DeviceClaims;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The rebuild laid in the town's place</b> (<see cref="ITownClaims.ClaimsAt"/>): what only the town lays gathered stage
/// by stage as the shadow gathers it, every producer sent what it reads once — the cars' lines read where the car tick
/// keeps them, every car the car tick holds laid off its tables (<c>car_claims_gather.comp</c>), the walkers kept on the
/// device — then bodies, lights, plans, walks and answers in one submit, and what they
/// came to written back into the town before it lays the backing; the backing and the grants are recorded at the head of
/// the tick's own submit (<see cref="RecordTheRest"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Read back as far as the last rebuild's counts and a margin</b> (<see cref="DeviceClaims.RecordReadBounded"/>), since
/// how many bodies are laid is the device's alone and a copy's size is the host's to say: every row, hold, answer and what
/// cut each hold, the lists' order, how each unit was numbered, what each plan came to, the channels under each car, what
/// each car on a pass found and what each walk came to. A rebuild that laid past the margin has the rest read in a second
/// submit (<see cref="ReadPast"/>), counted.
/// </para>
/// <para>
/// <b>A car on a pass is planned by the town as though its pass were clear</b>, and the device asks whether a body stands in
/// it (<c>rebuild_asks.comp</c>): where one does, that car's plan is never filed and its claim is the hold short of the body,
/// as <c>Following.Plan</c> lays it.
/// </para>
/// </remarks>
internal sealed partial class DeviceRebuild
{
    /// <summary>The most cars on a pass one rebuild sends asks for: past it, the town lays its own claims.</summary>
    public const int MostCarsOnAPass = 512;

    /// <summary>The car tick whose lines the plans read and whose grant tables the grants are written into — none for the shadow.</summary>
    readonly DeviceCarTick? _carTick;

    /// <summary>The walker tick whose table the walkers are read in from, where the rebuild keeps them (<see cref="DeviceWalkers.Keeps"/>).</summary>
    readonly DeviceWalkerTick? _walkerTick;

    /// <summary>Kept walkers: where each walker's passes are filed among the rest, scanned, and the kernel that counts and files them.</summary>
    GpuBuffer _walkerPassStarts = null!;

    PrefixScan _scanWalkerPasses = null!;
    Bound _walkerPasses;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct WalkerPassesPush(uint WalkerCount, uint CoverRoom, uint HostPassRows, uint Counting);

    readonly bool _ownsTheClaims;

    /// <summary>What a rebuild in the town hands back besides the claims.</summary>
    GpuBuffer _unitHoldsOut = null!;

    /// <summary>What the rebuild says of each car and each walker, packed (<c>rebuild_agents_out.comp</c>), on the device and read back.</summary>
    GpuBuffer _carsOut = null!, _carsRead = null!, _walkersOut = null!, _walkersRead = null!;

    Bound _agentsOutKernel;

    /// <summary>What a rebuild says of one car (<c>rebuild_agents_out.comp</c>'s <c>CarOut</c>): its plan, and bits.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly record struct CarOut(float ClaimToM, float CommittedToM, float HorizonM, uint Bits)
    {
        public const uint LaidAHold = 1u, HeldInItsPass = 2u;
        public const int ChannelsShift = 8, WheelsShift = 16;
    }

    /// <summary>
    /// What a rebuild says of one walker (<c>rebuild_agents_out.comp</c>'s <c>WalkOut</c>): bits, and what its hold came to —
    /// where it ended, what it keeps off what cut it and the way it was cut on (infinity, nothing and none with no hold).
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly record struct WalkOut(uint Bits, float EndsAtM, float MarginM, int CutOn)
    {
        public const uint LaidAHold = 1u, CommittedAStraight = 2u;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct AgentsPush(uint CarCount, uint WalkerCount, uint FirstWalkerUnit);

    /// <summary>Every hold as the town keeps it once answered (<c>rebuild_holds_out.comp</c>), on the device and read back.</summary>
    GpuBuffer _holdsOut = null!, _holdsRead = null!;

    Bound _holdsOutKernel;

    /// <summary>One hold as the town keeps it once answered (<c>rebuild_holds_out.comp</c>'s <c>HoldOut</c>), field for field.</summary>
    /// <param name="CutterToM">How far the claim that cut it reached when it did — the rest of it being its row, which only a cut moves the end of.</param>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly record struct HoldOut(
        uint FirstRow, uint EndRow, float StandingMarginM, uint Flags, float KeptOffWhatItMayPassM, float PassesBelowMps, float CutLineM,
        float MarginM, uint CutBy, int CutOn, float CutterToM, uint Pad = 0u);

    /// <summary>How many rows and holds the next rebuild in the town reads back in its own submit, and how many rows there is room for.</summary>
    int _rowsBound, _holdsBound, _slotRoom;

    /// <summary>
    /// How much more than the last rebuild laid the next is read back as: a thirty-second more, and this many rows or holds
    /// besides — a bound on the work, and never on what is laid. A town's claims move a few hundred rows from one rebuild to
    /// the next; every row read past what was laid is bus time spent on nothing.
    /// </summary>
    const int ReadPastShare = 32, ReadPastSlack = 1024;

    /// <summary>How many rebuilds in the town laid past what their submit read back, so the rest was read in a second.</summary>
    public long ReadPast { get; private set; }

    /// <summary>
    /// <b>What may not be mirrored exactly</b>: how many times a car at a bay was found on other channels by the device than
    /// it was on — which the town puts it on once the claims come back, after the passes are laid, where its own rebuild puts
    /// it on them before. A manoeuvre swept from its bay in the same rebuild read the atlas on the channels it had.
    /// </summary>
    public long OnOtherChannelsAtABay { get; private set; }

    /// <summary>
    /// <b>What may not be mirrored exactly</b>: how many walkers the device committed to a straight its own walk was read
    /// off, where the town's walk would not have committed them — so that walk was planned down a straight the town does not
    /// hold, and is sent the town's next time.
    /// </summary>
    public long StraightsTheTownWouldNotCommit { get; private set; }

    /// <summary>How many of the host's rows, holds, units and edits the rebuild's own submit sent — the backing's are sent after.</summary>
    int _rowsSent, _holdsSent, _unitsSent, _rowEditsSent, _holdEditsSent;

    int _askCount;

    /// <summary>Room for one car on a pass's asks, as the town writes them.</summary>
    BodyAsk[] _scratchAsks = [];

    /// <summary>Whether a backing was asked of a car this laying, so what it cut of the answers is looked for.</summary>
    bool _backsUp;

    /// <summary>
    /// What only the town knows of a car this rebuild (<c>car_claims_gather.comp</c>'s <c>CarTermsRow</c>), for a car whose
    /// body and plan's rows the device lays off what it holds of it; nothing for a car the town lays them for.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct CarTermsRow(uint Flags, uint Channels, float AllowanceM, float KeptOffM)
    {
        public const uint Held = 1u, DrivesTheRoute = 2u, OnACall = 4u, LineIsReverse = 8u, PassBegun = 16u;
    }

    /// <summary>
    /// One build as a body is laid from (<c>car_claims_gather.comp</c>'s <c>CarShapeRow</c>): its axles ahead of its centre
    /// (<see cref="Agents.Car.Body.TyreModel.WheelAtM"/>), half its track, and its nose and tail past its rear axle.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct CarShapeRow(
        float FrontAxleM, float RearAxleM, float HalfTrackM, float NoseAheadOfAxleM, float TailBehindAxleM, float Pad0 = 0f,
        float Pad1 = 0f, float Pad2 = 0f);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct GatherPush(uint CarCount, float StopSpeedMps);

    /// <summary>Each car's terms, on the device where they changed, and the kernel that lays the cars it holds from them.</summary>
    ResidentRows<CarTermsRow> _carTerms = null!;

    Bound _carsGather;

    /// <summary>
    /// <b>Which cars' bodies and plans' rows the device lays this rebuild</b> (<c>car_claims_gather.comp</c>) — those whose
    /// reading the car tick holds as its own (<see cref="DeviceCarTick.HoldsTheReadingOf"/>) — rather than the town.
    /// </summary>
    bool[] _laidOnTheDevice = [];

    /// <summary>How many rebuilds in the town asked a car backing up for its ground, which the town lays after the answers.</summary>
    public long BackedUp { get; private set; }

    void InTheTownOf(TownWorld world)
    {
        _priming = false;
        var tables = _claims.OnTheDevice;
        _holdsOut = Device<HoldOut>(_holdRoom);
        _holdsRead = Readback<HoldOut>(_holdRoom);
        _holdsOutKernel = Bound.Of<NoPush>(_vk, "rebuild_holds_out.comp", [tables.Holds, tables.Answers, _holdCutters, _holdsOut, tables.Tally]);
        _slotRoom = world.Occupancy.Capacity;
        (_rowsBound, _holdsBound) = Bounds(world.Occupancy.SlotCount, world.Occupancy.HoldCount);
        _unitHoldsOut = Readback<uint>(_units);
        _carsOut = Device<CarOut>(_cars);
        _carsRead = Readback<CarOut>(_cars);
        _walkersOut = Device<WalkOut>(_walkersRoom);
        _walkersRead = Readback<WalkOut>(_walkersRoom);
        var walkerCounts = _walkers.OnTheDevice.Counts;
        _agentsOutKernel = Bound.Of<AgentsPush>(
            _vk, "rebuild_agents_out.comp",
            [_plans.OnTheDevice.Results, _askAnswers, _bodies.OnTheDevice.Counts, _carsOut, walkerCounts, _walkersOut, _unitHolds, tables.Answers]);
        _scratchAsks = new BodyAsk[Overtaking.MostAsksOfAPass(world.Atlas.Marks)];
        LayTheCarsGather(world);
        LayTheWaysOut(world);
        if (!_walkers.Keeps) return;

        var (walkerCovers, _, _, _) = _walkers.OnTheDevice;
        _walkerPassStarts = Device<uint>(_walkersRoom);
        _scanWalkerPasses = new PrefixScan(_vk, _scans, _walkerPassStarts, _blockSums, _sums, (uint)Sum.WalkerPasses);
        _walkerPasses = Bound.Of<WalkerPassesPush>(
            _vk, "rebuild_walker_passes.comp", [walkerCounts, walkerCovers, _walkerPassStarts, tables.Rows, tables.Tally]);
    }

    /// <summary>
    /// The kernel that lays every car the car tick holds off what the device has of it, bound over the car tick's tables
    /// and every build's shape, laid once.
    /// </summary>
    void LayTheCarsGather(TownWorld world)
    {
        var cars = world.Cars;
        var shapes = new CarShapeRow[Agents.Car.Body.CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < shapes.Length; variant++)
        {
            var build = cars.BuildOfVariant(variant);
            shapes[variant] = new CarShapeRow(
                Agents.Car.Body.TyreModel.WheelAtM(build, 0).X, Agents.Car.Body.TyreModel.WheelAtM(build, 2).X, build.HalfTrackM,
                build.NoseAheadOfAxleM, build.TailBehindAxleM);
        }

        var carTick = _carTick!;
        var tyres = carTick.Tyres.Table;
        var (ticks, contexts) = carTick.ReadingTables;
        var (planCars, planFigures, _, _) = _plans.OnTheDevice;
        _carTerms = new ResidentRows<CarTermsRow>(_vk, _cars, 1, hostOnly: true);
        _carsGather = Bound.Of<GatherPush>(
            _vk, "car_claims_gather.comp",
            [
                _carTerms.Device, tyres.BuildOf.Device, Laid<CarShapeRow>(shapes), tyres.Poses.Device, ticks, carTick.Lines.Lines, _grants,
                contexts, _bodies.Laid, planCars, planFigures,
            ]);
        _laidOnTheDevice = new bool[_cars];
    }

    /// <summary>
    /// The kernel that hands back the lists of the ways asked for (<c>rebuild_ways_out.comp</c>), what it writes and reads
    /// back into, and every signalled way, laid once.
    /// </summary>
    void LayTheWaysOut(TownWorld world)
    {
        var tables = _claims.OnTheDevice;
        _lightsWays = [.. world.SignalHolds.Table.Way.ToArray().Distinct().Order()];
        _waysAsked = new Sent<uint>(this, _ways);
        _waysScratch = new int[_ways];
        _waysHeads = Device<UInt4>(_ways);
        _waysHeadsRead = Readback<UInt4>(_ways);
        _waysRows = Device<ClaimRow>(_slotRoom);
        _waysRowsRead = Readback<ClaimRow>(_slotRoom);
        _waysRowCount = Device<uint>(1);
        _waysRowCountRead = Readback<uint>(1);
        _holdCuttersRead = Readback<ClaimRow>(_holdRoom);
        _waysRowsBound = Math.Min(_slotRoom, ReadPastSlack * 4);
        _slotNumbers = new uint[_slotRoom];
        for (var slot = 0; slot < _slotRoom; slot++) _slotNumbers[slot] = (uint)slot;

        _waysOut = Bound.Of<WaysPush>(
            _vk, "rebuild_ways_out.comp",
            [_waysAsked.Device, tables.Rows, tables.Order, tables.BodyRuns, tables.PlannedRuns, _waysHeads, _waysRows, _waysRowCount]);
    }

    /// <summary>
    /// <b>Which cars the device lays the bodies and plans' rows of</b>, each line made current where the car tick keeps it
    /// and each held car's reading laid where the town changed it since, and the poses of every car the town moved since the
    /// last step — at the head of a rebuild, before any body is laid.
    /// </summary>
    void LayWhatTheDeviceHolds(TownWorld town)
    {
        var cars = town.Cars;
        var carTick = _carTick!;
        for (var car = 0; car < _carCount; car++)
        {
            carTick.LayTheLineOf(cars, car);
            _laidOnTheDevice[car] = carTick.HoldsTheReadingOf(cars, car);
            if (_laidOnTheDevice[car]) CarsLaidOnTheDevice++;
        }

        carTick.Tyres.LayThePlacedPoses(cars, town.PhysicsForTheDevice);
    }

    /// <summary>How many cars the device has laid the bodies and plans' rows of itself, over every rebuild in the town.</summary>
    public long CarsLaidOnTheDevice { get; private set; }

    /// <summary>
    /// <b>Whether to hold every car the device lays to what the town would have laid for it</b> (<see cref="CarsLaidUnlike"/>),
    /// but for the way it faces, which is its body's own — a check of the kernel, at the cost of each laying's rows laid on the
    /// host too and copied back.
    /// </summary>
    public bool ChecksTheCarsLaid
    {
        get => _checksTheCars;
        set
        {
            _checksTheCars = value;
            if (!value || _bodiesExpected.Length > 0) return;

            _bodiesExpected = new DeviceBodies.BodyRow[_cars];
            _planRowsExpected = new UInt4[_cars];
            _planFiguresExpected = new System.Numerics.Vector4[_cars * 2];
            _bodiesChecked = Readback<DeviceBodies.BodyRow>(_cars);
            _planRowsChecked = Readback<UInt4>(_cars);
            _planFiguresChecked = Readback<System.Numerics.Vector4>(_cars * 2);
        }
    }

    bool _checksTheCars;
    DeviceBodies.BodyRow[] _bodiesExpected = [];
    UInt4[] _planRowsExpected = [];
    System.Numerics.Vector4[] _planFiguresExpected = [];
    GpuBuffer? _bodiesChecked, _planRowsChecked, _planFiguresChecked;

    /// <summary>How many cars the device laid other than the town would have, over every rebuild checked.</summary>
    public long CarsLaidUnlike { get; private set; }

    /// <summary>Checked: every car the device lays, as the town would lay its body at the head of the rebuild.</summary>
    void ExpectTheBodies(TownWorld town)
    {
        for (var car = 0; car < _carCount; car++)
        {
            if (_laidOnTheDevice[car]) _bodiesExpected[car] = DeviceBodies.RowOf(town, car, _config.Driving.StopSpeedMps) with { Forward = default };
        }
    }

    /// <summary>Checked: every car the device lays, as the town would lay its plan's rows before the plans.</summary>
    void ExpectThePlans(TownWorld town)
    {
        for (var car = 0; car < _carCount; car++)
        {
            if (_laidOnTheDevice[car]) _planRowsExpected[car] = DevicePlans.RowsOf(town, car, _planFiguresExpected.AsSpan(car * 2, 2));
        }
    }

    /// <summary>Checked: what the device laid of every car it lays, copied back, recorded; the caller barriers.</summary>
    void RecordReadTheCarsLaid(CommandBuffer commands)
    {
        var (planCars, planFigures, _, _) = _plans.OnTheDevice;
        _vk.Copy(commands, _bodies.Laid, _bodiesChecked!, Bytes<DeviceBodies.BodyRow>(_carCount));
        _vk.Copy(commands, planCars, _planRowsChecked!, Bytes<UInt4>(_carCount));
        _vk.Copy(commands, planFigures, _planFiguresChecked!, Bytes<System.Numerics.Vector4>(_carCount * 2));
    }

    /// <summary>How many cars the device laid other than the town would have, this rebuild.</summary>
    int CarsLaidOtherwise()
    {
        var bodies = _bodiesChecked!.Span<DeviceBodies.BodyRow>();
        var rows = _planRowsChecked!.Span<UInt4>();
        var figures = _planFiguresChecked!.Span<System.Numerics.Vector4>();
        var unlike = 0;
        for (var car = 0; car < _carCount; car++)
        {
            if (!_laidOnTheDevice[car]) continue;

            if (bodies[car] with { Forward = default } != _bodiesExpected[car] || rows[car] != _planRowsExpected[car]
                || figures[car * 2] != _planFiguresExpected[car * 2] || figures[(car * 2) + 1] != _planFiguresExpected[(car * 2) + 1])
            {
                unlike++;
            }
        }

        return unlike;
    }

    /// <summary>Every car's terms laid where they changed, as the cars stand before their plans: nothing for one the town lays.</summary>
    void LayTheCarsTerms(TownWorld town)
    {
        var cars = town.Cars;
        for (var car = 0; car < _carCount; car++)
        {
            if (!_laidOnTheDevice[car])
            {
                _carTerms.Lay(car, default);
                continue;
            }

            var flags = CarTermsRow.Held | (cars.BlueLight[car] ? CarTermsRow.OnACall : 0u)
                        | (cars.LineIsReverse[car] ? CarTermsRow.LineIsReverse : 0u) | (cars.Pass[car].Begun ? CarTermsRow.PassBegun : 0u);
            var drives = town.DrivesItsRoute(car);
            if (drives) flags |= CarTermsRow.DrivesTheRoute;

            // What it keeps off what it may pass is read off its lane, and only one under way on its line has one.
            var keptOffM = drives && cars.Line[car].LaneCount > 0 ? town.KeptOffWhatItMayPassM(car) : 0f;
            _carTerms.Lay(car, new CarTermsRow(flags, cars.Channels[car], town.OffTheLineAllowanceM(car), keptOffM));
        }
    }

    /// <summary>Kept walkers: how many passes each walker's body laid counted and scanned into where each is filed, recorded.</summary>
    void RecordTheWalkersPassesCounted(CommandBuffer commands)
    {
        _walkerPasses.Dispatch(commands, _walkerCount, new WalkerPassesPush((uint)_walkerCount, (uint)_walkers.CoverRoom, 0u, 1u));
        _vk.BetweenKernels(commands);
        _scanWalkerPasses.Record(commands, _walkerCount);
    }

    /// <summary>
    /// Kept walkers: every walker's passes filed after the host's (<c>rebuild_walker_passes.comp</c>), where the town files its
    /// own sidesteps after the cars' passes — recorded; the caller barriers.
    /// </summary>
    void RecordTheWalkersPassesFiled(CommandBuffer commands) =>
        _walkerPasses.Dispatch(
            commands, _walkerCount, new WalkerPassesPush((uint)_walkerCount, (uint)_walkers.CoverRoom, (uint)_passes.RowCount, 0u));

    /// <summary>How many asks there is room for: <see cref="MostCarsOnAPass"/> cars' worth.</summary>
    static int AskRoom(TownWorld world) => Math.Min(world.Cars.Capacity, MostCarsOnAPass) * Overtaking.MostAsksOfAPass(world.Atlas.Marks);

    /// <summary>Whether this rebuild's cars on a pass fit the room for their asks.</summary>
    public static bool HasRoomForThePasses(TownWorld world)
    {
        var cars = world.Cars;
        var onAPass = 0;
        for (var car = 0; car < cars.Count; car++)
        {
            if (cars.Pass[car].Begun) onAPass++;
        }

        return onAPass <= MostCarsOnAPass;
    }

    /// <summary>The host's time writing what the last rebuild in the town sent, in milliseconds — the stages' gathering included.</summary>
    public double LaidMs { get; private set; }

    /// <summary>Of it, the host's time from the rebuild's beginning to every walker stationed, in milliseconds — the town's own stationing, all but the cars' bodies sent.</summary>
    public double StationedMs { get; private set; }

    /// <summary>The host's time recording, submitting and waiting for it, in milliseconds.</summary>
    public double WaitedMs { get; private set; }

    /// <summary>The host's time writing what came back into the town, in milliseconds.</summary>
    public double MirroredMs { get; private set; }

    /// <summary>
    /// Of what the last rebuild in the town sent, in bytes: the cars' bodies and their plans' rows the town laid, the walkers
    /// as stationed and changed since, the cars' lines, the readings, poses and terms of the cars the device lays itself,
    /// each asked car's claim, and the rows, holds and asks the town laid itself.
    /// </summary>
    public (ulong Bodies, ulong Plans, ulong Walkers, ulong Lines, ulong Held, ulong CarClaims, ulong TownsOwn) SentParts { get; private set; }

    /// <summary>How many bytes the last rebuild in the town sent, and how many it read back.</summary>
    public (ulong Sent, ulong Read) Crossed { get; private set; }

    /// <summary>How many copy regions the last rebuild in the town's own submit recorded (<see cref="Runtime.Vk.RegionsCopied"/>).</summary>
    public int RegionsCopied { get; private set; }

    /// <summary>How many cars on a pass the last rebuild in the town found a body inside.</summary>
    public int HeldInTheirPasses { get; private set; }

    /// <summary>How many rows the last rebuild in the town laid, how many of them bodies and passes, and how many holds.</summary>
    public (int Rows, int Bodies, int Holds) Counted =>((int)_claims.TallyRead[Tally.Rows], (int)_claims.TallyRead[Tally.PassesEnd], (int)_claims.TallyRead[Tally.Holds]);

    long _laidFrom;

    /// <summary>
    /// <b>The town at one stage of a rebuild the device lays</b> (<see cref="ITownClaims.ClaimsAt"/>): what each producer is
    /// sent once the town has it, what the town laid itself since the stage before, and at
    /// <see cref="ClaimsStage.PlansLaid"/> the rebuild through the answers recorded, submitted, waited for and written back.
    /// </summary>
    public void InTheTown(TownWorld town, ClaimsStage stage)
    {
        var claims = town.Occupancy;
        switch (stage)
        {
            case ClaimsStage.Begun:
                _laidFrom = Stopwatch.GetTimestamp();
                _carCount = town.Cars.Count;
                _walkerCount = town.People.Count;
                _hostRowCount = _hostHoldCount = _rowEditCount = _holdEditCount = _askCount = 0;
                _hostUnitCount = (int)StandingUnit + 1;

                // Every body reads its occupant's line and every plan its own car's, where the car tick keeps them; no
                // stage of a laying lays a line, so one made current here serves both.
                LayWhatTheDeviceHolds(town);
                _bodies.Lay(town, _laidOnTheDevice.AsSpan(0, _carCount));
                if (_checksTheCars) ExpectTheBodies(town);
                break;

            case ClaimsStage.Stationed:
                StationedMs = Stopwatch.GetElapsedTime(_laidFrom).TotalMilliseconds;
                if (_walkers.Keeps) _walkers.LayTheKept(town, _walkerTick!);
                else _walkers.LayTheStationed(town);
                break;

            case ClaimsStage.CarsLaid:
                _carsLaidSlots = claims.SlotCount;
                break;

            case ClaimsStage.PassesLaid:
                _passesLaidSlots = claims.SlotCount;
                LayTheUnit(PassesUnit, claims, _carsLaidSlots, claims.HoldCount, Numbering.Passes, 0u);
                _passes = new HostRange(0, _hostRowCount, 0, 0);
                break;

            case ClaimsStage.PlansDue:
                _plans.Lay(town, _laidOnTheDevice.AsSpan(0, _carCount));
                LayTheCarsTerms(town);
                if (_checksTheCars) ExpectThePlans(town);
                _timeS = town.ElapsedS;
                _plansDueSlots = claims.SlotCount;
                _plansDueHolds = claims.HoldCount;
                KeepWhatWasLaid(claims, _passesLaidSlots, _plansDueSlots, _plansDueHolds);
                break;

            case ClaimsStage.DrivesDue:
                Watch(town, stage);
                break;

            case ClaimsStage.WalksDue:
                if (!_walkers.Keeps) _walkers.LayWhatChanged(town);
                LayTheCarsClaims(town);
                _planned = new HostRange(_passes.RowCount, _hostRowCount - _passes.RowCount, 0, _hostHoldCount);
                break;

            case ClaimsStage.PlansLaid:
                RecordThroughTheAnswers(town);
                break;

            case ClaimsStage.PlansAnswered:
                _answeredSlots = claims.SlotCount;
                _answeredHolds = claims.HoldCount;
                _backsUp = false;
                foreach (var car in town.CarsAskedThisLaying) _backsUp |= town.Cars.Action[car] == CarAction.BackUp;
                if (_backsUp)
                {
                    KeepWhatWasLaid(claims, 0, _answeredSlots, _answeredHolds);
                    BackedUp++;
                }
                break;

            case ClaimsStage.WalkersDue:
                var (rowsAt, holdsAt) = (_hostRowCount, _hostHoldCount);
                LayTheUnit(_hostUnitCount++, claims, _answeredSlots, _answeredHolds, Numbering.Backing, 0u);
                _backing = new HostRange(rowsAt, _hostRowCount - rowsAt, holdsAt, _hostHoldCount - holdsAt);
                _answeredEdits = _backsUp ? WhatWasCut(claims, 0, _answeredSlots, _answeredHolds, laid: false) : new HostRange(_rowEditCount, 0, _holdEditCount, 0);
                break;

            case ClaimsStage.GrantsRead:
                Pending = true;
                break;
        }
    }

    /// <summary>
    /// <b>Every body, light, plan and walk laid and every plan answered and cut, in one submit</b>: what was gathered sent,
    /// the chain recorded as far as the cut, everything it came to read back, and written into the town.
    /// </summary>
    void RecordThroughTheAnswers(TownWorld town)
    {
        var laid = Stopwatch.GetTimestamp();
        var vk = _vk;
        var regionsFrom = Runtime.Vk.RegionsCopied;
        var commands = _batch.Begin();
        _timer.Reset(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        _rowsSent = _hostRowCount;
        _holdsSent = _hostHoldCount;
        _unitsSent = _hostUnitCount;
        _rowEditsSent = _rowEditCount;
        _holdEditsSent = _holdEditCount;
        var townsOwn = Sent<ClaimRow>.BytesOf(_hostRowCount) + Sent<uint>.BytesOf(_hostRowCount) + Sent<HoldRow>.BytesOf(_hostHoldCount)
                       + Sent<uint>.BytesOf(_hostHoldCount) + Sent<HostUnit>.BytesOf(_hostUnitCount) + Sent<RowEdit>.BytesOf(_rowEditCount)
                       + Sent<HoldEdit>.BytesOf(_holdEditCount) + Sent<BodyAskRow>.BytesOf(_askCount);
        SentParts = (
            _bodies.SentBytes, _plans.SentBytes, _walkers.ResidentSentBytes, _carTick!.LinesMarkedBytes,
            _carTick.ReadingsMarkedBytes + _carTerms.MarkedBytes, _carClaims.MarkedBytes, townsOwn);
        var sent = SentParts.Bodies + SentParts.Plans + SentParts.Walkers + SentParts.Lines + SentParts.Held + SentParts.CarClaims + townsOwn;
        _bodies.RecordSend(commands);
        _plans.RecordSend(commands);
        var keeps = _walkers.Keeps;
        if (keeps) _walkers.RecordTheKeptSend(commands);
        else _walkers.RecordTheResidentSend(commands);
        _carTick.RecordTheLinesSent(commands);
        _carTick.RecordTheReadingsSent(commands);
        _carTerms.Send(commands);
        _hostRows.Send(commands, _hostRowCount);
        _hostRowUnit.Send(commands, _hostRowCount);
        _hostHolds.Send(commands, _hostHoldCount);
        _hostHoldUnit.Send(commands, _hostHoldCount);
        _hostUnits.Send(commands, _hostUnitCount);
        _rowEdits.Send(commands, _rowEditCount);
        _holdEdits.Send(commands, _holdEditCount);
        _carClaims.Send(commands);
        _asks.Send(commands, _askCount);
        vk.Fill(commands, _wayCounts, 0);
        GrantedWithTheAnswers = !AnyCarBacksUp(town);
        LandsLater = GrantedWithTheAnswers && !MirrorsAtOnce;
        if (LandsLater) RecordTheWaysAskedSent(commands, town.ReadersOccupancy);

        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Sent);

        _carsGather.Dispatch(commands, _carCount, new GatherPush((uint)_carCount, _config.Driving.StopSpeedMps));
        vk.BetweenKernels(commands);
        RecordTheBodies(commands);
        _timer.Stamp(commands, (int)Stage.BodiesLaid);
        RecordThePlans(commands);
        _timer.Stamp(commands, (int)Stage.PlansLaid);

        _claims.RecordAnswers(commands);
        _capture.DispatchIndirect(commands, _claims.OnTheDevice.Tally, Tally.HoldsGroups, default(NoPush));
        vk.BetweenKernels(commands);
        _claims.RecordCut(commands);
        _timer.Stamp(commands, (int)Stage.Answered);

        _holdsOutKernel.DispatchIndirect(commands, _claims.OnTheDevice.Tally, Tally.HoldsGroups, default(NoPush));
        _agentsOutKernel.Dispatch(
            commands, Math.Max(_carCount, _walkerCount), new AgentsPush((uint)_carCount, (uint)_walkerCount, (uint)(_stretches + 1 + _carCount)));
        if (LandsLater) _waysOut.Dispatch(commands, _waysListed, new WaysPush((uint)_waysListed, (uint)_slotRoom));
        if (GrantedWithTheAnswers) _grant.Dispatch(commands, _carCount, new UnitsPush((uint)_carCount, (uint)(_stretches + 1)));
        if (keeps)
        {
            // Read with the cars' grants or not at all: where a car backs up, the town reads every walker's after it.
            if (GrantedWithTheAnswers)
            {
                _walkerAnswers.Dispatch(commands, _walkerCount, new UnitsPush((uint)_walkerCount, (uint)(_stretches + 1 + _carCount)));
                vk.BetweenKernels(commands);
                _walkers.Record(commands, DeviceWalkers.Piece.Read);
            }

            _walkers.RecordTheKeptOut(commands);
        }

        vk.BeforeTransfer(commands);
        if (LandsLater)
        {
            _claims.RecordReadTheCounts(commands);
        }
        else
        {
            _claims.RecordReadBounded(commands, _rowsBound, 0);
            _claims.CopyPast<HoldOut>(commands, _holdsOut, _holdsRead, 0, _holdsBound);
        }

        vk.Copy(commands, _unitHolds, _unitHoldsOut, Bytes<uint>(_units));
        vk.Copy(commands, _carsOut, _carsRead, Bytes<CarOut>(_carCount));
        if (keeps) _walkers.RecordReadTheKept(commands);
        else vk.Copy(commands, _walkersOut, _walkersRead, Bytes<WalkOut>(_walkerCount));
        var walkersRead = keeps ? _walkers.KeptReadBytes : Bytes<WalkOut>(_walkerCount);
        if (GrantedWithTheAnswers) vk.Copy(commands, _grants, _grantsOut, Bytes<DeviceCarTick.GrantRow>(_carCount));
        if (_checksTheCars) RecordReadTheCarsLaid(commands);
        _timer.Stamp(commands, (int)Stage.Granted);
        _timer.Stamp(commands, (int)Stage.Read);
        RegionsCopied = (int)(Runtime.Vk.RegionsCopied - regionsFrom);
        _batch.SubmitAndWait();
        _timer.Read();
        var waited = Stopwatch.GetTimestamp();
        if (_checksTheCars) CarsLaidUnlike += CarsLaidOtherwise();

        _claims.TakeTheCounts();
        if (LandsLater)
        {
            _landing = Landing.Due;
        }
        else
        {
            ReadWhatWasLaidPast();
            MirrorTheLayer(town.ReadersOccupancy);
        }

        MirrorTheAgents(town);
        var rows = _claims.Rows.Length;
        var holds = _claims.Holds.Length;
        var layer = Bytes<ClaimRow>(rows) + Bytes<uint>(rows) + Bytes<HoldOut>(holds);
        Crossed = (sent, (LandsLater ? 0ul : layer) + Bytes<uint>(_units) + Bytes<CarOut>(_carCount) + walkersRead
                         + (GrantedWithTheAnswers ? Bytes<DeviceCarTick.GrantRow>(_carCount) : 0ul));
        LaidMs = Stopwatch.GetElapsedTime(_laidFrom, laid).TotalMilliseconds;
        WaitedMs = Stopwatch.GetElapsedTime(laid, waited).TotalMilliseconds;
        MirroredMs = Stopwatch.GetElapsedTime(waited).TotalMilliseconds;
        Rebuilds++;
    }

    /// <summary>
    /// The rows and holds the rebuild laid past what its submit read back, read in a second submit — and the next rebuild
    /// read as far as this one's counts and a margin.
    /// </summary>
    void ReadWhatWasLaidPast()
    {
        var (rows, holds) = (_claims.Rows.Length, _claims.Holds.Length);
        if (rows > _rowsBound || holds > _holdsBound)
        {
            ReadPast++;
            var commands = _batch.Begin();
            _claims.RecordReadPast(commands, _rowsBound, holds);
            _claims.CopyPast<HoldOut>(commands, _holdsOut, _holdsRead, _holdsBound, holds);
            _batch.SubmitAndWait();
        }

        (_rowsBound, _holdsBound) = Bounds(rows, holds);
    }

    /// <summary>How many rows and holds a rebuild after one that laid so many is read back as, within the claims' room.</summary>
    (int Rows, int Holds) Bounds(int rows, int holds) =>
        (Math.Min(_slotRoom, rows + (rows / ReadPastShare) + ReadPastSlack),
         Math.Min(_holdRoom, holds + (holds / ReadPastShare) + ReadPastSlack));

    /// <summary>
    /// <b>What the device laid and answered written into the layer every reader reads</b>, as the town's own laying would
    /// have left it: every slot, hold and list (<see cref="LaneOccupancy.MirrorTheSlot"/>).
    /// </summary>
    void MirrorTheLayer(LaneOccupancy occupancy)
    {
        var rows = _claims.Rows;
        var holds = _holdsRead.Span<HoldOut>()[.._claims.Holds.Length];
        occupancy.BeginTheMirror(rows.Length, holds.Length);
        for (var slot = 0; slot < rows.Length; slot++)
        {
            ref readonly var row = ref rows[slot];
            occupancy.MirrorTheSlot(slot, (int)row.Way, row.Claim, row.IsGone, row.StandsToM);
        }

        for (var hold = 0; hold < holds.Length; hold++)
        {
            ref readonly var held = ref holds[hold];
            var terms = new HolderTerms(held.KeptOffWhatItMayPassM, (held.Flags & HoldRow.OnACall) != 0, held.PassesBelowMps);
            var cutBy = held.CutBy == ClaimAnswer.NoRow ? LaneClaim.Nothing : rows[(int)held.CutBy].Claim with { ToM = held.CutterToM };
            occupancy.MirrorTheHold(
                hold, (int)held.FirstRow, (int)held.EndRow, held.StandingMarginM, (held.Flags & HoldRow.Answered) != 0, terms, held.CutLineM,
                held.MarginM, cutBy, held.CutOn);
        }

        // Every row was listed, each way's two lists a run of the order in turn: a run ends where its way or its kind does.
        var order = _claims.Order;
        var first = 0;
        for (var at = 1; at <= order.Length; at++)
        {
            ref readonly var head = ref rows[(int)order[first]];
            if (at < order.Length && rows[(int)order[at]].Way == head.Way && rows[(int)order[at]].HasBody == head.HasBody) continue;

            occupancy.MirrorTheList((int)head.Way, head.HasBody, order[first..at]);
            first = at;
        }
    }

    /// <summary>
    /// <b>What the device laid and answered of each agent written into the town</b>: the hold each car's claim and each
    /// walker's walk laid, what each car's plan came to, the channels found under each car, and how the answers went — and,
    /// where the device keeps the walkers, everything its stages made of each it changed.
    /// </summary>
    void MirrorTheAgents(TownWorld town)
    {
        var unitHolds = _unitHoldsOut.Span<uint>();
        var carsOut = _carsRead.Span<CarOut>();
        var firstCar = _stretches + 1;
        HeldInTheirPasses = 0;
        foreach (var car in town.CarsAskedThisLaying)
        {
            ref readonly var claim = ref _carClaims.LaidRow(car);
            ref readonly var said = ref carsOut[car];
            if ((claim.Flags & CarClaimRow.PlansOnTheDevice) != 0)
            {
                town.PlannedOnTheDevice(car, said.ClaimToM, said.CommittedToM, said.HorizonM);
                var laid = (said.Bits & CarOut.LaidAHold) != 0;
                town.HeldOnTheDevice(car, laid ? (int)unitHolds[firstCar + car] : LaneOccupancy.NoHold, inItsPass: false);
            }
            else if (claim.AskCount > 0 && (said.Bits & CarOut.HeldInItsPass) != 0)
            {
                HeldInTheirPasses++;
                town.HeldOnTheDevice(car, (int)unitHolds[firstCar + car], inItsPass: true);
            }
            else
            {
                town.HeldOnTheDevice(
                    car, claim.HoldUnit >= 0 ? (int)unitHolds[claim.HoldUnit] + claim.HoldOffset : LaneOccupancy.NoHold, inItsPass: false);
            }
        }

        var firstWalker = firstCar + _carCount;
        if (_walkers.Keeps)
        {
            _walkers.TakeTheKept(town, _walkerTick!, unitHolds, firstWalker);
        }
        else
        {
            var walks = _walkersRead.Span<WalkOut>();
            for (var person = 0; person < _walkerCount; person++)
            {
                var hold = (walks[person].Bits & WalkOut.LaidAHold) != 0 ? (int)unitHolds[firstWalker + person] : LaneOccupancy.NoHold;
                var committed = (walks[person].Bits & WalkOut.CommittedAStraight) != 0;
                if (committed) _walkers.SendTheStraightAgain(person);
                if (!town.WalkedOnTheDevice(person, hold, committed)) StraightsTheTownWouldNotCommit++;
            }
        }

        var cars = town.Cars;
        for (var car = 0; car < _carCount; car++)
        {
            var channels = (byte)(carsOut[car].Bits >> CarOut.ChannelsShift);
            if (channels != cars.Channels[car] && CarActions.IsAtABay(cars.Action[car])) OnOtherChannelsAtABay++;

            town.OnTheGroundOfItsBody(car, channels, (byte)(carsOut[car].Bits >> CarOut.WheelsShift));
        }

        town.AnsweredOnTheDevice(LaidAgain(_claims.Moved, _config.Road.MostSettlingPasses, out var settled), settled);
    }

    /// <summary>
    /// How many answers moved past the first pass before the town's own answering would have stopped
    /// (<see cref="LaneOccupancy.AnswerThePlans"/>) — on the first pass nothing moved, or on the first odd one at the bound
    /// — and whether that was because nothing moved.
    /// </summary>
    static int LaidAgain(ReadOnlySpan<uint> moved, int mostPasses, out bool settled)
    {
        var laidAgain = 0;
        settled = true;
        for (var pass = 1; pass < moved.Length; pass++)
        {
            laidAgain += (int)moved[pass];
            if (moved[pass] == 0) return laidAgain;
            if (pass >= mostPasses && pass % 2 == 1)
            {
                settled = false;
                return laidAgain;
            }
        }

        return laidAgain;
    }

    /// <summary>
    /// <b>The rest of a rebuild laid in the town, recorded at the head of the tick's own submit</b>: the backing the town laid
    /// after the answers and what it cut of them, then every asked car's grant written where the car tick reads it. The
    /// caller's next kernels and copies are barriered against it.
    /// </summary>
    public void RecordTheRest(CommandBuffer commands)
    {
        var vk = _vk;
        Pending = false;
        _hostRows.Send(commands, _rowsSent, _hostRowCount - _rowsSent);
        _hostRowUnit.Send(commands, _rowsSent, _hostRowCount - _rowsSent);
        _hostHolds.Send(commands, _holdsSent, _hostHoldCount - _holdsSent);
        _hostHoldUnit.Send(commands, _holdsSent, _hostHoldCount - _holdsSent);
        _hostUnits.Send(commands, _unitsSent, _hostUnitCount - _unitsSent);
        _rowEdits.Send(commands, _rowEditsSent, _rowEditCount - _rowEditsSent);
        _holdEdits.Send(commands, _holdEditsSent, _holdEditCount - _holdEditsSent);
        vk.AfterTransfer(commands);
        RecordTheBacking(commands);
        if (!GrantedWithTheAnswers) _grant.Dispatch(commands, _carCount, new UnitsPush((uint)_carCount, (uint)(_stretches + 1)));
        vk.BetweenKernels(commands);
        vk.BeforeTransfer(commands);
    }

    /// <summary>What the last rebuild in the town said of every walker, as its own submit read it back.</summary>
    public ReadOnlySpan<WalkOut> WalksRead => _walkersRead.Span<WalkOut>()[.._walkerCount];

    /// <summary>
    /// <b>Whether the last rebuild in the town read every asked car's grant with the answers</b>, in its own submit, and
    /// handed them back (<see cref="GrantsRead"/>) — so the town's tail reads no grant off the claims. Not where a car
    /// backs up: the town lays the backing after the answers, and it may take a plan queued behind it.
    /// </summary>
    public bool GrantedWithTheAnswers { get; private set; }

    /// <summary>
    /// <b>Whether the rebuild just laid in the town writes its layer back later</b> than its own stages (owner, root decision
    /// log 2026-10-07), <b>and in part</b> (2026-10-08): only its counts and what it says of each agent come back with it;
    /// then, behind the tick's own submit, every hold and what cut it, and of the rows only the lists of the ways the
    /// readers asked about lately (<see cref="LaneOccupancy.Knows(int)"/>) and the lights' holds on every signalled way
    /// (<see cref="SendTheLayerBack"/>) — written into the readers' layer on the tick after, while that tick's submit runs
    /// (<see cref="LandWhatWasSent"/>), or before any rebuild that comes sooner (<see cref="Land"/>). Never where a car backs
    /// up, whose ground the town lays off the whole answered layer, nor where the layer is mirrored at once.
    /// </summary>
    public bool LandsLater { get; private set; }

    /// <summary>
    /// How many mirrors a way asked about goes on being sent back after it was last asked about
    /// (<see cref="LaneOccupancy.WaysAskedSince"/>): a second's worth of rebuilds at the claims' interval, so a decision on
    /// its own clock finds what it read last time still there.
    /// </summary>
    const int WaysAskedForMirrors = 20;

    /// <summary>A way asked for whose lights' holds alone are sent back (<c>rebuild_ways_out.comp</c>'s <c>WayLightsOnly</c>).</summary>
    const uint LightsOnly = 0x80000000u;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct WaysPush(uint WayCount, uint RowRoom);

    /// <summary>Every way a light holds a stretch of, ascending, laid once: what a layer sent back in part always holds the lights' holds of.</summary>
    int[] _lightsWays = [];

    /// <summary>The ways asked for this rebuild, ascending — <see cref="LightsOnly"/> marking those whose lights' holds alone are.</summary>
    Sent<uint> _waysAsked = null!;

    int[] _waysScratch = [];
    int _waysListed;

    /// <summary>What <c>rebuild_ways_out.comp</c> hands back — a head a way asked for, its rows, how many — and every hold's cutter, read back.</summary>
    GpuBuffer _waysHeads = null!, _waysRows = null!, _waysRowCount = null!;

    GpuBuffer _waysHeadsRead = null!, _waysRowsRead = null!, _waysRowCountRead = null!, _holdCuttersRead = null!;
    Bound _waysOut;

    /// <summary>How many of the ways' rows a landing reads back with the rest, from the last's count and a margin.</summary>
    int _waysRowsBound;

    /// <summary>The slots a layer sent back in part numbers its rows into, in order: a list's slots are a run of them.</summary>
    uint[] _slotNumbers = [];

    /// <summary>How many bytes the last layer sent back carried.</summary>
    public ulong LandedBytes { get; private set; }

    /// <summary>How many ways the last layer sent back held the lists of, and how many it held the lights' holds of alone.</summary>
    public (int Known, int LightsOnly) WaysLanded { get; private set; }

    /// <summary>
    /// <b>Every way the readers asked about lately, and every signalled way, listed and sent</b> — each way
    /// once, ascending — and the count of what <c>rebuild_ways_out.comp</c> hands back cleared. The caller barriers.
    /// </summary>
    void RecordTheWaysAskedSent(CommandBuffer commands, LaneOccupancy readers)
    {
        var asked = readers.WaysAskedSince(readers.Mirrors - WaysAskedForMirrors, _waysScratch);
        var listed = _waysAsked.Rows;
        var (fromAsked, fromLights, count) = (0, 0, 0);
        while (fromAsked < asked || fromLights < _lightsWays.Length)
        {
            if (fromLights == _lightsWays.Length || (fromAsked < asked && _waysScratch[fromAsked] <= _lightsWays[fromLights]))
            {
                if (fromLights < _lightsWays.Length && _waysScratch[fromAsked] == _lightsWays[fromLights]) fromLights++;
                listed[count++] = (uint)_waysScratch[fromAsked++];
            }
            else
            {
                listed[count++] = (uint)_lightsWays[fromLights++] | LightsOnly;
            }
        }

        _waysListed = count;
        _waysAsked.Send(commands, count);
        _vk.Fill(commands, _waysRowCount, 0);
    }

    /// <summary>
    /// Whether every rebuild in the town writes its layer back with its own stages — for whoever holds that layer to the
    /// town's own on the tick it was laid. On by default; a device ticking the town in step turns it off.
    /// </summary>
    /// <remarks>
    /// <b>Only where the device keeps the walkers</b> (<see cref="DeviceWalkers.Keeps"/>), whose passes' word is given off
    /// the device's own answers: a town giving it would read the layer the readers read, a laying old.
    /// </remarks>
    public bool MirrorsAtOnce
    {
        get => _mirrorsAtOnce;
        set
        {
            if (!value && !_walkers.Keeps) throw new InvalidOperationException("A layer written back later needs the walkers kept on the device.");

            _mirrorsAtOnce = value;
        }
    }

    bool _mirrorsAtOnce = true;

    /// <summary>Where a layer that lands later is: laid, or sent back.</summary>
    enum Landing
    {
        None,
        Due,
        Sent,
    }

    Landing _landing;

    /// <summary>
    /// <b>The layer of a rebuild that lands later copied back, in part</b> (<see cref="LandsLater"/>), submitted and left
    /// running: every hold and what cut it, and the rows of the ways asked for as far as the last landing's count and a
    /// margin. Nothing else on the device writes them before the next rebuild. Nothing where no layer waits to be sent.
    /// </summary>
    public void SendTheLayerBack()
    {
        if (_landing != Landing.Due) return;

        var holds = _claims.Holds.Length;
        _waysRowsBound = Math.Min(_slotRoom, _waysRowsBound);
        var commands = _batch.Begin();
        _vk.BeforeTransfer(commands);
        _vk.Copy(commands, _waysRowCount, _waysRowCountRead, sizeof(uint));
        _claims.CopyPast<UInt4>(commands, _waysHeads, _waysHeadsRead, 0, _waysListed);
        _claims.CopyPast<ClaimRow>(commands, _waysRows, _waysRowsRead, 0, _waysRowsBound);
        _claims.CopyPast<HoldOut>(commands, _holdsOut, _holdsRead, 0, holds);
        _claims.CopyPast<ClaimRow>(commands, _holdCutters, _holdCuttersRead, 0, holds);
        LandedBytes = sizeof(uint) + Bytes<UInt4>(_waysListed) + Bytes<ClaimRow>(_waysRowsBound) + Bytes<HoldOut>(holds) + Bytes<ClaimRow>(holds);
        _landing = Landing.Sent;
        LayersSentBack++;
        _batch.Submit();
    }

    /// <summary>
    /// <b>A rebuild that landed later written into the town</b>: its layer copied back in part (or now, if it was not yet
    /// sent) and waited for — the rows past what it read read in a second submit — written into the layer every reader
    /// reads, and the town's own numbers for it taken up (<see cref="TownWorld.LandTheLaying"/>). Nothing where no layer
    /// waits.
    /// </summary>
    public void Land(TownWorld town)
    {
        if (_landing == Landing.None) return;

        SendTheLayerBack();
        var waited = Stopwatch.GetTimestamp();
        _batch.Wait();
        var rows = ReadTheWaysPast();
        var landed = Stopwatch.GetTimestamp();
        MirrorTheWaysAsked(town.ReadersOccupancy, rows);
        if (ChecksTheLayerSentInPart) LayersUnlike += UnlikeTheWholeLayer(town);
        town.LandTheLaying();
        _landing = Landing.None;
        LandingWaitedMs = Stopwatch.GetElapsedTime(waited, landed).TotalMilliseconds;
        LandedMs = Stopwatch.GetElapsedTime(landed).TotalMilliseconds;
    }

    /// <summary>
    /// How many of the ways' rows the landing handed back, the rest past what it read read now — and the next read as far
    /// as this one's count and a margin.
    /// </summary>
    int ReadTheWaysPast()
    {
        var count = (int)_waysRowCountRead.Span<uint>()[0];
        if (count > _waysRowsBound)
        {
            ReadPast++;
            var commands = _batch.Begin();
            _claims.CopyPast<ClaimRow>(commands, _waysRows, _waysRowsRead, _waysRowsBound, Math.Min(count, _slotRoom));
            _batch.SubmitAndWait();
        }

        _waysRowsBound = count + (count / ReadPastShare) + ReadPastSlack;
        return Math.Min(count, _slotRoom);
    }

    /// <summary>
    /// <b>A layer sent back in part written into a layer of the host's</b> (<see cref="LaneOccupancy.KnowTheWay"/>): every
    /// way asked for's lists, each in the order the rebuild laid it, every other signalled way's planned list holding its
    /// lights' holds alone, and every hold with what it came to and what cut it, as the claim stood when it did.
    /// </summary>
    void MirrorTheWaysAsked(LaneOccupancy occupancy, int count)
    {
        var rows = _waysRowsRead.Span<ClaimRow>()[..count];
        var heads = _waysHeadsRead.Span<UInt4>()[.._waysListed];
        var holds = _holdsRead.Span<HoldOut>()[.._claims.Holds.Length];
        var cutters = _holdCuttersRead.Span<ClaimRow>()[..holds.Length];
        occupancy.BeginTheMirror(count, holds.Length, everyWay: false);
        for (var slot = 0; slot < count; slot++)
        {
            ref readonly var row = ref rows[slot];
            occupancy.MirrorTheSlot(slot, (int)row.Way, row.Claim, row.IsGone, row.StandsToM);
        }

        var (known, lightsOnly) = (0, 0);
        for (var at = 0; at < heads.Length; at++)
        {
            ref readonly var head = ref heads[at];
            var (way, first, bodies, planned) = ((int)head.X, (int)head.Y, (int)head.Z, (int)head.W);
            if ((_waysAsked.Rows[at] & LightsOnly) != 0)
            {
                lightsOnly++;
            }
            else
            {
                occupancy.KnowTheWay(way);
                known++;
            }

            occupancy.MirrorTheList(way, true, _slotNumbers.AsSpan(first, bodies));
            occupancy.MirrorTheList(way, false, _slotNumbers.AsSpan(first + bodies, planned));
        }

        WaysLanded = (known, lightsOnly);
        for (var hold = 0; hold < holds.Length; hold++)
        {
            ref readonly var held = ref holds[hold];
            var terms = new HolderTerms(held.KeptOffWhatItMayPassM, (held.Flags & HoldRow.OnACall) != 0, held.PassesBelowMps);
            var cutBy = held.CutBy == ClaimAnswer.NoRow ? LaneClaim.Nothing : cutters[hold].Claim with { ToM = held.CutterToM };
            occupancy.MirrorTheHold(
                hold, 0, 0, held.StandingMarginM, (held.Flags & HoldRow.Answered) != 0, terms, held.CutLineM, held.MarginM, cutBy,
                held.CutOn);
        }
    }

    /// <summary>
    /// <b>Whether to hold every layer sent back in part to the whole of it</b> (<see cref="LayersUnlike"/>) — a check of what
    /// the readers read, at the cost of every row of each layer read back too and written into a layer of its own.
    /// </summary>
    public bool ChecksTheLayerSentInPart { get; set; }

    /// <summary>How many holds and ways a layer sent back in part held other than the whole layer did, over every landing checked.</summary>
    public long LayersUnlike { get; private set; }

    LaneOccupancy? _wholeLayer;
    LaneClaim[] _partList = [], _wholeList = [];

    /// <summary>
    /// The layer just landed in part held to the whole of it, read back now: every hold, every way asked for's two lists,
    /// and the lights' holds of every other signalled way — how many differ.
    /// </summary>
    int UnlikeTheWholeLayer(TownWorld town)
    {
        var commands = _batch.Begin();
        _claims.RecordReadTheRows(commands, 0, _claims.Rows.Length);
        _claims.RecordReadTheOrder(commands);
        _batch.SubmitAndWait();

        var part = town.ReadersOccupancy;
        var whole = _wholeLayer ??= new LaneOccupancy(town.Ways, _slotRoom, _holdRoom, town.Atlas.Marks);
        if (_partList.Length == 0) (_partList, _wholeList) = (new LaneClaim[_slotRoom], new LaneClaim[_slotRoom]);

        MirrorTheLayer(whole);
        var unlike = 0;
        for (var hold = 0; hold < _claims.Holds.Length; hold++)
        {
            var partM = part.HoldEndsAtM(hold, out var partMarginM, out var partCut);
            var wholeM = whole.HoldEndsAtM(hold, out var wholeMarginM, out var wholeCut);
            if (!partM.Equals(wholeM) || !partMarginM.Equals(wholeMarginM) || partCut != wholeCut || part.HoldCutOn(hold) != whole.HoldCutOn(hold))
            {
                unlike++;
            }
        }

        for (var at = 0; at < _waysListed; at++)
        {
            var way = (int)(_waysAsked.Rows[at] & ~LightsOnly);
            if ((_waysAsked.Rows[at] & LightsOnly) != 0)
            {
                var lights = 0;
                var listed = whole.CopyPlannedTo(way, _wholeList);
                foreach (ref readonly var claim in _wholeList.AsSpan(0, listed))
                {
                    if (claim.Of == LaneRoster.Signal) _wholeList[lights++] = claim;
                }

                if (!_partList.AsSpan(0, part.CopyPlannedTo(way, _partList)).SequenceEqual(_wholeList.AsSpan(0, lights))) unlike++;
                continue;
            }

            if (!_partList.AsSpan(0, part.CopyBodiesTo(way, _partList)).SequenceEqual(_wholeList.AsSpan(0, whole.CopyBodiesTo(way, _wholeList)))
                || !_partList.AsSpan(0, part.CopyPlannedTo(way, _partList)).SequenceEqual(_wholeList.AsSpan(0, whole.CopyPlannedTo(way, _wholeList))))
            {
                unlike++;
            }
        }

        return unlike;
    }

    /// <summary>A layer sent back whole on earlier ticks written into the town (<see cref="Land"/>); nothing for one still being sent.</summary>
    public void LandWhatWasSent(TownWorld town)
    {
        if (_landing == Landing.Sent) Land(town);
    }

    /// <summary>How many layers were copied back after their rebuild's own submit.</summary>
    public long LayersSentBack { get; private set; }

    /// <summary>The host's time waiting for the last layer copied back, and writing it into the town, in milliseconds.</summary>
    public double LandingWaitedMs { get; private set; }

    /// <inheritdoc cref="LandingWaitedMs"/>
    public double LandedMs { get; private set; }

    /// <summary>Whether any car this laying asked is backing up, whose ground the town lays after the answers.</summary>
    static bool AnyCarBacksUp(TownWorld town)
    {
        foreach (var car in town.CarsAskedThisLaying)
        {
            if (town.Cars.Action[car] == CarAction.BackUp) return true;
        }

        return false;
    }
}
