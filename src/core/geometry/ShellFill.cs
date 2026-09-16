using System.Numerics;
using System.Runtime.InteropServices;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>A shell cut into the triangles that cover it</b>: the points its boundary is read as, and the
/// triangles over them that fill the ground inside it and none of the ground outside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A shell is a 2D solid and not a set of lines.</b> It is closed rings of <see cref="ArcSeg"/>, each
/// walked with the ground it covers on the walker's right — one ring round the outside of every piece of it
/// and one round every hole any piece encloses. It may stand in pieces that touch nowhere, a piece may
/// enclose a hole and a hole may hold a piece of its own, and its edges bend. <b>None of that is a case
/// here</b>: a ring's own direction says which side its ground is on, so which ring is an outside and which
/// is a hole is read off the sign of the area it encloses and nothing is looked up. That is the same fact
/// <see cref="BandShell.Outset"/> moves a shell by, read for a different answer.
/// </para>
/// <para>
/// <b>Only the boundary is approximated, and only where it bends</b> (<see cref="Outline"/>). A straight
/// piece is one chord however long it is; a bent one is the fewest chords that bow inside it by no more than
/// the sag. A chord falls inside the arc it stands for, so a fill never reaches past the shell it
/// is of — it falls short of one by the sag on the outside of a bend and covers the sag's worth extra on the
/// inside of one.
/// </para>
/// <para>
/// <b>There are as few triangles as there are, and no fewer.</b> Nothing is added to the boundary — no
/// point inside the shape, no point on an edge — so a shell read as <c>n</c> points comes back as
/// <c>n − 2</c> triangles per piece and two more for every hole bridged into one, which is the fewest any
/// triangulation of those points can be.
/// </para>
/// <para>
/// <b>So the corners are the only lever, and there are three of them.</b> The sag decides how finely a bend
/// is read, and <c>thriftM</c> then takes back out every corner the line either side of it already stands
/// for (Douglas–Peucker). They are not the same knob: a sag is spent evenly whether a stretch needs it or
/// not, and the thinning spends it where the boundary actually bends. Over a town the pair at
/// <c>0.02 / 0.07</c> comes to <b>a quarter fewer triangles than the sag alone does at the same drawn
/// accuracy</b>, with the boundary eight centimetres off the arcs at the worst of it.
/// </para>
/// <para>
/// <b>The third is an angle, and it is the one a tight bend needs.</b> Both the others are budgets in
/// metres, and a bend tight enough spends neither — a quarter turn of a fifth of a metre bows four
/// centimetres off its own chord, so a sag of two allows sixty degrees a chord and a thinning of seven hands
/// the whole corner back. <c>turnRad</c> is how much of a turn one chord may stand for, and it binds where
/// the metres have stopped saying anything: on the fillets and kerb returns a town is full of, and nowhere
/// along a straight.
/// </para>
/// <para>
/// <b>And they are triangles rather than slivers, at that same count</b> (<see cref="Relax"/>). Cutting and
/// shaping are two questions: the clipper settles how many triangles there are and the flips that follow
/// settle which ones they are, over the same corners and the same ground. What comes back has the largest
/// small angle a triangulation of those corners can have.
/// </para>
/// <para>
/// <b>It knows nothing about what the shell is of</b>, and nothing about what the triangles are for. The
/// points come back in metres in the order the rings were read; a texture coordinate, a shade and a surface
/// are the caller's, computed off the position (<c>App.Render.GroundMesh</c>).
/// </para>
/// </remarks>
internal static partial class ShellFill
{
    /// <summary>
    /// <b>How far a chord may bow off the arc it stands for</b> where the caller states no tolerance of its
    /// own: a tenth of a millimetre, which is finer than any answer taken off a fill can resolve.
    /// </summary>
    /// <remarks>
    /// <b>It is the exactness floor and not the figure to draw at.</b> A float holds a quarter of a
    /// millimetre two kilometres from the origin, so a shell laid across a town is sampled finer here than
    /// it is stored — and a bend cut this fine costs a hundred times the triangles a picture can tell
    /// apart. Whatever draws states the tolerance its own picture is worth.
    /// </remarks>
    public const float SagM = 0.0001f;

    /// <summary>
    /// <b>The line a shell is drawn as</b>: every ring of it read as the points it is walked through and
    /// thinned to the ones that still stand for it, the last point of each implying the first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the whole of the approximating, and it is separable from the filling</b> — which is why it
    /// is a call of its own. A caller that both fills a shell and draws a line along it (a kerb,
    /// <c>App.Render.GroundMesh</c>) asks once and hands the answer to both, so the line it strikes and the
    /// edge of the fill beneath it are the same corners rather than two readings of one boundary at two
    /// tolerances.
    /// </para>
    /// <para>
    /// <b>A ring that encloses nothing is dropped here.</b> A shell merged out of bands can hand back a ring
    /// of three collinear pieces or one that doubles back on itself, and a shell offset inward hands back
    /// fewer rings than it was given; each goes now rather than being cut into triangles of no area that
    /// everything downstream then has to know to ignore.
    /// </para>
    /// </remarks>
    public static Vector2[][] Outline(
        ReadOnlySpan<ArcSeg[]> rings, float sagM = SagM, float thriftM = 0f, float turnRad = 0f)
    {
        if (rings.Length == 0 || sagM <= 0f) return [];

        var outline = new List<Vector2[]>(rings.Length);
        var pointsM = new List<Vector2>();
        foreach (var chain in rings)
        {
            pointsM.Clear();
            Flatten(chain, sagM, turnRad, pointsM);

            var kept = pointsM.Count;
            if (thriftM > 0f) kept = Thinned(CollectionsMarshal.AsSpan(pointsM), thriftM, turnRad);

            var laid = CollectionsMarshal.AsSpan(pointsM)[..kept];
            if (kept < 3 || MathF.Abs(SignedAreaM2(laid)) <= sagM * sagM) continue;

            outline.Add(laid.ToArray());
        }

        return outline.ToArray();
    }

    /// <summary>
    /// <b>The same line read again at a looser budget</b>, for a caller that wants two readings of one shell
    /// and wants them to agree: what comes back is a subset of what went in, so the coarse line's corners
    /// are the fine line's corners and the coarse line never stands further off the fine one than
    /// <paramref name="thriftM"/>.
    /// </summary>
    /// <remarks>
    /// <b>Two thinnings of one flattening and never two of their own.</b> Douglas–Peucker splits at the
    /// corner furthest off the chord whatever budget it is given, so the same walk at two budgets nests —
    /// and the error between the two readings is the looser budget itself rather than the sum of what each
    /// strays from the arcs. A caller that has to bound how far its two answers part (a fill under a kerb,
    /// <c>App.Render.GroundMesh</c>) can bound it by that one number only because they are nested.
    /// </remarks>
    public static Vector2[][] Outline(ReadOnlySpan<Vector2[]> outline, float thriftM, float turnRad = 0f)
    {
        if (thriftM <= 0f) return outline.ToArray();

        var looser = new List<Vector2[]>(outline.Length);
        foreach (var ring in outline)
        {
            var laid = ring.AsSpan().ToArray();
            var kept = Thinned(laid, thriftM, turnRad);
            if (kept < 3) continue;

            looser.Add(laid[..kept]);
        }

        return looser.ToArray();
    }

    /// <summary>
    /// <b>The shell those rings bound, filled</b> — the points and the triangles over them, both empty for
    /// a shell that encloses nothing.
    /// </summary>
    public static (Vector2[] PointsM, int[] Triangles) Of(
        ReadOnlySpan<ArcSeg[]> rings, float sagM = SagM, float thriftM = 0f, float turnRad = 0f) =>
        Of(Outline(rings, sagM, thriftM, turnRad));

    /// <summary>
    /// The same fill, of a shell already read as the line it is drawn as (<see cref="Outline"/>) — for a
    /// caller that has something else to do with that line.
    /// </summary>
    public static (Vector2[] PointsM, int[] Triangles) Of(ReadOnlySpan<Vector2[]> outline)
    {
        if (outline.Length == 0) return ([], []);

        var pointsM = new List<Vector2>();
        var ring = new List<Ring>(outline.Length);

        foreach (var closed in outline)
        {
            if (closed.Length < 3) continue;

            var from = pointsM.Count;
            pointsM.AddRange(closed);
            ring.Add(Ring.Over(
                CollectionsMarshal.AsSpan(pointsM).Slice(from, closed.Length), from, SignedAreaM2(closed)));
        }

        var points = CollectionsMarshal.AsSpan(pointsM);
        var triangles = new List<int>(pointsM.Count * 3);
        var holes = new List<Ring>();

        // Which piece each hole belongs to, worked out once for the hole rather than once for every piece
        // it might have belonged to: a city's boundary is hundreds of blocks against tens of thousands of
        // corners, and asking the question per pair is asking it a thousand times over.
        var parent = new int[ring.Count];
        for (var hole = 0; hole < ring.Count; hole++)
        {
            parent[hole] = ring[hole].AreaM2 < 0f ? Parent(ring, points, hole) : -1;
        }

        for (var outer = 0; outer < ring.Count; outer++)
        {
            if (ring[outer].AreaM2 < 0f) continue;

            holes.Clear();
            for (var hole = 0; hole < ring.Count; hole++)
            {
                if (parent[hole] == outer) holes.Add(ring[hole]);
            }

            Fill(points, ring[outer], holes, triangles);
        }

        var corners = pointsM.ToArray();
        var mesh = triangles.ToArray();
        Relax(corners, mesh);
        return (corners, mesh);
    }

    /// <summary>
    /// One ring as the stretch of points it was read as, the area it encloses and the box it stands in.
    /// </summary>
    /// <remarks>
    /// <b>The sign of the area is which kind of ring it is</b> — positive round the outside of a piece of
    /// the shell and negative round a hole — because every ring is walked with its ground on the walker's
    /// right. Nothing else distinguishes them and nothing has to.
    /// </remarks>
    readonly record struct Ring(int From, int Count, float AreaM2, Vector2 MinM, Vector2 MaxM)
    {
        public static Ring Over(ReadOnlySpan<Vector2> points, int from, float areaM2)
        {
            var minM = new Vector2(float.MaxValue);
            var maxM = new Vector2(float.MinValue);
            foreach (var pointM in points)
            {
                minM = Vector2.Min(minM, pointM);
                maxM = Vector2.Max(maxM, pointM);
            }

            return new Ring(from, points.Length, areaM2, minM, maxM);
        }

        public bool Holds(Vector2 pointM) =>
            pointM.X >= MinM.X && pointM.X <= MaxM.X && pointM.Y >= MinM.Y && pointM.Y <= MaxM.Y;
    }

    /// <summary>
    /// <b>Which piece of the shell a hole is a hole in</b>: the smallest outer ring that encloses it, or
    /// <c>-1</c> for a hole no piece holds.
    /// </summary>
    /// <remarks>
    /// <b>Smallest, because a shell nests.</b> A courtyard inside a block that stands in a lake inside a
    /// town is enclosed by the town's ring as much as by the block's, and it is the block's hole. Taking the
    /// first enclosing ring instead would hand the courtyard to the town and leave the block to be filled
    /// over it.
    /// </remarks>
    static int Parent(List<Ring> ring, ReadOnlySpan<Vector2> points, int hole)
    {
        var pointM = points[ring[hole].From];
        var parent = -1;
        var leastM2 = float.MaxValue;

        for (var outer = 0; outer < ring.Count; outer++)
        {
            if (ring[outer].AreaM2 < 0f || ring[outer].AreaM2 >= leastM2) continue;
            if (!ring[outer].Holds(pointM)) continue;
            if (!Encloses(points.Slice(ring[outer].From, ring[outer].Count), pointM)) continue;

            parent = outer;
            leastM2 = ring[outer].AreaM2;
        }

        return parent;
    }

    /// <summary>One ring read as the points it is drawn through, the last of which is its own first.</summary>
    /// <remarks>
    /// <b>Each piece gives its own start and the stations inside it, and never its end</b>: the end is the
    /// next piece's start, and the last piece's end is the ring's own first point. A ring read otherwise
    /// carries a duplicate at every joint, which is a corner the cutting then has to filter back out.
    /// </remarks>
    static void Flatten(ReadOnlySpan<ArcSeg> arcs, float sagM, float turnRad, List<Vector2> into)
    {
        foreach (var arc in arcs)
        {
            var chords = Chords(arc, sagM, turnRad);
            for (var chord = 0; chord < chords; chord++)
            {
                into.Add(arc.PointAtM(arc.LengthM * chord / chords));
            }
        }
    }

    /// <summary>
    /// <b>How few chords one piece is drawn as</b>: the fewest that each bow inside a bend by no more than
    /// the sag, and one for a straight however long it runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off the sag and never off the length</b> — a chord subtending <c>2·acos(1 − sag/R)</c> bows by
    /// exactly the sag, so a bend of any radius is cut at the angle its own radius earns. Counted by length
    /// as well, a car park's corner comes out finer than the straight it joins and the extra corners buy
    /// nothing: a texture coordinate is this shell's own position over a period, so it is affine in the
    /// position and reads exactly across a triangle of any shape at all.
    /// </para>
    /// <para>
    /// <b>A split stays on the chord it splits</b> — the stations are taken along the piece, so a bend cut
    /// finer than the sag asks follows the bend more closely and a straight cut finer is the same straight.
    /// Nothing about the shape moves.
    /// </para>
    /// <para>
    /// <b>And no chord stands for more of a turn than <paramref name="turnRad"/></b>, because the angle the
    /// sag earns a bend runs the wrong way: the step it allows is <c>2·acos(1 − sag/R)</c>, which
    /// <em>grows</em> as the radius shrinks — a bend of a seventh of a metre comes back at sixty degrees a
    /// chord, well inside a sag of two centimetres and plainly a polygon. The distance is right and the
    /// direction is not, so the tight corners the town is full of are where the second budget binds and the
    /// straights never reach it.
    /// </para>
    /// </remarks>
    static int Chords(in ArcSeg arc, float sagM, float turnRad)
    {
        var sweepRad = MathF.Abs(arc.Curvature * arc.LengthM);
        if (sweepRad <= 0f) return 1;

        var radiusM = 1f / MathF.Abs(arc.Curvature);
        var stepRad = 2f * MathF.Acos(Math.Clamp(1f - (sagM / radiusM), -1f, 1f));
        if (turnRad > 0f) stepRad = MathF.Min(stepRad, turnRad);

        return stepRad <= 0f ? 1 : Math.Max(1, (int)MathF.Ceiling(sweepRad / stepRad));
    }

    /// <summary>
    /// The area a closed polygon encloses, <b>signed by which way it is walked</b> — positive round the
    /// outside of a shape whose ground is on the walker's right.
    /// </summary>
    /// <remarks>
    /// Summed in <c>double</c> because the terms are coordinates multiplied by coordinates: a town two
    /// kilometres across cancels six of a float's seven figures in every one of them, and what is left is
    /// the answer.
    /// </remarks>
    static float SignedAreaM2(ReadOnlySpan<Vector2> polygon)
    {
        var twice = 0.0;
        for (int at = 0, before = polygon.Length - 1; at < polygon.Length; before = at++)
        {
            twice += ((double)polygon[before].X * polygon[at].Y) - ((double)polygon[at].X * polygon[before].Y);
        }

        return (float)(twice * 0.5);
    }

    /// <summary>Whether a closed polygon encloses a point, by the crossings of a ray cast from it.</summary>
    static bool Encloses(ReadOnlySpan<Vector2> polygon, Vector2 pointM)
    {
        var inside = false;
        for (int at = 0, before = polygon.Length - 1; at < polygon.Length; before = at++)
        {
            if (polygon[at].Y > pointM.Y == polygon[before].Y > pointM.Y) continue;

            var crossesAtX = ((double)polygon[before].X - polygon[at].X)
                * (pointM.Y - polygon[at].Y) / ((double)polygon[before].Y - polygon[at].Y) + polygon[at].X;
            if (pointM.X < crossesAtX) inside = !inside;
        }

        return inside;
    }
}
