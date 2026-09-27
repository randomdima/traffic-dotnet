using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>One closed shape with another taken out of it</b>: the ground inside the first and outside the second,
/// as the closed rings that bound it — so a shape with a hole in it comes back as a shape with a hole in it,
/// and a shape the cut took in two comes back as two.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every ring is walked with its ground on the walker's right</b>, going in and coming out
/// (<see cref="BandShell.Chains"/>), and <b>that convention is the whole of the construction</b>. The ground
/// the answer covers is the first shape's ground minus the second's, so a piece of the first shape's boundary
/// is on the answer where the second does not cover it, and a piece of the <em>second</em> shape's boundary
/// is on the answer where the first does — <b>walked the other way round</b>, because the ground the second
/// shape kept on its right is the ground this answer has just given up.
/// </para>
/// <para>
/// <b>So there are two questions and each is asked once.</b> Every piece of either boundary is cut at every
/// crossing it has with the other
/// (<see cref="Spline.CrossingsOf(in ArcSeg, in ArcSeg, Span{float}, Span{float})"/>), which is exactly where
/// the answer to "is this covered" changes; each stretch between two cuts is weighed at its own middle; and
/// what survives is strung into rings by the walk a merge and an offset both use
/// (<see cref="ArcRings.Of"/>). <b>There is no case in it</b> — a bite out of an edge, a hole punched clean
/// through the middle, a cut that severs the shape, a second shape standing entirely outside the first and
/// one swallowing it whole are one answer to one question.
/// </para>
/// <para>
/// <b>It takes the shapes as they stand and moves nothing.</b> A subtraction is not an inset: what comes out
/// is bounded by the two shapes' own lines, each at its own radius, so the layer an outset and a subtraction
/// make together is exactly the ground between the figure that struck the offset and the line it was struck
/// off, and its corners are the corners the town turns (TER-3c.3).
/// </para>
/// <para>
/// <b>Two boundaries that run along one another are outside what this answers.</b> A stretch of the first
/// shape's edge lying on a stretch of the second's is neither inside nor outside it, and which way the
/// arithmetic reads it is the last bits of a float's to say. Nothing here is a case for that, and nothing
/// needs to be while what is subtracted stands clear of what it is subtracted from — which an offset of a
/// shape always does, being a distance off it everywhere.
/// </para>
/// </remarks>
internal static class ArcSubtract
{
    /// <summary>
    /// <b>The ground <paramref name="fromRings"/> bounds with the ground <paramref name="takeRings"/> bounds
    /// taken out of it</b>, and the runs the walk could not close. <paramref name="level"/> is the level of the
    /// grid both shapes' pieces are binned at (<see cref="ChainIndex.OfPieces"/>) — the size of the features
    /// being asked about, which for a shape and an offset of it is the distance it was offset by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A run with two ends is a fault and is handed back apart</b>, exactly as a merge and an offset hand
    /// theirs back (<see cref="BandShell.Loose"/>): the boundary of one closed shape less another is closed,
    /// so a run that would not close is a crossing this pass did not find.
    /// </para>
    /// <para>
    /// <b>What this one may lose at a place is both shapes' worth.</b> A hand-over is a place on the
    /// standing shape's boundary and a place on the cutting shape's at once, and each of them may have had
    /// a piece passed over for being too short to walk and a tail dropped against the same figure — so the
    /// walk is told twice <see cref="ArcRings.LeastLostM"/> rather than the once a construction cutting a
    /// single boundary loses.
    /// </para>
    /// </remarks>
    public static (ArcSeg[][] Rings, ArcSeg[][] Loose) Of(
        ReadOnlySpan<ArcSeg[]> fromRings, ReadOnlySpan<ArcSeg[]> takeRings, GridLevel level)
    {
        var from = ArcRings.Flat(fromRings);
        if (from.Length == 0) return ([], []);

        var take = ArcRings.Flat(takeRings);
        if (take.Length == 0) return (fromRings.ToArray(), []);

        var standing = new Shape(from, level);
        var cutting = new Shape(take, level);

        var fromCutM = new List<float>?[from.Length];
        var takeCutM = new List<float>?[take.Length];
        Crossed(from, take, cutting.Index, fromCutM, takeCutM);

        var kept = new List<ArcSeg>(from.Length + take.Length);
        var edges = new List<float>();
        for (var at = 0; at < from.Length; at++)
        {
            Stretches(kept, from[at], fromCutM[at], cutting, keepInside: false, reversed: false, edges);
        }

        for (var at = 0; at < take.Length; at++)
        {
            Stretches(kept, take[at], takeCutM[at], standing, keepInside: true, reversed: true, edges);
        }

        return ArcRings.Of(kept, level.Grid, ArcRings.LeastLostM * 2f);
    }

    /// <summary>
    /// <b>Every crossing the two boundaries have, filed against both pieces it cuts.</b> Only across the two
    /// shapes and never within one: each is already a boundary that does not cross itself, and a cut made
    /// where nothing changes hands is a stretch weighed twice for one answer.
    /// </summary>
    /// <remarks>
    /// <b>Each pair solved once and both sides cut by it.</b> Solved from each side in turn, the two readings
    /// of one crossing stop a hair apart and the ring breaks there — which is why the answer is written into
    /// both lists here rather than being found again from the other end.
    /// </remarks>
    static void Crossed(
        ArcSeg[] from, ArcSeg[] take, ChainIndex takeIndex, List<float>?[] fromCutM, List<float>?[] takeCutM)
    {
        var candidate = new int[take.Length];
        Span<float> here = stackalloc float[2];
        Span<float> there = stackalloc float[2];

        for (var at = 0; at < from.Length; at++)
        {
            if (from[at].LengthM <= ArcRings.LeastPieceM) continue;

            var offered = takeIndex.Crossing(from.AsSpan(at, 1), 0f, candidate);
            for (var n = 0; n < offered && n < candidate.Length; n++)
            {
                var other = candidate[n];
                if (take[other].LengthM <= ArcRings.LeastPieceM) continue;

                var found = Spline.CrossingsOf(from[at], take[other], here, there);
                for (var cut = 0; cut < found; cut++)
                {
                    (fromCutM[at] ??= []).Add(here[cut]);
                    (takeCutM[other] ??= []).Add(there[cut]);
                }
            }
        }
    }

    /// <summary>
    /// <b>One piece split at its cuts, and each stretch of it kept or dropped on which side of the other
    /// shape its own middle stands</b> — handed on reversed where what is being walked is the shape coming
    /// out, whose ground is on the other hand once it is a hole.
    /// </summary>
    /// <remarks>
    /// <b>Asked at the middle, which is what the cutting is for.</b> Whether a place is covered changes
    /// exactly where the two boundaries cross, so a stretch between two crossings is covered along all of it
    /// or along none of it and one station settles it. <b>And two cuts nearer to each other than the shortest
    /// walkable stretch are one cut</b>, never a stretch to drop: two boundaries meeting tangentially cross
    /// wherever the last bits of a float say they do, and dropping the slivers between those leaves a hole as
    /// wide as the whole cluster (<see cref="ArcRings.LeastPieceM"/>).
    /// </remarks>
    static void Stretches(
        List<ArcSeg> kept, in ArcSeg piece, List<float>? cutAtM, Shape against, bool keepInside,
        bool reversed, List<float> edges)
    {
        if (piece.LengthM <= ArcRings.LeastPieceM) return;

        cutAtM?.Sort();
        edges.Clear();
        edges.Add(0f);
        var cuts = cutAtM?.Count ?? 0;
        for (var cut = 0; cut < cuts; cut++)
        {
            var atM = Math.Clamp(cutAtM![cut], 0f, piece.LengthM);
            if (atM - edges[^1] > ArcRings.LeastPieceM) edges.Add(atM);
        }

        // The piece's far end is an edge like any other, and a last stretch too short to be walked is the
        // cut before it that has to give way rather than the stretch itself.
        if (edges.Count > 1 && piece.LengthM - edges[^1] <= ArcRings.LeastPieceM) edges.RemoveAt(edges.Count - 1);

        edges.Add(piece.LengthM);
        Span<ArcSeg> one = stackalloc ArcSeg[1];
        Span<ArcSeg> turned = stackalloc ArcSeg[1];
        for (var at = 1; at < edges.Count; at++)
        {
            var fromM = edges[at - 1];
            var toM = edges[at];
            if (against.Covers(piece.PointAtM((fromM + toM) * 0.5f)) != keepInside) continue;

            one[0] = new ArcSeg(
                piece.PointAtM(fromM), piece.HeadingAtRad(fromM), toM - fromM, piece.Curvature);
            if (!reversed)
            {
                kept.Add(one[0]);
                continue;
            }

            Spline.ReverseInto(one, turned);
            kept.Add(turned[0]);
        }
    }

    /// <summary>
    /// <b>One shape as the two things a subtraction asks of it</b>: the pieces of boundary it is made of,
    /// all its rings in one numbering (<see cref="ArcRings.Flat"/>), and the lattice that says which of them
    /// is near a place or crosses a line (<see cref="ChainIndex.OfPieces"/>).
    /// </summary>
    readonly struct Shape
    {
        readonly ArcSeg[] _boundary;
        readonly int[] _near;
        readonly float[] _alongM;

        public Shape(ArcSeg[] boundary, GridLevel level)
        {
            _boundary = boundary;
            _near = new int[boundary.Length];
            _alongM = new float[boundary.Length];
            Index = ChainIndex.OfPieces(boundary, level);
        }

        public ChainIndex Index { get; }

        /// <summary>
        /// <b>Whether a place stands on the ground this shape covers</b>: the nearest point of its boundary
        /// is found, and the place is covered where it lies on that point's right — the hand every ring
        /// keeps its own ground on (<see cref="BandShell.Chains"/>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A distance has no sign and a boundary does</b>, which is why this is read off the direction
        /// rather than off how far away anything is: a place in the middle of a shape stands any distance
        /// from its edge, and a place the same distance outside it is the same number. It is the reading an
        /// offset settles its own side with (<see cref="ArcOutset"/>), asked here with no distance to bound
        /// it — so a shape already holed answers for its holes too, the piece nearest a place in one of them
        /// being that hole's own boundary and the place on its left.
        /// </para>
        /// <para>
        /// <b>Where the nearest point is a corner, every piece that owns it is asked and the readings are
        /// added</b>, which is the whole of what makes the answer a shape's rather than a piece's. A place
        /// off a corner that turns in on the ground stands on the ground side of one of the two pieces
        /// meeting there and on the far side of the other — <b>so either piece alone is a coin toss</b>, and
        /// the sum is the corner's own bisector, which is the direction the ground actually lies in.
        /// </para>
        /// </remarks>
        public bool Covers(Vector2 pointM)
        {
            var offered = Nearest(pointM, out var nearestSq);
            if (offered <= 0) return false;

            // Two pieces meeting at a corner compute the one point they share through their own arithmetic,
            // so what is one distance comes back as two that differ in the last bits of a float.
            var tiedM = MathF.Sqrt(nearestSq) + LineTolerance.RoundingM;
            var sideM = 0f;
            for (var at = 0; at < offered; at++)
            {
                ref readonly var piece = ref _boundary[_near[at]];
                var alongM = Math.Clamp(_alongM[at], 0f, piece.LengthM);
                var ontoM = piece.PointAtM(alongM);
                if (Vector2.Distance(ontoM, pointM) > tiedM) continue;

                sideM += Vector2.Dot(pointM - ontoM, Heading.RightOf(Heading.Unit(piece.HeadingAtRad(alongM))));
            }

            return sideM > 0f;
        }

        /// <summary>
        /// <b>Every piece of boundary near enough to a place to be the one it stands nearest</b>, left in
        /// <c>_near</c> and <c>_alongM</c>, and how far off the nearest of them is. The ring searched is
        /// grown out from the lattice's own cell until the best found is inside it — at which point nothing
        /// outside the ring can be nearer.
        /// </summary>
        /// <remarks>
        /// <b>Measured to the piece and not to the circle it lies on</b>, which is the whole reason this is
        /// here rather than <see cref="ChainIndex.Nearest"/>: that reading projects without clamping, so a
        /// long arc whose circle sweeps past a place beats the short piece the place actually stands beside.
        /// <b>Which side of the wrong piece a place is on is the wrong answer</b> — and the places it is
        /// wrong at are exactly the thin features a boundary is full of, where the pieces either side of a
        /// hairpin are a few centimetres apart and their circles are metres long.
        /// </remarks>
        int Nearest(Vector2 pointM, out float nearestSq)
        {
            nearestSq = float.MaxValue;
            if (_boundary.Length == 0) return 0;

            var window = Index.Window;
            var reachM = window.Level.CellM;
            var acrossM = (window.Width + window.Height) * window.Level.CellM;
            while (true)
            {
                var offered = Index.Near(pointM, reachM, _near, _alongM);
                if (offered > _near.Length) offered = _near.Length;

                nearestSq = float.MaxValue;
                for (var at = 0; at < offered; at++)
                {
                    ref readonly var piece = ref _boundary[_near[at]];
                    var alongM = Math.Clamp(_alongM[at], 0f, piece.LengthM);
                    nearestSq = MathF.Min(nearestSq, Vector2.DistanceSquared(piece.PointAtM(alongM), pointM));
                }

                if (offered > 0 && nearestSq <= reachM * reachM) return offered;
                if (reachM >= acrossM) return offered;

                reachM = MathF.Max(offered > 0 ? MathF.Sqrt(nearestSq) : 0f, reachM * 2f);
            }
        }
    }
}
