using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Runtime;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>The driver on the device</b> (<c>agents/car/control/kernels/follower.glsl</c>): what
/// <see cref="CarFollower.DriveTheLine"/> makes of a car on its line — the wheel, the pedal, the term that set the speed,
/// the speed the road alone allowed and whether the margin was spent — asked of the kernel over the fixture town's own
/// lines.
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class FollowerKernelTests(Lavapipe device) : IClassFixture<Lavapipe>
{
    static readonly SimConfig Config = SimConfig.Shipped();

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct PoseRow(
        Vector2 PositionM, Vector2 VelocityMps, Vector2 Forward, Vector2 AccelerationMps2, float HeadingRad, float YawRateRadPerS,
        float MassKg, float Pad);

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct CommandRow(float SteerRad, float ThrottleMps2, float BrakeMps2, uint HandbrakeAndReverse)
    {
        public static CommandRow Of(in DriveCommand command) =>
            new(command.SteerRad, command.ThrottleMps2, command.BrakeMps2, (command.Handbrake ? 1u : 0u) | (command.Reverse ? 2u : 0u));
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct ContextRow(
        float GroundCoefficient, float AuthorityM, uint GrantCutBy, float PlaceStopM, float MarginM, uint WaitsToPass,
        float HorizonM, float PassAsideM, float BackUpM, uint Blocked, float WaitAtM, float Pad)
    {
        public static ContextRow Of(in DriveContext context) => new(
            context.GroundCoefficient, context.AuthorityM, (uint)context.GrantCutBy, context.PlaceStopM, context.MarginM,
            context.WaitsToPass ? 1u : 0u, context.HorizonM, context.PassAsideM, context.BackUpM, context.Blocked ? 1u : 0u,
            context.WaitAtM, 0f);
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly record struct Figures(
        uint CarCount, float DtS, float LookaheadS, float GripMargin, float BrakingMargin, float StopSpeedMps,
        float WaitingToPassBrakingShare, float ReactionS, float CallPaceMps, float StandardGravityMps2, float WheelSpinFadeMps,
        float WheelSpinAllowanceMps, float MarkSlipMps, float MarkCorneringSlipMps, float PloughCrawlMps, float EngineBrakingMps2);

    sealed record Car(
        CarBuild Build, int BuildAt, CarPose Pose, ArcSeg[] Line, float[] Entries, float LengthM, float ProgressM, DriveContext Context,
        Vector2 Travel, float AlongMps, bool Reverse, DriveCommand Last, bool Laneless, bool BlueLight, float PaceMps);

    static Car[] Cars(int count, int seed, CarBuild[] builds)
    {
        var graph = RoadGraph.Build(Towns.Of(Towns.Fixture), Config);
        var random = new Random(seed);
        float Between(float least, float most) => least + (random.NextSingle() * (most - least));
        float Sometimes(float value, int oneIn) => random.Next(oneIn) == 0 ? value : float.PositiveInfinity;

        var room = LineAssembler.ArcsFor(graph);
        var cars = new List<Car>();
        while (cars.Count < count)
        {
            Span<int> chain = stackalloc int[4];
            chain[0] = random.Next(graph.LaneCount);
            var lanes = 1;
            while (lanes < chain.Length && graph.LanesFrom(chain[lanes - 1]).Length > 0)
            {
                var onward = graph.LanesFrom(chain[lanes - 1]);
                chain[lanes++] = onward[random.Next(onward.Length)];
            }

            var arcs = new ArcSeg[room];
            var starts = new float[LineAssembler.MostLanes];
            var ends = new float[LineAssembler.MostLanes];
            var line = LineAssembler.Assemble(graph, chain[..lanes], arcs, starts, ends);
            if (line.ArcCount == 0 || line.LengthM < 5f) continue;

            var laid = arcs[..line.ArcCount];
            var entries = new float[line.ArcCount];
            CornerLimits.Lay(laid, entries, Config);

            var buildAt = random.Next(builds.Length);
            var build = builds[buildAt];
            var progressM = Between(0f, line.LengthM);
            var on = Spline.SampleAt(laid, progressM);
            var heading = on.HeadingRad + Between(-0.2f, 0.2f);
            var forward = Heading.Unit(heading);
            var rearAxleM = on.PositionM + (Heading.RightOf(on.Direction) * Between(-0.8f, 0.8f));
            var alongMps = random.Next(6) == 0 ? Between(-0.1f, 0.1f) : Between(0f, build.MaxSpeedMps);
            var pose = new CarPose(
                rearAxleM + (forward * build.CentreAheadOfAxleM), heading, forward * alongMps, Between(-0.3f, 0.3f), build.MassKg,
                Vector2.Zero);
            var context = new DriveContext(
                Between(0.3f, 1f), Sometimes(Between(-2f, 120f), 2), (HeadwayKind)random.Next(7), Sometimes(Between(0f, 150f), 6),
                Between(0f, 2f), random.Next(8) == 0, Sometimes(Between(0f, 200f), 3), 0f, 0f, false, Sometimes(Between(0f, 60f), 8));
            var reverse = random.Next(10) == 0;
            var last = new DriveCommand(Between(-0.4f, 0.4f), random.Next(2) == 0 ? Between(0f, 3f) : 0f, random.Next(3) == 0 ? Between(0f, 6f) : 0f, random.Next(5) == 0, reverse);
            cars.Add(new Car(
                build, buildAt, pose, laid, entries, line.LengthM, progressM, context, reverse ? -forward : forward, alongMps, reverse, last,
                random.Next(12) == 0, random.Next(10) == 0, Sometimes(Between(2f, 15f), 10)));
        }

        return [.. cars];
    }

    /// <summary>
    /// <b>Two thousand cars on the fixture town's lines are driven as the host drives them</b>: the same term binds each,
    /// the same pedal is pressed by as much and the margin is spent on the same ones — to the device's own trigonometry.
    /// </summary>
    [Fact]
    public void TheLinesAreDrivenAsTheHostDrivesThem()
    {
        CarBuild[] builds = [CarBuild.Nominal(Config, Config.Car.DrivenFrontShare), .. CarCatalog.Shared.Variants.Select(look => CarBuild.Of(Config, look))];
        var cars = Cars(2000, 5, builds);

        var arcs = new List<DeviceGround.ArcRow>();
        var entries = new List<float>();
        var lines = new List<UInt4>();
        foreach (var car in cars)
        {
            var first = (uint)arcs.Count;
            arcs.AddRange(car.Line.Select(arc => DeviceGround.ArcRow.Of(arc)));
            entries.AddRange(car.Entries);
            lines.Add(new UInt4(first, (uint)arcs.Count, (car.Reverse ? 1u : 0u) | (car.Laneless ? 2u : 0u) | (car.BlueLight ? 4u : 0u), 0u));
        }

        using var buildBuffer = device.Holding<CarBuildRow>(builds.Select(build => CarBuildRow.Of(build)).ToArray());
        using var buildOf = device.Holding<uint>(cars.Select(car => (uint)car.BuildAt).ToArray());
        using var poses = device.Holding<PoseRow>(cars.Select(car => new PoseRow(
            car.Pose.PositionM, car.Pose.VelocityMps, car.Pose.Forward, car.Pose.AccelerationMps2, car.Pose.HeadingRad,
            car.Pose.YawRateRadPerS, car.Pose.MassKg, 0f)).ToArray());
        using var lineArcs = device.Holding<DeviceGround.ArcRow>(CollectionsMarshal.AsSpan(arcs));
        using var entryBuffer = device.Holding<float>(CollectionsMarshal.AsSpan(entries));
        using var lineBuffer = device.Holding<UInt4>(CollectionsMarshal.AsSpan(lines));
        using var lineFigures = device.Holding<Vector4>(cars.Select(car => new Vector4(car.ProgressM, car.LengthM, car.AlongMps, car.PaceMps)).ToArray());
        using var travels = device.Holding<Vector2>(cars.Select(car => car.Travel).ToArray());
        using var contexts = device.Holding<ContextRow>(cars.Select(car => ContextRow.Of(car.Context)).ToArray());
        using var lasts = device.Holding<CommandRow>(cars.Select(car => CommandRow.Of(car.Last)).ToArray());
        using var commands = device.Sized<CommandRow>(cars.Length);
        using var outcomes = device.Sized<Vector4>(cars.Length);

        GpuBuffer[] buffers = [buildBuffer, buildOf, poses, lineArcs, entryBuffer, lineBuffer, lineFigures, travels, contexts, lasts, commands, outcomes];
        using var kernel = new ComputeKernel(device.Vk, "drive_line.comp", buffers.Length, (uint)Marshal.SizeOf<Figures>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(device.Vk);
        var recording = batch.Begin();
        kernel.Dispatch(recording, set, (uint)((cars.Length + 63) / 64), new Figures(
            (uint)cars.Length, Config.TickSeconds, Config.Driving.LookaheadS, Config.Driving.GripMargin, Config.Driving.BrakingMargin,
            Config.Driving.StopSpeedMps, Config.Driving.WaitingToPassBrakingShare, Config.CarReactionS, Config.Ambulance.CallPaceMps,
            Config.Tyre.StandardGravityMps2, Config.Tyre.WheelSpinFadeMps, Config.Tyre.WheelSpinAllowanceMps, Config.Marks.SlipMps,
            Config.Marks.CorneringSlipMps, Config.Marks.PloughCrawlMps, Config.CarEngineBrakingMps2));
        batch.SubmitAndWait();

        var differ = new List<string>();
        for (var at = 0; at < cars.Length; at++)
        {
            var car = cars[at];
            var host = CarFollower.DriveTheLine(
                Config, car.Build, car.Pose, car.Line, car.Entries, car.ProgressM, car.LengthM, car.Context, car.Travel, car.AlongMps,
                car.Reverse, car.Last, default, car.Laneless, car.BlueLight, car.PaceMps);
            var command = commands.Span<CommandRow>()[at];
            var outcome = outcomes.Span<Vector4>()[at];
            if (!Near(host.Command.SteerRad, command.SteerRad) || !Near(host.Command.ThrottleMps2, command.ThrottleMps2)
                || !Near(host.Command.BrakeMps2, command.BrakeMps2) || CommandRow.Of(host.Command).HandbrakeAndReverse != command.HandbrakeAndReverse
                || (uint)host.Hold != (uint)outcome.X || !Near(host.PlannedMps, outcome.Y) || host.Hazard != (outcome.Z != 0f))
            {
                differ.Add($"car {at}: host {host}, device steer {command.SteerRad} throttle {command.ThrottleMps2} brake {command.BrakeMps2} hold {(DrivingHold)(uint)outcome.X} planned {outcome.Y}");
            }
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {cars.Length} drive differently: {string.Join("; ", differ.Take(5))}");
    }

    /// <summary>
    /// Within a part in a thousand and a hundredth absolute: the lead point is found by the device's own sine and the
    /// wheel by its own arctangent, which Vulkan allows far further off the host's than a float's last bit.
    /// </summary>
    static bool Near(float host, float onDevice) =>
        host == onDevice || MathF.Abs(host - onDevice) <= 1e-2f + (1e-3f * MathF.Abs(host));
}
