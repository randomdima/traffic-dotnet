using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Terrain;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>A whole car's tyre pass on the device</b> (<c>agents/car/body/kernels/tyre_pass.glsl</c>, run by
/// <c>tyre_pass.comp</c>, one thread a car): the cars' rows sent, the pass run over the ground's own tables
/// (<see cref="DeviceGround"/>) and the kinds' effects laid once, and what it wrote read back — the marks it laid as a
/// ring of records, filed in (car, wheel) order by <see cref="MarksLaid"/>.
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
    internal readonly record struct CarRow(float GroundCoefficient, float InertiaKgM2, float SlipThrottle, uint Flags)
    {
        public const uint Hand = 1u, DrivenSlipping = 2u, AtRest = 4u;

        public static uint FlagsOf(bool hand, bool drivenSlipping, byte level) =>
            (hand ? Hand : 0u) | (drivenSlipping ? DrivenSlipping : 0u) | ((uint)level << 8);
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
    internal readonly record struct ScrubRow(float SlidePowerM2S3, float PloughPowerM2S3, float SlideSpeedMps, uint Flags);

    /// <summary>One mark laid (<c>TyreMarkRow</c>): <see cref="CarWheel"/> is the car times four plus the wheel.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct MarkRow(Vector2 FromM, Vector2 ToM, float WidthM, float Intensity, uint CarWheel, uint Ploughed);

    /// <summary>What a kind of ground does to a wheel on it (<c>world/terrain/kernels/ground_effect.glsl</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct GroundEffectRow(float Coefficient, float DragMps2, float MarkFactor, uint Ploughs);

    /// <summary>The pass's figures (<c>TyrePassFigures</c>), field for field, each the host's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Figures(
        float DtS, float StandardGravityMps2, float WheelSpinFadeMps, float WheelSpinAllowanceMps, float MarkSlipMps,
        float MarkCorneringSlipMps, float PloughCrawlMps, float EngineBrakingMps2, float SlipBackOffS, float SlipRecoverS,
        float MinSlipThrottleFraction, float TractionThrottleFraction, float NominalGripMps2, float MarkPowerM2S3,
        float MarkOnsetM, float MarkPloughFloor, float MarkSpacingM)
    {
        public static Figures Of(SimConfig config) => new(
            config.TickSeconds, config.Tyre.StandardGravityMps2, config.Tyre.WheelSpinFadeMps, config.Tyre.WheelSpinAllowanceMps,
            config.Marks.SlipMps, config.Marks.CorneringSlipMps, config.Marks.PloughCrawlMps, config.CarEngineBrakingMps2,
            config.Tyre.SlipBackOffS, config.Tyre.SlipRecoverS, config.Tyre.MinSlipThrottleFraction,
            config.Tyre.TractionThrottleFraction, config.TyreGripMps2, config.Marks.PowerM2S3, config.Marks.OnsetM,
            config.Marks.PloughFloor, config.Marks.SpacingM);
    }

    /// <summary>The kernel's push constants: the ground's ask, its count the cars', then the figures.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PassPush
    {
        public DeviceGround.AskPush Ground;
        public Figures Figures;
    }

    const int WheelsACar = TyreModel.Wheels;

    readonly Vk _vk;
    readonly DeviceGround _ground;
    readonly Figures _figures;
    readonly List<GpuBuffer> _owned = [];
    readonly ComputeKernel _kernel;
    readonly DescriptorSet _set;
    readonly ComputeBatch _batch;
    readonly GpuTimer _timer;
    readonly (GpuBuffer From, GpuBuffer To, int Stride)[] _sent;
    readonly (GpuBuffer From, GpuBuffer To, int Stride)[] _read;
    readonly GpuBuffer _buildOfIn, _posesIn, _commandsIn, _carsIn, _wheelsIn;
    readonly GpuBuffer _carsOut, _wheelsOut, _surfacesOut, _impulsesOut, _scrubsOut, _markCountOut, _marksOut;
    readonly GpuBuffer _markCount, _marks;
    int _cars;

    /// <param name="effects">What each kind of ground does to a wheel, indexed by kind (<see cref="GroundLocator.EffectTable"/>).</param>
    /// <param name="builds">Every build a car may be, indexed as <see cref="BuildOf"/> names them.</param>
    /// <param name="capacity">The most cars a pass is asked of; the ring holds a mark for every wheel of them.</param>
    public DeviceTyres(
        Vk vk, SimConfig config, DeviceGround ground, ReadOnlySpan<GroundEffect> effects, ReadOnlySpan<CarBuild> builds, int capacity)
    {
        _vk = vk;
        _ground = ground;
        _figures = Figures.Of(config);
        Capacity = capacity;

        var buildRows = new CarBuildRow[builds.Length];
        for (var build = 0; build < buildRows.Length; build++) buildRows[build] = CarBuildRow.Of(builds[build]);

        var effectRows = new GroundEffectRow[effects.Length];
        for (var kind = 0; kind < effectRows.Length; kind++)
        {
            var effect = effects[kind];
            effectRows[kind] = new GroundEffectRow(effect.Coefficient, effect.DragMps2, effect.MarkFactor, effect.Ploughs ? 1u : 0u);
        }

        var wheels = capacity * WheelsACar;
        var buildBuffer = Laid<CarBuildRow>(buildRows);
        var effectBuffer = Laid<GroundEffectRow>(effectRows);
        var buildOf = Device<uint>(capacity);
        var poses = Device<PoseRow>(capacity);
        var commands = Device<CommandRow>(capacity);
        var cars = Device<CarRow>(capacity);
        var wheelRows = Device<WheelRow>(wheels);
        var surfaces = Device<SurfaceRow>(wheels);
        var impulses = Device<Vector4>(wheels);
        var scrubs = Device<ScrubRow>(wheels);
        _markCount = Device<uint>(1);
        _marks = Device<MarkRow>(wheels);

        _buildOfIn = Staging<uint>(capacity);
        _posesIn = Staging<PoseRow>(capacity);
        _commandsIn = Staging<CommandRow>(capacity);
        _carsIn = Staging<CarRow>(capacity);
        _wheelsIn = Staging<WheelRow>(wheels);
        _carsOut = Readback<CarRow>(capacity);
        _wheelsOut = Readback<WheelRow>(wheels);
        _surfacesOut = Readback<SurfaceRow>(wheels);
        _impulsesOut = Readback<Vector4>(wheels);
        _scrubsOut = Readback<ScrubRow>(wheels);
        _markCountOut = Readback<uint>(1);
        _marksOut = Readback<MarkRow>(wheels);

        _sent =
        [
            (_buildOfIn, buildOf, sizeof(uint)), (_posesIn, poses, Marshal.SizeOf<PoseRow>()),
            (_commandsIn, commands, Marshal.SizeOf<CommandRow>()), (_carsIn, cars, Marshal.SizeOf<CarRow>()),
            (_wheelsIn, wheelRows, WheelsACar * Marshal.SizeOf<WheelRow>()),
        ];
        _read =
        [
            (cars, _carsOut, Marshal.SizeOf<CarRow>()), (wheelRows, _wheelsOut, WheelsACar * Marshal.SizeOf<WheelRow>()),
            (surfaces, _surfacesOut, WheelsACar * Marshal.SizeOf<SurfaceRow>()),
            (impulses, _impulsesOut, WheelsACar * Marshal.SizeOf<Vector4>()), (scrubs, _scrubsOut, WheelsACar * Marshal.SizeOf<ScrubRow>()),
        ];

        GpuBuffer[] bound =
        [
            buildBuffer, buildOf, poses, commands, cars, wheelRows, surfaces, impulses, scrubs, _markCount, _marks, effectBuffer,
            .. ground.Tables,
        ];
        _kernel = new ComputeKernel(vk, "tyre_pass.comp", bound.Length, (uint)Marshal.SizeOf<PassPush>());
        _set = _kernel.Bind(bound);
        _batch = new ComputeBatch(vk);
        _timer = new GpuTimer(vk, 4);
    }

    /// <summary>The most cars a pass is asked of.</summary>
    public int Capacity { get; }

    /// <summary>Which of the builds each car is, written before a <see cref="Step"/>.</summary>
    public Span<uint> BuildOf => _buildOfIn.Span<uint>()[..Capacity];

    public Span<PoseRow> Poses => _posesIn.Span<PoseRow>()[..Capacity];

    public Span<CommandRow> Commands => _commandsIn.Span<CommandRow>()[..Capacity];

    /// <summary>Each car's own figures and the throttle it carries, as the pass reads them.</summary>
    public Span<CarRow> Cars => _carsIn.Span<CarRow>()[..Capacity];

    /// <summary>Each wheel's carried state, four to a car in the tyre model's order.</summary>
    public Span<WheelRow> Wheels => _wheelsIn.Span<WheelRow>()[..(Capacity * WheelsACar)];

    /// <summary>The cars as the last pass left them: the throttle wound on, and whether each was slipping or at rest.</summary>
    public ReadOnlySpan<CarRow> CarsAfter => _carsOut.Span<CarRow>()[.._cars];

    /// <summary>The wheels as the last pass left them: rims wound on, and the mark state.</summary>
    public ReadOnlySpan<WheelRow> WheelsAfter => _wheelsOut.Span<WheelRow>()[..(_cars * WheelsACar)];

    /// <summary>The ground under each wheel, nought under a car at rest.</summary>
    public ReadOnlySpan<SurfaceRow> Ground => _surfacesOut.Span<SurfaceRow>()[..(_cars * WheelsACar)];

    /// <summary>Each wheel's impulse and where it is spent: (impulse, at).</summary>
    public ReadOnlySpan<Vector4> Impulses => _impulsesOut.Span<Vector4>()[..(_cars * WheelsACar)];

    public ReadOnlySpan<ScrubRow> Scrub => _scrubsOut.Span<ScrubRow>()[..(_cars * WheelsACar)];

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
        var laid = _marksOut.Span<MarkRow>()[..Math.Min(MarkCount, Capacity * WheelsACar)];
        laid.Sort(static (a, b) => a.CarWheel.CompareTo(b.CarWheel));
        return laid;
    }

    /// <summary>
    /// One pass over the first <paramref name="cars"/> cars of what was written: sent, run, read back and waited for.
    /// One submit and one wait, whatever the count.
    /// </summary>
    public void Step(int cars)
    {
        if (cars > Capacity) throw new ArgumentOutOfRangeException(nameof(cars), $"The pass holds {Capacity} cars, not {cars}.");

        _cars = cars;
        var vk = _vk;
        var commands = _batch.Begin();
        _timer.Reset(commands);
        _timer.Stamp(commands, 0);
        foreach (var (from, to, stride) in _sent) vk.Copy(commands, from, to, (ulong)(cars * stride));
        vk.Fill(commands, _markCount, 0);
        vk.AfterTransfer(commands);
        _timer.Stamp(commands, 1);

        var push = new PassPush { Ground = _ground.PushFor(cars), Figures = _figures };
        _kernel.Dispatch(commands, _set, (uint)Math.Max(1, (cars + 63) / 64), push);
        vk.BeforeTransfer(commands);
        _timer.Stamp(commands, 2);

        foreach (var (from, to, stride) in _read) vk.Copy(commands, from, to, (ulong)(cars * stride));
        vk.Copy(commands, _markCount, _markCountOut, sizeof(uint));
        vk.Copy(commands, _marks, _marksOut, _marks.SizeBytes);
        _timer.Stamp(commands, 3);
        _batch.SubmitAndWait();
        _timer.Read();
    }

    GpuBuffer Device<T>(int count) where T : unmanaged => Own(
        _vk.CreateBuffer(
            Bytes<T>(count), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
            HostAccess.None));

    GpuBuffer Staging<T>(int count) where T : unmanaged =>
        Own(_vk.CreateBuffer(Bytes<T>(count), BufferUsageFlags.TransferSrcBit, HostAccess.Writes));

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
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
