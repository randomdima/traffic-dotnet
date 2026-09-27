using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>A block of one level's cells, both corners inclusive.</summary>
internal readonly record struct CellRange(int FromX, int FromY, int ToX, int ToY);

/// <summary>
/// <b>The rectangle of one level's cells an index keeps</b> (SIM-8): numbered on the grid, stored from its own
/// corner, so a cell of it is the same square of ground as the cell of any other index with those numbers.
/// </summary>
/// <remarks>
/// <b>What stands outside is held in the rim</b> — the nearest cell the window has — so a place off the edge
/// is answered rather than refused, and an index laid over the whole town answers for a car that has left it.
/// </remarks>
internal readonly struct GridWindow
{
    GridWindow(GridLevel level, int fromX, int fromY, int width, int height)
    {
        Level = level;
        FromX = fromX;
        FromY = fromY;
        Width = width;
        Height = height;
    }

    /// <summary>The cells of a level that a box of ground reaches, grown by <paramref name="marginCells"/> each side.</summary>
    public static GridWindow Over(GridLevel level, Vector2 leastM, Vector2 mostM, int marginCells = 0)
    {
        var (fromX, fromY) = level.CellOf(leastM);
        var (toX, toY) = level.CellOf(mostM);
        return Of(
            level, fromX - marginCells, fromY - marginCells, toX - fromX + 1 + (2 * marginCells),
            toY - fromY + 1 + (2 * marginCells));
    }

    public static GridWindow Of(GridLevel level, int fromX, int fromY, int width, int height) =>
        new(level, fromX, fromY, Math.Max(0, width), Math.Max(0, height));

    public GridLevel Level { get; }

    public int FromX { get; }

    public int FromY { get; }

    public int Width { get; }

    public int Height { get; }

    public int ToX => FromX + Width - 1;

    public int ToY => FromY + Height - 1;

    public int Count => Width * Height;

    public bool IsEmpty => Width == 0 || Height == 0;

    /// <summary>The window's own corner on the ground, and the far one.</summary>
    public Vector2 LeastM => Level.CornerM(FromX, FromY);

    public Vector2 MostM => Level.CornerM(FromX + Width, FromY + Height);

    public bool Holds(int x, int y) => (uint)(x - FromX) < (uint)Width && (uint)(y - FromY) < (uint)Height;

    /// <summary>Where a cell the window holds is stored.</summary>
    public int IndexOf(int x, int y) => ((y - FromY) * Width) + (x - FromX);

    public int ClampX(int x) => Math.Clamp(x, FromX, ToX);

    public int ClampY(int y) => Math.Clamp(y, FromY, ToY);

    /// <summary>Where the cell a point is held in is stored — its own, or the rim's nearest.</summary>
    public int IndexAt(Vector2 pointM) => IndexOf(ClampX(Level.CellOf(pointM.X)), ClampY(Level.CellOf(pointM.Y)));

    /// <summary>
    /// <b>The cells a box of ground could have anything in</b>, held to the window: a box off the edge reads
    /// the rim, since that is where anything off the edge was put. False only where the window is empty.
    /// </summary>
    public bool TryRange(Vector2 leastM, Vector2 mostM, out CellRange range)
    {
        if (IsEmpty)
        {
            range = default;
            return false;
        }

        range = new CellRange(
            ClampX(Level.CellOf(leastM.X)), ClampY(Level.CellOf(leastM.Y)), ClampX(Level.CellOf(mostM.X)),
            ClampY(Level.CellOf(mostM.Y)));
        return true;
    }

    /// <summary>
    /// <b>The cells of the window a box of ground overlaps</b>, and false where it overlaps none — for an index
    /// whose window holds everything it filed, so its rim stands for nothing off the edge.
    /// </summary>
    public bool TryOverlap(Vector2 leastM, Vector2 mostM, out CellRange range)
    {
        var (fromX, fromY) = Level.CellOf(leastM);
        var (toX, toY) = Level.CellOf(mostM);
        if (IsEmpty || toX < FromX || toY < FromY || fromX > ToX || fromY > ToY)
        {
            range = default;
            return false;
        }

        range = new CellRange(ClampX(fromX), ClampY(fromY), ClampX(toX), ClampY(toY));
        return true;
    }
}
