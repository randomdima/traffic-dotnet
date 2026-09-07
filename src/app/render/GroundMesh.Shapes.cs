using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Render;

/// <summary>The shapes the ground is cut from, and the triangles and vertices they are written down as.</summary>
internal sealed partial class GroundMesh
{
    void Rect(Vector2 minM, Vector2 sizeM, Surface surface, Vector3 tint, float[] periods)
    {
        var first = Vertex(minM, surface, tint, periods);
        Vertex(minM + new Vector2(sizeM.X, 0f), surface, tint, periods);
        Vertex(minM + sizeM, surface, tint, periods);
        Vertex(minM + new Vector2(0f, sizeM.Y), surface, tint, periods);
        Quad(first);
    }

    void OrientedRect(Vector2 centreM, Vector2 axis, Vector2 halfM, Surface surface, Vector3 tint, float[] periods)
    {
        var along = axis.LengthSquared() > 0f ? Vector2.Normalize(axis) : Vector2.UnitX;
        var across = new Vector2(-along.Y, along.X);
        var first = Vertex(centreM - along * halfM.X - across * halfM.Y, surface, tint, periods);
        Vertex(centreM + along * halfM.X - across * halfM.Y, surface, tint, periods);
        Vertex(centreM + along * halfM.X + across * halfM.Y, surface, tint, periods);
        Vertex(centreM - along * halfM.X + across * halfM.Y, surface, tint, periods);
        Quad(first);
    }

    /// <summary>
    /// An oriented rectangle whose four corners are turned on an arc of <paramref name="radiusM"/> —
    /// the pavement's own corner where the thing it wraps has a square one of its own.
    /// </summary>
    void RoundedRect(Vector2 centreM, Vector2 axis, Vector2 halfM, float radiusM, Surface surface, Vector3 tint,
        float[] periods)
    {
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
        var written = 0;

        // One walk round the perimeter, turning each corner about the point its two straight sides
        // run out at: the quarter arcs sweep the same way as the walk, so the fan closes on the
        // vertex it opened with and the straights fall out as the chords between the arcs.
        foreach (var quadrant in (ReadOnlySpan<int>)[0, 1, 2, 3])
        {
            var signU = quadrant is 0 or 3 ? 1f : -1f;
            var signV = quadrant is 0 or 1 ? 1f : -1f;
            var pivotM = centreM + (along * (straightM.X * signU)) + (across * (straightM.Y * signV));
            for (var step = 0; step <= steps; step++)
            {
                var angleRad = baseRad + (MathF.PI * 0.5f * (quadrant + ((float)step / steps)));
                Vertex(pivotM + (radius * new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad))), surface, tint, periods);
                written++;
                if (written > 1) Triangle(centre, centre + written - 1, centre + written);
            }
        }

        Triangle(centre, centre + written, centre + 1);
    }

    /// <summary>
    /// A road's own curve, laid out to a half-width either side. The arcs are sampled to a quarter of
    /// a metre of chord bow — well under a lane width, and
    /// what keeps a ribbon from showing a facet at every piece.
    /// </summary>
    /// <remarks>
    /// <b>A piece's first station is the last station of the piece before it</b>, so it is sampled once and
    /// not twice. Laid twice, a chain of more than one piece carries a strip of no width at every joint —
    /// triangles that draw nothing and that anything reading the mesh back has to know to throw away.
    /// </remarks>
    void Ribbon(ReadOnlySpan<ArcSeg> arcs, float halfWidthM, Surface surface, Vector3 tint, float[] periods)
    {
        if (halfWidthM <= 0f) return;

        var previous = -1;
        foreach (var arc in arcs)
        {
            var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / StepM(arc.Curvature)));
            for (var step = previous < 0 ? 0 : 1; step <= steps; step++)
            {
                var distanceM = arc.LengthM * step / steps;
                var headingRad = arc.HeadingAtRad(distanceM);
                var across = new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad));
                var centreM = arc.PointAtM(distanceM);

                var left = Vertex(centreM - across * halfWidthM, surface, tint, periods);
                Vertex(centreM + across * halfWidthM, surface, tint, periods);
                if (previous >= 0) Strip(previous, left);
                previous = left;
            }
        }
    }

    /// <summary>
    /// <b>The ground between two offsets of one line</b>, either or both of which may be to either side of
    /// it: the pavement as the band about the line it is walked down, and each of the two rims on it.
    /// </summary>
    /// <remarks>
    /// A ribbon that need not be centred, and the shape the pavement is (TER-3c.3). Struck at the arcs' own
    /// samples like every other strip here, so two skirts on one line — the band and the kerb line inside it
    /// — meet along the same chords rather than a sag apart.
    /// </remarks>
    void Skirt(
        ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, Surface surface, Vector3 tint, float[] periods)
    {
        if (fromM == toM) return;

        var previous = -1;
        foreach (var arc in arcs)
        {
            var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / StepM(arc.Curvature)));
            for (var step = previous < 0 ? 0 : 1; step <= steps; step++)
            {
                var distanceM = arc.LengthM * step / steps;
                previous = Station(arc.PointAtM(distanceM), arc.HeadingAtRad(distanceM), previous);
            }
        }

        int Station(Vector2 onM, float headingRad, int previous)
        {
            var across = new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad));
            var near = Vertex(onM + (across * fromM), surface, tint, periods);
            Vertex(onM + (across * toM), surface, tint, periods);
            if (previous >= 0) Strip(previous, near);

            return near;
        }
    }

    /// <summary>
    /// One side of a road's curve over one stretch of it, from <paramref name="outerM"/> inwards by
    /// <paramref name="widthM"/> — the ground a kerb line stands on, drawn again where no kerb is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It follows the road rather than the chord of it.</b> A car park is only offered where the kerb
    /// stays close to its own chord (GEN-4b), but close is not on it, and a straight patch over a bend
    /// leaves the line it was covering showing at both ends.
    /// </para>
    /// <para>
    /// <b>It reaches a chord's own sag past the line on both sides</b>, over ground that is the same
    /// surface either way. Two strips of one curve struck at different phases stand a sag apart at worst,
    /// and a hair of a line left showing still reads as the line — a kerb line is a hair wide to begin with.
    /// </para>
    /// </remarks>
    void EdgeStrip(
        ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float outerM, float widthM, Surface surface,
        Vector3 tint, float[] periods)
    {
        if (toM <= fromM || widthM <= 0f || outerM == 0f) return;

        var side = MathF.Sign(outerM);
        var innerM = outerM - (side * (widthM + ChordSagM));
        outerM += side * ChordSagM;
        var previous = -1;
        var walkedM = 0f;
        foreach (var arc in arcs)
        {
            var startM = MathF.Max(fromM - walkedM, 0f);
            var endM = MathF.Min(toM - walkedM, arc.LengthM);
            walkedM += arc.LengthM;
            if (endM <= startM) continue;

            // Struck between the arc's own samples and not this stretch's. What is being covered is a
            // chord between two of those, and a chord struck between any other pair of points leaves a
            // sliver of the line showing along the outside of a bend.
            var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / StepM(arc.Curvature)));
            var step = 0;
            var distanceM = startM;
            while (true)
            {
                var headingRad = arc.HeadingAtRad(distanceM);
                var across = new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad));
                var centreM = arc.PointAtM(distanceM);

                var inner = Vertex(centreM + across * innerM, surface, tint, periods);
                Vertex(centreM + across * outerM, surface, tint, periods);
                if (previous >= 0) Strip(previous, inner);
                previous = inner;

                if (distanceM >= endM) break;

                while (step <= steps && arc.LengthM * step / steps <= distanceM) step++;
                distanceM = step > steps ? endM : MathF.Min(arc.LengthM * step / steps, endM);
            }
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
                    var first = Vertex(previousM - previousAcrossM, surface, tint, periods);
                    Vertex(pointM - acrossM, surface, tint, periods);
                    Vertex(pointM + acrossM, surface, tint, periods);
                    Vertex(previousM + previousAcrossM, surface, tint, periods);
                    Quad(first);
                }

                previousM = pointM;
                previousAcrossM = acrossM;
                laid = true;
            }
        }
    }

    /// <summary>
    /// <b>The half-round that closes the end of a band</b>: everything within a radius of the place it
    /// stops at, on the side it faces. <b>Stepped along its own circumference</b>, as every other arc here
    /// is — walked by its radius a round of a pavement's width comes out a hexagon, and the flats read as
    /// a corner cut off the end of the concrete.
    /// </summary>
    void HalfRound(
        Vector2 centreM, float radiusM, Vector2 outwardM, Surface surface, Vector3 tint, float[] periods)
    {
        if (radiusM <= 0f) return;

        var steps = Steps(radiusM, MathF.PI);
        var fromRad = MathF.Atan2(outwardM.Y, outwardM.X) - (MathF.PI * 0.5f);
        var centre = Vertex(centreM, surface, tint, periods);
        for (var step = 0; step <= steps; step++)
        {
            var angleRad = fromRad + (MathF.PI * step / steps);
            Vertex(centreM + radiusM * new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad)), surface, tint, periods);
            if (step > 0) Triangle(centre, centre + step, centre + step + 1);
        }
    }

    /// <summary>
    /// <b>A band of tarmac grown by a distance</b>: the ribbon <paramref name="outM"/> wider on each side
    /// and the two square ends turned on the same figure (TER-3c.6). It is the pavement beside one piece
    /// of the town's tarmac, and the whole of it.
    /// </summary>
    void Band(
        ReadOnlySpan<ArcSeg> arcs, float halfM, float outM, Surface surface, Vector3 tint, float[] periods)
    {
        if (arcs.Length == 0) return;

        Ribbon(arcs, halfM + outM, surface, tint, periods);

        var last = arcs[^1];
        Cap(arcs[0].StartM, -arcs[0].StartUnit, halfM, outM, surface, tint, periods);
        Cap(last.EndM, Heading.Unit(last.HeadingAtRad(last.LengthM)), halfM, outM, surface, tint, periods);
    }

    /// <summary>
    /// <b>The ground that turns the square end of a band</b> (TER-3c.6): everything past that end and
    /// within <paramref name="outM"/> of it, which is the end grown by a distance — a straight run out,
    /// and a quarter turn about each of the two corners.
    /// </summary>
    /// <remarks>
    /// A fan from the middle of the end, which the shape is star-shaped about. It is the pavement's own
    /// answer drawn (<c>CityGen.GroundShapes.OffTheBandM</c>): a band grown by a distance turns its
    /// corners on that distance, so a ribbon squared off and left there is a bite of verge exactly where
    /// the walk wraps round.
    /// </remarks>
    void Cap(Vector2 endM, Vector2 outward, float halfM, float outM, Surface surface, Vector3 tint,
        float[] periods)
    {
        if (outM <= 0f) return;

        var right = new Vector2(-outward.Y, outward.X);
        var quarter = MathF.PI * 0.5f;
        var steps = Steps(outM, quarter);
        var middle = Vertex(endM, surface, tint, periods);
        var laid = 0;

        // Each corner in turn, and the boundary walked once from one side of the end to the other: the
        // corner to the left from square across the end round to straight out of it, the straight run
        // between the two, then the corner to the right on round to square across the other way.
        foreach (var side in (ReadOnlySpan<float>)[-1f, 1f])
        {
            var cornerM = endM + (halfM * side * right);
            for (var step = 0; step <= steps; step++)
            {
                var angleRad = quarter * (side < 0f ? step : steps - step) / steps;
                var atM = cornerM + (outM * ((MathF.Sin(angleRad) * outward) + (MathF.Cos(angleRad) * side * right)));
                Vertex(atM, surface, tint, periods);
                if (++laid > 1) Triangle(middle, middle + laid - 1, middle + laid);
            }
        }
    }

    /// <summary>
    /// A kerb fillet: the ground between the corner two arms leave and the arc that rounds it. The
    /// plan carries both tangent points and the centre the arc turns about, because a corner cannot
    /// be read back off any other shape.
    /// </summary>
    /// <remarks>
    /// <b><paramref name="insetM"/> moves the apex as well as the arc, and the two move opposite
    /// ways.</b> Only the arc is this shape's own boundary: its two straight sides are the arms'
    /// kerbs seen from the other side, and there the fillet has to reach a stroke *into* each arm or
    /// the arm's kerb line comes back up inside the junction, as far as the tangent point, where the
    /// paved ground is continuous and no kerb is. So the arc draws in and the apex draws out, each by
    /// a stroke measured square to the side it moves — and the arc centre standing on the bisector at
    /// <c>radius / sin(half the angle)</c> is what makes that offset <c>inset / radius</c> of the way
    /// from the arc's centre to the corner and out, whatever the angle between the arms.
    /// </remarks>
    void Fillet(Vector2 cornerM, Vector2 arcCentreM, float radiusM, Vector2 tangentAM, Vector2 tangentBM,
        float insetM, Surface surface, Vector3 tint, float[] periods)
    {
        if (radiusM <= 0f) return;

        var from = MathF.Atan2(tangentAM.Y - arcCentreM.Y, tangentAM.X - arcCentreM.X);
        var to = MathF.Atan2(tangentBM.Y - arcCentreM.Y, tangentBM.X - arcCentreM.X);
        var sweep = to - from;
        while (sweep > MathF.PI) sweep -= MathF.Tau;
        while (sweep < -MathF.PI) sweep += MathF.Tau;

        var apexM = Vector2.Lerp(cornerM, arcCentreM, -insetM / radiusM);
        var insetRadiusM = radiusM + insetM;
        var steps = Steps(insetRadiusM, sweep);
        var corner = Vertex(apexM, surface, tint, periods);
        for (var step = 0; step <= steps; step++)
        {
            var angleRad = from + sweep * step / steps;
            Vertex(arcCentreM + insetRadiusM * new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad)), surface, tint, periods);
            if (step > 0) Triangle(corner, corner + step, corner + step + 1);
        }
    }

    /// <summary>
    /// A water outline, cut into triangles by clipping ears off it. The outlines are concave — a river
    /// is nothing else — so a fan from any one vertex would paint over its own banks.
    /// </summary>
    void Polygon(ReadOnlySpan<Vector2> outline, Surface surface, Vector3 tint, float[] periods)
    {
        if (outline.Length < 3) return;

        var first = _vertices.Count;
        foreach (var pointM in outline) Vertex(pointM, surface, tint, periods);

        var remaining = new List<int>(outline.Length);
        for (var i = 0; i < outline.Length; i++) remaining.Add(i);
        if (SignedArea(outline) < 0f) remaining.Reverse();

        var guard = remaining.Count * remaining.Count;
        while (remaining.Count > 3 && guard-- > 0)
        {
            var cut = false;
            for (var i = 0; i < remaining.Count; i++)
            {
                var a = remaining[(i + remaining.Count - 1) % remaining.Count];
                var b = remaining[i];
                var c = remaining[(i + 1) % remaining.Count];
                if (!IsEar(outline, remaining, a, b, c)) continue;

                Triangle(first + a, first + b, first + c);
                remaining.RemoveAt(i);
                cut = true;
                break;
            }

            // A self-intersecting outline has no ear left to cut. Fanning the rest is visibly wrong
            // in one place rather than silently missing water everywhere, and the plan is what is
            // wrong in that case.
            if (!cut) break;
        }

        for (var i = 1; i + 1 < remaining.Count; i++) Triangle(first + remaining[0], first + remaining[i], first + remaining[i + 1]);
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
    /// circle and the town is made of small circles: every band's two ends are turned on a walk, and so is
    /// every corner of every car park. Counted by length at two to the metre with a floor of eight, a
    /// quarter turn of a walk came out twice as fine as the straight it joins and a fifth of the city's
    /// ground went on the difference.
    /// </remarks>
    static int Steps(float radiusM, float sweepRad)
    {
        var stepRad = 2f * MathF.Acos(Math.Clamp(1f - (ChordSagM / MathF.Max(radiusM, 1e-4f)), -1f, 1f));
        return Math.Clamp((int)MathF.Ceiling(MathF.Abs(sweepRad) / stepRad), 3, 96);
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

    int Vertex(Vector2 positionM, Surface surface, Vector3 tint, float[] periods)
    {
        var index = _vertices.Count;
        var period = surface == Surface.Paint ? 1f : periods[(int)surface];
        _vertices.Add(new GroundVertex(positionM, positionM / period, tint, surface));
        return index;
    }

    void Quad(int first)
    {
        Triangle(first, first + 1, first + 2);
        Triangle(first, first + 2, first + 3);
    }

    /// <summary>Two triangles between one pair of ribbon edges and the pair before it.</summary>
    void Strip(int previousLeft, int left)
    {
        Triangle(previousLeft, previousLeft + 1, left + 1);
        Triangle(previousLeft, left + 1, left);
    }

    void Triangle(int a, int b, int c)
    {
        _indices.Add((uint)a);
        _indices.Add((uint)b);
        _indices.Add((uint)c);
    }
}
