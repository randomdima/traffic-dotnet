using TrafficSimulation.Runtime;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>
/// The device's world made as the town is laid (<see cref="IWorldMaker"/>): its atlas filed and its marks worked out on the
/// device, and the atlas finished before the world is laid over it where the head can wait for that — a page's finishes
/// as its frames go by (<see cref="DeviceAtlas.Advance"/>).
/// </summary>
/// <param name="finishTheAtlasFirst">
/// Whether the atlas is finished before the world is laid over it, where the head can wait: false lays the world as a page
/// must, over a word standing in for the tables — for a test of what a page does, on a head that can be watched.
/// </param>
/// <param name="marksOnTheDevice">
/// Whether the marks are worked out on the device (<see cref="DeviceMarks"/>), within millimetres of the host's, or on the
/// host to the bit — for a test that holds a town laid onto the device to one handed over to it.
/// </param>
internal sealed class DeviceWorldMaker(Vk vk, bool finishTheAtlasFirst = true, bool marksOnTheDevice = true) : IWorldMaker
{
    readonly DeviceAtlas _atlas = new(vk);

    public IAtlasFiler Atlas => _atlas;

    public IMarker? Marker { get; } = marksOnTheDevice ? new DeviceMarks(vk) : null;

    /// <summary>The world laid, once it is.</summary>
    public DeviceWorld? Laid { get; private set; }

    public WorldLayer Lay(WorldLaying laying)
    {
        if (finishTheAtlasFirst) _atlas.Finish();
        return Laid = new DeviceWorld(vk, laying, _atlas);
    }

    /// <inheritdoc cref="DeviceAtlas.Census"/>
    public (long Points, int Entries, int MostWaysAtAPoint) AtlasCensus => _atlas.Census;

    /// <inheritdoc cref="DeviceAtlas.ReadBack"/>
    public void ReadTheAtlas(Action<AtlasTable> read) => _atlas.ReadBack(read);
}
