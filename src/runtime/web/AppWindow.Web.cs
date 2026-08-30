using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace TrafficSimulation.Runtime;

/// <summary>
/// The canvas and its input, driven by this engine's own loop — the browser's answer to the same
/// questions the desktop window answers, and the only file that knows a page is what the town is
/// standing in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Input is read out of memory rather than asked for.</b> The page's listeners write into two arrays
/// this class owns, which <c>town.js</c> holds a window onto for the life of the run, so a frame that
/// asks after twenty keys and the pointer crosses the wall no times at all. What a browser calls a
/// press is edge-triggered here exactly as the desktop's is: asking clears it, so a key that means
/// "toggle" cannot toggle sixty times a second while it is held.
/// </para>
/// <para>
/// <b>Three spaces meet here too</b>, and the arithmetic is the desktop's — <see cref="InUiPx"/> is
/// the same static method checked by the same test. The page hands the pointer over already in the
/// canvas's own pixels, so what is left is the scale.
/// </para>
/// </remarks>
internal sealed class AppWindow : IDisposable
{
    /// <summary>The keys the town reads, in the order <c>town.js</c> names them. The two lists are one list.</summary>
    const int Keys = 32;

    const int Down = 0;
    const int Pressed = Keys;
    const int Buttons = Keys * 2;

    const int PointerX = 0;
    const int PointerY = 1;
    const int Scroll = 2;
    const int ClickX = 3;
    const int ClickY = 4;
    const int ClickButton = 5;
    const int Width = 6;
    const int Height = 7;
    const int Scale = 8;
    const int Resized = 9;
    const int Axes = 10;

    readonly byte[] _keys = new byte[(Keys * 2) + 8];
    readonly double[] _axes = new double[Axes];
    readonly float _wantedUiScale;

    /// <summary>
    /// <b>Not an axis, and this is the whole reason it is a field.</b> Every axis is what the page saw,
    /// so a pump overwrites the lot of them from the page's own copy — and the page has no opinion
    /// about whether the run is over, so an axis holding that would be set by the menu and cleared by
    /// the very next frame. A latch here is set once and by nothing else, which is what makes the way
    /// out of the game work at all.
    /// </summary>
    bool _closing;

    public AppWindow(float wantedUiScale)
    {
        _wantedUiScale = wantedUiScale;

        // Once before the first frame, so the canvas has a size and a scale by the time the camera and
        // the interface are laid out against them.
        PumpEvents();
    }

    public bool IsClosing => _closing;

    public Vector2D<int> FramebufferSize => new((int)_axes[Width], (int)_axes[Height]);

    /// <summary>What a browser can say about where it is drawing, which is the display's density and no name.</summary>
    public string DisplayName => "the browser";

    /// <summary>
    /// How many of the display's own pixels the interface's pixel is worth: the page's own device
    /// pixel ratio, unless <c>ui-scale</c> named one.
    /// </summary>
    public float UiScale => _wantedUiScale > 0f ? _wantedUiScale : Math.Max(0.1f, (float)_axes[Scale]);

    /// <summary>The canvas, in the pixels the interface and the camera are laid out in.</summary>
    public Vector2 UiPx => new Vector2((float)_axes[Width], (float)_axes[Height]) / UiScale;

    /// <summary>Where the pointer is, <b>in interface pixels</b> — the space the interface is laid out in.</summary>
    public Vector2 PointerPx => InUiPx(new Vector2((float)_axes[PointerX], (float)_axes[PointerY]));

    /// <summary>The conversion itself, as arithmetic over three vectors and a factor, so it can be checked without a page.</summary>
    public static Vector2 InUiPx(
        Vector2 windowPx, Vector2D<int> windowSize, Vector2D<int> framebufferSize, float uiScale)
    {
        if (windowSize.X <= 0 || windowSize.Y <= 0 || uiScale <= 0f) return windowPx;

        return new Vector2(
            windowPx.X * framebufferSize.X / windowSize.X, windowPx.Y * framebufferSize.Y / windowSize.Y) / uiScale;
    }

    /// <summary>
    /// What the page has seen since the last frame asked. The edges — a press, a click, a notch, a
    /// resize — are handed over and forgotten by the page here, and cleared again as each is read, so
    /// each of them is answered exactly once.
    /// </summary>
    public void PumpEvents() => WebGpu.Pump(_keys, _axes);

    public bool IsKeyDown(Key key) => _keys[Down + Index(key)] != 0;

    public bool IsMouseDown(MouseButton button) => _keys[Buttons + Index(button)] != 0;

    /// <summary>Whether a key went down since this was last asked, and asking clears it.</summary>
    public bool TakePress(Key key)
    {
        var at = Pressed + Index(key);
        var pressed = _keys[at] != 0;
        _keys[at] = 0;
        return pressed;
    }

    /// <summary>The button pressed since this was last asked and where in interface pixels, and asking clears it.</summary>
    public bool TakeClick(out MouseButton button, out Vector2 atPx)
    {
        var pressed = (int)_axes[ClickButton];
        _axes[ClickButton] = -1;
        button = pressed switch
        {
            0 => MouseButton.Left,
            1 => MouseButton.Middle,
            2 => MouseButton.Right,
            _ => MouseButton.Unknown,
        };

        atPx = InUiPx(new Vector2((float)_axes[ClickX], (float)_axes[ClickY]));
        return pressed >= 0;
    }

    /// <summary>Notches since this was last asked, and asking clears them.</summary>
    public float TakeScroll()
    {
        var scrolled = (float)_axes[Scroll];
        _axes[Scroll] = 0;
        return scrolled;
    }

    /// <summary>Whether the canvas changed size since this was last asked — the one thing that makes a recording stale.</summary>
    public bool TakeResized()
    {
        var resized = _axes[Resized] != 0;
        _axes[Resized] = 0;
        return resized;
    }

    public void ToggleFullscreen() => WebGpu.Fullscreen();

    /// <summary>
    /// The way out of the game (OBS-2g). A page is not closed by anything in here — a tab belongs to
    /// the person looking at it — so what this does is stop the run: the next
    /// <see cref="TrafficSimulation.App.Main.Game.Step"/> returns without drawing, the browser is not
    /// asked for another frame, and the boot says so under the canvas.
    /// </summary>
    public void Close() => _closing = true;

    public void Dispose() => Close();

    Vector2 InUiPx(Vector2 canvasPx) => InUiPx(canvasPx, FramebufferSize, FramebufferSize, UiScale);

    /// <summary>
    /// The town's keys, as <c>town.js</c> indexes them. A key the page does not name is a key nothing
    /// reads, which is why the default is a slot no listener ever writes.
    /// </summary>
    static int Index(Key key) => key switch
    {
        Key.A => 0,
        Key.D => 1,
        Key.S => 2,
        Key.W => 3,
        Key.E => 4,
        Key.R => 5,
        Key.Up => 6,
        Key.Down => 7,
        Key.Left => 8,
        Key.Right => 9,
        Key.Escape => 10,
        Key.F11 => 11,
        Key.GraveAccent => 12,
        Key.Pause => 13,
        Key.Space => 14,
        Key.Number1 => 15,
        Key.Number2 => 16,
        Key.Number3 => 17,
        Key.ShiftLeft => 18,
        Key.ShiftRight => 19,
        _ => Keys - 1,
    };

    static int Index(MouseButton button) => button switch
    {
        MouseButton.Left => 0,
        MouseButton.Middle => 1,
        MouseButton.Right => 2,
        _ => 7,
    };
}
