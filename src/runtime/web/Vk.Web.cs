using System.Reflection;
using System.Runtime.InteropServices;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>The device the world's kernels run on, in a page</b>: the browser's half of <c>Vk.cs</c>'s compute — buffers,
/// copies, fills — over the WebGPU device <see cref="WebGpu.Start"/> already holds. It answers to the desktop's name
/// because the device world (<c>app/devicetick</c>) is one source for both heads (WEB-1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here crosses to the page while a step is recorded</b>: a copy, a fill or a dispatch is a few words in the
/// batch's own memory (<see cref="ComputeBatch"/>), and the batch crosses once when it is submitted.
/// </para>
/// <para>
/// <b>No barrier is recorded</b>: a page's queue orders its own dispatches and copies, each a usage scope of its own, so
/// what a kernel wrote is what the next one reads. And <b>nothing can be waited for</b>: a page has no fence, so what a
/// batch hands back lands in the host's memory between two frames (<see cref="ComputeBatch.Landed"/>).
/// </para>
/// </remarks>
internal sealed class Vk : IDisposable
{
    int _buffers, _kernels, _sets;
    long _deviceLocalBytes, _mappedBytes;

    public string DeviceName => "WebGPU";

    /// <summary>What the kernels' buffers hold, device-only and host-reached apart.</summary>
    public (long DeviceLocal, long Mapped) BufferBytes => (_deviceLocalBytes, _mappedBytes);

    /// <summary>
    /// A buffer a kernel may bind. One the host writes or reads keeps that memory on this side, pinned so the page can
    /// write what lands into it between frames (<see cref="GpuBuffer.Span{T}"/>).
    /// </summary>
    public GpuBuffer CreateBuffer(ulong sizeBytes, BufferUse usage, HostAccess host)
    {
        var id = ++_buffers;
        byte[]? memory = null;
        var address = 0;
        if (host != HostAccess.None)
        {
            memory = GC.AllocateArray<byte>(checked((int)sizeBytes), pinned: true);
            address = (int)Marshal.UnsafeAddrOfPinnedArrayElement(memory, 0);
            _mappedBytes += (long)sizeBytes;
        }
        else
        {
            _deviceLocalBytes += (long)sizeBytes;
        }

        WebGpu.KernelBuffer(id, checked((int)sizeBytes), address);
        return new GpuBuffer(this, id, sizeBytes, host, memory);
    }

    internal void Forget(GpuBuffer buffer)
    {
        if (buffer.Host == HostAccess.None) _deviceLocalBytes -= (long)buffer.SizeBytes;
        else _mappedBytes -= (long)buffer.SizeBytes;
    }

    internal int NextKernel() => ++_kernels;

    internal int NextSet() => ++_sets;

    /// <summary>A kernel's WGSL as the assembly ships it (<c>Kernels.Web.targets</c>).</summary>
    internal static string KernelSource(string shader)
    {
        var resource = $"Shaders/{shader}.wgsl";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException($"No embedded resource {resource}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void BetweenKernels(Recording commands)
    {
    }

    public void AfterTransfer(Recording commands)
    {
    }

    public void BeforeTransfer(Recording commands)
    {
    }

    /// <summary>A whole buffer filled with one 32-bit word, recorded.</summary>
    public void Fill(Recording commands, GpuBuffer buffer, uint word) => commands.Batch.Fill(buffer, 0, buffer.SizeBytes, word);

    /// <summary>A run of a buffer filled with one 32-bit word, recorded: <paramref name="offsetBytes"/> and <paramref name="bytes"/> are whole words.</summary>
    public void Fill(Recording commands, GpuBuffer buffer, ulong offsetBytes, ulong bytes, uint word) =>
        commands.Batch.Fill(buffer, offsetBytes, bytes, word);

    /// <summary>One buffer's first <paramref name="bytes"/> copied into another's, recorded.</summary>
    public void Copy(Recording commands, GpuBuffer from, GpuBuffer to, ulong bytes)
    {
        if (bytes > 0) commands.Batch.Copy(from, to, new CopyRun(0, 0, bytes));
    }

    /// <summary>Runs of one buffer copied each to its own place in another, recorded.</summary>
    public void Copy(Recording commands, GpuBuffer from, GpuBuffer to, ReadOnlySpan<CopyRun> runs)
    {
        foreach (var run in runs) commands.Batch.Copy(from, to, run);
    }

    /// <summary>Nothing: a page has nothing to wait on, and lets go of a buffer the queue still reads once it is done with it.</summary>
    public void WaitIdle()
    {
    }

    public void Dispose()
    {
    }
}
