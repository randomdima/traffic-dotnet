using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.Tests.Runtime;
using TrafficSimulation.World.Terrain;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>Two thousand cars' tyre pass, on the host and on the device</b>: cars of every variant on and off the fixture town's
/// roads, with every one of the sixteen ways their four wheels may have been laid on a way or off one, at rest, creeping,
/// held by their locks and driven hard, carrying every kind of rim, scrub and mark — <see cref="TyrePass.Step"/> asked of
/// each, and <c>tyre_pass.comp</c> of all of them at once.
/// </summary>
/// <remarks>
/// A front wheel of a car whose wheel is turned points by the device's own tangent and arctangent
/// (<see cref="TyrePassKernelTests.Near(float, float)"/>); everything else is the host's arithmetic to the bit.
/// </remarks>
public sealed class StagedTyrePass : IDisposable
{
    static readonly SimConfig Config = SimConfig.Shipped();

    internal sealed record Car(
        int BuildAt, CarBuild Build, CarPose Pose, DriveCommand Command, bool Hand, float InertiaKgM2,
        byte WheelsOnAWay, float[] SpinMps, float[] ScrubTravelM, bool[] Marking, Vector2[] MarkFromM, float[] MarkIntensity,
        float SlipThrottle, bool DrivenSlipping)
    {
        /// <summary>Whether this wheel points by the device's trigonometry rather than the body's own rotation.</summary>
        public bool Steered(int wheel) => wheel < TyreModel.SteeredWheels && Command.SteerRad != 0f;

        /// <summary>Whether its locks may hold it still this tick (<see cref="TyreModel.HoldStill"/>).</summary>
        public bool MayBeHeld => (Command.LocksEveryWheel || Command.Handbrake) && Command.ThrottleMps2 == 0f;
    }

    /// <summary>What the host's pass made of one car, its carried state as the pass left it.</summary>
    internal sealed record Outcome(
        bool AtRest, float SlipThrottle, bool DrivenSlipping, float[] SpinMps, float[] ScrubTravelM, bool[] Marking,
        Vector2[] MarkFromM, float[] MarkIntensity, WheelImpulse[] Impulses, TyreScrub[] Scrub);

    public StagedTyrePass()
    {
        var plan = Towns.Of(Towns.Fixture);
        var shapes = new GroundShapes(plan.Paving(Config), Config);
        var grounds = WheelGrounds.Of(Config, new GroundLocator(shapes, Config));
        CarBuild[] builds =
            [CarBuild.Nominal(Config, Config.Car.DrivenFrontShare), .. CarCatalog.Shared.Variants.Select(look => CarBuild.Of(Config, look))];
        Cars = Staged(2000, 7, builds, shapes, plan.WorldSizeM);

        var marks = new List<(int Car, TyreMark Mark)>();
        Host = new Outcome[Cars.Length];
        Span<TyreMark> laid = stackalloc TyreMark[TyreModel.Wheels];
        for (var at = 0; at < Cars.Length; at++)
        {
            var car = Cars[at];
            var (spin, travel, marking, from, intensity) =
                (car.SpinMps.ToArray(), car.ScrubTravelM.ToArray(), car.Marking.ToArray(), car.MarkFromM.ToArray(), car.MarkIntensity.ToArray());
            var (slipThrottle, slipping) = (car.SlipThrottle, car.DrivenSlipping);
            var ground = new SurfaceUnderWheel[TyreModel.Wheels];
            var impulses = new WheelImpulse[TyreModel.Wheels];
            var scrub = new TyreScrub[TyreModel.Wheels];
            var atRest = TyrePass.Step(
                Config, grounds, car.Build, car.Pose, car.Command, car.Hand, car.InertiaKgM2, car.WheelsOnAWay,
                new TyreCarry(spin, travel, marking, from, intensity, ref slipThrottle, ref slipping), ground, impulses, scrub, laid,
                out var count);
            for (var mark = 0; mark < count; mark++) marks.Add((at, laid[mark]));
            Host[at] = new Outcome(atRest, slipThrottle, slipping, spin, travel, marking, from, intensity, impulses, scrub);
        }

        HostMarks = [.. marks];

        using var tyres = new DeviceTyres(Device.Vk, Config, grounds, builds, Cars.Length);
        for (var at = 0; at < Cars.Length; at++)
        {
            var car = Cars[at];
            tyres.BuildOf[at] = (uint)car.BuildAt;
            tyres.Poses[at] = DeviceTyres.PoseRow.Of(car.Pose);
            tyres.Commands[at] = DeviceTyres.CommandRow.Of(car.Command);
            tyres.Cars[at] = new DeviceTyres.CarRow(
                car.InertiaKgM2, car.SlipThrottle, DeviceTyres.CarRow.FlagsOf(car.Hand, car.DrivenSlipping, car.WheelsOnAWay));
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                tyres.Wheels[at * TyreModel.Wheels + wheel] = new DeviceTyres.WheelRow(
                    car.MarkFromM[wheel], car.SpinMps[wheel], car.ScrubTravelM[wheel], car.MarkIntensity[wheel], car.Marking[wheel] ? 1u : 0u);
            }
        }

        tyres.Step(Cars.Length);
        CarsAfter = tyres.CarsAfter.ToArray();
        WheelsAfter = tyres.WheelsAfter.ToArray();
        Impulses = tyres.Impulses.ToArray();
        Scrub = tyres.Scrub.ToArray();
        MarkCount = tyres.MarkCount;
        Marks = tyres.MarksLaid().ToArray();
    }

    internal Lavapipe Device { get; } = new();

    internal Car[] Cars { get; }

    internal Outcome[] Host { get; }

    /// <summary>The marks the host laid, car by car in order and each car's in wheel order: what the town files.</summary>
    internal (int Car, TyreMark Mark)[] HostMarks { get; }

    internal DeviceTyres.CarRow[] CarsAfter { get; }

    internal DeviceTyres.WheelRow[] WheelsAfter { get; }

    internal Vector4[] Impulses { get; }

    internal DeviceTyres.ScrubRow[] Scrub { get; }

    internal int MarkCount { get; }

    internal DeviceTyres.MarkRow[] Marks { get; }

    /// <summary>
    /// Cars of every variant: half with their middle on the fixture town's road and half anywhere over it and a margin
    /// round it, so wheels stand on every kind of ground and straddle its edges. An eighth stand at rest with nothing
    /// carried, an eighth creep slowly enough for their locks to hold them, and the rest are driven anyhow.
    /// </summary>
    static Car[] Staged(int count, int seed, CarBuild[] builds, GroundShapes shapes, Vector2 worldSizeM)
    {
        var random = new Random(seed);
        float Between(float least, float most) => least + (random.NextSingle() * (most - least));
        bool OneIn(int n) => random.Next(n) == 0;

        Vector2 Anywhere() => new(Between(-0.05f, 1.05f) * worldSizeM.X, Between(-0.05f, 1.05f) * worldSizeM.Y);

        Vector2 OnTheRoad()
        {
            while (true)
            {
                var pointM = Anywhere();
                if (shapes.At(pointM) == Ground.Road) return pointM;
            }
        }

        var cars = new Car[count];
        Span<Vector2> atM = stackalloc Vector2[TyreModel.Wheels];
        for (var at = 0; at < count; at++)
        {
            var buildAt = random.Next(builds.Length);
            var build = builds[buildAt];
            var kind = random.Next(8);
            var atRest = kind == 0;
            var creeping = kind == 1;

            var heading = Between(-MathF.PI, MathF.PI);
            var velocity = atRest ? Vector2.Zero
                : creeping ? new Vector2(Between(-0.05f, 0.05f), Between(-0.05f, 0.05f))
                : new Vector2(Between(-22f, 22f), Between(-22f, 22f)) * (OneIn(5) ? 0.05f : 1f);
            var yawRate = atRest ? 0f : creeping ? Between(-0.01f, 0.01f) : Between(-1.2f, 1.2f);
            var pose = new CarPose(
                OneIn(2) ? OnTheRoad() : Anywhere(), heading, velocity, yawRate, build.MassKg,
                new Vector2(Between(-6f, 6f), Between(-7f, 7f)));

            var brake = random.Next(8) switch
            {
                0 => float.PositiveInfinity,
                1 or 2 => Between(0f, 9f),
                _ => 0f,
            };
            var throttle = atRest || creeping || brake > 0f || OneIn(3) ? 0f : Between(0f, 4f);
            var command = new DriveCommand(
                OneIn(3) ? 0f : Between(-0.55f, 0.55f), throttle, brake, creeping ? !OneIn(4) : OneIn(6), OneIn(7));

            TyreModel.WheelPointsM(build, pose, atM);
            var spin = new float[TyreModel.Wheels];
            var travel = new float[TyreModel.Wheels];
            var marking = new bool[TyreModel.Wheels];
            var from = new Vector2[TyreModel.Wheels];
            var intensity = new float[TyreModel.Wheels];
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                var awayM = Between(0f, 2f * Config.Marks.SpacingM);
                var awayRad = Between(-MathF.PI, MathF.PI);
                from[wheel] = atM[wheel] + (new Vector2(MathF.Cos(awayRad), MathF.Sin(awayRad)) * awayM);
                if (atRest) continue;

                spin[wheel] = creeping ? 0f : Vector2.Dot(velocity, pose.Forward) + Between(-3f, 3f);
                travel[wheel] = random.Next(6) switch
                {
                    0 or 1 or 2 => 0f,
                    3 => Config.Marks.OnsetM,
                    _ => Between(0f, Config.Marks.OnsetM),
                };
                marking[wheel] = OneIn(3);
                intensity[wheel] = Between(0f, 1f);
            }

            cars[at] = new Car(
                buildAt, build, pose, command, OneIn(5), build.MassKg * Between(1.2f, 3.5f),
                (byte)random.Next(1 << TyreModel.Wheels), spin, travel, marking, from, intensity,
                OneIn(3) ? 1f : Between(Config.Tyre.MinSlipThrottleFraction, 1f), OneIn(3));
        }

        return cars;
    }

    public void Dispose() => Device.Dispose();
}

/// <summary>
/// <b>A car's whole tyre pass on the device</b> (<c>agents/car/body/kernels/tyre_pass.glsl</c>): what
/// <see cref="TyrePass.Step"/> makes of a car — the standstill it skips, the throttle it lifts, the impulses it spends on
/// the ground under each wheel and the hold of its locks, the slide it reports, the marks it carries and lays — asked of
/// the kernel over the fixture town (<see cref="StagedTyrePass"/>).
/// </summary>
[Trait(Tier.Key, Tier.Device)]
[Trait(Priority.Key, Priority.P3)]
public class TyrePassKernelTests(StagedTyrePass staged) : IClassFixture<StagedTyrePass>
{
    /// <summary>The cars the host finds at rest are the ones the device skips, and each spends and scrubs nothing.</summary>
    [Fact]
    public void TheCarsAtRestAreTheHostsAndSpendNothing()
    {
        var differ = new List<string>();
        Each((at, car, host) =>
        {
            var onDevice = (staged.CarsAfter[at].Flags & DeviceTyres.CarRow.AtRest) != 0;
            if (host.AtRest != onDevice) differ.Add($"car {at}: host {host.AtRest}, device {onDevice}");
            if (!onDevice) return;

            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                var spent = staged.Impulses[(at * TyreModel.Wheels) + wheel];
                if (spent != Vector4.Zero || staged.Scrub[(at * TyreModel.Wheels) + wheel] != default)
                {
                    differ.Add($"car {at} wheel {wheel} at rest spent {spent}");
                }
            }
        });

        Assert.True(differ.Count == 0, $"{differ.Count} differ: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>Every car's throttle is backed off or recovered to the host's figure, to the bit, at rest or not.</summary>
    [Fact]
    public void TheThrottleBacksOffAsTheHostsDoes()
    {
        var differ = new List<string>();
        Each((at, car, host) =>
        {
            if (host.SlipThrottle != staged.CarsAfter[at].SlipThrottle)
            {
                differ.Add($"car {at}: host {host.SlipThrottle}, device {staged.CarsAfter[at].SlipThrottle}");
            }
        });

        Assert.True(differ.Count == 0, $"{differ.Count} differ: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>
    /// <b>A car its locks may not hold spends what the host's spends</b>: every patch's impulse and the point it is spent
    /// at, every rim wound on, and every scrub.
    /// </summary>
    [Fact]
    public void TheTyresSpendWhatTheHostsSpend() => AssertSpentAsTheHost(car => !car.MayBeHeld);

    /// <summary>
    /// <b>A car on its locks with nobody on the throttle is held as the host holds it</b> (CAR-52): held still whole where
    /// its rear pair grips the body's momentum and spin, its tyres' own answer where they do not.
    /// </summary>
    [Fact]
    public void ACarItsLocksHoldIsHeldAsTheHostHoldsIt() => AssertSpentAsTheHost(car => car.MayBeHeld);

    /// <summary>
    /// The cars whose driven wheels slipped under the pedal are the host's. A turned front wheel's slide is decided by
    /// the device's own trigonometry against the edge of its ellipse; no car of the sample stands close enough to it
    /// for that to cross.
    /// </summary>
    [Fact]
    public void TheDrivenWheelsSlipWhereTheHostsDo()
    {
        var differ = new List<string>();
        Each((at, car, host) =>
        {
            var onDevice = (staged.CarsAfter[at].Flags & DeviceTyres.CarRow.DrivenSlipping) != 0;
            if (host.DrivenSlipping != onDevice) differ.Add($"car {at}: host {host.DrivenSlipping}, device {onDevice}");
        });

        Assert.True(differ.Count == 0, $"{differ.Count} differ: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>
    /// Every wheel carries on the host's mark: the scrub banked toward one, whether a stretch is open, where it began and
    /// how dark it is.
    /// </summary>
    [Fact]
    public void EveryWheelCarriesTheHostsMark()
    {
        var differ = new List<string>();
        Each((at, car, host) =>
        {
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                var steered = car.Steered(wheel);
                var onDevice = staged.WheelsAfter[(at * TyreModel.Wheels) + wheel];
                if (!Same(host.ScrubTravelM[wheel], onDevice.ScrubTravelM, steered) || host.Marking[wheel] != (onDevice.Marking != 0)
                    || host.MarkFromM[wheel] != onDevice.MarkFromM || !Same(host.MarkIntensity[wheel], onDevice.MarkIntensity, steered))
                {
                    differ.Add(
                        $"car {at} wheel {wheel}: host {host.ScrubTravelM[wheel]} {host.Marking[wheel]} {host.MarkFromM[wheel]} " +
                        $"{host.MarkIntensity[wheel]}, device {onDevice}");
                }
            }
        });

        Assert.True(differ.Count == 0, $"{differ.Count} wheels differ: {string.Join("; ", differ.Take(6))}");
    }

    /// <summary>
    /// <b>The marks the ring holds are the host's</b>, filed in (car, wheel) order: each from and to the same points, as
    /// wide, as dark and as ploughed.
    /// </summary>
    [Fact]
    public void TheMarksLaidAreTheHosts()
    {
        var differ = new List<string>();
        var host = staged.HostMarks;
        var onDevice = staged.Marks;
        if (staged.MarkCount != host.Length) differ.Add($"host laid {host.Length}, device {staged.MarkCount}");

        for (var at = 0; at < Math.Min(host.Length, onDevice.Length); at++)
        {
            var (car, mark) = host[at];
            var laid = onDevice[at];
            var steered = staged.Cars[car].Steered(mark.Wheel);
            if (laid.CarWheel != (uint)((car * TyreModel.Wheels) + mark.Wheel) || laid.FromM != mark.FromM || laid.ToM != mark.ToM
                || laid.WidthM != mark.WidthM || !Same(mark.Intensity, laid.Intensity, steered) || (laid.Ploughed != 0) != mark.Ploughed)
            {
                differ.Add($"mark {at}: host car {car} {mark}, device {laid}");
            }
        }

        Assert.True(differ.Count == 0, $"{differ.Count} of {host.Length} differ: {string.Join("; ", differ.Take(6))}");
    }

    void AssertSpentAsTheHost(Func<StagedTyrePass.Car, bool> asked)
    {
        var differ = new List<string>();
        Each((at, car, host) =>
        {
            if (!asked(car) || host.AtRest) return;

            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
            {
                var steered = car.Steered(wheel);
                var spent = staged.Impulses[(at * TyreModel.Wheels) + wheel];
                var scrub = staged.Scrub[(at * TyreModel.Wheels) + wheel];
                var spin = staged.WheelsAfter[(at * TyreModel.Wheels) + wheel].SpinMps;
                var hostScrub = host.Scrub[wheel];
                var flags = (hostScrub.Ploughing ? 1u : 0u) | (hostScrub.Sliding ? 2u : 0u);
                if (!Same(host.Impulses[wheel].ImpulseNs, new Vector2(spent.X, spent.Y), steered)
                    || host.Impulses[wheel].AtM != new Vector2(spent.Z, spent.W) || !Same(host.SpinMps[wheel], spin, steered)
                    || !Same(hostScrub.SlidePowerM2S3, scrub.SlidePowerM2S3, steered)
                    || !Same(hostScrub.PloughPowerM2S3, scrub.PloughPowerM2S3, steered)
                    || !Same(hostScrub.SlideSpeedMps, scrub.SlideSpeedMps, steered) || flags != scrub.Flags)
                {
                    differ.Add($"car {at} wheel {wheel}: host {host.Impulses[wheel]} spin {host.SpinMps[wheel]} {hostScrub}, device {spent} spin {spin} {scrub}");
                }
            }
        });

        Assert.True(differ.Count == 0, $"{differ.Count} wheels differ: {string.Join("; ", differ.Take(6))}");
    }

    void Each(Action<int, StagedTyrePass.Car, StagedTyrePass.Outcome> check)
    {
        for (var at = 0; at < staged.Cars.Length; at++) check(at, staged.Cars[at], staged.Host[at]);
    }

    /// <summary>The host's figure to the bit, or <see cref="Near(float, float)"/> where it flows from a turned wheel.</summary>
    static bool Same(float host, float onDevice, bool steered) => steered ? Near(host, onDevice) : host == onDevice;

    static bool Same(Vector2 host, Vector2 onDevice, bool steered) => Same(host.X, onDevice.X, steered) && Same(host.Y, onDevice.Y, steered);

    /// <summary>
    /// Within a part in a thousand, and a hundredth absolute. A turned wheel points by the device's own tan, atan, sin
    /// and cos, which Vulkan allows to stand far further off the host's than a float's last bit; a mistake in the pass is
    /// a part in one, not in a thousand.
    /// </summary>
    internal static bool Near(float host, float onDevice) =>
        host == onDevice || MathF.Abs(host - onDevice) <= 1e-2f + (1e-3f * MathF.Abs(host));
}
