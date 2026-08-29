using System.Numerics;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.App.Screen;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// The furniture a run carries that belongs to no panel: the two buttons in the top-right corner and
/// the line saying what the selected unit is doing. What the run <em>is</em> and what it costs is the
/// status panel's, in the opposite corner (<see cref="StatusPanel"/>).
/// </summary>
/// <remarks>
/// <b>The panels keep to the corners.</b> The middle of the view is the town's, which is a claim the
/// reference frame makes and the cheapest one to break by putting a read-out where the eye is.
/// </remarks>
internal static class Chrome
{
    const float MarginPx = Theme.MarginPx;

    /// <summary>
    /// The gear in the top-right corner, which is what the menu hangs under. <b>Where the two buttons
    /// stand is arithmetic and not state</b>: the panels that hang off them are laid against the same
    /// call, so a popup cannot be drawn against a corner the button is no longer in.
    /// </summary>
    public static Rect GearAt(Vector2 uiPx) =>
        new(new Vector2(uiPx.X - MarginPx - Theme.GearPx, MarginPx), new Vector2(Theme.GearPx));

    /// <summary>The question mark beside it, which is what the control legend hangs under.</summary>
    public static Rect HelpAt(Vector2 uiPx) =>
        new(GearAt(uiPx).AtPx - new Vector2(Theme.GearPx + Theme.GapPx, 0f), new Vector2(Theme.GearPx));

    public static void Draw(
        ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerPx, TownWorld? world, bool menuOpen, bool helpOpen)
    {
        Corner(ref draw, HelpAt(uiPx), pointerPx, "?", helpOpen);
        Corner(ref draw, GearAt(uiPx), pointerPx, "=", menuOpen);

        if (world is not null) Selected(ref draw, uiPx, world);
    }

    /// <summary>
    /// One corner button. <b>It says whether the panel under it is showing</b>, because the panel it
    /// opens is a popup rather than a page: a button that reads the same open and shut is a button
    /// somebody presses twice.
    /// </summary>
    static void Corner(ref ScreenDraw draw, Rect box, Vector2 pointerPx, string glyph, bool showing)
    {
        Theme.Face(ref draw, box, pointerPx, showing ? Theme.RowPicked : Theme.Panel, showing);
        draw.Text(box.AtPx + new Vector2(9f, 6f), glyph, Theme.HeadingPx, Theme.Heading);
    }

    /// <summary>
    /// <b>CTL-1: the selected unit's behaviour state shows in the interface.</b> One line, in the
    /// bottom-left, out of the way of the scale legend in the bottom-right.
    /// </summary>
    /// <remarks>
    /// <b>A group is counted rather than described</b> (CTL-1b). One line has room for one unit's state,
    /// and thirty states stacked up the side of the screen is a panel over the town it is about — so what
    /// a selection of many says is how many of each kind it holds.
    /// </remarks>
    static void Selected(ref ScreenDraw draw, Vector2 uiPx, TownWorld world)
    {
        if (world.SelectedCount == 0) return;

        Span<char> text = stackalloc char[96];
        var line = new TextBuffer(text);
        var selection = world.Lead;
        if (world.SelectedCount > 1)
        {
            Group(ref line, world);
        }
        else if (selection.Kind == SelectionKind.Person)
        {
            line.Add("walker ");
            line.Add(selection.Index);
            line.Add("   ");
            line.Add(world.People.Wounded[selection.Index] ? "wounded"
                : world.People.Walking[selection.Index] ? "walking" : "standing");
            if (world.People.Manual[selection.Index]) line.Add("   under orders");
        }
        else
        {
            line.Add("car ");
            line.Add(selection.Index);
            line.Add("   ");
            line.Add(DrivingWords.CarName(world.Cars, selection.Index));
            line.Add("   ");
            line.Add(world.Cars.VelocityMps[selection.Index].Length() * 3.6f, "F0");
            line.Add(" km/h");
            if (world.IsUnderOrders(selection.Index)) line.Add(OrderWords(world.OrderOf(selection.Index)));
        }

        if (world.HandsOn) line.Add(world.SelectedCount > 1 ? "   hands at the wheel" : "   hand at the wheel");

        var widthPx = GlyphSheet.WidthPx(line.Length, Theme.TextPx);
        var box = new Rect(
            new Vector2(MarginPx, uiPx.Y - MarginPx - Theme.TextPx - 14f),
            new Vector2(widthPx + Theme.PaddingPx * 2f, Theme.TextPx + 14f));
        Theme.Frame(ref draw, box);
        draw.Text(box.AtPx + new Vector2(Theme.PaddingPx, 7f), line.Written, Theme.TextPx, Theme.Text);
    }

    /// <summary>
    /// <b>What a car under the player's orders is holding</b> (CTL-8), which is the same question CTL-1
    /// asks of its behaviour state — an ordered car's state is the order.
    /// </summary>
    /// <remarks>
    /// An order that is finished still says something, and what it says is CTL-4's: the car is the
    /// player's and is waiting to be told what to do next rather than deciding for itself.
    /// </remarks>
    static string OrderWords(PlayerOrder order) => order switch
    {
        PlayerOrder.DriveThere => "   ordered to a place",
        PlayerOrder.ParkThere => "   ordered to park",
        PlayerOrder.ParkAndWalkThere => "   ordered to park and walk on",
        PlayerOrder.FollowThatCar => "   ordered to follow",
        _ => "   awaiting orders",
    };

    /// <summary>How many units are picked out, and of what: the whole of what one line can say about many.</summary>
    static void Group(ref TextBuffer line, TownWorld world)
    {
        var cars = world.SelectedCountOf(SelectionKind.Car);
        var walkers = world.SelectedCountOf(SelectionKind.Person);

        line.Add(world.SelectedCount);
        line.Add(" units");

        if (cars > 0)
        {
            line.Add("   ");
            line.Add(cars);
            line.Add(cars == 1 ? " car" : " cars");
        }

        if (walkers <= 0) return;

        line.Add("   ");
        line.Add(walkers);
        line.Add(walkers == 1 ? " walker" : " walkers");
    }
}
