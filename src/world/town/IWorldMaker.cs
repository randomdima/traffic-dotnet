using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A world made as the town is laid</b>, in place of the managed one the town would otherwise stand and later hand over:
/// its atlas filed on the device that keeps it (<see cref="Atlas"/>, <see cref="RibbonAtlas.Filing"/>), and the world laid
/// off the town once its bodies stand. So the host never finds a point of the atlas, nor holds a world it throws away
/// (root decision-log.md, 2026-10-15). The device's is <c>DeviceWorldMaker</c>.
/// </summary>
internal interface IWorldMaker
{
    /// <summary>Where the atlas is filed as the town lays it.</summary>
    IAtlasFiler Atlas { get; }

    /// <summary>Where the marks are worked out as the town lays them (<see cref="IMarker"/>), or null for the host's own.</summary>
    IMarker? Marker { get; }

    /// <summary>The world, laid off the town as it stands, over the atlas filed by <see cref="Atlas"/>.</summary>
    WorldLayer Lay(WorldLaying laying);
}
