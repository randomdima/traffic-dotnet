using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;
using Vk = TrafficSimulation.Runtime.Vk;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.Tests.Runtime;

/// <summary>
/// <b>The device the kernels are asked of</b>: lavapipe, the CPU's own Vulkan, opened headless once per test class.
/// No GPU, no window and no desktop, and the same answer on every machine.
/// </summary>
public sealed class Lavapipe : IDisposable
{
    internal Vk Vk { get; } = Vk.Open("traffic-dotnet-tests", validation: false, deviceNamed: "llvmpipe");

    /// <summary>A storage buffer holding <paramref name="values"/>, mapped so the test reads back what a kernel wrote.</summary>
    internal GpuBuffer Holding<T>(ReadOnlySpan<T> values) where T : unmanaged
    {
        var buffer = Sized<T>(values.Length);
        buffer.Write(values);
        return buffer;
    }

    /// <summary>A mapped storage buffer of <paramref name="count"/> elements, zeroed.</summary>
    internal GpuBuffer Sized<T>(int count) where T : unmanaged
    {
        var buffer = Vk.CreateBuffer(
            (ulong)(Math.Max(1, count) * Unsafe.SizeOf<T>()),
            BufferUsageFlags.StorageBufferBit | BufferUsageFlags.IndirectBufferBit,
            HostAccess.Reads);
        buffer.Span<byte>().Clear();
        return buffer;
    }

    public void Dispose() => Vk.Dispose();
}
