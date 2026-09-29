using System.Numerics;
using System.Runtime.CompilerServices;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>Which side of a set of closed rings a point stands on</b>, as the number of times the rings wind
/// round it: above nought inside the shape they bound, nought or below outside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exact, and a lookup almost everywhere.</b> A lattice is laid over the rings and every cell keeps how
/// many times they wind round its low corner. A cell no ring passes through is one side throughout, so its
/// corner is the answer. A cell a ring does pass through walks from its corner to the point — up the cell's
/// own edge, then across — and counts the pieces it crosses on the way, which are the cell's own and no
/// others. Nothing is rounded to the cell: the lattice says where to look and never what is there.
/// </para>
/// <para>
/// <b>The pieces are cut wherever they turn through a quarter</b>, so each one runs one way in x and one
/// way in y: it crosses a line at most once, whether it does is read off its two ends, and its box is its
/// two ends.
/// </para>
/// <para>
/// <b>Counted half-open</b>: a piece crosses a line when one end stands strictly past it and the other does
/// not, so a joint standing on the line is counted once, by one of the two pieces meeting there. Read as
/// geometry, every line stands a hair past where it is drawn — a row's a hair above it and a column's a
/// hair right of it, the row's hair far the finer — and a crossing that lands exactly on the end of a leg
/// is decided by which way the piece is running there, as it would be a hair away. So a ring through a
/// corner of the lattice, or along one of its lines, is answered like any other.
/// </para>
/// <para>
/// <b>A winding and not a parity</b>, because it is how the fill reads the same rings
/// (<see cref="ShellFill"/>): a ring walked one way round is a piece of the shape and walked the other a
/// hole in it. The two readings differ only for a hole nothing encloses, which the fill leaves unfilled
/// and a winding of minus one puts outside.
/// </para>
/// </remarks>
internal sealed class RingSides
{
    /// <summary>
    /// How far past its own ends a piece's box is grown when it is filed into cells: far past what the
    /// floating point strays by, and far short of anything a cell is wide.
    /// </summary>
    const double FilingSlackM = 1e-3;

    /// <summary>
    /// How far the window reaches past the rings, in cells: a whole one, so the rim's corners stand clear of
    /// every ring and wind nought.
    /// </summary>
    const int MarginCells = 1;

    /// <summary>How many bits of a cell's word its corner's winding takes (<see cref="_cell"/>).</summary>
    const int WindingBits = 8;

    /// <summary>The cells of the grid the rings are filed over (SIM-8), at the level their caller chose.</summary>
    readonly GridWindow _window;

    /// <summary>The level's cell in doubles, so a corner is placed by one product wherever it is asked for.</summary>
    readonly double _cellM;

    /// <summary>
    /// <b>One word a cell</b>: the winding round its low corner in the low byte, and where its pieces begin in
    /// <see cref="_cellPiece"/> above it — one past the last cell carrying the total. One word and not two
    /// arrays, because an answer is a read the lattice cannot predict and one line fetched is half of two.
    /// </summary>
    /// <remarks>
    /// <b>A long and not an int</b>: twenty-four bits of filing are sixteen million, and a town thirty
    /// kilometres across files fourteen million of its carriageway's pieces into a lattice at the shell's level.
    /// </remarks>
    readonly long[] _cell;

    readonly int[] _cellPiece;
    readonly Piece[] _pieces;

    RingSides(GridWindow window, long[] cell, int[] cellPiece, Piece[] pieces)
    {
        _window = window;
        _cellM = window.Level.CellM;
        _cell = cell;
        _cellPiece = cellPiece;
        _pieces = pieces;
    }

    /// <summary>No rings at all: every point is outside.</summary>
    public static RingSides None { get; } = new(GridWindow.Of(new WorldGrid(1f).Main, 0, 0, 0, 0), [0], [], []);

    /// <summary>How many pieces the rings were cut into — a census.</summary>
    public int PieceCount => _pieces.Length;

    /// <summary>The cells of the grid the rings are filed over.</summary>
    public GridWindow Window => _window;

    /// <summary>How many cells the lattice has.</summary>
    public int CellCount => _window.Count;

    /// <summary>How many of them a ring passes through, which is where an answer costs more than a lookup.</summary>
    public int CrossedCellCount
    {
        get
        {
            var crossed = 0;
            for (var cell = 0; cell < CellCount; cell++)
            {
                if (_cell[cell + 1] >> WindingBits > _cell[cell] >> WindingBits) crossed++;
            }

            return crossed;
        }
    }

    /// <summary>What the lattice and the pieces hold, in bytes.</summary>
    public long Bytes =>
        (8L * _cell.Length) + (4L * _cellPiece.Length) + ((long)Unsafe.SizeOf<Piece>() * _pieces.Length);

    /// <summary>Whether the point stands inside the shape the rings bound.</summary>
    public bool Encloses(Vector2 pointM) => WindingAt(pointM) > 0;

    /// <summary>
    /// <b>How many times the rings wind round the point</b> — counted positive round a ring whose signed area
    /// is positive (<see cref="ShellFill"/>'s pieces) and negative round one whose area is negative (its
    /// holes). Nought anywhere off the lattice, which covers every ring.
    /// </summary>
    public int WindingAt(Vector2 pointM)
    {
        var x = (double)pointM.X;
        var y = (double)pointM.Y;
        var (column, row) = _window.Level.CellOf(pointM);

        // The corner is placed the way the lattice laid it, and a point a rounding short of it is the cell's
        // below: both legs then run forwards, and a leg run backwards would count its crossings the wrong way.
        var cornerX = Edge(column);
        var cornerY = Edge(row);
        if (x < cornerX) cornerX = Edge(--column);
        if (y < cornerY) cornerY = Edge(--row);
        if (!_window.Holds(column, row)) return 0;

        var cell = _window.IndexOf(column, row);
        var word = _cell[cell];
        var winding = (int)(sbyte)word;
        var first = (int)(word >> WindingBits);
        var last = (int)(_cell[cell + 1] >> WindingBits);
        if (first == last) return winding;

        // Up the cell's own left edge from its corner to the point's height, then across to the point: every
        // piece either leg crosses has its box in this cell, so this cell's pieces are all there are to ask.
        // Whether a piece reaches either line at all is two comparisons of its ends, asked here, so the
        // arithmetic of a crossing is paid only by the pieces that make one.
        var pieces = _pieces;
        var cellPiece = _cellPiece;
        for (var entry = first; entry < last; entry++)
        {
            ref readonly var piece = ref pieces[cellPiece[entry]];
            if (piece.FromM.X > cornerX != piece.ToM.X > cornerX) winding += piece.Up(cornerX, cornerY, y);
            if (piece.FromM.Y > y != piece.ToM.Y > y) winding -= piece.Across(y, cornerX, x);
        }

        return winding;
    }

    /// <summary>Where a line of the lattice stands, on either axis — the one product every corner is placed by.</summary>
    double Edge(int cell) => cell * _cellM;

    /// <summary>
    /// <b>The lattice laid over a set of closed rings</b>, each ring a chain whose last piece ends where its
    /// first begins. Build-time: it allocates freely.
    /// </summary>
    /// <param name="level">
    /// The level of the grid the pieces are filed at. A cell no ring passes through is answered by a lookup,
    /// so the finer the cells the more of the ground that is — at a word a cell.
    /// </param>
    public static RingSides Of(ReadOnlySpan<ArcSeg[]> rings, GridLevel level)
    {
        var pieces = new List<Piece>();
        foreach (var ring in rings)
        {
            for (var at = 0; at < ring.Length; at++)
            {
                // Each piece ends where the next begins, and the last where the first does, so a joint is one
                // point to both pieces meeting at it and is counted once.
                Cut(ring[at], ring[(at + 1) % ring.Length].StartM, pieces);
            }
        }

        if (pieces.Count == 0) return None;

        var leastM = new Vector2(float.MaxValue);
        var mostM = new Vector2(float.MinValue);
        foreach (var piece in pieces)
        {
            leastM = Vector2.Min(leastM, Vector2.Min(piece.FromM, piece.ToM));
            mostM = Vector2.Max(mostM, Vector2.Max(piece.FromM, piece.ToM));
        }

        var window = GridWindow.Over(level, leastM, mostM, MarginCells);
        if ((long)window.Width * window.Height > Array.MaxLength / 4)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level.CellM, $"{window.Width}x{window.Height} cells is too many");
        }

        var laid = pieces.ToArray();
        var cornerWinding = Corners(laid, window);
        var (cellFirst, cellPiece) = File(laid, window);

        var cell = new long[cellFirst.Length];
        for (var at = 0; at < cell.Length; at++)
        {
            var winding = at < cornerWinding.Length ? cornerWinding[at] : (sbyte)0;
            cell[at] = ((long)cellFirst[at] << WindingBits) | (byte)winding;
        }

        return new RingSides(window, cell, cellPiece, laid);
    }

    /// <summary>
    /// <b>One ring piece cut where its heading passes a quarter turn</b> — where its tangent stands square to
    /// an axis — so every part runs one way in x and one way in y.
    /// </summary>
    static void Cut(in ArcSeg arc, Vector2 endM, List<Piece> into)
    {
        if (arc.Curvature == 0f)
        {
            into.Add(Piece.Straight(arc.StartM, endM));
            return;
        }

        var curvature = (double)arc.Curvature;
        var fromRad = (double)arc.HeadingRad;
        var toRad = fromRad + (curvature * arc.LengthM);
        var quarter = Math.PI * 0.5;
        var direction = curvature > 0d ? 1 : -1;

        // The quarter turns strictly inside the sweep, in the order the piece meets them.
        var first = curvature > 0d
            ? (long)Math.Floor(fromRad / quarter) + 1
            : (long)Math.Ceiling(fromRad / quarter) - 1;

        var fromS = 0d;
        var fromM = arc.StartM;
        for (var turn = first; ; turn += direction)
        {
            var atRad = turn * quarter;
            if (curvature > 0d ? atRad >= toRad : atRad <= toRad) break;

            var atS = (atRad - fromRad) / curvature;
            var atM = arc.PointAtM((float)atS);
            into.Add(Piece.Curved(arc, fromS, atS, fromM, atM));
            fromS = atS;
            fromM = atM;
        }

        into.Add(Piece.Curved(arc, fromS, arc.LengthM, fromM, endM));
    }

    /// <summary>
    /// The winding round every cell's low corner: along each row's own line, the signed crossings standing
    /// past each corner, which is a ray cast from it — summed once a row from the right rather than once a
    /// corner.
    /// </summary>
    static sbyte[] Corners(Piece[] pieces, GridWindow window)
    {
        var crossings = new List<(int Row, double X, int Sign)>();
        var cellM = (double)window.Level.CellM;
        foreach (var piece in pieces)
        {
            // A row either side of the ones the piece's own span reaches, so a line a rounding off it is asked.
            var fromRow = Math.Max(window.FromY, window.Level.CellOf(MathF.Min(piece.FromM.Y, piece.ToM.Y)) - 1);
            var toRow = Math.Min(window.ToY, window.Level.CellOf(MathF.Max(piece.FromM.Y, piece.ToM.Y)) + 1);
            for (var row = fromRow; row <= toRow; row++)
            {
                var y = row * cellM;
                if (!piece.Spans(y)) continue;

                crossings.Add((row - window.FromY, piece.XAt(y), piece.Rises ? 1 : -1));
            }
        }

        crossings.Sort((one, other) => one.Row != other.Row ? one.Row.CompareTo(other.Row) : one.X.CompareTo(other.X));

        var winding = new sbyte[window.Count];
        var end = 0;
        for (var row = 0; row < window.Height; row++)
        {
            var start = end;
            while (end < crossings.Count && crossings[end].Row == row) end++;

            var past = end;
            var sum = 0;
            for (var column = window.Width - 1; column >= 0; column--)
            {
                var x = (window.FromX + column) * cellM;
                while (past > start && crossings[past - 1].X > x) sum += crossings[--past].Sign;

                winding[(row * window.Width) + column] = checked((sbyte)sum);
            }
        }

        return winding;
    }

    /// <summary>
    /// Every piece filed under every cell its box reaches, closed on all four sides — the two legs of an
    /// answer run along a cell's edges as well as through it.
    /// </summary>
    static (int[] CellFirst, int[] CellPiece) File(Piece[] pieces, GridWindow window)
    {
        var cellFirst = new int[window.Count + 1];
        foreach (var piece in pieces)
        {
            var range = Reach(piece, window);
            for (var row = range.FromY; row <= range.ToY; row++)
            {
                for (var column = range.FromX; column <= range.ToX; column++) cellFirst[window.IndexOf(column, row) + 1]++;
            }
        }

        for (var cell = 0; cell < window.Count; cell++) cellFirst[cell + 1] += cellFirst[cell];

        var cellPiece = new int[cellFirst[^1]];
        var written = (int[])cellFirst.Clone();
        for (var index = 0; index < pieces.Length; index++)
        {
            var range = Reach(pieces[index], window);
            for (var row = range.FromY; row <= range.ToY; row++)
            {
                for (var column = range.FromX; column <= range.ToX; column++)
                {
                    cellPiece[written[window.IndexOf(column, row)]++] = index;
                }
            }
        }

        return (cellFirst, cellPiece);
    }

    /// <summary>The cells one piece's box reaches, grown by <see cref="FilingSlackM"/>.</summary>
    static CellRange Reach(in Piece piece, GridWindow window)
    {
        var slack = new Vector2((float)FilingSlackM);
        window.TryRange(Vector2.Min(piece.FromM, piece.ToM) - slack, Vector2.Max(piece.FromM, piece.ToM) + slack, out var range);
        return range;
    }

    /// <summary>
    /// <b>One stretch of a ring that runs one way in x and one way in y</b>: a straight, or an arc inside one
    /// quarter of its circle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An arc is held by its start and the way back from its centre to it</b>
    /// (<paramref name="RadialM"/>), never by the centre: a road's gentlest bend has a centre kilometres off,
    /// and a crossing worked out as the centre plus a root would cancel every figure a float has. Worked from
    /// the start, the crossing is the start plus a difference of squares taken over a sum that cannot cancel.
    /// </para>
    /// <para>
    /// <b>A straight is held by its two slopes</b> (<paramref name="Slope"/>: across per up, and up per
    /// across), so a crossing is a multiply and not a divide. Neither is read for the line the piece runs
    /// along, which it never crosses.
    /// </para>
    /// </remarks>
    readonly record struct Piece(
        Vector2 FromM, Vector2 ToM, Vector2 RadialM, Vector2 Slope, sbyte SideX, sbyte SideY, bool Bends)
    {
        public static Piece Straight(Vector2 fromM, Vector2 toM)
        {
            var alongM = toM - fromM;
            return new Piece(
                fromM, toM, Vector2.Zero,
                new Vector2(alongM.Y != 0f ? alongM.X / alongM.Y : 0f, alongM.X != 0f ? alongM.Y / alongM.X : 0f),
                0, 0, false);
        }

        /// <summary>The part of an arc between two distances along it, which the caller keeps inside a quarter.</summary>
        public static Piece Curved(in ArcSeg arc, double fromS, double toS, Vector2 fromM, Vector2 toM)
        {
            var curvature = (double)arc.Curvature;
            var fromRad = arc.HeadingRad + (curvature * fromS);
            var middleRad = arc.HeadingRad + (curvature * (fromS + toS) * 0.5d);

            // From the centre to the point the heading stands at: a quarter turn back from the way the arc
            // bends (Heading.RightOf), at the radius.
            var (fromSin, fromCos) = Math.SinCos(fromRad);
            var (middleSin, middleCos) = Math.SinCos(middleRad);
            return new Piece(
                fromM, toM, new Vector2((float)(fromSin / curvature), (float)(-fromCos / curvature)), Vector2.Zero,
                Side(middleSin / curvature), Side(-middleCos / curvature), true);
        }

        static sbyte Side(double offM) => offM < 0d ? (sbyte)-1 : (sbyte)1;

        /// <summary>Whether the piece runs up the y axis rather than down it.</summary>
        public bool Rises => ToM.Y > FromM.Y;

        /// <summary>Whether the piece crosses the line at <paramref name="y"/>, counted half-open.</summary>
        public bool Spans(double y) => FromM.Y > y != ToM.Y > y;

        /// <summary>
        /// The signed crossing of the leg up the line at <paramref name="x"/> from <paramref name="fromY"/> to
        /// <paramref name="toY"/>, for a piece the caller has found reaches across that line: one for a piece
        /// running up x, which the leg enters the shape by.
        /// </summary>
        /// <remarks>
        /// The leg runs a hair right of its line and its two ends stand a finer hair above theirs, so a
        /// crossing exactly at an end is inside the leg at its foot only where the piece climbs rightwards, and
        /// at its head only where it does not.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Up(double x, double fromY, double toY)
        {
            var y = YAt(x);
            var climbs = ToM.Y != FromM.Y && ToM.Y > FromM.Y == ToM.X > FromM.X;
            if (y < fromY || (y == fromY && !climbs)) return 0;
            if (y > toY || (y == toY && climbs)) return 0;

            return ToM.X > FromM.X ? 1 : -1;
        }

        /// <summary>
        /// The signed crossing of the leg along the line at <paramref name="y"/> from just past
        /// <paramref name="fromX"/> to <paramref name="toX"/>, for a piece the caller has found spans that line:
        /// one for a piece running up y.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Across(double y, double fromX, double toX)
        {
            var x = XAt(y);
            if (x <= fromX || x > toX) return 0;

            return Rises ? 1 : -1;
        }

        /// <summary>Where the piece crosses the line at <paramref name="y"/>, which the caller has found it does.</summary>
        /// <remarks>
        /// A line through either end answers that end exactly, so whether the crossing stands on a leg's end
        /// is a comparison and not a rounding.
        /// </remarks>
        public double XAt(double y)
        {
            if (y == FromM.Y) return FromM.X;
            if (y == ToM.Y) return ToM.X;

            var upM = y - FromM.Y;
            if (!Bends) return FromM.X + (upM * Slope.X);

            var outM = (double)RadialM.X;
            var shortfall = upM * ((2d * RadialM.Y) + upM);
            var sum = outM + (SideX * Math.Sqrt(Math.Max(0d, (outM * outM) - shortfall)));
            return sum == 0d ? FromM.X : FromM.X - (shortfall / sum);
        }

        /// <summary>Where the piece crosses the line at <paramref name="x"/>, which the caller has found it does.</summary>
        public double YAt(double x)
        {
            if (x == FromM.X) return FromM.Y;
            if (x == ToM.X) return ToM.Y;

            var acrossM = x - FromM.X;
            if (!Bends) return FromM.Y + (acrossM * Slope.Y);

            var outM = (double)RadialM.Y;
            var shortfall = acrossM * ((2d * RadialM.X) + acrossM);
            var sum = outM + (SideY * Math.Sqrt(Math.Max(0d, (outM * outM) - shortfall)));
            return sum == 0d ? FromM.Y : FromM.Y - (shortfall / sum);
        }
    }
}
