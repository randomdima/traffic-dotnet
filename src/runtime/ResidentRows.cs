using Silk.NET.Vulkan;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>Rows the device keeps from one submit to the next</b>, a fixed number to an item, with a mapped copy beside them
/// the host writes into: it writes the rows of the items it changed, marks them, and <see cref="Send"/> copies only
/// those — as one copy, however many there are, so a submit crosses once a table whatever the town's size.
/// </summary>
/// <remarks>
/// <b>An item is marked at most once between sends</b>: marks are kept as runs, an item marked where the last run ends
/// extending it, so a dense pass is one run and the list of them — laid for every item at construction — never grows.
/// </remarks>
internal sealed unsafe class ResidentRows<T> : IDisposable where T : unmanaged
{
    readonly Vk _vk;
    readonly GpuBuffer _staging;
    readonly GpuBuffer? _readback;
    readonly BufferCopy[] _runs;
    int _runCount;

    /// <param name="readsBack">Whether the rows are ever handed back to the host (<see cref="Read"/>).</param>
    public ResidentRows(Vk vk, int items, int rowsPerItem, bool readsBack = false)
    {
        _vk = vk;
        Items = items;
        RowsPerItem = rowsPerItem;
        var bytes = (ulong)(Math.Max(1, items * rowsPerItem) * sizeof(T));
        Device = vk.CreateBuffer(
            bytes, BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        _staging = vk.CreateBuffer(bytes, BufferUsageFlags.TransferSrcBit, HostAccess.Writes);
        if (readsBack) _readback = vk.CreateBuffer(bytes, BufferUsageFlags.TransferDstBit, HostAccess.Reads);
        _runs = new BufferCopy[Math.Max(1, items)];
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
        if (_runCount > 0 && _runs[_runCount - 1].DstOffset + _runs[_runCount - 1].Size == offset)
        {
            _runs[_runCount - 1].Size += size;
            return;
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

    public void Dispose()
    {
        Device.Dispose();
        _staging.Dispose();
        _readback?.Dispose();
    }
}
