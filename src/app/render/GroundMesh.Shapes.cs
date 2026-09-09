using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Render;

/// <summary>The shapes the ground is cut from, and the triangles and vertices they are written down as.</summary>
/// <remarks>
/// <b>Every one of them is a shape of the town at a size</b>, and none of them knows what is beside it.
/// A layer is the union of the shapes in it and a union is stated by drawing them over one another
/// (TER-7b), so nothing here trims, clips, cuts or hands over to a neighbour — the piece that is drawn
/// last is what shows.
/// </remarks>
internal sealed partial class GroundMesh
{
    void Rect(Vector2 minM, Vector2 sizeM, Surface surface, Vector3 tint, float[] periods) =>
        Quad(
            Vertex(minM, surface, tint, periods),
            Vertex(minM + new Vector2(sizeM.X, 0f), surface, tint, periods),
            Vertex(minM + sizeM, surface, tint, periods),
            Vertex(minM + new Vector2(0f, sizeM.Y), surface, tint, periods));

    void OrientedRect(Vector2 centreM, Vector2 axis, Vector2 halfM, Surface surface, Vector3 tint, float[] periods)
    {
        if (halfM.X <= 0f || halfM.Y <= 0f) return;

        var along = axis.LengthSquared() > 0f ? Vector2.Normalize(axis) : Vector2.UnitX;
        var across = new Vector2(-along.Y, along.X);
        Quad(
            Vertex(centreM - along * halfM.X - across * halfM.Y, surface, tint, periods),
            Vertex(centreM + along * halfM.X - across * halfM.Y, surface, tint, periods),
            Vertex(centreM + along * halfM.X + across * halfM.Y, surface, tint, periods),
            Vertex(centreM - along * halfM.X + across * halfM.Y, surface, tint, periods));
    }

    /// <summary>
    /// An oriented rectangle whose four corners are turned on an arc of <paramref name="radiusM"/>:
    /// <b>a rectangle grown by that radius</b>, since a corner of the growth is the radius swung round the
    /// corner it grew from. It is what a car park's wrap is (TER-3c.3, <c>GroundShapes.InRoundedRect</c>)
    /// and what a road's own end grows into (<see cref="Grown"/>).
    /// </summary>
    void RoundedRect(Vector2 centreM, Vector2 axis, Vector2 halfM, float radiusM, Surface surface, Vector3 tint,
        float[] periods)
    {
        if (halfM.X <= 0f || halfM.Y <= 0f) return;

        var radius = MathF.Min(radiusM, MathF.Min(halfM.X, halfM.Y));
        if (radius <= 0f)
        {
            OrientedRect(centreM, axis, halfM, surface, tint, periods);
            return;
        }

        var along = axis.LengthSquared() > 0f ? Vector2.Normalize(axis) : Vector2.UnitX;
        var across = new Vector2(-along.Y, along.X);
        var straightM = halfM - new Vector2(radius);
        var steps = Steps(radius, MathF.PI * 0.5f);
        var baseRad = MathF.Atan2(along.Y, along.X);
        var centre = Vertex(centreM, surface, tint, periods);
        var previous = -1;
        var first = -1;

        // One walk round the perimeter, turning each corner about the point its two straight sides run out
        // at: the quarter arcs sweep the same way as the walk, so the fan closes on the vertex it opened
        // with and the straights fall out as the chords between the arcs.
        foreach (var quadrant in (ReadOnlySpan<int>)[0, 1, 2, 3])
        {
            var signU = quadrant is 0 or 3 ? 1f : -1f;
            var signV = quadrant is 0 or 1 ? 1f : -1f;
            var pivotM = centreM + (along * (straightM.X * signU)) + (across * (straightM.Y * signV));
            for (var step = 0; step <= steps; step++)
            {
                var angleRad = baseRad + (MathF.PI * 0.5f * (quadrant + ((float)step / steps)));
                var at = Vertex(
                    pivotM + (radius * new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad))), surface, tint,
                    periods);
                if (previous >= 0) TriangleUnlessFlat(centre, previous, at);
                else first = at;

                previous = at;
            }
        }

        if (first >= 0 && previous >= 0) TriangleUnlessFlat(centre, previous, first);
    }

    /// <summary>
    /// A road's own curve, laid out to a half-width either side. The arcs are sampled to a chord's bow —
    /// well under a lane width, and what keeps a ribbon from showing a facet at every piece.
    /// </summary>
    /// <remarks>
    /// <b>A piece's first station is the last station of the piece before it</b>, so it is sampled once and
    /// not twice. Laid twice, a chain of more than one piece carries a strip of no width at every joint —
    /// triangles that draw nothing and that anything reading the mesh back has to know to throw away.
    /// </remarks>
    void Ribbon(ReadOnlySpan<ArcSeg> arcs, float halfWidthM, Surface surface, Vector3 tint, float[] periods)
    {
        if (halfWidthM <= 0f) return;

        var previousLeft = -1;
        var previousRight = -1;
        foreach (var arc in arcs)
        {
            var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / StepM(arc.Curvature)));
            for (var step = previousLeft < 0 ? 0 : 1; step <= steps; step++)
            {
                var distanceM = arc.LengthM * step / steps;
                var headingRad = arc.HeadingAtRad(distanceM);
                var across = new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad));
                var centreM = arc.PointAtM(distanceM);

                var left = Vertex(centreM - across * halfWidthM, surface, tint, periods);
                var right = Vertex(centreM + across * halfWidthM, surface, tint, periods);
                if (previousLeft >= 0) Quad(previousLeft, previousRight, right, left);

                previousLeft = left;
                previousRight = right;
            }
        }
    }

    /// <summary>
    /// A kerb fillet: the ground between the corner two arms leave and the arc that rounds it. The plan
    /// carries both tangent points and the centre the arc turns about, because a corner cannot be read back
    /// off any other shape.
    /// </summary>
    /// <remarks>
    /// <b><paramref name="insetM"/> moves the apex as well as the arc, and the two move opposite ways.</b>
    /// Only the arc is this shape's own boundary: its two straight sides are the arms' kerbs seen from the
    /// other side, and there the fillet has to reach a stroke <em>into</em> each arm or the arm's kerb line
    /// comes back up inside the junction, as far as the tangent point, where the paved ground is continuous
    /// and no kerb is. So the arc draws in and the apex draws out, each by a stroke measured square to the
    /// side it moves — and the arc centre standing on the bisector at <c>radius / sin(half the angle)</c> is
    /// what makes that offset <c>inset / radius</c> of the way from the arc's centre to the corner and out,
    /// whatever the angle between the arms.
    /// </remarks>
    void Fillet(Vector2 cornerM, Vector2 arcCentreM, float radiusM, Vector2 tangentAM, Vector2 tangentBM,
        float insetM, Surface surface, Vector3 tint, float[] periods)
    {
        if (radiusM <= 0f) return;

        var insetRadiusM = radiusM + insetM;
        if (insetRadiusM <= 0f) return;

        var from = MathF.Atan2(tangentAM.Y - arcCentreM.Y, tangentAM.X - arcCentreM.X);
        var to = MathF.Atan2(tangentBM.Y - arcCentreM.Y, tangentBM.X - arcCentreM.X);
        var sweep = to - from;
        while (sweep > MathF.PI) sweep -= MathF.Tau;
        while (sweep < -MathF.PI) sweep += MathF.Tau;

        var apexM = Vector2.Lerp(cornerM, arcCentreM, -insetM / radiusM);
        var steps = Steps(insetRadiusM, sweep);
        var corner = Vertex(apexM, surface, tint, periods);
        var previous = -1;
        for (var step = 0; step <= steps; step++)
        {
            var angleRad = from + (sweep * step / steps);
            var at = Vertex(
                arcCentreM + (insetRadiusM * new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad))), surface, tint,
                periods);
            if (previous >= 0) TriangleUnlessFlat(corner, previous, at);

            previous = at;
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
        ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float halfWidthM, Surface surface, Vector3 tint,
        float[] periods)
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
                        Vertex(previousM - previousAcrossM, surface, tint, periods),
                        Vertex(pointM - acrossM, surface, tint, periods),
                        Vertex(pointM + acrossM, surface, tint, periods),
                        Vertex(previousM + previousAcrossM, surface, tint, periods));
                }

                previousM = pointM;
                previousAcrossM = acrossM;
                laid = true;
            }
        }
    }

    /// <summary>
    /// A water outline, cut into triangles by clipping ears off it. The outlines are concave — a river
    /// is nothing else — so a fan from any one vertex would paint over its own banks.
    /// </summary>
    /// <remarks>
    /// <b>The next ear is looked for past the last one and not from the start again.</b> An outline is
    /// walked at a chord's bow, so long stretches of it are convex and every vertex of them is an ear:
    /// searched from the start each time, the whole of it came off one corner as a fan of slivers a
    /// hundred deep, which is a triangulation nobody can read and a scan that is the square of the outline.
    /// Carried on past the ear just cut, the ring is thinned a vertex at a time all the way round.
    /// </remarks>
    void Polygon(ReadOnlySpan<Vector2> outline, Surface surface, Vector3 tint, float[] periods)
    {
        if (outline.Length < 3) return;

        var corners = new int[outline.Length];
        for (var at = 0; at < outline.Length; at++) corners[at] = Vertex(outline[at], surface, tint, periods);

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
    /// How far apart to sample an arc so its chord bows by no more than a drawing tolerance.
    /// </summary>
    /// <remarks>
    /// Not the plan's quarter-metre polyline tolerance, which is offered to a consumer that wants a
    /// polyline while anything that draws is told to use the arcs: two ribbons that meet along a bend,
    /// sampled a quarter of a metre inside their own offset curves and at different phases, leave a
    /// tapering sliver of the ground beneath showing between them.
    /// </remarks>
    static float StepM(float curvature)
    {
        var radiusM = 1f / MathF.Max(MathF.Abs(curvature), 1e-6f);
        return radiusM > 1e5f ? float.MaxValue : MathF.Max(0.5f, MathF.Sqrt(8f * ChordSagM * radiusM));
    }

    /// <summary>
    /// How many chords an arc of this radius and sweep is drawn as: <b>as few as bow within the same
    /// tolerance a ribbon is sampled to</b> (<see cref="ChordSagM"/>, <see cref="StepM"/>).
    /// </summary>
    /// <remarks>
    /// <b>Off the sag and not off the arc's length</b>, because the two say different things about a small
    /// circle and the town is made of small circles: every corner of every car park is turned on a walk.
    /// Counted by length at two to the metre with a floor of eight, a quarter turn of a walk came out twice
    /// as fine as the straight it joins and a fifth of the city's ground went on the difference.
    /// </remarks>
    static int Steps(float radiusM, float sweepRad)
    {
        var stepRad = 2f * MathF.Acos(Math.Clamp(1f - (ChordSagM / MathF.Max(radiusM, 1e-4f)), -1f, 1f));
        return Math.Clamp((int)MathF.Ceiling(MathF.Abs(sweepRad) / stepRad), 3, 96);
    }

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
    int Vertex(Vector2 positionM, Surface surface, Vector3 tint, float[] periods)
    {
        var period = surface == Surface.Paint ? 1f : periods[(int)surface];
        if (!_welding)
        {
            _vertices.Add(new GroundVertex(positionM, positionM / period, tint, surface));
            return _vertices.Count - 1;
        }

        var at = ((int)MathF.Round(positionM.X / OnePointM), (int)MathF.Round(positionM.Y / OnePointM), surface,
            (int)MathF.Round(tint.X * 1000f), (int)MathF.Round(tint.Y * 1000f), (int)MathF.Round(tint.Z * 1000f));
        if (_welds.TryGetValue(at, out var already)) return already;

        _welds[at] = _vertices.Count;
        _vertices.Add(new GroundVertex(positionM, positionM / period, tint, surface));
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
