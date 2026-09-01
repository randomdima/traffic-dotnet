using System.Numerics;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// The props as they are laid, over the grid that keeps <b>no two of them sharing any ground</b> (GEN-6c)
/// for a handful of comparisons apiece.
/// </summary>
/// <remarks>
/// <b>The two passes lay on patterns that know nothing of each other</b> (GEN-6b) — one walks the kerbs and
/// one sweeps a lattice — so neither pattern can be the index that keeps them apart, and a town's hundred
/// thousand discs cannot be asked pairwise. The grid's square is the widest prop's own width and the
/// clearance between two: a pair too near each other is within that, so the nine squares round a candidate
/// hold everything that could be, and everything outside them is further off than the rule can care about.
/// </remarks>
internal sealed class PropScatter
{
    readonly int[] _head;
    readonly List<int> _next = [];
    readonly int _columns;
    readonly int _rows;
    readonly float _squareM;
    readonly float _apartM;

    PropScatter(int columns, int rows, float squareM, float apartM)
    {
        _columns = columns;
        _rows = rows;
        _squareM = squareM;
        _apartM = apartM;
        _head = new int[columns * rows];
        Array.Fill(_head, -1);
    }

    public List<Vector2> CentreM { get; } = [];

    public List<float> RadiusM { get; } = [];

    public List<float> BearingRad { get; } = [];

    public List<byte> Kind { get; } = [];

    public static PropScatter Over(Vector2 acrossM, float widestM, float apartM)
    {
        var squareM = widestM + apartM;
        return new(
            (int)MathF.Ceiling(acrossM.X / squareM) + 1, (int)MathF.Ceiling(acrossM.Y / squareM) + 1,
            squareM, apartM);
    }

    /// <summary>Whether a candidate stands nearer a prop already laid than the clearance between them allows (GEN-6c).</summary>
    public bool Reaches(Vector2 atM, float reachM)
    {
        var (column, row) = SquareOf(atM);
        for (var down = Math.Max(0, row - 1); down <= Math.Min(_rows - 1, row + 1); down++)
        {
            for (var over = Math.Max(0, column - 1); over <= Math.Min(_columns - 1, column + 1); over++)
            {
                for (var prop = _head[(down * _columns) + over]; prop >= 0; prop = _next[prop])
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
        var (column, row) = SquareOf(atM);
        var square = (row * _columns) + column;

        _next.Add(_head[square]);
        _head[square] = CentreM.Count;

        CentreM.Add(atM);
        RadiusM.Add(reachM);
        BearingRad.Add(bearingRad);
        Kind.Add((byte)kind);
    }

    (int Column, int Row) SquareOf(Vector2 atM) => (
        Math.Clamp((int)MathF.Floor(atM.X / _squareM), 0, _columns - 1),
        Math.Clamp((int)MathF.Floor(atM.Y / _squareM), 0, _rows - 1));
}
