namespace TrafficSimulation.Multiplayer.Wire;

/// <summary>
/// Fields packed to the bit into a caller's buffer, least significant bit first. <b>It never throws and never grows</b>:
/// a write past the buffer's end sets <see cref="Overflowed"/> and is dropped, and the message is not sent.
/// </summary>
internal ref struct BitWriter(Span<byte> into)
{
    readonly Span<byte> _into = into;
    ulong _scratch;
    int _scratchBits;
    int _bytes;

    /// <summary>Whether anything was written past the end of the buffer.</summary>
    public bool Overflowed { get; private set; }

    /// <summary>The low <paramref name="bits"/> of a value, 1 to 32 of them.</summary>
    public void Write(uint value, int bits)
    {
        _scratch |= (value & Mask(bits)) << _scratchBits;
        _scratchBits += bits;
        while (_scratchBits >= 8)
        {
            if (_bytes < _into.Length) _into[_bytes++] = (byte)_scratch;
            else Overflowed = true;

            _scratch >>= 8;
            _scratchBits -= 8;
        }
    }

    public void Write(bool value) => Write(value ? 1u : 0u, 1);

    /// <summary>A signed value, zigzagged so a small one of either sign takes few bits.</summary>
    public void WriteSigned(int value, int bits) => Write(Zigzag.Encode(value), bits);

    /// <summary>The buffer's length written so far, the last byte padded with zeros, which is what a reader checks.</summary>
    public int Finish()
    {
        if (_scratchBits > 0) Write(0, 8 - _scratchBits);
        return _bytes;
    }

    internal static ulong Mask(int bits) => (1UL << bits) - 1;
}

/// <summary>A signed integer as an unsigned one in which magnitude, not sign, sets the bit count.</summary>
internal static class Zigzag
{
    public static uint Encode(int value) => (uint)((value << 1) ^ (value >> 31));

    public static int Decode(uint value) => (int)(value >> 1) ^ -(int)(value & 1);
}
