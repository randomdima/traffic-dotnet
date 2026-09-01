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

using TrafficSimulation.World.Statics;

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
/// <b>GEN-1b — no city is built until it is picked</b>, so the window and the device come up first and
/// the town comes up second: what a run that named none opens on is the menu, with the idle ring
/// (<see cref="IdleMap"/>) standing behind it. The renderer is rebuilt when the <em>map</em> changes, because its
/// ground mesh and its instance capacity are the town's; re-rolling the agent seed keeps it, since
/// the same plan stands up the same number of bodies.
/// </para>
/// </remarks>
internal sealed partial class Game : IDisposable
{
    readonly SimConfig _config;
    readonly AppWindow _window;

    /// <summary>
    /// Every look the town is drawn in. <b>Read when the first map is opened and not before</b>: the
    /// catalogues are every variant file in <c>assets/</c> and the header of every sheet they name, and
    /// the menu draws none of them. Set with <see cref="_world"/> in <see cref="Open"/>, which is why
    /// the frame reaches it behind the same guard.
    /// </summary>
    TownSprites? _looks;

    readonly Hud.Interface _ui;
    readonly PlayerHands _hands = new();
    readonly FrameMeter _meter = new();

    TownRenderer _renderer;
    Camera2D _camera;

    /// <summary>
    /// The sheets the next renderer is laid for. <b>Empty until a town is opened</b>: the menu is
    /// glyphs and quads over a ground and draws no sprite at all, so packing the whole atlas to show
    /// one is work done twice on a desktop — and on a page it is every one of the town's sheets
    /// fetched before anything can be clicked (<see cref="Main.Data.Art"/>).
    /// </summary>
    IReadOnlyList<SheetSource> _sheets = [];

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

    /// <summary>
    /// What <see cref="FrameTheTown"/> left the camera at, while it is still there. It is how a window
    /// resize tells a framing this run chose from one a reader has moved since — the turn included, since
    /// a town turned in place is a reader having moved it (OBS-1c).
    /// </summary>
    (Vector2 CentreM, float PixelsPerMetre, float TurnRad)? _framedAt;

    /// <summary>What the frame before this one took, which is the step the hands are read over.</summary>
    TimeSpan _lastFrame;

    /// <summary>
    /// When the frame before this one finished, so that what the next one waited to begin can be
    /// measured. Zero before the first has ended.
    /// </summary>
    long _frameClosedAt;

    public Game(
        SimConfig config, int width, int height, bool validate, float uiScale, Pacing pacing,
        bool fullscreen, string? display)
    {
        _config = config;
        _ui = new Hud.Interface(config.Trim);

        // The window and the machine under it are the one thing that is not the same on both, so they
        // are the one thing this root does not do itself (Game.Desktop.cs, Game.Web.cs).
        _window = Boot(width, height, validate, uiScale, pacing, fullscreen, display);

        // OBS-2k: what the panels need, handed to the window before the first thing is laid out against
        // it — the density is the display's until that would put the menu off the edge of the glass.
        _window.LeastUiPx = new Vector2(config.View.InterfaceLeastWidthPx, config.View.InterfaceLeastHeightPx);
        _renderer = NewRenderer(GroundMesh.Nothing(), spriteCapacity: 1);

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

    /// <summary>
    /// A map the menu was clicked on. <b>Not necessarily an open</b>: a desktop run has the plan on
    /// disk and opens it where it stands, and a page has to fetch it first — and a frame cannot wait
    /// on a fetch, so the browser's half only writes the name down and the boot's own <c>await</c>
    /// picks it up (<see cref="Main.Data.Town"/>).
    /// </summary>
    private partial void PickMap(string map);

    /// <summary>The machine, let go of after the renderer and before the window.</summary>
    partial void Shutdown();

    /// <summary>Whether the run is over — the window shut, or the page's own way of saying so.</summary>
    public bool Closed => _window.IsClosing;

    /// <summary>Whether a town is standing. Everything the interface offers is different either side of it.</summary>
    bool Running => _world is not null;

    /// <summary>
    /// The <c>--ui</c> switches, thrown before the first frame. <b>The same words the shot path
    /// takes</b>, so a measured run and a picture of one ask for the same thing.
    /// </summary>
    public void Switch(string[] wanted) => _ui.Apply(wanted);

    /// <summary>
    /// A town stood up before the first frame, for a run that is handed its loop rather than owning
    /// one. <b>The same thing the menu does when a map is picked</b>, and the same thing
    /// <see cref="Run"/> does with <c>--map</c>.
    /// </summary>
    /// <param name="behindTheMenu">Whether the menu stays up over it, which is what the idle map is opened as (GEN-1b).</param>
    public void Start(string map, bool behindTheMenu = false) => Open(map, behindTheMenu);

    /// <summary>
    /// <b>The map a run opens on when it was handed none</b> (GEN-1b): the ring is what the menu stands
    /// over until a reader picks something, and it is the same map on either head and in either
    /// configuration.
    /// </summary>
    public static string IdleMap => IdlePlan.Name;

    public int Run(string? openMap, double seconds)
    {
        // GEN-1b: a run that named a map opens on it, and one that named none opens on the menu with the
        // idle ring standing behind it.
        Open(openMap ?? IdleMap, behindTheMenu: openMap is null);

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

        // What this frame waited before it was allowed to begin. On the desktop it is the loop's own
        // turnaround and near enough nothing, because the wait for the display happens inside the
        // submit and the renderer reports it there. In a page it is the entire wait for the animation
        // callback — a browser paces a frame by choosing when to ask for the next one — and measuring
        // it here is what makes the two machines quote the same frame.
        var waitedMs = _frameClosedAt != 0
            ? Stopwatch.GetElapsedTime(_frameClosedAt, startedAt).TotalMilliseconds
            : 0d;

        // Past the longest a frame may be, nothing was drawn: a browser stops asking a hidden tab for
        // frames at all. <b>The figure is not touched</b> — a wait shortened to make the arithmetic
        // tidy leaves the read-out quoting the rate this build could draw at rather than the rate it
        // did, which is the mistake this whole measurement exists to avoid — and the meter drops the
        // frame instead. What is done here is the one thing only the loop can: the clock forgets the
        // time it was never asked to simulate.
        if (waitedMs > FrameMeter.LongestFrameMs) _clock.Resynchronise();

        var parts = new FrameParts(_ui.Status.Open);

        _window.PumpEvents();
        if (_window.TakeResized())
        {
            _renderer.Recreate();

            // The scale is read again with the size: a window dragged onto a second display is the one
            // way it changes without the process restarting.
            _uiPx = _window.UiPx;
            _camera.DevicePxPerUiPx = _window.UiScale;

            // <b>A town standing behind the menu is framed against the window and not once</b> (OBS-1b):
            // a canvas that settles its size a moment after the town stood up, or a window dragged wider,
            // would otherwise leave what the reader opened on half off the screen. <b>The moment anybody
            // moves the camera themselves, or shuts the menu the framing was for, it is theirs</b>
            // (OBS-1a) — so the follow stops at the first frame this is no longer where it was left.
            if (_framedAt is { } framed && _world is not null)
            {
                if (framed == (_camera.CentreM, _camera.PixelsPerMetre, _camera.TurnRad) && _ui.Menu.Open)
                {
                    _camera.SetSpan(_config.View.CameraDefaultViewM, _uiPx);
                    FrameTheTown(_world, _world.Plan.WorldSizeM);
                }
                else
                {
                    _framedAt = null;
                }
            }
        }

        parts.Mark(ref parts.PumpMs);

        // Never over more than a frame: the span carrying a stall is the one after it, and a pan
        // stepped over forty-five seconds of a tab nobody was looking at throws the camera off the map.
        ReadInput(MathF.Min((float)_lastFrame.TotalSeconds, (float)(FrameMeter.LongestFrameMs / 1000d)));
        parts.Mark(ref parts.InputMs);

        Advance(ref parts);
        Draw(ref parts);

        // The frame is what it cost plus what it waited to start, so a rate taken off it is the rate
        // the town is drawn at rather than the rate this build could draw it at. The hands are read
        // over the same span for the same reason: a pan is metres of real time, not of work.
        _frameClosedAt = Stopwatch.GetTimestamp();
        parts.BlockedMs += waitedMs;
        _lastFrame = Stopwatch.GetElapsedTime(startedAt, _frameClosedAt) + TimeSpan.FromMilliseconds(waitedMs);
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
    /// <remarks>
    /// <b>The start menu is not one of those popups</b> (GEN-1b): the ring behind it is a picture rather
    /// than a town somebody is playing, so nothing here is offered it — a camera dragged at the start menu
    /// would take the road out from under the panel standing in the middle of it.
    /// </remarks>
    void ReadInput(float seconds)
    {
        if (_window.TakePress(Key.F11)) _window.ToggleFullscreen();

        if (_window.TakePress(Key.Escape)) Escape();

        var playing = Running && !_ui.Menu.AtTheStart;
        if (playing)
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

        if (playing)
        {
            // CTL-9: the fingers first, because two of them take the camera off whatever one of them had
            // started — a pinch that was also finishing a drag would pan twice and select on the way up.
            _hands.ReadTouches(_window, _camera, _uiPx, _config);
            _hands.DriveCamera(_window, _camera, _uiPx, _config, seconds, _world!.HandsOn);
            _world.Hands(_hands.ReadKeys(_window, _world));
        }

        if (!_window.TakeClick(out var button, out var atPx))
        {
            // A gesture ends on the way up rather than on an event, so the button is asked about every
            // frame and not only on the frames a press arrived in (CTL-1b).
            if (playing) _hands.Pointer(_window, _camera, _uiPx, _config, _world!);
            return;
        }

        // CTL-1b: shift is read where the press was, because it is what says which gesture this is.
        var alsoKeep = _window.IsKeyDown(Key.ShiftLeft) || _window.IsKeyDown(Key.ShiftRight);

        // The interface is offered the click before the town under it, so a panel drawn over a car is
        // not also a way of selecting that car.
        var taken = _ui.Click(atPx, _uiPx, button == MouseButton.Left, playing, _camera, out var choice);

        // Unticking the box drops the tapes with it, which is one of the two ways OBS-2f says they go
        // — the other is a right-click, and the ruler handles that itself.
        if (!_ui.Switches.Ruler) _ui.Ruler.Clear();

        switch (choice.Action)
        {
            case MenuAction.OpenMap:
                PickMap(choice.Name);
                return;
            case MenuAction.Quit:
                _window.Close();
                return;
        }

        if (taken == ClickTaken.Yes) return;

        _hands.Click(button, atPx, alsoKeep, _camera, _uiPx, _world!, _ui.Switches, _ui.Ruler);

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
    /// <b>The start menu is the exception, and it is not a scene with no panel</b> (GEN-1b): it cannot be
    /// shut, so Escape has nothing to do at it and the way out of the game is the tab that says so.
    /// </remarks>
    void Escape()
    {
        if (_ui.Menu.AtTheStart) return;

        // One key for both popups, and it shuts whatever is up before it opens anything: two panels
        // hanging off two corners at once is a screen with more chrome than town on it.
        if (_ui.Controls.Open) _ui.Controls.Shut();
        else _ui.Menu.Toggle();
    }

    /// <summary>
    /// A map picked: the plan is read, the ground laid, the town stood up and the renderer rebuilt
    /// for it. The window, the device and the interface all outlive this.
    /// </summary>
    /// <param name="behindTheMenu">
    /// Whether the menu stays up over the town that has just stood (GEN-1b). It is what the idle map is
    /// opened as and nothing else is: a map somebody clicked is a map they asked to look at.
    /// </param>
    void Open(string map, bool behindTheMenu = false)
    {
        var plan = Maps.Plan(map, _config, BuildingCatalog.Shared.OrdinaryFootprintsM());
        _map = plan.Name;

        var mesh = GroundMesh.Build(plan, _config);
        var world = new TownWorld(plan, _config);

        _looks ??= TownSprites.Load();

        _renderer.Dispose();
        _sheets = _looks.Sheets;
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
        FrameTheTown(world, plan.WorldSizeM);
        _ui.TownChanged(behindTheMenu);
        _hands.TownChanged();
    }

    /// <summary>
    /// <b>Where a run opens looking</b> (OBS-1b): the middle of the town or the nearest road to it. What it
    /// left the camera at is kept, so a resize can tell this framing from one the reader has since moved.
    /// </summary>
    /// <remarks>
    /// <b>The town the start menu stands over is framed the same way as any other</b> (GEN-1b). The panel
    /// is in the middle of the screen and the middle of the ring is the field inside it, so the menu sits
    /// in the hole and the road is on screen all the way round; a town shoved aside to clear a panel that
    /// is no longer in a corner would be half off the window instead.
    /// </remarks>
    void FrameTheTown(TownWorld world, Vector2 worldSizeM)
    {
        var viewM = _camera.ViewSpanM(_uiPx);
        _camera.LookAt(Opening.LooksAtM(world.Terrain, worldSizeM, MathF.Min(viewM.X, viewM.Y) * 0.5f));
        _framedAt = (_camera.CentreM, _camera.PixelsPerMetre, _camera.TurnRad);
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
        if (_looks is { } looks && _world is not null)
        {
            // The cull span and not the view span: a turned town shows a diamond, and a body just outside
            // the upright rectangle is inside the picture (OBS-1c).
            sprites = looks.Fill(
                _world, _config, _camera.CentreM, _camera.CullSpanM(_uiPx), _renderer.Sprites);
        }

        _renderer.SetSpriteCount(sprites);
        parts.Mark(ref parts.SpritesMs);

        _renderer.SetOverlayCount(_ui.Draw(_renderer.Overlay, _renderer.Underlay, Describe(), out var under));
        _renderer.SetUnderlayCount(under);
        parts.Mark(ref parts.InterfaceMs);

        var (centreM, clipPerM, facing) = _camera.ForShader(_uiPx);
        var before = Crossings();
        _renderer.Frame(new CameraView(centreM, clipPerM, _uiPx, facing));
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
        Counting = Crossings() > 0,
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
