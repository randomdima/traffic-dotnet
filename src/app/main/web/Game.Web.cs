using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Main;

/// <summary>
/// The machine the browser run stands on: a canvas, a WebGPU device already started under it, and the
/// counter that says how often the wall between them was crossed.
/// </summary>
/// <remarks>
/// This file and <c>Game.Desktop.cs</c> are the same four answers given twice (WEB-1), and the project file
/// picks which one is compiled. <b>The device is not opened here</b>: WebGPU is asked for over a
/// promise, and a constructor cannot wait, so <see cref="Boot"/> has it running before this is reached.
/// </remarks>
internal sealed partial class Game
{
    /// <summary>The map the menu was clicked on and this run has not been handed the plan for yet.</summary>
    string? _wanted;

    private partial AppWindow Boot(
        int width, int height, bool validate, float uiScale, Pacing pacing, bool fullscreen, string? display) =>
        new AppWindow(uiScale);

    private partial TownRenderer NewRenderer(GroundMesh mesh, int spriteCapacity) =>
        TownRenderer.OnScreen(mesh, ProjectPaths.GroundSurfaceFiles(), _looks.Sheets, spriteCapacity);

    private partial long Crossings() => WebGpu.Crossings;

    /// <summary>
    /// The name, written down. A page has to fetch a plan before it can read one and this is called
    /// from inside a frame, so opening it here would be the loop waiting on the network.
    /// </summary>
    private partial void PickMap(string map) => _wanted = map;

    /// <summary>
    /// The map a click asked for, once. <b>The boot loop is what drains it</b>: that is the only place
    /// in a browser run where waiting for the plan to arrive is allowed (<see cref="Data.Town"/>).
    /// </summary>
    public string? TakeWanted()
    {
        var wanted = _wanted;
        _wanted = null;
        return wanted;
    }

    /// <summary>The device belongs to the page and outlives the run, so there is nothing to give back.</summary>
    partial void Shutdown()
    {
    }
}
