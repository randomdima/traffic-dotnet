using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// The arc chain every driven line is made of. Each test asks the geometry a question with a known
/// answer — a quarter circle's own radius, a straight's own length — rather than comparing one
/// derivation against another.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P1)]
public class SplineTests
{
    const float Tolerance = 1e-3f;

    [Fact]
    public void AStraightIsWalkedByItsOwnLength()
    {
        var arc = new ArcSeg(new Vector2(10f, 5f), 0f, 8f, 0f);

        Assert.Equal(new Vector2(18f, 5f), arc.EndM);
        Assert.Equal(0f, arc.HeadingAtRad(8f), Tolerance);
    }

    /// <summary>
    /// <b>The chord a bend affords bows off it by the sag asked for and no more</b> — which is what lets
    /// anything drawing or stationing an arc spend points on the bends and none on the straights. Asked of
    /// the arc itself: the middle of the chord against the middle of the arc it spans.
    /// </summary>
    [Theory]
    [InlineData(2f, 0.01f)]
    [InlineData(2f, 0.25f)]
    [InlineData(40f, 0.01f)]
    [InlineData(400f, 0.05f)]
    public void AChordBowsOffItsBendByTheSagAskedFor(float radiusM, float sagM)
    {
        var arc = new ArcSeg(Vector2.Zero, 0f, radiusM * MathF.PI * 0.5f, 1f / radiusM);
        var chordM = Spline.ChordForSagM(arc.Curvature, sagM);

        Assert.True(chordM > 0f && chordM < arc.LengthM, $"a {radiusM:F0} m bend affords a {chordM:F2} m chord");

        var bowM = (arc.PointAtM(chordM * 0.5f) - ((arc.StartM + arc.PointAtM(chordM)) * 0.5f)).Length();
        Assert.Equal(sagM, bowM, sagM * 0.05f);
    }

    /// <summary>And a straight is walked in one chord however long it is, which is the whole saving.</summary>
    [Fact]
    public void AStraightAffordsAChordOfAnyLength()
    {
        Assert.True(float.IsPositiveInfinity(Spline.ChordForSagM(0f, 0.01f)));
    }

    /// <summary>A quarter of a circle of radius R turns a right angle and ends R across and R along.</summary>
    [Theory]
    [InlineData(4f)]
    [InlineData(40f)]
    [InlineData(400f)]
    public void AQuarterCircleEndsWhereItsRadiusSaysItDoes(float radiusM)
    {
        var arc = new ArcSeg(Vector2.Zero, 0f, radiusM * MathF.PI * 0.5f, 1f / radiusM);

        Assert.Equal(MathF.PI * 0.5f, arc.HeadingAtRad(arc.LengthM), Tolerance);
        Assert.Equal(radiusM, arc.EndM.X, radiusM * Tolerance);
        Assert.Equal(radiusM, arc.EndM.Y, radiusM * Tolerance);
    }

    /// <summary>
    /// A road's own bend is a huge radius and a tiny curvature, and the arithmetic that walks it must
    /// not difference two sines that agree to five figures — the drift ends up in the join.
    /// </summary>
    [Fact]
    public void AVeryGentleBendIsWalkedWithoutLosingMetres()
    {
        var arc = new ArcSeg(Vector2.Zero, 0f, 300f, 1e-5f);

        Assert.Equal(300f, (arc.EndM - arc.StartM).Length(), 0.01f);
    }

    /// <summary>Positive curvature turns to the driver's right, which with <c>+y</c> down is a heading that grows.</summary>
    [Fact]
    public void PositiveCurvatureTurnsTowardsTheDriversRight()
    {
        var arc = new ArcSeg(Vector2.Zero, 0f, 5f, 0.1f);

        Assert.True(arc.EndM.Y > 0f);
        Assert.True(arc.HeadingAtRad(5f) > 0f);
    }

    [Fact]
    public void OffsettingKeepsTheTurnAndMovesTheRadius()
    {
        ReadOnlySpan<ArcSeg> arcs = [new ArcSeg(Vector2.Zero, 0f, 10f * MathF.PI * 0.5f, 0.1f)];
        Span<ArcSeg> offset = stackalloc ArcSeg[1];

        Spline.OffsetInto(arcs, 2f, offset);

        // Two metres to the inside of a ten-metre circle is an eight-metre one, turning the same corner.
        Assert.Equal(1f / 8f, offset[0].Curvature, Tolerance);
        Assert.Equal(MathF.PI * 0.5f, offset[0].Curvature * offset[0].LengthM, Tolerance);
        Assert.Equal(new Vector2(0f, 2f), offset[0].StartM);
    }

    [Fact]
    public void ASubChainIsTheStretchAsked()
    {
        ReadOnlySpan<ArcSeg> arcs = [new ArcSeg(Vector2.Zero, 0f, 10f, 0f), new ArcSeg(new Vector2(10f, 0f), 0f, 10f, 0f)];
        Span<ArcSeg> into = stackalloc ArcSeg[4];

        var written = Spline.SubChainInto(arcs, 5f, 15f, into);

        Assert.Equal(2, written);
        Assert.Equal(new Vector2(5f, 0f), into[0].StartM);
        Assert.Equal(10f, Spline.TotalLengthM(into[..written]), Tolerance);
    }

    /// <summary>
    /// <b>A biarc arrives at the pose it was asked for</b>, in position and in heading. That is the
    /// whole of what a junction join has to be, and getting the quadratic upside down still lands on
    /// the point — it just takes a few hundred metres to do it.
    /// </summary>
    [Theory]
    [InlineData(0f, 20f, 0f, 0f)]
    [InlineData(0f, 20f, 12f, 1.5707964f)]
    [InlineData(0f, -14f, 9f, -1.5707964f)]
    [InlineData(0.7f, 30f, -20f, 2.4f)]
    [InlineData(-2f, -18f, 4f, 1.1f)]
    public void ABiarcArrivesAtThePoseItWasGiven(float fromHeadingRad, float toX, float toY, float toHeadingRad)
    {
        var toM = new Vector2(toX, toY);
        Span<ArcSeg> join = stackalloc ArcSeg[2];

        var written = Spline.BiarcInto(Vector2.Zero, fromHeadingRad, toM, toHeadingRad, join);
        var laid = join[..written];

        Assert.True(written > 0);
        Assert.Equal(0f, (laid[^1].EndM - toM).Length(), 0.01f);
        Assert.Equal(0f, Spline.WrapRad(laid[^1].HeadingAtRad(laid[^1].LengthM) - toHeadingRad), 0.01f);

        // And it is one line: each piece leaves where the one before it arrived, heading the same way.
        for (var arc = 1; arc < laid.Length; arc++)
        {
            Assert.Equal(0f, (laid[arc].StartM - laid[arc - 1].EndM).Length(), 0.01f);
            Assert.Equal(0f, Spline.WrapRad(laid[arc].HeadingRad - laid[arc - 1].HeadingAtRad(laid[arc - 1].LengthM)), 0.01f);
        }
    }

    /// <summary>
    /// Two poses facing opposite ways a lane apart get the one arc through both of them — <b>a circle no
    /// car can hold</b>. At the shipped lane spacing it comes out at 1.5 m of radius against a car's own
    /// tightest 3.9 m, which is the arithmetic behind there being no movement through a box that reverses
    /// the direction of travel (TER-5f).
    /// </summary>
    [Fact]
    public void TwoOpposingPosesGetASemicircleTighterThanACarCanHold()
    {
        Span<ArcSeg> join = stackalloc ArcSeg[2];

        var written = Spline.BiarcInto(Vector2.Zero, 0f, new Vector2(0f, 3f), MathF.PI, join);
        var laid = join[..written];

        Assert.Equal(0f, (laid[^1].EndM - new Vector2(0f, 3f)).Length(), 0.01f);
        Assert.Equal(MathF.PI * 1.5f, Spline.TotalLengthM(laid), Tolerance);
        foreach (var arc in laid) Assert.True(1f / MathF.Abs(arc.Curvature) < 2f);
    }

    /// <summary>
    /// A line that doubles back past itself has two nearest points, and what a car wants is the one
    /// near the progress it had. That is what the window is for and it is the reason this is not a
    /// search over the whole line.
    /// </summary>
    [Fact]
    public void ProjectionAnswersInsideItsOwnWindow()
    {
        ReadOnlySpan<ArcSeg> arcs = [new ArcSeg(Vector2.Zero, 0f, 100f, 0f)];

        Assert.Equal(40f, Spline.ProjectM(arcs, new Vector2(40f, 2f), 40f, 8f), Tolerance);

        // The same point, asked about from far up the line, comes back as the near end of the window
        // rather than as the place it actually is.
        Assert.Equal(72f, Spline.ProjectM(arcs, new Vector2(40f, 2f), 80f, 8f), Tolerance);
    }

    /// <summary>Two straights meet at the one point that is on both of them, said as a distance along each.</summary>
    [Fact]
    public void TwoStraightsCrossWhereBothOfThemAre()
    {
        ReadOnlySpan<ArcSeg> along = [new ArcSeg(new Vector2(-10f, 0f), 0f, 40f, 0f)];
        ReadOnlySpan<ArcSeg> across = [new ArcSeg(new Vector2(6f, -5f), MathF.PI * 0.5f, 20f, 0f)];

        Span<SplineCrossing> found = stackalloc SplineCrossing[4];

        Assert.Equal(1, Spline.CrossingsM(along, across, 20f, 10f, found));
        Assert.Equal(16f, found[0].OneM, Tolerance);
        Assert.Equal(5f, found[0].OtherM, Tolerance);
    }

    /// <summary>
    /// <b>A skew crossing is the crossing and not the foot of a perpendicular onto it.</b> The point of one
    /// line nearest a place on the other agrees with the crossing only where the two meet square; a line 30°
    /// off puts that foot most of the offset past the meeting, which is the whole difference between a
    /// corner and a spike out of one.
    /// </summary>
    [Fact]
    public void ASkewCrossingIsNotTheNearestPointToIt()
    {
        const float skewRad = MathF.PI / 6f;
        var skew = new ArcSeg(Vector2.Zero, skewRad, 40f, 0f);
        ReadOnlySpan<ArcSeg> along = [new ArcSeg(new Vector2(-20f, 0f), 0f, 40f, 0f)];
        ReadOnlySpan<ArcSeg> across = [skew];

        // Three metres up the skew line, which stands 3·cos 30° along the flat one and 3·sin 30° off it.
        var offM = skew.PointAtM(3f);

        Span<SplineCrossing> found = stackalloc SplineCrossing[4];

        Assert.Equal(1, Spline.CrossingsM(along, across, 20f, 3f, found));
        Assert.Equal(20f, found[0].OneM, Tolerance);
        Assert.Equal(0f, found[0].OtherM, Tolerance);
        Assert.Equal(20f + (3f * MathF.Cos(skewRad)), Spline.ProjectM(along, offM, 20f, 10f), Tolerance);
    }

    /// <summary>
    /// A straight cuts a circle twice and a piece answers only for the cut its own length reaches, which is
    /// what keeps a bend from being carried round to the far side of the circle it is part of.
    /// </summary>
    [Fact]
    public void AnArcCrossesAStraightWhereItsOwnLengthReaches()
    {
        // A quarter circle of radius 10 from the origin turning right, so it runs from (0,0) to (10,10).
        ReadOnlySpan<ArcSeg> bend = [new ArcSeg(Vector2.Zero, 0f, 10f * MathF.PI * 0.5f, 0.1f)];
        ReadOnlySpan<ArcSeg> along = [new ArcSeg(new Vector2(-10f, 10f), 0f, 40f, 0f)];

        Span<SplineCrossing> found = stackalloc SplineCrossing[4];

        // The bend reaches y = 10 only at its own end, square on to the straight.
        Assert.Equal(1, Spline.CrossingsM(bend, along, 15f, 20f, found));
        Assert.Equal(10f * MathF.PI * 0.5f, found[0].OneM, Tolerance);
        Assert.Equal(20f, found[0].OtherM, Tolerance);
    }

    /// <summary>Two arcs cross where their circles do, at both of the two places both of them reach.</summary>
    [Fact]
    public void TwoArcsCrossWhereTheirCirclesDo()
    {
        // Two circles of radius 10 centred on (0,0) and (10,0), which cross half way along the line between
        // them and 10·sin 60° off it. Each arc is the half of its own circle that faces the other.
        ReadOnlySpan<ArcSeg> one = [new ArcSeg(new Vector2(0f, -10f), 0f, 10f * MathF.PI, 0.1f)];
        ReadOnlySpan<ArcSeg> other = [new ArcSeg(new Vector2(10f, -10f), MathF.PI, 10f * MathF.PI, -0.1f)];

        Span<SplineCrossing> found = stackalloc SplineCrossing[4];

        // Both halves reach both crossings, and the one asked about is the half turn nearer either start.
        Assert.Equal(2, Spline.CrossingsM(one, other, 8f, 8f, found));

        var atM = one[0].PointAtM(found[0].OneM);
        Assert.Equal(5f, atM.X, Tolerance);
        Assert.Equal(-10f * MathF.Sin(MathF.PI / 3f), atM.Y, Tolerance);
        Assert.Equal(0f, (other[0].PointAtM(found[0].OtherM) - atM).Length(), Tolerance);
    }

    /// <summary>
    /// <b>A bend a kilometre across crosses where both of its own ends say it does.</b> A road's arcs are a
    /// huge radius and a tiny curvature, and solved against the circle's centre the two distances that come
    /// back name points a metre and a half apart — near the crossing rather than on it.
    /// </summary>
    [Fact]
    public void AGentleBendCrossesWhereBothChainsAgreeItDoes()
    {
        // A bend of radius 2 km, which is a straight road's own drift, cut by a line across it.
        ReadOnlySpan<ArcSeg> bend = [new ArcSeg(new Vector2(1000f, 1000f), 0f, 60f, 1f / 2000f)];
        ReadOnlySpan<ArcSeg> across = [new ArcSeg(new Vector2(1030f, 990f), MathF.PI * 0.5f, 20f, 0f)];
        Span<SplineCrossing> found = stackalloc SplineCrossing[4];

        Assert.Equal(1, Spline.CrossingsM(bend, across, 30f, 10f, found));

        // A centimetre, which is what a float carries at a town's own coordinates — and two orders off what
        // solving against the centre came back with.
        var onTheBendM = bend[0].PointAtM(found[0].OneM);
        var onTheStraightM = across[0].PointAtM(found[0].OtherM);
        Assert.Equal(0f, (onTheBendM - onTheStraightM).Length(), 0.01f);
    }

    /// <summary>
    /// <b>Two chains that stop short of each other cross where they are run on</b>, which is the corner
    /// between two arms of a junction: both stop at their own mouths and the corner stands on neither.
    /// </summary>
    [Fact]
    public void ChainsRunOnPastTheirEndsCrossWhereTheyWouldHave()
    {
        // Two straights whose corner is at (10, 0), four metres past the end of one and three past the
        // other, which is the L a road turns at a mouth.
        ReadOnlySpan<ArcSeg> along = [new ArcSeg(new Vector2(0f, 0f), 0f, 6f, 0f)];
        ReadOnlySpan<ArcSeg> down = [new ArcSeg(new Vector2(10f, 3f), MathF.PI * 0.5f, 6f, 0f)];
        Span<SplineCrossing> found = stackalloc SplineCrossing[4];

        Assert.Equal(0, Spline.CrossingsM(along, down, 6f, 0f, found));

        Assert.Equal(1, Spline.CrossingsM(along, down, 6f, 0f, found, beyondM: 5f));
        Assert.Equal(10f, found[0].OneM, Tolerance);
        Assert.Equal(-3f, found[0].OtherM, Tolerance);
    }

    /// <summary>
    /// <b>Two parallels are answered for by nobody</b>, which is a caller's cue to fall back on something
    /// that always has an answer rather than to take a wrong one.
    /// </summary>
    [Fact]
    public void LinesThatNeverMeetHaveNoCrossing()
    {
        ReadOnlySpan<ArcSeg> along = [new ArcSeg(new Vector2(-10f, 0f), 0f, 40f, 0f)];
        ReadOnlySpan<ArcSeg> beside = [new ArcSeg(new Vector2(-10f, 3.5f), 0f, 40f, 0f)];
        Span<SplineCrossing> found = stackalloc SplineCrossing[4];

        Assert.Equal(0, Spline.CrossingsM(along, beside, 20f, 20f, found));
    }

    /// <summary>
    /// <b>A line that crosses another twice is answered for twice, nearest first.</b> Which of the two a
    /// caller means is the caller's own question, so both are handed back and the order is what saves it
    /// asking about the far one at all.
    /// </summary>
    [Fact]
    public void BothCrossingsComeBackWithTheNearerFirst()
    {
        // A full circle of radius 10 about the origin, cut by the x axis at (-10, 0) and (10, 0).
        ReadOnlySpan<ArcSeg> round = [new ArcSeg(new Vector2(0f, -10f), 0f, 20f * MathF.PI, 0.1f)];
        ReadOnlySpan<ArcSeg> along = [new ArcSeg(new Vector2(-20f, 0f), 0f, 40f, 0f)];
        Span<SplineCrossing> found = stackalloc SplineCrossing[4];

        // Asked about from the far end of the straight, which is the cut at (10, 0).
        Assert.Equal(2, Spline.CrossingsM(along, round, 40f, 0f, found));
        Assert.Equal(30f, found[0].OneM, Tolerance);
        Assert.Equal(10f, found[1].OneM, Tolerance);
    }
}
