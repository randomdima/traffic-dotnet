using TrafficSimulation.Runtime;
using static TrafficSimulation.Runtime.GpuBuffer;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>The buffers and kernels one of the device's classes lays and lets go of</b>: each made here is held until
/// <see cref="Dispose"/>, so a class names its children and this in its own disposal, and no list of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every size is a row at the least</b> (<see cref="GpuBuffer.Bytes{T}"/>): a count of nought lays one.
/// </para>
/// <para>
/// <b>What a buffer may be used for is named by what it is</b> — <see cref="Device{T}"/>, <see cref="Room{T}"/>,
/// <see cref="Mapped{T}"/>, <see cref="Written{T}"/>, <see cref="Staging{T}"/>, <see cref="Readback{T}"/> — and a caller that wants more of one adds
/// its bits rather than naming them over; anything else is made by the caller and handed to <see cref="Own"/>.
/// </para>
/// </remarks>
internal sealed class DeviceHoldings(Vk vk) : IDisposable
{
    /// <summary>
    /// The threads a workgroup runs, as every kernel dispatched by <see cref="Groups"/> declares it (<c>local_size_x = 64</c>).
    /// </summary>
    /// <remarks>
    /// The two must agree, or a dispatch falls short of its rows. A kernel of another size (<c>solve.comp</c>,
    /// <c>woken.comp</c>, <c>settle_spread.comp</c>, the scan's) is dispatched by a count of its own and never by this.
    /// </remarks>
    public const int Group = 64;

    readonly List<GpuBuffer> _buffers = [];
    readonly List<ComputeKernel> _kernels = [];

    /// <summary>The workgroups that cover <paramref name="count"/> threads, and one at the least.</summary>
    public static uint Groups(int count) => (uint)Math.Max(1, (count + Group - 1) / Group);

    /// <summary>What the buffers held hold, device-local and mapped together.</summary>
    public long BytesHeld
    {
        get
        {
            var bytes = 0L;
            foreach (var buffer in _buffers) bytes += (long)buffer.SizeBytes;
            return bytes;
        }
    }

    public ComputeKernel Kernel(string shader, int buffers, uint pushBytes, int sets = 1)
    {
        var kernel = new ComputeKernel(vk, shader, buffers, pushBytes, sets);
        _kernels.Add(kernel);
        return kernel;
    }

    /// <summary>Device-local rows a kernel reads and writes and a copy may fill or read.</summary>
    public GpuBuffer Device<T>(int count, BufferUse also = 0) where T : unmanaged => Own(
        vk.CreateBuffer(Bytes<T>(count), BufferUse.Storage | BufferUse.TransferSrc | BufferUse.TransferDst | also, HostAccess.None));

    /// <summary>Device-local rows a kernel reads and writes and only a copy into fills.</summary>
    public GpuBuffer Room<T>(int count, BufferUse also = 0) where T : unmanaged => Own(
        vk.CreateBuffer(Bytes<T>(count), BufferUse.Storage | BufferUse.TransferDst | also, HostAccess.None));

    /// <summary>A <see cref="Room{T}"/> holding these rows, sent once.</summary>
    public GpuBuffer Laid<T>(ReadOnlySpan<T> rows, BufferUse also = 0) where T : unmanaged
    {
        var buffer = Room<T>(rows.Length, also);
        buffer.Upload(rows);
        return buffer;
    }

    /// <summary>Rows a kernel writes straight into memory the host reads.</summary>
    /// <remarks>
    /// <b>Never the host's to write</b>: a page sends no such buffer to its device and lands the device's copy over the
    /// host's after each batch (<c>ComputeBatch.Web.cs</c>), so a write here is lost there while Vulkan keeps it.
    /// What the host writes for a kernel is <see cref="Written{T}"/>.
    /// </remarks>
    public GpuBuffer Mapped<T>(int count) where T : unmanaged =>
        Own(vk.CreateBuffer(Bytes<T>(count), BufferUse.Storage, HostAccess.Reads));

    /// <summary>Rows the host writes between steps and a kernel reads, sent ahead of each batch that binds them.</summary>
    public GpuBuffer Written<T>(int count) where T : unmanaged =>
        Own(vk.CreateBuffer(Bytes<T>(count), BufferUse.Storage, HostAccess.Writes));

    /// <summary>Memory the host writes and a copy takes to the device.</summary>
    public GpuBuffer Staging<T>(int count) where T : unmanaged =>
        Own(vk.CreateBuffer(Bytes<T>(count), BufferUse.TransferSrc, HostAccess.Writes));

    /// <summary>Memory the host reads back, written by a copy — or by a kernel, where <paramref name="also"/> binds it.</summary>
    public GpuBuffer Readback<T>(int count, BufferUse also = 0) where T : unmanaged =>
        Own(vk.CreateBuffer(Bytes<T>(count), BufferUse.TransferDst | also, HostAccess.Reads));

    public GpuBuffer Own(GpuBuffer buffer)
    {
        _buffers.Add(buffer);
        return buffer;
    }

    /// <summary>One buffer let go of ahead of the rest, where a larger takes its place.</summary>
    public void Release(GpuBuffer buffer)
    {
        _buffers.Remove(buffer);
        buffer.Dispose();
    }

    /// <summary>The kernels let go of ahead of the buffers, for a class whose children go between the two.</summary>
    public void DisposeTheKernels()
    {
        foreach (var kernel in _kernels) kernel.Dispose();
        _kernels.Clear();
    }

    public void Dispose()
    {
        DisposeTheKernels();
        foreach (var buffer in _buffers) buffer.Dispose();
    }
}
