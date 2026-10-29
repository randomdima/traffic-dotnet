using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// The pieces of ground that belong to no road and to no line through a box: the rectangles a slab of
/// paving is, and the rings the water and its shore are cut from.
/// </summary>
/// <remarks>
/// <b>A small ring is filed by row, and that is the reading.</b> A generated town's slabs are a few dozen
/// boxes and its water a handful of rings of a few hundred points at most, each tested against its own bounding box
/// first; but a coast's box is half the map, so a point inside it asks only the edges whose heights cross its own row
/// (<see cref="EdgeRows"/>). <b>A traced town's sea is filed by cell</b>: its shore is one ring of thousands of points whose
/// box is most of the city, so a ring longer than <see cref="Rings.FiledFrom"/> has its edges filed by the
/// cells of the town's grid (<see cref="EdgeCells"/>).
/// </remarks>
internal sealed partial class GroundShapes
{
    Vector2[] _slabMinM = [];
    Vector2[] _slabSizeM = [];

    Rings _water;
    Rings _shore;
    Rings _rail;
    Rings _plazas;

    /// <summary>Whether the railway's ballast covers a point or stands within reach of it (RAIL-4, RAIL-5).</summary>
    public bool RailWithin(Vector2 pointM, float reachM) => _rail.Within(pointM, reachM);

    /// <summary>
    /// A set of closed rings, the box each of them fits in, the edges of each long one by cell and of every other by row, and
    /// the rings themselves by the rows of the main grid their boxes span (<see cref="BoxRows"/>).
    /// </summary>
    readonly record struct Rings(CityPlan.RingArrays Of, Vector2[] LeastM, Vector2[] MostM, EdgeCells?[] Cells, EdgeRows?[] Rows, BoxRows ByRow)
    {
        /// <summary>The fewest points a ring is filed by cell at: past them, a walk of its edges costs more than finding the few near a point.</summary>
        public const int FiledFrom = 256;

        /// <summary>
        /// Whether a point stands inside any of them, by how many times a ray from it crosses the outline.
        /// The box is tested first, which is what keeps the water off the cost of a query on dry land.
        /// </summary>
        public bool Covers(Vector2 pointM)
        {
            foreach (var ring in ByRow.At(pointM.Y))
            {
                if (pointM.X < LeastM[ring].X || pointM.X > MostM[ring].X) continue;
                if (pointM.Y < LeastM[ring].Y || pointM.Y > MostM[ring].Y) continue;
                if (Cells[ring] is { } cells ? cells.Inside(Of.RingOf(ring), pointM) : Rows[ring]!.Inside(Of.RingOf(ring), pointM)) return true;
            }

            return false;
        }

        public static Rings Boxed(CityPlan.RingArrays rings, GridLevel level)
        {
            var leastM = new Vector2[rings.Count];
            var mostM = new Vector2[rings.Count];
            var cells = new EdgeCells?[rings.Count];
            var rows = new EdgeRows?[rings.Count];
            for (var ring = 0; ring < rings.Count; ring++)
            {
                var least = new Vector2(float.MaxValue);
                var most = new Vector2(float.MinValue);
                foreach (var pointM in rings.RingOf(ring))
                {
                    least = Vector2.Min(least, pointM);
                    most = Vector2.Max(most, pointM);
                }

                leastM[ring] = least;
                mostM[ring] = most;
                if (rings.RingOf(ring).Length >= FiledFrom) cells[ring] = new EdgeCells(rings.RingOf(ring), least, most, level.CellM);
                else rows[ring] = new EdgeRows(rings.RingOf(ring), least.Y, most.Y, level.CellM);
            }

            return new Rings(rings, leastM, mostM, cells, rows, new BoxRows(leastM, mostM, level));
        }

        /// <summary>
        /// Whether a point stands inside any of them or within reach of one. The edge distance is measured
        /// segment by segment over the rows within reach — and the box, grown by the
        /// reach, is what keeps a query on dry land off that cost.
        /// </summary>
        public bool Within(Vector2 pointM, float reachM)
        {
            // A row either side as well, a row being far wider than anything a float rounds the reach by; each ring asked
            // once, at the first of these rows it is filed in.
            var (first, last) = (ByRow.RowOf(pointM.Y - reachM) - 1, ByRow.RowOf(pointM.Y + reachM) + 1);
            for (var row = first; row <= last; row++)
            {
                foreach (var ring in ByRow.In(row))
                {
                    if (Math.Max(ByRow.RowOf(LeastM[ring].Y), first) != row) continue;
                    if (pointM.X < LeastM[ring].X - reachM || pointM.X > MostM[ring].X + reachM) continue;
                    if (pointM.Y < LeastM[ring].Y - reachM || pointM.Y > MostM[ring].Y + reachM) continue;

                    var edgeM = Of.RingOf(ring);
                    if (Cells[ring] is { } cells)
                    {
                        if (cells.Inside(edgeM, pointM) || cells.Near(edgeM, pointM, reachM)) return true;

                        continue;
                    }

                    if (Rows[ring]!.Inside(edgeM, pointM) || Rows[ring]!.Near(edgeM, pointM, reachM)) return true;
                }
            }

            return false;
        }

        public static float OffTheEdgeM(Vector2 fromM, Vector2 toM, Vector2 pointM)
        {
            var runM = toM - fromM;
            var lengthSquared = runM.LengthSquared();
            var alongM = lengthSquared > 0f
                ? Math.Clamp(Vector2.Dot(pointM - fromM, runM) / lengthSquared, 0f, 1f)
                : 0f;
            return (pointM - (fromM + (runM * alongM))).Length();
        }
    }

    /// <summary>
    /// <b>Rings filed by the rows of the main grid their boxes span</b> (SIM-8): a point asks only the rings whose box
    /// reaches its own height, rather than every ring the town has — every piece of railway, on every ask about the ground.
    /// </summary>
    /// <remarks>
    /// <b>Every ring the box test would keep, and the box test still asked</b>: a row is the floor of a height scaled by the
    /// grid, which rounds monotonically, so a height inside a box is in a row the box spans — and whether any ring covers a
    /// point does not hang on the order they are asked in.
    /// </remarks>
    internal sealed class BoxRows
    {
        readonly GridLevel _level;
        readonly int _firstRow;
        readonly int[] _start;
        readonly int[] _rings;

        public BoxRows(Vector2[] leastM, Vector2[] mostM, GridLevel level)
        {
            _level = level;
            var (first, last) = (int.MaxValue, int.MinValue);
            for (var ring = 0; ring < leastM.Length; ring++)
            {
                if (leastM[ring].Y > mostM[ring].Y) continue;

                first = Math.Min(first, RowOf(leastM[ring].Y));
                last = Math.Max(last, RowOf(mostM[ring].Y));
            }

            if (first > last)
            {
                (_start, _rings) = ([0], []);
                return;
            }

            _firstRow = first;
            _start = new int[last - first + 2];
            for (var ring = 0; ring < leastM.Length; ring++)
            {
                if (leastM[ring].Y > mostM[ring].Y) continue;

                for (var row = RowOf(leastM[ring].Y); row <= RowOf(mostM[ring].Y); row++) _start[row - first + 1]++;
            }

            for (var row = 0; row < _start.Length - 1; row++) _start[row + 1] += _start[row];

            _rings = new int[_start[^1]];
            var filled = new int[_start.Length - 1];
            for (var ring = 0; ring < leastM.Length; ring++)
            {
                if (leastM[ring].Y > mostM[ring].Y) continue;

                for (var row = RowOf(leastM[ring].Y) - first; row <= RowOf(mostM[ring].Y) - first; row++) _rings[_start[row] + filled[row]++] = ring;
            }
        }

        public int RowOf(float yM) => _level.CellOf(yM);

        /// <summary>The rings whose boxes span a row of the main grid, none off the rows any box spans.</summary>
        public ReadOnlySpan<int> In(int row)
        {
            var at = row - _firstRow;
            return (uint)at < (uint)(_start.Length - 1) ? _rings.AsSpan(_start[at], _start[at + 1] - _start[at]) : [];
        }

        public ReadOnlySpan<int> At(float yM) => In(RowOf(yM));
    }

    /// <summary>
    /// <b>One ring's edges filed by the rows of the town's grid their heights span</b>, edge <c>e</c> running from point
    /// <c>e</c> to the one before it: whether a point is inside asks only the edges of its own row, and how near it comes
    /// only those of the rows within reach of it.
    /// </summary>
    /// <remarks>
    /// <b>The walk of every edge answered by fewer of them, to the bit</b>: an edge a ray along a point's height crosses
    /// spans that height and so is filed in its row, each is asked with the arithmetic the walk asked it with, and which
    /// order the crossings are counted in leaves their parity alone. An edge within reach spans a height within reach,
    /// and the row either side of those is read as well, a row being far wider than anything a float rounds a reach by.
    /// </remarks>
    internal sealed class EdgeRows
    {
        readonly float _leastY;
        readonly float _cellM;
        readonly int _rows;
        readonly int[] _start;
        readonly int[] _edges;

        public EdgeRows(ReadOnlySpan<Vector2> ringM, float leastY, float mostY, float cellM)
        {
            (_leastY, _cellM) = (leastY, cellM);
            _rows = Math.Max(1, (int)MathF.Ceiling((mostY - leastY) / cellM));
            _start = new int[_rows + 1];
            for (int here = 0, there = ringM.Length - 1; here < ringM.Length; there = here++)
            {
                for (var row = Row(MathF.Min(ringM[here].Y, ringM[there].Y)); row <= Row(MathF.Max(ringM[here].Y, ringM[there].Y)); row++) _start[row + 1]++;
            }

            for (var row = 0; row < _rows; row++) _start[row + 1] += _start[row];

            _edges = new int[_start[^1]];
            var filled = new int[_rows];
            for (int here = 0, there = ringM.Length - 1; here < ringM.Length; there = here++)
            {
                for (var row = Row(MathF.Min(ringM[here].Y, ringM[there].Y)); row <= Row(MathF.Max(ringM[here].Y, ringM[there].Y)); row++)
                {
                    _edges[_start[row] + filled[row]++] = here;
                }
            }
        }

        int Row(float yM) => Math.Clamp((int)MathF.Floor((yM - _leastY) / _cellM), 0, _rows - 1);

        /// <summary>
        /// The edges filed in the row a height stands in: every edge a ray along that height can cross, for a caller
        /// counting the crossings with arithmetic of its own (<see cref="Zones.ZoneTree"/>).
        /// </summary>
        public ReadOnlySpan<int> At(float yM)
        {
            var row = Row(yM);
            return _edges.AsSpan(_start[row], _start[row + 1] - _start[row]);
        }

        public bool Inside(ReadOnlySpan<Vector2> ringM, Vector2 pointM)
        {
            var inside = false;
            foreach (var here in At(pointM.Y))
            {
                var a = ringM[here];
                var b = ringM[here == 0 ? ringM.Length - 1 : here - 1];
                if (a.Y > pointM.Y == b.Y > pointM.Y) continue;
                if (pointM.X < ((b.X - a.X) * (pointM.Y - a.Y) / (b.Y - a.Y)) + a.X) inside = !inside;
            }

            return inside;
        }

        public bool Near(ReadOnlySpan<Vector2> ringM, Vector2 pointM, float reachM)
        {
            var (first, last) = (Math.Max(0, Row(pointM.Y - reachM) - 1), Math.Min(_rows - 1, Row(pointM.Y + reachM) + 1));
            for (var at = _start[first]; at < _start[last + 1]; at++)
            {
                var here = _edges[at];
                if (Rings.OffTheEdgeM(ringM[here == 0 ? ringM.Length - 1 : here - 1], ringM[here], pointM) <= reachM) return true;
            }

            return false;
        }
    }

    /// <summary>
    /// <b>One long ring's edges filed by the cells of the town's grid they pass through</b>, edge <c>e</c> running
    /// from point <c>e</c> to the next: a point asks the edges in the cells round it, and whether it is inside
    /// asks only the edges along its own row east of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An edge is filed in every cell its box touches, and a crossing is counted in one of them</b> — the cell
    /// the crossing itself stands in, kept to the edge's own cells — so an edge across several cells of a row is
    /// crossed once and the count is the one a walk of the whole ring would make.
    /// </para>
    /// <para>
    /// <b>A cell no edge passes through is wholly inside or wholly outside</b>, settled once a row as it is filed,
    /// so the ray stops at the first such cell east of the point: what it crossed on the way, and which that cell
    /// is.
    /// </para>
    /// </remarks>
    internal sealed class EdgeCells
    {
        readonly Vector2 _leastM;
        readonly float _cellM;
        readonly int _columns;
        readonly int _rows;
        readonly int[] _start;
        readonly int[] _edges;
        readonly bool[] _emptyInside;

        public EdgeCells(ReadOnlySpan<Vector2> ringM, Vector2 leastM, Vector2 mostM, float cellM)
        {
            (_leastM, _cellM) = (leastM, cellM);
            _columns = Math.Max(1, (int)MathF.Ceiling((mostM.X - leastM.X) / cellM));
            _rows = Math.Max(1, (int)MathF.Ceiling((mostM.Y - leastM.Y) / cellM));
            _start = new int[(_columns * _rows) + 1];

            // Counted, summed into where each cell's run starts, then filled: one array of edges for the ring.
            foreach (var cell in CellsOf(ringM)) _start[cell.Cell + 1]++;
            for (var cell = 0; cell < _columns * _rows; cell++) _start[cell + 1] += _start[cell];

            _edges = new int[_start[^1]];
            var filled = new int[_columns * _rows];
            foreach (var (cell, edge) in CellsOf(ringM)) _edges[_start[cell] + filled[cell]++] = edge;

            _emptyInside = new bool[_columns * _rows];
            var crossingsX = new List<float>();
            for (var row = 0; row < _rows; row++)
            {
                var middleM = new Vector2(_leastM.X, _leastM.Y + ((row + 0.5f) * cellM));
                crossingsX.Clear();
                for (var column = 0; column < _columns; column++) Crossed(ringM, middleM, row, column, crossingsX);

                crossingsX.Sort();
                var east = 0;
                for (var column = 0; column < _columns; column++)
                {
                    var cell = (row * _columns) + column;
                    var centreX = _leastM.X + ((column + 0.5f) * cellM);
                    while (east < crossingsX.Count && crossingsX[east] <= centreX) east++;
                    if (_start[cell] == _start[cell + 1]) _emptyInside[cell] = (crossingsX.Count - east) % 2 == 1;
                }
            }
        }

        /// <summary>The crossings of a ray east along a row this cell counts (see the remarks), added to <paramref name="into"/>.</summary>
        void Crossed(ReadOnlySpan<Vector2> ringM, Vector2 pointM, int row, int column, List<float> into)
        {
            var cell = (row * _columns) + column;
            for (var at = _start[cell]; at < _start[cell + 1]; at++)
            {
                var edge = _edges[at];
                var (a, b) = (ringM[edge], ringM[(edge + 1) % ringM.Length]);
                if (a.Y > pointM.Y == b.Y > pointM.Y) continue;

                var crossingX = ((b.X - a.X) * (pointM.Y - a.Y) / (b.Y - a.Y)) + a.X;
                var counted = Math.Clamp(Cell(new Vector2(crossingX, pointM.Y)).Column, Cell(Vector2.Min(a, b)).Column, Cell(Vector2.Max(a, b)).Column);
                if (counted == column) into.Add(crossingX);
            }
        }

        /// <summary>Every cell each edge's box touches, edge by edge — a build-time walk, run twice.</summary>
        List<(int Cell, int Edge)> CellsOf(ReadOnlySpan<Vector2> ringM)
        {
            var cells = new List<(int Cell, int Edge)>(ringM.Length * 2);
            for (var edge = 0; edge < ringM.Length; edge++)
            {
                var (fromM, toM) = (ringM[edge], ringM[(edge + 1) % ringM.Length]);
                var (least, most) = (Cell(Vector2.Min(fromM, toM)), Cell(Vector2.Max(fromM, toM)));
                for (var row = least.Row; row <= most.Row; row++)
                {
                    for (var column = least.Column; column <= most.Column; column++) cells.Add(((row * _columns) + column, edge));
                }
            }

            return cells;
        }

        (int Column, int Row) Cell(Vector2 pointM) => (
            Math.Clamp((int)MathF.Floor((pointM.X - _leastM.X) / _cellM), 0, _columns - 1),
            Math.Clamp((int)MathF.Floor((pointM.Y - _leastM.Y) / _cellM), 0, _rows - 1));

        public bool Inside(ReadOnlySpan<Vector2> ringM, Vector2 pointM)
        {
            if (pointM.Y < _leastM.Y || pointM.Y > _leastM.Y + (_rows * _cellM)) return false;

            var inside = false;
            var (from, row) = Cell(pointM);
            for (var column = from; column < _columns; column++)
            {
                var cell = (row * _columns) + column;
                if (_start[cell] == _start[cell + 1]) return inside != _emptyInside[cell];

                for (var at = _start[cell]; at < _start[cell + 1]; at++)
                {
                    var edge = _edges[at];
                    var (a, b) = (ringM[edge], ringM[(edge + 1) % ringM.Length]);
                    if (a.Y > pointM.Y == b.Y > pointM.Y) continue;

                    var crossingX = ((b.X - a.X) * (pointM.Y - a.Y) / (b.Y - a.Y)) + a.X;
                    if (pointM.X >= crossingX) continue;

                    var counted = Math.Clamp(Cell(new Vector2(crossingX, pointM.Y)).Column, Cell(Vector2.Min(a, b)).Column, Cell(Vector2.Max(a, b)).Column);
                    if (counted == column) inside = !inside;
                }
            }

            return inside;
        }

        public bool Near(ReadOnlySpan<Vector2> ringM, Vector2 pointM, float reachM)
        {
            var (least, most) = (Cell(pointM - new Vector2(reachM)), Cell(pointM + new Vector2(reachM)));
            for (var row = least.Row; row <= most.Row; row++)
            {
                for (var column = least.Column; column <= most.Column; column++)
                {
                    var cell = (row * _columns) + column;
                    for (var at = _start[cell]; at < _start[cell + 1]; at++)
                    {
                        var edge = _edges[at];
                        if (Rings.OffTheEdgeM(ringM[edge], ringM[(edge + 1) % ringM.Length], pointM) <= reachM) return true;
                    }
                }
            }

            return false;
        }
    }

    bool SlabReaches(Vector2 pointM) => SlabWithin(pointM, 0f);

    bool SlabWithin(Vector2 pointM, float reachM)
    {
        for (var slab = 0; slab < _slabMinM.Length; slab++)
        {
            var offsetM = pointM - _slabMinM[slab];
            if (offsetM.X < -reachM || offsetM.Y < -reachM) continue;
            if (offsetM.X > _slabSizeM[slab].X + reachM || offsetM.Y > _slabSizeM[slab].Y + reachM) continue;

            return true;
        }

        return false;
    }

    /// <remarks>
    /// <b>A kerb fillet is not among them.</b> The wedge between two kerbs is what the boundary
    /// has left over once every movement has taken what it sweeps, and the boundary turns that corner
    /// itself (<see cref="LaneShell"/>) — so the shape the plan carries for it is drawn by nobody
    /// and answered by nobody, and the ground there is whichever side of that boundary it stands on.
    /// </remarks>
    void LayTheShapes(Paving paving, SimConfig config)
    {
        var plan = paving.Of;

        _slabMinM = plan.PavedAreas.MinM;
        _slabSizeM = plan.PavedAreas.SizeM;

        _water = Rings.Boxed(plan.Water.Outline, config.Grid.Main);
        _shore = Rings.Boxed(plan.Water.Shore, config.Grid.Main);
        _rail = Rings.Boxed(plan.Rails.Ballast, config.Grid.Main);
        _plazas = Rings.Boxed(plan.Plazas, config.Grid.Main);
    }
}
