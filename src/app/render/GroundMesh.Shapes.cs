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
    /// <b>A whole cross-section struck at one set of stations</b>: the bands between consecutive
    /// <paramref name="offsetsM"/> of one curve, over the stretch <paramref name="fromM"/> to
    /// <paramref name="toM"/> of it, each band carrying its own surface and tint. <b>This is how ground is
    /// laid so that it does not overlap</b> (TER-7b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stations are walked once and every band is struck on them</b>, which is the whole of the
    /// primitive. Two bands laid as two ribbons meet along two different chains of chords —
    /// each curve is sampled to its own curvature — so they stand a chord's sag apart at worst, and the
    /// only ways to close that are to overlap them or to leave the ground beneath showing. Struck here they
    /// share the seam exactly, because the seam is one offset evaluated once.
    /// </para>
    /// <para>
    /// <b>The seam is one position and two vertices.</b> A surface is a vertex attribute, so the band on
    /// either side of a seam needs a vertex of its own there; what matters, and what is true, is that the
    /// two stand at the same point.
    /// </para>
    /// <para>
    /// <b>An offset equal to the one before it lays no band and is not an error</b> — it is how a section
    /// that carries no rim, or none of one side's pavement, is stated without a second shape of offsets.
    /// </para>
    /// </remarks>
    void Strips(
        ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, ReadOnlySpan<float> offsetsM,
        ReadOnlySpan<Surface> surfaces, ReadOnlySpan<Vector3> tints, float[] periods)
    {
        Stations(arcs, fromM, toM);
        if (_stations.Count < 2) return;

        for (var band = 0; band + 1 < offsetsM.Length; band++)
        {
            if (offsetsM[band] == offsetsM[band + 1]) continue;

            var previous = -1;
            var previousFar = -1;
            foreach (var (atM, across, curvature) in _stations)
            {
                var nearM = atM + (across * ShortOfTheCentre(offsetsM[band], curvature));
                var farM = atM + (across * ShortOfTheCentre(offsetsM[band + 1], curvature));

                // <b>A band read in to an arc's own centre closes on one point</b>: every station's near
                // corner is that point, to the sag the arc is drawn to, and the strip between two of them is
                // one triangle of a fan from the first of them rather than two with one of no area. Where
                // the far corners crowd as well the band is a short stretch and not a fan, and it stays a
                // strip: fanned, half of it was a hole.
                if (previous >= 0
                    && Vector2.DistanceSquared(_vertices[previous].PositionM, nearM) < ChordSagM * ChordSagM
                    && Vector2.DistanceSquared(_vertices[previousFar].PositionM, farM) >= ChordSagM * ChordSagM)
                {
                    var far = Vertex(farM, surfaces[band], tints[band], periods);
                    Triangle(previous, previousFar, far);
                    previousFar = far;
                    continue;
                }

                var near = Vertex(nearM, surfaces[band], tints[band], periods);
                var nearFar = Vertex(farM, surfaces[band], tints[band], periods);
                if (previous >= 0)
                {
                    // A short stretch of a fan — a hair of arc at the end of a turn — is one triangle, and a
                    // band both of whose edges stand at the centre is none: the rest stood on two corners at
                    // one point.
                    TriangleUnlessFlat(previous, previousFar, nearFar);
                    TriangleUnlessFlat(previous, nearFar, near);
                }

                previous = near;
                previousFar = nearFar;
            }
        }
    }

    /// <summary>
    /// <b>An offset goes no further across a curve than its centre.</b> Inside a bend tighter than the
    /// offset — a run wrapping the inside of a movement's turn stands under half a walk from the centre —
    /// the edge that far across is a smaller arc turned the other way, and every strip between two of its
    /// stations crossed itself. Held at the centre the edge is one point and the band a fan.
    /// </summary>
    static float ShortOfTheCentre(float offsetM, float curvature) =>
        curvature * offsetM > 1f ? 1f / curvature : offsetM;

    /// <summary>A triangle, unless its three corners stand on one line and it covers nothing.</summary>
    void TriangleUnlessFlat(int a, int b, int c)
    {
        var aM = _vertices[a].PositionM;
        var bM = _vertices[b].PositionM;
        var cM = _vertices[c].PositionM;
        var twiceAreaM2 = ((bM.X - aM.X) * (cM.Y - aM.Y)) - ((bM.Y - aM.Y) * (cM.X - aM.X));
        if (MathF.Abs(twiceAreaM2) > OnePointM * OnePointM) Triangle(a, b, c);
    }

    /// <summary>
    /// Where a stretch of a chain is sampled: the arcs' own stations, and the two ends of the stretch.
    /// </summary>
    /// <remarks>
    /// <b>Struck between the arc's own samples and not the stretch's</b>, for the reason
    /// <see cref="CurvedMark"/> is: a chord between any other pair of points stands off the chord the rest
    /// of the town was drawn on, and a section that begins part-way along a bend would meet its neighbour
    /// a sag out of line.
    /// </remarks>
    void Stations(ReadOnlySpan<ArcSeg> arcs, float fromM, float toM)
    {
        _stations.Clear();
        if (toM <= fromM) return;

        var walkedM = 0f;
        foreach (var arc in arcs)
        {
            var startM = MathF.Max(fromM - walkedM, 0f);
            var endM = MathF.Min(toM - walkedM, arc.LengthM);
            walkedM += arc.LengthM;
            if (endM <= startM) continue;

            var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / StepM(arc.Curvature)));
            var step = 0;
            var distanceM = startM;
            while (true)
            {
                var headingRad = arc.HeadingAtRad(distanceM);
                var atM = arc.PointAtM(distanceM);

                // The arcs of a chain share their joints, so a station at the start of one stands where the
                // last station of the one before it does — to a rounding, where the joint is one point read
                // off two arcs. Laid twice, every joint carries a band of no length and every reader of the
                // mesh has to know to throw it away.
                if (_stations.Count == 0 || Vector2.DistanceSquared(_stations[^1].AtM, atM) >= OnePointM * OnePointM)
                {
                    _stations.Add((atM, new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad)), arc.Curvature));
                }

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
    /// <b>One stretch of one road, laid as the seven bands its cross-section is</b>: the rim, the walk and
    /// the kerb line on the left, the carriageway, and the same three mirrored on the right. All seven are
    /// struck on one set of stations (<see cref="Strips"/>), so the picture of a road is a partition of the
    /// ground it covers and nothing in it is drawn twice (TER-7b).
    /// </summary>
    /// <remarks>
    /// <b>A band of no width is how a side says what it has not got</b> — no rim where the town runs on
    /// past the walk, and neither walk nor kerb line where something else stands against the kerb. The
    /// offsets stay eight and in order either way, so there is one shape here and not a case per side.
    /// <para>
    /// <b>Where a side carries no pavement, the outermost band is the tarmac that is really there</b>
    /// (TER-3c.7): the ground between a street and the car park set back off it is a pocket of the town's
    /// own asphalt. <b>A whole walk of it</b>, because a side is bare only where what stands against its
    /// kerb is within a walk of it, so everything that far out is that thing's own tarmac or the pocket
    /// before it — which is the whole of what a car park has beyond its box and the band round it.
    /// </para>
    /// </remarks>
    /// <param name="pockets">
    /// Which half of the cross-section this call lays: the pockets of asphalt beside a side with something
    /// standing against its kerb (TER-3c.7), or everything else. <b>Two calls on one set of stations</b>,
    /// because the two halves are drawn at different times: a pocket is union ground the pavement's rounds
    /// at a mouth stand over, so it goes under them, while the carriageway is drawn back over every stroke
    /// (TER-3d) and goes last. The stations are the arcs' own either way, so the seam between the pocket
    /// and the carriageway is one offset evaluated twice to the same points.
    /// </param>
    void Section(
        ReadOnlySpan<ArcSeg> arcs, float halfM, in PavedSection section, in SectionShades shades,
        float[] periods, bool pockets)
    {
        if (pockets && section.Left != PavedEdge.None && section.Right != PavedEdge.None) return;

        Span<float> offsetsM = stackalloc float[8];
        Span<Surface> surfaces = stackalloc Surface[7];
        Span<Vector3> tints = stackalloc Vector3[7];

        surfaces[3] = Surface.Tarmac;
        tints[3] = Plain;
        Side(section.Left, halfM, shades, offsetsM, surfaces, tints, -1, pockets);
        Side(section.Right, halfM, shades, offsetsM, surfaces, tints, 1, pockets);

        if (!pockets)
        {
            Strips(arcs, section.FromM, section.ToM, offsetsM, surfaces, tints, periods);
            return;
        }

        // The two sides on their own, so that nothing is laid between them: the carriageway is the other
        // call's. Two walks of the same stations, which is what makes the seam the same points.
        Strips(arcs, section.FromM, section.ToM, offsetsM[..4], surfaces[..3], tints[..3], periods);
        Strips(arcs, section.FromM, section.ToM, offsetsM[4..], surfaces[4..], tints[4..], periods);
    }

    /// <summary>
    /// One side of a section, written outward from the kerb into the cross-section's own slots. <b>The two
    /// sides are one description read two ways round</b> rather than two descriptions: the right fills
    /// outward from the middle and the left fills inward to it.
    /// </summary>
    static void Side(
        PavedEdge edge, float halfM, in SectionShades shades, Span<float> offsetsM,
        Span<Surface> surfaces, Span<Vector3> tints, int side, bool pockets)
    {
        var bare = edge == PavedEdge.None;
        var walkEndsM = halfM + shades.WalkM - (edge == PavedEdge.WalkAndRim ? shades.EdgeM : 0f);

        // The four offsets outward from the kerb, and the three bands between them: the kerb line, the
        // walk, the rim. <b>What a side has not got is an offset equal to the one before it</b>, which lays
        // no band — so a side with no rim, one with a pocket of the town's own asphalt against its kerb
        // instead of any concrete at all, and one that is not this call's half to lay, are the same shape
        // stated with different figures.
        Span<float> atM = bare == pockets
            ? bare
                ? [halfM, halfM, halfM, halfM + shades.WalkM]
                : [halfM, halfM + shades.KerbM, walkEndsM, halfM + shades.WalkM]
            : [halfM, halfM, halfM, halfM];
        Span<Surface> made = bare
            ? [Surface.Tarmac, Surface.Tarmac, Surface.Tarmac]
            : [Surface.Tarmac, Surface.Pavement, Surface.Pavement];
        Span<Vector3> worn = bare ? [Plain, Plain, Plain] : [shades.Paint, Plain, shades.Edge];

        // The right side's offsets run 4 … 7 outward from the middle and its bands 4, 5, 6; the left's are
        // the same four and three, counted the other way.
        for (var outward = 0; outward < 4; outward++)
        {
            offsetsM[side > 0 ? 4 + outward : 3 - outward] = side * atM[outward];
        }

        for (var outward = 0; outward < 3; outward++)
        {
            var band = side > 0 ? 4 + outward : 2 - outward;
            surfaces[band] = made[outward];
            tints[band] = worn[outward];
        }
    }

    /// <summary>The figures and shades a road's cross-section is laid out of, gathered once.</summary>
    internal readonly record struct SectionShades(
        float WalkM, float KerbM, float EdgeM, Vector3 Paint, Vector3 Edge);

    /// <summary>
    /// <b>One run of the pavement, laid as the three bands its cross-section is</b>: the rim on the outside
    /// where the run is the town's outline, the walk, and the kerb line on the inside (TER-3d) — struck on
    /// one set of stations of the run's own line (<see cref="Strips"/>), so the concrete and the stroke on
    /// it share their seam. It is one side of a road's section (<see cref="Side"/>) read off the line the
    /// walk runs down instead of off the road, which is what a run that wraps anything but a road is.
    /// </summary>
    /// <remarks>
    /// The band stops a line's width short of the outside where it is the outline, which is what leaves the
    /// shell's shadow standing as the rim; where more of the town lies beyond it the rim is a band of no
    /// width and the walk reaches the whole half.
    /// </remarks>
    /// <summary>
    /// <b>The half-round that closes the end of a band</b>, facing out: everything within half a walk of
    /// the place the run stops at, on the side it faces, as the semicircle's own band read in to its
    /// centre — a fan, stepped along the circumference like every other arc here, since walked by its
    /// radius a round of a pavement's width comes out a hexagon and the flats read as a corner cut off the
    /// end of the concrete.
    /// </summary>
    /// <remarks>
    /// <b>It carries no rim of its own.</b> A run stops where its wrap dives inside other tarmac — a mouth,
    /// a corner two runs hand over at — and there the round stands inside the neighbouring band, where a rim
    /// on it was a dark arc drawn across the concrete. The one end that faces the grass is a road's dead
    /// end, and that rim is the road's end grown by a walk (<see cref="Cap"/>). Where the run is the
    /// outline the disc stops a line's width short, exactly as the band does.
    /// </remarks>
    void Round(in PavedCap cap, bool outline, in SectionShades shades, float[] periods)
    {
        var halfWalkM = shades.WalkM * 0.5f;
        var discM = outline ? halfWalkM - shades.EdgeM : halfWalkM;
        if (discM <= 0f) return;

        // Turned on a positive curvature so that its centre stands on the across side of every station,
        // from square across the run to square across the other way, through the way the end faces.
        var fromRad = MathF.Atan2(cap.OutwardM.Y, cap.OutwardM.X) - (MathF.PI * 0.5f);
        var startM = cap.PlaceM + (discM * new Vector2(MathF.Cos(fromRad), MathF.Sin(fromRad)));
        ReadOnlySpan<ArcSeg> rim = [new ArcSeg(startM, fromRad + (MathF.PI * 0.5f), MathF.PI * discM, 1f / discM)];

        Span<float> offsetsM = [discM, 0f];
        Span<Surface> surfaces = [Surface.Pavement];
        Span<Vector3> tints = [Plain];

        Strips(rim, 0f, MathF.PI * discM, offsetsM, surfaces, tints, periods);
    }

    /// <summary>
    /// <b>The corner two runs hand over at</b> (<see cref="Paving.Corners"/>): the wedge of concrete between
    /// the two bands, which is the sector of the round the turn's arc rims, read in to the place the arc
    /// turns about; and the kerb stroke along the arc. One cross-section on the arc's own stations, so the
    /// stroke and the concrete it stands on share their seam.
    /// </summary>
    /// <remarks>
    /// The turn's arcs are struck about the places the runs stop at on the pavement's side of the kerb
    /// (<c>Corner.Round</c>), so half a walk in along their across is that place, and the band read in to
    /// it is a fan. Where a turn changes rounds part way, each arc fans from its own place.
    /// </remarks>
    void Turn(ReadOnlySpan<ArcSeg> corner, in SectionShades shades, float[] periods)
    {
        Span<float> offsetsM = [shades.WalkM * 0.5f, shades.KerbM, 0f];
        Span<Surface> surfaces = [Surface.Pavement, Surface.Tarmac];
        Span<Vector3> tints = [Plain, shades.Paint];

        Strips(corner, 0f, Spline.TotalLengthM(corner), offsetsM, surfaces, tints, periods);
    }

    void Run(in PavedRun run, in SectionShades shades, float[] periods)
    {
        var halfWalkM = shades.WalkM * 0.5f;
        var side = run.RoadSide;
        var rimM = run.Outline ? halfWalkM - shades.EdgeM : halfWalkM;
        Span<float> offsetsM = [-side * halfWalkM, -side * rimM, side * (halfWalkM - shades.KerbM), side * halfWalkM];
        Span<Surface> surfaces = [Surface.Pavement, Surface.Pavement, Surface.Tarmac];
        Span<Vector3> tints = [shades.Edge, Plain, shades.Paint];

        Strips(run.Line, 0f, run.LengthM, offsetsM, surfaces, tints, periods);
    }

    /// <summary>
    /// <b>A junction's own ground, once</b> (<see cref="Paving.Boxes"/>): its closed outline walked at the
    /// same chord bow as every other arc here and cut into triangles by clipping ears off it, since a box
    /// with a wedge that turns no corner is concave at the node.
    /// </summary>
    void Box(ReadOnlySpan<ArcSeg> outline, float[] periods)
    {
        var pointsM = new List<Vector2>();
        foreach (var arc in outline)
        {
            var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / StepM(arc.Curvature)));
            for (var step = 0; step < steps; step++)
            {
                var atM = arc.PointAtM(arc.LengthM * step / steps);
                if (pointsM.Count == 0 || Vector2.DistanceSquared(pointsM[^1], atM) >= OnePointM * OnePointM) pointsM.Add(atM);
            }
        }

        if (pointsM.Count > 1 && Vector2.DistanceSquared(pointsM[0], pointsM[^1]) < OnePointM * OnePointM) pointsM.RemoveAt(pointsM.Count - 1);
        Polygon(CollectionsMarshal.AsSpan(pointsM), Surface.Tarmac, Plain, periods);
    }

    /// <summary>
    /// <b>One side of a road on its own</b> over a stretch that is a box's on the other side
    /// (<see cref="Paving.Stubs"/>): the kerb line, the walk and the rim that side carries, on the road's
    /// own stations, and nothing between it and the far kerb. A side with something standing against its
    /// kerb carries nothing here — that ground is the box's.
    /// </summary>
    void Stub(
        ReadOnlySpan<ArcSeg> arcs, float halfM, in PavedStub stub, in PavedSection section, in SectionShades shades,
        float[] periods)
    {
        var edge = stub.Side > 0f ? section.Right : section.Left;
        if (edge == PavedEdge.None) return;

        Span<float> offsetsM = stackalloc float[8];
        Span<Surface> surfaces = stackalloc Surface[7];
        Span<Vector3> tints = stackalloc Vector3[7];
        Side(edge, halfM, shades, offsetsM, surfaces, tints, stub.Side > 0f ? 1 : -1, pockets: false);

        var fromM = MathF.Max(stub.FromM, section.FromM);
        var toM = MathF.Min(stub.ToM, section.ToM);
        if (stub.Side > 0f) Strips(arcs, fromM, toM, offsetsM[4..], surfaces[4..], tints[4..], periods);
        else Strips(arcs, fromM, toM, offsetsM[..4], surfaces[..3], tints[..3], periods);
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
