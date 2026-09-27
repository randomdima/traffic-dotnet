using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// The props as they are laid, over the grid that keeps <b>no two of them sharing any ground</b> (GEN-6c)
/// for a handful of comparisons apiece.
/// </summary>
/// <remarks>
/// <b>The two passes lay on patterns that know nothing of each other</b> (GEN-6b) — one walks the kerbs and
/// one sweeps a lattice — so neither pattern can be the index that keeps them apart, and a town's hundred
/// thousand discs cannot be asked pairwise. <b>The index is the town's grid</b> (SIM-8), at the level whose
/// cell covers the widest prop's own width and the clearance between two: a pair too near each other is
/// within that, so the cells round a candidate hold everything that could be, and everything outside them is
/// further off than the rule can care about.
/// </remarks>
internal sealed class PropScatter
{
    readonly int[] _head;
    readonly List<int> _next = [];
    readonly GridWindow _window;
    readonly int _reach;
    readonly float _apartM;

    PropScatter(GridWindow window, int reach, float apartM)
    {
        _window = window;
        _reach = reach;
        _apartM = apartM;
        _head = new int[window.Count];
        Array.Fill(_head, -1);
    }

    public List<Vector2> CentreM { get; } = [];

    public List<float> RadiusM { get; } = [];

    public List<float> BearingRad { get; } = [];

    public List<byte> Kind { get; } = [];

    public static PropScatter Over(WorldGrid grid, Vector2 acrossM, float widestM, float apartM)
    {
        var nearestM = widestM + apartM;
        var level = grid.Covering(nearestM);
        return new(
            GridWindow.Over(level, Vector2.Zero, Vector2.Max(acrossM, Vector2.Zero)), level.CellsWithin(nearestM),
            apartM);
    }

    /// <summary>Whether a candidate stands nearer a prop already laid than the clearance between them allows (GEN-6c).</summary>
    public bool Reaches(Vector2 atM, float reachM)
    {
        var column = _window.ClampX(_window.Level.CellOf(atM.X));
        var row = _window.ClampY(_window.Level.CellOf(atM.Y));
        for (var down = _window.ClampY(row - _reach); down <= _window.ClampY(row + _reach); down++)
        {
            for (var over = _window.ClampX(column - _reach); over <= _window.ClampX(column + _reach); over++)
            {
                for (var prop = _head[_window.IndexOf(over, down)]; prop >= 0; prop = _next[prop])
                {
                    var clearM = reachM + RadiusM[prop] + _apartM;
                    if (Vector2.DistanceSquared(CentreM[prop], atM) < clearM * clearM) return true;
                }
            }
        }

        return false;
    }

    public void Add(Vector2 atM, float reachM, float bearingRad, PropKind kind)
    {
        var square = _window.IndexAt(atM);

        _next.Add(_head[square]);
        _head[square] = CentreM.Count;

        CentreM.Add(atM);
        RadiusM.Add(reachM);
        BearingRad.Add(bearingRad);
        Kind.Add((byte)kind);
    }
}
