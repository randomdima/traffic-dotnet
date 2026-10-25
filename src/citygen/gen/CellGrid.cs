using System.Numerics;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>A map laid as square cells</b>, row by row from its origin: the frame the partition (<see cref="Partition"/>) and the
/// routing (<see cref="Routes"/>) each walk at a cell size of their own.
/// </summary>
internal readonly record struct CellGrid(float CellM, int Columns, int Rows)
{
    public static CellGrid Over(Vector2 extentM, float cellM) =>
        new(cellM, Math.Max(1, (int)MathF.Ceiling(extentM.X / cellM)), Math.Max(1, (int)MathF.Ceiling(extentM.Y / cellM)));

    public int Count => Columns * Rows;

    public int Index(int column, int row) => (row * Columns) + column;

    public bool Holds(int column, int row) => column >= 0 && row >= 0 && column < Columns && row < Rows;

    public (int Column, int Row) CellOf(Vector2 pointM) =>
        (Math.Clamp((int)(pointM.X / CellM), 0, Columns - 1), Math.Clamp((int)(pointM.Y / CellM), 0, Rows - 1));

    public int IndexOf(Vector2 pointM)
    {
        var (column, row) = CellOf(pointM);
        return Index(column, row);
    }

    public Vector2 CentreOf(int index) => new(((index % Columns) + 0.5f) * CellM, ((index / Columns) + 0.5f) * CellM);

    /// <summary>
    /// <b>Every cell within a distance of a straight</b>, handed to <paramref name="each"/> — how a line is struck onto the
    /// grid as a band.
    /// </summary>
    public void Band(Vector2 fromM, Vector2 toM, float halfM, Action<int> each)
    {
        var (leastM, mostM) = (Vector2.Min(fromM, toM) - new Vector2(halfM), Vector2.Max(fromM, toM) + new Vector2(halfM));
        var (fromColumn, fromRow) = CellOf(leastM);
        var (toColumn, toRow) = CellOf(mostM);
        var runM = toM - fromM;
        var lengthM2 = runM.LengthSquared();
        for (var row = fromRow; row <= toRow; row++)
        {
            for (var column = fromColumn; column <= toColumn; column++)
            {
                var index = Index(column, row);
                var atM = CentreOf(index);
                var share = lengthM2 > 0f ? Math.Clamp(Vector2.Dot(atM - fromM, runM) / lengthM2, 0f, 1f) : 0f;
                if ((fromM + (runM * share) - atM).LengthSquared() <= halfM * halfM) each(index);
            }
        }
    }
}
