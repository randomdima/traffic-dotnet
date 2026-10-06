using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Physics;
using Xunit;

namespace TrafficSimulation.Tests.Physics;

/// <summary>
/// <b>The solver's last stage on the device</b> (<c>world/physics/kernels/integrate.comp</c>): what
/// <c>PhysicsWorld.Integrate</c> does to a body, asked of the kernel.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P0)]
public class IntegrateKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    const float DtS = 1f / 60f;
    const uint FiledFrozen = 1u << 4;

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Step(float DtS, uint MovingCount, uint FiledFrozenBit);

    sealed class Table(Lavapipe device, int bodies) : IDisposable
    {
        public readonly Vector2[] PositionM = new Vector2[bodies];
        public readonly float[] HeadingRad = new float[bodies];
        public readonly Vector2[] VelocityMps = new Vector2[bodies];
        public readonly float[] YawRateRadPerS = new float[bodies];
        public readonly Vector2[] PushMps = new Vector2[bodies];
        public readonly float[] PushYawRadPerS = new float[bodies];
        public readonly Vector2[] ExtentM = new Vector2[bodies];
        public readonly float[] CornerRadiusM = new float[bodies];
        public readonly uint[] Flags = new uint[bodies];

        GpuBuffer[] _buffers = [];

        public GpuBuffer[] Upload(ReadOnlySpan<int> moving)
        {
            _buffers =
            [
                device.Holding(moving),
                device.Holding<Vector2>(PositionM),
                device.Holding<float>(HeadingRad),
                device.Sized<Vector2>(bodies),
                device.Holding<Vector2>(VelocityMps),
                device.Holding<float>(YawRateRadPerS),
                device.Holding<Vector2>(PushMps),
                device.Holding<float>(PushYawRadPerS),
                device.Holding<Vector2>(ExtentM),
                device.Holding<float>(CornerRadiusM),
                device.Sized<Vector2>(bodies),
                device.Sized<Vector2>(bodies),
                device.Holding<uint>(Flags),
            ];
            return _buffers;
        }

        public void Dispose()
        {
            foreach (var buffer in _buffers) buffer.Dispose();
        }
    }

    static void Run(Lavapipe device, GpuBuffer[] buffers, int movingCount)
    {
        using var kernel = new ComputeKernel(device.Vk, "integrate.comp", buffers.Length, 12);
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var commands = batch.Begin();
        kernel.Dispatch(commands, set, (uint)((movingCount + 63) / 64), new Step(DtS, (uint)movingCount, FiledFrozen));
        batch.SubmitAndWait();
    }

    /// <summary>
    /// A body is moved by its own motion and the push together, to the same float the CPU's step lands it on — the
    /// arithmetic is unfused on the device as it is on the host.
    /// </summary>
    [Fact]
    public void ABodyMovesByItsMotionAndItsPushToTheSameFloat()
    {
        using var table = new Table(device, 1);
        table.PositionM[0] = new Vector2(1234.567f, -89.25f);
        table.VelocityMps[0] = new Vector2(13.7f, -2.1f);
        table.PushMps[0] = new Vector2(0.031f, 0.007f);
        var buffers = table.Upload([0]);

        Run(device, buffers, 1);

        var expected = table.PositionM[0] + (table.VelocityMps[0] + table.PushMps[0]) * DtS;
        Assert.Equal(expected, buffers[1].Span<Vector2>()[0]);
    }

    /// <summary>A heading turned past half a turn comes back in the half-open turn the engine reads, rotation and all.</summary>
    [Fact]
    public void AHeadingTurnedPastHalfATurnIsWrappedAndItsRotationFollows()
    {
        using var table = new Table(device, 1);
        table.HeadingRad[0] = MathF.PI - 0.01f;
        table.YawRateRadPerS[0] = 3f;
        var buffers = table.Upload([0]);

        Run(device, buffers, 1);

        var heading = buffers[2].Span<float>()[0];
        Assert.InRange(heading, -MathF.PI, MathF.PI);
        Assert.Equal(MathF.PI - 0.01f + 3f * DtS - 2f * MathF.PI, heading, 5);
        var rotation = buffers[3].Span<Vector2>()[0];
        Assert.Equal(MathF.Cos(heading), rotation.X, 5);
        Assert.Equal(MathF.Sin(heading), rotation.Y, 5);
    }

    /// <summary>The bounds are the rounded box's reach at its new rotation, about where it now stands.</summary>
    [Fact]
    public void TheBoundsAreTheRoundedBoxsReachWhereItNowStands()
    {
        using var table = new Table(device, 1);
        table.HeadingRad[0] = 0.4f;
        table.ExtentM[0] = new Vector2(2.2f, 0.9f);
        table.CornerRadiusM[0] = 0.15f;
        table.VelocityMps[0] = new Vector2(5f, 0f);
        var buffers = table.Upload([0]);

        Run(device, buffers, 1);

        var position = buffers[1].Span<Vector2>()[0];
        var half = Shape.HalfBoundsM(buffers[3].Span<Vector2>()[0], table.ExtentM[0], table.CornerRadiusM[0]);
        Assert.Equal(position - half, buffers[10].Span<Vector2>()[0]);
        Assert.Equal(position + half, buffers[11].Span<Vector2>()[0]);
    }

    /// <summary>
    /// Only the moving roster is integrated, and a body that moved leaves the frozen index's entry for it untrue
    /// (SOL-37) — one that did not move keeps it.
    /// </summary>
    [Fact]
    public void OnlyTheMovingRosterIsTakenAndOnlyAMovedBodyLeavesItsFiling()
    {
        using var table = new Table(device, 3);
        for (var body = 0; body < 3; body++)
        {
            table.PositionM[body] = new Vector2(body * 10f, 0f);
            table.Flags[body] = FiledFrozen;
        }

        table.VelocityMps[0] = new Vector2(1f, 0f);
        table.VelocityMps[2] = new Vector2(1f, 0f);
        var buffers = table.Upload([0, 1]);

        Run(device, buffers, 2);

        var flags = buffers[12].Span<uint>();
        Assert.Equal(0u, flags[0] & FiledFrozen);
        Assert.Equal(FiledFrozen, flags[1] & FiledFrozen);
        Assert.Equal(FiledFrozen, flags[2] & FiledFrozen);
        Assert.Equal(new Vector2(20f, 0f), buffers[1].Span<Vector2>()[2]);
    }
}
