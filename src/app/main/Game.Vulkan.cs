using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Main;

/// <summary>
/// The machine a run stands on where that machine is Vulkan: the device behind the glass, the counter
/// that says how often the wall between them was crossed, and the thread a town is laid on.
/// </summary>
/// <remarks>
/// <b>Shared by the two heads that draw with Vulkan</b> — the desktop's window and the handset's
/// activity — because their answers here are the same word for word: a real file system to read a plan
/// from and threads to read it on. What differs between them is the surface alone, and that is
/// <c>Game.Desktop.cs</c> and <c>Game.Android.cs</c>. The browser answers all of it differently and
/// compiles none of this (<c>Game.Web.cs</c>).
/// </remarks>
internal sealed partial class Game
{
    Runtime.Vk _vk = null!;

    /// <summary>
    /// The map being laid, or <c>null</c> when nothing is. <b>Written and read on the loop's thread
    /// only</b> — the thread the lay runs on touches nothing of this class.
    /// </summary>
    Task<LaidTown>? _laying;

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
