using System.Diagnostics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Network = TrafficSimulation.App.DeviceTick.DeviceRoutes.Network;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The town's route searches on the device beside the town's own</b> (<see cref="DeviceRoutes"/>): seeded requests over
/// both networks of a shipped city, made as the town makes them (<see cref="RouteAsks"/>), a batch of slots at a time —
/// every answer held to <see cref="World.Routing.RouteSearch"/>'s, and the batch timed on the device and on the host.
/// </summary>
/// <remarks>
/// <b>A batch takes as long as its slowest search</b>: one thread a slot, so the device's figure is the worst of its slots,
/// which is why the slowest search's settled links are printed beside it.
/// </remarks>
internal static class DeviceRoutesProbe
{
    /// <summary>How many batches of slots each network is asked.</summary>
    const int Batches = 32;

    /// <summary>Answers kept to print where the device parted from the town.</summary>
    const int DiffersShown = 6;

    /// <summary>Whether every answer was the town's.</summary>
    public static bool Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        using var root = new TownRoot(vk);
        using var routes = DeviceRoutes.Of(vk, root, world);
        using var batch = new ComputeBatch(vk);
        using var timer = new GpuTimer(vk, 2);
        var asks = new RouteAsks(world, config);
        var marks = asks.Unmarked();
        routes.LaySurcharges(Network.Driving, marks.Drive);
        routes.LaySurcharges(Network.Walking, marks.Walk);

        Console.WriteLine($"device routes probe — {map} on {vk.DeviceName}, {Batches} batches of {DeviceRoutes.Slots} slots a network");
        Console.WriteLine(
            $"driving {world.Driving.Graph.LinkCount} links in {world.Driving.Cells.CellCount} cells, walking {world.Walking.Runs.Graph.LinkCount} in " +
            $"{world.Walking.Cells.CellCount}; the slots' scratch {routes.ScratchBytes / (1024d * 1024d):F1} MiB");

        var kept = true;
        foreach (var network in (ReadOnlySpan<Network>)[Network.Driving, Network.Walking])
        {
            kept &= Measure(network, asks.Make(network, Batches * DeviceRoutes.Slots, seed: (int)network + 1), asks, marks, routes, batch, timer);
        }

        return kept;
    }

    static bool Measure(
        Network network, List<RouteAsks.Ask> asked, RouteAsks asks, RouteAsks.Marks marks, DeviceRoutes routes, ComputeBatch batch,
        GpuTimer timer)
    {
        var deviceMs = new double[Batches];
        var hostMs = new double[Batches];
        var differ = new List<string>();
        var (settled, worstSettled, worstAt, stopsShort, whole, none) = (0L, 0, 0, 0, 0, 0);
        for (var at = 0; at < Batches; at++)
        {
            var first = at * DeviceRoutes.Slots;
            for (var slot = 0; slot < DeviceRoutes.Slots; slot++)
            {
                var ask = asked[first + slot];
                routes.Ask(slot, network, ask.Entries, ask.Goals, ask.Closed, marks.Of(network).NowS, first + slot, (uint)(first + slot));
            }

            var commands = batch.Begin();
            timer.Reset(commands);
            timer.Stamp(commands, 0);
            routes.Record(commands);
            timer.Stamp(commands, 1);
            batch.SubmitAndWait();
            timer.Read();
            deviceMs[at] = timer.BetweenMs(0, 1);

            var took = Stopwatch.StartNew();
            for (var slot = 0; slot < DeviceRoutes.Slots; slot++) asks.Searched(asked[first + slot], marks, out _, out _);
            hostMs[at] = took.Elapsed.TotalMilliseconds;

            for (var slot = 0; slot < DeviceRoutes.Slots; slot++)
            {
                var answer = routes.Answer(slot);
                settled += answer.SettledLinks;
                if (answer.SettledLinks > worstSettled) (worstSettled, worstAt) = ((int)answer.SettledLinks, at);
                if (answer.Count == 0) none++;
                else if (answer.StopsShort != 0u) stopsShort++;
                else if (answer.OverTheWholeGraph != 0u) whole++;

                var differs = asks.Differs(asked[first + slot], marks, routes, slot, first + slot, (uint)(first + slot));
                if (differs is not null) differ.Add(differs);
            }
        }

        var count = asked.Count;
        Array.Sort(hostMs);
        var sorted = (double[])deviceMs.Clone();
        Array.Sort(sorted);
        Console.WriteLine($"{network}: {count} asked, {differ.Count} answered unlike the town's; {stopsShort} stop short, {whole} over the whole graph, {none} with no route");
        Console.WriteLine(
            $"  device  a batch {sorted[0],8:F3} ms least, {sorted[Batches / 2],8:F3} median, {sorted[^1],8:F3} most   " +
            $"(the slowest search settled {worstSettled} links, in a batch of {deviceMs[worstAt]:F3} ms)");
        Console.WriteLine($"  host    a batch {hostMs[0],8:F3} ms least, {hostMs[Batches / 2],8:F3} median, {hostMs[^1],8:F3} most   (one thread, one search after another)");
        Console.WriteLine($"  settled {settled / (double)count:F0} links a search on the mean");
        foreach (var line in differ.Take(DiffersShown)) Console.WriteLine($"  unlike: {line}");

        return differ.Count == 0;
    }
}
