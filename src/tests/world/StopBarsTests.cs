using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The bars the town is painted with: which lanes carry one, and where along the lane it stands. What one
/// <em>looks</em> like is the mesh's (<c>GroundMeshTests</c>).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class StopBarsTests
{
    /// <summary>
    /// <b>A bar stands on every lane driving at a crossing and nowhere else</b> (TER-6): the paint a driver
    /// holds at is the paint in front of the walk, so an arm with no crossing has nothing to hold for and an
    /// arm the traffic only leaves on is not driving at the one it has.
    /// </summary>
    [Fact]
    public void ABarIsPaintedOnEveryLaneDrivingAtACrossingAndOnNoOther()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var paving = plan.Paving(config);
        var lanes = paving.Lanes;
        var crossings = Crossings.Lay(plan, config, paving.RoadEnds(config).HeldM);
        var bars = StopBars.Lay(lanes, crossings, config);

        var painted = new HashSet<int>(bars.Lane.ToArray());
        var wanted = new HashSet<int>();
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            if (crossings.At(lanes.LaneRoad[lane], lanes.LaneForward[lane]) != Crossings.None) wanted.Add(lane);
        }

        // The staging and not the claim: a fixture with no crossing on it would leave both sets empty, and
        // pass.
        Assert.NotEmpty(wanted);
        Assert.Equal(wanted.Order(), painted.Order());
    }

    /// <summary>
    /// <b>A bar stands a setback clear of the crossing in front of it and never overlaps one</b> (TER-6):
    /// the walk is where it is and the stop is behind it, so what separates the two is
    /// <see cref="RoadFigures.StopBarSetbackM"/> of bare road and nothing else.
    /// </summary>
    /// <remarks>
    /// <b>Read in the band's own frame and at both ends of the bar's front edge</b>: the band is deep along its
    /// axis (TER-6), so the bare road in front of the bar is that far everywhere across the lane or the bar
    /// stands nearer the paint at one end than the other. <b>The worst of them is what is weighed</b>, so a
    /// reading that grows says by how much rather than naming whichever bar crossed the line first.
    /// </remarks>
    [Fact]
    public void ABarStandsASetbackBehindItsCrossing()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var paving = plan.Paving(config);
        var lanes = paving.Lanes;
        var crossings = Crossings.Lay(plan, config, paving.RoadEnds(config).HeldM);
        var bars = StopBars.Lay(lanes, crossings, config);
        var setbackM = config.Road.StopBarSetbackM;

        var worstM = 0f;
        var worstBar = 0;
        for (var bar = 0; bar < bars.Count; bar++)
        {
            var lane = bars.Lane[bar];
            var crossing = crossings.At(lanes.LaneRoad[lane], lanes.LaneForward[lane]);

            // The crossing's near edge is half its band toward the lane, struck along the band's own axis
            // because that is which way a band is deep (TER-6); the bar's is half its thickness forward of its middle.
            var axis = crossings.Axis[crossing];
            var towards = Vector2.Dot(axis, bars.Approach[bar]) >= 0f ? axis : -axis;
            var bandM = crossings.CentreM[crossing] - (towards * crossings.DepthM[crossing] * 0.5f);
            var edgeM = bars.CentreM[bar] + (bars.Approach[bar] * bars.ThicknessM[bar] * 0.5f);
            var acrossM = Heading.RightOf(bars.Approach[bar]) * (bars.SpanM[bar] * 0.5f);
            foreach (var endM in (ReadOnlySpan<Vector2>)[edgeM - acrossM, edgeM + acrossM])
            {
                var offM = MathF.Abs(Vector2.Dot(bandM - endM, towards) - setbackM);
                if (offM <= worstM) continue;

                (worstM, worstBar) = (offM, bar);
            }
        }

        Assert.NotEmpty(bars.Lane.ToArray());
        Assert.True(
            worstM <= LineTolerance.RoundingM,
            $"bar {worstBar} stands {worstM * 1000f:0.#} mm off a setback behind its crossing");
    }

    /// <summary>
    /// <b>A road crossed midway is held at each of its kerb ends</b> (TER-6, WLK-10a): a bar stands a setback
    /// clear of the last thing in front of it, and where the paint has gone to the middle of the street that
    /// is the end of the road's own kerb — so the bar holds at the mouth of the box it is held for rather
    /// than half a street short of it.
    /// </summary>
    /// <remarks>
    /// <b>Asked at a figure the fixture's streets answer to</b> (<see cref="WeldedBelowM"/>) rather than at
    /// the shipped one, which is a figure about a town rather than about this rule. <b>Which stands where is
    /// read in the metres of the lane the bar is on, and the gap along the axis of the place held behind</b>,
    /// which is the frame the bar was placed in (<see cref="Crossings.BehindM"/>).
    /// </remarks>
    [Fact]
    public void ARoadCrossedMidwayIsHeldAtEachOfItsKerbEnds()
    {
        var config = new SimConfig { Road = new RoadFigures { CrossedOnceBelowM = WeldedBelowM } };
        var plan = Towns.Of(Towns.Fixture);
        var paving = plan.Paving(SimConfig.Shipped());
        var lanes = paving.Lanes;
        var ends = KerbEnds.Of(paving, config);
        var crossed = Crossings.Lay(plan, config, ends.CrossedM);
        var holds = Crossings.Lay(plan, config, ends.HeldM);
        var bars = StopBars.Lay(lanes, holds, config);

        var behind = new Dictionary<int, int>();
        for (var bar = 0; bar < bars.Count; bar++) behind[bars.Lane[bar]] = bar;

        var asked = 0;
        var worstM = 0f;
        for (var crossing = 0; crossing < crossed.Count; crossing++)
        {
            if (!crossed.Midway[crossing]) continue;

            asked++;
            for (var lane = 0; lane < lanes.LaneCount; lane++)
            {
                if (lanes.LaneRoad[lane] != crossed.Road[crossing]) continue;

                Assert.Contains(lane, behind.Keys);

                var arcs = lanes.ArcsOf(lane);
                var lengthM = lanes.LaneLengthM[lane];
                var bar = behind[lane];
                var barM = Spline.ProjectM(arcs, bars.CentreM[bar], lengthM * 0.5f, lengthM);

                // Ahead of it down the lane, the kerb end this arm holds behind; and behind that, the paint
                // in the middle of the street, which the bar is not placed by.
                var kerb = holds.At(lanes.LaneRoad[lane], lanes.LaneForward[lane]);
                var kerbM = Spline.ProjectM(arcs, holds.CentreM[kerb], lengthM * 0.5f, lengthM);
                var paintM = Spline.ProjectM(arcs, crossed.CentreM[crossing], lengthM * 0.5f, lengthM);
                Assert.True(
                    paintM < barM && barM < kerbM,
                    $"the bar on lane {lane} stands at {barM:F1} m, the paint at {paintM:F1} m and the kerb "
                    + $"end at {kerbM:F1} m");

                var edgeM = bars.CentreM[bar] + (bars.Approach[bar] * bars.ThicknessM[bar] * 0.5f);
                var gapM = Vector2.Dot(holds.CentreM[kerb] - edgeM, bars.Approach[bar]);
                worstM = MathF.Max(worstM, MathF.Abs(gapM - config.Road.StopBarSetbackM));
            }
        }

        // The staging and not the claim: a fixture with no street short enough would ask nothing, and pass.
        Assert.True(asked > 0, $"no street of the fixture is cut twice within {WeldedBelowM:F0} m");
        Assert.InRange(worstM, 0f, LineTolerance.RoundingM);
    }

    /// <summary>
    /// The figure the fixture is read at, wide enough that some of its streets are crossed once between
    /// their ends (<see cref="RoadFigures.CrossedOnceBelowM"/>).
    /// </summary>
    const float WeldedBelowM = 60f;
}
