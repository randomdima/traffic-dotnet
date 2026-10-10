using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;
using static TrafficSimulation.Runtime.GpuBuffer;

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
        float LostOffM, uint Checks, uint FindCount = 0u, uint DueTick = 0u, uint Groups = 1u, uint FoundByTheTown = 0u,
        uint Pad0 = 0u, uint Pad1 = 0u, uint Pad2 = 0u);

    /// <summary><c>walker_readers.glsl</c>'s <c>WalkerReadersHold</c>, field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ReadersHold(uint Bits, int CutOn, uint Pad0, uint Pad1, DeviceClaims.ClaimRow Cutter);

    /// <summary><c>walker_decide.glsl</c>'s <c>WalkerClock</c>: a walker's leg's clock, and the route slot it was last run or laid at.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkerClock(float SinceS, float ClosestM, int RouteTakenAt, uint Pad = 0u);

    /// <summary>
    /// <c>walkers_decide.comp</c>'s <c>WalkerClockOut</c>: the clock a walker whose decision is more is decided with, and
    /// the route slot it was last run or laid at.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkerClockOut(float SinceS, float ClosestM, uint Stamp, int RouteTakenAt);

    /// <summary>A walker due, in the decision list: the town decides it whole whatever is found.</summary>
    public const uint ByTheTown = 0x80000000u;

    GpuBuffer _decideFigures = null!, _decideList = null!, _readersHolds = null!, _walkerVerdicts = null!, _walkerDecideOut = null!;

    /// <summary>The kept row of each walker whose decision was more than its clock, in the host's memory.</summary>
    GpuBuffer _walkerDecideRows = null!;
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
        _walkerDecideRows = _vk.CreateBuffer(Bytes<WalkerOut>(_walkers), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _walkerClocks = new ResidentRows<WalkerClock>(_vk, _walkers, 1);
        _decidesItself = new AgentSet(_walkers);
        _decidesItselfWords = new ResidentRows<uint>(_vk, Words, 1, hostOnly: true);
        _wordChanged = new bool[Words];
        _wordsChanged = new int[Words];
        _foundCounts = Device<uint>(2);
        _foundCountsOut = Readback<uint>(2);
        _foundWalkers = Own(_vk.CreateBuffer(Bytes<uint>(_walkers), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _found = new int[_walkers];
        GpuBuffer[] set =
        [
            .. _stationSet, _decideFigures, _decideList, ticked, _readersHolds, _walkerClocks.Device, _walkerVerdicts, _walkerDecideOut,
            _walkerDecideRows, _decidesItselfWords.Device, _foundCounts, _foundWalkers,
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

    /// <summary>
    /// The decisions laid asked, and those found (<see cref="FindTheDecisionsDue"/>), recorded after what they read is in; the
    /// caller barriers before what reads the clocks.
    /// </summary>
    public void RecordTheDecisions(CommandBuffer commands)
    {
        foreach (var word in _wordsChanged.AsSpan(0, _wordsChangedCount))
        {
            _wordChanged[word] = false;
            _decidesItselfWords!.Lay(word, _decidesItself!.WordOf(word << 5));
        }

        _wordsChangedCount = 0;
        _decidesItselfWords!.Send(commands);
        var finding = FindsTheDecisionsDue ? _decideFigures.Span<DecideFigures>()[0].FindCount : 0u;
        if (finding > 0) _vk.Fill(commands, _foundCounts, 0);
        _vk.AfterTransfer(commands);
        if (_decideCount == 0 && finding == 0) return;

        var threads = _decideCount + (int)finding;
        _decide!.Dispatch(commands, _decideSet, Groups(threads), _figures with { WalkerCount = (uint)threads });
        _decideCount = 0;
        if (finding == 0) return;

        _vk.BetweenKernels(commands);
        _vk.BeforeTransfer(commands);
        _vk.Copy(commands, _foundCounts, _foundCountsOut, Bytes<uint>(2));
    }

    /// <summary>
    /// <b>Whether the device finds this walker's decision itself</b> (<see cref="FindTheDecisionsDue"/>): the host neither
    /// puts it off nor names it, and decides it only where the device finds it more than its clock.
    /// </summary>
    public bool DecidesItself(int walker) => _decidesItself!.Has(walker);

    /// <summary>Whether the device finds this walker's decision itself from the next decisions asked on (<see cref="DecidesItself"/>).</summary>
    public void LetItDecideItself(int walker, bool decides)
    {
        if (decides == _decidesItself!.Has(walker)) return;

        if (decides) _decidesItself.Add(walker);
        else _decidesItself.Remove(walker);
        DecidingItself += decides ? 1 : -1;
        var word = walker >> 5;
        if (_wordChanged[word]) return;

        _wordChanged[word] = true;
        _wordsChanged[_wordsChangedCount++] = word;
    }

    /// <summary>How many walkers' decisions the device finds itself (<see cref="DecidesItself"/>).</summary>
    public int DecidingItself { get; private set; }

    /// <summary>The walkers below <paramref name="count"/> whose decisions the device does not find itself, in order.</summary>
    public ReadOnlySpan<int> TheWalkersNotDecidingThemselves(int count, Span<int> into) => into[.._decidesItself!.CopyAbsentTo(count, into)];

    /// <summary>
    /// <b>The decisions due on a tick of every walker whose decision the device finds itself</b>, asked with the next
    /// decisions laid (<see cref="LayTheDecisions"/>, which comes first): each one's clock run where that is the whole of it,
    /// and where it is not, named where <see cref="FoundMore"/> reads it with what the town decides it on
    /// (<see cref="DecidedOn"/>).
    /// </summary>
    /// <param name="tick">The loop's tick they were due on, by <paramref name="decisions"/>' turns.</param>
    /// <param name="walkers">How many walkers the table holds.</param>
    /// <param name="byTheTown">Whether every one is the town's to take whatever the device finds.</param>
    public void FindTheDecisionsDue(TickGroups decisions, long tick, int walkers, bool byTheTown)
    {
        ref var figures = ref _decideFigures.Span<DecideFigures>()[0];
        figures = figures with
        {
            FindCount = (uint)walkers, DueTick = (uint)(tick % decisions.Groups), Groups = (uint)decisions.Groups,
            FoundByTheTown = byTheTown ? 1u : 0u,
        };
        FindsTheDecisionsDue = true;
    }

    /// <summary>Whether the next decisions asked find the decisions due (<see cref="FindTheDecisionsDue"/>), until what was found is read.</summary>
    public bool FindsTheDecisionsDue { get; private set; }

    /// <summary>
    /// The walkers <see cref="FindTheDecisionsDue"/> found more than their clock, in order, and how many it found their clock
    /// alone — read once the submit that found them is waited for. Nothing where none were asked.
    /// </summary>
    public ReadOnlySpan<int> FoundMore(out int clockAlone)
    {
        clockAlone = 0;
        if (!FindsTheDecisionsDue) return default;

        FindsTheDecisionsDue = false;
        _decideFigures.Span<DecideFigures>()[0].FindCount = 0u;
        var counts = _foundCountsOut.Span<uint>();
        clockAlone = (int)counts[1];
        var found = _found.AsSpan(0, (int)counts[0]);
        MemoryMarshal.Cast<uint, int>(_foundWalkers.Span<uint>()[..found.Length]).CopyTo(found);
        found.Sort();
        return found;
    }

    /// <summary>What a kernel after the decisions reads of the walkers found: how many were more than their clock and how many their clock alone, and the walkers found more.</summary>
    public (GpuBuffer Counts, GpuBuffer More) FoundTables => (_foundCounts, _foundWalkers);

    /// <summary>
    /// Which walkers' decisions the device finds itself, as the host keeps them and as the decisions read them, and which words
    /// of those changed since they were sent; how many it found of each kind, on the device and where the host reads them; the
    /// walkers found more, in the host's memory, and read into order.
    /// </summary>
    AgentSet? _decidesItself;

    ResidentRows<uint>? _decidesItselfWords;
    bool[] _wordChanged = [];
    int[] _wordsChanged = [];
    int _wordsChangedCount;
    GpuBuffer _foundCounts = null!, _foundCountsOut = null!, _foundWalkers = null!;
    int[] _found = [];

    /// <summary>Whether a walker's decision the device was asked of was more than its clock, once the tick's submit is waited for.</summary>
    public bool DecidedMoreThanTheClock(int person)
    {
        var verdict = _walkerVerdicts.Span<uint>()[person];
        if (verdict >> 1 != _decideStamp) throw new InvalidOperationException($"Walker {person}'s decision was read past the tick that answered it.");

        return (verdict & 1u) != 0u;
    }

    /// <summary>The clock a walker whose decision was more than its clock is decided with — or, checked, any walker asked.</summary>
    public ref readonly WalkerClockOut DecidedOn(int person) => ref _walkerDecideOut.Span<WalkerClockOut>()[person];

    /// <summary>The kept row a walker whose decision was more than its clock was decided on, as <see cref="DecidedOn"/> says.</summary>
    ref readonly WalkerOut DecidedRowOf(int person) => ref _walkerDecideRows.Span<WalkerOut>()[person];

    void DisposeTheDecisions()
    {
        _decide?.Dispose();
        _walkerClocks?.Dispose();
        _decidesItselfWords?.Dispose();
        _decideFigures?.Dispose();
        _decideList?.Dispose();
        _walkerVerdicts?.Dispose();
        _walkerDecideOut?.Dispose();
        _walkerDecideRows?.Dispose();
    }
}
