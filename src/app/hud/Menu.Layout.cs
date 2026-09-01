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

    /// <summary>
    /// The room the start menu lays its rows in: <b>a share of the window's short side less the panel's own
    /// chrome</b> (<see cref="StartHeightShare"/>), and never what the rows came to. It is one size because
    /// what it has to stay inside is the field in the middle of the idle ring; a list longer than it
    /// scrolls, as the popup's does when a window is short.
    /// </summary>
    static float StartRoomPx(Vector2 uiPx) => MathF.Max(
        LinePitchPx,
        MathF.Min(MathF.Min(uiPx.X, uiPx.Y) * StartHeightShare, uiPx.Y - (Theme.MarginPx * 2f))
        - ChromeHeightPx(atTheStart: true));

    /// <summary>
    /// And what it is never shorter than, whatever the field comes to: <b>a group's own header and the
    /// tallest map under it</b>. On a handset the field is a fifth of a tall screen and one wrapped
    /// description is taller than that, which laid a panel carrying a heading and a band of empty panel —
    /// a menu of no maps at all. The panel stands over the road there, which is what its own width already
    /// does: the title and the way out are wider than the field on that window too.
    /// </summary>
    /// <remarks>
    /// <b>It is the tallest row and not the first</b>, so this is one size like everything else about this
    /// panel: opening a group cannot change it, and neither can scrolling to a longer description.
    /// </remarks>
    float LeastStartRoomPx()
    {
        var tallestPx = 0f;
        for (var row = 0; row < _rowCount; row++)
        {
            if (_rowGroup[row] < 0) tallestPx = MathF.Max(tallestPx, HeightOfRow(row));
        }

        return tallestPx > 0f ? Theme.RowPx + Theme.GapPx + tallestPx : LinePitchPx;
    }

    /// <summary>
    /// And the room the popup lays its rows in, off <b>how far down the window it may reach</b>
    /// (<see cref="PopupHeightShare"/>) rather than off how many rows it has. <b>It never reaches less far
    /// than its own fixed page needs</b> — the switches are laid at a pitch rather than scrolled, so a
    /// ceiling cutting into them would draw them outside the panel — and never past the window's margin.
    /// </summary>
    static float PopupRoomPx(Vector2 uiPx, Rect anchor)
    {
        var underTheButtonPx = anchor.Bottom + Theme.GapPx + ChromeHeightPx(atTheStart: false);
        var reachesPx = MathF.Min(
            uiPx.Y - Theme.MarginPx,
            MathF.Max(uiPx.Y * PopupHeightShare, underTheButtonPx + LeastContentHeightPx));

        return MathF.Max(LinePitchPx, reachesPx - underTheButtonPx);
    }

    /// <summary>
    /// The title's own band. <b>On the start menu it is as tall as the button standing in it</b>, which is
    /// the way out: with no tab strip under it that button has nowhere else to be, and the last thing
    /// across the top of the panel is what it already was.
    /// </summary>
    static float TitleHeightPx(bool atTheStart) => atTheStart ? Theme.RowPx : Theme.HeadingPx;

    static float RuleTopPx(bool atTheStart) => Theme.PaddingPx + TitleHeightPx(atTheStart) + Theme.GapPx;

    static float TabsTopPx(bool atTheStart) => RuleTopPx(atTheStart) + Theme.EdgePx + Theme.GapPx;

    /// <summary>Where the rows start: under the tab strip, or straight under the rule where there is none.</summary>
    static float ContentTopPx(bool atTheStart) =>
        TabsTopPx(atTheStart) + (atTheStart ? 0f : Theme.RowPx + Theme.GapPx);

    /// <summary>The panel's height less its content: the title, the tab strip and the paddings between them.</summary>
    static float ChromeHeightPx(bool atTheStart) => ContentTopPx(atTheStart) + Theme.PaddingPx;

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

    /// <summary>Whether a press on the map list is still down, and where it landed (<see cref="PressedRow"/>).</summary>
    bool _pressedOnARow;

    Vector2 _pressedAtPx;

    /// <summary>Where the pointer was last frame, so a drag is read as the pixels between two frames.</summary>
    Vector2 _lastPointerPx;

    /// <summary>What a drag has travelled and not yet spent on a row (<see cref="ScrollByPixels"/>).</summary>
    float _draggedPx;

    bool _figuresMoved;
    readonly string[] _rowNames = new string[MostRows];
    readonly string[] _rowDescriptions = new string[MostRows];

    /// <summary>Which group a row is the header of, or -1 where the row is a map.</summary>
    readonly int[] _rowGroup = new int[MostRows];

    /// <summary>
    /// How a row's description falls across the width it is laid at, and how many lines that came to.
    /// <b>Broken once, where the width is decided</b>, so what the row was measured for and what is drawn
    /// in it are the same break rather than two.
    /// </summary>
    readonly Range[] _rowLine = new Range[MostRows * MostDescriptionLines];

    readonly int[] _rowLines = new int[MostRows];

    /// <summary>
    /// Which groups are open, which is <b>a property of which menu this is</b> rather than of the list.
    /// The popup under the gear opens on the places alone — a menu of two cities should not read as a menu
    /// of two cities and a laboratory, and a mis-click on the row under a city there loses a running game.
    /// <b>The start menu opens on both</b>: nothing is running behind it, so a mis-click costs nothing and
    /// what a reader is there to do is read the whole catalogue.
    /// </summary>
    readonly bool[] _groupOpen = [true, false];

    Vector2 _laidFor;
    Rect _laidAt;

    /// <summary>
    /// And which of the two layouts the rectangles above are, since a map picked changes that without
    /// moving the window or the button: a panel laid once as the start menu and then drawn as the popup is
    /// the narrow centred one standing under the gear for the rest of the run.
    /// </summary>
    bool _laidAtTheStart = true;

    int _rowCount;

    /// <summary>How many rows the window has room for, and which one is at the top of them.</summary>
    int _shownRows;

    int _firstRow;

    /// <summary>Whether the menu is on the screen. Shutting it does not touch the town it was opened over.</summary>
    public bool Open { get; private set; } = true;

    /// <summary>
    /// <b>Whether this is the menu a run opens on</b> (GEN-1b), which is a different thing from the popup
    /// under the gear even though it is the same panel: it stands in the middle of the screen, it is laid
    /// wider, and <b>it cannot be shut</b> — the idle ring behind it is not a town anybody chose, so there
    /// is nothing to shut it onto. The way past it is picking a map, and the way out is the exit tab.
    /// </summary>
    /// <remarks>
    /// <b>It starts true and is cleared once</b>, by the first map somebody picks: a page whose town is
    /// still coming down the wire has the menu up over nothing at all (WEB-9), and that is the start menu
    /// as much as the one standing over the ring is.
    /// </remarks>
    public bool AtTheStart { get; private set; } = true;

    /// <summary>Which page is showing. Nothing here is a mode: the pages are one popup cut two ways.</summary>
    public int Page { get; private set; }

    public void Show() => Open = true;

    /// <summary>The menu as the way into the game rather than as the popup under the gear.</summary>
    public void StandAtTheStart()
    {
        AtTheStart = true;
        Open = true;
        LetGo();
        OpenAt(Maps);
        OpenTheGroups(atTheStart: true);
        Relay();
    }

    /// <summary>The groups each of the two menus opens on (<see cref="_groupOpen"/>).</summary>
    void OpenTheGroups(bool atTheStart)
    {
        for (var group = 0; group < Groups; group++) _groupOpen[group] = atTheStart || group == MainMaps;
    }

    public void Shut()
    {
        if (AtTheStart) return;

        Open = false;
    }

    public void Toggle()
    {
        if (AtTheStart) return;

        Open = !Open;
    }

    /// <summary>A map picked: the menu is the gear's popup again, and shuts onto the town that was asked for.</summary>
    public void ShutOntoTheTown()
    {
        AtTheStart = false;
        Open = false;
        LetGo();
        OpenTheGroups(atTheStart: false);
        Relay();
    }

    /// <summary>
    /// The rectangles again for the layout the panel is now in, where it has ever been laid. <b>A map picked
    /// changes the layout without moving the window or the button</b>, so nothing else would ask for it:
    /// the popup that opens next would be the start menu's own narrow panel, centred, under the gear.
    /// </summary>
    void Relay()
    {
        if (_laidFor == default) return;

        Lay(_laidFor, _laidAt);
    }

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
    /// so the thing that opened it is the thing it appears to come out of. <b>The start menu hangs off
    /// nothing</b> (<see cref="AtTheStart"/>) — it is the whole of what is on screen, so it stands in the
    /// middle of the window at a size of its own (<see cref="StartRoomPx"/>).
    /// </param>
    public void Lay(Vector2 uiPx, Rect anchor)
    {
        _laidFor = uiPx;
        _laidAt = anchor;
        _laidAtTheStart = AtTheStart;

        FillRows();

        // **The width is settled before the heights**, because on the start menu a description wraps to it
        // and a row is as tall as the lines that wrap came to. The popup's own width is what its widest row
        // wants, which is a measurement of the rows and not of the panel.
        //
        // **The window is the last word** (OBS-2k): what the rows want is what they are laid at until the
        // window has not got it, and then they are laid narrower. A panel that held its wanted width on a
        // window too narrow for it would be a panel with an edge off the glass — which is what the density
        // used to be dropped to prevent, at the cost of an interface nobody could read on a handset.
        var contentWidthPx = MathF.Min(
            WidestContentPx(uiPx) + Theme.InsetPx * 2f + ScrollBarPx + Theme.GapPx,
            uiPx.X - (Theme.MarginPx * 2f) - (Theme.PaddingPx * 2f));

        // The bar's room comes off the rows on every start-menu lay, scrolling or not: a description that
        // rewrapped the moment the list grew past the window is a panel that changes shape as it is read.
        WrapDescriptions(contentWidthPx - ScrollBarPx - Theme.GapPx);

        // **The rows answer to the window rather than the window to them.** A list of maps on a short
        // display grew the panel straight off the bottom of the screen, which is a menu hiding the
        // thing it was written to expose; what does not fit scrolls.
        var roomPx = AtTheStart
            ? MathF.Max(StartRoomPx(uiPx), LeastStartRoomPx())
            : PopupRoomPx(uiPx, anchor);

        _firstRow = Math.Clamp(_firstRow, 0, Math.Max(0, _rowCount - 1));
        while (_firstRow > 0 && HeightFrom(_firstRow - 1) <= roomPx) _firstRow--;

        // **The popup is as tall as its own page and the start menu is one size.** A band of empty panel
        // under the popup's last map, kept so that the other page would fit without the panel changing
        // height, reads as a list cut short rather than as a page that ended — where the start menu is
        // standing in a hole it has to keep inside, so there the height is the hole's and the list scrolls.
        _shownRows = Page == Maps ? Fitting(_firstRow, roomPx) : 0;
        var contentHeightPx = AtTheStart
            ? roomPx
            : MathF.Min(roomPx, Page switch
            {
                Maps => HeightOf(_firstRow, _shownRows),
                Figures => FiguresHeightPx,
                _ => LeastContentHeightPx,
            });

        var sizePx = new Vector2(
            Theme.PaddingPx * 2f + contentWidthPx, ChromeHeightPx(AtTheStart) + contentHeightPx);

        // Centred on every lay, which is where it already stood: the start menu is one size, so opening a
        // group moves neither its edges nor the row the pointer is resting on.
        var atPx = AtTheStart ? (uiPx - sizePx) * 0.5f : Theme.PopupAt(anchor, uiPx, sizePx.X);
        Box = new Rect(atPx, sizePx);

        // A tab the layout does not carry is laid as no rectangle at all, so it takes no click and draws
        // nothing. <b>The start menu carries only the way out</b>, and it stands on the title's own line,
        // at the end of it, which is where it already was when there was a strip to be the end of.
        var contentX = atPx.X + Theme.PaddingPx;
        var tabWidthPx = (contentWidthPx - (Theme.GapPx * (_tabs.Length - 1))) / _tabs.Length;
        for (var tab = 0; tab < _tabs.Length; tab++)
        {
            if (AtTheStart && !AtTheStartTab(tab))
            {
                _tabs[tab] = default;
                continue;
            }

            var atPxOfTab = AtTheStart
                ? new Vector2(contentX + contentWidthPx - ExitWidthPx, atPx.Y + Theme.PaddingPx)
                : new Vector2(contentX + tab * (tabWidthPx + Theme.GapPx), atPx.Y + TabsTopPx(false));

            _tabs[tab] = new Rect(atPxOfTab, new Vector2(AtTheStart ? ExitWidthPx : tabWidthPx, Theme.RowPx));
        }

        var contentTopY = atPx.Y + ContentTopPx(AtTheStart);

        // The bar takes its room off the rows, and only on a page that has one: a row ending short of
        // the panel edge on one page and flush with the tabs on another is two paddings. The start menu
        // gives it up on every lay, since that is the width its descriptions were broken to.
        var rowWidthPx = Scrolls || AtTheStart ? contentWidthPx - ScrollBarPx - Theme.GapPx : contentWidthPx;
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

    /// <summary>
    /// A group header is one row of chrome, and a map is its name over however many lines what it is came
    /// to (<see cref="WrapDescriptions"/>) — which on the popup is always the one it is laid wide enough
    /// for, and on the start menu is as many as the width it is laid at takes.
    /// </summary>
    float HeightOfRow(int row) =>
        _rowGroup[row] >= 0 ? Theme.RowPx : RowChromePx + TextHeightPx(NamePx(AtTheStart), _rowLines[row]);

    /// <summary>A name and the lines under it, without the room the row keeps clear above and below them.</summary>
    static float TextHeightPx(float namePx, int lines) =>
        namePx + (lines * (Theme.GapPx * 0.5f + Theme.SmallTextPx));

    /// <summary>
    /// What a two-line row keeps clear of its own edges, taken from the row height the theme already
    /// ships: a row that grows a line grows by that line and by nothing else.
    /// </summary>
    static readonly float RowChromePx = Theme.TallRowPx - TextHeightPx(Theme.TextPx, 1);

    /// <summary>
    /// Each map's description broken to the width the rows are laid at. <b>The popup is laid wide enough
    /// for the longest of them</b> and so never breaks one; the start menu is laid to a share of the
    /// window and breaks nearly all of them.
    /// </summary>
    void WrapDescriptions(float rowWidthPx)
    {
        var intoPx = rowWidthPx - (Theme.InsetPx * 2f);
        for (var row = 0; row < _rowCount; row++)
        {
            _rowLines[row] = _rowGroup[row] >= 0
                ? 0
                : GlyphSheet.WrapLines(
                    _rowDescriptions[row], intoPx, Theme.SmallTextPx, LinesOf(row));
        }
    }

    /// <summary>Where one row's broken description is kept, which is its own run of the flat array.</summary>
    Span<Range> LinesOf(int row) => _rowLine.AsSpan(row * MostDescriptionLines, MostDescriptionLines);

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
    /// <remarks>
    /// <b>The start menu is not measured against its rows at all</b> (<see cref="AtTheStart"/>): it is laid
    /// to a share of the window and the descriptions wrap to that, because what it has to keep inside is
    /// the picture behind it rather than the longest sentence in the catalogue. What it is still measured
    /// against is its own furniture — the title, and the way out standing on the title's line.
    /// </remarks>
    float WidestContentPx(Vector2 uiPx)
    {
        if (AtTheStart)
        {
            var furniturePx = GlyphSheet.WidthPx(Title.Length, Theme.HeadingPx) + Theme.GapPx + ExitWidthPx;
            return MathF.Max(furniturePx, MathF.Min(uiPx.X, uiPx.Y) * StartWidthShare);
        }

        var wantedPx = LeastContentPx;
        foreach (var map in MapCatalogue.Shipped())
        {
            wantedPx = MathF.Max(wantedPx, RowWidthPx(map.Name, map.Description, atTheStart: false));
        }

        foreach (var group in GroupNames)
        {
            wantedPx = MathF.Max(wantedPx, GlyphSheet.WidthPx(group.Length + GroupMark.Length, Theme.TextPx));
        }

        return MathF.Max(
            wantedPx,
            MathF.Max(WidestPx(Lines, Theme.TextPx), WidestPx(TrimFigures.Names, Theme.TextPx) + TrimShareRoomPx));
    }

    /// <summary>The room a trim's own share is drawn in, kept off the end of its name so the two never meet.</summary>
    static readonly float TrimShareRoomPx = GlyphSheet.WidthPx("1000%".Length, Theme.TextPx) + Theme.InsetPx * 2f;

    /// <summary>
    /// What a row wants to be laid at so that nothing in it breaks. <b>The popup's own question</b>: the
    /// start menu is laid to a share of the window instead and its descriptions wrap to that.
    /// </summary>
    static float RowWidthPx(string name, string description, bool atTheStart) => MathF.Max(
        GlyphSheet.WidthPx(name.Length, NamePx(atTheStart)),
        GlyphSheet.WidthPx(description.Length, Theme.SmallTextPx));

    /// <summary>Whether what was laid is still the layout that would be laid now.</summary>
    public bool LaidFor(Vector2 uiPx, Rect anchor) =>
        _laidFor == uiPx && _laidAt == anchor && _laidAtTheStart == AtTheStart;

    /// <summary>Whether the page showing has more rows than the window has room for.</summary>
    bool Scrolls => _shownRows < _rowCount;
}
