using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>The tyre model on the device</b> (<c>agents/car/body/kernels/tyre.glsl</c>): what <see cref="TyreModel.Step"/>
/// makes of a car's four patches, asked of the kernel over every variant, every pedal and every kind of ground.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class TyreKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct PoseRow(
        Vector2 PositionM, Vector2 VelocityMps, Vector2 Forward, Vector2 AccelerationMps2, float HeadingRad, float YawRateRadPerS,
        float MassKg, float Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CommandRow(float SteerRad, float ThrottleMps2, float BrakeMps2, uint HandbrakeAndReverse);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct SurfaceRow(float Coefficient, float DragMps2, float MarkThresholdM2S3, uint Ploughs);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ScrubRow(float SlidePowerM2S3, float PloughPowerM2S3, float SlideSpeedMps, uint Flags);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Figures(
        uint CarCount, float DtS, float WheelSpinFadeMps, float WheelSpinAllowanceMps, float MarkSlipMps,
        float MarkCorneringSlipMps, float PloughCrawlMps, float EngineBrakingMps2, float WorkingSlipTangent,
        float AlongSlipEpsilonMps, float LiftedAxleLoadShare, float AckermannLeastSteerRad, float InnerWheelLeastRadiusM);

    sealed record Car(CarBuild Build, int BuildAt, CarPose Pose, DriveCommand Command, float CeilingMps2, SurfaceUnderWheel[] Ground, float[] Spin);

    static Car[] Cars(int count, int seed, CarBuild[] builds)
    {
        var random = new Random(seed);
        float Between(float least, float most) => least + (random.NextSingle() * (most - least));
        var cars = new Car[count];
        for (var at = 0; at < count; at++)
        {
            var buildAt = random.Next(builds.Length);
            var build = builds[buildAt];
            var heading = Between(-MathF.PI, MathF.PI);
            var velocity = new Vector2(Between(-22f, 22f), Between(-22f, 22f)) * (random.Next(5) == 0 ? 0.05f : 1f);
            var pose = new CarPose(
                new Vector2(Between(0f, 2000f), Between(0f, 2000f)), heading, velocity, Between(-1.2f, 1.2f), build.MassKg,
                new Vector2(Between(-6f, 6f), Between(-7f, 7f)));
            var brake = random.Next(8) switch
            {
                0 => float.PositiveInfinity,
                1 or 2 => Between(0f, 9f),
                _ => 0f,
            };
            var command = new DriveCommand(
                random.Next(3) == 0 ? 0f : Between(-0.55f, 0.55f), brake > 0f ? 0f : Between(0f, 4f), brake,
                random.Next(6) == 0, random.Next(7) == 0);
            var ground = new SurfaceUnderWheel[TyreModel.Wheels];
            var spin = new float[TyreModel.Wheels];
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                ground[wheel] = new SurfaceUnderWheel(Between(0f, 1.1f), random.Next(3) == 0 ? Between(0f, 2f) : 0f, Between(0f, 40f), random.Next(4) == 0);
                spin[wheel] = Vector2.Dot(velocity, pose.Forward) + Between(-3f, 3f);
            }

            var ceiling = random.Next(4) == 0 ? float.PositiveInfinity : Between(0f, 6f);
            cars[at] = new Car(build, buildAt, pose, command, ceiling, ground, spin);
        }

        return cars;
    }

    /// <summary>
    /// <b>Every patch of two thousand cars spends what the host's model spends</b>, lands its rim where the host's does
    /// and scrubs the ground as the host's does, to the bit (<see cref="Same(float, float)"/>).
    /// </summary>
    [Fact]
    public void TheTyresSpendWhatTheHostsModelSpends()
    {
        CarBuild[] builds = [CarBuild.Nominal(Config, Config.Car.DrivenFrontShare), .. CarCatalog.Shared.Variants.Select(look => CarBuild.Of(Config, look))];
        var cars = Cars(2000, 3, builds);
        var dtS = Config.TickSeconds;

        using var buildBuffer = device.Holding<CarBuildRow>(builds.Select(build => CarBuildRow.Of(build)).ToArray());
        using var buildOf = device.Holding<uint>(cars.Select(car => (uint)car.BuildAt).ToArray());
        using var poses = device.Holding<PoseRow>(cars.Select(car => new PoseRow(
            car.Pose.PositionM, car.Pose.VelocityMps, car.Pose.Forward, car.Pose.AccelerationMps2, car.Pose.HeadingRad,
            car.Pose.YawRateRadPerS, car.Pose.MassKg, 0f)).ToArray());
        using var commands = device.Holding<CommandRow>(cars.Select(car => new CommandRow(
            car.Command.SteerRad, car.Command.ThrottleMps2, car.Command.BrakeMps2,
            (car.Command.Handbrake ? 1u : 0u) | (car.Command.Reverse ? 2u : 0u))).ToArray());
        using var ceilings = device.Holding<float>(cars.Select(car => car.CeilingMps2).ToArray());
        using var surfaces = device.Holding<SurfaceRow>(cars.SelectMany(car => car.Ground).Select(ground => new SurfaceRow(
            ground.Coefficient, ground.DragMps2, ground.MarkThresholdM2S3, ground.Ploughs ? 1u : 0u)).ToArray());
        using var spins = device.Holding<Vector4>(cars.Select(car => new Vector4(car.Spin[0], car.Spin[1], car.Spin[2], car.Spin[3])).ToArray());
        using var impulses = device.Sized<Vector4>(cars.Length * TyreModel.Wheels);
        using var scrubs = device.Sized<ScrubRow>(cars.Length * TyreModel.Wheels);

        GpuBuffer[] buffers = [buildBuffer, buildOf, poses, commands, ceilings, surfaces, spins, impulses, scrubs];
        using var kernel = new ComputeKernel(device.Vk, "tyres_step.comp", buffers.Length, (uint)Marshal.SizeOf<Figures>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var recording = batch.Begin();
        kernel.Dispatch(recording, set, (uint)((cars.Length + 63) / 64), new Figures(
            (uint)cars.Length, dtS, Config.Tyre.WheelSpinFadeMps, Config.Tyre.WheelSpinAllowanceMps, Config.Marks.SlipMps,
            Config.Marks.CorneringSlipMps, Config.Marks.PloughCrawlMps, Config.CarEngineBrakingMps2,
            Config.Tyre.WorkingSlipTangent, Config.Tyre.AlongSlipEpsilonMps, Config.Tyre.LiftedAxleLoadShare,
            Config.Tyre.AckermannLeastSteerRad, Config.Tyre.InnerWheelLeastRadiusM));
        batch.SubmitAndWait();

        var differ = new List<string>();
        Span<Vector2> atM = stackalloc Vector2[TyreModel.Wheels];
        Span<WheelImpulse> spent = stackalloc WheelImpulse[TyreModel.Wheels];
        Span<TyreScrub> scrub = stackalloc TyreScrub[TyreModel.Wheels];
        for (var at = 0; at < cars.Length; at++)
        {
            var car = cars[at];
            TyreModel.WheelPointsM(car.Build, car.Pose, atM);
            var spin = car.Spin.ToArray();
            TyreModel.Step(Config, car.Build, car.Pose, car.Command, car.CeilingMps2, atM, car.Ground, spin, dtS, spent, scrub);

            var deviceSpin = spins.Span<Vector4>()[at];
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                var onDevice = impulses.Span<Vector4>()[at * 4 + wheel];
                var scrubbed = scrubs.Span<ScrubRow>()[at * 4 + wheel];
                var spinOnDevice = wheel switch { 0 => deviceSpin.X, 1 => deviceSpin.Y, 2 => deviceSpin.Z, _ => deviceSpin.W };
                if (!Same(spent[wheel].ImpulseNs, new Vector2(onDevice.X, onDevice.Y)) || !Same(spin[wheel], spinOnDevice)
                    || !Same(scrub[wheel].SlideSpeedMps, scrubbed.SlideSpeedMps) || scrub[wheel].Sliding != ((scrubbed.Flags & 2u) != 0))
                {
                    differ.Add($"car {at} wheel {wheel}: host {spent[wheel].ImpulseNs} spin {spin[wheel]}, device ({onDevice.X}, {onDevice.Y}) spin {spinOnDevice}");
                }
            }
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {cars.Length * 4} patches differ: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>The same float to the bit.</summary>
    static bool Same(float host, float onDevice) => BitConverter.SingleToInt32Bits(host) == BitConverter.SingleToInt32Bits(onDevice);

    static bool Same(Vector2 host, Vector2 onDevice) => Same(host.X, onDevice.X) && Same(host.Y, onDevice.Y);
}
