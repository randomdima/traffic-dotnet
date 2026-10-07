using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.Tests.World;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Xunit;
using static TrafficSimulation.App.DeviceTick.DeviceRebuild;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The claims laid on the device in the town's place, mirrored back, are the town's own</b> (<see cref="DeviceTownClaims"/>):
/// the fixture town run twice in step, one laying its own claims and the other having the device lay them, and after every
/// rebuild each held to the other (<see cref="ClaimsLaidOnTheDevice"/>).
/// </summary>
/// <remarks>
/// <b>Every figure is compared to the bit</b>: a mirror a last bit off is a town that parts from its own a few ticks on.
/// </remarks>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceTownClaimsTests(ClaimsLaidOnTheDevice run) : IClassFixture<ClaimsLaidOnTheDevice>
{
    /// <summary>
    /// <b>Every slot is laid, cut and listed as the town's own rebuild lays it</b>: the same count, slot by slot the same
    /// claim on the same way taken out or not, and every way's bodies and planned pieces in the same order.
    /// </summary>
    [Fact]
    public void EverySlotIsLaidCutAndListedAsTheTownsOwn() => Assert.True(run.Slots.Kept, run.Slots.Said + run.Seen);

    /// <summary>
    /// <b>Every hold comes to what the town's came to</b>: over the same slots, with the same stand-off and terms, answered
    /// with the plans or laid where it stands, and ended at the same metre by the same claim on the same way.
    /// </summary>
    [Fact]
    public void EveryHoldComesToWhatTheTownsCameTo() => Assert.True(run.Holds.Kept, run.Holds.Said + run.Seen);

    /// <summary>
    /// <b>Every car holds and is granted what the town's car is</b>: its hold, how far its claim reaches and is committed,
    /// its horizon, what it was granted and kept off and what cut it, the room it backed up into, the channels it is on and
    /// which of its wheels its tyres grip on a way with.
    /// </summary>
    [Fact]
    public void EveryCarHoldsAndIsGrantedWhatTheTownsIs() => Assert.True(run.Cars.Kept, run.Cars.Said + run.Seen);

    /// <summary>
    /// <b>Every walker holds, is granted and aims as the town's walker does</b>: its hold, its grant and crossing, its
    /// action and pass, where it aims, and the straight it is committed to.
    /// </summary>
    [Fact]
    public void EveryWalkerHoldsAndAimsAsTheTownsDoes() => Assert.True(run.Walkers.Kept, run.Walkers.Said + run.Seen);

    /// <summary>
    /// <b>Every asked car's grant is written where its tick reads it as the town reads its own</b> — once the backing the
    /// town lays after the answers is filed on the device too: the grant, what cut it and its hold bits.
    /// </summary>
    [Fact]
    public void EveryAskedCarsGrantIsWrittenWhereItsTickReadsIt() => Assert.True(run.Grants.Kept, run.Grants.Said + run.Seen);

    /// <summary><b>The town drives as it does on its own claims</b>: every car and walker where the other town has it, tick for tick.</summary>
    [Fact]
    public void TheTownDrivesAsItDoesOnItsOwnClaims() => Assert.True(run.Poses.Kept, run.Poses.Said + run.Seen);
}

/// <summary>
/// <b>Two towns each run twice in step, once with the device laying its claims</b>, each claims rebuild held to the other
/// town's and what differed kept a kind at a time.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fixture, staged so a car backs up and drives a pass</b>: a car stood too near a wreck to step out round it, the
/// lane beside it then cleared, so it backs up into the plan of whoever queues behind it and drives a pass the device
/// looks for a body inside (CAR-50, <see cref="OvertakeInATownTests.StandTooNearAWreck"/>) — staged alike in both towns
/// before the device takes the claims.
/// </para>
/// <para>
/// <b>And the walked town once its walkers are out of their doors</b>: walkers stationed, laid, walking and crossing, owners
/// walking to their cars, and cars manoeuvring at bays — whose begun manoeuvres the town lays itself, so the claims pass
/// between the device and the town as the run goes.
/// </para>
/// </remarks>
public sealed class ClaimsLaidOnTheDevice : IDisposable
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>Long enough for every car the fixture stands to have a line.</summary>
    const int WarmUpTicks = 120;

    /// <summary>How long the walked town is left: its walkers out of their doors.</summary>
    static int WalkersOutTicks => (int)(Config.Building.DwellMaxS * Config.Sim.TickRateHz) + 120;

    /// <summary>Ten seconds of each town: two hundred rebuilds.</summary>
    const int WatchedTicks = 600;

    readonly Lavapipe _device = new();

    internal Held Slots { get; } = new("slots");

    internal Held Holds { get; } = new("holds");

    internal Held Cars { get; } = new("cars' claims");

    internal Held Walkers { get; } = new("walkers' walks");

    internal Held Grants { get; } = new("cars' grants on the device");

    internal Held Poses { get; } = new("ticks' poses");

    /// <summary>What the run asked of, for a message to say how much it covered.</summary>
    internal string Seen { get; }

    public ClaimsLaidOnTheDevice()
    {
        var backing = Watch(Towns.Of(Towns.Fixture), (town, loop) => StandTooNearAWreck(town, loop));
        var held = Watch(
            Towns.Of(Towns.Fixture), (town, loop) =>
            {
                var (driver, beside) = StandTooNearAWreck(town, loop);
                var cars = town.Cars;
                for (var tick = 0; tick < WatchedTicks && !cars.Pass[driver].Begun; tick++) loop.Advance(1);

                // Put down once the pass round the wreck is begun, alongside what it passes: a body inside what is left of it.
                var pass = cars.Pass[driver];
                pass.PoseAtM(cars.LineOf(driver), (pass.OutM + pass.BackM) * 0.5f, out var alongsideM, out var forward);
                OvertakeInATownTests.PutDown(town, beside, alongsideM, MathF.Atan2(forward.Y, forward.X));
            });
        var walked = Watch(Towns.Built, (_, loop) => loop.Advance(WalkersOutTicks));
        Seen = $" (the fixture backing up: {backing}; the fixture held in its pass: {held}; the walked town: {walked})";
    }

    /// <summary>
    /// A car stood too near a wreck to step out round it and the lane beside it cleared, so it backs up for the room and
    /// begins its pass (CAR-50): the car, and the one that stood beside it.
    /// </summary>
    static (int Driver, int Beside) StandTooNearAWreck(TownWorld town, SimLoop<TownWorld> loop)
    {
        loop.Advance(WarmUpTicks);
        var (driver, _, _, beside) = OvertakeInATownTests.StandTooNearAWreck(town, loop);
        OvertakeInATownTests.PutDown(town, beside, OvertakeInATownTests.FarOffTheTownM, 0f);
        return (driver, beside);
    }

    /// <summary>One town staged alike twice, then run in step with the device laying one's claims, held to the other after every rebuild.</summary>
    string Watch(CityPlan plan, Action<TownWorld, SimLoop<TownWorld>> stage)
    {
        using var own = new TownWorld(plan, Config);
        using var mirrored = new TownWorld(plan, Config);
        var ownLoop = new SimLoop<TownWorld>(own, Config);
        var mirroredLoop = new SimLoop<TownWorld>(mirrored, Config);
        stage(own, ownLoop);
        stage(mirrored, mirroredLoop);

        using var onDevice = new OnTheDevice(_device.Vk, mirrored, Grants);
        mirrored.ClaimsDevice = onDevice;
        long rebuilds = 0;
        for (var tick = 0; tick < WatchedTicks; tick++)
        {
            ownLoop.Advance(1);
            mirroredLoop.Advance(1);
            HoldThePoses(own, mirrored, tick);
            if (!own.ClaimsLaidThisTick) continue;

            rebuilds++;
            HoldTheSlots(own.Occupancy, mirrored.Occupancy, tick);
            HoldTheHolds(own.Occupancy, mirrored.Occupancy, tick);
            HoldTheCars(own, mirrored, tick);
            HoldTheWalkers(own, mirrored, tick);
        }

        mirrored.ClaimsDevice = null;
        var (here, byTheTown, _, _) = onDevice.Claims.Rebuilds;
        return $"{rebuilds} rebuilds of {own.Cars.Count} cars and {own.People.Count} walkers, {here} laid on the device and {byTheTown} "
               + $"by the town, {onDevice.HeldInTheirPasses} cars held short of a body inside their pass, {onDevice.BackedUp} cars backed up";
    }

    void HoldThePoses(TownWorld own, TownWorld mirrored, int tick)
    {
        Poses.Asked++;
        for (var car = 0; car < own.Cars.Count; car++)
        {
            var (a, b) = (own.Cars, mirrored.Cars);
            if (a.PositionM[car] == b.PositionM[car] && a.VelocityMps[car] == b.VelocityMps[car] && a.HeadingRad[car] == b.HeadingRad[car])
            {
                continue;
            }

            Poses.Add($"tick {tick} car {car}: own at {a.PositionM[car]} moving {a.VelocityMps[car]}; mirrored at {b.PositionM[car]} moving {b.VelocityMps[car]}");
            return;
        }

        for (var person = 0; person < own.People.Count; person++)
        {
            var (a, b) = (own.People, mirrored.People);
            if (a.PositionM[person] == b.PositionM[person] && a.VelocityMps[person] == b.VelocityMps[person]) continue;

            Poses.Add($"tick {tick} walker {person}: own at {a.PositionM[person]}; mirrored at {b.PositionM[person]}");
            return;
        }
    }

    void HoldTheSlots(LaneOccupancy own, LaneOccupancy mirrored, int tick)
    {
        Slots.Asked++;
        if (own.SlotCount != mirrored.SlotCount)
        {
            Slots.Add($"tick {tick}: own {own.SlotCount} slots, mirrored {mirrored.SlotCount}");
            return;
        }

        for (var slot = 0; slot < own.SlotCount; slot++)
        {
            Slots.Asked++;
            var (a, b) = (DeviceClaims.ClaimRow.OfSlot(own, slot), DeviceClaims.ClaimRow.OfSlot(mirrored, slot));
            if (a != b) Slots.Add($"tick {tick} slot {slot}: own {a}; mirrored {b}");
        }

        if (!own.OccupiedWays.SequenceEqual(mirrored.OccupiedWays)) Slots.Add($"tick {tick}: the ways laid on differ");

        Span<int> ownList = stackalloc int[256];
        Span<int> mirroredList = stackalloc int[256];
        foreach (var way in own.OccupiedWays)
        {
            foreach (var bodies in (ReadOnlySpan<bool>)[true, false])
            {
                var a = ownList[..own.ListOf(way, bodies, ownList)];
                var b = mirroredList[..mirrored.ListOf(way, bodies, mirroredList)];
                if (!a.SequenceEqual(b)) Slots.Add($"tick {tick} way {way}'s {(bodies ? "bodies" : "plans")}: own [{string.Join(", ", a.ToArray())}]; mirrored [{string.Join(", ", b.ToArray())}]");
            }
        }
    }

    void HoldTheHolds(LaneOccupancy own, LaneOccupancy mirrored, int tick)
    {
        Holds.Asked++;
        if (own.HoldCount != mirrored.HoldCount)
        {
            Holds.Add($"tick {tick}: own {own.HoldCount} holds, mirrored {mirrored.HoldCount}");
            return;
        }

        for (var hold = 0; hold < own.HoldCount; hold++)
        {
            Holds.Asked++;
            var a = HoldOf(own, hold);
            var b = HoldOf(mirrored, hold);
            if (!a.Equals(b)) Holds.Add($"tick {tick} hold {hold}: own {a}; mirrored {b}");
        }

        static ((int, int), float, bool, HolderTerms, float, float, LaneClaim, int) HoldOf(LaneOccupancy claims, int hold)
        {
            var endsAtM = claims.HoldEndsAtM(hold, out var marginM, out var cutBy);
            return (claims.PiecesOf(hold), claims.StandingMarginM(hold), claims.IsAnswered(hold), claims.TermsOf(hold), endsAtM, marginM, cutBy,
                    claims.HoldCutOn(hold));
        }
    }

    void HoldTheCars(TownWorld own, TownWorld mirrored, int tick)
    {
        for (var car = 0; car < own.Cars.Count; car++)
        {
            Cars.Asked++;
            var a = ClaimOf(own, car);
            var b = ClaimOf(mirrored, car);
            if (!a.Equals(b)) Cars.Add($"tick {tick} car {car}: own {a}; mirrored {b}");
        }

        static (int, float, float, float, float, float, float, HeadwayKind, float, float, byte, byte) ClaimOf(TownWorld town, int car)
        {
            var cars = town.Cars;
            return (town.PlanHoldOf(car), cars.AuthorityM[car], cars.HorizonM[car], cars.ClaimFromM[car], cars.ClaimToM[car], cars.CommittedToM[car],
                    cars.GrantMarginM[car], cars.GrantCutBy[car], cars.CoveredSinceClaimM[car], cars.BackRoomM[car], cars.Channels[car],
                    cars.WheelsOnAWay[car]);
        }
    }

    void HoldTheWalkers(TownWorld own, TownWorld mirrored, int tick)
    {
        for (var person = 0; person < own.People.Count; person++)
        {
            Walkers.Asked++;
            var a = WalkOf(own, person);
            var b = WalkOf(mirrored, person);
            if (!a.Equals(b)) Walkers.Add($"tick {tick} walker {person}: own {a}; mirrored {b}");
        }

        static (int, float, int, PersonAction, Sidestep, float, float, float, float, float, float) WalkOf(
            TownWorld town, int person)
        {
            var people = town.People;
            var ground = town.WalkingGround;
            return (town.WalkHold(person), people.GrantM[person], people.OnCrossing[person], people.Action[person], people.Pass[person],
                    people.DestinationM[person].X, people.DestinationM[person].Y, ground.StraightFromM(person).X, ground.StraightFromM(person).Y,
                    ground.StraightToM(person).X, ground.StraightToM(person).Y);
        }
    }

    public void Dispose() => _device.Dispose();

    /// <summary>
    /// <b>A town's claims laid on the device and nothing else</b> (<see cref="DeviceTownClaims"/>): the car tick's tables they
    /// are written into, which never ticks — and, the moment a rebuild laid here has every grant read, the rest of it
    /// recorded as the tick would record it and every asked car's grant read back and held to the town's.
    /// </summary>
    sealed class OnTheDevice : ITownClaims, IDisposable
    {
        readonly Vk _vk;
        readonly ClaimsRig _rig;
        readonly ComputeBatch _batch;
        readonly GpuBuffer _grants, _cutters, _bits;
        readonly Held _held;
        int _rebuild;

        public OnTheDevice(Vk vk, TownWorld town, Held grants)
        {
            _vk = vk;
            _held = grants;
            _rig = new ClaimsRig(vk, town);
            _batch = new ComputeBatch(vk);
            _grants = Readable<DeviceCarTick.GrantRow>(town.Cars.Capacity);
            _cutters = Readable<DeviceClaims.ClaimRow>(town.Cars.Capacity);
            _bits = Readable<uint>(town.Cars.Capacity);
        }

        public DeviceTownClaims Claims => _rig.Claims;

        /// <summary>How many cars the rebuilds laid here found a body inside their pass, and how many cars backed up.</summary>
        public long HeldInTheirPasses { get; private set; }

        /// <inheritdoc cref="HeldInTheirPasses"/>
        public long BackedUp { get; private set; }

        public bool LaysTheClaims(TownWorld town) => Claims.LaysTheClaims(town);

        /// <summary>The town reads every grant off its own claims, so each the device read is held to it.</summary>
        public bool ReadTheGrants => false;

        public void ClaimsAt(TownWorld town, ClaimsStage stage)
        {
            Claims.ClaimsAt(town, stage);
            if (stage != ClaimsStage.GrantsRead) return;

            _rebuild++;
            HeldInTheirPasses += Claims.Rebuild.HeldInTheirPasses;
            foreach (var car in town.CarsAskedThisLaying) BackedUp += float.IsNaN(town.Cars.BackRoomM[car]) ? 0 : 1;

            HoldTheGrants(town, _held, _rebuild);
        }

        /// <summary>
        /// The rest of the last rebuild recorded as the tick would record it, every car's grant, cutter and hold bits read
        /// back, and every asked car's held to what the town reads of its own claims — the place it was sent aside, which is
        /// a decision's.
        /// </summary>
        void HoldTheGrants(TownWorld town, Held grants, int rebuild)
        {
            var (onGrants, onCutters, onBits) = _rig.Cars.GrantTables;
            var commands = _batch.Begin();
            Claims.RecordTheRest(commands);
            _vk.Copy(commands, onGrants, _grants, _grants.SizeBytes);
            _vk.Copy(commands, onCutters, _cutters, _cutters.SizeBytes);
            _vk.Copy(commands, onBits, _bits, _bits.SizeBytes);
            _batch.SubmitAndWait();

            foreach (var car in town.CarsAskedThisLaying)
            {
                grants.Asked++;
                var host = TheTownsGrant(town, car, out var hostCutter, out var hostBits);
                var device = _grants.Span<DeviceCarTick.GrantRow>()[car] with { PlaceReachM = host.PlaceReachM, PlaceM = host.PlaceM };
                var cutter = _cutters.Span<DeviceClaims.ClaimRow>()[car];
                var bits = _bits.Span<uint>()[car];
                if (device != host || cutter != hostCutter || bits != hostBits)
                {
                    grants.Add($"rebuild {rebuild} car {car}: town {host} {hostCutter} bits {hostBits}; device {device} {cutter} bits {bits}");
                }
            }
        }

        GpuBuffer Readable<T>(int count) where T : unmanaged =>
            _vk.CreateBuffer(
                (ulong)(Math.Max(1, count) * System.Runtime.InteropServices.Marshal.SizeOf<T>()), Silk.NET.Vulkan.BufferUsageFlags.TransferDstBit,
                HostAccess.Reads);

        public void Dispose()
        {
            _grants.Dispose();
            _cutters.Dispose();
            _bits.Dispose();
            _batch.Dispose();
            _rig.Dispose();
        }
    }
}

/// <summary>
/// <b>Laying a town's claims on the device allocates nothing</b> once the town has settled into its size (rule 2): every
/// rebuild the device lays, the town's own stages of it included, from the first stage to every grant read.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceTownClaimsAllocationTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>How long the walked town is left: its walkers out of their doors.</summary>
    static int WalkersOutTicks => (int)(Config.Building.DwellMaxS * Config.Sim.TickRateHz) + 120;

    /// <summary>Long enough for every table the rebuild grows to have grown: a hundred rebuilds.</summary>
    const int SettleTicks = 300;

    /// <summary>Two hundred rebuilds counted.</summary>
    const int CountedTicks = 600;

    [Fact]
    public void LayingTheClaimsOnTheDeviceAllocatesNothing()
    {
        using var town = new TownWorld(Towns.Built, Config);
        var loop = new SimLoop<TownWorld>(town, Config);
        loop.Advance(WalkersOutTicks);
        using var rig = new ClaimsRig(device.Vk, town);
        var counted = new Counted(rig.Claims);
        town.ClaimsDevice = counted;
        loop.Advance(SettleTicks);

        counted.AllocatedBytes = 0;
        counted.Laid = 0;
        loop.Advance(CountedTicks);
        town.ClaimsDevice = null;

        Assert.True(counted.Laid > 0, "the device laid none of the town's claims");
        Assert.Equal(0L, counted.AllocatedBytes);
    }

    /// <summary>The claims laid on the device, with what each rebuild laid there allocated on this thread counted — and the rest of it recorded at once.</summary>
    sealed class Counted(DeviceTownClaims claims) : ITownClaims
    {
        public long AllocatedBytes;

        public long Laid;

        long _before;

        public bool LaysTheClaims(TownWorld town) => claims.LaysTheClaims(town);

        public bool ReadTheGrants => claims.ReadTheGrants;

        public void ClaimsAt(TownWorld town, ClaimsStage stage)
        {
            if (stage == ClaimsStage.Begun) _before = GC.GetAllocatedBytesForCurrentThread();

            claims.ClaimsAt(town, stage);
            if (stage != ClaimsStage.GrantsRead) return;

            AllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - _before;
            Laid++;
        }
    }
}

/// <summary>
/// <b>A town's claims laid on the device and nothing else</b> (<see cref="DeviceTownClaims"/>): the claims and the car tick
/// they are laid into — which never ticks — on their own.
/// </summary>
internal sealed class ClaimsRig : IDisposable
{
    static readonly SimConfig Config = SimConfig.Shipped();

    readonly DeviceTyres _tyres;
    readonly DeviceClaims _claims;

    public ClaimsRig(Vk vk, TownWorld town)
    {
        var cars = town.Cars;
        var occupancy = town.Occupancy;
        var builds = new CarBuild[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = cars.BuildOfVariant(variant);

        _tyres = new DeviceTyres(vk, Config, town.WheelGrounds, builds, cars.Capacity);
        _claims = new DeviceClaims(vk, occupancy.WayCount, occupancy.Capacity, occupancy.HoldCapacity, Config.Road.MostSettlingPasses, cars.Capacity);
        Cars = new DeviceCarTick(vk, Config, _tyres, town.Roads, cars.LineArcsOf(0).Length, town.Ways.OfRoadConnector(0), _claims.Cut);
        Claims = new DeviceTownClaims(vk, Config, town, _claims, Cars);
    }

    public DeviceCarTick Cars { get; }

    public DeviceTownClaims Claims { get; }

    public void Dispose()
    {
        Claims.Dispose();
        Cars.Dispose();
        _claims.Dispose();
        _tyres.Dispose();
    }
}
