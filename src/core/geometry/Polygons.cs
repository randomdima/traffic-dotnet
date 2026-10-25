using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>The questions asked of a closed ring of points: whether it holds a point, how much it holds, and whether two cross.</summary>
internal static class Polygons
{
    /// <summary>Whether a ring holds a point, by the count of its edges a ray from the point crosses.</summary>
    public static bool Encloses(ReadOnlySpan<Vector2> ring, Vector2 atM)
    {
        var inside = false;
        for (int here = 0, there = ring.Length - 1; here < ring.Length; there = here++)
        {
            var (a, b) = (ring[here], ring[there]);
            if ((a.Y > atM.Y) != (b.Y > atM.Y) && atM.X < ((b.X - a.X) * (atM.Y - a.Y) / (b.Y - a.Y)) + a.X) inside = !inside;
        }

        return inside;
    }

    /// <summary>The ground a ring holds, signed by the way round it is walked: positive anticlockwise in a y-up frame.</summary>
    public static float SignedAreaM2(ReadOnlySpan<Vector2> ring)
    {
        var twiceM2 = 0f;
        for (int here = 0, there = ring.Length - 1; here < ring.Length; there = here++)
        {
            twiceM2 += (ring[there].X * ring[here].Y) - (ring[here].X * ring[there].Y);
        }

        return twiceM2 * 0.5f;
    }

    /// <summary>
    /// <b>Whether any edge of one ring crosses any edge of the other.</b> Two rings neither of which crosses the other are
    /// apart or one inside the other, which a caller asks of a point if it matters.
    /// </summary>
    public static bool Cross(ReadOnlySpan<Vector2> one, ReadOnlySpan<Vector2> other)
    {
        var (otherLeastM, otherMostM) = Box(other);
        var (leastM, mostM) = Box(one);
        if (Vector2.Max(leastM, otherLeastM) is var lowM && Vector2.Min(mostM, otherMostM) is var highM && (lowM.X > highM.X || lowM.Y > highM.Y))
        {
            return false;
        }

        for (var i = 0; i < one.Length; i++)
        {
            var (a, b) = (one[i], one[(i + 1) % one.Length]);
            if (MathF.Max(a.X, b.X) < otherLeastM.X || MathF.Min(a.X, b.X) > otherMostM.X) continue;
            if (MathF.Max(a.Y, b.Y) < otherLeastM.Y || MathF.Min(a.Y, b.Y) > otherMostM.Y) continue;

            for (var j = 0; j < other.Length; j++)
            {
                if (SegmentsCross(a, b, other[j], other[(j + 1) % other.Length])) return true;
            }
        }

        return false;
    }

    /// <summary>Whether two segments cross each other, each strictly between its own ends.</summary>
    public static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var (ab, cd) = (b - a, d - c);
        var denominator = Cross(ab, cd);
        if (MathF.Abs(denominator) < 1e-9f) return false;

        var along = Cross(c - a, cd) / denominator;
        var alongOther = Cross(c - a, ab) / denominator;
        return along is > 0f and < 1f && alongOther is > 0f and < 1f;
    }

    /// <summary>The least and the most corner of the box a run of points stands in.</summary>
    public static (Vector2 LeastM, Vector2 MostM) Box(ReadOnlySpan<Vector2> points)
    {
        var (leastM, mostM) = (new Vector2(float.PositiveInfinity), new Vector2(float.NegativeInfinity));
        foreach (var pointM in points) (leastM, mostM) = (Vector2.Min(leastM, pointM), Vector2.Max(mostM, pointM));

        return (leastM, mostM);
    }

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
}
