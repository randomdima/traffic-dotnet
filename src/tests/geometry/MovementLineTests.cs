using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>
/// <b>A movement is drawn the way a driver takes it</b> (TER-5d.2, <see cref="Spline.MovementInto"/>): on along the lane
/// it leaves, turned or shifted across late and short, and on along the lane it joins.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
public class MovementLineTests
{
    /// <summary>
    /// <b>A turn runs on along its lane and turns at the corner</b>: a quarter turn whose lanes' lines cross ten metres on
    /// and six short of the lane out is straight until the junction's turn circle reaches the corner, that circle, and
    /// straight along the lane out — not one arc spread across the whole of the box.
    /// </summary>
    [Fact]
    public void ATurnRunsOnAlongItsLaneAndTurnsAtTheCorner()
    {
        var toM = new Vector2(10f, 6f);
        const float ToRad = MathF.PI * 0.5f;

        var line = Movement(Vector2.Zero, 0f, toM, ToRad);

        Assert.Equal((0f, 10f - TurnM), (line[0].Curvature, line[0].LengthM));
        Assert.Equal(1f / TurnM, line[1].Curvature, 1e-4f);
        Arrives(line, toM, ToRad);
    }

    /// <summary>
    /// <b>A corner its two lanes stand unevenly off is turned one way only</b>: the lane in ends a metre short of where
    /// the lane out's line crosses its own, the lane out begins twelve past it, and the equal-tangent biarc swings
    /// the other way first — the half road crossed before turning.
    /// </summary>
    [Fact]
    public void ACornerItsLanesStandUnevenlyOffIsTurnedOneWayOnly()
    {
        var toM = new Vector2(1f, 12f);
        const float ToRad = MathF.PI * 0.5f;
        Span<ArcSeg> biarc = stackalloc ArcSeg[2];
        var laid = Spline.BiarcInto(Vector2.Zero, 0f, toM, ToRad, biarc);
        Assert.True(Spline.SweptRad(biarc[..laid]) > Spline.AskedRad(biarc[..laid]) + 1e-3f);

        var line = Movement(Vector2.Zero, 0f, toM, ToRad);

        Assert.Equal(Spline.AskedRad(line), Spline.SweptRad(line), 1e-3f);
        Arrives(line, toM, ToRad);
    }

    /// <summary>
    /// <b>A straight on to a staggered arm never heads back the way it came</b>: fourteen metres across and eight ahead,
    /// the biarc's two arcs each turn a third of a turn; the movement turns off and back on no more than a quarter turn
    /// each.
    /// </summary>
    [Fact]
    public void AStraightOnToAStaggeredArmTurnsNoMoreThanAQuarterTurnEachWay()
    {
        var toM = new Vector2(8f, 14f);
        Span<ArcSeg> biarc = stackalloc ArcSeg[2];
        var laid = Spline.BiarcInto(Vector2.Zero, 0f, toM, 0f, biarc);
        Assert.True(MathF.Abs(biarc[0].LengthM * biarc[0].Curvature) > QuarterTurnRad);

        var line = Movement(Vector2.Zero, 0f, toM, 0f);

        foreach (var arc in line) Assert.True(MathF.Abs(arc.LengthM * arc.Curvature) <= QuarterTurnRad + 1e-3f);
        Arrives(line, toM, 0f);
    }

    /// <summary>
    /// <b>A lane shifting across a box runs on along its lane and shifts across at the end</b>: three metres over, nineteen
    /// metres on, it is straight until the two arcs of the turn circle that make the shift are all that is left.
    /// </summary>
    [Fact]
    public void ALaneShiftRunsOnAlongItsLaneAndShiftsLate()
    {
        const float AcrossM = 3f;
        var toM = new Vector2(18.9f, AcrossM);

        var line = Movement(Vector2.Zero, 0f, toM, 0f);

        // Two arcs turning opposite ways a half angle each, which cover twice the radius by its sine.
        var shiftRad = MathF.Acos(1f - (AcrossM / (2f * TurnM)));
        Assert.Equal(0f, line[0].Curvature);
        Assert.Equal(toM.X - (2f * TurnM * MathF.Sin(shiftRad)), line[0].LengthM, 0.01f);
        Arrives(line, toM, 0f);
    }

    /// <summary>
    /// <b>A lane carried on across a box a hair off its line keeps the biarc</b>: shifted less than half a lane, it is still
    /// in its lane, and the lane beside it and the paint between them are carried on the same way.
    /// </summary>
    [Fact]
    public void ALaneCarriedOnAHairOffItsLineKeepsTheBiarc()
    {
        var toM = new Vector2(18.9f, CarriedOnM * 0.5f);
        Span<ArcSeg> biarc = stackalloc ArcSeg[2];
        var laid = Spline.BiarcInto(Vector2.Zero, 0f, toM, 0f, biarc);

        Assert.Equal(biarc[..laid].ToArray(), Movement(Vector2.Zero, 0f, toM, 0f));
    }

    /// <summary>
    /// <b>A corner too tight for any car is still turned one way only</b>: the lane in ends a few centimetres short of the
    /// corner, and the biarc reaches a wider circle only by swinging out over the lanes beside — which is never drawn.
    /// </summary>
    [Fact]
    public void ACornerTooTightIsStillTurnedOneWayOnly()
    {
        var toM = new Vector2(0.05f, 12f);
        const float ToRad = MathF.PI * 0.5f;

        var line = Movement(Vector2.Zero, 0f, toM, ToRad);

        Assert.Equal(Spline.AskedRad(line), Spline.SweptRad(line), 1e-3f);
    }

    /// <summary>
    /// <b>A turn made at once is on the circle asked from its first metre to its last</b>
    /// (<see cref="Spline.TurnedAtOnceInto"/>): a quarter turn to the right between two lanes eight metres apart either way,
    /// turned off the first lane on the circle, straight, and turned onto the second on it.
    /// </summary>
    [Fact]
    public void ATurnMadeAtOnceBeginsAndEndsOnTheCircleAsked()
    {
        const float RadiusM = 4f;
        var toM = new Vector2(8f, 8f);
        Span<ArcSeg> into = stackalloc ArcSeg[3];

        var line = into[..Spline.TurnedAtOnceInto(Vector2.Zero, 0f, toM, MathF.PI * 0.5f, RadiusM, 1f, into)];

        Assert.Equal(1f / RadiusM, line[0].Curvature, 1e-4f);
        Assert.Equal(1f / RadiusM, line[^1].Curvature, 1e-4f);
        Arrives(line.ToArray(), toM, MathF.PI * 0.5f);
    }

    static float TurnM => SimConfig.Shipped().JunctionTurnRoomM;

    /// <summary>Half a lane, which a lane may shift across a box and still be carried on.</summary>
    static float CarriedOnM => SimConfig.Shipped().LaneWidthM * 0.5f;

    const float QuarterTurnRad = MathF.PI * 0.5f;

    static ArcSeg[] Movement(Vector2 fromM, float fromRad, Vector2 toM, float toRad)
    {
        var straightRad = SimConfig.Shipped().Road.TurnStraightToleranceDeg * MathF.PI / 180f;
        Span<ArcSeg> into = stackalloc ArcSeg[Spline.MostMovementArcs];
        var laid = Spline.MovementInto(fromM, fromRad, toM, toRad, TurnM, straightRad, CarriedOnM, carriedThrough: false, into);
        return into[..laid].ToArray();
    }

    static void Arrives(ArcSeg[] line, Vector2 toM, float toRad)
    {
        Assert.Equal(0f, Vector2.Distance(line[^1].EndM, toM), 0.01f);
        Assert.Equal(0f, Spline.WrapRad(line[^1].HeadingAtRad(line[^1].LengthM) - toRad), 0.01f);
    }
}
