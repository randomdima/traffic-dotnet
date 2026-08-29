using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// A stretch of one way's line, sampled at the clearance it is going to be compared at: the points, the
/// metre the first of them stands at, and the step between them.
/// </summary>
/// <remarks>
/// <b>A stretch and not the whole way</b>, because the two things compared are regularly not the same size.
/// A join is a dozen metres and samples whole; a lane is two hundred and would sample at eight-metre steps
/// under the same budget, which is a section opened out by eight metres at either end — a grant cut half a
/// street early. What a short way can touch of a long one is a window around it, and that is what is sampled.
/// </remarks>
/// <param name="LengthM">The whole way's length, which is what a section is clamped to however little of it was looked at.</param>
internal readonly ref struct SampledWay(ReadOnlySpan<Vector2> pointsM, float fromM, float stepM, float LengthM)
{
    public readonly ReadOnlySpan<Vector2> PointsM = pointsM;

    public readonly float FromM = fromM;

    public readonly float StepM = stepM;

    public readonly float WholeM = LengthM;
}

/// <summary>
/// <b>Where two of the town's lines are driven over each other</b>, measured off the lines and off nothing
/// else (TER-5c) — the one implementation of the question, so that a junction's joins and the way into a
/// parking space are answered the same way.
/// </summary>
/// <remarks>
/// <para>
/// <b>The measurement is between the two lines and not between their crossing points</b>: two movements
/// that pass within a car's width never touch each other's paint and still cannot both be made, so the
/// question is how near the lines come rather than whether they intersect.
/// </para>
/// <para>
/// <b>Each of the pair takes a section of the other, and the two are measured apart.</b> A long line and a
/// short one crossing it do not cover the same length of each other, and giving both the same interval
/// would hand the short one the whole of the long one.
/// </para>
/// </remarks>
internal static class LineOverlap
{
    /// <summary>
    /// How many points a stretch is measured as. <b>A bound on the work and not a figure behaviour
    /// reads</b>: past it the step opens out, which reads the stretch a few centimetres coarser.
    /// </summary>
    public const int MostSamples = 24;

    /// <summary>
    /// One stretch of one way, sampled into the caller's span. <b>A crossing's ends are read to the step it
    /// was sampled at</b> (<see cref="Covered"/>), so the step is what the caller wants a section measured
    /// to: the clearance itself where a line is long and the ground either side of it is a junction, and
    /// finer where a few metres either way is the whole of what is being asked about.
    /// </summary>
    /// <param name="wantedStepM">
    /// The coarsest step this stretch may be walked at. It is only ever made finer — the budget
    /// (<see cref="MostSamples"/>) is a ceiling on the count and never a floor on the step.
    /// </param>
    public static int Sample(
        ReadOnlySpan<ArcSeg> arcs, float fromM, float toM, float lengthM, float wantedStepM, Span<Vector2> into,
        out float stepM)
    {
        stepM = 0f;
        if (arcs.Length == 0) return 0;

        fromM = MathF.Max(0f, fromM);
        toM = MathF.Min(lengthM, toM);
        if (toM <= fromM) return 0;

        var count = Math.Clamp((int)((toM - fromM) / wantedStepM) + 2, 2, MostSamples);
        stepM = (toM - fromM) / (count - 1);
        for (var at = 0; at < count; at++) into[at] = Spline.SampleAt(arcs, fromM + (stepM * at)).PositionM;

        return count;
    }

    /// <summary>
    /// The two sections one crossing makes, in each way's own metres, or <see langword="false"/> where the
    /// two lines stay a clearance apart everywhere they were looked at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Asked both ways round, and a pair either of them finds is kept</b>: the samples fall a clearance
    /// apart, so one direction can come up empty at the margin where the other did not — two lines crossing
    /// square is exactly the case, since the samples either side of the meeting point can both stand a
    /// clearance and a half from it. Dropping the pair for that would be a crossing nothing refuses.
    /// </para>
    /// <para>
    /// <b>The missing end is the found one's shadow, and never the whole way.</b> A crossing found from one
    /// side is a stretch of ground, and where it falls on the other line is the samples of that line
    /// standing nearest it. Taken as the whole way instead — which is right enough for two joins a dozen
    /// metres long — a way at a bay took the whole of the street it crossed.
    /// </para>
    /// </remarks>
    public static bool Measure(
        in SampledWay a, in SampledWay b, float clearanceM, out (float FromM, float ToM) onA,
        out (float FromM, float ToM) onB)
    {
        onA = default;
        onB = default;
        if (a.PointsM.Length < 2 || b.PointsM.Length < 2) return false;

        var overA = Covered(a, b, clearanceM, out onB);
        if (!Covered(b, a, clearanceM, out onA))
        {
            if (!overA) return false;

            return Shadow(b, onB, a, clearanceM, out onA);
        }

        return overA || Shadow(a, onA, b, clearanceM, out onB);
    }

    /// <summary>
    /// Where a stretch of one line falls on another: the samples of <paramref name="onto"/> standing
    /// nearest the samples of <paramref name="of"/> that the stretch covers, opened out by a step either
    /// way like every other section. <b>False where none of them stands near enough</b>, which is the
    /// crossing turning out to be an artefact of where the samples fell and not a crossing at all.
    /// </summary>
    static bool Shadow(
        in SampledWay of, (float FromM, float ToM) section, in SampledWay onto, float clearanceM,
        out (float FromM, float ToM) shadow)
    {
        // The pair has to be as near as the crossing the section stands for, allowing for the step the
        // nearest sample can fall short by. Unbounded, two lines running parallel a lane apart shadow the
        // whole of each other, because every sample of one has a nearest sample on the other.
        var reachM = clearanceM + onto.StepM;
        var leastAt = int.MaxValue;
        var mostAt = int.MinValue;
        for (var at = 0; at < of.PointsM.Length; at++)
        {
            var alongM = of.FromM + (at * of.StepM);
            if (alongM < section.FromM - of.StepM || alongM > section.ToM + of.StepM) continue;

            var nearest = -1;
            var nearestM = reachM * reachM;
            for (var other = 0; other < onto.PointsM.Length; other++)
            {
                var apartM = (onto.PointsM[other] - of.PointsM[at]).LengthSquared();
                if (apartM >= nearestM) continue;

                nearestM = apartM;
                nearest = other;
            }

            if (nearest < 0) continue;

            leastAt = Math.Min(leastAt, nearest);
            mostAt = Math.Max(mostAt, nearest);
        }

        if (mostAt < leastAt)
        {
            shadow = default;
            return false;
        }

        shadow = (
            MathF.Max(0f, onto.FromM + (leastAt * onto.StepM) - onto.StepM),
            MathF.Min(onto.WholeM, onto.FromM + (mostAt * onto.StepM) + onto.StepM));
        return true;
    }

    /// <summary>
    /// Which metres of <paramref name="crossed"/> come within the clearance of <paramref name="over"/>, as
    /// the one interval spanning them: a pair of lines that touch twice is one movement driven over the
    /// ground between, and two sections with a gap in the middle would leave that ground free.
    /// </summary>
    static bool Covered(in SampledWay over, in SampledWay crossed, float clearanceM, out (float FromM, float ToM) section)
    {
        var leastAt = -1;
        var mostAt = -1;
        for (var at = 0; at < crossed.PointsM.Length; at++)
        {
            if (ToChainM(over.PointsM, crossed.PointsM[at]) > clearanceM) continue;

            if (leastAt < 0) leastAt = at;
            mostAt = at;
        }

        if (leastAt < 0)
        {
            section = default;
            return false;
        }

        // The samples are the ends of the section and the body crossing has width, so it is opened out by a
        // step either way: the true crossing lies between the last sample outside and the first one in.
        section = (
            MathF.Max(0f, crossed.FromM + (leastAt * crossed.StepM) - crossed.StepM),
            MathF.Min(crossed.WholeM, crossed.FromM + (mostAt * crossed.StepM) + crossed.StepM));
        return true;
    }

    /// <summary>
    /// How far a point stands off a whole polyline — every piece of it measured, which for a chain this
    /// short is cheaper than working out which piece to measure.
    /// </summary>
    public static float ToChainM(ReadOnlySpan<Vector2> chain, Vector2 pointM)
    {
        var leastM = float.PositiveInfinity;
        for (var at = 0; at + 1 < chain.Length; at++)
        {
            leastM = MathF.Min(leastM, ToSegmentM(chain[at], chain[at + 1], pointM));
        }

        return leastM;
    }

    /// <summary>How far a point stands off a straight between two others.</summary>
    static float ToSegmentM(Vector2 fromM, Vector2 toM, Vector2 atM)
    {
        var run = toM - fromM;
        var lengthSq = run.LengthSquared();
        if (lengthSq < 1e-8f) return (atM - fromM).Length();

        var along = Math.Clamp(Vector2.Dot(atM - fromM, run) / lengthSq, 0f, 1f);
        return (atM - (fromM + (run * along))).Length();
    }
}
