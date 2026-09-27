using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.World.Road;

/// <summary>One way under a body, and the stretch of it the body stands over, in that way's own metres.</summary>
internal readonly record struct WayCover(int Way, float FromM, float ToM);

/// <summary>
/// <b>What the atlas is laid from</b>: every way of the town's one numbering (<see cref="TownWays"/>) as its
/// line and the width it is travelled at. Handed over as data, so the atlas learns nothing about which
/// network drew a way.
/// </summary>
internal interface IRibbonLines
{
    int WayCount { get; }

    ReadOnlySpan<ArcSeg> LineOf(int way, out float widthM);
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
/// <b>The lattice samples a point and never a cell</b>, and it is what a body is read over. A point is filed
/// under every way whose band it lies within reach of (<see cref="ReachOf"/>), with how far outside the band
/// it stands; a body covers a way where a point inside it lies inside the band — or outside it by less than
/// the body reaches past the point. So a body over a band is never between two points, and a body up to a
/// band's edge is not on it: a car against the paint is on its own lane alone
/// (<see cref="SimConfig.RibbonLatticeStepM"/>).
/// </para>
/// <para>
/// <b>Only points some ribbon covers are stored</b>, a row at a time: each row holds one entry per way over
/// each of its covered points, in column order and then way order. A body's shape is a few rows of a few
/// columns, so a lookup is a binary search per row and a walk of what it found. An entry is nine bytes —
/// the column, the way, the metre along it to a tenth, and how far outside its band it stands — which is
/// what the town's memory is quoted at.
/// </para>
/// </remarks>
internal sealed class RibbonAtlas
{
    /// <summary>How finely a metre along a way is kept: a tenth, well inside half a lattice step.</summary>
    const float AlongPerMetre = 10f;

    /// <summary>What a whole reach outside a band is filed as (<see cref="_entryOutside"/>).</summary>
    const float OutsidePerReach = byte.MaxValue;

    /// <summary>Below this curvature a piece's centre is further off than any town is wide, and it is read as a straight.</summary>
    const float StraightCurvature = 1e-6f;

    readonly float _stepM;
    readonly Vector2 _originM;
    readonly int _rows;

    /// <summary>Where each row's entries begin; one past the last row is the total.</summary>
    readonly int[] _rowFirst;

    readonly ushort[] _entryColumn;
    readonly int[] _entryWay;
    readonly ushort[] _entryAlong;

    /// <summary>
    /// How far outside its way's band each point stands, in <see cref="OutsidePerReach"/>ths of the reach and
    /// rounded up — nothing for a point on the band.
    /// </summary>
    readonly byte[] _entryOutside;

    readonly float[] _lengthM;

    RibbonAtlas(
        float stepM, Vector2 originM, int rows, int[] rowFirst, ushort[] entryColumn, int[] entryWay,
        ushort[] entryAlong, byte[] entryOutside, float[] lengthM, WayCrossings marks, int points,
        int mostWaysAtAPoint)
    {
        _stepM = stepM;
        _originM = originM;
        _rows = rows;
        _rowFirst = rowFirst;
        _entryColumn = entryColumn;
        _entryWay = entryWay;
        _entryAlong = entryAlong;
        _entryOutside = entryOutside;
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
    public float StepM => _stepM;

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
    public int EntryCount => _entryWay.Length;

    /// <summary>The most ways any one lattice point lies under.</summary>
    public int MostWaysAtAPoint { get; }

    /// <summary>What the atlas holds, in bytes: the figure a town's memory is quoted against.</summary>
    public long Bytes => (4L * (_rowFirst.Length + _lengthM.Length)) + (9L * _entryWay.Length);

    /// <summary>
    /// <b>Every way a box stands over, and the stretch of each</b> — the collider of a car, at the pose the
    /// solver left it in. Lattice points inside the box are looked up, each counted where the box reaches
    /// further past it than it stands outside the way's band; the stretch is the least and most metre among
    /// them, widened by half a lattice step at each end and held to the way.
    /// </summary>
    /// <returns>How many ways were written; a way past the room given is dropped and counted.</returns>
    public int UnderBox(Vector2 centreM, Vector2 forward, float halfLengthM, float halfWidthM, Span<WayCover> into)
    {
        var right = Heading.RightOf(forward);
        var alongM = forward * halfLengthM;
        var acrossM = right * halfWidthM;
        Span<Vector2> corners =
        [
            centreM + alongM + acrossM, centreM + alongM - acrossM, centreM - alongM - acrossM,
            centreM - alongM + acrossM,
        ];

        var box = new Box(centreM, forward, right, halfLengthM, halfWidthM);
        var leastY = MathF.Min(MathF.Min(corners[0].Y, corners[1].Y), MathF.Min(corners[2].Y, corners[3].Y));
        var mostY = MathF.Max(MathF.Max(corners[0].Y, corners[1].Y), MathF.Max(corners[2].Y, corners[3].Y));
        var written = 0;
        for (var row = RowAtOrAbove(leastY); row <= RowAtOrBelow(mostY); row++)
        {
            if (!AcrossTheBox(corners, RowY(row), out var fromX, out var toX)) continue;

            written = Gather(row, fromX, toX, box, into, written);
        }

        return Widened(into, written);
    }

    /// <summary>The same for a disc — a walker's collider.</summary>
    public int UnderDisc(Vector2 centreM, float radiusM, Span<WayCover> into)
    {
        var disc = new Disc(centreM, radiusM);
        var written = 0;
        for (var row = RowAtOrAbove(centreM.Y - radiusM); row <= RowAtOrBelow(centreM.Y + radiusM); row++)
        {
            var offY = RowY(row) - centreM.Y;
            var halfM = (radiusM * radiusM) - (offY * offY);
            if (halfM < 0f) continue;

            halfM = MathF.Sqrt(halfM);
            written = Gather(row, centreM.X - halfM, centreM.X + halfM, disc, into, written);
        }

        return Widened(into, written);
    }

    /// <summary>How many ways have been dropped for want of room since the town was laid — a gate's figure.</summary>
    public long Dropped => _dropped;

    long _dropped;

    /// <summary>A body's collider, as how far inside it a point lies.</summary>
    interface IBody
    {
        float DepthM(Vector2 pointM);
    }

    readonly struct Box(Vector2 centreM, Vector2 forward, Vector2 right, float halfLengthM, float halfWidthM) : IBody
    {
        public float DepthM(Vector2 pointM)
        {
            var offM = pointM - centreM;
            return MathF.Min(
                halfLengthM - MathF.Abs(Vector2.Dot(offM, forward)), halfWidthM - MathF.Abs(Vector2.Dot(offM, right)));
        }
    }

    readonly struct Disc(Vector2 centreM, float radiusM) : IBody
    {
        public float DepthM(Vector2 pointM) => radiusM - Vector2.Distance(pointM, centreM);
    }

    /// <summary>
    /// The ways under the points of one row between two x, each point counted only where the body reaches
    /// further past it than it stands outside that way's band — so ground the body and the band both hold
    /// lies between them, and a body whose edge only meets the band's is not on it.
    /// </summary>
    int Gather<TBody>(int row, float fromX, float toX, in TBody body, Span<WayCover> into, int written)
        where TBody : struct, IBody
    {
        var fromColumn = (int)MathF.Ceiling(((fromX - _originM.X) / _stepM) - 0.5f);
        var toColumn = (int)MathF.Floor(((toX - _originM.X) / _stepM) - 0.5f);
        if (toColumn < fromColumn) return written;

        var metresPerOutside = ReachOf(_stepM) / OutsidePerReach;
        var y = RowY(row);
        var last = _rowFirst[row + 1];
        for (var at = LowerBound(_entryColumn, _rowFirst[row], last, fromColumn);
             at < last && _entryColumn[at] <= toColumn;
             at++)
        {
            var outside = _entryOutside[at];
            if (outside != 0)
            {
                var pointM = new Vector2(_originM.X + ((_entryColumn[at] + 0.5f) * _stepM), y);
                if (body.DepthM(pointM) <= outside * metresPerOutside) continue;
            }

            written = Grow(into, written, _entryWay[at], _entryAlong[at] / AlongPerMetre);
        }

        return written;
    }

    int Grow(Span<WayCover> into, int written, int way, float alongM)
    {
        for (var slot = 0; slot < written; slot++)
        {
            if (into[slot].Way != way) continue;

            into[slot] = new WayCover(
                way, MathF.Min(into[slot].FromM, alongM), MathF.Max(into[slot].ToM, alongM));
            return written;
        }

        if (written == into.Length)
        {
            _dropped++;
            return written;
        }

        into[written] = new WayCover(way, alongM, alongM);
        return written + 1;
    }

    /// <summary>
    /// A point stands for the half step either side of it along the way, so the stretch a body covers is
    /// widened by that much — and held to the way's own two ends.
    /// </summary>
    int Widened(Span<WayCover> into, int written)
    {
        var halfM = _stepM * 0.5f;
        for (var slot = 0; slot < written; slot++)
        {
            ref readonly var cover = ref into[slot];
            into[slot] = cover with
            {
                FromM = MathF.Max(0f, cover.FromM - halfM),
                ToM = MathF.Min(_lengthM[cover.Way], cover.ToM + halfM),
            };
        }

        return written;
    }

    float RowY(int row) => _originM.Y + ((row + 0.5f) * _stepM);

    int RowAtOrAbove(float y) => Math.Max(0, (int)MathF.Ceiling(((y - _originM.Y) / _stepM) - 0.5f));

    int RowAtOrBelow(float y) => Math.Min(_rows - 1, (int)MathF.Floor(((y - _originM.Y) / _stepM) - 0.5f));

    /// <summary>Where one horizontal line crosses a convex quadrilateral, as the span of x inside it.</summary>
    static bool AcrossTheBox(ReadOnlySpan<Vector2> corners, float y, out float fromX, out float toX)
    {
        fromX = float.PositiveInfinity;
        toX = float.NegativeInfinity;
        for (var index = 0; index < corners.Length; index++)
        {
            var one = corners[index];
            var other = corners[(index + 1) % corners.Length];
            if ((y < one.Y && y < other.Y) || (y > one.Y && y > other.Y)) continue;

            if (one.Y == other.Y)
            {
                fromX = MathF.Min(fromX, MathF.Min(one.X, other.X));
                toX = MathF.Max(toX, MathF.Max(one.X, other.X));
                continue;
            }

            var x = one.X + ((y - one.Y) * (other.X - one.X) / (other.Y - one.Y));
            fromX = MathF.Min(fromX, x);
            toX = MathF.Max(toX, x);
        }

        return toX >= fromX;
    }

    static int LowerBound(ushort[] sorted, int from, int to, int value)
    {
        while (from < to)
        {
            var middle = (from + to) >>> 1;
            if (sorted[middle] < value) from = middle + 1;
            else to = middle;
        }

        return from;
    }

    /// <summary>
    /// <b>The atlas laid over every way of the town.</b> Build-time: it allocates freely and runs on as many
    /// threads as there are, and nothing it produces is written to again.
    /// </summary>
    /// <param name="stepM">How far apart the lattice's points stand (<see cref="SimConfig.RibbonLatticeStepM"/>).</param>
    /// <param name="touchM">
    /// How deep inside both two ribbons have to overlap before they are marked
    /// (<see cref="SimConfig.RibbonTouchM"/>) — what keeps two ribbons laid edge to edge from being marked.
    /// </param>
    public static RibbonAtlas Lay(IRibbonLines lines, float stepM, float touchM)
    {
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

        var originM = new Vector2(
            (MathF.Floor(leastM.X / stepM) - 1f) * stepM, (MathF.Floor(leastM.Y / stepM) - 1f) * stepM);
        var columns = (int)MathF.Ceiling((mostM.X - originM.X) / stepM) + 2;
        var rows = (int)MathF.Ceiling((mostM.Y - originM.Y) / stepM) + 2;

        // Every way's own points, found a way at a time on as many threads as there are.
        var perWay = new Sample[wayCount][];
        InChunks.Over(
            wayCount,
            () => new List<Sample>(),
            (found, way) => perWay[way] = PointsOf(lines, way, lengthM[way], originM, stepM, columns, rows, found));

        // Filed a row at a time: counted, placed, and then each row put in column and way order.
        var rowFirst = new int[rows + 1];
        foreach (var samples in perWay)
        {
            foreach (var sample in samples) rowFirst[(sample.Key / columns) + 1]++;
        }

        for (var row = 0; row < rows; row++) rowFirst[row + 1] += rowFirst[row];

        var total = rowFirst[rows];
        var keys = new long[total];
        var items = new long[total];
        var cursor = (int[])rowFirst.Clone();
        for (var way = 0; way < wayCount; way++)
        {
            foreach (var sample in perWay[way])
            {
                var at = cursor[sample.Key / columns]++;
                keys[at] = ((long)(sample.Key % columns) << 32) | (uint)way;
                items[at] = ((long)BitConverter.SingleToInt32Bits(sample.MarginM) << 32)
                            | (uint)BitConverter.SingleToInt32Bits(sample.AlongM);
            }

            perWay[way] = [];
        }

        InChunks.Over(
            rows,
            () => 0,
            (_, row) => Array.Sort(keys, items, rowFirst[row], rowFirst[row + 1] - rowFirst[row]));

        if (columns > ushort.MaxValue)
        {
            throw new InvalidOperationException($"a town {columns} lattice columns wide is wider than the atlas can file");
        }

        var entryColumn = new ushort[total];
        var entryWay = new int[total];
        var entryAlong = new ushort[total];
        var entryOutside = new byte[total];
        var reachM = ReachOf(stepM);
        var points = 0;
        var mostAtAPoint = 0;
        var atThePoint = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var at = rowFirst[row]; at < rowFirst[row + 1]; at++)
            {
                var column = (int)(keys[at] >> 32);
                var way = (int)(uint)keys[at];
                var alongM = BitConverter.Int32BitsToSingle((int)(uint)items[at]);
                if (at == rowFirst[row] || entryColumn[at - 1] != column)
                {
                    points++;
                    atThePoint = 0;
                }

                mostAtAPoint = Math.Max(mostAtAPoint, ++atThePoint);
                entryColumn[at] = (ushort)column;
                entryWay[at] = way;
                entryAlong[at] = Filed(alongM, lengthM[way]);

                // Rounded up, so that a body whose edge only meets the band's is never read as over it.
                var outsideM = reachM - BitConverter.Int32BitsToSingle((int)(items[at] >> 32));
                entryOutside[at] = (byte)Math.Clamp(MathF.Ceiling(outsideM / reachM * OutsidePerReach), 0f, OutsidePerReach);
            }
        }

        var marks = RibbonMarks.Of(lines, lengthM, stepM, touchM);

        return new RibbonAtlas(
            stepM, originM, rows, rowFirst, entryColumn, entryWay, entryAlong, entryOutside, lengthM, marks,
            points, mostAtAPoint);
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

    /// <summary>One lattice point a way's ribbon covers: where it is, how far along the way, and how far inside its edges.</summary>
    readonly record struct Sample(int Key, float AlongM, float MarginM);

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
    static Sample[] PointsOf(
        IRibbonLines lines, int way, float lengthM, Vector2 originM, float stepM, int columns, int rows,
        List<Sample> found)
    {
        found.Clear();
        var arcs = lines.LineOf(way, out var widthM);
        if (arcs.Length == 0 || widthM <= 0f || lengthM <= 0f) return [];

        var halfM = (widthM * 0.5f) + ReachOf(stepM);

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

            var fromColumn = Math.Max(0, (int)MathF.Ceiling(((leastM.X - originM.X) / stepM) - 0.5f));
            var toColumn = Math.Min(columns - 1, (int)MathF.Floor(((mostM.X - originM.X) / stepM) - 0.5f));
            var fromRow = Math.Max(0, (int)MathF.Ceiling(((leastM.Y - originM.Y) / stepM) - 0.5f));
            var toRow = Math.Min(rows - 1, (int)MathF.Floor(((mostM.Y - originM.Y) / stepM) - 0.5f));
            for (var row = fromRow; row <= toRow; row++)
            {
                var y = originM.Y + ((row + 0.5f) * stepM);
                for (var column = fromColumn; column <= toColumn; column++)
                {
                    var pointM = new Vector2(originM.X + ((column + 0.5f) * stepM), y);
                    if (!Foot(arc, pointM, out var onArcM, out var offM)) continue;
                    if (onArcM < -behindM || onArcM > arc.LengthM + pastM || offM >= halfM) continue;

                    // Reaching past a joint is never reaching past the chain's own ends, which are square
                    // however short the piece next to them is.
                    if (startM + onArcM < 0f || startM + onArcM > lengthM) continue;

                    found.Add(new Sample(
                        (row * columns) + column, startM + Math.Clamp(onArcM, 0f, arc.LengthM), halfM - offM));
                }
            }

            startM += arc.LengthM;
        }

        found.Sort(static (one, other) =>
            one.Key != other.Key ? one.Key.CompareTo(other.Key) : other.MarginM.CompareTo(one.MarginM));

        var kept = new List<Sample>(found.Count);
        foreach (var sample in found)
        {
            if (kept.Count > 0 && kept[^1].Key == sample.Key) continue;

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
