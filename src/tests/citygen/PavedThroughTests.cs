using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// <b>The junctions a road runs through as one line</b> (<see cref="RoadCuts.RunsThrough"/>, TER-5b): what
/// decides that a box paves nothing of its own, so that its movements are no pieces of the tarmac's outline
/// and nothing is drawn there but the two arms' own sections.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P6)]
public class PavedThroughTests
{
    public static TheoryData<string> Maps => Towns.EveryLaidMap();

    /// <summary>
    /// A place the road runs through is a place with two arms and nothing else: a fork keeps its box,
    /// however its arms lie.
    /// </summary>
    [Theory]
    [MemberData(nameof(Maps))]
    public void OnlyANodeWithTwoArmsIsRunThrough(string map)
    {
        var plan = Towns.Of(map);
        var arms = RoadCuts.ArmsPerJunction(plan.Ground);
        var through = RoadCuts.RunsThrough(plan.Ground);

        Assert.Equal(arms.Length, through.Length);
        for (var junction = 0; junction < through.Length; junction++)
        {
            Assert.True(
                !through[junction] || arms[junction] == 2,
                $"{map}: junction {junction} at {plan.Junctions.CentreM[junction]} has {arms[junction]} arms and is run through");
        }
    }

    /// <summary>
    /// <b>The fixture's mid-block crossing is such a place</b>: two arms swept onto one line, carrying the
    /// zebra an inline junction exists for (TER-5b). Without one the question above is asked of nothing.
    /// </summary>
    [Fact]
    public void TheFixtureHasAJunctionItsRoadRunsThrough()
    {
        var through = RoadCuts.RunsThrough(Towns.Of(Towns.Fixture).Ground);

        Assert.Contains(true, through);
    }

    /// <summary>
    /// <b>Two arms that do not meet keep their box</b>: an arm ending short of the other, or wider than it,
    /// is a step in the kerb and not one line, and what stands round that step is the box's own ground.
    /// </summary>
    [Fact]
    public void TwoArmsThatDoNotMeetKeepTheirJunction()
    {
        var plan = Towns.Of(Towns.Fixture);
        var roads = plan.Ground.Roads;
        var widened = new float[roads.WidthM.Length];
        Array.Copy(roads.WidthM, widened, widened.Length);

        var through = RoadCuts.RunsThrough(plan.Ground);
        var junction = Array.IndexOf(through, true);
        var arm = Array.IndexOf(roads.FromJunction, junction);
        if (arm < 0) arm = Array.IndexOf(roads.ToJunction, junction);
        widened[arm] += SimConfig.Shipped().Road.PaintLineWidthM;

        var stepped = plan.Ground.With(
            new CityPlan.RoadArrays
            {
                FromJunction = roads.FromJunction, ToJunction = roads.ToJunction, WidthM = widened,
                Flow = roads.Flow, SegmentOffsets = roads.SegmentOffsets, Segments = roads.Segments,
            },
            plan.Ground.Bridges, plan.Ground.Junctions, plan.Ground.JunctionCorners, plan.Ground.Crosswalks,
            plan.Ground.StopLines);

        Assert.False(RoadCuts.RunsThrough(stepped)[junction]);
    }
}
