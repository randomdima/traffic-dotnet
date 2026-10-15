using System.Numerics;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>The device's quotients and roots are the host's to the bit</b> (<c>core/geometry/kernels/rounded.glsl</c>): every
/// pair's quotient and every root the nearest float, as the host's own division and <see cref="MathF.Sqrt"/> are.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P4)]
public class RoundedKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>
    /// Any two finite floats, two of a town's sizes, and each figure beside a binade's edge, where a neighbour below is
    /// half the step of one above.
    /// </summary>
    static Vector2[] Asked()
    {
        var random = new Random(29);
        var asked = new List<Vector2>();
        for (var draw = 0; draw < 1 << 20; draw++)
        {
            asked.Add(new Vector2(AnyFinite(random), AnyFinite(random)));
            asked.Add(new Vector2((random.NextSingle() - 0.5f) * 4000f, (random.NextSingle() - 0.5f) * 40f));
        }

        for (var exponent = -20; exponent <= 20; exponent++)
        {
            var edge = MathF.ScaleB(1f, exponent);
            for (var step = -3; step <= 3; step++)
            {
                var near = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(edge) + step);
                asked.Add(new Vector2(near, 3f));
                asked.Add(new Vector2(7f, near));
                asked.Add(new Vector2(near, near * 1.5f));
            }
        }

        return [.. asked];
    }

    static float AnyFinite(Random random)
    {
        while (true)
        {
            var x = BitConverter.UInt32BitsToSingle((uint)random.NextInt64(0, 1L << 32));
            if (float.IsFinite(x)) return x;
        }
    }

    Vector2[] OnTheDevice(Vector2[] asked)
    {
        using var pairs = device.Holding<Vector2>(asked);
        using var values = device.Sized<Vector2>(asked.Length);
        GpuBuffer[] buffers = [pairs, values];
        using var kernel = new ComputeKernel(device.Vk, "rounded_values.comp", buffers.Length, sizeof(uint));
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var recording = batch.Begin();
        kernel.Dispatch(recording, set, (uint)((asked.Length + 63) / 64), (uint)asked.Length);
        batch.SubmitAndWait();
        return values.Span<Vector2>().ToArray();
    }

    static bool Same(float one, float other) =>
        float.IsNaN(one) ? float.IsNaN(other) : BitConverter.SingleToInt32Bits(one) == BitConverter.SingleToInt32Bits(other);

    /// <summary>A figure a kernel flushes to nought, as every kernel does: asked nothing.</summary>
    static bool Flushed(float x) => float.IsSubnormal(x);

    [Fact]
    public void EveryQuotientIsTheHostsToTheBit()
    {
        var asked = Asked();
        var values = OnTheDevice(asked);

        var differ = new List<string>();
        var count = 0;
        for (var at = 0; at < asked.Length; at++)
        {
            var (a, b) = (asked[at].X, asked[at].Y);
            var quotient = a / b;
            if (Flushed(a) || Flushed(b) || Flushed(quotient)) continue;

            count++;
            if (!Same(values[at].X, quotient)) differ.Add($"{a:R} / {b:R}: host {quotient:R}, device {values[at].X:R}");
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {count} differ: {string.Join("; ", differ.Take(8))}");
    }

    [Fact]
    public void EveryRootIsTheHostsToTheBit()
    {
        var asked = Asked();
        var values = OnTheDevice(asked);

        var differ = new List<string>();
        var count = 0;
        for (var at = 0; at < asked.Length; at++)
        {
            var x = asked[at].X;
            var root = MathF.Sqrt(x);
            if (Flushed(x) || Flushed(root)) continue;

            count++;
            if (!Same(values[at].Y, root)) differ.Add($"sqrt {x:R}: host {root:R}, device {values[at].Y:R}");
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {count} differ: {string.Join("; ", differ.Take(8))}");
    }
}
