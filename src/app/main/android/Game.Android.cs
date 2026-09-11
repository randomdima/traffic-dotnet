using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Main;

/// <summary>
/// The one thing about a handset run that is the handset's: the activity's own glass, with a Vulkan
/// surface made of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The glass is already there.</b> The activity took its window's surface before it started this
/// thread (<see cref="AppWindow.Shows"/>), so a size, a density and an <c>ANativeWindow</c> are waiting
/// and nothing here opens anything. The words a desktop run is given are the ones a handset has no
/// answer for: it is fullscreen, there is one display, and the size is the glass's.
/// </para>
/// <para>
/// Everything past this call is the desktop's own machine — the same device, the same swapchain, the
/// same renderer and the same SPIR-V (AND-1), and <c>Game.Vulkan.cs</c> is where the two heads meet.
/// </para>
/// </remarks>
internal sealed partial class Game
{
    private partial AppWindow Boot(int width, int height, bool validate, float uiScale, Pacing pacing, bool fullscreen, string? display)
    {
        // AND-8 — the instance is created at Vulkan 1.3 like the desktop's, which is the floor the
        // manifest asks the store for rather than something this call discovers.
        var glass = AppWindow.OnTheGlass(uiScale);
        _vk = Runtime.Vk.Open("traffic-dotnet", validate, glass.VkSurface);
        _vk.WantedPacing = pacing;
        return glass;
    }
}
