using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>A line the generator lays a road along before it is a road</b>: points, and how far along it each stands. An
/// intercity road's route, a district's edge and a link across a barrier are each one (<see cref="Paths"/>).
/// </summary>
internal sealed class Polyline
{
    public Polyline(Vector2[] pointsM)
    {
        PointsM = pointsM;
        AlongM = new float[pointsM.Length];
        for (var point = 1; point < pointsM.Length; point++) AlongM[point] = AlongM[point - 1] + Vector2.Distance(pointsM[point - 1], pointsM[point]);
    }

    public Vector2[] PointsM { get; }

    /// <summary>How far along the line each point stands.</summary>
    public float[] AlongM { get; }

    public float LengthM => AlongM.Length == 0 ? 0f : AlongM[^1];

    public Vector2 FirstM => PointsM[0];

    public Vector2 LastM => PointsM[^1];

    /// <summary>The place this far along it, held to its ends.</summary>
    public Vector2 At(float alongM)
    {
        var piece = PieceAt(alongM, out var share);
        return Blend.Lerp(PointsM[piece], PointsM[piece + 1], share);
    }

    /// <summary>The way it runs this far along it.</summary>
    public Vector2 Heading(float alongM)
    {
        var piece = PieceAt(alongM, out _);
        var runM = PointsM[piece + 1] - PointsM[piece];
        return runM.LengthSquared() > 0f ? Vector2.Normalize(runM) : Vector2.UnitX;
    }

    int PieceAt(float alongM, out float share)
    {
        if (PointsM.Length < 2)
        {
            share = 0f;
            return 0;
        }

        var at = Array.BinarySearch(AlongM, alongM);
        var piece = Math.Clamp(at >= 0 ? at : ~at - 1, 0, PointsM.Length - 2);
        var spanM = AlongM[piece + 1] - AlongM[piece];
        share = spanM > 0f ? Math.Clamp((alongM - AlongM[piece]) / spanM, 0f, 1f) : 0f;
        return piece;
    }

    /// <summary>How far along it a straight first crosses it, or none where it does not.</summary>
    public float? Crossed(Vector2 fromM, Vector2 toM)
    {
        var run = toM - fromM;
        var (best, bestShare) = ((float?)null, float.PositiveInfinity);
        for (var piece = 0; piece + 1 < PointsM.Length; piece++)
        {
            var leg = PointsM[piece + 1] - PointsM[piece];
            var denominator = (run.X * leg.Y) - (run.Y * leg.X);
            if (MathF.Abs(denominator) < 1e-9f) continue;

            var offM = PointsM[piece] - fromM;
            var share = ((offM.X * leg.Y) - (offM.Y * leg.X)) / denominator;
            var legShare = ((offM.X * run.Y) - (offM.Y * run.X)) / denominator;
            if (share is < 0f or > 1f || legShare is < 0f or > 1f || share >= bestShare) continue;

            (best, bestShare) = (AlongM[piece] + (legShare * leg.Length()), share);
        }

        return best;
    }

    /// <summary>How far along it the place nearest a point stands, and how far off it the point is.</summary>
    public (float AlongM, float AwayM) Nearest(Vector2 pointM)
    {
        var (bestAlongM, bestM2) = (0f, float.PositiveInfinity);
        for (var piece = 0; piece + 1 < PointsM.Length; piece++)
        {
            var runM = PointsM[piece + 1] - PointsM[piece];
            var lengthM2 = runM.LengthSquared();
            var share = lengthM2 > 0f ? Math.Clamp(Vector2.Dot(pointM - PointsM[piece], runM) / lengthM2, 0f, 1f) : 0f;
            var awayM2 = (PointsM[piece] + (runM * share) - pointM).LengthSquared();
            if (awayM2 >= bestM2) continue;

            (bestAlongM, bestM2) = (AlongM[piece] + (share * MathF.Sqrt(lengthM2)), awayM2);
        }

        return (bestAlongM, MathF.Sqrt(bestM2));
    }

    /// <summary>How near the last a point a road is laid through may stand: nearer, the two make a leg no corner fits on.</summary>
    const float PointsApartLeastM = 1f;

    /// <summary>The points of it strictly between two distances along it, each kept clear of both by a margin, and of each other.</summary>
    public Vector2[] Between(float fromM, float toM, float clearM)
    {
        var (lowM, highM) = fromM <= toM ? (fromM + clearM, toM - clearM) : (toM + clearM, fromM - clearM);
        var between = new List<Vector2>();
        var lastM = float.NegativeInfinity;
        for (var point = 0; point < PointsM.Length; point++)
        {
            if (AlongM[point] <= lowM || AlongM[point] >= highM || AlongM[point] - lastM < PointsApartLeastM) continue;

            between.Add(PointsM[point]);
            lastM = AlongM[point];
        }

        if (fromM > toM) between.Reverse();
        return [.. between];
    }

    /// <summary>
    /// <b>A run of points thinned to the fewest that keep every one of them within a tolerance</b> (Douglas–Peucker), its
    /// ends kept.
    /// </summary>
    public static List<Vector2> Thinned(IReadOnlyList<Vector2> pointsM, float toleranceM)
    {
        if (pointsM.Count < 3) return [.. pointsM];

        var kept = new bool[pointsM.Count];
        (kept[0], kept[^1]) = (true, true);
        var spans = new Stack<(int From, int To)>();
        spans.Push((0, pointsM.Count - 1));
        while (spans.Count > 0)
        {
            var (from, to) = spans.Pop();
            var (furthest, furthestM) = (-1, toleranceM);
            for (var point = from + 1; point < to; point++)
            {
                var offM = OffM(pointsM[point], pointsM[from], pointsM[to]);
                if (offM > furthestM) (furthest, furthestM) = (point, offM);
            }

            if (furthest < 0) continue;

            kept[furthest] = true;
            spans.Push((from, furthest));
            spans.Push((furthest, to));
        }

        var thinned = new List<Vector2>();
        for (var point = 0; point < pointsM.Count; point++)
        {
            if (kept[point]) thinned.Add(pointsM[point]);
        }

        return thinned;
    }

    /// <summary>A run of points walked again at an even step from its first to its last, which it keeps.</summary>
    public static List<Vector2> Resampled(IReadOnlyList<Vector2> pointsM, float stepM)
    {
        var line = new Polyline([.. pointsM]);
        var steps = Math.Max(1, (int)MathF.Round(line.LengthM / stepM));
        var walked = new List<Vector2>(steps + 1);
        for (var step = 0; step < steps; step++) walked.Add(line.At(line.LengthM * step / steps));
        walked.Add(pointsM[^1]);
        return walked;
    }

    /// <summary>
    /// <b>A run of points with its corners cut</b> (Chaikin): each straight between two points replaced by the two points a
    /// quarter of the way in from either end, as many times as asked, its own two ends kept — so a line traced off cells
    /// comes out as the smooth curve the cells were cut along, and not as their steps.
    /// </summary>
    public static List<Vector2> Cut(IReadOnlyList<Vector2> pointsM, int times)
    {
        var cut = new List<Vector2>(pointsM);
        for (var time = 0; time < times && cut.Count > 2; time++)
        {
            var next = new List<Vector2>(cut.Count * 2) { cut[0] };
            for (var point = 0; point + 1 < cut.Count; point++)
            {
                next.Add(Blend.Lerp(cut[point], cut[point + 1], 0.25f));
                next.Add(Blend.Lerp(cut[point], cut[point + 1], 0.75f));
            }

            next.Add(cut[^1]);
            cut = next;
        }

        return cut;
    }

    /// <summary>How far a point stands off the straight through two others.</summary>
    static float OffM(Vector2 pointM, Vector2 aM, Vector2 bM)
    {
        var runM = bM - aM;
        var lengthM = runM.Length();
        if (lengthM <= 0f) return Vector2.Distance(pointM, aM);

        return MathF.Abs(((pointM.X - aM.X) * runM.Y) - ((pointM.Y - aM.Y) * runM.X)) / lengthM;
    }
}
