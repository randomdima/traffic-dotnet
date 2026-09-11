using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using TrafficSimulation.App.Main;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Runtime;

namespace TrafficSimulation.App.Android;

/// <summary>
/// The whole of the bootstrap (AND-2): one activity that takes its own window's surface, hands the
/// native window to the machine and runs this engine's own loop on a thread of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no view and no layout.</b> <c>Window.TakeSurface</c> is what a native activity does, and
/// it gives the run the activity's whole window with no view hierarchy over it — so there is no Android
/// widget anywhere in this build, no measure pass, and nothing between the glass and the swapchain.
/// Touches reach the activity itself for the same reason: with no view to consume them, the window hands
/// them here.
/// </para>
/// <para>
/// <b>The loop is on a thread of its own and the surface belongs to this one.</b> The lifecycle
/// callbacks are the activity's thread, the frame is the loop's, and the one thing that crosses between
/// them is <see cref="AppWindow"/>'s handoff. A destroyed surface is waited out rather than raced
/// (AND-7): the run is told to stop and joined before the native window is released, because a device
/// presenting to a window that has been given back is the one way this crashes in the platform's own
/// code.
/// </para>
/// </remarks>
[Activity(
    // Named, rather than left to the generated wrapper's hash of a hash: it is what `adb shell am
    // start` has to be handed, and a name that moves when the class does is a command that stops
    // working for reasons nobody can see.
    Name = "dev.trafficdotnet.town.TownActivity",
    Label = "traffic-dotnet",
    MainLauncher = true,
    Exported = true,
    Theme = "@android:style/Theme.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout |
                           ConfigChanges.Density | ConfigChanges.KeyboardHidden)]
// Public, alone in this build: the platform instantiates it through a generated Java wrapper, which is
// the one caller in the project that is not in the project.
public sealed partial class TownActivity : Activity, ISurfaceHolderCallback2
{
    /// <summary>The logcat tag every line of a run comes out under: <c>adb logcat -s town</c> is the read-out.</summary>
    internal const string Tag = "town";

    /// <summary>How long the loop is given to unwind once the surface has gone, before the window is released anyway.</summary>
    const int UnwindMs = 3000;

    /// <summary>The fingers on the glass by the id the platform gave each, in the order they came down (CTL-9).</summary>
    readonly int[] _fingers = new int[10];

    int _down;

    /// <summary>The <c>ANativeWindow</c> this activity acquired, or zero when it holds none.</summary>
    nint _glass;

    Thread? _run;
    int _widthPx;
    int _heightPx;

    /// <summary>What the intent asked for (AND-5) — the words are the desktop's.</summary>
    string? _map;

    string[] _ui = [];
    float _uiScale;
    double _seconds;

    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);

        Console.SetOut(new LogWriter());

        // AND-5: the extras are the command line. `adb shell am start -n dev.trafficdotnet.town/... -e
        // map Odesa -e ui nodes,paths` is `--map Odesa --ui nodes,paths`.
        _map = Intent?.GetStringExtra("map");
        _ui = (Intent?.GetStringExtra("ui") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _uiScale = Figure(Intent?.GetStringExtra("ui-scale"));
        _seconds = Figure(Intent?.GetStringExtra("seconds"));

        var window = Window ?? throw new InvalidOperationException("An activity with no window cannot hold a town.");
        window.AddFlags(WindowManagerFlags.KeepScreenOn);
        window.InsetsController?.Hide(WindowInsets.Type.SystemBars());

        // The glass itself, asked for without a view to put it in.
        window.TakeSurface(this);
    }

    public void SurfaceCreated(ISurfaceHolder holder)
    {
        // Nothing yet: a surface has no size until it is changed to one, and the swapchain is built at
        // the size the first frame will be drawn at rather than rebuilt on it.
    }

    public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] global::Android.Graphics.Format format, int width, int height)
    {
        _widthPx = width;
        _heightPx = height;

        if (_glass == 0) Stand(holder);
        else AppWindow.Resized(width, height);
    }

    public void SurfaceDestroyed(ISurfaceHolder holder)
    {
        AppWindow.Lost();

        // Waited out on the activity's thread, which is what the platform's contract asks for: past this
        // call the surface may be gone, so the run has to have stopped drawing to it first.
        if (_run is { } running)
        {
            running.Join(UnwindMs);
            _run = null;
        }

        if (_glass == 0) return;

        Release(_glass);
        _glass = 0;
    }

    /// <summary>Nothing to redraw on demand: this engine draws every frame and owns its own loop.</summary>
    public void SurfaceRedrawNeeded(ISurfaceHolder holder)
    {
    }

    /// <summary>
    /// The way back, which is this town's <c>Escape</c> (OBS-2g): it opens and shuts the menu, and
    /// leaving the game is done from there rather than by the key itself. <b>Not
    /// <c>OnBackPressed</c></b>, which is the platform's way of finishing an activity and would take the
    /// town down on the way to the menu.
    /// </summary>
    public override bool OnKeyDown([GeneratedEnum] Keycode keyCode, KeyEvent? e)
    {
        if (keyCode != Keycode.Back) return base.OnKeyDown(keyCode, e);

        AppWindow.Back();
        return true;
    }

    /// <summary>
    /// The fingers, in the platform's own terms and in the browser head's order (CTL-9): the first one
    /// down is the pointer and the button, and the rest are the camera's. The positions are the window's
    /// own pixels, which — with no view over it — is the surface's.
    /// </summary>
    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null) return false;

        var at = e.ActionIndex;
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
            case MotionEventActions.PointerDown:
                Came(e.GetPointerId(at));
                if (Primary(e.GetPointerId(at))) AppWindow.Pressed(At(e, at));
                break;

            case MotionEventActions.Move:
                if (_down > 0 && e.FindPointerIndex(_fingers[0]) is var first && first >= 0)
                    AppWindow.Moved(At(e, first));
                break;

            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
                var lifted = e.GetPointerId(at);
                var wasPrimary = Primary(lifted);
                Went(lifted);
                if (wasPrimary) AppWindow.Lifted();
                break;

            // A gesture the system took over — an edge swipe, a call arriving — is fingers that never
            // come up, and a town left mid-pinch by one.
            case MotionEventActions.Cancel:
                _down = 0;
                AppWindow.Lifted();
                break;

            default:
                return base.OnTouchEvent(e);
        }

        Write(e);
        return true;
    }

    /// <summary>
    /// The native window behind the activity's surface. <b>The one call in this build that needs a JNI
    /// environment</b>, which is why it is here and not in <c>runtime/</c>: what crosses down to the
    /// machine is a pointer (<see cref="AndroidSurface"/>).
    /// </summary>
    [LibraryImport("android", EntryPoint = "ANativeWindow_fromSurface")]
    private static partial nint FromSurface(nint env, nint surface);

    [LibraryImport("android", EntryPoint = "ANativeWindow_release")]
    private static partial void Release(nint window);

    /// <summary>One position, in the surface's own pixels.</summary>
    static Vector2 At(MotionEvent e, int index) => new(e.GetX(index), e.GetY(index));

    /// <summary>A figure an extra carried, or zero for one it did not — the same default as the words' own.</summary>
    static float Figure(string? said) =>
        float.TryParse(said, NumberStyles.Float, CultureInfo.InvariantCulture, out var figure) ? figure : 0f;

    /// <summary>
    /// The glass acquired and the run started on it. What the loop needs before its first frame is a
    /// window, a size and a density, and all three are read here (<see cref="AppWindow.Shows"/>).
    /// </summary>
    void Stand(ISurfaceHolder holder)
    {
        var surface = holder.Surface ?? throw new InvalidOperationException("The holder handed over no surface.");
        _glass = FromSurface(JNIEnv.Handle, surface.Handle);
        if (_glass == 0)
        {
            Log.Error(Tag, "the activity's surface gave no native window, so there is nothing to draw on");
            Finish();
            return;
        }

        AppWindow.Shows(_glass, _widthPx, _heightPx, Resources?.DisplayMetrics?.Density ?? 1f);
        _run = new Thread(Drive) { Name = Tag, IsBackground = false };
        _run.Start();
    }

    /// <summary>
    /// The run itself: the town's files unpacked, the figures read, and this engine's own loop until the
    /// glass goes or the menu is left through. <b>The same three lines <c>Program.Main</c> ends with</b>,
    /// and the loop is <see cref="Game.Step"/>'s on either head — <b>and only those three</b>: the shot,
    /// the sheet and the probes are the words this head does not offer (AND-4).
    /// </summary>
    void Drive()
    {
        try
        {
            Papers.Unpack(Assets!, FilesDir!.AbsolutePath, Install());

            // The only place the figures are read, exactly as on the desktop.
            var config = SimConfig.Load();

            // GEN-1b: with no map named the game opens on the start menu with the idle ring behind it and
            // no city until one is picked. An extra naming one is that choice made earlier.
            using var game = new Game(
                config, _widthPx, _heightPx, validate: false, _uiScale, Pacing.Fifo,
                fullscreen: true, display: null);

            game.Switch(_ui);
            Log.Info(Tag, $"the town kept its claims: {game.Run(_map, _seconds) == 0}");
        }
        catch (Exception broke)
        {
            Log.Error(Tag, broke.ToString());
        }
        finally
        {
            // A run that has ended is an activity with nothing to show: the glass is the only thing this
            // process was standing up (AND-7).
            RunOnUiThread(Finish);
        }
    }

    /// <summary>
    /// What the folder was unpacked from, as the APK's own file says it: a reinstall or an upgrade writes
    /// it again and is the one thing that makes the unpacked tree stale.
    /// </summary>
    string Install()
    {
        var apk = new FileInfo(ApplicationInfo!.SourceDir!);
        return $"{apk.Length} {apk.LastWriteTimeUtc:O}";
    }

    void Came(int id)
    {
        if (_down < _fingers.Length) _fingers[_down++] = id;
    }

    bool Primary(int id) => _down > 0 && _fingers[0] == id;

    /// <summary>
    /// One finger gone, and the others left in the order they came down: a finger lifted out of the
    /// middle of three must not renumber the pair the camera is reading.
    /// </summary>
    void Went(int id)
    {
        for (var at = 0; at < _down; at++)
        {
            if (_fingers[at] != id) continue;

            for (var next = at; next < _down - 1; next++) _fingers[next] = _fingers[next + 1];
            _down--;
            return;
        }
    }

    /// <summary>How many are down and where the first two are, written on every change rather than once a frame.</summary>
    void Write(MotionEvent e)
    {
        var first = Vector2.Zero;
        var second = Vector2.Zero;

        if (_down > 0 && e.FindPointerIndex(_fingers[0]) is var one && one >= 0) first = At(e, one);
        if (_down > 1 && e.FindPointerIndex(_fingers[1]) is var two && two >= 0) second = At(e, two);

        AppWindow.Fingers(_down, first, second);
    }
}
