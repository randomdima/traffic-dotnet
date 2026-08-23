using System.Numerics;
using TrafficSimulation.App.Screen;

namespace TrafficSimulation.App.Hud;

/// <summary>How wide and how tall the panel wants to be, and where each row of the page showing lands.</summary>
internal sealed partial class Menu
{
    /// <summary>The tab column, as wide as the longest page name plus a row's own inset either side.</summary>
    static readonly float TabWidthPx = WidestPx(PageNames, from: 0, Theme.TextPx) + Theme.InsetPx * 2f;

    /// <summary>How far into the content column a control's description starts, past the key that names it.</summary>
    static readonly float LegendKeyWidthPx =
        WidestPx(ControlLegend, from: 0, Theme.SmallTextPx, step: 2) + Theme.PaddingPx * 2f;

    /// <summary>The legend's own rows are a line of text rather than a row of chrome, so they sit closer together.</summary>
    const float LegendPitchPx = Theme.SmallTextPx + Theme.GapPx;

    const float RowPitchPx = Theme.TallRowPx + Theme.GapPx;
    const float LinePitchPx = Theme.RowPx + Theme.GapPx;

    static readonly float RuleTopPx = Theme.PaddingPx + Theme.HeadingPx + Theme.GapPx;
    static readonly float ContentTopPx = RuleTopPx + Theme.EdgePx + Theme.GapPx;

    /// <summary>The panel's height less its content: the title, the footer and the paddings between them.</summary>
    static readonly float ChromeHeightPx = ContentTopPx + Theme.PaddingPx + Theme.RowPx + Theme.PaddingPx;

    /// <summary>What the content column is never shorter than, which is the tab column standing beside it.</summary>
    static readonly float LeastContentHeightPx = MathF.Max(
        PageNames.Length * LinePitchPx - Theme.GapPx,
        MathF.Max(MostLines * LinePitchPx - Theme.GapPx, ControlLegend.Length / 2 * LegendPitchPx - Theme.GapPx));

    static float WidestPx(string[] of, int from, float textPx, int step = 1)
    {
        var widthPx = 0f;
        for (var at = from; at < of.Length; at += step)
        {
            widthPx = MathF.Max(widthPx, GlyphSheet.WidthPx(of[at].Length, textPx));
        }

        return widthPx;
    }

    readonly Rect[] _tabs = new Rect[7];
    readonly Rect[] _rows = new Rect[MostRows];
    readonly Rect[] _lines = new Rect[MostLines];
    readonly string[] _rowNames = new string[MostRows];
    readonly string[] _rowDescriptions = new string[MostRows];
    readonly bool[] _rowIsCheck = new bool[MostRows];

    Rect _legend;
    Rect _close;
    Rect _quit;
    Vector2 _laidFor;
    bool _laidWithTown;
    int _rowCount;

    /// <summary>How many rows the window has room for, and which one is at the top of them.</summary>
    int _shownRows;

    int _firstRow;

    /// <summary>Whether the menu is on the screen. Closing it does not touch the town it was opened over.</summary>
    public bool Open { get; private set; } = true;

    /// <summary>Which page is showing. Nothing here is a mode: the pages are one panel cut seven ways.</summary>
    public int Page { get; private set; }

    /// <summary>
    /// What the last check printed, shown on the checks page — because <b>anything opened from the
    /// menu is by definition being looked at</b>, and somebody who opened it from a menu has no
    /// terminal behind them.
    /// </summary>
    public string[] LastOutput { get; set; } = [];

    public void Show() => Open = true;

    public void Shut() => Open = false;

    public void Toggle() => Open = !Open;

    /// <summary>Which page is showing. What a shot script asks for, and what a tab does for a player.</summary>
    public void OpenAt(int page)
    {
        Page = Math.Clamp(page, 0, PageNames.Length - 1);
        _firstRow = 0;
    }

    /// <summary>The panel itself, for whatever else has to keep out of its way.</summary>
    public Rect Box { get; private set; }

    public void Lay(Vector2 uiPx) => Lay(uiPx, hasTown: false);

    public void Lay(Vector2 uiPx, bool hasTown)
    {
        _laidFor = uiPx;
        _laidWithTown = hasTown;

        FillRows();

        // **The rows answer to the window rather than the window to them.** Eleven checks on a short
        // display grew the panel straight off the bottom of the screen, which is a menu hiding the
        // thing it was written to expose; what does not fit scrolls.
        var roomPx = uiPx.Y - Theme.PaddingPx * 4f - ChromeHeightPx;
        _shownRows = Math.Clamp((int)((roomPx + Theme.GapPx) / RowPitchPx), 1, Math.Max(_rowCount, 1));
        _firstRow = Math.Clamp(_firstRow, 0, Math.Max(0, _rowCount - _shownRows));

        // Never past the room the window has, which is the same rule the rows answer to: the panel that
        // grew off the bottom of a short display grew by the height of its *tallest page*, whether or not
        // the page showing was that one.
        var wantedHeightPx = IsList(Page)
            ? MathF.Max(LeastContentHeightPx, _shownRows * RowPitchPx - Theme.GapPx)
            : LeastContentHeightPx;
        var contentHeightPx = MathF.Min(roomPx, wantedHeightPx);

        // The width is every page's at once, so tabbing moves nothing sideways: what one page needs is
        // what the panel is, and the bar the list pages lose their rows' width to is counted in it.
        var contentWidthPx = MathF.Min(
            WidestContentPx() + Theme.InsetPx * 2f + ScrollBarPx + Theme.GapPx,
            MathF.Max(LeastContentPx, uiPx.X - Theme.PaddingPx * 4f - TabWidthPx));

        var widthPx = Theme.PaddingPx * 3f + TabWidthPx + contentWidthPx;
        var heightPx = ChromeHeightPx + contentHeightPx;
        var atPx = new Vector2(
            (uiPx.X - widthPx) * 0.5f, MathF.Max(Theme.PaddingPx * 2f, (uiPx.Y - heightPx) * 0.5f));
        Box = new Rect(atPx, new Vector2(widthPx, heightPx));

        var contentTopY = atPx.Y + ContentTopPx;
        for (var tab = 0; tab < _tabs.Length; tab++)
        {
            _tabs[tab] = new Rect(
                new Vector2(atPx.X + Theme.PaddingPx, contentTopY + tab * LinePitchPx),
                new Vector2(TabWidthPx, Theme.RowPx));
        }

        var contentX = atPx.X + Theme.PaddingPx * 2f + TabWidthPx;

        // The bar takes its room off the rows, and only on a page that has one: a row ending short of
        // the panel edge on one page and flush with the footer on another is two paddings.
        var rowWidthPx = Scrolls ? contentWidthPx - ScrollBarPx - Theme.GapPx : contentWidthPx;
        for (var slot = 0; slot < _shownRows; slot++)
        {
            _rows[slot] = new Rect(
                new Vector2(contentX, contentTopY + slot * RowPitchPx), new Vector2(rowWidthPx, Theme.TallRowPx));
        }

        for (var line = 0; line < MostLines; line++)
        {
            _lines[line] = new Rect(
                new Vector2(contentX, contentTopY + line * LinePitchPx), new Vector2(contentWidthPx, Theme.RowPx));
        }

        _legend = new Rect(new Vector2(contentX, contentTopY), new Vector2(contentWidthPx, contentHeightPx));

        // The footer runs the panel's whole width rather than the content column's: it belongs to the
        // menu and not to whichever page is showing.
        var footerWidthPx = widthPx - Theme.PaddingPx * 2f;
        var footerY = Box.Bottom - Theme.PaddingPx - Theme.RowPx;
        if (hasTown)
        {
            var halfPx = (footerWidthPx - Theme.GapPx) * 0.5f;
            _close = new Rect(new Vector2(atPx.X + Theme.PaddingPx, footerY), new Vector2(halfPx, Theme.RowPx));
            _quit = new Rect(
                new Vector2(atPx.X + Theme.PaddingPx + halfPx + Theme.GapPx, footerY),
                new Vector2(halfPx, Theme.RowPx));
        }
        else
        {
            // Nothing to go back to, so the way out takes the whole footer.
            _close = default;
            _quit = new Rect(new Vector2(atPx.X + Theme.PaddingPx, footerY), new Vector2(footerWidthPx, Theme.RowPx));
        }
    }

    void FillRows()
    {
        _rowCount = 0;
        if (Page == Checks)
        {
            foreach (var check in CheckCatalogue.Shipped) AddRow(check.Name, check.Description, isCheck: true);
            return;
        }

        if (!IsList(Page)) return;

        foreach (var map in MapCatalogue.On(Page == Places ? MapKind.Place : MapKind.Scenario))
        {
            AddRow(map.Name, map.Description, isCheck: false);
        }
    }

    /// <summary>
    /// What the widest thing on any page needs, since the panel is one width for all seven — a
    /// description cut off mid-word is the one thing every claim about this frame forbids.
    /// </summary>
    static float WidestContentPx()
    {
        var wantedPx = LeastContentPx;
        foreach (var map in MapCatalogue.Shipped()) wantedPx = MathF.Max(wantedPx, RowWidthPx(map.Name, map.Description));
        foreach (var check in CheckCatalogue.Shipped)
        {
            wantedPx = MathF.Max(wantedPx, RowWidthPx(check.Name, check.Description));
        }

        wantedPx = MathF.Max(wantedPx, WidestPx(Lines, from: 0, Theme.TextPx));
        return MathF.Max(wantedPx, LegendKeyWidthPx + WidestPx(ControlLegend, from: 1, Theme.SmallTextPx, step: 2));
    }

    static float RowWidthPx(string name, string description) => MathF.Max(
        GlyphSheet.WidthPx(name.Length, Theme.TextPx), GlyphSheet.WidthPx(description.Length, Theme.SmallTextPx));

    /// <summary>Whether the page showing has more rows than the window has room for.</summary>
    bool Scrolls => _rowCount > _shownRows;
}
