
using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Physics;

/// <summary>
/// A set of bodies' bounding boxes filed on a level of the grid (SIM-8), which is the whole of this solver's
/// broad phase. It answers one question and no other: <em>what could be in this box</em>.
/// </summary>
/// <remarks>
/// <para>
/// A body is written into every cell its box touches, unlike <see cref="Core.Geometry.BucketGrid"/>,
/// which indexes a centre and widens the query by the largest radius in the set. The static set holds a
/// tree beside a building, so one radius for all would make every query pay for the largest thing in town.
/// </para>
/// <para>
/// Nothing is cleared between rebuilds: a cell is live only while its stamp matches the current
/// generation, and the counting sort's prefix runs over the cells this set actually reached rather than
/// over the grid, so a rebuild is linear in the bodies. Every array is reused, so a steady state
/// allocates nothing. The window is the set's own and is retaken at each rebuild, so a rig with four
/// bodies in it is not priced at a town's cell count — <b>and its cells are the grid's</b>, so a cell here
/// is the square of ground every other index means by those numbers.
/// </para>
/// <para>
/// <b>A set spread wider than <see cref="MostCells"/> is kept in a window that size about its middle</b>,
/// and whatever stands past it is filed in the rim: every box query still finds it, since a box past the
/// rim reads the rim.
/// </para>
/// <para>
/// <b>The rim is correct and not cheap.</b> A rim cell holds everything in its row or column past the
/// window, so the bodies there are each other's candidates and the broad phase is quadratic in what one row
/// of the outskirts holds. The window is 2 048 cells a side — sixteen kilometres at the main level, whose
/// cell is four car widths — so a town wider than that pays it every tick, in the moving set and the static
/// one alike. No shipped town is that wide.
/// </para>
/// </remarks>
internal sealed class CellGrid
{
    /// <summary>What no window may exceed however far its bodies are spread: the rest are filed in its rim.</summary>
    const int MostCells = 1 << 22;

    GridWindow _window;

    /// <summary>
    /// One cell's three numbers, side by side rather than in three arrays of their own. <b>Every pass over
    /// the grid wants two or three of them about the same cell</b> — the count pass its stamp and its
    /// count, the prefix its count and where it starts, a query its stamp, start and count — and a cell
    /// index is scattered by construction, so three arrays is three lines fetched for what fits in one.
    /// </summary>
    /// <remarks>
    /// The filling pass writes through <see cref="Start"/> and steps it back after, rather than keeping a
    /// cursor of its own: a fourth number a cell is a third more of the town's two largest grids.
    /// </remarks>
    struct Cell
    {
        public int Stamp;
        public int Start;
        public int Count;
    }

    Cell[] _cell = [];
    int[] _touched = [];
    int[] _items = [];

    /// <summary>
    /// Each body's own block of cells, in the order the set was handed over — <b>worked out on the
    /// counting pass and spent on the filling one</b>, because the two passes ask the same question of
    /// the same box and a rebuild is the town's whole moving roster.
    /// </summary>
    CellBlock[] _block = [];

    Vector2[] _leastM = [];
    Vector2[] _mostM = [];

    int _touchedCount;
    int _generation;
    int _entryCount;

    /// <summary>
    /// <b>The cells the set was filed over</b>, numbered on the grid. <b>For whoever draws or points at the
    /// grid</b> (OBS-2x) and for nothing that queries it: a query hands over a box or a segment and is given
    /// the cells.
    /// </summary>
    public GridWindow Window => _window;

    /// <summary>
    /// Lay the grid over the bodies named, at their bounding boxes as they now stand. The bound arrays
    /// are the world's own and are kept by reference: an index that copied them would be a second truth.
    /// </summary>
    public void Rebuild(ReadOnlySpan<int> bodies, Vector2[] leastM, Vector2[] mostM, GridLevel level)
    {
        _leastM = leastM;
        _mostM = mostM;
        _entryCount = 0;
        _touchedCount = 0;
        NextGeneration();

        if (bodies.Length == 0)
        {
            _window = GridWindow.Of(level, 0, 0, 0, 0);
            return;
        }

        var least = new Vector2(float.MaxValue);
        var most = new Vector2(float.MinValue);
        foreach (var body in bodies)
        {
            least = Vector2.Min(least, leastM[body]);
            most = Vector2.Max(most, mostM[body]);
        }

        Size(level, least, most);

        if (_block.Length < bodies.Length) _block = new CellBlock[Math.Max(bodies.Length, _block.Length * 2)];

        for (var slot = 0; slot < bodies.Length; slot++)
        {
            var body = bodies[slot];
            Span(body, out var fromX, out var fromY, out var toX, out var toY);
            _block[slot] = new CellBlock(fromX, fromY, toX, toY);
            for (var y = fromY; y <= toY; y++)
            {
                for (var x = fromX; x <= toX; x++)
                {
                    var stored = _window.IndexOf(x, y);
                    ref var cell = ref _cell[stored];
                    if (cell.Stamp != _generation)
                    {
                        cell.Stamp = _generation;
                        cell.Count = 0;
                        if (_touchedCount == _touched.Length) Array.Resize(ref _touched, Math.Max(64, _touched.Length * 2));

                        _touched[_touchedCount++] = stored;
                    }

                    cell.Count++;
                    _entryCount++;
                }
            }
        }

        if (_items.Length < _entryCount) _items = new int[Math.Max(_entryCount, _items.Length * 2)];

        var at = 0;
        for (var slot = 0; slot < _touchedCount; slot++)
        {
            ref var cell = ref _cell[_touched[slot]];
            cell.Start = at;
            at += cell.Count;
        }

        // In body order, so a cell's items come out ascending and the pairs built from them are ordered
        // by index rather than by discovery — which is what makes a digest reproducible.
        for (var slot = 0; slot < bodies.Length; slot++)
        {
            var body = bodies[slot];
            ref readonly var block = ref _block[slot];
            for (var y = block.FromY; y <= block.ToY; y++)
            {
                for (var x = block.FromX; x <= block.ToX; x++)
                {
                    _items[_cell[_window.IndexOf(x, y)].Start++] = body;
                }
            }
        }

        for (var slot = 0; slot < _touchedCount; slot++)
        {
            ref var cell = ref _cell[_touched[slot]];
            cell.Start -= cell.Count;
        }
    }

    /// <summary>
    /// <b>What only a rebuild works with, let go of</b>: each body's block and the cells it touched, for a grid
    /// that is laid once — the statics — and so would keep a working set the size of the town's every body.
    /// A rebuild after it lays them again.
    /// </summary>
    public void ForgetTheRebuild()
    {
        _block = [];
        _touched = [];
    }

    /// <summary>The block of cells one body's box covers, as the two passes of a rebuild both want it.</summary>
    readonly record struct CellBlock(int FromX, int FromY, int ToX, int ToY);

    /// <summary>What one live cell holds, numbered on the grid, or nothing where no body reached it or the window has no such cell.</summary>
    public ReadOnlySpan<int> Items(int x, int y)
    {
        if (!_window.Holds(x, y)) return ReadOnlySpan<int>.Empty;

        ref readonly var cell = ref _cell[_window.IndexOf(x, y)];
        if (cell.Stamp != _generation) return ReadOnlySpan<int>.Empty;

        return _items.AsSpan(cell.Start, cell.Count);
    }

    /// <summary>
    /// The cells an axis-aligned box could have a body in — the rim's where it stands past it, since that is
    /// where a body past it was filed — or false where the set is empty.
    /// </summary>
    public bool TryRange(Vector2 leastM, Vector2 mostM, out CellRange range) => _window.TryRange(leastM, mostM, out range);

    /// <summary>
    /// <b>Whether cell (<paramref name="x"/>, <paramref name="y"/>) of a box's <paramref name="range"/> is the first
    /// it shares with a body met there</b> — the one cell a pair found in several is kept in, so a query needs no
    /// mark of what it has already met.
    /// </summary>
    /// <remarks>
    /// A cell is a monotone function of a coordinate, so the first cell two boxes share is the cell of the larger of
    /// their least corners, which is the larger of their first cells; and a body met in a cell was filed from its own
    /// first cell or before. <b>Only for a body whose box meets the range's</b> and that was filed at the box it has
    /// now.
    /// </remarks>
    public bool FirstShared(in CellRange range, int x, int y, Vector2 bodyLeastM) =>
        (x == range.FromX || _window.ClampX(_window.Level.CellOf(bodyLeastM.X)) == x)
        && (y == range.FromY || _window.ClampY(_window.Level.CellOf(bodyLeastM.Y)) == y);

    /// <summary>
    /// The window the set is filed in: the level's cells its boxes reach, or at most <see cref="MostCells"/>
    /// of them about the middle of those.
    /// </summary>
    void Size(GridLevel level, Vector2 leastM, Vector2 mostM)
    {
        var window = GridWindow.Over(level, leastM, mostM);
        var side = (int)Math.Sqrt(MostCells);
        if ((long)window.Width * window.Height > MostCells)
        {
            var width = Math.Min(window.Width, side);
            var height = Math.Min(window.Height, side);
            window = GridWindow.Of(
                level, window.FromX + ((window.Width - width) / 2), window.FromY + ((window.Height - height) / 2),
                width, height);
        }

        _window = window;
        var cells = window.Count;
        if (_cell.Length >= cells) return;

        // Half again, because the bounds are the set's own and a roster spreading by a metre would
        // otherwise lay four new arrays for one more column of cells — a steady state that allocates.
        var room = cells + cells / 2;
        _cell = new Cell[room];
        _generation = 1;
    }

    void Span(int body, out int fromX, out int fromY, out int toX, out int toY)
    {
        _window.TryRange(_leastM[body], _mostM[body], out var range);
        (fromX, fromY, toX, toY) = (range.FromX, range.FromY, range.ToX, range.ToY);
    }

    /// <summary>
    /// The stamp a live cell carries from here on. The wrap is the whole reason this is a method: a
    /// stamp coming round to a value still standing in the array would make a stale cell read live.
    /// </summary>
    void NextGeneration()
    {
        if (_generation == int.MaxValue)
        {
            Array.Clear(_cell);
            _generation = 0;
        }

        _generation++;
    }
}
