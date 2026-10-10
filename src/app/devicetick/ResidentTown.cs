using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The town resident on the device</b>: every table it is made of held there and every tick run there, the host
/// recording the tick and reading nothing of the town back to run the next (root decision log, 2026-10-11).
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid once off a town that has ticked once on its device</b> (<see cref="DeviceTown"/>): that tick lays every table the
/// device's own tick reads, as the town has it; the town is then let go and ticks no more, and the rest of it — every
/// field no row of those tables holds, its registries and its map — is laid beside them (<see cref="TownState"/>), every
/// table on one root of addresses (<see cref="TownRoot"/>).
/// </para>
/// <para>
/// <b>What a tick records is the same every tick</b> (rule 1): the lists its kernels run over are written on the device and
/// dispatched indirectly, so nothing the host records depends on what the town holds.
/// </para>
/// </remarks>
internal sealed class ResidentTown : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ListsPush(uint Cars, uint People, uint Mode, uint Group, uint DueTick, uint Groups);

    /// <summary>
    /// The resident town's own lists (<c>resident_lists.comp</c>): their counts on the root, four words a list — the count
    /// and its dispatch — and each list's table from <see cref="ListsId"/>, in this order.
    /// </summary>
    internal enum List { Taken, CarsLeft, CarsDue, WalkersDue }

    const int ListCountsId = 2, ListsId = 3, Lists = 4;

    /// <summary>The car tick's workgroup (<c>car_tick.comp</c>), which the list's padding and dispatch are counted in.</summary>
    const uint CarTickGroup = 64;

    readonly Vk _vk;
    readonly DeviceTown _laid;
    readonly TownRoot _root;
    readonly TownState _state;
    readonly DeviceRoutes _routes;
    readonly GpuBuffer _counts;
    readonly GpuBuffer _listCounts;
    readonly ComputeKernel _lists;
    readonly DescriptorSet _listsSet;
    readonly ComputeBatch _batch;
    readonly float _dtS;
    readonly int _cars, _people;

    /// <summary>When the claims are laid (<see cref="SimFigures.ClaimsIntervalS"/>), counted in the town's own ticks.</summary>
    readonly TickGroups _claimsClock;

    /// <summary>Whose turn of the decisions each tick is (<see cref="SimFigures.AgentDecisionIntervalS"/>).</summary>
    readonly TickGroups _decisions;

    /// <summary>The town's ticks and its clock, the host's to count: what the lights and the claims are laid at.</summary>
    long _ticks;

    float _elapsedS;

    public ResidentTown(Vk vk, SimConfig config, TownWorld town)
    {
        if (!vk.HasInt64) throw new NotSupportedException($"{vk.DeviceName} holds no 64-bit integers, which reaching the town's tables by address needs.");

        _vk = vk;
        _dtS = config.TickSeconds;
        _claimsClock = new TickGroups(config.Sim.ClaimsIntervalS, config.Sim.TickRateHz);

        // One tick of the town on its device lays every table that tick reads; the town is then let go, its poses taken back.
        _laid = new DeviceTown(vk, config, town);
        town.Device = _laid;
        new SimLoop<TownWorld>(town, config).Advance();
        town.Device = null;
        _ticks = 1;
        _elapsedS = town.ElapsedS;

        _cars = town.Cars.Count;
        _people = town.People.Count;
        _root = new TownRoot(vk);
        var (_, cars, walkers, bodies, _, kept) = _laid.Parts;
        cars.Register(_root);
        walkers.Register(_root);
        kept.Register(_root);
        _root.Set(TownRows.CarsBodyRows, bodies.Phase.CarBodies);
        _counts = Owned(TownRows.Counts, Laid<TownRows.CountsRow>([CountsOf(town)]));
        _state = new TownState(vk, _root, town);
        _routes = DeviceRoutes.Of(vk, _root, town);

        _decisions = new TickGroups(config.Sim.AgentDecisionIntervalS, config.Sim.TickRateHz);
        _listCounts = vk.CreateBuffer(
            Bytes<uint>(4 * Lists), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.IndirectBufferBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None);
        _root.Set(ListCountsId, _listCounts);
        _root.Set(ListsId + (int)List.Taken, cars.TakenTable);
        Owned(ListsId + (int)List.CarsLeft, Device<uint>(town.Cars.Capacity));
        Owned(ListsId + (int)List.CarsDue, Device<uint>(town.Cars.Capacity));
        Owned(ListsId + (int)List.WalkersDue, Device<uint>(town.People.Capacity));
        _lists = new ComputeKernel(vk, "resident_lists.comp", 1, (uint)Marshal.SizeOf<ListsPush>());
        _listsSet = _lists.Bind([_root.Buffer]);
        _batch = new ComputeBatch(vk);
    }

    /// <summary>The root every resident kernel reaches the town's tables through.</summary>
    public TownRoot Root => _root;

    /// <summary>The device's tick that laid the tables, whose tables a frame draws off (<see cref="DeviceSprites"/>).</summary>
    public DeviceTown Tables => _laid;

    /// <summary>How many cars and walkers the town holds.</summary>
    public (int Cars, int People) Counts => (_cars, _people);

    /// <summary>
    /// <b>One tick of the town</b>, recorded, submitted and waited for: the cars the car tick takes listed, their tick and
    /// the tyres, the walkers' tick, and the bodies' step.
    /// </summary>
    public void Tick()
    {
        var (_, cars, walkers, bodies, claims, _) = _laid.Parts;
        var vk = _vk;
        var commands = _batch.Begin();
        vk.AfterTheSubmitsBefore(commands);
        if (_claimsClock.Turn(_ticks, 0))
        {
            claims.Rebuild.RecordResident(commands, _cars, _people, _elapsedS);
            vk.BetweenKernels(commands);
        }

        vk.Fill(commands, _listCounts, 0u);
        vk.AfterTransfer(commands);
        var units = Math.Max(_cars, _people);
        var groups = (uint)Math.Max(1, (units + (int)CarTickGroup - 1) / (int)CarTickGroup);
        var lists = new ListsPush(
            (uint)_cars, (uint)_people, 0u, CarTickGroup, (uint)(_ticks % _decisions.Groups), (uint)_decisions.Groups);
        _lists.Dispatch(commands, _listsSet, groups, lists);
        vk.BetweenKernels(commands);
        _lists.Dispatch(commands, _listsSet, groups, lists with { Mode = 1u });
        vk.BetweenKernels(commands);
        _ticks++;
        _elapsedS += _dtS;

        // The searches the decisions asked, each answered by the agent's next decision.
        _routes.Record(commands);

        cars.RecordTheTickOver(commands, _listCounts, DispatchOf(List.Taken), _cars);
        vk.BetweenKernels(commands);
        walkers.RecordTheTick(commands);
        vk.BetweenKernels(commands);
        bodies.Phase.Record(commands, _cars, _people, _dtS, DeviceBodyPhase.Reads.None);
        _batch.SubmitAndWait();
    }

    /// <summary>Where a list's dispatch stands in the counts, in bytes.</summary>
    static ulong DispatchOf(List list) => (ulong)(((4 * (int)list) + 1) * sizeof(uint));

    GpuBuffer Device<T>(int count) where T : unmanaged =>
        _vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.StorageBufferBit, HostAccess.None);

    static TownRows.CountsRow CountsOf(TownWorld town)
    {
        var cars = town.Cars;
        var people = town.People;
        return new TownRows.CountsRow(
            (uint)cars.Capacity, (uint)people.Capacity, (uint)cars.Count, (uint)people.Count, (uint)cars.LineArcsOf(0).Length,
            (uint)LineAssembler.MostLanes, (uint)TyreModel.Wheels, (uint)CarFleet.RouteLanesPerCar,
            (uint)PersonFleet.RouteWaysPerPerson, (uint)WalkingGround.MostRunsOfAStraight, (uint)Sidestepping.MostRunsOfAPass,
            (uint)town.Roster.AgentOfCar(0));
    }

    GpuBuffer Owned(int id, GpuBuffer table)
    {
        _owned.Add(table);
        _root.Set(id, table);
        return table;
    }

    GpuBuffer Laid<T>(ReadOnlySpan<T> rows) where T : unmanaged
    {
        var table = _vk.CreateBuffer(Bytes<T>(rows.Length), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        var copy = rows.ToArray();
        table.Upload<T>(copy.Length, into => copy.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return table;
    }

    readonly List<GpuBuffer> _owned = [];

    public void Dispose()
    {
        _batch.Dispose();
        _lists.Dispose();
        _listCounts.Dispose();
        foreach (var table in _owned) table.Dispose();
        _routes.Dispose();
        _state.Dispose();
        _root.Dispose();
        _laid.Dispose();
    }
}
