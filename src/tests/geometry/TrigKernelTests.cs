using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>The device's trigonometry is the host's to the bit</b> (<c>core/geometry/kernels/trig.glsl</c>): every angle's sine,
/// cosine, tangent and arctangent and every point's bearing, as <see cref="Trig"/> answers them.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P4)]
public class TrigKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>Where the reduction turns from the parts of π/2 to 4/π's bits (<c>TrigShortWayBelow</c>).</summary>
    const float ShortWayBelow = 4096f;

    /// <summary>
    /// A heading's worth and an arc's either side of nought, every exponent a float has, and every edge the reductions
    /// turn at — tiny, π/4, the long way, a whole number of quarter turns — a float either side; each with a second
    /// figure, a point's other, drawn the same ways.
    /// </summary>
    static Vector2[] Asked()
    {
        var random = new Random(23);
        var angles = new List<float>();
        for (var draw = 0; draw < 1 << 20; draw++) angles.Add((random.NextSingle() - 0.5f) * 16f);
        for (var draw = 0; draw < 1 << 20; draw++)
        {
            var bits = (uint)random.NextInt64(0, 1L << 32);
            if (float.IsFinite(BitConverter.UInt32BitsToSingle(bits))) angles.Add(BitConverter.UInt32BitsToSingle(bits));
        }

        foreach (var edge in (ReadOnlySpan<float>)[0f, 1f / 4096f, MathF.PI / 4f, ShortWayBelow, float.Epsilon, float.MaxValue])
        {
            for (var step = -4; step <= 4; step++)
            {
                var near = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(edge) + step);
                angles.Add(near);
                angles.Add(-near);
            }
        }

        for (var quarter = -400; quarter <= 400; quarter++)
        {
            var at = quarter * (MathF.PI / 2f);
            for (var step = -3; step <= 3; step++) angles.Add(BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(at) + step));
        }

        angles.AddRange([float.PositiveInfinity, float.NegativeInfinity, float.NaN, -0f]);
        var asked = new Vector2[angles.Count];
        for (var at = 0; at < asked.Length; at++) asked[at] = new Vector2(angles[at], angles[random.Next(angles.Count)]);

        return asked;
    }

    (Vector4[] Values, float[] Bearings) OnTheDevice(Vector2[] asked)
    {
        using var points = device.Holding<Vector2>(asked);
        using var values = device.Sized<Vector4>(asked.Length);
        using var bearings = device.Sized<float>(asked.Length);
        GpuBuffer[] buffers = [points, values, bearings];
        using var kernel = new ComputeKernel(device.Vk, "trig_values.comp", buffers.Length, sizeof(uint));
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var recording = batch.Begin();
        kernel.Dispatch(recording, set, (uint)((asked.Length + 63) / 64), (uint)asked.Length);
        batch.SubmitAndWait();
        return (values.Span<Vector4>().ToArray(), bearings.Span<float>().ToArray());
    }

    static bool Same(float one, float other) =>
        float.IsNaN(one) ? float.IsNaN(other) : BitConverter.SingleToInt32Bits(one) == BitConverter.SingleToInt32Bits(other);

    /// <summary>
    /// Whether a figure is one a kernel flushes to nought, as every kernel does (<c>world/physics</c>'s log): so a
    /// subnormal argument or answer is asked nothing.
    /// </summary>
    static bool Flushed(float x) => float.IsSubnormal(x);

    static string Bits(float x) => $"{x:R} (0x{BitConverter.SingleToUInt32Bits(x):x8})";

    [Fact]
    public void EveryAnglesSineAndCosineIsTheHostsToTheBit()
    {
        var asked = Asked();
        var (values, _) = OnTheDevice(asked);

        var differ = new List<string>();
        var count = 0;
        for (var at = 0; at < asked.Length; at++)
        {
            var angle = asked[at].X;
            var (sine, cosine) = Trig.SinCos(angle);
            if (Flushed(angle) || Flushed(sine) || Flushed(cosine)) continue;

            count++;
            if (Same(values[at].X, sine) && Same(values[at].Y, cosine)) continue;

            differ.Add($"{Bits(angle)}: host {sine:R}, {cosine:R}; device {values[at].X:R}, {values[at].Y:R}");
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {count} differ: {string.Join("; ", differ.Take(8))}");
    }

    [Fact]
    public void EveryAnglesTangentIsTheHostsToTheBit()
    {
        var asked = Asked();
        var (values, _) = OnTheDevice(asked);

        var differ = new List<string>();
        var count = 0;
        for (var at = 0; at < asked.Length; at++)
        {
            var angle = asked[at].X;
            var tangent = Trig.Tan(angle);
            if (Flushed(angle) || Flushed(tangent)) continue;

            count++;
            if (!Same(values[at].Z, tangent)) differ.Add($"{Bits(angle)}: host {tangent:R}, device {values[at].Z:R}");
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {count} differ: {string.Join("; ", differ.Take(8))}");
    }

    [Fact]
    public void EveryArctangentAndBearingIsTheHostsToTheBit()
    {
        var asked = Asked();
        var (values, bearings) = OnTheDevice(asked);

        var differ = new List<string>();
        for (var at = 0; at < asked.Length; at++)
        {
            var (x, y) = (asked[at].X, asked[at].Y);
            var arctangent = Trig.Atan(x);
            if (!Flushed(x) && !Flushed(arctangent) && !Same(values[at].W, arctangent))
            {
                differ.Add($"atan {Bits(x)}: host {arctangent:R}, device {values[at].W:R}");
            }

            var bearing = Trig.Atan2(y, x);
            if (!Flushed(x) && !Flushed(y) && !Flushed(bearing) && !Same(bearings[at], bearing))
            {
                differ.Add($"atan2 {Bits(y)}, {Bits(x)}: host {bearing:R}, device {bearings[at]:R}");
            }
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {2 * asked.Length} differ: {string.Join("; ", differ.Take(8))}");
    }
}
