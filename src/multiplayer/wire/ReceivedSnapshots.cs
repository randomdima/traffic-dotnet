namespace TrafficSimulation.Multiplayer.Wire;

/// <summary>
/// <b>The snapshots a player holds</b>, each read whole against the baseline it names, and kept by sequence to be named
/// as one in turn and to be drawn between (MP-7). The newest it holds is what it acknowledges.
/// </summary>
/// <remarks>
/// A snapshot older than the newest held is dropped, and one whose baseline is no longer held cannot be read and is
/// dropped too — the acknowledgement stays where it was, so the server's next is a difference from something held.
/// </remarks>
internal sealed class ReceivedSnapshots
{
    readonly WireShape _shape;
    readonly Snapshot[] _kept;
    readonly MoverState[] _entries;
    readonly int[] _left;

    /// <summary>What the server has said of every seat a snapshot can name, by its number.</summary>
    readonly SeatCard[] _cards = new SeatCard[1 << SeatCard.SeatBits];

    public ReceivedSnapshots(in WireShape shape, int kept, int mostMovers)
    {
        _shape = shape;
        _kept = new Snapshot[kept];
        for (var i = 0; i < kept; i++) _kept[i] = new Snapshot(mostMovers);
        _entries = new MoverState[mostMovers];
        _left = new int[mostMovers];
    }

    /// <summary>The newest snapshot held, or nought for none — what the player acknowledges.</summary>
    public uint Newest { get; private set; }

    /// <summary>How many slots it keeps, which is how far back a snapshot may be found.</summary>
    public int Kept => _kept.Length;

    /// <summary>
    /// A snapshot message read and kept, and whether it was: false for one older than the newest, one whose baseline is
    /// not held, and one that is not a snapshot at all (MP-10).
    /// </summary>
    public bool Take(ReadOnlySpan<byte> message)
    {
        var r = new BitReader(message);
        var header = SnapshotHeader.Read(ref r, _shape);

        // Before anything can drop the snapshot: the server has told these once and will not again.
        SeatCard.ReadSection(ref r, _cards);
        if (r.Failed || header.Sequence == 0 || header.Sequence <= Newest) return false;

        var into = SlotOf(header.Sequence);
        ReadOnlySpan<MoverState> baseline = default;
        if (header.Baseline != 0)
        {
            var held = SlotOf(header.Baseline);
            if (held.Sequence != header.Baseline || held == into) return false;

            baseline = held.Movers.AsSpan(0, held.Count);
        }

        var count = SnapshotCodec.Read(ref r, _shape, baseline, into.Movers, _entries, _left);
        if (count < 0 || !r.Finished)
        {
            into.Sequence = 0;
            return false;
        }

        into.Sequence = header.Sequence;
        into.Header = header;
        into.Count = count;
        Newest = header.Sequence;
        return true;
    }

    /// <summary>What the server last said of <paramref name="seat"/> (MP-15), or false before it has said anything.</summary>
    public bool Card(int seat, out SeatCard card)
    {
        card = (uint)seat < (uint)_cards.Length ? _cards[seat] : default;
        return card.Name is not null;
    }

    /// <summary>A held snapshot by its sequence, or null.</summary>
    public Snapshot? Find(uint sequence)
    {
        var slot = SlotOf(sequence);
        return sequence != 0 && slot.Sequence == sequence ? slot : null;
    }

    Snapshot SlotOf(uint sequence) => _kept[(int)(sequence % (uint)_kept.Length)];

    /// <summary>One snapshot held: what it said before its movers, and the movers, in mover order.</summary>
    internal sealed class Snapshot(int mostMovers)
    {
        public uint Sequence;
        public SnapshotHeader Header;
        public int Count;
        public readonly MoverState[] Movers = new MoverState[mostMovers];

        public ReadOnlySpan<MoverState> Held => Movers.AsSpan(0, Count);
    }
}
