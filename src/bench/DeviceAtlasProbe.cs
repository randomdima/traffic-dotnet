using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Which ways a running town's bodies stand over, asked on the device and on the host</b>: once a town has driven a
/// while, every car's box and every walker's disc — what a claims rebuild lays (TER-4c.2) — read off the atlas by both,
/// timed, and held to one answer. The host's reads are uncached, which is the read the device ports; how its recall
/// answers the same bodies a tick at a time afterwards is printed beside them.
/// </summary>
internal static class DeviceAtlasProbe
{
    const int WarmupTicks = 600;
    const int Rounds = 20;

    /// <summary>How long the recall is watched for after the timing: a few seconds of creeping, queueing and driving.</summary>
    const int RecallTicks = 120;

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        var atlas = world.Atlas;
        var bodies = Bodies(world, out var boxes);
        const int Room = RibbonAtlas.MostWaysUnderABody;

        var onTheHost = new WayCover[bodies.Length * Room];
        var hostCounts = new int[bodies.Length];
        var hostMs = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            var took = Stopwatch.StartNew();
            for (var at = 0; at < bodies.Length; at++) hostCounts[at] = Read(atlas, bodies[at], onTheHost.AsSpan(at * Room, Room));
            hostMs = Math.Min(hostMs, took.Elapsed.TotalMilliseconds);
        }

        using var vk = Vk.Open("traffic-dotnet", validation: false);
        using var deviceAtlas = new DeviceAtlas(vk, atlas.Table);
        using var asked = vk.CreateBuffer((ulong)(bodies.Length * Marshal.SizeOf<DeviceAtlas.BodyRow>()), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        using var counts = vk.CreateBuffer((ulong)(bodies.Length * Marshal.SizeOf<DeviceAtlas.CountRow>()), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        using var covers = vk.CreateBuffer((ulong)(bodies.Length * Room * Marshal.SizeOf<WayCover>()), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        asked.Write<DeviceAtlas.BodyRow>(bodies);
        GpuBuffer[] buffers = [asked, counts, covers, .. deviceAtlas.Tables];
        using var kernel = new ComputeKernel(vk, "atlas_under.comp", buffers.Length, (uint)Marshal.SizeOf<DeviceAtlas.AskPush>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(vk);
        using var timer = new GpuTimer(vk, 2);
        var deviceMs = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            var commands = batch.Begin();
            timer.Reset(commands);
            timer.Stamp(commands, 0);
            kernel.Dispatch(commands, set, (uint)((bodies.Length + 63) / 64), deviceAtlas.PushFor(bodies.Length));
            timer.Stamp(commands, 1);
            batch.SubmitAndWait();
            timer.Read();
            deviceMs = Math.Min(deviceMs, timer.BetweenMs(0, 1));
        }

        var deviceCounts = counts.Span<DeviceAtlas.CountRow>();
        var onTheDevice = covers.Span<WayCover>();
        var (boxesDiffer, discsDiffer, ways, dropped) = (0, 0, 0L, 0L);
        for (var at = 0; at < bodies.Length; at++)
        {
            var count = hostCounts[at];
            ways += count;
            dropped += deviceCounts[at].Dropped;
            var same = deviceCounts[at].Count == count
                       && onTheDevice.Slice(at * Room, count).SequenceEqual(onTheHost.AsSpan(at * Room, count));
            if (same) continue;

            if (at < boxes) boxesDiffer++;
            else discsDiffer++;
        }

        var (recallAsks, recallKept, recallDiffer) = WatchTheRecall(world, loop);

        Console.WriteLine($"device atlas probe — {map} on {vk.DeviceName}, {world.Cars.Count} cars and {bodies.Length - boxes} walkers with a body after {WarmupTicks} ticks");
        Console.WriteLine($"{bodies.Length} bodies asked ({boxes} boxes, {bodies.Length - boxes} discs), the best of {Rounds} rounds, the host's reads uncached:");
        Console.WriteLine($"  host   {hostMs,8:F3} ms   {hostMs * 1000d / bodies.Length,6:F3} µs a body");
        Console.WriteLine($"  device {deviceMs,8:F3} ms   {deviceMs * 1000d / bodies.Length,6:F3} µs a body");
        Console.WriteLine($"  {boxesDiffer + discsDiffer} of {bodies.Length} found over other ways ({boxesDiffer} boxes, {discsDiffer} discs); {ways} ways found, {dropped} dropped on the device");
        Console.WriteLine($"recall, the next {RecallTicks} ticks: {recallAsks} asks, {recallKept} answered from the last reading, {recallDiffer} otherwise than a fresh read");
    }

    /// <summary>Every car's box, then every walker's disc that stands in the world (PHY-7), as the claims lay them.</summary>
    static DeviceAtlas.BodyRow[] Bodies(TownWorld world, out int boxes)
    {
        var cars = world.Cars;
        var people = world.People;
        var bodies = new List<DeviceAtlas.BodyRow>(cars.Count + people.Count);
        for (var car = 0; car < cars.Count; car++)
        {
            var halfM = cars.BuildOf(car).CollisionSizeM * 0.5f;
            bodies.Add(DeviceAtlas.BodyRow.Box(cars.PositionM[car], Heading.Unit(cars.HeadingRad[car]), halfM.X, halfM.Y, cars.Channels[car]));
        }

        boxes = bodies.Count;
        for (var person = 0; person < people.Count; person++)
        {
            if (people.Inside[person].Any) continue;

            bodies.Add(DeviceAtlas.BodyRow.Disc(people.PositionM[person], people.RadiusM[person], CityPlan.RoadArrays.GroundChannel));
        }

        return [.. bodies];
    }

    static int Read(RibbonAtlas atlas, in DeviceAtlas.BodyRow body, Span<WayCover> into) =>
        body.IsADisc != 0
            ? atlas.UnderDisc(body.CentreM, body.HalfLengthM, into, (byte)body.Channels)
            : atlas.UnderBox(body.CentreM, body.Forward, body.HalfLengthM, body.HalfWidthM, into, (byte)body.Channels);

    /// <summary>
    /// The town driven on a tick at a time, every body asked of a recall of its own as the claims ask it and of a fresh
    /// read: how many asks, how many the recall answered without reading, and how many it answered otherwise.
    /// </summary>
    static (long Asks, long Kept, long Differ) WatchTheRecall(TownWorld world, SimLoop<TownWorld> loop)
    {
        var atlas = world.Atlas;
        var (cars, people) = (world.Cars, world.People);
        var carRecall = new RibbonAtlas.Recall(cars.Count);
        var walkerRecall = new RibbonAtlas.Recall(people.Count);
        var fresh = new WayCover[RibbonAtlas.MostWaysUnderABody];
        var (asks, kept, differ) = (0L, 0L, 0L);

        void Asked(RibbonAtlas.Recall recall, int index, ReadOnlySpan<WayCover> recalled, RibbonAtlas.Recall.Reading before, int count)
        {
            asks++;
            if (recall.Last[index] == before) kept++;
            if (!recalled.SequenceEqual(fresh.AsSpan(0, count))) differ++;
        }

        for (var tick = 0; tick < RecallTicks; tick++)
        {
            loop.Advance();
            for (var car = 0; car < cars.Count; car++)
            {
                var halfM = cars.BuildOf(car).CollisionSizeM * 0.5f;
                var (centreM, forward, channels) = (cars.PositionM[car], Heading.Unit(cars.HeadingRad[car]), cars.Channels[car]);
                var before = carRecall.Last[car];
                var recalled = atlas.UnderBox(centreM, forward, halfM.X, halfM.Y, carRecall, car, channels);
                Asked(carRecall, car, recalled, before, atlas.UnderBox(centreM, forward, halfM.X, halfM.Y, fresh, channels));
            }

            for (var person = 0; person < people.Count; person++)
            {
                if (people.Inside[person].Any) continue;

                var (centreM, radiusM) = (people.PositionM[person], people.RadiusM[person]);
                var before = walkerRecall.Last[person];
                var recalled = atlas.UnderDisc(centreM, radiusM, walkerRecall, person);
                Asked(walkerRecall, person, recalled, before, atlas.UnderDisc(centreM, radiusM, fresh));
            }
        }

        return (asks, kept, differ);
    }
}
