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

    private partial AppWindow Boot(int width, int height, bool validate, float uiScale, Pacing pacing, bool fullscreen, string? display)
    {
        var window = AppWindow.Open("traffic-dotnet", width, height, uiScale, fullscreen, display);
        _vk = Runtime.Vk.Open("traffic-dotnet", validate, window.VkSurface);
        _vk.WantedPacing = pacing;
        return window;
    }

    private partial TownRenderer NewRenderer(GroundMesh mesh, int spriteCapacity) => TownRenderer.OnScreen(
        _vk, _window, mesh, ProjectPaths.GroundSurfaceFiles(), _looks.Sheets, spriteCapacity);

    private partial long Crossings() => Runtime.Vk.Crossings;

    partial void Shutdown() => _vk.Dispose();
}
