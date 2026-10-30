namespace TrafficSimulation.Multiplayer.Wire;

/// <summary>
/// The reading half of <see cref="BitWriter"/>, over bytes nobody vouches for (MP-10). <b>It never throws</b>: a read
/// past the end, or a field above the most its reader allows, sets <see cref="Failed"/> and reads zero from then on, so
/// a message is read to its end and judged once.
/// </summary>
internal ref struct BitReader(ReadOnlySpan<byte> from)
{
    readonly ReadOnlySpan<byte> _from = from;
    ulong _scratch;
    int _scratchBits;
    int _next;

    public bool Failed { get; private set; }

    public uint Read(int bits)
    {
        while (_scratchBits < bits)
        {
            if (_next >= _from.Length)
            {
                Failed = true;
                return 0;
            }

            _scratch |= (ulong)_from[_next++] << _scratchBits;
            _scratchBits += 8;
        }

        var value = (uint)(_scratch & BitWriter.Mask(bits));
        _scratch >>= bits;
        _scratchBits -= bits;
        return Failed ? 0 : value;
    }

    /// <summary>A field no larger than <paramref name="most"/>; one larger fails the message.</summary>
    public uint Read(int bits, uint most)
    {
        var value = Read(bits);
        if (value <= most) return value;

        Failed = true;
        return 0;
    }

    public bool ReadBool() => Read(1) != 0;

    /// <summary>The message failed by a field that read cleanly but cannot stand for anything, judged by its reader.</summary>
    public void Fail() => Failed = true;

    public int ReadSigned(int bits) => Zigzag.Decode(Read(bits));

    /// <summary>
    /// <b>Whether the message read cleanly to its end</b>: nothing failed, every byte was taken, and the padding of the
    /// last one is the zeros a writer leaves — so trailing bytes are a malformed message and not ignored ones.
    /// </summary>
    public readonly bool Finished => !Failed && _next == _from.Length && _scratch == 0;
}
