using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

internal static partial class ShellFill
{
    /// <summary>
    /// <b>One piece of the shell cut into triangles by clipping ears off it</b>: every corner that is convex
    /// and holds nothing else of the ring inside it comes off as a triangle, and the ring is a corner
    /// shorter. A ring of <c>n</c> corners gives <c>n − 2</c> triangles, which is what every triangulation of
    /// those corners gives and therefore the fewest there are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A hole is cut into the ring round it rather than held beside it</b> (<see cref="Bridged"/>), so
    /// what is clipped is always one ring with no holes — and the two corners that bridging adds are why a
    /// piece holding <c>h</c> holes comes to <c>n + 2h − 2</c> triangles.
    /// </para>
    /// <para>
    /// <b>The ear test is asked of the corners near the ear and not of all of them</b>
    /// (<see cref="IsEarNear"/>). Whether a corner is an ear is whether any reflex corner stands inside it,
    /// which is a question about the ring's own neighbourhood; asked of every corner of a town's boundary it
    /// is the square of forty thousand, and a city's ground went from a load to a lunch break. The corners
    /// are strung a second time along a Z-order curve, which brings a place's neighbours within reach of it
    /// in one dimension, and the scan stops at the ear's own bounding box.
    /// </para>
    /// <para>
    /// <b>It is the earcut algorithm</b> (Mapbox, ISC), written here in this project's own terms: ear
    /// clipping, holes bridged in at their leftmost corner, the Z-order index over the ears, and the two
    /// recovery passes a boundary that crosses itself needs. <b>The passes are not decoration</b> — a merge
    /// of bands can hand back a ring that touches itself at a corner, and a clipper with no recovery leaves
    /// the rest of that ring unfilled rather than saying so.
    /// </para>
    /// </remarks>
    static void Fill(ReadOnlySpan<Vector2> points, Ring outer, List<Ring> holes, List<int> into)
    {
        var ring = Linked(points, outer.From, outer.Count);
        if (ring is null || ring.Next == ring.Prev) return;

        if (holes.Count > 0) ring = Bridged(points, holes, ring, outer);

        // The box is the outer ring's, every hole being inside it, and it is what the Z-order is measured
        // over. A piece small enough that the scan is cheaper than the index is clipped without one.
        var spanM = Math.Max(outer.MaxM.X - outer.MinM.X, outer.MaxM.Y - outer.MinM.Y);
        var invSize = outer.Count > HashedAbove && spanM > 0f ? ZOrderSteps / spanM : 0.0;

        EarClip(ring, into, outer.MinM.X, outer.MinM.Y, invSize, 0);
    }

    /// <summary>
    /// How many corners a piece has to hold before the ears are indexed rather than scanned. Under it the
    /// index costs more to lay and sort than the scan it saves.
    /// </summary>
    const int HashedAbove = 80;

    /// <summary>
    /// How finely the Z-order curve divides the piece's own box: fifteen bits each way, which is what two
    /// interleaved coordinates fit in a signed int.
    /// </summary>
    const double ZOrderSteps = 32767.0;

    /// <summary>
    /// <b>Twice the area of the triangle three corners make, negative where they turn the way the ring they
    /// are on is walked</b> — so a corner of a filled ring is convex exactly where this is under nought.
    /// </summary>
    /// <remarks>
    /// In <c>double</c>, like every predicate here. The corners are metres from an origin a town away, and
    /// the difference of two products of them is the last two figures of a float.
    /// </remarks>
    static double Wind(Node p, Node q, Node r) => ((q.Y - p.Y) * (r.X - q.X)) - ((q.X - p.X) * (r.Y - q.Y));

    /// <summary>The ring clipped, and what is left of it handed to the next pass when nothing more comes off.</summary>
    /// <remarks>
    /// <b>Three passes and the third gives up by cutting the ring in two.</b> A ring with no ear left is a
    /// ring that crosses itself: the first pass rubs out the corners that turn through nothing, the second
    /// takes out the pairs that cross their own neighbours, and the third looks for a diagonal that splits
    /// what is left into two rings each of which may have an ear.
    /// </remarks>
    static void EarClip(Node? ear, List<int> into, double minX, double minY, double invSize, int pass)
    {
        if (ear is null) return;
        if (pass == 0 && invSize != 0.0) Curve(ear, minX, minY, invSize);

        var stop = ear;
        while (ear!.Prev != ear.Next)
        {
            var prev = ear.Prev;
            var next = ear.Next;

            if (invSize != 0.0 ? IsEarNear(ear, minX, minY, invSize) : IsEar(ear))
            {
                into.Add(prev.At);
                into.Add(ear.At);
                into.Add(next.At);

                Remove(ear);
                ear = next.Next;
                stop = next.Next;
                continue;
            }

            ear = next;
            if (ear != stop) continue;

            var left = Filtered(ear, null);
            if (left is not null)
            {
                if (pass == 0) EarClip(left, into, minX, minY, invSize, 1);
                else if (pass == 1) EarClip(Cured(left, into), into, minX, minY, invSize, 2);
                else if (pass == 2) SplitClip(left, into, minX, minY, invSize);
            }

            break;
        }
    }

    /// <summary>
    /// Whether a corner can come off as a triangle: convex, and with no corner of the ring that is itself
    /// reflex standing inside it.
    /// </summary>
    static bool IsEar(Node ear)
    {
        var a = ear.Prev;
        var b = ear;
        var c = ear.Next;
        if (Wind(a, b, c) >= 0.0) return false;

        var x0 = Math.Min(a.X, Math.Min(b.X, c.X));
        var y0 = Math.Min(a.Y, Math.Min(b.Y, c.Y));
        var x1 = Math.Max(a.X, Math.Max(b.X, c.X));
        var y1 = Math.Max(a.Y, Math.Max(b.Y, c.Y));

        var node = c.Next;
        while (node != a)
        {
            if (node.X >= x0 && node.X <= x1 && node.Y >= y0 && node.Y <= y1
                && Inside(a, b, c, node) && Wind(node.Prev, node, node.Next) >= 0.0)
            {
                return false;
            }

            node = node.Next;
        }

        return true;
    }

    /// <summary>
    /// The same question asked of the corners the Z-order curve puts near this one, walked outward from the
    /// ear in both directions and stopped at its own box.
    /// </summary>
    static bool IsEarNear(Node ear, double minX, double minY, double invSize)
    {
        var a = ear.Prev;
        var b = ear;
        var c = ear.Next;
        if (Wind(a, b, c) >= 0.0) return false;

        var x0 = Math.Min(a.X, Math.Min(b.X, c.X));
        var y0 = Math.Min(a.Y, Math.Min(b.Y, c.Y));
        var x1 = Math.Max(a.X, Math.Max(b.X, c.X));
        var y1 = Math.Max(a.Y, Math.Max(b.Y, c.Y));

        var leastZ = ZOrder(x0, y0, minX, minY, invSize);
        var mostZ = ZOrder(x1, y1, minX, minY, invSize);

        var back = ear.PrevZ;
        var on = ear.NextZ;

        while (back is not null && back.Z >= leastZ && on is not null && on.Z <= mostZ)
        {
            if (Blocks(back, a, b, c, x0, y0, x1, y1)) return false;
            back = back.PrevZ;

            if (Blocks(on, a, b, c, x0, y0, x1, y1)) return false;
            on = on.NextZ;
        }

        while (back is not null && back.Z >= leastZ)
        {
            if (Blocks(back, a, b, c, x0, y0, x1, y1)) return false;
            back = back.PrevZ;
        }

        while (on is not null && on.Z <= mostZ)
        {
            if (Blocks(on, a, b, c, x0, y0, x1, y1)) return false;
            on = on.NextZ;
        }

        return true;
    }

    /// <summary>Whether one corner is what stops a triangle being an ear.</summary>
    static bool Blocks(Node node, Node a, Node b, Node c, double x0, double y0, double x1, double y1) =>
        node != a && node != c
        && node.X >= x0 && node.X <= x1 && node.Y >= y0 && node.Y <= y1
        && Inside(a, b, c, node) && Wind(node.Prev, node, node.Next) >= 0.0;

    /// <summary>
    /// <b>The pairs of corners that cross their own neighbours, taken off as triangles</b> — the second
    /// pass's whole of what it does about a ring with no ear.
    /// </summary>
    static Node? Cured(Node start, List<int> into)
    {
        var node = start;
        do
        {
            var a = node.Prev;
            var b = node.Next.Next;

            if (!Same(a, b) && Crosses(a, node, node.Next, b) && LocallyInside(a, b) && LocallyInside(b, a))
            {
                into.Add(a.At);
                into.Add(node.At);
                into.Add(b.At);

                Remove(node);
                Remove(node.Next);
                node = start = b;
            }

            node = node.Next;
        }
        while (node != start);

        return Filtered(node, null);
    }

    /// <summary>
    /// <b>The ring cut in two on the first diagonal that is one</b>, each half clipped on its own. It is
    /// what is left to try when a ring has no ear and no crossing pair either.
    /// </summary>
    static void SplitClip(Node start, List<int> into, double minX, double minY, double invSize)
    {
        var a = start;
        do
        {
            var b = a.Next.Next;
            while (b != a.Prev)
            {
                if (a.At != b.At && ValidDiagonal(a, b))
                {
                    var other = Split(a, b);

                    var one = Filtered(a, a.Next);
                    var two = Filtered(other, other.Next);
                    EarClip(one, into, minX, minY, invSize, 0);
                    EarClip(two, into, minX, minY, invSize, 0);
                    return;
                }

                b = b.Next;
            }

            a = a.Next;
        }
        while (a != start);
    }

    /// <summary>
    /// <b>Every hole cut into the ring that holds it</b>, leftmost first: a hole is joined to the outer ring
    /// by a pair of corners standing on one another, so what comes back is one ring walked so that the
    /// hole's own ground stays outside it.
    /// </summary>
    /// <remarks>
    /// <b>Leftmost first because a bridge is found by casting a ray left from the hole.</b> A hole reached
    /// in that order can only ever bridge to the outer ring or to a hole already cut into it, both of which
    /// are one ring by then.
    /// </remarks>
    static Node Bridged(ReadOnlySpan<Vector2> points, List<Ring> holes, Node outer, Ring box)
    {
        var queue = new List<Node>(holes.Count);
        var corners = box.Count;
        foreach (var hole in holes)
        {
            var ring = Linked(points, hole.From, hole.Count);
            if (ring is null) continue;

            if (ring == ring.Next) ring.Steiner = true;
            queue.Add(Leftmost(ring));
            corners += hole.Count;
        }

        queue.Sort(static (one, other) => one.X.CompareTo(other.X));

        var rows = new EdgeRows(box.MinM.Y, box.MaxM.Y, corners);
        rows.AddRing(outer);
        Action<Node> refiled = rows.Add;
        foreach (var hole in queue)
        {
            var bridge = HoleBridge(hole, outer, rows);
            if (bridge is null) continue;

            // The hole's edges join the ring, and a split and a filter change the edge a corner leaves on:
            // each is filed again under the rows its new edge crosses.
            rows.AddRing(hole);
            var reverse = Split(bridge, hole);
            rows.Add(bridge);
            rows.Add(reverse);
            rows.Add(reverse.Next);
            Filtered(reverse, reverse.Next, refiled);
            outer = Filtered(bridge, bridge.Next, refiled) ?? outer;
        }

        return outer;
    }

    /// <summary>
    /// <b>Which corner of the ring a hole is joined to, asked of the corners near the hole</b>
    /// (<see cref="EdgeRows"/>) — the same corner <see cref="HoleBridge(Node, Node)"/> finds by walking the
    /// whole ring, and that walk itself wherever two corners tie and the order it meets them in decides.
    /// </summary>
    /// <remarks>
    /// <b>Walked for every hole, the join was the ring's corners times its holes</b>: a town thirty
    /// kilometres by twenty-three has a hundred thousand blocks in one ring of millions of corners, and its
    /// ground did not finish filling in a quarter of an hour.
    /// </remarks>
    static Node? HoleBridge(Node hole, Node outer, EdgeRows rows)
    {
        var holeX = hole.X;
        var holeY = hole.Y;
        var reachedX = double.MinValue;
        Node? found = null;
        var tied = false;

        foreach (var node in rows.Crossing(holeY, holeY))
        {
            if (holeY > node.Y || holeY < node.Next.Y || node.Next.Y == node.Y) continue;

            var atX = node.X + ((holeY - node.Y) * (node.Next.X - node.X) / (node.Next.Y - node.Y));
            if (atX > holeX) continue;

            if (atX > reachedX)
            {
                reachedX = atX;
                found = node.X < node.Next.X ? node : node.Next;
                tied = false;
            }
            else if (atX == reachedX)
            {
                tied = true;
            }
        }

        if (tied) return HoleBridge(hole, outer);
        if (found is null) return null;
        if (reachedX == holeX) return found;

        var foundX = found.X;
        var foundY = found.Y;
        var leastTan = double.MaxValue;
        var best = found;
        foreach (var node in rows.Crossing(Math.Min(holeY, foundY), Math.Max(holeY, foundY)))
        {
            var above = holeY < foundY;
            if (!(holeX >= node.X && node.X >= foundX && holeX != node.X
                  && Inside(
                      above ? holeX : reachedX, holeY, foundX, foundY, above ? reachedX : holeX, holeY,
                      node.X, node.Y)))
            {
                continue;
            }

            var tan = Math.Abs(holeY - node.Y) / (holeX - node.X);
            if (!LocallyInside(node, hole)) continue;

            if (tan < leastTan)
            {
                leastTan = tan;
                best = node;
                tied = false;
            }
            else if (tan == leastTan)
            {
                tied = true;
            }
        }

        return tied ? HoleBridge(hole, outer) : best;
    }

    /// <summary>
    /// <b>The ring's corners filed by the rows of the piece's own box their edge crosses</b> — so a hole asks
    /// the edges beside it rather than the ring. An edge a corner no longer leaves on stays filed and is
    /// passed over when it is read, so what a row hands back is never short of an edge that crosses it.
    /// </summary>
    sealed class EdgeRows
    {
        readonly double _fromY;
        readonly double _rowM;
        readonly List<Node>[] _rows;
        readonly List<Node> _read = [];
        int _stamp;

        /// <param name="corners">How many corners the ring will hold, which the rows are cut to: as many rows as the square root of that, so a row holds as many.</param>
        public EdgeRows(float fromY, float toY, int corners)
        {
            _rows = new List<Node>[Math.Max(1, (int)Math.Sqrt(corners))];
            for (var row = 0; row < _rows.Length; row++) _rows[row] = [];

            _fromY = fromY;
            _rowM = Math.Max(((double)toY - fromY) / _rows.Length, double.Epsilon);
        }

        public void Add(Node node)
        {
            var to = Row(Math.Max(node.Y, node.Next.Y));
            for (var row = Row(Math.Min(node.Y, node.Next.Y)); row <= to; row++) _rows[row].Add(node);
        }

        public void AddRing(Node start)
        {
            var node = start;
            do
            {
                Add(node);
                node = node.Next;
            }
            while (node != start);
        }

        /// <summary>Every corner still on the ring whose edge was filed under a row between two heights, each once.</summary>
        public List<Node> Crossing(double fromY, double toY)
        {
            _stamp++;
            _read.Clear();
            var to = Row(toY);
            for (var row = Row(fromY); row <= to; row++)
            {
                foreach (var node in _rows[row])
                {
                    if (node.Seen == _stamp || node.Prev.Next != node) continue;

                    node.Seen = _stamp;
                    _read.Add(node);
                }
            }

            return _read;
        }

        int Row(double y) => Math.Clamp((int)Math.Floor((y - _fromY) / _rowM), 0, _rows.Length - 1);
    }

    /// <summary>
    /// <b>Which corner of the ring a hole is joined to</b>: the ray cast left from the hole's leftmost
    /// corner names an edge, and the corner is that edge's own or whichever reflex corner between them the
    /// hole can see at the shallower angle.
    /// </summary>
    static Node? HoleBridge(Node hole, Node outer)
    {
        var node = outer;
        var holeX = hole.X;
        var holeY = hole.Y;
        var reachedX = double.MinValue;
        Node? found = null;

        do
        {
            if (holeY <= node.Y && holeY >= node.Next.Y && node.Next.Y != node.Y)
            {
                var atX = node.X + ((holeY - node.Y) * (node.Next.X - node.X) / (node.Next.Y - node.Y));
                if (atX <= holeX && atX > reachedX)
                {
                    reachedX = atX;
                    found = node.X < node.Next.X ? node : node.Next;

                    // The hole stands on the edge itself: its own end of it is the join, and no corner
                    // between them can be nearer than nothing.
                    if (atX == holeX) return found;
                }
            }

            node = node.Next;
        }
        while (node != outer);

        if (found is null) return null;

        var stop = found;
        var foundX = found.X;
        var foundY = found.Y;
        var leastTan = double.MaxValue;

        node = found;
        do
        {
            var above = holeY < foundY;
            if (holeX >= node.X && node.X >= foundX && holeX != node.X
                && Inside(
                    above ? holeX : reachedX, holeY, foundX, foundY, above ? reachedX : holeX, holeY,
                    node.X, node.Y))
            {
                var tan = Math.Abs(holeY - node.Y) / (holeX - node.X);
                if (LocallyInside(node, hole)
                    && (tan < leastTan || (tan == leastTan && (node.X > found.X || Wraps(found, node)))))
                {
                    found = node;
                    leastTan = tan;
                }
            }

            node = node.Next;
        }
        while (node != stop);

        return found;
    }

    /// <summary>Whether the corner one turns through holds the corner another turns through.</summary>
    static bool Wraps(Node one, Node other) =>
        Wind(one.Prev, one, other.Prev) < 0.0 && Wind(other.Next, one, one.Next) < 0.0;

    /// <summary>Whether the straight between two corners of a ring is a diagonal of it and not a chord over its outside.</summary>
    static bool ValidDiagonal(Node a, Node b) =>
        a.Next.At != b.At && a.Prev.At != b.At && !CrossesRing(a, b)
        && ((LocallyInside(a, b) && LocallyInside(b, a) && MiddleInside(a, b)
                && (Wind(a.Prev, a, b.Prev) != 0.0 || Wind(a, b.Prev, b) != 0.0))
            || (Same(a, b) && Wind(a.Prev, a, a.Next) > 0.0 && Wind(b.Prev, b, b.Next) > 0.0));

    /// <summary>Whether a straight leaving one corner sets off into the ring's own ground.</summary>
    static bool LocallyInside(Node a, Node b) =>
        Wind(a.Prev, a, a.Next) < 0.0
            ? Wind(a, b, a.Next) >= 0.0 && Wind(a, a.Prev, b) >= 0.0
            : Wind(a, b, a.Prev) < 0.0 || Wind(a, a.Next, b) < 0.0;

    /// <summary>Whether the middle of the straight between two corners stands inside the ring.</summary>
    static bool MiddleInside(Node a, Node b)
    {
        var node = a;
        var inside = false;
        var midX = (a.X + b.X) * 0.5;
        var midY = (a.Y + b.Y) * 0.5;

        do
        {
            if (node.Y > midY != node.Next.Y > midY && node.Next.Y != node.Y
                && midX < ((node.Next.X - node.X) * (midY - node.Y) / (node.Next.Y - node.Y)) + node.X)
            {
                inside = !inside;
            }

            node = node.Next;
        }
        while (node != a);

        return inside;
    }

    /// <summary>Whether two straights cross, the ends that touch counted as crossings.</summary>
    static bool Crosses(Node p1, Node q1, Node p2, Node q2)
    {
        var o1 = Sign(Wind(p1, q1, p2));
        var o2 = Sign(Wind(p1, q1, q2));
        var o3 = Sign(Wind(p2, q2, p1));
        var o4 = Sign(Wind(p2, q2, q1));

        if (o1 != o2 && o3 != o4) return true;
        if (o1 == 0 && OnSegment(p1, p2, q1)) return true;
        if (o2 == 0 && OnSegment(p1, q2, q1)) return true;
        if (o3 == 0 && OnSegment(p2, p1, q2)) return true;

        return o4 == 0 && OnSegment(p2, q1, q2);
    }

    /// <summary>Whether the straight between two corners crosses any edge of the ring they are on.</summary>
    static bool CrossesRing(Node a, Node b)
    {
        var node = a;
        do
        {
            if (node.At != a.At && node.Next.At != a.At && node.At != b.At && node.Next.At != b.At
                && Crosses(node, node.Next, a, b))
            {
                return true;
            }

            node = node.Next;
        }
        while (node != a);

        return false;
    }

    static bool OnSegment(Node p, Node q, Node r) =>
        q.X <= Math.Max(p.X, r.X) && q.X >= Math.Min(p.X, r.X)
        && q.Y <= Math.Max(p.Y, r.Y) && q.Y >= Math.Min(p.Y, r.Y);

    static int Sign(double value) => value > 0.0 ? 1 : value < 0.0 ? -1 : 0;

    static bool Same(Node one, Node other) => one.X == other.X && one.Y == other.Y;

    static bool Inside(Node a, Node b, Node c, Node p) => Inside(a.X, a.Y, b.X, b.Y, c.X, c.Y, p.X, p.Y);

    static bool Inside(
        double ax, double ay, double bx, double by, double cx, double cy, double px, double py) =>
        (cx - px) * (ay - py) >= (ax - px) * (cy - py)
        && (ax - px) * (by - py) >= (bx - px) * (ay - py)
        && (bx - px) * (cy - py) >= (cx - px) * (by - py);

    /// <summary>
    /// The corners of one ring rubbed out where they turn through nothing or stand where the one before
    /// them stands, which is what a bridge and a clip both leave behind.
    /// </summary>
    /// <param name="refiled">Told of every corner whose edge a removal lengthened, for an index of the edges to file it again.</param>
    static Node? Filtered(Node? start, Node? end, Action<Node>? refiled = null)
    {
        if (start is null) return null;

        end ??= start;
        var node = start;
        bool again;

        do
        {
            again = false;
            if (!node.Steiner && (Same(node, node.Next) || Wind(node.Prev, node, node.Next) == 0.0))
            {
                Remove(node);
                refiled?.Invoke(node.Prev);
                node = end = node.Prev;
                if (node == node.Next) break;

                again = true;
            }
            else
            {
                node = node.Next;
            }
        }
        while (again || node != end);

        return end;
    }

    /// <summary>One stretch of points strung into a ring, walked the way it was given.</summary>
    /// <remarks>
    /// <b>Nothing is turned round here.</b> Which way a ring goes is which kind of ring it is
    /// (<see cref="Ring"/>), and it arrives walked the way its own ground says.
    /// </remarks>
    static Node? Linked(ReadOnlySpan<Vector2> points, int from, int count)
    {
        Node? last = null;
        for (var at = 0; at < count; at++) last = Insert(from + at, points[from + at], last);

        if (last is not null && Same(last, last.Next))
        {
            Remove(last);
            last = last.Next;
        }

        return last;
    }

    static Node Insert(int at, Vector2 pointM, Node? after)
    {
        var node = new Node(at, pointM);
        if (after is null)
        {
            node.Prev = node;
            node.Next = node;
            return node;
        }

        node.Next = after.Next;
        node.Prev = after;
        after.Next.Prev = node;
        after.Next = node;
        return node;
    }

    static void Remove(Node node)
    {
        node.Next.Prev = node.Prev;
        node.Prev.Next = node.Next;

        if (node.PrevZ is not null) node.PrevZ.NextZ = node.NextZ;
        if (node.NextZ is not null) node.NextZ.PrevZ = node.PrevZ;
    }

    /// <summary>
    /// One ring cut into two along the straight between two of its corners, each of which becomes two
    /// corners standing at one place.
    /// </summary>
    static Node Split(Node a, Node b)
    {
        var a2 = new Node(a.At, a.PointM);
        var b2 = new Node(b.At, b.PointM);
        var afterA = a.Next;
        var beforeB = b.Prev;

        a.Next = b;
        b.Prev = a;

        a2.Next = afterA;
        afterA.Prev = a2;

        b2.Next = a2;
        a2.Prev = b2;

        beforeB.Next = b2;
        b2.Prev = beforeB;

        return b2;
    }

    static Node Leftmost(Node start)
    {
        var node = start;
        var leftmost = start;

        do
        {
            if (node.X < leftmost.X || (node.X == leftmost.X && node.Y < leftmost.Y)) leftmost = node;
            node = node.Next;
        }
        while (node != start);

        return leftmost;
    }

    /// <summary>The ring strung a second time in Z-order, which is what puts a corner's neighbours in reach of it.</summary>
    static void Curve(Node start, double minX, double minY, double invSize)
    {
        var node = start;
        do
        {
            if (node.Z < 0) node.Z = ZOrder(node.X, node.Y, minX, minY, invSize);
            node.PrevZ = node.Prev;
            node.NextZ = node.Next;
            node = node.Next;
        }
        while (node != start);

        node.PrevZ!.NextZ = null;
        node.PrevZ = null;
        SortZ(node);
    }

    /// <summary>The Z-order list sorted, bottom up, which a linked list can be without an array to hold it.</summary>
    static void SortZ(Node? list)
    {
        var inSize = 1;
        int merges;

        do
        {
            var p = list;
            list = null;
            Node? tail = null;
            merges = 0;

            while (p is not null)
            {
                merges++;
                var q = p;
                var pSize = 0;
                for (var step = 0; step < inSize && q is not null; step++)
                {
                    pSize++;
                    q = q.NextZ;
                }

                var qSize = inSize;
                while (pSize > 0 || (qSize > 0 && q is not null))
                {
                    Node taken;
                    if (pSize == 0)
                    {
                        taken = q!;
                        q = q!.NextZ;
                        qSize--;
                    }
                    else if (qSize == 0 || q is null)
                    {
                        taken = p!;
                        p = p!.NextZ;
                        pSize--;
                    }
                    else if (p!.Z <= q.Z)
                    {
                        taken = p;
                        p = p.NextZ;
                        pSize--;
                    }
                    else
                    {
                        taken = q;
                        q = q.NextZ;
                        qSize--;
                    }

                    if (tail is not null) tail.NextZ = taken;
                    else list = taken;

                    taken.PrevZ = tail;
                    tail = taken;
                }

                p = q;
            }

            if (tail is not null) tail.NextZ = null;
            inSize *= 2;
        }
        while (merges > 1);
    }

    /// <summary>
    /// A place on the Z-order curve: the two coordinates over the piece's own box, each spread to every
    /// other bit and interleaved.
    /// </summary>
    static int ZOrder(double xM, double yM, double minX, double minY, double invSize)
    {
        var x = (int)((xM - minX) * invSize);
        var y = (int)((yM - minY) * invSize);

        x = (x | (x << 8)) & 0x00FF00FF;
        x = (x | (x << 4)) & 0x0F0F0F0F;
        x = (x | (x << 2)) & 0x33333333;
        x = (x | (x << 1)) & 0x55555555;

        y = (y | (y << 8)) & 0x00FF00FF;
        y = (y | (y << 4)) & 0x0F0F0F0F;
        y = (y | (y << 2)) & 0x33333333;
        y = (y | (y << 1)) & 0x55555555;

        return x | (y << 1);
    }

    /// <summary>
    /// One corner of a ring while it is being clipped: where it is, which point of the shell it is, and its
    /// two neighbours round the ring and along the Z-order curve.
    /// </summary>
    /// <remarks>
    /// <b>It is laying scratch and nothing holds one afterwards</b> — what comes back from a fill is points
    /// and indices. The coordinates are carried in <c>double</c> beside the point they came from, because
    /// every predicate the clipping asks is a difference of products of them.
    /// </remarks>
    sealed class Node(int at, Vector2 pointM)
    {
        /// <summary>Which point of the shell this corner is. Two corners of a bridge share one.</summary>
        public readonly int At = at;

        public readonly Vector2 PointM = pointM;

        public readonly double X = pointM.X;

        public readonly double Y = pointM.Y;

        public Node Prev = null!;

        public Node Next = null!;

        /// <summary>Where this corner falls on the Z-order curve, or under nought before it is placed.</summary>
        public int Z = -1;

        public Node? PrevZ;

        public Node? NextZ;

        /// <summary>The last read of <see cref="EdgeRows"/> that handed this corner back, so no read hands it back twice.</summary>
        public int Seen;

        /// <summary>
        /// Whether this corner may never be rubbed out: a hole that came to one point is still a hole, and
        /// a filter that turns through nothing at it would drop the whole of it.
        /// </summary>
        public bool Steiner;
    }
}
