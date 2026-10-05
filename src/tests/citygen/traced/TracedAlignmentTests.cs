using System.Numerics;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A traced road's line is normalised</b> (GEN-57, <see cref="TracedAlignment"/>): a point nearer the straight
/// past it than the tolerance is no corner, a bend drawn as a polygon is one corner rounded through the bend, an
/// S-bend is two, and the line stands within the tolerance of its survey both ways — each asked of a line laid by
/// hand.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class TracedAlignmentTests
{
    static readonly float ToleranceM = SimConfig.Shipped().CityGen.TracedCornerToleranceM;

    /// <summary>Half a street's carriageway of one lane each way, the least a traced street's corner is rounded at.</summary>
    const float LeastM = 3.5f;

    /// <summary>How far round a bend a mapper puts each node of it.</summary>
    const float NodeStepDeg = 6f;

    const float BendRadiusM = 60f;

    /// <summary><b>A straight drawn with its nodes off it by less than the tolerance</b> is one straight.</summary>
    [Fact]
    public void PointsNearerAStraightThanTheToleranceAreNoCorners()
    {
        var jogM = ToleranceM * 0.6f;
        Vector2[] surveyedM = [new(100f, 500f), new(200f, 500f + jogM), new(300f, 500f - jogM), new(400f, 500f + jogM), new(500f, 500f)];

        var (pointsM, _, _) = TracedAlignment.Of(surveyedM, LeastM, ToleranceM);

        Assert.Equal(new[] { surveyedM[0], surveyedM[^1] }, pointsM);
    }

    /// <summary>
    /// <b>A bend drawn as a polygon is one corner</b>, where the straights either side of it meet, and the bend's
    /// own radius is among those that corner keeps its survey within the tolerance at.
    /// </summary>
    [Fact]
    public void ABendDrawnAsAPolygonIsOneCorner()
    {
        var surveyedM = Bend(new Vector2(400f, 500f), headingDeg: 0f, turnDeg: 90f);

        var (pointsM, tightestM, widestM) = TracedAlignment.Of([new(100f, 500f), .. surveyedM, new(460f, 900f)], LeastM, ToleranceM);

        Assert.Equal(3, pointsM.Length);
        Assert.Equal(new Vector2(460f, 500f), pointsM[1], new VectorWithin(ToleranceM));
        Assert.InRange(BendRadiusM, tightestM[0], widestM[0]);
    }

    /// <summary><b>An S-bend is two corners</b>: two bends turning opposite ways are never one.</summary>
    [Fact]
    public void AnSBendIsTwoCorners()
    {
        var (pointsM, _, _) = TracedAlignment.Of(SBend(), LeastM, ToleranceM);

        Assert.Equal(4, pointsM.Length);
    }

    /// <summary>
    /// <b>Every surveyed point stands within the tolerance of the line, and the line within it of the survey</b>:
    /// an S-bend drawn with its nodes jogged off it, rounded with both corners' legs shared.
    /// </summary>
    [Fact]
    public void AJoggedSBendIsLaidWithinTheToleranceOfItBothWays()
    {
        var surveyedM = SBend();
        for (var point = 1; point < surveyedM.Length - 1; point++)
        {
            surveyedM[point] += new Vector2(0f, (point % 2 == 0 ? 0.4f : -0.4f) * ToleranceM);
        }

        var line = Laid(surveyedM, LeastM);

        Assert.InRange(FurthestApartM(line, surveyedM), 0f, ToleranceM);
    }

    /// <summary>
    /// <b>The line between two surveyed points stands within the tolerance of the survey too</b>: two corners turning
    /// almost back on themselves off a short first leg, whose straights meet hundreds of metres off, are not one.
    /// </summary>
    [Fact]
    public void ACornerWhoseStraightsMeetFarOffDoesNotBulgeBetweenItsPoints()
    {
        Vector2[] surveyedM = [new(378.2f, 659.1f), new(371.9f, 657.8f), new(399.2f, 536.5f), new(404.3f, 537.3f)];

        var line = Laid(surveyedM, ToleranceM * 0.1f);

        Assert.InRange(FurthestApartM(line, surveyedM), 0f, ToleranceM);
    }

    /// <summary>
    /// <b>A leg too short for what both its corners want keeps each its tightest</b>, and is not overrun by the two:
    /// two bends close together, each wanting the whole of the leg between them.
    /// </summary>
    [Fact]
    public void ALegBothCornersWantMoreOfKeepsEachItsTightest()
    {
        Vector2[] pointsM = [new(0f, 0f), new(100f, 0f), new(120f, 20f), new(220f, 20f)];
        float[] tightestM = [20f, 30f];
        float[] widestM = [100f, 100f];

        var reachM = TracedAlignment.Reaches(pointsM, tightestM, widestM);

        var halfTurnTan = Spline.HalfTurnTan(pointsM, 1);
        Assert.True(reachM[0] >= tightestM[0] * halfTurnTan && reachM[1] >= tightestM[1] * halfTurnTan, $"reaches {reachM[0]}, {reachM[1]}");
        Assert.InRange(reachM[0] + reachM[1], 0f, Vector2.Distance(pointsM[1], pointsM[2]));
    }

    /// <summary>A bend of <see cref="BendRadiusM"/>, a node every <see cref="NodeStepDeg"/> (<see cref="SurveyedLines.Bend"/>).</summary>
    static Vector2[] Bend(Vector2 fromM, float headingDeg, float turnDeg) =>
        SurveyedLines.Bend(fromM, headingDeg, turnDeg, BendRadiusM, NodeStepDeg);

    /// <summary>A straight east, a bend 45° right, a bend 45° left straight after it, and a straight on east.</summary>
    static Vector2[] SBend()
    {
        var rightM = Bend(new Vector2(200f, 500f), headingDeg: 0f, turnDeg: 45f);
        var leftM = Bend(rightM[^1], headingDeg: 45f, turnDeg: -45f);
        return [new(100f, 500f), .. rightM, .. leftM[1..], leftM[^1] + new Vector2(100f, 0f)];
    }

    static ArcSeg[] Laid(Vector2[] surveyedM, float leastM) => SurveyedLines.Laid(surveyedM, leastM, ToleranceM);

    static float FurthestApartM(ReadOnlySpan<ArcSeg> line, ReadOnlySpan<Vector2> surveyedM) =>
        SurveyedLines.FurthestApartM(line, surveyedM);

    sealed class VectorWithin(float toleranceM) : IEqualityComparer<Vector2>
    {
        public bool Equals(Vector2 one, Vector2 other) => Vector2.Distance(one, other) <= toleranceM;

        public int GetHashCode(Vector2 obj) => 0;
    }
}
