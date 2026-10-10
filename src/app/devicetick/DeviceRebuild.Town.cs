using System.Diagnostics;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using static TrafficSimulation.App.DeviceTick.DeviceClaims;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The rebuild laid in the town's place</b> (<see cref="ITownClaims.ClaimsAt"/>): what only the town lays gathered stage
/// by stage as the shadow gathers it, every producer sent what it reads once — the cars' lines read where the car tick
/// keeps them, every car the car tick holds laid off its tables (<c>car_claims_gather.comp</c>), the walkers kept on the
/// device — then bodies, lights, plans, walks, answers, the backing and every grant in one submit, and what they came to
/// written back into the town.
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
/// <b>A car on a pass is planned past it as though its pass were clear</b>, and the device asks whether a body stands in it
/// (<c>rebuild_asks.comp</c>): where one does, that car's plan is never filed and its claim is the hold short of the body,
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

    /// <summary>
    /// The kernel that copies each kept walker's hold as the town's readers read it off the last rebuild laid here
    /// (<c>walkers_readers.comp</c>), and that rebuild's walkers and first walker's unit — or none laid yet.
    /// </summary>
    Bound _walkersReaders;

    int _walkersLaid = -1, _firstWalkerUnitLaid;

    /// <summary>What a rebuild says of one car (<c>rebuild_agents_out.comp</c>'s <c>CarOut</c>): its plan, and bits.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly record struct CarOut(float ClaimFromM, float ClaimToM, float CommittedToM, float HorizonM, uint Bits)
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

    /// <summary>
    /// What a rebuild hands back of a car the town holds, or one found on other channels (<c>rebuild_cars_out.comp</c>'s
    /// <c>CarAnswer</c>): its <see cref="CarOut"/>, its grant, and the stamp of the rebuild that wrote it.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly record struct CarAnswer(
        float ClaimFromM, float ClaimToM, float CommittedToM, float HorizonM, uint Bits, DeviceCarTick.GrantOut Grant, uint Stamp);

    /// <summary>What a hold word of <c>rebuild_cars_out.comp</c> says: no hold, or the hold held short of the body inside its pass.</summary>
    const uint HoldNone = 0x7fffffffu, HoldInItsPass = 0x80000000u;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct CarsOutPush(uint CarCount, uint FirstCarUnit, uint Stamp);

    /// <summary>
    /// <see cref="AnswersOnlyWhatTheTownHolds"/>: which cars the town holds as the device left them, a bit a car, written by
    /// the host; what the rebuild says of those and of the cars found on other channels, each asked car's hold, and the cars
    /// found on other channels — in the host's own memory — and how many those were.
    /// </summary>
    GpuBuffer _answersWanted = null!, _carAnswers = null!, _carHolds = null!, _carsMoved = null!, _carsMovedCount = null!, _carsMovedCountRead = null!;

    Bound _carsOutKernel;

    /// <summary>The stamp the last rebuild in the town wrote its cars' answers with, never the zero of one unwritten.</summary>
    uint _answerStamp;

    /// <summary>Whether the rebuild just laid in the town hands back only the cars the town holds (<see cref="AnswersOnlyWhatTheTownHolds"/>).</summary>
    bool _answeredInPart;

    /// <summary>The cars the town held as the device left them when the rebuild was laid, a bit a car; and those found on other channels.</summary>
    uint[] _wantedWords = [], _carsMovedBits = [];

    /// <summary>How many cars the last rebuild in the town found on other channels, and how many it handed back in all.</summary>
    int _movedCount, _carsAnswered;

    /// <summary>
    /// <b>Whether a rebuild in the town hands back only the cars the town holds as the device left them</b>
    /// (<see cref="DeviceCarTick.TownHolds"/>; <see cref="DeviceCarTick.ReadsEveryCar"/> off) and those found on other
    /// channels than the town laid them on (<c>rebuild_cars_out.comp</c>), every asked car's hold besides — rather than
    /// every car's plan, grant, channels and wheels. Every other car's plan and grant stay as the town's laying cleared them,
    /// and come back with its reading (<see cref="DeviceCarTick.OutRow.Grant"/>) — all but how far its plan reached
    /// (<see cref="Agents.Car.Body.CarFleet.ClaimToM"/>), which only probes of a town the host lays read; which of its
    /// wheels stand on a way come back with its tyre row.
    /// </summary>
    public bool AnswersOnlyWhatTheTownHolds => _carTick is { ReadsEveryCar: false };

    /// <summary>Whether the last rebuild in the town handed back only the cars the town holds (<see cref="AnswersOnlyWhatTheTownHolds"/>).</summary>
    public bool AnsweredInPart => _answeredInPart;

    /// <summary>
    /// What the last rebuild in the town handed back of a car the town held when it was laid (<see cref="AnsweredInPart"/>) —
    /// false for any other car, whose plan and grant are the device's.
    /// </summary>
    public bool HandedBack(int car, out CarAnswer said)
    {
        said = default;
        if ((_wantedWords[car >> 5] & (1u << (car & 31))) == 0) return false;

        said = _carAnswers.Span<CarAnswer>()[car];
        ExpectTheStamp(car, said);
        return true;
    }

    void ExpectTheStamp(int car, in CarAnswer said)
    {
        if (said.Stamp != _answerStamp) throw new InvalidOperationException($"Car {car}'s answer was read past the rebuild that wrote it.");
    }

    /// <summary>The cars the town holds as the device left them, marked for the rebuild to hand back.</summary>
    void WantTheCarsTheTownHolds()
    {
        var wanted = _wantedWords.AsSpan();
        _carTick!.TheCarsTheTownHolds(_carCount, wanted);

        // Built here and copied across whole: the table is memory the host only writes, slow to read back a word at a time.
        wanted.CopyTo(_answersWanted.Span<uint>());
    }

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
    int _rowsBound, _holdsBound;

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

    /// <summary>
    /// What only the town knows of a car this rebuild (<c>car_claims_gather.comp</c>'s <c>CarTermsRow</c>), for a car whose
    /// body and plan's rows the device lays off what it holds of it — on a pass begun, where on its line the pass ends;
    /// nothing for a car the town lays them for.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    readonly record struct CarTermsRow(
        uint Flags, uint Channels, float AllowanceM, float KeptOffM, float PassEndsM = 0f, uint Pad0 = 0u, uint Pad1 = 0u, uint Pad2 = 0u)
    {
        public const uint Held = 1u, DrivesTheRoute = 2u, OnACall = 4u, LineIsReverse = 8u, PassBegun = 16u, Asked = 32u;
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

    /// <summary>Which cars this laying asked (<see cref="TownWorld.CarsAskedThisLaying"/>), by car.</summary>
    bool[] _askedThisLaying = [];

    /// <summary>How many rebuilds in the town laid a car's backing (<c>rebuild_backing.comp</c>).</summary>
    public long BackedUp { get; private set; }

    void InTheTownOf(TownWorld world)
    {
        _priming = false;
        var tables = _claims.OnTheDevice;
        _holdsOut = Device<HoldOut>(_holdRoom);
        _holdsRead = Readback<HoldOut>(_holdRoom);
        _holdsOutKernel = Bound.Of<NoPush>(_vk, "rebuild_holds_out.comp", [tables.Holds, tables.Answers, _holdCutters, _holdsOut, tables.Tally]);
        (_mostRows, _mostPieces, _mostProposed) = (
            world.Occupancy.Capacity, DevicePlans.MostPiecesOf(world.Atlas.Marks), DeviceWalkers.MostProposed(world.Atlas.Marks));
        (_rowsBound, _holdsBound) = Bounds(world.Occupancy.SlotCount, world.Occupancy.HoldCount);
        _unitHoldsOut = Readback<uint>(_units);
        _carsOut = Device<CarOut>(_cars);
        _carsRead = Readback<CarOut>(_cars);
        _walkersOut = Device<WalkOut>(_walkersRoom);
        _walkersRead = Readback<WalkOut>(_walkersRoom);
        var walkerCounts = _walkers.OnTheDevice.Counts;
        _agentsOutKernel = Bound.Of<AgentsPush>(
            _vk, "rebuild_agents_out.comp",
            [
                _plans.OnTheDevice.Results, _askAnswers, _bodies.OnTheDevice.Counts, _carsOut, walkerCounts, _walkersOut, _unitHolds,
                tables.Answers, _carTick!.Tyres.Table.Cars.Device, tables.Tally,
            ]);
        if (_walkers.Keeps)
        {
            _walkersReaders = Bound.Of<UnitsPush>(
                _vk, "walkers_readers.comp", [_walkers.KeptTable, _unitHolds, tables.Answers, _holdCutters, _walkers.ReadersHolds]);
        }

        LayTheCarsGather(world);
        LayTheCarsOut();
        LayTheHoldsAsked(_carTick!);
        LayTheWaysOut(world);
        if (!_walkers.Keeps) return;

        _walkers.HandBackThrough(_unitHolds, tables.Tally);
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
        _askedThisLaying = new bool[_cars];
    }

    /// <summary>
    /// The kernel that hands back only the cars the town holds and those found on other channels
    /// (<c>rebuild_cars_out.comp</c>), and the host's memory it writes into, laid once.
    /// </summary>
    void LayTheCarsOut()
    {
        _answersWanted = Own(_vk.CreateBuffer(Bytes<uint>((_cars + 31) / 32), BufferUsageFlags.StorageBufferBit, HostAccess.Writes));
        _carAnswers = Own(_vk.CreateBuffer(Bytes<CarAnswer>(_cars), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _carHolds = Own(_vk.CreateBuffer(Bytes<uint>(_cars), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _carsMoved = Own(_vk.CreateBuffer(Bytes<uint>(_cars), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _carsMovedCount = Device<uint>(1);
        _carsMovedCountRead = Readback<uint>(1);
        _wantedWords = new uint[(_cars + 31) / 32];
        _carsMovedBits = new uint[_wantedWords.Length];
        _carsOutKernel = Bound.Of<CarsOutPush>(
            _vk, "rebuild_cars_out.comp",
            [
                _carClaims.Device, _plans.OnTheDevice.Results, _askAnswers, _unitHolds, _bodies.OnTheDevice.Counts, _carTerms.Device,
                _grants, _answersWanted, _carAnswers, _carHolds, _carsMoved, _carsMovedCount, _standingHoldOf,
            ]);
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
        _waysRows = Device<ClaimRow>(_claims.RowRoom);
        _waysRowsRead = Readback<ClaimRow>(_claims.RowRoom);
        _waysRowCount = Device<uint>(1);
        _waysRowCountRead = Readback<uint>(1);
        _holdCuttersRead = Readback<ClaimRow>(_holdRoom);
        _waysRowsBound = Math.Min(_claims.RowRoom, ReadPastSlack * 4);
        NumberTheSlots();

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
        carTick.TakeBackWhatTheTownMoved(town);
        _layingCount = carTick.TheCarsSetSinceTheLaying(town, _layingCars).Length;
        foreach (var car in LayingCars)
        {
            carTick.LayTheLineOf(cars, car);
            _laidOnTheDevice[car] = carTick.HoldsTheReadingOf(cars, car);
            if (_laidOnTheDevice[car]) CarsLaidOnTheDevice++;
        }

        carTick.Tyres.LayThePlacedPoses(cars, town.PhysicsForTheDevice);
    }

    /// <summary>
    /// <b>The cars this laying lays again</b> (<see cref="DeviceCarTick.TheCarsSetSinceTheLaying"/>), in order: every other is a
    /// car the device keeps and the town set nothing of since the last, whose line, reading, body, plan, terms and claim are
    /// what that laying left — held so where the visits are checked (<see cref="HoldTheUnlaidCars"/>).
    /// </summary>
    ReadOnlySpan<int> LayingCars => _layingCars.AsSpan(0, _layingCount);

    int[] _layingCars = [];
    int _layingCount;

    /// <summary>
    /// Checked: every car this laying did not lay again held to what laying it would have — laid on the device, and the same
    /// terms and claim — or it throws.
    /// </summary>
    void HoldTheUnlaidCars(TownWorld town)
    {
        var cars = town.Cars;
        var asked = _askedThisLaying.AsSpan(0, _carCount);
        var laying = LayingCars;
        var next = 0;
        for (var car = 0; car < _carCount; car++)
        {
            if (next < laying.Length && laying[next] == car)
            {
                next++;
                continue;
            }

            // A car the device keeps, or one at rest whose rows the host laid and nothing has moved since.
            var unlike = !_laidOnTheDevice[car] && !_carTick!.IsLeftAtRest(car) ? "whether the device lays it"
                : asked[car] != _askedNow[car] || (_laidOnTheDevice[car] && !asked[car]) ? "whether the laying asks it"
                : asked[car] && town.PlansItsLine(car) != _plansItsLine[car] ? "whether it plans down its line"
                : cars.Pass[car].Begun || _onAPass[car] ? "its pass"
                : TermsOf(town, cars, car, asked[car]) != _carTerms.LaidRow(car) ? "its terms"
                : !_carClaims.IsLaid(car, [ClaimOf(town, car)]) ? "its claim"
                : null;
            if (unlike is null) continue;

            throw new InvalidOperationException(
                $"Car {car}'s claim was changed by the town while the device kept it, and not marked touched: {unlike}.");
        }
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

            _checkedLaid = new bool[_cars];
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

    /// <summary>How many cars the device laid were held to what the town would have laid (<see cref="ExpectTheBodies"/>), over every rebuild checked.</summary>
    public long CarsLaidChecked { get; private set; }

    /// <summary>
    /// Checked: every car the device lays, as the town would lay its body at the head of the rebuild — of those the town
    /// holds as the device left them (<see cref="DeviceCarTick.TownHolds"/>); a car the device kept is laid off what only it
    /// holds.
    /// </summary>
    void ExpectTheBodies(TownWorld town)
    {
        for (var car = 0; car < _carCount; car++)
        {
            _checkedLaid[car] = _laidOnTheDevice[car] && _carTick!.TownHolds(car);
            if (_checkedLaid[car]) _bodiesExpected[car] = DeviceBodies.RowOf(town, car, _config.Driving.StopSpeedMps) with { Forward = default };
        }
    }

    /// <summary>Checked: every car <see cref="ExpectTheBodies"/> checks, as the town would lay its plan's rows before the plans.</summary>
    void ExpectThePlans(TownWorld town)
    {
        for (var car = 0; car < _carCount; car++)
        {
            if (_checkedLaid[car]) _planRowsExpected[car] = DevicePlans.RowsOf(town, car, _planFiguresExpected.AsSpan(car * 2, 2));
        }
    }

    /// <summary>Which cars the device lays this rebuild are held to what the town would have laid (<see cref="ExpectTheBodies"/>).</summary>
    bool[] _checkedLaid = [];

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
            if (!_checkedLaid[car]) continue;

            CarsLaidChecked++;
            if (bodies[car] with { Forward = default } != _bodiesExpected[car] || rows[car] != _planRowsExpected[car]
                || figures[car * 2] != _planFiguresExpected[car * 2] || figures[(car * 2) + 1] != _planFiguresExpected[(car * 2) + 1])
            {
                unlike++;
            }
        }

        return unlike;
    }

    /// <summary>
    /// Every car's terms laid where they changed, as the cars stand before their plans: of one the town lays, its channels alone.
    /// </summary>
    /// <remarks>
    /// <b>A car the device holds has its claim cleared there</b> where this laying asked it: the ground it covered since
    /// counted from nothing again on the device (<c>car_claims_gather.comp</c>), as the town's own clearing counts it
    /// (<see cref="Agents.Car.Actions.DrivingGround.ClearTheClaim"/>) — so the town's is no change to send.
    /// </remarks>
    void LayTheCarsTerms(TownWorld town)
    {
        var cars = town.Cars;
        var asked = _askedThisLaying.AsSpan(0, _carCount);
        asked.Clear();
        foreach (var car in town.CarsAskedThisLaying)
        {
            asked[car] = true;

            // Kept as it stands, the next laying asks it nothing: laid again with that one.
            if (town.StandsAsLaidNext(car)) _carTick!.LayAgainWithTheClaims(car);
        }

        foreach (var car in LayingCars)
        {
            if (_laidOnTheDevice[car] && asked[car]) _carTick!.ClaimClearedOnTheDevice(car);
            _carTerms.Lay(car, TermsOf(town, cars, car, asked[car]));
        }
    }

    /// <summary>
    /// One car's terms (<see cref="LayTheCarsTerms"/>): of one the town lays, its channels alone — which the rebuild hands a car
    /// back on other channels than (<c>rebuild_cars_out.comp</c>).
    /// </summary>
    CarTermsRow TermsOf(TownWorld town, CarFleet cars, int car, bool asked)
    {
        if (!_laidOnTheDevice[car]) return new CarTermsRow(0u, cars.Channels[car], 0f, 0f);

        var pass = cars.Pass[car];
        var flags = CarTermsRow.Held | (cars.BlueLight[car] ? CarTermsRow.OnACall : 0u)
                    | (cars.LineIsReverse[car] ? CarTermsRow.LineIsReverse : 0u) | (pass.Begun ? CarTermsRow.PassBegun : 0u)
                    | (asked ? CarTermsRow.Asked : 0u);
        var drives = town.DrivesItsRoute(car);
        if (drives) flags |= CarTermsRow.DrivesTheRoute;

        // What it keeps off what it may pass is read off its lane, and only one under way on its line has one.
        var keptOffM = drives && cars.Line[car].LaneCount > 0 ? town.KeptOffWhatItMayPassM(car) : 0f;
        return new CarTermsRow(flags, cars.Channels[car], town.OffTheLineAllowanceM(car), keptOffM, pass.Begun ? pass.EndsM : 0f);
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
    /// <param name="mayBeOnAPass">
    /// Every car that may be on a begun pass: the town begins one, and reads the car every tick while it is
    /// (<see cref="TownWorld.ReadsTheTickOf"/>) — so the cars the town holds (<see cref="DeviceCarTick.TownHolds"/>).
    /// </param>
    public static bool HasRoomForThePasses(TownWorld world, ReadOnlySpan<int> mayBeOnAPass)
    {
        var cars = world.Cars;
        var onAPass = 0;
        foreach (var car in mayBeOnAPass)
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

    /// <summary>
    /// Of what the last rebuild in the town read back in its own submit, in bytes: the layer where it is not sent back later,
    /// the hold each unit's first was numbered, what each car's plan came to, what it made of the walkers, and the grants.
    /// </summary>
    public (ulong Layer, ulong UnitHolds, ulong Cars, ulong Walkers, ulong Grants) ReadParts { get; private set; }

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
                GrowAhead();
                _laidFrom = Stopwatch.GetTimestamp();
                _carCount = town.Cars.Count;
                _walkerCount = town.People.Count;
                _hostRowCount = _hostHoldCount = _askCount = 0;
                _hostUnitCount = PassesUnit + 1;

                // Every body reads its occupant's line and every plan its own car's, where the car tick keeps them; no
                // stage of a laying lays a line, so one made current here serves both.
                LayWhatTheDeviceHolds(town);
                _bodies.Lay(town, _laidOnTheDevice.AsSpan(0, _carCount), LayingCars);
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
                _plans.Lay(town, _laidOnTheDevice.AsSpan(0, _carCount), LayingCars);
                LayTheCarsTerms(town);
                if (_checksTheCars) ExpectThePlans(town);
                _timeS = town.ElapsedS;
                _plansDueSlots = claims.SlotCount;
                break;

            case ClaimsStage.DrivesDue:
                Watch(town, stage);
                break;

            case ClaimsStage.WalksDue:
                if (!_walkers.Keeps) _walkers.LayWhatChanged(town);
                LayTheCarsClaims(town);
                AskTheBacking(town, takes: true);
                if (_backingCount > 0) BackedUp++;
                break;

            case ClaimsStage.PlansLaid:
                RecordThroughTheAnswers(town);
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
        var commands = _sendBatch.Begin();
        _timer.Reset(commands);
        _timer.Stamp(commands, (int)Stage.Began);

        var townsOwn = Sent<ClaimRow>.BytesOf(_hostRowCount) + Sent<uint>.BytesOf(_hostRowCount) + Sent<HoldRow>.BytesOf(_hostHoldCount)
                       + Sent<uint>.BytesOf(_hostHoldCount) + Sent<HostUnit>.BytesOf(_hostUnitCount) + StandingSentBytes
                       + Sent<BodyAskRow>.BytesOf(_askCount) + Sent<BackingAskRow>.BytesOf(_backingCount);
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
        RecordTheStandingSent(commands);
        _carClaims.Send(commands);
        _asks.Send(commands, _askCount);
        _backingAsks.Send(commands, _backingCount);
        vk.Fill(commands, _wayCounts, 0);
        LandsLater = !MirrorsAtOnce;
        _answeredInPart = AnswersOnlyWhatTheTownHolds;
        if (_answeredInPart)
        {
            vk.Fill(commands, _carsMovedCount, 0);
            WantTheCarsTheTownHolds();
        }
        if (LandsLater) RecordTheWaysAskedSent(commands, town.ReadersOccupancy);

        _timer.Stamp(commands, (int)Stage.Sent);
        _sendBatch.Submit();

        // The sends in a submit of their own, so the host may write the mapped copies they read once it is done
        // (WaitForTheSends) while the rest runs on.
        _toldNoRoom = TellsNoRoomEvery > 0 && ++_rebuildsHere % TellsNoRoomEvery == 0;
        RecordTheChain(town, again: false);
        RegionsCopied = (int)(Runtime.Vk.RegionsCopied - regionsFrom);
        _sentBytes = sent;
        _submittedAt = laid;
        _answersInFlight = true;
        (_walkersLaid, _firstWalkerUnitLaid) = (_walkerCount, _stretches + 1 + _carCount);
        AnswersDue = _fitted && !_toldNoRoom && LandsLater && AnswersAfterTheTick && !town.HandsOn;
        if (AnswersDue) RanOn++;
        else TakeTheAnswers(town);
    }

    /// <summary>
    /// Whether a rebuild in the town has fitted its rooms yet, and they were grown ahead of it since: none runs on before —
    /// the first may pass a room begun too small, and only a rebuild waited for can be laid again.
    /// </summary>
    bool _fitted;

    /// <summary>
    /// <b>The rebuild after its sends, recorded and submitted</b>: every body, light, plan and walk laid, every plan answered
    /// and cut and the backing laid, what each agent is handed on, and everything it came to read back. <paramref name="again"/>, it is laid once
    /// more over what its sends left on the device — a rebuild that passed a room, which wrote nothing that outlives it but the
    /// kept walkers' rows, put back first as it read them in, and the counts its sends begin at nought, cleared again.
    /// </summary>
    void RecordTheChain(TownWorld town, bool again)
    {
        var vk = _vk;
        var keeps = _walkers.Keeps;
        var commands = _batch.Begin();
        vk.AfterTheSubmitsBefore(commands);
        if (again)
        {
            _timer.Reset(commands);
            _timer.Stamp(commands, (int)Stage.Began);
            vk.BeforeTransfer(commands);
            if (keeps) _walkers.RecordTheKeptAgain(commands);
            vk.Fill(commands, _wayCounts, 0);
            if (_answeredInPart) vk.Fill(commands, _carsMovedCount, 0);
            if (LandsLater) vk.Fill(commands, _waysRowCount, 0);
            vk.AfterTransfer(commands);
            _timer.Stamp(commands, (int)Stage.Sent);
        }

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
        RecordTheBacking(commands);

        _holdsOutKernel.DispatchIndirect(commands, _claims.OnTheDevice.Tally, Tally.HoldsGroups, default(NoPush));
        _agentsOutKernel.Dispatch(
            commands, Math.Max(_carCount, _walkerCount), new AgentsPush((uint)_carCount, (uint)_walkerCount, (uint)(_stretches + 1 + _carCount)));
        if (LandsLater) _waysOut.Dispatch(commands, _waysListed, new WaysPush((uint)_waysListed, (uint)_claims.RowRoom));
        _grant.Dispatch(commands, _carCount, new UnitsPush((uint)_carCount, (uint)(_stretches + 1)));
        if (keeps)
        {
            _walkerAnswers.Dispatch(commands, _walkerCount, new UnitsPush((uint)_walkerCount, (uint)(_stretches + 1 + _carCount)));
            vk.BetweenKernels(commands);
            _walkers.Record(commands, DeviceWalkers.Piece.Read);
            _walkers.WantTheWalkers(town, every: !AnswersOnlyWhatTheTownHolds);
            _walkers.RecordTheKeptOut(commands, _stretches + 1 + _carCount);
        }

        if (_answeredInPart)
        {
            vk.BetweenKernels(commands);
            _carsOutKernel.Dispatch(commands, _carCount, new CarsOutPush((uint)_carCount, (uint)(_stretches + 1), ++_answerStamp));
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

        if (_answeredInPart)
        {
            // Of the units' holds only the walkers' — each where the whole would put it — and none where the device keeps
            // the walkers, whose holds come with them; the cars' are their answers'.
            var firstWalker = Bytes<uint>(_stretches + 1 + _carCount);
            if (!keeps)
            {
                vk.Copy(
                    commands, _unitHolds, _unitHoldsOut, [new BufferCopy(firstWalker, firstWalker, Math.Max(sizeof(uint), Bytes<uint>(_walkerCount)))]);
            }

            vk.Copy(commands, _carsMovedCount, _carsMovedCountRead, sizeof(uint));
        }
        else
        {
            vk.Copy(commands, _unitHolds, _unitHoldsOut, Bytes<uint>(_units));
            vk.Copy(commands, _carsOut, _carsRead, Bytes<CarOut>(_carCount));
            vk.Copy(commands, _grants, _grantsOut, Bytes<DeviceCarTick.GrantRow>(_carCount));
        }

        if (_backingCount > 0) vk.Copy(commands, _reached, _reachedRead, Bytes<float>(_backingCount));
        if (keeps) _walkers.RecordReadTheKept(commands);
        else vk.Copy(commands, _walkersOut, _walkersRead, Bytes<WalkOut>(_walkerCount));
        _walkersReadBytes = keeps ? _walkers.KeptReadBytes : Bytes<WalkOut>(_walkerCount);
        if (_checksTheCars) RecordReadTheCarsLaid(commands);
        _timer.Stamp(commands, (int)Stage.Granted);
        _timer.Stamp(commands, (int)Stage.Read);
        _batch.Submit();
    }

    /// <summary>Whether any rebuild was laid here yet, for <see cref="RecordTheWalkersReaders"/> to copy the walkers' holds off.</summary>
    public bool LaidAny => _walkersLaid >= 0;

    /// <summary>
    /// <b>What a car's look for room reads of the layer the last rebuild laid</b> (<see cref="DeviceSwitchLook"/>): the atlas,
    /// every way's marks, the claims as that rebuild's lists left them, and the ways it sent back to the readers' layer —
    /// ascending, <see cref="LightsOnly"/> marking those sent for their lights' holds alone — of which
    /// <see cref="WaysSentBack"/> are its.
    /// </summary>
    internal (DeviceAtlas Atlas, (GpuBuffer At, GpuBuffer Sections) Marks, DeviceClaims.Tables Claims, GpuBuffer WaysSent) ForTheLook =>
        (_bodies.Atlas, _plans.Marks, _claims.OnTheDevice, _waysAsked.Device);

    /// <summary>How many ways the last rebuild laid here sent back to the readers' layer (<see cref="ForTheLook"/>).</summary>
    internal int WaysSentBack => _waysListed;

    /// <summary>
    /// <b>Each kept walker's hold as the town's readers read it, copied off the last rebuild laid here</b>
    /// (<c>walkers_readers.comp</c>) into <see cref="DeviceWalkers.ReadersHolds"/> — recorded in a tick's submit behind that
    /// rebuild (<see cref="RecordTheTickBehind"/>), where the readers have its claims. False, and nothing recorded, where no
    /// rebuild was laid here yet.
    /// </summary>
    public bool RecordTheWalkersReaders(CommandBuffer commands)
    {
        if (_walkersLaid < 0) return false;

        _walkersReaders.Dispatch(commands, _walkersLaid, new UnitsPush((uint)_walkersLaid, (uint)_firstWalkerUnitLaid));
        return true;
    }

    /// <summary>How many rebuilds in the town ran on while the town went on (<see cref="AnswersDue"/>).</summary>
    public long RanOn { get; private set; }

    /// <summary>
    /// <b>Whether a rebuild laid in the town may write its answers back after the tick's own submit</b> rather than before
    /// any agent is ticked (<see cref="AnswersDue"/>) — set where that tick is the device's, and written back in it
    /// (<c>DeviceTown</c>).
    /// </summary>
    public bool AnswersAfterTheTick { get; set; }

    /// <summary>
    /// <b>Whether the rebuild just laid in the town runs on while the town goes on</b> (<see cref="AnswersAfterTheTick"/>):
    /// its plans, holds and grants, and what it made of each walker, are written back once the tick's own submit is behind
    /// it (<see cref="TakeTheAnswers"/>), and a car the town ticks itself is ticked after them (<c>DeviceTown.Takes</c>) —
    /// never where a hand is on, whose walker the town walks in its turn on what the rebuild made of it.
    /// </summary>
    public bool AnswersDue { get; private set; }

    /// <summary>Whether the rebuild's own submit is in flight, its answers not yet written back.</summary>
    bool _answersInFlight;

    /// <summary>
    /// <b>The rebuild's sends waited for</b>: what they copied out of the mapped copies of the car tick's lines, readings and
    /// poses and the walkers' tables is on the device, and the host may write those copies again. Nothing where none is in
    /// flight.
    /// </summary>
    public void WaitForTheSends() => _sendBatch.Wait();

    ulong _sentBytes;
    ulong _walkersReadBytes;
    long _submittedAt;

    /// <summary>Of <see cref="WaitedMs"/>, the host's time with nothing to do but wait for the rebuild, in milliseconds.</summary>
    public double IdleMs { get; private set; }

    /// <summary>
    /// <b>What the rebuild in the town answered, written back</b> once its submit is waited for: the counts, the layer where
    /// it is not sent back later, and every agent (<see cref="MirrorTheAgents"/>). Nothing where nothing is in flight.
    /// </summary>
    public void TakeTheAnswers(TownWorld town)
    {
        if (!_answersInFlight) return;

        var ranOn = AnswersDue;
        var idle = Stopwatch.GetTimestamp();
        _batch.Wait();
        _sendBatch.Wait();
        _answersInFlight = false;
        AnswersDue = false;
        _timer.Read();
        LayAgainWhereItPassedARoom(town, ranOn);
        _laidLast = _claims.Needs;
        _fitted = true;
        var waited = Stopwatch.GetTimestamp();
        IdleMs = Stopwatch.GetElapsedTime(idle, waited).TotalMilliseconds;
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
        ReadParts = _answeredInPart
            ? (LandsLater ? 0ul : layer, _walkers.Keeps ? 0ul : Bytes<uint>(_walkerCount),
               Bytes<CarAnswer>(_carsAnswered) + Bytes<uint>(town.CarsAskedThisLaying.Length + _movedCount + 1), _walkersReadBytes, 0ul)
            : (LandsLater ? 0ul : layer, Bytes<uint>(_units), Bytes<CarOut>(_carCount), _walkersReadBytes,
               Bytes<DeviceCarTick.GrantRow>(_carCount));
        var (readLayer, unitHolds, carsRead, walkersRead, grantsRead) = ReadParts;
        Crossed = (_sentBytes, readLayer + unitHolds + carsRead + walkersRead + grantsRead);
        LaidMs = Stopwatch.GetElapsedTime(_laidFrom, _submittedAt).TotalMilliseconds;
        WaitedMs = Stopwatch.GetElapsedTime(_submittedAt, waited).TotalMilliseconds;
        MirroredMs = Stopwatch.GetElapsedTime(waited).TotalMilliseconds;
        Rebuilds++;
    }

    /// <summary>
    /// <b>A rebuild that passed a room laid again until one fits</b> (<c>claims_tally.glsl</c>): every room it passed grown
    /// to what it laid, kept at the headroom, and the rebuild recorded once more over what its sends left on the device, at
    /// the same instant of the town. A rebuild that ran on cannot be: the tick it ran beside read what it left, and a room
    /// passed there is a defect — rooms are grown ahead of every rebuild (<see cref="GrowAhead"/>).
    /// </summary>
    void LayAgainWhereItPassedARoom(TownWorld town, bool ranOn)
    {
        for (var laying = 1; _claims.Overflowed; laying++)
        {
            var (rows, pieces, proposed) = _claims.Needs;
            var passed = $"{rows} rows in {_claims.RowRoom}, {pieces} pieces a car in {_plans.MostPieces}, {proposed} rows a walk in {_walkers.ProposedRoom}";
            if (ranOn) throw new InvalidOperationException($"A rebuild that ran on while the town went on passed its room: {passed}.");
            if (laying > MostLayings) throw new InvalidOperationException($"A rebuild was laid {MostLayings} times and never fitted: {passed}.");

            GrowTheRooms(_claims.Needs);
            RebuildsLaidAgain++;
            _toldNoRoom = false;
            RecordTheChain(town, again: true);
            _batch.Wait();
            _timer.Read();
        }
    }

    /// <summary>How many times a rebuild is laid again before its rooms are taken to be wrong: one growth fits whatever it laid.</summary>
    const int MostLayings = 2;

    /// <summary>
    /// <b>For a check of laying a rebuild again</b>: every this many rebuilds in the town is told its rows have no room, so it
    /// passes it and is laid again at the same instant with the room it has (<see cref="LayAgainWhereItPassedARoom"/>) —
    /// nought, as shipped, for never. None of them runs on.
    /// </summary>
    internal int TellsNoRoomEvery { get; set; }

    /// <summary>Whether the rebuild being recorded is told its rows have no room (<see cref="TellsNoRoomEvery"/>).</summary>
    bool _toldNoRoom;

    long _rebuildsHere;

    /// <summary>How many rebuilds in the town passed a room and were laid again (<see cref="LayAgainWhereItPassedARoom"/>).</summary>
    public long RebuildsLaidAgain { get; private set; }

    /// <summary>How many times the rooms grew, ahead of a rebuild or for one laid again.</summary>
    public long RoomsGrown { get; private set; }

    /// <summary>What the last rebuild that fitted laid: its rows, and the most a car's plan and a walk did.</summary>
    (int Rows, int Pieces, int Proposed) _laidLast;

    /// <summary>
    /// <b>Every room grown ahead of a rebuild where the last one laid more than its share of it</b> (<see cref="Headroom"/>) —
    /// with nothing in flight and no layer waiting to land, which is the head of a laying.
    /// </summary>
    void GrowAhead() => GrowTheRooms(_laidLast);

    /// <summary>
    /// Every room too small for <paramref name="laid"/> at the headroom grown to it, no further than what could ever be laid —
    /// the device waited for first, since a room grown is a buffer every kernel over it is pointed at again.
    /// </summary>
    void GrowTheRooms((int Rows, int Pieces, int Proposed) laid)
    {
        var rows = Math.Min(_mostRows, RoomFor(laid.Rows));
        var pieces = Math.Min(_mostPieces, RoomFor(laid.Pieces));
        var proposed = Math.Min(_mostProposed, RoomFor(laid.Proposed));
        if (rows <= _claims.RowRoom && pieces <= _plans.MostPieces && proposed <= _walkers.ProposedRoom) return;

        _vk.WaitIdle();
        RoomsGrown++;
        _plans.GrowPieces(pieces);
        _walkers.GrowProposed(proposed);
        if (rows <= _claims.RowRoom) return;

        _claims.GrowRows(rows);
        _waysRows.Grow(Bytes<ClaimRow>(rows));
        _waysRowsRead.Grow(Bytes<ClaimRow>(rows));
        NumberTheSlots();
    }

    /// <summary>The most rows, a car's pieces and a walk's rows any rebuild could lay: what a room is never grown past.</summary>
    int _mostRows, _mostPieces, _mostProposed;

    /// <summary>Every slot the rows have room for named its own number, for a list mirrored straight off it (<see cref="MirrorTheWaysAsked"/>).</summary>
    void NumberTheSlots()
    {
        var numbered = _slotNumbers.Length;
        if (numbered >= _claims.RowRoom) return;

        Array.Resize(ref _slotNumbers, _claims.RowRoom);
        for (var slot = numbered; slot < _slotNumbers.Length; slot++) _slotNumbers[slot] = (uint)slot;
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
        (Math.Min(_claims.RowRoom, rows + (rows / ReadPastShare) + ReadPastSlack),
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
        var firstCar = _stretches + 1;
        HeldInTheirPasses = 0;
        if (_answeredInPart) HoldTheCarsTheTownHolds(town);
        else HoldEveryCar(town, unitHolds, firstCar);

        var firstWalker = firstCar + _carCount;
        if (_walkers.Keeps)
        {
            _walkers.TakeTheKept(town, _walkerTick!);
            _walkersReadBytes = _walkers.KeptReadBytes;
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

        if (_answeredInPart) GroundTheCarsTheTownHolds(town);
        else GroundEveryCar(town);

        var reached = _reachedRead.Span<float>();
        for (var at = 0; at < _backingCount; at++) town.BackedUpOnTheDevice(_backingAsked[at], reached[at]);

        town.AnsweredOnTheDevice(LaidAgain(_claims.Moved, _config.Road.MostSettlingPasses, out var settled), settled);
    }

    /// <summary><see cref="MirrorTheAgents"/> where every car is handed back: each asked car's plan and hold.</summary>
    void HoldEveryCar(TownWorld town, ReadOnlySpan<uint> unitHolds, int firstCar)
    {
        var carsOut = _carsRead.Span<CarOut>();
        foreach (var car in town.CarsAskedThisLaying)
        {
            ref readonly var claim = ref _carClaims.LaidRow(car);
            ref readonly var said = ref carsOut[car];
            if ((claim.Flags & CarClaimRow.PlansOnTheDevice) != 0)
            {
                town.PlannedOnTheDevice(car, said.ClaimFromM, said.ClaimToM, said.CommittedToM, said.HorizonM);
                var inItsPass = claim.AskCount > 0 && (said.Bits & CarOut.HeldInItsPass) != 0;
                if (inItsPass) HeldInTheirPasses++;
                var laid = inItsPass || (said.Bits & CarOut.LaidAHold) != 0;
                town.HeldOnTheDevice(car, laid ? (int)unitHolds[firstCar + car] : LaneOccupancy.NoHold, inItsPass);
            }
            else if ((claim.Flags & CarClaimRow.Stands) != 0)
            {
                var stands = _standingSaid.Span<int>()[_standingOf[car]];
                town.HeldOnTheDevice(car, stands >= 0 ? (int)unitHolds[firstCar - 1] + stands : LaneOccupancy.NoHold, inItsPass: false);
            }
            else
            {
                town.HeldOnTheDevice(car, LaneOccupancy.NoHold, inItsPass: false);
            }
        }
    }

    /// <summary>
    /// <see cref="MirrorTheAgents"/> where only the cars the town holds are handed back (<see cref="AnswersOnlyWhatTheTownHolds"/>):
    /// every asked car's hold, and the plan of each the town holds — every other car's plan and grant left as its laying
    /// cleared them, and handed back with its reading.
    /// </summary>
    void HoldTheCarsTheTownHolds(TownWorld town)
    {
        var holds = _carHolds.Span<uint>();
        foreach (var car in town.CarsAskedThisLaying)
        {
            if ((_carClaims.LaidRow(car).Flags & CarClaimRow.PlansOnTheDevice) != 0 && HandedBack(car, out var said))
            {
                town.PlannedOnTheDevice(car, said.ClaimFromM, said.ClaimToM, said.CommittedToM, said.HorizonM);
            }

            var word = holds[car];
            var inItsPass = (word & HoldInItsPass) != 0;
            if (inItsPass) HeldInTheirPasses++;
            town.HeldOnTheDevice(car, word == HoldNone ? LaneOccupancy.NoHold : (int)(word & ~HoldInItsPass), inItsPass);
        }
    }

    /// <summary>The channels and wheels of every car, as the last rebuild in the town found them under its body.</summary>
    void GroundEveryCar(TownWorld town)
    {
        var carsOut = _carsRead.Span<CarOut>();
        for (var car = 0; car < _carCount; car++) Ground(town, car, carsOut[car].Bits);
    }

    /// <summary>
    /// The channels and wheels of every car the town holds, and of every car found on other channels than the town laid it
    /// on — in order, as <see cref="GroundEveryCar"/> takes them. Every other car's are what the town has of it, but which
    /// wheels stand on a way, handed back with its reading.
    /// </summary>
    void GroundTheCarsTheTownHolds(TownWorld town)
    {
        var moved = _carsMovedBits.AsSpan(0, _wantedWords.Length);
        moved.Clear();
        _movedCount = (int)Math.Min(_carsMovedCountRead.Span<uint>()[0], (uint)_carCount);
        foreach (var car in _carsMoved.Span<uint>()[.._movedCount]) moved[(int)(car >> 5)] |= 1u << (int)(car & 31);

        _carsAnswered = 0;
        var answers = _carAnswers.Span<CarAnswer>();
        for (var word = 0; word < moved.Length; word++)
        {
            var bits = _wantedWords[word] | moved[word];
            while (bits != 0)
            {
                var car = (word * 32) + System.Numerics.BitOperations.TrailingZeroCount(bits);
                bits &= bits - 1;
                ExpectTheStamp(car, answers[car]);
                Ground(town, car, answers[car].Bits);
                _carsAnswered++;
            }
        }
    }

    void Ground(TownWorld town, int car, uint bits)
    {
        var channels = (byte)(bits >> CarOut.ChannelsShift);
        if (channels != town.Cars.Channels[car] && CarActions.IsAtABay(town.Cars.Action[car])) OnOtherChannelsAtABay++;

        var wheels = (byte)(bits >> CarOut.WheelsShift);
        if (channels != town.Cars.Channels[car]) _carTick!.LayAgainWithTheClaims(car);
        town.OnTheGroundOfItsBody(car, channels, wheels);
        _carTick!.KnowTheWheelsOnAWay(car, wheels);
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
    /// <b>The tick after a rebuild laid in the town, recorded behind it</b>: every write of the rebuild's own submit — still
    /// running where it runs on (<see cref="AnswersDue"/>) — made visible to whatever the tick's head records next.
    /// </summary>
    public void RecordTheTickBehind(CommandBuffer commands)
    {
        Pending = false;
        _vk.AfterTheSubmitsBefore(commands);
    }

    /// <summary>
    /// <b>Where the walkers a rebuild laid in the town changed are aimed, and whether they walk</b>, written into the walker
    /// tick's table in the tick after it — after the town's walkers are written whole over that table, and before it ticks
    /// (<see cref="DeviceWalkers.RecordTheKeptAims"/>). Nothing where the device keeps no walker. The caller barriers.
    /// </summary>
    public void RecordTheKeptAims(CommandBuffer commands)
    {
        if (_walkers.Keeps) _walkers.RecordTheKeptAims(commands);
    }

    /// <summary>What the last rebuild in the town said of every walker, as its own submit read it back.</summary>
    public ReadOnlySpan<WalkOut> WalksRead => _walkersRead.Span<WalkOut>()[.._walkerCount];

    /// <summary>
    /// <b>Whether the rebuild just laid in the town writes its layer back later</b> than its own stages (owner, root decision
    /// log 2026-10-07), <b>and in part</b> (2026-10-08): only its counts and what it says of each agent come back with it;
    /// then, behind the tick's own submit, of the rows only the lists of the ways the readers asked about lately
    /// (<see cref="LaneOccupancy.Knows(int)"/>) and the lights' holds on every signalled way — and of the holds only those
    /// the town asks for as it reads them (<see cref="HoldsAsAsked"/>), or every one
    /// (<see cref="SendTheLayerBack"/>) — written into the readers' layer on the tick after, while that tick's submit runs
    /// (<see cref="LandWhatWasSent"/>), or before any rebuild that comes sooner (<see cref="Land"/>). Never where the layer is
    /// mirrored at once (<see cref="MirrorsAtOnce"/>).
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
    /// running: the rows of the ways asked for as far as the last landing's count and a margin, and every hold, what cut it
    /// and each car's hold number — copied on the device as the readers' layer will hold them, and back only where every hold lands
    /// (<see cref="HoldsAsAsked"/>). Nothing else on the device writes them before the next rebuild. Nothing where no layer
    /// waits to be sent.
    /// </summary>
    public void SendTheLayerBack()
    {
        if (_landing != Landing.Due) return;

        var holds = _claims.Holds.Length;
        _waysRowsBound = Math.Min(_claims.RowRoom, _waysRowsBound);
        var commands = _batch.Begin();
        _vk.BeforeTransfer(commands);
        _vk.Copy(commands, _waysRowCount, _waysRowCountRead, sizeof(uint));
        _claims.CopyPast<UInt4>(commands, _waysHeads, _waysHeadsRead, 0, _waysListed);
        _claims.CopyPast<ClaimRow>(commands, _waysRows, _waysRowsRead, 0, _waysRowsBound);
        _claims.CopyPast<HoldOut>(commands, _holdsOut, _readersHoldsOut, 0, holds);
        _claims.CopyPast<ClaimRow>(commands, _holdCutters, _readersHoldCutters, 0, holds);
        _claims.CopyPast<int>(commands, _holdOf, _readersHoldOf, 0, _cars);
        if (_walkers.Keeps) _claims.CopyPast<int>(commands, _walkers.KeptHolds, _readersWalkHoldOf, 0, _walkersRoom);
        LandedBytes =sizeof(uint) + Bytes<UInt4>(_waysListed) + Bytes<ClaimRow>(_waysRowsBound);
        if (LandsEveryHold)
        {
            _claims.CopyPast<HoldOut>(commands, _holdsOut, _holdsRead, 0, holds);
            _claims.CopyPast<ClaimRow>(commands, _holdCutters, _holdCuttersRead, 0, holds);
            LandedBytes += Bytes<HoldOut>(holds) + Bytes<ClaimRow>(holds);
        }
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
            _claims.CopyPast<ClaimRow>(commands, _waysRows, _waysRowsRead, _waysRowsBound, Math.Min(count, _claims.RowRoom));
            _batch.SubmitAndWait();
        }

        _waysRowsBound = count + (count / ReadPastShare) + ReadPastSlack;
        return Math.Min(count, _claims.RowRoom);
    }

    /// <summary>
    /// <b>A layer sent back in part written into a layer of the host's</b> (<see cref="LaneOccupancy.KnowTheWay"/>): every
    /// way asked for's lists, each in the order the rebuild laid it, every other signalled way's planned list holding its
    /// lights' holds alone — and, where every hold lands (<see cref="HoldsAsAsked"/>), every hold with what it came to and
    /// what cut it, as the claim stood when it did; otherwise none, each asked for as it is read.
    /// </summary>
    void MirrorTheWaysAsked(LaneOccupancy occupancy, int count)
    {
        var rows = _waysRowsRead.Span<ClaimRow>()[..count];
        var heads = _waysHeadsRead.Span<UInt4>()[.._waysListed];
        var holdCount = _claims.Holds.Length;
        occupancy.BeginTheMirror(count, holdCount, everyWay: false, everyHold: LandsEveryHold);
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
        if (!LandsEveryHold) return;

        var holds = _holdsRead.Span<HoldOut>()[..holdCount];
        var cutters = _holdCuttersRead.Span<ClaimRow>()[..holdCount];
        for (var hold = 0; hold < holdCount; hold++) MirrorTheHold(occupancy, hold, holds[hold], cutters[hold]);
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
        var room = town.Occupancy.Capacity;
        var whole = _wholeLayer ??= new LaneOccupancy(town.Ways, room, _holdRoom, town.Atlas.Marks);
        if (_partList.Length == 0) (_partList, _wholeList) = (new LaneClaim[room], new LaneClaim[room]);

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
}
