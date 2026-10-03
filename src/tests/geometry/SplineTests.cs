using System.Numerics;
using TrafficSimulation.Core.Config;
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
    /// <b>A corner that would spend more than half a turn of heading is refused</b>
    /// (<see cref="Spline.CorneredInto"/>): the heading between two poses is never more than half a turn, so
    /// anything past it is a corner turning twice where once would do — and what the caller lays instead is
    /// the straight and the pivots at its ends, which spend exactly what the two poses ask for.
    /// </summary>
    /// <remarks>
    /// <b>Staged where the radius is not the reason.</b> Two poses pointing the same way five metres apart
    /// sideways get an S of a metre and a quarter of radius, well inside what a walker holds, and that S
    /// spends the better part of two turns to arrive facing the way it set off.
    /// </remarks>
    [Fact]
    public void ACornerSpendingMoreThanHalfATurnIsRefused()
    {
        var toM = new Vector2(0.2f, 5f);
        Span<ArcSeg> join = stackalloc ArcSeg[2];
        var written = Spline.BiarcInto(Vector2.Zero, 0f, toM, 0f, join);

        // The staging: the construction does join these two poses, and what it joins them with winds.
        Assert.Equal(2, written);
        Assert.True(Spline.SweptRad(join[..written]) > Spline.HalfATurnRad);
        foreach (var arc in join[..written]) Assert.True(1f / MathF.Abs(arc.Curvature) > WalkerTightestTurnM);

        Assert.Equal(0, Spline.CorneredInto(Vector2.Zero, 0f, toM, 0f, WalkerTightestTurnM, join));
    }

    /// <summary>
    /// <b>A right angle with room either side is a straight, a quarter circle at the radius asked and a
    /// straight</b> (<see cref="Spline.RoundedInto"/>): the arc leaves the first leg the radius short of the
    /// corner and meets the second leg the radius past it.
    /// </summary>
    [Fact]
    public void ARightAngleIsRoundedAtTheRadiusAskedWithAStraightEitherSide()
    {
        Span<ArcSeg> laid = stackalloc ArcSeg[3];
        var written = Spline.RoundedInto([Vector2.Zero, new Vector2(100f, 0f), new Vector2(100f, 100f)], 20f, laid);

        Assert.Equal(3, written);
        Assert.Equal(0f, laid[0].Curvature);
        Assert.Equal(80f, laid[0].LengthM, Tolerance);
        Assert.Equal(1f / 20f, laid[1].Curvature, Tolerance);
        Assert.Equal(100f, laid[1].EndM.X, Tolerance);
        Assert.Equal(20f, laid[1].EndM.Y, Tolerance);
        Assert.Equal(0f, laid[2].Curvature);
        Assert.Equal(100f, laid[2].EndM.X, Tolerance);
        Assert.Equal(100f, laid[2].EndM.Y, Tolerance);
    }

    /// <summary>
    /// <b>A corner whose shorter leg has no room for the radius asked takes half that leg</b>
    /// (<see cref="Spline.RoundedInto"/>), which for a right angle is a radius of half the leg.
    /// </summary>
    [Fact]
    public void ACornerOnAShortLegIsRoundedAsWideAsHalfTheLegAffords()
    {
        Span<ArcSeg> laid = stackalloc ArcSeg[3];
        var written = Spline.RoundedInto([Vector2.Zero, new Vector2(10f, 0f), new Vector2(10f, 100f)], 20f, laid);

        Assert.Equal(3, written);
        Assert.Equal(5f, laid[1].StartM.X, Tolerance);
        Assert.Equal(0f, laid[1].StartM.Y, Tolerance);
        Assert.Equal(1f / 5f, laid[1].Curvature, Tolerance);
    }

    /// <summary><b>A point a leg only runs through is no corner</b>, and the two legs either side of it are one straight.</summary>
    [Fact]
    public void APointALegRunsThroughIsNoCorner()
    {
        Span<ArcSeg> laid = stackalloc ArcSeg[3];
        var written = Spline.RoundedInto([Vector2.Zero, new Vector2(40f, 0f), new Vector2(100f, 0f)], 20f, laid);

        Assert.Equal(1, written);
        Assert.Equal(0f, laid[0].Curvature);
        Assert.Equal(100f, laid[0].LengthM, Tolerance);
    }

    /// <summary>
    /// <b>Two corners that all but share a leg leave no straight that turns</b>
    /// (<see cref="Spline.RoundedInto"/>): rounded at a hair under half the leg, they leave a sliver of it
    /// between them, and that sliver lies on the leg's own bearing — asked tens of kilometres from the
    /// origin, where a bearing read off the sliver's own two ends is a float's step in whatever direction.
    /// </summary>
    [Fact]
    public void TwoCornersAllButSharingALegJoinWithoutATurnBetweenThem()
    {
        const float LegM = 6f;
        const float SliverM = 6e-4f;

        Span<ArcSeg> laid = stackalloc ArcSeg[5];
        for (var step = 0; step < 256; step++)
        {
            var atM = new Vector2(8192f + (step * 91.37f), 30000f - (step * 83.11f));
            var along = Heading.Unit(0.3f + (step * 0.01f));
            var across = Heading.RightOf(along);
            var cornerM = atM + (along * 60f);
            var written = Spline.RoundedInto(
                [atM, cornerM, cornerM + (across * LegM), cornerM + (across * LegM) + (along * 60f)],
                (LegM - SliverM) * 0.5f, laid);

            for (var piece = 1; piece < written; piece++)
            {
                var endsOnRad = laid[piece - 1].HeadingAtRad(laid[piece - 1].LengthM);
                Assert.InRange(MathF.Abs(Spline.WrapRad(laid[piece].HeadingRad - endsOnRad)), 0f, 1e-3f);
            }
        }
    }

    /// <summary>
    /// The tightest circle the corner is asked to hold, which is the walker's
    /// (<see cref="SimConfig.WalkerTightestTurnM"/>) because the walk is what lays corners at this scale.
    /// </summary>
    static float WalkerTightestTurnM => SimConfig.Shipped().WalkerTightestTurnM;

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
    /// <b>A crossing stands where it stands however far from the origin the town laid it</b>, and however
    /// shallow the bend it is on. Asked at a town's own coordinates of a bend whose radius runs to
    /// kilometres: the distance along the bend must put the point back where the two lines actually meet.
    /// </summary>
    /// <remarks>
    /// <b>It is a precision test and it is written as one.</b> The same geometry at the origin was right to
    /// a micron while a kilometre and a half out it came back metres away — the distance along being read as
    /// the turn over the curvature, which multiplies whatever the last bits of a town-sized coordinate left
    /// by the radius. At the shallowest bend here the answer landed off the piece altogether and a plain
    /// crossing of two lines came back as no crossing at all.
    /// </remarks>
    [Theory]
    [InlineData(1e-6f)]
    [InlineData(5e-6f)]
    [InlineData(1e-4f)]
    [InlineData(1e-2f)]
    public void ACrossingIsFoundWhereTwoLinesMeetHoweverFarFromTheOrigin(float curvature)
    {
        var bend = new ArcSeg(new Vector2(2000f, 1500f), 0.3f, 20f, curvature);
        var meetM = bend.PointAtM(7f);
        var acrossM = Heading.RightOf(Heading.Unit(bend.HeadingAtRad(7f)));
        var across = new ArcSeg(meetM - (acrossM * 3f), MathF.Atan2(acrossM.Y, acrossM.X), 6f, 0f);

        Span<float> alongBendM = stackalloc float[2];
        Span<float> alongAcrossM = stackalloc float[2];

        Assert.Equal(1, Spline.CrossingsOf(bend, across, alongBendM, alongAcrossM));
        Assert.Equal(0f, (bend.PointAtM(alongBendM[0]) - meetM).Length(), Tolerance);
        Assert.Equal(0f, (across.PointAtM(alongAcrossM[0]) - meetM).Length(), Tolerance);
    }

    /// <summary>
    /// <b>A crossing a stride along a piece that barely bends is a crossing a stride along it</b>, whichever
    /// way the piece bends. A ribbon edge offset off a straight road carries a curvature of a few
    /// millionths, so which side of its start heading the chord to a point stands is decided by the last
    /// bits of a town coordinate rather than by the bend — and a distance along read off that sign comes
    /// back negative, a whole circle from the piece, and is dropped.
    /// </summary>
    [Theory]
    [InlineData(4e-6f)]
    [InlineData(-4e-6f)]
    public void ACrossingOnAPieceThatBarelyBendsStandsWhereItDoes(float curvature)
    {
        var barelyBent = new ArcSeg(new Vector2(2559.5f, 2141.8f), -2.568f, 7.4f, curvature);
        var meetM = barelyBent.PointAtM(1.75f);
        var acrossM = Heading.RightOf(Heading.Unit(barelyBent.HeadingAtRad(1.75f)));
        var across = new ArcSeg(meetM - (acrossM * 2f), MathF.Atan2(acrossM.Y, acrossM.X), 4f, 0f);

        Span<float> alongBentM = stackalloc float[2];
        Span<float> alongAcrossM = stackalloc float[2];

        Assert.Equal(1, Spline.CrossingsOf(barelyBent, across, alongBentM, alongAcrossM));
        Assert.Equal(1.75f, alongBentM[0], 0.01f);
        Assert.Equal(0f, (across.PointAtM(alongAcrossM[0]) - meetM).Length(), Tolerance);
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

    /// <summary>
    /// <b>A line cut into pieces is the line it was</b> (<see cref="Spline.JoinedInto"/>): however many
    /// places a chain was cut at, what comes back is one piece per curve it really holds, running the
    /// whole way and ending where the last of the pieces it replaced did.
    /// </summary>
    /// <remarks>
    /// <b>Asked at a town's own coordinates and at a road's own curvature.</b> A bend of a kilometre and a
    /// half over two kilometres from the origin is where a join is decided in the last bits of a float, and
    /// a test at the origin is a test of arithmetic that is never asked for.
    /// </remarks>
    [Theory]
    [InlineData(0f)]
    [InlineData(1f / 1500f)]
    [InlineData(-1f / 1500f)]
    public void AChainCutIntoPiecesJoinsBackIntoTheOneItWas(float curvature)
    {
        var whole = new ArcSeg(new Vector2(2083.97f, 242.52f), -0.935f, 60f, curvature);
        ReadOnlySpan<float> cutAtM = [12f, 18.6f, 19.1f, 44f];
        Span<ArcSeg> pieces = stackalloc ArcSeg[cutAtM.Length + 1];
        var fromM = 0f;
        for (var at = 0; at <= cutAtM.Length; at++)
        {
            var toM = at < cutAtM.Length ? cutAtM[at] : whole.LengthM;
            pieces[at] = new ArcSeg(whole.PointAtM(fromM), whole.HeadingAtRad(fromM), toM - fromM, curvature);
            fromM = toM;
        }

        Span<ArcSeg> joined = stackalloc ArcSeg[pieces.Length];

        Assert.Equal(1, Spline.JoinedInto(pieces, Tolerance, joined));
        Assert.Equal(whole.LengthM, joined[0].LengthM, Tolerance);
        Assert.Equal(0f, (joined[0].EndM - whole.EndM).Length(), Tolerance);
    }

    /// <summary>
    /// <b>A chain that turns keeps the pieces it turns at</b>: a join is rubbed out where the line carries
    /// on and nowhere else, so a corner of a fraction of a degree is a corner and not a rounding.
    /// </summary>
    [Theory]
    [InlineData(0.01f)]
    [InlineData(-0.01f)]
    public void AChainThatTurnsKeepsThePieceItTurnsAt(float turnRad)
    {
        var first = new ArcSeg(new Vector2(2083.97f, 242.52f), -0.935f, 12f, 0f);
        var second = new ArcSeg(first.EndM, first.HeadingRad + turnRad, 6.6f, 0f);
        Span<ArcSeg> joined = stackalloc ArcSeg[2];

        Assert.Equal(2, Spline.JoinedInto([first, second], Tolerance, joined));
    }

    /// <summary>
    /// <b>A piece that runs backwards over the one before it is a fold and joins nothing</b>: an offset
    /// taken tighter than the bend it comes off hands back a length below nought
    /// (<see cref="Spline.OffsetInto"/>), and two such pieces added together are a shape neither of them is.
    /// </summary>
    [Fact]
    public void APieceRunningBackwardsIsNotJoinedToTheOneBeforeIt()
    {
        var ahead = new ArcSeg(new Vector2(2083.97f, 242.52f), -0.935f, 12f, 0f);
        var back = new ArcSeg(ahead.EndM, ahead.HeadingRad, -5f, 0f);
        Span<ArcSeg> joined = stackalloc ArcSeg[2];

        Assert.Equal(2, Spline.JoinedInto([ahead, back], Tolerance, joined));
    }

    /// <summary>
    /// <b>A disc's own area, read off the one piece it is</b> — the shape whose enclosed ground nobody has to
    /// derive, and the one that says the circular segment a bend cuts off its chord is added with the sign
    /// the bend turns in.
    /// </summary>
    [Fact]
    public void ADiscEnclosesTheAreaItsOwnRadiusGivesIt()
    {
        const float radiusM = 12f;
        ArcSeg[] disc = [new ArcSeg(Vector2.Zero, 0f, 2f * MathF.PI * radiusM, 1f / radiusM)];

        Assert.Equal(MathF.PI * radiusM * radiusM, Spline.EnclosedM2(disc), Tolerance);
    }

    /// <summary>
    /// <b>A ring whose pieces do not meet encloses the ground up to the straights that close it</b>, which is
    /// the ground anything filling the ring covers: a fill draws from one piece's end to the next piece's
    /// start, so an area that left those straights out would be an area of a shape nothing lays. A town's
    /// merged perimeter has thousands of such joints and no fill of one is wrong for covering them.
    /// </summary>
    /// <remarks>
    /// A square's four sides, each stopping short of the corner it was heading for and each starting short
    /// of the one it came from: the ring is an octagon, and what it encloses is the square less the four
    /// corner triangles the closing straights cut off.
    /// </remarks>
    [Fact]
    public void ARingWhosePiecesDoNotMeetEnclosesTheGroundTheStraightsAcrossThemBound()
    {
        const float sideM = 10f;
        const float shortM = 2f;
        const float runM = sideM - (2f * shortM);

        ArcSeg[] ring =
        [
            new ArcSeg(new Vector2(shortM, 0f), 0f, runM, 0f),
            new ArcSeg(new Vector2(sideM, shortM), MathF.PI / 2f, runM, 0f),
            new ArcSeg(new Vector2(sideM - shortM, sideM), MathF.PI, runM, 0f),
            new ArcSeg(new Vector2(0f, sideM - shortM), -MathF.PI / 2f, runM, 0f),
        ];

        Assert.Equal((sideM * sideM) - (4f * 0.5f * shortM * shortM), Spline.EnclosedM2(ring), Tolerance);
    }
}
