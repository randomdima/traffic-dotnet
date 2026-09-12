using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>The line that stands a fixed distance to one side of a closed ring</b>: the ring walked at stations,
/// each station moved sideways by the offset and every corner it turns away from brought round at that offset
/// (<see cref="Round"/>), whatever the move folded through the ring dropped, and what is left smoothed over
/// a window and laid back as a chain.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is not <see cref="Spline.OffsetInto"/>, and the difference is the whole of this file.</b> Offsetting
/// a chain piece by piece moves every piece and asks nothing about the rest of the line, which is exactly
/// right for a lane inside its own road and wrong for a ring: a corner turned the other way sweeps the
/// offset <em>through</em> the ring and comes back as a loop standing inside the shape, and two sides of a
/// gap narrower than twice the offset each produce a line past the other. What makes the answer a boundary
/// rather than a heap of offset pieces is the one rule <see cref="Of"/> enforces — <b>no point of it stands
/// nearer the ring than the offset</b> — and a point that breaks it is dropped rather than trimmed, so the
/// line closes across the fold instead of doubling back through it.
/// </para>
/// <para>
/// <b>Walked and not solved.</b> Where two offset pieces really cross is a solvable question and pairing up
/// which crossings bound the answer is not, so the distance rule is asked of stations along the line instead:
/// it needs no case for a fold inside a fold, and it says the same thing about a ring of ten pieces and a
/// ring of ten thousand. What it costs is that the answer stands within a station of where the fold really
/// closes, which is a centimetre or two on a line that exists to be looked at.
/// </para>
/// <para>
/// <b>Smoothing is a window and not a fillet</b>: each station is replaced by the mean of the line within
/// half a window either side of it, so <em>nothing the line does over less than the window survives</em> —
/// a corner the ring turned, and equally the notch left where a fold was dropped, which a corner rounding
/// would leave exactly as it found it. The line it leaves stands a little inside the offset on a bend, by
/// about <c>w²/24R</c>, which at a window of metres and a bend of tens of them is millimetres.
/// </para>
/// </remarks>
internal static class Extrusion
{
    /// <summary>
    /// How finely the ring is walked. A quarter of a metre: the fold the distance rule drops is bounded by
    /// the station, and everything this line is laid against — a kerb, a pavement, a fence — is drawn at
    /// centimetres.
    /// </summary>
    const float StationM = 0.25f;

    /// <summary>
    /// How far inside the offset a station may stand and still be kept. <b>It is a rounding and not a
    /// slack</b>: a point of the offset stands at exactly the offset from its own foot, and what this
    /// covers is that the ring is measured at stations rather than continuously. Read as a slack — a tenth
    /// of the offset, say — the line keeps a bite out of every corner it turns.
    /// </summary>
    const float FoldM = 0.02f;

    /// <summary>
    /// How far the laid chain may bow off the stations it was walked from. Two centimetres: the line is a
    /// polyline by construction and this is what decides how many pieces say so — a hundred for a ring
    /// round a town rather than the ten thousand stations it was walked at.
    /// </summary>
    const float SagM = 0.02f;

    /// <summary>Below this two stations are one place and the second lays no piece.</summary>
    const float ApartM = 1e-3f;

    /// <summary>What no grid may exceed however far a ring is spread: the cell grows instead.</summary>
    const int MostCells = 1 << 22;

    /// <summary>
    /// <b>The ring that stands <paramref name="offsetM"/> to one side of this one</b> — positive to the
    /// driver's right, as <see cref="Spline.OffsetInto"/> is — smoothed over <paramref name="smoothM"/>
    /// metres of its own length. Empty where the offset leaves nothing: a ring smaller than the offset it
    /// was moved inside of is not a ring.
    /// </summary>
    /// <remarks>
    /// <paramref name="ring"/> is read as closed, whether or not its last piece ends exactly where its
    /// first begins, and what comes back is closed.
    /// </remarks>
    public static ArcSeg[] Of(ReadOnlySpan<ArcSeg> ring, float offsetM, float smoothM)
    {
        if (ring.Length == 0) return [];

        var offsets = new float[ring.Length];
        Array.Fill(offsets, offsetM);
        ReadOnlySpan<ArcSeg[]> rings = [ring.ToArray()];
        ReadOnlySpan<float[]> arcOffsetM = [offsets];
        var extruded = Of(rings, arcOffsetM, smoothM);
        return extruded.Length > 0 ? extruded[0] : [];
    }

    /// <summary>
    /// <b>Every ring of a set moved to one side of itself, each piece of each ring by its own offset</b>
    /// — one answer per ring given, empty where the offset left that ring nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The offset is a figure per piece and not per ring</b>, because the ring the town's ground is said
    /// in runs down the <em>lines</em> cars are driven on rather than along the edge of what they lay
    /// (<c>LaneShell</c>): the edge stands half a band out, and a band is a lane's width along a lane and a
    /// space's width along the way into a bay. Every distance the town has is therefore this ring moved by
    /// that half-width plus the distance wanted, which is what makes two such lines exact offsets of one
    /// curve and their band exactly as wide as the difference between them. Where two pieces carry different
    /// offsets the line steps between them, which is what a step in the width of the ground <em>is</em>.
    /// </para>
    /// <para>
    /// <b>The fold rule is asked of the whole set and not of the ring being moved</b> (<see cref="Clear"/>):
    /// a town's ground has a ring round the outside and one round every block it encloses, and an offset
    /// that folded through a neighbour would otherwise stand while one that folded through itself was
    /// dropped. One grid over every station of every ring answers both, and costs the same.
    /// </para>
    /// </remarks>
    public static ArcSeg[][] Of(
        ReadOnlySpan<ArcSeg[]> rings, ReadOnlySpan<float[]> arcOffsetM, float smoothM)
    {
        var onTheRing = new List<Vector2>[rings.Length];
        var moved = new List<Vector2>[rings.Length];
        var movedOffsetM = new List<float>[rings.Length];
        var stations = 0;
        var deepestM = 0f;
        for (var at = 0; at < rings.Length; at++)
        {
            var ring = rings[at];
            var offsets = arcOffsetM[at];
            var lengthM = Spline.TotalLengthM(ring);
            if (ring.Length == 0 || lengthM <= StationM * 3f) continue;

            var room = (int)MathF.Ceiling(lengthM / StationM) + ring.Length;
            onTheRing[at] = new List<Vector2>(room);
            moved[at] = new List<Vector2>(room);
            movedOffsetM[at] = new List<float>(room);
            Walk(ring, offsets, onTheRing[at], moved[at], movedOffsetM[at]);
            stations += onTheRing[at].Count;
            foreach (var offsetM in offsets) deepestM = MathF.Max(deepestM, MathF.Abs(offsetM));
        }

        if (stations == 0) return [];

        // One grid over every station of every ring, at the deepest reach any of them will ask about — the
        // cell is the reach, so a shallower question still reads the nine cells it needs.
        var all = new Vector2[stations];
        var into = 0;
        foreach (var walked in onTheRing)
        {
            if (walked is null) continue;

            walked.CopyTo(all, into);
            into += walked.Count;
        }

        var grid = new Stations(all, MathF.Max(deepestM - FoldM, StationM));

        var extruded = new ArcSeg[rings.Length][];
        for (var at = 0; at < rings.Length; at++)
        {
            extruded[at] = [];
            if (onTheRing[at] is null) continue;

            var kept = Clear(
                grid, [.. onTheRing[at]], [.. moved[at]], [.. movedOffsetM[at]], arcOffsetM[at]);
            if (kept.Length < 3) continue;

            extruded[at] = Straights(Corners(Smoothed(kept, smoothM)));
        }

        return extruded;
    }

    /// <summary>
    /// One ring walked at stations: where each station stands on the ring, where the offset takes it, and
    /// how far that was — which <see cref="Clear"/> needs back, since a station's own offset is what says
    /// whether it is still the offset.
    /// </summary>
    static void Walk(
        ReadOnlySpan<ArcSeg> ring, ReadOnlySpan<float> arcOffsetM, List<Vector2> onTheRing,
        List<Vector2> moved, List<float> movedOffsetM)
    {
        for (var piece = 0; piece < ring.Length; piece++)
        {
            ref readonly var arc = ref ring[piece];
            var offsetM = arcOffsetM[piece];
            var stations = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / StationM));
            var stepM = arc.LengthM / stations;
            for (var station = 0; station < stations; station++)
            {
                var atM = station * stepM;
                var pointM = arc.PointAtM(atM);
                onTheRing.Add(pointM);
                moved.Add(pointM + (Heading.RightOf(Heading.Unit(arc.HeadingAtRad(atM))) * offsetM));
                movedOffsetM.Add(offsetM);
            }

            var next = (piece + 1) % ring.Length;
            Round(arc, ring[next], arcOffsetM[next], moved, movedOffsetM);
        }
    }

    /// <summary>
    /// <b>The turn the offset comes round at a corner the ring turns away from it</b>: the line swung about
    /// the corner itself at the offset, which is where the offset of a corner is. Cut off with a straight
    /// instead, a right angle loses a third of the distance the whole line exists to keep.
    /// </summary>
    /// <remarks>
    /// A corner the ring turns <em>into</em> the offset gets none: the two sides run past one another there
    /// and what settles them is the fold rule (<see cref="Clear"/>), not a line drawn between them. The
    /// corner is taken as the piece boundary itself rather than as a station, so the swing is about the
    /// point the answer is measured from and stands at the offset to the millimetre.
    /// </remarks>
    static void Round(
        in ArcSeg arriving, in ArcSeg leaving, float offsetM, List<Vector2> moved, List<float> movedOffsetM)
    {
        var arrivingRad = arriving.HeadingAtRad(arriving.LengthM);
        var turnRad = Spline.WrapRad(leaving.HeadingRad - arrivingRad);
        if (turnRad * offsetM >= 0f) return;

        var steps = (int)MathF.Ceiling(MathF.Abs(turnRad * offsetM) / StationM);
        for (var step = 1; step < steps; step++)
        {
            var alongRad = arrivingRad + (turnRad * step / steps);
            moved.Add(leaving.StartM + (Heading.RightOf(Heading.Unit(alongRad)) * offsetM));
            movedOffsetM.Add(offsetM);
        }
    }

    /// <summary>
    /// <b>The stations of the offset that are still the offset</b>, in the order they were walked: each one
    /// the distance from the ring, <em>on the side the offset took it</em>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The distance is to the ring and not to the station it came from</b>: a fold is where a far piece of
    /// the ring comes near, which nothing local can see.
    /// </para>
    /// <para>
    /// <b>And the side is not the distance.</b> A ring that runs out along a line and back — a slit into the
    /// shape, which a town's outline has wherever one lane's edge doubles back on itself — turns through half
    /// a circle at the tip, and the offset comes round that turn into the shape. Every point of that turn is
    /// honestly the offset from the ring and some of them are the offset from everything else too, so the
    /// distance keeps them and what is drawn is a spike across ground the ring was meant to be outside of.
    /// Which side is wanted is the ring's own hand: the shoelace says which way it is wound, and the sign of
    /// the offset says whether it was moved into what that encloses or out of it.
    /// </para>
    /// </remarks>
    static Vector2[] Clear(
        Stations grid, Vector2[] onTheRing, Vector2[] moved, float[] movedOffsetM,
        ReadOnlySpan<float> arcOffsetM)
    {
        // Asked of the ring's own shape rather than of its stations: a crossing count over ten thousand
        // quarter-metre segments is the same answer as one over the few hundred the shape really has, and a
        // point this test is asked about already stands an offset clear of both.
        var shapeM = Corners(onTheRing);
        var inside = MathF.Sign(Hand(arcOffsetM)) * MathF.Sign(TwiceOver(shapeM));

        var kept = new List<Vector2>(moved.Length);
        for (var station = 0; station < moved.Length; station++)
        {
            var pointM = moved[station];
            if (grid.Within(pointM, MathF.Abs(movedOffsetM[station]) - FoldM)) continue;
            if (inside != 0 && Inside(shapeM, pointM) != (inside > 0)) continue;

            kept.Add(pointM);
        }

        return [.. kept];
    }

    /// <summary>
    /// Which side of the ring it was moved to. <b>One ring is moved to one side</b>: a piece moved the
    /// other way from its neighbours is a fold the rule has no reading of, so the first piece that moved at
    /// all says which side the whole of it is on.
    /// </summary>
    static float Hand(ReadOnlySpan<float> arcOffsetM)
    {
        foreach (var offsetM in arcOffsetM)
        {
            if (offsetM != 0f) return offsetM;
        }

        return 0f;
    }

    /// <summary>
    /// Twice the area a closed polyline encloses, signed — <b>positive where it is walked with what it
    /// encloses on its right</b>, which is the hand a perimeter keeps the town on.
    /// </summary>
    static float TwiceOver(List<Vector2> cornersM)
    {
        var over = 0f;
        for (int at = 0, before = cornersM.Count - 1; at < cornersM.Count; before = at++)
        {
            over += (cornersM[before].X * cornersM[at].Y) - (cornersM[at].X * cornersM[before].Y);
        }

        return over;
    }

    /// <summary>
    /// Whether a place stands within a closed polyline, by the crossings a ray out of it makes — odd within
    /// and even without.
    /// </summary>
    static bool Inside(List<Vector2> cornersM, Vector2 pointM)
    {
        var inside = false;
        for (int at = 0, before = cornersM.Count - 1; at < cornersM.Count; before = at++)
        {
            var one = cornersM[at];
            var other = cornersM[before];
            if (one.Y > pointM.Y == other.Y > pointM.Y) continue;

            if (pointM.X < (((other.X - one.X) * (pointM.Y - one.Y)) / (other.Y - one.Y)) + one.X) inside = !inside;
        }

        return inside;
    }

    /// <summary>
    /// Each station replaced by the mean of the ones within half a window either side of it along the line,
    /// <b>each weighted by the metres it stands for</b> — so the stations either side of a dropped fold,
    /// which stand for the whole of it, weigh what the gap is long rather than what a station is.
    /// </summary>
    /// <remarks>
    /// The window is capped at half the ring, past which every station averages the whole of it and the
    /// answer is one point.
    /// </remarks>
    static Vector2[] Smoothed(Vector2[] pointM, float windowM)
    {
        var count = pointM.Length;
        var spanM = new float[count];
        var totalM = 0f;
        for (var at = 0; at < count; at++)
        {
            spanM[at] = (pointM[(at + 1) % count] - pointM[at]).Length();
            totalM += spanM[at];
        }

        var halfM = MathF.Min(windowM * 0.5f, totalM * 0.25f);
        if (halfM <= 0f) return pointM;

        var smoothed = new Vector2[count];
        for (var at = 0; at < count; at++)
        {
            var weight = Stands(spanM, at);
            var sum = pointM[at] * weight;
            Gather(pointM, spanM, at, 1, halfM, ref sum, ref weight);
            Gather(pointM, spanM, at, -1, halfM, ref sum, ref weight);
            smoothed[at] = sum / weight;
        }

        return smoothed;
    }

    /// <summary>
    /// The window walked one way out of a station, taking in each one it reaches. <b>Walked outwards rather
    /// than over a count of neighbours</b>: what the window holds is metres of line, and the stations are a
    /// step apart everywhere except across a dropped fold.
    /// </summary>
    static void Gather(
        Vector2[] pointM, float[] spanM, int at, int way, float halfM, ref Vector2 sum, ref float weight)
    {
        var count = pointM.Length;
        var alongM = 0f;
        for (var step = 1; step < count; step++)
        {
            alongM += spanM[Round(way > 0 ? at + step - 1 : at - step, count)];
            if (alongM > halfM) return;

            var other = Round(at + (way * step), count);
            var carries = Stands(spanM, other);
            sum += pointM[other] * carries;
            weight += carries;
        }
    }

    /// <summary>One step round a closed ring of stations, either way.</summary>
    static int Round(int at, int count) => ((at % count) + count) % count;

    /// <summary>How many metres of the line one station stands for: half the span either side of it.</summary>
    static float Stands(float[] spanM, int at) => (spanM[Round(at - 1, spanM.Length)] + spanM[at]) * 0.5f;

    /// <summary>
    /// <b>The corners of a run of stations</b> — the ones the shape would be missing without, a station
    /// within the sag of the straight between the two that bracket it saying nothing that straight does not.
    /// The run is read as closed and comes back closed, without the first corner written twice.
    /// </summary>
    static List<Vector2> Corners(Vector2[] pointM)
    {
        var count = pointM.Length;
        var corners = new List<Vector2> { pointM[0] };
        var anchor = 0;
        for (var at = 2; at <= count; at++)
        {
            if (Bows(pointM, anchor, at) <= SagM) continue;

            corners.Add(pointM[(at - 1) % count]);
            anchor = at - 1;
        }

        // Never a corner a rounding from the one it set off from: a ring whose last piece is a millimetre
        // long is one a reader sees a dot on.
        while (corners.Count > 1 && (corners[^1] - corners[0]).Length() <= ApartM) corners.RemoveAt(corners.Count - 1);

        return corners;
    }

    /// <summary>The corners laid as the closed chain of straights they are, shut on the one it set off from.</summary>
    static ArcSeg[] Straights(List<Vector2> cornersM)
    {
        var chain = new List<ArcSeg>(cornersM.Count + 1);
        for (var corner = 0; corner < cornersM.Count; corner++)
        {
            var fromM = cornersM[corner];
            var runM = cornersM[(corner + 1) % cornersM.Count] - fromM;
            var pieceM = runM.Length();
            if (pieceM <= ApartM) continue;

            chain.Add(new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), pieceM, 0f));
        }

        return chain.Count >= 3 ? [.. chain] : [];
    }

    /// <summary>How far the stations between two of them stand off the straight that would replace them.</summary>
    static float Bows(Vector2[] pointM, int anchor, int at)
    {
        var count = pointM.Length;
        var fromM = pointM[anchor];
        var run = pointM[at % count] - fromM;
        var runM = run.Length();
        if (runM <= ApartM) return float.MaxValue;

        var along = run / runM;
        var bowM = 0f;
        for (var station = anchor + 1; station < at; station++)
        {
            var off = pointM[station % count] - fromM;
            bowM = MathF.Max(bowM, MathF.Abs((off.X * along.Y) - (off.Y * along.X)));
        }

        return bowM;
    }

    /// <summary>
    /// The ring's own stations over a uniform grid, for the one question the fold rule asks of them:
    /// <b>whether anything at all stands within a reach of a point</b>. It answers with a yes rather than
    /// with what it found, so a busy corner costs the first station it meets and not the sixty that are
    /// there.
    /// </summary>
    sealed class Stations
    {
        readonly Vector2[] _pointM;
        readonly int[] _cellStart;
        readonly int[] _entry;
        readonly Vector2 _originM;
        readonly float _inverseCellM;
        readonly int _width;
        readonly int _height;

        /// <summary>
        /// The cell is the reach, so everything within one stands in the nine cells about it — which is what
        /// <see cref="Within"/> reads and the whole of why it is a grid.
        /// </summary>
        public Stations(Vector2[] pointM, float cellM)
        {
            _pointM = pointM;

            var leastM = new Vector2(float.MaxValue);
            var mostM = new Vector2(float.MinValue);
            foreach (var point in pointM)
            {
                leastM = Vector2.Min(leastM, point);
                mostM = Vector2.Max(mostM, point);
            }

            var spanM = mostM - leastM;
            while ((((long)(spanM.X / cellM) + 1) * ((long)(spanM.Y / cellM) + 1)) > MostCells) cellM *= 2f;

            _originM = leastM;
            _inverseCellM = 1f / cellM;
            _width = (int)(spanM.X * _inverseCellM) + 1;
            _height = (int)(spanM.Y * _inverseCellM) + 1;

            _cellStart = new int[(_width * _height) + 1];
            foreach (var point in pointM) _cellStart[Cell(point) + 1]++;

            for (var cell = 0; cell < _width * _height; cell++) _cellStart[cell + 1] += _cellStart[cell];

            var cursor = new int[_width * _height];
            _entry = new int[pointM.Length];
            for (var station = 0; station < pointM.Length; station++)
            {
                var cell = Cell(pointM[station]);
                _entry[_cellStart[cell] + cursor[cell]++] = station;
            }
        }

        /// <summary>Whether any station stands within <paramref name="reachM"/> of the point.</summary>
        public bool Within(Vector2 pointM, float reachM)
        {
            if (reachM <= 0f) return false;

            var reachSq = reachM * reachM;
            var atX = Math.Clamp((int)((pointM.X - _originM.X) * _inverseCellM), 0, _width - 1);
            var atY = Math.Clamp((int)((pointM.Y - _originM.Y) * _inverseCellM), 0, _height - 1);
            for (var y = Math.Max(0, atY - 1); y <= Math.Min(_height - 1, atY + 1); y++)
            {
                for (var x = Math.Max(0, atX - 1); x <= Math.Min(_width - 1, atX + 1); x++)
                {
                    var cell = (y * _width) + x;
                    for (var entry = _cellStart[cell]; entry < _cellStart[cell + 1]; entry++)
                    {
                        if ((_pointM[_entry[entry]] - pointM).LengthSquared() < reachSq) return true;
                    }
                }
            }

            return false;
        }

        int Cell(Vector2 pointM)
        {
            var x = Math.Clamp((int)((pointM.X - _originM.X) * _inverseCellM), 0, _width - 1);
            var y = Math.Clamp((int)((pointM.Y - _originM.Y) * _inverseCellM), 0, _height - 1);
            return (y * _width) + x;
        }
    }
}
