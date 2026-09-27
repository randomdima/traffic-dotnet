using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Render;
using TrafficSimulation.App.Screen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Hud;

/// <summary>What the menu was asked for, once something on it has been clicked.</summary>
internal readonly record struct MenuChoice(MenuAction Action, string Name)
{
    public static MenuChoice None => default;
}

internal enum MenuAction : byte
{
    None,
    OpenMap,
    Quit,
}

/// <summary>
/// <b>There is one menu and it hangs off the gear.</b> Two pages — the map to open, and everything a debug
/// session turns, cut into sections (OBS-2y) — and the way out of the game as a third tab beside them.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a popup and not a mode.</b> The gear opens and shuts it, Escape does the same, and a click
/// anywhere off the panel shuts it — so there is no close button on it, and the run underneath keeps
/// its keys while it is up. Nothing here tears a town down: the world, the loop, the camera and the
/// renderer are replaced by <em>opening a map</em> and by nothing else.
/// </para>
/// <para>
/// <b>GEN-1b is a fact about the state and not about a second panel</b>: at start no city is loaded, so
/// what is behind the menu is the idle ring the game opens on — or, for the moment a page is still
/// fetching that, nothing at all. It is the same panel with the same rows, laid <em>in the middle of the
/// window</em> and a size larger (<see cref="AtTheStart"/>), and it cannot be shut: the ring is what a
/// reader looks at while deciding rather than a town they chose, so there is nothing to shut it onto and
/// the only two ways past it are picking a map and the exit tab.
/// </para>
/// <para>
/// <b>OBS-2a — the map list it reads is the map list the command line reads</b>
/// (<see cref="MapCatalogue"/>). A map that opens one way and not the other is two lists.
/// </para>
/// <para>
/// <b>Everything is laid once and kept</b> until the window, the page or a group changes, so what was
/// drawn and what a click is tested against are the same rectangles rather than two copies of one
/// layout.
/// </para>
/// </remarks>
internal sealed partial class Menu
{
    /// <summary>The pages, in the order their tabs run across the top of the panel.</summary>
    public const int Maps = 0;

    /// <summary>
    /// <b>Everything a debug session turns, a section a thing it is opened to look at</b> (OBS-2y,
    /// <see cref="DebugLayers.Sections"/>): the layers, the road's trims beside the car layers they are felt
    /// in, the shell probe's figures beside the boundary it is struck off, and the ground's own layers beside
    /// the wireframe drawn over them.
    /// </summary>
    public const int Debug = 1;

    const int Pages = 2;

    /// <summary>The last tab, which is a button rather than a page: it leaves the game.</summary>
    public const int ExitTab = 2;

    static readonly string[] TabNames = ["Maps", "Debug", "Exit"];

    /// <summary>
    /// The widest a tab is laid. <b>A tab is a label and not a share of the panel</b>: three of them spread
    /// across a panel laid for the longest map description are three bars a third of a screen wide.
    /// </summary>
    const float MostTabPx = 150f;

    /// <summary>The two collapsible groups the map page is cut into, and which kind of map each holds.</summary>
    public const int MainMaps = 0;

    public const int Scenarios = 1;
    const int Groups = 2;

    static readonly string[] GroupNames = ["Main maps", "Debug scenarios"];

    static MapKind KindOf(int group) => group == MainMaps ? MapKind.Place : MapKind.Scenario;

    const int MostRows = 32;

    /// <summary>The bar down the rows when there are more of them than the window has room for.</summary>
    const float ScrollBarPx = 4f;

    /// <summary>What the content column is never narrower than, so a page of short rows still reads as a menu.</summary>
    const float LeastContentPx = 460f;

    /// <summary>
    /// <b>The start menu has no tab strip</b> (<see cref="AtTheStart"/>): it carries one page, and a strip
    /// of one tab is a row of chrome that says nothing. The way out keeps its place as the last thing
    /// across the top, which there is the title's own line.
    /// </summary>
    static bool AtTheStartTab(int tab) => tab == ExitTab;

    /// <summary>
    /// What the start menu writes a map's name in. <b>One size up</b>, because it is read across a room
    /// from a screen nothing else is on rather than glanced at beside a running town — and it is the
    /// theme's own heading size rather than a sixth figure of this panel's. <b>The line under it is not</b>:
    /// it is a sentence rather than a label, it wraps, and a sentence a size up is a panel that covers the
    /// picture it is standing on.
    /// </summary>
    static float NamePx(bool atTheStart) => atTheStart ? Theme.HeadingPx : Theme.TextPx;

    /// <summary>
    /// How much of the window's short side the start menu is laid across. <b>It is the field inside the
    /// idle ring that this figure is really about</b>: the ring is laid to the opening view across the
    /// short side (`OBS-1b`), so the grass in the middle of it is a fixed share of that side too, and a
    /// panel measured against a wide window's width would stand across the road instead of in the field.
    /// </summary>
    /// <remarks>
    /// <b>The field is a rounded square and not a disc</b> (<c>IdlePlan.CornerShare</c>), which is what
    /// makes this a width rather than a diagonal: the panel is laid against the straight sides, and only a
    /// panel grown to nearly the whole field reaches the corners at all. This is the width that leaves the
    /// collapsed list inside it once its descriptions have wrapped and grown the rows.
    /// </remarks>
    const float StartWidthShare = 0.45f;

    /// <summary>
    /// And how much of it the panel is tall. <b>The start menu is one size whatever is open in it</b>: it
    /// stands in the field inside the ring, and a panel that grew as a group opened would grow out across
    /// the road — so the height is the field's and never the list's, and a list longer than it scrolls.
    /// </summary>
    /// <remarks>
    /// It is a share of the <em>short</em> side like the width, and for the same reason: the field is
    /// square and is a fixed share of the side the opening view is a figure across (`OBS-1b`). A little
    /// under half leaves the panel's top and bottom edges inside the straight sides of the loop.
    /// </remarks>
    const float StartHeightShare = 0.5f;

    /// <summary>
    /// And how far down the window the popup under the gear may reach, as a share of the window's height.
    /// <b>A popup is furniture beside a town and not a panel over it</b>: one that runs from the corner
    /// button to the bottom edge is the full-screen menu it replaced, drawn over the very town every row on
    /// it is a question about. A page longer than this scrolls, as it already does on a short window.
    /// </summary>
    const float PopupHeightShare = 0.5f;

    /// <summary>The most lines a description is broken across before what is left of it is dropped.</summary>
    const int MostDescriptionLines = 3;

    /// <summary>The way out, as wide as its own word and no wider — it stands on the title's line.</summary>
    static readonly float ExitWidthPx =
        GlyphSheet.WidthPx(TabNames[ExitTab].Length, Theme.TextPx) + (Theme.InsetPx * 2f);

    /// <summary>What the panel calls itself, which is the widest thing on a page of few short rows.</summary>
    const string Title = "traffic-dotnet";

    /// <summary>The mark ahead of an open group's name, which is also the room a shut one's takes.</summary>
    const string GroupMark = "- ";

    /// <summary>The middle of a laid row, which is what the suite clicks to ask the layout and the hit test the same question.</summary>
    public Vector2 RowMiddlePx(int row) => Middle(_rows[row - _firstRow]);

    /// <summary>The middle of one tab, on the same terms.</summary>
    public Vector2 TabMiddlePx(int tab) => Middle(_tabs[tab]);

    /// <summary>And of one section's row down the side of the debug page.</summary>
    public Vector2 SectionMiddlePx(int section) => Middle(_sections[section]);

    /// <summary>And of the row that turns every layer off.</summary>
    public Vector2 AllOffMiddlePx => Middle(_allOff);

    /// <summary>
    /// And of one layer's row, in the section showing. <b>A layer another section holds has no row here</b>, and
    /// asking for one is a test asking the wrong page.
    /// </summary>
    public Vector2 LayerMiddlePx(DebugLayer layer)
    {
        var layers = DebugLayers.Sections[Section].Layers;
        var row = Array.IndexOf(layers, layer);
        if (row < 0) throw new InvalidOperationException($"{layer} is not in {DebugLayers.Sections[Section].Name}");

        return Middle(_layerRows[row]);
    }

    /// <summary>And of one row of the ground's own layers, or of the row that puts them all back.</summary>
    public Vector2 GroundMiddlePx(int row) => Middle(_grounds[row]);

    /// <summary>And of one slider's track, which is where a click puts that figure back where it started.</summary>
    public Vector2 TrimMiddlePx(int slider) => Middle(_trims[slider]);

    /// <summary>
    /// Where along one slider's track a value falls, for a caller pointing at one rather than clicking
    /// blind. <b>In that row's own units</b> — a share of the shipped figure for a trim, and metres for the
    /// probe's distance (<see cref="ShellRow"/>).
    /// </summary>
    public Vector2 TrimAtPx(int slider, float value)
    {
        var box = _trims[slider];
        return new Vector2(
            box.AtPx.X + (TrackInsetPx + (WhereOnTheTrack(slider, value) * TrackWidthPx(box))), Middle(box).Y);
    }

    static Vector2 Middle(Rect box) => box.AtPx + box.SizePx * 0.5f;

    /// <summary>How many rows the map page laid, group headers and all.</summary>
    public int RowCount => _rowCount;

    /// <summary>And how tall one of them came out, which is what a wrapped description is read off.</summary>
    public float RowHeightPx(int row) => HeightOfRow(row);

    /// <summary>The wheel over a page longer than the window, in notches.</summary>
    public void Scroll(float notches)
    {
        if (!Scrolls) return;

        _firstRow = Math.Clamp(_firstRow - (int)MathF.Round(notches), 0, _rowCount - _shownRows);
        Lay(_laidFor, _laidAt);
    }

    /// <summary>
    /// The same page dragged rather than wheeled, by the pixels the pointer went down the panel. <b>It is
    /// the only scroll a finger has</b> — a handset has no wheel to take (CTL-9) — and the pointer that
    /// asks for it is the mouse's as well, so the list answers one gesture and not two.
    /// </summary>
    /// <remarks>
    /// <b>The rows come and go whole, so the travel is held and spent as each row's own height goes by.</b>
    /// A list whose descriptions wrap has no pitch to divide by: its rows are as tall as what is written in
    /// them, and a scroll in pixels divided by an average would drift a row every screenful. What is left
    /// over at either end of the list is dropped rather than banked, so a drag off the bottom does not have
    /// to be unwound before the list comes back.
    /// </remarks>
    void ScrollByPixels(float downPx)
    {
        if (!Scrolls)
        {
            _draggedPx = 0f;
            return;
        }

        _draggedPx += downPx;
        var firstRow = _firstRow;

        while (_draggedPx > 0f && firstRow > 0 && _draggedPx >= HeightOfRow(firstRow - 1) + Theme.GapPx)
        {
            _draggedPx -= HeightOfRow(firstRow - 1) + Theme.GapPx;
            firstRow--;
        }

        var lastFirstRow = _rowCount - _shownRows;
        while (_draggedPx < 0f && firstRow < lastFirstRow && -_draggedPx >= HeightOfRow(firstRow) + Theme.GapPx)
        {
            _draggedPx += HeightOfRow(firstRow) + Theme.GapPx;
            firstRow++;
        }

        if ((firstRow == 0 && _draggedPx > 0f) || (firstRow >= lastFirstRow && _draggedPx < 0f)) _draggedPx = 0f;

        if (firstRow == _firstRow) return;

        _firstRow = firstRow;
        Lay(_laidFor, _laidAt);
    }

    /// <summary>A click on the menu. Everything it can do is here, so nothing outside it has to know its layout.</summary>
    public MenuChoice Click(Vector2 pointPx, DebugSwitches switches, TrimFigures trims)
    {
        for (var tab = 0; tab < _tabs.Length; tab++)
        {
            if (!_tabs[tab].Contains(pointPx)) continue;

            if (tab == ExitTab) return new MenuChoice(MenuAction.Quit, string.Empty);

            OpenAt(tab);
            Lay(_laidFor, _laidAt);
            return MenuChoice.None;
        }

        if (Page == Maps)
        {
            PressedRow(pointPx);
            return MenuChoice.None;
        }

        return ClickedDebug(pointPx, switches, trims);
    }

    /// <summary>
    /// A click on the ground's own layers: one taken out of the picture or put back, or the row under them
    /// that puts the whole of it back. <b>The rows that read the mesh take no click</b> — they are what the
    /// section is looked at for and there is nothing about them to press.
    /// </summary>
    MenuChoice ClickedGroundRow(Vector2 pointPx, GroundSwitches ground)
    {
        for (var row = 0; row < _grounds.Length; row++)
        {
            if (!_grounds[row].Contains(pointPx)) continue;

            if (row == WholeGroundRow) ground.ShowWhole();
            else if (row < GroundParts.Count) ground.Toggle((GroundPart)row);

            return MenuChoice.None;
        }

        return MenuChoice.None;
    }

    /// <summary>
    /// A press on a slider: the row it landed in is taken hold of and moved to where the pointer is, and it
    /// stays held until the button comes up (<see cref="Pointer"/>). The row past the last trim is the one
    /// that puts every trim back where the build shipped it.
    /// </summary>
    MenuChoice ClickedTrim(Vector2 pointPx, DebugSwitches switches, TrimFigures trims)
    {
        for (var slider = 0; slider < _trims.Length; slider++)
        {
            if (!_trims[slider].Contains(pointPx)) continue;

            if (slider == ResetRow)
            {
                trims.Reset();
                _figuresMoved = true;
                return MenuChoice.None;
            }

            _held = slider;
            Move(slider, ValueAt(slider, pointPx.X, _trims[slider]), switches, trims);
            return MenuChoice.None;
        }

        return MenuChoice.None;
    }

    /// <summary>
    /// A press on the map list. <b>It picks nothing</b>: a drag and a tap begin identically, and a row
    /// opened on the way down would be whichever row the scroll was started on top of (CTL-1b). What the
    /// press landed on is opened by <see cref="Pointer"/> on the way back up.
    /// </summary>
    void PressedRow(Vector2 pointPx)
    {
        _pressedAtPx = pointPx;
        _lastPointerPx = pointPx;
        _draggedPx = 0f;
        _pressedOnARow = true;
    }

    /// <summary>
    /// The pointer while a button is down, offered every frame, and <b>what the press turns out to have
    /// been</b>. A trim follows it and takes effect as it goes, so the town answers under the hand that is
    /// moving it — which is the whole of what makes that page an instrument rather than a form to be
    /// submitted; the map list scrolls under it and gives back the row the press landed on only where the
    /// pointer never travelled.
    /// </summary>
    /// <param name="dragPx">
    /// How far the pointer travels before the press is a drag rather than a tap (CTL-1b) — the figure the
    /// town is dragged by, handed in so that a tap on a panel and a tap on a road are the same movement.
    /// </param>
    public MenuChoice Pointer(
        Vector2 pointPx, bool held, float dragPx, DebugSwitches switches, TrimFigures trims)
    {
        if (_held >= 0)
        {
            if (held) Move(_held, ValueAt(_held, pointPx.X, _trims[_held]), switches, trims);
            else _held = -1;

            return MenuChoice.None;
        }

        if (!_pressedOnARow) return MenuChoice.None;

        if (held)
        {
            ScrollByPixels(pointPx.Y - _lastPointerPx.Y);
            _lastPointerPx = pointPx;
            return MenuChoice.None;
        }

        _pressedOnARow = false;
        return (pointPx - _pressedAtPx).LengthSquared() > dragPx * dragPx
            ? MenuChoice.None
            : ClickedRow(_pressedAtPx);
    }

    /// <summary>Whatever the pointer had hold of, let go of — the panel it was over is not the panel any more.</summary>
    void LetGo()
    {
        _held = -1;
        _pressedOnARow = false;
        _draggedPx = 0f;
    }

    /// <summary>
    /// One slider to where the pointer put it. <b>A pointer resting on a track is not a figure moving</b>:
    /// the value is read back after the clamp, so a drag held against either stop stands the town up once
    /// rather than every frame it is held there.
    /// </summary>
    /// <remarks>
    /// <b>The probe's two figures are not figures the town is stood up for</b> (<see cref="ShellRow"/>,
    /// OBS-2w): they move a line this overlay draws and nothing the town was laid with, so what they stale
    /// is the layer's own cache (<c>DebugSwitches.Generation</c>) and never the ground under it.
    /// </remarks>
    void Move(int slider, float toValue, DebugSwitches switches, TrimFigures trims)
    {
        if (slider == ShellRow)
        {
            switches.Shell.SetOutwardM(toValue);
            return;
        }

        if (slider == RoundingRow)
        {
            switches.Shell.SetRoundedM(toValue);
            return;
        }

        var wasShare = trims.Of(slider);
        trims.Set(slider, toValue);
        _figuresMoved |= trims.Of(slider) != wasShare;
    }

    /// <summary>
    /// Whether a figure has moved since this was last asked, which is what stands the town up again. Taken
    /// rather than read, because it is an edge and not a state.
    /// </summary>
    public bool TakeFiguresMoved()
    {
        if (!_figuresMoved) return false;

        _figuresMoved = false;
        return true;
    }

    /// <summary>
    /// <b>The row the shell probe's distance is dragged on</b> (OBS-2w, <see cref="ShellProbe"/>) — a
    /// track in metres rather than a share of a shipped figure.
    /// </summary>
    public const int ShellRow = TrimFigures.Count;

    /// <summary>
    /// And the row under it, which turns how tightly that shape is allowed to turn — a track in metres of
    /// radius, on its own stops and not the distance's.
    /// </summary>
    public const int RoundingRow = ShellRow + 1;

    /// <summary>How many rows of the debug page are a figure to be dragged: one a trim, and the probe's two.</summary>
    public const int Sliders = RoundingRow + 1;

    /// <summary>The row under them, which is not one: it puts every figure back where the build shipped it.</summary>
    public const int ResetRow = Sliders;

    /// <summary>How much of a trim row is chrome either side of the track it is dragged along.</summary>
    const float TrackInsetPx = Theme.InsetPx;

    static float TrackWidthPx(Rect box) => MathF.Max(1f, box.SizePx.X - (TrackInsetPx * 2f));

    /// <summary>
    /// <b>A trim's track is a decade either side of shipped and is laid out logarithmically</b>, so 100% is
    /// the middle of it and halving reads as far from the centre as doubling. A linear track would put
    /// shipped a tenth of the way along and give nine tenths of the travel to figures nobody wants.
    /// </summary>
    static readonly float Decades = MathF.Log(TrimFigures.Most / TrimFigures.Least);

    /// <summary>
    /// <b>How far along its own track a slider's value stands</b>, which is the one place the kinds of row
    /// differ: a trim is a share read logarithmically, and the probe's two figures are read straight off a
    /// track between their own stops (<see cref="ShellRow"/>, <see cref="RoundingRow"/>). <b>Read here and
    /// written in <see cref="ValueAt"/>, and nowhere else</b> — a track drawn on one mapping and dragged on
    /// another is a knob that does not follow the pointer.
    /// </summary>
    static float WhereOnTheTrack(int slider, float value) => slider switch
    {
        ShellRow => (Math.Clamp(value, ShellProbe.LeastM, ShellProbe.MostM) - ShellProbe.LeastM) / ProbeTravelM,
        RoundingRow => (Math.Clamp(value, ShellProbe.LeastRoundM, ShellProbe.MostRoundM) - ShellProbe.LeastRoundM)
                       / RoundingTravelM,
        _ => MathF.Log(Math.Clamp(value, TrimFigures.Least, TrimFigures.Most) / TrimFigures.Least) / Decades,
    };

    /// <summary>And what the pointer standing at a place on that track asks for, in that row's own units.</summary>
    static float ValueAt(int slider, float atX, Rect box)
    {
        var along = Math.Clamp((atX - box.AtPx.X - TrackInsetPx) / TrackWidthPx(box), 0f, 1f);

        return slider switch
        {
            ShellRow => ShellProbe.LeastM + (along * ProbeTravelM),
            RoundingRow => ShellProbe.LeastRoundM + (along * RoundingTravelM),
            _ => TrimFigures.Least * MathF.Exp(Decades * along),
        };
    }

    /// <summary>What the slider reads now, in the units its own track is laid in.</summary>
    static float ValueOf(int slider, DebugSwitches switches, TrimFigures trims) => slider switch
    {
        ShellRow => switches.Shell.OutwardM,
        RoundingRow => switches.Shell.RoundedM,
        _ => trims.Of(slider),
    };

    /// <summary>
    /// And where that row's track is filled from: what the build ships for a trim, and where each of the
    /// probe's figures stands before anybody turns it. <b>A row is read by how far it has been taken from
    /// its own home</b>, which is what the mark on the track stands at.
    /// </summary>
    static float HomeOf(int slider) => slider switch
    {
        ShellRow => ShellProbe.StartsAtM,
        RoundingRow => ShellProbe.StartsRoundM,
        _ => 1f,
    };

    /// <summary>The name a slider's row carries, which is the figure's own and not this panel's.</summary>
    static string NameOf(int slider) => slider switch
    {
        ShellRow => ShellProbe.Named,
        RoundingRow => ShellProbe.NamedRounding,
        _ => TrimFigures.Names[slider],
    };

    static float ProbeTravelM => ShellProbe.MostM - ShellProbe.LeastM;

    static float RoundingTravelM => ShellProbe.MostRoundM - ShellProbe.LeastRoundM;

    MenuChoice ClickedRow(Vector2 pointPx)
    {
        for (var slot = 0; slot < _shownRows && _firstRow + slot < _rowCount; slot++)
        {
            if (!_rows[slot].Contains(pointPx)) continue;

            var row = _firstRow + slot;
            if (_rowGroup[row] < 0) return new MenuChoice(MenuAction.OpenMap, _rowNames[row]);

            _groupOpen[_rowGroup[row]] = !_groupOpen[_rowGroup[row]];
            _firstRow = 0;
            Lay(_laidFor, _laidAt);
            return MenuChoice.None;
        }

        return MenuChoice.None;
    }

    void AddGroup(int group)
    {
        if (_rowCount >= MostRows) return;

        _rowNames[_rowCount] = GroupNames[group];
        _rowDescriptions[_rowCount] = string.Empty;
        _rowGroup[_rowCount] = group;
        _rowCount++;
    }

    void AddMap(string name, string description)
    {
        if (_rowCount >= MostRows) return;

        _rowNames[_rowCount] = name;
        _rowDescriptions[_rowCount] = description;
        _rowGroup[_rowCount] = -1;
        _rowCount++;
    }
}
