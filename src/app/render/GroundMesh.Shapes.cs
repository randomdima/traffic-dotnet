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
        var steps = Steps(radius * MathF.PI * 0.5f);
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
    void Ribbon(ReadOnlySpan<ArcSeg> arcs, float halfWidthM, Surface surface, Vector3 tint, float[] periods)
    {
        if (halfWidthM <= 0f) return;

        var previous = -1;
        foreach (var arc in arcs)
        {
            var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / StepM(arc.Curvature)));
            for (var step = 0; step <= steps; step++)
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

    void Disc(Vector2 centreM, float radiusM, Surface surface, Vector3 tint, float[] periods)
    {
        if (radiusM <= 0f) return;

        var steps = Steps(radiusM);
        var centre = Vertex(centreM, surface, tint, periods);
        for (var step = 0; step <= steps; step++)
        {
            var angleRad = MathF.Tau * step / steps;
            Vertex(centreM + radiusM * new Vector2(MathF.Cos(angleRad), MathF.Sin(angleRad)), surface, tint, periods);
            if (step > 0) Triangle(centre, centre + step, centre + step + 1);
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
        var steps = Steps(insetRadiusM * MathF.Abs(sweep));
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

    static int Steps(float lengthM) => Math.Clamp((int)MathF.Ceiling(lengthM * 2f), 8, 96);

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
