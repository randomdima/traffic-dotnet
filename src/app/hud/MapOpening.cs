using System.Numerics;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>OBS-2n — a map picked says so while it is being opened</b>: the name of it on a card in the middle
/// of the window, from the click until the town it names is standing.
/// </summary>
/// <remarks>
/// <para>
/// <b>It says which map and nothing about how far along it is.</b> There are frames all through the wait
/// on either head — the card is drawn over a town that is still ticking — but what is being waited for is
/// a fetch in a page and a lay on the desktop, so a bar filling would be measuring one head's work in the
/// other's units, and a card that moved on one and stood still on the other would be two pieces of
/// furniture wearing one name.
/// </para>
/// <para>
/// <b>While it is up it is the whole of the interface</b> (<see cref="Interface.Draw"/>): the panels, the
/// buttons and the popups are not drawn and nothing takes a click. The card is the only thing on screen
/// that is about what is happening, and a map list still standing under it is a second map waiting to be
/// picked while the first is on the wire.
/// </para>
/// </remarks>
internal sealed class MapOpening
{
    /// <summary>What the card says, either side of the map's own name. Printable ASCII, as everything the interface draws is.</summary>
    const string Verb = "Opening ";

    const string Trailing = "...";

    static readonly string[] Words = [Verb, Trailing];

    /// <summary>The map picked and not standing yet, or <c>null</c> while nothing is being opened.</summary>
    public string? Map { get; private set; }

    public bool Showing => Map is not null;

    /// <summary>The card as it was last drawn, which is what the suite reads its placing off.</summary>
    public Rect Box { get; private set; }

    /// <summary>A map picked on the menu. What is on screen from here until the town it names is standing.</summary>
    public void Opens(string map) => Map = map;

    /// <summary>The town is standing, so there is nothing being opened any more.</summary>
    public void Stood() => Map = null;

    /// <summary>
    /// The card, centred in the window. <b>As wide as its own line until the window is narrower than
    /// that</b>, which is the rule every panel here answers to (OBS-2k).
    /// </summary>
    public void Draw(ref ScreenDraw draw, Vector2 uiPx)
    {
        if (Map is not { } map) return;

        Span<char> text = stackalloc char[64];
        var written = new TextBuffer(text);
        written.Add(Verb);
        written.Add(map);
        written.Add(Trailing);

        var contentWidthPx = MathF.Min(
            GlyphSheet.WidthPx(written.Length, Theme.HeadingPx),
            MathF.Max(Theme.HeadingPx, uiPx.X - ((Theme.MarginPx + Theme.PaddingPx) * 2f)));
        var sizePx = new Vector2(
            contentWidthPx + (Theme.PaddingPx * 2f), Theme.HeadingPx + (Theme.PaddingPx * 2f));

        Box = new Rect((uiPx - sizePx) * 0.5f, sizePx);
        Theme.Frame(ref draw, Box);
        draw.TextFitted(
            Box.AtPx + new Vector2(Theme.PaddingPx), written.Written, Theme.HeadingPx, Theme.Heading,
            contentWidthPx);
    }

    /// <summary>Every string it draws that is its own, for the suite that holds the interface to the glyph sheet's range.</summary>
    public static ReadOnlySpan<string> Strings => Words;
}
