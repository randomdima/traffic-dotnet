using System.Numerics;
using TrafficSimulation.App.Debug;
using TrafficSimulation.App.Screen;

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
/// <b>There is one menu and it hangs off the gear.</b> Two pages — the map to open and the debug
/// switches — and the way out of the game as a third tab beside them.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a popup and not a mode.</b> The gear opens and shuts it, Escape does the same, and a click
/// anywhere off the panel shuts it — so there is no close button on it, and the run underneath keeps
/// its keys while it is up. Nothing here tears a town down: the world, the loop, the camera and the
/// renderer are replaced by <em>opening a map</em> and by nothing else.
/// </para>
/// <para>
/// <b>GEN-1b is a fact about the state and not about a second panel</b>: at start no map is loaded, so
/// the menu is what is on the screen and there is nothing behind it. It is the same popup in the same
/// corner either way — only what is behind it changes, and with nothing behind it there is nothing to
/// shut it onto, so it stays up until a map is picked.
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

    public const int Debug = 1;
    const int Pages = 2;

    /// <summary>The third tab, which is a button rather than a page: it leaves the game.</summary>
    public const int ExitTab = 2;

    static readonly string[] TabNames = ["Maps", "Debug", "Exit"];

    /// <summary>The two collapsible groups the map page is cut into, and which kind of map each holds.</summary>
    public const int MainMaps = 0;

    public const int Scenarios = 1;
    const int Groups = 2;

    static readonly string[] GroupNames = ["Main maps", "Debug scenarios"];

    static MapKind KindOf(int group) => group == MainMaps ? MapKind.Place : MapKind.Scenario;

    const int MostRows = 32;

    /// <summary>The most switch rows the debug page lays, which is what the content column is never shorter than.</summary>
    const int MostLines = 8;

    /// <summary>The bar down the rows when there are more of them than the window has room for.</summary>
    const float ScrollBarPx = 4f;

    /// <summary>What the content column is never narrower than, so a page of short rows still reads as a menu.</summary>
    const float LeastContentPx = 460f;

    /// <summary>
    /// Every fixed line the debug page draws, so the panel is wide enough for all of them at once.
    /// </summary>
    /// <remarks>
    /// <b>Printable ASCII only</b>, here and in every other string the interface draws: the glyph
    /// sheet carries that range and nothing else, so an em dash is drawn as a space and reads as a
    /// missing word rather than as a missing glyph.
    /// </remarks>
    static readonly string[] Lines =
    [
        "Car lines", "Walker lines", "Nodes and links", "Lane reservations", "Collision", "Turn circles", "Ruler",
        "Track figures",
    ];

    /// <summary>The middle of a laid row, which is what the suite clicks to ask the layout and the hit test the same question.</summary>
    public Vector2 RowMiddlePx(int row) => Middle(_rows[row - _firstRow]);

    /// <summary>The middle of one tab, and of one switch row, on the same terms.</summary>
    public Vector2 TabMiddlePx(int tab) => Middle(_tabs[tab]);

    public Vector2 LineMiddlePx(int line) => Middle(_lines[line]);

    static Vector2 Middle(Rect box) => box.AtPx + box.SizePx * 0.5f;

    /// <summary>How many rows the map page laid, group headers and all.</summary>
    public int RowCount => _rowCount;

    /// <summary>The wheel over a page longer than the window, in notches.</summary>
    public void Scroll(float notches)
    {
        if (!Scrolls) return;

        _firstRow = Math.Clamp(_firstRow - (int)MathF.Round(notches), 0, _rowCount - _shownRows);
        Lay(_laidFor, _laidAt);
    }

    /// <summary>A click on the menu. Everything it can do is here, so nothing outside it has to know its layout.</summary>
    public MenuChoice Click(Vector2 pointPx, DebugSwitches switches)
    {
        for (var tab = 0; tab < _tabs.Length; tab++)
        {
            if (!_tabs[tab].Contains(pointPx)) continue;

            if (tab == ExitTab) return new MenuChoice(MenuAction.Quit, string.Empty);

            OpenAt(tab);
            Lay(_laidFor, _laidAt);
            return MenuChoice.None;
        }

        if (Page == Maps) return ClickedRow(pointPx);

        for (var line = 0; line < MostLines; line++)
        {
            if (!_lines[line].Contains(pointPx)) continue;

            switches.Toggle(ref Switch(switches, line));
            break;
        }

        return MenuChoice.None;
    }

    /// <summary>
    /// The switch a row of the debug page stands for. <b>One place, so the row that is drawn and the
    /// row that is toggled cannot come apart</b> — they were two switch statements, and a layer
    /// inserted in the middle of the list toggled its neighbour.
    /// </summary>
    static ref bool Switch(DebugSwitches switches, int line)
    {
        switch (line)
        {
            case 0: return ref switches.CarLines;
            case 1: return ref switches.WalkerLines;
            case 2: return ref switches.Nodes;
            case 3: return ref switches.Reservations;
            case 4: return ref switches.Collision;
            case 5: return ref switches.TurnCircles;
            case 6: return ref switches.Ruler;
            default: return ref switches.TrackFigures;
        }
    }

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
