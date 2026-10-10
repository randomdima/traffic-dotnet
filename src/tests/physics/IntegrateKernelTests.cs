using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.App.DeviceTick;
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
    const uint FiledFrozen = (uint)BodyFlags.FiledFrozen;

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

        public GpuBuffer Pose => _buffers[1];

        public GpuBuffer Heading => _buffers[2];

        public GpuBuffer Bounds => _buffers[6];

        public GpuBuffer Filter => _buffers[7];

        public GpuBuffer[] Upload(ReadOnlySpan<int> moving)
        {
            var pose = new Vector4[bodies];
            var motion = new Vector4[bodies];
            var push = new Vector4[bodies];
            var shape = new Vector4[bodies];
            var filter = new UInt4[bodies];
            for (var body = 0; body < bodies; body++)
            {
                pose[body] = new Vector4(PositionM[body], MathF.Cos(HeadingRad[body]), MathF.Sin(HeadingRad[body]));
                motion[body] = new Vector4(VelocityMps[body], YawRateRadPerS[body], 0f);
                push[body] = new Vector4(PushMps[body], PushYawRadPerS[body], 0f);
                shape[body] = new Vector4(ExtentM[body], CornerRadiusM[body], 0f);
                filter[body] = new UInt4(Flags[body], 0, 0, 0);
            }

            _buffers =
            [
                device.Holding(moving),
                device.Holding<Vector4>(pose),
                device.Holding<float>(HeadingRad),
                device.Holding<Vector4>(motion),
                device.Holding<Vector4>(push),
                device.Holding<Vector4>(shape),
                device.Sized<Vector4>(bodies),
                device.Holding<UInt4>(filter),
                device.Sized<uint>(bodies),
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
        using var kernel = new ComputeKernel(device.Vk, "integrate.comp", buffers.Length, (uint)Marshal.SizeOf<DeviceSolver.StepPush>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var commands = batch.Begin();
        var push = new DeviceSolver.StepPush { DtS = DtS, RosterCount = (uint)movingCount, FiledFrozenBit = FiledFrozen };
        kernel.Dispatch(commands, set, (uint)((movingCount + 63) / 64), push);
        batch.SubmitAndWait();
    }

    static Vector2 PositionOf(Table table, int body) => new(table.Pose.Span<Vector4>()[body].X, table.Pose.Span<Vector4>()[body].Y);

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
        Run(device, table.Upload([0]), 1);

        var expected = table.PositionM[0] + (table.VelocityMps[0] + table.PushMps[0]) * DtS;
        Assert.Equal(expected, PositionOf(table, 0));
    }

    /// <summary>A heading turned past half a turn comes back in the half-open turn the engine reads, rotation and all.</summary>
    [Fact]
    public void AHeadingTurnedPastHalfATurnIsWrappedAndItsRotationFollows()
    {
        using var table = new Table(device, 1);
        table.HeadingRad[0] = MathF.PI - 0.01f;
        table.YawRateRadPerS[0] = 3f;
        Run(device, table.Upload([0]), 1);

        var heading = table.Heading.Span<float>()[0];
        Assert.InRange(heading, -MathF.PI, MathF.PI);
        Assert.Equal(MathF.PI - 0.01f + 3f * DtS - 2f * MathF.PI, heading, 5);
        var pose = table.Pose.Span<Vector4>()[0];
        Assert.Equal(MathF.Cos(heading), pose.Z, 5);
        Assert.Equal(MathF.Sin(heading), pose.W, 5);
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
        Run(device, table.Upload([0]), 1);

        var pose = table.Pose.Span<Vector4>()[0];
        var position = new Vector2(pose.X, pose.Y);
        var half = Shape.HalfBoundsM(new Vector2(pose.Z, pose.W), table.ExtentM[0], table.CornerRadiusM[0]);
        var bounds = table.Bounds.Span<Vector4>()[0];
        Assert.Equal(position - half, new Vector2(bounds.X, bounds.Y));
        Assert.Equal(position + half, new Vector2(bounds.Z, bounds.W));
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
        Run(device, table.Upload([0, 1]), 2);

        var filter = table.Filter.Span<UInt4>();
        Assert.Equal(0u, filter[0].X & FiledFrozen);
        Assert.Equal(FiledFrozen, filter[1].X & FiledFrozen);
        Assert.Equal(FiledFrozen, filter[2].X & FiledFrozen);
        Assert.Equal(new Vector2(20f, 0f), PositionOf(table, 2));
    }
}
