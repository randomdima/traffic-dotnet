using System.Runtime;
using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Drive;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Main;

/// <summary>
/// What about a desktop run is the desktop's: a window with a Vulkan surface under it, the device behind the glass, the
/// counter that says how often the wall between them was crossed, the thread a town is laid on, and CoreCLR's collector
/// to hand an open back with.
/// </summary>
/// <remarks>
/// This file and <c>Game.Web.cs</c> are the same answer given twice, and the project files pick which one is compiled:
/// the desktop has a real file system to read a plan from and threads to read it on, the browser neither. Everything
/// about a run — the order of a frame, the interface, the town — is in <see cref="Game"/> itself and is written once.
/// </remarks>
internal sealed partial class Game
{
    Runtime.Vk _vk = null!;

    /// <summary>
    /// The map being laid, or <c>null</c> when nothing is. <b>Written and read on the loop's thread
    /// only</b> — the thread the lay runs on touches nothing of this class.
    /// </summary>
    Task<LaidTown>? _laying;

    /// <summary>
    /// Every town this run stands has its world stepped on the device that draws it (<see cref="DeviceWorld"/>), and
    /// what moves in it drawn off the device's own state — a window's default, which <c>--managed-world</c> turns off.
    /// </summary>
    public bool TicksOnTheDevice { get; init; }

    private partial TownRenderer NewRenderer(GroundMesh mesh, SpriteRoom room) => TownRenderer.OnScreen(
        _vk, _window, mesh, GroundMesh.SurfaceFiles(), _sheets, room);

    private partial long Crossings() => Runtime.Vk.Crossings;

    private partial Runtime.Vk? EngineDevice() => _vk;

    /// <summary>The image the frame being filled is drawn into: the frame's kernels ride in its recording.</summary>
    private partial int FrameSlot() => _renderer.ImageIndex;

    private partial int EngineSlots() => _renderer.ImageCount;

    /// <summary>
    /// <b>DRV-8 — a second target on the device already drawing this town.</b> The ground and the looks are
    /// the window's own, because they are what the town is: an eye laid from a second cut of either would
    /// be a picture of a different town at the same tick.
    /// </summary>
    private partial BotEye? NewEye(CityPlan plan, GroundMesh ground, int widthPx, int heightPx) =>
        new(_vk, plan, ground, _looks!, _config, widthPx, heightPx);

    partial void LetGoOfTheTriangles(GroundMesh ground) => ground.ForgetTheTriangles();

    partial void TickOnTheDevice(TownWorld? world)
    {
        if (world is null || !TicksOnTheDevice) return;

        // What the town lets go of with the handover is collected by the handover where the town is big enough to want it,
        // and given back to the machine by the stand's own collection (HandBackTheOpen).
        world.HandTheWorldTo(laying => new DeviceWorld(_vk, laying));
    }

    private partial DeviceWorldMaker? WorldMaker() => TicksOnTheDevice ? new DeviceWorldMaker(_vk) : null;

    /// <summary>
    /// The map picked, laid on a thread of its own (<see cref="LaidTown"/>) and stood up on the first
    /// frame it is ready in. <b>No frame waits for it</b> (OBS-2n): the window keeps pumping, the town
    /// already up keeps ticking, and the card keeps saying which map is on its way.
    /// </summary>
    /// <remarks>
    /// One at a time, which the card enforces for it: nothing under the card takes a click, so there is
    /// no second map to pick while the first is being laid.
    /// </remarks>
    partial void OpenWhatWasPicked()
    {
        if (TakeWanted() is { } picked) _laying = LayAside(picked);
        if (_laying is not { IsCompleted: true } laid) return;

        _laying = null;

        // The result, and with it whatever the lay threw: a map that cannot be opened fails the run here
        // rather than on a thread nobody is watching.
        Stand(laid.GetAwaiter().GetResult());
    }

    /// <summary>
    /// A method of its own because the lambda's capture is made where its variable's scope begins: written beside
    /// the pattern above, that is the top of every frame, picked or not.
    /// </summary>
    Task<LaidTown> LayAside(string map) => Task.Run(() => LaidTown.Lay(map, _config));

    /// <summary>
    /// The town being laid when the run ended, waited out and dropped. <b>Waited out rather than
    /// abandoned</b>: the device is torn down a moment later, and a lay left running past that is a
    /// thread building a world for a machine that has gone.
    /// </summary>
    partial void ForgetWhatWasBeingOpened()
    {
        if (_laying is not { } laying) return;

        _laying = null;
        try
        {
            laying.GetAwaiter().GetResult().World.Dispose();
        }
        catch (Exception broke)
        {
            Console.WriteLine($"the map being opened when the run ended could not be laid: {broke.Message}");
        }
    }

    private partial AppWindow Boot(int width, int height, bool validate, float uiScale, Pacing pacing, bool fullscreen, string? display)
    {
        var window = AppWindow.Open("traffic-dotnet", width, height, uiScale, fullscreen, display);
        _vk = Runtime.Vk.Open("traffic-dotnet", validate, window.VkSurface);
        _vk.WantedPacing = pacing;
        return window;
    }

    partial void Shutdown() => _vk.Dispose();

    static partial void HandBackTheOpen()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }
}
