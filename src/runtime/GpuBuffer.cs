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
    readonly HostAccess _host;
    readonly void* _mapped;

    public GpuBuffer(Vk vk, Buffer buffer, DeviceMemory memory, ulong sizeBytes, nint mapped, HostAccess host)
    {
        _vk = vk;
        _mapped = (void*)mapped;
        _host = host;
        Handle = buffer;
        Memory = memory;
        SizeBytes = sizeBytes;
        vk.Held(host, (long)sizeBytes);
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
        data.CopyTo(Span<T>());
    }

    /// <summary>
    /// <b>The size of a buffer of <paramref name="count"/> elements</b>, never empty. Counted in 64 bits: a claim table
    /// past 2 GiB counted in an <see cref="int"/> wraps to a size no device has, refused as out of memory.
    /// </summary>
    public static ulong Bytes<T>(int count) where T : unmanaged => (ulong)Math.Max(1, count) * (ulong)sizeof(T);

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
        CopyFrom(staging, bytes, readAt, readBy);
    }

    /// <summary>
    /// <b>Rows written into a device-local buffer</b> as <see cref="Upload{T}(int, StagedFill{T}, PipelineStageFlags2, AccessFlags2)"/>
    /// writes them, copied straight into the staging buffer — no second copy of a table held on the host to fill it from —
    /// from <paramref name="atBytes"/> on.
    /// </summary>
    public void Upload<T>(ReadOnlySpan<T> rows, PipelineStageFlags2 readAt, AccessFlags2 readBy, ulong atBytes = 0) where T : unmanaged
    {
        if (rows.IsEmpty) return;

        var bytes = (ulong)rows.Length * (ulong)sizeof(T);
        using var staging = _vk.CreateBuffer(bytes, BufferUsageFlags.TransferSrcBit, hostVisible: true);
        rows.CopyTo(staging.Span<T>());
        CopyFrom(staging, bytes, readAt, readBy, atBytes);
    }

    /// <summary>And rows of another kind, each turned into the device's as it is written into the staging buffer.</summary>
    public void Upload<TFrom, T>(
        ReadOnlySpan<TFrom> rows, delegate*<in TFrom, T> convert, PipelineStageFlags2 readAt, AccessFlags2 readBy, ulong atBytes = 0)
        where TFrom : unmanaged where T : unmanaged
    {
        if (rows.IsEmpty) return;

        var bytes = (ulong)rows.Length * (ulong)sizeof(T);
        using var staging = _vk.CreateBuffer(bytes, BufferUsageFlags.TransferSrcBit, hostVisible: true);
        var into = staging.Span<T>();
        for (var at = 0; at < rows.Length; at++) into[at] = convert(rows[at]);

        CopyFrom(staging, bytes, readAt, readBy, atBytes);
    }

    /// <summary>
    /// A staging buffer's first <paramref name="bytes"/> copied across to <paramref name="atBytes"/>, waited for, and made
    /// visible to its reader.
    /// </summary>
    void CopyFrom(GpuBuffer staging, ulong bytes, PipelineStageFlags2 readAt, AccessFlags2 readBy, ulong atBytes = 0)
    {
        var (source, target) = (staging.Handle, Handle);
        _vk.OneShot(commands =>
        {
            var region = new BufferCopy { DstOffset = atBytes, Size = bytes };
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
                Offset = atBytes,
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

    public void Dispose()
    {
        _vk.DestroyBuffer(Handle, Memory);
        _vk.Held(_host, -(long)SizeBytes);
    }
}

/// <summary>The elements a staged upload writes, into memory the upload already owns.</summary>
internal delegate void StagedFill<T>(Span<T> into) where T : unmanaged;

/// <summary>What the CPU does with a buffer's memory, which decides the memory it is made of.</summary>
internal enum HostAccess
{
    /// <summary>Nothing: device-local, and reached only by a copy or a kernel.</summary>
    None,

    /// <summary>Writes it, mapped and coherent — what a frame's instances and a tick's commands are written into.</summary>
    Writes,

    /// <summary>Reads it back, mapped and cached where the device has such memory — what an instrument or a test reads.</summary>
    Reads,

    /// <summary>
    /// <b>Writes it every frame for the device to read in that frame</b>, mapped and coherent, and in the device's own
    /// memory where the device offers it mapped (resizable BAR, or memory the two share): what a frame's instances,
    /// quads, counts and camera are written into. Where it is not offered, or that heap is spent, it is
    /// <see cref="Writes"/>.
    /// </summary>
    Streams,
}
