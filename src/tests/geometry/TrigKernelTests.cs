using System.Numerics;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>The device's sine and cosine are the host's to the bit</b> (<c>core/geometry/kernels/trig.glsl</c>): every angle's,
/// as <see cref="MathF.Sin"/>, <see cref="MathF.Cos"/> and <see cref="MathF.SinCos"/> answer it — glibc's FMA path on
/// the machine the suite runs on.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P4)]
public class TrigKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    /// <summary>
    /// A heading's worth and an arc's either side of nought, every exponent a float has, and every edge the reduction
    /// turns at — tiny, π/4, the long way at 120, a whole number of quarter turns — a float either side.
    /// </summary>
    static float[] Angles()
    {
        var random = new Random(23);
        var angles = new List<float>();
        for (var draw = 0; draw < 1 << 20; draw++) angles.Add((random.NextSingle() - 0.5f) * 16f);
        for (var draw = 0; draw < 1 << 20; draw++)
        {
            var bits = (uint)random.NextInt64(0, 1L << 32);
            if (float.IsFinite(BitConverter.UInt32BitsToSingle(bits))) angles.Add(BitConverter.UInt32BitsToSingle(bits));
        }

        foreach (var edge in (ReadOnlySpan<float>)[0f, 1f / 4096f, MathF.PI / 4f, 120f, float.Epsilon, float.MaxValue])
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
        return [.. angles];
    }

    Vector2[] OnTheDevice(float[] angles)
    {
        using var asked = device.Holding<float>(angles);
        using var values = device.Sized<Vector2>(angles.Length);
        GpuBuffer[] buffers = [asked, values];
        using var kernel = new ComputeKernel(device.Vk, "trig_values.comp", buffers.Length, sizeof(uint));
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var recording = batch.Begin();
        kernel.Dispatch(recording, set, (uint)((angles.Length + 63) / 64), (uint)angles.Length);
        batch.SubmitAndWait();
        return values.Span<Vector2>().ToArray();
    }

    static bool Same(float one, float other) =>
        float.IsNaN(one) ? float.IsNaN(other) : BitConverter.SingleToInt32Bits(one) == BitConverter.SingleToInt32Bits(other);

    [Fact]
    public void EveryAnglesSineAndCosineIsTheHostsToTheBit()
    {
        var angles = Angles();
        var values = OnTheDevice(angles);

        var differ = new List<string>();
        for (var at = 0; at < angles.Length; at++)
        {
            var angle = angles[at];
            var (sine, cosine) = MathF.SinCos(angle);
            var (deviceSine, deviceCosine) = (values[at].X, values[at].Y);
            if (Same(deviceSine, MathF.Sin(angle)) && Same(deviceCosine, MathF.Cos(angle)) && Same(deviceSine, sine) && Same(deviceCosine, cosine)) continue;

            differ.Add($"{angle:R} (0x{BitConverter.SingleToUInt32Bits(angle):x8}): host {MathF.Sin(angle):R}, {MathF.Cos(angle):R}; device {deviceSine:R}, {deviceCosine:R}");
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {angles.Length} differ: {string.Join("; ", differ.Take(8))}");
    }
}
