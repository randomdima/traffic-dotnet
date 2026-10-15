using System.Runtime.CompilerServices;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// The world layer's trigonometry, in floats alone: the same operations in the same order as
/// <c>core/geometry/kernels/trig.glsl</c>, so a body stepped by the managed world and by the device turns to the same bit.
/// </summary>
/// <remarks>
/// <para><b>Why not <see cref="MathF"/></b>: glibc's <c>sincosf</c> sums in doubles, which WebGPU has none of and a
/// handset's Vulkan seldom has, and its correctly rounded tangent and arctangents need a double's last bits to settle
/// (core log, 2026-10-15). These are Cephes' single-precision polynomials, within three floats of exact — nothing the town
/// can see — and every step a sum, product or quotient the host and any Vulkan driver round alike: the kernel's sums are
/// <c>precise</c>, and its quotients <c>rounded.glsl</c>'s nearest float. The .NET JIT fuses nothing it is not asked to.</para>
/// <para>WGSL cannot forbid a driver to fuse a product into a sum, so a page's kernels may stand a float off this.</para>
/// <para>A subnormal argument or answer is nought on the device, as in every kernel; here it is kept.</para>
/// </remarks>
internal static class Trig
{
    /// <summary>2/π, rounded to a float.</summary>
    const float TwoOverPi = 0.636619772367581343f;

    /// <summary>
    /// π/2 in three parts (Cephes' π/4 ones, doubled): the first two short enough that a whole number of quarter turns
    /// below <see cref="ShortWayBelow"/> times either is exact.
    /// </summary>
    const float HalfPi1 = 1.5703125f;
    const float HalfPi2 = 4.837512969970703125e-4f;
    const float HalfPi3 = 7.54978995489188216e-8f;

    /// <summary>
    /// Below this the argument is reduced by the parts above; past it, by 4/π's bits in integers (Payne and Hanek), which is
    /// exact to the last bit asked for whatever the argument.
    /// </summary>
    const float ShortWayBelow = 4096f;

    /// <summary>4/π's bits, a byte further along each word (glibc's <c>__inv_pio4</c>), for a large argument's reduction.</summary>
    static ReadOnlySpan<uint> FourOverPiBits =>
    [
        0xa2u, 0xa2f9u, 0xa2f983u, 0xa2f9836eu, 0xf9836e4eu, 0x836e4e44u, 0x6e4e4415u, 0x4e441529u, 0x441529fcu, 0x1529fc27u,
        0x29fc2757u, 0xfc2757d1u, 0x2757d1f5u, 0x57d1f534u, 0xd1f534ddu, 0xf534ddc0u, 0x34ddc0dbu, 0xddc0db62u, 0xc0db6295u,
        0xdb629599u, 0x6295993cu, 0x95993c43u, 0x993c4390u, 0x3c439041u,
    ];

    /// <summary>π/2 × 2^-62, rounded to a float: what one unit of a large argument's 62-bit fraction of a quadrant is worth.</summary>
    const float HalfPiOver2To62 = 3.40612158e-19f;

    /// <summary>Below this magnitude sine is the argument and cosine one, as the polynomials would answer.</summary>
    const float TinyBelow = 1f / 4096f;

    const float Sin0 = -1.9515295891e-4f;
    const float Sin1 = 8.3321608736e-3f;
    const float Sin2 = -1.6666654611e-1f;
    const float Cos0 = 2.443315711809948e-5f;
    const float Cos1 = -1.388731625493765e-3f;
    const float Cos2 = 4.166664568298827e-2f;

    const float Tan0 = 9.38540185543e-3f;
    const float Tan1 = 3.11992232697e-3f;
    const float Tan2 = 2.44301354525e-2f;
    const float Tan3 = 5.34112807005e-2f;
    const float Tan4 = 1.33387994085e-1f;
    const float Tan5 = 3.33331568548e-1f;

    /// <summary>tan(3π/8) and tan(π/8), where the arctangent folds its argument.</summary>
    const float Tan3PiOver8 = 2.414213562373095f;
    const float TanPiOver8 = 0.4142135623730950f;

    const float Atan0 = 8.05374449538e-2f;
    const float Atan1 = -1.38776856032e-1f;
    const float Atan2Coefficient = 1.99777106478e-1f;
    const float Atan3 = -3.33329491539e-1f;

    const float Pi = 3.14159265358979323846f;
    const float HalfPi = 1.57079632679489661923f;
    const float QuarterPi = 0.78539816339744830962f;

    /// <summary>sin and cos of <paramref name="x"/>.</summary>
    public static (float Sin, float Cos) SinCos(float x)
    {
        if (float.IsNaN(x) || float.IsInfinity(x)) return (float.NaN, float.NaN);

        var magnitude = MathF.Abs(x);
        if (magnitude < TinyBelow) return (x, 1f);

        var reduced = Reduce(x, out var quadrant);
        var (sine, cosine) = Kernel(reduced);
        return (quadrant & 3) switch
        {
            0 => (sine, cosine),
            1 => (cosine, -sine),
            2 => (-sine, -cosine),
            _ => (-cosine, sine),
        };
    }

    public static float Sin(float x) => SinCos(x).Sin;

    public static float Cos(float x) => SinCos(x).Cos;

    /// <summary>tan of <paramref name="x"/>; past <see cref="ShortWayBelow"/>, the sine over the cosine.</summary>
    public static float Tan(float x)
    {
        if (float.IsNaN(x) || float.IsInfinity(x)) return float.NaN;
        if (MathF.Abs(x) < TinyBelow) return x;

        if (MathF.Abs(x) >= ShortWayBelow)
        {
            var (sine, cosine) = SinCos(x);
            return sine / cosine;
        }

        var reduced = Reduce(x, out var quadrant);
        var squared = reduced * reduced;
        var series = Tan0 * squared + Tan1;
        series = series * squared + Tan2;
        series = series * squared + Tan3;
        series = series * squared + Tan4;
        series = series * squared + Tan5;
        var tangent = series * squared * reduced + reduced;
        return (quadrant & 1) != 0 ? -1f / tangent : tangent;
    }

    /// <summary>atan of <paramref name="x"/>.</summary>
    public static float Atan(float x)
    {
        if (float.IsNaN(x)) return float.NaN;

        var magnitude = MathF.Abs(x);
        var rad = AtanOfMagnitude(magnitude);
        return x < 0f ? -rad : rad;
    }

    /// <summary>The bearing of (<paramref name="x"/>, <paramref name="y"/>), signed zero by signed zero as <see cref="MathF.Atan2"/>.</summary>
    public static float Atan2(float y, float x)
    {
        if (float.IsNaN(x) || float.IsNaN(y)) return float.NaN;

        var below = float.IsNegative(y);
        var behind = float.IsNegative(x);
        if (x == 0f)
        {
            if (y == 0f) return behind ? (below ? -Pi : Pi) : y;
            return below ? -HalfPi : HalfPi;
        }

        if (float.IsInfinity(x))
        {
            var far = float.IsInfinity(y) ? (behind ? 3f * QuarterPi : QuarterPi) : (behind ? Pi : 0f);
            return below ? -far : far;
        }

        if (float.IsInfinity(y)) return below ? -HalfPi : HalfPi;
        if (y == 0f) return behind ? (below ? -Pi : Pi) : y;

        var z = AtanOfMagnitude(MathF.Abs(y) / MathF.Abs(x));
        var rad = behind ? Pi - z : z;
        return below ? -rad : rad;
    }

    static float AtanOfMagnitude(float magnitude)
    {
        float folded;
        float offset;
        if (magnitude > Tan3PiOver8)
        {
            folded = -1f / magnitude;
            offset = HalfPi;
        }
        else if (magnitude > TanPiOver8)
        {
            folded = (magnitude - 1f) / (magnitude + 1f);
            offset = QuarterPi;
        }
        else
        {
            folded = magnitude;
            offset = 0f;
        }

        var squared = folded * folded;
        var series = Atan0 * squared + Atan1;
        series = series * squared + Atan2Coefficient;
        series = series * squared + Atan3;
        var rad = series * squared * folded + folded;
        return offset + rad;
    }

    /// <summary>Sine and cosine of an argument within π/4 of nought.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static (float Sin, float Cos) Kernel(float reduced)
    {
        var squared = reduced * reduced;
        var sinSeries = Sin0 * squared + Sin1;
        sinSeries = sinSeries * squared + Sin2;
        var sine = sinSeries * squared * reduced + reduced;

        var cosSeries = Cos0 * squared + Cos1;
        cosSeries = cosSeries * squared + Cos2;
        var cosine = cosSeries * squared * squared - 0.5f * squared + 1f;
        return (sine, cosine);
    }

    /// <summary><paramref name="x"/> less its nearest whole number of quarter turns, which is <paramref name="quadrant"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float Reduce(float x, out int quadrant)
    {
        var magnitude = MathF.Abs(x);
        float reduced;
        if (magnitude < ShortWayBelow)
        {
            var turns = (int)(magnitude * TwoOverPi + 0.5f);
            var whole = (float)turns;
            reduced = magnitude - whole * HalfPi1;
            reduced -= whole * HalfPi2;
            reduced -= whole * HalfPi3;
            quadrant = turns;
        }
        else
        {
            reduced = ReduceLarge(BitConverter.SingleToUInt32Bits(magnitude), out quadrant);
        }

        if (x < 0f)
        {
            reduced = -reduced;
            quadrant = -quadrant;
        }

        return reduced;
    }

    /// <summary>
    /// glibc's <c>reduce_large</c> in 32-bit words, as the kernel takes it: the 62-bit fraction of a quadrant past the
    /// nearest whole one, turned to radians in floats.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static float ReduceLarge(uint bits, out int quadrant)
    {
        var at = (int)((bits >> 26) & 15u);
        var mantissa = ((bits & 0xffffffu) | 0x800000u) << (int)((bits >> 23) & 7u);
        var high = mantissa * FourOverPiBits[at];
        var middle = (ulong)mantissa * FourOverPiBits[at + 4];
        var lowest = (ulong)mantissa * FourOverPiBits[at + 8];

        var low = (uint)(lowest >> 32) + (uint)middle;
        var carry = low < (uint)middle ? 1u : 0u;
        high += (uint)(middle >> 32) + carry;
        var turns = (high + 0x20000000u) >> 30;
        high -= turns << 30;
        quadrant = (int)turns;

        var fraction = (float)(int)high * 4294967296f + (float)low;
        return fraction * HalfPiOver2To62;
    }
}
