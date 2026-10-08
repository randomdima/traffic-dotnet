using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Physics;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>A whole car's tyre pass on the device</b> (<c>agents/car/body/kernels/tyre_pass.glsl</c>, run by
/// <c>tyre_pass.comp</c>, one thread a car): the cars' rows sent — which of each car's wheels stood on a way when its
/// body was last laid among them — the pass run over what a wheel on a way and off one is worth, laid once, and what it
/// wrote read back — the marks it laid as a ring of records, filed in (car, wheel) order by <see cref="MarksLaid"/>.
/// </summary>
/// <remarks>
/// <b>Run in shadow</b>: every pass is handed the host's own state for its cars, so <see cref="TyrePass.Step"/> and the
/// kernel are asked one question. What the state is sent from and read back into is mapped memory beside the device's
/// own, and <see cref="PassMs"/> is the kernel alone.
/// </remarks>
internal sealed class DeviceTyres : IDisposable
{
    /// <summary>The body at the start of the tick (<c>TyrePose</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct PoseRow(
        Vector2 PositionM, Vector2 VelocityMps, Vector2 Forward, Vector2 AccelerationMps2, float HeadingRad, float YawRateRadPerS,
        float MassKg, float Pad)
    {
        public static PoseRow Of(in CarPose pose) => new(
            pose.PositionM, pose.VelocityMps, pose.Forward, pose.AccelerationMps2, pose.HeadingRad, pose.YawRateRadPerS, pose.MassKg, 0f);

        /// <summary>A car's pose as the fleet holds it.</summary>
        public static PoseRow Of(CarFleet cars, int car) => Of(new CarPose(
            cars.PositionM[car], cars.HeadingRad[car], cars.VelocityMps[car], cars.YawRateRadPerS[car], cars.MassKg[car],
            cars.AccelerationMps2[car]));
    }

    /// <summary>The pedals and the wheel (<c>TyreCommand</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CommandRow(float SteerRad, float ThrottleMps2, float BrakeMps2, uint HandbrakeAndReverse)
    {
        public static CommandRow Of(in DriveCommand command) =>
            new(command.SteerRad, command.ThrottleMps2, command.BrakeMps2, (command.Handbrake ? 1u : 0u) | (command.Reverse ? 2u : 0u));
    }

    /// <summary>One car's own figures and flags (<c>TyreCarRow</c>): <see cref="Flags"/> holds what <see cref="FlagsOf"/> packs.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct CarRow(float InertiaKgM2, float SlipThrottle, uint Flags)
    {
        public const uint Hand = 1u, DrivenSlipping = 2u, AtRest = 4u;

        /// <param name="wheelsOnAWay">Which of its wheels stood on a way when its body was last laid (<see cref="CarFleet.WheelsOnAWay"/>).</param>
        public static uint FlagsOf(bool hand, bool drivenSlipping, byte wheelsOnAWay) =>
            (hand ? Hand : 0u) | (drivenSlipping ? DrivenSlipping : 0u) | ((uint)wheelsOnAWay << 8);
    }

    /// <summary>One wheel's carried state (<c>TyreWheelRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WheelRow(Vector2 MarkFromM, float SpinMps, float ScrubTravelM, float MarkIntensity, uint Marking);

    /// <summary>The ground under one patch (<c>TyreSurface</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct SurfaceRow(float Coefficient, float DragMps2, float MarkThresholdM2S3, uint Ploughs)
    {
        public static SurfaceRow Of(in SurfaceUnderWheel surface) =>
            new(surface.Coefficient, surface.DragMps2, surface.MarkThresholdM2S3, surface.Ploughs ? 1u : 0u);
    }

    /// <summary>What one patch did to its ground (<c>TyreScrub</c>): <see cref="Flags"/> is ploughing 1, sliding 2.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ScrubRow(float SlidePowerM2S3, float PloughPowerM2S3, float SlideSpeedMps, uint Flags)
    {
        public const uint Ploughing = 1u, Sliding = 2u;
    }

    /// <summary>One mark laid (<c>TyreMarkRow</c>): <see cref="CarWheel"/> is the car times four plus the wheel.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct MarkRow(Vector2 FromM, Vector2 ToM, float WidthM, float Intensity, uint CarWheel, uint Ploughed);

    /// <summary>The pass's figures (<c>TyrePassFigures</c>), field for field, each the host's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Figures(
        float DtS, float StandardGravityMps2, float WheelSpinFadeMps, float WheelSpinAllowanceMps, float MarkSlipMps,
        float MarkCorneringSlipMps, float PloughCrawlMps, float EngineBrakingMps2, float SlipBackOffS, float SlipRecoverS,
        float MinSlipThrottleFraction, float TractionThrottleFraction, float NominalGripMps2, float MarkOnsetM,
        float MarkPloughFloor, float MarkSpacingM)
    {
        public static Figures Of(SimConfig config) => new(
            config.TickSeconds, config.Tyre.StandardGravityMps2, config.Tyre.WheelSpinFadeMps, config.Tyre.WheelSpinAllowanceMps,
            config.Marks.SlipMps, config.Marks.CorneringSlipMps, config.Marks.PloughCrawlMps, config.CarEngineBrakingMps2,
            config.Tyre.SlipBackOffS, config.Tyre.SlipRecoverS, config.Tyre.MinSlipThrottleFraction,
            config.Tyre.TractionThrottleFraction, config.TyreGripMps2, config.Marks.OnsetM, config.Marks.PloughFloor,
            config.Marks.SpacingM);
    }

    /// <summary>The kernel's push constants: how many cars, then the figures.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PassPush
    {
        public uint CarCount;
        public Figures Figures;
    }

    const int WheelsACar = TyreModel.Wheels;

    readonly Vk _vk;
    readonly Figures _figures;
    readonly List<GpuBuffer> _owned = [];
    readonly ComputeKernel _kernel;
    readonly DescriptorSet _set;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    readonly ResidentRows<uint> _buildOf;
    readonly ResidentRows<PoseRow> _poses;
    readonly ResidentRows<CommandRow> _commands;
    readonly ResidentRows<CarRow> _carRows;
    readonly ResidentRows<WheelRow> _wheels;
    readonly ResidentRows<Vector4> _impulses;
    readonly ResidentRows<ScrubRow> _scrubs;
    readonly GpuBuffer _markCountOut;
    readonly GpuBuffer _markCount;
    readonly BufferCopy[] _handOverRuns;

    /// <summary>
    /// The ring of marks, written by the kernel straight into memory the host reads: a tick lays tens of the fleet's
    /// thousands of wheels, and a copy of the whole ring would move every one of them across the bus.
    /// </summary>
    readonly GpuBuffer _marks;
    int _cars;

    /// <param name="grounds">What a wheel on a way and off one stands on, laid once.</param>
    /// <param name="builds">Every build a car may be, indexed as <see cref="BuildOf"/> names them.</param>
    /// <param name="capacity">The most cars a pass is asked of; the ring holds a mark for every wheel of them.</param>
    public DeviceTyres(Vk vk, SimConfig config, in WheelGrounds grounds, ReadOnlySpan<CarBuild> builds, int capacity)
    {
        _vk = vk;
        _figures = Figures.Of(config);
        Capacity = capacity;

        var buildRows = new CarBuildRow[builds.Length];
        for (var build = 0; build < buildRows.Length; build++) buildRows[build] = CarBuildRow.Of(builds[build]);

        var wheels = capacity * WheelsACar;
        var buildBuffer = Laid<CarBuildRow>(buildRows);
        var wheelGroundsBuffer = Laid<SurfaceRow>([SurfaceRow.Of(grounds.OffAWay), SurfaceRow.Of(grounds.OnAWay)]);
        _buildOf = new ResidentRows<uint>(vk, capacity, 1, hostOnly: true);
        _poses = new ResidentRows<PoseRow>(vk, capacity, 1);
        _commands = new ResidentRows<CommandRow>(vk, capacity, 1, readsBack: true);
        _carRows = new ResidentRows<CarRow>(vk, capacity, 1, readsBack: true);
        _wheels = new ResidentRows<WheelRow>(vk, capacity, WheelsACar, readsBack: true);
        _impulses = new ResidentRows<Vector4>(vk, capacity, WheelsACar, readsBack: true);
        _scrubs = new ResidentRows<ScrubRow>(vk, capacity, WheelsACar, readsBack: true);
        _markCount = Device<uint>(1);
        _marks = Own(_vk.CreateBuffer(Bytes<MarkRow>(wheels), BufferUsageFlags.StorageBufferBit, HostAccess.Reads));
        _markCountOut = Readback<uint>(1);
        _handOverRuns = new BufferCopy[capacity];
        KeptOnTheDevice = new bool[capacity];

        Table = (buildBuffer, _buildOf, _poses, _commands, _carRows, _wheels);
        GpuBuffer[] bound =
        [
            buildBuffer, _buildOf.Device, _poses.Device, _commands.Device, _carRows.Device, _wheels.Device, _impulses.Device,
            _scrubs.Device, _markCount, _marks, wheelGroundsBuffer,
        ];
        _kernel = new ComputeKernel(vk, "tyre_pass.comp", bound.Length, (uint)Marshal.SizeOf<PassPush>());
        _set = _kernel.Bind(bound);
        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, 4);
    }

    /// <summary>The most cars a pass is asked of.</summary>
    public int Capacity { get; }

    /// <summary>
    /// <b>The car table the pass reads and writes, kept on the device from one pass to the next</b> — the builds, which
    /// each car is, its pose, its command, its own row and its wheels — for a caller that marks only the cars it changed
    /// and runs kernels before the pass in the same submit (<see cref="DeviceCarTick"/>).
    /// </summary>
    public (GpuBuffer Builds, ResidentRows<uint> BuildOf, ResidentRows<PoseRow> Poses, ResidentRows<CommandRow> Commands,
        ResidentRows<CarRow> Cars, ResidentRows<WheelRow> Wheels) Table { get; }

    /// <summary>
    /// Whether <see cref="Read"/> hands back what each wheel did to its ground — scratch to a town whose tick the device
    /// runs, and the shadow's to compare.
    /// </summary>
    public bool ReadsScrub { get; set; } = true;

    /// <summary>
    /// Whether <see cref="Read"/> hands back every car's wheels and impulses. <b>A town whose tick the device runs keeps
    /// them there</b>: the bodies' phase spends the impulses where the pass left them, and a car's wheels go back to the
    /// host only once the car leaves the device's tick (<see cref="RecordHandOver"/>, <see cref="KeptOnTheDevice"/>).
    /// </summary>
    public bool ReadsEveryWheel { get; set; } = true;

    /// <summary>
    /// <b>Which cars' wheels the device holds newer than the host's copy</b>: those a pass ran for while
    /// <see cref="ReadsEveryWheel"/> is off, until they are handed back. Kept by whoever reads the pass; a car marked here
    /// is never laid from the host's copy.
    /// </summary>
    public bool[] KeptOnTheDevice { get; }

    /// <summary>
    /// <b>Those cars' wheels copied back</b> where <see cref="WheelsAfter"/> reads them, recorded — the wheels as the last
    /// pass left them, which nothing on the device writes until the next. The caller barriers before it.
    /// </summary>
    public void RecordHandOver(CommandBuffer commands, ReadOnlySpan<int> cars) => _wheels.ReadItems(commands, cars, _handOverRuns);

    /// <summary>
    /// Those cars' commands and own rows copied back where <see cref="CommandRead"/> and <see cref="CarRead"/> read them,
    /// recorded — for a reader that does not <see cref="Read"/> every car's. The caller barriers before it.
    /// </summary>
    public void RecordHandOverOfTheRows(CommandBuffer commands, ReadOnlySpan<int> cars)
    {
        _commands.ReadItems(commands, cars, _handOverRuns);
        _carRows.ReadItems(commands, cars, _handOverRuns);
    }

    /// <summary>A car's command as the last copy back of it left it.</summary>
    public CommandRow CommandRead(int car) => _commands.AllRead[car];

    /// <summary>A car's own row as the last copy back of it left it.</summary>
    public CarRow CarRead(int car) => _carRows.AllRead[car];

    /// <summary>How many marks the last pass laid, copied back where <see cref="MarkCount"/> reads it, recorded; the caller barriers before it.</summary>
    public void RecordReadTheMarkCount(CommandBuffer commands) => _vk.Copy(commands, _markCount, _markCountOut, sizeof(uint));

    /// <summary>
    /// Each wheel's impulse and where it is spent, as the pass leaves it on the device — four a car in the tyre model's
    /// order, (impulse, at) — for the bodies' phase that spends them (<see cref="DeviceBodyPhase"/>).
    /// </summary>
    public GpuBuffer ImpulseTable => _impulses.Device;

    /// <summary>Which of the builds each car is, written before a <see cref="Step"/>.</summary>
    public Span<uint> BuildOf => _buildOf.All;

    public Span<PoseRow> Poses => _poses.All;

    /// <summary>
    /// <b>The pose of every car whose body the town changed since the solver's last step</b> — put down, reweighed, frozen
    /// as the step ended — laid again from the fleet, which the town wrote with it, and marked to be sent.
    /// </summary>
    public void LayThePlacedPoses(CarFleet cars, PhysicsWorld physics)
    {
        foreach (var body in physics.Changes)
        {
            var tag = physics.TagOf(new BodyId(body + 1));
            if (tag.Kind != BodyKind.Car || tag.Index < 0 || tag.Index >= cars.Count) continue;

            _poses.All[tag.Index] = PoseRow.Of(cars, tag.Index);
            _poses.Mark(tag.Index);
        }
    }

    public Span<CommandRow> Commands => _commands.All;

    /// <summary>Each car's own figures and the throttle it carries, as the pass reads them.</summary>
    public Span<CarRow> Cars => _carRows.All;

    /// <summary>Each wheel's carried state, four to a car in the tyre model's order.</summary>
    public Span<WheelRow> Wheels => _wheels.All;

    /// <summary>The commands as the last read left them.</summary>
    public ReadOnlySpan<CommandRow> CommandsAfter => _commands.AllRead[.._cars];

    /// <summary>The cars as the last pass left them: the throttle wound on, and whether each was slipping or at rest.</summary>
    public ReadOnlySpan<CarRow> CarsAfter => _carRows.AllRead[.._cars];

    /// <summary>The wheels as the last pass left them: rims wound on, and the mark state.</summary>
    public ReadOnlySpan<WheelRow> WheelsAfter => _wheels.AllRead[..(_cars * WheelsACar)];

    /// <summary>Each wheel's impulse and where it is spent: (impulse, at).</summary>
    public ReadOnlySpan<Vector4> Impulses => _impulses.AllRead[..(_cars * WheelsACar)];

    public ReadOnlySpan<ScrubRow> Scrub => _scrubs.AllRead[..(_cars * WheelsACar)];

    /// <summary>How many marks the last pass laid, which may be more than the ring held (<see cref="Capacity"/> × 4).</summary>
    public int MarkCount => (int)_markCountOut.Span<uint>()[0];

    /// <summary>The device's own time for the last pass's kernel, in milliseconds, with nothing sent or read in it.</summary>
    public double PassMs => _timer.BetweenMs(1, 2);

    /// <summary>The device's time spent taking the last pass's rows in, and handing its answers back, in milliseconds.</summary>
    public double CopiesMs => _timer.BetweenMs(0, 1) + _timer.BetweenMs(2, 3);

    /// <summary>
    /// The marks the last pass laid, filed in (car, wheel) order — which is the order the town lays them in when it
    /// steps its cars in order — and each car's are in wheel order as <see cref="TyrePass.Step"/> lays them.
    /// </summary>
    public ReadOnlySpan<MarkRow> MarksLaid()
    {
        var laid = _marks.Span<MarkRow>()[..Math.Min(MarkCount, Capacity * WheelsACar)];
        laid.Sort(static (a, b) => a.CarWheel.CompareTo(b.CarWheel));
        return laid;
    }

    /// <summary>
    /// One pass over the first <paramref name="cars"/> cars of what was written: sent, run, read back and waited for.
    /// One submit and one wait, whatever the count.
    /// </summary>
    public void Step(int cars)
    {
        var commands = _batch.Begin();
        _timer.Reset(commands);
        _timer.Stamp(commands, 0);
        Send(commands, cars);
        _vk.AfterTransfer(commands);
        _timer.Stamp(commands, 1);

        Run(commands, cars);
        _vk.BeforeTransfer(commands);
        _timer.Stamp(commands, 2);

        Read(commands);
        _timer.Stamp(commands, 3);
        _batch.SubmitAndWait();
        _timer.Read();
    }

    /// <summary>The rows written for the first <paramref name="cars"/> cars taken in and the ring emptied, recorded; the caller barriers after.</summary>
    public void Send(CommandBuffer commands, int cars)
    {
        if (cars > Capacity) throw new ArgumentOutOfRangeException(nameof(cars), $"The pass holds {Capacity} cars, not {cars}.");

        _buildOf.Mark(0, cars);
        _poses.Mark(0, cars);
        _commands.Mark(0, cars);
        _carRows.Mark(0, cars);
        _wheels.Mark(0, cars);
        SendMarked(commands);
    }

    /// <summary>Only the rows marked in <see cref="Table"/> taken in and the ring emptied, recorded; the caller barriers after.</summary>
    public void SendMarked(CommandBuffer commands)
    {
        _buildOf.Send(commands);
        _poses.Send(commands);
        _commands.Send(commands);
        _carRows.Send(commands);
        _wheels.Send(commands);
        _vk.Fill(commands, _markCount, 0);
    }

    /// <summary>The pass over the first <paramref name="cars"/> cars of the table, recorded; the caller barriers after.</summary>
    public void Run(CommandBuffer commands, int cars)
    {
        _cars = cars;
        var push = new PassPush { CarCount = (uint)cars, Figures = _figures };
        _kernel.Dispatch(commands, _set, (uint)Math.Max(1, (cars + 63) / 64), push);
    }

    /// <summary>What the last <see cref="Run"/> wrote, recorded to be handed back where <see cref="CarsAfter"/> and the rest read it.</summary>
    public void Read(CommandBuffer commands)
    {
        _commands.Read(commands, _cars);
        _carRows.Read(commands, _cars);
        if (ReadsEveryWheel)
        {
            _wheels.Read(commands, _cars);
            _impulses.Read(commands, _cars);
        }

        if (ReadsScrub) _scrubs.Read(commands, _cars);

        RecordReadTheMarkCount(commands);
    }

    GpuBuffer Device<T>(int count) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None));

    GpuBuffer Readback<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferDstBit, HostAccess.Reads));

    /// <summary>A device-local buffer holding these rows, sent once.</summary>
    GpuBuffer Laid<T>(T[] rows) where T : unmanaged
    {
        var buffer = Own(_vk.CreateBuffer(
            Bytes<T>(rows.Length), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None));
        buffer.Upload<T>(rows.Length, into => rows.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        return buffer;
    }

    static unsafe ulong Bytes<T>(int count) where T : unmanaged => (ulong)(Math.Max(1, count) * sizeof(T));

    GpuBuffer Own(GpuBuffer buffer)
    {
        _owned.Add(buffer);
        return buffer;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _batch.Dispose();
        _kernel.Dispose();
        _buildOf.Dispose();
        _poses.Dispose();
        _commands.Dispose();
        _carRows.Dispose();
        _wheels.Dispose();
        _impulses.Dispose();
        _scrubs.Dispose();
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
