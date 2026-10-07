using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using TrafficSimulation.Agents.Car.Body;
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
/// while, every car's four wheels — what a claims rebuild lays (TER-4c.2) — and every car's box and walker's disc, which
/// are what a pass, a switch, a bay and a walker stepping back sweep, read off the atlas by both, timed, and held to one
/// answer.
/// </summary>
internal static class DeviceAtlasProbe
{
    const int WarmupTicks = 600;
    const int Rounds = 20;

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

        var (boxHostMs, boxDeviceMs) = TimeTheBoxes(atlas, vk, deviceAtlas, bodies.AsSpan(0, boxes));
        var wheels = WheelsOf(world.Cars, config.Road.HeldAroundAWheelM);
        var (wheelHostMs, wheelDeviceMs, wheelsDiffer, wheelWays) = TimeTheWheels(atlas, vk, deviceAtlas, wheels);


        Console.WriteLine($"device atlas probe — {map} on {vk.DeviceName}, {world.Cars.Count} cars and {bodies.Length - boxes} walkers with a body after {WarmupTicks} ticks");
        Console.WriteLine($"{bodies.Length} bodies asked ({boxes} boxes, {bodies.Length - boxes} discs), the best of {Rounds} rounds, the host's reads uncached:");
        Console.WriteLine($"  host   {hostMs,8:F3} ms   {hostMs * 1000d / bodies.Length,6:F3} µs a body");
        Console.WriteLine($"  device {deviceMs,8:F3} ms   {deviceMs * 1000d / bodies.Length,6:F3} µs a body");
        Console.WriteLine($"  {boxesDiffer + discsDiffer} of {bodies.Length} found over other ways ({boxesDiffer} boxes, {discsDiffer} discs); {ways} ways found, {dropped} dropped on the device");
        Console.WriteLine($"every car read both ways, the best of {Rounds} rounds:");
        Console.WriteLine($"  its box     host {boxHostMs,8:F3} ms {boxHostMs * 1000d / boxes,6:F3} µs a car   device {boxDeviceMs,8:F3} ms");
        Console.WriteLine($"  its wheels  host {wheelHostMs,8:F3} ms {wheelHostMs * 1000d / boxes,6:F3} µs a car   device {wheelDeviceMs,8:F3} ms   ({wheelWays} ways found, {wheelsDiffer} cars found over other ways on the device)");
        Console.WriteLine($"every car's wheels by the ground's kind under each, and of them on a way: {WheelsOnTheGround(world, wheels)}");
    }

    /// <summary>Every car's box alone, read on the host and on the device: the best of the rounds of each.</summary>
    static (double HostMs, double DeviceMs) TimeTheBoxes(RibbonAtlas atlas, Vk vk, DeviceAtlas deviceAtlas, ReadOnlySpan<DeviceAtlas.BodyRow> boxes)
    {
        Span<WayCover> into = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        var hostMs = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            var took = Stopwatch.StartNew();
            foreach (ref readonly var box in boxes) Read(atlas, box, into);
            hostMs = Math.Min(hostMs, took.Elapsed.TotalMilliseconds);
        }

        return (hostMs, TimeOnTheDevice<DeviceAtlas.BodyRow, DeviceAtlas.CountRow>(vk, deviceAtlas, "atlas_under.comp", boxes, out _, out _));
    }

    /// <summary>
    /// Every car's wheels, read on the host and on the device: the best of the rounds of each, how many cars the two
    /// found over other ways or with another wheel on one, and how many ways the host found.
    /// </summary>
    static (double HostMs, double DeviceMs, int Differ, long Ways) TimeTheWheels(
        RibbonAtlas atlas, Vk vk, DeviceAtlas deviceAtlas, DeviceAtlas.WheelsRow[] cars)
    {
        const int Room = RibbonAtlas.MostWaysUnderABody;
        var onTheHost = new WayCover[cars.Length * Room];
        var hostCounts = new int[cars.Length];
        var hostWheels = new byte[cars.Length];
        Span<Vector2> wheelsM = stackalloc Vector2[TyreModel.Wheels];
        var hostMs = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            var took = Stopwatch.StartNew();
            for (var at = 0; at < cars.Length; at++)
            {
                ref readonly var car = ref cars[at];
                (wheelsM[0], wheelsM[1], wheelsM[2], wheelsM[3]) = (car.Wheel0M, car.Wheel1M, car.Wheel2M, car.Wheel3M);
                hostCounts[at] = atlas.UnderWheels(
                    wheelsM, car.AroundAWheelM, onTheHost.AsSpan(at * Room, Room), out hostWheels[at], (byte)car.Channels);
            }

            hostMs = Math.Min(hostMs, took.Elapsed.TotalMilliseconds);
        }

        var ways = 0L;
        foreach (var count in hostCounts) ways += count;

        var deviceMs = TimeOnTheDevice<DeviceAtlas.WheelsRow, UInt4>(vk, deviceAtlas, "atlas_wheels.comp", cars, out var counts, out var covers);
        var differ = 0;
        for (var at = 0; at < cars.Length; at++)
        {
            var count = hostCounts[at];
            var same = counts[at].X == count && counts[at].Z == hostWheels[at]
                       && covers.AsSpan(at * Room, count).SequenceEqual(onTheHost.AsSpan(at * Room, count));
            if (!same) differ++;
        }

        return (hostMs, deviceMs, differ, ways);
    }

    /// <summary>One atlas kernel over these rows, the best of the rounds, and what its last round wrote.</summary>
    static double TimeOnTheDevice<TRow, TCount>(
        Vk vk, DeviceAtlas deviceAtlas, string shader, ReadOnlySpan<TRow> rows, out TCount[] counts, out WayCover[] covers)
        where TRow : unmanaged where TCount : unmanaged
    {
        const int Room = RibbonAtlas.MostWaysUnderABody;
        using var asked = vk.CreateBuffer((ulong)(Math.Max(1, rows.Length) * Marshal.SizeOf<TRow>()), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        using var countsOut = vk.CreateBuffer((ulong)(Math.Max(1, rows.Length) * Marshal.SizeOf<TCount>()), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        using var coversOut = vk.CreateBuffer((ulong)(Math.Max(1, rows.Length) * Room * Marshal.SizeOf<WayCover>()), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        asked.Write(rows);
        GpuBuffer[] buffers = [asked, countsOut, coversOut, .. deviceAtlas.Tables];
        using var kernel = new ComputeKernel(vk, shader, buffers.Length, (uint)Marshal.SizeOf<DeviceAtlas.AskPush>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(vk);
        using var timer = new GpuTimer(vk, 2);
        var deviceMs = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            var commands = batch.Begin();
            timer.Reset(commands);
            timer.Stamp(commands, 0);
            kernel.Dispatch(commands, set, (uint)((rows.Length + 63) / 64), deviceAtlas.PushFor(rows.Length));
            timer.Stamp(commands, 1);
            batch.SubmitAndWait();
            timer.Read();
            deviceMs = Math.Min(deviceMs, timer.BetweenMs(0, 1));
        }

        counts = countsOut.Span<TCount>()[..rows.Length].ToArray();
        covers = coversOut.Span<WayCover>()[..(rows.Length * Room)].ToArray();
        return deviceMs;
    }

    /// <summary>
    /// <b>What every car's wheels stand on, asked both ways</b>: the kind of ground the shell answers under each (TER-7)
    /// against whether it stands on a way (TER-7b) — what a wheel's grip turned on, and what it turns on now.
    /// </summary>
    static string WheelsOnTheGround(TownWorld world, DeviceAtlas.WheelsRow[] cars)
    {
        var kinds = Enum.GetValues<Ground>();
        var all = new int[kinds.Length];
        var onAWay = new int[kinds.Length];
        Span<Vector2> wheelsM = stackalloc Vector2[TyreModel.Wheels];
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        for (var car = 0; car < cars.Length; car++)
        {
            ref readonly var row = ref cars[car];
            (wheelsM[0], wheelsM[1], wheelsM[2], wheelsM[3]) = (row.Wheel0M, row.Wheel1M, row.Wheel2M, row.Wheel3M);
            world.Atlas.UnderWheels(wheelsM, row.AroundAWheelM, under, out var wheels, (byte)row.Channels);
            for (var wheel = 0; wheel < wheelsM.Length; wheel++)
            {
                var kind = (int)world.Terrain.At(wheelsM[wheel], world.Cars.LevelOf(car)).Ground;
                all[kind]++;
                if (((wheels >> wheel) & 1) != 0) onAWay[kind]++;
            }
        }

        return string.Join(", ", kinds.Where(kind => all[(int)kind] > 0).Select(kind => $"{kind} {all[(int)kind]} ({onAWay[(int)kind]})"));
    }

    /// <summary>Every car's four wheels where the fleet has it, as the claims lay its body (<c>TownWorld.LayTheCarsBody</c>).</summary>
    static DeviceAtlas.WheelsRow[] WheelsOf(CarFleet cars, float aroundAWheelM)
    {
        var rows = new DeviceAtlas.WheelsRow[cars.Count];
        Span<Vector2> wheelsM = stackalloc Vector2[TyreModel.Wheels];
        for (var car = 0; car < cars.Count; car++)
        {
            ref readonly var build = ref cars.BuildOf(car);
            TyreModel.WheelPointsM(
                build, new CarPose(cars.PositionM[car], cars.HeadingRad[car], Vector2.Zero, 0f, 0f, Vector2.Zero), wheelsM);
            rows[car] = new DeviceAtlas.WheelsRow(wheelsM[0], wheelsM[1], wheelsM[2], wheelsM[3], aroundAWheelM, cars.Channels[car]);
        }

        return rows;
    }

    /// <summary>Every car's box, as a pass sweeps it, then every walker's disc that stands in the world (PHY-7), as stepping back onto its walk reads it.</summary>
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
}
