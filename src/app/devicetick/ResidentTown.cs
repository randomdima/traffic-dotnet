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
    readonly record struct ListsPush(uint Cars, uint Mode, uint Group);

    /// <summary>The ids of the resident town's own lists on the root (<c>resident_lists.comp</c>).</summary>
    const int ListCountsId = 2, TakenId = 3;

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

        _listCounts = vk.CreateBuffer(
            Bytes<uint>(4), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.IndirectBufferBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None);
        _root.Set(ListCountsId, _listCounts);
        _root.Set(TakenId, cars.TakenTable);
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

        _ticks++;
        _elapsedS += _dtS;
        vk.Fill(commands, _listCounts, 0u);
        vk.AfterTransfer(commands);
        var groups = (uint)((_cars + (int)CarTickGroup - 1) / (int)CarTickGroup);
        _lists.Dispatch(commands, _listsSet, Math.Max(1u, groups), new ListsPush((uint)_cars, 0u, CarTickGroup));
        vk.BetweenKernels(commands);
        _lists.Dispatch(commands, _listsSet, Math.Max(1u, groups), new ListsPush((uint)_cars, 1u, CarTickGroup));
        vk.BetweenKernels(commands);

        cars.RecordTheTickOver(commands, _listCounts, sizeof(uint), _cars);
        vk.BetweenKernels(commands);
        walkers.RecordTheTick(commands);
        vk.BetweenKernels(commands);
        bodies.Phase.Record(commands, _cars, _people, _dtS, DeviceBodyPhase.Reads.None);
        _batch.SubmitAndWait();
    }

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
