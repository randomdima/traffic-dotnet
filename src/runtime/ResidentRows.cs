using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>Rows the device keeps from one submit to the next</b>, a fixed number to an item, with a mapped copy beside them
/// the host writes into: it writes the rows of the items it changed, marks them, and <see cref="Send"/> copies only
/// those — as one copy, or one kernel past a few runs, however many there are, so a submit crosses once a table whatever
/// the town's size.
/// </summary>
/// <remarks>
/// <para>
/// <b>An item is marked at most once between sends</b>: marks are kept as runs, an item marked where the last run ends
/// extending it, so a dense pass is one run and the list of them — laid for every item at construction — never grows.
/// </para>
/// <para>
/// <b>Rows only the host writes</b> (<c>hostOnly</c>) carry a run over a gap of unmarked rows shorter than
/// <see cref="BridgedGapBytes"/>: those rows' mapped copy is what the device holds, so sending them again changes nothing
/// and saves a region.
/// </para>
/// </remarks>
internal sealed unsafe class ResidentRows<T> : IDisposable where T : unmanaged
{
    /// <summary>
    /// The gap a run is carried over rather than a second begun: a region costs a copy about what this many bytes do
    /// (<c>--bench devicecopy</c>: 0.85 µs a region against 1.8 GB/s on a PCIe link of 5 GT/s × 4).
    /// </summary>
    const ulong BridgedGapBytes = 1536;

    /// <summary>
    /// The most runs a send copies; past them a kernel copies them instead (<c>copy_runs.comp</c>), reading the mapped copy
    /// over the bus, which costs a dispatch whatever their count where a copy costs about 0.6 µs a run
    /// (<c>--bench devicecopy</c>).
    /// </summary>
    const int CopiedRunsAtMost = 8;

    readonly Vk _vk;
    readonly GpuBuffer _staging;
    readonly GpuBuffer? _readback;
    readonly BufferCopy[] _runs;

    /// <summary>The kernel that copies a send of many runs, its set over the mapped copy and the device's, and its runs in words.</summary>
    readonly ComputeKernel? _runCopy;

    readonly DescriptorSet _runCopySet;
    readonly GpuBuffer? _wordRuns;

    /// <summary>
    /// The batch the last kernel copy was recorded into (<see cref="ComputeBatch.Begun"/>): its runs are read when it runs, so
    /// a second send in the same recording is a copy.
    /// </summary>
    long _copiedByKernelIn = -1;

    /// <summary>Whether a run is carried over a gap of unmarked rows (<see cref="BridgedGapBytes"/>): only the host writes them.</summary>
    readonly bool _bridges;

    int _runCount;

    /// <param name="readsBack">Whether the rows are ever handed back to the host (<see cref="Read"/>).</param>
    /// <param name="hostOnly">Whether only the host writes the rows, so the mapped copy is what the device holds.</param>
    public ResidentRows(Vk vk, int items, int rowsPerItem, bool readsBack = false, bool hostOnly = false)
    {
        _vk = vk;
        Items = items;
        RowsPerItem = rowsPerItem;
        _bridges = hostOnly;
        var bytes = (ulong)(Math.Max(1, items * rowsPerItem) * sizeof(T));
        Device = vk.CreateBuffer(
            bytes, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        _staging = vk.CreateBuffer(bytes, BufferUsageFlags.TransferSrcBit | BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        if (readsBack) _readback = vk.CreateBuffer(bytes, BufferUsageFlags.TransferDstBit, HostAccess.Reads);
        _runs = new BufferCopy[Math.Max(1, items)];
        if (sizeof(T) % sizeof(uint) == 0 && items > CopiedRunsAtMost)
        {
            _wordRuns = vk.CreateBuffer((ulong)(_runs.Length * sizeof(WordRun)), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
            _runCopy = new ComputeKernel(vk, "copy_runs.comp", 3, sizeof(uint));
            _runCopySet = _runCopy.Bind([_staging, Device, _wordRuns]);
        }

        if (!hostOnly) return;

        // The device and the mapped copy both begin as zeros, so a gap a run is carried over sends what the device holds.
        All.Clear();
        Device.Upload<T>(items * rowsPerItem, static into => into.Clear(), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
    }

    public int Items { get; }

    public int RowsPerItem { get; }

    /// <summary>The rows as the device keeps them, for a kernel to bind.</summary>
    public GpuBuffer Device { get; }

    /// <summary>Every item's rows as the host writes them, to be marked.</summary>
    public Span<T> All => _staging.Span<T>()[..(Items * RowsPerItem)];

    /// <summary>Every item's rows as the last <see cref="Read"/> handed them back.</summary>
    public ReadOnlySpan<T> AllRead => _readback!.Span<T>()[..(Items * RowsPerItem)];

    public void Mark(int item) => Mark(item, 1);

    /// <summary>A run of items to be sent, joined to the last run where it begins at that run's end.</summary>
    public void Mark(int first, int count) =>
        MarkBytes((ulong)first * (ulong)(RowsPerItem * sizeof(T)), (ulong)count * (ulong)(RowsPerItem * sizeof(T)));

    void MarkBytes(ulong offset, ulong size)
    {
        if (size == 0) return;

        if (_runCount > 0)
        {
            ref var last = ref _runs[_runCount - 1];
            var end = last.DstOffset + last.Size;
            if (offset == end || (_bridges && offset > end && offset - end <= BridgedGapBytes))
            {
                last.Size = offset + size - last.DstOffset;
                return;
            }
        }

        if (_runCount == _runs.Length) throw new InvalidOperationException("An item was marked twice between sends.");

        _runs[_runCount++] = new BufferCopy(offset, offset, size);
    }

    /// <summary>
    /// The marked runs copied to the device and forgotten, recorded — by a kernel past <see cref="CopiedRunsAtMost"/> of them;
    /// the caller barriers before a kernel reads them (<see cref="Vk.AfterTransfer"/>), and after any that wrote them
    /// (<see cref="Vk.BeforeTransfer"/>).
    /// </summary>
    public void Send(CommandBuffer commands)
    {
        if (_runCopy is null || _runCount <= CopiedRunsAtMost || _copiedByKernelIn == ComputeBatch.Begun)
        {
            _vk.Copy(commands, _staging, Device, _runs.AsSpan(0, _runCount));
        }
        else
        {
            _copiedByKernelIn = ComputeBatch.Begun;
            var words = _wordRuns!.Span<WordRun>();
            for (var run = 0; run < _runCount; run++)
            {
                ref readonly var copy = ref _runs[run];
                words[run] = new WordRun((uint)(copy.SrcOffset / sizeof(uint)), (uint)(copy.DstOffset / sizeof(uint)), (uint)(copy.Size / sizeof(uint)), 0u);
            }

            _runCopy.Dispatch(commands, _runCopySet, (uint)_runCount, (uint)_runCount);
        }

        _runCount = 0;
    }

    /// <summary>The first <paramref name="items"/> items copied back, recorded; read once the submit is waited for.</summary>
    public void Read(CommandBuffer commands, int items) =>
        _vk.Copy(commands, Device, _readback!, (ulong)(items * RowsPerItem * sizeof(T)));

    public void Dispose()
    {
        _runCopy?.Dispose();
        _wordRuns?.Dispose();
        Device.Dispose();
        _staging.Dispose();
        _readback?.Dispose();
    }
}

/// <summary>One run <c>copy_runs.comp</c> copies: where it starts in each buffer and how long it is, all in 32-bit words.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct WordRun(uint From, uint To, uint Words, uint Pad);
