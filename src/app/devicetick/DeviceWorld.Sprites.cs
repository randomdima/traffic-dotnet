using TrafficSimulation.App.Render;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>The desktop's draw of what moves, off the world's own state (<see cref="DeviceSprites"/>).</summary>
internal sealed partial class DeviceWorld
{
    /// <summary>What moves, drawn off the device's own state; none until <see cref="DrawWith"/> lays it.</summary>
    public DeviceSprites? Sprites { get; private set; }

    /// <summary>What moves drawn off this world's own state from here on, by the emit laid for it.</summary>
    public DeviceSprites DrawWith(TownWorld town, TownSprites looks, SpriteRoom room)
    {
        Sprites?.Dispose();
        return Sprites = DeviceSprites.For(_vk, Config, town, looks, room, this);
    }

    /// <summary>
    /// <b>What the emit reads of the world</b> (<c>sprites_world.comp</c>): each car's and walker's own row, the cars'
    /// commands, and the two views' rows by slot (<see cref="WorldView.Slot"/>) — where every body stands and points, and a
    /// car's channels, as the view the host is showing has them — the wagons' among them.
    /// </summary>
    public (GpuBuffer Cars, GpuBuffer Walkers, GpuBuffer Commands, GpuBuffer[] CarViews, GpuBuffer[] WalkerViews, GpuBuffer[] WagonViews) Drawn =>
        (_carRows, _walkerRows, _tyres.Table.Commands.Device, _carViews, _walkerViews, _wagonViews);

    partial void DisposeTheSprites() => Sprites?.Dispose();
}
