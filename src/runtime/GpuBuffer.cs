using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace TrafficSimulation.Runtime;

/// <summary>
/// A buffer and the memory under it. Host-visible memory is mapped once at startup and never unmapped,
/// so writing into it is a <see cref="Span{T}"/> over memory the driver already owns: no copy, no pin,
/// no marshalling and no allocation.
/// </summary>
internal sealed unsafe class GpuBuffer : IDisposable
{
    readonly Vk _vk;
    readonly void* _mapped;

    public GpuBuffer(Vk vk, Buffer buffer, DeviceMemory memory, ulong sizeBytes, void* mapped)
    {
        _vk = vk;
        _mapped = mapped;
        Handle = buffer;
        Memory = memory;
        SizeBytes = sizeBytes;
    }

    public Buffer Handle { get; }

    public DeviceMemory Memory { get; }

    public ulong SizeBytes { get; }

    /// <summary>The mapped memory as the type being written into it. Host-visible buffers only.</summary>
    public Span<T> Span<T>() where T : unmanaged
    {
        if (_mapped is null) throw new InvalidOperationException("This buffer is device-local: nothing is mapped.");

        return new Span<T>(_mapped, (int)(SizeBytes / (ulong)sizeof(T)));
    }

    public void Write<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        MemoryMarshal.Cast<T, byte>(data).CopyTo(Span<byte>());
    }

    public void Dispose() => _vk.DestroyBuffer(Handle, Memory);
}
