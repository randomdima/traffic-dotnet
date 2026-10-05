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
internal sealed class RibbonAtlas
{
    /// <summary>How finely a metre along a way is kept: a tenth, well inside half a lattice step.</summary>
    const float AlongPerMetre = 10f;

    /// <summary>What a whole reach outside a band is filed as (<see cref="Entry.Outside"/>).</summary>
    const float OutsidePerReach = byte.MaxValue;

    /// <summary>Below this curvature a piece's centre is further off than any town is wide, and it is read as a straight.</summary>
    const float StraightCurvature = 1e-6f;

    /// <summary>
    /// What two readings of one point's depth in a body may differ by in rounding alone, as a share of how far the
    /// body reaches from its centre — some hundreds of times what single-precision arithmetic over that reach can
    /// put between them.
    /// </summary>
    const float RoundingShare = 1f / 4096f;

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
    readonly record struct Entry(byte Outside, byte Column, ushort Along, int Way);

    /// <summary>
    /// <b>How many points of a row one start stands for</b>, as a shift: two, 1 m at the ribbon's level. A start a
    /// point reads only the body's own points at 257 starts a cell, the atlas's second largest table; a start a row
    /// reads past a car's box to the cell's edge on every row, and a crowded junction's point carries many ways.
    /// </summary>
    const int PointsAStartShift = 1;

    static int StartShiftOf(int shift) => Math.Min(PointsAStartShift, shift);

    /// <summary>A kept cell's starts: one a run of every row, and one past the last.</summary>
    static int StartsPerCellOf(int shift) => (1 << ((2 * shift) - StartShiftOf(shift))) + 1;

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
    /// <param name="channels">
    /// The channels the body is on (<see cref="IRibbonLines.ChannelsOf"/>): only the ways on one of them are found, so
    /// a car on a bridge stands on the bridge's lanes and not on those of the road passing under it.
    /// </param>
    /// <returns>How many ways were written; a way past the room given is dropped and counted.</returns>
    public int UnderBox(
        Vector2 centreM, Vector2 forward, float halfLengthM, float halfWidthM, Span<WayCover> into, byte channels = Ground) =>
        Widened(into, Read(new Box(centreM, forward, halfLengthM, halfWidthM), channels, into, out _));

    /// <summary>The same for a disc — a walker's collider.</summary>
    public int UnderDisc(Vector2 centreM, float radiusM, Span<WayCover> into, byte channels = Ground) =>
        Widened(into, Read(new Disc(centreM, radiusM), channels, into, out _));

    /// <summary>The channel of every way of a town with no bridge over its roads, and of the walk (<see cref="IRibbonLines.ChannelsOf"/>).</summary>
    const byte Ground = CityGen.CityPlan.RoadArrays.GroundChannel;

    /// <summary>The collision channels a way is on (<see cref="IRibbonLines.ChannelsOf"/>).</summary>
    public byte ChannelsOf(int way) => _wayChannels.Length > 0 ? _wayChannels[way] : Ground;

    /// <summary>
    /// <see cref="UnderBox(Vector2, Vector2, float, float, Span{WayCover}, byte)"/> <b>for one body of a roster</b>,
    /// kept in <paramref name="recall"/>: its last answer where no verdict that answer was made of can have changed
    /// since, and a fresh read where one can — the same to the bit either way. A body that has changed channels is
    /// read again.
    /// </summary>
    /// <returns>The ways, good until this body is next asked for.</returns>
    public ReadOnlySpan<WayCover> UnderBox(
        Vector2 centreM, Vector2 forward, float halfLengthM, float halfWidthM, Recall recall, int body, byte channels = Ground)
    {
        var box = new Box(centreM, forward, halfLengthM, halfWidthM);
        var asked = new Recall.Reading(centreM, forward, halfLengthM, halfWidthM, channels, 0f, 0);
        ref readonly var last = ref recall.Last[body];

        // A frozen body is asked about at the pose it was read at, and pays the comparison and nothing else.
        if (asked.SamePose(last)) return Recalled(box, kept: true, recall, body, asked);

        // No point inside the box is further from its centre than a corner is, so no depth in it moves by more
        // than the centre moved, the corner swung and either half grew.
        var reachM = MathF.Sqrt((halfLengthM * halfLengthM) + (halfWidthM * halfWidthM));
        var movedM = Vector2.Distance(centreM, last.CentreM) + (reachM * Vector2.Distance(forward, last.Forward))
                     + MathF.Abs(halfLengthM - last.HalfLengthM) + MathF.Abs(halfWidthM - last.HalfWidthM);
        var kept = channels == last.Channels && Unmoved(movedM, reachM, last.SlackM)
                   && SamePoints(box, new Box(last.CentreM, last.Forward, last.HalfLengthM, last.HalfWidthM));

        return Recalled(box, kept, recall, body, asked);
    }

    /// <summary>The same for a disc — a walker's collider, whose radius is kept as its half length.</summary>
    public ReadOnlySpan<WayCover> UnderDisc(Vector2 centreM, float radiusM, Recall recall, int body, byte channels = Ground)
    {
        var disc = new Disc(centreM, radiusM);
        var asked = new Recall.Reading(centreM, Vector2.Zero, radiusM, 0f, channels, 0f, 0);
        ref readonly var last = ref recall.Last[body];
        if (asked.SamePose(last)) return Recalled(disc, kept: true, recall, body, asked);

        var movedM = Vector2.Distance(centreM, last.CentreM) + MathF.Abs(radiusM - last.HalfLengthM);
        var kept = channels == last.Channels && Unmoved(movedM, radiusM, last.SlackM)
                   && SamePoints(disc, new Disc(last.CentreM, last.HalfLengthM));

        return Recalled(disc, kept, recall, body, asked);
    }

    /// <summary>How many ways have been dropped for want of room since the town was laid — a gate's figure.</summary>
    public long Dropped => _dropped;

    long _dropped;

    /// <summary>
    /// <b>What the atlas last found under each body of one roster</b>, the pose it found it at, and how near that
    /// reading came to another answer — so a body that has not moved far enough to change one is not read again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A body is not always exactly where it stood a tick ago</b>, and one creeping or settling moves by far less
    /// than would change what is under it. So an answer is kept against how far the body may move — the
    /// <see cref="Reading.SlackM"/> — and not against its pose alone; a frozen body (SOL-37) is the exact pose, and
    /// costs a comparison.
    /// </para>
    /// <para>
    /// <b>A read's answer is made of two things and nothing else</b>: which lattice points lie inside the body,
    /// and which of their ways the body reaches further past than the point stands outside them. The first is
    /// asked again exactly (<see cref="SamePoints"/>); the second cannot have turned while no depth has moved by
    /// the slack. A body at exactly the pose it was read at is neither: the same read, so the same answer.
    /// </para>
    /// <para>
    /// Laid once for a roster's capacity, a body's room is <see cref="MostWaysUnderABody"/>, and a body over more
    /// ways than that is dropped and counted once for the reading rather than once a tick.
    /// </para>
    /// </remarks>
    public sealed class Recall
    {
        public Recall(int bodies)
        {
            Last = new Reading[bodies];
            Array.Fill(Last, Reading.Never);
            _covers = new WayCover[bodies * MostWaysUnderABody];
        }

        /// <summary>
        /// One body's last reading: the pose and the channels it was read at, the least any depth test it took stood
        /// from the other verdict, and how many ways it found.
        /// </summary>
        internal readonly record struct Reading(
            Vector2 CentreM, Vector2 Forward, float HalfLengthM, float HalfWidthM, byte Channels, float SlackM, int Count)
        {
            /// <summary>A body never read: it stands nowhere and has no slack, so it is always read.</summary>
            public static readonly Reading Never = new(new Vector2(float.NaN), Vector2.Zero, 0f, 0f, 0, 0f, 0);

            /// <summary>Whether this is the other's pose and channels exactly — a read of the same thing, so the same answer.</summary>
            public bool SamePose(in Reading other) =>
                CentreM == other.CentreM && Forward == other.Forward
                && HalfLengthM == other.HalfLengthM && HalfWidthM == other.HalfWidthM && Channels == other.Channels;
        }

        internal readonly Reading[] Last;

        readonly WayCover[] _covers;

        internal Span<WayCover> CoversOf(int body) => _covers.AsSpan(body * MostWaysUnderABody, MostWaysUnderABody);
    }

    /// <summary>
    /// Whether a body whose every depth has moved by at most <paramref name="movedM"/> is still on the side of
    /// every depth test its last reading took, less what the two readings' rounding may differ by.
    /// </summary>
    static bool Unmoved(float movedM, float reachM, float slackM) => movedM + (reachM * RoundingShare) < slackM;

    /// <summary>A body's last answer where <paramref name="kept"/>, and a fresh read, kept for next time, where not.</summary>
    ReadOnlySpan<WayCover> Recalled<TBody>(scoped in TBody body, bool kept, Recall recall, int index, Recall.Reading asked)
        where TBody : struct, IBody
    {
        var covers = recall.CoversOf(index);
        ref var last = ref recall.Last[index];
        if (kept) return covers[..last.Count];

        var count = Widened(covers, Read(body, asked.Channels, covers, out var slackM));
        last = asked with { SlackM = slackM, Count = count };
        return covers[..count];
    }

    /// <summary>
    /// Whether two bodies take in the same lattice points — the same rows, and the same columns of each — asked
    /// exactly as <see cref="Read"/> asks it.
    /// </summary>
    bool SamePoints<TBody>(in TBody one, in TBody other) where TBody : struct, IBody
    {
        var firstRow = _level.FirstMiddleFrom(one.LeastY);
        var lastRow = _level.LastMiddleTo(one.MostY);
        if (firstRow != _level.FirstMiddleFrom(other.LeastY) || lastRow != _level.LastMiddleTo(other.MostY)) return false;

        for (var row = firstRow; row <= lastRow; row++)
        {
            var y = _level.MiddleM(row);
            var crosses = one.Across(y, out var fromX, out var toX);
            if (crosses != other.Across(y, out var otherFromX, out var otherToX)) return false;
            if (!crosses) continue;

            if (_level.FirstMiddleFrom(fromX) != _level.FirstMiddleFrom(otherFromX)
                || _level.LastMiddleTo(toX) != _level.LastMiddleTo(otherToX))
            {
                return false;
            }
        }

        return true;
    }

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
    /// <param name="slackM">The least any of those depths stood from its way's outside, either side of it.</param>
    int Read<TBody>(in TBody body, byte channels, Span<WayCover> into, out float slackM) where TBody : struct, IBody
    {
        var wayChannels = _wayChannels;
        var found = MemoryMarshal.Cast<WayCover, Found>(into);
        var firstRow = _level.FirstMiddleFrom(body.LeastY);
        var lastRow = _level.LastMiddleTo(body.MostY);

        var slack = float.PositiveInfinity;
        var written = 0;
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
                        var outsideM = entry.Outside * _metresPerOutside;
                        slack = MathF.Min(slack, MathF.Abs(depthM - outsideM));
                        if (depthM <= outsideM) continue;
                    }

                    written = Grow(found, written, entry.Way, entry.Along);
                }
            }
        }

        slackM = slack;
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
    /// <param name="anyoneWalks">
    /// Whether the town stands anybody on foot. <b>Where nobody is, a walked way is filed only where the traffic
    /// crosses it</b> — a zebra's paint: the walk is read only under walkers and the claims they hold, and on a
    /// traced city it is half of every point. The marks are worked out from the ribbons, so they hold every way
    /// either way.
    /// </param>
    public static RibbonAtlas Lay(IRibbonLines lines, GridLevel level, float touchM, bool anyoneWalks)
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

        var cells = GridWindow.Over(main, leastM, mostM);
        var filed = Filed(lines, lengthM, level, cells, anyoneWalks);
        var marks = RibbonMarks.Of(lines, lengthM, stepM, touchM, main);

        return new RibbonAtlas(
            level, cells, filed.CellSlot, filed.SlotFirst, filed.PointStart, filed.Entries, lengthM, ChannelsOf(lines), marks,
            filed.Points, filed.MostAtAPoint);
    }

    /// <summary>What filing every way's points leaves the atlas: its kept cells, their starts and their entries.</summary>
    readonly record struct FiledCells(
        int[] CellSlot, int[] SlotFirst, ushort[] PointStart, Entry[] Entries, int Points, int MostAtAPoint);

    /// <summary>
    /// <b>Every way's points found and filed into the cells they fall in</b>, each cell in row, column and way order.
    /// </summary>
    /// <remarks>
    /// <b>A method of its own so that what filing works in is gone when it returns</b>: the blocks every point waits
    /// in (<see cref="Laying"/>) are as large as the entries, and held through the marks' own working set
    /// (<see cref="RibbonMarks"/>) the three together were the whole of an open's peak on a traced city.
    /// </remarks>
    static FiledCells Filed(IRibbonLines lines, float[] lengthM, GridLevel level, GridWindow cells, bool anyoneWalks)
    {
        var wayCount = lines.WayCount;
        var main = cells.Level;
        var shift = level.Depth - main.Depth;
        var reachM = ReachOf(level.CellM);

        // Every way's own points, found a way at a time on as many threads as there are, kept as what filing
        // them needs, and counted under their cells as they are found.
        var perWay = new ArraySegment<Filing>[wayCount];
        var cellCount = new int[cells.Count];
        InChunks.Over(
            wayCount,
            () => new Laying(cells, shift, reachM),
            (laying, way) =>
            {
                if (!anyoneWalks && !lines.IsDriven(way) && lines.ZebraOf(way) == RibbonMarks.NoZebra) return;

                var laid = laying.Kept(PointsOf(lines, way, lengthM[way], level, laying.Found));
                perWay[way] = laid;
                foreach (var filing in laid.AsSpan()) Interlocked.Increment(ref cellCount[filing.Cell]);
            });

        // Filed a cell at a time: the cells that hold any given their place, every point put in its cell in
        // whatever order the threads reach it, and each cell then put in row, column and way order — which
        // no two entries share, so the order they were filed in is gone.
        var cellSlot = new int[cells.Count];
        var slots = 0;
        for (var cell = 0; cell < cells.Count; cell++) cellSlot[cell] = cellCount[cell] > 0 ? slots++ : -1;

        var slotFirst = new int[slots + 1];
        for (var cell = 0; cell < cells.Count; cell++)
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

        // An entry's column carries the whole of its point's place in the cell until the cell is sorted on it, and
        // only then its column: a point's place is a byte (above), and a second table of them is the entries' size
        // over again.
        var total = slotFirst[slots];
        var mask = (1 << shift) - 1;
        var entries = new Entry[total];
        var cursor = (int[])slotFirst.Clone();
        InChunks.Over(
            wayCount,
            () => 0,
            (_, way) =>
            {
                foreach (var filing in perWay[way].AsSpan())
                {
                    var at = Interlocked.Increment(ref cursor[cellSlot[filing.Cell]]) - 1;
                    entries[at] = new Entry(filing.Outside, filing.InCell, filing.Along, way);
                }
            });

        // A cell's sort keys are made on the thread sorting it, in a buffer as long as the fullest cell, rather
        // than held for every entry of the town at once.
        var mostInACell = 0;
        for (var slot = 0; slot < slots; slot++) mostInACell = Math.Max(mostInACell, slotFirst[slot + 1] - slotFirst[slot]);

        var startsPerCell = StartsPerCellOf(shift);
        var pointStart = new ushort[slots * startsPerCell];
        var census = new (int Points, int MostAtAPoint)[slots];
        InChunks.Over(
            slots,
            () => new long[mostInACell],
            (keys, slot) =>
            {
                var first = slotFirst[slot];
                var count = slotFirst[slot + 1] - first;
                var cellKeys = keys.AsSpan(0, count);
                var cellEntries = entries.AsSpan(first, count);
                for (var at = 0; at < count; at++) cellKeys[at] = ((long)cellEntries[at].Column << 32) | (uint)cellEntries[at].Way;
                cellKeys.Sort(cellEntries);
                foreach (ref var entry in cellEntries) entry = entry with { Column = (byte)(entry.Column & mask) };
                census[slot] = Started(cellKeys, pointStart.AsSpan(slot * startsPerCell, startsPerCell), shift);
            });

        var points = 0;
        var mostAtAPoint = 0;
        foreach (var cell in census)
        {
            points += cell.Points;
            mostAtAPoint = Math.Max(mostAtAPoint, cell.MostAtAPoint);
        }

        return new FiledCells(cellSlot, slotFirst, pointStart, entries, points, mostAtAPoint);
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
    /// <b>One thread's lattice points</b>: every way it has laid, back to back in blocks and as they will be
    /// filed, so a way's points are a stretch of a block rather than an array of their own.
    /// </summary>
    /// <remarks>
    /// An array a way is tens of thousands of small allocations from every thread at once, and the workstation
    /// heap hands out room under one lock: on Odesa a third of the pass's CPU went to waiting for it.
    /// </remarks>
    sealed class Laying(GridWindow cells, int shift, float reachM)
    {
        /// <summary>How many points a block holds, unless one way's own need more.</summary>
        const int BlockPoints = 1 << 16;

        /// <summary>The one way being laid, found before it is kept.</summary>
        public readonly List<Sample> Found = [];

        readonly int _mask = (1 << shift) - 1;
        Filing[] _block = [];
        int _used;

        /// <summary>A way's points, filed into this thread's block, and where they now stand.</summary>
        public ArraySegment<Filing> Kept(ReadOnlySpan<Sample> points)
        {
            if (_used + points.Length > _block.Length)
            {
                _block = GC.AllocateUninitializedArray<Filing>(Math.Max(BlockPoints, points.Length));
                _used = 0;
            }

            var kept = _block.AsSpan(_used, points.Length);
            for (var at = 0; at < points.Length; at++)
            {
                var point = points[at];
                kept[at] = new Filing(
                    cells.IndexOf(point.X >> shift, point.Y >> shift),
                    (byte)(((point.Y & _mask) << shift) | (point.X & _mask)),
                    Outside(point.MarginM, reachM),
                    Filed(point.AlongM));
            }

            var segment = new ArraySegment<Filing>(_block, _used, points.Length);
            _used += points.Length;
            return segment;
        }
    }

    /// <summary>
    /// <b>One point as it is filed</b>: its main cell, which point of that cell, and its entry's two figures
    /// already worked out — under half a <see cref="Sample"/>, which is what every point of the town costs while it
    /// waits between being found and being filed.
    /// </summary>
    readonly record struct Filing(int Cell, byte InCell, byte Outside, ushort Along);

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
    static ReadOnlySpan<Sample> PointsOf(IRibbonLines lines, int way, float lengthM, GridLevel level, List<Sample> found)
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

        static int Order(Sample one, Sample other) =>
            one.Y != other.Y ? one.Y.CompareTo(other.Y)
            : one.X != other.X ? one.X.CompareTo(other.X)
            : one.OnItsPiece != other.OnItsPiece ? other.OnItsPiece.CompareTo(one.OnItsPiece)
            : other.MarginM.CompareTo(one.MarginM);

        // A piece finds its points in order, so a way of one piece, or of pieces that never reach one another's
        // points, is in order already. Only where no two points are equal can that skip the sort: one that is
        // handed equal points leaves them in an order of its own.
        var sorted = CollectionsMarshal.AsSpan(found);
        var inOrder = true;
        for (var at = 1; at < sorted.Length && inOrder; at++) inOrder = Order(sorted[at - 1], sorted[at]) < 0;
        if (!inOrder) found.Sort(Order);

        var kept = 0;
        foreach (var sample in sorted)
        {
            if (kept > 0 && sorted[kept - 1].X == sample.X && sorted[kept - 1].Y == sample.Y) continue;

            sorted[kept++] = sample;
        }

        return sorted[..kept];
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
