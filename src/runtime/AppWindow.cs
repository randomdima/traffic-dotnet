using System.Numerics;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace TrafficSimulation.Runtime;

/// <summary>
/// The window and its input, driven by this engine's own loop.
/// </summary>
/// <remarks>
/// <para><c>IWindow.Run</c> is never called — it owns the loop, and the loop's five-phase order is
/// this engine's. The window is initialised and then pumped a step at a time.</para>
/// <para>It owns the one number the desktop's scale factor may reach the rest of the engine through,
/// <see cref="UiScale"/>, and hands out <see cref="UiPx"/>. Everything above this class works in
/// interface pixels; the framebuffer's own go to the swapchain and the viewport and nowhere else.</para>
/// </remarks>
internal sealed class AppWindow : IDisposable
{
    /// <summary>How long the desktop is given to answer a change of window state, and how often it is asked.</summary>
    const int SettleMs = 250;

    const int SettleStepMs = 5;

    readonly IWindow _window;
    readonly IInputContext _input;
    readonly IKeyboard? _keyboard;
    readonly IMouse? _mouse;

    /// <summary>
    /// Which keys have gone down since anybody last asked. Indexed by the key's own value, which is
    /// dense enough that an array is smaller and quicker than a set — and asking clears the entry,
    /// so a key that means "toggle" cannot toggle sixty times a second while it is held.
    /// </summary>
    readonly bool[] _pressed = new bool[512];

    /// <summary>
    /// What was asked for on the command line, and zero when nothing was: the platform's own figure
    /// is then what <see cref="UiScale"/> answers.
    /// </summary>
    readonly float _wantedUiScale;

    float _scroll;
    MouseButton? _clicked;
    Vector2 _clickedAtPx;
    Vector2D<int> _lastFramebufferSize;
    bool _fullscreen;

    AppWindow(IWindow window, IInputContext input, float wantedUiScale, bool fullscreen)
    {
        _window = window;
        _input = input;
        _wantedUiScale = wantedUiScale;
        _fullscreen = fullscreen;
        _keyboard = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;
        _mouse = input.Mice.Count > 0 ? input.Mice[0] : null;
        _lastFramebufferSize = window.FramebufferSize;

        if (_keyboard is not null)
        {
            _keyboard.KeyDown += (_, key, _) =>
            {
                if ((int)key >= 0 && (int)key < _pressed.Length) _pressed[(int)key] = true;
            };
        }

        // Wheel and click are events, not polled state: a frame asking "is it down now" misses a press
        // and release that both landed between two frames, and reads one long press as a click a frame.
        if (_mouse is null) return;

        _mouse.Scroll += (_, wheel) => _scroll += wheel.Y;

        _mouse.MouseDown += (mouse, button) =>
        {
            _clicked = button;
            _clickedAtPx = InUiPx(mouse.Position);
        };
    }

    /// <summary>
    /// A pointer position, in the pixels the interface is laid out in — which is the space everything
    /// above this class works in.
    /// </summary>
    /// <remarks>
    /// Three spaces meet here, and on a scaled desktop no two are the same: a 2× display hands back a
    /// 1800 × 1400 framebuffer for a 900 × 700 window while the pointer still arrives in 0…900, and
    /// <c>--ui-scale</c> names a third. Converting once here is what keeps the interface's hit tests
    /// and the camera's zoom-about-pointer from each solving it separately.
    /// </remarks>
    Vector2 InUiPx(Vector2 windowPx) => InUiPx(windowPx, _window.Size, _window.FramebufferSize, UiScale);

    /// <summary>The conversion itself, as arithmetic over three vectors and a factor, so it can be checked without a window.</summary>
    public static Vector2 InUiPx(
        Vector2 windowPx, Vector2D<int> windowSize, Vector2D<int> framebufferSize, float uiScale)
    {
        if (windowSize.X <= 0 || windowSize.Y <= 0 || uiScale <= 0f) return windowPx;

        return new Vector2(
            windowPx.X * framebufferSize.X / windowSize.X, windowPx.Y * framebufferSize.Y / windowSize.Y) / uiScale;
    }

    public bool IsClosing => _window.IsClosing;

    public Vector2D<int> FramebufferSize => _window.FramebufferSize;

    /// <summary>
    /// The window the interface is never laid out on fewer pixels than (OBS-2k), or zero for no cap.
    /// Set once by the composition root, off the figures, before the first frame is laid.
    /// </summary>
    public Vector2 LeastUiPx { get; set; }

    /// <summary>The display the window is on, by the name the desktop knows it by.</summary>
    public string DisplayName => _window.Monitor?.Name ?? "an unnamed display";

    /// <summary>
    /// How many of the display's own pixels the interface's pixel is worth: the ratio the platform
    /// already applies, capped by what the panels need (<see cref="InterfaceScale"/>) and overridden
    /// outright by <c>--ui-scale</c> — the way out where a platform reports 1 on a display nobody would
    /// call unscaled. Without it a 4K screen draws a 15-pixel label at a third of its designed size.
    /// The town is unaffected: the camera opens on a span in metres.
    /// </summary>
    public float UiScale => InterfaceScale.Fitted(
        _wantedUiScale,
        _window.Size.X > 0 ? (float)_window.FramebufferSize.X / _window.Size.X : 1f,
        new Vector2(_window.FramebufferSize.X, _window.FramebufferSize.Y),
        LeastUiPx);

    /// <summary>The window, in the pixels the interface and the camera are laid out in.</summary>
    public Vector2 UiPx => new Vector2(_window.FramebufferSize.X, _window.FramebufferSize.Y) / UiScale;

    /// <summary>The window's own Vulkan surface: the one place the graphics API and the window meet.</summary>
    public IVkSurface VkSurface =>
        _window.VkSurface ?? throw new InvalidOperationException("The window was not created for Vulkan.");

    /// <summary>Where the pointer is, <b>in interface pixels</b> — the space the interface is laid out in.</summary>
    public Vector2 PointerPx => _mouse is null ? Vector2.Zero : InUiPx(_mouse.Position);

    /// <summary>
    /// Where the fingers on the glass are, in interface pixels, and how many were written (CTL-9).
    /// <b>Always none here</b>: a desktop window has a mouse, and GLFW hands out no touches at all —
    /// so the two-finger gestures are the browser's and this answers the same question with a zero
    /// rather than with a second code path above it.
    /// </summary>
    public int Touches(Span<Vector2> intoPx) => 0;

    /// <summary>
    /// Opens the window, fullscreen unless <c>--windowed</c> asked otherwise, on the display
    /// <c>--display</c> names or the one the pointer is on when it names none. <c>--size W H</c> is
    /// still the size it restores to, since <c>F11</c> is a state and not a mode change.
    /// </summary>
    /// <remarks>
    /// It opens windowed either way and goes fullscreen after: the platform places a new window
    /// wherever it likes, so the display has to be chosen and moved to rather than asked for up front.
    /// </remarks>
    public static AppWindow Open(
        string title, int width, int height, float uiScale, bool fullscreen, string? display)
    {
        // GLFW rather than SDL: same vendor as the graphics bindings, one native asset at the boundary.
        Window.PrioritizeGlfw();

        var options = WindowOptions.DefaultVulkan;
        options.Title = title;
        options.Size = new Vector2D<int>(width, height);
        options.WindowState = WindowState.Normal;
        options.WindowBorder = WindowBorder.Resizable;
        options.VSync = true;

        var window = Window.Create(options);
        window.Initialize();
        var input = window.CreateInput();

        // Before the size is read for the first time, so the swapchain is built at the size it will be
        // drawn at rather than at the window's and rebuilt on the first frame.
        if (fullscreen)
        {
            var was = window.FramebufferSize;
            GoFullscreen(window, display is null ? PointerMonitor(window, input) : Display(window, display));
            Settle(window, was);
        }
        else if (display is not null)
        {
            // Short of fullscreen the monitor is a corner to sit in rather than a screen to fill.
            window.Monitor = Display(window, display);
        }

        return new AppWindow(window, input, uiScale, fullscreen);
    }

    /// <summary>
    /// Fullscreen on the display named, and on the one the window is already on when none is.
    /// </summary>
    /// <remarks>
    /// <b><see cref="WindowState.Fullscreen"/> on its own always takes the primary display</b>,
    /// whichever one the window was on. Setting the monitor afterwards is what moves it, and it is
    /// only a move once the window is already fullscreen.
    /// </remarks>
    static void GoFullscreen(IWindow window, IMonitor? on)
    {
        on ??= window.Monitor;
        window.WindowState = WindowState.Fullscreen;
        if (on is not null) window.Monitor = on;
    }

    /// <summary>
    /// The display <c>--display</c> names, by the desktop's own name for it or by its index. A name
    /// nobody offers is an error rather than a silent fallback: naming one at all says the guess
    /// underneath was wrong.
    /// </summary>
    static IMonitor Display(IWindow window, string named)
    {
        var offered = new List<string>();
        foreach (var monitor in Silk.NET.Windowing.Monitor.GetMonitors(window))
        {
            if (string.Equals(monitor.Name, named, StringComparison.OrdinalIgnoreCase) ||
                monitor.Index.ToString() == named)
                return monitor;

            offered.Add($"{monitor.Index} {monitor.Name}");
        }

        throw new ArgumentException($"Unknown display {named}. Takes {string.Join(", ", offered)}.");
    }

    /// <summary>
    /// The display the pointer is on, which is the display the run was started from.
    /// </summary>
    /// <remarks>
    /// The cursor is reported relative to the window even while it is somewhere else entirely, so the
    /// two added are a position on the desktop. <b>A session whose pointer is not global answers
    /// nothing useful</b> — a Wayland client is told where the cursor is only while it is over one of
    /// its own surfaces — and the desktop's own guess, which is where it placed the window, then
    /// stands instead.
    /// </remarks>
    static IMonitor? PointerMonitor(IWindow window, IInputContext input)
    {
        if (input.Mice.Count == 0) return null;

        var cursor = input.Mice[0].Position;
        var atPx = window.Position + new Vector2D<int>((int)cursor.X, (int)cursor.Y);
        foreach (var monitor in Silk.NET.Windowing.Monitor.GetMonitors(window))
            if (monitor.Bounds.Contains(atPx))
                return monitor;

        return null;
    }

    /// <summary>
    /// Waits out a change of window state, which the desktop answers with an event rather than on the
    /// spot: the size read before that answer arrives is still the size the window used to be.
    /// </summary>
    static void Settle(IWindow window, Vector2D<int> was)
    {
        for (var waitedMs = 0; waitedMs < SettleMs && window.FramebufferSize == was; waitedMs += SettleStepMs)
        {
            window.DoEvents();
            Thread.Sleep(SettleStepMs);
        }
    }

    /// <summary>One pump of the platform's event queue. Allocates nothing.</summary>
    public void PumpEvents() => _window.DoEvents();

    public bool IsKeyDown(Key key) => _keyboard?.IsKeyPressed(key) ?? false;

    public bool IsMouseDown(MouseButton button) => _mouse?.IsButtonPressed(button) ?? false;

    /// <summary>
    /// Whether a key went down since this was last asked, and asking clears it. Every key that means
    /// <em>toggle</em> is read this way: a settings panel opened by <c>IsKeyPressed</c> opens and
    /// shuts as fast as the loop runs.
    /// </summary>
    public bool TakePress(Key key)
    {
        var at = (int)key;
        if (at < 0 || at >= _pressed.Length || !_pressed[at]) return false;

        _pressed[at] = false;
        return true;
    }

    /// <summary>
    /// <c>F11</c>, on the display the window is on. It is the same window either way and the swapchain
    /// is rebuilt off the resize like any other.
    /// </summary>
    public void ToggleFullscreen()
    {
        _fullscreen = !_fullscreen;
        if (_fullscreen) GoFullscreen(_window, _window.Monitor);
        else _window.WindowState = WindowState.Normal;
    }

    /// <summary>The button pressed since this was last asked and where in interface pixels, if any. Asking clears it.</summary>
    public bool TakeClick(out MouseButton button, out Vector2 atPx)
    {
        button = _clicked ?? MouseButton.Unknown;
        atPx = _clickedAtPx;
        var clicked = _clicked is not null;
        _clicked = null;
        return clicked;
    }

    /// <summary>Notches since this was last asked, and asking clears them.</summary>
    public float TakeScroll()
    {
        var scroll = _scroll;
        _scroll = 0f;
        return scroll;
    }

    /// <summary>Whether the framebuffer changed size since this was last asked — the swapchain's cue.</summary>
    public bool TakeResized()
    {
        if (_window.FramebufferSize == _lastFramebufferSize) return false;

        _lastFramebufferSize = _window.FramebufferSize;
        return true;
    }

    public void Close() => _window.Close();

    public void Dispose()
    {
        _input.Dispose();
        _window.Dispose();
    }
}
