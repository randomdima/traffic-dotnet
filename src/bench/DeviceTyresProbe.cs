using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>A running town's whole tyre pass on the device and on the host</b>: once a town has driven a while, every car's
/// tyres for one tick — the ground under four wheels, the ceiling, the standstill, the model, the hold, the slide and
/// the marks — run as the tick runs them (<see cref="TyrePass.Step"/>, car by car) and as one kernel
/// (<see cref="DeviceTyres"/>), timed, and held to one answer.
/// </summary>
/// <remarks>
/// Both are handed the same state every round, as the town left it, so a round is the same tick asked again. The
/// device's figure is its kernel alone; what taking the rows in and handing them back costs is printed beside it, and
/// is what a town resident on the device does not pay.
/// </remarks>
internal static class DeviceTyresProbe
{
    const int WarmupTicks = 600;
    const int Rounds = 20;

    /// <summary>What a car's tyres carry, as the town left it, and the pass's scratch, both four to a car.</summary>
    sealed class Carried(int cars)
    {
        public readonly float[] SpinMps = new float[cars * TyreModel.Wheels];
        public readonly float[] ScrubTravelM = new float[cars * TyreModel.Wheels];
        public readonly bool[] Marking = new bool[cars * TyreModel.Wheels];
        public readonly Vector2[] MarkFromM = new Vector2[cars * TyreModel.Wheels];
        public readonly float[] MarkIntensity = new float[cars * TyreModel.Wheels];
        public readonly float[] SlipThrottle = new float[cars];
        public readonly bool[] DrivenSlipping = new bool[cars];
        public readonly bool[] AtRest = new bool[cars];
        public readonly SurfaceUnderWheel[] Ground = new SurfaceUnderWheel[cars * TyreModel.Wheels];
        public readonly WheelImpulse[] Impulses = new WheelImpulse[cars * TyreModel.Wheels];
        public readonly TyreScrub[] Scrub = new TyreScrub[cars * TyreModel.Wheels];
        public readonly (int Car, TyreMark Mark)[] Laid = new (int, TyreMark)[cars * TyreModel.Wheels];
        public int LaidCount;

        public TyreCarry Of(int car) => new(
            SpinMps.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels), ScrubTravelM.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels),
            Marking.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels), MarkFromM.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels),
            MarkIntensity.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels), ref SlipThrottle[car], ref DrivenSlipping[car]);

        public void TakeFrom(CarFleet fleet, int cars)
        {
            var wheels = cars * TyreModel.Wheels;
            fleet.WheelSpinMps.AsSpan(0, wheels).CopyTo(SpinMps);
            fleet.ScrubTravelM.AsSpan(0, wheels).CopyTo(ScrubTravelM);
            fleet.Marking.AsSpan(0, wheels).CopyTo(Marking);
            fleet.MarkFromM.AsSpan(0, wheels).CopyTo(MarkFromM);
            fleet.MarkIntensity.AsSpan(0, wheels).CopyTo(MarkIntensity);
            fleet.SlipThrottle.AsSpan(0, cars).CopyTo(SlipThrottle);
            fleet.DrivenSlipping.AsSpan(0, cars).CopyTo(DrivenSlipping);
        }
    }

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        var fleet = world.Cars;
        var count = fleet.Count;
        var poses = new CarPose[count];
        for (var car = 0; car < count; car++)
        {
            poses[car] = new CarPose(
                fleet.PositionM[car], fleet.HeadingRad[car], fleet.VelocityMps[car], fleet.YawRateRadPerS[car], fleet.MassKg[car],
                fleet.AccelerationMps2[car]);
        }

        var host = new Carried(count);
        var hostMs = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            host.TakeFrom(fleet, count);
            var took = Stopwatch.StartNew();
            HostPass(config, world.WheelGrounds, world.PhysicsForInstruments, fleet, poses, host);
            hostMs = Math.Min(hostMs, took.Elapsed.TotalMilliseconds);
        }

        var buildOf = new uint[count];
        var builds = new List<CarBuild>();
        var buildAt = new Dictionary<byte, int>();
        for (var car = 0; car < count; car++)
        {
            if (!buildAt.TryGetValue(fleet.Variant[car], out var at))
            {
                at = builds.Count;
                buildAt.Add(fleet.Variant[car], at);
                builds.Add(fleet.BuildOf(car));
            }

            buildOf[car] = (uint)at;
        }

        using var vk = Vk.Open("traffic-dotnet", validation: false);
        using var tyres = new DeviceTyres(vk, config, world.WheelGrounds, builds.ToArray(), count);
        var physics = world.PhysicsForInstruments;
        for (var car = 0; car < count; car++)
        {
            tyres.BuildOf[car] = buildOf[car];
            tyres.Poses[car] = DeviceTyres.PoseRow.Of(poses[car]);
            tyres.Commands[car] = DeviceTyres.CommandRow.Of(fleet.Command[car]);
            tyres.Cars[car] = new DeviceTyres.CarRow(
                1f / physics.InverseInertiaOf(fleet.Body[car]), fleet.SlipThrottle[car],
                DeviceTyres.CarRow.FlagsOf(fleet.Action[car] == CarAction.Hand, fleet.DrivenSlipping[car], fleet.WheelsOnAWay[car]));
            for (var wheel = car * TyreModel.Wheels; wheel < (car + 1) * TyreModel.Wheels; wheel++)
            {
                tyres.Wheels[wheel] = new DeviceTyres.WheelRow(
                    fleet.MarkFromM[wheel], fleet.WheelSpinMps[wheel], fleet.ScrubTravelM[wheel], fleet.MarkIntensity[wheel],
                    fleet.Marking[wheel] ? 1u : 0u);
            }
        }

        var deviceMs = double.MaxValue;
        var copiesMs = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            tyres.Step(count);
            deviceMs = Math.Min(deviceMs, tyres.PassMs);
            copiesMs = Math.Min(copiesMs, tyres.CopiesMs);
        }

        var (differ, notToTheBit, first) = Compare(host, tyres, count);
        var hostMarks = host.Laid.AsSpan(0, host.LaidCount);
        var deviceMarks = tyres.MarksLaid();
        var marksDiffer = MarksDiffer(hostMarks, deviceMarks);
        var atRest = 0;
        for (var car = 0; car < count; car++) atRest += host.AtRest[car] ? 1 : 0;

        Console.WriteLine($"device tyres probe — {map} on {vk.DeviceName}, {count} cars after {WarmupTicks} ticks, {count - atRest} not at rest");
        Console.WriteLine($"every car's whole tyre pass for one tick, the best of {Rounds} rounds:");
        Console.WriteLine($"  host   {hostMs,8:F3} ms   {hostMs * 1000d / count,6:F3} µs a car");
        Console.WriteLine($"  device {deviceMs,8:F3} ms   {deviceMs * 1000d / count,6:F3} µs a car   (+{copiesMs:F3} ms taking the rows in and handing them back)");
        Console.WriteLine($"  {differ} of {count} cars differ past the device's trigonometry; {notToTheBit} not to the bit");
        if (first >= 0) Describe(fleet, host, tyres, first);
        Console.WriteLine($"  marks laid: host {hostMarks.Length}, device {tyres.MarkCount}, {marksDiffer} differ");
    }

    /// <summary>One car the two answered differently, wheel by wheel, so a difference says where it came from.</summary>
    static void Describe(CarFleet fleet, Carried host, DeviceTyres tyres, int car)
    {
        var after = tyres.CarsAfter[car];
        Console.WriteLine(
            $"    the first, car {car}: {fleet.Command[car]}, {fleet.Action[car]}; throttle host {host.SlipThrottle[car]} device " +
            $"{after.SlipThrottle}, slipping host {host.DrivenSlipping[car]} device {(after.Flags & DeviceTyres.CarRow.DrivenSlipping) != 0}");
        for (var wheel = car * TyreModel.Wheels; wheel < (car + 1) * TyreModel.Wheels; wheel++)
        {
            var spent = tyres.Impulses[wheel];
            Console.WriteLine(
                $"    wheel {wheel - (car * TyreModel.Wheels)}: host {host.Impulses[wheel]} spin {host.SpinMps[wheel]} {host.Scrub[wheel]} " +
                $"{host.Ground[wheel]} travel {host.ScrubTravelM[wheel]} marking {host.Marking[wheel]} from {host.MarkFromM[wheel]} " +
                $"{host.MarkIntensity[wheel]}; device {spent} {tyres.WheelsAfter[wheel]} {tyres.Scrub[wheel]}");
        }
    }

    /// <summary>Every car's pass as the tick asks it, in roster order, with the marks each lays kept in that order.</summary>
    static void HostPass(SimConfig config, in WheelGrounds grounds, PhysicsWorld physics, CarFleet fleet, CarPose[] poses, Carried host)
    {
        Span<TyreMark> marks = stackalloc TyreMark[TyreModel.Wheels];
        host.LaidCount = 0;
        for (var car = 0; car < poses.Length; car++)
        {
            var four = car * TyreModel.Wheels;
            host.AtRest[car] = TyrePass.Step(
                config, grounds, fleet.BuildOf(car), poses[car], fleet.Command[car], fleet.Action[car] == CarAction.Hand,
                1f / physics.InverseInertiaOf(fleet.Body[car]), fleet.WheelsOnAWay[car], host.Of(car),
                host.Ground.AsSpan(four, TyreModel.Wheels), host.Impulses.AsSpan(four, TyreModel.Wheels),
                host.Scrub.AsSpan(four, TyreModel.Wheels), marks, out var laid);
            for (var mark = 0; mark < laid; mark++) host.Laid[host.LaidCount++] = (car, marks[mark]);
        }
    }

    /// <summary>
    /// How many cars the device answered differently: past the device's trigonometry on any figure or on any outcome at
    /// all, and — of every figure — not to the bit.
    /// </summary>
    static (int Differ, int NotToTheBit, int First) Compare(Carried host, DeviceTyres tyres, int count)
    {
        var differ = 0;
        var notToTheBit = 0;
        var first = -1;
        for (var car = 0; car < count; car++)
        {
            var after = tyres.CarsAfter[car];
            var outcomes = host.AtRest[car] == ((after.Flags & DeviceTyres.CarRow.AtRest) != 0)
                && host.DrivenSlipping[car] == ((after.Flags & DeviceTyres.CarRow.DrivenSlipping) != 0);
            var near = Near(host.SlipThrottle[car], after.SlipThrottle);
            var bits = host.SlipThrottle[car] == after.SlipThrottle;
            for (var wheel = car * TyreModel.Wheels; wheel < (car + 1) * TyreModel.Wheels; wheel++)
            {
                var carried = tyres.WheelsAfter[wheel];
                var spent = tyres.Impulses[wheel];
                var scrub = tyres.Scrub[wheel];
                var hostScrub = host.Scrub[wheel];
                outcomes &= host.Marking[wheel] == (carried.Marking != 0)
                    && ((hostScrub.Ploughing ? 1u : 0u) | (hostScrub.Sliding ? 2u : 0u)) == scrub.Flags;
                ReadOnlySpan<(float Host, float Device)> figures =
                [
                    (host.SpinMps[wheel], carried.SpinMps), (host.ScrubTravelM[wheel], carried.ScrubTravelM),
                    (host.MarkFromM[wheel].X, carried.MarkFromM.X), (host.MarkFromM[wheel].Y, carried.MarkFromM.Y),
                    (host.MarkIntensity[wheel], carried.MarkIntensity), (host.Impulses[wheel].ImpulseNs.X, spent.X),
                    (host.Impulses[wheel].ImpulseNs.Y, spent.Y), (host.Impulses[wheel].AtM.X, spent.Z), (host.Impulses[wheel].AtM.Y, spent.W),
                    (hostScrub.SlidePowerM2S3, scrub.SlidePowerM2S3), (hostScrub.PloughPowerM2S3, scrub.PloughPowerM2S3),
                    (hostScrub.SlideSpeedMps, scrub.SlideSpeedMps),
                ];
                foreach (var (onHost, onDevice) in figures)
                {
                    near &= Near(onHost, onDevice);
                    bits &= onHost == onDevice;
                }
            }

            if (!outcomes || !near)
            {
                differ++;
                if (first < 0) first = car;
            }

            if (!outcomes || !bits) notToTheBit++;
        }

        return (differ, notToTheBit, first);
    }

    /// <summary>The marks laid that are not the host's, filed in the same (car, wheel) order.</summary>
    static int MarksDiffer(ReadOnlySpan<(int Car, TyreMark Mark)> host, ReadOnlySpan<DeviceTyres.MarkRow> onDevice)
    {
        var differ = Math.Abs(host.Length - onDevice.Length);
        for (var at = 0; at < Math.Min(host.Length, onDevice.Length); at++)
        {
            var (car, mark) = host[at];
            var laid = onDevice[at];
            if (laid.CarWheel != (uint)((car * TyreModel.Wheels) + mark.Wheel) || laid.FromM != mark.FromM || laid.ToM != mark.ToM
                || laid.WidthM != mark.WidthM || !Near(mark.Intensity, laid.Intensity) || (laid.Ploughed != 0) != mark.Ploughed)
            {
                differ++;
            }
        }

        return differ;
    }

    /// <summary>Within a part in a thousand and a hundredth absolute — the tests' own reading of the device's trigonometry.</summary>
    static bool Near(float host, float onDevice) => host == onDevice || MathF.Abs(host - onDevice) <= 1e-2f + (1e-3f * MathF.Abs(host));
}
