using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>A pair of floats as two halves in one word, <c>x</c> in the low sixteen bits.</summary>
/// <remarks>
/// <b>Rounded to the nearest half, ties to even</b> (<see cref="Half"/>'s conversion), which a kernel does in integer
/// arithmetic (<c>floats.glsl</c>'s <c>floatHalf</c>) rather than through <c>packHalf2x16</c>, whose rounding a driver
/// chooses — so a word the device packs is the host's to the bit. Unpacking is exact on either.
/// </remarks>
internal static class Halves
{
    public static uint Pack(Vector2 pair) =>
        BitConverter.HalfToUInt16Bits((Half)pair.X) | ((uint)BitConverter.HalfToUInt16Bits((Half)pair.Y) << 16);

    public static Vector2 Unpack(uint word) =>
        new((float)BitConverter.UInt16BitsToHalf((ushort)word), (float)BitConverter.UInt16BitsToHalf((ushort)(word >> 16)));
}
