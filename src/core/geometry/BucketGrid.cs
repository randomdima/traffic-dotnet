using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// A town's circles — the walkers of the proximity index, the bays of a car park — filed by centre on a
/// level of the grid (SIM-8). It answers <em>what is near here</em> with a <b>superset</b>: everything that
/// could overlap the query is returned, and the caller does the fine test it was going to do anyway.
/// </summary>
/// <remarks>
/// <para>
/// Every item is indexed once, in the cell its centre falls in, and a query is widened by the
/// largest radius in the set instead. Writing an item into every cell its circle touches would make
/// one large item cost a hundred entries and return it a hundred times, and the deduplication that
/// then becomes necessary is a per-query allocation.
/// </para>
/// <para>
/// <b>Which is the trade the solver's own index takes the other way round</b>, and it is right there for
/// the same reason it is wrong here: a set of one size widens by nothing, and a set holding a tree beside
/// a building would make every query pay for the building. Two stores, one grid.
/// </para>
/// <para>
/// <b>The window is the whole town</b>, and a centre off its edge is filed in the rim — so a query reaching
/// the edge finds it, and a query off the edge is answered, not refused: with whatever of the town it
/// reaches, which is nothing where it reaches none.
/// </para>
/// <para>
/// A rebuild is linear in the items and never in the buckets, which is what makes an index over a
/// whole town affordable sixty times a second: a bucket is live only while its stamp matches the
/// current generation, so nothing is cleared between rebuilds and the counting sort's prefix runs over
/// the buckets this set actually reached. Every array is reused, so a rebuild allocates nothing.
/// </para>
/// </remarks>
internal sealed class BucketGrid
{
    readonly GridWindow _window;

    /// <summary>Where a live bucket's items begin in <see cref="_items"/>, and how many it has.</summary>
    /// <remarks>Both are meaningless where <see cref="_stamp"/> does not match <see cref="_generation"/>.</remarks>
    readonly int[] _bucketStart;

    readonly int[] _bucketCount;

    /// <summary>The fill cursor of a live bucket, which is its start again by the time the scatter ends.</summary>
    readonly int[] _fillCursor;

    /// <summary>Which rebuild last wrote each bucket. What makes clearing the whole grid unnecessary.</summary>
    readonly int[] _stamp;

    Vector2[] _centresM = [];
    int[] _items = [];

    /// <summary>The distinct buckets this set reached, so the prefix sum walks those and nothing else.</summary>
    int[] _touched = [];

    int _touchedCount;
    int _generation;
    int _count;
    float _maxRadiusM;

    /// <param name="level">The level of the grid the centres are filed at.</param>
    /// <param name="worldSizeM">The town's extent from the world's origin, which the window covers.</param>
    public BucketGrid(GridLevel level, Vector2 worldSizeM)
    {
        _window = GridWindow.Over(level, Vector2.Zero, Vector2.Max(worldSizeM, Vector2.Zero));
        _bucketStart = new int[_window.Count];
        _bucketCount = new int[_window.Count];
        _fillCursor = new int[_window.Count];
        _stamp = new int[_window.Count];
    }

    public static BucketGrid Build(GridLevel level, Vector2 worldSizeM, Vector2[] centresM, float[] radiiM)
    {
        var grid = new BucketGrid(level, worldSizeM);
        grid.Rebuild(centresM, radiiM, centresM.Length);
        return grid;
    }

    /// <summary>The cells the centres are filed over — the town's, at the level given.</summary>
    public GridWindow Window => _window;

    public int Count => _count;

    /// <summary>
    /// Lay the index over the arrays as they stand. The arrays are kept by reference rather than
    /// copied — they are the roster's own, and an index that copied them would be a second truth.
    /// </summary>
    public void Rebuild(Vector2[] centresM, float[] radiiM, int count)
    {
        if (count > centresM.Length || count > radiiM.Length) throw new ArgumentOutOfRangeException(nameof(count));

        _centresM = centresM;
        _count = count;
        _maxRadiusM = 0f;
        if (_items.Length < count)
        {
            _items = new int[count];
            _touched = new int[count];
        }

        NextGeneration();

        _touchedCount = 0;
        for (var item = 0; item < count; item++)
        {
            var bucket = BucketOf(centresM[item]);
            if (_stamp[bucket] != _generation)
            {
                _stamp[bucket] = _generation;
                _bucketCount[bucket] = 0;
                _touched[_touchedCount++] = bucket;
            }

            _bucketCount[bucket]++;
            if (radiiM[item] > _maxRadiusM) _maxRadiusM = radiiM[item];
        }

        var at = 0;
        for (var slot = 0; slot < _touchedCount; slot++)
        {
            var bucket = _touched[slot];
            _bucketStart[bucket] = at;
            _fillCursor[bucket] = at;
            at += _bucketCount[bucket];
        }

        for (var item = 0; item < count; item++) _items[_fillCursor[BucketOf(centresM[item])]++] = item;
    }

    /// <summary>
    /// Every item that could reach within <paramref name="radiusM"/> of the point, and possibly some
    /// that cannot. Returns how many there are, having written as many as fit: a result larger than
    /// <paramref name="found"/> is truncated, and a caller that does not check has silently turned the
    /// superset into a subset. A query off the edge of the town is answered, not refused.
    /// </summary>
    public int Query(Vector2 centreM, float radiusM, Span<int> found)
    {
        var reach = new Vector2(radiusM + _maxRadiusM);
        if (!_window.TryOverlap(centreM - reach, centreM + reach, out var range)) return 0;

        var written = 0;
        for (var y = range.FromY; y <= range.ToY; y++)
        {
            for (var x = range.FromX; x <= range.ToX; x++)
            {
                var bucket = _window.IndexOf(x, y);
                if (_stamp[bucket] != _generation) continue;

                var start = _bucketStart[bucket];
                for (var slot = start; slot < start + _bucketCount[bucket]; slot++)
                {
                    if (written < found.Length) found[written] = _items[slot];
                    written++;
                }
            }
        }

        return written;
    }

    /// <summary>
    /// The nearest item to a point by centre distance, or -1 when the index is empty. The search
    /// widens a ring of buckets at a time and <b>terminates</b>: it stops as soon as the best it has
    /// is closer than the ring it would search next, and in any case once the rings have covered the
    /// whole grid.
    /// </summary>
    public int Nearest(Vector2 pointM, out float distanceM)
    {
        var originX = _window.ClampX(_window.Level.CellOf(pointM.X));
        var originY = _window.ClampY(_window.Level.CellOf(pointM.Y));
        var lastRing = Math.Max(_window.Width, _window.Height);

        var best = -1;
        var bestDistanceSquared = float.PositiveInfinity;
        for (var ring = 0; ring <= lastRing; ring++)
        {
            for (var y = _window.ClampY(originY - ring); y <= _window.ClampY(originY + ring); y++)
            {
                for (var x = _window.ClampX(originX - ring); x <= _window.ClampX(originX + ring); x++)
                {
                    // Only the ring itself: everything inside it was searched on an earlier round.
                    if (ring > 0 && Math.Abs(x - originX) != ring && Math.Abs(y - originY) != ring) continue;

                    var bucket = _window.IndexOf(x, y);
                    if (_stamp[bucket] != _generation) continue;

                    var start = _bucketStart[bucket];
                    for (var slot = start; slot < start + _bucketCount[bucket]; slot++)
                    {
                        var item = _items[slot];
                        var distanceSquared = Vector2.DistanceSquared(_centresM[item], pointM);
                        if (distanceSquared >= bestDistanceSquared) continue;

                        bestDistanceSquared = distanceSquared;
                        best = item;
                    }
                }
            }

            // A closer item can only be in a ring nearer than the one already searched, plus the
            // largest radius by which a centre-indexed item can stick out of its own bucket.
            var searchedM = ring * _window.Level.CellM;
            if (best >= 0 && bestDistanceSquared <= (searchedM + _maxRadiusM) * (searchedM + _maxRadiusM)) break;
        }

        distanceM = best >= 0 ? MathF.Sqrt(bestDistanceSquared) : float.PositiveInfinity;
        return best;
    }

    /// <summary>
    /// The stamp a live bucket carries from here on. The wrap is the whole reason this is a method: a
    /// stamp coming round to a value still standing in the array would make a stale bucket read live.
    /// </summary>
    void NextGeneration()
    {
        if (_generation == int.MaxValue)
        {
            Array.Clear(_stamp);
            _generation = 0;
        }

        _generation++;
    }

    int BucketOf(Vector2 pointM) => _window.IndexAt(pointM);
}
