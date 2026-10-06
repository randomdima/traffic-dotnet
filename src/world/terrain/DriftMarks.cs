using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Terrain;

/// <summary>
/// One stretch of mark: a quad spanning the travel of one wheel since the last one, the width of the
/// tyre that made it, and how hard that tyre was working the ground over it.
/// </summary>
/// <remarks>
/// <b>Ploughed is what it is and not how it looks</b> — displaced ground rather than rubber laid on a
/// surface that is still there. What each kind is drawn as belongs to whatever is drawing it.
/// </remarks>
internal readonly record struct DriftMark(Vector2 CentreM, float LengthM, float WidthM, float HeadingRad, float Intensity, bool Ploughed);

/// <summary>
/// The record the traffic leaves on the ground: wherever a tyre works the surface harder than it can
/// shrug off — a slide on tarmac, simply rolling over grass — that stretch of ground is marked, and
/// stays marked.
/// </summary>
/// <remarks>
/// <para>
/// <b>Marks are pure scenery.</b> Nothing samples them, no agent sees one, and no rule is written
/// against one. They are the only thing in the town that is written by the physics and read by
/// nothing but the renderer.
/// </para>
/// <para>
/// The buffer is a ring holding its whole capacity: the oldest mark is overwritten once it is full,
/// which is the only sense in which a permanent mark has a limit. It is laid once and never grows, so
/// a town driven for an hour costs exactly what one driven for a minute costs.
/// </para>
/// <para>
/// <b>Every mark is also filed by the row of the grid its centre is in</b> (SIM-8), oldest first, so a
/// picture of a street reads the street's rows and not the ring. Oldest first is what keeps the filing
/// free: the place the ring overwrites next holds the oldest mark of all, which is the first of its row.
/// A row and not a cell, because a row costs two words however wide the town is.
/// </para>
/// </remarks>
internal sealed class DriftMarks
{
    /// <summary>Shorter than this and there is no mark to speak of.</summary>
    const float MinLengthM = 1e-3f;

    const int None = -1;

    readonly DriftMark[] _marks;
    readonly GridLevel _level;
    readonly int _fromRow;
    readonly int[] _rowOf;
    readonly int[] _youngerInRow;
    readonly int[] _oldestInRow;
    readonly int[] _youngestInRow;
    int _next;

    /// <param name="leastYM">The town's own extent across its rows; a mark past it is filed in the nearest.</param>
    public DriftMarks(int capacity, GridLevel level, float leastYM, float mostYM)
    {
        _marks = new DriftMark[Math.Max(1, capacity)];
        _rowOf = new int[_marks.Length];
        _youngerInRow = new int[_marks.Length];
        _level = level;
        _fromRow = level.CellOf(leastYM);
        _oldestInRow = new int[Math.Max(1, level.CellOf(mostYM) - _fromRow + 1)];
        _youngestInRow = new int[_oldestInRow.Length];
        Clear();
    }

    /// <summary>How many of the ring's places have been written — the whole of it once it has wrapped.</summary>
    public int Count { get; private set; }

    public ReadOnlySpan<DriftMark> Laid => _marks.AsSpan(0, Count);

    public int RowCount => _oldestInRow.Length;

    /// <summary>How tall a row is, which is longer than any mark: a stretch is laid every <c>Marks.SpacingM</c> of a wheel's travel.</summary>
    public float RowM => _level.CellM;

    /// <summary>The rows a band of the map from <paramref name="leastYM"/> to <paramref name="mostYM"/> reaches, the town's edge rows standing for what is past them.</summary>
    public (int From, int To) Rows(float leastYM, float mostYM) => (RowOf(leastYM), RowOf(mostYM));

    /// <summary>The oldest mark's place in a row, or −1 where it holds none.</summary>
    public int OldestIn(int row) => _oldestInRow[row];

    /// <summary>The next younger mark's place in the same row, or −1 past the youngest.</summary>
    public int YoungerThan(int slot) => _youngerInRow[slot];

    public ref readonly DriftMark At(int slot) => ref _marks[slot];

    /// <summary>Lay one stretch, from where the wheel was when the stretch began to where it is now.</summary>
    public void Mark(Vector2 fromM, Vector2 toM, float widthM, float intensity, bool ploughed)
    {
        var spanM = toM - fromM;
        var lengthM = spanM.Length();
        if (lengthM < MinLengthM || intensity <= 0f || widthM <= 0f) return;

        if (Count == _marks.Length) Unfile(_next);

        var centreM = Vector2.Lerp(fromM, toM, 0.5f);
        _marks[_next] = new DriftMark(
            centreM, lengthM, widthM, MathF.Atan2(spanM.Y, spanM.X), Math.Clamp(intensity, 0f, 1f), ploughed);
        File(_next, RowOf(centreM.Y));

        _next = (_next + 1) % _marks.Length;
        Count = Math.Min(Count + 1, _marks.Length);
    }

    /// <summary>Forget the lot, which is what opening another town does.</summary>
    public void Clear()
    {
        _next = 0;
        Count = 0;
        Array.Fill(_oldestInRow, None);
        Array.Fill(_youngestInRow, None);
    }

    int RowOf(float yM) => Math.Clamp(_level.CellOf(yM) - _fromRow, 0, _oldestInRow.Length - 1);

    void File(int slot, int row)
    {
        _rowOf[slot] = row;
        _youngerInRow[slot] = None;
        if (_youngestInRow[row] == None) _oldestInRow[row] = slot;
        else _youngerInRow[_youngestInRow[row]] = slot;

        _youngestInRow[row] = slot;
    }

    /// <summary>The ring's oldest mark taken off its row, of which it is the oldest too.</summary>
    void Unfile(int slot)
    {
        var row = _rowOf[slot];
        _oldestInRow[row] = _youngerInRow[slot];
        if (_youngestInRow[row] == slot) _youngestInRow[row] = None;
    }
}
