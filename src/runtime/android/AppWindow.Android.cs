using System.Numerics;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace TrafficSimulation.Runtime;

/// <summary>
/// The activity's glass and the fingers on it, driven by this engine's own loop — the handset's answer
/// to the same questions the desktop window and the browser canvas answer, and the only file in
/// <c>runtime/</c> that knows the town is being held.
/// </summary>
/// <remarks>
/// <para>
/// <b>The glass arrives before the run and the run never asks for it.</b> The activity is the window
/// here: it takes its own surface, acquires the <c>ANativeWindow</c>, says how big it is and how dense
/// the display is (<see cref="Shows"/>), and only then starts the thread the loop runs on. So the
/// handoff is a few statics written by one thread and read by another, and <see cref="OnTheGlass"/> is
/// what turns them into the window the composition root holds.
/// </para>
/// <para>
/// <b>Input is written where the frame will read it, exactly as the page's is</b> (AND-6). The touch listener
/// runs on the activity's thread and writes these fields; the frame reads them and clears the edges — a
/// press, a tap, a resize — so each is answered once. A finger that is still down is state and is read
/// as state. <see cref="PumpEvents"/> therefore has nothing to do: there is no queue on this side of
/// the boundary to drain.
/// </para>
/// <para>
/// <b>Two spaces meet here rather than three.</b> A touch arrives in the surface's own pixels, which is
/// the framebuffer's, so what is left is the scale — and that is <see cref="InUiPx"/>, the desktop's own
/// arithmetic checked by the desktop's own test.
/// </para>
/// </remarks>
internal sealed class AppWindow : IDisposable
{
    /// <summary>The <c>ANativeWindow*</c> the activity acquired, or zero when there is no glass to draw on.</summary>
    static nint _glass;

    static int _widthPx;
    static int _heightPx;

    /// <summary>How many of the display's own pixels there are to the point, which is what a handset reports.</summary>
    static float _density = 1f;

    /// <summary>
    /// The fingers on the glass, in the order they came down, of which the run reads the first two
    /// (CTL-9). <b>Ordering is the activity's</b>: a finger lifted out of the middle of three must not
    /// renumber the others, and the listener is the only place that knows which came first.
    /// </summary>
    static Vector2 _firstFingerPx;

    static Vector2 _secondFingerPx;
    static volatile int _fingers;

    static Vector2 _pointerPx;
    static Vector2 _clickedAtPx;

    /// <summary>Which button the tap counted as, or <c>-1</c> for none. Left, always: a finger is one button.</summary>
    static volatile int _clicked = -1;

    static volatile bool _pressing;
    static volatile bool _escaped;
    static volatile bool _resized;

    /// <summary>
    /// Whether the glass has gone — the activity paused, or the system took the surface back. <b>It ends
    /// the run</b> (AND-7): the device presents to a window that no longer exists, so the next
    /// <c>Step</c> returns without drawing and the loop unwinds.
    /// </summary>
    static volatile bool _lost;

    /// <summary>The way out of the game (OBS-2g), which on a handset is the menu's own and not a window's.</summary>
    static volatile bool _closed;

    readonly float _wantedUiScale;
    readonly AndroidSurface _surface;

    AppWindow(float wantedUiScale, nint glass)
    {
        _wantedUiScale = wantedUiScale;
        _surface = new AndroidSurface(glass);
    }

    /// <summary>
    /// The window the town is about to be drawn in, over the glass the activity is already holding.
    /// Called from the loop's own thread, once per run.
    /// </summary>
    public static AppWindow OnTheGlass(float wantedUiScale)
    {
        if (_glass == 0)
            throw new InvalidOperationException("There is no glass: the activity starts the run once it has a surface.");

        _closed = false;
        _lost = false;
        return new AppWindow(wantedUiScale, _glass);
    }

    /// <summary>
    /// The glass, as the activity hands it over: the window it acquired, the pixels across and down, and
    /// the display's own density. <b>Before the run starts</b>, so the swapchain is built at the size it
    /// will be drawn at and the interface is laid out on the density it will be read at.
    /// </summary>
    public static void Shows(nint glass, int widthPx, int heightPx, float density)
    {
        _glass = glass;
        _widthPx = widthPx;
        _heightPx = heightPx;
        _density = density > 0f ? density : 1f;
        _resized = false;
    }

    /// <summary>The glass, at its new size — a rotation, or a system bar coming and going. The swapchain's cue.</summary>
    public static void Resized(int widthPx, int heightPx)
    {
        if (widthPx == _widthPx && heightPx == _heightPx) return;

        _widthPx = widthPx;
        _heightPx = heightPx;
        _resized = true;
    }

    /// <summary>
    /// The glass, taken back. The run ends and the window is dropped: what the activity does with the
    /// thread and with the surface itself is the activity's (AND-7).
    /// </summary>
    public static void Lost()
    {
        _lost = true;
        _glass = 0;
        _fingers = 0;
        _pressing = false;
    }

    /// <summary>Whether the run has let go of the glass, so the activity may release the window it acquired.</summary>
    public static bool Standing => !_lost && !_closed;

    /// <summary>
    /// A finger down, in the surface's own pixels. <b>Only the first is a button</b> (CTL-9): a second
    /// one landing is the camera's, and counted as a press it would be a second tap on whatever it came
    /// down on.
    /// </summary>
    public static void Pressed(Vector2 atPx)
    {
        // A tap has no move before it, so the pointer goes where the press was: the run resolves what was
        // tapped against the pointer as much as against the click.
        _pointerPx = atPx;
        _clickedAtPx = atPx;
        _clicked = 0;
        _pressing = true;
    }

    /// <summary>The first finger, moved. What a drag is made of, and the same event a mouse move is.</summary>
    public static void Moved(Vector2 atPx) => _pointerPx = atPx;

    /// <summary>The first finger, lifted. The button goes up; where it was stays where it was.</summary>
    public static void Lifted() => _pressing = false;

    /// <summary>
    /// How many fingers are down and where the first two are, in the surface's own pixels. Written on
    /// every change rather than once a frame, because a frame reads it and does not ask for it.
    /// </summary>
    public static void Fingers(int count, Vector2 firstPx, Vector2 secondPx)
    {
        _firstFingerPx = firstPx;
        _secondFingerPx = secondPx;
        _fingers = count;
    }

    /// <summary>
    /// The system's way back, which is this town's <c>Escape</c> (OBS-2g): it opens and shuts the menu,
    /// and the menu is what leaving the game is done from. <b>A handset has no keyboard and nothing above
    /// this file learns that</b> — the one key it can send is the one the interface already answers.
    /// </summary>
    public static void Back() => _escaped = true;

    public bool IsClosing => _lost || _closed;

    public Vector2D<int> FramebufferSize => new(_widthPx, _heightPx);

    /// <summary>What a handset can say about where it is drawing, which is the density and no name.</summary>
    public string DisplayName => $"the glass at {_density:F1} device pixels to the point";

    /// <summary>
    /// The glass the interface is never laid out on fewer pixels than (OBS-2k), or zero for no cap. Set
    /// once by the composition root, off the figures, before the first frame is laid.
    /// </summary>
    public Vector2 LeastUiPx { get; set; }

    /// <summary>
    /// How many of the display's own pixels the interface's pixel is worth: the display's density,
    /// capped by what the panels need (<see cref="InterfaceScale"/>) and overridden outright by
    /// <c>ui-scale</c>. <b>The cap is what a handset is for</b>: three device pixels to the point over
    /// 1080 of them across is 360 for the interface, which is narrower than the menu.
    /// </summary>
    public float UiScale => InterfaceScale.Fitted(
        _wantedUiScale, _density, new Vector2(_widthPx, _heightPx), LeastUiPx);

    /// <summary>The glass, in the pixels the interface and the camera are laid out in.</summary>
    public Vector2 UiPx => new Vector2(_widthPx, _heightPx) / UiScale;

    /// <summary>The surface Vulkan presents to, which is the activity's own window.</summary>
    public IVkSurface VkSurface => _surface;

    /// <summary>Where the first finger is, <b>in interface pixels</b> — the space the interface is laid out in.</summary>
    public Vector2 PointerPx => InUiPx(_pointerPx);

    /// <summary>
    /// Where the fingers on the glass are, in interface pixels, and how many were written (CTL-9). <b>The
    /// listener's own count and not the run's</b> — a finger is on the glass or it is not, and what two of
    /// them at once mean is read from these two positions a frame apart
    /// (<see cref="TrafficSimulation.App.PlayerControl.TouchGesture"/>).
    /// </summary>
    public int Touches(Span<Vector2> intoPx)
    {
        var count = Math.Min(_fingers, intoPx.Length);
        if (count > 0) intoPx[0] = InUiPx(_firstFingerPx);
        if (count > 1) intoPx[1] = InUiPx(_secondFingerPx);

        return count;
    }

    /// <summary>The conversion itself, as arithmetic over three vectors and a factor, so it can be checked without a handset.</summary>
    public static Vector2 InUiPx(
        Vector2 windowPx, Vector2D<int> windowSize, Vector2D<int> framebufferSize, float uiScale)
    {
        if (windowSize.X <= 0 || windowSize.Y <= 0 || uiScale <= 0f) return windowPx;

        return new Vector2(
            windowPx.X * framebufferSize.X / windowSize.X, windowPx.Y * framebufferSize.Y / windowSize.Y) / uiScale;
    }

    /// <summary>
    /// Nothing. The listener writes where the frame reads, so there is no queue on this side to drain —
    /// and the edges are cleared as each is taken rather than at the top of a frame.
    /// </summary>
    public void PumpEvents()
    {
    }

    /// <summary>Never down: a handset has no keyboard, and the one key it sends is edge-triggered (<see cref="TakePress"/>).</summary>
    public bool IsKeyDown(Key key) => false;

    public bool IsMouseDown(MouseButton button) => button == MouseButton.Left && _pressing;

    /// <summary>Whether a key went down since this was last asked, and asking clears it.</summary>
    public bool TakePress(Key key)
    {
        if (key != Key.Escape || !_escaped) return false;

        _escaped = false;
        return true;
    }

    /// <summary>The tap since this was last asked and where in interface pixels, if any. Asking clears it.</summary>
    public bool TakeClick(out MouseButton button, out Vector2 atPx)
    {
        var tapped = _clicked >= 0;
        _clicked = -1;
        button = tapped ? MouseButton.Left : MouseButton.Unknown;
        atPx = InUiPx(_clickedAtPx);
        return tapped;
    }

    /// <summary>
    /// Nothing. A wheel is a mouse's; a handset zooms by pinching, which is two fingers and is read as a
    /// gesture rather than as notches (CTL-9).
    /// </summary>
    public float TakeScroll() => 0f;

    /// <summary>Whether the glass changed size since this was last asked — the swapchain's cue.</summary>
    public bool TakeResized()
    {
        if (!_resized) return false;

        _resized = false;
        return true;
    }

    /// <summary>Nothing to toggle: a run on a handset is fullscreen and there is no second state for it to be in.</summary>
    public void ToggleFullscreen()
    {
    }

    /// <summary>
    /// The way out of the game (OBS-2g). It stops the run — the next
    /// <see cref="TrafficSimulation.App.Main.Game.Step"/> returns without drawing — and the activity
    /// finishes once the loop has unwound, because a process left standing with no glass is a town
    /// nobody can see.
    /// </summary>
    public void Close() => _closed = true;

    public void Dispose() => Close();

    Vector2 InUiPx(Vector2 surfacePx) => InUiPx(surfacePx, FramebufferSize, FramebufferSize, UiScale);
}
