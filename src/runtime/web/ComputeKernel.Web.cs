using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>One compute shader and the buffers it binds, in a page</b>: the browser's half of <c>ComputeKernel.cs</c>, off the
/// WGSL the build translated from the same GLSL (<c>Kernels.Web.targets</c>). Its push constants are a window of the
/// batch's own uniform buffer, since WebGPU has none (<c>town.js</c>).
/// </summary>
internal sealed class ComputeKernel : IDisposable
{
    readonly Vk _vk;
    readonly int _id;
    readonly uint _pushBytes;

    /// <param name="shader">The kernel's file, as the desktop names it: <c>integrate.comp</c>.</param>
    /// <param name="buffers">How many storage buffers the kernel binds.</param>
    /// <param name="pushBytes">The size of its push-constant block, a multiple of four; nought for none.</param>
    /// <param name="sets">How many sets will be bound through it; a page keeps no pool to size.</param>
    public ComputeKernel(Vk vk, string shader, int buffers, uint pushBytes, int sets = 1)
    {
        _vk = vk;
        _id = vk.NextKernel();
        _pushBytes = pushBytes;
        Buffers = buffers;
        WebGpu.KernelPipeline(_id, shader, Vk.KernelSource(shader), (int)pushBytes);
    }

    /// <summary>How many storage buffers a set of this kernel binds.</summary>
    public int Buffers { get; }

    /// <summary>A set over these buffers, in binding order, made once.</summary>
    public BoundSet Bind(ReadOnlySpan<GpuBuffer> buffers) => Bind(buffers, default);

    /// <summary>A set over these buffers, made again in place of <paramref name="into"/> where that is one already.</summary>
    public BoundSet Bind(ReadOnlySpan<GpuBuffer> buffers, BoundSet into)
    {
        if (buffers.Length != Buffers) throw new ArgumentException($"The kernel binds {Buffers} buffers, not {buffers.Length}.");

        var id = into.Id != 0 ? into.Id : _vk.NextSet();
        Span<int> ids = stackalloc int[buffers.Length];
        for (var binding = 0; binding < buffers.Length; binding++) ids[binding] = buffers[binding].Id;
        WebGpu.KernelBind(id, _id, ids);
        return new BoundSet(id, buffers.ToArray());
    }

    /// <summary>The kernel run over <paramref name="groups"/> workgroups, with <paramref name="push"/> as its constants.</summary>
    public void Dispatch<TPush>(Recording commands, BoundSet set, uint groups, in TPush push) where TPush : unmanaged =>
        commands.Batch.Dispatch(_id, set, groups, Constants(push));

    /// <summary>The kernel run over as many workgroups as a buffer says when the queue reaches it.</summary>
    public void DispatchIndirect<TPush>(Recording commands, BoundSet set, GpuBuffer counts, ulong offsetBytes, in TPush push)
        where TPush : unmanaged =>
        commands.Batch.DispatchIndirect(_id, set, counts, offsetBytes, Constants(push));

    unsafe ReadOnlySpan<byte> Constants<TPush>(in TPush push) where TPush : unmanaged
    {
        if (_pushBytes == 0) return default;
        if ((uint)sizeof(TPush) != _pushBytes) throw new ArgumentException($"The kernel's constants are {_pushBytes} bytes, not {sizeof(TPush)}.");

        return MemoryMarshal.AsBytes(new ReadOnlySpan<TPush>(in Unsafe.AsRef(in push)));
    }

    public void Dispose()
    {
    }
}

/// <summary>What a kernel made with no push constants is dispatched with; its size is never read.</summary>
internal readonly struct NoPush;
