using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.App.Render;

/// <summary>The shapes the ground is cut from, and the triangles and vertices they are written down as.</summary>
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
        var along = axis.LengthSquared() > 0f ? Vector2.Normalize(axis) : Vector2.UnitX;
        var across = new Vector2(-along.Y, along.X);
        Quad(
            Vertex(centreM - along * halfM.X - across * halfM.Y, surface, tint, periods),
            Vertex(centreM + along * halfM.X - across * halfM.Y, surface, tint, periods),
            Vertex(centreM + along * halfM.X + across * halfM.Y, surface, tint, periods),
            Vertex(centreM - along * halfM.X + across * halfM.Y, surface, tint, periods));
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
    /// <b>It carries its own rim where its run is the outline</b>, as the outermost of its bands — struck on
    /// the disc's own stations, so the concrete and the line round it share their seam the way a run's do.
    /// The band it closes stops a line's width short of the outside and the disc has to stop there too;
    /// what stands in that line's width is this, and left to the run's own rim — which is a strip along a
    /// line and has no way round an end — it was a crescent of grass at every stop in the town.
    /// <para>
    /// Where the run is <em>not</em> the outline there is no rim: the round stands inside the neighbouring
    /// band — a mouth, a corner two runs hand over at — and a line drawn round it there is a dark arc across
    /// the concrete.
    /// </para>
    /// </remarks>
    void Round(in PavedCap cap, bool outline, in SectionShades shades, float[] periods)
    {
        var halfWalkM = shades.WalkM * 0.5f;
        if (halfWalkM <= 0f) return;

        // Struck at the band's own half-width and read inward, so the rim is the first band off the arc and
        // the concrete the rest of the way to the centre. Turned on a positive curvature so that the centre
        // stands on the across side of every station, from square across the run to square across the other
        // way, through the way the end faces.
        var fromRad = MathF.Atan2(cap.OutwardM.Y, cap.OutwardM.X) - (MathF.PI * 0.5f);
        var startM = cap.PlaceM + (halfWalkM * new Vector2(MathF.Cos(fromRad), MathF.Sin(fromRad)));
        ReadOnlySpan<ArcSeg> rim =
            [new ArcSeg(startM, fromRad + (MathF.PI * 0.5f), MathF.PI * halfWalkM, 1f / halfWalkM)];

        Span<float> rimmedM = [0f, shades.EdgeM, halfWalkM];
        Span<float> bareM = [0f, halfWalkM];
        Span<Surface> surfaces = [Surface.Pavement, Surface.Pavement];
        Span<Vector3> tints = [shades.Edge, Plain];

        if (outline) Strips(rim, 0f, MathF.PI * halfWalkM, rimmedM, surfaces, tints, periods);
        else Strips(rim, 0f, MathF.PI * halfWalkM, bareM, surfaces[1..], tints[1..], periods);
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

    /// <summary>
    /// <b>The rim round a corner the shell turns</b> (<see cref="Paving.ShellCorners"/>): a run's rim is a
    /// band of its own line and a road's a band of the road's, and two offsets of a corner do not meet —
    /// where the grass makes a corner into the pavement each rim stops square at it, and the ground within
    /// a line's width of the corner point itself is the sector between the two. Read in to the corner, so
    /// it is a fan like every other round here.
    /// </summary>
    /// <remarks>
    /// <b>Laid after the roads</b>, with the rims (<see cref="Rim"/>): it stands where a road's bare side
    /// is laid over the end of the run beside it, and laid with the runs it was painted out with the rest.
    /// </remarks>
    void ShellCorner(in PavedShellCorner corner, in SectionShades shades, float[] periods)
    {
        var rimM = shades.EdgeM;
        if (rimM <= 0f) return;

        // Turned on a positive curvature so that the corner stands on the across side of every station.
        var (fromM, toM) = Cross(corner.FromM, corner.ToM) >= 0f ? (corner.FromM, corner.ToM) : (corner.ToM, corner.FromM);
        var sweepRad = MathF.Atan2(Cross(fromM, toM), Vector2.Dot(fromM, toM));
        var alongM = Heading.RightOf(fromM);
        ReadOnlySpan<ArcSeg> rim =
            [new ArcSeg(corner.PlaceM + (fromM * rimM), MathF.Atan2(alongM.Y, alongM.X), sweepRad * rimM, 1f / rimM)];

        Span<float> offsetsM = [rimM, 0f];
        Span<Surface> surfaces = [Surface.Pavement];
        Span<Vector3> tints = [shades.Edge];

        Strips(rim, 0f, sweepRad * rimM, offsetsM, surfaces, tints, periods);
    }

    /// <remarks>
    /// <b>The walk stops a line's width short of the outside over the stretch whose outer edge is the
    /// outline</b> (<see cref="PavedRun.RimFromM"/>) and reaches the whole half elsewhere — three stretches
    /// on the run's own stations, so the seams between them are one offset evaluated once. What stands in
    /// that line's width is the rim, laid on the same stations by <see cref="Rim"/> after the roads.
    /// </remarks>
    void Run(in PavedRun run, in SectionShades shades, float[] periods)
    {
        var halfWalkM = shades.WalkM * 0.5f;
        var side = run.RoadSide;
        Span<float> bareM = [-side * halfWalkM, side * (halfWalkM - shades.KerbM), side * halfWalkM];
        Span<float> rimmedM = [-side * (halfWalkM - shades.EdgeM), side * (halfWalkM - shades.KerbM), side * halfWalkM];
        Span<Surface> surfaces = [Surface.Pavement, Surface.Tarmac];
        Span<Vector3> tints = [Plain, shades.Paint];

        if (!HasRim(run))
        {
            Strips(run.Line, 0f, run.LengthM, bareM, surfaces, tints, periods);
            return;
        }

        Strips(run.Line, 0f, run.RimFromM, bareM, surfaces, tints, periods);
        Strips(run.Line, run.RimFromM, run.RimToM, rimmedM, surfaces, tints, periods);
        Strips(run.Line, run.RimToM, run.LengthM, bareM, surfaces, tints, periods);
    }

    /// <summary>
    /// <b>A run's rim</b>: the line's width on the outside of its band over the stretch whose outer edge is
    /// the outline (<see cref="PavedRun.RimFromM"/>) and no further — a run's line runs on past the shell's
    /// corner to where it meets the next run's, and a rim run on with it stood half a walk deep inside the
    /// next band. On the same stations as the band (<see cref="Run"/>), so the seam between them is shared.
    /// </summary>
    /// <remarks>
    /// <b>Laid after the roads.</b> At a corner where a run hands over to a road, the wedge of the run's rim
    /// nearest the corner stands inside the road's band, and the road's bare side laid over it painted it
    /// out to the road's own edge — a sliver of concrete in the rim at every such corner.
    /// </remarks>
    void Rim(in PavedRun run, in SectionShades shades, float[] periods)
    {
        if (!HasRim(run)) return;

        var halfWalkM = shades.WalkM * 0.5f;
        Span<float> offsetsM = [-run.RoadSide * halfWalkM, -run.RoadSide * (halfWalkM - shades.EdgeM)];
        Span<Surface> surfaces = [Surface.Pavement];
        Span<Vector3> tints = [shades.Edge];

        Strips(run.Line, run.RimFromM, run.RimToM, offsetsM, surfaces, tints, periods);
    }

    static bool HasRim(in PavedRun run) => run.Outline && run.RimToM > run.RimFromM;

    /// <summary>
    /// <b>The band across a hand-over, struck off the two runs' own end stations</b> — each corner is the
    /// corner that run's own band already stands on, so the ground between them closes exactly rather than
    /// to a tolerance. <b>Every hand-over gets one</b> (<see cref="Paving.Next"/>): two ends that stop at
    /// one point leave a strip of no width and no triangles, and two that stop a kink apart leave the lens
    /// between their cross-sections, which is what this covers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How much of the cross-section it carries depends on what else covers the hand-over.</b> A turn's
    /// wedge is the sector between two kerbs that meet at an angle and it reaches the place each arc turns
    /// about — the run's own line, half a walk — so it covers the band's road half and no more, and the
    /// bridge carries the outer half over it. A hand-over between two kerbs that lie along one another
    /// (<see cref="Paving.Straight"/>) gets no wedge at all, and the bridge carries the whole cross-section,
    /// rim to kerb.
    /// </para>
    /// <para>
    /// <b>The two ends carry their own offsets</b>, so a rim that is the outline on one side of the step and
    /// not on the other tapers across it rather than stopping square. <b>Laid in two passes like a run's own
    /// band</b> (<see cref="Run"/>, <see cref="Rim"/>): the walk and the kerb line with the runs, the rim
    /// after the roads.
    /// </para>
    /// </remarks>
    /// <param name="wedged">Whether a turn's wedge covers the band's road half here, leaving this the outer half.</param>
    void Bridge(in PavedRun from, bool fromStart, in PavedRun onto, bool ontoStart, in SectionShades shades,
        bool rim, bool wedged, float[] periods)
    {
        Span<float> fromM = stackalloc float[5];
        Span<float> ontoM = stackalloc float[5];
        BandAt(from, fromStart, shades, fromM);
        BandAt(onto, ontoStart, shades, ontoM);

        // The rim is the outermost band, then the walk to the run's own line, then the walk on to the kerb
        // line and the kerb line itself — exactly as a run's own cross-section reads them, cut at the line
        // so that the half a wedge covers is a band of its own and the seam with it is one offset.
        Span<Surface> surfaces = [Surface.Pavement, Surface.Pavement, Surface.Pavement, Surface.Tarmac];
        Span<Vector3> tints = [shades.Edge, Plain, Plain, shades.Paint];
        var (a, b) = (EndStationOf(from, fromStart), EndStationOf(onto, ontoStart));
        var toM = wedged ? 3 : 5;
        if (rim) StripBetween(a, fromM[..2], b, ontoM[..2], surfaces[..1], tints[..1], periods);
        else StripBetween(a, fromM[1..toM], b, ontoM[1..toM], surfaces[1..(toM - 1)], tints[1..(toM - 1)], periods);
    }

    /// <summary>
    /// Where a run's band stops at one of its ends, and which way across the band lies there: the station
    /// <see cref="Stations"/> strikes at that end, read off the same arc.
    /// </summary>
    static (Vector2 AtM, Vector2 Across) EndStationOf(in PavedRun run, bool atStart)
    {
        var arc = atStart ? run.Line[0] : run.Line[^1];
        var headingRad = atStart ? arc.HeadingRad : arc.HeadingAtRad(arc.LengthM);
        return (atStart ? arc.StartM : arc.EndM, new Vector2(-MathF.Sin(headingRad), MathF.Cos(headingRad)));
    }

    /// <summary>
    /// A run's five offsets at one of its ends — the outer edge, the rim's inner edge, the run's own line,
    /// the kerb line's outer edge and the kerb — the same figures <see cref="Run"/> and <see cref="Rim"/>
    /// strike that end on, with the line a turn's wedge reaches among them.
    /// </summary>
    static void BandAt(in PavedRun run, bool atStart, in SectionShades shades, Span<float> offsetsM)
    {
        var halfWalkM = shades.WalkM * 0.5f;
        var side = run.RoadSide;
        var rimmed = HasRim(run)
                     && (atStart ? run.RimFromM <= OnePointM : run.RimToM >= run.LengthM - OnePointM);
        offsetsM[0] = -side * halfWalkM;
        offsetsM[1] = -side * (rimmed ? halfWalkM - shades.EdgeM : halfWalkM);
        offsetsM[2] = 0f;
        offsetsM[3] = side * (halfWalkM - shades.KerbM);
        offsetsM[4] = side * halfWalkM;
    }

    /// <summary>
    /// The bands between two stations that carry their own offsets — one strip each, the corners taken from
    /// the two ends as they stand rather than struck along a curve between them.
    /// </summary>
    void StripBetween(
        (Vector2 AtM, Vector2 Across) from, ReadOnlySpan<float> fromM,
        (Vector2 AtM, Vector2 Across) onto, ReadOnlySpan<float> ontoM,
        ReadOnlySpan<Surface> surfaces, ReadOnlySpan<Vector3> tints, float[] periods)
    {
        for (var band = 0; band + 1 < fromM.Length; band++)
        {
            var near = Vertex(from.AtM + (from.Across * fromM[band]), surfaces[band], tints[band], periods);
            var far = Vertex(from.AtM + (from.Across * fromM[band + 1]), surfaces[band], tints[band], periods);
            var ontoFar = Vertex(onto.AtM + (onto.Across * ontoM[band + 1]), surfaces[band], tints[band], periods);
            var ontoNear = Vertex(onto.AtM + (onto.Across * ontoM[band]), surfaces[band], tints[band], periods);
            TriangleUnlessFlat(near, far, ontoFar);
            TriangleUnlessFlat(near, ontoFar, ontoNear);
        }
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
    /// <remarks>
    /// <b>Over the stub's own stretch, and what it carries off the section at the cut</b>
    /// (<c>GroundMesh.SectionAtTheCut</c>): the stub stands where the road's sections have already given
    /// the ground to the box, so the section says which bands to lay and the stub says how far.
    /// </remarks>
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

        if (stub.Side > 0f) Strips(arcs, stub.FromM, stub.ToM, offsetsM[4..], surfaces[4..], tints[4..], periods);
        else Strips(arcs, stub.FromM, stub.ToM, offsetsM[..4], surfaces[..3], tints[..3], periods);
    }

    /// <summary>
    /// A water outline, cut into triangles by clipping ears off it. The outlines are concave — a river
    /// is nothing else — so a fan from any one vertex would paint over its own banks.
    /// </summary>
    /// <remarks>
    /// <b>The next ear is looked for past the last one and not from the start again.</b> An outline is
    /// walked at a chord's bow, so most of a junction's box is convex and every vertex of it is an ear:
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

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    static bool InsideTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 pointM) =>
        Cross(a, b, pointM) >= 0f && Cross(b, c, pointM) >= 0f && Cross(c, a, pointM) >= 0f;

    /// <summary>
    /// One corner of the ground, <b>and the same corner however many shapes meet at it</b>: a corner a shape
    /// asks for where one already stands — the same point, wearing the same surface and the same shade — is
    /// that one, and the two shapes come away sharing it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is what makes the ground one mesh rather than a heap of islands.</b> Every shape here is laid
    /// against its neighbours' own edges (TER-7b), so where two of them meet they ask for the same corners —
    /// and laid as corners of their own they were two sets of vertices standing at one place, which no reader
    /// of the mesh can tell from two pieces that merely happen to abut. Shared, the seam is one vertex and
    /// the picture cannot open along it however the numbers round.
    /// </para>
    /// <para>
    /// <b>A surface and a shade are part of which corner this is</b>, not decoration on it: the concrete and
    /// the kerb stroke that meet along a seam stand at one place and are two corners, because a surface is a
    /// vertex attribute and the seam is where one gives way to the other.
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
