using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What a copy costs the device each way</b>: mapped memory the host writes into the device's own, and the device's
/// own into mapped memory the host reads — whole, and cut into many runs as a table a tick changes only some rows of is
/// sent — by the device's own copy and by a kernel that copies (<c>runtime/shaders/copy_runs.comp</c>), timed on the
/// device, the best of a few rounds.
/// </summary>
internal static class DeviceCopyProbe
{
    const int Rounds = 8;

    /// <summary>How long a run a whole copy is cut into for the kernel, so that many workgroups share it.</summary>
    const int WholeRunBytes = 16 << 10;

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct RunRow(uint From, uint To, uint Words, uint Pad);

    public static void Run(SimConfig config)
    {
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        Console.WriteLine($"device copy probe — {vk.DeviceName}, GB/s");
        Console.WriteLine($"{"bytes",12}{"runs",8}{"copy in",10}{"copy out",10}{"kernel in",11}{"kernel out",12}");
        foreach (var (bytes, runs) in (ReadOnlySpan<(int, int)>)[(1 << 20, 1), (16 << 20, 1), (1 << 20, 1_000), (1 << 20, 10_000), (4 << 20, 10_000)])
        {
            var copy = Measure(vk, bytes, runs, byKernel: false);
            var kernel = Measure(vk, bytes, runs, byKernel: true);
            Console.WriteLine(
                $"{bytes,12}{runs,8}{bytes / copy.InMs / 1e6,10:F2}{bytes / copy.OutMs / 1e6,10:F2}{bytes / kernel.InMs / 1e6,11:F2}" +
                $"{bytes / kernel.OutMs / 1e6,12:F2}");
        }
    }

    static (double InMs, double OutMs) Measure(Vk vk, int bytes, int runs, bool byKernel)
    {
        const BufferUsageFlags Usage = BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit;
        using var staging = vk.CreateBuffer((ulong)bytes, Usage, HostAccess.Writes);
        using var onDevice = vk.CreateBuffer((ulong)bytes, Usage, HostAccess.None);
        using var readback = vk.CreateBuffer((ulong)bytes, Usage, HostAccess.Reads);
        using var batch = new ComputeBatch(vk);
        using var timer = new GpuTimer(vk, 3);

        // Runs of equal size with a gap of the same size between them, as a table's changed rows are; a whole copy is one
        // run to the device's copy and runs of WholeRunBytes to the kernel.
        var runBytes = runs == 1 ? bytes : bytes / runs / 2;
        var stride = runs == 1 ? bytes : runBytes * 2;
        var regions = new BufferCopy[runs];
        for (var run = 0; run < runs; run++) regions[run] = new BufferCopy((ulong)(run * stride), (ulong)(run * stride), (ulong)runBytes);

        var kernelRuns = runs == 1 ? bytes / WholeRunBytes : runs;
        var kernelRunBytes = runs == 1 ? WholeRunBytes : runBytes;
        var kernelStride = runs == 1 ? WholeRunBytes : stride;
        var rows = new RunRow[kernelRuns];
        for (var run = 0; run < kernelRuns; run++)
        {
            var word = (uint)(run * kernelStride / sizeof(uint));
            rows[run] = new RunRow(word, word, (uint)(kernelRunBytes / sizeof(uint)), 0u);
        }

        using var runRows = vk.CreateBuffer((ulong)(kernelRuns * Marshal.SizeOf<RunRow>()), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        rows.AsSpan().CopyTo(runRows.Span<RunRow>());
        using var kernel = new ComputeKernel(vk, "copy_runs.comp", 3, sizeof(uint), sets: 2);
        var into = kernel.Bind([staging, onDevice, runRows]);
        var outOf = kernel.Bind([onDevice, readback, runRows]);

        var bestIn = double.MaxValue;
        var bestOut = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            var commands = batch.Begin();
            timer.Reset(commands);
            timer.Stamp(commands, 0);
            if (byKernel) kernel.Dispatch(commands, into, (uint)kernelRuns, (uint)kernelRuns);
            else vk.Copy(commands, staging, onDevice, regions);

            vk.AfterTransfer(commands);
            vk.BetweenKernels(commands);
            timer.Stamp(commands, 1);
            if (byKernel) kernel.Dispatch(commands, outOf, (uint)kernelRuns, (uint)kernelRuns);
            else vk.Copy(commands, onDevice, readback, regions);

            timer.Stamp(commands, 2);
            batch.SubmitAndWait();
            timer.Read();
            bestIn = Math.Min(bestIn, timer.BetweenMs(0, 1));
            bestOut = Math.Min(bestOut, timer.BetweenMs(1, 2));
        }

        return (bestIn, bestOut);
    }
}
