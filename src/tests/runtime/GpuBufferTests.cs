using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Runtime;

/// <summary>
/// <b>A buffer that grows is what every kernel bound over it reads and writes after</b> (<see cref="GpuBuffer.Grow"/>): the
/// set bound before it grew reaches past where it used to end, with nothing bound again.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P2)]
public class GpuBufferTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>How many words each buffer holds before it grows.</summary>
    const int WordsBefore = 64;

    /// <summary>How many it grows to: a run copied past the old end lands only in the new buffer.</summary>
    const int WordsAfter = 1024;

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct RunRow(uint From, uint To, uint Words, uint Pad = 0u);

    [Fact]
    public void AKernelBoundBeforeTheBufferGrewWritesPastWhereItEnded()
    {
        var vk = device.Vk;
        const BufferUsageFlags Usage = BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit;
        using var from = vk.CreateBuffer(WordsBefore * sizeof(uint), Usage, HostAccess.Writes);
        using var to = vk.CreateBuffer(WordsBefore * sizeof(uint), Usage, HostAccess.None);
        using var runs = vk.CreateBuffer((ulong)Marshal.SizeOf<RunRow>(), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        using var read = vk.CreateBuffer(WordsAfter * sizeof(uint), BufferUsageFlags.TransferDstBit, HostAccess.Reads);
        using var kernel = new ComputeKernel(vk, "copy_runs.comp", 3, sizeof(uint));
        var set = kernel.Bind([from, to, runs]);

        from.Grow(WordsAfter * sizeof(uint));
        to.Grow(WordsAfter * sizeof(uint));
        var words = from.Span<uint>();
        for (var word = 0; word < WordsAfter; word++) words[word] = (uint)word * 3u;
        const uint At = WordsAfter - WordsBefore;
        runs.Span<RunRow>()[0] = new RunRow(At, At, WordsBefore);

        using var batch = new ComputeBatch(vk);
        var commands = batch.Begin();
        vk.Fill(commands, to, 0u);
        vk.AfterTransfer(commands);
        kernel.Dispatch(commands, set, 1u, 1u);
        vk.BeforeTransfer(commands);
        vk.Copy(commands, to, read, to.SizeBytes);
        batch.SubmitAndWait();

        var landed = read.Span<uint>();
        Assert.Equal(WordsAfter * sizeof(uint), (int)to.SizeBytes);
        for (var word = 0; word < WordsAfter; word++) Assert.Equal(word >= At ? (uint)word * 3u : 0u, landed[word]);
    }
}
