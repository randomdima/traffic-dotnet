using System.Numerics;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>Surveyed lines drawn by hand, laid as a traced road's are, and how far a laid line stands off one.</summary>
internal static class SurveyedLines
{
    const float SampleStepM = 0.25f;

    /// <summary>
    /// A bend drawn as a mapper draws one, a node every <paramref name="nodeStepDeg"/>, from a place on a heading,
    /// turning to the right as positive — the place and the bend's last node included.
    /// </summary>
    public static Vector2[] Bend(Vector2 fromM, float headingDeg, float turnDeg, float radiusM, float nodeStepDeg)
    {
        var heading = Heading.Unit(headingDeg * MathF.PI / 180f);
        var toCentre = MathF.Sign(turnDeg) * new Vector2(-heading.Y, heading.X);
        var centreM = fromM + (toCentre * radiusM);
        var nodes = (int)MathF.Round(MathF.Abs(turnDeg) / nodeStepDeg);
        var bendM = new Vector2[nodes + 1];
        for (var node = 0; node <= nodes; node++)
        {
            var turnedRad = turnDeg * node / nodes * MathF.PI / 180f;
            var (sin, cos) = MathF.SinCos(turnedRad);
            var outward = -toCentre;
            bendM[node] = centreM + (radiusM * new Vector2((outward.X * cos) - (outward.Y * sin), (outward.X * sin) + (outward.Y * cos)));
        }

        return bendM;
    }

    /// <summary>A surveyed line normalised and rounded as a traced road's is, at one least radius.</summary>
    public static ArcSeg[] Laid(Vector2[] surveyedM, float leastM, float toleranceM)
    {
        var (pointsM, tightestM, widestM) = TracedAlignment.Of(surveyedM, leastM, toleranceM);
        var arcs = new ArcSeg[(2 * pointsM.Length) - 3];
        var count = Spline.RoundedInto(pointsM, TracedAlignment.Reaches(pointsM, tightestM, widestM), arcs);
        return arcs[..count];
    }

    /// <summary>
    /// How far a line and the surveyed line it was laid from stand apart: every surveyed point off the line, and the
    /// line, read every <see cref="SampleStepM"/>, off the surveyed line.
    /// </summary>
    public static float FurthestApartM(ReadOnlySpan<ArcSeg> line, ReadOnlySpan<Vector2> surveyedM)
    {
        var lengthM = Spline.TotalLengthM(line);
        var furthestM = 0f;
        foreach (var pointM in surveyedM)
        {
            var onM = Spline.ProjectM(line, pointM, lengthM * 0.5f, lengthM);
            furthestM = MathF.Max(furthestM, Vector2.Distance(Spline.SampleAt(line, onM).PositionM, pointM));
        }

        for (var alongM = 0f; alongM <= lengthM; alongM += SampleStepM)
        {
            var atM = Spline.SampleAt(line, alongM).PositionM;
            var offM = float.PositiveInfinity;
            for (var point = 1; point < surveyedM.Length; point++)
            {
                offM = MathF.Min(offM, SegmentOffM(atM, surveyedM[point - 1], surveyedM[point]));
            }

            furthestM = MathF.Max(furthestM, offM);
        }

        return furthestM;
    }

    static float SegmentOffM(Vector2 pointM, Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        var along = Math.Clamp(Vector2.Dot(pointM - fromM, runM) / runM.LengthSquared(), 0f, 1f);
        return Vector2.Distance(pointM, fromM + (runM * along));
    }
}
