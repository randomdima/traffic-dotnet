using System.Numerics;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// The cell grid while a town is being laid: the one place a generated map writes ground that is not a
/// road's own shape, and the one place a later stage asks whether a piece of ground is still free.
/// </summary>
/// <remarks>
/// <b>It is the sibling of <see cref="GroundPainter"/> and not a second copy of it.</b> The painter lays
/// the shapes a plan is made of — the carriageway, the mouth, the fillet, the paint — walking them by arc
/// length. This lays the two things a generator needs that no road shape can give: the ground everything
/// starts as, the water cut out of it, and the question <em>is this rectangle still untouched</em>, which is
/// what makes a building slot legal by construction rather than by a retry.
/// </remarks>
internal readonly struct GenRaster(Ground[] cells, int gridWidth, int gridHeight, float cellSizeM)
{
    public int Width => gridWidth;

    public int Height => gridHeight;

    public float CellSizeM => cellSizeM;

    public void Fill(Ground ground) => Array.Fill(cells, ground);

    /// <summary>
    /// One closed outline filled in, by the rows it crosses rather than by the cells it contains: a town's
    /// water is a fifth of its ground, and asking every cell whether it is inside the polygon is the one
    /// whole-grid sweep a generated town cannot afford twice.
    /// </summary>
    public void FillOutline(ReadOnlySpan<Vector2> outlineM, Ground ground)
    {
        if (outlineM.Length < 3) return;

        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        foreach (var point in outlineM)
        {
            minY = MathF.Min(minY, point.Y);
            maxY = MathF.Max(maxY, point.Y);
        }

        var firstRow = Math.Max(0, (int)MathF.Floor(minY / cellSizeM));
        var lastRow = Math.Min(gridHeight - 1, (int)MathF.Ceiling(maxY / cellSizeM));
        Span<float> crossings = stackalloc float[64];
        var spilled = outlineM.Length > crossings.Length ? new float[outlineM.Length] : null;

        for (var row = firstRow; row <= lastRow; row++)
        {
            var y = (row + 0.5f) * cellSizeM;
            var edges = spilled is null ? crossings : spilled.AsSpan();
            var found = CrossingsAt(outlineM, y, edges);
            if (found < 2) continue;

            var line = edges[..found];
            line.Sort();
            for (var pair = 0; pair + 1 < found; pair += 2)
            {
                var from = Math.Max(0, (int)MathF.Floor(line[pair] / cellSizeM));
                var to = Math.Min(gridWidth - 1, (int)MathF.Floor(line[pair + 1] / cellSizeM));
                for (var column = from; column <= to; column++) cells[(row * gridWidth) + column] = ground;
            }
        }
    }

    /// <summary>Where one scanline crosses the outline, as x in metres — the half-open rule, so a vertex is counted once.</summary>
    static int CrossingsAt(ReadOnlySpan<Vector2> outlineM, float y, Span<float> into)
    {
        var found = 0;
        for (var edge = 0; edge < outlineM.Length && found < into.Length; edge++)
        {
            var a = outlineM[edge];
            var b = outlineM[(edge + 1) % outlineM.Length];
            if (a.Y > b.Y) (a, b) = (b, a);
            if (y < a.Y || y >= b.Y || a.Y == b.Y) continue;

            into[found++] = a.X + ((y - a.Y) / (b.Y - a.Y) * (b.X - a.X));
        }

        return found;
    }

    public Ground At(Vector2 pointM)
    {
        var cell = CellAt(pointM);
        return cell < 0 ? Ground.Water : cells[cell];
    }

    /// <summary>
    /// Whether every cell under a rectangle standing on a bearing is the ground given — the question a slot
    /// asks before it is cut, and the reason nothing has to be laid and taken back again. <b>Off the grid
    /// counts as taken</b>, so a slot half over the edge of the world is one that is never cut.
    /// </summary>
    public bool IsAll(Vector2 centreM, Vector2 axis, Vector2 halfExtentM, Ground ground) =>
        IsAll(centreM, axis, halfExtentM, ground, ground);

    /// <summary>
    /// Whether every cell under a disc is the ground given — what a prop asks of the ground its own girth
    /// stands on. <b>Off the grid counts as taken</b>, so nothing is stood half over the edge of the world.
    /// </summary>
    public bool IsAll(Vector2 centreM, float radiusM, Ground ground)
    {
        var stepM = cellSizeM * 0.5f;
        var steps = (int)MathF.Ceiling(radiusM * 2f / stepM);
        for (var down = 0; down <= steps; down++)
        {
            var alongM = MathF.Min(-radiusM + (down * stepM), radiusM);
            var acrossM = MathF.Sqrt(MathF.Max(0f, (radiusM * radiusM) - (alongM * alongM)));
            var across = (int)MathF.Ceiling(acrossM * 2f / stepM);
            for (var over = 0; over <= across; over++)
            {
                var cell = CellAt(
                    centreM + new Vector2(alongM, MathF.Min(-acrossM + (over * stepM), acrossM)));
                if (cell < 0 || cells[cell] != ground) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Which grounds appear anywhere under a disc, one bit a kind — what a prop asks to know what it is
    /// standing beside (GEN-6b). <b>Off the grid contributes nothing</b>: the void past the edge of the
    /// world is not something a prop could be laid along.
    /// </summary>
    public int GroundsUnder(Vector2 centreM, float radiusM)
    {
        var firstRow = Math.Max(0, (int)MathF.Floor((centreM.Y - radiusM) / cellSizeM));
        var lastRow = Math.Min(gridHeight - 1, (int)MathF.Floor((centreM.Y + radiusM) / cellSizeM));

        // Row by row, over the span the circle cuts out of each rather than over its bounding box: this is
        // asked of every candidate a town's whole grass surface affords, which is a hundred thousand discs.
        var found = 0;
        for (var row = firstRow; row <= lastRow; row++)
        {
            var downM = (((row + 0.5f) * cellSizeM) - centreM.Y) / radiusM;
            var acrossM = radiusM * MathF.Sqrt(MathF.Max(0f, 1f - (downM * downM)));
            var firstColumn = Math.Max(0, (int)MathF.Floor((centreM.X - acrossM) / cellSizeM));
            var lastColumn = Math.Min(gridWidth - 1, (int)MathF.Floor((centreM.X + acrossM) / cellSizeM));

            var span = cells.AsSpan((row * gridWidth) + firstColumn, Math.Max(0, lastColumn - firstColumn + 1));
            foreach (var ground in span) found |= 1 << (int)ground;
        }

        return found;
    }

    /// <summary>The same of either of two grounds — a car park reaches back over the pavement it fronts.</summary>
    public bool IsAll(Vector2 centreM, Vector2 axis, Vector2 halfExtentM, Ground ground, Ground orGround)
    {
        var side = new Vector2(-axis.Y, axis.X);
        var stepM = cellSizeM * 0.5f;
        for (var alongM = -halfExtentM.X; alongM <= halfExtentM.X; alongM += stepM)
        {
            for (var acrossM = -halfExtentM.Y; acrossM <= halfExtentM.Y; acrossM += stepM)
            {
                var cell = CellAt(centreM + (axis * alongM) + (side * acrossM));
                if (cell < 0 || (cells[cell] != ground && cells[cell] != orGround)) return false;
            }
        }

        return true;
    }

    public int CellAt(Vector2 pointM)
    {
        var x = (int)MathF.Floor(pointM.X / cellSizeM);
        var y = (int)MathF.Floor(pointM.Y / cellSizeM);
        return x < 0 || y < 0 || x >= gridWidth || y >= gridHeight ? -1 : (y * gridWidth) + x;
    }

    public Vector2 CentreOf(int cell) =>
        new(((cell % gridWidth) + 0.5f) * cellSizeM, ((cell / gridWidth) + 0.5f) * cellSizeM);
}
