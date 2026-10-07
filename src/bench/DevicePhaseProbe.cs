using System.Diagnostics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Phase 4 handed to the device in step, against the town's own, on a running town</b> (<see cref="DeviceTownBodies"/>):
/// once the town has driven a while, its own phase timed for a while, then the device set as the town's and its phase
/// timed for as long — the host's whole phase, and inside it the host laying the rows, the device's sends, kernels and
/// hand-back, the host's wait, and the host writing the answer into the fleets and the table apart.
/// </summary>
/// <remarks>
/// <b>The device steps the bodies alone here</b>: it takes no agent and runs no tyre pass, so every car's wheels and every
/// walker's impulse are the town's and are sent — which the town's device, ticking the cars and walkers in its own
/// earlier submit, does not send. Those bytes are quoted apart.
/// </remarks>
internal static class DevicePhaseProbe
{
    const int WarmupTicks = 600;
    const int MeasuredTicks = 300;
    const float MillisecondsPerSecond = 1000f;

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        loop.Phases.Reset();
        world.Sub.Reset();
        loop.Timed = world.Timed = true;
        var hostTicks = new List<double>();
        for (var tick = 0; tick < MeasuredTicks; tick++) hostTicks.Add(PhaseFourMs(loop));

        var hostPhaseMs = loop.Phases.MillisecondsPer(loop.Phases.BodyTicks);
        var hostSolverMs = loop.Phases.MillisecondsPer(world.Sub.SolverTicks);
        var hostContactsMs = loop.Phases.MillisecondsPer(loop.Phases.ContactTicks);

        var cars = world.Cars;
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        var builds = new CarBuild[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = cars.BuildOfVariant(variant);

        using var tyres = new DeviceTyres(vk, config, world.WheelGrounds, builds, cars.Capacity);
        using var bodies = new DeviceTownBodies(vk, config, world, tyres);
        world.Device = bodies;

        loop.Phases.Reset();
        loop.Advance();
        var wholeMs = loop.Phases.MillisecondsPer(loop.Phases.BodyTicks);
        var wholeKb = bodies.Phase.SentBytes / 1024d;

        var deviceTicks = new List<double>();
        var lay = new List<double>();
        var wait = new List<double>();
        var handBack = new List<double>();
        var roster = new List<double>();
        var settling = new List<double>();
        var sent = new List<double>();
        var kernels = new List<double>();
        var read = new List<double>();
        var sentKb = new List<double>();
        var wheelsKb = new List<double>();
        var readKb = new List<double>();
        var contacts = 0L;
        var began = 0L;
        loop.Phases.Reset();
        world.Sub.Reset();
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            deviceTicks.Add(PhaseFourMs(loop));
            var phase = bodies.Phase;
            lay.Add(bodies.LayMs);
            wait.Add(bodies.WaitMs);
            handBack.Add(bodies.HandBackMs);
            roster.Add(bodies.RosterMs);
            settling.Add(bodies.SettlingMs);
            sent.Add(phase.SentMs);
            kernels.Add(phase.KernelsMs);
            read.Add(phase.ReadMs);
            sentKb.Add(phase.SentBytes / 1024d);
            wheelsKb.Add(phase.WheelBytes / 1024d);
            readKb.Add(bodies.ReadBytes / 1024d);
            contacts += bodies.Solver.ContactCount;
            foreach (var _ in world.PhysicsForInstruments.BeganTouchingThisStep()) began++;
        }

        loop.Timed = world.Timed = false;
        world.Device = null;
        var devicePhaseMs = loop.Phases.MillisecondsPer(loop.Phases.BodyTicks);
        var deviceStepMs = loop.Phases.MillisecondsPer(world.Sub.SolverTicks);
        var deviceContactsMs = loop.Phases.MillisecondsPer(loop.Phases.ContactTicks);

        Console.WriteLine(
            $"device phase probe — {map} on {vk.DeviceName}, {cars.Count} cars, {world.People.Count} walkers and "
            + $"{bodies.Solver.BodyCount} bodies after {WarmupTicks} ticks; {MeasuredTicks} ticks each way");
        Console.WriteLine(
            $"  the town's own phase 4                  median {Median(hostTicks),8:F3} ms   99th {Percentile(hostTicks, 0.99):F3} ms, mean "
            + $"{hostPhaseMs:F3} ms, of it the solver's step {hostSolverMs:F3} ms; phase 5 {hostContactsMs:F3} ms");
        Console.WriteLine(
            $"  phase 4 handed to the device            median {Median(deviceTicks),8:F3} ms   99th {Percentile(deviceTicks, 0.99):F3} ms, mean "
            + $"{devicePhaseMs:F3} ms, of it the device's step {deviceStepMs:F3} ms; phase 5 {deviceContactsMs:F3} ms");
        Console.WriteLine($"    host, laying the rows                 median {Median(lay),8:F3} ms");
        Console.WriteLine($"    host, submit to answer                median {Median(wait),8:F3} ms");
        Console.WriteLine($"      device, taking the rows in          median {Median(sent),8:F3} ms   {Median(sentKb):F0} KiB, of it {Median(wheelsKb):F0} KiB the town's wheels");
        Console.WriteLine($"      device, impulses, step, cars back   median {Median(kernels),8:F3} ms");
        Console.WriteLine($"      device, handing back                median {Median(read),8:F3} ms   {Median(readKb):F0} KiB");
        Console.WriteLine(
            $"    host, writing the answer back         median {Median(handBack),8:F3} ms   of it the table's roster {Median(roster):F3} ms, "
            + $"its waking, freezing and touches {Median(settling):F3} ms");
        Console.WriteLine($"  the first step, laid whole: phase 4 {wholeMs:F3} ms, {wholeKb:F0} KiB sent");
        Console.WriteLine(
            $"  an empty submit, to answer              median {EmptySubmitMs(vk, 0),8:F3} ms back to back, "
            + $"{EmptySubmitMs(vk, (int)MathF.Ceiling(config.TickSeconds * MillisecondsPerSecond)):F3} ms after a tick's idle");
        Console.WriteLine($"  {contacts / (double)MeasuredTicks:F1} contacts a step, {began / (double)MeasuredTicks:F2} pairs began touching a step");
    }

    /// <summary>One tick advanced, and what its phase 4 took, in milliseconds.</summary>
    static double PhaseFourMs(SimLoop<TownWorld> loop)
    {
        var before = loop.Phases.BodyTicks;
        loop.Advance();
        return Stopwatch.GetElapsedTime(0, loop.Phases.BodyTicks - before).TotalMilliseconds;
    }

    /// <summary>
    /// What a submit with nothing in it costs the host from submit to answer — back to back, or with the device left idle
    /// for <paramref name="idleMs"/> first, as it is for the rest of a tick: the floor under the wait.
    /// </summary>
    static double EmptySubmitMs(Vk vk, int idleMs)
    {
        using var batch = new ComputeBatch(vk);
        var waits = new List<double>();
        for (var round = 0; round < MeasuredTicks; round++)
        {
            if (idleMs > 0) Thread.Sleep(idleMs);

            var began = Stopwatch.GetTimestamp();
            batch.Begin();
            batch.SubmitAndWait();
            waits.Add(Stopwatch.GetElapsedTime(began).TotalMilliseconds);
        }

        return Median(waits);
    }

    static double Median(List<double> values) => Percentile(values, 0.5);

    static double Percentile(List<double> values, double share)
    {
        if (values.Count == 0) return double.NaN;

        values.Sort();
        return values[Math.Min(values.Count - 1, (int)(share * values.Count))];
    }
}
