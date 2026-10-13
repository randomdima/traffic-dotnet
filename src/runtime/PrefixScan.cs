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

    /// <summary>log2 of <see cref="Block"/>, which a scan sized on the device counts its blocks with.</summary>
    public const int BlockShift = 11;

    /// <summary><c>scan.comp</c>'s push constants, field for field.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ScanPush(
        uint Count, uint SumAt, uint GroupsAt, uint Capacity, uint CountAt, uint CountShift, uint ArgsAt, uint Pad0 = 0);

    /// <summary><c>scan_add.comp</c>'s push constants.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct AddPush(uint Count, uint CountAt);

    public ComputeKernel Scan { get; } = new(vk, "scan.comp", buffers: 4, pushBytes: 32, sets: 64);

    public ComputeKernel Add { get; } = new(vk, "scan_add.comp", buffers: 3, pushBytes: 8, sets: 32);

    public void Dispose()
    {
        Scan.Dispose();
        Add.Dispose();
    }
}

/// <summary>
/// <b>An exclusive prefix sum of one buffer of counts</b>, in place or into a buffer of its own, with its grand total
/// written to a slot of another — how a run of per-cell or per-body counts becomes where each one's run starts.
/// Deterministic: integer sums in a fixed tree.
/// </summary>
/// <remarks>
/// <para>
/// One dispatch and one barrier where one block holds every count; three and three past that. The sets are written once,
/// at construction; the count is a push constant, so a recording names how many it scans. Where it is given a slot for
/// them, the total pass also writes the indirect dispatch a pass over the totalled items is sized by, so no kernel of
/// its own runs between the scan and that pass — and, where given another, the two a scan of those items is
/// dispatched by (<see cref="RecordIndirect"/>).
/// </para>
/// <para>
/// <b>Never more than <see cref="ScanKernels.MostCounts"/></b>: a scan of more throws as it is recorded, and one sized on
/// the device is held to that many by the figure it is given. Whatever owns a scan whose length grows with the town
/// says at laying that it stays under it.
/// </para>
/// </remarks>
internal sealed class PrefixScan
{
    /// <summary>A scan that writes no indirect dispatch (<c>scan.comp</c>'s <c>NoGroups</c>).</summary>
    public const uint NoGroups = uint.MaxValue;

    /// <summary>A scan whose count is the push constant's alone (<c>scan.comp</c>'s <c>NoCount</c>).</summary>
    const uint NoCount = uint.MaxValue;

    readonly Vk _vk;
    readonly ScanKernels _kernels;
    readonly DescriptorSet _blocksSet;
    readonly DescriptorSet _totalSet;
    readonly DescriptorSet _wholeSet;
    readonly DescriptorSet _addSet;
    readonly GpuBuffer _totals;
    readonly uint _totalAt;
    readonly uint _groupsAt;
    readonly uint _capacity;
    readonly uint _argsAt;

    /// <summary>A scan of <paramref name="data"/> in place.</summary>
    public PrefixScan(Vk vk, ScanKernels kernels, GpuBuffer data, GpuBuffer blockSums, GpuBuffer totals, uint totalAt)
        : this(vk, kernels, data, data, blockSums, totals, totalAt)
    {
    }

    /// <param name="counts">The counts, kept as they are unless <paramref name="starts"/> is the same buffer.</param>
    /// <param name="starts">Where each count's run starts, written.</param>
    /// <param name="blockSums">Scratch for the block totals: <see cref="ScanKernels.Block"/> uints at least.</param>
    /// <param name="totals">
    /// Where the grand total goes, at <paramref name="totalAt"/> — and where a scan sized on the device reads its count.
    /// </param>
    /// <param name="groupsAt">
    /// Where in <paramref name="totals"/> the indirect dispatch over the totalled items goes — a workgroup of 64 for each
    /// of the first <paramref name="capacity"/>, and at least one — or <see cref="NoGroups"/>.
    /// </param>
    /// <param name="argsAt">
    /// Where in <paramref name="totals"/> the two indirect dispatches of a scan over those first
    /// <paramref name="capacity"/> go (<see cref="RecordIndirect"/>), six words — or <see cref="NoGroups"/>.
    /// </param>
    public PrefixScan(
        Vk vk, ScanKernels kernels, GpuBuffer counts, GpuBuffer starts, GpuBuffer blockSums, GpuBuffer totals, uint totalAt,
        uint groupsAt = NoGroups, uint capacity = 0, uint argsAt = NoGroups)
    {
        _vk = vk;
        _kernels = kernels;
        _totals = totals;
        _totalAt = totalAt;
        _groupsAt = groupsAt;
        _capacity = capacity;
        _argsAt = argsAt;
        _blocksSet = kernels.Scan.Bind([counts, starts, blockSums, totals]);
        _totalSet = kernels.Scan.Bind([blockSums, blockSums, totals, totals]);
        _wholeSet = kernels.Scan.Bind([counts, starts, totals, totals]);
        _addSet = kernels.Add.Bind([starts, blockSums, totals]);
    }

    /// <summary>The scan of the first <paramref name="count"/> counts recorded, made visible to whatever reads it next.</summary>
    public void Record(CommandBuffer commands, int count)
    {
        if (count > ScanKernels.MostCounts) throw new ArgumentOutOfRangeException(nameof(count), count, "more counts than one scan covers");

        var blocks = (uint)Math.Max(1, (count + ScanKernels.Block - 1) / ScanKernels.Block);
        if (blocks == 1)
        {
            _kernels.Scan.Dispatch(commands, _wholeSet, 1, Total((uint)count));
            _vk.BetweenKernels(commands);
            return;
        }

        _kernels.Scan.Dispatch(commands, _blocksSet, blocks, new ScanKernels.ScanPush((uint)count, 0, NoGroups, 0, NoCount, 0, NoGroups));
        _vk.BetweenKernels(commands);
        _kernels.Scan.Dispatch(commands, _totalSet, 1, Total(blocks));
        _vk.BetweenKernels(commands);
        _kernels.Add.Dispatch(commands, _addSet, (uint)((count + 255) / 256), new ScanKernels.AddPush((uint)count, NoCount));
        _vk.BetweenKernels(commands);
    }

    /// <summary>
    /// <b>The scan of as many counts as a pass before it left at <paramref name="countAt"/></b> of the totals, held to
    /// <paramref name="most"/>, recorded off the dispatches an earlier scan wrote at <paramref name="argsAt"/> for the
    /// same figure — three dispatches whatever it comes to, and none of them over the counts past it.
    /// </summary>
    public void RecordIndirect(CommandBuffer commands, uint countAt, uint argsAt, int most)
    {
        if (most > ScanKernels.MostCounts) throw new ArgumentOutOfRangeException(nameof(most), most, "more counts than one scan covers");

        var bound = (uint)most;
        _kernels.Scan.DispatchIndirect(
            commands, _blocksSet, _totals, argsAt * sizeof(uint), new ScanKernels.ScanPush(bound, 0, NoGroups, 0, countAt, 0, NoGroups));
        _vk.BetweenKernels(commands);
        _kernels.Scan.Dispatch(commands, _totalSet, 1, Total(bound) with { CountAt = countAt, CountShift = ScanKernels.BlockShift });
        _vk.BetweenKernels(commands);
        _kernels.Add.DispatchIndirect(commands, _addSet, _totals, (argsAt + 3) * sizeof(uint), new ScanKernels.AddPush(bound, countAt));
        _vk.BetweenKernels(commands);
    }

    ScanKernels.ScanPush Total(uint count) => new(count, _totalAt, _groupsAt, _capacity, NoCount, 0, _argsAt);
}
