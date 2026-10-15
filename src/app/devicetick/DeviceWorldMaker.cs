using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>The device's world made as the town is laid (<see cref="IWorldMaker"/>): its atlas filed straight onto the device.</summary>
internal sealed class DeviceWorldMaker(Vk vk) : IWorldMaker
{
    readonly DeviceAtlas _atlas = new(vk);

    public IAtlasKeeper Atlas => _atlas;

    /// <summary>The world laid, once it is.</summary>
    public DeviceWorld? Laid { get; private set; }

    public WorldLayer Lay(WorldLaying laying) => Laid = new DeviceWorld(vk, laying, _atlas);
}
