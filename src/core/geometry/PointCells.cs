using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>Points filed by the cell of one grid level they stand in, added one at a time</b> (SIM-8) — for a
/// builder that keeps asking what is near a place while it keeps adding places, where an index laid again
/// for every question would be the square of what it holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sparse, and linked rather than listed</b>: a cell is one head in a dictionary and a point is a link to
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
    readonly Dictionary<(int X, int Y), int> _lastIn = [];
    readonly List<int> _filedBefore = [];
    readonly List<Vector2> _pointM = [];

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

        var cell = level.CellOf(pointM);
        _filedBefore.Add(_lastIn.TryGetValue(cell, out var last) ? last : -1);
        _lastIn[cell] = point;
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
                if (!_lastIn.TryGetValue((atX, atY), out var point)) continue;

                for (; point >= 0; point = _filedBefore[point])
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
                if (!_lastIn.TryGetValue((atX, atY), out var point)) continue;

                for (; point >= 0; point = _filedBefore[point])
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
                    if (!_lastIn.TryGetValue((atX, atY), out var point)) continue;

                    for (; point >= 0; point = _filedBefore[point])
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

        foreach (var pointM in _pointM)
        {
            if (IsFinite(pointM)) best = MathF.Min(best, Vector2.DistanceSquared(pointM, atM));
        }

        return best;
    }

    static bool IsFinite(Vector2 pointM) => float.IsFinite(pointM.X) && float.IsFinite(pointM.Y);
}
