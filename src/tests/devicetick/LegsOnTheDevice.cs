using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using Network = TrafficSimulation.App.DeviceTick.DeviceRoutes.Network;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>A town laid on the device for the leg's probes</b> (<c>town_legs_check.comp</c>): its state (<see cref="TownState"/>),
/// its route service (<see cref="DeviceRoutes"/>, the town's surcharges and closures laid), the row tables a car's line and
/// tick are kept in, and — staged at the top of the root — a list of cars, and the town's answers to what the leg asks of
/// another port: an errand, a car's action, the way a car pulls into a bay. Laid off the town as it stands, so a probe
/// run here starts where the town's own call starts.
/// </summary>
internal sealed class LegsOnTheDevice : IDisposable
{
    /// <summary>The probes the kernel runs (<c>Probe…</c>).</summary>
    public const uint LayLine = 0, TryPlan = 1, TakesOnlyTheClock = 2, WatchTheProgress = 3, FreeBayNear = 4, Claims = 5, Nearest = 6;

    /// <summary>Words of a car's row before its line's (<c>RowHead</c>), and the word a probe's own answer is written at.</summary>
    public const int Head = 32, Answer = 29;

    // The ids the kernel names: its output, the list, the answers, each person's, the bays' stops and the town's own.
    const int OutId = TownRoot.Ids - 1;
    const int AnswersId = TownRoot.Ids - 2;
    const int PeopleAnswersId = TownRoot.Ids - 3;
    const int StopsId = TownRoot.Ids - 4;
    const int ListId = TownRoot.Ids - 5;
    const int GlobalsId = TownRoot.Ids - 6;

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Push(uint Probe, uint Count, uint OutId, uint Stride, float SinceLastDecisionS, uint FromTheQueue);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct TownCountsRow(
        uint CarCapacity, uint PeopleCapacity, uint Cars, uint People, uint ArcsPerCar, uint MostLanes, uint Wheels,
        uint RouteLanesPerCar, uint RouteWaysPerPerson, uint MostRunsOfAStraight, uint MostRunsOfAPass, uint FirstCarAgent);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct TickRow(float ProgressM, float CoveredSinceClaimM, float AlongMps, float OffLineM, float PlannedMps, uint Hold, uint Flags);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct GrantRow(
        float AuthorityM, float HorizonM, float ClaimFromM, float CommittedToM, float GrantMarginM, uint GrantCutBy, int CutOn,
        float PlaceReachM, Vector2 PlaceM);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct BoxRow(float ToTheBoxM, float LightAheadM, uint Flags, uint Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ContextRow(
        float AuthorityM, uint GrantCutBy, float PlaceStopM, float MarginM, uint WaitsToPass, float HorizonM, float PassAsideM,
        float BackUpM, uint Blocked, float WaitAtM, float Pad0, float Pad1);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct LineRow(uint ArcCount, uint LaneCount, float LengthM, uint FinalApproach);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct PoseRow(
        Vector2 PositionM, Vector2 VelocityMps, Vector2 Forward, Vector2 AccelerationMps2, float HeadingRad, float YawRateRadPerS,
        float MassKg, float Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CommandRow(float SteerRad, float ThrottleMps2, float BrakeMps2, uint HandbrakeAndReverse);

    /// <summary>What the town answers each car of what the leg asks of another port (<c>LegsAnswers</c>), field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct AnswersRow(
        uint AimedAtAPlace, int EntranceLane, float EntranceAlongM, uint UnderOrders, uint Evacuator, uint Patrol, uint WheelHeldOver,
        float OffTheLineAllowanceM, uint ClosedLinksFor, uint GetsPast, uint WantsToSwitch, uint Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct GlobalsRow(uint ClosedLinksNow, uint HoldAgents, int BayCount, uint Pad);

    readonly Vk _vk;
    readonly TownRoot _root;
    readonly TownState _state;
    readonly List<GpuBuffer> _owned = [];
    readonly GpuBuffer _out;
    readonly int _count;
    ComputeKernel? _kernel;

    public LegsOnTheDevice(Lavapipe device, TownWorld town, IReadOnlyList<(int Car, int Argument)> list)
    {
        _vk = device.Vk;
        _root = new TownRoot(_vk);
        _state = new TownState(_vk, _root, town);
        Routes = DeviceRoutes.Of(_vk, _root, town);
        Routes.LaySurcharges(Network.Driving, town.DriveSurcharges);
        Routes.LayClosed(Network.Driving, town.ClosedLinks);
        _count = list.Count;
        Stride = StrideOf(town);

        LayTheRows(device, town);
        LayTheAnswers(device, town);
        var listed = new int[Math.Max(1, list.Count) * 2];
        for (var row = 0; row < list.Count; row++) (listed[2 * row], listed[(2 * row) + 1]) = list[row];

        Set(ListId, device.Holding<int>(listed));
        _out = Set(OutId, device.Sized<uint>((list.Count + 1) * Stride));
    }

    /// <summary>
    /// The kernel a probe is run by: the leg inlined into one kernel for every probe is past the optimiser's room for ids,
    /// and the small probes share one so as not to wait on lavapipe compiling a large one.
    /// </summary>
    static string KernelOf(uint probe) => probe switch
    {
        LayLine => "town_legs_check.comp",
        TryPlan => "town_legs_check_plan.comp",
        WatchTheProgress => "town_legs_check_watch.comp",
        _ => "town_legs_check_bays.comp",
    };

    public DeviceRoutes Routes { get; }

    /// <summary>Words a car's row takes: its figures, its line's arcs and entries, its lanes and its route queue.</summary>
    public int Stride { get; }

    public static int StrideOf(TownWorld town) =>
        Head + (9 * town.Cars.LineArcsOf(0).Length) + (5 * LineAssembler.MostLanes) + CarFleet.RouteLanesPerCar;

    /// <summary>
    /// The probe run over the list, once — or twice with the route service's searches between, so an ask made on the first
    /// lands on the second — and each car's row read back, the leg's counts after them all.
    /// </summary>
    public uint[] Run(uint probe, float sinceLastDecisionS = 0f, bool fromTheQueue = false, bool landed = false)
    {
        var push = new Push(probe, (uint)_count, OutId, (uint)Stride, sinceLastDecisionS, fromTheQueue ? 1u : 0u);
        _kernel?.Dispose();
        var kernel = _kernel = new ComputeKernel(_vk, KernelOf(probe), 1, (uint)Marshal.SizeOf<Push>());
        var set = kernel.Bind([_root.Buffer]);
        _vk.OneShot(commands =>
        {
            kernel.Dispatch(commands, set, 1, push);
            if (!landed) return;

            _vk.BetweenKernels(commands);
            Routes.Record(commands);
            kernel.Dispatch(commands, set, 1, push);
        });
        return _out.Span<uint>()[..((_count + 1) * Stride)].ToArray();
    }

    /// <summary>
    /// <b>What the town holds of a car, word for word as the kernel writes it</b> (<c>putTheCar</c>): its line and where it
    /// stops, its route queue, its clock and its claims, then the line's arcs, entries and lanes and the queue's lanes.
    /// </summary>
    public static uint[] RowOf(TownWorld town, int car, int stride)
    {
        var cars = town.Cars;
        var row = new uint[stride];
        var line = cars.Line[car];
        var clock = town.DriveClocks.Of(car);
        row[0] = (uint)line.ArcCount;
        row[1] = (uint)line.LaneCount;
        row[2] = Bits(line.LengthM);
        row[3] = Flag(LineReader.EndsWhereTheLegDoes(cars, car));
        row[4] = (uint)cars.StopsForBay[car];
        row[5] = Flag(cars.StopsAtItsPlace[car]);
        row[6] = Flag(town.AsksTheRoute(car));
        row[7] = Flag(cars.LineIsReverse[car]);
        row[8] = Bits(cars.AuthorityM[car]);
        row[9] = (uint)cars.RouteTaken[car];
        row[10] = (uint)cars.RouteCount[car];
        row[11] = Flag(cars.RouteRunsOut[car]);
        row[12] = (uint)cars.RouteEndsOn[car];
        row[13] = Bits(cars.RouteEndsAtM[car]);
        row[14] = (uint)cars.TurnsBackOn[car];
        row[15] = Bits(cars.ProgressM[car]);
        row[17] = cars.LineLaid[car];
        row[18] = cars.RouteLaid[car];
        row[19] = (uint)town.Parking.TurnOf(car);
        row[20] = (uint)town.Parking.ClaimedBayOf(car);
        row[21] = Bits(clock.SinceS);
        row[22] = Bits(clock.ClosestM);
        row[23] = cars.Reroutes[car];
        row[24] = (uint)cars.Action[car];
        row[25] = Flag(cars.Driven[car]);
        row[26] = Flag(cars.HasDestination[car]);
        row[27] = Bits(cars.CoveredSinceClaimM[car]);
        row[28] = Bits(cars.HorizonM[car]);

        var arcs = cars.LineArcsOf(0).Length;
        var laid = cars.LineOf(car);
        var entries = cars.EntriesOf(car);
        for (var arc = 0; arc < laid.Length; arc++)
        {
            var words = ArcRow.Of(laid[arc]);
            var (a, b) = (words.A, words.B);
            ReadOnlySpan<float> each = [a.X, a.Y, a.Z, a.W, b.X, b.Y, b.Z, b.W];
            for (var word = 0; word < 8; word++) row[Head + (8 * arc) + word] = Bits(each[word]);

            row[Head + (8 * arcs) + arc] = Bits(entries[arc]);
        }

        var lanesAt = Head + (9 * arcs);
        for (var slot = 0; slot < line.LaneCount; slot++)
        {
            row[lanesAt + (5 * slot)] = (uint)cars.ChainOf(car)[slot];
            row[lanesAt + (5 * slot) + 1] = Bits(cars.LaneStartsOf(car)[slot]);
            row[lanesAt + (5 * slot) + 2] = Bits(cars.LaneEndsOf(car)[slot]);
            row[lanesAt + (5 * slot) + 3] = (uint)cars.LaneFirstArcsOf(car)[slot];
            row[lanesAt + (5 * slot) + 4] = Flag(cars.JoinBreaksOf(car)[slot]);
        }

        var routeAt = lanesAt + (5 * LineAssembler.MostLanes);
        for (var at = 0; at < cars.RouteCount[car]; at++) row[routeAt + at] = (uint)cars.RouteOf(car)[at];

        return row;
    }

    /// <summary>The first few words of the listed rows the device wrote unlike the town's, named, or none.</summary>
    public static List<string> Unlike(uint[] read, int stride, IReadOnlyList<int> rows, Func<int, uint[]> expected, int words = -1)
    {
        var unlike = new List<string>();
        foreach (var row in rows)
        {
            var wanted = expected(row);
            var compared = words < 0 ? stride : words;
            for (var word = 0; word < compared && unlike.Count < 8; word++)
            {
                var got = read[(row * stride) + word];
                if (got != wanted[word]) unlike.Add($"row {row} word {word}: device {got:X8}, town {wanted[word]:X8}");
            }
        }

        return unlike;
    }

    static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    static uint Flag(bool value) => value ? 1u : 0u;

    void LayTheRows(Lavapipe device, TownWorld town)
    {
        var cars = town.Cars;
        var capacity = cars.Capacity;
        var arcsPerCar = cars.LineArcsOf(0).Length;
        var lanes = LineAssembler.MostLanes;
        Set(1, device.Holding<TownCountsRow>([new TownCountsRow(
            (uint)capacity, (uint)town.People.Capacity, (uint)cars.Count, (uint)town.People.Count, (uint)arcsPerCar, (uint)lanes,
            TyreModel.Wheels, CarFleet.RouteLanesPerCar, PersonFleet.RouteWaysPerPerson, 0u, 0u, (uint)town.People.Count)]));

        var ticks = new TickRow[capacity];
        var grants = new GrantRow[capacity];
        var boxes = new BoxRow[capacity];
        var contexts = new ContextRow[capacity];
        var lines = new LineRow[capacity];
        var arcs = new ArcRow[capacity * arcsPerCar];
        var entries = new float[capacity * arcsPerCar];
        var chain = new int[capacity * lanes];
        var starts = new float[capacity * lanes];
        var ends = new float[capacity * lanes];
        var firstArcs = new uint[capacity * lanes];
        var breaks = new uint[capacity * lanes];
        var clocks = new Vector2[capacity];
        var poses = new PoseRow[capacity];
        var commands = new CommandRow[capacity];
        var buildOf = new uint[capacity];
        for (var car = 0; car < capacity; car++)
        {
            ticks[car] = new TickRow(
                cars.ProgressM[car], cars.CoveredSinceClaimM[car], cars.AlongMps[car], cars.OffLineM[car], cars.PlannedMps[car],
                (uint)cars.Hold[car], 0u);
            grants[car] = new GrantRow(
                cars.AuthorityM[car], cars.HorizonM[car], cars.ClaimFromM[car], cars.CommittedToM[car], cars.GrantMarginM[car],
                (uint)cars.GrantCutBy[car], -1, 0f, default);
            boxes[car] = new BoxRow(cars.ToTheBoxM[car], cars.LightAheadM[car], 0u, 0u);
            var told = cars.Context[car];
            contexts[car] = new ContextRow(
                told.AuthorityM, (uint)told.GrantCutBy, told.PlaceStopM, told.MarginM, Flag(told.WaitsToPass), told.HorizonM,
                told.PassAsideM, told.BackUpM, Flag(told.Blocked), told.WaitAtM, 0f, 0f);
            var line = cars.Line[car];
            lines[car] = new LineRow((uint)line.ArcCount, (uint)line.LaneCount, line.LengthM, Flag(LineReader.EndsWhereTheLegDoes(cars, car)));
            var carArcs = cars.LineArcsOf(car);
            var carEntries = cars.LineEntriesOf(car);
            for (var arc = 0; arc < arcsPerCar; arc++)
            {
                arcs[(car * arcsPerCar) + arc] = ArcRow.Of(carArcs[arc]);
                entries[(car * arcsPerCar) + arc] = carEntries[arc];
            }

            for (var slot = 0; slot < lanes; slot++)
            {
                chain[(car * lanes) + slot] = cars.ChainOf(car)[slot];
                starts[(car * lanes) + slot] = cars.LaneStartsOf(car)[slot];
                ends[(car * lanes) + slot] = cars.LaneEndsOf(car)[slot];
                firstArcs[(car * lanes) + slot] = (uint)cars.LaneFirstArcsOf(car)[slot];
                breaks[(car * lanes) + slot] = Flag(cars.JoinBreaksOf(car)[slot]);
            }

            var (sinceS, closestM) = town.DriveClocks.Of(car);
            clocks[car] = new Vector2(sinceS, closestM);
            poses[car] = new PoseRow(
                cars.PositionM[car], cars.VelocityMps[car], Heading.Unit(cars.HeadingRad[car]), cars.AccelerationMps2[car],
                cars.HeadingRad[car], cars.YawRateRadPerS[car], cars.MassKg[car], 0f);
            var command = cars.Command[car];
            commands[car] = new CommandRow(
                command.SteerRad, command.ThrottleMps2, command.BrakeMps2, (command.Handbrake ? 1u : 0u) | (command.Reverse ? 2u : 0u));
            buildOf[car] = cars.Variant[car];
        }

        var builds = new CarBuildRow[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = CarBuildRow.Of(cars.BuildOfVariant(variant));

        Set(64, device.Holding<TickRow>(ticks));
        Set(66, device.Holding<GrantRow>(grants));
        Set(69, device.Holding<BoxRow>(boxes));
        Set(70, device.Holding<ContextRow>(contexts));
        Set(72, device.Holding<LineRow>(lines));
        Set(73, device.Holding<ArcRow>(arcs));
        Set(74, device.Holding<float>(entries));
        Set(75, device.Holding<int>(chain));
        Set(76, device.Holding<float>(starts));
        Set(77, device.Holding<float>(ends));
        Set(78, device.Holding<uint>(firstArcs));
        Set(79, device.Holding<uint>(breaks));
        Set(81, device.Holding<Vector2>(clocks));
        Set(82, device.Holding<PoseRow>(poses));
        Set(83, device.Holding<CommandRow>(commands));
        Set(87, device.Holding<uint>(buildOf));
        Set(88, device.Holding<CarBuildRow>(builds));

        // The walk rows (walker_rows.glsl's WalkerRow, 38 words), of which the leg reads a person's trip stage alone.
        const int walkWords = 38, stageWord = 34;
        var walks = new uint[Math.Max(1, town.People.Capacity) * walkWords];
        for (var person = 0; person < town.People.Count; person++) walks[(person * walkWords) + stageWord] = (uint)town.People.Stage[person];

        Set(259, device.Holding<uint>(walks));
    }

    void LayTheAnswers(Lavapipe device, TownWorld town)
    {
        var cars = town.Cars;
        var answers = new AnswersRow[cars.Capacity];
        for (var car = 0; car < cars.Count; car++)
        {
            var entrance = town.TheEntranceItIsSentTo(car, out var lane, out var alongM) ? lane : -1;
            var getsPast = town.Overtaking.MayGetPastWhatCutIt(car, out _, out _)
                           && town.Overtaking.ComesUpTo(car, cars.AlongMps[car], cars.Context[car].AuthorityM);
            answers[car] = new AnswersRow(
                Flag(town.IsAimedAtAPlaceInTheRoad(car)), entrance, alongM, Flag(town.IsUnderOrders(car)), Flag(town.IsAnEvacuator(car)),
                Flag(town.IsAPatrolCar(car)), Flag(town.WheelIsHeldOver(car)), town.OffTheLineAllowanceM(car),
                Flag(town.ClosedLinksBar(car)), Flag(getsPast), Flag(town.Switching.Wants(car, out _)), 0u);
        }

        var atTheWheel = new uint[Math.Max(1, town.People.Count)];
        for (var person = 0; person < town.People.Count; person++) atTheWheel[person] = Flag(town.IsAtTheWheel(person));

        // What ParkingIn.StopForTheBayM answers each car's build at each bay's street lanes — any car of that build's.
        var bays = town.Parking.BayCount;
        var variants = CarCatalog.Shared.SheetCount;
        var stops = new float[Math.Max(1, variants * bays * BayStreets.MostLanes)];
        Array.Fill(stops, float.NaN);
        var streets = town.BayStreets.Kept.Lane;
        for (var variant = 0; variant < variants; variant++)
        {
            var wearer = Array.IndexOf(cars.Variant, (byte)variant, 0, cars.Count);
            if (wearer < 0) continue;

            for (var bay = 0; bay < bays; bay++)
            {
                for (var slot = 0; slot < BayStreets.MostLanes; slot++)
                {
                    var lane = streets[(bay * BayStreets.MostLanes) + slot];
                    if (lane < 0) continue;

                    stops[(((variant * bays) + bay) * BayStreets.MostLanes) + slot] = town.ParkingIn.StopForTheBayM(wearer, bay, lane);
                }
            }
        }

        Set(AnswersId, device.Holding<AnswersRow>(answers));
        Set(PeopleAnswersId, device.Holding<uint>(atTheWheel));
        Set(StopsId, device.Holding<float>(stops));
        Set(GlobalsId, device.Holding<GlobalsRow>([new GlobalsRow(Flag(town.ClosedLinksBarNow), Flag(town.HoldAgents), bays, 0u)]));
    }

    GpuBuffer Set(int id, GpuBuffer buffer)
    {
        _owned.Add(buffer);
        _root.Set(id, buffer);
        return buffer;
    }

    public void Dispose()
    {
        _kernel?.Dispose();
        Routes.Dispose();
        _state.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
        _root.Dispose();
    }
}
