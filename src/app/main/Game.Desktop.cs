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
/// This file, <c>Game.Android.cs</c> and <c>Game.Web.cs</c> are the same answer given three times, and
/// the project files pick which one is compiled. Everything the two Vulkan heads answer alike is
/// <c>Game.Vulkan.cs</c>; everything about a run — the order of a frame, the interface, the town — is in
/// <see cref="Game"/> itself and is written once.
/// </remarks>
internal sealed partial class Game
{
    DeviceTown? _deviceTown;
    DeviceSprites? _deviceSprites;

    /// <summary>
    /// <c>--device-tick</c>: every town this run stands has its agents ticked on the device in step
    /// (<see cref="DeviceTown"/>), on the device that draws it — and what moves in it emitted there
    /// (<see cref="DeviceSprites"/>), off the car tick's own tables.
    /// </summary>
    public bool TicksOnTheDevice { get; init; }

    partial void TickOnTheDevice(TownWorld? world)
    {
        _deviceSprites?.Dispose();
        _deviceSprites = null;
        _deviceTown?.Dispose();
        _deviceTown = null;
        if (world is null || !TicksOnTheDevice) return;

        _deviceTown = new DeviceTown(_vk, _config, world);
        world.Device = _deviceTown;
        _movingOnTheDevice = _vk.HasFloat64;
    }

    partial void LayTheMovingOnTheDevice(TownWorld world, SpriteRoom room)
    {
        if (!_movingOnTheDevice || _deviceTown is not { } town) return;

        _deviceSprites = DeviceSprites.For(_vk, _config, world, _looks!, room, town.CarTick);
        _renderer.DrawTheMovingFrom(_deviceSprites.Instances, _deviceSprites.Draws, _deviceSprites.AboveFrom);
    }

    partial void EmitTheMoving(Vector2 viewCentreM, Vector2 viewSpanM, float pixelsPerMetre)
    {
        if (_deviceSprites is not { } sprites || _world is not { } world) return;

        sprites.Lay(world);
        sprites.EmitForTheFrame(viewCentreM, viewSpanM, pixelsPerMetre, world.ElapsedS);
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
