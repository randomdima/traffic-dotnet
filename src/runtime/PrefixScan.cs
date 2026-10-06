using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>The two kernels every exclusive prefix sum is made of</b>, built once and shared by every
/// <see cref="PrefixScan"/> a device keeps.
/// </summary>
internal sealed class ScanKernels(Vk vk) : IDisposable
{
    /// <summary>What a scan of one buffer may cover: a block of 2 048 a workgroup, and one workgroup over the blocks.</summary>
    public const int MostCounts = Block * Block;

    /// <summary>The counts one workgroup scans.</summary>
    public const int Block = 2048;

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ScanPush(uint Count, uint SumAt);

    public ComputeKernel Scan { get; } = new(vk, "scan.comp", buffers: 2, pushBytes: 8, sets: 64);

    public ComputeKernel Add { get; } = new(vk, "scan_add.comp", buffers: 2, pushBytes: 4, sets: 32);

    public void Dispose()
    {
        Scan.Dispose();
        Add.Dispose();
    }
}

/// <summary>
/// <b>An exclusive prefix sum of one buffer of counts, in place</b>, with its grand total written to a slot of another
/// — how a run of per-cell or per-body counts becomes where each one's run starts. Deterministic: integer sums in a
/// fixed tree.
/// </summary>
/// <remarks>
/// Three dispatches and two barriers, whatever the count. The sets are written once, at construction; the count is a
/// push constant, so a recording names how many it scans.
/// </remarks>
internal sealed class PrefixScan
{
    readonly Vk _vk;
    readonly ScanKernels _kernels;
    readonly DescriptorSet _blocksSet;
    readonly DescriptorSet _totalSet;
    readonly DescriptorSet _addSet;
    readonly uint _totalAt;

    /// <param name="data">The counts, scanned in place.</param>
    /// <param name="blockSums">Scratch for the block totals: <see cref="ScanKernels.Block"/> uints at least.</param>
    /// <param name="totals">Where the grand total goes, at <paramref name="totalAt"/>.</param>
    public PrefixScan(Vk vk, ScanKernels kernels, GpuBuffer data, GpuBuffer blockSums, GpuBuffer totals, uint totalAt)
    {
        _vk = vk;
        _kernels = kernels;
        _totalAt = totalAt;
        _blocksSet = kernels.Scan.Bind([data, blockSums]);
        _totalSet = kernels.Scan.Bind([blockSums, totals]);
        _addSet = kernels.Add.Bind([data, blockSums]);
    }

    /// <summary>The scan of the first <paramref name="count"/> counts recorded, made visible to whatever reads it next.</summary>
    public void Record(CommandBuffer commands, int count)
    {
        if (count > ScanKernels.MostCounts) throw new ArgumentOutOfRangeException(nameof(count), count, "more counts than one scan covers");

        var blocks = (uint)Math.Max(1, (count + ScanKernels.Block - 1) / ScanKernels.Block);
        _kernels.Scan.Dispatch(commands, _blocksSet, blocks, new ScanKernels.ScanPush((uint)count, 0));
        _vk.BetweenKernels(commands);
        _kernels.Scan.Dispatch(commands, _totalSet, 1, new ScanKernels.ScanPush(blocks, _totalAt));
        _vk.BetweenKernels(commands);
        if (blocks > 1)
        {
            _kernels.Add.Dispatch(commands, _addSet, (uint)((count + 255) / 256), (uint)count);
            _vk.BetweenKernels(commands);
        }
    }
}
