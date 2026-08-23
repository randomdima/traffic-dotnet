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
    /// A turn-around is two poses facing opposite ways a lane apart, and the join between them is
    /// drawn — <b>and is a circle no car can hold</b>. At the shipped lane spacing it comes out at
    /// 1.5 m of radius against a car's own tightest 3.9 m, which is why turning a car round is a
    /// manoeuvre with a reverse in it and not a line to be followed.
    /// </summary>
    [Fact]
    public void ATurnAroundIsDrawnAndIsTighterThanACarCanHold()
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
}
