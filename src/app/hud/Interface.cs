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

    /// <summary>
    /// The box a drag is laying over the town this frame, or an empty one (CTL-1b). It is the input
    /// layer's gesture and the interface only draws it, which is what keeps picking in one slice (CTL-6).
    /// </summary>
    public Rect MarqueePx { get; init; }

    public string MapName { get; init; }

    public long Tick { get; init; }

    /// <summary>
    /// What the frame cost and where the tick went, <b>as the meter's own window read them</b> rather
    /// than as the frame being drawn — <see cref="FrameMeter"/>. A path that takes no windows, the
    /// offscreen one, leaves the timings at zero and fills in the phases alone.
    /// </summary>
    public FrameFigures Frame { get; init; }

    public long Crossings { get; init; }

    /// <summary>
    /// Whether the crossing counter is compiled in at all. Zero crossings a frame means two different
    /// things — a Release build, where the counter is not there, and a run before its first steady
    /// frame — and the panel says which.
    /// </summary>
    public bool Counting { get; init; }

    /// <summary>
    /// What the proving ground's own instrument has seen so far, or <see langword="null"/> on every other
    /// map. <b>It is the run's and not the panel's</b> — the figures are gathered every tick and a panel
    /// draws once a frame, so a panel that kept its own would be reading a fraction of the laps.
    /// </summary>
    public TrackMetrics? Track { get; init; }

    /// <summary>
    /// What this map claims about itself and how it is doing, as the run's own watches
    /// (<see cref="Scenarios.For"/>). Empty before a town is standing, and the run's for the same reason
    /// the figures above are: a claim is answered off every tick and not off the frames anybody drew.
    /// </summary>
    public ReadOnlySpan<ScenarioWatch> Scenario { get; init; }
}

/// <summary>Where a click landed once the interface has had it, so the town is not also clicked through a panel.</summary>
internal enum ClickTaken
{
    /// <summary>Nothing on the interface was under it, so it belongs to the town.</summary>
    No,

    /// <summary>A panel took it — either it acted on it or it swallowed it, and either way the town does not see it.</summary>
    Yes,
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
internal sealed class Interface(TrimFigures trims)
{
    /// <summary>
    /// <b>The figures the panel turns, which are the run's own and never a copy.</b> A trim is read where
    /// a car is built and where the ground is catalogued, so a panel holding a second set would move a
    /// slider and nothing else.
    /// </summary>
    public TrimFigures Trims { get; } = trims;

    /// <summary>The popup under the gear: which map to open and which layers to draw.</summary>
    public Menu Menu { get; } = new();

    /// <summary>The popup under the question mark: every key the player has.</summary>
    public ControlsCard Controls { get; } = new();

    /// <summary>The corner the run reads itself off: the rate, the map and the pace, over what they cost.</summary>
    public StatusPanel Status { get; } = new();

    public DebugSwitches Switches { get; } = new();

    public DebugOverlay Overlay { get; } = new();

    public Ruler Ruler { get; } = new();

    /// <summary>The proving ground's figures, one collapsible section per shape. A switch rather than furniture.</summary>
    public TrackPanel Track { get; } = new();

    public RunState Run { get; } = new();

    /// <summary>
    /// <c>--ui</c>, as the switches and popups it names. <b>One list, read by the shot path and by the
    /// windowed run alike</b> — a word that opened a layer in a picture and did nothing in the game
    /// would be two vocabularies, and the words are the switches' and the menu's own so that a script
    /// reads the same as what is on the screen.
    /// </summary>
    /// <remarks>
    /// The words are matched <b>whole</b> rather than as substrings: <c>menu-debug</c> is the debug page
    /// and <c>menu</c> is the map page, which a substring test cannot tell apart. A word nobody offers
    /// is an error rather than a silent fallback.
    /// </remarks>
    public void Apply(string[] wanted)
    {
        foreach (var name in wanted)
        {
            switch (name)
            {
                case "none":
                    break;
                // `menu` is the panel a run opens on and `menu-run` the popup under the gear: the same rows
                // laid two ways (GEN-1b), and two pictures rather than one.
                case "menu":
                    Menu.StandAtTheStart();
                    break;
                case "menu-run":
                    Menu.Show();
                    break;
                // The popup with the scenarios opened, which is the one panel where that group starts
                // shut: the start menu opens on both, so asking it for them would be asking for `menu`.
                case "menu-scenarios":
                    Menu.ShutOntoTheTown();
                    Menu.Show();
                    Menu.OpenGroup(Menu.Scenarios);
                    break;
                case "menu-debug":
                    Menu.ShutOntoTheTown();
                    Menu.Show();
                    Menu.OpenAt(Menu.Debug);
                    break;
                case "menu-figures":
                    Menu.ShutOntoTheTown();
                    Menu.Show();
                    Menu.OpenAt(Menu.Figures);
                    break;
                case "controls":
                    Controls.Show();
                    break;
                case "frame":
                    Status.Show();
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
                case "turn-circles":
                    Switches.Toggle(ref Switches.TurnCircles);
                    break;
                case "ruler":
                    Switches.Toggle(ref Switches.Ruler);
                    break;
                case "track":
                    Switches.Toggle(ref Switches.TrackFigures);
                    break;
                case "scenario":
                    Status.ShowSection(StatusPanel.Claims);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown --ui switch {name}. Takes none, menu, menu-scenarios, menu-debug, menu-figures, " +
                        "menu-run, controls, frame, scenario, car-lines, walker-lines, nodes, reservations, " +
                        "collision, turn-circles, ruler, track.");
            }
        }
    }

    /// <summary>
    /// A click, offered to the interface before the town underneath it.
    /// <b>A panel that is drawn over the town takes the clicks that land on it</b> — a read-out whose
    /// figures could be clicked through was a read-out that selected whatever car was behind it.
    /// </summary>
    /// <remarks>
    /// <b>A click off an open popup shuts it, and is taken.</b> Dismissing a panel and selecting the car
    /// that happened to be under the pointer are two different intentions, and one click is one of them.
    /// </remarks>
    /// <param name="primary">Whether it was the left button. Anything else is swallowed but acts on nothing.</param>
    /// <param name="hasTown">
    /// Whether there is a town behind the menu that somebody asked for. The idle ring is not one (GEN-1b),
    /// so while the start menu is up there is nothing to shut it onto and nothing behind it to click on:
    /// every click off the panel is taken and acts on nothing.
    /// </param>
    /// <param name="camera">
    /// The camera, for the one piece of furniture that acts on it: the compass, which puts a turned town
    /// back north-up (OBS-1c). Nothing else here touches it — the camera is the input layer's (CTL-6).
    /// </param>
    public ClickTaken Click(
        Vector2 atPx, Vector2 uiPx, bool primary, bool hasTown, Camera.Camera2D camera, out MenuChoice choice)
    {
        choice = MenuChoice.None;

        // The start menu owns the screen: the buttons it would hang off are not drawn, and a click
        // anywhere but on the panel is swallowed rather than dismissing it.
        if (Menu.AtTheStart)
        {
            if (primary && Menu.Box.Contains(atPx)) choice = Menu.Click(atPx, Switches, Trims);
            return ClickTaken.Yes;
        }

        // The compass is furniture only while the town is turned, so it takes a click only then: a
        // button that is not drawn is not a button, whatever the corner it would have stood in.
        if (camera.IsTurned && Chrome.CompassAt(uiPx).Contains(atPx))
        {
            if (primary) camera.FaceNorth();
            return ClickTaken.Yes;
        }

        var gear = Chrome.GearAt(uiPx);
        var help = Chrome.HelpAt(uiPx);
        if (gear.Contains(atPx))
        {
            if (primary)
            {
                Menu.Toggle();
                Controls.Shut();
            }

            return ClickTaken.Yes;
        }

        if (help.Contains(atPx))
        {
            if (primary)
            {
                Controls.Toggle();
                Menu.Shut();
            }

            return ClickTaken.Yes;
        }

        if (Menu.Open && Menu.Box.Contains(atPx))
        {
            if (primary) choice = Menu.Click(atPx, Switches, Trims);
            return ClickTaken.Yes;
        }

        if (Controls.Open && Controls.Box.Contains(atPx)) return ClickTaken.Yes;

        // Off an open popup: it shuts, and the town does not also see the click.
        if (Controls.Open || (Menu.Open && hasTown))
        {
            Controls.Shut();
            if (hasTown) Menu.Shut();
            return ClickTaken.Yes;
        }

        if (!hasTown) return ClickTaken.Yes;

        if (Status.Click(atPx)) return ClickTaken.Yes;

        return Switches.TrackFigures && Track.Click(atPx) ? ClickTaken.Yes : ClickTaken.No;
    }

    /// <summary>Whether the pointer is over a panel that would rather have the wheel than the camera.</summary>
    public bool WheelIsThePanels(Vector2 atPx) => Menu.Open && Menu.Box.Contains(atPx);

    /// <summary>The town has changed under everything that held a place in it.</summary>
    /// <param name="behindTheMenu">
    /// Whether the town was stood up <em>behind</em> the menu rather than picked on it (GEN-1b). <b>A menu
    /// shut onto a town nobody asked for would be the game choosing for the reader</b>: the idle map is
    /// what the menu is drawn over, and the menu is still what a run opens on.
    /// </param>
    public void TownChanged(bool behindTheMenu = false)
    {
        Overlay.TownChanged();
        Ruler.TownChanged();
        Track.TownChanged();
        if (behindTheMenu) Menu.StandAtTheStart();
        else Menu.ShutOntoTheTown();

        Controls.Shut();
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

        var world = frame.World;

        // Which watches this map's results may be read off, which is none of them on a place. Every town
        // is watched whichever map it is, so that a headless run has the two claims every town owes; what
        // a place has not got is anybody to show them to. A run somebody opened to play in is not a test,
        // and a laboratory read-out over a city is a read-out with no question behind it.
        var claimed = MapCatalogue.IsScenario(frame.MapName) ? frame.Scenario : default;

        if (world is not null)
        {
            Overlay.Draw(
                ref draw, ref ground, world, frame.Config, Switches, frame.Camera.CentreM,
                frame.Camera.CullSpanM(frame.UiPx), frame.Camera.PixelsPerMetre);

            underWritten = ground.Written;

            // Over the bodies and under every panel: the mark stands where the unit is, so it belongs with
            // the town rather than with the furniture (CTL-1). Its path goes down first, so the brackets and
            // the goal mark stand over the line rather than under it (CTL-1a).
            SelectionPath.Draw(ref draw, world, frame.Config, frame.Camera.PixelsPerMetre);
            SelectionMark.Draw(ref draw, world, frame.Config, frame.Camera.PixelsPerMetre);

            // Over the marks and under the panels: the box is being drawn now and the brackets say what
            // was picked out before it (CTL-1b).
            Marquee.Draw(ref draw, frame.MarqueePx);

            if (Switches.Ruler)
            {
                Ruler.Draw(
                    ref draw, frame.Camera, frame.UiPx, frame.Camera.WorldAt(frame.PointerPx, frame.UiPx));
            }

            // What the selected unit is doing, standing at the unit rather than in a corner (CTL-1). Over
            // the brackets it is laid against and under every panel.
            UnitLabel.Draw(ref draw, frame.UiPx, world, frame.Config, frame.Camera, claimed);

            // GEN-1b: <b>the read-out and the legend say what a run is, and the start menu is not one.</b>
            // The ring behind the panel is a picture rather than a town somebody opened, and a frame rate
            // and a scale bar over it are answers to questions nobody has asked yet.
            if (!Menu.AtTheStart)
            {
                Status.Draw(
                    ref draw, frame.PointerPx, frame.MapName, Run, frame.Tick, frame.Frame, frame.Crossings,
                    frame.Counting, draw.Written, world, Overlay.Relaid, claimed);

                // The proving ground's own read-out, over the furniture it sits under and behind the popups.
                if (Switches.TrackFigures && frame.Track is { } track)
                {
                    Track.Draw(ref draw, frame.PointerPx, Status.Box.Bottom + Theme.GapPx, track);
                }

                // The legend is furniture, has no switch, and is drawn from the moment a town is standing.
                ScaleLegend.Draw(ref draw, frame.UiPx, frame.Camera.PixelsPerMetre);
            }
        }

        // GEN-1b: the start menu is the whole of what is on screen, so the two corner buttons are not
        // drawn under it — a gear that opens what is already open and cannot shut it is a button that
        // teaches nothing, and the legend behind it is about keys no town is listening for yet.
        if (!Menu.AtTheStart)
        {
            Chrome.Draw(ref draw, frame.UiPx, frame.PointerPx, Menu.Open, Controls.Open, frame.Camera.TurnRad);
        }

        // Last of all, over the furniture as well as the layers.
        if (Menu.Open) Menu.Draw(ref draw, frame.UiPx, Chrome.GearAt(frame.UiPx), frame.PointerPx, Switches, Trims);
        if (Controls.Open) Controls.Draw(ref draw, frame.UiPx, Chrome.HelpAt(frame.UiPx));

        return draw.Written;
    }
}
