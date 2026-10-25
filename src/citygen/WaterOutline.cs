using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The water as a map may carry it: cut to the map's own edges</b> (GEN-2b).
/// </summary>
/// <remarks>
/// <b>A shore is drawn past the town on purpose and cut afterwards.</b> A bank that closed inside the extent
/// would be a lake, and a sea has no far shore on the map at all, so the shape is laid well outside and this
/// is what makes it a shape the map can hold: the outline is also what the water is <em>drawn</em> from, and
/// an uncut ring paints open sea over the void beyond the world. It is the one place the cut is made, for the
/// towns that are generated and the fixtures that arrive as files alike.
/// </remarks>
internal static class WaterOutline
{
    /// <summary>
    /// The ring clipped against the four edges of the map, a half-plane at a time. Nothing is added that was
    /// not on the ring except the points where it crosses an edge, so a shore that was already inside comes
    /// back as it went in.
    /// </summary>
    public static Vector2[] CutToTheMap(ReadOnlySpan<Vector2> ringM, Vector2 extentM)
    {
        var ring = new List<Vector2>(ringM.Length * 2);
        ring.AddRange(ringM);

        var cut = new List<Vector2>(ring.Count);
        for (var edge = 0; edge < 4; edge++)
        {
            cut.Clear();
            for (var point = 0; point < ring.Count; point++)
            {
                var atM = ring[point];
                var nextM = ring[(point + 1) % ring.Count];
                var inside = Inside(atM, edge, extentM);
                if (inside) cut.Add(atM);
                if (inside != Inside(nextM, edge, extentM)) cut.Add(Crossing(atM, nextM, edge, extentM));
            }

            (ring, cut) = (cut, ring);
            if (ring.Count == 0) return [];
        }

        return [.. ring];
    }

    /// <summary>
    /// <b>Two rings joined into the one ring round both</b>, where the second reaches into the first and crosses its edge
    /// exactly twice — a river's mouth in the water it flows into. False, and nothing joined, for any other meeting: a
    /// river that never reaches the other, or one that wanders back over it.
    /// </summary>
    /// <remarks>
    /// <b>One ring and not two drawn over each other</b>: the water fades out over its shore from its edge, and an edge
    /// lying inside other water is a fade, and a line of foam, drawn across the open river. The join keeps each ring's
    /// own points and adds only the two crossings, walking each ring the way that leaves the other. <b>Each crossing is
    /// then rounded</b> to <paramref name="roundM"/> (<see cref="Rounded"/>): two banks meeting at a crossing meet at a
    /// point, and a point of land standing into the water reads as cut with a knife.
    /// </remarks>
    /// <param name="roundM">The radius each crossing is rounded to; none leaves it the point it is.</param>
    /// <param name="toleranceM">How far a chord of the rounding may stand off its circle.</param>
    public static bool Joined(ReadOnlySpan<Vector2> one, ReadOnlySpan<Vector2> other, float roundM, float toleranceM, out Vector2[] joined)
    {
        joined = [];
        var (minM, maxM) = Bounds(other);
        Span<RingCrossing> found = stackalloc RingCrossing[3];
        var crossings = 0;
        for (var i = 0; i < one.Length && crossings < found.Length; i++)
        {
            var (fromM, toM) = (one[i], one[(i + 1) % one.Length]);
            if (MathF.Max(fromM.X, toM.X) < minM.X || MathF.Min(fromM.X, toM.X) > maxM.X) continue;
            if (MathF.Max(fromM.Y, toM.Y) < minM.Y || MathF.Min(fromM.Y, toM.Y) > maxM.Y) continue;

            for (var j = 0; j < other.Length && crossings < found.Length; j++)
            {
                if (!Crosses(fromM, toM, other[j], other[(j + 1) % other.Length], out var alongOne, out var alongOther)) continue;

                found[crossings++] = new RingCrossing(i, alongOne, j, alongOther, Blend.Lerp(fromM, toM, alongOne));
            }
        }

        if (crossings != 2) return false;

        // Start where the first ring walks out of the second, so what it walks to the next crossing is its own outside.
        var (enter, leave) = (found[0], found[1]);
        if (Inside(other, Leaving(one, enter.One, enter.AlongOne, leave.One, leave.AlongOne, enter.AtM, leave.AtM))) (enter, leave) = (leave, enter);

        var ring = new List<Vector2>(one.Length + other.Length + 2) { enter.AtM };
        var count = Between(enter.One, enter.AlongOne, leave.One, leave.AlongOne, one.Length);
        for (var k = 0; k < count; k++) ring.Add(one[(enter.One + 1 + k) % one.Length]);

        ring.Add(leave.AtM);

        // And on round the second the way that leaves the first, whichever way round the second was drawn.
        var forward = Between(leave.Other, leave.AlongOther, enter.Other, enter.AlongOther, other.Length);
        if (!Inside(one, Leaving(other, leave.Other, leave.AlongOther, enter.Other, enter.AlongOther, leave.AtM, enter.AtM)))
        {
            for (var k = 0; k < forward; k++) ring.Add(other[(leave.Other + 1 + k) % other.Length]);
        }
        else
        {
            var backward = Between(enter.Other, enter.AlongOther, leave.Other, leave.AlongOther, other.Length);
            for (var k = 0; k < backward; k++) ring.Add(other[((leave.Other - k) % other.Length + other.Length) % other.Length]);
        }

        var rounded = Rounded(ring, ring.IndexOf(leave.AtM), roundM, toleranceM);
        joined = [.. Rounded(rounded, rounded.IndexOf(enter.AtM), roundM, toleranceM)];
        return true;
    }

    /// <summary>
    /// <b>A ring with one of its corners rounded</b>: the corner cut back along both sides as far as a circle of the
    /// radius touches them, and the circle's arc laid between. The ring as it was where the corner is no corner or a
    /// side is too short to cut.
    /// </summary>
    /// <remarks>
    /// <b>Each side's way is read a radius along it, not off its next point</b>: a bank is drawn through points a few
    /// metres apart, and its first chord off the corner is a crossing's own sliver. The ring that comes back starts at
    /// the arc, so a caller rounding two corners finds the second again by its place.
    /// </remarks>
    /// <summary>How near straight on, or near doubling back, two sides of a corner stand when it is left as it is.</summary>
    const float StraightWithinRad = 0.01f;

    static List<Vector2> Rounded(List<Vector2> ring, int corner, float radiusM, float toleranceM)
    {
        if (radiusM <= 0f || corner < 0) return ring;

        var cornerM = ring[corner];
        var (backM, _) = Walked(ring, corner, -1, radiusM);
        var (aheadM, _) = Walked(ring, corner, 1, radiusM);
        if (Vector2.DistanceSquared(backM, cornerM) <= 0f || Vector2.DistanceSquared(aheadM, cornerM) <= 0f) return ring;

        var back = Vector2.Normalize(backM - cornerM);
        var ahead = Vector2.Normalize(aheadM - cornerM);
        var halfRad = Trig.Acos(Math.Clamp(Vector2.Dot(back, ahead), -1f, 1f)) * 0.5f;
        if (halfRad <= StraightWithinRad || halfRad >= (MathF.PI * 0.5f) - StraightWithinRad) return ring;

        var cutM = radiusM / Trig.Tan(halfRad);
        var (fromM, lastBack) = Walked(ring, corner, -1, cutM);
        var (toM, lastAhead) = Walked(ring, corner, 1, cutM);
        var removed = ((corner - lastBack + ring.Count) % ring.Count) + ((lastAhead - corner + ring.Count) % ring.Count) + 1;
        if (lastBack < 0 || lastAhead < 0 || removed >= ring.Count - 2) return ring;

        var centreM = cornerM + (Vector2.Normalize(back + ahead) * (radiusM / Trig.Sin(halfRad)));
        var fromRad = Trig.Atan2(fromM.Y - centreM.Y, fromM.X - centreM.X);
        var sweepRad = Trig.Atan2(toM.Y - centreM.Y, toM.X - centreM.X) - fromRad;
        sweepRad -= MathF.Tau * MathF.Round(sweepRad / MathF.Tau);
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(sweepRad) / (2f * Trig.Acos(1f - MathF.Min(1f, toleranceM / radiusM)))));
        var (fromRadiusM, toRadiusM) = (Vector2.Distance(fromM, centreM), Vector2.Distance(toM, centreM));

        var rounded = new List<Vector2>(ring.Count - removed + steps + 1) { fromM };
        for (var step = 1; step < steps; step++)
        {
            var share = step / (float)steps;
            var (sin, cos) = Trig.SinCos(fromRad + (sweepRad * share));
            rounded.Add(centreM + (new Vector2(cos, sin) * (fromRadiusM + ((toRadiusM - fromRadiusM) * share))));
        }

        rounded.Add(toM);
        for (var at = (lastAhead + 1) % ring.Count; at != lastBack; at = (at + 1) % ring.Count) rounded.Add(ring[at]);

        return rounded;
    }

    /// <summary>
    /// The place a distance along a ring from one of its points, walking one way round, and the last of its points the
    /// walk reached — or that point at minus one where the ring is not that long.
    /// </summary>
    static (Vector2 AtM, int Reached) Walked(List<Vector2> ring, int from, int step, float distanceM)
    {
        var leftM = distanceM;
        var at = from;
        for (var k = 0; k + 1 < ring.Count; k++)
        {
            var next = (at + step + ring.Count) % ring.Count;
            var runM = Vector2.Distance(ring[at], ring[next]);
            if (runM >= leftM && runM > 0f) return (Blend.Lerp(ring[at], ring[next], leftM / runM), at);

            leftM -= runM;
            at = next;
        }

        return (ring[at], -1);
    }

    /// <summary>Where one ring's edge crosses another's: the edge of each and how far along it, and the place.</summary>
    readonly record struct RingCrossing(int One, float AlongOne, int Other, float AlongOther, Vector2 AtM);

    /// <summary>How many of a ring's points a walk forward from one place on it to another passes.</summary>
    static int Between(int fromEdge, float fromAlong, int toEdge, float toAlong, int points)
    {
        var count = ((toEdge - fromEdge) % points + points) % points;
        return count == 0 && toAlong < fromAlong ? points : count;
    }

    /// <summary>A place just past the start of a walk forward from one place on a ring toward another.</summary>
    static Vector2 Leaving(ReadOnlySpan<Vector2> ring, int fromEdge, float fromAlong, int toEdge, float toAlong, Vector2 fromM, Vector2 toM) =>
        Between(fromEdge, fromAlong, toEdge, toAlong, ring.Length) == 0
            ? (fromM + toM) * 0.5f
            : (fromM + ring[(fromEdge + 1) % ring.Length]) * 0.5f;

    /// <summary>Whether two runs cross, and how far along each.</summary>
    static bool Crosses(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out float alongA, out float alongB)
    {
        var (runA, runB, apart) = (a1 - a0, b1 - b0, b0 - a0);
        var denominator = Cross(runA, runB);
        (alongA, alongB) = (0f, 0f);
        if (denominator == 0f) return false;

        alongA = Cross(apart, runB) / denominator;
        alongB = Cross(apart, runA) / denominator;
        return alongA is >= 0f and < 1f && alongB is >= 0f and < 1f;
    }

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

    /// <summary>Whether a place is inside a ring, by how many of its edges a line from it crosses.</summary>
    static bool Inside(ReadOnlySpan<Vector2> ring, Vector2 atM)
    {
        var inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            if ((ring[i].Y > atM.Y) == (ring[j].Y > atM.Y)) continue;
            if (atM.X < ring[j].X + ((ring[i].X - ring[j].X) * (atM.Y - ring[j].Y) / (ring[i].Y - ring[j].Y))) inside = !inside;
        }

        return inside;
    }

    static (Vector2 MinM, Vector2 MaxM) Bounds(ReadOnlySpan<Vector2> ring)
    {
        var (minM, maxM) = (new Vector2(float.PositiveInfinity), new Vector2(float.NegativeInfinity));
        foreach (var pointM in ring) (minM, maxM) = (Vector2.Min(minM, pointM), Vector2.Max(maxM, pointM));
        return (minM, maxM);
    }

    /// <summary>Which side of one of the map's four edges a point stands, the inside being the map's own ground.</summary>
    static bool Inside(Vector2 atM, int edge, Vector2 extentM) => edge switch
    {
        0 => atM.X >= 0f,
        1 => atM.X <= extentM.X,
        2 => atM.Y >= 0f,
        _ => atM.Y <= extentM.Y,
    };

    /// <summary>Where a run between two points meets that edge.</summary>
    static Vector2 Crossing(Vector2 fromM, Vector2 toM, int edge, Vector2 extentM)
    {
        var atM = edge switch
        {
            0 => 0f,
            1 => extentM.X,
            2 => 0f,
            _ => extentM.Y,
        };

        var along = edge < 2
            ? (atM - fromM.X) / (toM.X - fromM.X)
            : (atM - fromM.Y) / (toM.Y - fromM.Y);
        var crossingM = Blend.Lerp(fromM, toM, Math.Clamp(along, 0f, 1f));

        // On the edge and not a rounding either side of it: the next edge's cut reads this point back, and a
        // shore a millionth of a metre off the map is still off the map.
        return edge < 2 ? crossingM with { X = atM } : crossingM with { Y = atM };
    }
}
