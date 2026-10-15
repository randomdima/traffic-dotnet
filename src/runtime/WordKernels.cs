using System.Runtime.InteropServices;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>A copy and a fill of whole words recorded as kernels of the run's own</b> (<c>copy_words.comp</c>,
/// <c>fill_words.comp</c>), for a buffer a step copies or fills every time. Each binds its buffers once and is recorded
/// with the run as push constants; the caller barriers around it as around any kernel.
/// </summary>
/// <remarks>
/// RADV takes a copy or a fill of 4 kB or more through a compute pipeline of its own, looked up for every call: in a town
/// stepped on the device those lookups were half the host's time recording a step (<c>qq prof</c>, O30), where a kernel's
/// binding and dispatch cost a few calls.
/// </remarks>
internal sealed class WordKernels(Vk vk, int sets) : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CopyPush(uint FromWord, uint ToWord, uint Words);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct FillPush(uint AtWord, uint Words, uint Word);

    /// <summary>How many words a thread takes, short of the most groups a run is dispatched over.</summary>
    const uint WordsAThread = 4;

    /// <summary>The most workgroups one dimension of a dispatch may have that every device takes.</summary>
    const uint MostGroups = 65535;

    const uint Group = 64;

    readonly ComputeKernel _copy = new(vk, "copy_words.comp", 2, 12, sets);
    readonly ComputeKernel _fill = new(vk, "fill_words.comp", 1, 12, sets);

    public BoundSet BindCopy(GpuBuffer from, GpuBuffer to) => _copy.Bind([from, to]);

    public BoundSet BindFill(GpuBuffer buffer) => _fill.Bind([buffer]);

    /// <summary>A run of whole words copied between the buffers <paramref name="set"/> binds (<see cref="BindCopy"/>), recorded.</summary>
    public void Copy(Recording commands, BoundSet set, CopyRun run)
    {
        var words = WordsOf(run.Bytes);
        if (words == 0) return;

        _copy.Dispatch(commands, set, Groups(words), new CopyPush(WordsOf(run.FromBytes), WordsOf(run.ToBytes), words));
    }

    /// <summary>A run of whole words of the buffer <paramref name="set"/> binds (<see cref="BindFill"/>) filled with one word, recorded.</summary>
    public void Fill(Recording commands, BoundSet set, ulong offsetBytes, ulong bytes, uint word)
    {
        var words = WordsOf(bytes);
        if (words == 0) return;

        _fill.Dispatch(commands, set, Groups(words), new FillPush(WordsOf(offsetBytes), words, word));
    }

    static uint WordsOf(ulong bytes) => checked((uint)(bytes / sizeof(uint)));

    static uint Groups(uint words) => Math.Clamp((words + (Group * WordsAThread) - 1) / (Group * WordsAThread), 1u, MostGroups);

    public void Dispose()
    {
        _copy.Dispose();
        _fill.Dispose();
    }
}
