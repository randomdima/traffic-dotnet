using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Shot;

/// <summary>
/// One frame of a town drawn into an image with no window under it and written out as a PNG.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is one shot path and this is it.</b> <c>--shot</c>, <c>--sheet</c> and the end-to-end
/// visual tests in <c>src/tests/e2e/</c> all come through here, because a frame taken through a second
/// drawing path would be a picture of that path rather than of the game: the panels and the layers are
/// drawn by the same <see cref="Hud.Interface"/> the windowed game draws them with.
/// </para>
/// <para>
/// It needs no display, no compositor and nothing to steal focus, and the picture is exactly the
/// size it was asked for rather than whatever the desktop's scale factor made of a window.
/// </para>
/// <para>
/// <b>What it writes is the game's own picture and nothing else</b> (SHT-1). A caption, a scale bar or
/// a cell label belongs under the frame and is <see cref="Caption"/>'s, so that what a reviewer judges
/// and what the game draws are the same pixels.
/// </para>
/// </remarks>
internal static class ShotRun
{
    /// <summary>
    /// Stage one scenario and photograph it. Everything that decides what the frame contains is in
    /// <paramref name="ask"/>, so the same request reproduces the same picture: the map and the
    /// tick are the town, and the camera is pinned rather than left wherever it was.
    /// </summary>
    public static ShotReport Take(ShotRequest ask, SimConfig config)
    {
        var ui = new Interface();
        var wanted = ask.Ui ?? [];
        var onMenu = Array.Exists(wanted, name => name is "menu" or "menu-scenarios");

        // A frame of the town and nothing else, which is what a picture of the *ground* is judged as:
        // the panels are the interface's own subject and belong to the frames that are about it.
        var bare = Array.IndexOf(wanted, "none") >= 0;

        ui.Menu.Shut();
        ui.Apply(wanted);
        foreach (var pointM in ask.RulerPointsM ?? []) ui.Ruler.Click(pointM);

        var plan = TownReader.ReadFile(ProjectPaths.TownFile(ask.Map));

        // GEN-1b in a picture: the menu is shown over no town at all, because nothing has been built
        // yet. A menu photographed over a city would be a picture of the wrong claim.
        var mesh = onMenu ? GroundMesh.Nothing() : GroundMesh.Build(plan, config);
        var looks = TownSprites.Load();
        using var world = new TownWorld(plan, config);

        using var vk = Vk.Open("traffic-dotnet", ask.Validate);
        using var renderer = TownRenderer.Offscreen(
            vk, ask.WidthPx, ask.HeightPx, mesh, ProjectPaths.GroundSurfaceFiles(), looks.Sheets,
            TownSprites.CapacityFor(plan, config));

        // A shot has no desktop under it, so its interface pixels are the image's own unless
        // UiScale asks for the picture a scaled display would have shown.
        var uiScale = ask.UiScale > 0f ? ask.UiScale : 1f;
        var uiPx = new Vector2(ask.WidthPx, ask.HeightPx) / uiScale;
        var camera = new Camera2D(config, plan.WorldSizeM, uiPx) { DevicePxPerUiPx = uiScale };
        if (ask.ViewM > 0f) camera.SetSpan(ask.ViewM, uiPx);
        if (ask.AtM is { } atM) camera.LookAt(atM);

        // A shot of a town that has never ticked is a town of walkers standing on their spawns, which
        // is a picture of the plan rather than of the simulation. Seconds says how far in.
        var loop = new SimLoop<TownWorld>(world, config);
        loop.Timed = ui.Status.Open;
        world.Timed = ui.Status.Open;

        // What the map claims about itself is answered a tick at a time, so the run is advanced one at a
        // time and watched — which is what makes a picture of either panel a picture of the same run.
        var scenario = onMenu ? [] : Scenarios.For(world, config);
        var track = Scenarios.FiguresIn(scenario);
        var ticks = (int)(ask.Seconds * config.Sim.TickRateHz);
        for (var tick = 0; tick < ticks; tick++)
        {
            loop.Advance();
            foreach (var watch in scenario) watch.Saw(world);
        }

        looks.ReadAspects(renderer);
        if (!onMenu) looks.Lay(plan, world.Uses);
        var sprites = onMenu ? 0 : looks.Fill(world, config, camera.CentreM, camera.ViewSpanM(uiPx), renderer.Sprites);
        renderer.SetSpriteCount(sprites);

        // The pointer is put outside the frame, so nothing is drawn hovered: a shot with a row lit
        // under a pointer nobody can see is a shot of a state the reader cannot account for.
        var under = 0;
        var quads = bare ? 0 : ui.Draw(renderer.Overlay, renderer.Underlay, new InterfaceFrame
        {
            World = onMenu ? null : world,
            Config = config,
            Camera = camera,
            UiPx = uiPx,
            PointerPx = -Vector2.One,
            MapName = plan.Name,
            Tick = loop.Tick,

            // The phases and nothing else: there is no window to time on this path, so the read-out
            // says the frame was not measured rather than printing the zero it would come to.
            Frame = new FrameFigures { Phases = loop.Phases, Sub = world.Sub },
            Track = track,
            Scenario = onMenu ? default : scenario,
        }, out under);

        renderer.SetOverlayCount(quads);
        renderer.SetUnderlayCount(under);

        var (centreM, clipPerM) = camera.ForShader(uiPx);
        var crossingsBefore = Vk.Crossings;
        renderer.Frame(new CameraView(centreM, clipPerM, uiPx));
        var crossings = Vk.Crossings - crossingsBefore;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ask.Path))!);
        renderer.Shot(ask.Path);

        return new ShotReport(
            plan.Name, ask.Path, ask.WidthPx, ask.HeightPx, camera.ViewSpanM(uiPx), camera.CentreM,
            renderer.TriangleCount, sprites, TownSprites.CapacityFor(plan, config), loop.Tick, quads + under,
            crossings, plan.Seed);
    }
}

/// <summary>
/// What to photograph. <see cref="ViewM"/> is the span across the frame's <b>short</b> side, as
/// <c>--view</c> asks for it, and <see cref="AtM"/> is where the camera is pinned — a named place of
/// the map, never a search of the town. <see cref="Ui"/> is the <c>--ui</c> word list, matched whole
/// by <see cref="Interface.Apply"/>: <c>none</c> is a bare frame of the town, an empty list is the
/// ordinary interface, and the rest name a layer or a menu page.
/// </summary>
/// <param name="Seconds">How far into a seeded run the frame is taken. Zero is the plan rather than
/// the simulation: every walker still standing on its spawn.</param>
internal readonly record struct ShotRequest(
    string Map,
    string Path,
    int WidthPx,
    int HeightPx,
    float ViewM = 0f,
    Vector2? AtM = null,
    string[]? Ui = null,
    float UiScale = 0f,
    double Seconds = 0,
    IReadOnlyList<Vector2>? RulerPointsM = null,
    bool Validate = false);

/// <summary>What the frame turned out to be — the census a caller prints or asserts on.</summary>
internal readonly record struct ShotReport(
    string Map,
    string Path,
    int WidthPx,
    int HeightPx,
    Vector2 SpanM,
    Vector2 CentreM,
    int Triangles,
    int Sprites,
    int SpriteCapacity,
    long Tick,

    /// <summary>Both buffers' worth: the ground marks drawn under the bodies and everything drawn over them.</summary>
    int InterfaceQuads,
    long Crossings,

    /// <summary>The plan's own seed, which is what makes the picture reproducible from the caption alone.</summary>
    ulong Seed)
{
    /// <summary>
    /// What a metre is worth on this frame — the one figure a review quotes when it says a thing is
    /// too small to judge, and the figure the caption's scale bar is graduated against.
    /// </summary>
    public float PxPerM => SpanM.X > 0f ? WidthPx / SpanM.X : 0f;
}
