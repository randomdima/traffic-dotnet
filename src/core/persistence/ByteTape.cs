using System.Buffers.Binary;
using System.Numerics;

namespace TrafficSimulation.Core.Persistence;

/// <summary>
/// A write head, laying one field at a time in the conventions <see cref="ByteCursor"/> reads:
/// little-endian, no padding and no alignment. <b>The two are one contract</b> — a field written here is
/// a field taken there, in the same order and the same width, and the round trip is what says so.
/// </summary>
/// <remarks>
/// It grows a buffer, which is the one thing <see cref="ByteCursor"/> refuses to do: a town is written
/// by a tool laying a map and read by a game standing one up, and only the second of those is on a
/// clock. Nothing on a tick writes bytes.
/// </remarks>
internal sealed class ByteTape
{
    readonly List<byte> _bytes = new(1 << 16);

    public int Length => _bytes.Count;

    public void U8(byte value) => _bytes.Add(value);

    public void I8(sbyte value) => _bytes.Add((byte)value);

    public void U32(uint value)
    {
        Span<byte> four = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(four, value);
        _bytes.AddRange(four);
    }

    public void U64(ulong value)
    {
        Span<byte> eight = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(eight, value);
        _bytes.AddRange(eight);
    }

    public void F32(float value)
    {
        Span<byte> four = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(four, value);
        _bytes.AddRange(four);
    }

    public void V2(Vector2 value)
    {
        F32(value.X);
        F32(value.Y);
    }

    /// <summary>The <c>u32</c> that counts the run after it, which is how every run in the format begins.</summary>
    public void Count(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        U32((uint)count);
    }

    public void Bytes(ReadOnlySpan<byte> bytes) => _bytes.AddRange(bytes);

    public byte[] Written() => _bytes.ToArray();
}
