using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>
/// One rectangle a footprint is cut into: its middle, the bearing of its <c>x</c>, its sides along and across that,
/// and the radius its corners are rounded at — nought for one cut out of a larger outline.
/// </summary>
internal readonly record struct FootprintPart(Vector2 CentreM, float HeadingRad, Vector2 SizeM, float CornerM)
{
    /// <summary>How round it is: its corners' radius as a share of half its short side, nought square and one a stadium or a circle.</summary>
    public float CornerShare => CornerM / (MathF.Min(SizeM.X, SizeM.Y) * 0.5f);
}

/// <summary>
/// <b>A traced footprint as the few rectangles it is built of</b> (GEN-57): an outline of any shape, less its
/// courtyards, cut into at most <see cref="CityGenFigures.TracedPartsMost"/> rectangles on its own bearing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bearing is the one its walls keep</b>: each edge's direction folded onto a quarter turn and weighed by its
/// length, so an L, a U or a courtyard block reads square to its own walls and a mapper's slanted corner is outvoted.
/// </para>
/// <para>
/// <b>A footprint filling its rounded box is that box</b> (<see cref="CityGenFigures.TracedRectangularShare"/>), which
/// is most of a city. Its corners are read off how far the outline stands in from each corner of its box — an arc of
/// radius <c>r</c> stands <c>r(√2 − 1)</c> in — and the middle two of the four taken, so a silo is a circle, a
/// pavilion a stadium and a block with one corner cut at the junction still square. The rest is read into a grid of
/// cells on its bearing and cut greedily: the largest rectangle of
/// cells still uncovered, grown out across any edge whose next row is mostly inside, until the rectangles cover
/// <see cref="CityGenFigures.TracedPartsCoverShare"/> of it or the next would be a sliver. Largest first is what reads
/// a courtyard block as its two long wings and then the two between them.
/// </para>
/// </remarks>
internal sealed class FootprintParts
{
    /// <summary>A grid of a footprint's cells, kept between footprints so a city is cut without allocating one a building.</summary>
    bool[] _inside = [];
    bool[] _taken = [];
    int[] _heights = [];
    int[] _stack = [];
    readonly List<float> _crossings = [];

    public static float BearingRad(ReadOnlySpan<Vector2> outline)
    {
        var (cos, sin) = (0f, 0f);
        for (var at = 0; at < outline.Length; at++)
        {
            var edge = outline[(at + 1) % outline.Length] - outline[at];
            var lengthM = edge.Length();
            if (lengthM <= 0f) continue;

            var folded = 4f * MathF.Atan2(edge.Y, edge.X);
            cos += lengthM * MathF.Cos(folded);
            sin += lengthM * MathF.Sin(folded);
        }

        return MathF.Atan2(sin, cos) * 0.25f;
    }

    /// <summary>The rectangles footprint <paramref name="footprint"/> is cut into, added to <paramref name="into"/>.</summary>
    public void Cut(CityPlan.FootprintArrays footprints, int footprint, CityGenFigures figures, List<FootprintPart> into)
    {
        var (first, past) = (footprints.RingOffsets[footprint], footprints.RingOffsets[footprint + 1]);
        var outline = footprints.Rings.RingOf(first);
        if (outline.Length < 3) return;

        var headingRad = BearingRad(outline);
        var along = Heading.Unit(headingRad);
        var across = Heading.RightOf(along);

        var (leastA, mostA, leastB, mostB) = (float.MaxValue, float.MinValue, float.MaxValue, float.MinValue);
        foreach (var pointM in outline)
        {
            var (a, b) = (Vector2.Dot(pointM, along), Vector2.Dot(pointM, across));
            (leastA, mostA, leastB, mostB) = (MathF.Min(leastA, a), MathF.Max(mostA, a), MathF.Min(leastB, b), MathF.Max(mostB, b));
        }

        var areaM2 = 0f;
        for (var ring = first; ring < past; ring++) areaM2 += ring == first ? MathF.Abs(Area(footprints.Rings.RingOf(ring))) : -MathF.Abs(Area(footprints.Rings.RingOf(ring)));

        var boxM2 = (mostA - leastA) * (mostB - leastB);
        if (areaM2 < figures.TracedFootprintSmallestM2 || boxM2 <= 0f) return;

        var cornerM = CornerM(outline, leastA, mostA, leastB, mostB, along, across);
        if (areaM2 >= (boxM2 - ((4f - MathF.PI) * cornerM * cornerM)) * figures.TracedRectangularShare)
        {
            // A wall's width and no room behind it is an awning or a fence a mapper drew as a building.
            if (MathF.Min(mostA - leastA, mostB - leastB) < figures.TracedPartNarrowestM) return;

            into.Add(Part(leastA, mostA, leastB, mostB, along, across, headingRad) with { CornerM = cornerM });
            return;
        }

        var cellM = MathF.Max(figures.TracedFootprintCellLeastM, MathF.Max(mostA - leastA, mostB - leastB) / figures.TracedFootprintCells);
        var columns = Math.Max(1, (int)MathF.Ceiling((mostA - leastA) / cellM));
        var rows = Math.Max(1, (int)MathF.Ceiling((mostB - leastB) / cellM));
        var inside = Fill(footprints, first, past, along, across, leastA, leastB, cellM, columns, rows);
        if (inside == 0) return;

        var narrowest = Math.Max(1, (int)MathF.Ceiling(figures.TracedPartNarrowestM / cellM));
        var smallest = Math.Max(narrowest * narrowest, (int)(inside * figures.TracedPartSmallestShare));
        var covered = 0;
        var cut = 0;
        while (cut < figures.TracedPartsMost && covered < inside * figures.TracedPartsCoverShare)
        {
            if (Largest(columns, rows, narrowest) is not var (column, row, width, height) || width * height < smallest) break;

            Grow(columns, rows, ref column, ref row, ref width, ref height);
            for (var y = row; y < row + height; y++)
            {
                for (var x = column; x < column + width; x++)
                {
                    var cell = (y * columns) + x;
                    if (_inside[cell] && !_taken[cell]) covered++;
                    _taken[cell] = true;
                }
            }

            into.Add(Part(
                leastA + (column * cellM), leastA + ((column + width) * cellM),
                leastB + (row * cellM), leastB + ((row + height) * cellM), along, across, headingRad));
            cut++;
        }

        // An outline no rectangle of a wall's width fits in — a ring of sheds, a sliver — is its box.
        if (cut == 0) into.Add(Part(leastA, mostA, leastB, mostB, along, across, headingRad));
    }

    static FootprintPart Part(float leastA, float mostA, float leastB, float mostB, Vector2 along, Vector2 across, float headingRad) =>
        new((along * ((leastA + mostA) * 0.5f)) + (across * ((leastB + mostB) * 0.5f)), headingRad, new Vector2(mostA - leastA, mostB - leastB), 0f);

    /// <summary>The radius the outline rounds its box's corners at: the middle two of the four corners' readings, no more than half its short side.</summary>
    static float CornerM(ReadOnlySpan<Vector2> outline, float leastA, float mostA, float leastB, float mostB, Vector2 along, Vector2 across)
    {
        Span<float> radiiM = stackalloc float[4];
        var corner = 0;
        foreach (var a in (ReadOnlySpan<float>)[leastA, mostA])
        {
            foreach (var b in (ReadOnlySpan<float>)[leastB, mostB])
            {
                var cornerM = (along * a) + (across * b);
                var inM = float.MaxValue;
                for (var at = 0; at < outline.Length; at++) inM = MathF.Min(inM, OffTheEdgeM(outline[at], outline[(at + 1) % outline.Length], cornerM));

                radiiM[corner++] = inM / (MathF.Sqrt(2f) - 1f);
            }
        }

        radiiM.Sort();
        return MathF.Min((radiiM[1] + radiiM[2]) * 0.5f, MathF.Min(mostA - leastA, mostB - leastB) * 0.5f);
    }

    static float OffTheEdgeM(Vector2 fromM, Vector2 toM, Vector2 pointM)
    {
        var runM = toM - fromM;
        var lengthSq = runM.LengthSquared();
        var alongShare = lengthSq > 0f ? Math.Clamp(Vector2.Dot(pointM - fromM, runM) / lengthSq, 0f, 1f) : 0f;
        return Vector2.Distance(pointM, fromM + (runM * alongShare));
    }

    /// <summary>Every cell whose middle the footprint holds, by the even-odd rule over all its rings, and how many.</summary>
    int Fill(
        CityPlan.FootprintArrays footprints, int first, int past, Vector2 along, Vector2 across, float leastA, float leastB,
        float cellM, int columns, int rows)
    {
        var cells = columns * rows;
        if (_inside.Length < cells)
        {
            _inside = new bool[cells];
            _taken = new bool[cells];
        }

        Array.Clear(_inside, 0, cells);
        Array.Clear(_taken, 0, cells);
        var inside = 0;
        for (var row = 0; row < rows; row++)
        {
            var b = leastB + ((row + 0.5f) * cellM);
            _crossings.Clear();
            for (var ring = first; ring < past; ring++)
            {
                var points = footprints.Rings.RingOf(ring);
                for (var at = 0; at < points.Length; at++)
                {
                    var (p, q) = (points[at], points[(at + 1) % points.Length]);
                    var (pb, qb) = (Vector2.Dot(p, across), Vector2.Dot(q, across));
                    if ((pb > b) == (qb > b)) continue;

                    var (pa, qa) = (Vector2.Dot(p, along), Vector2.Dot(q, along));
                    _crossings.Add(pa + ((b - pb) / (qb - pb) * (qa - pa)));
                }
            }

            _crossings.Sort();
            for (var pair = 0; pair + 1 < _crossings.Count; pair += 2)
            {
                var from = Math.Max(0, (int)MathF.Ceiling(((_crossings[pair] - leastA) / cellM) - 0.5f));
                var to = Math.Min(columns - 1, (int)MathF.Floor(((_crossings[pair + 1] - leastA) / cellM) - 0.5f));
                for (var column = from; column <= to; column++)
                {
                    _inside[(row * columns) + column] = true;
                    inside++;
                }
            }
        }

        return inside;
    }

    /// <summary>
    /// The largest rectangle of cells inside and not yet taken, at least <paramref name="narrowest"/> cells each way —
    /// row by row over the run of free cells above each column, each run's widest rectangle read off a stack.
    /// </summary>
    (int Column, int Row, int Width, int Height)? Largest(int columns, int rows, int narrowest)
    {
        if (_heights.Length < columns + 1)
        {
            _heights = new int[columns + 1];
            _stack = new int[columns + 1];
        }

        Array.Clear(_heights, 0, columns + 1);
        (int Column, int Row, int Width, int Height)? best = null;
        var bestCells = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var cell = (row * columns) + column;
                _heights[column] = _inside[cell] && !_taken[cell] ? _heights[column] + 1 : 0;
            }

            var top = 0;
            for (var column = 0; column <= columns; column++)
            {
                // A run shorter than a wall's width cannot stand in any rectangle, so it closes the ones open over it.
                var height = column < columns && _heights[column] >= narrowest ? _heights[column] : 0;
                while (top > 0 && Run(_stack[top - 1]) >= height)
                {
                    var tall = Run(_stack[--top]);
                    var left = top > 0 ? _stack[top - 1] + 1 : 0;
                    var width = column - left;
                    if (tall >= narrowest && width >= narrowest && tall * width > bestCells)
                    {
                        bestCells = tall * width;
                        best = (left, row - tall + 1, width, tall);
                    }
                }

                _stack[top++] = column;
            }
        }

        return best;

        int Run(int column) => column < columns && _heights[column] >= narrowest ? _heights[column] : 0;
    }

    /// <summary>
    /// A rectangle grown a row at a time across any edge whose next row is mostly cells inside and not yet taken, so
    /// its walls stand where the outline's do rather than a cell inside them.
    /// </summary>
    void Grow(int columns, int rows, ref int column, ref int row, ref int width, ref int height)
    {
        for (var grown = true; grown;)
        {
            grown = false;
            if (column > 0 && Mostly(column - 1, row, 1, height)) (column, width, grown) = (column - 1, width + 1, true);
            if (column + width < columns && Mostly(column + width, row, 1, height)) (width, grown) = (width + 1, true);
            if (row > 0 && Mostly(column, row - 1, width, 1)) (row, height, grown) = (row - 1, height + 1, true);
            if (row + height < rows && Mostly(column, row + height, width, 1)) (height, grown) = (height + 1, true);
        }

        bool Mostly(int x0, int y0, int w, int h)
        {
            var free = 0;
            for (var y = y0; y < y0 + h; y++)
            {
                for (var x = x0; x < x0 + w; x++)
                {
                    var cell = (y * columns) + x;
                    if (_taken[cell]) return false;
                    if (_inside[cell]) free++;
                }
            }

            return free * 2 > w * h;
        }
    }

    static float Area(ReadOnlySpan<Vector2> ring)
    {
        var twice = 0f;
        for (var at = 0; at < ring.Length; at++)
        {
            var (a, b) = (ring[at], ring[(at + 1) % ring.Length]);
            twice += (a.X * b.Y) - (b.X * a.Y);
        }

        return twice * 0.5f;
    }
}
