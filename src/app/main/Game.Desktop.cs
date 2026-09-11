using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Main;

/// <summary>
/// The one thing about a desktop run that is the desktop's: a window with a Vulkan surface under it.
/// </summary>
/// <remarks>
/// This file, <c>Game.Android.cs</c> and <c>Game.Web.cs</c> are the same answer given three times, and
/// the project files pick which one is compiled. Everything the two Vulkan heads answer alike is
/// <c>Game.Vulkan.cs</c>; everything about a run — the order of a frame, the interface, the town — is in
/// <see cref="Game"/> itself and is written once.
/// </remarks>
internal sealed partial class Game
{
    private partial AppWindow Boot(int width, int height, bool validate, float uiScale, Pacing pacing, bool fullscreen, string? display)
    {
        var window = AppWindow.Open("traffic-dotnet", width, height, uiScale, fullscreen, display);
        _vk = Runtime.Vk.Open("traffic-dotnet", validate, window.VkSurface);
        _vk.WantedPacing = pacing;
        return window;
    }
}
