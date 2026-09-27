using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Debug;

/// <summary>
/// <b>The stones the marks of one pass have already taken</b>, so that what stands on one of them is a
/// mark and not a smear. Where a mark may fall is the town's grid to say (<see cref="MarkGrid"/>); which of
/// the lines crossing it there actually carries one is this.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the fan and not the straight that needs it.</b> Lines that run together cross the grid at the
/// same places, which is the whole point of the grid — but the movements leaving one lane end all set off
/// from one point, so a junction otherwise comes out with a chevron for every arm piled on one stone and a
/// reader cannot tell a fan from a blot.
/// </para>
/// <para>
/// <b>Two marks that say opposite things are not a smear.</b> A pair of lines over one piece of ground —
/// the way into a bay and the way back out of it, a stretch travelled both ways — is read by its chevrons
/// crossing, and that crossing is the whole of what tells the pair apart. So a stone is refused only to a
/// mark saying what the mark already standing there says.
/// </para>
/// <para>
/// <b>The cells are the town's grid's and not the view's</b> (SIM-8), on the level that covers the spacing,
/// so which stones a pass refuses does not change when the camera moves.
/// </para>
/// </remarks>
internal sealed class MarkClaims
{
    /// <summary>The claims of a pass that keeps none — an agent's own two pieces of route, which are one line and no fan.</summary>
    public static readonly MarkClaims None = new();

    /// <summary>Where a claim stands, and which way the mark standing there says its ground is travelled.</summary>
    Vector2[] _atM = [];
    Vector2[] _facing = [];

    /// <summary>The cells of the view, on the level of the grid that covers the spacing — empty where the pass keeps none.</summary>
    GridWindow _window;

    float _apartM;

    /// <summary>
    /// Nothing claimed, and the ground the pass about to be drawn covers. <b>A pass with no spacing to keep
    /// claims nothing and refuses nothing</b>, which is what a framing too far out to draw a mark at all
    /// asks for.
    /// </summary>
    public void Clear(WorldGrid grid, Vector2 centreM, Vector2 spanM, float apartM)
    {
        _window = default;
        if (!float.IsFinite(apartM) || apartM <= 0f) return;

        var halfM = spanM * 0.5f;
        _window = GridWindow.Over(grid.Covering(apartM), centreM - halfM, centreM + halfM);
        _apartM = apartM;

        var cells = _window.Count;
        if (_facing.Length < cells)
        {
            _atM = new Vector2[cells];
            _facing = new Vector2[cells];
        }

        Array.Clear(_facing, 0, cells);
    }

    /// <summary>
    /// <b>Whether a mark may stand here, taking the stone where it may.</b> True where nothing within the
    /// spacing already says what this mark says — and true outside the ground the pass was cleared for,
    /// where there is nothing to know.
    /// </summary>
    public bool Take(Vector2 atM, Vector2 facing)
    {
        if (_window.IsEmpty) return true;

        var (across, down) = _window.Level.CellOf(atM);
        if (!_window.Holds(across, down)) return true;

        // A cell covers the spacing, so everything within the spacing of this mark stands in the cells
        // round its own.
        var reach = _window.Level.CellsWithin(_apartM);
        for (var row = _window.ClampY(down - reach); row <= _window.ClampY(down + reach); row++)
        {
            for (var column = _window.ClampX(across - reach); column <= _window.ClampX(across + reach); column++)
            {
                var cell = _window.IndexOf(column, row);
                if (_facing[cell] == Vector2.Zero || Vector2.Dot(_facing[cell], facing) <= 0f) continue;
                if (Vector2.DistanceSquared(_atM[cell], atM) < _apartM * _apartM) return false;
            }
        }

        var own = _window.IndexOf(across, down);
        if (_facing[own] == Vector2.Zero)
        {
            _atM[own] = atM;
            _facing[own] = facing;
        }

        return true;
    }
}
