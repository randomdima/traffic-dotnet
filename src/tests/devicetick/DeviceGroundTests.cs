using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.CityGen.Traced;
using TrafficSimulation.Tests.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.DeviceTick;

/// <summary>
/// <b>The ground asked on the device</b> (<c>citygen/kernels/ground.glsl</c>): the same answer
/// <see cref="GroundShapes.At(Vector2, byte)"/> gives, off the same shapes (TER-7), at every point asked.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class DeviceGroundTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>What the device answers at each point, on each point's level.</summary>
    Ground[] Ask(GroundShapes shapes, ReadOnlySpan<Vector4> points)
    {
        using var ground = new DeviceGround(device.Vk, shapes.Table);
        using var asked = device.Holding(points);
        using var answers = device.Sized<uint>(points.Length);
        GpuBuffer[] buffers = [asked, answers, .. ground.Tables];
        using var kernel = new ComputeKernel(device.Vk, "ground_at.comp", buffers.Length, (uint)Marshal.SizeOf<DeviceGround.AskPush>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var commands = batch.Begin();
        kernel.Dispatch(commands, set, (uint)((points.Length + 63) / 64), ground.PushFor(points.Length));
        batch.SubmitAndWait();

        var read = answers.Span<uint>();
        var grounds = new Ground[points.Length];
        for (var at = 0; at < grounds.Length; at++) grounds[at] = (Ground)read[at];
        return grounds;
    }

    static Vector4[] Scattered(Vector2 worldSizeM, int count, int seed, byte level = 0)
    {
        var random = new Random(seed);
        var points = new Vector4[count];
        for (var at = 0; at < count; at++)
        {
            points[at] = new Vector4(
                (random.NextSingle() * 1.1f - 0.05f) * worldSizeM.X, (random.NextSingle() * 1.1f - 0.05f) * worldSizeM.Y, level, 0f);
        }

        return points;
    }

    static void AssertTheSame(GroundShapes shapes, ReadOnlySpan<Vector4> points, Ground[] onTheDevice)
    {
        var differ = new List<string>();
        for (var at = 0; at < points.Length; at++)
        {
            var pointM = new Vector2(points[at].X, points[at].Y);
            var onTheHost = shapes.At(pointM, (byte)points[at].Z);
            if (onTheHost != onTheDevice[at]) differ.Add($"({pointM.X:F4}, {pointM.Y:F4}) host {onTheHost}, device {onTheDevice[at]}");
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {points.Length} differ: {string.Join("; ", differ.Take(8))}");
    }

    /// <summary>
    /// Over the fixture town and a margin round it, every point is answered on the device as on the host — every kind
    /// of ground among them.
    /// </summary>
    [Fact]
    public void TheFixtureTownIsTheSameGroundOnTheDevice()
    {
        var plan = Towns.Of(Towns.Fixture);
        var shapes = new GroundShapes(plan.Paving(Config), Config);
        var points = Scattered(plan.WorldSizeM, 40_000, 11);

        var grounds = Ask(shapes, points);

        AssertTheSame(shapes, points, grounds);
        Assert.Contains(Ground.Road, grounds);
        Assert.Contains(Ground.Sidewalk, grounds);
        Assert.Contains(Ground.Grass, grounds);
    }

    /// <summary>On a deck over a street the level above is answered as the host answers it, and the street under it too.</summary>
    [Fact]
    public void ADeckOverAStreetIsTheSameGroundOnBothLevels()
    {
        var plan = TracedPlanTests.OverAStreet();
        var shapes = new GroundShapes(plan.Paving(Config), Config);
        Vector4[] points = [.. Scattered(plan.WorldSizeM, 10_000, 12, level: 0), .. Scattered(plan.WorldSizeM, 10_000, 13, level: 1)];

        AssertTheSame(shapes, points, Ask(shapes, points));
    }
}
