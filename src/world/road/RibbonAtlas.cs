using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Road;

/// <summary>One way under a body, and the stretch of it the body stands over, in that way's own metres.</summary>
internal readonly record struct WayCover(int Way, float FromM, float ToM);

/// <summary>
/// <b>What the atlas is laid from</b>: every way of the town's one numbering (<see cref="TownWays"/>) as its
/// line, the width it is travelled at, and which zebra it paints. Handed over as data, so the atlas learns
/// nothing about which network drew a way.
/// </summary>
internal interface IRibbonLines
{
    int WayCount { get; }

    ReadOnlySpan<ArcSeg> LineOf(int way, out float widthM);

    /// <summary>
    /// <b>The zebra a way is the paint of</b> — one of the walking lanes over it — or
    /// <see cref="RibbonMarks.NoZebra"/>, which is every other way. Its marks with the traffic are the whole
    /// zebra and not the ground the two ribbons share (TER-5c.3).
    /// </summary>
    int ZebraOf(int way);

    /// <summary>Whether the traffic drives this way, as against walks it.</summary>
    bool IsDriven(int way);
}

/// <summary>
/// <b>The ground of every way in the town, worked out once when the town is laid</b> — which ribbons cover
/// each point of a fixed lattice and how far along each of them that point is, and which ribbons overlap
/// which (<see cref="Marks"/>). <b>Everything the reservations need to know about the world's shape is
/// here</b>, so that laying and reading them is lookups and arithmetic and nothing else (TER-4c.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>A ribbon is a way's line swept to the way's own width, with square ends</b> — a lane's, a join's, a
/// bay's, a pavement's. Two ways meet where their ribbons share ground, and only there: the two lanes of a
/// carriageway, a lane's hand-over to its connector and two streams passing a lane apart lie edge to edge
/// and share none of it (<see cref="SimConfig.RibbonTouchM"/>). <b>Which ribbons share ground, and over
/// which metres, is worked out from the ribbons themselves</b> (<see cref="RibbonMarks"/>) and never from the
/// lattice.
/// </para>
/// <para>
/// <b>The lattice samples a point and never a cell</b>, and it is what a body is read over: the middles of
/// one level of the grid (<see cref="SimConfig.RibbonLevel"/>, SIM-8). A point is filed under every way
/// whose band it lies within reach of (<see cref="ReachOf"/>), with how far outside the band it stands; a
/// body covers a way where a point inside it lies inside the band — or outside it by less than the body
/// reaches past the point. So a body over a band is never between two points, and a body up to a band's
/// edge is not on it: a car against the paint is on its own lane alone.
/// </para>
/// <para>
/// <b>Kept a main cell of the grid at a time</b>, and only where some ribbon covers a point of it: each cell
/// holds one entry per way over each of its covered points, in row order, then column, then way, with where
/// each point's entries begin. A body's shape is a cell or two of a few rows each, so a lookup reads the
/// entries of the points inside it and no others, <b>all in memory the cell holds together</b> — the town's
/// rows run kilometres, and a body read off a row at a time was a search of each of them and a fetch from
/// wherever it landed. An entry is eight bytes — how far outside its band it stands, the metre along the way
/// to a tenth, and the way — and every point of a kept cell has a start of two more, which is what the
/// town's memory is quoted at.
/// </para>
/// <para>
/// <b>A start for every point rather than every row</b> is a fifth more memory, and it is what keeps a lookup
/// to the points inside the body: read from its row's first entry, a car's row holds over three entries for
/// every one the car stands on, and passing over them is a fifth of the lookup.
/// </para>
/// </remarks>
internal sealed class RibbonAtlas
{
    /// <summary>How finely a metre along a way is kept: a tenth, well inside half a lattice step.</summary>
    const float AlongPerMetre = 10f;

    /// <summary>What a whole reach outside a band is filed as (<see cref="Entry.Outside"/>).</summary>
    const float OutsidePerReach = byte.MaxValue;

    /// <summary>Below this curvature a piece's centre is further off than any town is wide, and it is read as a straight.</summary>
    const float StraightCurvature = 1e-6f;

    /// <summary>
    /// <b>One way over one point</b>: how far outside the way's band the point stands in
    /// <see cref="OutsidePerReach"/>ths of the reach and rounded up — nothing for a point on the band — the
    /// metre along the way in tenths, and the way. Which point it is over is where it is kept.
    /// </summary>
    readonly record struct Entry(byte Outside, ushort Along, int Way);

    /// <summary>
    /// One way found under a body so far, in the tenths the atlas files a metre in, so a body's stretch of it
    /// is the least and most of what was filed and is turned into metres once (<see cref="Widened"/>).
    /// </summary>
    /// <remarks>
    /// <b>The tenths are floats</b>, because a float's least and most are taken without a branch and an
    /// integer's are not: kept as integers, the jumps they mispredict cost a lookup a fifth.
    /// </remarks>
    readonly record struct Found(int Way, float FromTenths, float ToTenths);

    /// <summary>The level the points stand at the middles of.</summary>
    readonly GridLevel _level;

    /// <summary>How many halvings a main cell is cut into to reach the points, so a point's cell is a shift away.</summary>
    readonly int _shift;

    /// <summary>A point's row or column within its main cell, as a mask.</summary>
    readonly int _mask;

    /// <summary>How many starts one kept cell has: one a point, and one past the last.</summary>
    readonly int _startsPerCell;

    /// <summary>What one step of <see cref="Entry.Outside"/> is in metres.</summary>
    readonly float _metresPerOutside;

    /// <summary>The main cells the ribbons reach.</summary>
    readonly GridWindow _cells;

    /// <summary>Where each of those cells' entries are kept, or −1 for a cell no ribbon covers a point of.</summary>
    readonly int[] _cellSlot;

    /// <summary>Where each kept cell's entries begin; one past the last cell is the total.</summary>
    readonly int[] _slotFirst;

    /// <summary>
    /// Where each point of a kept cell has its entries, row by row, counted from the cell's first entry — one
    /// past the last point is the cell's count.
    /// </summary>
    readonly ushort[] _pointStart;

    readonly Entry[] _entries;
    readonly float[] _lengthM;

    RibbonAtlas(
        GridLevel level, GridWindow cells, int[] cellSlot, int[] slotFirst, ushort[] pointStart, Entry[] entries,
        float[] lengthM, WayCrossings marks, int points, int mostWaysAtAPoint)
    {
        _level = level;
        _shift = level.Depth - cells.Level.Depth;
        _mask = (1 << _shift) - 1;
        _startsPerCell = (1 << (2 * _shift)) + 1;
        _metresPerOutside = ReachOf(level.CellM) / OutsidePerReach;
        _cells = cells;
        _cellSlot = cellSlot;
        _slotFirst = slotFirst;
        _pointStart = pointStart;
        _entries = entries;
        _lengthM = lengthM;
        Marks = marks;
        PointCount = points;
        MostWaysAtAPoint = mostWaysAtAPoint;
    }

    /// <summary>
    /// <b>Which ways share ground with which, and where</b> (TER-5c): for every pair of ribbons that
    /// overlap, the section of each over which they touch, filed under both (<see cref="RibbonMarks"/>).
    /// <b>The marks on the reservation index</b> — a main claim over a section of one way places a secondary
    /// claim over the section of the other that it names (TER-5c.1).
    /// </summary>
    public WayCrossings Marks { get; }

    /// <summary>How far apart the lattice's points stand.</summary>
    public float StepM => _level.CellM;

    /// <summary>
    /// <b>How far past its way's band a point is still filed under it</b>: half the lattice's diagonal, which
    /// is the furthest any ground is from the nearest point standing for it — so a body reaching over a band
    /// always has a point inside it within this of the band.
    /// </summary>
    public static float ReachOf(float stepM) => stepM * MathF.Sqrt(2f) * 0.5f;

    /// <summary>Whether a piece is read as a straight rather than an arc.</summary>
    public static bool IsStraight(in ArcSeg arc) => MathF.Abs(arc.Curvature) < StraightCurvature;

    /// <summary>How many lattice points some ribbon covers — a census.</summary>
    public int PointCount { get; }

    /// <summary>How many (point, way) pairs those points carry, which is the standing cost of the atlas.</summary>
    public int EntryCount => _entries.Length;

    /// <summary>The most ways any one lattice point lies under.</summary>
    public int MostWaysAtAPoint { get; }

    /// <summary>What the atlas holds, in bytes: the figure a town's memory is quoted against.</summary>
    public long Bytes =>
        (4L * (_cellSlot.Length + _slotFirst.Length + _lengthM.Length)) + (2L * _pointStart.Length)
        + ((long)Unsafe.SizeOf<Entry>() * _entries.Length);

    /// <summary>
    /// <b>Every way a box stands over, and the stretch of each</b> — the collider of a car, at the pose the
    /// solver left it in. Lattice points inside the box are looked up, each counted where the box reaches
    /// further past it than it stands outside the way's band; the stretch is the least and most metre among
    /// them, widened by half a lattice step at each end and held to the way.
    /// </summary>
    /// <returns>How many ways were written; a way past the room given is dropped and counted.</returns>
    public int UnderBox(Vector2 centreM, Vector2 forward, float halfLengthM, float halfWidthM, Span<WayCover> into)
    {
        var box = new Box(centreM, forward, halfLengthM, halfWidthM);
        return Widened(into, Read(box, box.LeastY, box.MostY, into));
    }

    /// <summary>The same for a disc — a walker's collider.</summary>
    public int UnderDisc(Vector2 centreM, float radiusM, Span<WayCover> into) =>
        Widened(into, Read(new Disc(centreM, radiusM), centreM.Y - radiusM, centreM.Y + radiusM, into));

    /// <summary>How many ways have been dropped for want of room since the town was laid — a gate's figure.</summary>
    public long Dropped => _dropped;

    long _dropped;

    /// <summary>A body's collider, as how far inside it a point lies and where a row of points crosses it.</summary>
    interface IBody
    {
        float DepthM(Vector2 pointM);

        /// <summary>Where one horizontal line crosses the body, as the span of x inside it.</summary>
        bool Across(float y, out float fromX, out float toX);
    }

    /// <summary>A car's collider: its two axes, and its four corners in order round it.</summary>
    readonly struct Box : IBody
    {
        readonly Vector2 _centreM, _forward, _right, _a, _b, _c, _d;
        readonly float _halfLengthM, _halfWidthM;

        public Box(Vector2 centreM, Vector2 forward, float halfLengthM, float halfWidthM)
        {
            _centreM = centreM;
            _forward = forward;
            _right = Heading.RightOf(forward);
            _halfLengthM = halfLengthM;
            _halfWidthM = halfWidthM;

            var alongM = forward * halfLengthM;
            var acrossM = _right * halfWidthM;
            _a = centreM + alongM + acrossM;
            _b = centreM + alongM - acrossM;
            _c = centreM - alongM - acrossM;
            _d = centreM - alongM + acrossM;
        }

        public float LeastY => MathF.Min(MathF.Min(_a.Y, _b.Y), MathF.Min(_c.Y, _d.Y));

        public float MostY => MathF.Max(MathF.Max(_a.Y, _b.Y), MathF.Max(_c.Y, _d.Y));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float DepthM(Vector2 pointM)
        {
            var offM = pointM - _centreM;
            return MathF.Min(
                _halfLengthM - MathF.Abs(Vector2.Dot(offM, _forward)), _halfWidthM - MathF.Abs(Vector2.Dot(offM, _right)));
        }

        public bool Across(float y, out float fromX, out float toX)
        {
            fromX = float.PositiveInfinity;
            toX = float.NegativeInfinity;
            Cross(_a, _b, y, ref fromX, ref toX);
            Cross(_b, _c, y, ref fromX, ref toX);
            Cross(_c, _d, y, ref fromX, ref toX);
            Cross(_d, _a, y, ref fromX, ref toX);
            return toX >= fromX;
        }

        /// <summary>Where the line meets one edge, added to the span.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Cross(Vector2 one, Vector2 other, float y, ref float fromX, ref float toX)
        {
            if ((y < one.Y && y < other.Y) || (y > one.Y && y > other.Y)) return;

            if (one.Y == other.Y)
            {
                fromX = MathF.Min(fromX, MathF.Min(one.X, other.X));
                toX = MathF.Max(toX, MathF.Max(one.X, other.X));
                return;
            }

            var x = one.X + ((y - one.Y) * (other.X - one.X) / (other.Y - one.Y));
            fromX = MathF.Min(fromX, x);
            toX = MathF.Max(toX, x);
        }
    }

    readonly struct Disc(Vector2 centreM, float radiusM) : IBody
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float DepthM(Vector2 pointM) => radiusM - Vector2.Distance(pointM, centreM);

        public bool Across(float y, out float fromX, out float toX)
        {
            var offY = y - centreM.Y;
            var halfM = (radiusM * radiusM) - (offY * offY);
            if (halfM < 0f)
            {
                fromX = toX = 0f;
                return false;
            }

            halfM = MathF.Sqrt(halfM);
            fromX = centreM.X - halfM;
            toX = centreM.X + halfM;
            return true;
        }
    }

    /// <summary>
    /// <b>The ways under a body's points</b>, a row at a time, each point counted only where the body reaches
    /// further past it than it stands outside that way's band — so ground the body and the band both hold lies
    /// between them, and a body whose edge only meets the band's is not on it.
    /// </summary>
    int Read<TBody>(in TBody body, float leastY, float mostY, Span<WayCover> into) where TBody : struct, IBody
    {
        var found = MemoryMarshal.Cast<WayCover, Found>(into);
        var firstRow = _level.FirstMiddleFrom(leastY);
        var lastRow = _level.LastMiddleTo(mostY);

        var written = 0;
        for (var row = firstRow; row <= lastRow; row++)
        {
            var y = _level.MiddleM(row);
            if (!body.Across(y, out var fromX, out var toX)) continue;

            var fromColumn = _level.FirstMiddleFrom(fromX);
            var toColumn = _level.LastMiddleTo(toX);
            if (toColumn < fromColumn) continue;

            var cellRow = row >> _shift;
            var rowFirstPoint = (row & _mask) << _shift;
            for (var cellColumn = fromColumn >> _shift; cellColumn <= toColumn >> _shift; cellColumn++)
            {
                if (!_cells.Holds(cellColumn, cellRow)) continue;

                var slot = _cellSlot[_cells.IndexOf(cellColumn, cellRow)];
                if (slot < 0) continue;

                var cellFirstColumn = cellColumn << _shift;
                var first = _slotFirst[slot];
                var starts = (slot * _startsPerCell) + rowFirstPoint;
                var lastColumn = Math.Min(toColumn - cellFirstColumn, _mask);
                for (var column = Math.Max(fromColumn - cellFirstColumn, 0); column <= lastColumn; column++)
                {
                    var end = first + _pointStart[starts + column + 1];
                    for (var at = first + _pointStart[starts + column]; at < end; at++)
                    {
                        ref readonly var entry = ref _entries[at];
                        if (entry.Outside != 0 &&
                            body.DepthM(new Vector2(_level.MiddleM(cellFirstColumn + column), y)) <= entry.Outside * _metresPerOutside)
                        {
                            continue;
                        }

                        written = Grow(found, written, entry.Way, entry.Along);
                    }
                }
            }
        }

        return written;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int Grow(Span<Found> found, int written, int way, float alongTenths)
    {
        for (var slot = 0; slot < written; slot++)
        {
            if (found[slot].Way != way) continue;

            found[slot] = new Found(
                way, MathF.Min(found[slot].FromTenths, alongTenths), MathF.Max(found[slot].ToTenths, alongTenths));
            return written;
        }

        if (written == found.Length)
        {
            _dropped++;
            return written;
        }

        found[written] = new Found(way, alongTenths, alongTenths);
        return written + 1;
    }

    /// <summary>
    /// A point stands for the half step either side of it along the way, so the stretch a body covers is
    /// widened by that much — and held to the way's own two ends. The tenths it was gathered in become metres
    /// here, once a way.
    /// </summary>
    int Widened(Span<WayCover> into, int written)
    {
        var found = MemoryMarshal.Cast<WayCover, Found>(into);
        var halfM = _level.CellM * 0.5f;
        for (var slot = 0; slot < written; slot++)
        {
            var one = found[slot];
            into[slot] = new WayCover(
                one.Way,
                MathF.Max(0f, (one.FromTenths / AlongPerMetre) - halfM),
                MathF.Min(_lengthM[one.Way], (one.ToTenths / AlongPerMetre) + halfM));
        }

        return written;
    }

    /// <summary>
    /// <b>The atlas laid over every way of the town.</b> Build-time: it allocates freely and runs on as many
    /// threads as there are, and nothing it produces is written to again.
    /// </summary>
    /// <param name="level">
    /// The level of the grid the points stand at the middles of (<see cref="SimConfig.RibbonLevel"/>); they
    /// are kept a main cell at a time.
    /// </param>
    /// <param name="touchM">
    /// How deep inside both two ribbons have to overlap before they are marked
    /// (<see cref="SimConfig.RibbonTouchM"/>) — what keeps two ribbons laid edge to edge from being marked.
    /// </param>
    public static RibbonAtlas Lay(IRibbonLines lines, GridLevel level, float touchM)
    {
        var main = level.Grid.Main;
        var shift = level.Depth - main.Depth;
        var stepM = level.CellM;
        var wayCount = lines.WayCount;
        var lengthM = new float[wayCount];
        var leastM = new Vector2(float.MaxValue);
        var mostM = new Vector2(float.MinValue);
        for (var way = 0; way < wayCount; way++)
        {
            var arcs = lines.LineOf(way, out var widthM);
            lengthM[way] = Spline.TotalLengthM(arcs);
            foreach (var arc in arcs) Bound(arc, (widthM * 0.5f) + ReachOf(stepM), ref leastM, ref mostM);
        }

        if (leastM.X > mostM.X) leastM = mostM = Vector2.Zero;

        var cells = GridWindow.Over(main, leastM, mostM);

        // Every way's own points, found a way at a time on as many threads as there are.
        var perWay = new Sample[wayCount][];
        InChunks.Over(
            wayCount,
            () => new List<Sample>(),
            (found, way) => perWay[way] = PointsOf(lines, way, lengthM[way], level, found));

        // Filed a cell at a time: counted, the cells that hold any given their place, and each cell then
        // put in row, column and way order.
        var mask = (1 << shift) - 1;
        var cellCount = new int[cells.Count];
        foreach (var samples in perWay)
        {
            foreach (var sample in samples) cellCount[cells.IndexOf(sample.X >> shift, sample.Y >> shift)]++;
        }

        var cellSlot = new int[cells.Count];
        var slots = 0;
        for (var cell = 0; cell < cells.Count; cell++) cellSlot[cell] = cellCount[cell] > 0 ? slots++ : -1;

        var slotFirst = new int[slots + 1];
        for (var cell = 0; cell < cells.Count; cell++)
        {
            if (cellSlot[cell] >= 0) slotFirst[cellSlot[cell] + 1] = cellCount[cell];
        }

        for (var slot = 0; slot < slots; slot++) slotFirst[slot + 1] += slotFirst[slot];

        var total = slotFirst[slots];
        var keys = new long[total];
        var items = new long[total];
        var cursor = (int[])slotFirst.Clone();
        for (var way = 0; way < wayCount; way++)
        {
            foreach (var sample in perWay[way])
            {
                var at = cursor[cellSlot[cells.IndexOf(sample.X >> shift, sample.Y >> shift)]]++;
                var inCell = ((sample.Y & mask) << shift) | (sample.X & mask);
                keys[at] = ((long)inCell << 32) | (uint)way;
                items[at] = ((long)BitConverter.SingleToInt32Bits(sample.MarginM) << 32)
                            | (uint)BitConverter.SingleToInt32Bits(sample.AlongM);
            }

            perWay[way] = [];
        }

        InChunks.Over(
            slots,
            () => 0,
            (_, slot) => Array.Sort(keys, items, slotFirst[slot], slotFirst[slot + 1] - slotFirst[slot]));

        var startsPerCell = (1 << (2 * shift)) + 1;
        var pointStart = new ushort[slots * startsPerCell];
        var entries = new Entry[total];
        var reachM = ReachOf(stepM);
        var points = 0;
        var mostAtAPoint = 0;
        var atThePoint = 0;
        for (var slot = 0; slot < slots; slot++)
        {
            var first = slotFirst[slot];
            if (slotFirst[slot + 1] - first > ushort.MaxValue)
            {
                throw new InvalidOperationException($"a cell {main.CellM:F0} m across holds more than {ushort.MaxValue} entries");
            }

            var starts = slot * startsPerCell;
            var point = 0;
            for (var at = first; at < slotFirst[slot + 1]; at++)
            {
                var inCell = (int)(keys[at] >> 32);
                var way = (int)(uint)keys[at];
                var alongM = BitConverter.Int32BitsToSingle((int)(uint)items[at]);
                if (at == first || (int)(keys[at - 1] >> 32) != inCell)
                {
                    points++;
                    atThePoint = 0;
                }

                mostAtAPoint = Math.Max(mostAtAPoint, ++atThePoint);
                for (; point <= inCell; point++) pointStart[starts + point] = (ushort)(at - first);

                // Rounded up, so that a body whose edge only meets the band's is never read as over it.
                var outsideM = reachM - BitConverter.Int32BitsToSingle((int)(items[at] >> 32));
                entries[at] = new Entry(
                    (byte)Math.Clamp(MathF.Ceiling(outsideM / reachM * OutsidePerReach), 0f, OutsidePerReach),
                    Filed(alongM, lengthM[way]), way);
            }

            for (; point < startsPerCell; point++) pointStart[starts + point] = (ushort)(slotFirst[slot + 1] - first);
        }

        var marks = RibbonMarks.Of(lines, lengthM, stepM, touchM, main);

        return new RibbonAtlas(level, cells, cellSlot, slotFirst, pointStart, entries, lengthM, marks, points, mostAtAPoint);
    }

    /// <summary>A metre along a way as the atlas files it, which a way longer than the field holds refuses.</summary>
    static ushort Filed(float alongM, float lengthM)
    {
        var filed = MathF.Round(alongM * AlongPerMetre);
        if (lengthM * AlongPerMetre > ushort.MaxValue)
        {
            throw new InvalidOperationException($"a way {lengthM:F0} m long is longer than the atlas can file");
        }

        return (ushort)Math.Clamp(filed, 0f, ushort.MaxValue);
    }

    /// <summary>
    /// One lattice point a way's ribbon covers: which cell of the points' level it is the middle of, how far
    /// along the way, and how far inside its edges.
    /// </summary>
    readonly record struct Sample(int X, int Y, float AlongM, float MarginM);

    /// <summary>
    /// <b>The lattice points one way's ribbon covers</b>, each once: every point within half the way's width
    /// and the reach of its line, whose foot on the line falls between the line's own two ends.
    /// </summary>
    /// <remarks>
    /// A point is measured against each piece whose box it is in, and a point two pieces both reach — the
    /// inside of a joint — keeps the reading it lies deepest inside. <b>The ends are square</b>: a foot past
    /// the first piece's start or the last piece's end is off the ribbon, which is what keeps a lane and the
    /// connector it hands over to from sharing the metre they meet at.
    /// </remarks>
    static Sample[] PointsOf(IRibbonLines lines, int way, float lengthM, GridLevel level, List<Sample> found)
    {
        found.Clear();
        var arcs = lines.LineOf(way, out var widthM);
        if (arcs.Length == 0 || widthM <= 0f || lengthM <= 0f) return [];

        var halfM = (widthM * 0.5f) + ReachOf(level.CellM);

        var startM = 0f;
        for (var index = 0; index < arcs.Length; index++)
        {
            var arc = arcs[index];
            var leastM = new Vector2(float.MaxValue);
            var mostM = new Vector2(float.MinValue);
            Bound(arc, halfM, ref leastM, ref mostM);

            // Past its own two ends a piece reaches only where the chain carries on: a joint is covered by
            // whichever of its two pieces the point lies deepest in, and the chain's own ends are square.
            var behindM = index == 0 ? 0f : halfM;
            var pastM = index == arcs.Length - 1 ? 0f : halfM;

            var toColumn = level.LastMiddleTo(mostM.X);
            var toRow = level.LastMiddleTo(mostM.Y);
            for (var row = level.FirstMiddleFrom(leastM.Y); row <= toRow; row++)
            {
                for (var column = level.FirstMiddleFrom(leastM.X); column <= toColumn; column++)
                {
                    var pointM = level.MiddleM(column, row);
                    if (!Foot(arc, pointM, out var onArcM, out var offM)) continue;
                    if (onArcM < -behindM || onArcM > arc.LengthM + pastM || offM >= halfM) continue;

                    // Reaching past a joint is never reaching past the chain's own ends, which are square
                    // however short the piece next to them is.
                    if (startM + onArcM < 0f || startM + onArcM > lengthM) continue;

                    found.Add(new Sample(column, row, startM + Math.Clamp(onArcM, 0f, arc.LengthM), halfM - offM));
                }
            }

            startM += arc.LengthM;
        }

        found.Sort(static (one, other) =>
            one.Y != other.Y ? one.Y.CompareTo(other.Y)
            : one.X != other.X ? one.X.CompareTo(other.X)
            : other.MarginM.CompareTo(one.MarginM));

        var kept = new List<Sample>(found.Count);
        foreach (var sample in found)
        {
            if (kept.Count > 0 && kept[^1].X == sample.X && kept[^1].Y == sample.Y) continue;

            kept.Add(sample);
        }

        return [.. kept];
    }

    /// <summary>
    /// <b>Where a point's foot on one piece's own circle or line stands, and how far off it the point is</b> —
    /// the distance along the piece measured from its start, unclamped, so a caller can tell a foot inside
    /// the piece from one past either end.
    /// </summary>
    static bool Foot(in ArcSeg arc, Vector2 pointM, out float alongM, out float offM)
    {
        var along = arc.StartUnit;
        if (IsStraight(arc))
        {
            var toPoint = pointM - arc.StartM;
            alongM = Vector2.Dot(toPoint, along);
            offM = MathF.Abs(Spline.Cross(along, toPoint));
            return true;
        }

        var radiusM = 1f / arc.Curvature;
        var centreM = arc.StartM + (radiusM * Heading.RightOf(along));
        var fromCentre = arc.StartM - centreM;
        var toPointFromCentre = pointM - centreM;
        var distanceM = toPointFromCentre.Length();
        if (distanceM < 1e-6f)
        {
            alongM = offM = 0f;
            return false;
        }

        var turnRad = MathF.Atan2(
            Spline.Cross(fromCentre, toPointFromCentre), Vector2.Dot(fromCentre, toPointFromCentre));
        alongM = turnRad / arc.Curvature;

        // The far half of the circle is behind the start as easily as past the end; a piece shorter than
        // its circle is measured round the way that lands inside it where there is one.
        var roundM = MathF.Tau / MathF.Abs(arc.Curvature);
        if (alongM < 0f && alongM + roundM <= arc.LengthM + (roundM * 0.5f)) alongM += roundM;

        offM = MathF.Abs(distanceM - MathF.Abs(radiusM));
        return true;
    }

    /// <summary>One piece's box, grown by the half-width its ribbon reaches either side of it, added to the box given.</summary>
    public static void Bound(in ArcSeg arc, float halfM, ref Vector2 leastM, ref Vector2 mostM)
    {
        const int Samples = 8;
        var ownLeastM = new Vector2(float.MaxValue);
        var ownMostM = new Vector2(float.MinValue);
        for (var step = 0; step <= Samples; step++)
        {
            var atM = arc.PointAtM(arc.LengthM * step / Samples);
            ownLeastM = Vector2.Min(ownLeastM, atM);
            ownMostM = Vector2.Max(ownMostM, atM);
        }

        // A chord of an eighth of a piece bows off it by no more than this, and the ribbon reaches its
        // half-width past that.
        var chordM = arc.LengthM / Samples;
        var sagM = MathF.Abs(arc.Curvature) * chordM * chordM / 8f;
        var growM = new Vector2(halfM + sagM);
        leastM = Vector2.Min(leastM, ownLeastM - growM);
        mostM = Vector2.Max(mostM, ownMostM + growM);
    }
}
