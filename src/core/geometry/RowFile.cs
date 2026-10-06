using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>A set laid once, filed by the row of the grid each of its points is in</b> (SIM-8), so what a box of ground
/// can hold is one run of items a row, read whole and culled across by the reader.
/// </summary>
/// <remarks>
/// A row and not a cell, because a window of rows costs a word a row however wide the town is, and a box a
/// frame asks about is a handful of rows: what a row holds past the box's sides is the price of that.
/// </remarks>
internal sealed class RowFile
{
    readonly GridLevel _level;
    readonly int _fromRow;
    readonly int[] _rowStarts;
    readonly int[] _items;

    RowFile(GridLevel level, int fromRow, int[] rowStarts, int[] items)
    {
        _level = level;
        _fromRow = fromRow;
        _rowStarts = rowStarts;
        _items = items;
    }

    public int RowCount => _rowStarts.Length - 1;

    /// <summary>The items of <paramref name="pointsM"/>, by their index in it, filed by the row each point is in.</summary>
    public static RowFile Of(ReadOnlySpan<Vector2> pointsM, GridLevel level)
    {
        if (pointsM.IsEmpty) return new RowFile(level, 0, [0], []);

        var fromRow = int.MaxValue;
        var toRow = int.MinValue;
        foreach (var pointM in pointsM)
        {
            var row = level.CellOf(pointM.Y);
            fromRow = Math.Min(fromRow, row);
            toRow = Math.Max(toRow, row);
        }

        var rowStarts = new int[toRow - fromRow + 2];
        foreach (var pointM in pointsM) rowStarts[level.CellOf(pointM.Y) - fromRow + 1]++;
        for (var row = 1; row < rowStarts.Length; row++) rowStarts[row] += rowStarts[row - 1];

        var items = new int[pointsM.Length];
        var next = rowStarts[..^1];
        for (var item = 0; item < pointsM.Length; item++) items[next[level.CellOf(pointsM[item].Y) - fromRow]++] = item;

        return new RowFile(level, fromRow, rowStarts, items);
    }

    /// <summary>The rows a band of the map from <paramref name="leastYM"/> to <paramref name="mostYM"/> reaches, and false where it misses every one filed.</summary>
    public bool TryRows(float leastYM, float mostYM, out int fromRow, out int toRow)
    {
        fromRow = Math.Max(_level.CellOf(leastYM) - _fromRow, 0);
        toRow = Math.Min(_level.CellOf(mostYM) - _fromRow, RowCount - 1);
        return fromRow <= toRow;
    }

    /// <summary>What one row holds, numbered as <see cref="TryRows"/> numbers it.</summary>
    public ReadOnlySpan<int> Row(int row) => _items.AsSpan(_rowStarts[row], _rowStarts[row + 1] - _rowStarts[row]);
}
