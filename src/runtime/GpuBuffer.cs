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

    /// <summary>
    /// <b>Elements written into a device-local buffer</b> (one made with <see cref="BufferUsageFlags.TransferDstBit"/>):
    /// filled into a staging buffer of their size, copied across, waited for, and made visible to the stage that
    /// reads them. A submit and a wait, so it is for what is written once or when a reader asks — never a frame.
    /// </summary>
    public void Upload<T>(int count, StagedFill<T> fill, PipelineStageFlags2 readAt, AccessFlags2 readBy) where T : unmanaged
    {
        if (count == 0) return;

        var bytes = (ulong)count * (ulong)sizeof(T);
        using var staging = _vk.CreateBuffer(bytes, BufferUsageFlags.TransferSrcBit, hostVisible: true);
        fill(staging.Span<T>()[..count]);

        var (source, target) = (staging.Handle, Handle);
        _vk.OneShot(commands =>
        {
            var region = new BufferCopy { Size = bytes };
            Vk.Count();
            _vk.Api.CmdCopyBuffer(commands, source, target, 1, &region);

            var barrier = new BufferMemoryBarrier2
            {
                SType = StructureType.BufferMemoryBarrier2,
                SrcStageMask = PipelineStageFlags2.CopyBit,
                SrcAccessMask = AccessFlags2.TransferWriteBit,
                DstStageMask = readAt,
                DstAccessMask = readBy,
                Buffer = target,
                Size = bytes,
            };
            var dependency = new DependencyInfo
            {
                SType = StructureType.DependencyInfo,
                BufferMemoryBarrierCount = 1,
                PBufferMemoryBarriers = &barrier,
            };
            Vk.Count();
            _vk.Api.CmdPipelineBarrier2(commands, &dependency);
        });
    }

    public void Dispose() => _vk.DestroyBuffer(Handle, Memory);
}

/// <summary>The elements a staged upload writes, into memory the upload already owns.</summary>
internal delegate void StagedFill<T>(Span<T> into) where T : unmanaged;
