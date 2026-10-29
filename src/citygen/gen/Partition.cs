using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>What keeps a place out of every district (GEN-64), which is also what two districts are parted by.</summary>
internal enum Barrier : byte
{
    None,

    /// <summary>The water — a river or the sea — its shore and the strip every road keeps off it (GEN-71).</summary>
    Water,

    /// <summary>A railway, its ballast and the clearance a junction keeps off it.</summary>
    Rail,

    /// <summary>An intercity road and the ground a street keeps off it (GEN-68).</summary>
    Road,

    /// <summary>The margin round the map's edge.</summary>
    Edge,
}

/// <summary>
/// <b>The land cut into districts</b> (GEN-64…66): the dry ground the barriers leave — the water and its shore, each
/// intercity road's corridor, each railway's — fallen into regions no barrier crosses, and each region cut into as many
/// districts as keep every one under the largest a district gets.
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid on square cells</b> (<see cref="CityGenFigures.DistrictCellM"/>), because a region's edge is whatever shape a
/// bank or a corridor makes and no shape is assumed of it: a cell is barrier or land, a region is the land a cell reaches
/// without crossing a barrier, and a district is cells of one region. <b>So no district spans a barrier by
/// construction</b>, which is the rule (GEN-64).
/// </para>
/// <para>
/// <b>A region is cut by its districts' middles</b>: each cell goes to the nearest, the middles move to the middle of what
/// they took, and again — so the districts come out compact and near one size. <b>Nearness is measured on a bent
/// field</b> (<see cref="CityGenFigures.DistrictBendShare"/>), so the edge between two districts is a smooth curve and not
/// the straight a plain nearest-middle cut makes (GEN-66). A district left in pieces by its region's shape keeps its
/// largest, and the rest go to the district they lie against.
/// </para>
/// </remarks>
internal sealed class Partition
{
    /// <summary>A cell no district holds: a barrier's, or a region too small to be one.</summary>
    public const short None = -1;


    Partition(CellGrid grid, short[] label, Barrier[] barrier, int count)
    {
        Grid = grid;
        Label = label;
        Barrier = barrier;
        Count = count;
        CentreM = new Vector2[count];
        AreaM2 = new float[count];
        var cells = new int[count];
        for (var cell = 0; cell < label.Length; cell++)
        {
            if (label[cell] < 0) continue;

            CentreM[label[cell]] += grid.CentreOf(cell);
            cells[label[cell]]++;
        }

        for (var district = 0; district < count; district++)
        {
            CentreM[district] /= Math.Max(1, cells[district]);
            AreaM2[district] = cells[district] * grid.CellM * grid.CellM;
        }

        ReachM = new float[count];
        var halfDiagonalM = grid.CellM * MathF.Sqrt(0.5f);
        for (var cell = 0; cell < label.Length; cell++)
        {
            if (label[cell] < 0) continue;

            var offM = Vector2.Distance(grid.CentreOf(cell), CentreM[label[cell]]) + halfDiagonalM;
            ReachM[label[cell]] = MathF.Max(ReachM[label[cell]], offM);
        }
    }

    public CellGrid Grid { get; }

    /// <summary>The district each cell is, or <see cref="None"/>.</summary>
    public short[] Label { get; }

    /// <summary>Which barrier each cell is, if any.</summary>
    public Barrier[] Barrier { get; }

    public int Count { get; }

    /// <summary>The middle of the ground each district holds.</summary>
    public Vector2[] CentreM { get; }

    public float[] AreaM2 { get; }

    /// <summary>How far from its middle the furthest of each district's ground reaches, whatever its shape.</summary>
    public float[] ReachM { get; }

    /// <summary>The district a point stands in, or <see cref="None"/> — off the map too.</summary>
    public int At(Vector2 pointM) =>
        pointM.X < 0f || pointM.Y < 0f || pointM.X >= Grid.Columns * Grid.CellM || pointM.Y >= Grid.Rows * Grid.CellM
            ? None
            : Label[Grid.IndexOf(pointM)];

    /// <summary>
    /// <b>How much of each district's edge is a barrier of these kinds</b>, as a share of its edge cells: what a district
    /// borders (GEN-74).
    /// </summary>
    public float[] EdgeShares(Func<Barrier, bool> kinds)
    {
        var (edge, against) = (new int[Count], new int[Count]);
        for (var cell = 0; cell < Label.Length; cell++)
        {
            var district = Label[cell];
            if (district < 0) continue;

            var (column, row) = (cell % Grid.Columns, cell / Grid.Columns);
            var (onTheEdge, onAKind) = (false, false);
            foreach (var (dx, dy) in Four)
            {
                if (!Grid.Holds(column + dx, row + dy)) continue;

                var next = Grid.Index(column + dx, row + dy);
                if (Label[next] == district) continue;

                onTheEdge = true;
                onAKind |= kinds(Barrier[next]);
            }

            edge[district] += onTheEdge ? 1 : 0;
            against[district] += onAKind ? 1 : 0;
        }

        var share = new float[Count];
        for (var district = 0; district < Count; district++) share[district] = edge[district] == 0 ? 0f : against[district] / (float)edge[district];

        return share;
    }

    /// <summary>
    /// <b>The same cut with only the districts kept</b> (GEN-76): every other district's ground is open land, as a region too
    /// small to be one is, and the kept are numbered again from nought.
    /// </summary>
    public Partition Only(bool[] kept)
    {
        var label = (short[])Label.Clone();
        for (var cell = 0; cell < label.Length; cell++)
        {
            if (label[cell] >= 0 && !kept[label[cell]]) label[cell] = None;
        }

        return new Partition(Grid, label, Barrier, Renumbered(label, Count));
    }

    /// <param name="barrier">Which barrier each cell of the grid is, if any (<see cref="Gen.Barrier.None"/>).</param>
    public static Partition Cut(CellGrid grid, Barrier[] barrier, SimConfig config, ulong seed)
    {
        var figures = config.CityGen;
        var blocked = new bool[grid.Count];
        for (var cell = 0; cell < grid.Count; cell++) blocked[cell] = barrier[cell] != Gen.Barrier.None;

        var regions = Regions(grid, blocked);
        var label = new short[grid.Count];
        Array.Fill(label, None);

        var draw = new Rng(seed, Streams.District);
        var bend = new Bend(ref draw, MathF.Sqrt(figures.DistrictLargestM2) * figures.DistrictBendShare, MathF.Sqrt(figures.DistrictLargestM2));
        var count = 0;
        var cellM2 = grid.CellM * grid.CellM;
        for (var at = 0; at < regions.Count; at++)
        {
            var cells = regions[at];
            var areaM2 = cells.Count * cellM2;
            if (areaM2 < figures.DistrictLeastM2) continue;

            // <b>A stream a region, keyed on where it starts</b> (GEN-11): a recut in one region moves no other's middles.
            var regionDraw = new Rng(seed, Streams.Keyed(Streams.District ^ RegionStream, (ulong)cells[0]));
            var districts = (int)MathF.Ceiling(areaM2 / figures.DistrictLargestM2);
            // Cut into one more each time its largest is too big, and the cut whose largest is least kept where none is under.
            var (cut, bestM2) = (Array.Empty<short>(), float.PositiveInfinity);
            for (var recut = 0; recut <= figures.DistrictRecuts; recut++)
            {
                var tried = CutRegion(grid, cells, districts + recut, bend, figures.DistrictSettles, ref regionDraw);
                var largestM2 = LargestM2(tried, districts + recut, cellM2);
                if (largestM2 < bestM2) (cut, bestM2) = (tried, largestM2);
                if (largestM2 <= figures.DistrictLargestM2) break;
            }

            if (bestM2 > figures.DistrictLargestM2) cut = Halved(grid, cells, cut, bend, figures, ref regionDraw);

            var made = cut.Length == 0 ? 0 : cut.Max() + 1;
            for (var cell = 0; cell < cells.Count; cell++) label[cells[cell]] = (short)(count + cut[cell]);

            count += made;
        }

        Whole(grid, label, count, figures.DistrictLargestM2);
        NoneTooSmall(grid, label, count, figures);
        return new Partition(grid, label, barrier, Renumbered(label, count));
    }

    /// <summary>The stream a region's cut is drawn on, keyed on the region's first cell as well.</summary>
    const ulong RegionStream = 0x7265_6769_6F6E_0000;

    /// <summary>Every piece of land no barrier crosses, each the cells it is, four ways joined.</summary>
    static List<List<int>> Regions(CellGrid grid, bool[] blocked)
    {
        var region = new int[grid.Count];
        Array.Fill(region, -1);
        var regions = new List<List<int>>();
        var queue = new Queue<int>();
        for (var start = 0; start < grid.Count; start++)
        {
            if (blocked[start] || region[start] >= 0) continue;

            var cells = new List<int>();
            region[start] = regions.Count;
            queue.Enqueue(start);
            while (queue.TryDequeue(out var cell))
            {
                cells.Add(cell);
                var (column, row) = (cell % grid.Columns, cell / grid.Columns);
                foreach (var (dx, dy) in Four)
                {
                    if (!grid.Holds(column + dx, row + dy)) continue;

                    var next = grid.Index(column + dx, row + dy);
                    if (blocked[next] || region[next] >= 0) continue;

                    region[next] = regions.Count;
                    queue.Enqueue(next);
                }
            }

            regions.Add(cells);
        }

        return regions;
    }

    static readonly (int, int)[] Four = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>
    /// <b>One region cut into districts</b>, as each of its cells' district: middles spread over it as far from each other
    /// as they can stand, then settled (<see cref="CityGenFigures.DistrictSettles"/>) — each cell to the nearest middle on the bent field, each
    /// middle to the middle of what it took.
    /// </summary>
    static short[] CutRegion(CellGrid grid, List<int> cells, int districts, Bend bend, int settles, ref Rng draw)
    {
        var bentM = new Vector2[cells.Count];
        for (var cell = 0; cell < cells.Count; cell++) bentM[cell] = bend.Of(grid.CentreOf(cells[cell]));

        // The first middle at a drawn cell, and each after it at the cell furthest from every middle already placed.
        var middleM = new Vector2[districts];
        middleM[0] = bentM[draw.NextInt(cells.Count)];
        var nearestM2 = new float[cells.Count];
        Array.Fill(nearestM2, float.PositiveInfinity);
        for (var placed = 1; placed < districts; placed++)
        {
            var (furthest, furthestM2) = (0, -1f);
            for (var cell = 0; cell < cells.Count; cell++)
            {
                nearestM2[cell] = MathF.Min(nearestM2[cell], Vector2.DistanceSquared(bentM[cell], middleM[placed - 1]));
                if (nearestM2[cell] > furthestM2) (furthest, furthestM2) = (cell, nearestM2[cell]);
            }

            middleM[placed] = bentM[furthest];
        }

        var cut = new short[cells.Count];
        var sumM = new Vector2[districts];
        var taken = new int[districts];
        for (var settle = 0; settle <= settles; settle++)
        {
            Array.Clear(sumM);
            Array.Clear(taken);
            // Each cell's nearest on as many threads as there are, and the sums in the cells' order on this one: a sum's
            // order is what the middles come out of.
            var near = new Middles(middleM, bend.SpanM);
            InChunks.Over(cells.Count, static () => 0, (_, cell) => cut[cell] = (short)near.Nearest(bentM[cell]));
            for (var cell = 0; cell < cells.Count; cell++)
            {
                sumM[cut[cell]] += bentM[cell];
                taken[cut[cell]]++;
            }

            for (var district = 0; district < districts; district++)
            {
                if (taken[district] > 0) middleM[district] = sumM[district] / taken[district];
            }
        }

        return cut;
    }

    /// <summary>
    /// <b>Every district a region's cut left too big, cut in two on its own cells</b> (GEN-65), and again until none is: a
    /// region's middles settle to districts near one size and not to one size, and a long region cut into many can keep
    /// one over the largest however many it is cut into.
    /// </summary>
    static short[] Halved(CellGrid grid, List<int> cells, short[] cut, Bend bend, CityGenFigures figures, ref Rng draw)
    {
        var cellM2 = grid.CellM * grid.CellM;
        var districts = cut.Max() + 1;
        for (var district = 0; district < districts; district++)
        {
            var mine = new List<int>();
            for (var at = 0; at < cut.Length; at++)
            {
                if (cut[at] == district) mine.Add(at);
            }

            if (mine.Count * cellM2 <= figures.DistrictLargestM2 || mine.Count < 2) continue;

            var own = new List<int>(mine.Count);
            foreach (var at in mine) own.Add(cells[at]);

            var halves = CutRegion(grid, own, 2, bend, figures.DistrictSettles, ref draw);
            var moved = 0;
            for (var at = 0; at < mine.Count; at++)
            {
                if (halves[at] != 1) continue;

                cut[mine[at]] = (short)districts;
                moved++;
            }

            if (moved == 0 || moved == mine.Count) continue;

            // The half handed a number of its own is asked in its turn, and this one again.
            districts++;
            district--;
        }

        return cut;
    }

    static float LargestM2(short[] cut, int districts, float cellM2)
    {
        var taken = new int[districts];
        foreach (var district in cut) taken[district]++;

        return taken.Max() * cellM2;
    }

    /// <summary>
    /// <b>Every district made one piece</b>: its largest kept, and each other piece given to the district it lies most
    /// against of those it leaves no bigger than a district gets (GEN-65) — or to none, where it lies against no such
    /// district.
    /// </summary>
    /// <remarks>
    /// <b>Again until a pass gives nothing away</b>: a piece is given to a neighbour by the labels the pass began with, so a
    /// piece given to a district whose own stray piece was given on in the same pass is left a stray of its new district.
    /// Each pass leaves fewer pieces, so a pass that does not is the last.
    /// </remarks>
    static void Whole(CellGrid grid, short[] label, int count, float largestM2)
    {
        for (var last = int.MaxValue; ;)
        {
            var (pieces, given) = WholeOnce(grid, label, count, largestM2);
            if (given == 0 || pieces >= last) return;

            last = pieces;
        }
    }

    /// <summary>One pass of <see cref="Whole"/>: how many pieces it found, and how many of them it gave away.</summary>
    static (int Pieces, int Given) WholeOnce(CellGrid grid, short[] label, int count, float largestM2)
    {
        var cellM2 = grid.CellM * grid.CellM;
        var piece = new int[grid.Count];
        Array.Fill(piece, -1);
        var pieces = new List<(short District, List<int> Cells)>();
        var queue = new Queue<int>();
        for (var start = 0; start < grid.Count; start++)
        {
            if (label[start] < 0 || piece[start] >= 0) continue;

            var cells = new List<int>();
            piece[start] = pieces.Count;
            queue.Enqueue(start);
            while (queue.TryDequeue(out var cell))
            {
                cells.Add(cell);
                var (column, row) = (cell % grid.Columns, cell / grid.Columns);
                foreach (var (dx, dy) in Four)
                {
                    if (!grid.Holds(column + dx, row + dy)) continue;

                    var next = grid.Index(column + dx, row + dy);
                    if (label[next] != label[start] || piece[next] >= 0) continue;

                    piece[next] = pieces.Count;
                    queue.Enqueue(next);
                }
            }

            pieces.Add((label[start], cells));
        }

        var largest = new int[count];
        Array.Fill(largest, -1);
        for (var at = 0; at < pieces.Count; at++)
        {
            var district = pieces[at].District;
            if (largest[district] < 0 || pieces[at].Cells.Count > pieces[largest[district]].Cells.Count) largest[district] = at;
        }

        var held = new int[count];
        foreach (var (district, cells) in pieces) held[district] += cells.Count;

        var givenAway = 0;
        for (var at = 0; at < pieces.Count; at++)
        {
            var (district, cells) = pieces[at];
            if (largest[district] == at) continue;

            // To a neighbour it leaves no bigger than a district gets (GEN-65), or to none.
            var given = MostAgainst(grid, label, cells, district, other => (held[other] + cells.Count) * cellM2 <= largestM2);
            foreach (var cell in cells) label[cell] = given;
            held[district] -= cells.Count;
            if (given >= 0) held[given] += cells.Count;
            givenAway++;
        }

        return (pieces.Count, givenAway);
    }

    /// <summary>
    /// The district some cells lie most against, the lower number of two as much — of those <paramref name="takes"/> lets
    /// have them, where it says — or none, where they lie against none.
    /// </summary>
    static short MostAgainst(CellGrid grid, short[] label, List<int> cells, short district, Func<short, bool>? takes = null)
    {
        var against = new Dictionary<short, int>();
        foreach (var cell in cells)
        {
            var (column, row) = (cell % grid.Columns, cell / grid.Columns);
            foreach (var (dx, dy) in Four)
            {
                if (!grid.Holds(column + dx, row + dy)) continue;

                var other = label[grid.Index(column + dx, row + dy)];
                if (other >= 0 && other != district && (takes is null || takes(other))) against[other] = against.GetValueOrDefault(other) + 1;
            }
        }

        return against.Count == 0 ? None : against.MaxBy(pair => (pair.Value, -pair.Key)).Key;
    }

    /// <summary>
    /// <b>No district smaller than the least a district takes</b> (GEN-65): one the cut left under it is given to the
    /// district it lies most against of those that stay no bigger than a district gets with it, and is open land where
    /// none can take it. Smallest first, so what one takes is weighed before the next is asked.
    /// </summary>
    /// <remarks>Run on whole districts (<see cref="Whole"/>), and a district given whole to one it touches stays whole.</remarks>
    static void NoneTooSmall(CellGrid grid, short[] label, int count, CityGenFigures figures)
    {
        var cellsOf = new List<int>[count];
        for (var district = 0; district < count; district++) cellsOf[district] = [];
        for (var cell = 0; cell < label.Length; cell++)
        {
            if (label[cell] >= 0) cellsOf[label[cell]].Add(cell);
        }

        var cellM2 = grid.CellM * grid.CellM;
        var order = new List<int>();
        for (var district = 0; district < count; district++)
        {
            if (cellsOf[district].Count > 0 && cellsOf[district].Count * cellM2 < figures.DistrictLeastM2) order.Add(district);
        }

        order.Sort((one, other) => (cellsOf[one].Count, one).CompareTo((cellsOf[other].Count, other)));
        foreach (var district in order)
        {
            var cells = cellsOf[district];
            if (cells.Count == 0 || cells.Count * cellM2 >= figures.DistrictLeastM2) continue;

            var given = MostAgainst(
                grid, label, cells, (short)district, other => (cellsOf[other].Count + cells.Count) * cellM2 <= figures.DistrictLargestM2);
            foreach (var cell in cells) label[cell] = given;
            if (given >= 0) cellsOf[given].AddRange(cells);
            cells.Clear();
        }
    }

    /// <summary>The districts numbered again from nought, any left holding no cell dropped.</summary>
    static int Renumbered(short[] label, int count)
    {
        var to = new short[count];
        Array.Fill(to, None);
        short next = 0;
        for (var cell = 0; cell < label.Length; cell++)
        {
            if (label[cell] < 0) continue;
            if (to[label[cell]] < 0) to[label[cell]] = next++;

            label[cell] = to[label[cell]];
        }

        return next;
    }

    /// <summary>
    /// <b>The edges between districts</b>, each the line two districts share between the places it ends — where a third
    /// district or a barrier meets them — as cell corners, in order. Two districts sharing an edge in two places share
    /// two lines.
    /// </summary>
    public List<(int A, int B, List<Vector2> Line)> Seams()
    {
        var edges = new Dictionary<(int A, int B), List<(int From, int To)>>();
        for (var row = 0; row < Grid.Rows; row++)
        {
            for (var column = 0; column < Grid.Columns; column++)
            {
                var here = Label[Grid.Index(column, row)];
                if (here < 0) continue;

                if (column + 1 < Grid.Columns && Label[Grid.Index(column + 1, row)] is var right and >= 0 && right != here)
                {
                    Add(here, right, Corner(column + 1, row), Corner(column + 1, row + 1));
                }

                if (row + 1 < Grid.Rows && Label[Grid.Index(column, row + 1)] is var below and >= 0 && below != here)
                {
                    Add(here, below, Corner(column, row + 1), Corner(column + 1, row + 1));
                }
            }
        }

        var seams = new List<(int, int, List<Vector2>)>();
        foreach (var ((a, b), pieces) in edges)
        {
            foreach (var chain in Chains(pieces, corner => Meets(corner) >= 3)) seams.Add((a, b, [.. chain.Select(CornerM)]));
        }

        return seams;

        void Add(short one, short other, int from, int to)
        {
            var key = one < other ? (one, other) : (other, one);
            if (!edges.TryGetValue(key, out var pieces)) edges[key] = pieces = [];
            pieces.Add((from, to));
        }
    }

    /// <summary>
    /// <b>A district's own outline</b>: the longest closed line round the cells it holds, as cell corners in order —
    /// its outer edge, a hole in it being a shorter one.
    /// </summary>
    public List<Vector2> Outline(int district)
    {
        var pieces = new List<(int From, int To)>();
        for (var row = 0; row < Grid.Rows; row++)
        {
            for (var column = 0; column < Grid.Columns; column++)
            {
                if (Label[Grid.Index(column, row)] != district) continue;

                if (!Is(column - 1, row)) pieces.Add((Corner(column, row), Corner(column, row + 1)));
                if (!Is(column + 1, row)) pieces.Add((Corner(column + 1, row), Corner(column + 1, row + 1)));
                if (!Is(column, row - 1)) pieces.Add((Corner(column, row), Corner(column + 1, row)));
                if (!Is(column, row + 1)) pieces.Add((Corner(column, row + 1), Corner(column + 1, row + 1)));
            }
        }

        var longest = Chains(pieces, static _ => false).MaxBy(chain => chain.Count) ?? [];
        return [.. longest.Select(CornerM)];

        bool Is(int column, int row) => Grid.Holds(column, row) && Label[Grid.Index(column, row)] == district;
    }

    /// <summary>The corner at a cell's top-left, numbered over a grid one corner wider and taller than the cells.</summary>
    int Corner(int column, int row) => (row * (Grid.Columns + 1)) + column;

    Vector2 CornerM(int corner) => new(corner % (Grid.Columns + 1) * Grid.CellM, corner / (Grid.Columns + 1) * Grid.CellM);

    /// <summary>How many different things — districts, and barrier or open land as one — meet at a corner.</summary>
    int Meets(int corner)
    {
        var (column, row) = (corner % (Grid.Columns + 1), corner / (Grid.Columns + 1));
        Span<short> seen = stackalloc short[4];
        var distinct = 0;
        foreach (var (dx, dy) in (ReadOnlySpan<(int, int)>)[(-1, -1), (0, -1), (-1, 0), (0, 0)])
        {
            var label = Grid.Holds(column + dx, row + dy) ? Label[Grid.Index(column + dx, row + dy)] : None;
            if (!seen[..distinct].Contains(label)) seen[distinct++] = label;
        }

        return distinct;
    }

    /// <summary>
    /// <b>Pieces of edge joined end to end into lines</b>: each from a corner where it stops — one piece reaching it, or a
    /// corner <paramref name="ends"/> says is an end — to the next; and what is left once every such line is walked, the
    /// closed lines, each from any corner of it round to itself.
    /// </summary>
    static List<List<int>> Chains(List<(int From, int To)> pieces, Func<int, bool> ends)
    {
        var at = new Dictionary<int, List<int>>();
        for (var piece = 0; piece < pieces.Count; piece++)
        {
            foreach (var corner in (ReadOnlySpan<int>)[pieces[piece].From, pieces[piece].To])
            {
                if (!at.TryGetValue(corner, out var list)) at[corner] = list = [];
                list.Add(piece);
            }
        }

        var walked = new bool[pieces.Count];
        var chains = new List<List<int>>();
        foreach (var (corner, list) in at)
        {
            if (list.Count != 1 && !ends(corner)) continue;

            foreach (var piece in list)
            {
                if (!walked[piece]) chains.Add(Walk(corner, piece));
            }
        }

        for (var piece = 0; piece < pieces.Count; piece++)
        {
            if (!walked[piece]) chains.Add(Walk(pieces[piece].From, piece));
        }

        return chains;

        List<int> Walk(int from, int piece)
        {
            var chain = new List<int> { from };
            var corner = from;
            while (true)
            {
                walked[piece] = true;
                corner = pieces[piece].From == corner ? pieces[piece].To : pieces[piece].From;
                chain.Add(corner);
                if (corner == from || ends(corner)) return chain;

                var next = -1;
                foreach (var other in at[corner])
                {
                    if (!walked[other])
                    {
                        next = other;
                        break;
                    }
                }

                if (next < 0) return chain;

                piece = next;
            }
        }
    }

    /// <summary>
    /// <b>The middles filed by square</b>, a district's span a side, so a cell asks the few round it and not every middle in
    /// its region — ring by ring outward until no square of the next ring can stand nearer than the nearest found: ring
    /// <c>k</c> is at least <c>k − 1</c> sides off a place in the middle square, and a middle found in ring 0 can be up to
    /// √2 sides off, so stopping one ring past the first find misses nearer middles.
    /// </summary>
    /// <remarks>
    /// <para>Asked from several threads at once: nothing is filed after it is laid.</para>
    /// <para>
    /// <b>Filed over the squares the middles' own box spans</b>, each square's middles in their own order, and a square
    /// outside that box holds none — a cell is asked millions of times a cut, and a hashed square costs more than the
    /// distances read in it.
    /// </para>
    /// </remarks>
    sealed class Middles
    {
        readonly Vector2[] _middleM;
        readonly float _sideM;
        readonly int _leastX;
        readonly int _leastY;
        readonly int _columns;
        readonly int _rows;
        readonly int[] _start;
        readonly int[] _in;

        public Middles(Vector2[] middleM, float sideM)
        {
            (_middleM, _sideM) = (middleM, sideM);
            var (leastX, leastY, mostX, mostY) = (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
            foreach (var atM in middleM)
            {
                var (x, y) = Square(atM);
                (leastX, leastY, mostX, mostY) = (Math.Min(leastX, x), Math.Min(leastY, y), Math.Max(mostX, x), Math.Max(mostY, y));
            }

            (_leastX, _leastY) = (leastX, leastY);
            (_columns, _rows) = middleM.Length == 0 ? (0, 0) : (mostX - leastX + 1, mostY - leastY + 1);
            _start = new int[(_columns * _rows) + 1];
            foreach (var atM in middleM) _start[Filed(Square(atM)) + 1]++;
            for (var square = 0; square < _columns * _rows; square++) _start[square + 1] += _start[square];

            _in = new int[middleM.Length];
            var filled = new int[_columns * _rows];
            for (var middle = 0; middle < middleM.Length; middle++)
            {
                var square = Filed(Square(middleM[middle]));
                _in[_start[square] + filled[square]++] = middle;
            }
        }

        (int X, int Y) Square(Vector2 atM) => ((int)MathF.Floor(atM.X / _sideM), (int)MathF.Floor(atM.Y / _sideM));

        int Filed((int X, int Y) square) => ((square.Y - _leastY) * _columns) + square.X - _leastX;

        /// <summary>The middles filed in one square, none for a square off the middles' box.</summary>
        ReadOnlySpan<int> In(int x, int y)
        {
            if ((uint)(x - _leastX) >= (uint)_columns || (uint)(y - _leastY) >= (uint)_rows) return [];

            var square = Filed((x, y));
            return _in.AsSpan(_start[square], _start[square + 1] - _start[square]);
        }

        /// <summary>The squared distance from a place to the nearest of a square's ground.</summary>
        float BoxM2(Vector2 atM, int x, int y)
        {
            var offX = MathF.Max(MathF.Max((x * _sideM) - atM.X, atM.X - ((x + 1) * _sideM)), 0f);
            var offY = MathF.Max(MathF.Max((y * _sideM) - atM.Y, atM.Y - ((y + 1) * _sideM)), 0f);
            return (offX * offX) + (offY * offY);
        }

        public int Nearest(Vector2 atM)
        {
            var (x, y) = Square(atM);
            var (nearest, bestM2) = (0, float.PositiveInfinity);
            var filed = 0;

            // How far in from its own square's nearest side the place stands: ring k is that and k − 1 sides off at the least.
            var inM = MathF.Min(
                MathF.Min(atM.X - (x * _sideM), ((x + 1) * _sideM) - atM.X),
                MathF.Min(atM.Y - (y * _sideM), ((y + 1) * _sideM) - atM.Y));
            for (var ring = 0; filed < _middleM.Length; ring++)
            {
                var leastM = inM + ((ring - 1) * _sideM);
                if (ring > 0 && leastM * leastM >= bestM2) break;

                for (var dy = -ring; dy <= ring; dy++)
                {
                    // The ring's own squares: its two rows whole, and the two ends of every row between them.
                    var step = dy == -ring || dy == ring ? 1 : Math.Max(1, 2 * ring);
                    for (var dx = -ring; dx <= ring; dx += step)
                    {
                        var list = In(x + dx, y + dy);
                        if (list.IsEmpty) continue;

                        filed += list.Length;
                        if (BoxM2(atM, x + dx, y + dy) >= bestM2) continue;

                        foreach (var middle in list)
                        {
                            var awayM2 = Vector2.DistanceSquared(atM, _middleM[middle]);
                            if (awayM2 < bestM2) (nearest, bestM2) = (middle, awayM2);
                        }
                    }
                }
            }

            return nearest;
        }
    }

    /// <summary>
    /// <b>The field nearness is measured on</b>: every place moved by two slow waves each way, as far as the bend share
    /// says and over the span of a district — so the line where two districts' middles are equally near bends with it.
    /// </summary>
    readonly struct Bend
    {
        readonly Vector4 _phase;
        readonly float _amplitudeM;
        readonly float _perM;

        public Bend(ref Rng draw, float amplitudeM, float spanM)
        {
            _phase = new Vector4(draw.NextFloat(0f, MathF.Tau), draw.NextFloat(0f, MathF.Tau), draw.NextFloat(0f, MathF.Tau), draw.NextFloat(0f, MathF.Tau));
            _amplitudeM = amplitudeM;
            _perM = MathF.Tau / spanM;
            SpanM = spanM;
        }

        /// <summary>The span of a district the waves run over, which is also the side the middles are filed by.</summary>
        public float SpanM { get; }

        /// <summary>
        /// How much faster, and how much of the first's height, the second wave each way runs: <b>the form of the bend and no
        /// figure of the town</b> — how far it bends is the brief's bend share — as a noise's second octave is.
        /// </summary>
        const float SecondWaveFaster = 1.7f, SecondWaveShare = 0.5f;

        public Vector2 Of(Vector2 atM) => atM + (new Vector2(
            Trig.Sin((atM.Y * _perM) + _phase.X) + (SecondWaveShare * Trig.Sin((atM.X * _perM * SecondWaveFaster) + _phase.Y)),
            Trig.Sin((atM.X * _perM) + _phase.Z) + (SecondWaveShare * Trig.Sin((atM.Y * _perM * SecondWaveFaster) + _phase.W)))
            * (_amplitudeM / (1f + SecondWaveShare)));
    }
}
