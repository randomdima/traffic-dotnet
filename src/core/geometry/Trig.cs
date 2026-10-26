using System.Runtime.CompilerServices;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// The town's trigonometry, in floats alone: the same operations in the same order as
/// <c>core/geometry/kernels/trig.glsl</c>, so a body stepped by the managed world and by the device turns to the same bit,
/// and a town laid on the desktop and in a page is laid alike.
/// </summary>
/// <remarks>
/// <para><b>Nothing a town is laid or ticked through asks <see cref="MathF"/> or <see cref="Math"/> for a transcendental</b>
/// (core log, 2026-10-20): they are the platform's libm — glibc on the desktop, emscripten's in a page — and part by a
/// float, which a car park's bays carry into a different town. <see cref="Asin"/>, <see cref="Acos"/> and the double
/// <see cref="SinCos(double)"/> are the host's alone and no kernel mirrors them; <see cref="Powers"/> is the rest.</para>
/// <para><b>Why not <see cref="MathF"/></b>: glibc's <c>sincosf</c> sums in doubles, which WebGPU has none of and a
/// Vulkan driver need not have, and its correctly rounded tangent and arctangents need a double's last bits to settle
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
        return Quadrant(sine, cosine, quadrant & 3);
    }

    /// <summary>
    /// The kernel's pair turned through whole quarter turns: (s, c), (c, −s), (−s, −c), (−c, s). In bits rather than
    /// a switch, because a heading is in any quadrant and a branch on it is a guess; a float's negation is its sign
    /// bit, so the answer is the switch's to the bit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static (float Sin, float Cos) Quadrant(float sine, float cosine, int quadrant)
    {
        uint s = BitConverter.SingleToUInt32Bits(sine), c = BitConverter.SingleToUInt32Bits(cosine);
        var swap = (uint)-(quadrant & 1);
        var sinBits = ((s & ~swap) | (c & swap)) ^ ((uint)(quadrant & 2) << 30);
        var cosBits = ((c & ~swap) | (s & swap)) ^ ((uint)((quadrant + 1) & 2) << 30);
        return (BitConverter.UInt32BitsToSingle(sinBits), BitConverter.UInt32BitsToSingle(cosBits));
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

        // Both folds in bits, as the quadrant is (see Quadrant): x and y are not nought here, so behind and below are
        // their sign bits, and z is not negative, so its sign bit is y's.
        var z = AtanOfMagnitude(MathF.Abs(y) / MathF.Abs(x));
        var behindMask = (uint)((int)BitConverter.SingleToUInt32Bits(x) >> 31);
        var rad = (BitConverter.SingleToUInt32Bits(z) & ~behindMask) | (BitConverter.SingleToUInt32Bits(Pi - z) & behindMask);
        return BitConverter.UInt32BitsToSingle(rad ^ (BitConverter.SingleToUInt32Bits(y) & SignBit));
    }

    const uint SignBit = 0x8000_0000u;

    const float Asin0 = 4.2163199048e-2f;
    const float Asin1 = 2.4181311049e-2f;
    const float Asin2 = 4.5470025998e-2f;
    const float Asin3 = 7.4953002686e-2f;
    const float Asin4 = 1.6666752422e-1f;

    /// <summary>Below this the arcsine is its argument, as the series would answer.</summary>
    const float AsinTinyBelow = 1e-4f;

    /// <summary>asin of <paramref name="x"/> (Cephes' <c>asinf</c>); NaN outside [−1, 1].</summary>
    public static float Asin(float x)
    {
        var magnitude = MathF.Abs(x);
        if (!(magnitude <= 1f)) return float.NaN;

        // Past a half, asin(m) = π/2 − 2 asin(√((1 − m)/2)), whose argument the series is good for.
        var folded = magnitude > 0.5f;
        var z = folded ? 0.5f * (1f - magnitude) : magnitude * magnitude;
        var reduced = folded ? MathF.Sqrt(z) : magnitude;
        var rad = reduced;
        if (reduced >= AsinTinyBelow)
        {
            var series = Asin0 * z + Asin1;
            series = series * z + Asin2;
            series = series * z + Asin3;
            series = series * z + Asin4;
            rad = series * z * reduced + reduced;
        }

        if (folded) rad = HalfPi - (rad + rad);
        return x < 0f ? -rad : rad;
    }

    /// <summary>acos of <paramref name="x"/>; NaN outside [−1, 1].</summary>
    public static float Acos(float x)
    {
        if (!(MathF.Abs(x) <= 1f)) return float.NaN;

        // Near ±1 by the half angle, where π/2 − asin would lose what the answer has left.
        if (x < -0.5f) return Pi - 2f * Asin(MathF.Sqrt(0.5f * (1f + x)));
        if (x > 0.5f) return 2f * Asin(MathF.Sqrt(0.5f * (1f - x)));
        return HalfPi - Asin(x);
    }

    const double SinD1 = -1.66666666666666324348e-01;
    const double SinD2 = 8.33333333332248946124e-03;
    const double SinD3 = -1.98412698298579493134e-04;
    const double SinD4 = 2.75573137070700676789e-06;
    const double SinD5 = -2.50507602534068634195e-08;
    const double SinD6 = 1.58969099521155010221e-10;
    const double CosD1 = 4.16666666666666019037e-02;
    const double CosD2 = -1.38888888888741095749e-03;
    const double CosD3 = 2.48015872894767294178e-05;
    const double CosD4 = -2.75573143513906633035e-07;
    const double CosD5 = 2.08757232129817482790e-09;
    const double CosD6 = -1.13596475577881948265e-11;

    /// <summary>2⁻²⁶: below it a double's sine is its argument and its cosine one, to the bit.</summary>
    const double DoubleTinyBelow = 1.4901161193847656e-8;

    const double ToNearestWhole = 6755399441055744.0;
    const double TwoOverPiD = 6.36619772367581382433e-01;
    const double QuarterPiD = 7.85398163397448278999e-01;
    const double HalfPiD1 = 1.57079632673412561417e+00;
    const double HalfPiD1Tail = 6.07710050650619224932e-11;
    const double HalfPiD2 = 6.07710050630396597660e-11;
    const double HalfPiD2Tail = 2.02226624879595063154e-21;
    const double HalfPiD3 = 2.02226624871116645580e-21;
    const double HalfPiD3Tail = 8.47842766036889956997e-32;

    /// <summary>
    /// sin and cos of <paramref name="x"/> in doubles (fdlibm's, as musl keeps it): within a double of exact below
    /// 2²⁰·π/2 radians, which is more turn than anything in a town; past it the reduction loses bits, but every head
    /// loses the same ones.
    /// </summary>
    public static (double Sin, double Cos) SinCos(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return (double.NaN, double.NaN);

        var magnitude = Math.Abs(x);
        if (magnitude < DoubleTinyBelow) return (x, 1d);
        if (magnitude <= QuarterPiD) return (SinKernel(x, 0d, false), CosKernel(x, 0d));

        var quadrant = QuarterTurnsOf(x, out var head, out var tail);
        double sine = SinKernel(head, tail, true), cosine = CosKernel(head, tail);
        return (quadrant & 3) switch
        {
            0 => (sine, cosine),
            1 => (cosine, -sine),
            2 => (-sine, -cosine),
            _ => (-cosine, sine),
        };
    }

    public static double Sin(double x) => SinCos(x).Sin;

    static double SinKernel(double x, double tail, bool hasTail)
    {
        var z = x * x;
        var w = z * z;
        var r = SinD2 + z * (SinD3 + z * SinD4) + z * w * (SinD5 + z * SinD6);
        var v = z * x;
        return hasTail ? x - ((z * (0.5 * tail - v * r) - tail) - v * SinD1) : x + v * (SinD1 + z * r);
    }

    static double CosKernel(double x, double tail)
    {
        var z = x * x;
        var w = z * z;
        var r = z * (CosD1 + z * (CosD2 + z * CosD3)) + w * w * (CosD4 + z * (CosD5 + z * CosD6));
        var half = 0.5 * z;
        w = 1.0 - half;
        return w + (((1.0 - w) - half) + (z * r - x * tail));
    }

    /// <summary>
    /// <paramref name="x"/> less its nearest whole number of quarter turns, as a head and a tail, by π/2 in three parts and
    /// their tails (musl's <c>__rem_pio2</c>, its medium case for every argument).
    /// </summary>
    static int QuarterTurnsOf(double x, out double head, out double tail)
    {
        var turns = x * TwoOverPiD + ToNearestWhole - ToNearestWhole;
        var quadrant = (int)(long)turns;
        var r = x - turns * HalfPiD1;
        var w = turns * HalfPiD1Tail;
        head = r - w;

        var exponentX = (int)((BitConverter.DoubleToUInt64Bits(x) >> 52) & 0x7ff);
        var exponentHead = (int)((BitConverter.DoubleToUInt64Bits(head) >> 52) & 0x7ff);
        if (exponentX - exponentHead > 16)
        {
            var t = r;
            w = turns * HalfPiD2;
            r = t - w;
            w = turns * HalfPiD2Tail - ((t - r) - w);
            head = r - w;
            exponentHead = (int)((BitConverter.DoubleToUInt64Bits(head) >> 52) & 0x7ff);
            if (exponentX - exponentHead > 49)
            {
                t = r;
                w = turns * HalfPiD3;
                r = t - w;
                w = turns * HalfPiD3Tail - ((t - r) - w);
                head = r - w;
            }
        }

        tail = (r - head) - w;
        return quadrant;
    }

    /// <summary>Every bit set where <paramref name="condition"/> holds, none where it does not, without a branch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint MaskOf(bool condition) => (uint)-Unsafe.BitCast<bool, byte>(condition);

    /// <summary><paramref name="whenSet"/> where the mask is set and <paramref name="otherwise"/> where it is clear, bit for bit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float Pick(uint mask, float whenSet, float otherwise) => BitConverter.UInt32BitsToSingle(
        (BitConverter.SingleToUInt32Bits(whenSet) & mask) | (BitConverter.SingleToUInt32Bits(otherwise) & ~mask));

    /// <remarks>
    /// The three folds — −1/m past tan(3π/8), (m − 1)/(m + 1) past tan(π/8), m below — are one quotient whose two
    /// sides are picked in bits, the last as m/1, which is m exactly: a bearing's ratio is any size, and a branch on
    /// it is a guess.
    /// </remarks>
    static float AtanOfMagnitude(float magnitude)
    {
        var far = MaskOf(magnitude > Tan3PiOver8);
        var middle = MaskOf(magnitude > TanPiOver8) & ~far;
        var numerator = Pick(far, -1f, Pick(middle, magnitude - 1f, magnitude));
        var denominator = Pick(far, magnitude, Pick(middle, magnitude + 1f, 1f));
        var folded = numerator / denominator;
        var offset = Pick(far, HalfPi, Pick(middle, QuarterPi, 0f));

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

        // x's sign, carried in bits (see Quadrant). A nought never reaches here: both callers answer it first.
        var sign = BitConverter.SingleToUInt32Bits(x) & SignBit;
        var negate = (int)sign >> 31;
        quadrant = (quadrant ^ negate) - negate;
        return BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(reduced) ^ sign);
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
