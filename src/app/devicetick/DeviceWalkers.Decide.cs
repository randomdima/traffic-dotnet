using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The kept walkers' decisions put off a tick, asked of the device first</b> (<c>walkers_decide.comp</c>): at the head of
/// the next tick's submit, of each walker's kept row, where the bodies' step left it and its hold as the town's readers read
/// it (<see cref="ReadersHolds"/>), whether the decision is its leg's clock alone (<see cref="TownWorld.WalkerTakesOnlyTheClock"/>)
/// — and where it is, the clock is run there and nothing comes back; where it is more, its clock is handed back and the
/// town decides it.
/// </summary>
/// <remarks>
/// <b>Only a walker the kept table holds as the town does is asked</b> (<see cref="HoldsAsTheTownHas"/>): the table is
/// written over at a rebuild with what the town changed since the last (<see cref="LayTheKept"/>), so a walker the town
/// changed meanwhile is the town's to decide whole.
/// </remarks>
internal sealed partial class DeviceWalkers
{
    /// <summary><c>walkers_decide.comp</c>'s <c>WalkerDecideFigures</c>, field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal record struct DecideFigures(
        uint Stamp, uint Count, float WayInReachM, float WalksBeforeTheNextM, float ByM, float PatienceS, float SinceLastDecisionS,
        float LostOffM, uint Checks, uint Pad0, uint Pad1, uint Pad2);

    /// <summary><c>walker_readers.glsl</c>'s <c>WalkerReadersHold</c>, field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ReadersHold(uint Bits, int CutOn, uint Pad0, uint Pad1, DeviceClaims.ClaimRow Cutter);

    /// <summary><c>walker_decide.glsl</c>'s <c>WalkerClock</c>: a walker's leg's clock, and the route slot it was last run or laid at.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkerClock(float SinceS, float ClosestM, int RouteTakenAt, uint Pad = 0u);

    /// <summary><c>walkers_decide.comp</c>'s <c>WalkerClockOut</c>: the clock a walker whose decision is more is decided with.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkerClockOut(float SinceS, float ClosestM, uint Stamp, uint Pad);

    /// <summary>A walker due, in the decision list: the town decides it whole whatever is found.</summary>
    public const uint ByTheTown = 0x80000000u;

    GpuBuffer _decideFigures = null!, _decideList = null!, _readersHolds = null!, _walkerVerdicts = null!, _walkerDecideOut = null!;
    ResidentRows<WalkerClock>? _walkerClocks;
    ComputeKernel? _decide;
    DescriptorSet _decideSet;
    uint _decideStamp;
    int _decideCount;

    /// <summary>Each kept walker's hold as the town's readers read it, written by the rebuild that keeps up with them.</summary>
    public GpuBuffer ReadersHolds => _readersHolds;

    /// <summary>
    /// Each walker's last verdict — the stamp of the decisions laid (<see cref="DecideStamp"/>) shifted past whether it is
    /// more than the clock — for a kernel after the decisions that reads which were handed to the town.
    /// </summary>
    public GpuBuffer Verdicts => _walkerVerdicts;

    /// <inheritdoc cref="Verdicts"/>
    public uint DecideStamp => _decideStamp;

    void DecideOverThem(GpuBuffer ticked)
    {
        _decideFigures = _vk.CreateBuffer((ulong)Marshal.SizeOf<DecideFigures>(), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        _decideList = _vk.CreateBuffer(Bytes<uint>(_walkers), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        _readersHolds = Device<ReadersHold>(_walkers);
        _walkerVerdicts = _vk.CreateBuffer(Bytes<uint>(_walkers), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _walkerDecideOut = _vk.CreateBuffer(Bytes<WalkerClockOut>(_walkers), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _walkerClocks = new ResidentRows<WalkerClock>(_vk, _walkers, 1);
        GpuBuffer[] set =
        [
            .. _stationSet, _decideFigures, _decideList, ticked, _readersHolds, _walkerClocks.Device, _walkerVerdicts, _walkerDecideOut,
        ];
        _decide = new ComputeKernel(_vk, "walkers_decide.comp", set.Length, (uint)Marshal.SizeOf<Figures>());
        _decideSet = _decide.Bind(set);
    }

    /// <summary>
    /// <b>Whether the kept table holds this walker as the town does</b> — every field its decision reads, and its route — so
    /// the device may be asked its decision.
    /// </summary>
    /// <remarks>
    /// Of the row, only what <c>walker_decide.glsl</c> reads is held to the town's — where the town's walker stands is the
    /// walker tick's table's, laid every tick, and its hold the readers' copy.
    /// </remarks>
    public bool HoldsAsTheTownHas(TownWorld world, int person)
    {
        if (!_isKnown[person]) return false;

        ref readonly var known = ref _known[person];
        var people = world.People;
        var flags = (people.Walking[person] ? WalkerRow.Walking : 0u) | (people.Wounded[person] ? WalkerRow.Wounded : 0u)
                    | (people.Inside[person].Any ? WalkerRow.Inside : 0u) | (people.RouteRunsOut[person] ? WalkerRow.RunsOut : 0u);
        const uint Read = WalkerRow.Walking | WalkerRow.Wounded | WalkerRow.Inside | WalkerRow.RunsOut;
        return (known.Flags & Read) == flags && known.Stage == (uint)people.Stage[person] && known.Action == (uint)people.Action[person]
               && known.GoalM == people.GoalM[person] && known.DestinationM == people.DestinationM[person]
               && known.RouteTaken == people.RouteTaken[person] && known.RouteCount == people.RouteCount[person]
               && known.RouteToM.Equals(people.RouteToM[person]) && known.OnWayM.Equals(people.OnWayM[person])
               && known.OffWayM.Equals(people.OffWayM[person]) && known.PassAsideM.Equals(people.Pass[person].AsideM)
               && known.GrantM.Equals(people.GrantM[person]) && known.RadiusM.Equals(people.RadiusM[person])
               && _routes!.IsLaid(person, people.RouteOf(person)[..people.RouteCount[person]]);
    }

    /// <summary>A walker's leg's clock as the town holds it, laid where the device runs it, with the route slot it was laid at.</summary>
    public void LayTheClock(int person, float sinceS, float closestM, int routeTaken)
    {
        _walkerClocks!.Rows(person)[0] = new WalkerClock(sinceS, closestM, routeTaken);
        _walkerClocks.Mark(person);
    }

    /// <summary>
    /// <b>The walkers' decisions put off a tick that the next tick asks of the device</b>: each due, with <see cref="ByTheTown"/>
    /// where the town decides it whole, and what the decision reads besides the walker.
    /// </summary>
    public void LayTheDecisions(ReadOnlySpan<uint> due, in DecideFigures figures)
    {
        _decideStamp++;
        due.CopyTo(_decideList.Span<uint>());
        _decideFigures.Span<DecideFigures>()[0] = figures with { Stamp = _decideStamp, Count = (uint)due.Length };
        _decideCount = due.Length;
    }

    /// <summary>The clocks laid since the last tick, copied across, recorded; the caller barriers.</summary>
    public void RecordTheClocksSent(CommandBuffer commands) => _walkerClocks!.Send(commands);

    /// <summary>The decisions laid asked, recorded after what they read is in; the caller barriers before what reads the clocks.</summary>
    public void RecordTheDecisions(CommandBuffer commands)
    {
        if (_decideCount == 0) return;

        _decide!.Dispatch(commands, _decideSet, Groups(_decideCount), _figures with { WalkerCount = (uint)_decideCount });
        _decideCount = 0;
    }

    /// <summary>Whether a walker's decision the device was asked of was more than its clock, once the tick's submit is waited for.</summary>
    public bool DecidedMoreThanTheClock(int person)
    {
        var verdict = _walkerVerdicts.Span<uint>()[person];
        if (verdict >> 1 != _decideStamp) throw new InvalidOperationException($"Walker {person}'s decision was read past the tick that answered it.");

        return (verdict & 1u) != 0u;
    }

    /// <summary>The clock a walker whose decision was more than its clock is decided with — or, checked, any walker asked.</summary>
    public ref readonly WalkerClockOut DecidedOn(int person) => ref _walkerDecideOut.Span<WalkerClockOut>()[person];

    void DisposeTheDecisions()
    {
        _decide?.Dispose();
        _walkerClocks?.Dispose();
        _decideFigures?.Dispose();
        _decideList?.Dispose();
        _walkerVerdicts?.Dispose();
        _walkerDecideOut?.Dispose();
    }
}
