using System.Numerics;
using System.Runtime.InteropServices;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>Points filed by the cell of one grid level they stand in, added one at a time</b> (SIM-8) — for a
/// builder that keeps asking what is near a place while it keeps adding places, where an index laid again
/// for every question would be the square of what it holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sparse, and linked rather than listed</b>: a cell is one head in a <see cref="CellTable"/> and a point is a link to
/// the point filed before it in the same cell, so the memory is the points' whatever extent they are spread
/// over, and adding one allocates nothing past the growth of two lists.
/// </para>
/// <para>
/// <b>Every answer is the one asking each point in turn would give</b> — the same test, and a minimum, which
/// no order of asking can change. A point that is not finite is held and never filed: no distance test
/// against it passes either.
/// </para>
/// </remarks>
internal sealed class PointCells(GridLevel level)
{
    readonly CellTable _lastIn = new();
    readonly List<int> _filedBefore = [];
    readonly List<Vector2> _pointM = [];

    /// <summary>The cells the points are known to fall in, held as an array rather than in the table; none where the caller named no extent.</summary>
    readonly GridWindow _window;

    /// <summary>Each cell of the window's last point one up, so a fresh array is every cell empty.</summary>
    readonly int[] _lastInWindowUp = [];

    /// <summary>
    /// <b>For points known to stand inside <paramref name="leastM"/>–<paramref name="mostM"/></b>, whose cells are an array
    /// read along a row rather than a table read anywhere: a nearest found rings of cells out reads hundreds of them. A
    /// point outside is filed in the table as before, so the answers are the same whatever the extent.
    /// </summary>
    public PointCells(GridLevel level, Vector2 leastM, Vector2 mostM)
        : this(level)
    {
        _window = GridWindow.Over(level, leastM, mostM);
        _lastInWindowUp = new int[_window.Count];
    }

    int LastIn(int x, int y) =>
        _window.Holds(x, y) ? _lastInWindowUp[_window.IndexOf(x, y)] - 1 : _lastIn[GridLevel.KeyOf(x, y)];

    public int Count => _pointM.Count;

    public Vector2 this[int point] => _pointM[point];

    /// <returns>The point's number, which is how many were added before it.</returns>
    public int Add(Vector2 pointM)
    {
        var point = _pointM.Count;
        _pointM.Add(pointM);
        if (!IsFinite(pointM))
        {
            _filedBefore.Add(-1);
            return point;
        }

        var (x, y) = level.CellOf(pointM);
        _filedBefore.Add(LastIn(x, y));
        if (_window.Holds(x, y)) _lastInWindowUp[_window.IndexOf(x, y)] = point + 1;
        else _lastIn.Set(GridLevel.KeyOf(x, y), point);

        return point;
    }

    /// <summary>Whether any point stands strictly nearer than <paramref name="reachM"/>.</summary>
    public bool AnyWithin(Vector2 atM, float reachM)
    {
        if (!IsFinite(atM)) return false;

        // One ring past the reach, so a point the float arithmetic of a cell edge put a cell further out is
        // still read.
        var rings = level.CellsWithin(reachM) + 1;
        var (x, y) = level.CellOf(atM);
        for (var atY = y - rings; atY <= y + rings; atY++)
        {
            for (var atX = x - rings; atX <= x + rings; atX++)
            {
                for (var point = LastIn(atX, atY); point >= 0; point = _filedBefore[point])
                {
                    if (Vector2.DistanceSquared(_pointM[point], atM) < reachM * reachM) return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// <b>Every point no further than <paramref name="reachM"/></b>, in the order they were added — which is
    /// the order a scan of every point would have met them in.
    /// </summary>
    public void Within(Vector2 atM, float reachM, List<int> into)
    {
        into.Clear();
        if (!IsFinite(atM)) return;

        var rings = level.CellsWithin(reachM) + 1;
        var (x, y) = level.CellOf(atM);
        for (var atY = y - rings; atY <= y + rings; atY++)
        {
            for (var atX = x - rings; atX <= x + rings; atX++)
            {
                for (var point = LastIn(atX, atY); point >= 0; point = _filedBefore[point])
                {
                    if (Vector2.DistanceSquared(_pointM[point], atM) <= reachM * reachM) into.Add(point);
                }
            }
        }

        into.Sort();
    }

    /// <summary>
    /// <b>The squared distance to the nearest point, or <paramref name="boundSq"/> where none is nearer</b> —
    /// so a caller that already knows the nearest of some of the points passes that and is told the nearest
    /// of all of them.
    /// </summary>
    /// <remarks>
    /// <b>The rings stop at whichever is cheaper</b>: once the best found is nearer than the next ring could
    /// hold, or once they have read as many cells as there are points, when every point is asked instead. A
    /// sparse set is then a scan of the set and a dense one a few rings, and no cell size has to be chosen for
    /// either.
    /// </remarks>
    public float NearestSq(Vector2 atM, float boundSq = float.PositiveInfinity)
    {
        if (!IsFinite(atM)) return boundSq;

        var (x, y) = level.CellOf(atM);
        var best = boundSq;
        var cellsRead = 0;
        for (var ring = 0; cellsRead < _pointM.Count; ring++)
        {
            for (var atY = y - ring; atY <= y + ring; atY++)
            {
                // The ring's own cells: its two rows whole, and two cells of every row between them.
                var step = atY == y - ring || atY == y + ring ? 1 : Math.Max(1, 2 * ring);
                for (var atX = x - ring; atX <= x + ring; atX += step)
                {
                    cellsRead++;
                    for (var point = LastIn(atX, atY); point >= 0; point = _filedBefore[point])
                    {
                        best = MathF.Min(best, Vector2.DistanceSquared(_pointM[point], atM));
                    }
                }
            }

            // A point in a further ring stands at least this many whole cells off — one fewer than the rings
            // read, so a point a cell edge's rounding moved is still counted.
            var clearM = (ring - 1) * level.CellM;
            if (ring > 0 && best <= clearM * clearM) return best;
        }

        return NearestSqFrom(0, atM, best);
    }

    /// <summary>
    /// <b>The squared distance to the nearest point, for a caller whose <paramref name="boundSq"/> is already the
    /// nearest of the points added before <paramref name="since"/></b> — only the ones added since can be nearer.
    /// </summary>
    /// <remarks>
    /// <b>Those are asked in turn where there are no more of them than cells the rings would read before they could
    /// stop at the bound</b>, and the rings run otherwise. The answer is a minimum either way, so it is
    /// <see cref="NearestSq(Vector2, float)"/>'s to the bit.
    /// </remarks>
    public float NearestSq(Vector2 atM, float boundSq, int since)
    {
        if (!IsFinite(atM)) return boundSq;

        return _pointM.Count - since <= CellsReadToClear(boundSq)
            ? NearestSqFrom(since, atM, boundSq)
            : NearestSq(atM, boundSq);
    }

    /// <summary>The cells the rings read through the first one that clears a nearest at <paramref name="boundSq"/>.</summary>
    double CellsReadToClear(float boundSq)
    {
        var rings = Math.Ceiling(Math.Sqrt(boundSq) / level.CellM) + 1;
        var across = (2 * rings) + 1;
        return across * across;
    }

    float NearestSqFrom(int since, Vector2 atM, float best)
    {
        foreach (var pointM in CollectionsMarshal.AsSpan(_pointM)[since..])
        {
            if (IsFinite(pointM)) best = MathF.Min(best, Vector2.DistanceSquared(pointM, atM));
        }

        return best;
    }

    /// <summary>
    /// <b>The nearest point, and of two as near the one added first</b> — what a scan of every point in turn keeping only a
    /// strictly nearer one answers — or −1 where none is finite.
    /// </summary>
    /// <remarks>
    /// The rings stop once the next could hold nothing as near (<see cref="NearestSq"/>'s bound, strictly), or once they
    /// have read as many cells as there are points, when every point is asked instead.
    /// </remarks>
    public int Nearest(Vector2 atM)
    {
        if (!IsFinite(atM)) return -1;

        var (x, y) = level.CellOf(atM);
        var best = -1;
        var bestSq = float.PositiveInfinity;
        var cellsRead = 0;
        for (var ring = 0; cellsRead < _pointM.Count; ring++)
        {
            for (var atY = y - ring; atY <= y + ring; atY++)
            {
                var step = atY == y - ring || atY == y + ring ? 1 : Math.Max(1, 2 * ring);
                for (var atX = x - ring; atX <= x + ring; atX += step)
                {
                    cellsRead++;
                    for (var point = LastIn(atX, atY); point >= 0; point = _filedBefore[point]) Keep(point);
                }
            }

            var clearM = (ring - 1) * level.CellM;
            if (ring > 0 && bestSq < clearM * clearM) return best;
        }

        for (var point = 0; point < _pointM.Count; point++)
        {
            if (IsFinite(_pointM[point])) Keep(point);
        }

        return best;

        void Keep(int point)
        {
            var distanceSq = Vector2.DistanceSquared(_pointM[point], atM);
            if (distanceSq < bestSq || (distanceSq == bestSq && point < best))
            {
                (best, bestSq) = (point, distanceSq);
            }
        }
    }

    static bool IsFinite(Vector2 pointM) => float.IsFinite(pointM.X) && float.IsFinite(pointM.Y);
}
