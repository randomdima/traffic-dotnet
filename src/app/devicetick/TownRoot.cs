using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>Every table of the town by its id</b>, as the address a resident kernel reaches it at
/// (<c>world/town/kernels/town_root.glsl</c>): bound at set 0, binding 0, so a kernel needs nothing else bound to read or
/// write any table the town holds.
/// </summary>
/// <remarks>
/// <b>One id a table and one table a field</b>: each module names its ids in its own range and its own header, and a
/// field an existing row table already holds is reached there. A table that grows moves, and is set again here
/// (<see cref="GpuBuffer.Grow"/> does it).
/// </remarks>
internal sealed class TownRoot : IAddressBook, IDisposable
{
    /// <summary>How many ids the root has room for — every module's range.</summary>
    public const int Ids = 2048;

    readonly GpuBuffer _root;
    readonly GpuBuffer?[] _tables = new GpuBuffer?[Ids];

    public TownRoot(Vk vk)
    {
        _root = vk.CreateBuffer(GpuBuffer.Bytes<ulong>(Ids), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        _root.Span<ulong>().Clear();
    }

    /// <summary>What a resident kernel binds first.</summary>
    public GpuBuffer Buffer => _root;

    /// <summary>The table an id names, or none yet.</summary>
    public GpuBuffer? this[int id] => _tables[id];

    /// <summary>
    /// <b>A table set at its id</b>, its address written where kernels read it. An id names one table for the root's life.
    /// </summary>
    public void Set(int id, GpuBuffer table)
    {
        if (_tables[id] is { } held && !ReferenceEquals(held, table))
        {
            throw new InvalidOperationException($"Id {id} already names another table.");
        }

        _tables[id] = table;
        table.AddressedIn(this, id);
        Moved(id, table);
    }

    /// <summary>A table that moved, its new address written at its id.</summary>
    public void Moved(int id, GpuBuffer buffer) => _root.Span<ulong>()[id] = buffer.Address;

    public void Dispose() => _root.Dispose();
}
