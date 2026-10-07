using System.Diagnostics;
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
/// <b>A following car's whole tick on the device beside the town's, on a running town</b> (<see cref="DeviceCarTick"/>):
/// once the town has driven a while, every tick hands each car the device takes over as the town's tick will read it, over
/// the claims the device itself answered and cut on the last rebuild — timed, and held to one answer.
/// </summary>
internal static class DeviceCarTickProbe
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
        var claims = world.Occupancy;
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        using var answered = new DeviceClaims(
            vk, claims.WayCount, claims.Capacity, claims.HoldCapacity, config.Road.MostSettlingPasses, cars.Capacity);
        var shapes = new GroundShapes(plan.Paving(config), config);
        using var ground = new DeviceGround(vk, shapes.Table);
        var builds = new CarBuild[CarCatalog.Shared.SheetCount];
        for (var variant = 0; variant < builds.Length; variant++) builds[variant] = cars.BuildOfVariant(variant);

        using var tyres = new DeviceTyres(vk, config, ground, world.Terrain.EffectTable, builds, cars.Capacity);
        using var tick = new DeviceCarTick(
            vk, config, tyres, ground, world.Roads, cars.LineArcsOf(0).Length, world.Ways.OfRoadConnector(0), answered.Cut);

        // The device's claims are the ones the tick reads, so nothing is asked of it until it has answered a rebuild.
        var rebuilt = false;
        world.WatchingTheClaims = (town, stage) =>
        {
            if (stage != ClaimsStage.PlansLaid) return;

            answered.Lay(town.Occupancy);
            answered.Answer();
            rebuilt = true;
        };

        var taken = new int[cars.Capacity];
        var count = 0;
        var tickMs = new List<double>();
        var tyresMs = new List<double>();
        var copiesMs = new List<double>();
        var layMs = new List<double>();
        var hostMs = new List<double>();
        var takenCounts = new List<int>();
        var differ = 0;
        var asked = 0;
        string? first = null;
        var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var examples = new Dictionary<string, string>(StringComparer.Ordinal);
        var host = new Stopwatch();
        loop.WatchingTheAgents = (town, stage) =>
        {
            if (!rebuilt) return;

            if (stage == AgentsStage.Due)
            {
                var laying = Stopwatch.StartNew();
                count = 0;
                for (var car = 0; car < town.Cars.Count; car++)
                {
                    if (!DeviceCarTick.Takes(town, car)) continue;

                    tick.Lay(count, town, car, town.Cars.Variant[car]);
                    taken[count++] = car;
                }

                layMs.Add(laying.Elapsed.TotalMilliseconds);
                tick.Step(count);
                tickMs.Add(tick.TickMs);
                tyresMs.Add(tick.TyresMs);
                copiesMs.Add(tick.CopiesMs);
                takenCounts.Add(count);
                host.Restart();
                return;
            }

            hostMs.Add(host.Elapsed.TotalMilliseconds);
            for (var slot = 0; slot < count; slot++)
            {
                asked++;
                var why = tick.Disagreement(slot, town, taken[slot]);
                if (why == null) continue;

                differ++;
                first ??= $"tick {loop.Tick} car {taken[slot]}: {why}";
                var kind = why[..why.IndexOf(':')];
                kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
                examples.TryAdd(kind, $"tick {loop.Tick} car {taken[slot]}: {why}");
            }
        };

        var until = loop.Tick + MeasuredTicks;
        while (loop.Tick < until) loop.Advance();
        loop.WatchingTheAgents = null;
        world.WatchingTheClaims = null;

        Console.WriteLine($"device car tick probe — {map} on {vk.DeviceName}, {cars.Count} cars and {world.People.Count} walkers after {WarmupTicks} ticks");
        Console.WriteLine($"{tickMs.Count} ticks, median {Median(takenCounts.ConvertAll(taken => (double)taken)):F0} following cars a tick taken by the device:");
        Console.WriteLine($"  host, every agent's tick   median {Median(hostMs),8:F3} ms   most {hostMs.Max(),8:F3} ms");
        Console.WriteLine($"  device ground and drive    median {Median(tickMs),8:F3} ms   most {tickMs.Max(),8:F3} ms");
        Console.WriteLine($"  device tyres               median {Median(tyresMs),8:F3} ms   most {tyresMs.Max(),8:F3} ms");
        Console.WriteLine($"  taking the rows in and handing them back (shadow only) median {Median(copiesMs),8:F3} ms");
        Console.WriteLine($"  writing the rows to send (shadow only) median {Median(layMs),8:F3} ms");
        Console.WriteLine($"  {differ} of {asked} car-ticks differ; {tick.SlideOnsets} wheels at a slide's onset");
        foreach (var (kind, many) in kinds) Console.WriteLine($"    {kind}, {many}: the first {examples[kind]}");
    }

    static double Median(List<double> values)
    {
        if (values.Count == 0) return double.NaN;

        values.Sort();
        return values[values.Count / 2];
    }
}
