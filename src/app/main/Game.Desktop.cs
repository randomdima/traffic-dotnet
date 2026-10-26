using System.Numerics;
using System.Runtime;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Render;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Main;

/// <summary>
/// What about a desktop run is the desktop's: a window with a Vulkan surface under it, and CoreCLR's
/// collector to hand an open back with.
/// </summary>
/// <remarks>
/// This file and <c>Game.Web.cs</c> are the same answer given twice, and the project files pick which
/// one is compiled. The rest of the desktop's Vulkan machine is <c>Game.Vulkan.cs</c>; everything about a
/// run — the order of a frame, the interface, the town — is in <see cref="Game"/> itself and is written once.
/// </remarks>
internal sealed partial class Game
{
    DeviceWorld? _deviceWorld;

    /// <summary>
    /// Every town this run stands has its world stepped on the device that draws it (<see cref="DeviceWorld"/>), and
    /// what moves in it drawn off the device's own state — a window's default, which <c>--managed-world</c> turns off.
    /// </summary>
    public bool TicksOnTheDevice { get; init; }

    partial void TickOnTheDevice(TownWorld? world)
    {
        _deviceWorld = null;
        if (world is null || !TicksOnTheDevice) return;

        // What the town lets go of with the handover is collected by the handover where the town is big enough to want it,
        // and given back to the machine by the stand's own collection (HandBackTheOpen).
        world.HandTheWorldTo(laying => _deviceWorld = new DeviceWorld(_vk, laying));
        _movingOnTheDevice = true;
    }

    private partial DeviceWorldMaker? WorldMaker() => TicksOnTheDevice ? new DeviceWorldMaker(_vk) : null;

    partial void TookTheDeviceWorld(TownWorld town, DeviceWorld world)
    {
        _deviceWorld = world;
        _movingOnTheDevice = true;
    }

    partial void LayTheMovingOnTheDevice(TownWorld world, SpriteRoom room)
    {
        if (_deviceWorld is not { } device) return;

        // The marks are the device's whether or not what moves is drawn there: the host's ring is not filled.
        var (rows, ring) = device.GroundMarks;
        _renderer.DrawTheMarksFrom(rows, ring);
        if (!_movingOnTheDevice) return;

        _renderer.DrawTheMovingFrom(device.DrawWith(world, _looks!, room));
    }

    partial void EmitTheMoving(Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        if (_deviceWorld is not { Sprites: { } sprites } device || _world is not { } world) return;

        sprites.WriteTheFrame(_renderer.ImageIndex, viewCentreM, viewSpanM, pixelsPerMetre, world.ElapsedS, device.SignalsTaken);
        _renderer.Wrote(sprites.FrameBytes);
    }

    private partial AppWindow Boot(int width, int height, bool validate, float uiScale, Pacing pacing, bool fullscreen, string? display)
    {
        var window = AppWindow.Open("traffic-dotnet", width, height, uiScale, fullscreen, display);
        _vk = Runtime.Vk.Open("traffic-dotnet", validate, window.VkSurface);
        _vk.WantedPacing = pacing;
        return window;
    }

    static partial void HandBackTheOpen()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }
}
