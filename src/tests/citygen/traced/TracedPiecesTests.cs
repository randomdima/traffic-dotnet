using System.Numerics;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.CityGen.Traced;

/// <summary>
/// <b>A traced road is laid in the fewest pieces that keep its survey</b> (GEN-57, <see cref="TracedPieces"/>): a run
/// of pieces is one arc, a biarc, one corner between straights, or two bends and the straight between, between the
/// poses it keeps; a road's end keeps
/// as much as it is asked to; and nothing is laid tighter than the road can be rounded — each asked of a line laid by
/// hand.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class TracedPiecesTests
{
    static readonly float ToleranceM = SimConfig.Shipped().CityGen.TracedLineToleranceM;

    static readonly float CornerToleranceM = SimConfig.Shipped().CityGen.TracedCornerToleranceM;

    /// <summary>Half a street's carriageway of one lane, the least a traced street of one lane is rounded at.</summary>
    const float LeastM = 1.75f;

    /// <summary>A map wide enough that nothing laid by hand here comes near its edge.</summary>
    static readonly Vector2 MapM = new(1000f, 1000f);

    /// <summary>
    /// <b>A run of three pieces between two poses is the two arcs that join them</b>: a jog of two arcs and a straight
    /// between two long straights, its ends kept on their poses, is four pieces.
    /// </summary>
    [Fact]
    public void AJogBetweenTwoStraightsIsTwoArcs()
    {
        var laid = Jog(leadM: 100f);

        var line = Fewest(laid, Sampled(laid), RoadEnd.Pose, RoadEnd.Pose);

        Assert.Equal(4, line.Length);
    }

    /// <summary>
    /// <b>A road's end asked to keep its pose keeps it</b>: a road that is a jog and little else, laid in fewer pieces,
    /// leaves where it left and arrives where it arrived, on the headings it did.
    /// </summary>
    [Fact]
    public void AnEndKeptOnItsPoseLeavesAndArrivesOnIt()
    {
        var laid = Jog(leadM: 2f);

        var line = Fewest(laid, Sampled(laid), RoadEnd.Pose, RoadEnd.Pose);

        Assert.True(line.Length < laid.Length, $"laid in {line.Length} pieces of {laid.Length}");
        Assert.Equal((laid[0].StartM, laid[0].HeadingRad), (line[0].StartM, line[0].HeadingRad));
        Assert.InRange(Vector2.Distance(laid[^1].EndM, line[^1].EndM), 0f, 1e-3f);
        Assert.InRange(MathF.Abs(Spline.WrapRad(line[^1].HeadingAtRad(line[^1].LengthM) - laid[^1].HeadingAtRad(laid[^1].LengthM))), 0f, 1e-4f);
    }

    /// <summary>
    /// <b>A road's end asked to keep its piece keeps it as it was</b>: the same road, its first and last straights
    /// untouched however the jog between them is laid.
    /// </summary>
    [Fact]
    public void AnEndKeptAsItsPieceIsLaidAsItWas()
    {
        var laid = Jog(leadM: 2f);

        var line = Fewest(laid, Sampled(laid), RoadEnd.Piece, RoadEnd.Piece);

        Assert.True(line.Length < laid.Length, $"laid in {line.Length} pieces of {laid.Length}");
        Assert.Equal((laid[0], laid[^1]), (line[0], line[^1]));
    }

    /// <summary>
    /// <b>A road whose ends keep only their place, one bend drawn as a polygon, is one arc</b>: a road that starts
    /// inside a bend and ends a step past it, which its corner laid as a straight, the bend and a straight.
    /// </summary>
    [Fact]
    public void ARoadStartedInsideABendIsOneArc()
    {
        var bendM = SurveyedLines.Bend(new Vector2(100f, 100f), headingDeg: 0f, turnDeg: 110f, radiusM: 16f, nodeStepDeg: 6f);
        Vector2[] surveyedM = [.. bendM, bendM[^1] + (Vector2.Normalize(bendM[^1] - bendM[^2]) * 0.83f)];

        var line = Fewest(SurveyedLines.Laid(surveyedM, LeastM, CornerToleranceM), surveyedM, RoadEnd.Place, RoadEnd.Place);

        Assert.Single(line);
    }

    /// <summary>
    /// <b>Two bends with a straight drawn wavering between them are the bends and one straight</b>: two quarter turns
    /// a hundred metres apart, the way between them drawn as two faint bends of a degree each, laid between the poses
    /// its ends keep.
    /// </summary>
    [Fact]
    public void TwoBendsWithAWaveringStraightBetweenAreTheBendsAndOneStraight()
    {
        var laid = Chain(new Vector2(0f, 500f), (20f, 0f), (Quarter(15f), 1f / 15f), (Degree(3000f), 1f / 3000f),
            (Degree(3000f), -1f / 3000f), (Quarter(15f), -1f / 15f), (20f, 0f));

        var line = Fewest(laid, Sampled(laid), RoadEnd.Pose, RoadEnd.Pose);

        Assert.Single(line, piece => piece.Curvature == 0f && piece.LengthM > 90f);
    }

    /// <summary>
    /// <b>A stub of straight before a bend is taken into it</b>: a metre east, a sixth of a turn right at 20 m and 50 m
    /// on, its ends kept on their poses, is the bend and the straight.
    /// </summary>
    [Fact]
    public void AStubBeforeABendIsTakenIntoIt()
    {
        var laid = Chain(new Vector2(0f, 500f), (1f, 0f), (20f * MathF.PI / 3f, 1f / 20f), (50f, 0f));

        var line = Fewest(laid, Sampled(laid), RoadEnd.Pose, RoadEnd.Pose);

        Assert.Equal(2, line.Length);
        Assert.Single(line, piece => piece.Curvature != 0f);
    }

    /// <summary>
    /// <b>A bend drawn as two corners between two straights is one</b>: 100 m east, 20° right and 40° more at 20 m with
    /// 4 m between, and 100 m on, its ends kept on their poses, is a straight, an arc and a straight.
    /// </summary>
    [Fact]
    public void ABendDrawnAsTwoCornersBetweenStraightsIsOne()
    {
        var laid = Chain(new Vector2(0f, 500f), (100f, 0f), (20f * float.DegreesToRadians(20f), 1f / 20f), (4f, 0f),
            (20f * float.DegreesToRadians(40f), 1f / 20f), (100f, 0f));

        var line = Fewest(laid, Sampled(laid), RoadEnd.Pose, RoadEnd.Pose);

        Assert.Equal(3, line.Length);
        Assert.Single(line, piece => piece.Curvature != 0f);
    }

    /// <summary>
    /// <b>The arc, straight and arc between two poses arrive on the second</b>, the straight tangent to both arcs: a
    /// right turn off one heading east and a left turn back onto it, 60 m further south.
    /// </summary>
    [Fact]
    public void AnArcAStraightAndAnArcJoinTwoPoses()
    {
        Span<ArcSeg> line = stackalloc ArcSeg[3];
        Vector2 toM = new(100f, 560f);

        Assert.True(TracedPieces.ArcStraightArc(new Vector2(0f, 500f), 0f, 1f / 20f, toM, 0f, -1f / 20f, line));
        Assert.InRange(Vector2.Distance(line[2].EndM, toM), 0f, 1e-3f);
        Assert.InRange(MathF.Abs(Spline.WrapRad(line[2].HeadingAtRad(line[2].LengthM))), 0f, 1e-4f);
        Assert.InRange(MathF.Abs(Spline.WrapRad(line[1].HeadingRad - line[0].HeadingAtRad(line[0].LengthM))), 0f, 1e-4f);
        Assert.InRange(MathF.Abs(Spline.WrapRad(line[2].HeadingRad - line[1].HeadingRad)), 0f, 1e-4f);
    }

    /// <summary>
    /// <b>A run only arcs tighter than the road can be rounded keep is laid as it was</b>: the jog, with a least
    /// radius no biarc across it keeps its survey at.
    /// </summary>
    [Fact]
    public void AJogOnlyTighterArcsKeepIsLaidAsItWas()
    {
        var laid = Jog(leadM: 100f);

        var line = TracedPieces.Fewest(laid, Sampled(laid), MapM, leastRadiusM: 400f, ToleranceM, RoadEnd.Pose, RoadEnd.Pose);

        Assert.Equal(laid, line);
    }

    /// <summary><b>Each piece laid starts where the one before it ends, on the heading it ends on.</b></summary>
    [Fact]
    public void EveryPieceCarriesOnFromTheOneBefore()
    {
        var laid = Jog(leadM: 100f);

        var line = Fewest(laid, Sampled(laid), RoadEnd.Place, RoadEnd.Place);

        for (var piece = 1; piece < line.Length; piece++)
        {
            var before = line[piece - 1];
            Assert.InRange(Vector2.Distance(before.EndM, line[piece].StartM), 0f, 1e-3f);
            Assert.InRange(MathF.Abs(Spline.WrapRad(line[piece].HeadingRad - before.HeadingAtRad(before.LengthM))), 0f, 1e-3f);
        }
    }

    /// <summary><b>The line laid stands within the tolerance of its survey, and the survey within it of the line.</b></summary>
    [Fact]
    public void TheLineStandsWithinTheToleranceOfItsSurveyBothWays()
    {
        var laid = Jog(leadM: 100f);
        var surveyedM = Sampled(laid);

        var line = Fewest(laid, surveyedM, RoadEnd.Place, RoadEnd.Place);

        Assert.InRange(SurveyedLines.FurthestApartM(line, surveyedM), 0f, ToleranceM);
    }

    static ArcSeg[] Fewest(ArcSeg[] laid, Vector2[] surveyedM, RoadEnd atStart, RoadEnd atEnd) =>
        TracedPieces.Fewest(laid, surveyedM, MapM, LeastM, ToleranceM, atStart, atEnd);

    /// <summary>A straight east this long, 15° right and back at 40 m with 3 m between, and as long east again.</summary>
    static ArcSeg[] Jog(float leadM)
    {
        var turnM = 40f * float.DegreesToRadians(15f);
        return Chain(new Vector2(0f, 500f), (leadM, 0f), (turnM, 1f / 40f), (3f, 0f), (turnM, -1f / 40f), (leadM, 0f));
    }

    static float Quarter(float radiusM) => radiusM * MathF.PI * 0.5f;

    static float Degree(float radiusM) => radiusM * float.DegreesToRadians(1f);

    /// <summary>Pieces end to end from a place heading east, each its length and its curvature.</summary>
    static ArcSeg[] Chain(Vector2 fromM, params (float LengthM, float Curvature)[] pieces)
    {
        var chain = new ArcSeg[pieces.Length];
        var headingRad = 0f;
        for (var piece = 0; piece < pieces.Length; piece++)
        {
            chain[piece] = new ArcSeg(fromM, headingRad, pieces[piece].LengthM, pieces[piece].Curvature);
            (fromM, headingRad) = (chain[piece].EndM, chain[piece].HeadingAtRad(pieces[piece].LengthM));
        }

        return chain;
    }

    /// <summary>A line surveyed a node every 2 m along it, and at its end.</summary>
    static Vector2[] Sampled(ReadOnlySpan<ArcSeg> line)
    {
        var lengthM = Spline.TotalLengthM(line);
        var nodes = (int)MathF.Ceiling(lengthM / 2f);
        var surveyedM = new Vector2[nodes + 1];
        for (var node = 0; node <= nodes; node++) surveyedM[node] = Spline.SampleAt(line, lengthM * node / nodes).PositionM;
        return surveyedM;
    }
}
