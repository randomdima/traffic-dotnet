using System.Numerics;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// The furniture a run carries that belongs to no panel: the buttons in the top-right corner. What
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
    /// The gear in the top-right corner, which is what the menu hangs under. <b>Where the buttons
    /// stand is arithmetic and not state</b>: the panels that hang off them are laid against the same
    /// call, so a popup cannot be drawn against a corner the button is no longer in.
    /// </summary>
    public static Rect GearAt(Vector2 uiPx) =>
        new(new Vector2(uiPx.X - MarginPx - Theme.GearPx, MarginPx), new Vector2(Theme.GearPx));

    /// <summary>The question mark beside it, which is what the control legend hangs under.</summary>
    public static Rect HelpAt(Vector2 uiPx) =>
        new(GearAt(uiPx).AtPx - new Vector2(Theme.GearPx + Theme.GapPx, 0f), new Vector2(Theme.GearPx));

    /// <summary>
    /// The screen, filled and given back — <c>F11</c> for a reader who has not got one (OBS-2l). <b>It is
    /// the one corner button drawn under the start menu</b> (GEN-1b): the gear and the question mark are
    /// about a town and there is none yet, where this is about the window a town will stand in — and a
    /// handset is at its smallest exactly while somebody is choosing on it.
    /// </summary>
    /// <param name="atTheStart">
    /// Whether it is the only button on screen, in which case it stands in the corner the gear would have.
    /// A lone button third along, with two empty places beside it, reads as two buttons that failed to draw.
    /// </param>
    public static Rect ScreenAt(Vector2 uiPx, bool atTheStart) => atTheStart
        ? GearAt(uiPx)
        : new Rect(HelpAt(uiPx).AtPx - new Vector2(Theme.GearPx + Theme.GapPx, 0f), new Vector2(Theme.GearPx));

    /// <summary>
    /// The compass beside the two of them, which is <b>drawn only while the town is turned</b> (OBS-1c)
    /// and puts it back north-up when it is pressed. A compass on a town already north-up is a button
    /// that does nothing, and the needle standing straight up is the whole of what it would say.
    /// </summary>
    public static Rect CompassAt(Vector2 uiPx) =>
        new(ScreenAt(uiPx, atTheStart: false).AtPx - new Vector2(Theme.GearPx + Theme.GapPx, 0f),
            new Vector2(Theme.GearPx));

    /// <param name="turnRad">How far the town is turned on screen, which is what the needle answers.</param>
    public static void Draw(
        ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerPx, bool menuOpen, bool helpOpen, float turnRad)
    {
        if (turnRad != 0f) Compass(ref draw, CompassAt(uiPx), pointerPx, turnRad);

        Corner(ref draw, HelpAt(uiPx), pointerPx, "?", helpOpen);
        Corner(ref draw, GearAt(uiPx), pointerPx, "=", menuOpen);
    }

    /// <summary>
    /// The screen button, which is drawn on its own because it is drawn under the start menu as well
    /// (<see cref="ScreenAt"/>). <b>It says nothing about its own state and the other two do</b>: a popup
    /// is hidden behind its button where a window filling the screen is the thing being looked at.
    /// </summary>
    public static void DrawScreen(ref ScreenDraw draw, Vector2 uiPx, Vector2 pointerPx, bool atTheStart)
    {
        var box = ScreenAt(uiPx, atTheStart);
        Theme.Face(ref draw, box, pointerPx, Theme.Panel);

        // Corner brackets rather than a glyph: the sheet is printable ASCII, which has no mark that reads
        // as a screen, and a rectangle drawn whole reads as a box rather than as the edges of one.
        var inner = box.Inset(BracketInsetPx);
        for (var corner = 0; corner < 4; corner++)
        {
            var right = (corner & 1) != 0;
            var down = (corner & 2) != 0;
            var atPx = new Vector2(right ? inner.Right : inner.AtPx.X, down ? inner.Bottom : inner.AtPx.Y);
            var armPx = new Vector2(right ? -BracketArmPx : BracketArmPx, down ? -BracketArmPx : BracketArmPx);

            draw.LinePx(atPx, atPx + new Vector2(armPx.X, 0f), BracketWidthPx, Theme.Heading);
            draw.LinePx(atPx, atPx + new Vector2(0f, armPx.Y), BracketWidthPx, Theme.Heading);
        }
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
    /// The needle: north as the town is drawing it, which is straight up turned by however far the town
    /// is. <b>The half that points north is the accent and the half behind it is not</b>, because a bar
    /// with both ends alike says which way the town lies without saying which way is north.
    /// </summary>
    static void Compass(ref ScreenDraw draw, Rect box, Vector2 pointerPx, float turnRad)
    {
        Theme.Face(ref draw, box, pointerPx, Theme.Panel);

        // North is up in the town and up is -y on the screen, so the turn takes (0, -1) round with it.
        var northPx = new Vector2(MathF.Sin(turnRad), -MathF.Cos(turnRad)) * (box.SizePx.Y * 0.5f - NeedleInsetPx);
        var middlePx = box.AtPx + box.SizePx * 0.5f;
        draw.LinePx(middlePx, middlePx - northPx, NeedleWidthPx, Theme.Dim);
        draw.LinePx(middlePx, middlePx + northPx, NeedleWidthPx, Theme.Accent);
    }

    const float NeedleInsetPx = 7f;
    const float NeedleWidthPx = 2.5f;

    /// <summary>How far inside the button the brackets stand, and how long each arm of one is.</summary>
    const float BracketInsetPx = 8f;

    const float BracketArmPx = 5f;
    const float BracketWidthPx = 2f;
}
