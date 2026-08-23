using System.Buffers.Binary;
using System.Numerics;

namespace TrafficSimulation.Core.Persistence;

/// <summary>
/// A read head over a block of bytes, taking one field at a time: little-endian, no padding and no
/// alignment, which is the whole of the <c>.town</c> format's conventions. A <c>ref struct</c> over the
/// caller's span, so reading a nine-megabyte town copies it no further than the array it was read into.
/// </summary>
/// <remarks>
/// Every overrun is a <see cref="FormatException"/> naming the offset: a file that is nearly the one
/// that was written is the worst outcome, because it is the one that looks like it worked.
/// </remarks>
internal ref struct ByteCursor(ReadOnlySpan<byte> bytes)
{
    readonly ReadOnlySpan<byte> _bytes = bytes;
    int _at;

    public readonly int Offset => _at;

    public readonly int Remaining => _bytes.Length - _at;

    public byte U8() => Take(1)[0];

    public sbyte I8() => (sbyte)Take(1)[0];

    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    public ulong U64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));

    public float F32() => BinaryPrimitives.ReadSingleLittleEndian(Take(4));

    public Vector2 V2() => new(F32(), F32());

    /// <summary>
    /// A <c>u32</c> that counts the run after it, refused rather than trusted when it cannot be one: a
    /// corrupt count is how a reader ends up allocating gigabytes before it notices.
    /// </summary>
    public int Count(string what, int bytesEach)
    {
        var count = U32();
        if (count > (uint)int.MaxValue || (long)count * bytesEach > Remaining)
        {
            throw new FormatException($"{what}: a count of {count} at offset {_at - 4} needs more than the {Remaining} bytes left.");
        }

        return (int)count;
    }

    public ReadOnlySpan<byte> Take(int count)
    {
        if (count > Remaining) throw new FormatException($"Truncated: {count} bytes wanted at offset {_at}, {Remaining} left.");

        var taken = _bytes.Slice(_at, count);
        _at += count;
        return taken;
    }
}
