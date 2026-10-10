using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The town's route searches on the device</b> (<c>world/routing/kernels/route_search.comp</c>): both networks' graphs
/// and cells laid once and set in the root at ids 384–447 (<see cref="TownRoot"/>,
/// <c>world/routing/kernels/route_tables.glsl</c>), a fixed run of request slots, and every slot pending searched to
/// completion as <see cref="RouteSearch.Plan(int, int, LinkSurcharges?, ReadOnlySpan{bool}, out int)"/> searches it — the
/// same links, goal, section and cost, to the bit.
/// </summary>
/// <remarks>
/// <para>
/// <b>A search is a request</b>: whoever wants a route writes a slot pending, and reads its answer once the slot is done,
/// the stamp it asked with echoed. On the resident engine other kernels write the requests and read the answers;
/// <see cref="Ask"/>, <see cref="Answer"/> and <see cref="LinksOf"/> are the host's, for the suite and the probe only.
/// </para>
/// <para>
/// <b>One thread a slot, and every slot's scratch its own</b> — a cost, a predecessor, a stamp and a heap place for every
/// link of the larger network and every cell of the larger set of cells — so a slot serves either network and nothing a
/// search writes is another's. A slot carries its planners' generations and its window's stamp from one search to the
/// next, which is what lets a search start clean without clearing a town's worth of links.
/// </para>
/// <para>
/// <b>A search is one thread's, and as slow as the device is to answer one load after another</b>: a batch takes as long
/// as its slowest search, and <c>--bench deviceroutes</c> prints both.
/// </para>
/// <para>
/// <b>The surcharges and the closed links are laid by the host for now</b> (<see cref="LaySurcharges"/>,
/// <see cref="LayClosed"/>); a kernel writes them once the town's errands are the device's.
/// </para>
/// </remarks>
internal sealed class DeviceRoutes : IDisposable
{
    /// <summary>How many searches one recording runs at most: one workgroup each.</summary>
    public const int Slots = 64;

    /// <summary>
    /// <b>The most entries a request carries</b> (<c>ROUTE_MOST_ENTRIES</c>): a car's own lane and every lane abeam of it on
    /// its stretch (CAR-53), or the lanes a bay pulls out onto. A town with more lanes to a way is refused where it is
    /// laid (<see cref="Of"/>).
    /// </summary>
    public const int MostEntries = 8;

    /// <summary>The most goals (<c>ROUTE_MOST_GOALS</c>): both directions of a stretch, or both lanes of a pavement.</summary>
    public const int MostGoals = 2;

    /// <summary>
    /// How many links one answer has room for — the town's own searches' bound (<c>TownWorld.MostRunsInARoute</c>), which
    /// a longer route is refused at, so it is the same figure or not the same answer.
    /// </summary>
    public const int MostLinks = 256;

    /// <summary>How many marks a network's surcharges hold — as many as the town's (<c>TownWorld.MostWaysGivenUpOn</c>).</summary>
    public const int MostMarks = 64;

    /// <summary>A request slot's state (<c>RouteSlot*</c>): written pending by its asker, done by the search.</summary>
    public const uint Empty = 0u;

    public const uint Pending = 1u;

    public const uint Done = 2u;

    /// <summary>The two networks, by the number a request names them by.</summary>
    public enum Network
    {
        Driving = 0,
        Walking = 1,
    }

    // The ids route_tables.glsl names: network n's run from Networks + n * NetworkIds, then the tables both share.
    const int Networks = 384;
    const int NetworkIds = 16;
    const int GraphTurnStart = 0;
    const int GraphTurnTo = 1;
    const int GraphTurnPriceM = 2;
    const int GraphWeightM = 3;
    const int Fine = 0;
    const int Coarse = 4;
    const int CellOfId = 8;
    const int NearMId = 9;
    const int CellLinkStartId = 10;
    const int CellLinksId = 11;
    const int NetworkFiguresId = 12;
    const int SurchargesId = 13;
    const int ClosedId = 14;
    const int FiguresId = 416;
    const int RequestsId = 417;
    const int ResultsId = 418;
    const int ResultLinksId = 419;
    const int SlotStateId = 420;
    const int FineStateId = 421;
    const int FineHeapId = 422;
    const int CoarseStateId = 423;
    const int CoarseHeapId = 424;
    const int CrossedId = 425;
    const int InWindowId = 426;
    const int WindowCellsId = 427;

    /// <summary>Per agent, the slot its ask is held in and the stamp it last asked with (<c>route_asks.glsl</c>).</summary>
    const int AskSlotId = 428;

    const int AskStampId = 429;

    /// <summary>Where a surcharges table's marks start, past its count (<c>RouteSurchargeTable</c>).</summary>
    const int MarksAtBytes = 16;

    /// <summary>The room a request row carries its entries in.</summary>
    [InlineArray(MostEntries)]
    internal struct EntryRoom
    {
        RouteEntry _entry;
    }

    /// <summary>The room a request row carries its goals in.</summary>
    [InlineArray(MostGoals)]
    internal struct GoalRoom
    {
        RouteGoal _goal;
    }

    /// <summary>
    /// One request slot (<c>RouteRequest</c>): its state, the network, the entries and goals as the C# search is handed
    /// them, whether the closed links bar it (SRV-10), the clock its surcharges are read at, and who asked with what stamp.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RequestRow
    {
        public uint State;
        public uint Network;
        public uint EntryCount;
        public uint GoalCount;
        public uint ClosedApply;
        public float NowS;
        public uint Owner;
        public uint Stamp;
        public EntryRoom Entries;
        public GoalRoom Goals;
    }

    /// <summary>
    /// One slot's answer (<c>RouteResult</c>): what <see cref="RouteSearch"/> answers its asker — how many links, the goal
    /// reached or led to, whether it stops short or was searched over the whole graph, its cost and how many links it
    /// settled — and the asker and stamp it answers.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ResultRow(
        uint Count, int GoalSlot, uint StopsShort, uint OverTheWholeGraph, float CostM, uint SettledLinks, uint Owner, uint Stamp);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct NetworkRow(uint LinkCount, uint CellCount, uint SectionCells, float AimWithinM);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct FiguresRow(uint Slots, uint FineRoom, uint CellRoom, uint MostLinks);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct MarkRow(int Link, float UntilS, float PriceM);

    /// <summary>Bytes of one link's search state (<c>RouteLinkState</c>), one heap place (<c>RouteHeapEntry</c>) and one slot's carry (<c>RouteSlotRow</c>).</summary>
    const int LinkStateBytes = 16;

    const int HeapEntryBytes = 8;

    const int SlotRowBytes = 16;

    readonly Vk _vk;
    readonly TownRoot _root;
    readonly List<GpuBuffer> _owned = [];
    readonly GpuBuffer _requests;
    readonly GpuBuffer _results;
    readonly GpuBuffer _resultLinks;
    readonly GpuBuffer[] _surcharges = new GpuBuffer[2];
    readonly GpuBuffer[] _closed = new GpuBuffer[2];
    readonly ComputeKernel _kernel;
    readonly DescriptorSet _set;

    /// <summary>Both of a town's networks laid, the town's lanes checked against a request's room for entries.</summary>
    public static DeviceRoutes Of(Vk vk, TownRoot root, TownWorld town)
    {
        var mostEntries = Math.Max(BayStreets.MostLanes, town.Roads.MostLanesOfAWay);
        if (mostEntries > MostEntries)
        {
            throw new NotSupportedException($"The town drives {mostEntries} lanes abeam and a request has room for {MostEntries}.");
        }

        return new DeviceRoutes(vk, root, town.Driving.Cells, town.Walking.Cells, town.People.Capacity + town.Cars.Capacity);
    }

    /// <param name="agents">How many agents may ask: the roster's room, walkers then cars.</param>
    public DeviceRoutes(Vk vk, TownRoot root, RouteCells driving, RouteCells walking, int agents = 0)
    {
        _vk = vk;
        _root = root;
        var fineRoom = Math.Max(driving.Graph.LinkCount, walking.Graph.LinkCount);
        var cellRoom = Math.Max(driving.CellCount, walking.CellCount);

        LayNetwork(Network.Driving, driving);
        LayNetwork(Network.Walking, walking);
        Set(FiguresId, Laid<FiguresRow>([new FiguresRow(Slots, (uint)fineRoom, (uint)cellRoom, MostLinks)]));

        _requests = Set(RequestsId, Mapped<RequestRow>(Slots, HostAccess.Writes));
        _results = Set(ResultsId, Mapped<ResultRow>(Slots, HostAccess.Reads));
        _resultLinks = Set(ResultLinksId, Mapped<int>(Slots * MostLinks, HostAccess.Reads));

        // Every table a stamp is read off starts at nought, which no search's stamp is.
        GpuBuffer[] stamped =
        [
            Set(SlotStateId, Scratch(Slots * SlotRowBytes)),
            Set(FineStateId, Scratch((long)Slots * fineRoom * LinkStateBytes)),
            Set(CoarseStateId, Scratch((long)Slots * cellRoom * LinkStateBytes)),
            Set(InWindowId, Scratch((long)Slots * cellRoom * sizeof(uint))),
        ];
        var noSlot = new int[Math.Max(1, agents)];
        Array.Fill(noSlot, -1);
        Set(AskSlotId, Laid<int>(noSlot));
        Set(AskStampId, Laid<uint>(new uint[Math.Max(1, agents)]));
        Set(FineHeapId, Scratch((long)Slots * fineRoom * HeapEntryBytes));
        Set(CoarseHeapId, Scratch((long)Slots * cellRoom * HeapEntryBytes));
        Set(CrossedId, Scratch((long)Slots * (cellRoom + 1) * sizeof(int)));
        Set(WindowCellsId, Scratch((long)Slots * cellRoom * sizeof(int)));
        vk.OneShot(commands =>
        {
            foreach (var table in stamped) vk.Fill(commands, table, 0u);
            vk.AfterTransfer(commands);
        });

        _kernel = new ComputeKernel(vk, "route_search.comp", 1, 0u);
        _set = _kernel.Bind([root.Buffer]);
    }

    /// <summary>How many bytes the slots' scratch takes on the device — what running <see cref="Slots"/> searches at once costs.</summary>
    public ulong ScratchBytes { get; private set; }

    /// <summary>
    /// <b>Every pending slot searched</b>, its answer written and the slot marked done — recorded, and made visible to the
    /// kernels after it. One dispatch whatever the town, and nothing allocated.
    /// </summary>
    /// <remarks>
    /// <b>A workgroup a slot</b>: a wave's lanes go in step, so eight searches a wave took twice as long as one each,
    /// measured on the probe.
    /// </remarks>
    public void Record(CommandBuffer commands)
    {
        _kernel.Dispatch(commands, _set, Slots, default(NoPush));
        _vk.BetweenKernels(commands);
    }

    /// <summary>
    /// <b>A network's surcharges laid as the town holds them</b>, every slot marked whether live or spent: a search asks
    /// each its life at the clock its request names (<see cref="LinkSurcharges.SlotsMarked"/>).
    /// </summary>
    public void LaySurcharges(Network network, LinkSurcharges surcharges)
    {
        var count = surcharges.SlotsMarked;
        if (count > MostMarks) throw new NotSupportedException($"{count} marks, and the device holds {MostMarks}.");

        var table = _surcharges[(int)network].Span<byte>();
        var marks = MemoryMarshal.Cast<byte, MarkRow>(table[MarksAtBytes..]);
        for (var slot = 0; slot < count; slot++)
        {
            var (link, priceM, untilS) = surcharges.MarkIn(slot);
            marks[slot] = new MarkRow(link, untilS, priceM);
        }

        MemoryMarshal.Cast<byte, uint>(table)[0] = (uint)count;
    }

    /// <summary>A network's closed links laid, one flag a link — or none closed, for an empty span.</summary>
    public void LayClosed(Network network, ReadOnlySpan<bool> closed)
    {
        var flags = _closed[(int)network].Span<uint>();
        flags.Clear();
        for (var link = 0; link < closed.Length; link++) flags[link] = closed[link] ? 1u : 0u;
    }

    /// <summary>
    /// A request written into a slot and the slot made pending — the host's own, for the suite and the probe: on the
    /// resident engine a kernel asks.
    /// </summary>
    /// <param name="closed">Whether the closed links bar the search (SRV-10), as a non-empty span does the C#'s.</param>
    /// <param name="nowS">The clock the surcharges are read at — theirs (<see cref="LinkSurcharges.NowS"/>).</param>
    public void Ask(
        int slot, Network network, ReadOnlySpan<RouteEntry> entries, ReadOnlySpan<RouteGoal> goals, bool closed, float nowS,
        int owner, uint stamp)
    {
        if (entries.Length > MostEntries || goals.Length > MostGoals)
        {
            throw new ArgumentException($"{entries.Length} entries and {goals.Length} goals, and a request has room for {MostEntries} and {MostGoals}.");
        }

        ref var row = ref _requests.Span<RequestRow>()[slot];
        row.Network = (uint)network;
        row.EntryCount = (uint)entries.Length;
        row.GoalCount = (uint)goals.Length;
        row.ClosedApply = closed ? 1u : 0u;
        row.NowS = nowS;
        row.Owner = (uint)owner;
        row.Stamp = stamp;
        entries.CopyTo(row.Entries);
        goals.CopyTo(row.Goals);
        row.State = Pending;
    }

    /// <summary>A slot's state as the device last left it.</summary>
    public uint StateOf(int slot) => _requests.Span<RequestRow>()[slot].State;

    /// <summary>A slot's answer, read once the slot is done.</summary>
    public ResultRow Answer(int slot) => _results.Span<ResultRow>()[slot];

    /// <summary>The links of a slot's answer, in the order they are travelled.</summary>
    public ReadOnlySpan<int> LinksOf(int slot, int count) => _resultLinks.Span<int>().Slice(slot * MostLinks, count);

    void LayNetwork(Network network, RouteCells cells)
    {
        var first = Networks + ((int)network * NetworkIds);
        LayGraph(first + Fine, cells.Graph);
        LayGraph(first + Coarse, cells.Coarse);
        Set(first + CellOfId, Laid(cells.CellsOfLinks));
        Set(first + NearMId, Laid(cells.NearMiddlesOfLinks));

        var linkStart = new uint[cells.CellCount + 1];
        for (var cell = 0; cell < cells.CellCount; cell++) linkStart[cell + 1] = linkStart[cell] + (uint)cells.LinksOf(cell).Length;

        var links = new int[linkStart[cells.CellCount]];
        for (var cell = 0; cell < cells.CellCount; cell++) cells.LinksOf(cell).CopyTo(links.AsSpan((int)linkStart[cell]));

        Set(first + CellLinkStartId, Laid<uint>(linkStart));
        Set(first + CellLinksId, Laid<int>(links));
        Set(first + NetworkFiguresId, Laid<NetworkRow>(
            [new NetworkRow((uint)cells.Graph.LinkCount, (uint)cells.CellCount, (uint)cells.SectionCells, cells.AimWithinM)]));

        var surcharges = Set(first + SurchargesId, Mapped<byte>(MarksAtBytes + (MostMarks * Marshal.SizeOf<MarkRow>()), HostAccess.Writes));
        surcharges.Span<byte>().Clear();
        _surcharges[(int)network] = surcharges;

        var closed = Set(first + ClosedId, Mapped<uint>(cells.Graph.LinkCount, HostAccess.Writes));
        closed.Span<uint>().Clear();
        _closed[(int)network] = closed;
    }

    /// <summary>A graph's four tables (<c>GraphTurnStart</c> …) from its first id: the turns in the graph's own order.</summary>
    void LayGraph(int first, TravelGraph graph)
    {
        var turnStart = new uint[graph.LinkCount + 1];
        for (var link = 0; link < graph.LinkCount; link++) turnStart[link + 1] = turnStart[link] + (uint)graph.TurnsFrom(link).Length;

        var turnTo = new int[turnStart[graph.LinkCount]];
        var turnPriceM = new float[turnTo.Length];
        var weightM = new float[graph.LinkCount];
        for (var link = 0; link < graph.LinkCount; link++)
        {
            graph.TurnsFrom(link).CopyTo(turnTo.AsSpan((int)turnStart[link]));
            graph.TurnPricesFrom(link).CopyTo(turnPriceM.AsSpan((int)turnStart[link]));
            weightM[link] = graph.WeightM(link);
        }

        Set(first + GraphTurnStart, Laid<uint>(turnStart));
        Set(first + GraphTurnTo, Laid<int>(turnTo));
        Set(first + GraphTurnPriceM, Laid<float>(turnPriceM));
        Set(first + GraphWeightM, Laid<float>(weightM));
    }

    GpuBuffer Set(int id, GpuBuffer table)
    {
        _root.Set(id, table);
        return table;
    }

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    /// <summary>A device-local table holding these rows, sent once.</summary>
    GpuBuffer Laid<T>(ReadOnlySpan<T> rows) where T : unmanaged
    {
        var buffer = Own(_vk.CreateBuffer(Bytes<T>(rows.Length), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));
        var copy = rows.ToArray();
        buffer.Upload<T>(copy.Length, into => copy.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    GpuBuffer Mapped<T>(int count, HostAccess host) where T : unmanaged
    {
        var buffer = Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.StorageBufferBit, host));
        buffer.Span<byte>().Clear();
        return buffer;
    }

    /// <summary>A device-local table only the searches write, filled where the device needs it clean.</summary>
    GpuBuffer Scratch(long bytes)
    {
        ScratchBytes += (ulong)Math.Max(1L, bytes);
        return Own(_vk.CreateBuffer((ulong)Math.Max(1L, bytes), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));
    }

    public void Dispose()
    {
        _kernel.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
