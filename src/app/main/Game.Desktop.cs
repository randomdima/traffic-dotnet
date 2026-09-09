using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Main;

/// <summary>
/// The machine the desktop run stands on: a window with a Vulkan surface, a device behind it, and the
/// counter that says how often the wall between them was crossed.
/// </summary>
/// <remarks>
/// This file and <c>Game.Web.cs</c> are the same three answers given twice, and the project file picks
/// which one is compiled. Everything else about a run — the order of a frame, the interface, the town —
/// is in <see cref="Game"/> itself and is written once.
/// </remarks>
internal sealed partial class Game
{
    Runtime.Vk _vk = null!;

    /// <summary>
    /// The map being laid, or <c>null</c> when nothing is. <b>Written and read on the loop's thread
    /// only</b> — the thread the lay runs on touches nothing of this class.
    /// </summary>
    Task<LaidTown>? _laying;

    private partial AppWindow Boot(int width, int height, bool validate, float uiScale, Pacing pacing, bool fullscreen, string? display)
    {
        var window = AppWindow.Open("traffic-dotnet", width, height, uiScale, fullscreen, display);
        _vk = Runtime.Vk.Open("traffic-dotnet", validate, window.VkSurface);
        _vk.WantedPacing = pacing;
        return window;
    }

    private partial TownRenderer NewRenderer(GroundMesh mesh, int spriteCapacity) => TownRenderer.OnScreen(
        _vk, _window, mesh, ProjectPaths.GroundSurfaceFiles(), _sheets, spriteCapacity);

    private partial long Crossings() => Runtime.Vk.Crossings;

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
        if (TakeWanted() is { } picked) _laying = Task.Run(() => LaidTown.Lay(picked, _config));
        if (_laying is not { IsCompleted: true } laid) return;

        _laying = null;

        // The result, and with it whatever the lay threw: a map that cannot be opened fails the run here
        // rather than on a thread nobody is watching.
        Stand(laid.GetAwaiter().GetResult());
    }

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

    partial void Shutdown() => _vk.Dispose();
}
