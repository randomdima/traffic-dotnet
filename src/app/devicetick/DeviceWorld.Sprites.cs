using TrafficSimulation.App.Render;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.DeviceTick;

/// <summary>The desktop's draw of what moves, off the world's own state (<see cref="DeviceSprites"/>).</summary>
internal sealed partial class DeviceWorld
{
    /// <summary>What moves, drawn off the device's own state; none until <see cref="DrawWith"/> lays it.</summary>
    public DeviceSprites? Sprites { get; private set; }

    /// <summary>
    /// What moves drawn off this world's own state from here on, by the emit laid for it — and the cars' smoke's tracks
    /// laid behind every step from here on, which the emit lays the smoke along (CAR-54).
    /// </summary>
    public DeviceSprites DrawWith(TownWorld town, TownSprites looks, SpriteRoom room)
    {
        Sprites?.Dispose();
        LayTheSmoke();
        return Sprites = DeviceSprites.For(_vk, Config, town, looks, room, this);
    }

    /// <summary>
    /// <b>Every car's smoke's track as it stands behind the last step submitted</b>, read back — for an instrument: no frame
    /// reads it, the emit reading it where it lies.
    /// </summary>
    public (DeviceSmoke.Head[] Heads, DeviceSmoke.Point[] Points) ReadTheSmoke()
    {
        var smoke = Smoke ?? throw new InvalidOperationException("No track is laid until something draws off the world (DrawWith).");
        using var heads = _vk.CreateBuffer(smoke.Heads.SizeBytes, Silk.NET.Vulkan.BufferUsageFlags.TransferDstBit, HostAccess.Reads);
        using var points = _vk.CreateBuffer(smoke.Points.SizeBytes, Silk.NET.Vulkan.BufferUsageFlags.TransferDstBit, HostAccess.Reads);
        using var batch = new ComputeBatch(_vk);
        var commands = batch.Begin();
        _vk.AfterTheSubmitsBefore(commands);
        _vk.Copy(commands, smoke.Heads, heads, smoke.Heads.SizeBytes);
        _vk.Copy(commands, smoke.Points, points, smoke.Points.SizeBytes);
        batch.SubmitAndWait();
        return (heads.Span<DeviceSmoke.Head>().ToArray(), points.Span<DeviceSmoke.Point>().ToArray());
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
