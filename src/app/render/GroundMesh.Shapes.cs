using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Render;

/// <summary>The shapes the ground is cut from, and the triangles and vertices they are written down as.</summary>
/// <remarks>
/// <b>A ring of the town's boundary, and the few things that are not one</b> — a rectangle, a ribbon about
/// a bridge, a mark on a curve. A layer of the ground is one region bounded by that boundary (TER-7b), so
/// what is cut into triangles here is a closed outline rather than a heap of pieces overlapping into one.
/// </remarks>
internal sealed partial class GroundMesh
{
    void Rect(Vector2 minM, Vector2 sizeM, Surface surface, Vector3 tint) =>
        Quad(
            Vertex(minM, surface, tint),
            Vertex(minM + new Vector2(sizeM.X, 0f), surface, tint),
            Vertex(minM + sizeM, surface, tint),
            Vertex(minM + new Vector2(0f, sizeM.Y), surface, tint));

    void OrientedRect(Vector2 centreM, Vector2 axis, Vector2 halfM, Surface surface, Vector3 tint)
    {
        if (halfM.X <= 0f || halfM.Y <= 0f) return;

        var along = axis.LengthSquared() > 0f ? Vector2.Normalize(axis) : Vector2.UnitX;
        var across = new Vector2(-along.Y, along.X);
        Quad(
            Vertex(centreM - along * halfM.X - across * halfM.Y, surface, tint),
            Vertex(centreM + along * halfM.X - across * halfM.Y, surface, tint),
            Vertex(centreM + along * halfM.X + across * halfM.Y, surface, tint),
            Vertex(centreM - along * halfM.X + across * halfM.Y, surface, tint));
    }

    /// <summary>
    /// <b>A line of the town's own, laid as the ground half a width either side of it</b>: a kerb along the
    /// driven ground's boundary or along the walk's outer face (TER-3d), and a bridge's deck along the road
    /// it carries. <b>The line it is struck from runs down the middle of it</b>, so no part of a stroke ever
    /// stands further from that line than half its own width — at a bend, at a corner and at the tightest
    /// hook the boundary has alike.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A width and not a layer.</b> It is laid over whatever the fills left along that line rather than
    /// cut out of them, so what it covers is exactly <paramref name="widthM"/> wherever the line runs.
    /// Struck as the ground between two offsets instead, a kerb is the difference between two shapes each
    /// thinned on its own terms, and what survives of it is whatever the thinning left.
    /// </para>
    /// <para>
    /// <b>One run over the whole line and never a piece at a time.</b> Every cross-section is stitched to the
    /// one before it, and on a ring (<paramref name="closed"/>) the last to the first. Laid piece by piece
    /// instead, the run breaks at every joint.
    /// </para>
    /// <para>
    /// <b>A corner is one cross-section on the bisector wherever that stands for the turn</b>, and the swept
    /// fan where it does not (<see cref="Turned"/>). A cross-section laid on the bisector at the half-width
    /// pinches the ribbon to <c>w·cos ½θ</c> across the corner — which is the same figure a chord bows off
    /// the arc it stands for, so it is held to <see cref="ChordSagM"/> by the same count every other bend
    /// here is drawn by, and a corner that turns harder than that is fanned instead. <b>The two cost a quad
    /// and two</b>, so the cheap one is what a boundary's tens of thousands of corners are drawn as.
    /// </para>
    /// <para>
    /// <b>And neither leaves the disc of half a width about the corner</b>: a cross-section never reaches
    /// past the half-width, so both ends of every edge of the ribbon lie within it of the line and so
    /// therefore does the whole of that edge. <b>A mitre would not</b> — struck where the two offset lines
    /// meet, it stands <c>½w(sec ½θ − 1)</c> outside the line at every corner, which is a kerb seen to
    /// spike off its own line (TER-3d).
    /// </para>
    /// <para>
    /// <b>Where the line turns tighter than half the width, that side is what the turn affords</b>
    /// (<see cref="Reach"/>). The merge leaves hooks of a few centimetres' radius where a movement's ribbon
    /// folds through itself, and an edge laid at a constant half-width along one reaches the middle of the
    /// turn before it has run out — everything past that comes back on the far side of the line, which is the
    /// one thing a stroke may not do. It stops at the middle of the turn instead.
    /// </para>
    /// </remarks>
    void Stroke(
        ReadOnlySpan<Vector2> line, float widthM, bool closed, Surface surface, Vector3 tint)
    {
        if (line.Length < 2 || widthM <= 0f) return;

        var halfM = widthM * 0.5f;
        var last = line.Length - 1;
        var firstLeft = -1;
        var firstRight = -1;
        var previousLeft = -1;
        var previousRight = -1;

        void Station(Vector2 pointM, Vector2 across, Vector2 reach)
        {
            var left = Vertex(pointM - (across * reach.X), surface, tint);
            var right = Vertex(pointM + (across * reach.Y), surface, tint);
            if (previousLeft >= 0)
            {
                Quad(previousLeft, previousRight, right, left);
            }
            else
            {
                firstLeft = left;
                firstRight = right;
            }

            previousLeft = left;
            previousRight = right;
        }

        for (var at = 0; at <= last; at++)
        {
            var pointM = line[at];
            var arriving = closed || at > 0 ? Along(line[at > 0 ? at - 1 : last], pointM) : Vector2.Zero;
            var leaving = closed || at < last ? Along(pointM, line[at < last ? at + 1 : 0]) : Vector2.Zero;
            if (arriving == Vector2.Zero) arriving = leaving;
            if (leaving == Vector2.Zero) leaving = arriving;
            if (arriving == Vector2.Zero) continue;

            var turnRad = MathF.Atan2(Spline.Cross(arriving, leaving), Vector2.Dot(arriving, leaving));
            var reach = Reach(Turning(line, at, last, closed, turnRad), turnRad, halfM);
            var fromAcross = Heading.RightOf(arriving);
            var ontoAcross = Heading.RightOf(leaving);
            var bisector = fromAcross + ontoAcross;
            if (Steps(MathF.Max(reach.X, reach.Y), turnRad) <= 1 && bisector.LengthSquared() > Cusp)
            {
                Station(pointM, Vector2.Normalize(bisector), reach);
                continue;
            }

            Station(pointM, fromAcross, reach);
            Turned(pointM, fromAcross, reach, ontoAcross, reach, surface, tint,
                ref previousLeft, ref previousRight);
            Station(pointM, ontoAcross, reach);
        }

        // The seam of a ring is no corner at all: its first point was laid knowing what arrives at it, so
        // what is left is the quad back to it.
        if (closed && previousLeft >= 0 && firstLeft >= 0)
        {
            Quad(previousLeft, previousRight, firstRight, firstLeft);
        }
    }

    /// <summary>
    /// <b>Every ring of a line struck but the pieces asked to be left</b> — a bridge's end where it lands, the map's
    /// edge where the ground runs on past it — each run between two of those struck open, and a ring with none of them
    /// struck closed.
    /// </summary>
    void Stroke(
        ReadOnlySpan<Vector2[]> line, Func<Vector2, Vector2, bool> unstruck, float widthM, Surface surface, Vector3 tint)
    {
        var run = new List<Vector2>();
        foreach (var ring in line)
        {
            var count = ring.Length;
            var first = -1;
            for (var piece = 0; piece < count && first < 0; piece++)
            {
                if (unstruck(ring[piece], ring[(piece + 1) % count])) first = piece;
            }

            if (first < 0)
            {
                Stroke(ring, widthM, closed: true, surface, tint);
                continue;
            }

            for (var step = 1; step <= count; step++)
            {
                var piece = (first + step) % count;
                var from = ring[piece];
                var to = ring[(piece + 1) % count];
                if (unstruck(from, to))
                {
                    Struck();
                    continue;
                }

                if (run.Count == 0) run.Add(from);
                run.Add(to);
            }

            Struck();
        }

        void Struck()
        {
            if (run.Count >= 2) Stroke(CollectionsMarshal.AsSpan(run), widthM, closed: false, surface, tint);
            run.Clear();
        }
    }

    /// <summary>
    /// The same stroke along a chain of arcs, for a line no fill is cut to (a bridge's deck): the chain read
    /// as the points a stroke of this width is drawn through (<see cref="Stations"/>) and then laid as any
    /// other line.
    /// </summary>
    void Stroke(
        ReadOnlySpan<ArcSeg> line, float widthM, bool closed, Surface surface, Vector3 tint)
    {
        if (line.Length < 1 || widthM <= 0f) return;

        Stroke(Walked(line, widthM * 0.5f, closed), widthM, closed, surface, tint);
    }

    /// <summary>
    /// A chain read as the points a stroke of a half-width is drawn through: each piece's own start and the
    /// stations inside it, and the far end of the chain where it is not a ring whose end is its own start.
    /// </summary>
    static Vector2[] Walked(ReadOnlySpan<ArcSeg> line, float halfM, bool closed)
    {
        var pointsM = new List<Vector2>();
        foreach (var arc in line)
        {
            var stations = Stations(arc, halfM);
            for (var step = 0; step < stations; step++) pointsM.Add(arc.PointAtM(arc.LengthM * step / stations));
        }

        if (!closed) pointsM.Add(line[^1].PointAtM(line[^1].LengthM));

        return pointsM.ToArray();
    }

    /// <summary>The step from one point of a line to the next, as a direction, or nothing where they are one point.</summary>
    static Vector2 Along(Vector2 fromM, Vector2 ontoM)
    {
        var stepM = ontoM - fromM;
        var lengthM = stepM.Length();
        return lengthM > 0f ? stepM / lengthM : Vector2.Zero;
    }

    /// <summary>
    /// <b>The radius the line turns at one of its corners</b>, read off the length either side of it against
    /// the turn between them — which is what a radius is, and comes back as the piece's own on a line read
    /// from arcs at any sampling at all.
    /// </summary>
    /// <remarks>
    /// <b>What <see cref="Reach"/> asked an arc for directly</b>, and it has to be asked of the corner
    /// instead, a line being handed over already read as points. <b>The length either side and never the
    /// shorter of the two</b>: a thinned line leaves short chords wherever it kept two corners close
    /// together, and a corner is not a hook because the chord into it is short — read that way, an ordinary
    /// sharp corner between two long runs came back at a few centimetres of radius and the stroke narrowed
    /// to it, leaving the kerb short of its own width at a place the line does not turn tightly at all.
    /// </remarks>
    static float Turning(ReadOnlySpan<Vector2> line, int at, int last, bool closed, float turnRad)
    {
        var turn = MathF.Abs(turnRad);
        if (turn <= 0f) return float.PositiveInfinity;

        var alongM = 0f;
        var sides = 0;
        if (closed || at > 0)
        {
            alongM += Vector2.Distance(line[at > 0 ? at - 1 : last], line[at]);
            sides++;
        }

        if (closed || at < last)
        {
            alongM += Vector2.Distance(line[at], line[at < last ? at + 1 : 0]);
            sides++;
        }

        return sides == 0 ? float.PositiveInfinity : alongM / sides / turn;
    }

    /// <summary>
    /// <b>How far a stroke may be carried to either side of a corner</b>: half the width, unless the line
    /// turns that way tighter than that — in which case it is the turn's own radius, where that edge closes
    /// on the middle of the turn and there is no further to go. The left of the line first, then the right.
    /// </summary>
    /// <remarks>
    /// A turn to the right is positive, which is the side the centre of it is on, so it is the right edge
    /// that a right-hander closes in on.
    /// </remarks>
    static Vector2 Reach(float radiusM, float turnRad, float halfM)
    {
        if (radiusM >= halfM) return new Vector2(halfM, halfM);

        return turnRad > 0f ? new Vector2(halfM, radiusM) : new Vector2(radiusM, halfM);
    }

    /// <summary>
    /// How short the sum of two cross-sections is before the corner between them is a cusp with no bisector
    /// to lay one on: a hundredth, which is half a degree off turning right round.
    /// </summary>
    const float Cusp = 0.01f;

    /// <summary>
    /// <b>How many cross-sections one piece of a stroke is laid at</b>: as few as leave its outer edge bowing
    /// under <see cref="ChordSagM"/>, that being the widest circle anything about the piece is drawn on.
    /// </summary>
    /// <remarks>
    /// <b>Off the outer edge and not off the line</b>, because the edge drawn on the outside of a turn goes
    /// round a circle half a width wider than the line's — so a count taken off the line leaves the thing
    /// actually drawn bowing by the sag times how much wider that circle is, which on a hook is most of the
    /// width.
    /// </remarks>
    static int Stations(in ArcSeg piece, float halfM) =>
        Steps((1f / MathF.Abs(piece.Curvature)) + halfM, MathF.Abs(piece.LengthM * piece.Curvature));

    /// <summary>
    /// <b>The corner a stroke turns too hard to cross on one cross-section</b>: the cross-sections either
    /// side of it stand at one place on two headings, and what turns between them is the same cross-section
    /// swept about that place — laid as the chords that bow under <see cref="ChordSagM"/>, like every other
    /// bend here.
    /// </summary>
    /// <remarks>
    /// <b>The stroke at a corner is the whole of the two sectors the cross-section turns through</b>, and
    /// what is laid across them stands for that: one cross-section on the bisector covers their chords and
    /// leaves a notch of <c>½w(1 − cos ½θ)</c> on the outside, which is why it serves only while that notch
    /// is under the sag (<see cref="Stroke"/>). Past it the sweep is fanned, or a corner that turns right
    /// round loses the whole half-width. On the inside of the turn either leaves an overlap, which is what
    /// the ground there is.
    /// </remarks>
    void Turned(
        Vector2 cornerM, Vector2 fromAcross, Vector2 fromReach, Vector2 ontoAcross, Vector2 ontoReach,
        Surface surface, Vector3 tint, ref int previousLeft, ref int previousRight)
    {
        var turnRad = MathF.Atan2(Spline.Cross(fromAcross, ontoAcross), Vector2.Dot(fromAcross, ontoAcross));
        var fans = Steps(MathF.Max(MathF.Max(fromReach.X, fromReach.Y), MathF.Max(ontoReach.X, ontoReach.Y)),
            turnRad);
        var fromRad = MathF.Atan2(fromAcross.Y, fromAcross.X);
        for (var fan = 1; fan < fans; fan++)
        {
            var through = (float)fan / fans;
            var across = Heading.Unit(fromRad + (turnRad * through));
            var reach = Vector2.Lerp(fromReach, ontoReach, through);
            var left = Vertex(cornerM - (across * reach.X), surface, tint);
            var right = Vertex(cornerM + (across * reach.Y), surface, tint);
            Quad(previousLeft, previousRight, right, left);
            previousLeft = left;
            previousRight = right;
        }
    }

    /// <summary>
    /// One mark laid down a stretch of a road's own curve, a half-width either side of it — a lane dash
    /// that <b>bends with the bend it is on</b> rather than standing as the chord of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Piece by piece, each piece its own quad</b> rather than one strip of shared corners: a mark is
    /// read back out of the mesh four vertices at a time (<see cref="FirstMarkVertex"/>).
    /// </para>
    /// <para>
    /// <b>Two pieces meet on the cross-section they share</b>, and not each on its own tangent. Paint is
    /// the ground drawn through a multiplying tint, so a piece overlapping the next reads as a bright
    /// notch at the joint and one falling short of it as a nick out of the line — at every joint of every
    /// dash on the bend.
    /// </para>
    /// </remarks>
    void CurvedMark(
        ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float halfWidthM, Surface surface, Vector3 tint)
    {
        if (toM <= fromM || halfWidthM <= 0f) return;

        var previousM = Vector2.Zero;
        var previousAcrossM = Vector2.Zero;
        var laid = false;
        var walkedM = 0f;
        foreach (var arc in arcs)
        {
            var startM = MathF.Max(fromM - walkedM, 0f);
            var endM = MathF.Min(toM - walkedM, arc.LengthM);
            walkedM += arc.LengthM;
            if (endM <= startM) continue;

            // Past the first arc the walk starts at its second sample: the first stands where the last
            // arc's end did, so the piece between them is the one already laid.
            var steps = Math.Max(1, (int)MathF.Ceiling((endM - startM) / StepM(arc.Curvature)));
            for (var step = laid ? 1 : 0; step <= steps; step++)
            {
                var distanceM = startM + ((endM - startM) * step / steps);
                var headingRad = arc.HeadingAtRad(distanceM);
                var acrossM = new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad)) * halfWidthM;
                var pointM = arc.PointAtM(distanceM);
                if (laid)
                {
                    Quad(
                        Vertex(previousM - previousAcrossM, surface, tint),
                        Vertex(pointM - acrossM, surface, tint),
                        Vertex(pointM + acrossM, surface, tint),
                        Vertex(previousM + previousAcrossM, surface, tint));
                }

                previousM = pointM;
                previousAcrossM = acrossM;
                laid = true;
            }
        }
    }

    /// <summary>
    /// <b>A whole shell filled</b> (<see cref="ShellFill"/>): the ground inside its rings and none of the
    /// ground outside them, at the tolerance everything else here is sampled to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The shape and not its pieces.</b> A layer of the ground is one band of the town's boundary
    /// (TER-7b), and such a region encloses holes — a city block is one — which no heap of overlapping
    /// pieces can state and no single ring can either. What is handed
    /// over is every ring of it at once, and which of them is a hole is read off the winding it carries.
    /// </para>
    /// <para>
    /// <b>The line is read outside and handed in</b> (<see cref="ShellFill.Outline"/>), and it is the coarse
    /// of the two readings this layer is laid from (<see cref="Filled"/>): what a fill may be got wrong by
    /// is what the kerb over its boundary hides, so it carries the corners that budget leaves and not the
    /// ones the kerb's own line needs.
    /// </para>
    /// </remarks>
    void Shell(ReadOnlySpan<Vector2[]> outline, Surface surface, Vector3 tint)
    {
        var (pointsM, triangles) = ShellFill.Of(outline);
        if (triangles.Length == 0) return;

        var corners = new int[pointsM.Length];
        for (var at = 0; at < pointsM.Length; at++) corners[at] = Vertex(pointsM[at], surface, tint);

        for (var at = 0; at + 2 < triangles.Length; at += 3)
        {
            TriangleUnlessFlat(corners[triangles[at]], corners[triangles[at + 1]], corners[triangles[at + 2]]);
        }
    }

    /// <summary>
    /// A closed outline, cut into triangles by clipping ears off it — a ring of the town's boundary or of
    /// the water's. Both are concave — a river is nothing else, and so is a street grid — so a fan from any
    /// one vertex would paint over its own banks.
    /// </summary>
    /// <remarks>
    /// <b>The next ear is looked for past the last one and not from the start again.</b> An outline is
    /// walked at a chord's bow, so long stretches of it are convex and every vertex of them is an ear:
    /// searched from the start each time, the whole of it came off one corner as a fan of slivers a
    /// hundred deep, which is a triangulation nobody can read and a scan that is the square of the outline.
    /// Carried on past the ear just cut, the ring is thinned a vertex at a time all the way round.
    /// </remarks>
    void Polygon(ReadOnlySpan<Vector2> outline, Surface surface, Vector3 tint)
    {
        if (outline.Length < 3) return;

        var corners = new int[outline.Length];
        for (var at = 0; at < outline.Length; at++) corners[at] = Vertex(outline[at], surface, tint);

        var remaining = new List<int>(outline.Length);
        for (var i = 0; i < outline.Length; i++) remaining.Add(i);
        if (SignedArea(outline) < 0f) remaining.Reverse();

        var from = 0;
        var guard = remaining.Count * remaining.Count;
        while (remaining.Count > 3 && guard-- > 0)
        {
            var cut = false;
            for (var scanned = 0; scanned < remaining.Count; scanned++)
            {
                var i = (from + scanned) % remaining.Count;
                var a = remaining[(i + remaining.Count - 1) % remaining.Count];
                var b = remaining[i];
                var c = remaining[(i + 1) % remaining.Count];
                if (!IsEar(outline, remaining, a, b, c)) continue;

                Triangle(corners[a], corners[b], corners[c]);
                remaining.RemoveAt(i);

                // Past the vertex that took the ear's place, so the next ear is the one after next round
                // the ring rather than the one this cut just made of its neighbour.
                from = (i + 1) % remaining.Count;
                cut = true;
                break;
            }

            // A self-intersecting outline has no ear left to cut. Fanning the rest is visibly wrong
            // in one place rather than silently missing water everywhere, and the plan is what is
            // wrong in that case.
            if (!cut) break;
        }

        for (var i = 1; i + 1 < remaining.Count; i++)
        {
            Triangle(corners[remaining[0]], corners[remaining[i]], corners[remaining[i + 1]]);
        }
    }

    /// <summary>
    /// How far apart to sample an arc so its chord bows by no more than a drawing tolerance
    /// (<see cref="Spline.ChordForSagM"/>), which is the whole distance for a straight.
    /// </summary>
    /// <remarks>
    /// Not the plan's quarter-metre polyline tolerance, which is offered to a consumer that wants a
    /// polyline while anything that draws is told to use the arcs: two ribbons that meet along a bend,
    /// sampled a quarter of a metre inside their own offset curves and at different phases, leave a
    /// tapering sliver of the ground beneath showing between them.
    /// <b>And no floor under it.</b> A step that may not go below half a metre only ever binds on a piece
    /// tighter than a metre and a half of radius, which is where the sag asks for a finer step and not a
    /// coarser one — so the floor fired nowhere but on the pieces it was worst on, and a hook of a few
    /// centimetres came out as the single chord across it.
    /// </remarks>
    static float StepM(float curvature) => Spline.ChordForSagM(curvature, ChordSagM);

    /// <summary>
    /// How many chords an arc of this radius and sweep is drawn as: <b>as few as bow within the same
    /// tolerance a ribbon is sampled to</b> (<see cref="ChordSagM"/>, <see cref="StepM"/>).
    /// </summary>
    /// <remarks>
    /// <b>Off the sag and not off the arc's length</b>, because the two say different things about a small
    /// circle and the town is made of small circles: every corner of every car park is turned on a walk.
    /// Counted by length at two to the metre with a floor of eight, a quarter turn of a walk came out twice
    /// as fine as the straight it joins and a fifth of the city's ground went on the difference.
    /// <b>A radius no chord ever leaves comes back as one chord</b>, which is what a straight is and what a
    /// sweep under one step's worth of turn is.
    /// </remarks>
    static int Steps(float radiusM, float sweepRad)
    {
        var stepRad = 2f * MathF.Acos(Math.Clamp(1f - (ChordSagM / MathF.Max(radiusM, 1e-4f)), -1f, 1f));
        if (stepRad <= 0f || MathF.Abs(sweepRad) <= stepRad) return 1;

        return Math.Min((int)MathF.Ceiling(MathF.Abs(sweepRad) / stepRad), MostSteps);
    }

    /// <summary>How many chords one bend is ever drawn as, which a ring of a town's own size reaches.</summary>
    const int MostSteps = 96;

    /// <summary>A triangle, unless its three corners stand on one line and it covers nothing.</summary>
    void TriangleUnlessFlat(int a, int b, int c)
    {
        var aM = _vertices[a].PositionM;
        var bM = _vertices[b].PositionM;
        var cM = _vertices[c].PositionM;
        var twiceAreaM2 = ((bM.X - aM.X) * (cM.Y - aM.Y)) - ((bM.Y - aM.Y) * (cM.X - aM.X));
        if (MathF.Abs(twiceAreaM2) > OnePointM * OnePointM) Triangle(a, b, c);
    }

    static float SignedArea(ReadOnlySpan<Vector2> polygon)
    {
        var twice = 0f;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            twice += (polygon[j].X - polygon[i].X) * (polygon[j].Y + polygon[i].Y);
        }

        return twice * 0.5f;
    }

    static bool IsEar(ReadOnlySpan<Vector2> outline, List<int> remaining, int a, int b, int c)
    {
        if (Cross(outline[a], outline[b], outline[c]) <= 0f) return false;

        foreach (var other in remaining)
        {
            if (other == a || other == b || other == c) continue;
            if (InsideTriangle(outline[a], outline[b], outline[c], outline[other])) return false;
        }

        return true;
    }

    static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    static bool InsideTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 pointM) =>
        Cross(a, b, pointM) >= 0f && Cross(b, c, pointM) >= 0f && Cross(c, a, pointM) >= 0f;

    /// <summary>
    /// One corner of the ground, <b>and the same corner however many shapes ask for it</b>: a corner a shape
    /// asks for where one already stands — the same point, wearing the same surface and the same shade — is
    /// that one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a dedupe and not a seam.</b> Layers are drawn over one another, so nothing here depends on
    /// two shapes sharing a corner; what it buys is that the ribbon laid at a size and the ribbon laid a
    /// line's width inside it do not each carry their own copy of the stations they agree on.
    /// </para>
    /// <para>
    /// <b>A surface and a shade are part of which corner this is</b>, not decoration on it, because both
    /// are vertex attributes.
    /// </para>
    /// <para>
    /// <b>The marks are not welded</b> (<see cref="FirstMarkVertex"/>). A dash, a bar and a zebra's stripe are
    /// quads of four corners each, read back that way by anything asking what was painted, and a stripe that
    /// shared a corner with the one beside it would not be four corners any more.
    /// </para>
    /// </remarks>
    int Vertex(Vector2 positionM, Surface surface, Vector3 tint)
    {
        var shade = GroundVertex.Pack(tint, surface);
        if (!_welding)
        {
            _vertices.Add(new GroundVertex(positionM, shade));
            return _vertices.Count - 1;
        }

        var at = ((int)MathF.Round(positionM.X / OnePointM), (int)MathF.Round(positionM.Y / OnePointM), shade);
        if (_welds.TryGetValue(at, out var already)) return already;

        _welds[at] = _vertices.Count;
        _vertices.Add(new GroundVertex(positionM, shade));
        return _vertices.Count - 1;
    }

    /// <summary>Two triangles across four corners, wound the way they were given.</summary>
    void Quad(int a, int b, int c, int d)
    {
        TriangleUnlessFlat(a, b, c);
        TriangleUnlessFlat(a, c, d);
    }

    void Triangle(int a, int b, int c)
    {
        _indices.Add((uint)a);
        _indices.Add((uint)b);
        _indices.Add((uint)c);
    }
}
