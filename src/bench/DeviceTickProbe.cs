using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The cars' side of a tick chained on the device, beside the town's, on a running town</b> (<see cref="DeviceTick"/>):
/// once the town has driven a while, every tick the device ticks the cars it takes, runs their tyres, spends the impulses
/// its own tyres left and steps the bodies, in one submit — timed, and where it left each car held to where the town's
/// step left it.
/// </summary>
/// <remarks>
/// <b>Every pose is quoted, and nothing fails on it</b>: the device's wheels are its own, a step solved in colours lands
/// a little apart from one solved in contact order, and each half is held to the town on its own by the probes before
/// this (<c>--bench devicecartick</c>, <c>--bench devicebodies</c>).
/// </remarks>
internal static class DeviceTickProbe
{
    const int WarmupTicks = 600;
    const int MeasuredTicks = 300;

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        var cars = world.Cars;
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        using var rebuild = new DeviceRebuild(vk, config, world);
        var builds = new CarBuild[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = cars.BuildOfVariant(variant);

        using var tyres = new DeviceTyres(vk, config, world.WheelGrounds, builds, cars.Capacity);
        using var carTick = new DeviceCarTick(
            vk, config, tyres, world.Roads, cars.LineArcsOf(0).Length, world.Ways.OfRoadConnector(0), rebuild.Claims,
            (rebuild.Grants, rebuild.Cutters, rebuild.Told));
        using var solver = new DeviceSolver(vk, config, world.PhysicsForInstruments.Table);
        using var bodies = new DeviceBodyPhase(vk, config, solver, tyres, world.People.Capacity + (2 * cars.Capacity));
        using var walkerTick = new DeviceWalkerTick(vk, config, world.People.Capacity);
        using var tick = new DeviceTick(vk, carTick, bodies, walkerTick, rebuild);

        // The device's claims are the ones its tick reads, so nothing is asked of it until a rebuild of its own is laid.
        var rebuilt = false;
        world.WatchingTheClaims = (town, stage) =>
        {
            rebuild.Watch(town, stage);
            if (stage == ClaimsStage.GrantsRead) rebuilt = true;
        };

        var taken = new int[cars.Capacity];
        var count = 0;
        var prepared = false;
        var stepped = false;
        var kernelsMs = new List<double>();
        var carMs = new List<double>();
        var rebuildMs = new List<double>();
        var bodiesMs = new List<double>();
        var hostMs = new List<double>();
        var apartM = new List<double>();
        var walkersApartM = new List<double>();
        var walkerMs = new List<double>();
        var walkers = 0;
        var walkersAsked = 0;
        var walkersDiffer = 0;
        string? firstWalker = null;
        var asked = 0;
        var host = new Stopwatch();
        loop.WatchingTheAgents = (town, stage) =>
        {
            if (!rebuilt) return;

            if (stage == AgentsStage.Ticked)
            {
                carTick.SettleTheTown(town);
                return;
            }

            if (stepped)
            {
                var poses = bodies.PosesAfter;
                foreach (var car in taken.AsSpan(0, count))
                {
                    asked++;
                    apartM.Add(Vector2.Distance(poses[car].PositionM, town.Cars.PositionM[car]));
                }

                var walked = walkerTick.WalkersAfter;
                for (var person = 0; person < walkers; person++)
                {
                    if (town.People.Inside[person].Any) continue;

                    walkersApartM.Add(Vector2.Distance(walked[person].PositionM, town.People.PositionM[person]));
                }

                hostMs.Add(host.Elapsed.TotalMilliseconds);
                stepped = false;
            }

            count = carTick.Prepare(town, taken, resident: true);
            walkers = walkerTick.Lay(town);
            prepared = true;
            host.Restart();
        };
        world.BeforeTheImpulses = town =>
        {
            if (!prepared) return;

            host.Stop();
            bodies.Lay(town, config.TickSeconds, everyWheel: false);
            tick.Step(count, town.Cars.Count, walkers, config.TickSeconds, readBack: true);
            carTick.SettleTheDevice(town.Cars, taken.AsSpan(0, count));
            kernelsMs.Add(tick.KernelsMs);
            if (tick.Rebuilt) rebuildMs.Add(tick.RebuildMs);
            carMs.Add(carTick.TickMs + carTick.TyresMs);
            walkerMs.Add(walkerTick.TickMs);
            bodiesMs.Add(bodies.KernelsMs);
            for (var person = 0; person < walkers; person++)
            {
                walkersAsked++;
                var why = walkerTick.Disagreement(town, person);
                if (why == null) continue;

                walkersDiffer++;
                firstWalker ??= $"tick {loop.Tick}: {why}";
            }
            prepared = false;
            stepped = true;
            host.Start();
        };

        var until = loop.Tick + MeasuredTicks;
        while (loop.Tick < until) loop.Advance();
        loop.WatchingTheAgents = null;
        world.WatchingTheClaims = null;
        world.BeforeTheImpulses = null;

        apartM.Sort();
        walkersApartM.Sort();
        Console.WriteLine($"device tick probe — {map} on {vk.DeviceName}, {cars.Count} cars and {world.People.Count} walkers after {WarmupTicks} ticks");
        Console.WriteLine($"{kernelsMs.Count} ticks, the agents' side chained in one submit:");
        Console.WriteLine($"  host, the agents' ticks and decisions and the bodies' phase  median {Median(hostMs),8:F3} ms");
        Console.WriteLine($"  device, every kernel     median {Median(kernelsMs),8:F3} ms   most {kernelsMs.Max(),8:F3} ms");
        Console.WriteLine($"    the claims rebuild     median {Median(rebuildMs),8:F3} ms   on {rebuildMs.Count} of the ticks");
        Console.WriteLine($"    the car tick and tyres median {Median(carMs),8:F3} ms");
        Console.WriteLine($"    the walkers' tick      median {Median(walkerMs),8:F3} ms");
        Console.WriteLine($"    the bodies' phase      median {Median(bodiesMs),8:F3} ms");
        Console.WriteLine(
            $"  where the device left {asked} car-ticks from where the town's step did: median {Percentile(apartM, 0.5) * 1000d:F3} mm, " +
            $"99th {Percentile(apartM, 0.99) * 1000d:F3} mm, most {(apartM.Count > 0 ? apartM[^1] : 0d) * 1000d:F3} mm");
        Console.WriteLine(
            $"  and {walkersApartM.Count} walker-ticks: median {Percentile(walkersApartM, 0.5) * 1000d:F3} mm, " +
            $"99th {Percentile(walkersApartM, 0.99) * 1000d:F3} mm, most {(walkersApartM.Count > 0 ? walkersApartM[^1] : 0d) * 1000d:F3} mm");
        Console.WriteLine($"  {walkersDiffer} of {walkersAsked} walker-ticks ticked differently; {walkerTick.Boarded} boarded or stepped off between the tick and the step");
        if (firstWalker != null) Console.WriteLine($"    the first: {firstWalker}");
    }

    static double Median(List<double> values)
    {
        if (values.Count == 0) return double.NaN;

        values.Sort();
        return values[values.Count / 2];
    }

    static double Percentile(List<double> sorted, double share) =>
        sorted.Count == 0 ? double.NaN : sorted[Math.Min(sorted.Count - 1, (int)(share * sorted.Count))];
}
