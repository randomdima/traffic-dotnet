using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>One town soaked with its world on the device, beside the same town with its world in managed code</b>
/// (<see cref="DeviceWorld"/>, <see cref="SoakProbe.Sample"/>): what each did, how long each took, and whether the device's
/// kept every claim the map makes. The two need not agree to the body — the device's arithmetic is its own — but a town
/// whose world is on the device keeps what the town keeps.
/// </summary>
internal static class DeviceWorldProbe
{
    public static bool Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        Console.WriteLine(
            $"device world probe — {map} on {vk.DeviceName}, {SoakProbe.WarmupTicks} warm-up ticks, " +
            $"{SoakProbe.MeasuredTicks} measured");
        Laid(map, config, vk);
        Console.WriteLine($"{"world",-9}{"walkers",9}{"cars",6}{"down",6}{"wrecked",9}{"km driven",12}{"touches",9}{"s",8}");

        var managedAt = Stopwatch.GetTimestamp();
        var managed = SoakProbe.Sample(map, config);
        var managedS = Stopwatch.GetElapsedTime(managedAt).TotalSeconds;
        Print("managed", managed, managedS);

        var deviceAt = Stopwatch.GetTimestamp();
        var device = SoakProbe.Sample(map, config, layTheWorld: laying => new DeviceWorld(vk, laying));
        var deviceS = Stopwatch.GetElapsedTime(deviceAt).TotalSeconds;
        Print("device", device, deviceS);
        Apart(map, config, vk);

        return ScenarioReport.Print(map, [device], SoakProbe.MeasuredTicks / config.Sim.TickRateHz);
    }

    /// <summary>
    /// <b>The town laid on the device alone</b>, first, so a town too big to soak beside its managed world still says what it
    /// costs: the buffers its laying holds, and — past the warm-up — what a tick records and how long one takes.
    /// </summary>
    static void Laid(string map, SimConfig config, Vk vk)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var device = new TownWorld(plan, config);
        var (deviceBefore, mappedBefore) = vk.BufferBytes;
        device.HandTheWorldTo(laying => new DeviceWorld(vk, laying));
        var (deviceAfter, mappedAfter) = vk.BufferBytes;
        Console.WriteLine(
            $"laid on the device: {(deviceAfter - deviceBefore) / 1e6:F1} MB device-local, " +
            $"{(mappedAfter - mappedBefore) / 1e6:F1} MB mapped, of which the solver's step " +
            $"{((DeviceWorld)device.World).SolverBytes / 1e6:F1} MB");
        var loop = new SimLoop<TownWorld>(device, config);
        loop.Advance(SoakProbe.WarmupTicks);
        Recorded(loop);
    }

    /// <summary>
    /// The two worlds stepped side by side off one plan, and how far apart their cars and walkers stand at a few ticks
    /// along — nought for as long as the device's arithmetic is the host's, and growing once it is not.
    /// </summary>
    static void Apart(string map, SimConfig config, Vk vk)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var managed = new TownWorld(plan, config);
        using var device = new TownWorld(plan, config);
        device.HandTheWorldTo(laying => new DeviceWorld(vk, laying));
        var managedLoop = new SimLoop<TownWorld>(managed, config);
        var deviceLoop = new SimLoop<TownWorld>(device, config);
        Console.Write("apart, the furthest car and walker (m) at tick");
        foreach (var tick in (ReadOnlySpan<int>)[1, 2, 10, 60, 600])
        {
            while (managedLoop.Tick < tick)
            {
                managedLoop.Advance();
                deviceLoop.Advance();
            }

            var carM = 0f;
            for (var car = 0; car < managed.Cars.Count; car++)
            {
                carM = MathF.Max(carM, Vector2.Distance(managed.Cars.PositionM[car], device.Cars.PositionM[car]));
            }

            var walkerM = 0f;
            for (var person = 0; person < managed.People.Count; person++)
            {
                walkerM = MathF.Max(walkerM, Vector2.Distance(managed.People.PositionM[person], device.People.PositionM[person]));
            }

            Console.Write($"  {tick}: {carM:G3} / {walkerM:G3}");
        }

        Console.WriteLine();

        // The step ahead shown, so the fleets the host fills from stand where the device draws (WorldLayer.StepsAhead).
        device.LayTheClaims();
        Drawn(device, config, plan);
    }

    /// <summary>
    /// What a device tick records, on average over a stretch of ticks: dispatches, barriers and submits — the figure the
    /// step's latency follows, read whatever else shares the device (counted in DEBUG only) — and the wall clock it took.
    /// </summary>
    static void Recorded(SimLoop<TownWorld> loop)
    {
        const int ticks = 120;
        var (dispatches, barriers, submits) = (Vk.Dispatches, Vk.Barriers, ComputeBatch.Begun);
        var at = Stopwatch.GetTimestamp();
        for (var tick = 0; tick < ticks; tick++) loop.Advance();
        var tickMs = Stopwatch.GetElapsedTime(at).TotalMilliseconds / ticks;
        Console.WriteLine(
            $"recorded a tick: {(Vk.Dispatches - dispatches) / (float)ticks:F1} dispatches, " +
            $"{(Vk.Barriers - barriers) / (float)ticks:F1} barriers, {(ComputeBatch.Begun - submits) / (float)ticks:F2} submits; " +
            $"{tickMs:F2} ms a tick");
    }

    /// <summary>
    /// What moves in the device's town drawn off the device's own state (<see cref="DeviceSprites"/>) beside the host's
    /// fill of the same town and view, which reads the fleets the view was read into: how many instances each drew and how
    /// many differ, at a street and at the whole town.
    /// </summary>
    static void Drawn(TownWorld town, SimConfig config, CityPlan plan)
    {
        var looks = TownSprites.Load(config);
        looks.ReadAspects(SheetAtlas.Pack(looks.Sheets));
        var room = looks.RoomFor(plan, config);
        var over = new SpriteInstance[room.Over];
        var above = new SpriteInstance[Math.Max(1, room.Above)];
        var sprites = ((DeviceWorld)town.World).DrawWith(town, looks, room);
        var uiPx = new Vector2(1600f, 900f);
        var townPxPerM = MathF.Min(uiPx.X / plan.WorldSizeM.X, uiPx.Y / plan.WorldSizeM.Y);
        var busiest = 0;
        for (var car = 1; car < town.Cars.Count; car++)
        {
            if (town.Cars.VelocityMps[car].LengthSquared() > town.Cars.VelocityMps[busiest].LengthSquared()) busiest = car;
        }

        Console.Write("drawn, instances host / device / differing:");
        foreach (var (where, centreM, pxPerM) in (ReadOnlySpan<(string, Vector2, float)>)
                 [("street", town.Cars.PositionM[busiest], 1600f / 45f), ("town", plan.WorldSizeM * 0.5f, townPxPerM)])
        {
            var spanM = uiPx / pxPerM;
            var (overCount, aboveCount) = looks.FillMoving(town, config, centreM, spanM, pxPerM, over, above);
            sprites.Emit(centreM, spanM, pxPerM, town.ElapsedS);
            var differ = Differing(over.AsSpan(0, overCount), sprites.Over) + Differing(above.AsSpan(0, aboveCount), sprites.Above);
            Console.Write($"  {where} {overCount + aboveCount} / {sprites.OverCount + sprites.AboveCount} / {differ}");
        }

        Console.WriteLine();
    }

    static int Differing(ReadOnlySpan<SpriteInstance> host, ReadOnlySpan<SpriteInstance> onDevice)
    {
        var differ = Math.Abs(host.Length - onDevice.Length);
        for (var at = 0; at < Math.Min(host.Length, onDevice.Length); at++)
        {
            if (host[at] != onDevice[at]) differ++;
        }

        return differ;
    }

    static void Print(string world, TownWatch watch, double seconds) => Console.WriteLine(
        $"{world,-9}{watch.Walkers,9}{watch.Cars,6}{watch.Down,6}{watch.Wrecked,9}{watch.DrivenM / 1000f,12:F1}" +
        $"{watch.Touches,9}{seconds,8:F1}");
}
