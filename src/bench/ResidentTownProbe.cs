using System.Diagnostics;
using System.Numerics;
using Silk.NET.Vulkan;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The town resident on the device</b> (<see cref="ResidentTown"/>): a shipped city warmed on the host, handed to the
/// device, and ticked there — the tick timed, and how many cars and walkers moved and how many poses came out of it not a
/// number, read once before and once after.
/// </summary>
internal static class ResidentTownProbe
{
    /// <summary>Ticks the town runs on the host before it is handed over, so its cars are on the road.</summary>
    const int Warm = 300;

    /// <summary>Ticks it runs resident, timed.</summary>
    const int Measured = 600;

    /// <summary>How far a body has to have gone to count as moved, in metres.</summary>
    const float MovedM = 1f;

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);

        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var town = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(town, config);
        for (var tick = 0; tick < Warm; tick++) loop.Advance();

        using var vk = Vk.Open("traffic-dotnet", validation: false);
        var handedAt = Stopwatch.GetTimestamp();
        using var resident = new ResidentTown(vk, config, town);
        var handedMs = Stopwatch.GetElapsedTime(handedAt).TotalMilliseconds;

        var cars = town.Cars.Count;
        var people = town.People.Count;
        var carsBefore = Read<DeviceTyres.PoseRow>(vk, resident.Root[TownRows.CarsPoses]!, cars);
        var peopleBefore = Read<DeviceWalkerTick.WalkerRow>(vk, resident.Root[TownRows.PeopleTable]!, people);

        var tickMs = new double[Measured];
        for (var tick = 0; tick < Measured; tick++)
        {
            var began = Stopwatch.GetTimestamp();
            resident.Tick();
            tickMs[tick] = Stopwatch.GetElapsedTime(began).TotalMilliseconds;
        }

        var carsAfter = Read<DeviceTyres.PoseRow>(vk, resident.Root[TownRows.CarsPoses]!, cars);
        var peopleAfter = Read<DeviceWalkerTick.WalkerRow>(vk, resident.Root[TownRows.PeopleTable]!, people);
        var (carsMoved, carsLost) = (0, 0);
        for (var car = 0; car < cars; car++)
        {
            var at = carsAfter[car].PositionM;
            if (!float.IsFinite(at.X) || !float.IsFinite(at.Y)) carsLost++;
            else if (Vector2.Distance(at, carsBefore[car].PositionM) > MovedM) carsMoved++;
        }

        var (peopleMoved, peopleLost) = (0, 0);
        for (var person = 0; person < people; person++)
        {
            var at = peopleAfter[person].PositionM;
            if (!float.IsFinite(at.X) || !float.IsFinite(at.Y)) peopleLost++;
            else if (Vector2.Distance(at, peopleBefore[person].PositionM) > MovedM) peopleMoved++;
        }

        Array.Sort(tickMs);
        Console.WriteLine($"resident town probe — {map} on {vk.DeviceName}, {cars} cars and {people} people, warmed {Warm} ticks on the host");
        Console.WriteLine($"  handed over in {handedMs:F0} ms; {Measured} ticks resident: median {tickMs[Measured / 2]:F2} ms, p90 {tickMs[Measured * 9 / 10]:F2}, worst {tickMs[^1]:F2}");
        Console.WriteLine($"  cars moved {carsMoved} of {cars}, not a number {carsLost}; walkers moved {peopleMoved} of {people}, not a number {peopleLost}");
    }

    /// <summary>A device table's first rows, copied out once — the probe's own read, not the town's.</summary>
    static T[] Read<T>(Vk vk, GpuBuffer table, int count) where T : unmanaged
    {
        var bytes = GpuBuffer.Bytes<T>(count);
        using var read = vk.CreateBuffer(bytes, BufferUsageFlags.TransferDstBit, HostAccess.Reads);
        vk.OneShot(commands => vk.Copy(commands, table, read, bytes));
        return read.Span<T>()[..count].ToArray();
    }
}
