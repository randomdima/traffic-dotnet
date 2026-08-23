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

    AppWindow(IWindow window, IInputContext input, float wantedUiScale)
    {
        _window = window;
        _input = input;
        _wantedUiScale = wantedUiScale;
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
    /// How many of the display's own pixels the interface's pixel is worth: the ratio the platform
    /// already applies, unless <c>--ui-scale</c> named one — the way out where a platform reports 1 on
    /// a display nobody would call unscaled. Without it a 4K screen draws a 15-pixel label at a third
    /// of its designed size. The town is unaffected: the camera opens on a span in metres.
    /// </summary>
    public float UiScale => _wantedUiScale > 0f
        ? _wantedUiScale
        : _window.Size.X > 0 ? (float)_window.FramebufferSize.X / _window.Size.X : 1f;

    /// <summary>The window, in the pixels the interface and the camera are laid out in.</summary>
    public Vector2 UiPx => new Vector2(_window.FramebufferSize.X, _window.FramebufferSize.Y) / UiScale;

    /// <summary>The window's own Vulkan surface: the one place the graphics API and the window meet.</summary>
    public IVkSurface VkSurface =>
        _window.VkSurface ?? throw new InvalidOperationException("The window was not created for Vulkan.");

    /// <summary>Where the pointer is, <b>in interface pixels</b> — the space the interface is laid out in.</summary>
    public Vector2 PointerPx => _mouse is null ? Vector2.Zero : InUiPx(_mouse.Position);

    public static AppWindow Open(string title, int width, int height, float uiScale)
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
        return new AppWindow(window, window.CreateInput(), uiScale);
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
    /// <c>F11</c>. Borderless-fullscreen rather than a mode change. The window never <em>opens</em>
    /// fullscreen: a run launched that way cannot be looked at beside anything.
    /// </summary>
    public void ToggleFullscreen()
    {
        _fullscreen = !_fullscreen;
        _window.WindowState = _fullscreen ? WindowState.Fullscreen : WindowState.Normal;
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
