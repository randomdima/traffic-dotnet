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

    static void AssertEndsAt(ReadOnlySpan<ArcSeg> arcs, BayManoeuvre.Pose pose)
    {
        var end = Spline.SampleAt(arcs, Spline.TotalLengthM(arcs));
        Assert.True(
            Vector2.Distance(end.PositionM, pose.AtM) <= ToleranceM,
            $"the line ends at {end.PositionM}, {Vector2.Distance(end.PositionM, pose.AtM):F3} m off {pose.AtM}");
        Assert.Equal(1f, Vector2.Dot(end.Direction, Heading.Unit(pose.HeadingRad)), 3);
    }
}
