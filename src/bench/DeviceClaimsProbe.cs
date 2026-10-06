using System.Diagnostics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The plans answered on the device beside the host, on a running town</b>: once the town has driven a while, every
/// claims rebuild of a stretch of it handed to both as the town laid it — the host answering and cutting the plans as it
/// does every rebuild, the device doing the same — timed, and held to one answer.
/// </summary>
internal static class DeviceClaimsProbe
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
        var claims = world.Occupancy;
        using var onDevice = new DeviceClaims(vk, claims.WayCount, claims.Capacity, claims.HoldCapacity, config.Road.MostSettlingPasses);

        using var plans = new DevicePlans(vk, config, world);
        var planMs = new List<double>();
        var plansDiffer = 0;
        var carsPlanned = 0;
        world.BeforeThePlansAreLaid = town =>
        {
            plans.Lay(town);
            plans.Plan(town.Cars.Count);
            planMs.Add(plans.LaidMs);
        };

        var hostMs = new List<double>();
        var deviceMs = new List<double>();
        var layMs = new List<double>();
        var listMs = new List<double>();
        var differ = 0;
        var answered = 0;
        var host = new Stopwatch();
        world.BeforeThePlansAreAnswered = town =>
        {
            var results = plans.Results;
            for (var car = 0; car < town.Cars.Count; car++)
            {
                if (town.Cars.Pass[car].Begun || !town.Occupancy.IsAnswered(town.PlanHoldOf(car))) continue;

                carsPlanned++;
                var (first, end) = town.Occupancy.PiecesOf(town.PlanHoldOf(car));
                if (results[car].ClaimToM != town.Cars.ClaimToM[car] || results[car].Pieces != end - first) plansDiffer++;
            }

            var laying = Stopwatch.StartNew();
            onDevice.Lay(town.Occupancy);
            layMs.Add(laying.Elapsed.TotalMilliseconds);
            onDevice.Answer();
            deviceMs.Add(onDevice.BetweenMs(DeviceClaims.Stage.Began, DeviceClaims.Stage.Cut));
            listMs.Add(onDevice.BetweenMs(DeviceClaims.Stage.Began, DeviceClaims.Stage.Listed));
            host.Restart();
        };
        world.AfterThePlansAreAnswered = town =>
        {
            hostMs.Add(host.Elapsed.TotalMilliseconds);
            var occupancy = town.Occupancy;
            var answers = onDevice.Answers;
            for (var hold = 0; hold < occupancy.HoldCount; hold++)
            {
                if (!occupancy.IsAnswered(hold)) continue;

                answered++;
                var endsAtM = occupancy.HoldEndsAtM(hold, out var marginM, out _);
                if (endsAtM != answers[hold].CutLineM || marginM != answers[hold].MarginM) differ++;
            }
        };

        var until = loop.Tick + MeasuredTicks;
        while (loop.Tick < until) loop.Advance();
        world.BeforeThePlansAreLaid = null;
        world.BeforeThePlansAreAnswered = null;
        world.AfterThePlansAreAnswered = null;

        Console.WriteLine($"device claims probe — {map} on {vk.DeviceName}, {world.Cars.Count} cars and {world.People.Count} walkers after {WarmupTicks} ticks");
        Console.WriteLine($"{hostMs.Count} rebuilds, {answered / Math.Max(1, hostMs.Count)} plans each, {onDevice.Passes} passes on the device:");
        Console.WriteLine($"  host answers and cuts   median {Median(hostMs),8:F3} ms   most {hostMs.Max(),8:F3} ms");
        Console.WriteLine($"  device lists, answers and cuts median {Median(deviceMs),8:F3} ms   most {deviceMs.Max(),8:F3} ms");
        Console.WriteLine($"  of which laying the lists  median {Median(listMs),8:F3} ms");
        Console.WriteLine($"  writing the rows to send (shadow only) median {Median(layMs),8:F3} ms");
        Console.WriteLine($"  {differ} of {answered} plans answered differently");
        Console.WriteLine($"  cars' plans laid on the device median {Median(planMs),8:F3} ms; {plansDiffer} of {carsPlanned} reached or laid differently");
    }

    static double Median(List<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? 0d : sorted[sorted.Length / 2];
    }
}
