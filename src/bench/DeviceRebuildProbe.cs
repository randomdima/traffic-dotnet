using System.Diagnostics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;
using static TrafficSimulation.App.DeviceTick.DeviceRebuild;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Every claims rebuild of a running town chained on the device beside the host's</b> (<see cref="DeviceRebuild"/>):
/// once the town has driven a while, each rebuild of a stretch of it handed to the device stage by stage as the town lays
/// it, recorded into one submit, timed against the town's own rebuild, and held to one answer.
/// </summary>
/// <remarks>
/// <b>What each side's time leaves out</b>: the host's is the rebuild the town ran, less what the watcher did inside it;
/// the device's is its kernels alone, from the first producer to the last grant. Sending the producers' inputs and the
/// host's own units across, writing them on the host and reading every answer back are the shadow's, and quoted apart.
/// </remarks>
internal static class DeviceRebuildProbe
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

        using var vk = Vk.Open("traffic-dotnet", validation: false);
        using var onDevice = new DeviceRebuild(vk, config, world);
        Held rows = new("slots"), holds = new("holds"), answers = new("answers"), grants = new("cars' grants"), walkers = new("walkers' readings");
        var hostMs = new List<double>();
        var deviceMs = new List<double>();
        var bodiesMs = new List<double>();
        var plansMs = new List<double>();
        var answersMs = new List<double>();
        var grantsMs = new List<double>();
        var sentMs = new List<double>();
        var sentMb = new List<double>();
        var linesMb = new List<double>();
        var readMs = new List<double>();
        var writtenMs = new List<double>();
        var heldMs = new List<double>();
        var slots = new List<double>();
        long grown = 0, hostRows = 0, backing = 0, standing = 0;
        var host = new Stopwatch();
        var watcher = new Stopwatch();
        world.WatchingTheClaims = (town, stage) =>
        {
            if (stage == ClaimsStage.Begun)
            {
                host.Restart();
                watcher.Reset();
            }

            if (stage == ClaimsStage.GrantsRead)
            {
                hostMs.Add(host.Elapsed.TotalMilliseconds - watcher.Elapsed.TotalMilliseconds);
                writtenMs.Add(watcher.Elapsed.TotalMilliseconds);
                onDevice.Watch(town, stage);
                deviceMs.Add(onDevice.BetweenMs(Stage.Sent, Stage.Granted));
                bodiesMs.Add(onDevice.BetweenMs(Stage.Sent, Stage.BodiesLaid));
                plansMs.Add(onDevice.BetweenMs(Stage.BodiesLaid, Stage.PlansLaid));
                answersMs.Add(onDevice.BetweenMs(Stage.PlansLaid, Stage.Answered));
                grantsMs.Add(onDevice.BetweenMs(Stage.Answered, Stage.Granted));
                sentMs.Add(onDevice.BetweenMs(Stage.Began, Stage.Sent));
                sentMb.Add(onDevice.SentBytes.All / 1e6);
                linesMb.Add(onDevice.SentBytes.Lines / 1e6);
                readMs.Add(onDevice.BetweenMs(Stage.Granted, Stage.Read));
                watcher.Restart();
                onDevice.HoldToTheTown(town, rows, holds, answers, grants, walkers);
                slots.Add(town.Occupancy.SlotCount);
                grown += onDevice.Grown;
                hostRows += onDevice.HostRowsSent;
                backing += onDevice.BackingAsked;
                standing += onDevice.StandingHeld;
                heldMs.Add(watcher.Elapsed.TotalMilliseconds);
                return;
            }

            watcher.Start();
            onDevice.Watch(town, stage);
            watcher.Stop();
        };

        var until = loop.Tick + MeasuredTicks;
        while (loop.Tick < until) loop.Advance();
        world.WatchingTheClaims = null;

        Console.WriteLine($"device rebuild probe — {map} on {vk.DeviceName}, {world.Cars.Count} cars and {world.People.Count} walkers after {WarmupTicks} ticks");
        Console.WriteLine($"{hostMs.Count} rebuilds, median {Median(slots):F0} slots; medians a rebuild, one submit each:");
        Console.WriteLine($"  host, the town's whole rebuild         {Median(hostMs),8:F3} ms   most {Max(hostMs),8:F3} ms");
        Console.WriteLine($"  device, chained kernels only          {Median(deviceMs),8:F3} ms   most {Max(deviceMs),8:F3} ms");
        Console.WriteLine($"    of which bodies laid and numbered     {Median(bodiesMs),8:F3} ms");
        Console.WriteLine($"    lights, plans, walks numbered, listed {Median(plansMs),8:F3} ms");
        Console.WriteLine($"    answered and cut                      {Median(answersMs),8:F3} ms");
        Console.WriteLine($"    backing, grants and walkers read      {Median(grantsMs),8:F3} ms");
        Console.WriteLine($"  shadow only: inputs and host's units sent {Median(sentMs),8:F3} ms ({Median(sentMb):F1} MB, the cars' lines {Median(linesMb):F1} MB), read back {Median(readMs),8:F3} ms");
        Console.WriteLine($"  shadow only: written on the host stage by stage {Median(writtenMs),8:F3} ms, held to the town {Median(heldMs),8:F3} ms");
        Console.WriteLine($"  {grown} bodies grown into another, {hostRows} rows the host laid, {backing} cars' backing laid, {standing} holds laid where they stand");
        foreach (var held in (ReadOnlySpan<Held>)[rows, holds, answers, grants, walkers]) Console.WriteLine($"  {held.Said}");
    }

    static double Median(List<double> values)
    {
        if (values.Count == 0) return double.NaN;

        var sorted = values.ToArray();
        Array.Sort(sorted);
        return sorted[sorted.Length / 2];
    }

    static double Max(List<double> values) => values.Count == 0 ? double.NaN : values.Max();
}
