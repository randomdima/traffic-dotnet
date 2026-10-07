using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The town's walkers ticked on the device</b> (<c>world/town/kernels/walkers_tick.comp</c>): each walker's own tick and a
/// casualty's slide, over a walker table the device keeps from one tick to the next, leaving each walker's impulse where
/// the bodies' phase spends it and reads the walker back into the table after the step (<see cref="DeviceBodyPhase"/>).
/// </summary>
/// <remarks>
/// <b>Run in shadow</b> (<see cref="Lay"/>, <see cref="Disagreement"/>): the town still ticks its walkers, and the
/// device is handed every walker as the town's tick reads it. <see cref="TickMs"/> is the kernel alone.
/// </remarks>
internal sealed partial class DeviceWalkerTick : IDisposable
{
    /// <summary>A walker as the device keeps it (<c>WalkerTableRow</c>).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct WalkerRow(
        Vector2 PositionM, Vector2 VelocityMps, Vector2 DeclaredMps, Vector2 AimM, float HeadingRad, float MassKg, uint Flags,
        float DistanceWalkedM)
    {
        public const uint Walking = 1u, Wounded = 2u, Inside = 4u, Paused = 8u;
    }

    /// <summary>The kernel's figures, field for field with <c>walkers_tick.comp</c>'s, each the host's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Figures(
        uint WalkerCount, float DtS, float WalkSpeedMps, float TurnRateDegPerS, float FootGripMps2, float SlidingGripMps2)
    {
        public static Figures Of(SimConfig config) => new(
            0u, config.TickSeconds, config.PersonWalkSpeedMps, config.PersonTurnRateDegPerS, config.PersonFootGripMps2,
            config.PersonSlidingGripMps2);
    }

    readonly Vk _vk;
    readonly SimConfig _config;
    readonly Figures _figures;
    readonly ResidentRows<WalkerRow> _walkers;
    readonly ResidentRows<Vector2> _impulses;
    readonly ResidentRows<int> _bodies;
    readonly ComputeKernel _kernel;
    readonly DescriptorSet _set;
    readonly GpuTimer _timer;
    int _count;

    public DeviceWalkerTick(Vk vk, SimConfig config, int capacity)
    {
        _vk = vk;
        _config = config;
        _figures = Figures.Of(config);
        Capacity = capacity;
        _walkers = new ResidentRows<WalkerRow>(vk, capacity, 1, readsBack: true);
        _impulses = new ResidentRows<Vector2>(vk, capacity, 1, readsBack: true);
        _bodies = new ResidentRows<int>(vk, capacity, 1);

        // No walker is body -1, so every walker's first lay differs from what was "sent" and is.
        _bodies.All.Fill(-1);
        _kernel = new ComputeKernel(vk, "walkers_tick.comp", 2, (uint)Marshal.SizeOf<Figures>());
        _set = _kernel.Bind([_walkers.Device, _impulses.Device]);
        _timer = new GpuTimer(vk, 2);
    }

    /// <summary>The most walkers the table holds.</summary>
    public int Capacity { get; }

    /// <summary>How many walkers the last tick was run for.</summary>
    public int Count => _count;

    /// <summary>The walker table, for the bodies' phase that spends its impulses and reads it back.</summary>
    public GpuBuffer Table => _walkers.Device;

    /// <summary>Each walker's impulse, for the bodies' phase that spends it.</summary>
    public GpuBuffer ImpulseTable => _impulses.Device;

    /// <summary>Which body each walker is, for the bodies' phase.</summary>
    public GpuBuffer BodyTable => _bodies.Device;

    /// <summary>Every walker as the last read left it.</summary>
    public ReadOnlySpan<WalkerRow> WalkersAfter => _walkers.AllRead[.._count];

    /// <summary>Every walker's impulse as the last read left it.</summary>
    public ReadOnlySpan<Vector2> ImpulsesAfter => _impulses.AllRead[.._count];

    /// <summary>The device's own time for the last tick's kernel, in milliseconds.</summary>
    public double TickMs => _timer.BetweenMs(0, 1);

    /// <summary>
    /// <b>One tick of the first <paramref name="walkers"/> walkers recorded into a caller's submit</b>: what was marked
    /// sent and the kernel run, leaving its writes for the caller to barrier before whatever reads them.
    /// </summary>
    public void Record(CommandBuffer commands, int walkers)
    {
        if (walkers > Capacity) throw new ArgumentOutOfRangeException(nameof(walkers), $"The table holds {Capacity} walkers, not {walkers}.");

        _count = walkers;
        _walkers.Send(commands);
        _impulses.Send(commands);
        _bodies.Send(commands);
        _vk.AfterTransfer(commands);
        _timer.Reset(commands);
        _timer.Stamp(commands, 0);
        _kernel.Dispatch(commands, _set, (uint)Math.Max(1, (walkers + 63) / 64), _figures with { WalkerCount = (uint)walkers });
        _timer.Stamp(commands, 1);
    }

    /// <summary>What the walkers' tick and the bodies' phase after it left, recorded to be handed back.</summary>
    public void RecordRead(CommandBuffer commands)
    {
        _vk.BeforeTransfer(commands);
        _walkers.Read(commands, _count);
        _impulses.Read(commands, _count);
    }

    /// <summary>The device's stamps of the last tick taken in, once its submit has been waited for.</summary>
    public void ReadTimes() => _timer.Read();

    public void Dispose()
    {
        _timer.Dispose();
        _kernel.Dispose();
        _walkers.Dispose();
        _impulses.Dispose();
        _bodies.Dispose();
    }
}
