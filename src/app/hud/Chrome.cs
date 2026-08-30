using System.Numerics;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// The furniture a run carries that belongs to no panel: the two buttons in the top-right corner. What
/// the run <em>is</em> and what it costs is the status panel's, in the opposite corner
/// (<see cref="StatusPanel"/>), and what the selected unit is doing stands beside that unit
/// (<see cref="UnitLabel"/>).
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

    public static void Draw(ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerPx, bool menuOpen, bool helpOpen)
    {
        Corner(ref draw, HelpAt(uiPx), pointerPx, "?", helpOpen);
        Corner(ref draw, GearAt(uiPx), pointerPx, "=", menuOpen);
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
}
