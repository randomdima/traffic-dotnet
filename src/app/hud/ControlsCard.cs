using System.Numerics;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>Every control the player has</b>, as a popup hanging off the question mark beside the gear.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is its own popup and not a page of the menu.</b> The menu is what a player goes to in order
/// to change something; this is what they go to in order to find out what a key does, and a legend
/// behind a tab of the settings is a legend read once and never found again.
/// </para>
/// <para>
/// <b>Printable ASCII only</b>, here and in every other string the interface draws: the glyph sheet
/// carries that range and nothing else, so an em dash is drawn as a space and reads as a missing word
/// rather than as a missing glyph.
/// </para>
/// </remarks>
internal sealed class ControlsCard
{
    /// <summary>Key then meaning, in pairs.</summary>
    static readonly string[] Legend =
    [
        "Arrows / middle-drag / wheel", "Camera, unless a unit is being driven",
        "Left-click", "Select a unit; click nothing to deselect",
        "Left-drag", "Select every unit inside the box",
        "Shift-click / shift-drag", "Add to the selection, or drop a unit from it",
        "Right-click", "Order every selected unit there",
        "  on a road", "A car drives there and stands in the lane",
        "  on a car park", "A car parks in the nearest free bay to it",
        "  on another car", "A car follows it",
        "  anywhere else", "A car parks nearest and its driver walks the rest",
        "W A S D", "Take the wheel of all of them: throttle, brake, steer",
        "Space", "Handbrake: a car's rear pair, a walker stands",
        "E", "Work each selected unit's own lever",
        "R", "Release the wheel and the orders; the units decide again",
        "1 2 3", "Pace, as a multiple of real time, capped at 3x",
        "`", "Freeze: nothing decides, steps, collides or ages",
        "Pause", "Hold the agents; the bodies keep stepping",
        "F11", "Fullscreen",
        "Esc", "Opens and shuts the menu; the gear does too",
    ];

    /// <summary>The legend's rows are lines of text rather than rows of chrome, so they sit closer together.</summary>
    const float PitchPx = Theme.SmallTextPx + Theme.GapPx;

    static readonly float KeyWidthPx = Widest(from: 0) + Theme.PaddingPx * 2f;

    static readonly float ContentWidthPx = KeyWidthPx + Widest(from: 1) + Theme.InsetPx * 2f;

    static readonly float ContentHeightPx = Legend.Length / 2 * PitchPx - Theme.GapPx;

    static float Widest(int from)
    {
        var widthPx = 0f;
        for (var at = from; at < Legend.Length; at += 2)
        {
            widthPx = MathF.Max(widthPx, GlyphSheet.WidthPx(Legend[at].Length, Theme.SmallTextPx));
        }

        return widthPx;
    }

    public bool Open { get; private set; }

    public Rect Box { get; private set; }

    public void Toggle() => Open = !Open;

    public void Shut() => Open = false;

    public void Show() => Open = true;

    /// <param name="anchor">The question mark it hangs under, aligned to its trailing edge.</param>
    public void Draw(ref ScreenDraw draw, Vector2 uiPx, Rect anchor)
    {
        var widthPx = ContentWidthPx + Theme.PaddingPx * 2f;
        var heightPx = ContentHeightPx + Theme.PaddingPx * 2f + Theme.HeadingPx + Theme.GapPx * 2f + Theme.EdgePx;
        var atPx = Theme.PopupAt(anchor, uiPx, widthPx);
        Box = new Rect(atPx, new Vector2(widthPx, heightPx));
        Theme.Frame(ref draw, Box);

        draw.Text(atPx + new Vector2(Theme.PaddingPx, Theme.PaddingPx), "Controls", Theme.HeadingPx, Theme.Heading);
        var ruleY = Theme.PaddingPx + Theme.HeadingPx + Theme.GapPx;
        Theme.Separator(ref draw, atPx + new Vector2(Theme.PaddingPx, ruleY), widthPx - Theme.PaddingPx * 2f);

        var firstPx = atPx + new Vector2(Theme.PaddingPx + Theme.InsetPx, ruleY + Theme.EdgePx + Theme.GapPx);
        for (var row = 0; row * 2 + 1 < Legend.Length; row++)
        {
            var rowAtPx = firstPx + new Vector2(0f, row * PitchPx);
            draw.TextFitted(rowAtPx, Legend[row * 2], Theme.SmallTextPx, Theme.Heading, KeyWidthPx);
            draw.TextFitted(
                rowAtPx + new Vector2(KeyWidthPx, 0f), Legend[row * 2 + 1], Theme.SmallTextPx, Theme.Text,
                ContentWidthPx - KeyWidthPx);
        }
    }

    /// <summary>Every string it draws, for the suite that holds the interface to the glyph sheet's range.</summary>
    public static ReadOnlySpan<string> Strings => Legend;
}
