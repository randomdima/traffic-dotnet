namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>Numbers filed by the cell they were added in, and read back cell by cell in the order they were added</b> (SIM-8) — for
/// a builder whose search keeps the first of two equal answers, or the last, and so has to meet a cell's numbers in a fixed
/// order. The numbers are 0, 1, 2… as added; what they number is the caller's.
/// </summary>
/// <remarks>
/// A cell is one entry in a <see cref="CellTable"/> and a number a link to the next one added in its cell, so a cell is never
/// a list of its own and adding one allocates nothing past the growth of two lists.
/// </remarks>
internal sealed class CellChains(int capacity = 0)
{
    readonly CellTable _cellOf = new(capacity);
    readonly List<(int First, int Last)> _cells = [];
    readonly List<int> _next = [];

    public int Count => _next.Count;

    /// <summary>The first number added in a cell, or −1 where none was.</summary>
    public int FirstIn(int cellX, int cellY) => _cellOf[GridLevel.KeyOf(cellX, cellY)] is var cell and >= 0 ? _cells[cell].First : -1;

    /// <summary>The next number added in the same cell, or −1 after the last.</summary>
    public int After(int number) => _next[number];

    /// <returns>The number filed, which is how many were added before it.</returns>
    public int Add(int cellX, int cellY)
    {
        var number = _next.Count;
        _next.Add(-1);
        var key = GridLevel.KeyOf(cellX, cellY);
        var cell = _cellOf[key];
        if (cell < 0)
        {
            _cellOf.Set(key, _cells.Count);
            _cells.Add((number, number));
            return number;
        }

        _next[_cells[cell].Last] = number;
        _cells[cell] = (_cells[cell].First, number);
        return number;
    }
}
