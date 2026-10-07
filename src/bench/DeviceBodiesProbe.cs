using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The bodies' phase on the device beside the town's, on a running town</b> (<see cref="DeviceBodyPhase"/>): once the
/// town has driven a while, every tick hands the device the bodies as the town's step finds them and the impulses its
/// tick wrote, and the device spends them, steps and reads every car back into the car table — timed against the town's
/// own phase, and every <see cref="CompareEvery"/> ticks held to it: the impulses to the bit, the poses after the step
/// quoted for how far apart they land.
/// </summary>
/// <remarks>
/// <b>The device solves in colours and the town in contact order</b>, so a crowded step lands a little apart; what is
/// quoted is how far, and nothing fails on it (<c>DeviceBodyPhaseTests</c> holds the two in one order).
/// </remarks>
internal static class DeviceBodiesProbe
{
    const int WarmupTicks = 600;
    const int MeasuredTicks = 600;
    const int CompareEvery = 60;

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        var cars = world.Cars;
        var physics = world.PhysicsForInstruments;
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        var builds = new CarBuild[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = cars.BuildOfVariant(variant);

        using var tyres = new DeviceTyres(vk, config, world.WheelGrounds, builds, cars.Capacity);
        using var solver = new DeviceSolver(vk, config, physics.Table);
        using var phase = new DeviceBodyPhase(vk, config, solver, tyres, world.People.Capacity + (2 * cars.Capacity));

        var compare = false;
        var shadowTicks = 0L;
        var layMs = new List<double>();
        var applyMs = new List<double>();
        var stepMs = new List<double>();
        var mirrorMs = new List<double>();
        var sentMs = new List<double>();
        var sentKb = new List<double>();
        var readMs = new List<double>();
        var bodiesAsked = 0L;
        var bodiesDiffer = 0L;
        string? firstDiffer = null;
        world.BeforeTheImpulses = town =>
        {
            var began = Stopwatch.GetTimestamp();
            phase.Lay(town, config.TickSeconds, everyWheel: true);
            layMs.Add(Stopwatch.GetElapsedTime(began).TotalMilliseconds);
            phase.Step(town.Cars.Count, config.TickSeconds, compare);
            applyMs.Add(phase.ApplyMs);
            stepMs.Add(phase.StepMs);
            mirrorMs.Add(phase.MirrorMs);
            sentMs.Add(phase.SentMs);
            sentKb.Add(phase.SentBytes / 1024d);
            if (compare) readMs.Add(phase.ReadMs);
            shadowTicks += Stopwatch.GetTimestamp() - began;
        };
        world.BeforeTheSolverSteps = before =>
        {
            if (!compare) return;

            var began = Stopwatch.GetTimestamp();
            var table = before.Table;
            for (var body = 0; body < table.Count; body++)
            {
                bodiesAsked++;
                var why = phase.Applied(table, body);
                if (why == null) continue;

                bodiesDiffer++;
                firstDiffer ??= why;
            }

            shadowTicks += Stopwatch.GetTimestamp() - began;
        };

        var apartM = new List<float>();
        var apartMps = new List<float>();
        var apartRad = new List<float>();
        var apartForward = new List<float>();
        var apartMps2 = new List<float>();
        loop.Phases.Reset();
        world.Sub.Reset();
        loop.Timed = world.Timed = true;
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            compare = tick % CompareEvery == 0;
            loop.Advance();
            if (!compare) continue;

            var poses = phase.PosesAfter;
            for (var car = 0; car < cars.Count; car++)
            {
                var row = poses[car];
                apartM.Add(Vector2.Distance(cars.PositionM[car], row.PositionM));
                apartMps.Add(Vector2.Distance(cars.VelocityMps[car], row.VelocityMps));
                apartRad.Add(MathF.Abs(Spline.WrapRad(cars.HeadingRad[car] - row.HeadingRad)));
                apartForward.Add(Vector2.Distance(physics.RotationOf(cars.Body[car]), row.Forward));
                apartMps2.Add(Vector2.Distance(cars.AccelerationMps2[car], row.AccelerationMps2));
            }
        }

        loop.Timed = world.Timed = false;
        world.BeforeTheImpulses = null;
        world.BeforeTheSolverSteps = null;

        var hostMs = loop.Phases.MillisecondsPer(loop.Phases.BodyTicks - shadowTicks);
        var hostSolverMs = loop.Phases.MillisecondsPer(world.Sub.SolverTicks);
        var kernelsMs = new List<double>(applyMs.Count);
        for (var at = 0; at < applyMs.Count; at++) kernelsMs.Add(applyMs[at] + stepMs[at] + mirrorMs[at]);

        Console.WriteLine($"device bodies probe — {map} on {vk.DeviceName}, {cars.Count} cars, {world.People.Count} walkers and {solver.BodyCount} bodies after {WarmupTicks} ticks");
        Console.WriteLine($"{MeasuredTicks} ticks, the device solving in colours:");
        Console.WriteLine($"  host, the bodies' phase           mean {hostMs,8:F3} ms");
        Console.WriteLine($"    of it the solver's step         mean {hostSolverMs,8:F3} ms");
        Console.WriteLine($"  device, the kernels             median {Median(kernelsMs),8:F3} ms   most {kernelsMs.Max(),8:F3} ms");
        Console.WriteLine($"    spending the impulses         median {Median(applyMs),8:F3} ms   most {applyMs.Max(),8:F3} ms");
        Console.WriteLine($"    the solver's step             median {Median(stepMs),8:F3} ms   most {stepMs.Max(),8:F3} ms");
        Console.WriteLine($"    reading the cars back         median {Median(mirrorMs),8:F3} ms   most {mirrorMs.Max(),8:F3} ms");
        Console.WriteLine($"  shadow only: taking the town in median {Median(sentMs),8:F3} ms on the device, {Median(layMs):F3} ms laying it, {Median(sentKb):F0} KiB");
        Console.WriteLine($"  shadow only: handing back       median {Median(readMs),8:F3} ms");
        Console.WriteLine($"every {CompareEvery}th tick, the device beside the town:");
        Console.WriteLine($"  impulses spent: {bodiesDiffer} of {bodiesAsked} bodies differ, to the bit but at rest's edge{(firstDiffer == null ? "" : $"; the first {firstDiffer}")}");
        Console.WriteLine($"  over {apartM.Count} car-ticks after the step, median / 99th / most:");
        Console.WriteLine($"    position      {Quoted(apartM, 1000f, "mm")}");
        Console.WriteLine($"    velocity      {Quoted(apartMps, 1000f, "mm/s")}");
        Console.WriteLine($"    heading       {Quoted(apartRad, 1f, "rad")}");
        Console.WriteLine($"    forward       {Quoted(apartForward, 1f, "")}");
        Console.WriteLine($"    acceleration  {Quoted(apartMps2, 1f, "m/s²")}");
    }

    static string Quoted(List<float> apart, float scale, string unit)
    {
        if (apart.Count == 0) return "none";

        apart.Sort();
        return $"{apart[apart.Count / 2] * scale:G3} / {apart[(int)(apart.Count * 0.99)] * scale:G3} / {apart[^1] * scale:G3} {unit}";
    }

    static double Median(List<double> values)
    {
        if (values.Count == 0) return double.NaN;

        values.Sort();
        return values[values.Count / 2];
    }
}
