using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary><see cref="Powers"/> stands within a few floats of the exact answer over the ranges the town asks of it.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P7)]
public class PowersTests
{
    const int MostFloatsOff = 3;

    static long FloatsApart(float one, float other)
    {
        static long Ordered(float x)
        {
            var bits = BitConverter.SingleToInt32Bits(x);
            return bits < 0 ? int.MinValue - (long)bits : bits;
        }

        return Math.Abs(Ordered(one) - Ordered(other));
    }

    [Fact]
    public void ExponentialIsWithinThreeFloats()
    {
        var random = new Random(51);
        var worst = 0L;
        for (var draw = 0; draw < 1 << 18; draw++)
        {
            var x = (random.NextSingle() - 0.5f) * 170f;
            worst = Math.Max(worst, FloatsApart(Powers.Exp(x), (float)Math.Exp(x)));
        }

        Assert.InRange(worst, 0, MostFloatsOff);
    }

    [Fact]
    public void LogarithmIsWithinThreeFloats()
    {
        var random = new Random(53);
        var worst = 0L;
        for (var draw = 0; draw < 1 << 18; draw++)
        {
            var x = MathF.ScaleB(1f + random.NextSingle(), random.Next(-140, 127));
            worst = Math.Max(worst, FloatsApart(Powers.Log(x), (float)Math.Log(x)));
        }

        Assert.InRange(worst, 0, MostFloatsOff);
    }

    /// <summary>
    /// A share drawn in [0, 1) to a skew of a tenth to ten, which is what a dwell asks: a power's floats off are
    /// <see cref="Powers.Log"/>'s and the product's carried by the exponent, |y ln x| of them a float each, and
    /// <see cref="Powers.Exp"/>'s three.
    /// </summary>
    [Fact]
    public void APowerOfAShareIsWithinWhatItsExponentCarries()
    {
        var random = new Random(57);
        for (var draw = 0; draw < 1 << 18; draw++)
        {
            var (x, y) = (random.NextSingle(), 0.1f + (random.NextSingle() * 9.9f));
            var carried = (long)MathF.Ceiling(4f * MathF.Abs(y * (float)Math.Log(x)));
            Assert.InRange(FloatsApart(Powers.Pow(x, y), (float)Math.Pow(x, y)), 0, carried + MostFloatsOff);
        }
    }

    [Fact]
    public void CubeRootIsWithinAFloat()
    {
        var random = new Random(59);
        var worst = 0L;
        for (var draw = 0; draw < 1 << 18; draw++)
        {
            var x = MathF.ScaleB((random.NextSingle() - 0.5f) * 2f, random.Next(-60, 60));
            worst = Math.Max(worst, FloatsApart(Powers.Cbrt(x), (float)Math.Cbrt(x)));
        }

        Assert.InRange(worst, 0, 1);
    }
}
