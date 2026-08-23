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
using PresentModeKHR = Silk.NET.Vulkan.PresentModeKHR;

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
internal sealed class Game : IDisposable
{
    readonly SimConfig _config;
    readonly AppWindow _window;
    readonly Runtime.Vk _vk;
    readonly TownSprites _looks;

    readonly Hud.Interface _ui = new();
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
    SimClock _clock;
    CityGen.CityPlan? _plan;
    string _map = string.Empty;
    ulong _agentSeed;

    long _crossingsPerFrame;
    long _frames;

    public Game(SimConfig config, int width, int height, bool validate, float uiScale, PresentModeKHR presentMode)
    {
        _config = config;
        _looks = TownSprites.Load();

        _window = AppWindow.Open("traffic-dotnet", width, height, uiScale);
        _vk = Runtime.Vk.Open("traffic-dotnet", validate, _window.VkSurface);
        _vk.WantedPresentMode = presentMode;
        _renderer = TownRenderer.OnScreen(
            _vk, _window, GroundMesh.Nothing(), ProjectPaths.GroundSurfaceFiles(), _looks.Sheets, spriteCapacity: 1);
        _looks.ReadAspects(_renderer);

        _uiPx = _window.UiPx;
        _camera = new Camera2D(config, Vector2.One, _uiPx) { DevicePxPerUiPx = _window.UiScale };
        _clock = new SimClock(config.TickSeconds, config.Sim.SoakMaxTimeScale);

        Console.WriteLine($"{_window.FramebufferSize.X}x{_window.FramebufferSize.Y} framebuffer at " +
                          $"{_window.UiScale:F2}x desktop scale, so the interface is laid out on " +
                          $"{_uiPx.X:F0}x{_uiPx.Y:F0} — --ui-scale N overrides it");
    }

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
        var lastFrame = TimeSpan.Zero;

        while (!_window.IsClosing && Stopwatch.GetTimestamp() < deadline)
        {
            // The frame is measured end to end and its parts are marked off inside it, so what the
            // read-out prints under the total is a partition of that total and not a second opinion
            // about it (FrameParts). Nothing is stamped unless the read-out is open.
            var startedAt = Stopwatch.GetTimestamp();
            var parts = new FrameParts(_ui.Switches.FrameReadout);

            _window.PumpEvents();
            if (_window.TakeResized())
            {
                _renderer.Recreate();

                // The scale is read again with the size: a window dragged onto a second display is
                // the one way it changes without the process restarting.
                _uiPx = _window.UiPx;
                _camera.DevicePxPerUiPx = _window.UiScale;
            }

            parts.Mark(ref parts.PumpMs);

            ReadInput((float)lastFrame.TotalSeconds);
            parts.Mark(ref parts.InputMs);

            Advance(ref parts);
            Draw(ref parts);

            lastFrame = Stopwatch.GetElapsedTime(startedAt);
            parts.WholeMs = lastFrame.TotalMilliseconds;
            Measure(in parts);
        }

        // The last window's rate rather than the run's: the run's would carry the startup frames the
        // meter drops on purpose, and the read-out this quotes never showed a figure that included them.
        var rate = $"{_frames} frames, {_meter.Figures.Fps:F0} fps in the last window";
        Console.WriteLine(Runtime.Vk.Crossings == 0
            ? $"{rate}; the crossing counter is compiled out of a Release build, which is the point of it"
            : $"{rate}, {_crossingsPerFrame} crossings in the last steady one");
        Budget();

        return 0;
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

        // The partition is only taken while the read-out is on, so a run that did not ask for it is
        // told that rather than shown a row of zeroes and a residual holding the whole frame.
        if (!_ui.Switches.FrameReadout)
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
    /// The keys and the pointer, in the order the layers are offered them. The run keys work in any
    /// scene, which is what makes a check opened from the menu keep the keys it was just using.
    /// </summary>
    void ReadInput(float seconds)
    {
        if (_window.TakePress(Key.F11)) _window.ToggleFullscreen();

        if (_window.TakePress(Key.Escape)) Escape();

        // The menu is modal over a town: it covers the screen, so the keys that drive one are not
        // offered while it is up.
        if (!Running || _ui.Menu.Open)
        {
            MenuInput();
            return;
        }

        if (_window.TakePress(Key.GraveAccent)) _ui.Run.ToggleFreeze();
        if (_window.TakePress(Key.Number1)) _ui.Run.SetPace(1f);
        if (_window.TakePress(Key.Number2)) _ui.Run.SetPace(2f);
        if (_window.TakePress(Key.Number3)) _ui.Run.SetPace(3f);
        if (_window.TakePress(Key.Pause)) _ui.Run.AgentsHeld = !_ui.Run.AgentsHeld;
        if (_window.TakePress(Key.R)) _world!.ReleaseHands();

        _hands.DriveCamera(_window, _camera, _uiPx, seconds, _world!.HandsOn);
        _world.Hands(_hands.ReadKeys(_window, _world));

        if (!_window.TakeClick(out var button, out var atPx)) return;

        // The interface is offered the click before the town under it, so a panel drawn over a car is
        // not also a way of selecting that car.
        switch (_ui.Click(atPx))
        {
            case ClickTaken.Gear:
                _ui.Menu.Show();
                return;
            case ClickTaken.Yes:
                return;
        }

        PlayerHands.Click(button, atPx, _camera, _uiPx, _world, _ui.Switches, _ui.Ruler);
    }

    /// <summary>
    /// <b>OBS-2g — Escape opens and closes the menu, and the way out of the game is the
    /// <c>Exit game</c> button on it.</b> Closing it leaves the town it was opened over exactly as it
    /// was: a menu that could only be left by picking a map was a menu that cost a run to look at.
    /// </summary>
    /// <remarks>
    /// With no map loaded there is nothing behind the menu to close it onto, so it is the way out of
    /// the game — which is what OBS-2g means by a scene that carries no panel.
    /// </remarks>
    void Escape()
    {
        if (Running) _ui.Menu.Toggle();
        else _window.Close();
    }

    void MenuInput()
    {
        // The wheel is the menu's while the menu is up: a page longer than the window scrolls, and a
        // camera that zoomed behind it would be a town nobody asked to move.
        var scrolled = _window.TakeScroll();
        if (scrolled != 0f) _ui.Menu.Scroll(scrolled);

        if (!_window.TakeClick(out var button, out var atPx) || button != MouseButton.Left) return;

        var choice = _ui.Menu.Click(atPx, Running, _ui.Switches, _ui.Run);

        // Unticking the box drops the tapes with it, which is one of the two ways OBS-2f says they go
        // — the other is a right-click, and the ruler handles that itself.
        if (!_ui.Switches.Ruler) _ui.Ruler.Clear();

        switch (choice.Action)
        {
            case MenuAction.OpenMap:
                Open(choice.Name);
                break;
            case MenuAction.RunCheck:
                _ui.Menu.LastOutput = RunWatched(choice.Name);
                break;
            case MenuAction.ReRollSeeds:
                // Off the clock, because a re-roll is the one place in this engine where the point
                // is a town nobody has seen — a seed drawn from the run's own stream would give the
                // same second town every time.
                var seed = new Rng((ulong)Stopwatch.GetTimestamp(), 0);
                Rebuild(((ulong)seed.NextUint() << 32) | seed.NextUint());
                break;
            case MenuAction.Close:
                _ui.Menu.Shut();
                break;
            case MenuAction.Quit:
                _window.Close();
                break;
        }
    }

    /// <summary>
    /// A check, run the way somebody watching runs it: <b>it still prints</b>, and what it printed
    /// is kept for the panel, because whoever opened it from a menu has no terminal behind them.
    /// </summary>
    static string[] RunWatched(string name)
    {
        if (!CheckCatalogue.TryFind(name, out var check)) return [$"No check named {name}."];

        var was = Console.Out;
        using var caught = new StringWriter();
        try
        {
            Console.SetOut(caught);
            check.Run(SimConfig.Load());
        }
        catch (Exception failure)
        {
            caught.WriteLine($"{failure.GetType().Name}: {failure.Message}");
        }
        finally
        {
            Console.SetOut(was);
        }

        var printed = caught.ToString();
        Console.Write(printed);
        return printed.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// A map picked: the plan is read, the ground laid, the town stood up and the renderer rebuilt
    /// for it. The window, the device and the interface all outlive this.
    /// </summary>
    void Open(string map)
    {
        var plan = TownReader.ReadFile(ProjectPaths.TownFile(map));
        _plan = plan;
        _map = plan.Name;
        _agentSeed = plan.Seed;

        var mesh = GroundMesh.Build(plan, _config);
        var world = new TownWorld(plan, _config);

        _renderer.Dispose();
        _renderer = TownRenderer.OnScreen(
            _vk, _window, mesh, ProjectPaths.GroundSurfaceFiles(), _looks.Sheets, TownSprites.CapacityFor(plan, _config));
        _looks.ReadAspects(_renderer);
        _looks.Lay(plan);

        _world?.Dispose();
        _world = world;
        _loop = new SimLoop<TownWorld>(world, _config);
        _track = TrackMetrics.Measures(world) ? new TrackMetrics(_config, world) : null;
        _clock = new SimClock(_config.TickSeconds, _config.Sim.SoakMaxTimeScale);
        _camera = new Camera2D(_config, plan.WorldSizeM, _uiPx) { DevicePxPerUiPx = _window.UiScale };
        _ui.TownChanged();
    }

    /// <summary>
    /// <b>Re-rolling the seed is the whole life cycle in one call.</b> The old town is removed before
    /// it is freed — a town whose physics runs one more frame after its roster is gone is bodies
    /// nobody is deciding for — and every rig that held a point in the old town is told it has gone.
    /// </summary>
    void Rebuild(ulong agentSeed)
    {
        if (_plan is null) return;

        var was = _world;
        _world = null;
        _loop = null;
        was?.Dispose();

        _agentSeed = agentSeed;
        var world = new TownWorld(_plan, _config, agentSeed: agentSeed);
        _world = world;
        _loop = new SimLoop<TownWorld>(world, _config);
        _track = TrackMetrics.Measures(world) ? new TrackMetrics(_config, world) : null;
        _clock.Resynchronise();
        _ui.TownChanged();
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
            if (_track is null)
            {
                _loop.Advance(due);
            }
            else
            {
                // A tick at a time, because the figures are a tick's own: a peak speed and a lift onto
                // the brakes both happen inside one decision interval, and a frame is several ticks.
                for (var tick = 0; tick < due; tick++)
                {
                    _loop.Advance();
                    _track.Saw(_loop.World);
                }
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
        var before = Runtime.Vk.Crossings;
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
        if (_frames >= 1) _crossingsPerFrame = Runtime.Vk.Crossings - before;
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
        MapName = _map,
        WorldSeed = _plan?.Seed ?? 0,
        AgentSeed = _agentSeed,
        Tick = _loop?.Tick ?? 0,
        Frame = _meter.Figures,
        Crossings = _crossingsPerFrame,
        Track = _track,
    };

    public void Dispose()
    {
        _world?.Dispose();
        _renderer.Dispose();
        _vk.Dispose();
        _window.Dispose();
    }
}
