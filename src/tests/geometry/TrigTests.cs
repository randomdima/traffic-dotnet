using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <see cref="Trig"/> stands within three floats of the exact answer over every angle the town turns through — a heading's
/// worth either side of nought and an arc's — and over every arctangent and bearing.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P7)]
public class TrigTests
{
    const int MostFloatsOff = 3;

    /// <summary>How many floats apart two finite floats are.</summary>
    static long FloatsApart(float one, float other)
    {
        static long Ordered(float x)
        {
            var bits = BitConverter.SingleToInt32Bits(x);
            return bits < 0 ? int.MinValue - (long)bits : bits;
        }

        return Math.Abs(Ordered(one) - Ordered(other));
    }

    static IEnumerable<float> Angles()
    {
        var random = new Random(31);
        for (var draw = 0; draw < 1 << 18; draw++) yield return (random.NextSingle() - 0.5f) * 16f;
        for (var draw = 0; draw < 1 << 16; draw++) yield return (random.NextSingle() - 0.5f) * 8000f;
    }

    [Fact]
    public void SineAndCosineAreWithinThreeFloats()
    {
        var worst = 0L;
        foreach (var angle in Angles())
        {
            var (sine, cosine) = Trig.SinCos(angle);
            worst = Math.Max(worst, FloatsApart(sine, (float)Math.Sin(angle)));
            worst = Math.Max(worst, FloatsApart(cosine, (float)Math.Cos(angle)));
        }

        Assert.InRange(worst, 0, MostFloatsOff);
    }

    [Fact]
    public void TangentIsWithinThreeFloats()
    {
        var worst = 0L;
        foreach (var angle in Angles())
        {
            worst = Math.Max(worst, FloatsApart(Trig.Tan(angle), (float)Math.Tan(angle)));
        }

        Assert.InRange(worst, 0, MostFloatsOff);
    }

    [Fact]
    public void ArctangentAndBearingAreWithinThreeFloats()
    {
        var random = new Random(37);
        var worst = 0L;
        for (var draw = 0; draw < 1 << 18; draw++)
        {
            var (y, x) = ((random.NextSingle() - 0.5f) * 200f, (random.NextSingle() - 0.5f) * 200f);
            worst = Math.Max(worst, FloatsApart(Trig.Atan(x), (float)Math.Atan(x)));
            worst = Math.Max(worst, FloatsApart(Trig.Atan2(y, x), (float)Math.Atan2(y, x)));
        }

        Assert.InRange(worst, 0, MostFloatsOff);
    }

    [Fact]
    public void ArcsineAndArccosineAreWithinThreeFloats()
    {
        var random = new Random(41);
        var worst = 0L;
        for (var draw = 0; draw < 1 << 18; draw++)
        {
            var x = (random.NextSingle() * 2f) - 1f;
            worst = Math.Max(worst, FloatsApart(Trig.Asin(x), (float)Math.Asin(x)));
            worst = Math.Max(worst, FloatsApart(Trig.Acos(x), (float)Math.Acos(x)));
        }

        Assert.InRange(worst, 0, MostFloatsOff);
    }

    [Fact]
    public void ADoubleSineAndCosineAreWithinADouble()
    {
        static long DoublesApart(double one, double other)
        {
            static long Ordered(double x)
            {
                var bits = BitConverter.DoubleToInt64Bits(x);
                return bits < 0 ? long.MinValue - bits : bits;
            }

            return Math.Abs(Ordered(one) - Ordered(other));
        }

        var random = new Random(43);
        var worst = 0L;
        for (var draw = 0; draw < 1 << 18; draw++)
        {
            var angle = (random.NextDouble() - 0.5) * (draw % 2 == 0 ? 16.0 : 2e5);
            var (sine, cosine) = Trig.SinCos(angle);
            worst = Math.Max(worst, DoublesApart(sine, Math.Sin(angle)));
            worst = Math.Max(worst, DoublesApart(cosine, Math.Cos(angle)));
        }

        Assert.InRange(worst, 0, 1);
    }
}
