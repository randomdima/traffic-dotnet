
using System.Numerics;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// A uniform grid of cells over a set of bodies' bounding boxes, which is the whole of this solver's
/// broad phase. It answers two questions and no others: <em>what could be in this box</em>, and
/// <em>what could this segment cross, in the order it would cross it</em>.
/// </summary>
/// <remarks>
/// <para>
/// A body is written into every cell its box touches, unlike <see cref="Shared.Spatial.BucketGrid"/>,
/// which indexes a centre and widens the query by the largest radius in the set. A grid a ray walks
/// cannot widen a query — a segment crosses the cells it crosses — and the static set holds a tree
/// beside a building, so one radius for all would make every query pay for the largest thing in town.
/// </para>
/// <para>
/// Nothing is cleared between rebuilds: a cell is live only while its stamp matches the current
/// generation, and the counting sort's prefix runs over the cells this set actually reached rather than
/// over the grid, so a rebuild is linear in the bodies. Every array is reused, so a steady state
/// allocates nothing. The bounds are the set's own and are retaken at each rebuild, so a rig with four
/// bodies in it is not priced at a town's cell count.
/// </para>
/// </remarks>
internal sealed class CellGrid
{
    /// <summary>What no grid may exceed however far its bodies are spread: the cell size grows instead.</summary>
    const int MostCells = 1 << 22;

    float _cellM = 1f;
    float _inverseCellM = 1f;
    Vector2 _originM;
    int _width;
    int _height;

    int[] _cellStart = [];
    int[] _cellCount = [];
    int[] _cellCursor = [];
    int[] _cellStamp = [];
    int[] _touched = [];
    int[] _items = [];

    Vector2[] _leastM = [];
    Vector2[] _mostM = [];

    int _touchedCount;
    int _generation;
    int _entryCount;

    public int BodyCount { get; private set; }

    /// <summary>How many cells the bodies actually reached, which is the figure a census quotes rather than the grid's size.</summary>
    public int LiveCellCount => _touchedCount;

    public float CellSizeM => _cellM;

    /// <summary>
    /// Lay the grid over the bodies named, at their bounding boxes as they now stand. The bound arrays
    /// are the world's own and are kept by reference: an index that copied them would be a second truth.
    /// </summary>
    public void Rebuild(ReadOnlySpan<int> bodies, Vector2[] leastM, Vector2[] mostM, float cellSizeM)
    {
        _leastM = leastM;
        _mostM = mostM;
        BodyCount = bodies.Length;
        _entryCount = 0;
        _touchedCount = 0;
        NextGeneration();

        if (bodies.Length == 0)
        {
            _width = 0;
            _height = 0;
            return;
        }

        var least = new Vector2(float.MaxValue);
        var most = new Vector2(float.MinValue);
        foreach (var body in bodies)
        {
            least = Vector2.Min(least, leastM[body]);
            most = Vector2.Max(most, mostM[body]);
        }

        Size(least, most, cellSizeM);

        for (var slot = 0; slot < bodies.Length; slot++)
        {
            var body = bodies[slot];
            Span(body, out var fromX, out var fromY, out var toX, out var toY);
            for (var y = fromY; y <= toY; y++)
            {
                for (var x = fromX; x <= toX; x++)
                {
                    var cell = y * _width + x;
                    if (_cellStamp[cell] != _generation)
                    {
                        _cellStamp[cell] = _generation;
                        _cellCount[cell] = 0;
                        if (_touchedCount == _touched.Length) Array.Resize(ref _touched, Math.Max(64, _touched.Length * 2));

                        _touched[_touchedCount++] = cell;
                    }

                    _cellCount[cell]++;
                    _entryCount++;
                }
            }
        }

        if (_items.Length < _entryCount) _items = new int[Math.Max(_entryCount, _items.Length * 2)];

        var at = 0;
        for (var slot = 0; slot < _touchedCount; slot++)
        {
            var cell = _touched[slot];
            _cellStart[cell] = at;
            _cellCursor[cell] = at;
            at += _cellCount[cell];
        }

        // In body order, so a cell's items come out ascending and the pairs built from them are ordered
        // by index rather than by discovery — which is what makes a digest reproducible.
        for (var slot = 0; slot < bodies.Length; slot++)
        {
            var body = bodies[slot];
            Span(body, out var fromX, out var fromY, out var toX, out var toY);
            for (var y = fromY; y <= toY; y++)
            {
                for (var x = fromX; x <= toX; x++)
                {
                    _items[_cellCursor[y * _width + x]++] = body;
                }
            }
        }
    }

    /// <summary>What one live cell holds, or nothing where no body reached it.</summary>
    public ReadOnlySpan<int> Items(int x, int y)
    {
        var cell = y * _width + x;
        if (_cellStamp[cell] != _generation) return ReadOnlySpan<int>.Empty;

        return _items.AsSpan(_cellStart[cell], _cellCount[cell]);
    }

    /// <summary>The cells an axis-aligned box could reach, or false where it lies off the grid entirely.</summary>
    public bool TryRange(Vector2 leastM, Vector2 mostM, out int fromX, out int fromY, out int toX, out int toY)
    {
        fromX = (int)MathF.Floor((leastM.X - _originM.X) * _inverseCellM);
        fromY = (int)MathF.Floor((leastM.Y - _originM.Y) * _inverseCellM);
        toX = (int)MathF.Floor((mostM.X - _originM.X) * _inverseCellM);
        toY = (int)MathF.Floor((mostM.Y - _originM.Y) * _inverseCellM);

        if (toX < 0 || toY < 0 || fromX >= _width || fromY >= _height) return false;

        fromX = Math.Max(fromX, 0);
        fromY = Math.Max(fromY, 0);
        toX = Math.Min(toX, _width - 1);
        toY = Math.Min(toY, _height - 1);
        return true;
    }

    /// <summary>The cells a segment crosses, in the order it crosses them. See <see cref="RayWalk"/>.</summary>
    public RayWalk Walk(Vector2 fromM, Vector2 travelM) => new(this, fromM, travelM);

    /// <summary>
    /// A segment's cells, one at a time, each with the fraction of the segment at which it is left. A
    /// caster that has already found something nearer than that fraction is finished, which is what
    /// makes a cast cost the ground it covers rather than the grid it covers it in.
    /// </summary>
    /// <remarks>
    /// Amanatides and Woo's traversal, and a <c>ref struct</c> so the items it hands back are a span of
    /// the grid's own array and nothing is copied or allocated to walk it.
    /// </remarks>
    public ref struct RayWalk
    {
        readonly CellGrid _grid;
        readonly int _stepX;
        readonly int _stepY;
        readonly float _deltaX;
        readonly float _deltaY;
        readonly float _to;

        int _x;
        int _y;
        float _crossX;
        float _crossY;
        bool _started;
        bool _done;

        public RayWalk(CellGrid grid, Vector2 fromM, Vector2 travelM)
        {
            _grid = grid;
            _done = true;

            if (grid._width == 0 || grid._height == 0) return;

            var least = grid._originM;
            var most = grid._originM + new Vector2(grid._width, grid._height) * grid._cellM;
            if (!Clip(fromM, travelM, least, most, out var from, out var to)) return;

            _to = to;
            var enteredM = fromM + travelM * from;
            _x = Math.Clamp((int)MathF.Floor((enteredM.X - least.X) * grid._inverseCellM), 0, grid._width - 1);
            _y = Math.Clamp((int)MathF.Floor((enteredM.Y - least.Y) * grid._inverseCellM), 0, grid._height - 1);

            Axis(fromM.X, travelM.X, least.X, grid._cellM, _x, out _stepX, out _crossX, out _deltaX);
            Axis(fromM.Y, travelM.Y, least.Y, grid._cellM, _y, out _stepY, out _crossY, out _deltaY);
            _done = false;
        }

        /// <summary>The bodies in the cell the walk currently stands in.</summary>
        public ReadOnlySpan<int> Items { get; private set; }

        /// <summary>The fraction of the whole segment at which this cell is left behind.</summary>
        public float ExitFraction { get; private set; }

        public bool MoveNext()
        {
            if (_done) return false;

            if (_started)
            {
                float entered;
                if (_crossX < _crossY)
                {
                    _x += _stepX;
                    entered = _crossX;
                    _crossX += _deltaX;
                }
                else
                {
                    _y += _stepY;
                    entered = _crossY;
                    _crossY += _deltaY;
                }

                if (entered >= _to || _x < 0 || _x >= _grid._width || _y < 0 || _y >= _grid._height)
                {
                    _done = true;
                    return false;
                }
            }

            _started = true;
            Items = _grid.Items(_x, _y);
            ExitFraction = MathF.Min(MathF.Min(_crossX, _crossY), _to);
            return true;
        }

        /// <summary>Which way this axis is walked, where its first cell boundary falls, and how far apart the rest are.</summary>
        static void Axis(float fromM, float travelM, float leastM, float cellM, int cell, out int step, out float cross, out float delta)
        {
            if (MathF.Abs(travelM) < 1e-9f)
            {
                step = 0;
                cross = float.PositiveInfinity;
                delta = float.PositiveInfinity;
                return;
            }

            step = travelM > 0f ? 1 : -1;
            delta = MathF.Abs(cellM / travelM);
            var boundaryM = leastM + (travelM > 0f ? cell + 1 : cell) * cellM;
            cross = (boundaryM - fromM) / travelM;
        }

        /// <summary>The stretch of the segment that lies inside the grid's own rectangle, as two fractions of it.</summary>
        static bool Clip(Vector2 fromM, Vector2 travelM, Vector2 leastM, Vector2 mostM, out float from, out float to)
        {
            from = 0f;
            to = 1f;
            for (var axis = 0; axis < 2; axis++)
            {
                var at = axis == 0 ? fromM.X : fromM.Y;
                var along = axis == 0 ? travelM.X : travelM.Y;
                var least = axis == 0 ? leastM.X : leastM.Y;
                var most = axis == 0 ? mostM.X : mostM.Y;

                if (MathF.Abs(along) < 1e-9f)
                {
                    if (at < least || at > most) return false;

                    continue;
                }

                var enters = (least - at) / along;
                var leaves = (most - at) / along;
                if (enters > leaves) (enters, leaves) = (leaves, enters);

                from = MathF.Max(from, enters);
                to = MathF.Min(to, leaves);
                if (to < from) return false;
            }

            return true;
        }
    }

    /// <summary>
    /// The grid's own rectangle and cell size, one cell of margin all round so a body sitting exactly on
    /// the far edge still has a cell of its own. Where a set is spread over a very large world the cell
    /// size grows rather than the cell count.
    /// </summary>
    void Size(Vector2 leastM, Vector2 mostM, float cellSizeM)
    {
        var spanM = Vector2.Max(mostM - leastM, Vector2.Zero);
        _cellM = MathF.Max(cellSizeM, 1e-3f);
        while (Cells(spanM, _cellM) > MostCells) _cellM *= 2f;

        _inverseCellM = 1f / _cellM;
        _originM = leastM;
        _width = (int)MathF.Floor(spanM.X * _inverseCellM) + 1;
        _height = (int)MathF.Floor(spanM.Y * _inverseCellM) + 1;

        var cells = _width * _height;
        if (_cellStamp.Length >= cells) return;

        // Half again, because the bounds are the set's own and a roster spreading by a metre would
        // otherwise lay four new arrays for one more column of cells — a steady state that allocates.
        var room = cells + cells / 2;
        _cellStart = new int[room];
        _cellCount = new int[room];
        _cellCursor = new int[room];
        _cellStamp = new int[room];
        _generation = 1;
    }

    static long Cells(Vector2 spanM, float cellM) =>
        ((long)MathF.Floor(spanM.X / cellM) + 1) * ((long)MathF.Floor(spanM.Y / cellM) + 1);

    void Span(int body, out int fromX, out int fromY, out int toX, out int toY)
    {
        fromX = Math.Clamp((int)MathF.Floor((_leastM[body].X - _originM.X) * _inverseCellM), 0, _width - 1);
        fromY = Math.Clamp((int)MathF.Floor((_leastM[body].Y - _originM.Y) * _inverseCellM), 0, _height - 1);
        toX = Math.Clamp((int)MathF.Floor((_mostM[body].X - _originM.X) * _inverseCellM), 0, _width - 1);
        toY = Math.Clamp((int)MathF.Floor((_mostM[body].Y - _originM.Y) * _inverseCellM), 0, _height - 1);
    }

    /// <summary>
    /// The stamp a live cell carries from here on. The wrap is the whole reason this is a method: a
    /// stamp coming round to a value still standing in the array would make a stale cell read live.
    /// </summary>
    void NextGeneration()
    {
        if (_generation == int.MaxValue)
        {
            Array.Clear(_cellStamp);
            _generation = 0;
        }

        _generation++;
    }
}
