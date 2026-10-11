using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// <b>Every field of the town no row table holds, on the device</b>: each per-agent array, each registry and the static
/// map the decisions read, laid once off a <see cref="TownWorld"/> into a device-local table at its own id of the root
/// (<see cref="TownRoot"/>), where <c>world/town/kernels/town_cars.glsl</c> and its siblings name it after its C# field.
/// </summary>
/// <remarks>
/// <para>
/// <b>One storage place a field</b>: a field one of <c>town_rows.glsl</c>'s tables already holds is reached there and laid
/// by whoever lays that table, never here. Each module's tables are laid by its own partial, at the ids its header names
/// in the module's range — the two must agree id for id, which <c>TownStateTests</c> holds.
/// </para>
/// <para>
/// <b>Laid once, at load</b>: one staged upload a table, a submit and a wait each, and nothing a tick. A table is sized for
/// the fleets' capacities, and a <see cref="bool"/>, a byte or an enum is widened to a 32-bit word.
/// </para>
/// </remarks>
internal sealed partial class TownState : IDisposable
{
    readonly Vk _vk;
    readonly TownRoot _root;
    readonly List<GpuBuffer> _owned = [];

    public TownState(Vk vk, TownRoot root, TownWorld town)
    {
        _vk = vk;
        _root = root;
        LayTheCars(town);
        LayThePeople(town);
        LayThePlaces(town);
        LayTheErrands(town);
        LayTheMap(town);
        LayTheLaneIndexes(town);
        LayTheCarActions(town);
        LayTheWalkerActions(town);
        LayTheLegs(town);
    }

    /// <summary>A device-local table holding these rows, sent once through a staging buffer and set at its id.</summary>
    void Lay<T>(int id, ReadOnlySpan<T> rows) where T : unmanaged
    {
        var buffer = _vk.CreateBuffer(
            GpuBuffer.Bytes<T>(rows.Length), BufferUsageFlags.StorageBufferBit | BufferUsageFlags.TransferDstBit, HostAccess.None);
        _owned.Add(buffer);
        var copy = rows.ToArray();
        buffer.Upload<T>(copy.Length, into => copy.CopyTo(into), PipelineStageFlags2.ComputeShaderBit, AccessFlags2.ShaderStorageReadBit);
        _root.Set(id, buffer);
    }

    /// <summary>A table of one row a value, each the row <paramref name="row"/> makes of it.</summary>
    void Lay<TValue, TRow>(int id, ReadOnlySpan<TValue> values, Func<TValue, TRow> row) where TRow : unmanaged
    {
        var rows = new TRow[values.Length];
        for (var at = 0; at < rows.Length; at++) rows[at] = row(values[at]);
        Lay<TRow>(id, rows);
    }

    /// <summary>A table of flags, one word a flag.</summary>
    void LayFlags(int id, ReadOnlySpan<bool> flags) => Lay(id, flags, flag => flag ? 1u : 0u);

    /// <summary>
    /// <b>A table of narrow values or an enum's, one word a value</b>: a byte, a short or an enum of either widened without
    /// its sign, an enum of four bytes copied bit for bit — so a <c>−1</c> of one is read back through an <c>Ints</c> table.
    /// </summary>
    void LayWidened<T>(int id, ReadOnlySpan<T> values) where T : unmanaged
    {
        var words = new uint[values.Length];
        for (var at = 0; at < words.Length; at++) words[at] = Widened(values[at]);
        Lay<uint>(id, words);
    }

    static uint Widened<T>(T value) where T : unmanaged => Unsafe.SizeOf<T>() switch
    {
        1 => Unsafe.As<T, byte>(ref value),
        2 => Unsafe.As<T, ushort>(ref value),
        4 => Unsafe.As<T, uint>(ref value),
        _ => throw new NotSupportedException($"{typeof(T).Name} is not a word or narrower."),
    };

    /// <summary>One row of scalars, the module's own (its <c>…State</c> struct in its header).</summary>
    void LayOne<T>(int id, T row) where T : unmanaged => Lay<T>(id, [row]);

    public void Dispose()
    {
        foreach (var buffer in _owned) buffer.Dispose();
    }
}
