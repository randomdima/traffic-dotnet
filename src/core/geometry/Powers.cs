namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// The town's exponentials and roots, in floats alone (Cephes' single-precision series): what <see cref="Trig"/> is to
/// the angles, so a town is laid and ticked alike on the desktop and in a page whichever libm stands under each.
/// </summary>
/// <remarks>
/// Every step is a float sum, product, quotient or root, a power of two put on by <see cref="MathF.ScaleB"/>, or a floor —
/// each of which every head rounds alike. Within a few floats of exact; <see cref="Pow"/> as far as its exponent carries
/// <see cref="Log"/>'s error.
/// </remarks>
internal static class Powers
{
    /// <summary>Past this e^x is past the greatest float, and below <see cref="ExpLeast"/> it is nought.</summary>
    const float ExpGreatest = 88.72283905206835f;

    const float ExpLeast = -103.278929903431851103f;
    const float Log2E = 1.44269504088896341f;

    /// <summary>ln 2 in two parts: the first short enough that a whole number of them below 2⁹ is exact.</summary>
    const float Ln2High = 0.693359375f;

    const float Ln2Low = -2.12194440e-4f;

    const float Exp0 = 1.9875691500e-4f;
    const float Exp1 = 1.3981999507e-3f;
    const float Exp2 = 8.3334519073e-3f;
    const float Exp3 = 4.1665795894e-2f;
    const float Exp4 = 1.6666665459e-1f;
    const float Exp5 = 5.0000001201e-1f;

    const float HalfRootTwo = 0.707106781186547524f;

    const float Log0 = 7.0376836292e-2f;
    const float Log1 = -1.1514610310e-1f;
    const float Log2 = 1.1676998740e-1f;
    const float Log3 = -1.2420140846e-1f;
    const float Log4 = 1.4249322787e-1f;
    const float Log5 = -1.6668057665e-1f;
    const float Log6 = 2.0000714765e-1f;
    const float Log7 = -2.4999993993e-1f;
    const float Log8 = 3.3333331174e-1f;

    /// <summary>e to the <paramref name="x"/>.</summary>
    public static float Exp(float x)
    {
        if (float.IsNaN(x)) return float.NaN;
        if (x > ExpGreatest) return float.PositiveInfinity;
        if (x < ExpLeast) return 0f;

        var twos = MathF.Floor(Log2E * x + 0.5f);
        x -= twos * Ln2High;
        x -= twos * Ln2Low;

        var squared = x * x;
        var series = Exp0 * x + Exp1;
        series = series * x + Exp2;
        series = series * x + Exp3;
        series = series * x + Exp4;
        series = series * x + Exp5;
        var power = series * squared + x + 1f;
        return MathF.ScaleB(power, (int)twos);
    }

    /// <summary>ln <paramref name="x"/>: −∞ at nought and NaN below it.</summary>
    public static float Log(float x)
    {
        if (float.IsNaN(x) || x < 0f) return float.NaN;
        if (x == 0f) return float.NegativeInfinity;
        if (float.IsPositiveInfinity(x)) return x;

        // x as a mantissa in [½, 1) and a power of two, a subnormal brought up first.
        var twos = 0;
        if (!float.IsNormal(x))
        {
            x *= 16777216f;
            twos = -24;
        }

        var bits = BitConverter.SingleToUInt32Bits(x);
        twos += (int)((bits >> 23) & 0xff) - 126;
        x = BitConverter.UInt32BitsToSingle((bits & 0x807f_ffffu) | 0x3f00_0000u);

        if (x < HalfRootTwo)
        {
            twos--;
            x = x + x - 1f;
        }
        else
        {
            x -= 1f;
        }

        var squared = x * x;
        var series = Log0 * x + Log1;
        series = series * x + Log2;
        series = series * x + Log3;
        series = series * x + Log4;
        series = series * x + Log5;
        series = series * x + Log6;
        series = series * x + Log7;
        series = series * x + Log8;
        var y = series * x * squared;

        var exponent = (float)twos;
        y += Ln2Low * exponent;
        y += -0.5f * squared;
        return x + y + Ln2High * exponent;
    }

    /// <summary><paramref name="x"/> to the <paramref name="y"/>, for a base not below nought; NaN for one that is.</summary>
    public static float Pow(float x, float y)
    {
        if (float.IsNaN(x) || float.IsNaN(y) || x < 0f) return float.NaN;
        if (y == 0f) return 1f;
        if (x == 0f) return y > 0f ? 0f : float.PositiveInfinity;

        return Exp(y * Log(x));
    }

    /// <summary>The cube root of <paramref name="x"/>, its sign kept.</summary>
    public static float Cbrt(float x)
    {
        if (x == 0f || !float.IsFinite(x)) return x;

        var magnitude = MathF.Abs(x);
        double root = Exp(Log(magnitude) / 3f);

        // One Newton step in doubles takes the series' few floats off.
        root -= (root * root * root - magnitude) / (3d * root * root);
        return MathF.CopySign((float)root, x);
    }
}
