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
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The ground asked on the device and on the host, of a running town's cars</b>: once a town has driven a while,
/// five points a car — where it stands and a wheel's reach either side of it fore and aft — asked of both, timed, and
/// held to one answer. It is the question the tyres and the line ask every tick, at the size a town asks it.
/// </summary>
internal static class DeviceGroundProbe
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

        var shapes = new GroundShapes(plan.Paving(config), config);
        var points = CarPoints(world);
        var levels = new byte[points.Length];
        for (var at = 0; at < points.Length; at++) levels[at] = (byte)points[at].Z;

        var onTheHost = new Ground[points.Length];
        var hostMs = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            var took = Stopwatch.StartNew();
            for (var at = 0; at < points.Length; at++) onTheHost[at] = shapes.At(new Vector2(points[at].X, points[at].Y), levels[at]);
            hostMs = Math.Min(hostMs, took.Elapsed.TotalMilliseconds);
        }

        using var vk = Vk.Open("traffic-dotnet", validation: false);
        using var ground = new DeviceGround(vk, shapes.Table);
        using var asked = vk.CreateBuffer((ulong)(points.Length * 16), BufferUsageFlags.StorageBufferBit, HostAccess.Writes);
        using var answers = vk.CreateBuffer((ulong)(points.Length * 4), BufferUsageFlags.StorageBufferBit, HostAccess.Reads);
        asked.Write<Vector4>(points);
        GpuBuffer[] buffers = [asked, answers, .. ground.Tables];
        using var kernel = new ComputeKernel(vk, "ground_at.comp", buffers.Length, (uint)Marshal.SizeOf<DeviceGround.AskPush>());
        var set = kernel.Bind(buffers);
        using var batch = new ComputeBatch(vk);
        using var timer = new GpuTimer(vk, 2);
        var deviceMs = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            var commands = batch.Begin();
            timer.Reset(commands);
            timer.Stamp(commands, 0);
            kernel.Dispatch(commands, set, (uint)((points.Length + 63) / 64), ground.PushFor(points.Length));
            timer.Stamp(commands, 1);
            batch.SubmitAndWait();
            timer.Read();
            deviceMs = Math.Min(deviceMs, timer.BetweenMs(0, 1));
        }

        var read = answers.Span<uint>();
        var differ = 0;
        for (var at = 0; at < points.Length; at++)
        {
            if ((uint)onTheHost[at] != read[at]) differ++;
        }

        Console.WriteLine($"device ground probe — {map} on {vk.DeviceName}, {world.Cars.Count} cars after {WarmupTicks} ticks");
        Console.WriteLine($"{points.Length} points asked, the best of {Rounds} rounds:");
        Console.WriteLine($"  host   {hostMs,8:F3} ms   {hostMs * 1000d / points.Length,6:F3} µs a point");
        Console.WriteLine($"  device {deviceMs,8:F3} ms   {deviceMs * 1000d / points.Length,6:F3} µs a point");
        Console.WriteLine($"  {differ} of {points.Length} answered differently");
    }

    /// <summary>Five points a car: where it stands and where its four wheels touch, on its level.</summary>
    static Vector4[] CarPoints(TownWorld world)
    {
        var cars = world.Cars;
        var points = new Vector4[cars.Count * 5];
        Span<Vector2> wheels = stackalloc Vector2[TyreModel.Wheels];
        for (var car = 0; car < cars.Count; car++)
        {
            var pose = new CarPose(cars.PositionM[car], cars.HeadingRad[car], cars.VelocityMps[car], cars.YawRateRadPerS[car], 0f, Vector2.Zero);
            TyreModel.WheelPointsM(cars.BuildOf(car), pose, wheels);
            var level = cars.LevelOf(car);
            points[car * 5] = Point(cars.PositionM[car], level);
            for (var wheel = 0; wheel < TyreModel.Wheels; wheel++) points[car * 5 + 1 + wheel] = Point(wheels[wheel], level);
        }

        return points;
    }

    static Vector4 Point(Vector2 atM, byte level) => new(atM, level, 0f);
}
