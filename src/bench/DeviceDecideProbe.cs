using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Town;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Every decision a device takes held to the town's own</b> (<see cref="DeviceTown.ChecksTheDecisions"/>), on a map:
/// whether each car's and walker's decision is its clock alone, and of a switching car the look for room the device took,
/// figure for figure — how many were held, how many differed, and the first that did.
/// </summary>
internal static class DeviceDecideProbe
{
    const int WarmupTicks = 600;
    const int CheckedTicks = 600;

    public static void Run(string map, SimConfig config)
    {
        Warmup.TheProcess(config);
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        using var world = new TownWorld(plan, config);
        var loop = new SimLoop<TownWorld>(world, config);
        while (loop.Tick < WarmupTicks) loop.Advance();

        using var device = new DeviceTown(vk, config, world) { ChecksTheDecisions = true };
        world.Device = device;
        for (var tick = 0; tick < CheckedTicks; tick++) loop.Advance();
        world.Device = null;

        var (waits, looked, unknown) = device.LooksTaken;
        Console.WriteLine(
            $"device decisions probe — {map} on {vk.DeviceName}, {WarmupTicks} ticks warmed, {CheckedTicks} checked: " +
            $"{device.DecisionsChecked} verdicts held to the town's own, {device.DecisionsUnlike} unlike; switching cars' looks the " +
            $"device took {waits} not looking yet, {looked} looked and found no room, {unknown} found ground the layer does not hold, " +
            $"{device.LooksApart} of them alike but for a figure, at most {device.LooksMostApartM * 1000f:F3} mm apart");
        foreach (var seen in device.DecisionsUnlikeSeen) Console.WriteLine($"  {seen}");
    }
}
