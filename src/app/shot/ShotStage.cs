using System.Numerics;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.Render;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Shot;

/// <summary>
/// <b>The machine a frame is drawn with, held on to between frames of one town</b>: the device, the
/// offscreen renderer laid for that town's ground, and the looks its bodies are drawn in. What it draws is
/// a town somebody else is standing and ticking, so one run can photograph the same world at several
/// moments (SHT-1).
/// </summary>
/// <remarks>
/// <para>
/// <b>There is one drawing path and it is <see cref="Draw"/>.</b> <see cref="ShotRun"/> stands a town,
/// ticks it and asks for one frame; a hand-driven run
/// (<c>--drive</c>) keeps this alive and asks for a frame whenever its script does. A second path would be
/// a picture of that path rather than of the game.
/// </para>
/// <para>
/// Standing this up is a device, a swapchain-free renderer and every sheet the town wears, so it is the
/// expensive half of a picture and the half that is worth keeping — drawing a frame off it is a fraction
/// of a second.
/// </para>
/// </remarks>
internal sealed class ShotStage : IDisposable
{
    readonly SimConfig _config;
    readonly Vk _vk;
    readonly TownRenderer _renderer;
    readonly TownSprites _looks;
    readonly int _widthPx;
    readonly int _heightPx;

    /// <summary>Whether the device under this stage is its own, and so whether letting go of it closes one.</summary>
    readonly bool _ownsDevice;

    ShotStage(
        SimConfig config, CityPlan plan, GroundMesh ground, TownSprites looks, Vk vk, TownRenderer renderer,
        int widthPx, int heightPx, bool ownsDevice = true)
    {
        _config = config;
        _vk = vk;
        _renderer = renderer;
        _looks = looks;
        _widthPx = widthPx;
        _heightPx = heightPx;
        _ownsDevice = ownsDevice;
        Plan = plan;
        Ground = ground;
    }

    /// <summary>The plan the town being photographed was laid from, which every frame's caption is of.</summary>
    public CityPlan Plan { get; }

    /// <summary>The ground the renderer was laid from — the one the town is drawn out of and never a second cut of it.</summary>
    public GroundMesh Ground { get; }

    /// <summary>
    /// The device and the renderer for a town of this plan, at one frame size. <b>Every frame it draws is
    /// that size</b>, exactly as every cell of a sheet is (SHT-3).
    /// </summary>
    public static ShotStage For(CityPlan plan, SimConfig config, int widthPx, int heightPx, bool validate)
    {
        var ground = GroundMesh.Build(plan, config);
        var looks = TownSprites.Load(config);
        var vk = Vk.Open("traffic-dotnet", validate);
        var renderer = TownRenderer.Offscreen(
            vk, widthPx, heightPx, ground, ProjectPaths.GroundSurfaceFiles(), looks.Sheets, TownSprites.RoomFor(plan, config));

        return new ShotStage(config, plan, ground, looks, vk, renderer, widthPx, heightPx);
    }

    /// <summary>
    /// <b>A second frame size on a device a town is already being drawn on</b> — the offscreen eye a bot
    /// seat looks through (DRV-8). The ground and the looks are the ones the window is drawing, because
    /// they are what this town <em>is</em> and a second cut of either would be a second town.
    /// </summary>
    /// <remarks>
    /// <b>It draws on the loop's own thread, between that loop's frames.</b> The read-back waits the device
    /// idle (<see cref="TownRenderer.Shot"/>), so an eye that opened every frame would be a window that
    /// stuttered once a frame; what it costs is paid by whoever asks for a frame and by nobody else.
    /// </remarks>
    public static ShotStage On(
        Vk vk, CityPlan plan, GroundMesh ground, TownSprites looks, SimConfig config, int widthPx, int heightPx)
    {
        var renderer = TownRenderer.Offscreen(
            vk, widthPx, heightPx, ground, ProjectPaths.GroundSurfaceFiles(), looks.Sheets, TownSprites.RoomFor(plan, config));

        return new ShotStage(config, plan, ground, looks, vk, renderer, widthPx, heightPx, ownsDevice: false);
    }

    /// <summary>
    /// The interface a request asks for, dressed before the first frame: the menu shut onto the town, the
    /// <c>--ui</c> words applied, and whatever the request stands the reader's own tools at.
    /// </summary>
    /// <remarks>
    /// It is built from the request rather than inside <see cref="Draw"/> because what it opens decides
    /// whether the <em>tick</em> is measured: a run hands its loop the same answer the panel is drawn from.
    /// </remarks>
    public static Interface Dressed(in ShotRequest ask, SimConfig config)
    {
        var ui = new Interface(config.Trim);

        // A shot is a picture of a town somebody asked for unless a switch says otherwise, so the menu
        // starts as the popup under the gear rather than as the panel a run opens on.
        ui.Menu.ShutOntoTheTown();
        ui.Apply(ask.Ui ?? []);
        foreach (var pointM in ask.RulerPointsM ?? []) ui.Ruler.Click(pointM);

        if (ask.PickedM is { } pickedM) ui.Pick.Click(pickedM);
        return ui;
    }

    /// <summary>
    /// One frame of a town that is standing, written out as a PNG. Everything that decides what is in it is
    /// in <paramref name="ask"/> — the camera is pinned rather than left wherever it was — so the same
    /// request over the same tick is the same picture.
    /// </summary>
    /// <param name="tick">Which tick of that town this is a picture of, for the caption and the report.</param>
    /// <param name="frame">
    /// What the tick cost, where one was measured. There is no window on this path, so a run that measured
    /// nothing hands over the default and the read-out says the frame was not measured rather than printing
    /// the zero it would come to.
    /// </param>
    public ShotReport Draw(
        in ShotRequest ask, Interface ui, TownWorld world, long tick, ReadOnlySpan<ScenarioWatch> scenario,
        in FrameFigures frame)
    {
        if (ask.WidthPx != _widthPx || ask.HeightPx != _heightPx)
        {
            throw new ArgumentException(
                $"This stage draws {_widthPx}x{_heightPx} frames and was asked for {ask.WidthPx}x{ask.HeightPx}: " +
                "a renderer is laid for one frame size.");
        }

        // A frame of the town and nothing else, which is what a picture of the *ground* is judged as:
        // the panels are the interface's own subject and belong to the frames that are about it.
        var bare = Array.IndexOf(ask.Ui ?? [], "none") >= 0;

        // OBS-2v: a layer the switches have taken out of the ground is out of the picture here as well.
        // The game asks this of the renderer every frame; a shot is one frame and asks once.
        _renderer.ShowGround(ui.Switches.Ground.Shown);

        // A shot has no desktop under it, so its interface pixels are the image's own unless
        // UiScale asks for the picture a scaled display would have shown.
        var uiScale = ask.UiScale > 0f ? ask.UiScale : 1f;
        var uiPx = new Vector2(_widthPx, _heightPx) / uiScale;
        var camera = new Camera2D(_config, Plan.WorldSizeM, uiPx) { DevicePxPerUiPx = uiScale };
        if (ask.ViewM > 0f) camera.SetSpan(ask.ViewM, uiPx);

        // Where a run opens looking (OBS-1b), so an unframed picture is the frame the game opens on
        // rather than a second answer about the same map.
        camera.LookAt(
            ask.AtM ?? Opening.LooksAtM(world.Terrain, _config, Plan.WorldSizeM, camera.ViewSpanM(uiPx).Y * 0.5f));

        // About the middle of the frame, so the turn moves what is in the picture round rather than
        // moving the picture off what was framed (OBS-1c).
        camera.Turn(float.DegreesToRadians(ask.TurnDeg), uiPx * 0.5f, uiPx);

        _looks.ReadAspects(_renderer.Atlas);
        _looks.Lay(Plan, world.Uses, _config);
        _renderer.LayStanding(_looks.Standing.Instances);
        var counts = _looks.Fill(
            world, _config, camera.CentreM, camera.CullSpanM(uiPx), camera.PixelsPerMetre, _renderer.SpritesUnder,
            _renderer.SpritesOver, _renderer.SpritesAbove);
        _renderer.SetSpriteCount(counts);
        var sprites = counts.Drawn;

        // The pointer is put outside the frame, so nothing is drawn hovered: a shot with a row lit
        // under a pointer nobody can see is a shot of a state the reader cannot account for. <b>Unless the
        // request asked for one</b> (OBS-2t) — a reading taken under the pointer is a thing to be looked
        // at, and a picture of it has to be askable for without a window.
        var pointerPx = ask.PointerM is { } pointerM ? camera.ScreenAt(pointerM, uiPx) : -Vector2.One;
        var under = 0;
        var underAbove = 0;
        var quads = bare ? 0 : ui.Draw(_renderer.Overlay, _renderer.Underlay, _renderer.UnderlayAbove, new InterfaceFrame
        {
            World = world,
            Ground = Ground,
            Config = _config,
            Camera = camera,
            UiPx = uiPx,
            PointerPx = pointerPx,
            MapName = Plan.Name,
            Tick = tick,
            Frame = frame,
            Scenario = scenario,
        }, out under, out underAbove);

        _renderer.SetOverlayCount(quads);
        _renderer.SetUnderlayCount(under, underAbove);

        var (centreM, clipPerM, facing) = camera.ForShader(uiPx);
        var crossingsBefore = Vk.Crossings;
        _renderer.Frame(new CameraView(centreM, clipPerM, uiPx, facing));
        var crossings = Vk.Crossings - crossingsBefore;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ask.Path))!);
        _renderer.Shot(ask.Path);

        return new ShotReport(
            Plan.Name, ask.Path, _widthPx, _heightPx, camera.ViewSpanM(uiPx), camera.CentreM,
            _renderer.TriangleCount, sprites, _renderer.Room.Written + _renderer.Room.Standing, tick, quads + under,
            crossings, Plan.Seed);
    }

    public void Dispose()
    {
        _renderer.Dispose();
        if (_ownsDevice) _vk.Dispose();
    }
}
