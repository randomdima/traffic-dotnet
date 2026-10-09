using System.Diagnostics;
using System.Runtime.CompilerServices;
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
/// four of one map warmed alike — the host's, the device's with the bodies stepped in the agents' submit, the device's
/// with them stepped after the decisions (<see cref="DeviceTown.StepsWithTheTick"/>), and the first watched every tick
/// as a window watches it (<see cref="TownWatch"/>) — then each run for the same ticks with the phases timed: what each
/// phase and the watch cost a tick, and what the town did meanwhile — how fast its cars went, how many lost their lines,
/// braked hard or began touching.
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
        double DeviceKernelsMs, double WatchMs, int CarsTaken, double MeanMps, int Rejoining, long HardBrakings, long Touches,
        (long Asked, long Made, long Withdrawn) Switches, (long Asked, long Made, long Withdrawn) Passes,
        (long Asked, long Made, long Withdrawn) Sidesteps);

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        var host = Measure(plan, config, null);
        Console.WriteLine("the device, the bodies stepped after the decisions:");
        var after = Measure(plan, config, vk, stepsWithTheTick: false);
        Console.WriteLine("the device, the bodies stepped with the agents' tick:");
        var device = Measure(plan, config, vk);
        Console.WriteLine("the device, the bodies stepped with the agents' tick, watched:");
        var watched = Measure(plan, config, vk, watched: true);

        ReadOnlySpan<(string, Window)> runs = [("host", host), ("device", device), ("after", after), ("watched", watched)];
        Console.WriteLine($"device town probe — {map} on {vk.DeviceName}, {WarmupTicks} ticks warmed, {MeasuredTicks} measured; ms a tick");
        Console.WriteLine($"{"",-8}{"tick",8}{"wall",8}{"claims",8}{"agents",8}{"decide",8}{"bodies",8}{"contacts",10}{"device",8}{"watch",8}");
        foreach (var (name, run) in runs)
        {
            Console.WriteLine(
                $"{name,-8}{run.TickMs,8:F2}{run.WallMs,8:F2}{run.ClaimsMs,8:F2}{run.AgentsMs,8:F2}{run.DecisionsMs,8:F2}{run.BodiesMs,8:F2}" +
                $"{run.ContactsMs,10:F2}{run.DeviceKernelsMs,8:F2}{run.WatchMs,8:F2}");
        }

        Console.WriteLine($"what each town did over the {MeasuredTicks} ticks:");
        foreach (var (name, run) in runs)
        {
            Console.WriteLine(
                $"  {name,-8} cars driven at {run.MeanMps:F2} m/s on average, {run.Rejoining} off their lines, {run.HardBrakings} hard brakings, " +
                $"{run.Touches} pairs began touching{(run.CarsTaken > 0 ? $"; {run.CarsTaken} cars a tick on the device" : "")}; " +
                $"lane switches asked/made/withdrawn {run.Switches.Asked}/{run.Switches.Made}/{run.Switches.Withdrawn}, passes " +
                $"{run.Passes.Asked}/{run.Passes.Made}/{run.Passes.Withdrawn}, walkers' sidesteps {run.Sidesteps.Asked}/" +
                $"{run.Sidesteps.Made}/{run.Sidesteps.Withdrawn}");
        }
    }

    /// <param name="watched">Whether the town is watched every tick as a window watches it, the watch's time apart from the tick's.</param>
    static Window Measure(CityPlan plan, SimConfig config, Vk? vk, bool stepsWithTheTick = true, bool watched = false)
    {
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        if (vk is not null)
        {
            var claims = world.Occupancy;
            Console.WriteLine(
                $"  room laid: {world.Cars.Capacity} cars, {world.People.Capacity} people, claims {claims.Capacity} slots " +
                $"({claims.Capacity * (long)Unsafe.SizeOf<DeviceClaims.ClaimRow>() / (1 << 20)} MiB a copy) and {claims.HoldCapacity} holds");
        }

        var watch = watched ? new TownWatch(world, config) : null;
        using var device = vk is null ? null : new DeviceTown(vk, config, world) { StepsWithTheTick = stepsWithTheTick };
        world.Device = device;
        var watchMs = 0d;
        var brakings = world.HardBrakings;
        var touches = world.Touches;
        var (switching, overtaking, sidestepping) = (world.Switching, world.Overtaking, world.Sidestepping);
        var switches = (switching.Asked, switching.Made, switching.Withdrawn);
        var passes = (overtaking.Asked, overtaking.Made, overtaking.Withdrawn);
        var sidesteps = (sidestepping.Asked, sidestepping.Made, sidestepping.Withdrawn);
        var (keptPatched, keptChanged, keptHanded, keptLeft) = (0L, 0L, 0L, 0L);
        loop.Phases.Reset();
        world.Sub.Reset();
        loop.Timed = world.Timed = true;
        var deviceMs = 0d;
        var prepareMs = 0d;
        var waitMs = 0d;
        var (decidedMs, idleMs, stepIdleMs) = (0d, 0d, 0d);
        var movedOnFrom = device?.CarsTheTownMovedOn ?? 0L;
        var takeBackMs = 0d;
        var submitMs = 0d;
        var copiesMs = 0d;
        var claimsMs = 0d;
        var sentBytes = 0d;
        var walkersPatched = 0L;
        var (holdsAsked, holdsAnswered) = (0L, 0L);
        var (tickRegions, rebuildRegions) = (0L, 0L);
        var bodiesLayMs = 0d;
        var bodiesWaitMs = 0d;
        var bodiesBackMs = 0d;
        var bodiesKernelsMs = 0d;
        var taken = 0L;
        var rebuilds = 0;
        var (claimsLaidMs, claimsWaitMs, claimsMirrorMs, claimsSentMs, claimsKernelsMs, claimsReadMs) = (0d, 0d, 0d, 0d, 0d, 0d);
        var claimsStationedMs = 0d;
        var claimsIdleMs = 0d;
        var ranOnFrom = device?.Claims.Rebuild.RanOn ?? 0L;
        var putOffFrom = device?.CarsPutOff ?? 0L;
        var (claimsBodiesMs, claimsPlansMs) = (0d, 0d);
        var (claimsSentMb, claimsReadMb) = (0d, 0d);
        var sentParts = new double[7];
        var readParts = new double[5];
        var (laidRows, laidBodies, laidHolds) = (0L, 0L, 0L);
        var stages = Enum.GetValues<ClaimsStage>();
        var stageTownMs = new double[stages.Length];
        var stageHereMs = new double[stages.Length];
        var (mostHostRows, mostHostHolds) = (0, 0);
        var (landingWaitedMs, landedMs) = (0d, 0d);
        var landedMb = 0d;
        var (waysKnown, waysLightsOnly) = (0L, 0L);
        var sentBack = device?.Claims.Rebuild.LayersSentBack ?? 0L;
        var rebuilt = device?.Claims.Rebuild.Rebuilds ?? 0L;
        var (handedInTheStep, handedAlone) = device?.HandedOver ?? default;
        var handOverSubmits = device?.HandOverSubmits ?? 0L;
        var drivenByTheTownFrom = device?.CarsDrivenByTheTown ?? 0L;
        var drivenByTheTown = drivenByTheTownFrom;
        var leftFrom = device?.CarsLeftOnTheDevice ?? 0L;
        var byTheClockFrom = world.DecidedByTheClock;
        var byTheTownFrom = device?.DecidedByTheTown ?? 0L;
        var walkersByTheClockFrom = world.WalkersDecidedByTheClock;
        var walkersByTheTownFrom = device?.WalkersDecidedByTheTown ?? 0L;
        var takenWholeFrom = device?.TakenBackWhole ?? 0L;
        var ticksDrivingAny = 0;
        var started = Stopwatch.GetTimestamp();
        for (var tick = 0; tick < MeasuredTicks; tick++)
        {
            loop.Advance();
            if (watch is not null)
            {
                var sawFrom = Stopwatch.GetTimestamp();
                watch.Saw(world);
                watchMs += Stopwatch.GetElapsedTime(sawFrom).TotalMilliseconds;
            }

            if (device is null) continue;

            var rebuild = device.Claims.Rebuild;
            if (rebuild.Rebuilds != rebuilt)
            {
                rebuilt = rebuild.Rebuilds;
                rebuilds++;
                claimsLaidMs += rebuild.LaidMs;
                claimsStationedMs += rebuild.StationedMs;
                claimsWaitMs += rebuild.WaitedMs;
                claimsIdleMs += rebuild.IdleMs;
                claimsMirrorMs += rebuild.MirroredMs;
                claimsSentMs += rebuild.BetweenMs(DeviceRebuild.Stage.Began, DeviceRebuild.Stage.Sent);
                claimsKernelsMs += rebuild.BetweenMs(DeviceRebuild.Stage.Sent, DeviceRebuild.Stage.Answered);
                claimsBodiesMs += rebuild.BetweenMs(DeviceRebuild.Stage.Sent, DeviceRebuild.Stage.BodiesLaid);
                claimsPlansMs += rebuild.BetweenMs(DeviceRebuild.Stage.BodiesLaid, DeviceRebuild.Stage.PlansLaid);
                claimsReadMs += rebuild.BetweenMs(DeviceRebuild.Stage.Answered, DeviceRebuild.Stage.Read);
                claimsSentMb += rebuild.Crossed.Sent / 1e6;
                rebuildRegions += rebuild.RegionsCopied;
                var parts = rebuild.SentParts;
                ReadOnlySpan<ulong> each = [parts.Bodies, parts.Plans, parts.Walkers, parts.Lines, parts.Held, parts.CarClaims, parts.TownsOwn];
                for (var part = 0; part < each.Length; part++) sentParts[part] += each[part] / 1e6;
                var read = rebuild.ReadParts;
                ReadOnlySpan<ulong> eachRead = [read.Layer, read.UnitHolds, read.Cars, read.Walkers, read.Grants];
                for (var part = 0; part < eachRead.Length; part++) readParts[part] += eachRead[part] / 1e6;
                claimsReadMb += rebuild.Crossed.Read / 1e6;
                var (rows, bodies, holds) = rebuild.Counted;
                (mostHostRows, mostHostHolds) = (Math.Max(mostHostRows, rebuild.HostRowsSent), Math.Max(mostHostHolds, rebuild.HostHoldsSent));

                // The last landing's, which was the rebuild before this one's.
                (landingWaitedMs, landedMs) = (landingWaitedMs + rebuild.LandingWaitedMs, landedMs + rebuild.LandedMs);
                landedMb += rebuild.LandedBytes / 1e6;
                (waysKnown, waysLightsOnly) = (waysKnown + rebuild.WaysLanded.Known, waysLightsOnly + rebuild.WaysLanded.LightsOnly);
                (laidRows, laidBodies, laidHolds) = (laidRows + rows, laidBodies + bodies, laidHolds + holds);
                var kept = rebuild.Walkers;
                (keptPatched, keptChanged, keptHanded, keptLeft) = (
                    keptPatched + kept.KeptPatched, keptChanged + kept.KeptChanged.Changed, keptHanded + kept.KeptChanged.Handed,
                    keptLeft + kept.KeptLeft);
                var (townMs, hereMs) = device.Claims.StageMs;
                for (var stage = 0; stage < stages.Length; stage++)
                {
                    stageTownMs[stage] += townMs[stage];
                    stageHereMs[stage] += hereMs[stage];
                }
            }

            deviceMs += device.KernelsMs;
            taken += device.CarsTaken;
            if (device.CarsDrivenByTheTown != drivenByTheTown) ticksDrivingAny++;
            drivenByTheTown = device.CarsDrivenByTheTown;
            prepareMs += device.PrepareMs;
            waitMs += device.WaitMs;
            decidedMs += device.DecidedMs;
            idleMs += device.IdleMs;
            stepIdleMs += device.StepIdleMs;
            takeBackMs += device.TakeBackMs;
            submitMs += device.SubmitMs;
            copiesMs += device.CopiesMs;
            claimsMs += device.ClaimsMs;
            sentBytes += device.SentBytes;
            walkersPatched += device.WalkersPatched;
            (holdsAsked, holdsAnswered) = (holdsAsked + device.Claims.Rebuild.HoldsAsked.Asked, holdsAnswered + device.Claims.Rebuild.HoldsAsked.Answered);
            tickRegions += device.RegionsCopied;
            bodiesLayMs += device.Bodies.LayMs;
            bodiesWaitMs += device.Bodies.WaitMs;
            bodiesBackMs += device.Bodies.HandBackMs;
            bodiesKernelsMs += device.Bodies.Phase.KernelsMs;
        }

        if (device != null)
        {
            var movedOn = device.CarsTheTownMovedOn - movedOnFrom;
            Console.WriteLine(
                $"  the device's round trip, ms a tick: writing what is sent {prepareMs / MeasuredTicks:F2}, submitting and waiting " +
                $"{waitMs / MeasuredTicks:F2} (of it the decisions put off a tick {decidedMs / MeasuredTicks:F2} and nothing but waiting " +
                $"{idleMs / MeasuredTicks:F2}), writing what came back {takeBackMs / MeasuredTicks:F2} (of it waiting for the bodies' " +
                $"step {stepIdleMs / MeasuredTicks:F2}); {movedOn} cars the town moved on from while their tick ran");
            Console.WriteLine(
                $"  of the wait, the device's own: the submit {submitMs / MeasuredTicks:F2}, of it the town's claims taken in " +
                $"{claimsMs / MeasuredTicks:F2} and the car rows in and out {copiesMs / MeasuredTicks:F2}; " +
                $"{sentBytes / MeasuredTicks / 1024:F0} KiB of car rows sent a tick, {walkersPatched / (double)MeasuredTicks:F0} walkers written " +
                $"whole a tick; {tickRegions / MeasuredTicks} copy regions a tick's submit, {rebuildRegions / Math.Max(1, rebuilds)} a " +
                "rebuild's");
            Console.WriteLine(
                $"  the bodies' phase, ms a tick: laying {bodiesLayMs / MeasuredTicks:F2}, submitting and waiting {bodiesWaitMs / MeasuredTicks:F2} " +
                $"(the device's kernels {bodiesKernelsMs / MeasuredTicks:F2}), writing what came back {bodiesBackMs / MeasuredTicks:F2}");
            Console.WriteLine(
                $"  wheels handed back to the town as their cars left the device: {device.HandedOver.InTheStep - handedInTheStep} in the bodies' " +
                $"submit, {device.HandedOver.Alone - handedAlone} in {device.HandOverSubmits - handOverSubmits} submits of their own; " +
                $"{(drivenByTheTown - drivenByTheTownFrom) / (double)MeasuredTicks:F1} driven cars a tick the town ticked itself, on " +
                $"{ticksDrivingAny} of the {MeasuredTicks} ticks; {(device.CarsLeftOnTheDevice - leftFrom) / (double)MeasuredTicks:F0} " +
                $"cars a tick left on the device unread, every car taken back {device.TakenBackWhole - takenWholeFrom} times; " +
                $"{(world.DecidedByTheClock - byTheClockFrom) / (double)MeasuredTicks:F0} cars' decisions a tick their clock alone, run on " +
                $"the device, and {(device.DecidedByTheTown - byTheTownFrom) / (double)MeasuredTicks:F0} the device asked of handed to the town; " +
                $"walkers' {(world.WalkersDecidedByTheClock - walkersByTheClockFrom) / (double)MeasuredTicks:F0} and " +
                $"{(device.WalkersDecidedByTheTown - walkersByTheTownFrom) / (double)MeasuredTicks:F0}");
            var (here, byTheTown, onlyTheTown, tooMany) = device.Claims.Rebuilds;
            var each = Math.Max(1, rebuilds);
            Console.WriteLine(
                $"  the claims, ms a rebuild laid on the device ({rebuilds} measured; {here} laid there and {byTheTown} by the town since it was " +
                $"set, {onlyTheTown} of them for what only the town answers and {tooMany} for too many cars on a pass): the town laying its own " +
                $"and gathering {claimsLaidMs / each:F2} (stationing its walkers {claimsStationedMs / each:F2} of it), submitting and waiting " +
                $"{claimsWaitMs / each:F2}, writing back {claimsMirrorMs / each:F2}");
            Console.WriteLine(
                $"  of the wait, the device's own: sending {claimsSentMs / each:F2} ({claimsSentMb / each:F2} MB), the kernels through the answers " +
                $"{claimsKernelsMs / each:F2} (the bodies {claimsBodiesMs / each:F2}, the plans {claimsPlansMs / each:F2}, the answers " +
                $"{(claimsKernelsMs - claimsBodiesMs - claimsPlansMs) / each:F2}), reading back {claimsReadMs / each:F2} " +
                $"({claimsReadMb / each:F2} MB) — {laidRows / each} rows, " +
                $"{laidBodies / each} of them bodies and passes, and {laidHolds / each} holds a rebuild; {device.Claims.Rebuild.ReadPast} " +
                $"laid past what their submit read back, {device.Claims.Rebuild.OnOtherChannelsAtABay} cars at a bay put on other " +
                $"channels and {device.Claims.Rebuild.BackedUp} rebuilds with a car backing up since the device was set; the town laid " +
                $"at most {mostHostRows} rows and {mostHostHolds} holds of a rebuild itself");
            Console.WriteLine(
                $"  of what a rebuild sends, MB: the cars' bodies the town laid {sentParts[0] / each:F2}, their plans {sentParts[1] / each:F2}, " +
                $"the walkers {sentParts[2] / each:F2}, the cars' lines {sentParts[3] / each:F2}, the readings and terms of the cars the " +
                $"device lays {sentParts[4] / each:F2}, the cars' claims {sentParts[5] / each:F2}, the town's own {sentParts[6] / each:F2}");
            Console.WriteLine(
                $"  of what a rebuild reads back in its own submit, MB: the layer {readParts[0] / each:F2}, the units' holds " +
                $"{readParts[1] / each:F2}, the cars' plans {readParts[2] / each:F2}, the walkers {readParts[3] / each:F2}, the grants " +
                $"{readParts[4] / each:F2}");
            Console.WriteLine(
                $"  the layer written back a tick later for {device.Claims.Rebuild.LayersSentBack - sentBack} of them, inside the next tick's submit: " +
                $"waiting for it {landingWaitedMs / each:F2} and writing it into the town {landedMs / each:F2} ms a rebuild; sent back in part, " +
                $"{landedMb / each:F2} MB a rebuild: {(device.Claims.Rebuild.HoldsAsAsked ? "no hold" : "every hold")}, the lists of " +
                $"{waysKnown / each} ways the readers asked about and the lights' holds of {waysLightsOnly / each} more; holds asked for " +
                $"{holdsAsked / (double)MeasuredTicks:F0} a tick, {holdsAnswered / (double)MeasuredTicks:F0} of them handed back");
            var walkers = device.Claims.Rebuild.Walkers;
            if (walkers.Keeps)
            {
                Console.WriteLine(
                    $"  the walkers kept on the device: {keptPatched / each} a rebuild written over as the town changed them, " +
                    $"{keptChanged / each} changed by the rebuild, {keptHanded / each} of them and of those left before handed back, " +
                    $"{keptLeft / each} left on the device unread");
            }
            Console.WriteLine(
                $"  {device.Claims.Rebuild.RanOn - ranOnFrom} rebuilds ran on while the town went on, their answers taken up after " +
                $"the tick was laid, and {device.CarsPutOff - putOffFrom} cars' own ticks by the town put off until then; the host " +
                $"waited for the device {claimsIdleMs / each:F2} ms of a rebuild's submitting and waiting");
            var rooms = device.Claims.Rebuild.RoomsNow;
            Console.WriteLine(
                $"  the claims' rooms as they stand: {rooms.Rows} rows ({rooms.Rows * (long)Unsafe.SizeOf<DeviceClaims.ClaimRow>() / (1 << 20)} " +
                $"MiB a copy), {rooms.Pieces} pieces a car's plan, {rooms.Proposed} rows a walk; grown {device.Claims.Rebuild.RoomsGrown} " +
                $"times and {device.Claims.Rebuild.RebuildsLaidAgain} rebuilds laid again since the device was set");
            Console.Write("  the host's rebuild stage by stage, ms a rebuild, the town's laying before each + the device's at it:");
            for (var stage = 0; stage < stages.Length; stage++)
            {
                Console.Write($" {stages[stage]} {stageTownMs[stage] / each:F2}+{stageHereMs[stage] / each:F2}");
            }

            Console.WriteLine();
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
            watchMs / MeasuredTicks, (int)(taken / MeasuredTicks), mps / Math.Max(1, driven), rejoining, world.HardBrakings - brakings, world.Touches - touches,
            (switching.Asked - switches.Asked, switching.Made - switches.Made, switching.Withdrawn - switches.Withdrawn),
            (overtaking.Asked - passes.Asked, overtaking.Made - passes.Made, overtaking.Withdrawn - passes.Withdrawn),
            (sidestepping.Asked - sidesteps.Asked, sidestepping.Made - sidesteps.Made, sidestepping.Withdrawn - sidesteps.Withdrawn));
    }
}
