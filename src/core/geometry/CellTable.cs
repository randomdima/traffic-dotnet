using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>A cell's key to a number, for an index that files cells sparsely while it is still being added to</b> (SIM-8): the
/// keys <see cref="GridLevel.KeyOf(int, int)"/> makes, held open-addressed rather than in a <c>Dictionary</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because most asks are of a cell that holds nothing</b>, round a point a ring of cells at a time, and an empty slot
/// answers that in one read where a dictionary hashes, divides and walks a bucket. A key is already the cell multiplied by an
/// odd constant, so its top bits are the slot and no second hash is taken.
/// </para>
/// <para>
/// <b>Never more than half full</b>, so a run of taken slots stays short, and nothing is ever removed. A slot holds the number
/// one up, so a fresh array is every slot empty.
/// </para>
/// </remarks>
internal sealed class CellTable
{
    struct Slot
    {
        public long Key;
        public int ValueUp;
    }

    Slot[] _slots;
    int _shift;

    public CellTable(int capacity = 0)
    {
        var slots = Math.Max(16, (int)BitOperations.RoundUpToPowerOf2((uint)capacity * 2u));
        _slots = new Slot[slots];
        _shift = 64 - BitOperations.Log2((uint)slots);
    }

    public int Count { get; private set; }

    /// <summary>The number filed against a cell, or −1 where none is.</summary>
    public int this[long key]
    {
        get
        {
            var mask = _slots.Length - 1;
            for (var at = SlotOf(key); ; at = (at + 1) & mask)
            {
                ref readonly var slot = ref _slots[at];
                if (slot.ValueUp == 0) return -1;
                if (slot.Key == key) return slot.ValueUp - 1;
            }
        }
    }

    /// <summary>Files a number against a cell, in place of whatever was filed there. A number is never negative.</summary>
    public void Set(long key, int value)
    {
        if ((Count + 1) * 2 > _slots.Length) Grow();

        var mask = _slots.Length - 1;
        for (var at = SlotOf(key); ; at = (at + 1) & mask)
        {
            ref var slot = ref _slots[at];
            if (slot.ValueUp == 0)
            {
                (slot.Key, slot.ValueUp) = (key, value + 1);
                Count++;
                return;
            }

            if (slot.Key != key) continue;

            slot.ValueUp = value + 1;
            return;
        }
    }

    int SlotOf(long key) => (int)((ulong)key >> _shift);

    void Grow()
    {
        var old = _slots;
        _slots = new Slot[old.Length * 2];
        _shift--;
        var mask = _slots.Length - 1;
        foreach (var slot in old)
        {
            if (slot.ValueUp == 0) continue;

            var at = SlotOf(slot.Key);
            while (_slots[at].ValueUp != 0) at = (at + 1) & mask;
            _slots[at] = slot;
        }
    }
}
