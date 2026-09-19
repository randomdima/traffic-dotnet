using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The arrows the town is painted with: which lanes carry one, what each of them says and where the paint
/// stands. What one <em>looks</em> like is the mesh's (<c>GroundMeshTests</c>).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class LaneArrowsTests
{
    /// <summary>
    /// <b>An arrow stands on every lane that holds at a bar and has somewhere to go</b> (TER-6a): what it
    /// says is the movements the junction in front offers, so a lane that is offered none — the way into a
    /// bay is the one a town lays — has nothing to say and carries no paint.
    /// </summary>
    [Fact]
    public void AnArrowIsPaintedOnEveryLaneThatHoldsAtABarAndOnNoOther()
    {
        var config = SimConfig.Shipped();
        var (lanes, bars) = Painted(config);
        var arrows = LaneArrows.Lay(lanes, bars, config);

        var wanted = new HashSet<int>();
        foreach (var lane in bars.Lane)
        {
            if (lanes.ConnectorAt[lane + 1] > lanes.ConnectorAt[lane]) wanted.Add(lane);
        }

        // The staging and not the claim: a fixture with no bar on it would leave both sets empty, and pass.
        Assert.NotEmpty(wanted);
        Assert.Equal(wanted.Order(), new HashSet<int>(arrows.Lane.ToArray()).Order());
    }

    /// <summary>
    /// <b>An arrow says every turn its lane offers and no other</b> (TER-6a): one branch per kind of
    /// movement, so a lane offered two ways round to the same hand is one branch and a lane offered all
    /// three is one arrow of three rather than a glyph of its own.
    /// </summary>
    [Fact]
    public void AnArrowSaysEveryTurnItsLaneOffersAndNoOther()
    {
        var config = SimConfig.Shipped();
        var (lanes, bars) = Painted(config);
        var arrows = LaneArrows.Lay(lanes, bars, config);

        var asked = new int[3];
        for (var arrow = 0; arrow < arrows.Count; arrow++)
        {
            var lane = arrows.Lane[arrow];
            var offered = new HashSet<LaneTurn>();
            for (var at = lanes.ConnectorAt[lane]; at < lanes.ConnectorAt[lane + 1]; at++)
            {
                offered.Add(lanes.ConnectorKind[at]);
            }

            var said = arrows.BranchTurn[arrows.BranchAt[arrow]..arrows.BranchAt[arrow + 1]].ToArray();
            Assert.Equal(offered.Order(), said.Order());
            Assert.Equal(said.Length, said.Distinct().Count());
            asked[said.Length - 1]++;
        }

        // The staging and not the claim: a town whose lanes all offered one movement would never ask what a
        // second branch does.
        Assert.True(asked[1] + asked[2] > 0, "no lane of the fixture offers more than one movement");
    }

    /// <summary>
    /// <b>An arrow stands a setback of clear road behind its bar</b> (TER-6a): it is placed by the one mark
    /// in front of it and by nothing else (TER-6, rule 3), in the metres of the lane both of them are on.
    /// </summary>
    [Fact]
    public void AnArrowStandsASetbackBehindItsBar()
    {
        var config = SimConfig.Shipped();
        var (lanes, bars) = Painted(config);
        var arrows = LaneArrows.Lay(lanes, bars, config);

        var behind = new Dictionary<int, int>();
        for (var bar = 0; bar < bars.Count; bar++) behind[bars.Lane[bar]] = bar;

        Assert.NotEmpty(arrows.Lane.ToArray());
        for (var arrow = 0; arrow < arrows.Count; arrow++)
        {
            var bar = behind[arrows.Lane[arrow]];
            var tipM = arrows.FromM[arrow] + config.Road.LaneArrowLengthM;
            var gapM = bars.AlongM[bar] - (bars.ThicknessM[bar] * 0.5f) - tipM;

            Assert.Equal(config.Road.LaneArrowSetbackM, gapM, 3);
            Assert.True(arrows.FromM[arrow] > 0f, $"the arrow on lane {arrows.Lane[arrow]} starts off its lane");
        }
    }

    /// <summary>
    /// <b>Every arrow is the same length down its lane, whatever it says</b> (TER-6a): the furthest-reaching
    /// branch ends at the arrow's own length from its tail, so what a bend does not spend advancing is spent
    /// on the shaft behind it and two arrows saying different things end together.
    /// </summary>
    /// <remarks>
    /// <b>Measured along the shaft's own bearing</b>, which is the frame the glyph was laid out in: the lane
    /// curves under it, and a point a metre and a half off the line projects back onto a bend's arc at a
    /// reading of its own that has nothing to do with how long the mark is.
    /// </remarks>
    [Fact]
    public void EveryArrowIsTheSameLengthDownItsLane()
    {
        var config = SimConfig.Shipped();
        var (lanes, bars) = Painted(config);
        var arrows = LaneArrows.Lay(lanes, bars, config);

        var halfHeadM = config.Road.LaneArrowHeadWidthM * 0.5f;
        var headM = config.Road.LaneArrowHeadLengthM;
        var halfShaftM = config.Road.LaneArrowShaftWidthM * 0.5f;

        Assert.NotEmpty(arrows.Lane.ToArray());
        for (var arrow = 0; arrow < arrows.Count; arrow++)
        {
            var fork = Spline.SampleAt(lanes.ArcsOf(arrows.Lane[arrow]), arrows.ForkM[arrow]);
            var advanceM = 0f;
            for (var at = arrows.BranchAt[arrow]; at < arrows.BranchAt[arrow + 1]; at++)
            {
                foreach (var cornerM in Corners(arrows.Branch[at], headM, halfHeadM, halfShaftM))
                {
                    advanceM = MathF.Max(advanceM, Vector2.Dot(cornerM - fork.PositionM, fork.Direction));
                }
            }

            var lengthM = arrows.ForkM[arrow] - arrows.FromM[arrow] + advanceM;
            Assert.Equal(config.Road.LaneArrowLengthM, lengthM, 2);
        }
    }

    /// <summary>
    /// <b>The whole glyph stands on the lane it is painted on</b> (TER-6a), the far corner of every head
    /// included: the bend's radius is solved out of the room the lane leaves, so however sharply the movement
    /// behind it turns, no part of an arrow is painted over the kerb beside it or over the line down the
    /// middle of the road.
    /// </summary>
    /// <remarks>
    /// <b>Measured off the lane's own line rather than in the frame the bend was solved in</b>, which is what
    /// the claim is about — and asked of the ribbon as well as the heads, the widest point of a branch being
    /// the head's outer corner only while the bend keeps its radius.
    /// </remarks>
    [Fact]
    public void AnArrowStaysOnTheLaneItIsPaintedOn()
    {
        var config = SimConfig.Shipped();
        var (lanes, bars) = Painted(config);
        var arrows = LaneArrows.Lay(lanes, bars, config);

        var halfHeadM = config.Road.LaneArrowHeadWidthM * 0.5f;
        var headM = config.Road.LaneArrowHeadLengthM;
        var halfShaftM = config.Road.LaneArrowShaftWidthM * 0.5f;

        var worstM = 0f;
        Assert.NotEmpty(arrows.Lane.ToArray());
        for (var arrow = 0; arrow < arrows.Count; arrow++)
        {
            var lane = arrows.Lane[arrow];
            var arcs = lanes.ArcsOf(lane);
            var halfLaneM = lanes.LaneWidthM[lane] * 0.5f;
            for (var at = arrows.BranchAt[arrow]; at < arrows.BranchAt[arrow + 1]; at++)
            {
                var branch = arrows.Branch[at];
                foreach (var cornerM in Corners(branch, headM, halfHeadM, halfShaftM))
                {
                    Spline.ProjectM(arcs, cornerM, arrows.ForkM[arrow], config.Road.LaneArrowLengthM, out var offSq);
                    var offM = MathF.Sqrt(offSq);
                    Assert.True(
                        offM <= halfLaneM,
                        $"the arrow on lane {lane} reaches {offM:F2} m off its line, on a lane "
                        + $"{halfLaneM * 2f:F2} m wide");

                    worstM = MathF.Max(worstM, offM);
                }
            }
        }

        // The staging and not the claim: a town of straight arrows would never reach across its lanes at all.
        Assert.True(worstM > halfShaftM, "no arrow of the fixture bends");
    }

    /// <summary>
    /// The corners of one branch: the outer edge of the ribbon, walked, and the three points of the head at
    /// the end of it.
    /// </summary>
    static IEnumerable<Vector2> Corners(ArcSeg branch, float headM, float halfHeadM, float halfShaftM)
    {
        for (var step = 0; step <= Walked; step++)
        {
            var alongM = branch.LengthM * step / Walked;
            var across = Heading.RightOf(Heading.Unit(branch.HeadingAtRad(alongM))) * halfShaftM;
            yield return branch.PointAtM(alongM) + across;
            yield return branch.PointAtM(alongM) - across;
        }

        var atM = branch.EndM;
        var along = Heading.Unit(branch.HeadingAtRad(branch.LengthM));
        var barb = Heading.RightOf(along) * halfHeadM;
        yield return atM + barb;
        yield return atM - barb;
        yield return atM + (along * headM);
    }

    /// <summary>The lanes of the fixture and the bars on them, which is what an arrow is laid off.</summary>
    static (LaneLines Lanes, StopBars Bars) Painted(SimConfig config)
    {
        var plan = Towns.Of(Towns.Fixture);
        var lanes = plan.Paving(config).Lanes;
        var crossings = Crossings.Lay(plan, config, FootConnectors.Lay(plan, config).Crossed());
        return (lanes, StopBars.Lay(lanes, crossings, config));
    }

    /// <summary>How finely a branch's ribbon is walked to find how far across the lane it ever reaches.</summary>
    const int Walked = 8;
}
