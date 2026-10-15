using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A world made as the town is laid</b>, in place of the managed one the town would otherwise stand and later hand over:
/// its atlas filed straight into it a band at a time (<see cref="Atlas"/>, <c>RibbonAtlas.Stream</c>), and the world laid
/// off the town once its bodies stand. So the host never holds the whole atlas, nor a world it throws away — on a traced
/// city a gigabyte off the open's peak (root decision-log.md, 2026-10-15). The device's is <c>DeviceWorldMaker</c>.
/// </summary>
internal interface IWorldMaker
{
    /// <summary>Where the atlas is filed as the town lays it.</summary>
    IAtlasKeeper Atlas { get; }

    /// <summary>The world, laid off the town as it stands, over the atlas already filed into <see cref="Atlas"/>.</summary>
    WorldLayer Lay(WorldLaying laying);
}
