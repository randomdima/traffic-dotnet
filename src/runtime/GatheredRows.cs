using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace TrafficSimulation.Runtime;

/// <summary>
/// <b>Rows the device keeps, handed to the host only where named</b> (<c>rows_gather.comp</c>): a gather copies the named
/// items' rows out of a buffer on the device into the host's memory and stamps each, one dispatch whatever their count — so
/// what crosses follows what is read, not how many there are. A row is the host's to read where it carries the stamp
/// asked for (<see cref="Holds"/>).
/// </summary>
/// <remarks>
/// <b>The names are the host's or a kernel's.</b> The host names items into a list a slot (<see cref="Name"/>), sent with
/// the gather that records it and not named into again until that gather is waited for; a kernel's list and its count are
/// buffers on the device (<see cref="BindListed"/>), gathered in the submit that wrote them.
/// </remarks>
internal sealed unsafe class GatheredRows<T> : IDisposable where T : unmanaged
{
    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Push(uint RowWords, uint Stamp, uint Most, uint CountAt, uint NamedFrom);

    readonly GpuBuffer _rows, _stamps;

    /// <summary>Each slot's list: how many in the first word, the items after it, so a gather reads both off one buffer.</summary>
    readonly GpuBuffer[] _named;

    readonly int[] _namedCount;
    readonly ComputeKernel _kernel;

    /// <param name="items">How many items, a row each, the rows hold room for.</param>
    /// <param name="slots">How many lists the host names items into, each gathered on its own.</param>
    /// <param name="sets">How many gathers will be bound, one a source and a list.</param>
    public GatheredRows(Vk vk, int items, int slots, int sets)
    {
        if (sizeof(T) % sizeof(uint) != 0) throw new ArgumentException($"A row of {sizeof(T)} bytes is not whole words.");

        var room = Math.Max(1, items);
        _rows = vk.CreateBuffer((ulong)(room * sizeof(T)), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _stamps = vk.CreateBuffer((ulong)(room * sizeof(uint)), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        _stamps.Span<uint>().Clear();
        _named = new GpuBuffer[slots];
        for (var slot = 0; slot < slots; slot++)
        {
            _named[slot] = vk.CreateBuffer((ulong)((room + 1) * sizeof(uint)), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        }

        _namedCount = new int[slots];
        _kernel = new ComputeKernel(vk, "rows_gather.comp", 5, (uint)sizeof(Push), sets);
    }

    /// <summary>A gather of the items named into <paramref name="slot"/>, out of <paramref name="from"/> — bound once for every gather of them.</summary>
    public DescriptorSet BindNamed(GpuBuffer from, int slot) => _kernel.Bind([from, _rows, _stamps, _named[slot], _named[slot]]);

    /// <summary>
    /// A gather of the items a kernel listed in <paramref name="list"/>, out of <paramref name="from"/>, as many as a word of
    /// <paramref name="counts"/> says (<see cref="Record(CommandBuffer, DescriptorSet, int, int, uint)"/>).
    /// </summary>
    public DescriptorSet BindListed(GpuBuffer from, GpuBuffer list, GpuBuffer counts) => _kernel.Bind([from, _rows, _stamps, list, counts]);

    /// <summary>One item named into a slot's list; an item named twice is gathered twice.</summary>
    public void Name(int slot, int item) => _named[slot].Span<uint>()[1 + _namedCount[slot]++] = (uint)item;

    /// <summary>How many items are named into a slot's list since it was last gathered.</summary>
    public int NamedIn(int slot) => _namedCount[slot];

    /// <summary>A slot's named items gathered out of what <paramref name="named"/> binds, stamped, recorded — and the slot emptied.</summary>
    public void Record(CommandBuffer commands, DescriptorSet named, int slot, uint stamp)
    {
        var count = _namedCount[slot];
        _namedCount[slot] = 0;
        if (count == 0) return;

        _named[slot].Span<uint>()[0] = (uint)count;
        _kernel.Dispatch(commands, named, Groups(count), new Push(RowWords, stamp, (uint)count, 0u, 1u));
        GatheredBytes += (ulong)count * (ulong)(sizeof(T) + sizeof(uint));
    }

    /// <summary>
    /// The items a kernel listed gathered, stamped, recorded: as many as word <paramref name="countAt"/> of the bound counts
    /// says when the queue reaches it, and at most <paramref name="most"/>.
    /// </summary>
    public void Record(CommandBuffer commands, DescriptorSet listed, int countAt, int most, uint stamp) =>
        _kernel.Dispatch(commands, listed, Groups(most), new Push(RowWords, stamp, (uint)most, (uint)countAt, 0u));

    /// <summary>Whether an item's row was last gathered with this stamp, read once that gather's submit is waited for.</summary>
    public bool Holds(int item, uint stamp) => _stamps.Span<uint>()[item] == stamp;

    /// <summary>An item's row as its last gather left it.</summary>
    public ref readonly T Row(int item) => ref _rows.Span<T>()[item];

    /// <summary>How many bytes the host's named gathers wrote into its memory, rows and stamps, since this was last set.</summary>
    public ulong GatheredBytes { get; set; }

    static uint RowWords => (uint)(sizeof(T) / sizeof(uint));

    static uint Groups(int count) => (uint)Math.Max(1, (count + 63) / 64);

    public void Dispose()
    {
        _kernel.Dispose();
        foreach (var named in _named) named.Dispose();
        _stamps.Dispose();
        _rows.Dispose();
    }
}
