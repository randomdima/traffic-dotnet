using TrafficSimulation.App.DeviceTick;
using TrafficSimulation.App.Drive;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

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
    /// <summary>The page's device as the world's kernels see it, made the first time a town is handed to it.</summary>
    Vk? _kernels;

    /// <summary>
    /// Every town this page stands has its world stepped on the device (<see cref="DeviceWorld"/>), as a desktop window's
    /// is — unless <c>?managed-world</c> said otherwise, or the device cannot hold the kernels, when it is stepped here
    /// (WEB-10).
    /// </summary>
    /// <remarks>
    /// What moves is still drawn off the host's fill and the tyres' marks are not drawn: the emit's kernels bind more
    /// storage buffers than a page is given, and nothing reads the marks' ring back.
    /// </remarks>
    public bool TicksOnTheDevice { get; init; }

    partial void TickOnTheDevice(TownWorld? world)
    {
        if (world is null || !TicksOnTheDevice) return;

        if (!WebGpu.KernelsFit())
        {
            Console.WriteLine("This device cannot hold the world's kernels: the world is stepped in managed code.");
            return;
        }

        var kernels = _kernels ??= new Vk();
        world.HandTheWorldTo(laying => new DeviceWorld(kernels, laying));
    }

    private partial AppWindow Boot(
        int width, int height, bool validate, float uiScale, Pacing pacing, bool fullscreen, string? display) =>
        new AppWindow(uiScale);

    /// <summary>
    /// <b>No ground either, before a town is opened.</b> A renderer laid for no sheets is the menu's
    /// (<see cref="_sheets"/>), and it draws no ground to put a surface on — which is what lets the
    /// page open on one file instead of six, since a picture in a page is a fetch and a picture on the
    /// desktop is already on the disk.
    /// </summary>
    private partial TownRenderer NewRenderer(GroundMesh mesh, SpriteRoom room) => TownRenderer.OnScreen(
        mesh, _sheets.Count == 0 ? [] : ProjectPaths.GroundSurfaceFiles(), _sheets, room);

    private partial long Crossings() => WebGpu.Crossings;

    /// <summary>
    /// <b>Nothing, in a page.</b> A second seat is an instrument driven from a file on a disk (DRV-8), and
    /// a page has neither — so nothing here opens a second target for one to look through.
    /// </summary>
    private partial BotEye? NewEye(CityPlan plan, GroundMesh ground, int widthPx, int heightPx) => null;

    /// <summary>
    /// <b>Nothing, in a page.</b> The art has to be fetched before a town can be drawn and this is the end
    /// of a frame, so the loop would be waiting on the network; the boot's own <c>await</c> drains
    /// <see cref="TakeWanted"/> instead, and the card stands over every frame drawn until it has
    /// (<see cref="Data.Art"/>).
    /// </summary>
    partial void OpenWhatWasPicked()
    {
    }

    /// <summary>The device belongs to the page and outlives the run, so there is nothing to give back.</summary>
    partial void Shutdown()
    {
    }

    /// <summary>
    /// <b>Nothing, in a page.</b> A WebAssembly memory only ever grows, so what a collection frees stays the
    /// page's whatever it compacts — and Mono has no large-object compaction mode to ask for.
    /// </summary>
    static partial void HandBackTheOpen()
    {
    }
}
