using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Bench;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Hud;

/// <summary>What a frame of the interface is drawn against — the run's state, gathered once so nothing reaches for it.</summary>
internal readonly ref struct InterfaceFrame
{
    public required TownWorld? World { get; init; }

    public required SimConfig Config { get; init; }

    public required Camera.Camera2D Camera { get; init; }

    /// <summary>
    /// The window in <b>interface pixels</b>, which is the space every panel here is laid out in and
    /// the space the pointer arrives in. On a scaled desktop it is not the framebuffer: that is the
    /// display's own pixels, and the only things measured in those are the swapchain and the viewport
    /// (<see cref="Runtime.AppWindow.UiScale"/>).
    /// </summary>
    public required Vector2 UiPx { get; init; }

    public required Vector2 PointerPx { get; init; }

    public string MapName { get; init; }

    public ulong WorldSeed { get; init; }

    public ulong AgentSeed { get; init; }

    public long Tick { get; init; }

    /// <summary>
    /// What the frame cost and where the tick went, <b>as the meter's own window read them</b> rather
    /// than as the frame being drawn — <see cref="FrameMeter"/>. A path that takes no windows, the
    /// offscreen one, leaves the timings at zero and fills in the phases alone.
    /// </summary>
    public FrameFigures Frame { get; init; }

    public long Crossings { get; init; }

    /// <summary>
    /// What the proving ground's own instrument has seen so far, or <see langword="null"/> on every other
    /// map. <b>It is the run's and not the panel's</b> — the figures are gathered every tick and a panel
    /// draws once a frame, so a panel that kept its own would be reading a fraction of the laps.
    /// </summary>
    public TrackMetrics? Track { get; init; }
}

/// <summary>Where a click landed once the interface has had it, so the town is not also clicked through a panel.</summary>
internal enum ClickTaken
{
    /// <summary>Nothing on the interface was under it, so it belongs to the town.</summary>
    No,

    /// <summary>A panel took it — either it acted on it or it swallowed it, and either way the town does not see it.</summary>
    Yes,

    /// <summary>The gear, which is the one piece of furniture that opens the menu.</summary>
    Gear,
}

/// <summary>
/// The whole interface, in the order it is drawn: the debug layers under it, the panels over them,
/// and the scale legend last so that nothing is drawn on top of the one thing that may have nothing
/// behind it.
/// </summary>
/// <remarks>
/// <b>It is one pass over one buffer</b>, which is what makes a panel opening cost a different number
/// in an indirect buffer and nothing else. It is also why the shot path and the windowed game share
/// it: a reference frame taken through a different drawing path would be a picture of that path.
/// </remarks>
internal sealed class Interface
{
    /// <summary>The one panel: the map to open, the switches, the seeds, the pace and the legend are its pages.</summary>
    public Menu Menu { get; } = new();

    public Hud Hud { get; } = new();

    /// <summary>What the frame and the tick cost, in collapsible sections. A switch rather than furniture.</summary>
    public FrameReadout Readout { get; } = new();

    public DebugSwitches Switches { get; } = new();

    public DebugOverlay Overlay { get; } = new();

    public Ruler Ruler { get; } = new();

    /// <summary>The proving ground's figures, one collapsible section per shape. A switch rather than furniture.</summary>
    public TrackPanel Track { get; } = new();

    public RunState Run { get; } = new();

    /// <summary>
    /// <c>--ui</c>, as the switches and pages it names. <b>One list, read by the shot path and by the
    /// windowed run alike</b> — a word that opened a layer in a picture and did nothing in the game
    /// would be two vocabularies, and the words are the switches' and the menu's own so that a script
    /// reads the same as what is on the screen.
    /// </summary>
    /// <remarks>
    /// The words are matched <b>whole</b> rather than as substrings: <c>menu-run</c> is the menu over a
    /// town and <c>menu</c> is the menu with none loaded, which a substring test cannot tell apart. A
    /// word nobody offers is an error rather than a silent fallback.
    /// </remarks>
    public void Apply(string[] wanted)
    {
        foreach (var name in wanted)
        {
            switch (name)
            {
                case "none":
                    break;
                case "menu":
                case "menu-run":
                    Menu.Show();
                    break;
                case "menu-scenarios":
                    Menu.Show();
                    Menu.OpenAt(Menu.Scenarios);
                    break;
                case "menu-checks":
                    Menu.Show();
                    Menu.OpenAt(Menu.Checks);
                    break;
                case "menu-layers":
                    Menu.Show();
                    Menu.OpenAt(Menu.Layers);
                    break;
                case "menu-seeds":
                    Menu.Show();
                    Menu.OpenAt(Menu.Seeds);
                    break;
                case "menu-pace":
                    Menu.Show();
                    Menu.OpenAt(Menu.Pace);
                    break;
                case "menu-controls":
                    Menu.Show();
                    Menu.OpenAt(Menu.Controls);
                    break;
                case "frame":
                    Switches.Toggle(ref Switches.FrameReadout);
                    break;
                case "car-lines":
                    Switches.Toggle(ref Switches.CarLines);
                    break;
                case "walker-lines":
                    Switches.Toggle(ref Switches.WalkerLines);
                    break;
                case "nodes":
                    Switches.Toggle(ref Switches.Nodes);
                    break;
                case "reservations":
                    Switches.Toggle(ref Switches.Reservations);
                    break;
                case "collision":
                    Switches.Toggle(ref Switches.Collision);
                    break;
                case "ruler":
                    Switches.Toggle(ref Switches.Ruler);
                    break;
                case "track":
                    Switches.Toggle(ref Switches.TrackFigures);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown --ui switch {name}. Takes none, menu, menu-scenarios, menu-checks, menu-layers, " +
                        "menu-seeds, menu-pace, menu-controls, menu-run, frame, car-lines, walker-lines, nodes, " +
                        "reservations, collision, ruler, track.");
            }
        }
    }

    /// <summary>
    /// A click over a running town, offered to the interface before the town underneath it.
    /// <b>A panel that is drawn over the town takes the clicks that land on it</b> — a read-out whose
    /// figures could be clicked through was a read-out that selected whatever car was behind it.
    /// </summary>
    public ClickTaken Click(Vector2 atPx)
    {
        // The gear is the only piece of furniture a click acts on; everything else the interface
        // offers is a page of the menu itself.
        if (Hud.Gear.Contains(atPx)) return ClickTaken.Gear;

        if (Switches.FrameReadout && Readout.Click(atPx)) return ClickTaken.Yes;

        return Switches.TrackFigures && Track.Click(atPx) ? ClickTaken.Yes : ClickTaken.No;
    }

    /// <summary>The town has changed under everything that held a place in it.</summary>
    public void TownChanged()
    {
        Overlay.TownChanged();
        Ruler.TownChanged();
        Track.TownChanged();
        Menu.Shut();
    }

    /// <summary>Everything the interface draws this frame, and how many quads it came to.</summary>
    /// <param name="under">
    /// Where the town's own ground marks go — a buffer of its own because it is drawn <em>before</em> the
    /// bodies. Everything else is written into <paramref name="into"/> and drawn after them.
    /// </param>
    /// <param name="underWritten">How many ground marks were written, which is that draw's own count.</param>
    public int Draw(Span<OverlayQuad> into, Span<OverlayQuad> under, in InterfaceFrame frame, out int underWritten)
    {
        var draw = new ScreenDraw(into);
        var ground = new ScreenDraw(under);
        underWritten = 0;

        // GEN-1b: with no map loaded there is nothing to draw an interface over, and the menu is the
        // whole of what is on screen. It is the same panel either way — only what is behind it changes.
        if (frame.World is null)
        {
            Menu.Draw(
                ref draw, frame.UiPx, frame.PointerPx, hasTown: false, Switches, Run, frame.WorldSeed,
                frame.AgentSeed);
            return draw.Written;
        }

        var world = frame.World;
        Overlay.Draw(
            ref draw, ref ground, world, frame.Config, Switches, frame.Camera.CentreM,
            frame.Camera.ViewSpanM(frame.UiPx), frame.Camera.PixelsPerMetre);

        underWritten = ground.Written;

        if (Switches.Ruler)
        {
            Ruler.Draw(
                ref draw, frame.Camera, frame.UiPx, frame.Camera.WorldAt(frame.PointerPx, frame.UiPx));
        }

        Hud.Draw(
            ref draw, frame.UiPx, frame.PointerPx, frame.MapName, frame.WorldSeed, frame.AgentSeed, world, Run,
            frame.Tick);

        if (Switches.FrameReadout)
        {
            Readout.Draw(
                ref draw, frame.UiPx, frame.PointerPx, frame.Frame, frame.Crossings, draw.Written, world,
                Overlay.Relaid);
        }

        // The proving ground's own read-out, over the furniture it sits under and behind the menu.
        if (Switches.TrackFigures && frame.Track is { } track) Track.Draw(ref draw, frame.PointerPx, track);

        // The legend is furniture, has no switch, and is drawn from the moment a town is standing.
        ScaleLegend.Draw(ref draw, frame.UiPx, frame.Camera.PixelsPerMetre);

        // Last of all, over the furniture as well as the layers: the menu takes the whole screen, and
        // it takes it without the town behind it being torn down.
        if (Menu.Open)
        {
            Menu.Draw(
                ref draw, frame.UiPx, frame.PointerPx, hasTown: true, Switches, Run, frame.WorldSeed,
                frame.AgentSeed);
        }

        return draw.Written;
    }
}
