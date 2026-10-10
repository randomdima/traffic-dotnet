using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>A switching car's look for room, on the device</b> (CAR-53; <c>car_switch_aside.comp</c>, <c>car_switch_look.comp</c>):
/// where a car moving across has its decision asked of the device (<see cref="DeviceCarTick.DecideTermsRow.Switching"/>),
/// what <see cref="Switching.Consider"/> would make of it past the turn — how far across the lane beside stands, whether it
/// looks again yet, and the step it would draw, sweep and ask the claims the town's readers hold for — worked out ahead of
/// the town's turn, and taken by the town as it would have taken its own (<see cref="Switching.TakeTheLook"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Only a look that changes nothing the car does is the device's</b>: one that found room and asks for a step, or more
/// than the device's rooms hold, makes the decision more than its clock, and the town takes it whole.
/// </para>
/// <para>
/// <b>It reads the layer the readers read</b>: the claims and the ways sent back with the last rebuild laid here
/// (<see cref="DeviceRebuild.ForTheLook"/>), which a tick laying a rebuild of its own leaves behind — so on that tick, or
/// where the town laid the claims itself, the town looks (<see cref="DeviceTown"/>'s terms).
/// </para>
/// </remarks>
internal sealed class DeviceSwitchLook : IDisposable
{
    /// <summary>One car moving across as the town left it (<c>SwitchInRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct InRow(uint Channels, float LooksInS, float FromRestM, float RoomToM);

    /// <summary>
    /// What its decision came to (<c>SwitchOutRow</c>, <see cref="SwitchLook"/>): how, the figures it left, the ways its look
    /// asked the layer about (<see cref="LookOf"/>), the stamp of the tick that wrote it — and why a look found no room, with
    /// the claim that refused it where one did, for whoever holds it to the town's (<see cref="WhyOf"/>).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct OutRow(
        uint Outcome, float AsideM, float LooksInS, float FromRestM, float RoomToM, uint MarkFirst, uint MarkCount, uint Stamp,
        Why Why, uint RefusedWay, int RefusedOccupant, uint RefusedFlags, float RefusedFromM, float RefusedToM);

    /// <summary>Why a look found no room (<c>car_switch_look.comp</c>'s <c>Why…</c>), or that it found some.</summary>
    internal enum Why : uint
    {
        Room,
        NoStep,
        OffTheRoad,
        BodyPastItsRuns,
        AskedPastItsRuns,
        Refused,
    }

    /// <summary>Where a claim refused this tick's look for a car: the way it is on, and whose it is.</summary>
    public bool RefusedBy(int car, out int way, out int occupant, out LaneRoster roster)
    {
        ref readonly var row = ref _out.Span<OutRow>()[car];
        (way, occupant, roster) = ((int)row.RefusedWay, row.RefusedOccupant, (LaneRoster)((row.RefusedFlags >> 4) & 3u));
        return row.Why == Why.Refused;
    }

    /// <summary>
    /// Why this tick's look for a car came out as it did, and — where a claim refused it — that claim as the device held it,
    /// in the words of the town's own <see cref="LaneClaim"/>'s figures.
    /// </summary>
    public string WhyOf(int car)
    {
        ref readonly var row = ref _out.Span<OutRow>()[car];
        if (row.Why != Why.Refused) return $"{row.Why}";

        var roster = (LaneRoster)((row.RefusedFlags >> 4) & 3u);
        var passing = (row.RefusedFlags & (1u << 9)) != 0u;
        var secondary = (row.RefusedFlags & (1u << 7)) != 0u;
        return $"refused on way {row.RefusedWay} by {roster} {row.RefusedOccupant} [{row.RefusedFromM}, {row.RefusedToM})" +
               $"{(passing ? " passing" : "")}{(secondary ? " secondary" : "")}";
    }

    /// <summary>What a look is drawn and swept by, field for field with <c>car_switch_look.comp</c>'s, each the host's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Figures(
        float ReactionS, float PassSpareM, float StandOffM, float GripMargin, float BrakingMargin, float AskEveryS,
        float SinceLastDecisionS, float Tau, float TauSquared, float FourPiSquared, uint FirstConnector, uint FirstFootway,
        uint ArcsPerCar, uint MostLanes, uint MostRunsOfASwitch, uint Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct AsidePush(uint CarCount, uint MostLanes);

    [StructLayout(LayoutKind.Sequential)]
    struct LookPush
    {
        public DeviceAtlas.AskPush Atlas;
        public uint Stamp;
        public uint WaysKnown;
        public uint MarkRoom;
        public uint Mode;
        public uint DueRoom;
        public uint PoolRoom;
    }

    /// <summary><c>car_switch_look.comp</c>'s passes: each decision due sorted, then each look listed taken.</summary>
    const uint Sort = 0u, Look = 1u;

    /// <summary><c>car_switch_look.comp</c>'s workgroup: a look's, every read of its stations at once.</summary>
    const int LookGroup = 256;

    /// <summary>A look left to the town for want of room (<c>SwitchDefers</c>): its outcome is the town's own to find.</summary>
    public const SwitchOutcome Defers = (SwitchOutcome)uint.MaxValue;

    /// <summary>How many ways every look of a tick may ask the layer about between them, past which a look is the town's.</summary>
    const int MarkRoom = 1 << 16;

    /// <summary>How many looks of a tick the device takes, past which one is the town's: each a workgroup, a room of a switch's runs and two pools.</summary>
    const int DueRoom = 1 << 9;

    /// <summary>How many ways a look's stations may read between them in each of its two sweeps, past which it is the town's.</summary>
    const int PoolRoom = 1 << 10;

    /// <summary>The words <c>car_switch_look.comp</c> counts in, as each tick begins: no look listed, a dispatch one deep, no way marked.</summary>
    static ReadOnlySpan<uint> CountsAtTheStart => [0u, 1u, 1u, 0u];

    /// <summary>The host's own 2π and 4π², folded as <see cref="Overtake"/> and <see cref="CarFollower.ShapeAPass"/> fold them.</summary>
    const float Tau = MathF.PI * 2f;

    const float FourPiSquared = 4f * MathF.PI * MathF.PI;

    readonly Vk _vk;
    readonly DeviceAtlas _atlas;
    readonly DeviceRebuild _rebuild;
    readonly List<GpuBuffer> _owned = [];
    readonly ResidentRows<InRow> _in;
    readonly GpuBuffer _asides, _out, _marked, _counts;
    readonly ComputeKernel _aside, _look;
    readonly DescriptorSet _asideSet, _lookSet;
    readonly uint _mostLanes;
    readonly GpuTimer _timer;

    /// <summary>Whether the passes were recorded since the times were last read.</summary>
    bool _timed;

    uint _stamp;

    /// <summary>The stamps a tick's passes are timed between.</summary>
    enum Stage { Began, Asides, Decided, Sorted, Looked }

    public DeviceSwitchLook(Vk vk, SimConfig config, TownWorld town, DeviceCarTick.DecideTables cars, DeviceRebuild rebuild)
    {
        _vk = vk;
        _rebuild = rebuild;
        var (atlas, marks, claims, waysSent) = rebuild.ForTheLook;
        _atlas = atlas;
        var roads = town.Roads;
        var ways = town.Ways;
        var capacity = town.Cars.Capacity;
        _mostLanes = (uint)LineAssembler.MostLanes;

        var laneFirstArc = new uint[roads.LaneCount + 1];
        var laneArcs = new List<ArcRow>();
        var laneBeside = new int[2 * Math.Max(1, roads.LaneCount)];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            laneFirstArc[lane] = (uint)laneArcs.Count;
            foreach (var arc in roads.ArcsOf(lane)) laneArcs.Add(ArcRow.Of(arc));
            laneBeside[2 * lane] = roads.LaneBeside(lane, inward: false);
            laneBeside[(2 * lane) + 1] = roads.LaneBeside(lane, inward: true);
        }

        laneFirstArc[roads.LaneCount] = (uint)laneArcs.Count;
        if (laneArcs.Count == 0) laneArcs.Add(default);

        var wayLength = new float[Math.Max(1, ways.Count)];
        var zebraWays = new uint[Math.Max(1, (ways.Count + 31) / 32)];
        for (var way = 0; way < ways.Count; way++)
        {
            wayLength[way] = ways.LengthM(way);
            if (town.IsTheCrossing(way)) zebraWays[way >> 5] |= 1u << (way & 31);
        }

        var figures = new Figures(
            config.CarReactionS, config.Driving.PassSpareM, config.Driving.StandOffM, config.Driving.GripMargin,
            config.Driving.BrakingMargin, config.Driving.LaneSwitchAskEveryS,
            new TickGroups(config.Sim.AgentDecisionIntervalS, config.Sim.TickRateHz).IntervalS, Tau, Tau * Tau, FourPiSquared,
            (uint)ways.OfRoadConnector(0), (uint)ways.FirstFootwayWay, (uint)town.Cars.LineArcsOf(0).Length, _mostLanes,
            Switching.MostRunsOfASwitch, 0u);

        _in = new ResidentRows<InRow>(vk, capacity, 1, hostOnly: true);
        _asides = Own(vk.CreateBuffer(Bytes<float>(capacity), BufferUsageFlags.StorageBufferBit, HostAccess.None));
        _out = Own(vk.CreateBuffer(Bytes<OutRow>(capacity), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _marked = Own(vk.CreateBuffer(Bytes<uint>(MarkRoom), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _counts = Own(
            vk.CreateBuffer(
                Bytes<uint>(CountsAtTheStart.Length),
                BufferUsageFlags.StorageBufferBit | BufferUsageFlags.IndirectBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));
        var swept = Own(
            vk.CreateBuffer(Bytes<SweptRunRow>(DueRoom * Switching.MostRunsOfASwitch), BufferUsageFlags.StorageBufferBit, HostAccess.None));
        var covers = Own(vk.CreateBuffer(Bytes<CoverRow>(DueRoom * 2 * PoolRoom), BufferUsageFlags.StorageBufferBit, HostAccess.None));
        var due = Own(vk.CreateBuffer(Bytes<uint>(DueRoom), BufferUsageFlags.StorageBufferBit, HostAccess.None));
        var laneLength = Laid<float>(roads.LaneLengthM);

        _aside = new ComputeKernel(vk, "car_switch_aside.comp", 11, (uint)Marshal.SizeOf<AsidePush>());
        _asideSet = _aside.Bind(
        [
            cars.DecideList, cars.DecideTerms, cars.Ticks, cars.Lines, cars.Chain, Laid<ArcRow>(CollectionsMarshal.AsSpan(laneArcs)),
            Laid<uint>(laneFirstArc), laneLength, Laid<float>(roads.LaneWidthM), Laid<int>(laneBeside), _asides,
        ]);

        GpuBuffer[] look =
        [
            cars.DecideList, cars.DecideTerms, cars.Builds, cars.BuildOf, cars.Ticks, cars.Lines, cars.Arcs, cars.Chain, cars.Ends,
            cars.Passes, cars.Verdicts, cars.DecidedAt, cars.Clocks, cars.SwitchClocks, _in.Device, _asides, _out, _marked, _counts,
            swept, Laid<Figures>([figures]), Laid<int>(laneBeside), Laid<float>(wayLength), marks.At, marks.Sections,
            Laid<uint>(zebraWays), waysSent, claims.Rows, claims.BodyRuns, claims.PlannedRuns, claims.Order, covers, .. atlas.Tables,
            due,
        ];
        _look = new ComputeKernel(vk, "car_switch_look.comp", look.Length, (uint)Marshal.SizeOf<LookPush>());
        _lookSet = _look.Bind(look);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
    }

    /// <summary>The device's time for the last tick's passes, in milliseconds: the asides, the decisions they ran beside, the sort and the looks.</summary>
    public (double AsidesMs, double DecidedMs, double SortedMs, double LookedMs) TimesMs { get; private set; }

    /// <summary>The device's stamps of the last tick's passes taken in, once its submit has been waited for — none where it had no decision due.</summary>
    public void ReadTimes()
    {
        if (!_timed)
        {
            TimesMs = default;
            return;
        }

        _timed = false;
        _timer.Read();
        TimesMs = (
            _timer.BetweenMs((int)Stage.Began, (int)Stage.Asides), _timer.BetweenMs((int)Stage.Asides, (int)Stage.Decided),
            _timer.BetweenMs((int)Stage.Decided, (int)Stage.Sorted), _timer.BetweenMs((int)Stage.Sorted, (int)Stage.Looked));
    }

    /// <summary>A car moving across whose decision is asked of the device next, as the town left it.</summary>
    public void Lay(int car, in InRow row) => _in.Lay(car, row);

    /// <summary>What was laid sent, and the counts the next looks are kept by begun again, recorded with the tick's own sends.</summary>
    public void RecordTheSends(CommandBuffer commands)
    {
        _in.Send(commands);
        _vk.Update(commands, _counts, 0, CountsAtTheStart);
    }

    /// <summary>How far across the lane beside stands for each switching car of the first <paramref name="count"/> decisions due, recorded.</summary>
    public void RecordTheAsides(CommandBuffer commands, int count)
    {
        _timer.Reset(commands);
        _timer.Stamp(commands, (int)Stage.Began);
        _timed = true;
        _aside.Dispatch(commands, _asideSet, Groups(count), new AsidePush((uint)count, _mostLanes));
        _vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Asides);
    }

    /// <summary>
    /// The switching cars of the first <paramref name="count"/> decisions due, once the decisions are asked and before
    /// anything reads their verdicts, recorded — the tick stamped <paramref name="stamp"/>: each sorted a thread a decision,
    /// then each look listed taken a workgroup a look, as many as the sort listed.
    /// </summary>
    public void RecordTheLooks(CommandBuffer commands, int count, uint stamp)
    {
        _stamp = stamp;
        var push = new LookPush
        {
            Atlas = _atlas.PushFor(count), Stamp = stamp, WaysKnown = (uint)_rebuild.WaysSentBack, MarkRoom = MarkRoom, Mode = Sort,
            DueRoom = DueRoom, PoolRoom = PoolRoom,
        };
        _timer.Stamp(commands, (int)Stage.Decided);
        _look.Dispatch(commands, _lookSet, (uint)Math.Max(1, (count + LookGroup - 1) / LookGroup), push);
        _vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Sorted);
        _look.DispatchIndirect(commands, _lookSet, _counts, 0, push with { Mode = Look });
        _vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Looked);
    }

    /// <summary>What a switching car's decision the device left it came to, once the tick's submit has been waited for.</summary>
    public SwitchLook LookOf(int car, bool inward, out ReadOnlySpan<uint> askedAbout)
    {
        ref readonly var row = ref _out.Span<OutRow>()[car];
        if (row.Stamp != _stamp) throw new InvalidOperationException($"Car {car}'s look was read past the tick that took it.");

        askedAbout = _marked.Span<uint>().Slice((int)row.MarkFirst, (int)row.MarkCount);
        return new SwitchLook(
            (SwitchOutcome)row.Outcome, inward, Turned: false, row.AsideM, row.LooksInS, row.FromRestM, row.RoomToM, Overtake.None);
    }

    /// <summary>Whether this tick's look for a car was left to the town for want of room (<see cref="Defers"/>).</summary>
    public bool Deferred(int car)
    {
        ref readonly var row = ref _out.Span<OutRow>()[car];
        return row.Stamp == _stamp && (SwitchOutcome)row.Outcome == Defers;
    }

    static uint Groups(int count) => (uint)Math.Max(1, (count + 63) / 64);

    static ulong Bytes<T>(int count) where T : unmanaged => (ulong)(Math.Max(1, count) * Marshal.SizeOf<T>());

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    /// <summary>A device-local buffer holding these rows, sent once.</summary>
    GpuBuffer Laid<T>(ReadOnlySpan<T> rows) where T : unmanaged
    {
        var buffer = Own(_vk.CreateBuffer(Bytes<T>(rows.Length), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));
        var copy = rows.ToArray();
        buffer.Upload<T>(copy.Length, into => copy.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    /// <summary>One run of swept ground as the look's rooms hold it (<c>SweptRun</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SweptRunRow(int Way, float FromM, float ToM, float FirstAtM, float LastAtM);

    /// <summary>One way a station read, as a look's pools hold it (<c>AtlasCover</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CoverRow(int Way, float FromM, float ToM);

    public void Dispose()
    {
        _timer.Dispose();
        _look.Dispose();
        _aside.Dispose();
        _in.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
