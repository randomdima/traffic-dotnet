using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The boundary of the band every driven line lays</b> — its two edges and the square end at either end
/// of it — and <b>where one line's edge crosses another line's band</b>, solved
/// (<see cref="Spline.CrossingsM"/>) rather than walked.
/// </summary>
/// <remarks>
/// <para>
/// <b>A band's edge is its line offset by half a width, which is an arc about the same centre</b>: the
/// pieces keep the angle they subtend and move their radius, so where two of them cross is the same closed
/// form as where two lines cross and is exact to a float. <b>That place is where a stretch of the outside
/// ends</b> (<see cref="LaneShell"/>), and it is the one figure the shell cannot afford to guess at — two
/// stretches that stop at one crossing are recognised as meeting by how near their two ends stand.
/// </para>
/// <para>
/// <b>Walked instead, the error is the probe and not the station.</b> The walk asks whether a point a step
/// outside the edge stands on any other band, so however finely it is bisected it converges on where the
/// <em>probe</em> crossed — short of where the edge crosses by that step divided by however shallowly the
/// two bands meet, and a way out of a bay leaves the lane it serves at a few degrees. On Odesa that put the
/// median meeting 10 mm out, five hundred of them past the 150 mm that is one place, and the worst of them
/// 444 mm: a pairing radius wide enough to cover that is wide enough to hand a car park's bay to the wrong
/// neighbour.
/// </para>
/// <para>
/// <b>What it cannot answer it says so about.</b> A stretch can also stop where the ground itself stops
/// without any band's edge crossing there — the apron that rounds a junction corner is driven ground no
/// line lays a band on — and a line whose own bend is tighter than it is wide has no edge to offset. Both
/// come back <c>false</c> and the walk's own bisection stands.
/// </para>
/// </remarks>
internal sealed class BandEdges
{
    /// <summary>
    /// How many bands may stand at one point before the cut is left to the walk. The same figure the walk
    /// reads the ground with: six ways off one bay converge on a pose and a box carries a movement per pair
    /// of arms.
    /// </summary>
    const int MostBandsNear = 64;

    /// <summary>
    /// How many places one edge may cross one boundary piece before the rest are dropped. A line and the
    /// way out of the bay beside it cross where the way pulls out and again where it comes back; nothing in
    /// a town crosses one edge more often than a handful.
    /// </summary>
    const int MostCrossings = 8;

    /// <summary>
    /// How far either side of the walk's own answer a crossing is looked for. <b>It is the walk's error and
    /// not a tolerance on the answer</b>: bisecting a probe that stands a step outside the edge lands short
    /// of the crossing by that step divided by however shallowly the two bands meet, which on a city comes
    /// to a fraction of a metre — so the crossing that explains a cut is regularly outside the pair of
    /// stations that noticed it. Half the walk's own station is that with room.
    /// </summary>
    const float AboutM = 0.5f;

    readonly Paving _paving;
    readonly ChainIndex _bands;
    readonly float[] _halfM;
    readonly float[] _lengthM;
    readonly float _mostHalfM;

    /// <summary>Each line's two edges, or null where its own bend is tighter than its half width.</summary>
    readonly ArcSeg[]?[] _left;
    readonly ArcSeg[]?[] _right;

    public BandEdges(Paving paving, ChainIndex bands, float[] halfM, float[] lengthM, float mostHalfM)
    {
        _paving = paving;
        _bands = bands;
        _halfM = halfM;
        _lengthM = lengthM;
        _mostHalfM = mostHalfM;
        _left = new ArcSeg[halfM.Length][];
        _right = new ArcSeg[halfM.Length][];

        for (var line = 0; line < halfM.Length; line++)
        {
            var arcs = paving.ArcsOfDriven(line);
            if (arcs.Length == 0) continue;

            _left[line] = Offset(arcs, -halfM[line]);
            _right[line] = Offset(arcs, halfM[line]);
        }
    }

    /// <summary>
    /// <b>Where one edge of one line really crosses into another band</b>, given the place the walk put
    /// that crossing and the way it was heading: the nearest place to it that a band's own boundary
    /// explains. False where none does, which leaves the walk's answer standing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The walk says which crossing and the arithmetic says where it is</b>, which is the division of
    /// labour the corner already runs on (<c>LaneShell.Walk.Crossing</c>, <see cref="Spline.CrossingsM"/>).
    /// Two bands cross wherever they happen to and no distance says which of those a cut means; what says it
    /// is that the ground changed hands there, which is what the walk measured — to a fraction of a metre,
    /// which is plenty to pick a crossing and nowhere near enough to place one.
    /// </para>
    /// <para>
    /// <b>And a candidate is verified against the band whose boundary it is</b> (<see cref="Enters"/>).
    /// Several boundaries stand within a stride of a junction corner and only one of them is this cut, so a
    /// crossing is kept only where the edge is going <em>under</em> that band there rather than out from
    /// under it.
    /// </para>
    /// </remarks>
    public bool CutM(int line, bool onTheLeft, float aboutM, float towardsTheCoverM, out float atM)
    {
        atM = 0f;
        var edge = onTheLeft ? _left[line] : _right[line];
        if (edge is null) return false;

        var arcs = _paving.ArcsOfDriven(line);
        Span<int> near = stackalloc int[MostBandsNear];
        Span<float> alongM = stackalloc float[MostBandsNear];
        var found = _bands.Near(
            EdgeAt(arcs, line, onTheLeft, aboutM), _mostHalfM + Kerbs.JoinedM + AboutM, near, alongM);
        if (found > near.Length) return false;

        var into = MathF.Sign(towardsTheCoverM - aboutM);
        var bestM = float.PositiveInfinity;
        for (var at = 0; at < found; at++)
        {
            if (near[at] == line) continue;

            Cut(arcs, edge, near[at], alongM[at], aboutM, into, ref bestM);
        }

        if (float.IsPositiveInfinity(bestM)) return false;

        atM = bestM;
        return true;
    }

    /// <summary>
    /// The cut one other band makes about this place, kept where it stands nearer to it than whatever has
    /// been found.
    /// </summary>
    void Cut(
        ReadOnlySpan<ArcSeg> arcs, ArcSeg[] edge, int other, float atOtherM, float aboutM, int into,
        ref float bestM)
    {
        Span<ArcSeg> cap = stackalloc ArcSeg[1];
        Span<SplineCrossing> found = stackalloc SplineCrossing[MostCrossings];

        for (var side = 0; side < 4; side++)
        {
            var boundary = Boundary(other, side, cap);
            if (boundary.Length == 0) continue;

            var count = Spline.CrossingsM(edge, boundary, OnTheEdge(arcs, edge, aboutM), atOtherM, found);
            for (var at = 0; at < count; at++)
            {
                var crossing = found[at];
                var onLineM = OnTheLine(arcs, edge, crossing.OneM);
                if (MathF.Abs(onLineM - aboutM) > AboutM) continue;
                if (MathF.Abs(onLineM - aboutM) >= MathF.Abs(bestM - aboutM)) continue;
                if (!Enters(edge, crossing, into, other, side, boundary)) continue;

                bestM = onLineM;
            }
        }
    }

    /// <summary>
    /// <b>Whether the edge goes <em>under</em> the other band here rather than out from under it</b>: the
    /// way the edge is travelling, against the way that piece of boundary faces out.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the two directions and never of a point either side.</b> A crossing is where the edge
    /// stands on the boundary, so which side of it the band is on has to be read off how the two are
    /// pointing: a place a centimetre along a crossing two bands make at a couple of degrees stands a third
    /// of a millimetre inside, which at a town's coordinates is the last bit of a float — and the shallow
    /// crossing is the one this is for.
    /// </remarks>
    bool Enters(
        ReadOnlySpan<ArcSeg> edge, SplineCrossing at, int into, int other, int side,
        ReadOnlySpan<ArcSeg> boundary)
    {
        var travel = Spline.SampleAt(edge, at.OneM).Direction * into;
        return Vector2.Dot(travel, Outward(other, side, boundary, at.OtherM)) < 0f;
    }

    /// <summary>
    /// <b>Which way one piece of a band's boundary faces out of it</b>: away from the line for an edge, and
    /// off the end of the line for a square end (TER-3c.6).
    /// </summary>
    Vector2 Outward(int line, int side, ReadOnlySpan<ArcSeg> boundary, float atM)
    {
        var arcs = _paving.ArcsOfDriven(line);
        return side switch
        {
            0 => -Spline.SampleAt(boundary, atM).Right,
            1 => Spline.SampleAt(boundary, atM).Right,
            2 => -Spline.SampleAt(arcs, 0f).Direction,
            _ => Spline.SampleAt(arcs, _lengthM[line]).Direction,
        };
    }

    /// <summary>One of a band's four boundary pieces: its two edges, then the square end at either end of it.</summary>
    ReadOnlySpan<ArcSeg> Boundary(int line, int side, Span<ArcSeg> cap)
    {
        var left = _left[line];
        var right = _right[line];
        if (left is null || right is null) return default;

        return side switch
        {
            0 => left,
            1 => right,
            2 => Cap(left[0].StartM, right[0].StartM, cap),
            _ => Cap(left[^1].EndM, right[^1].EndM, cap),
        };
    }

    static ReadOnlySpan<ArcSeg> Cap(Vector2 fromM, Vector2 toM, Span<ArcSeg> into)
    {
        var runM = toM - fromM;
        var lengthM = runM.Length();
        if (lengthM <= Kerbs.RoundingM) return default;

        into[0] = new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), lengthM, 0f);
        return into;
    }

    /// <summary>
    /// A line offset by half its band, or null where a piece of it turns tighter than that — an edge that
    /// folds through itself is no edge, and the walk answers for the few lines that have one.
    /// </summary>
    static ArcSeg[]? Offset(ReadOnlySpan<ArcSeg> arcs, float offsetM)
    {
        var edge = new ArcSeg[arcs.Length];
        Spline.OffsetInto(arcs, offsetM, edge);
        foreach (var piece in edge)
        {
            if (piece.LengthM <= 0f) return null;
        }

        return edge;
    }

    /// <summary>
    /// <b>A distance along an edge said as a distance along the line it was offset from.</b> An offset piece
    /// subtends the angle its parent does, so the two run in step within a piece and the map is the ratio of
    /// their lengths — exactly, and not by projecting one onto the other.
    /// </summary>
    static float OnTheLine(ReadOnlySpan<ArcSeg> line, ReadOnlySpan<ArcSeg> edge, float atEdgeM) =>
        InStep(edge, line, atEdgeM);

    /// <summary>And a distance along the line said as a distance along one of its edges.</summary>
    static float OnTheEdge(ReadOnlySpan<ArcSeg> line, ReadOnlySpan<ArcSeg> edge, float atLineM) =>
        InStep(line, edge, atLineM);

    /// <summary>
    /// One chain's distance read off the other's, piece for piece. The last piece answers for everything
    /// past it, which is how a crossing solved off the end of a line comes back as a distance past its own
    /// end rather than clamped to it.
    /// </summary>
    static float InStep(ReadOnlySpan<ArcSeg> from, ReadOnlySpan<ArcSeg> to, float atM)
    {
        var fromPieceM = 0f;
        var toPieceM = 0f;
        for (var piece = 0; piece < from.Length; piece++)
        {
            var pastM = atM - fromPieceM;
            if (piece == from.Length - 1 || pastM <= from[piece].LengthM)
            {
                var scale = from[piece].LengthM > 0f ? to[piece].LengthM / from[piece].LengthM : 1f;
                return toPieceM + (pastM * scale);
            }

            fromPieceM += from[piece].LengthM;
            toPieceM += to[piece].LengthM;
        }

        return toPieceM;
    }

    Vector2 EdgeAt(ReadOnlySpan<ArcSeg> arcs, int line, bool onTheLeft, float atM)
    {
        var on = Spline.SampleAt(arcs, atM);
        return on.PositionM + (on.Right * (onTheLeft ? -_halfM[line] : _halfM[line]));
    }
}
