using System.Diagnostics;
using System.Numerics;
using Silk.NET.Input;
using TrafficSimulation.App.Camera;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Hud;
using TrafficSimulation.App.PlayerControl;
using TrafficSimulation.App.Render;
using TrafficSimulation.Bench;
using TrafficSimulation.Runtime;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Main;

/// <summary>
/// The composition root: it loads the figures, shows the start menu, builds a town when one is
/// picked, and runs the loop.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dependencies are injected from here.</b> No global singletons and no service locator; the
/// figures are read once, at the top, and handed down. The one thing this owns that nothing else may
/// is the order of a frame: pump, read the hands, advance the clock's worth of ticks, fill the
/// buffers, submit.
/// </para>
/// <para>
/// <b>GEN-1b — nothing is built until a map is picked</b>, so the window and the device come up first
/// and the town comes up second. The renderer is rebuilt when the <em>map</em> changes, because its
/// ground mesh and its instance capacity are the town's; re-rolling the agent seed keeps it, since
/// the same plan stands up the same number of bodies.
/// </para>
/// </remarks>
internal sealed partial class Game : IDisposable
{
    readonly SimConfig _config;
    readonly AppWindow _window;
    readonly TownSprites _looks;

    readonly Hud.Interface _ui;
    readonly PlayerHands _hands = new();
    readonly FrameMeter _meter = new();

    TownRenderer _renderer;
    Camera2D _camera;

    /// <summary>The window in interface pixels: what the camera frames and what the panels are laid out in.</summary>
    Vector2 _uiPx;

    TownWorld? _world;
    SimLoop<TownWorld>? _loop;

    /// <summary>The proving ground's own instrument, on the proving ground and nowhere else.</summary>
    TrackMetrics? _track;

    /// <summary>
    /// What the town standing claims about itself, watched every tick (<see cref="Scenarios.For"/>). It is
    /// the map's own claims and the two every town owes, and it is what the panel along the bottom draws
    /// and what the run prints on its way out.
    /// </summary>
    ScenarioWatch[] _scenario = [];
    SimClock _clock;
    string _map = string.Empty;

    long _crossingsPerFrame;
    long _frames;

    /// <summary>What the frame before this one took, which is the step the hands are read over.</summary>
    TimeSpan _lastFrame;

    public Game(
        SimConfig config, int width, int height, bool validate, float uiScale, Pacing pacing,
        bool fullscreen, string? display)
    {
        _config = config;
        _ui = new Hud.Interface(config.Trim);
        _looks = TownSprites.Load();

        // The window and the machine under it are the one thing that is not the same on both, so they
        // are the one thing this root does not do itself (Game.Desktop.cs, Game.Web.cs).
        _window = Boot(width, height, validate, uiScale, pacing, fullscreen, display);
        _renderer = NewRenderer(GroundMesh.Nothing(), spriteCapacity: 1);
        _looks.ReadAspects(_renderer);

        _uiPx = _window.UiPx;
        _camera = new Camera2D(config, Vector2.One, _uiPx) { DevicePxPerUiPx = _window.UiScale };
        _clock = new SimClock(config.TickSeconds, config.Sim.SoakMaxTimeScale);

        Console.WriteLine($"{_window.FramebufferSize.X}x{_window.FramebufferSize.Y} framebuffer on " +
                          $"{_window.DisplayName} at {_window.UiScale:F2}x desktop scale, so the interface is " +
                          $"laid out on {_uiPx.X:F0}x{_uiPx.Y:F0} — --ui-scale N overrides it");
    }

    /// <summary>The window, and the machine that draws into it. Answered by whichever half of this class the build compiled.</summary>
    private partial AppWindow Boot(int width, int height, bool validate, float uiScale, Pacing pacing, bool fullscreen, string? display);

    /// <summary>A renderer for the town about to stand, laid for the ground and the bodies it will hold.</summary>
    private partial TownRenderer NewRenderer(GroundMesh mesh, int spriteCapacity);

    /// <summary>Crossings of the wall between managed code and the machine, since the process started. Zero where the counter is compiled out.</summary>
    private partial long Crossings();

    /// <summary>The machine, let go of after the renderer and before the window.</summary>
    partial void Shutdown();

    /// <summary>Whether a town is standing. Everything the interface offers is different either side of it.</summary>
    bool Running => _world is not null;

    /// <summary>
    /// The <c>--ui</c> switches, thrown before the first frame. <b>The same words the shot path
    /// takes</b>, so a measured run and a picture of one ask for the same thing.
    /// </summary>
    public void Switch(string[] wanted) => _ui.Apply(wanted);

    public int Run(string? openMap, double seconds)
    {
        if (openMap is not null) Open(openMap);

        var deadline = seconds > 0
            ? Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency)
            : long.MaxValue;

        while (Step() && Stopwatch.GetTimestamp() < deadline)
        {
        }

        // The last window's rate rather than the run's: the run's would carry the startup frames the
        // meter drops on purpose, and the read-out this quotes never showed a figure that included them.
        var rate = $"{_frames} frames, {_meter.Figures.Fps:F0} fps in the last window";
        Console.WriteLine(Crossings() == 0
            ? $"{rate}; the crossing counter is compiled out of a Release build, which is the point of it"
            : $"{rate}, {_crossingsPerFrame} crossings in the last steady one");
        Budget();

        // What the town claimed about itself and whether it kept it — the panel's own table, printed on
        // the way out so that a run nobody sat in front of (`--seconds`) is a run something can gate on.
        // A broken claim is a failed run, which is the whole of what makes this more than a read-out.
        return _world is not null && !ScenarioReport.Print(_map, _scenario, _world.ElapsedS) ? 1 : 0;
    }

    /// <summary>
    /// One frame, and whether there is to be another. <b>This and not <see cref="Run"/> is the loop
    /// body</b>: a desktop run drives it from a <c>while</c>, and a browser cannot — a page that
    /// blocked its thread would be a page that never painted — so there it is what the animation
    /// callback calls.
    /// </summary>
    /// <remarks>
    /// The frame is measured end to end and its parts are marked off inside it, so what the read-out
    /// prints under the total is a partition of that total and not a second opinion about it
    /// (<see cref="FrameParts"/>). Nothing is stamped unless the read-out is open.
    /// </remarks>
    public bool Step()
    {
        if (_window.IsClosing) return false;

        var startedAt = Stopwatch.GetTimestamp();
        var parts = new FrameParts(_ui.Status.Open);

        _window.PumpEvents();
        if (_window.TakeResized())
        {
            _renderer.Recreate();

            // The scale is read again with the size: a window dragged onto a second display is the one
            // way it changes without the process restarting.
            _uiPx = _window.UiPx;
            _camera.DevicePxPerUiPx = _window.UiScale;
        }

        parts.Mark(ref parts.PumpMs);

        ReadInput((float)_lastFrame.TotalSeconds);
        parts.Mark(ref parts.InputMs);

        Advance(ref parts);
        Draw(ref parts);

        _lastFrame = Stopwatch.GetElapsedTime(startedAt);
        parts.WholeMs = _lastFrame.TotalMilliseconds;
        Measure(in parts);
        return !_window.IsClosing;
    }

    /// <summary>
    /// The last window's frame budget, printed on the way out. <b>It is what a run nobody sat in front
    /// of is worth measuring for</b>: <c>--seconds</c> with the read-out switched on gives the same
    /// partition of the frame the panel shows, in a form a log can keep.
    /// </summary>
    void Budget()
    {
        var frame = _meter.Figures;
        if (frame.FrameMs <= 0d) return;

        Console.WriteLine(
            $"{"",-9}frame {frame.FrameMs:F2} ms = cpu {frame.CpuMs:F2} + blocked {frame.BlockedMs:F2}, " +
            $"worst {frame.WorstMs:F2}");

        // The partition is only taken while the status panel is open, so a run that did not ask for it
        // is told that rather than shown a row of zeroes and a residual holding the whole frame.
        if (!_ui.Status.Open)
        {
            Console.WriteLine($"{"",-9}where the cpu went is not measured unless it is asked for: --ui frame");
            return;
        }

        Console.WriteLine(
            $"{"",-9}cpu   {frame.CpuMs:F2} ms = sim {frame.SimMs:F2} ({frame.TicksPerFrame:F1} ticks) + " +
            $"sprites {frame.SpritesMs:F2} + interface {frame.InterfaceMs:F2} + submit {frame.SubmitMs:F2} + " +
            $"pump {frame.PumpMs:F2} + input {frame.InputMs:F2} + other {frame.OtherMs:F2}");
    }

    /// <summary>
    /// The keys and the pointer, in the order the layers are offered them. <b>The popups are not
    /// modal</b>: the run keys and the camera work while one is up, because a settings panel that
    /// stops the town is a panel nobody opens mid-run to look at the town.
    /// </summary>
    void ReadInput(float seconds)
    {
        if (_window.TakePress(Key.F11)) _window.ToggleFullscreen();

        if (_window.TakePress(Key.Escape)) Escape();

        if (Running)
        {
            if (_window.TakePress(Key.GraveAccent)) _ui.Run.ToggleFreeze();
            if (_window.TakePress(Key.Number1)) _ui.Run.SetPace(1f);
            if (_window.TakePress(Key.Number2)) _ui.Run.SetPace(2f);
            if (_window.TakePress(Key.Number3)) _ui.Run.SetPace(3f);
            if (_window.TakePress(Key.Pause)) _ui.Run.AgentsHeld = !_ui.Run.AgentsHeld;
            if (_window.TakePress(Key.R)) _world!.ReleaseHands();

            // CTL-7: the selected unit's own action, on a press rather than a hold — it is a lever being
            // pulled once, and the only one anything in this town has is the evacuator's arm.
            if (_window.TakePress(Key.E)) _world!.WorkTheAction();
        }

        // A figure being dragged follows the pointer and takes effect as it goes — <b>on the town that is
        // standing</b>, which is the whole point of the panel: the marks stay on the road, the cars stay
        // where they are, and what changed is the only thing that changed. Standing the fleet up again is
        // sixteen builds and a ground catalogue, so it is cheap enough to spend on the frames a hand is
        // actually moving something.
        _ui.Menu.Drag(_window.PointerPx, _window.IsMouseDown(MouseButton.Left), _ui.Trims);
        if (_ui.Menu.TakeFiguresMoved()) _world?.FiguresChanged();

        // The wheel is the menu's while the pointer is over it: a page longer than the window scrolls,
        // and a camera that zoomed behind it would be a town nobody asked to move. Taking it here is
        // what leaves the camera nothing to zoom by, so one wheel serves both without a mode.
        if (_ui.WheelIsThePanels(_window.PointerPx))
        {
            var scrolled = _window.TakeScroll();
            if (scrolled != 0f) _ui.Menu.Scroll(scrolled);
        }

        if (Running)
        {
            _hands.DriveCamera(_window, _camera, _uiPx, seconds, _world!.HandsOn);
            _world.Hands(_hands.ReadKeys(_window, _world));
        }

        if (!_window.TakeClick(out var button, out var atPx))
        {
            // A gesture ends on the way up rather than on an event, so the button is asked about every
            // frame and not only on the frames a press arrived in (CTL-1b).
            if (Running) _hands.Pointer(_window, _camera, _uiPx, _config, _world!);
            return;
        }

        // The interface is offered the click before the town under it, so a panel drawn over a car is
        // not also a way of selecting that car.
        var taken = _ui.Click(atPx, _uiPx, button == MouseButton.Left, Running, out var choice);

        // Unticking the box drops the tapes with it, which is one of the two ways OBS-2f says they go
        // — the other is a right-click, and the ruler handles that itself.
        if (!_ui.Switches.Ruler) _ui.Ruler.Clear();

        switch (choice.Action)
        {
            case MenuAction.OpenMap:
                Open(choice.Name);
                return;
            case MenuAction.Quit:
                _window.Close();
                return;
        }

        if (taken == ClickTaken.Yes) return;

        _hands.Click(button, atPx, _camera, _uiPx, _world!, _ui.Switches, _ui.Ruler);

        // A press and a release inside one frame is still a click, so the gesture is offered its way up
        // in the frame it began in.
        _hands.Pointer(_window, _camera, _uiPx, _config, _world!);
    }

    /// <summary>
    /// <b>OBS-2g — Escape opens and shuts the menu, and the way out of the game is the <c>Exit</c> tab
    /// on it.</b> Shutting it leaves the town it was opened over exactly as it was: a menu that could
    /// only be left by picking a map was a menu that cost a run to look at.
    /// </summary>
    /// <remarks>
    /// With no map loaded there is nothing behind the menu to shut it onto, so Escape is the way out of
    /// the game — which is what OBS-2g means by a scene that carries no panel.
    /// </remarks>
    void Escape()
    {
        if (!Running)
        {
            _window.Close();
            return;
        }

        // One key for both popups, and it shuts whatever is up before it opens anything: two panels
        // hanging off two corners at once is a screen with more chrome than town on it.
        if (_ui.Controls.Open) _ui.Controls.Shut();
        else _ui.Menu.Toggle();
    }

    /// <summary>
    /// A map picked: the plan is read, the ground laid, the town stood up and the renderer rebuilt
    /// for it. The window, the device and the interface all outlive this.
    /// </summary>
    void Open(string map)
    {
        var plan = TownReader.ReadFile(ProjectPaths.TownFile(map));
        _map = plan.Name;

        var mesh = GroundMesh.Build(plan, _config);
        var world = new TownWorld(plan, _config);

        _renderer.Dispose();
        _renderer = NewRenderer(mesh, TownSprites.CapacityFor(plan, _config));
        _looks.ReadAspects(_renderer);
        _looks.Lay(plan, world.Uses);

        _world?.Dispose();
        _world = world;
        _loop = new SimLoop<TownWorld>(world, _config);

        // The watches are built with the town and not once it is running: one of them stages what its map
        // is about — the exam's thirty-six orders, the crossings' five walkers — and a staging that began
        // on the tenth tick would be measuring whatever the map did with the first nine.
        _scenario = Scenarios.For(world, _config);
        _track = Scenarios.FiguresIn(_scenario);
        _clock = new SimClock(_config.TickSeconds, _config.Sim.SoakMaxTimeScale);
        _camera = new Camera2D(_config, plan.WorldSizeM, _uiPx) { DevicePxPerUiPx = _window.UiScale };
        _ui.TownChanged();
        _hands.TownChanged();
    }

    /// <summary>
    /// Every tick the clock is owed, and how many that was. <b>The count is what makes the frame's
    /// simulation row and the tick's own figures answerable to each other</b> — at pace 3 a frame runs
    /// three ticks, and on a stalled one it runs none.
    /// </summary>
    void Advance(ref FrameParts parts)
    {
        if (_loop is not null)
        {
            _clock.TimeScale = _ui.Run.TimeScale;
            _loop.Timed = parts.Timed;
            _loop.World.Timed = parts.Timed;
            _loop.World.HoldAgents = _ui.Run.AgentsHeld;

            var due = _clock.TicksDue();

            // A tick at a time, because what the watches count is a tick's own: a peak speed, a lift onto
            // the brakes and a body a tick deeper into another all happen inside one decision interval,
            // and a frame is several ticks.
            for (var tick = 0; tick < due; tick++)
            {
                _loop.Advance();
                foreach (var watch in _scenario) watch.Saw(_loop.World);
            }

            parts.SimTicks = due;
        }

        // Marked whether or not a town is standing, so the menu's own frames leave the cursor where
        // the next part expects it rather than paying the gap into the sprites.
        parts.Mark(ref parts.SimMs);
    }

    /// <summary>
    /// The frame just spent, into the read-out's meter. <b>The phase counters are left standing until
    /// the meter says a window closed</b>, which is what makes the figures it publishes that window's
    /// mean per tick rather than the last frame's — <see cref="FrameMeter"/>.
    /// </summary>
    void Measure(in FrameParts parts)
    {
        if (_loop is null)
        {
            _meter.Frame(parts, default, default);
            return;
        }

        if (!_meter.Frame(parts, _loop.Phases, _loop.World.Sub)) return;

        _loop.Phases.Reset();
        _loop.World.Sub.Reset();
    }

    /// <summary>
    /// One frame: fill the sprite buffer, fill the overlay buffer, submit. Everything that changed
    /// this frame is already in mapped memory by the time the five calls happen.
    /// </summary>
    void Draw(ref FrameParts parts)
    {
        var sprites = 0;
        if (_world is not null)
        {
            sprites = _looks.Fill(
                _world, _config, _camera.CentreM, _camera.ViewSpanM(_uiPx), _renderer.Sprites);
        }

        _renderer.SetSpriteCount(sprites);
        parts.Mark(ref parts.SpritesMs);

        _renderer.SetOverlayCount(_ui.Draw(_renderer.Overlay, _renderer.Underlay, Describe(), out var under));
        _renderer.SetUnderlayCount(under);
        parts.Mark(ref parts.InterfaceMs);

        var (centreM, clipPerM) = _camera.ForShader(_uiPx);
        var before = Crossings();
        _renderer.Frame(new CameraView(centreM, clipPerM, _uiPx));
        parts.Mark(ref parts.SubmitMs);

        // The renderer's own account of what it waited for, taken off the frame just drawn and taken
        // *out* of the submit: the wall clock is the presenter's number and the difference is this
        // build's. Never below zero — a frame that had to rebuild the swapchain left the last figure
        // standing while doing none of the waiting it describes.
        parts.BlockedMs = _renderer.BlockedMs;
        parts.SubmitMs = Math.Max(0d, parts.SubmitMs - parts.BlockedMs);

        // Measured around one steady frame and never around the first: that one carries the
        // swapchain's own calls, and a figure that included them would not be the frame's.
        if (_frames >= 1) _crossingsPerFrame = Crossings() - before;
        _frames++;
    }

    /// <summary>The run's state, gathered once, so the interface reaches for nothing while it draws.</summary>
    InterfaceFrame Describe() => new()
    {
        World = _world,
        Config = _config,
        Camera = _camera,
        UiPx = _uiPx,
        PointerPx = _window.PointerPx,
        MarqueePx = _hands.MarqueePx(_window.PointerPx, _config.View.SelectionDragPx),
        MapName = _map,
        Tick = _loop?.Tick ?? 0,
        Frame = _meter.Figures,
        Crossings = _crossingsPerFrame,
        Track = _track,
        Scenario = _scenario,
    };

    public void Dispose()
    {
        _world?.Dispose();
        _renderer.Dispose();
        Shutdown();
        _window.Dispose();
    }
}
