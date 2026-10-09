using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace TrafficSimulation.Runtime;

/// <summary>
/// A buffer and the memory under it. Host-visible memory is mapped once at startup and never unmapped,
/// so writing into it is a <see cref="Span{T}"/> over memory the driver already owns: no copy, no pin,
/// no marshalling and no allocation.
/// </summary>
/// <remarks>
/// <b>It may grow</b> (<see cref="Grow"/>): the same object over a bigger buffer, every set a kernel bound it in pointed at
/// the new one — so whoever holds it, and every kernel, goes on reading it with nothing rebound by hand.
/// </remarks>
internal sealed unsafe class GpuBuffer : IDisposable
{
    readonly Vk _vk;
    readonly BufferUsageFlags _usage;
    readonly HostAccess _host;
    void* _mapped;

    /// <summary>Every binding of every set a kernel bound this buffer in (<see cref="ComputeKernel.Bind"/>), pointed again where it grows.</summary>
    readonly List<(DescriptorSet Set, uint Binding)> _boundIn = [];

    public GpuBuffer(Vk vk, Buffer buffer, DeviceMemory memory, ulong sizeBytes, nint mapped, BufferUsageFlags usage, HostAccess host)
    {
        _vk = vk;
        _mapped = (void*)mapped;
        _usage = usage;
        _host = host;
        Handle = buffer;
        Memory = memory;
        SizeBytes = sizeBytes;
    }

    public Buffer Handle { get; private set; }

    public DeviceMemory Memory { get; private set; }

    public ulong SizeBytes { get; private set; }

    /// <summary>
    /// <b>The buffer made at least <paramref name="sizeBytes"/> long</b>: a new buffer and memory of the same use under this
    /// object, and every set it was bound in pointed at it. Nothing where it is that long already.
    /// </summary>
    /// <param name="keep">
    /// Whether what it holds is kept, copied by the host — mapped memory only. Otherwise the new buffer holds nothing yet.
    /// </param>
    /// <remarks>
    /// <b>Nothing on the device may be reading it</b>, nor any recording still to be submitted bind it: a set is rewritten in
    /// place, which no pending submit may hold (<see cref="Vk.WaitIdle"/>). A span taken of it before is of memory freed.
    /// </remarks>
    public void Grow(ulong sizeBytes, bool keep = false)
    {
        if (sizeBytes <= SizeBytes) return;
        if (keep && _mapped is null) throw new InvalidOperationException("Only mapped memory is kept as it grows: the host copies it.");

        var (buffer, memory, mapped) = _vk.AllocateBuffer(sizeBytes, _usage, _host);
        if (keep) System.Buffer.MemoryCopy(_mapped, (void*)mapped, sizeBytes, SizeBytes);
        _vk.DestroyBuffer(Handle, Memory);
        (Handle, Memory, SizeBytes) = (buffer, memory, sizeBytes);
        _mapped = (void*)mapped;
        _vk.Rebind(CollectionsMarshal.AsSpan(_boundIn), buffer);
    }

    /// <summary>One binding of a set this buffer was bound in, kept so a growth points it at the new buffer.</summary>
    internal void BoundIn(DescriptorSet set, uint binding) => _boundIn.Add((set, binding));

    /// <summary>Every binding of a set no longer in use forgotten, as its kernel goes.</summary>
    internal void Unbound(DescriptorSet set) => _boundIn.RemoveAll(bound => bound.Set.Handle == set.Handle);

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

/// <summary>What the CPU does with a buffer's memory, which decides the memory it is made of.</summary>
internal enum HostAccess
{
    /// <summary>Nothing: device-local, and reached only by a copy or a kernel.</summary>
    None,

    /// <summary>Writes it, mapped and coherent — what a frame's instances and a tick's commands are written into.</summary>
    Writes,

    /// <summary>Reads it back, mapped and cached where the device has such memory — what an event ring is read out of.</summary>
    Reads,
}
