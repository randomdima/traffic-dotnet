using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Tests.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.World;

/// <summary>
/// The lines the lane paint runs down: which roads are one carriageway, and that what comes back is one
/// line rather than a heap of them. What the paint then <em>looks</em> like is the mesh's
/// (<c>GroundMeshTests</c>).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class CentrelineRunsTests
{
    /// <summary>
    /// <b>Every street is painted down one run for each line between its lanes, and nothing else is in any</b>
    /// (TER-6): a one-way street of one lane and a bay have a single ribbon apiece and no seam for a line to
    /// stand in, and a road in more runs than it has lines would be a street painted twice.
    /// </summary>
    [Fact]
    public void EveryStreetIsPaintedDownOneRunForEachLineBetweenItsLanes()
    {
        var plan = Towns.Of(Towns.Fixture);
        var runs = CentrelineRuns.Lay(plan, SimConfig.Shipped());

        var laid = new List<int>();
        for (var run = 0; run < runs.Count; run++) laid.AddRange(runs.RoadsOf(run).ToArray());

        var wanted = new List<int>();
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            for (var line = 0; line < plan.Roads.LinesBetweenLanes(road); line++) wanted.Add(road);
        }

        Assert.Equal(wanted, laid.Order().ToList());
    }

    /// <summary>
    /// <b>A run is one unbroken line</b>: every piece of it starts where the piece before it ended, the
    /// ground between two roads included. A run that merely listed the roads would leave the paint to jump
    /// the junction it carries on through, and a dash laid across that gap would be struck from nothing.
    /// </summary>
    [Fact]
    public void ARunIsOneUnbrokenLine()
    {
        var runs = CentrelineRuns.Lay(Towns.Of(Towns.Fixture), SimConfig.Shipped());

        for (var run = 0; run < runs.Count; run++)
        {
            var line = runs.Of(run);
            for (var piece = 1; piece < line.Length; piece++)
            {
                var offM = Vector2.Distance(line[piece - 1].EndM, line[piece].StartM);
                Assert.True(offM <= LineTolerance.RoundingM,
                    $"run {run} breaks {offM:0.####} m between its pieces {piece - 1} and {piece}");
            }
        }
    }

    /// <summary>
    /// <b>A run carries on through a junction that forks nothing and stops at one that does</b> — the one
    /// thing a run says that a road does not. A node two streets meet at is not somewhere a driver going
    /// past could turn, so the two halves of that street are one carriageway and take one line; anything a
    /// driver could turn onto ends the run.
    /// </summary>
    [Fact]
    public void ARunCarriesOnThroughAJunctionThatForksNothing()
    {
        var plan = Towns.Of(Towns.Fixture);
        var runs = CentrelineRuns.Lay(plan, SimConfig.Shipped());

        var run = new int[plan.Roads.Count];
        Array.Fill(run, -1);
        for (var laid = 0; laid < runs.Count; laid++)
        {
            foreach (var road in runs.RoadsOf(laid)) run[road] = laid;
        }

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            var standing = new List<int>();
            for (var road = 0; road < plan.Roads.Count; road++)
            {
                if (plan.Roads.IsABay(road)) continue;

                if (plan.Roads.FromJunction[road] == junction) standing.Add(road);
                if (plan.Roads.ToJunction[road] == junction) standing.Add(road);
            }

            if (standing.Count != 2 || standing[0] == standing[1]) continue;

            var forksNothing = plan.Roads.LinesBetweenLanes(standing[0]) == 1
                && plan.Roads.LinesBetweenLanes(standing[1]) == 1;
            if (!forksNothing) continue;

            Assert.True(run[standing[0]] == run[standing[1]],
                $"junction {junction} forks nothing, and roads {standing[0]} and {standing[1]} take "
                + $"runs {run[standing[0]]} and {run[standing[1]]}");
        }
    }

    /// <summary>
    /// <b>The line down a run stops behind the bar at either end of it</b> (TER-6): the bar is the last
    /// paint an arm carries before the junction, so a line reaching past it crosses the bar and then runs
    /// under the zebra in front of it, showing through the gaps between the stripes.
    /// </summary>
    /// <remarks>
    /// <b>Measured against the bars that were laid and not against the figure they were laid at</b>: a bar
    /// stands on its lane and the line on the carriageway between the two, so the two are compared in the
    /// bar's own frame — how far behind its back edge the paint stops. What the half-carriageway between
    /// those two frames costs over the bend an arm makes is <see cref="FramesM"/>, which is a centimetre
    /// and not a tolerance the claim leans on.
    /// </remarks>
    [Fact]
    public void TheLineDownARunStopsBehindTheBarAtEitherEndOfIt()
    {
        var config = SimConfig.Shipped();
        var plan = Towns.Of(Towns.Fixture);
        var paving = plan.Paving(config);
        var lanes = paving.Lanes;

        // The crossings the town actually lays and not a set staged here: where one stands is the walk's
        // to say (WLK-10), and the reach the line is trimmed by is read off that place — so a claim about
        // the trim asked over somebody else's crossings is a claim about nothing the town paints. <b>The
        // places the arms hold behind and not the paint</b>: a line stops behind the bar, and on a road
        // crossed midway the bar and the zebra are two different places.
        var crossings = Crossings.Lay(plan, config, paving.RoadEnds(config).HeldM);
        var bars = StopBars.Lay(lanes, crossings, config);
        var runs = CentrelineRuns.Lay(plan, config);

        var asked = 0;
        var worstM = 0f;
        var worstRun = 0;
        var worstLane = 0;
        for (var bar = 0; bar < bars.Count; bar++)
        {
            var lane = bars.Lane[bar];
            var arrivesAt = JunctionArms.End(lanes.LaneRoad[lane], lanes.LaneForward[lane]);
            for (var run = 0; run < runs.Count; run++)
            {
                var atHead = runs.HeadEnd(run) == arrivesAt;
                if (!atHead && runs.TailEnd(run) != arrivesAt) continue;

                var line = runs.Of(run);
                var (fromM, toM) = runs.PaintedM(run, crossings, config);
                var stopsM = Spline.SampleAt(line, atHead ? fromM : toM).PositionM;

                // The bar's back edge is half its thickness against the way it is driven at, and the paint
                // behind it is further against that way again.
                var backM = bars.CentreM[bar] - (bars.Approach[bar] * bars.ThicknessM[bar] * 0.5f);
                var behindM = Vector2.Dot(backM - stopsM, bars.Approach[bar]);
                asked++;
                if (behindM >= -worstM) continue;

                worstM = -behindM;
                worstRun = run;
                worstLane = lane;
            }
        }

        // The staging and not the claim: a fixture whose runs ended at no bar would ask nothing, and pass.
        Assert.True(asked > 0, "no run of the fixture ends at an arm carrying a bar");
        Assert.True(
            worstM <= FramesM,
            $"the line down run {worstRun} stops {worstM:0.##} m past the bar on lane {worstLane}");
    }

    /// <summary>
    /// How far the run's metres and a lane's may part over the paint at one arm's end: half a carriageway
    /// of offset, over the bend a street makes in the seven metres that paint takes.
    /// </summary>
    /// <remarks>
    /// <b>Measured on the fixture rather than derived</b>, which is why the claim is stated as a bound it
    /// does not lean on, and <b>the worst of them is what is weighed</b> so that a reading which grows says
    /// by how much rather than naming whichever run first crossed a line. It grew when the bar's own edge
    /// moved onto the band's axis (TER-6) — the paint stops a fixed reach down the run whatever the band
    /// did, so every centimetre the bar moves comes out of the margin between them.
    /// </remarks>
    const float FramesM = 0.04f;
}
