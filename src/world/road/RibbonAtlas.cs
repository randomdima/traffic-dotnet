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

    /// <summary>
    /// <b>The collision channels a way is on</b> (<see cref="CityGen.CityPlan.RoadArrays.ChannelOf"/>): two ways on no
    /// channel in common share no ground however their ribbons lie in plan, and a body is read onto the ways on a
    /// channel of its own alone.
    /// </summary>
    byte ChannelsOf(int way);
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
internal sealed partial class RibbonAtlas
{
    /// <summary>How finely a metre along a way is kept: a tenth, well inside half a lattice step.</summary>
    internal const float AlongPerMetre = 10f;

    /// <summary>The longest way the atlas files: its points' distance along it is a <see cref="ushort"/> of tenths.</summary>
    internal const float LongestWayM = ushort.MaxValue / AlongPerMetre;

    /// <summary>What a whole reach outside a band is filed as (<see cref="Entry.Outside"/>).</summary>
    internal const float OutsidePerReach = byte.MaxValue;

    /// <summary>Below this curvature a piece's centre is further off than any town is wide, and it is read as a straight.</summary>
    internal const float StraightCurvature = 1e-6f;

    /// <summary>How near an arc's centre a point stands before it has no foot on the arc to speak of (<see cref="Foot"/>).</summary>
    internal const float LeastFromCentreM = 1e-6f;

    /// <summary>
    /// <b>One way over one point</b>: how far outside the way's band the point stands in
    /// <see cref="OutsidePerReach"/>ths of the reach and rounded up — nothing for a point on the band — the point's
    /// column in its cell, the metre along the way in tenths, and the way. Which row it is over is where it is kept.
    /// </summary>
    /// <remarks>
    /// The column rides in the record's padding, so it costs nothing, and it is what lets a start stand for a run of
    /// points rather than one (<see cref="PointsAStartShift"/>): a read takes the runs the body reaches and passes
    /// over the columns outside it.
    /// </remarks>
    internal readonly record struct Entry(byte Outside, byte Column, ushort Along, int Way);

    /// <summary>
    /// <b>How many points of a row one start stands for</b>, as a shift: two, 1 m at the ribbon's level. A start a
    /// point reads only the body's own points at 257 starts a cell, the atlas's second largest table; a start a row
    /// reads past a car's box to the cell's edge on every row, and a crowded junction's point carries many ways.
    /// </summary>
    const int PointsAStartShift = 1;

    internal static int StartShiftOf(int shift) => Math.Min(PointsAStartShift, shift);

    /// <summary>A kept cell's starts: one a run of every row, and one past the last.</summary>
    internal static int StartsPerCellOf(int shift) => (1 << ((2 * shift) - StartShiftOf(shift))) + 1;

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

    /// <summary>How many points of a row one start stands for, as a shift — <see cref="PointsAStartShift"/>, or fewer in a smaller cell.</summary>
    readonly int _startShift;

    /// <summary>How many starts one row of a kept cell has, as a shift.</summary>
    readonly int _rowStartsShift;

    /// <summary>How many starts one kept cell has: <see cref="_rowStartsShift"/>'s worth a row, and one past the last.</summary>
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
    /// Where each run of points of a kept cell has its entries (<see cref="PointsAStartShift"/>), row by row, counted
    /// from the cell's first entry — one past the last run is the cell's count.
    /// </summary>
    readonly ushort[] _pointStart;

    /// <summary>The entries, from the cell slots' firsts on.</summary>
    readonly Entry[] _entries;

    readonly float[] _lengthM;

    /// <summary>Every way's channels, or empty where every way is on the ground's alone (<see cref="ChannelsOf(IRibbonLines)"/>).</summary>
    readonly byte[] _wayChannels;

    RibbonAtlas(
        GridLevel level, GridWindow cells, int[] cellSlot, int[] slotFirst, ushort[] pointStart, Entry[] entries,
        float[] lengthM, byte[] wayChannels, WayCrossings marks, int points, int mostWaysAtAPoint)
    {
        _wayChannels = wayChannels;
        _level = level;
        _shift = level.Depth - cells.Level.Depth;
        _mask = (1 << _shift) - 1;
        _startShift = StartShiftOf(_shift);
        _rowStartsShift = _shift - _startShift;
        _startsPerCell = StartsPerCellOf(_shift);
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

    /// <summary>
    /// How many ways one body may be found over. A bound on a caller's stack span; a way past it is dropped and
    /// counted (<see cref="Dropped"/>), and a gate holds that count at nothing.
    /// </summary>
    public const int MostWaysUnderABody = 48;

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
    public int EntryCount => _slotFirst[^1];

    /// <summary>The most ways any one lattice point lies under.</summary>
    public int MostWaysAtAPoint { get; }

    /// <summary>What the atlas holds, in bytes: the figure a town's memory is quoted against.</summary>
    public long Bytes =>
        (4L * (_cellSlot.Length + _slotFirst.Length + _lengthM.Length)) + (2L * _pointStart.Length)
        + ((long)Unsafe.SizeOf<Entry>() * EntryCount);


    /// <summary>
    /// <b>Every way a box stands over, and the stretch of each</b> — the collider of a car, where a pass, a switch or
    /// a bay stands it. Lattice points inside the box are looked up, each counted where the box reaches
    /// further past it than it stands outside the way's band; the stretch is the least and most metre among
    /// them, widened by half a lattice step at each end and held to the way.
    /// </summary>
    /// <param name="channels">
    /// The channels the body is on (<see cref="IRibbonLines.ChannelsOf"/>): only the ways on one of them are found, so
    /// a car on a bridge stands on the bridge's lanes and not on those of the road passing under it.
    /// </param>
    /// <returns>How many ways were written; a way past the room given is dropped and counted.</returns>
    public int UnderBox(
        Vector2 centreM, Vector2 forward, float halfLengthM, float halfWidthM, Span<WayCover> into, byte channels = Ground) =>
        Widened(into, Read(new Box(centreM, forward, halfLengthM, halfWidthM), channels, into));

    /// <summary>The same for a disc — a walker's collider.</summary>
    public int UnderDisc(Vector2 centreM, float radiusM, Span<WayCover> into, byte channels = Ground) =>
        Widened(into, Read(new Disc(centreM, radiusM), channels, into));

    /// <summary>
    /// <b>Which of a car's wheels stand on a way, and every way they stand on with the stretch its body holds of it</b>
    /// (TER-7b, TER-4c.2): a wheel is on every way on one of <paramref name="channels"/> whose band holds the lattice point
    /// nearest it, and on none is on the grass. Each way's stretch is the least and most metre its wheels stand at,
    /// widened by half a lattice step and by <paramref name="aroundAWheelM"/>, and held to the way; the ways are in wheel
    /// order.
    /// </summary>
    /// <param name="aroundAWheelM">How far the body is held either side of each wheel (<c>RoadFigures.HeldAroundAWheelM</c>).</param>
    /// <param name="onAWay">A bit a wheel, in the order <paramref name="wheelsM"/> has them, set for each on a way.</param>
    /// <returns>How many ways were written — none for a car with no wheel on a way; a way past the room given is dropped and counted.</returns>
    public int UnderWheels(
        ReadOnlySpan<Vector2> wheelsM, float aroundAWheelM, Span<WayCover> into, out byte onAWay, byte channels = Ground)
    {
        var found = MemoryMarshal.Cast<WayCover, Found>(into);
        var wayChannels = _wayChannels;
        var written = 0;
        var dropped = 0;
        byte wheelsOn = 0;

        // Each wheel's run is found, and asked of memory, before the wheel ahead of it is read: the run is a cell's
        // table away and nothing in the read before it depends on it, so the miss is taken while that read runs.
        var column = 0;
        var entries = wheelsM.IsEmpty ? default : EntriesOfThePointNearest(wheelsM[0], out column);
        for (var wheel = 0; wheel < wheelsM.Length; wheel++)
        {
            var nextColumn = 0;
            var next = wheel + 1 < wheelsM.Length ? EntriesOfThePointNearest(wheelsM[wheel + 1], out nextColumn) : default;
            Prefetch(next);

            foreach (ref readonly var entry in entries)
            {
                if (!OnTheWay(entry, column, channels, wayChannels)) continue;

                written = Grow(found, written, entry.Way, entry.Along, ref dropped);
                wheelsOn |= (byte)(1 << wheel);
            }

            entries = next;
            column = nextColumn;
        }

        onAWay = wheelsOn;
        CountDropped(dropped);
        return Widened(into, written, aroundAWheelM);
    }

    /// <summary>
    /// <b>The same for one wheel alone</b> — a walker, which stands as a single wheel at its centre (TER-4c.2), held
    /// <paramref name="aroundM"/> either side.
    /// </summary>
    public int UnderAWheel(Vector2 wheelM, float aroundM, Span<WayCover> into, byte channels = Ground) =>
        UnderWheels(new ReadOnlySpan<Vector2>(in wheelM), aroundM, into, out _, channels);

    /// <summary>Whether an entry of a point at this column of its cell is a way on one of the channels whose band holds the point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool OnTheWay(in Entry entry, int column, byte channels, byte[] wayChannels) =>
        entry.Column == column && entry.Outside == 0 && (wayChannels.Length == 0 || (wayChannels[entry.Way] & channels) != 0);

    /// <summary>
    /// The entries of the run of points holding the lattice point nearest <paramref name="pointM"/>, and that point's
    /// column in its cell — every entry of the run whose column is another's is passed over. Empty where no ribbon
    /// covers the point's cell.
    /// </summary>
    ReadOnlySpan<Entry> EntriesOfThePointNearest(Vector2 pointM, out int column)
    {
        var (pointColumn, pointRow) = _level.CellOf(pointM);
        column = pointColumn & _mask;
        var cellColumn = pointColumn >> _shift;
        var cellRow = pointRow >> _shift;
        if (!_cells.Holds(cellColumn, cellRow)) return default;

        var slot = _cellSlot[_cells.IndexOf(cellColumn, cellRow)];
        if (slot < 0) return default;

        var start = (slot * _startsPerCell) + ((pointRow & _mask) << _rowStartsShift) + (column >> _startShift);
        var run = _pointStart.AsSpan(start, 2);
        int from = run[0], to = run[1];
        return _entries.AsSpan(_slotFirst[slot] + from, to - from);
    }

    /// <summary>The channel of every way of a town with no bridge over its roads, and of the walk (<see cref="IRibbonLines.ChannelsOf"/>).</summary>
    const byte Ground = CityGen.CityPlan.RoadArrays.GroundChannel;

    /// <summary>The collision channels a way is on (<see cref="IRibbonLines.ChannelsOf"/>).</summary>
    public byte ChannelsOf(int way) => _wayChannels.Length > 0 ? _wayChannels[way] : Ground;

    /// <summary>How many ways have been dropped for want of room since the town was laid — a gate's figure.</summary>
    /// <remarks>Counted from the actors' thread and the managed world's step beside it alike (<c>ManagedWorld</c>).</remarks>
    public long Dropped => Interlocked.Read(ref _dropped);

    long _dropped;

    /// <summary>A body's collider, as how far inside it a point lies and where a row of points crosses it.</summary>
    interface IBody
    {
        float LeastY { get; }

        float MostY { get; }

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
        public float LeastY => centreM.Y - radiusM;

        public float MostY => centreM.Y + radiusM;

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
    /// <param name="channels">The body's channels: a way on none of them is passed over as though not there.</param>
    int Read<TBody>(in TBody body, byte channels, Span<WayCover> into) where TBody : struct, IBody
    {
        var wayChannels = _wayChannels;
        var found = MemoryMarshal.Cast<WayCover, Found>(into);
        var firstRow = _level.FirstMiddleFrom(body.LeastY);
        var lastRow = _level.LastMiddleTo(body.MostY);

        var written = 0;
        var dropped = 0;
        for (var row = firstRow; row <= lastRow; row++)
        {
            var y = _level.MiddleM(row);
            if (!body.Across(y, out var fromX, out var toX)) continue;

            var fromColumn = _level.FirstMiddleFrom(fromX);
            var toColumn = _level.LastMiddleTo(toX);
            if (toColumn < fromColumn) continue;

            var cellRow = row >> _shift;
            var rowFirstStart = (row & _mask) << _rowStartsShift;
            for (var cellColumn = fromColumn >> _shift; cellColumn <= toColumn >> _shift; cellColumn++)
            {
                if (!_cells.Holds(cellColumn, cellRow)) continue;

                var slot = _cellSlot[_cells.IndexOf(cellColumn, cellRow)];
                if (slot < 0) continue;

                var cellFirstColumn = cellColumn << _shift;
                var first = _slotFirst[slot];
                var starts = (slot * _startsPerCell) + rowFirstStart;
                var firstColumn = Math.Max(fromColumn - cellFirstColumn, 0);
                var lastColumn = Math.Min(toColumn - cellFirstColumn, _mask);
                var columns = (uint)(lastColumn - firstColumn);
                var end = first + _pointStart[starts + (lastColumn >> _startShift) + 1];
                for (var at = first + _pointStart[starts + (firstColumn >> _startShift)]; at < end; at++)
                {
                    ref readonly var entry = ref _entries[at];
                    if ((uint)(entry.Column - firstColumn) > columns) continue;
                    if (wayChannels.Length > 0 && (wayChannels[entry.Way] & channels) == 0) continue;

                    if (entry.Outside != 0)
                    {
                        var depthM = body.DepthM(new Vector2(_level.MiddleM(cellFirstColumn + entry.Column), y));
                        if (depthM <= entry.Outside * _metresPerOutside) continue;
                    }

                    written = Grow(found, written, entry.Way, entry.Along, ref dropped);
                }
            }
        }

        CountDropped(dropped);
        return written;
    }

    /// <remarks>
    /// The tenths are whole numbers, never NaN nor −0, which is the only ground the native least and most
    /// answer differently from <see cref="MathF.Min(float, float)"/> on — and they are one instruction each.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int Grow(Span<Found> found, int written, int way, float alongTenths, ref int dropped)
    {
        for (var slot = 0; slot < written; slot++)
        {
            if (found[slot].Way != way) continue;

            found[slot] = new Found(
                way, float.MinNative(found[slot].FromTenths, alongTenths), float.MaxNative(found[slot].ToTenths, alongTenths));
            return written;
        }

        if (written == found.Length)
        {
            dropped++;
            return written;
        }

        found[written] = new Found(way, alongTenths, alongTenths);
        return written + 1;
    }

    static unsafe void Prefetch(ReadOnlySpan<Entry> run)
    {
        if (!System.Runtime.Intrinsics.X86.Sse.IsSupported || run.IsEmpty) return;

        System.Runtime.Intrinsics.X86.Sse.Prefetch0(Unsafe.AsPointer(ref MemoryMarshal.GetReference(run)));
    }

    /// <summary>Counted once a call: an interlocked add inside the walk would be a fence on every entry.</summary>
    void CountDropped(int dropped)
    {
        if (dropped != 0) Interlocked.Add(ref _dropped, dropped);
    }

    /// <summary>
    /// A point stands for the half step either side of it along the way, so the stretch a body covers is
    /// widened by that much and by <paramref name="pastM"/> — and held to the way's own two ends. The tenths it was
    /// gathered in become metres here, once a way.
    /// </summary>
    /// <param name="pastM">How far the body reaches past the points it was read at: nothing for a collider read whole.</param>
    /// <remarks>
    /// The reach is positive and the tenths whole, so neither end is NaN nor −0 and the native least and most
    /// are <see cref="MathF.Min(float, float)"/>'s to the bit.
    /// </remarks>
    int Widened(Span<WayCover> into, int written, float pastM = 0f)
    {
        var covers = into[..written];
        var found = MemoryMarshal.Cast<WayCover, Found>(covers);
        var lengthM = _lengthM;
        var reachM = (_level.CellM * 0.5f) + pastM;
        for (var slot = 0; slot < found.Length; slot++)
        {
            var one = found[slot];
            covers[slot] = new WayCover(
                one.Way,
                float.MaxNative(0f, (one.FromTenths / AlongPerMetre) - reachM),
                float.MinNative(lengthM[one.Way], (one.ToTenths / AlongPerMetre) + reachM));
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
    /// <param name="anyoneWalks">
    /// Whether the town stands anybody on foot. <b>Where nobody is, a walked way is filed only where the traffic
    /// crosses it</b> — a zebra's paint: the walk is read only under walkers and the claims they hold, and on a
    /// traced city it is half of every point. The marks are worked out from the ribbons, so they hold every way
    /// either way.
    /// </param>
    /// <param name="betweenStages">
    /// Called between the marks and the filing — where a caller laying a town large enough to want it collects what the
    /// marks worked in.
    /// </param>
    public static RibbonAtlas Lay(IRibbonLines lines, GridLevel level, float touchM, bool anyoneWalks, Action? betweenStages = null)
    {
        // The marks before the filing, and what they worked in let go of between: the two working sets are each the
        // size of the entries, and laid the other way round both stood at once at the open's peak.
        var (lengthM, cells) = Framed(lines, level);
        var marks = MarksOf(lines, lengthM, level, touchM);
        betweenStages?.Invoke();
        return Filed(lines, lengthM, level, cells, anyoneWalks, marks);
    }

    /// <summary>
    /// <b>Which ways share ground with which, and where</b> (<see cref="Marks"/>), for an atlas a device files
    /// (<see cref="Filing"/>): the same marks <see cref="Lay"/>'s atlas carries.
    /// </summary>
    public static WayCrossings MarksOf(IRibbonLines lines, float[] lengthM, GridLevel level, float touchM) =>
        RibbonMarks.Of(lines, lengthM, level.CellM, touchM, level.Grid.Main);

    /// <summary>Every way's length, and the main cells the ribbons reach — what filing the points is laid over.</summary>
    static (float[] LengthM, GridWindow Cells) Framed(IRibbonLines lines, GridLevel level)
    {
        static string Longest(ReadOnlySpan<ArcSeg> arcs)
        {
            var longest = arcs[0];
            foreach (var arc in arcs)
            {
                if (arc.LengthM > longest.LengthM) longest = arc;
            }

            return $"{longest.LengthM:F1} m from {longest.StartM} bending {longest.Curvature:G3}";
        }

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
            if (lengthM[way] * AlongPerMetre > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"a way {lengthM[way]:F0} m long is longer than the atlas can file: {(lines.IsDriven(way) ? "driven" : "walked")} way {way}, " +
                    $"{widthM} m wide, from {arcs[0].StartM} to {arcs[^1].EndM} in {arcs.Length} arcs, the longest {Longest(arcs)}");
            }

            foreach (var arc in arcs) Bound(arc, (widthM * 0.5f) + ReachOf(stepM), ref leastM, ref mostM);
        }

        if (leastM.X > mostM.X) leastM = mostM = Vector2.Zero;

        if (1 << (2 * shift) > byte.MaxValue + 1)
        {
            throw new InvalidOperationException($"{1 << shift} points across a main cell are more than a filing can name");
        }

        return (lengthM, GridWindow.Over(main, leastM, mostM));
    }

    /// <summary>
    /// <b>Every way's points found and filed into the cells they fall in</b>, each cell in row, column and way order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A method of its own so that what filing works in is gone when it returns</b>: held through the marks' own
    /// working set (<see cref="RibbonMarks"/>), it was the whole of an open's peak on a traced city.
    /// </para>
    /// <para>
    /// <b>Every way's points are found once to count each cell's and again to file them</b>: kept from the one to the
    /// other, they were the entries' size over again at the open's peak — a gigabyte on a traced city.
    /// </para>
    /// </remarks>
    static RibbonAtlas Filed(
        IRibbonLines lines, float[] lengthM, GridLevel level, GridWindow cells, bool anyoneWalks, WayCrossings marks)
    {
        var wayCount = lines.WayCount;
        var shift = level.Depth - cells.Level.Depth;
        var reachM = ReachOf(level.CellM);
        var mask = (1 << shift) - 1;

        bool Files(int way) => anyoneWalks || lines.IsDriven(way) || lines.ZebraOf(way) != RibbonMarks.NoZebra;
        int CellOf(in Sample point) => cells.IndexOf(point.X >> shift, point.Y >> shift);

        // Every way's own points, found a way at a time on as many threads as there are, and counted under their cells.
        var cellCount = new int[cells.Count];
        InChunks.Over(
            wayCount,
            () => (Found: new List<Sample>(), Places: new List<long>()),
            (working, way) =>
            {
                if (!Files(way)) return;

                foreach (var point in PointsOf(lines, way, lengthM[way], level, working.Found, working.Places))
                {
                    Interlocked.Increment(ref cellCount[CellOf(point)]);
                }
            });

        var (cellSlot, slotFirst) = SlotsOf(cellCount, cells.Level);
        var slots = slotFirst.Length - 1;

        // Filed a cell at a time: every point put in its cell in whatever order the threads reach it, and each cell then
        // put in row, column and way order — which no two entries share, so the order they were filed in is gone. An
        // entry's column carries the whole of its point's place in the cell until the cell is sorted on it, and only then
        // its column: a point's place is a byte (above), and a second table of them is the entries' size over again.
        var entries = new Entry[slotFirst[slots]];
        var cursor = (int[])slotFirst.Clone();
        InChunks.Over(
            wayCount,
            () => (Found: new List<Sample>(), Places: new List<long>()),
            (working, way) =>
            {
                if (!Files(way)) return;

                foreach (var point in PointsOf(lines, way, lengthM[way], level, working.Found, working.Places))
                {
                    var at = Interlocked.Increment(ref cursor[cellSlot[CellOf(point)]]) - 1;
                    var inCell = (byte)(((point.Y & mask) << shift) | (point.X & mask));
                    entries[at] = new Entry(Outside(point.MarginM, reachM), inCell, Filed(point.AlongM), way);
                }
            });

        // A cell's working sets are made on the thread sorting it, as long as the fullest cell, rather than held for every
        // entry of the town at once.
        var mostInACell = 0;
        for (var slot = 0; slot < slots; slot++) mostInACell = Math.Max(mostInACell, slotFirst[slot + 1] - slotFirst[slot]);

        var startsPerCell = StartsPerCellOf(shift);
        var pointStart = new ushort[slots * startsPerCell];
        var cellCensus = new (int Points, int MostAtAPoint)[slots];
        InChunks.Over(
            slots,
            () => (Keys: new long[mostInACell], Sorted: new Entry[mostInACell], PlaceFrom: new int[(1 << (2 * shift)) + 1]),
            (working, slot) =>
            {
                var first = slotFirst[slot];
                var count = slotFirst[slot + 1] - first;
                var cellKeys = working.Keys.AsSpan(0, count);
                var cellEntries = entries.AsSpan(first, count);
                InPlaceOrder(cellEntries, working.Sorted.AsSpan(0, count), working.PlaceFrom);
                for (var at = 0; at < count; at++) cellKeys[at] = ((long)cellEntries[at].Column << 32) | (uint)cellEntries[at].Way;
                foreach (ref var entry in cellEntries) entry = entry with { Column = (byte)(entry.Column & mask) };
                cellCensus[slot] = Started(cellKeys, pointStart.AsSpan(slot * startsPerCell, startsPerCell), shift);
            });

        var points = 0;
        var mostAtAPoint = 0;
        foreach (var cell in cellCensus)
        {
            points += cell.Points;
            mostAtAPoint = Math.Max(mostAtAPoint, cell.MostAtAPoint);
        }

        return new RibbonAtlas(
            level, cells, cellSlot, slotFirst, pointStart, entries, lengthM, ChannelsOf(lines), marks, points, mostAtAPoint);
    }

    /// <summary>
    /// <b>Where each main cell's entries are kept</b>, off how many each holds: a slot for every cell that holds any,
    /// numbered in row order, and where each slot's entries begin — one past the last slot is the total.
    /// </summary>
    /// <remarks>The host's own filing and a device's (<see cref="AtlasFiling"/>) both lay their slots here.</remarks>
    public static (int[] CellSlot, int[] SlotFirst) SlotsOf(ReadOnlySpan<int> cellCount, GridLevel main)
    {
        var cellSlot = new int[cellCount.Length];
        var slots = 0;
        for (var cell = 0; cell < cellCount.Length; cell++) cellSlot[cell] = cellCount[cell] > 0 ? slots++ : -1;

        var slotFirst = new int[slots + 1];
        for (var cell = 0; cell < cellCount.Length; cell++)
        {
            if (cellSlot[cell] >= 0) slotFirst[cellSlot[cell] + 1] = cellCount[cell];
        }

        for (var slot = 0; slot < slots; slot++)
        {
            if (slotFirst[slot + 1] > ushort.MaxValue)
            {
                throw new InvalidOperationException($"a cell {main.CellM:F0} m across holds more than {ushort.MaxValue} entries");
            }

            slotFirst[slot + 1] += slotFirst[slot];
        }

        return (cellSlot, slotFirst);
    }

    /// <summary>
    /// <b>One cell's entries put in place and way order</b>, while each entry's column still carries its whole place in
    /// the cell — the order any sort on the two gives, since no two entries share both. Counted out by place, which is a
    /// byte, and then each place's few ways put in order: they come in out of it only where threads filed the cell at
    /// once, and in it where one did.
    /// </summary>
    static void InPlaceOrder(Span<Entry> entries, Span<Entry> sorted, Span<int> placeFrom)
    {
        placeFrom.Clear();
        foreach (var entry in entries) placeFrom[entry.Column + 1]++;
        for (var place = 1; place < placeFrom.Length; place++) placeFrom[place] += placeFrom[place - 1];
        foreach (var entry in entries) sorted[placeFrom[entry.Column]++] = entry;

        for (var at = 1; at < sorted.Length; at++)
        {
            var entry = sorted[at];
            var before = at - 1;
            for (; before >= 0 && sorted[before].Column == entry.Column && sorted[before].Way > entry.Way; before--)
            {
                sorted[before + 1] = sorted[before];
            }

            sorted[before + 1] = entry;
        }

        sorted.CopyTo(entries);
    }

    /// <summary>
    /// Every way's channels (<see cref="IRibbonLines.ChannelsOf"/>), or <b>none at all where every way is on the
    /// ground's alone</b> — every town but one with a bridge over its roads — so a read there asks nothing it does not
    /// have to.
    /// </summary>
    public static byte[] ChannelsOf(IRibbonLines lines)
    {
        var channels = new byte[lines.WayCount];
        var any = false;
        for (var way = 0; way < channels.Length; way++)
        {
            channels[way] = lines.ChannelsOf(way);
            any |= channels[way] != Ground;
        }

        return any ? channels : [];
    }

    /// <summary>A metre along a way as the atlas files it; a way longer than the field holds is refused when laid.</summary>
    static ushort Filed(float alongM) => (ushort)Math.Clamp(MathF.Round(alongM * AlongPerMetre), 0f, ushort.MaxValue);

    /// <summary>
    /// How far outside its way's band a point stands, as <see cref="Entry.Outside"/> files it — rounded up, so
    /// that a body whose edge only meets the band's is never read as over it.
    /// </summary>
    static byte Outside(float marginM, float reachM) =>
        (byte)Math.Clamp(MathF.Ceiling((reachM - marginM) / reachM * OutsidePerReach), 0f, OutsidePerReach);

    /// <summary>
    /// Where each run of one cell's points has its entries, read off the cell's keys in order; how many points
    /// hold any, and the most ways at one of them. A point's run is its place in the cell shifted down, because
    /// its column is the low bits of it.
    /// </summary>
    static (int Points, int MostAtAPoint) Started(ReadOnlySpan<long> keys, Span<ushort> starts, int shift)
    {
        var startShift = StartShiftOf(shift);
        var points = 0;
        var mostAtAPoint = 0;
        var atThePoint = 0;
        var run = 0;
        for (var at = 0; at < keys.Length; at++)
        {
            var inCell = (int)(keys[at] >> 32);
            if (at == 0 || (int)(keys[at - 1] >> 32) != inCell)
            {
                points++;
                atThePoint = 0;
            }

            mostAtAPoint = Math.Max(mostAtAPoint, ++atThePoint);
            for (; run <= inCell >> startShift; run++) starts[run] = (ushort)at;
        }

        for (; run < starts.Length; run++) starts[run] = (ushort)keys.Length;

        return (points, mostAtAPoint);
    }

    /// <summary>
    /// One lattice point a way's ribbon covers: which cell of the points' level it is the middle of, how far
    /// along the way, how far inside its edges, and whether its foot fell on the piece that read it rather than
    /// past one of that piece's ends.
    /// </summary>
    readonly record struct Sample(int X, int Y, float AlongM, float MarginM, bool OnItsPiece);

    /// <summary>
    /// <b>The lattice points one way's ribbon covers</b>, each once: every point within half the way's width
    /// and the reach of its line, whose foot on the line falls between the line's own two ends.
    /// </summary>
    /// <remarks>
    /// A point is measured against each piece whose box it is in. <b>A point two pieces both reach is read off the
    /// piece its foot falls on</b>, and only a point whose foot falls on neither — outside a bend, where the two
    /// pieces part — keeps the reading it lies deepest inside, at the joint. A piece carried on past its end is its
    /// circle and not the next piece, and outside a bend it runs nearer the points there than the bend does: read off
    /// it, a point metres down the next piece would be filed at the joint's metre. <b>The ends are square</b>: a foot past the first
    /// piece's start or the last piece's end is off the ribbon, which is what keeps a lane and the connector it hands
    /// over to from sharing the metre they meet at. <b>A piece measures only the runs of each row its band can reach</b>
    /// (<see cref="RunsAcross"/>) and not the whole of its box, which for a piece running corner to corner is the band
    /// many times over.
    /// <para>What it returns is a view of <paramref name="found"/>, good until that list is next used.</para>
    /// </remarks>
    static ReadOnlySpan<Sample> PointsOf(
        IRibbonLines lines, int way, float lengthM, GridLevel level, List<Sample> found, List<long> places)
    {
        Find(lines, way, lengthM, level, found);

        // A piece finds its points in order, so a way of one piece, or of pieces that never reach one another's
        // points, is in row and column order already, each point once.
        var sorted = CollectionsMarshal.AsSpan(found);
        var inOrder = true;
        for (var at = 1; at < sorted.Length && inOrder; at++) inOrder = PlaceOf(sorted[at - 1]) < PlaceOf(sorted[at]);
        if (inOrder) return sorted;

        // Sorted by place alone, a key a sample, and of a place's readings the one that keeps is chosen below: through a
        // comparison of the whole reading the sort was a twentieth of a page's open.
        places.Clear();
        foreach (var sample in sorted) places.Add(PlaceOf(sample));
        var keys = CollectionsMarshal.AsSpan(places);
        KeyedSort.Sort(keys, sorted);

        var kept = 0;
        for (var at = 0; at < sorted.Length; at++)
        {
            if (kept > 0 && keys[at] == keys[at - 1])
            {
                if (Deeper(sorted[at], sorted[kept - 1])) sorted[kept - 1] = sorted[at];
                continue;
            }

            sorted[kept++] = sorted[at];
        }

        return sorted[..kept];
    }

    /// <summary>A point's row and column as one number that orders them: the row, then the column, each signed.</summary>
    static long PlaceOf(in Sample sample) => ((long)sample.Y << 32) | (uint)(sample.X ^ int.MinValue);

    /// <summary>
    /// Whether one reading of a point is kept over another: <b>the one whose foot fell on the piece that read it</b>, then
    /// the one the point lies deeper inside, then — read as deep, which only a joint's two pieces can — the nearer the
    /// way's start, so no order the readings were sorted in decides it.
    /// </summary>
    static bool Deeper(in Sample one, in Sample other) =>
        one.OnItsPiece != other.OnItsPiece ? one.OnItsPiece
        : one.MarginM != other.MarginM ? one.MarginM > other.MarginM
        : one.AlongM < other.AlongM;

    /// <summary>
    /// <b>Every reading of a lattice point one way's pieces make</b>, in the order the pieces make them: a point two
    /// pieces reach is read twice (<see cref="PointsOf"/> keeps one).
    /// </summary>
    static ReadOnlySpan<Sample> Find(IRibbonLines lines, int way, float lengthM, GridLevel level, List<Sample> found)
    {
        found.Clear();
        var arcs = lines.LineOf(way, out var widthM);
        if (arcs.Length == 0 || widthM <= 0f || lengthM <= 0f) return [];

        var halfM = (widthM * 0.5f) + ReachOf(level.CellM);

        Span<RowRun> runs = stackalloc RowRun[2];
        var startM = 0f;
        for (var index = 0; index < arcs.Length; index++)
        {
            var arc = arcs[index];
            var leastM = new Vector2(float.MaxValue);
            var mostM = new Vector2(float.MinValue);
            Bound(arc, halfM, ref leastM, ref mostM);

            // Past its own two ends a piece reaches only where the chain carries on — the outside of a bend, which
            // neither piece's own stretch covers — and the chain's own ends are square.
            var behindM = index == 0 ? 0f : halfM;
            var pastM = index == arcs.Length - 1 ? 0f : halfM;

            var fromColumn = level.FirstMiddleFrom(leastM.X);
            var toColumn = level.LastMiddleTo(mostM.X);
            var toRow = level.LastMiddleTo(mostM.Y);
            for (var row = level.FirstMiddleFrom(leastM.Y); row <= toRow; row++)
            {
                var across = RunsAcross(arc, level.MiddleM(row), halfM, behindM, pastM, level.CellM, runs);
                for (var run = 0; run < across; run++)
                {
                    var lastColumn = Math.Min(toColumn, level.LastMiddleTo((float)runs[run].ToM));
                    for (var column = Math.Max(fromColumn, level.FirstMiddleFrom((float)runs[run].FromM)); column <= lastColumn; column++)
                    {
                        var pointM = level.MiddleM(column, row);
                        if (!Foot(arc, pointM, out var onArcM, out var offM)) continue;
                        if (onArcM < -behindM || onArcM > arc.LengthM + pastM || offM >= halfM) continue;

                        // Reaching past a joint is never reaching past the chain's own ends, which are square
                        // however short the piece next to them is.
                        if (startM + onArcM < 0f || startM + onArcM > lengthM) continue;

                        found.Add(new Sample(
                            column, row, startM + Math.Clamp(onArcM, 0f, arc.LengthM), halfM - offM,
                            onArcM >= 0f && onArcM <= arc.LengthM));
                    }
                }
            }

            startM += arc.LengthM;
        }

        return CollectionsMarshal.AsSpan(found);
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
        if (distanceM < LeastFromCentreM)
        {
            alongM = offM = 0f;
            return false;
        }

        // Our own and not the library's, so the device files the point the host does (atlas_points.glsl).
        var turnRad = Trig.Atan2(
            Spline.Cross(fromCentre, toPointFromCentre), Vector2.Dot(fromCentre, toPointFromCentre));
        alongM = turnRad / arc.Curvature;

        // The far half of the circle is behind the start as easily as past the end; a piece shorter than
        // its circle is measured round the way that lands inside it where there is one.
        var roundM = MathF.Tau / MathF.Abs(arc.Curvature);
        if (alongM < 0f && alongM + roundM <= arc.LengthM + (roundM * 0.5f)) alongM += roundM;

        offM = MathF.Abs(distanceM - MathF.Abs(radiusM));
        return true;
    }

    /// <summary>One stretch of a row of the lattice, in metres across the town, that a piece's band may reach.</summary>
    readonly record struct RowRun(double FromM, double ToM);

    /// <summary>
    /// How far out of a circle's radius single-precision arithmetic can put a point's distance from its centre and
    /// the centre itself, as a share of the radius — some tens of times the rounding of either.
    /// </summary>
    const double RadiusRoundingShare = 4e-6;

    /// <summary>
    /// <b>Where along one row of the lattice a piece's band can reach</b> — from <paramref name="behindM"/> before its
    /// start to <paramref name="pastM"/> past its end, within <paramref name="halfM"/> of its line — as at most two
    /// runs: a straight's band crosses a row once, and an arc's ring either side of its middle. Returns how many.
    /// </summary>
    /// <remarks>
    /// <b>Never narrower than the band as <see cref="Foot"/> reads it</b>: each run is grown by
    /// <paramref name="slackM"/>, and an arc's ring by a share of its radius as well, since near the row a ring
    /// touches a hair of rounding in a distance is metres along the row. What is inside a run is read as every point of
    /// the piece's box was, so the points a piece files are the ones it filed.
    /// </remarks>
    static int RunsAcross(in ArcSeg arc, float yM, float halfM, float behindM, float pastM, float slackM, Span<RowRun> into)
    {
        double startX = arc.StartM.X, startY = arc.StartM.Y;
        double alongX = arc.StartUnit.X, alongY = arc.StartUnit.Y;
        var across = Heading.RightOf(arc.StartUnit);

        if (IsStraight(arc))
        {
            double fromM = -behindM, toM = arc.LengthM + pastM;
            Span<double> xs = [startX + (alongX * fromM) - (across.X * halfM), startX + (alongX * toM) - (across.X * halfM), startX + (alongX * toM) + (across.X * halfM), startX + (alongX * fromM) + (across.X * halfM)];
            Span<double> ys = [startY + (alongY * fromM) - (across.Y * halfM), startY + (alongY * toM) - (across.Y * halfM), startY + (alongY * toM) + (across.Y * halfM), startY + (alongY * fromM) + (across.Y * halfM)];

            var leastX = double.MaxValue;
            var mostX = double.MinValue;
            for (var corner = 0; corner < 4; corner++)
            {
                var next = (corner + 1) % 4;
                if ((ys[corner] - yM) * (ys[next] - yM) > 0.0) continue;

                if (ys[corner] == ys[next])
                {
                    leastX = Math.Min(leastX, Math.Min(xs[corner], xs[next]));
                    mostX = Math.Max(mostX, Math.Max(xs[corner], xs[next]));
                    continue;
                }

                var xM = xs[corner] + ((yM - ys[corner]) * (xs[next] - xs[corner]) / (ys[next] - ys[corner]));
                leastX = Math.Min(leastX, xM);
                mostX = Math.Max(mostX, xM);
            }

            if (leastX > mostX) return 0;

            into[0] = new RowRun(leastX - slackM, mostX + slackM);
            return 1;
        }

        double radiusM = 1f / arc.Curvature;
        var centreX = startX + (radiusM * across.X);
        var centreY = startY + (radiusM * across.Y);
        var roundingM = slackM + (Math.Abs(radiusM) * RadiusRoundingShare);
        var outerM = Math.Abs(radiusM) + halfM + roundingM;
        var innerM = Math.Abs(radiusM) - halfM - roundingM;

        var offRowM = yM - centreY;
        if (Math.Abs(offRowM) >= outerM) return 0;

        var outsideM = Math.Sqrt((outerM * outerM) - (offRowM * offRowM)) + slackM;
        var insideM = innerM > Math.Abs(offRowM) ? Math.Sqrt((innerM * innerM) - (offRowM * offRowM)) - slackM : 0.0;
        if (insideM <= 0.0)
        {
            into[0] = new RowRun(centreX - outsideM, centreX + outsideM);
            return 1;
        }

        into[0] = new RowRun(centreX - outsideM, centreX - insideM);
        into[1] = new RowRun(centreX + insideM, centreX + outsideM);
        return 2;
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
