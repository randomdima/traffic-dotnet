using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

internal static partial class ShellFill
{
    /// <summary>
    /// <b>The same triangles made triangular</b>: every diagonal of the cut that has a better one turned
    /// into it, which is Delaunay and maximises the smallest angle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not one triangle more or fewer, because a flip is not a cut.</b> Turning the shared edge of two
    /// triangles is two triangles before and after, over the same four corners and the same ground — so the
    /// count a fill comes to is settled by the cutting alone, and everything here is about shape. That is
    /// what makes the two separable: the clipper is asked for the fewest triangles and this is asked for the
    /// best ones, and neither can spend the other's answer.
    /// </para>
    /// <para>
    /// <b>An edge is turned when the far corner of the triangle beside it stands inside the circle through
    /// this one's three</b>, which is the one local test whose fixed point is the triangulation no other
    /// beats on that measure. It is what takes an ear clipper's output apart: clipping walks the ring taking
    /// corners off in the order it meets them, so a long stretch of boundary comes off as a fan of slivers
    /// from whatever corner happened to be convex, and every one of those fans is a run of edges each of
    /// which loses the circle test.
    /// </para>
    /// <para>
    /// <b>A turn is refused unless the quad is strictly convex</b>, which the circle test alone does not
    /// promise. On a shell it matters: a triangulation bounded by a fixed boundary is full of quads that
    /// close on themselves, and turning one lays a triangle over its neighbour and another over the grass.
    /// </para>
    /// <para>
    /// <b>Nothing here bounds how many edges meet at a corner and nothing needs to.</b> A pass that turned
    /// edges away from hubs used to follow this one, from when the boundary was read as chords alone and a
    /// clipper's fan left corners with sixty-four edges on them. Thinning the boundary first takes the
    /// near-collinear corners those fans grew from out of the shape altogether, and Delaunay by itself now
    /// leaves twelve at the busiest corner of a town — so the pass was two edges' worth of relief and a
    /// second traversal, and it is gone.
    /// </para>
    /// </remarks>
    static void Relax(ReadOnlySpan<Vector2> pointsM, int[] triangles)
    {
        if (triangles.Length < 6) return;

        Circled(pointsM, triangles, Twins(triangles));
    }

    /// <summary>
    /// <b>Which half-edge is the other side of which</b>, or <c>-1</c> where an edge is the shell's own
    /// boundary and has no other side.
    /// </summary>
    /// <remarks>
    /// <b>The boundary is what the walk left unpaired and is not looked up.</b> Every edge inside the fill
    /// is walked once each way — once by each of the two triangles on it — and every edge of the shell's
    /// boundary once only, so pairing the walk by direction says which is which without the rings being
    /// consulted. <b>The slit a bridged hole leaves pairs like any other edge</b>, and it should: it is a
    /// line of no width through ground the shell covers, and a turn across it is as legal as any.
    /// </remarks>
    static int[] Twins(int[] triangles)
    {
        var twin = new int[triangles.Length];
        Array.Fill(twin, -1);

        var walked = new Dictionary<(int From, int To), int>(triangles.Length);
        for (var edge = 0; edge < triangles.Length; edge++)
        {
            var fromCorner = triangles[edge];
            var toCorner = triangles[Along(edge)];

            if (walked.TryGetValue((toCorner, fromCorner), out var other) && twin[other] < 0)
            {
                twin[edge] = other;
                twin[other] = edge;
                continue;
            }

            walked.TryAdd((fromCorner, toCorner), edge);
        }

        return twin;
    }

    /// <summary>Every diagonal turned until none of them has a better one.</summary>
    /// <remarks>
    /// <b>Worked off a list of what a turn can have spoiled, and the list reaches one ring further than the
    /// turn does.</b> A turn remakes two triangles, so the four edges round them are plainly different
    /// questions than they were — but so is every edge of the triangles standing <em>against</em> those
    /// four, because a diagonal is weighed against the corner across it and that corner has moved. A list
    /// that asked only about the four left several hundred edges of a town's ground illegal and the fans
    /// this exists to take apart still standing; the shape of that defect is a mesh that is quietly not
    /// Delaunay with nothing to say so, which is why the reach is written down here rather than left to be
    /// re-derived.
    /// </remarks>
    static void Circled(ReadOnlySpan<Vector2> pointsM, int[] triangles, int[] twin)
    {
        var pending = new Queue<int>(triangles.Length);
        var queued = new bool[triangles.Length];
        for (var edge = 0; edge < triangles.Length; edge++)
        {
            pending.Enqueue(edge);
            queued[edge] = true;
        }

        var guard = triangles.Length * TurnsPerEdge;
        while (pending.Count > 0 && guard-- > 0)
        {
            var edge = pending.Dequeue();
            queued[edge] = false;

            if (!Reads(triangles, twin, edge, out var quad)) continue;
            if (!Convex(pointsM, quad)) continue;
            if (!Circled(pointsM, quad)) continue;

            Turn(triangles, twin, quad);

            // The two triangles kept their places and changed their corners, so every edge of them and
            // every edge standing against one of those is a different question than it was.
            Ask(pending, queued, twin, quad.Ar);
            Ask(pending, queued, twin, quad.Al);
            Ask(pending, queued, twin, quad.Bl);
            Ask(pending, queued, twin, Along(quad.Twin));
            Ask(pending, queued, twin, quad.Edge);
        }
    }

    /// <summary>
    /// One edge put back on the list, <b>and the edges of whatever stands the other side of it</b>. A turn
    /// changes the corner a neighbour's own diagonals are weighed against, which is a question about that
    /// neighbour's edges and not only about the four the turn remade — the reach a list one ring short of
    /// this leaves several hundred edges of a town illegal.
    /// </summary>
    static void Ask(Queue<int> pending, bool[] queued, int[] twin, int edge)
    {
        Wanted(pending, queued, edge);

        var other = twin[edge];
        if (other < 0) return;

        var across = other - (other % 3);
        for (var side = 0; side < 3; side++) Wanted(pending, queued, across + side);
    }

    static void Wanted(Queue<int> pending, bool[] queued, int edge)
    {
        if (queued[edge]) return;

        queued[edge] = true;
        pending.Enqueue(edge);
    }

    /// <summary>
    /// How many turns one edge of the cut may be part of before the pass gives up. A mesh that has not
    /// settled by then is one the predicates disagree about, which is a defect rather than a shape.
    /// </summary>
    const int TurnsPerEdge = 64;

    /// <summary>
    /// <b>The four corners either side of one edge of the cut</b>, and the half-edges a turn of it relinks.
    /// </summary>
    /// <remarks>
    /// <c>Corner → Opposite</c> is the edge itself, walked by the first of the two triangles; <c>Apex</c> is
    /// what that triangle stands on and <c>Across</c> what the other does. A turn replaces the first pair
    /// with the second, which is why both are named rather than indexed.
    /// </remarks>
    readonly record struct Diagonal(
        int Edge, int Twin, int Ar, int Al, int Bl, int Apex, int Corner, int Opposite, int Across);

    /// <summary>The quad one half-edge stands in, or false where it is the boundary and stands in none.</summary>
    static bool Reads(int[] triangles, int[] twin, int edge, out Diagonal quad)
    {
        quad = default;

        var other = twin[edge];
        if (other < 0) return false;

        var one = edge - (edge % 3);
        var across = other - (other % 3);
        var ar = one + ((edge + 2) % 3);
        var al = one + ((edge + 1) % 3);
        var bl = across + ((other + 2) % 3);

        quad = new Diagonal(
            edge, other, ar, al, bl, triangles[ar], triangles[edge], triangles[al], triangles[bl]);
        return true;
    }

    /// <summary>
    /// Whether the quad two triangles make is strictly convex, which is whether turning its diagonal leaves
    /// two triangles covering the same ground.
    /// </summary>
    /// <remarks>
    /// <b>Only the two corners the diagonal ends at are asked.</b> The other two are corners of the
    /// triangles as they stand and are convex already, so a quad that closes on itself does so at one end of
    /// the edge being turned or the other.
    /// </remarks>
    static bool Convex(ReadOnlySpan<Vector2> pointsM, in Diagonal quad) =>
        Turns(pointsM[quad.Apex], pointsM[quad.Corner], pointsM[quad.Across]) > 0.0
        && Turns(pointsM[quad.Across], pointsM[quad.Opposite], pointsM[quad.Apex]) > 0.0;

    /// <summary>
    /// <b>Whether a diagonal has a better one</b>: whether the two corners it does not touch subtend more
    /// than a straight angle between them, which is the same thing as either standing inside the circle
    /// through the other three.
    /// </summary>
    /// <remarks>
    /// <b>Read as the two angles and not as the circle through four points.</b> The determinant form
    /// squares distances, so over a town it is differences of terms in the trillions and its sign near a
    /// tie is the last bit of a double — and a tie is exactly what a shell is full of, four corners of a
    /// straight-sided strip being cocircular by construction. Read that way the pass oscillated: an edge
    /// illegal, turned, and illegal again the other way for as many sweeps as it was given. <b>The angles
    /// are bounded, so the tie is a number rather than a coin toss</b> and
    /// <see cref="Wobble"/> settles it one way.
    /// </remarks>
    static bool Circled(ReadOnlySpan<Vector2> pointsM, in Diagonal quad)
    {
        var apex = pointsM[quad.Apex];
        var across = pointsM[quad.Across];
        var corner = pointsM[quad.Corner];
        var opposite = pointsM[quad.Opposite];

        var oneFrom = corner - apex;
        var oneTo = opposite - apex;
        var otherFrom = opposite - across;
        var otherTo = corner - across;

        var oneCos = Dot(oneFrom, oneTo);
        var oneSin = Math.Abs(Across(oneFrom, oneTo));
        var otherCos = Dot(otherFrom, otherTo);
        var otherSin = Math.Abs(Across(otherFrom, otherTo));

        // sin(one + other) < 0 is the two of them coming to more than a straight angle. Over the product of
        // the four lengths, so the figure is a sine and the wobble is a figure about angles.
        var scale = (double)oneFrom.Length() * oneTo.Length() * otherFrom.Length() * otherTo.Length();
        return scale > 0.0 && (((oneSin * otherCos) + (oneCos * otherSin)) / scale) < -Wobble;
    }

    /// <summary>
    /// How far past a straight angle, as a sine, the two corners have to come before the diagonal between
    /// them is turned. <b>A tie is left alone</b>: four cocircular corners have two triangulations as good
    /// as one another, and a pass that turns between them on the last bit of an arithmetic never settles.
    /// </summary>
    const double Wobble = 1e-9;

    static double Dot(Vector2 one, Vector2 other) =>
        ((double)one.X * other.X) + ((double)one.Y * other.Y);

    static double Across(Vector2 one, Vector2 other) =>
        ((double)one.X * other.Y) - ((double)one.Y * other.X);

    /// <summary>
    /// One diagonal turned: the two triangles keep their places in the list and change which corners they
    /// stand on, and the four edges round them are relinked to their new sides.
    /// </summary>
    static void Turn(int[] triangles, int[] twin, in Diagonal quad)
    {
        triangles[quad.Edge] = quad.Across;
        triangles[quad.Twin] = quad.Apex;

        var pastBl = twin[quad.Bl];
        var pastAr = twin[quad.Ar];

        Link(twin, quad.Edge, pastBl);
        Link(twin, quad.Twin, pastAr);
        Link(twin, quad.Ar, quad.Bl);
    }

    static void Link(int[] twin, int edge, int other)
    {
        twin[edge] = other;
        if (other >= 0) twin[other] = edge;
    }

    /// <summary>The next half-edge round the triangle this one is in.</summary>
    static int Along(int edge) => edge % 3 == 2 ? edge - 2 : edge + 1;

    /// <summary>
    /// Twice the area of the triangle three corners make, positive where they turn the way a filled triangle
    /// is wound.
    /// </summary>
    static double Turns(Vector2 a, Vector2 b, Vector2 c) =>
        (((double)b.X - a.X) * ((double)c.Y - a.Y)) - (((double)b.Y - a.Y) * ((double)c.X - a.X));
}
