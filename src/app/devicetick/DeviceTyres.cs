using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Terrain;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>A whole car's tyre pass on the device</b> (<c>agents/car/body/kernels/tyre_pass.glsl</c>, run by
/// <c>tyre_pass.comp</c>, one thread a car): the cars' rows sent — which of each car's wheels stood on a way when its
/// body was last laid among them — the pass run over what a wheel on a way and off one is worth, laid once, and the
/// marks it laid laid into the ground's ring on the device (<see cref="GroundMarks"/>).
/// </summary>
/// <remarks>
/// <b>The car table stays on the device</b> (<see cref="Table"/>): <see cref="DeviceWorld"/> records the pass into its own
/// submit (<see cref="SendMarked"/>, <see cref="Run"/>) between kernels that read and write the same rows, and reads none
/// of it back. <see cref="Step"/> hands every row back instead, the marks too as records filed in (car, wheel) order by
/// <see cref="MarksLaid()"/>, so a test can ask the kernel and <see cref="TyrePass.Step"/> one question.
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

        /// <param name="wheelsOnAWay">Which of its wheels stood on a way when its body was last laid (<see cref="World.Town.CarView.WheelsOnAWay"/>).</param>
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
        float DtS, float WheelSpinFadeMps, float WheelSpinAllowanceMps, float MarkSlipMps, float MarkCorneringSlipMps,
        float PloughCrawlMps, float EngineBrakingMps2, float SlipBackOffPerTick, float SlipRecoverPerTick,
        float MinSlipThrottleFraction, float TractionThrottleFraction, float NominalGripMps2, float MarkOnsetM,
        float MarkPloughFloor, float MarkSpacingM, float WorkingSlipTangent, float AlongSlipEpsilonMps,
        float LiftedAxleLoadShare, float AckermannLeastSteerRad, float InnerWheelLeastRadiusM)
    {
        public static Figures Of(SimConfig config) => new(
            config.TickSeconds, config.Tyre.WheelSpinFadeMps, config.Tyre.WheelSpinAllowanceMps, config.Marks.SlipMps,
            config.Marks.CorneringSlipMps, config.Marks.PloughCrawlMps, config.CarEngineBrakingMps2,
            config.TyreSlipBackOffPerTick, config.TyreSlipRecoverPerTick, config.Tyre.MinSlipThrottleFraction,
            config.Tyre.TractionThrottleFraction, config.TyreGripMps2, config.Marks.OnsetM, config.Marks.PloughFloor,
            config.Marks.SpacingM, config.Tyre.WorkingSlipTangent, config.Tyre.AlongSlipEpsilonMps,
            config.Tyre.LiftedAxleLoadShare, config.Tyre.AckermannLeastSteerRad, config.Tyre.InnerWheelLeastRadiusM);
    }

    /// <summary>The kernel's push constants: the figures, then how many cars and whether the scrub is written.</summary>
    /// <remarks>The figures lead because a page binds this block as a uniform, where a struct member must start on 16
    /// bytes (tyre_pass.comp).</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PassPush
    {
        public Figures Figures;
        public uint CarCount;
        public uint HandsBack;
    }

    const int WheelsACar = TyreModel.Wheels;

    readonly Vk _vk;
    readonly Figures _figures;
    readonly DeviceHoldings _held;
    readonly ComputeKernel _kernel;
    readonly BoundSet _set;
    readonly ComputeBatch _batch;
    readonly ResidentRows<uint> _buildOf;
    readonly ResidentRows<PoseRow> _poses;
    readonly ResidentRows<CommandRow> _commands;
    readonly ResidentRows<CarRow> _carRows;
    readonly ResidentRows<WheelRow> _wheels;
    readonly ResidentRows<Vector4> _impulses;
    readonly ResidentRows<ScrubRow> _scrubs;
    readonly GpuBuffer _markCountOut;
    readonly GpuBuffer _markCount;

    /// <summary>
    /// The marks a pass laid as records of their car and wheel, written by the kernel straight into memory the host reads
    /// — a test's (<see cref="MarksLaid()"/>), and a row long where nobody asks.
    /// </summary>
    readonly GpuBuffer _marks;
    readonly bool _handsBack;
    int _cars;

    /// <param name="grounds">What a wheel on a way and off one stands on, laid once.</param>
    /// <param name="builds">Every build a car may be, indexed as <see cref="BuildOf"/> names them.</param>
    /// <param name="capacity">The most cars a pass is asked of; a pass that hands back keeps a mark record for every wheel of them.</param>
    /// <param name="handsBack">
    /// Whether a pass hands its rows and each patch's scrub back to the host (<see cref="Step"/>) — a test's question: the
    /// town reads only the marks.
    /// </param>
    public DeviceTyres(Vk vk, SimConfig config, in WheelGrounds grounds, ReadOnlySpan<CarBuild> builds, int capacity, bool handsBack = false)
    {
        _vk = vk;
        _held = new DeviceHoldings(vk);
        _figures = Figures.Of(config);
        _handsBack = handsBack;
        Capacity = capacity;

        var wheels = capacity * WheelsACar;
        var buildBuffer = _held.Laid<CarBuildRow>(CarBuildRow.Of(builds));
        var wheelGroundsBuffer = _held.Laid<SurfaceRow>([SurfaceRow.Of(grounds.OffAWay), SurfaceRow.Of(grounds.OnAWay)]);
        WheelGrounds = wheelGroundsBuffer;
        _buildOf = new ResidentRows<uint>(vk, capacity, 1, hostOnly: true);
        _poses = new ResidentRows<PoseRow>(vk, capacity, 1);
        _commands = new ResidentRows<CommandRow>(vk, capacity, 1);
        _carRows = new ResidentRows<CarRow>(vk, capacity, 1, readsBack: handsBack);
        _wheels = new ResidentRows<WheelRow>(vk, capacity, WheelsACar, readsBack: handsBack);
        _impulses = new ResidentRows<Vector4>(vk, capacity, WheelsACar, readsBack: handsBack);
        _scrubs = new ResidentRows<ScrubRow>(vk, capacity, WheelsACar, readsBack: handsBack);
        _markCount = _held.Device<uint>(1);
        _marks = _held.Mapped<MarkRow>(handsBack ? wheels : 1);
        _markCountOut = _held.Readback<uint>(1);

        var drawnCapacity = Math.Max(1, config.Marks.Capacity);
        GroundMarks = _held.Own(_vk.CreateBuffer(Bytes<DriftMarkRow>(drawnCapacity), BufferUse.Storage, HostAccess.None));
        GroundMarkRing = _held.Own(_vk.CreateBuffer(
            Bytes<DriftMarkRing>(1), BufferUse.Storage | BufferUse.Indirect | BufferUse.TransferDst, HostAccess.None));
        GroundMarkRing.Upload<DriftMarkRing>(1, into => into[0] = DriftMarkRing.After(0, drawnCapacity));

        Table = (buildBuffer, _buildOf, _poses, _commands, _carRows, _wheels);
        GpuBuffer[] bound =
        [
            buildBuffer, _buildOf.Device, _poses.Device, _commands.Device, _carRows.Device, _wheels.Device, _impulses.Device,
            _scrubs.Device, _markCount, _marks, wheelGroundsBuffer, GroundMarks, GroundMarkRing,
        ];
        _kernel = new ComputeKernel(vk, "tyre_pass.comp", bound.Length, (uint)Marshal.SizeOf<PassPush>());
        _set = _kernel.Bind(bound);
        _batch = new ComputeBatch(vk);
    }

    /// <summary>The most cars a pass is asked of.</summary>
    public int Capacity { get; }

    /// <summary>
    /// <b>The ground's ring of marks, on the device and drawn from there</b> (<see cref="DriftMarkRow"/>,
    /// <c>drift_marks.glsl</c>): every pass lays its marks into it, and nothing of it crosses to the host.
    /// </summary>
    public GpuBuffer GroundMarks { get; }

    /// <summary>
    /// And its words (<see cref="DriftMarkRing"/>), whose head a pass after the tyres' moves past the tick's marks
    /// (<c>world_view.comp</c>) — the indirect buffer of a frame's draw of the ring.
    /// </summary>
    public GpuBuffer GroundMarkRing { get; }

    /// <summary>What a wheel off every way and on one stands on (<c>TyreSurface</c>), laid once, for a kernel binding it too.</summary>
    public GpuBuffer WheelGrounds { get; }

    /// <summary>
    /// The figures retuned under a standing town: every build and both grounds laid again over what the pass reads, with
    /// nothing in flight.
    /// </summary>
    public void Retune(in WheelGrounds grounds, ReadOnlySpan<CarBuild> builds)
    {
        var buildRows = CarBuildRow.Of(builds);
        Table.Builds.Upload<CarBuildRow>(buildRows.Length, into => buildRows.CopyTo(into));
        SurfaceRow[] surfaces = [SurfaceRow.Of(grounds.OffAWay), SurfaceRow.Of(grounds.OnAWay)];
        WheelGrounds.Upload<SurfaceRow>(surfaces.Length, into => surfaces.CopyTo(into));
    }

    /// <summary>
    /// <b>The car table the pass reads and writes, kept on the device from one pass to the next</b> — the builds, which
    /// each car is, its pose, its command, its own row and its wheels — for a caller that marks only the cars it changed
    /// and runs kernels around the pass in the same submit (<see cref="DeviceWorld"/>).
    /// </summary>
    public (GpuBuffer Builds, ResidentRows<uint> BuildOf, ResidentRows<PoseRow> Poses, ResidentRows<CommandRow> Commands,
        ResidentRows<CarRow> Cars, ResidentRows<WheelRow> Wheels) Table { get; }

    /// <summary>How many marks the last pass laid, copied back where <see cref="MarkCount"/> reads it, recorded; the caller barriers before it.</summary>
    void RecordReadTheMarkCount(Recording commands) => _vk.Copy(commands, _markCount, _markCountOut, sizeof(uint));

    /// <summary>
    /// Each wheel's impulse and where it is spent, as the pass leaves it on the device — four a car in the tyre model's
    /// order, (impulse, at) — for the kernel that spends them (<c>world_tow.comp</c>).
    /// </summary>
    public GpuBuffer ImpulseTable => _impulses.Device;

    /// <summary>Which of the builds each car is, written before a <see cref="Step"/>.</summary>
    public Span<uint> BuildOf => _buildOf.All;

    public Span<PoseRow> Poses => _poses.All;

    public Span<CommandRow> Commands => _commands.All;

    /// <summary>Each car's own figures and the throttle it carries, as the pass reads them.</summary>
    public Span<CarRow> Cars => _carRows.All;

    /// <summary>Each wheel's carried state, four to a car in the tyre model's order.</summary>
    public Span<WheelRow> Wheels => _wheels.All;

    /// <summary>The cars as the last pass left them: the throttle wound on, and whether each was slipping or at rest.</summary>
    public ReadOnlySpan<CarRow> CarsAfter => _carRows.AllRead[.._cars];

    /// <summary>The wheels as the last pass left them: rims wound on, and the mark state.</summary>
    public ReadOnlySpan<WheelRow> WheelsAfter => _wheels.AllRead[..(_cars * WheelsACar)];

    /// <summary>Each wheel's impulse and where it is spent: (impulse, at).</summary>
    public ReadOnlySpan<Vector4> Impulses => _impulses.AllRead[..(_cars * WheelsACar)];

    public ReadOnlySpan<ScrubRow> Scrub => _scrubs.AllRead[..(_cars * WheelsACar)];

    /// <summary>How many marks the last <see cref="Step"/> laid, which may be more than the records hold (<see cref="Capacity"/> × 4).</summary>
    public int MarkCount => (int)_markCountOut.Span<uint>()[0];

    /// <summary>
    /// The marks the last <see cref="Step"/> laid, filed in (car, wheel) order — which is the order the town lays them in
    /// when it steps its cars in order — and each car's are in wheel order as <see cref="TyrePass.Step"/> lays them. Only a
    /// pass that hands back keeps these records.
    /// </summary>
    public ReadOnlySpan<MarkRow> MarksLaid()
    {
        var laid = _marks.Span<MarkRow>()[..Math.Min(MarkCount, _handsBack ? Capacity * WheelsACar : 0)];
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
        Send(commands, cars);
        _vk.AfterTransfer(commands);
        Run(commands, cars);
        _vk.BeforeTransfer(commands);
        Read(commands);
        _batch.SubmitAndWait();
    }

    /// <summary>The rows written for the first <paramref name="cars"/> cars taken in and the ring emptied, recorded; the caller barriers after.</summary>
    void Send(Recording commands, int cars)
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
    public void SendMarked(Recording commands)
    {
        _buildOf.Send(commands);
        _poses.Send(commands);
        _commands.Send(commands);
        _carRows.Send(commands);
        _wheels.Send(commands);
        _vk.Fill(commands, _markCount, 0);
    }

    /// <summary>The pass over the first <paramref name="cars"/> cars of the table, recorded; the caller barriers after.</summary>
    public void Run(Recording commands, int cars)
    {
        _cars = cars;
        var push = new PassPush { CarCount = (uint)cars, HandsBack = _handsBack ? 1u : 0u, Figures = _figures };
        _kernel.Dispatch(commands, _set, DeviceHoldings.Groups(cars), push);
    }

    /// <summary>What the last <see cref="Run"/> wrote, recorded to be handed back where <see cref="CarsAfter"/> and the rest read it.</summary>
    void Read(Recording commands)
    {
        _carRows.Read(commands, _cars);
        _wheels.Read(commands, _cars);
        _impulses.Read(commands, _cars);
        _scrubs.Read(commands, _cars);
        RecordReadTheMarkCount(commands);
    }

    public void Dispose()
    {
        _batch.Dispose();
        _kernel.Dispose();
        _buildOf.Dispose();
        _poses.Dispose();
        _commands.Dispose();
        _carRows.Dispose();
        _wheels.Dispose();
        _impulses.Dispose();
        _scrubs.Dispose();
        _held.Dispose();
    }
}
