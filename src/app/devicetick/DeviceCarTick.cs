using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>A following car's whole tick on the device</b> (<c>world/town/kernels/car_tick.glsl</c>): the ground under it
/// (<c>car_ground.comp</c>), its line read, what it is told and the line driven (<c>car_tick.comp</c>), then its tyres
/// (<see cref="DeviceTyres"/>) — three kernels over one table, in one submit.
/// </summary>
/// <remarks>
/// <para>
/// <b>The table is the device's, indexed by the fleet's own car</b>, and kept from one tick to the next
/// (<see cref="ResidentRows{T}"/>): a car's line, where it is on it, its command, its tyres and the junction ahead are
/// what its last tick left. The host sends what it changed — every car's pose, what each taken car is told and was
/// granted, and a line, a reading or a whole car where something other than the tick changed it — and only that.
/// </para>
/// <para>
/// <b>Run in shadow</b> (<see cref="Prepare"/>, <see cref="Disagreement"/>): the town still ticks every car, and the
/// device's tick of the cars it takes is held to it. <see cref="TickMs"/> and <see cref="TyresMs"/> are the kernels alone.
/// </para>
/// </remarks>
internal sealed partial class DeviceCarTick : IDisposable
{
    /// <summary>What a car's tick carries from one tick to the next and reports, the device's own (<c>CarTickRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct TickRow(
        float ProgressM, float CoveredSinceClaimM, float AlongMps, float OffLineM, float PlannedMps, uint Hold, uint Flags)
    {
        public const uint Lost = 32u, AsksTheRoute = 64u, Shifted = 128u, Hazard = 256u;
    }

    /// <summary>What the host tells a car's tick (<c>CarToldRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ToldRow(uint Flags, float PaceMps, float AllowanceM, float Pad)
    {
        public const uint OnACall = 1u, LineIsReverse = 2u, HasAPlace = 4u, HasAHold = 8u, Cut = 16u;
    }

    /// <summary>What the last claims rebuild granted a car and what cut it, and the place it was sent to (<c>CarGrantRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct GrantRow(
        float AuthorityM, float HorizonM, float ClaimFromM, float CommittedToM, float GrantMarginM, uint GrantCutBy, int CutOn,
        float PlaceReachM, Vector2 PlaceM);

    /// <summary>How much of a car's line there is, and whether it ends where the leg does (<c>CarLineRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct LineRow(uint ArcCount, uint LaneCount, float LengthM, uint FinalApproach);

    /// <summary>What the driver was told (<c>DriveContextRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ContextRow(
        float GroundCoefficient, float AuthorityM, uint GrantCutBy, float PlaceStopM, float MarginM, uint WaitsToPass, float HorizonM,
        float PassAsideM, float BackUpM, uint Blocked, float WaitAtM, float Pad);

    /// <summary>The junction ahead (<c>BoxAheadRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct BoxRow(float ToTheBoxM, float LightAheadM, uint Flags, uint Pad)
    {
        public const uint Claimed = 1u, Turning = 2u, Inside = 4u, Committed = 8u;
    }

    /// <summary>The tick's figures, field for field with <c>car_tick.comp</c>'s, each the host's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Figures(
        uint CarCount, uint ArcsPerCar, uint MostLanes, uint FirstConnector, float RoundingM, float SlowerToPassMps,
        float QueueInCarLengths, float DtS, float LookaheadS, float GripMargin, float BrakingMargin, float StopSpeedMps,
        float WaitingToPassBrakingShare, float ReactionS, float CallPaceMps, float StandardGravityMps2, float WheelSpinFadeMps,
        float WheelSpinAllowanceMps, float MarkSlipMps, float MarkCorneringSlipMps, float PloughCrawlMps, float EngineBrakingMps2)
    {
        public static Figures Of(SimConfig config, int arcsPerCar, int firstConnector) => new(
            0u, (uint)arcsPerCar, (uint)LineAssembler.MostLanes, (uint)firstConnector, LineTolerance.RoundingM,
            config.Ambulance.SlowerToPassMps, config.Patience.LightQueueInCarLengths, config.TickSeconds, config.Driving.LookaheadS,
            config.Driving.GripMargin, config.Driving.BrakingMargin, config.Driving.StopSpeedMps,
            config.Driving.WaitingToPassBrakingShare, config.CarReactionS, config.Ambulance.CallPaceMps,
            config.Tyre.StandardGravityMps2, config.Tyre.WheelSpinFadeMps, config.Tyre.WheelSpinAllowanceMps, config.Marks.SlipMps,
            config.Marks.CorneringSlipMps, config.Marks.PloughCrawlMps, config.CarEngineBrakingMps2);
    }

    /// <summary>The stamps a tick is timed between.</summary>
    enum Stage { Began, Sent, Ticked, Tyred, Read }

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly DeviceTyres _tyres;
    readonly DeviceGround _ground;
    readonly Figures _figures;
    readonly List<GpuBuffer> _laid = [];
    readonly ResidentRows<TickRow> _ticks;
    readonly ResidentRows<ToldRow> _told;
    readonly ResidentRows<GrantRow> _grants;
    readonly ResidentRows<DeviceClaims.ClaimRow> _cutters;
    readonly ResidentRows<BoxRow> _boxes;
    readonly ResidentRows<ContextRow> _contexts;
    readonly ResidentRows<LineRow> _lines;
    readonly ResidentRows<DeviceGround.ArcRow> _arcs;
    readonly ResidentRows<float> _entries;
    readonly ResidentRows<int> _chain;
    readonly ResidentRows<float> _starts;
    readonly ResidentRows<float> _ends;
    readonly ResidentRows<uint> _firstArcs;
    readonly ResidentRows<uint> _breaks;
    readonly ResidentRows<uint> _taken;
    readonly ComputeKernel _groundKernel, _tickKernel;
    readonly DescriptorSet _groundSet, _tickSet;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    int _cars;

    /// <param name="tyres">The tyre pass whose table the tick writes the commands into; its capacity is the fleet's.</param>
    /// <param name="arcsPerCar">The room each car's line has (<see cref="Agents.Car.Body.CarFleet.LineArcsOf"/>).</param>
    /// <param name="firstConnector">The way the town numbers its first connector (<c>TownWays.OfRoadConnector</c>).</param>
    /// <param name="claims">The claims as the last rebuild cut them: every slot's row, and each way's planned pieces in order.</param>
    public DeviceCarTick(
        Vk vk, SimConfig config, DeviceTyres tyres, DeviceGround ground, RoadGraph roads, int arcsPerCar, int firstConnector,
        (GpuBuffer Rows, GpuBuffer PlannedRuns, GpuBuffer PlannedOrder) claims)
    {
        _vk = vk;
        _config = config;
        _tyres = tyres;
        _ground = ground;
        _figures = Figures.Of(config, arcsPerCar, firstConnector);
        var capacity = tyres.Capacity;
        var lanes = LineAssembler.MostLanes;

        var connectorAt = new uint[roads.LaneCount + 1];
        var lanePassable = new uint[roads.LaneCount];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var run = roads.ConnectorsFrom(lane);
            connectorAt[lane] = (uint)run.FirstId;
            connectorAt[lane + 1] = (uint)(run.FirstId + run.Count);
            lanePassable[lane] = Overtaking.IsALaneToPassFrom(roads, lane) ? 1u : 0u;
        }

        var connectorTo = new int[roads.ConnectorCount];
        var connectorRung = new uint[roads.ConnectorCount];
        var connectorLength = new float[roads.ConnectorCount];
        for (var connector = 0; connector < roads.ConnectorCount; connector++)
        {
            connectorTo[connector] = roads.ConnectorTo(connector);
            connectorRung[connector] = (uint)roads.FirmOnConnector(connector);
            connectorLength[connector] = roads.ConnectorLengthM(connector);
        }

        _ticks = new ResidentRows<TickRow>(vk, capacity, 1, readsBack: true);
        _told = new ResidentRows<ToldRow>(vk, capacity, 1);
        _grants = new ResidentRows<GrantRow>(vk, capacity, 1);
        _cutters = new ResidentRows<DeviceClaims.ClaimRow>(vk, capacity, 1);
        _boxes = new ResidentRows<BoxRow>(vk, capacity, 1, readsBack: true);
        _contexts = new ResidentRows<ContextRow>(vk, capacity, 1, readsBack: true);
        _lines = new ResidentRows<LineRow>(vk, capacity, 1, readsBack: true);
        _arcs = new ResidentRows<DeviceGround.ArcRow>(vk, capacity, arcsPerCar);
        _entries = new ResidentRows<float>(vk, capacity, arcsPerCar);
        _chain = new ResidentRows<int>(vk, capacity, lanes, readsBack: true);
        _starts = new ResidentRows<float>(vk, capacity, lanes, readsBack: true);
        _ends = new ResidentRows<float>(vk, capacity, lanes, readsBack: true);
        _firstArcs = new ResidentRows<uint>(vk, capacity, lanes);
        _breaks = new ResidentRows<uint>(vk, capacity, lanes);
        _taken = new ResidentRows<uint>(vk, capacity, 1);

        var table = tyres.Table;
        GpuBuffer[] onTheGround = [table.Poses.Device, table.Cars.Device, _lines.Device, _taken.Device, tyres.Effects, .. ground.Tables];
        _groundKernel = new ComputeKernel(vk, "car_ground.comp", onTheGround.Length, (uint)Marshal.SizeOf<DeviceGround.AskPush>());
        _groundSet = _groundKernel.Bind(onTheGround);

        GpuBuffer[] ticked =
        [
            table.Builds, table.BuildOf.Device, table.Poses.Device, table.Commands.Device, table.Cars.Device, _arcs.Device,
            _entries.Device, _chain.Device, _starts.Device, _ends.Device, _firstArcs.Device, _breaks.Device, _lines.Device,
            Laid(roads.LaneLengthM), Laid(connectorAt), Laid(connectorTo), Laid(connectorRung), Laid(connectorLength),
            Laid(lanePassable), claims.Rows, claims.PlannedRuns, claims.PlannedOrder, _cutters.Device, _grants.Device,
            _told.Device, _ticks.Device, _contexts.Device, _boxes.Device, _taken.Device,
        ];
        _tickKernel = new ComputeKernel(vk, "car_tick.comp", ticked.Length, (uint)Marshal.SizeOf<Figures>());
        _tickSet = _tickKernel.Bind(ticked);

        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, Enum.GetValues<Stage>().Length);
        ShadowOf(capacity);
    }

    /// <summary>The most cars the table holds: the tyre pass's.</summary>
    public int Capacity => _tyres.Capacity;

    /// <summary>The tyre pass the tick hands its commands to, whose table holds each car's pose, command and wheels.</summary>
    public DeviceTyres Tyres => _tyres;

    public ReadOnlySpan<TickRow> TicksAfter => _ticks.AllRead[.._cars];

    public ReadOnlySpan<LineRow> LinesAfter => _lines.AllRead[.._cars];

    public ReadOnlySpan<int> ChainAfter(int car) => _chain.RowsRead(car);

    public ReadOnlySpan<float> StartsAfter(int car) => _starts.RowsRead(car);

    public ReadOnlySpan<float> EndsAfter(int car) => _ends.RowsRead(car);

    public ReadOnlySpan<ContextRow> ContextsAfter => _contexts.AllRead[.._cars];

    public ReadOnlySpan<BoxRow> BoxesAfter => _boxes.AllRead[.._cars];

    public ReadOnlySpan<DeviceTyres.CommandRow> CommandsAfter => _tyres.CommandsAfter;

    /// <summary>The device's own time for the last tick's ground and drive, in milliseconds, with nothing sent or read in it.</summary>
    public double TickMs => _timer.BetweenMs((int)Stage.Sent, (int)Stage.Ticked);

    /// <summary>The device's own time for the last tick's tyre pass, in milliseconds.</summary>
    public double TyresMs => _timer.BetweenMs((int)Stage.Ticked, (int)Stage.Tyred);

    /// <summary>The device's time taking the last tick's rows in, in milliseconds.</summary>
    public double SentMs => _timer.BetweenMs((int)Stage.Began, (int)Stage.Sent);

    /// <summary>The device's time handing the last tick's answers back, in milliseconds — the shadow's alone.</summary>
    public double ReadMs => _timer.BetweenMs((int)Stage.Tyred, (int)Stage.Read);

    /// <summary>How many bytes the last tick took in, every table's.</summary>
    public ulong SentBytes { get; private set; }

    /// <summary>How many of them were lines' arcs and their entry figures.</summary>
    public ulong LinesSentBytes { get; private set; }

    /// <summary>
    /// <b>One tick</b>: what was marked sent, the ground and the drive run for the first <paramref name="taken"/> cars of
    /// the list, the tyres for the fleet's first <paramref name="cars"/>, and what they left read back and waited for.
    /// One submit and one wait, and a copy a table, whatever the count.
    /// </summary>
    public void Step(int taken, int cars)
    {
        if (cars > Capacity) throw new ArgumentOutOfRangeException(nameof(cars), $"The tick holds {Capacity} cars, not {cars}.");

        _cars = cars;
        var vk = _vk;
        var commands = _batch.Begin();
        _timer.Reset(commands);
        _timer.Stamp(commands, (int)Stage.Began);
        _taken.Mark(0, taken);
        var table = _tyres.Table;
        SentBytes = table.BuildOf.MarkedBytes + table.Poses.MarkedBytes + table.Commands.MarkedBytes + table.Cars.MarkedBytes
                    + table.Wheels.MarkedBytes + _ticks.MarkedBytes + _told.MarkedBytes + _grants.MarkedBytes + _cutters.MarkedBytes
                    + _boxes.MarkedBytes + _lines.MarkedBytes + _arcs.MarkedBytes + _entries.MarkedBytes + _chain.MarkedBytes
                    + _starts.MarkedBytes + _ends.MarkedBytes + _firstArcs.MarkedBytes + _breaks.MarkedBytes + _taken.MarkedBytes;
        LinesSentBytes = _arcs.MarkedBytes + _entries.MarkedBytes;
        _tyres.SendMarked(commands);
        _ticks.Send(commands);
        _told.Send(commands);
        _grants.Send(commands);
        _cutters.Send(commands);
        _boxes.Send(commands);
        _lines.Send(commands);
        _arcs.Send(commands);
        _entries.Send(commands);
        _chain.Send(commands);
        _starts.Send(commands);
        _ends.Send(commands);
        _firstArcs.Send(commands);
        _breaks.Send(commands);
        _taken.Send(commands);
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Sent);

        var groups = (uint)Math.Max(1, (taken + 63) / 64);
        _groundKernel.Dispatch(commands, _groundSet, groups, _ground.PushFor(taken));
        vk.BetweenKernels(commands);
        _tickKernel.Dispatch(commands, _tickSet, groups, _figures with { CarCount = (uint)taken });
        vk.BetweenKernels(commands);
        _timer.Stamp(commands, (int)Stage.Ticked);

        _tyres.Run(commands, cars);
        vk.BeforeTransfer(commands);
        _timer.Stamp(commands, (int)Stage.Tyred);

        _tyres.Read(commands);
        _ticks.Read(commands, cars);
        _lines.Read(commands, cars);
        _chain.Read(commands, cars);
        _starts.Read(commands, cars);
        _ends.Read(commands, cars);
        _contexts.Read(commands, cars);
        _boxes.Read(commands, cars);
        _timer.Stamp(commands, (int)Stage.Read);
        _batch.SubmitAndWait();
        _timer.Read();
    }

    /// <summary>A device-local buffer holding these rows, sent once.</summary>
    GpuBuffer Laid<T>(T[] rows) where T : unmanaged
    {
        var buffer = _vk.CreateBuffer(
            (ulong)(Math.Max(1, rows.Length) * Marshal.SizeOf<T>()), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None);
        _laid.Add(buffer);
        buffer.Upload<T>(rows.Length, into => rows.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _batch.Dispose();
        _tickKernel.Dispose();
        _groundKernel.Dispose();
        foreach (var table in (ReadOnlySpan<IDisposable>)[
                     _ticks, _told, _grants, _cutters, _boxes, _contexts, _lines, _arcs, _entries, _chain, _starts, _ends, _firstArcs,
                     _breaks, _taken])
        {
            table.Dispose();
        }

        foreach (var buffer in _laid) buffer.Dispose();
    }
}
