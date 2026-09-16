using System.Numerics;

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
/// <b>The cells are the world's and not the view's</b>: their corners stand at whole numbers of the
/// spacing from the world origin, so which stones a pass refuses does not change when the camera moves.
/// </para>
/// </remarks>
internal sealed class MarkClaims
{
    /// <summary>The claims of a pass that keeps none — an agent's own two pieces of route, which are one line and no fan.</summary>
    public static readonly MarkClaims None = new();

    /// <summary>Where a claim stands, and which way the mark standing there says its ground is travelled.</summary>
    Vector2[] _atM = [];
    Vector2[] _facing = [];

    Vector2 _cornerM;
    int _across;
    int _down;
    float _apartM;

    /// <summary>
    /// Nothing claimed, and the ground the pass about to be drawn covers. <b>A pass with no spacing to keep
    /// claims nothing and refuses nothing</b>, which is what a framing too far out to draw a mark at all
    /// asks for.
    /// </summary>
    public void Clear(Vector2 centreM, Vector2 spanM, float apartM)
    {
        _across = 0;
        if (!float.IsFinite(apartM) || apartM <= 0f) return;

        _cornerM = Floored(centreM - (spanM * 0.5f), apartM);
        _apartM = apartM;
        _across = (int)MathF.Ceiling(spanM.X / apartM) + 1;
        _down = (int)MathF.Ceiling(spanM.Y / apartM) + 1;

        var cells = _across * _down;
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
        if (_across == 0) return true;

        var across = (int)MathF.Floor((atM.X - _cornerM.X) / _apartM);
        var down = (int)MathF.Floor((atM.Y - _cornerM.Y) / _apartM);
        if (across < 0 || down < 0 || across >= _across || down >= _down) return true;

        // A cell is the spacing across, so everything within the spacing of this mark stands in one of the
        // nine around it.
        for (var row = Math.Max(0, down - 1); row <= Math.Min(_down - 1, down + 1); row++)
        {
            for (var column = Math.Max(0, across - 1); column <= Math.Min(_across - 1, across + 1); column++)
            {
                var cell = (row * _across) + column;
                if (_facing[cell] == Vector2.Zero || Vector2.Dot(_facing[cell], facing) <= 0f) continue;
                if (Vector2.DistanceSquared(_atM[cell], atM) < _apartM * _apartM) return false;
            }
        }

        var own = (down * _across) + across;
        if (_facing[own] == Vector2.Zero)
        {
            _atM[own] = atM;
            _facing[own] = facing;
        }

        return true;
    }

    static Vector2 Floored(Vector2 atM, float apartM) =>
        new(MathF.Floor(atM.X / apartM) * apartM, MathF.Floor(atM.Y / apartM) * apartM);
}
