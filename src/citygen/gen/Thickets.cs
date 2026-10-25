using System.Numerics;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>Where the open ground grows thick and where it lies bare</b> (GEN-6g): a field over the map, off its seed, that the
/// wild pass's share is multiplied by — about one on the whole, nought in a clearing and up to two in a thicket.
/// </summary>
/// <remarks>
/// <para>
/// <b>Value noise, two octaves, each a lattice of the map's own draws hashed off the corner and not stored</b>, so a city
/// a hundred square kilometres across costs nothing to hold and any thread may ask it anything. The second octave is half
/// as wide and half as strong, which is what breaks a thicket's edge up from a blob into a ragged line.
/// </para>
/// <para>
/// <b>The contrast stretches the noise about its middle before it is clamped</b>: noise of two octaves seldom strays far
/// from half, so unstretched the field would thin and thicken by a quarter and the eye would read it as even.
/// </para>
/// </remarks>
internal readonly struct Thickets(ulong seed, float acrossM, float contrast)
{
    /// <summary>The most <see cref="At"/> answers: twice the field's mean, so the ground on the whole grows at its zone's share.</summary>
    public const float Thickest = 2f;

    /// <summary>How thickly a place grows against the zone's own share: nought to <see cref="Thickest"/>.</summary>
    public float At(Vector2 atM)
    {
        var noise = (Octave(atM / acrossM, 0) * (2f / 3f)) + (Octave(atM * 2f / acrossM, 1) * (1f / 3f));
        return Thickest * Math.Clamp(0.5f + ((noise - 0.5f) * contrast), 0f, 1f);
    }

    float Octave(Vector2 at, ulong octave)
    {
        var (column, row) = ((long)MathF.Floor(at.X), (long)MathF.Floor(at.Y));
        var (x, y) = (Smooth(at.X - column), Smooth(at.Y - row));
        var top = float.Lerp(Corner(column, row, octave), Corner(column + 1, row, octave), x);
        var bottom = float.Lerp(Corner(column, row + 1, octave), Corner(column + 1, row + 1, octave), x);
        return float.Lerp(top, bottom, y);
    }

    static float Smooth(float t) => t * t * (3f - (2f * t));

    /// <summary>One lattice corner's draw in [0, 1): SplitMix64's finaliser over the seed, the octave and the corner.</summary>
    float Corner(long column, long row, ulong octave)
    {
        var mixed = seed ^ (octave * 0x9E37_79B9_7F4A_7C15UL) ^ ((ulong)column * 0xBF58_476D_1CE4_E5B9UL) ^ ((ulong)row * 0x94D0_49BB_1331_11EBUL);
        mixed = (mixed ^ (mixed >> 30)) * 0xBF58_476D_1CE4_E5B9UL;
        mixed = (mixed ^ (mixed >> 27)) * 0x94D0_49BB_1331_11EBUL;
        mixed ^= mixed >> 31;
        return (mixed >> 40) * (1f / (1 << 24));
    }
}
