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
    RunCheck,
    ReRollSeeds,
    Close,
    Quit,
}

/// <summary>
/// <b>There is one menu.</b> Which map is open is a page of it, exactly as the debug switches, the
/// seeds, the pace and the control legend are pages of it — <b>OBS-2</b>, <b>OBS-2a</b> and
/// <b>OBS-2g</b> are three claims about the same panel.
/// </summary>
/// <remarks>
/// <para>
/// <b>GEN-1b is a fact about the state and not about a second panel</b>: at start no map is loaded, so
/// the menu is what is on the screen and there is nothing behind it. Load one and the same menu closes
/// over the town it built; Escape and the gear open it again, and closing it leaves that town exactly
/// as it was. Nothing here tears a town down — the world, the loop, the camera and the renderer are
/// replaced by <em>opening a map</em> and by nothing else.
/// </para>
/// <para>
/// <b>OBS-2a — the lists it reads are the lists the command line reads</b>, both for the maps
/// (<see cref="MapCatalogue"/>) and for the checks (<see cref="CheckCatalogue"/>). A check nobody can
/// launch is a check nobody runs, and a map that opens one way and not the other is two lists.
/// </para>
/// <para>
/// <b>Everything is laid once a frame and kept</b>, so what was drawn and what a click is tested
/// against are the same rectangles rather than two copies of one layout.
/// </para>
/// </remarks>
internal sealed partial class Menu
{
    /// <summary>The pages, in the order their tabs run down the panel's edge.</summary>
    public const int Places = 0;

    public const int Scenarios = 1;
    public const int Checks = 2;
    public const int Layers = 3;
    public const int Seeds = 4;
    public const int Pace = 5;
    public const int Controls = 6;

    static readonly string[] PageNames = ["Places", "Scenarios", "Checks", "Debug layers", "Seeds", "Pace", "Controls"];

    /// <summary>The three pages that are a list of things to open, which are the ones that scroll.</summary>
    static bool IsList(int page) => page <= Checks;

    /// <summary>The three pages that are about a town, and say so rather than lying when there is none.</summary>
    static bool NeedsTown(int page) => page is Layers or Seeds or Pace;

    const int MostRows = 32;

    /// <summary>The most short rows any page lays, which is what the content column is never shorter than.</summary>
    const int MostLines = 8;

    /// <summary>The bar down the rows when there are more of them than the window has room for.</summary>
    const float ScrollBarPx = 4f;

    /// <summary>What the content column is never narrower than, so a page of short rows still reads as a menu.</summary>
    const float LeastContentPx = 460f;

    /// <summary>
    /// <b>Every control the player has</b>, which is what the <c>Controls</c> page claims to be. Key
    /// then meaning, in pairs.
    /// </summary>
    /// <remarks>
    /// <b>Printable ASCII only</b>, here and in every other string the interface draws: the glyph
    /// sheet carries that range and nothing else, so an em dash is drawn as a space and reads as a
    /// missing word rather than as a missing glyph.
    /// </remarks>
    static readonly string[] ControlLegend =
    [
        "Arrows / drag / wheel", "Camera, unless a unit is being driven",
        "Left-click", "Select a unit; click nothing to deselect",
        "Right-click", "Order the selected walker to walk there",
        "W A S D", "Take the wheel: throttle, brake, steer",
        "Space", "Handbrake: a car's rear pair, a walker stands",
        "R", "Release the wheel; the unit decides again",
        "1 2 3", "Pace, as a multiple of real time, capped at 3x",
        "`", "Freeze: nothing decides, steps, collides or ages",
        "Pause", "Hold the agents; the bodies keep stepping",
        "F11", "Fullscreen",
        "Esc", "Opens and closes this menu; the gear does too",
    ];

    /// <summary>Every fixed line the pages that are not a list draw, so the panel is wide enough for all of them at once.</summary>
    static readonly string[] Lines =
    [
        "Frame read-out", "Car lines", "Walker lines", "Nodes and links", "Lane reservations", "Collision",
        "Ruler", "Track figures",
        "World seed   18446744073709551615", "Agent seed   18446744073709551615",
        "The world seed is the town file's own and changes with the map.",
        "Re-roll the agent seed and rebuild",
        "Pace   1.0x   (frozen)", "Agents held  (Pause)",
        "1 / 2 / 3   set the pace, capped at 3x", "`           freeze and unfreeze",
        "A pace above 4x integrates the physics coarsely and manufactures",
        "collisions the model never had, so the cap is kept.",
        NoTown,
    ];

    const string NoTown = "No town yet - pick one on the Places page.";

    /// <summary>The middle of a laid row, which is what the suite clicks to ask the layout and the hit test the same question.</summary>
    public Vector2 RowMiddlePx(int row) => _rows[row - _firstRow].AtPx + _rows[row - _firstRow].SizePx * 0.5f;

    /// <summary>The wheel over a page longer than the window, in notches.</summary>
    public void Scroll(float notches)
    {
        if (!Scrolls) return;

        _firstRow = Math.Clamp(_firstRow - (int)MathF.Round(notches), 0, _rowCount - _shownRows);
    }

    /// <summary>A click on the menu. Everything it can do is here, so nothing outside it has to know its layout.</summary>
    public MenuChoice Click(Vector2 pointPx, bool hasTown, DebugSwitches switches, RunState run)
    {
        for (var tab = 0; tab < _tabs.Length; tab++)
        {
            if (!_tabs[tab].Contains(pointPx)) continue;

            OpenAt(tab);
            Lay(_laidFor, _laidWithTown);
            return MenuChoice.None;
        }

        if (hasTown && _close.Contains(pointPx)) return new MenuChoice(MenuAction.Close, string.Empty);
        if (_quit.Contains(pointPx)) return new MenuChoice(MenuAction.Quit, string.Empty);

        if (IsList(Page))
        {
            for (var slot = 0; slot < _shownRows && _firstRow + slot < _rowCount; slot++)
            {
                if (!_rows[slot].Contains(pointPx)) continue;

                var row = _firstRow + slot;
                return new MenuChoice(_rowIsCheck[row] ? MenuAction.RunCheck : MenuAction.OpenMap, _rowNames[row]);
            }

            return MenuChoice.None;
        }

        if (!hasTown) return MenuChoice.None;

        switch (Page)
        {
            case Layers:
                if (_lines[0].Contains(pointPx)) switches.Toggle(ref switches.FrameReadout);
                else if (_lines[1].Contains(pointPx)) switches.Toggle(ref switches.CarLines);
                else if (_lines[2].Contains(pointPx)) switches.Toggle(ref switches.WalkerLines);
                else if (_lines[3].Contains(pointPx)) switches.Toggle(ref switches.Nodes);
                else if (_lines[4].Contains(pointPx)) switches.Toggle(ref switches.Reservations);
                else if (_lines[5].Contains(pointPx)) switches.Toggle(ref switches.Collision);
                else if (_lines[6].Contains(pointPx)) switches.Toggle(ref switches.Ruler);
                else if (_lines[7].Contains(pointPx)) switches.Toggle(ref switches.TrackFigures);
                break;

            case Seeds when _lines[3].Contains(pointPx):
                return new MenuChoice(MenuAction.ReRollSeeds, string.Empty);

            case Pace when _lines[1].Contains(pointPx):
                run.AgentsHeld = !run.AgentsHeld;
                break;
        }

        return MenuChoice.None;
    }

    void AddRow(string name, string description, bool isCheck)
    {
        if (_rowCount >= MostRows) return;

        _rowNames[_rowCount] = name;
        _rowDescriptions[_rowCount] = description;
        _rowIsCheck[_rowCount] = isCheck;
        _rowCount++;
    }
}
