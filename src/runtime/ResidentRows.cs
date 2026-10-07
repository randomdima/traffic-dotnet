using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>Rows the device keeps from one submit to the next</b>, a fixed number to an item, with a mapped copy beside them
/// the host writes into: it writes the rows of the items it changed, marks them, and <see cref="Send"/> copies only
/// those — as one copy, however many there are, so a submit crosses once a table whatever the town's size.
/// </summary>
/// <remarks>
/// <para>
/// <b>An item is marked at most once between sends</b>: marks are kept as runs, an item marked where the last run ends
/// extending it, so a dense pass is one run and the list of them — laid for every item at construction — never grows.
/// </para>
/// <para>
/// <b>Rows only the host writes</b> (<c>hostOnly</c>) are laid with <see cref="Lay"/>, which marks a row only where it
/// differs from the last laid, and a run is carried over a gap of unmarked rows shorter than <see cref="BridgedGapBytes"/>:
/// those rows' mapped copy is what the device holds, so sending them again changes nothing and saves a region. A table laid
/// with <see cref="Lay"/> is written through it alone — <b>or a kernel writes rows of it and the host is told what it
/// wrote</b> (<see cref="Assume"/>), which keeps the mapped copy what the device holds — <b>or is not told</b>
/// (<see cref="Forget"/>), in a table that bridges no gap, and the row is sent whole the next time it is laid.
/// </para>
/// </remarks>
internal sealed unsafe class ResidentRows<T> : IDisposable where T : unmanaged
{
    /// <summary>
    /// The gap a run is carried over rather than a second begun: a region costs a copy about what this many bytes do
    /// (<c>--bench devicecopy</c>: 0.85 µs a region against 1.8 GB/s on a PCIe link of 5 GT/s × 4).
    /// </summary>
    const ulong BridgedGapBytes = 1536;

    readonly Vk _vk;
    readonly GpuBuffer _staging;
    readonly GpuBuffer? _readback;
    readonly BufferCopy[] _runs;
    readonly T[]? _laid;

    /// <summary>The items whose row a kernel wrote and the host does not know (<see cref="Forget"/>).</summary>
    readonly bool[]? _forgotten;

    readonly bool _bridges;
    int _runCount;

    /// <param name="readsBack">Whether the rows are ever handed back to the host (<see cref="Read"/>).</param>
    /// <param name="hostOnly">Whether only the host writes the rows, and lays them with <see cref="Lay"/>.</param>
    /// <param name="bridges">
    /// Whether a run is carried over a gap of unmarked rows (<see cref="BridgedGapBytes"/>) — never for a table whose rows a
    /// kernel writes without the host knowing what (<see cref="Forget"/>), since the mapped copy is then not what the device
    /// holds.
    /// </param>
    public ResidentRows(Vk vk, int items, int rowsPerItem, bool readsBack = false, bool hostOnly = false, bool bridges = true)
    {
        _vk = vk;
        Items = items;
        RowsPerItem = rowsPerItem;
        _bridges = hostOnly && bridges;
        var bytes = (ulong)(Math.Max(1, items * rowsPerItem) * sizeof(T));
        Device = vk.CreateBuffer(
            bytes, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        _staging = vk.CreateBuffer(bytes, BufferUsageFlags.TransferSrcBit, HostAccess.Writes);
        if (readsBack) _readback = vk.CreateBuffer(bytes, BufferUsageFlags.TransferDstBit, HostAccess.Reads);
        _runs = new BufferCopy[Math.Max(1, items)];
        if (!hostOnly) return;

        // The device, the mapped copy and the last laid all begin as zeros, so a row laid as zeros needs no sending.
        _laid = new T[items * rowsPerItem];
        _forgotten = new bool[Math.Max(1, items)];
        All.Clear();
        Device.Upload<T>(items * rowsPerItem, static into => into.Clear(), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
    }

    public int Items { get; }

    public int RowsPerItem { get; }

    /// <summary>The rows as the device keeps them, for a kernel to bind.</summary>
    public GpuBuffer Device { get; }

    /// <summary>Every item's rows as the host writes them, to be marked.</summary>
    public Span<T> All => _staging.Span<T>()[..(Items * RowsPerItem)];

    /// <summary>One item's rows as the host writes them before marking it.</summary>
    public Span<T> Rows(int item) => _staging.Span<T>().Slice(item * RowsPerItem, RowsPerItem);

    /// <summary>One item's rows as the last <see cref="Read"/> handed them back.</summary>
    public ReadOnlySpan<T> RowsRead(int item) => _readback!.Span<T>().Slice(item * RowsPerItem, RowsPerItem);

    /// <summary>Every item's rows as the last <see cref="Read"/> handed them back.</summary>
    public ReadOnlySpan<T> AllRead => _readback!.Span<T>()[..(Items * RowsPerItem)];

    public void Mark(int item) => Mark(item, 1);

    /// <summary>One item's single row written and marked, unless it is bit for bit the row last laid there.</summary>
    public void Lay(int item, in T row)
    {
        ref var laid = ref _laid![item * RowsPerItem];
        if (!_forgotten![item] && MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in row)).SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in laid))))
        {
            return;
        }

        _forgotten[item] = false;
        laid = row;
        Rows(item)[0] = row;
        Mark(item);
    }

    /// <summary>
    /// The first of one item's rows written and marked, unless they are bit for bit those last laid there — or
    /// <paramref name="again"/>, where what the device holds may be other than what was laid.
    /// </summary>
    public void LayRows(int item, ReadOnlySpan<T> rows, bool again = false)
    {
        var laid = _laid!.AsSpan(item * RowsPerItem, rows.Length);
        if (!again && !_forgotten![item] && MemoryMarshal.AsBytes(rows).SequenceEqual(MemoryMarshal.AsBytes(laid))) return;

        _forgotten![item] = false;
        rows.CopyTo(laid);
        rows.CopyTo(Rows(item));
        MarkRows(item, rows.Length);
    }

    /// <summary>One item's single row as last laid (<see cref="Lay"/>) — read off the host's own copy, never the mapped one.</summary>
    public ref readonly T LaidRow(int item) => ref _laid![item * RowsPerItem];

    /// <summary>
    /// <b>One item's single row taken as what the device now holds</b>, written there by a kernel rather than sent: the
    /// next <see cref="Lay"/> is read against it, and a gap a run is carried over sends it unchanged.
    /// </summary>
    public void Assume(int item, in T row)
    {
        _laid![item * RowsPerItem] = row;
        _forgotten![item] = false;
        Rows(item)[0] = row;
    }

    /// <summary>
    /// <b>One item's single row left to whatever a kernel wrote there</b>, which the host does not know: the next
    /// <see cref="Lay"/> sends its row whatever was laid before. Only for a table that does not bridge.
    /// </summary>
    public void Forget(int item)
    {
        if (_bridges) throw new InvalidOperationException("A table whose gaps are bridged keeps the device's rows in its mapped copy; it cannot forget one.");

        _forgotten![item] = true;
    }

    /// <summary>A run of items to be sent, joined to the last run where it begins at that run's end.</summary>
    public void Mark(int first, int count) =>
        MarkBytes((ulong)first * (ulong)(RowsPerItem * sizeof(T)), (ulong)count * (ulong)(RowsPerItem * sizeof(T)));

    /// <summary>Only the first <paramref name="rows"/> of one item's rows to be sent — the rest of its room unread.</summary>
    public void MarkRows(int item, int rows) =>
        MarkBytes((ulong)item * (ulong)(RowsPerItem * sizeof(T)), (ulong)Math.Min(rows, RowsPerItem) * (ulong)sizeof(T));

    /// <summary>How many bytes the marks so far will send.</summary>
    public ulong MarkedBytes { get; private set; }

    void MarkBytes(ulong offset, ulong size)
    {
        if (size == 0) return;

        MarkedBytes += size;
        if (_runCount > 0)
        {
            ref var last = ref _runs[_runCount - 1];
            var end = last.DstOffset + last.Size;
            if (offset == end || (_bridges && offset > end && offset - end <= BridgedGapBytes))
            {
                MarkedBytes += offset - end;
                last.Size = offset + size - last.DstOffset;
                return;
            }
        }

        if (_runCount == _runs.Length) throw new InvalidOperationException("An item was marked twice between sends.");

        _runs[_runCount++] = new BufferCopy(offset, offset, size);
    }

    /// <summary>The marked runs copied to the device and forgotten, recorded; the caller barriers before a kernel reads them.</summary>
    public void Send(CommandBuffer commands)
    {
        _vk.Copy(commands, _staging, Device, _runs.AsSpan(0, _runCount));
        _runCount = 0;
        MarkedBytes = 0;
    }

    /// <summary>The first <paramref name="items"/> items copied back, recorded; read once the submit is waited for.</summary>
    public void Read(CommandBuffer commands, int items) =>
        _vk.Copy(commands, Device, _readback!, (ulong)(items * RowsPerItem * sizeof(T)));

    /// <summary>
    /// Only these items copied back, each where <see cref="RowsRead"/> reads it, recorded as one copy — an item next to the
    /// last joined to its run. The items are distinct; <paramref name="runs"/> is the caller's room for the runs.
    /// </summary>
    public void ReadItems(CommandBuffer commands, ReadOnlySpan<int> items, Span<BufferCopy> runs)
    {
        var itemBytes = (ulong)(RowsPerItem * sizeof(T));
        var count = 0;
        foreach (var item in items)
        {
            var offset = (ulong)item * itemBytes;
            if (count > 0 && runs[count - 1].SrcOffset + runs[count - 1].Size == offset)
            {
                runs[count - 1].Size += itemBytes;
                continue;
            }

            runs[count++] = new BufferCopy(offset, offset, itemBytes);
        }

        _vk.Copy(commands, Device, _readback!, runs[..count]);
    }

    public void Dispose()
    {
        Device.Dispose();
        _staging.Dispose();
        _readback?.Dispose();
    }
}
