using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>What keeps a place out of every district (GEN-64), which is also what two districts are parted by.</summary>
internal enum Barrier : byte
{
    None,

    /// <summary>The water, its shore and the strip every road keeps off it (GEN-71).</summary>
    Water,

    /// <summary>The sea, which nothing crosses.</summary>
    Sea,

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

    /// <summary>How many times the middles are moved to the middle of what they took.</summary>
    const int Settles = 8;

    /// <summary>How many more districts a region is cut into, at the most, when its first cut leaves one too big.</summary>
    const int Recuts = 6;

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

            var districts = (int)MathF.Ceiling(areaM2 / figures.DistrictLargestM2);
            short[] cut = [];
            for (var recut = 0; recut <= Recuts; recut++)
            {
                cut = CutRegion(grid, cells, districts + recut, bend, ref draw);
                if (LargestM2(cut, districts + recut, cellM2) <= figures.DistrictLargestM2) break;
            }

            var made = cut.Length == 0 ? 0 : cut.Max() + 1;
            for (var cell = 0; cell < cells.Count; cell++) label[cells[cell]] = (short)(count + cut[cell]);

            count += made;
        }

        Whole(grid, label, count);
        return new Partition(grid, label, barrier, Renumbered(label, count));
    }

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
    /// as they can stand, then settled (<see cref="Settles"/>) — each cell to the nearest middle on the bent field, each
    /// middle to the middle of what it took.
    /// </summary>
    static short[] CutRegion(CellGrid grid, List<int> cells, int districts, Bend bend, ref Rng draw)
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
        for (var settle = 0; settle <= Settles; settle++)
        {
            Array.Clear(sumM);
            Array.Clear(taken);
            var near = new Middles(middleM, bend.SpanM);
            for (var cell = 0; cell < cells.Count; cell++)
            {
                var nearest = near.Nearest(bentM[cell]);
                cut[cell] = (short)nearest;
                sumM[nearest] += bentM[cell];
                taken[nearest]++;
            }

            for (var district = 0; district < districts; district++)
            {
                if (taken[district] > 0) middleM[district] = sumM[district] / taken[district];
            }
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
    /// against — or to none, where it lies against no district.
    /// </summary>
    static void Whole(CellGrid grid, short[] label, int count)
    {
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

        for (var at = 0; at < pieces.Count; at++)
        {
            var (district, cells) = pieces[at];
            if (largest[district] == at) continue;

            var against = new Dictionary<short, int>();
            foreach (var cell in cells)
            {
                var (column, row) = (cell % grid.Columns, cell / grid.Columns);
                foreach (var (dx, dy) in Four)
                {
                    if (!grid.Holds(column + dx, row + dy)) continue;

                    var other = label[grid.Index(column + dx, row + dy)];
                    if (other >= 0 && other != district) against[other] = against.GetValueOrDefault(other) + 1;
                }
            }

            var given = against.Count == 0 ? None : against.MaxBy(pair => (pair.Value, -pair.Key)).Key;
            foreach (var cell in cells) label[cell] = given;
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
    /// its region — ring by ring outward until one ring finds any, and one ring past that, since the nearest can stand in
    /// the next ring out.
    /// </summary>
    sealed class Middles
    {
        readonly Vector2[] _middleM;
        readonly float _sideM;
        readonly Dictionary<(int, int), List<int>> _in = [];

        public Middles(Vector2[] middleM, float sideM)
        {
            (_middleM, _sideM) = (middleM, sideM);
            for (var middle = 0; middle < middleM.Length; middle++)
            {
                var key = Square(middleM[middle]);
                if (!_in.TryGetValue(key, out var list)) _in[key] = list = [];
                list.Add(middle);
            }
        }

        (int, int) Square(Vector2 atM) => ((int)MathF.Floor(atM.X / _sideM), (int)MathF.Floor(atM.Y / _sideM));

        public int Nearest(Vector2 atM)
        {
            var (x, y) = Square(atM);
            var (nearest, bestM2) = (0, float.PositiveInfinity);
            var foundAt = -1;
            for (var ring = 0; foundAt < 0 || ring <= foundAt + 1; ring++)
            {
                for (var dy = -ring; dy <= ring; dy++)
                {
                    for (var dx = -ring; dx <= ring; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring || !_in.TryGetValue((x + dx, y + dy), out var list)) continue;

                        foreach (var middle in list)
                        {
                            var awayM2 = Vector2.DistanceSquared(atM, _middleM[middle]);
                            if (awayM2 < bestM2) (nearest, bestM2) = (middle, awayM2);
                        }

                        if (foundAt < 0) foundAt = ring;
                    }
                }

                if (ring > 1024) break;
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

        public Vector2 Of(Vector2 atM) => atM + (new Vector2(
            Trig.Sin((atM.Y * _perM) + _phase.X) + (0.5f * Trig.Sin((atM.X * _perM * 1.7f) + _phase.Y)),
            Trig.Sin((atM.X * _perM) + _phase.Z) + (0.5f * Trig.Sin((atM.Y * _perM * 1.7f) + _phase.W))) * (_amplitudeM / 1.5f));
    }
}
