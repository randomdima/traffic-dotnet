using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>The one grid every index over the map is laid on</b> (SIM-8): lines through the world's origin a main
/// cell apart, and levels under it that cut every cell into halves, quarters and so on down.
/// </summary>
/// <remarks>
/// <para>
/// <b>An index numbers its cells on this grid and never on a lattice of its own</b>, so any two indexes know
/// how their cells relate without asking either: a cell lies in the coarser cell its coordinates shift right
/// to, and holds the block of finer ones they shift left to (<see cref="GridLevel.Up"/>,
/// <see cref="GridLevel.Down"/>). What an index keeps in its cells is its own — bodies, circles, lines,
/// samples — and so is the rectangle of cells it keeps (<see cref="GridWindow"/>); the numbering is the grid's.
/// </para>
/// <para>
/// <b>Only halvings</b>, because they are what makes that relation exact. A point's cell on every level is
/// the floor of one scaled coordinate — the metres over the main cell — times a power of two, and scaling a
/// float by a power of two loses nothing: no rounding can put a point in a fine cell outside the coarse cell
/// holding it, whatever the main cell is. <b>The one exception is the subnormal floats a hair either side of
/// nought</b>, which a halving can round to nought itself — no place in a town stands there.
/// </para>
/// <para>
/// <b>A level is chosen for a reason the index states</b>: the main cell for anything asked about a car or a
/// street, a finer one only where a coarser one would be wrong or slow by a measure the index names.
/// </para>
/// </remarks>
internal readonly struct WorldGrid
{
    /// <summary>How many halvings the grid runs to: an eight-metre cell cut sixteen times is an eighth of a millimetre.</summary>
    public const int MostDepth = 16;

    readonly float _perMainM;

    public WorldGrid(float mainCellM)
    {
        if (!(mainCellM > 0f) || float.IsInfinity(mainCellM))
        {
            throw new ArgumentOutOfRangeException(nameof(mainCellM), mainCellM, "a grid's cell has a width");
        }

        MainCellM = mainCellM;
        _perMainM = 1f / mainCellM;
    }

    public float MainCellM { get; }

    /// <summary>The main cell's own level.</summary>
    public GridLevel Main => Level(0);

    /// <summary>The level <paramref name="depth"/> halvings under the main cell.</summary>
    public GridLevel Level(int depth)
    {
        if ((uint)depth > MostDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, $"the grid runs {MostDepth} halvings deep");
        }

        return new GridLevel(MainCellM, _perMainM, depth);
    }

    /// <summary>The level that cuts a main cell <paramref name="across"/> ways each side — a power of two.</summary>
    public GridLevel Split(int across)
    {
        if (across <= 0 || (across & (across - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(across), across, "a main cell is split by halving, so across is a power of two");
        }

        return Level(System.Numerics.BitOperations.Log2((uint)across));
    }

    /// <summary>
    /// <b>The finest level whose cell is at least <paramref name="reachM"/></b> — so the cells either side of a
    /// point's own hold everything within reach of it. The main cell where the reach is wider, and a caller
    /// then reads as many cells round as <see cref="GridLevel.CellsWithin"/> says.
    /// </summary>
    public GridLevel Covering(float reachM)
    {
        var depth = 0;
        while (depth < MostDepth && MainCellM / (1 << (depth + 1)) >= reachM) depth++;

        return Level(depth);
    }

    /// <summary>
    /// <b>The coarsest level whose cell is no wider than <paramref name="cellM"/></b> — for a raster, whose
    /// resolution is the answer it gives. The main cell where that is wider still.
    /// </summary>
    public GridLevel Within(float cellM)
    {
        var depth = 0;
        while (depth < MostDepth && MainCellM / (1 << depth) > cellM) depth++;

        return Level(depth);
    }
}

/// <summary>
/// <b>One level of the grid</b>: its cell, and the arithmetic every index uses to find a point's cell on it
/// and a cell's place on the ground. There is no other.
/// </summary>
internal readonly struct GridLevel
{
    readonly float _perCellM;

    internal GridLevel(float mainCellM, float perMainM, int depth)
    {
        Depth = depth;
        CellM = mainCellM / (1 << depth);
        _perCellM = perMainM * (1 << depth);
    }

    /// <summary>How many halvings under the main cell this level is.</summary>
    public int Depth { get; }

    public float CellM { get; }

    /// <summary>How many of this level's cells run across one main cell.</summary>
    public int Across => 1 << Depth;

    /// <summary>
    /// The grid this is a level of — for a builder handed one level that has others to choose, since the
    /// main cell is this one's cell doubled back exactly.
    /// </summary>
    public WorldGrid Grid => new(CellM * Across);

    /// <summary>The level one halving coarser — the main cell's own at the top.</summary>
    public GridLevel Coarser => Depth == 0 ? this : new GridLevel(CellM * 2f, _perCellM * 0.5f, Depth - 1);

    /// <summary>Which cell of this level a coordinate falls in, on either axis.</summary>
    public int CellOf(float atM) => (int)MathF.Floor(atM * _perCellM);

    public (int X, int Y) CellOf(Vector2 pointM) => (CellOf(pointM.X), CellOf(pointM.Y));

    /// <summary>Where a cell begins, on either axis.</summary>
    public float EdgeM(int cell) => cell * CellM;

    public float MiddleM(int cell) => (cell + 0.5f) * CellM;

    public Vector2 CornerM(int x, int y) => new(EdgeM(x), EdgeM(y));

    public Vector2 MiddleM(int x, int y) => new(MiddleM(x), MiddleM(y));

    /// <summary>The first cell whose middle stands at or past a coordinate — for a lattice sampled at its cells' middles.</summary>
    public int FirstMiddleFrom(float atM) => (int)MathF.Ceiling((atM * _perCellM) - 0.5f);

    /// <summary>The last cell whose middle stands at or short of a coordinate.</summary>
    public int LastMiddleTo(float atM) => (int)MathF.Floor((atM * _perCellM) - 0.5f);

    /// <summary>How many cells it takes, from a line of the level, to cover a span.</summary>
    public int CellsAcross(float spanM) => (int)MathF.Ceiling(spanM * _perCellM);

    /// <summary>
    /// <b>How many cells round a point's own a neighbourhood has to read</b> to hold everything within
    /// <paramref name="reachM"/> of it — one where the cell is at least the reach.
    /// </summary>
    public int CellsWithin(float reachM) => Math.Max(1, (int)MathF.Ceiling(reachM * _perCellM));

    /// <summary>The cell of a coarser level this one's cell lies in.</summary>
    public int Up(int cell, GridLevel coarser) => cell >> (Depth - coarser.Depth);

    /// <summary>The first cell of a finer level under this one's cell, which holds <c>finer.Across / Across</c> of them each way.</summary>
    public int Down(int cell, GridLevel finer) => cell << (finer.Depth - Depth);
}
