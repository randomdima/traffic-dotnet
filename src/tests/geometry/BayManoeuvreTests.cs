using System.Numerics;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>The lines a car's rear axle is driven along into a bay and out of one</b> (<see cref="BayManoeuvre"/>,
/// GEN-4f), asked of a car on a street running along +x with the bay a quarter turn to its left.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class BayManoeuvreTests
{
    const float ToleranceM = 1e-2f;

    const float RadiusM = 4f;

    const float StraightensUpM = 0.2f;

    const float MostSwingRad = MathF.PI / 4f;

    /// <summary>Along the street, heading +x.</summary>
    static readonly BayManoeuvre.Pose OnTheStreet = new(Vector2.Zero, 0f);

    /// <summary>A pose in a bay whose axis runs +y, this far along the street and this far into the bay.</summary>
    static BayManoeuvre.Pose InTheBay(float alongM, float intoM) => new(new Vector2(alongM, intoM), MathF.PI / 2f);

    /// <summary><b>Nose in ends at the pose it was asked for</b>, square in the bay.</summary>
    [Fact]
    public void NoseInEndsAtThePoseItWasAskedFor()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.NoseIn(OnTheStreet, InTheBay(12f, 6f), RadiusM, StraightensUpM, MostSwingRad, arcs);

        Assert.Equal(1, shape.Pieces);
        AssertEndsAt(arcs[..shape.ArcCount], InTheBay(12f, 6f));
    }

    /// <summary><b>And turns only on the circle it is given</b>: every piece that bends bends at that radius.</summary>
    [Fact]
    public void NoseInTurnsOnlyOnTheCircleItIsGiven()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.NoseIn(OnTheStreet, InTheBay(12f, 2f), RadiusM, StraightensUpM, MostSwingRad, arcs);

        Assert.True(shape.Exists);
        foreach (var arc in arcs[..shape.ArcCount])
        {
            if (arc.Curvature != 0f) Assert.Equal(1f / RadiusM, MathF.Abs(arc.Curvature), 4);
        }
    }

    /// <summary>
    /// <b>A car standing further from the bay than its circle turns straight in</b>: nothing it lays bends away
    /// from the bay.
    /// </summary>
    [Fact]
    public void NoseInTurnsStraightInWhereItsCircleFits()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.NoseIn(OnTheStreet, InTheBay(12f, RadiusM + 1f), RadiusM, StraightensUpM, MostSwingRad, arcs);

        foreach (var arc in arcs[..shape.ArcCount]) Assert.True(arc.Curvature >= 0f);
    }

    /// <summary>
    /// <b>A car standing nearer the bay than its circle swings away from it first</b>, by the least that lets its
    /// turn in end square: the first piece that bends, bends away.
    /// </summary>
    [Fact]
    public void NoseInSwingsAwayWhereItStandsNearerThanItsCircle()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.NoseIn(OnTheStreet, InTheBay(12f, RadiusM - 1f), RadiusM, StraightensUpM, MostSwingRad, arcs);

        var firstBend = 0f;
        foreach (var arc in arcs[..shape.ArcCount])
        {
            if (arc.Curvature == 0f) continue;

            firstBend = arc.Curvature;
            break;
        }

        Assert.True(firstBend < 0f);
        AssertEndsAt(arcs[..shape.ArcCount], InTheBay(12f, RadiusM - 1f));
    }

    /// <summary><b>None once the car has passed where its turn in begins</b>: it cannot nose in from there.</summary>
    [Fact]
    public void NoseInIsRefusedPastWhereItsTurnBegins()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.NoseIn(OnTheStreet, InTheBay(RadiusM * 0.5f, 6f), RadiusM, StraightensUpM, MostSwingRad, arcs);

        Assert.False(shape.Exists);
    }

    /// <summary>
    /// <b>Backing in is on past the bay forwards and back into it in reverse</b>: two pieces, the first driven
    /// forwards along the street and the second in reverse, ending at the pose.
    /// </summary>
    [Fact]
    public void BackInPullsOnPastTheBayAndBacksIntoIt()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.BackIn(OnTheStreet, InTheBay(6f, 8f), RadiusM, StraightensUpM, arcs);

        Assert.Equal(2, shape.Pieces);
        Assert.False(shape.IsReverse(0));
        Assert.True(shape.IsReverse(1));
        Assert.Equal(0f, arcs[0].Curvature);
        Assert.Equal(6f + RadiusM, arcs[0].LengthM, 2);
        AssertEndsAt(arcs[..shape.ArcCount], InTheBay(6f, 8f));
    }

    /// <summary>
    /// <b>Out of the bay onto the street's line where the circle fits</b>: it lands on the line, travelling along
    /// it.
    /// </summary>
    [Fact]
    public void OutOfTheBayLandsOnTheStreetsLineWhereItsCircleFits()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var from = new BayManoeuvre.Pose(new Vector2(0f, RadiusM + 2f), -MathF.PI / 2f);
        var shape = BayManoeuvre.OutOfTheBay(from, reverse: true, Vector2.Zero, MathF.PI, RadiusM, StraightensUpM, RadiusM, arcs);

        Assert.True(shape.Exists);
        Assert.True(shape.IsReverse(0));
        var end = Spline.SampleAt(arcs[..shape.ArcCount], Spline.TotalLengthM(arcs[..shape.ArcCount]));
        Assert.Equal(0f, end.PositionM.Y, 2);
        Assert.Equal(1f, Vector2.Dot(end.Direction, -Vector2.UnitX), 3);
    }

    /// <summary>
    /// <b>And past the line by the least that fits where it does not</b>: a car nearer the line than its circle
    /// straightens up that much further out.
    /// </summary>
    [Fact]
    public void OutOfTheBayLandsPastTheLineByTheLeastThatFits()
    {
        const float standsM = RadiusM - 1.5f;
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var from = new BayManoeuvre.Pose(new Vector2(0f, standsM), -MathF.PI / 2f);
        var shape = BayManoeuvre.OutOfTheBay(from, reverse: true, Vector2.Zero, MathF.PI, RadiusM, StraightensUpM, RadiusM, arcs);

        var end = Spline.SampleAt(arcs[..shape.ArcCount], Spline.TotalLengthM(arcs[..shape.ArcCount]));
        Assert.Equal(-(RadiusM - standsM), end.PositionM.Y, 2);
    }

    /// <summary>How far the kerb's place stands to the left of the street's line: a lane's half and a roadside's.</summary>
    const float KerbBesideM = 2.85f;

    /// <summary>A pose at the kerb, alongside the street to its left, this far along it and travelling this way.</summary>
    static BayManoeuvre.Pose AtTheKerb(float alongM, float headingRad = 0f) => new(new Vector2(alongM, KerbBesideM), headingRad);

    /// <summary>
    /// <b>Into a place at the kerb forwards is an S</b> (GEN-4n): one piece, ending at the pose, travelling along the
    /// kerb.
    /// </summary>
    [Fact]
    public void AlongsideInSlidesIntoThePlaceOnAnS()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.AlongsideIn(OnTheStreet, AtTheKerb(20f), RadiusM, StraightensUpM, arcs);

        Assert.Equal(1, shape.Pieces);
        AssertEndsAt(arcs[..shape.ArcCount], AtTheKerb(20f));
    }

    /// <summary><b>And is refused nearer the place than the S takes</b>: a car that has come too far cannot slide in forwards.</summary>
    [Fact]
    public void AlongsideInIsRefusedNearerThanTheSTakes()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];

        Assert.False(BayManoeuvre.AlongsideIn(OnTheStreet, AtTheKerb(1f), RadiusM, StraightensUpM, arcs).Exists);
    }

    /// <summary>
    /// <b>Backing in at the kerb is on past the place forwards and back into it on an S in reverse</b>: two pieces,
    /// ending at the pose with the car pointing along the kerb.
    /// </summary>
    [Fact]
    public void AlongsideBackInPullsOnPastThePlaceAndBacksIntoIt()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.AlongsideBackIn(OnTheStreet, AtTheKerb(3f, MathF.PI), RadiusM, StraightensUpM, arcs);

        Assert.Equal(2, shape.Pieces);
        Assert.False(shape.IsReverse(0));
        Assert.True(shape.IsReverse(1));
        AssertEndsAt(arcs[..shape.ArcCount], AtTheKerb(3f, MathF.PI));
    }

    /// <summary>How far past the place a parallel park backs in.</summary>
    const float DeepM = 0.8f;

    /// <summary>
    /// <b>A parallel park is on past the place, back in on an S, and forward into it</b> (GEN-4n): three pieces, the middle
    /// one in reverse, ending at the pose square along the kerb.
    /// </summary>
    [Fact]
    public void ParallelInBacksInAndPullsForwardIntoThePlace()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.ParallelIn(OnTheStreet, AtTheKerb(3f), outSign: -1f, DeepM, RadiusM, StraightensUpM, arcs);

        Assert.Equal(3, shape.Pieces);
        Assert.Equal((false, true, false), (shape.IsReverse(0), shape.IsReverse(1), shape.IsReverse(2)));
        AssertEndsAt(arcs[..shape.ArcCount], AtTheKerb(3f));
    }

    /// <summary>
    /// <b>And the S back in ends past the place by the depth</b>, its nose still turned out toward the street: the axle 0.8 m
    /// short of the place along the kerb, turned 11.5° out — the turn on a 4 m circle that carries it 0.8 m forward.
    /// </summary>
    [Fact]
    public void ParallelInBacksInPastThePlaceTurnedOut()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.ParallelIn(OnTheStreet, AtTheKerb(3f), outSign: -1f, DeepM, RadiusM, StraightensUpM, arcs);

        var backIn = arcs.Slice(shape.FirstArcOf(1), shape.ArcsOf(1));
        var end = Spline.SampleAt(backIn, Spline.TotalLengthM(backIn));
        Assert.Equal(3f - DeepM, end.PositionM.X, 2);
        Assert.Equal(-11.5f, float.RadiansToDegrees(Spline.WrapRad(end.HeadingRad - MathF.PI)), 1);
    }

    /// <summary>
    /// <b>A straightening squares the car abeam of its place</b> (GEN-4i), whichever gear it turns in: from 5° off and a
    /// tenth of a metre behind, it ends pointing along the place's line with its axle abeam of the place's.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StraightenEndsSquareAbeamOfThePlace(bool forwardFirst)
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var from = new BayManoeuvre.Pose(new Vector2(-0.1f, 0.05f), float.DegreesToRadians(5f));
        var shape = BayManoeuvre.Straighten(from, OnTheStreet, forwardFirst, RadiusM, arcs, out _);

        var last = arcs.Slice(shape.FirstArcOf(shape.Pieces - 1), shape.ArcsOf(shape.Pieces - 1));
        var end = Spline.SampleAt(last, Spline.TotalLengthM(last));
        var travel = shape.IsReverse(shape.Pieces - 1) ? -Vector2.UnitX : Vector2.UnitX;
        Assert.Equal(0f, end.PositionM.X, 2);
        Assert.Equal(1f, Vector2.Dot(end.Direction, travel), 4);
    }

    /// <summary>
    /// <b>And is one piece where its straight runs on in the gear it turned in</b>: a metre behind its place, the turn forward
    /// leaves it short of it, and the straight on is forward too.
    /// </summary>
    [Fact]
    public void StraightenIsOnePieceWhereItRunsOnInOneGear()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var from = new BayManoeuvre.Pose(new Vector2(-1f, 0f), float.DegreesToRadians(5f));

        Assert.Equal(1, BayManoeuvre.Straighten(from, OnTheStreet, forwardFirst: true, RadiusM, arcs, out _).Pieces);
    }

    /// <summary>
    /// <b>Off the kerb is an S out onto the street's line</b>, after backing up where it is asked to: it lands on the
    /// line, travelling along it.
    /// </summary>
    [Fact]
    public void AlongsideOutLandsOnTheStreetsLine()
    {
        Span<ArcSeg> arcs = stackalloc ArcSeg[BayManoeuvre.MostArcs];
        var shape = BayManoeuvre.AlongsideOut(AtTheKerb(0f), 1f, Vector2.Zero, 0f, RadiusM, StraightensUpM, arcs);

        Assert.Equal(2, shape.Pieces);
        Assert.True(shape.IsReverse(0));
        var end = Spline.SampleAt(arcs[..shape.ArcCount], Spline.TotalLengthM(arcs[..shape.ArcCount]));
        Assert.Equal(0f, end.PositionM.Y, 2);
        Assert.Equal(1f, Vector2.Dot(end.Direction, Vector2.UnitX), 3);
    }

    static void AssertEndsAt(ReadOnlySpan<ArcSeg> arcs, BayManoeuvre.Pose pose)
    {
        var end = Spline.SampleAt(arcs, Spline.TotalLengthM(arcs));
        Assert.True(
            Vector2.Distance(end.PositionM, pose.AtM) <= ToleranceM,
            $"the line ends at {end.PositionM}, {Vector2.Distance(end.PositionM, pose.AtM):F3} m off {pose.AtM}");
        Assert.Equal(1f, Vector2.Dot(end.Direction, Heading.Unit(pose.HeadingRad)), 3);
    }
}
