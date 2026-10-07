using System.Diagnostics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>A town ticked with the device in step against the same town ticked by the host alone</b> (<see cref="DeviceTown"/>):
/// two of one map warmed alike, then each run for the same ticks with the phases timed — what each phase cost a tick, and
/// what the town did meanwhile: how fast its cars went, how many lost their lines, braked hard or began touching.
/// </summary>
/// <remarks>
/// <b>The two towns part after the first tick the device takes</b>, by a float's noise that the town's own chaos grows,
/// so what is held side by side is how each town behaved, never where each car is.
/// </remarks>
internal static class DeviceTownProbe
{
    const int WarmupTicks = 600;
    const int MeasuredTicks = 600;

    readonly record struct Window(
        double TickMs, double ClaimsMs, double AgentsMs, double DecisionsMs, double BodiesMs, double ContactsMs, double WallMs,
        double DeviceKernelsMs, int CarsTaken, double MeanMps, int Rejoining, long HardBrakings, long Touches);

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        var host = Measure(plan, config, null);
        var device = Measure(plan, config, vk);

        Console.WriteLine($"device town probe — {map} on {vk.DeviceName}, {WarmupTicks} ticks warmed, {MeasuredTicks} measured; ms a tick");
        Console.WriteLine($"{"",-8}{"tick",8}{"wall",8}{"claims",8}{"agents",8}{"decide",8}{"bodies",8}{"contacts",10}{"device",8}");
        foreach (var (name, run) in (ReadOnlySpan<(string, Window)>)[("host", host), ("device", device)])
        {
            Console.WriteLine(
                $"{name,-8}{run.TickMs,8:F2}{run.WallMs,8:F2}{run.ClaimsMs,8:F2}{run.AgentsMs,8:F2}{run.DecisionsMs,8:F2}{run.BodiesMs,8:F2}" +
                $"{run.ContactsMs,10:F2}{run.DeviceKernelsMs,8:F2}");
        }

        Console.WriteLine($"what each town did over the {MeasuredTicks} ticks:");
        foreach (var (name, run) in (ReadOnlySpan<(string, Window)>)[("host", host), ("device", device)])
        {
            Console.WriteLine(
                $"  {name,-8} cars driven at {run.MeanMps:F2} m/s on average, {run.Rejoining} off their lines, {run.HardBrakings} hard brakings, " +
                $"{run.Touches} pairs began touching{(run.CarsTaken > 0 ? $"; {run.CarsTaken} cars a tick on the device" : "")}");
        }
    }

    static Window Measure(CityPlan plan, SimConfig config, Vk? vk)
    {
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        using var device = vk is null ? null : new DeviceTown(vk, config, world);
        world.Device = device;
        var brakings = world.HardBrakings;
        var touches = world.Touches;
        loop.Phases.Reset();
        world.Sub.Reset();
        loop.Timed = world.Timed = true;
        var deviceMs = 0d;
        var prepareMs = 0d;
        var waitMs = 0d;
        var takeBackMs = 0d;
        var submitMs = 0d;
        var copiesMs = 0d;
        var claimsMs = 0d;
        var sentBytes = 0d;
        var bodiesLayMs = 0d;
        var bodiesWaitMs = 0d;
        var bodiesBackMs = 0d;
        var bodiesKernelsMs = 0d;
        var taken = 0L;
        var rebuilds = 0;
        var (claimsLaidMs, claimsWaitMs, claimsMirrorMs, claimsSentMs, claimsKernelsMs, claimsReadMs) = (0d, 0d, 0d, 0d, 0d, 0d);
        var claimsStationedMs = 0d;
        var (claimsSentMb, claimsReadMb) = (0d, 0d);
        var (laidRows, laidBodies, laidHolds) = (0L, 0L, 0L);
        var rebuilt = device?.Claims.Rebuild.Rebuilds ?? 0L;
        var (handedInTheStep, handedAlone) = device?.HandedOver ?? default;
        var handOverSubmits = device?.HandOverSubmits ?? 0L;
        var started = Stopwatch.GetTimestamp();
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance();
            if (device is null) continue;

            var rebuild = device.Claims.Rebuild;
            if (rebuild.Rebuilds != rebuilt)
            {
                rebuilt = rebuild.Rebuilds;
                rebuilds++;
                claimsLaidMs += rebuild.LaidMs;
                claimsStationedMs += rebuild.StationedMs;
                claimsWaitMs += rebuild.WaitedMs;
                claimsMirrorMs += rebuild.MirroredMs;
                claimsSentMs += rebuild.BetweenMs(DeviceRebuild.Stage.Began, DeviceRebuild.Stage.Sent);
                claimsKernelsMs += rebuild.BetweenMs(DeviceRebuild.Stage.Sent, DeviceRebuild.Stage.Answered);
                claimsReadMs += rebuild.BetweenMs(DeviceRebuild.Stage.Answered, DeviceRebuild.Stage.Read);
                claimsSentMb += rebuild.Crossed.Sent / 1e6;
                claimsReadMb += rebuild.Crossed.Read / 1e6;
                var (rows, bodies, holds) = rebuild.Counted;
                (laidRows, laidBodies, laidHolds) = (laidRows + rows, laidBodies + bodies, laidHolds + holds);
            }

            deviceMs += device.KernelsMs;
            taken += device.CarsTaken;
            prepareMs += device.PrepareMs;
            waitMs += device.WaitMs;
            takeBackMs += device.TakeBackMs;
            submitMs += device.SubmitMs;
            copiesMs += device.CopiesMs;
            claimsMs += device.ClaimsMs;
            sentBytes += device.SentBytes;
            bodiesLayMs += device.Bodies.LayMs;
            bodiesWaitMs += device.Bodies.WaitMs;
            bodiesBackMs += device.Bodies.HandBackMs;
            bodiesKernelsMs += device.Bodies.Phase.KernelsMs;
        }

        if (device != null)
        {
            Console.WriteLine(
                $"  the device's round trip, ms a tick: writing what is sent {prepareMs / MeasuredTicks:F2}, submitting and waiting " +
                $"{waitMs / MeasuredTicks:F2}, writing what came back {takeBackMs / MeasuredTicks:F2}");
            Console.WriteLine(
                $"  of the wait, the device's own: the submit {submitMs / MeasuredTicks:F2}, of it the town's claims taken in " +
                $"{claimsMs / MeasuredTicks:F2} and the car rows in and out {copiesMs / MeasuredTicks:F2}; " +
                $"{sentBytes / MeasuredTicks / 1024:F0} KiB of car rows sent a tick");
            Console.WriteLine(
                $"  the bodies' phase, ms a tick: laying {bodiesLayMs / MeasuredTicks:F2}, submitting and waiting {bodiesWaitMs / MeasuredTicks:F2} " +
                $"(the device's kernels {bodiesKernelsMs / MeasuredTicks:F2}), writing what came back {bodiesBackMs / MeasuredTicks:F2}");
            Console.WriteLine(
                $"  wheels handed back to the town as their cars left the device: {device.HandedOver.InTheStep - handedInTheStep} in the bodies' " +
                $"submit, {device.HandedOver.Alone - handedAlone} in {device.HandOverSubmits - handOverSubmits} submits of their own");
            var (here, byTheTown, onlyTheTown, tooMany) = device.Claims.Rebuilds;
            var each = Math.Max(1, rebuilds);
            Console.WriteLine(
                $"  the claims, ms a rebuild laid on the device ({rebuilds} measured; {here} laid there and {byTheTown} by the town since it was " +
                $"set, {onlyTheTown} of them for what only the town answers and {tooMany} for too many cars on a pass): the town laying its own " +
                $"and gathering {claimsLaidMs / each:F2} (stationing its walkers {claimsStationedMs / each:F2} of it), submitting and waiting " +
                $"{claimsWaitMs / each:F2}, writing back {claimsMirrorMs / each:F2}");
            Console.WriteLine(
                $"  of the wait, the device's own: sending {claimsSentMs / each:F2} ({claimsSentMb / each:F2} MB), the kernels through the answers " +
                $"{claimsKernelsMs / each:F2}, reading back {claimsReadMs / each:F2} ({claimsReadMb / each:F2} MB) — {laidRows / each} rows, " +
                $"{laidBodies / each} of them bodies and passes, and {laidHolds / each} holds a rebuild; {device.Claims.Rebuild.ReadPast} " +
                $"laid past what their submit read back and {device.Claims.Rebuild.OnOtherChannelsAtABay} cars at a bay put on other " +
                "channels since the device was set");
        }

        var wallMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds / MeasuredTicks;
        loop.Timed = world.Timed = false;
        world.Device = null;

        var phases = loop.Phases;
        var sub = world.Sub;
        var cars = world.Cars;
        var driven = 0;
        var mps = 0d;
        var rejoining = 0;
        for (var car = 0; car < cars.Count; car++)
        {
            if (!cars.Driven[car]) continue;

            driven++;
            mps += cars.VelocityMps[car].Length();
            if (cars.Action[car] == CarAction.Rejoin) rejoining++;
        }

        return new Window(
            phases.MillisecondsPer(phases.WholeTicks), phases.MillisecondsPer(phases.IndexTicks),
            phases.MillisecondsPer(sub.WalkerTicks + sub.CarTicks), phases.MillisecondsPer(sub.WalkerDecisionTicks + sub.CarDecisionTicks),
            phases.MillisecondsPer(phases.BodyTicks), phases.MillisecondsPer(phases.ContactTicks), wallMs, deviceMs / MeasuredTicks,
            (int)(taken / MeasuredTicks), mps / Math.Max(1, driven), rejoining, world.HardBrakings - brakings, world.Touches - touches);
    }
}
