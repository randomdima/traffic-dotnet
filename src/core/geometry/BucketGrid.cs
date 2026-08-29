using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// A uniform bucket grid over a town's circles — props, buildings, and the moving bodies of the
/// proximity index. It answers <em>what is near here</em> with a <b>superset</b>: everything that
/// could overlap the query is returned, and the caller does the fine test it was going to do anyway.
/// </summary>
/// <remarks>
/// <para>
/// Every item is indexed once, in the bucket its centre falls in, and a query is widened by the
/// largest radius in the set instead. Writing an item into every bucket its circle touches would make
/// one large item cost a hundred entries and return it a hundred times, and the deduplication that
/// then becomes necessary is a per-query allocation.
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
    readonly float _bucketSizeM;
    readonly float _inverseBucketM;
    readonly int _width;
    readonly int _height;

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

    public BucketGrid(Vector2 worldSizeM, float bucketSizeM)
    {
        if (bucketSizeM <= 0f) throw new ArgumentOutOfRangeException(nameof(bucketSizeM), bucketSizeM, "A bucket has a size.");

        _bucketSizeM = bucketSizeM;
        _inverseBucketM = 1f / bucketSizeM;
        _width = Math.Max(1, (int)MathF.Ceiling(worldSizeM.X / bucketSizeM));
        _height = Math.Max(1, (int)MathF.Ceiling(worldSizeM.Y / bucketSizeM));
        _bucketStart = new int[_width * _height];
        _bucketCount = new int[_width * _height];
        _fillCursor = new int[_width * _height];
        _stamp = new int[_width * _height];
    }

    public static BucketGrid Build(Vector2 worldSizeM, float bucketSizeM, Vector2[] centresM, float[] radiiM)
    {
        var grid = new BucketGrid(worldSizeM, bucketSizeM);
        grid.Rebuild(centresM, radiiM, centresM.Length);
        return grid;
    }

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
        var reachM = radiusM + _maxRadiusM;
        var minX = Math.Max(0, (int)MathF.Floor((centreM.X - reachM) * _inverseBucketM));
        var maxX = Math.Min(_width - 1, (int)MathF.Floor((centreM.X + reachM) * _inverseBucketM));
        var minY = Math.Max(0, (int)MathF.Floor((centreM.Y - reachM) * _inverseBucketM));
        var maxY = Math.Min(_height - 1, (int)MathF.Floor((centreM.Y + reachM) * _inverseBucketM));

        var written = 0;
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var bucket = y * _width + x;
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
        var originX = Math.Clamp((int)MathF.Floor(pointM.X * _inverseBucketM), 0, _width - 1);
        var originY = Math.Clamp((int)MathF.Floor(pointM.Y * _inverseBucketM), 0, _height - 1);
        var lastRing = Math.Max(_width, _height);

        var best = -1;
        var bestDistanceSquared = float.PositiveInfinity;
        for (var ring = 0; ring <= lastRing; ring++)
        {
            for (var y = Math.Max(0, originY - ring); y <= Math.Min(_height - 1, originY + ring); y++)
            {
                for (var x = Math.Max(0, originX - ring); x <= Math.Min(_width - 1, originX + ring); x++)
                {
                    // Only the ring itself: everything inside it was searched on an earlier round.
                    if (ring > 0 && Math.Abs(x - originX) != ring && Math.Abs(y - originY) != ring) continue;

                    var bucket = y * _width + x;
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
            var searchedM = ring * _bucketSizeM;
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

    int BucketOf(Vector2 pointM)
    {
        var x = Math.Clamp((int)MathF.Floor(pointM.X * _inverseBucketM), 0, _width - 1);
        var y = Math.Clamp((int)MathF.Floor(pointM.Y * _inverseBucketM), 0, _height - 1);
        return y * _width + x;
    }
}
