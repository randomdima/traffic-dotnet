using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>How far a point stands off a set of closed rings, and on which side</b> — negative on the side the
/// rings are walked with on their right, positive on their left, and <c>reachM</c> where none of them is
/// near enough to have an opinion.
/// </summary>
/// <remarks>
/// <para>
/// <b>One field for a set and not one test per ring.</b> The boundary of a region is every ring of it — one
/// round the outside and one round each hole — and no single ring answers whether a point is in the region:
/// a point can stand clear of the ring it was struck from and inside the one beside it. Asked ring by ring
/// that is the answer nobody computes, and what it costs is a line drawn across the very ground the ring
/// was meant to stay outside of.
/// </para>
/// <para>
/// <b>The side is the nearest piece's own hand and not a crossing count.</b> A ray cast over every ring is
/// the length of the boundary per query; the nearest piece is nine cells of a grid, and its hand is the
/// same answer wherever the rings are consistently wound. Where the nearest place is a corner rather than
/// a piece, <b>the pieces meeting there answer together</b> — their two outward normals summed — which is
/// what keeps the sign right in the wedge outside a sharp corner, where taking whichever piece a float
/// preferred reads inside as often as out.
/// </para>
/// <para>
/// <b>The cell is the reach</b>, so everything within one stands in the nine cells about the point. A
/// question asked further off than that is not asked: the answer comes back as the reach itself, which is a
/// caller's signal that the boundary had nothing to say rather than a distance it can trust.
/// </para>
/// <para>
/// Built once and asked from a tick: the build allocates, and a query allocates nothing.
/// </para>
/// </remarks>
internal sealed class RingField
{
    /// <summary>
    /// How near two pieces have to stand to one point to be one corner of it — a millimetre, which is what
    /// two computations of one distance disagree by.
    /// </summary>
    const float SameM = 1e-3f;

    /// <summary>What no grid may exceed however far a set is spread: the cell grows instead.</summary>
    const int MostCells = 1 << 22;

    readonly Vector2[] _fromM;
    readonly Vector2[] _toM;
    readonly int[] _cellStart;
    readonly int[] _entry;
    readonly Vector2 _originM;
    readonly float _inverseCellM;
    readonly int _width;
    readonly int _height;

    readonly float[] _standsOffM;

    /// <summary>The rings as the chains of arcs they are, read as the straights between each arc's ends.</summary>
    public RingField(ReadOnlySpan<ArcSeg[]> rings, float cellM)
        : this(Corners(rings), cellM)
    {
    }

    /// <summary>
    /// And as the runs of stations they were walked at, each run read as closed —
    /// <paramref name="standsOffM"/> giving how far off each piece the answer is measured <em>from</em>.
    /// </summary>
    /// <remarks>
    /// <b>A piece may stand off itself.</b> Where the rings are the lines a town is driven on rather than
    /// the edge of what they lay, each piece's own band stands between the two, and how wide that band is
    /// differs piece by piece — a lane's width along a lane, a space's along the way into a bay. Given
    /// those, what comes back is the <em>clearance</em>: how far a point stands off the nearest band rather
    /// than off the nearest line, which is the figure every rule about a boundary is actually written in.
    /// Measured as a bare distance instead, a point standing two metres clear of a bay was thrown out for
    /// standing three from a lane that wanted four and a half.
    /// </remarks>
    public RingField(ReadOnlySpan<Vector2[]> rings, float cellM, ReadOnlySpan<float[]> standsOffM = default)
    {
        var count = 0;
        foreach (var ring in rings)
        {
            if (ring.Length >= 2) count += ring.Length;
        }

        _fromM = new Vector2[count];
        _toM = new Vector2[count];
        _standsOffM = new float[count];
        var at = 0;
        var deepestM = 0f;
        var leastM = new Vector2(float.MaxValue);
        var mostM = new Vector2(float.MinValue);
        for (var ring = 0; ring < rings.Length; ring++)
        {
            var walked = rings[ring];
            if (walked.Length < 2) continue;

            var stands = standsOffM.IsEmpty ? [] : standsOffM[ring];
            for (var corner = 0; corner < walked.Length; corner++)
            {
                var next = (corner + 1) % walked.Length;
                _fromM[at] = walked[corner];
                _toM[at] = walked[next];

                // The wider of the two ends, so a piece spanning a step in the band is the band it has to
                // keep a reader clear of rather than the one it is about to become.
                _standsOffM[at] = stands.Length == 0
                    ? 0f
                    : MathF.Max(stands[corner], stands[next]);
                deepestM = MathF.Max(deepestM, _standsOffM[at]);
                leastM = Vector2.Min(leastM, walked[corner]);
                mostM = Vector2.Max(mostM, walked[corner]);
                at++;
            }
        }

        // A piece matters to a question asked a reach away when its own band reaches that far, so the cell
        // has to cover the reach and the deepest band together.
        cellM += deepestM;

        if (count == 0)
        {
            leastM = Vector2.Zero;
            mostM = Vector2.Zero;
        }

        var spanM = mostM - leastM;
        while ((((long)(spanM.X / cellM) + 1) * ((long)(spanM.Y / cellM) + 1)) > MostCells) cellM *= 2f;

        CellM = cellM - deepestM;
        _originM = leastM;
        _inverseCellM = 1f / cellM;
        _width = (int)(spanM.X * _inverseCellM) + 1;
        _height = (int)(spanM.Y * _inverseCellM) + 1;

        // A piece is written into every cell its own box touches, so a query reading the nine about it sees
        // every piece that could stand within a cell of it however that piece happens to lie.
        _cellStart = new int[(_width * _height) + 1];
        for (var piece = 0; piece < count; piece++)
        {
            var (leastCell, mostCell) = Box(piece);
            for (var y = leastCell.Y; y <= mostCell.Y; y++)
            {
                for (var x = leastCell.X; x <= mostCell.X; x++) _cellStart[(y * _width) + x + 1]++;
            }
        }

        for (var cell = 0; cell < _width * _height; cell++) _cellStart[cell + 1] += _cellStart[cell];

        var cursor = new int[_width * _height];
        _entry = new int[_cellStart[^1]];
        for (var piece = 0; piece < count; piece++)
        {
            var (leastCell, mostCell) = Box(piece);
            for (var y = leastCell.Y; y <= mostCell.Y; y++)
            {
                for (var x = leastCell.X; x <= mostCell.X; x++)
                {
                    var cell = (y * _width) + x;
                    _entry[_cellStart[cell] + cursor[cell]++] = piece;
                }
            }
        }
    }

    /// <summary>
    /// How far the answer reaches. A question asked further off than this comes back as this, which is not
    /// a distance.
    /// </summary>
    public float CellM { get; }

    /// <summary>Whether any ring was given at all — an empty field has nothing to say about anywhere.</summary>
    public bool Any => _fromM.Length > 0;

    /// <summary>
    /// <b>How far off the rings the point stands</b>, negative on the side they are walked with on their
    /// right.
    /// </summary>
    public float OffM(Vector2 pointM, float reachM) =>
        Nearest(pointM, reachM, out _, out var offM) ? offM : reachM;

    /// <summary>
    /// The same, with the place on the boundary it was measured to — which is what a caller pushing a point
    /// clear of the rings needs, since the way out is the way from there to here.
    /// </summary>
    public bool Nearest(Vector2 pointM, float reachM, out Vector2 footM, out float offM) =>
        Nearest(pointM, reachM, out footM, out offM, out _, out _);

    /// <summary>
    /// And with the way off the rings on their left — the direction a caller wanting to stand a distance
    /// clear of them moves in, which at a corner is the two pieces' normals together and so bisects the
    /// wedge.
    /// </summary>
    public bool Nearest(
        Vector2 pointM, float reachM, out Vector2 footM, out float offM, out Vector2 leftM, out float bandM)
    {
        footM = pointM;
        offM = reachM;
        leftM = Vector2.Zero;
        bandM = 0f;

        var clearestM = reachM;
        var outward = Vector2.Zero;
        var found = false;
        var atX = Math.Clamp((int)((pointM.X - _originM.X) * _inverseCellM), 0, _width - 1);
        var atY = Math.Clamp((int)((pointM.Y - _originM.Y) * _inverseCellM), 0, _height - 1);
        for (var y = Math.Max(0, atY - 1); y <= Math.Min(_height - 1, atY + 1); y++)
        {
            for (var x = Math.Max(0, atX - 1); x <= Math.Min(_width - 1, atX + 1); x++)
            {
                var cell = (y * _width) + x;
                for (var entry = _cellStart[cell]; entry < _cellStart[cell + 1]; entry++)
                {
                    Weigh(_entry[entry], pointM, ref clearestM, ref footM, ref outward, ref bandM, ref found);
                }
            }
        }

        if (!found) return false;

        var lengthM = outward.Length();
        leftM = lengthM > 0f ? outward / lengthM : Vector2.Zero;

        // A point standing on the boundary has no side, and nought is the right answer for it.
        offM = Vector2.Dot(pointM - footM, outward) < 0f ? -clearestM : clearestM;
        return true;
    }

    /// <summary>
    /// One piece weighed against the nearest found so far. <b>A piece nearer by more than a rounding takes
    /// the answer; one within a rounding of it joins in</b>, which is how a corner comes to answer with both
    /// of its pieces.
    /// </summary>
    void Weigh(
        int piece, Vector2 pointM, ref float clearestM, ref Vector2 footM, ref Vector2 outward,
        ref float bandM, ref bool found)
    {
        var fromM = _fromM[piece];
        var runM = _toM[piece] - fromM;
        var lengthSq = runM.LengthSquared();
        var alongM = lengthSq > 0f
            ? Math.Clamp(Vector2.Dot(pointM - fromM, runM) / lengthSq, 0f, 1f)
            : 0f;
        var atM = fromM + (runM * alongM);
        var clearM = Vector2.Distance(pointM, atM) - _standsOffM[piece];
        if (found && clearM > clearestM + SameM) return;

        // The far side is the piece's left, what the rings enclose standing on their right throughout.
        var left = new Vector2(runM.Y, -runM.X);
        if (lengthSq > 0f) left /= MathF.Sqrt(lengthSq);

        if (!found || clearM < clearestM - SameM)
        {
            clearestM = clearM;
            footM = atM;
            outward = left;
            bandM = _standsOffM[piece];
            found = true;
            return;
        }

        bandM = MathF.Max(bandM, _standsOffM[piece]);

        clearestM = MathF.Min(clearestM, clearM);
        outward += left;
    }

    static Vector2[][] Corners(ReadOnlySpan<ArcSeg[]> rings)
    {
        var corners = new Vector2[rings.Length][];
        for (var ring = 0; ring < rings.Length; ring++)
        {
            corners[ring] = new Vector2[rings[ring].Length];
            for (var arc = 0; arc < rings[ring].Length; arc++) corners[ring][arc] = rings[ring][arc].StartM;
        }

        return corners;
    }

    ((int X, int Y) Least, (int X, int Y) Most) Box(int piece) =>
        (Cell(Vector2.Min(_fromM[piece], _toM[piece])), Cell(Vector2.Max(_fromM[piece], _toM[piece])));

    (int X, int Y) Cell(Vector2 pointM) =>
    (
        Math.Clamp((int)((pointM.X - _originM.X) * _inverseCellM), 0, _width - 1),
        Math.Clamp((int)((pointM.Y - _originM.Y) * _inverseCellM), 0, _height - 1)
    );
}
