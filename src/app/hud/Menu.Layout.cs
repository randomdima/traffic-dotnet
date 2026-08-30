using System.Numerics;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Hud;

/// <summary>Where the popup hangs, how wide it wants to be, and where each row of the page showing lands.</summary>
internal sealed partial class Menu
{
    const float RowPitchPx = Theme.TallRowPx + Theme.GapPx;
    const float LinePitchPx = Theme.RowPx + Theme.GapPx;

    /// <summary>A group's own header, which is a row that says what is under it rather than one that opens a town.</summary>
    const float GroupPitchPx = Theme.RowPx + Theme.GapPx;

    static readonly float RuleTopPx = Theme.PaddingPx + Theme.HeadingPx + Theme.GapPx;
    static readonly float TabsTopPx = RuleTopPx + Theme.EdgePx + Theme.GapPx;
    static readonly float ContentTopPx = TabsTopPx + Theme.RowPx + Theme.GapPx;

    /// <summary>The panel's height less its content: the title, the tab strip and the paddings between them.</summary>
    static readonly float ChromeHeightPx = ContentTopPx + Theme.PaddingPx;

    /// <summary>What the content column is never shorter than, which is the debug page laid whole.</summary>
    static readonly float LeastContentHeightPx = MostLines * LinePitchPx - Theme.GapPx;

    /// <summary>And the figures page, which is one row a trim and the one under them that resets the lot.</summary>
    static readonly float FiguresHeightPx = (TrimFigures.Count + 1) * TrimPitchPx - Theme.GapPx;

    /// <summary>A trim row carries its name and its share on two lines, as a map row carries its own.</summary>
    const float TrimPitchPx = Theme.TallRowPx + Theme.GapPx;

    static float WidestPx(string[] of, float textPx)
    {
        var widthPx = 0f;
        foreach (var line in of) widthPx = MathF.Max(widthPx, GlyphSheet.WidthPx(line.Length, textPx));

        return widthPx;
    }

    readonly Rect[] _tabs = new Rect[4];
    readonly Rect[] _rows = new Rect[MostRows];
    readonly Rect[] _lines = new Rect[MostLines];

    /// <summary>One track a trim, and the reset row under them.</summary>
    readonly Rect[] _trims = new Rect[TrimFigures.Count + 1];

    /// <summary>Which trim the pointer has hold of, or -1 while nothing is being dragged.</summary>
    int _held = -1;

    bool _figuresMoved;
    readonly string[] _rowNames = new string[MostRows];
    readonly string[] _rowDescriptions = new string[MostRows];

    /// <summary>Which group a row is the header of, or -1 where the row is a map.</summary>
    readonly int[] _rowGroup = new int[MostRows];

    /// <summary>
    /// Which groups are open. <b>The main maps are and the scenarios are not</b>: a menu of two cities
    /// should not read as a menu of two cities and a laboratory.
    /// </summary>
    readonly bool[] _groupOpen = [true, false];

    Vector2 _laidFor;
    Rect _laidAt;
    int _rowCount;

    /// <summary>How many rows the window has room for, and which one is at the top of them.</summary>
    int _shownRows;

    int _firstRow;

    /// <summary>Whether the menu is on the screen. Shutting it does not touch the town it was opened over.</summary>
    public bool Open { get; private set; } = true;

    /// <summary>Which page is showing. Nothing here is a mode: the pages are one popup cut two ways.</summary>
    public int Page { get; private set; }

    public void Show() => Open = true;

    public void Shut() => Open = false;

    public void Toggle() => Open = !Open;

    /// <summary>Which page is showing. What a shot script asks for, and what a tab does for a player.</summary>
    public void OpenAt(int page)
    {
        Page = Math.Clamp(page, 0, Pages - 1);
        _firstRow = 0;
    }

    /// <summary>Open a group of the map page, which is what a shot script asks for by name.</summary>
    public void OpenGroup(int group)
    {
        _groupOpen[Math.Clamp(group, 0, Groups - 1)] = true;
        _firstRow = 0;
    }

    public bool IsGroupOpen(int group) => _groupOpen[group];

    /// <summary>The panel itself, for whatever else has to keep out of its way.</summary>
    public Rect Box { get; private set; }

    /// <param name="anchor">
    /// The gear the popup hangs under: it opens below that button and is aligned to its trailing edge,
    /// so the thing that opened it is the thing it appears to come out of.
    /// </param>
    public void Lay(Vector2 uiPx, Rect anchor)
    {
        _laidFor = uiPx;
        _laidAt = anchor;

        FillRows();

        // **The rows answer to the window rather than the window to them.** A list of maps on a short
        // display grew the panel straight off the bottom of the screen, which is a menu hiding the
        // thing it was written to expose; what does not fit scrolls.
        var topY = anchor.Bottom + Theme.GapPx;
        var roomPx = MathF.Max(LinePitchPx, uiPx.Y - Theme.MarginPx - topY - ChromeHeightPx);

        _firstRow = Math.Clamp(_firstRow, 0, Math.Max(0, _rowCount - 1));
        while (_firstRow > 0 && HeightFrom(_firstRow - 1) <= roomPx) _firstRow--;

        // **Each page is as tall as its own content.** A band of empty panel under the last map, kept
        // so that the other page would fit without the panel changing height, reads as a list that was
        // cut short rather than as a page that ended.
        _shownRows = Page == Maps ? Fitting(_firstRow, roomPx) : 0;
        var contentHeightPx = MathF.Min(roomPx, Page switch
        {
            Maps => HeightOf(_firstRow, _shownRows),
            Figures => FiguresHeightPx,
            _ => LeastContentHeightPx,
        });

        // The width is every page's at once, so tabbing moves nothing sideways: what one page needs is
        // what the panel is, and the bar the map page loses its rows' width to is counted in it.
        var contentWidthPx = MathF.Min(
            WidestContentPx() + Theme.InsetPx * 2f + ScrollBarPx + Theme.GapPx,
            MathF.Max(LeastContentPx, uiPx.X - Theme.MarginPx * 2f - Theme.PaddingPx * 2f));

        var widthPx = Theme.PaddingPx * 2f + contentWidthPx;
        var atPx = Theme.PopupAt(anchor, uiPx, widthPx);
        Box = new Rect(atPx, new Vector2(widthPx, ChromeHeightPx + contentHeightPx));

        var contentX = atPx.X + Theme.PaddingPx;
        var tabWidthPx = (contentWidthPx - (Theme.GapPx * (_tabs.Length - 1))) / _tabs.Length;
        for (var tab = 0; tab < _tabs.Length; tab++)
        {
            _tabs[tab] = new Rect(
                new Vector2(contentX + tab * (tabWidthPx + Theme.GapPx), atPx.Y + TabsTopPx),
                new Vector2(tabWidthPx, Theme.RowPx));
        }

        var contentTopY = atPx.Y + ContentTopPx;

        // The bar takes its room off the rows, and only on a page that has one: a row ending short of
        // the panel edge on one page and flush with the tabs on another is two paddings.
        var rowWidthPx = Scrolls ? contentWidthPx - ScrollBarPx - Theme.GapPx : contentWidthPx;
        var downPx = 0f;
        for (var slot = 0; slot < _shownRows; slot++)
        {
            var heightOfRowPx = HeightOfRow(_firstRow + slot);
            _rows[slot] = new Rect(new Vector2(contentX, contentTopY + downPx), new Vector2(rowWidthPx, heightOfRowPx));
            downPx += heightOfRowPx + Theme.GapPx;
        }

        for (var line = 0; line < MostLines; line++)
        {
            _lines[line] = new Rect(
                new Vector2(contentX, contentTopY + line * LinePitchPx), new Vector2(contentWidthPx, Theme.RowPx));
        }

        for (var trim = 0; trim < _trims.Length; trim++)
        {
            _trims[trim] = new Rect(
                new Vector2(contentX, contentTopY + trim * TrimPitchPx), new Vector2(contentWidthPx, Theme.TallRowPx));
        }
    }

    /// <summary>A group header is one row of chrome and a map is a row carrying the line that says what it is.</summary>
    float HeightOfRow(int row) => _rowGroup[row] < 0 ? Theme.TallRowPx : Theme.RowPx;

    /// <summary>What the rows from <paramref name="first"/> to the end of the list come to, gaps and all.</summary>
    float HeightFrom(int first) => HeightOf(first, _rowCount - first);

    float HeightOf(int first, int rows)
    {
        var heightPx = 0f;
        for (var row = first; row < first + rows; row++) heightPx += HeightOfRow(row) + Theme.GapPx;

        return MathF.Max(0f, heightPx - Theme.GapPx);
    }

    /// <summary>How many rows from <paramref name="first"/> fit in the room the window has left.</summary>
    int Fitting(int first, float roomPx)
    {
        if (_rowCount == 0) return 0;

        var rows = 0;
        while (first + rows < _rowCount && HeightOf(first, rows + 1) <= roomPx) rows++;

        return Math.Max(1, rows);
    }

    void FillRows()
    {
        _rowCount = 0;
        if (Page != Maps) return;

        for (var group = 0; group < Groups; group++)
        {
            AddGroup(group);
            if (!_groupOpen[group]) continue;

            foreach (var map in MapCatalogue.On(KindOf(group))) AddMap(map.Name, map.Description);
        }
    }

    /// <summary>
    /// What the widest thing on either page needs, since the panel is one width for both — a
    /// description cut off mid-word is the one thing every claim about this frame forbids.
    /// </summary>
    static float WidestContentPx()
    {
        var wantedPx = LeastContentPx;
        foreach (var map in MapCatalogue.Shipped())
        {
            wantedPx = MathF.Max(wantedPx, RowWidthPx(map.Name, map.Description));
        }

        return MathF.Max(
            wantedPx,
            MathF.Max(WidestPx(Lines, Theme.TextPx), WidestPx(TrimFigures.Names, Theme.TextPx) + TrimShareRoomPx));
    }

    /// <summary>The room a trim's own share is drawn in, kept off the end of its name so the two never meet.</summary>
    static readonly float TrimShareRoomPx = GlyphSheet.WidthPx("1000%".Length, Theme.TextPx) + Theme.InsetPx * 2f;

    static float RowWidthPx(string name, string description) => MathF.Max(
        GlyphSheet.WidthPx(name.Length, Theme.TextPx), GlyphSheet.WidthPx(description.Length, Theme.SmallTextPx));

    /// <summary>Whether the page showing has more rows than the window has room for.</summary>
    bool Scrolls => _shownRows < _rowCount;
}
