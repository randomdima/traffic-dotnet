using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.Core.Config;
using Vk = TrafficSimulation.Runtime.Vk;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What opening a map costs as a window opens it</b>: <see cref="LoadProbe"/>'s stages, with the world laid onto a
/// device as the town is (<see cref="DeviceWorldMaker"/>) — the atlas filed onto it a band at a time and no managed world
/// stood.
/// </summary>
internal static class DeviceLoadProbe
{
    public static void Run(string map, SimConfig config)
    {
        using var vk = Vk.Open("traffic-dotnet", validation: false);
        LoadProbe.Run(map, config, new DeviceWorldMaker(vk));
    }
}
