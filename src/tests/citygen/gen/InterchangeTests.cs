using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;
using Xunit;
using Crossings = TrafficSimulation.World.Road.Crossings;

namespace TrafficSimulation.Tests.CityGen.Gen;

/// <summary>
/// <b>A bypassed town's intercity roads are met only at interchanges</b> (GEN-68): a road carried over each, and slip roads
/// off and onto its kerb lanes, so nothing on it stops.
/// </summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P5)]
public class InterchangeTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static readonly Lazy<CityPlan> Plan = new(() => Towns.LayFresh(Towns.Brief(Towns.CitySeed, buildings: 900, country: true)));

    /// <summary>How many intercity roads run through each junction, and whether anything but a slip road meets it there.</summary>
    static (int[] Through, bool[] MetOtherwise) Junctions(CityPlan.RoadArrays roads, int junctions)
    {
        var (through, other) = (new int[junctions], new bool[junctions]);
        for (var road = 0; road < roads.Count; road++)
        {
            foreach (var junction in (ReadOnlySpan<int>)[roads.FromJunction[road], roads.ToJunction[road]])
            {
                if (roads.Class[road] == RoadClass.Intercity) through[junction]++;
                else if (roads.Class[road] != RoadClass.Slip) other[junction] = true;
            }
        }

        return (through, other);
    }

    /// <summary><b>Where an intercity road runs through a junction, only its slip roads meet it there.</b></summary>
    [Fact]
    public void AnIntercityRoadIsMetOnlyBySlipRoads()
    {
        var plan = Plan.Value;
        Assert.Contains(RoadClass.Slip, plan.Roads.Class);

        var (through, other) = Junctions(plan.Roads, plan.Junctions.CentreM.Length);
        for (var junction = 0; junction < through.Length; junction++)
        {
            Assert.False(through[junction] >= 2 && other[junction], $"junction {junction} at {plan.Junctions.CentreM[junction]} is met on its intercity road by a road no slip");
        }
    }

    /// <summary><b>No junction an intercity road runs through carries lights</b>: nothing on it stops (TLT-3).</summary>
    [Fact]
    public void NoJunctionAnIntercityRoadRunsThroughIsLit()
    {
        var plan = Plan.Value;
        var (through, _) = Junctions(plan.Roads, plan.Junctions.CentreM.Length);
        for (var junction = 0; junction < through.Length; junction++)
        {
            Assert.False(through[junction] >= 2 && plan.Junctions.Lit[junction], $"junction {junction} at {plan.Junctions.CentreM[junction]} on an intercity road is lit");
        }
    }

    /// <summary>
    /// <b>A slip road is driven off and onto its intercity road's kerb lane alone</b> (TER-5j), however shallow it leaves, so
    /// the lane beside passes by.
    /// </summary>
    [Fact]
    public void ASlipRoadLeavesAndJoinsTheKerbLaneAlone()
    {
        var plan = Plan.Value;
        var roads = RoadGraph.Build(plan, Config);
        var slipping = 0;
        for (var connector = 0; connector < roads.ConnectorCount; connector++)
        {
            var (from, to) = (roads.ConnectorFrom(connector), roads.ConnectorTo(connector));
            var (fromClass, toClass) = (plan.Roads.Class[roads.LaneRoad[from]], plan.Roads.Class[roads.LaneRoad[to]]);
            if (fromClass == RoadClass.Intercity && toClass == RoadClass.Slip)
            {
                slipping++;
                Assert.Equal(0, roads.LaneFromKerb[from]);
            }
            else if (fromClass == RoadClass.Slip && toClass == RoadClass.Intercity)
            {
                slipping++;
                Assert.Equal(0, roads.LaneFromKerb[to]);
            }
        }

        Assert.True(slipping > 0, "no slip road was driven onto or off an intercity road");
    }

    /// <summary>
    /// <b>A road carried over an intercity road meets it nowhere</b>: neither junction it comes down at is one an intercity
    /// road meets — the bridge is no junction on the road under it.
    /// </summary>
    [Fact]
    public void ARoadCarriedOverAnIntercityRoadMeetsItNowhere()
    {
        var roads = Plan.Value.Roads;
        var intercityAt = new bool[Plan.Value.Junctions.CentreM.Length];
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads.Class[road] == RoadClass.Intercity) (intercityAt[roads.FromJunction[road]], intercityAt[roads.ToJunction[road]]) = (true, true);
        }

        var carried = Enumerable.Range(0, roads.Count)
            .Where(road => roads.LevelOf(road) == CityPlan.RoadArrays.Over && roads.Class[road] != RoadClass.Intercity).ToList();
        Assert.NotEmpty(carried);
        Assert.All(carried, road => Assert.False(
            intercityAt[roads.FromJunction[road]] || intercityAt[roads.ToJunction[road]],
            $"road {road} carried over comes down at a junction an intercity road meets"));
    }

    /// <summary>
    /// <b>The kerb lane a slip road leaves carries on along the intercity road as well</b>: leaving is the lane's choice and
    /// not where it ends, as a fork into two would make it (TER-5j).
    /// </summary>
    [Fact]
    public void TheKerbLaneASlipRoadLeavesCarriesOn()
    {
        var plan = Plan.Value;
        var roads = RoadGraph.Build(plan, Config);
        var (leaves, carriesOn) = (new HashSet<int>(), new HashSet<int>());
        for (var connector = 0; connector < roads.ConnectorCount; connector++)
        {
            var (from, to) = (roads.ConnectorFrom(connector), roads.ConnectorTo(connector));
            if (plan.Roads.Class[roads.LaneRoad[from]] != RoadClass.Intercity) continue;

            if (plan.Roads.Class[roads.LaneRoad[to]] == RoadClass.Slip) leaves.Add(from);
            if (plan.Roads.Class[roads.LaneRoad[to]] == RoadClass.Intercity) carriesOn.Add(from);
        }

        Assert.NotEmpty(leaves);
        Assert.All(leaves, lane => Assert.Contains(lane, carriesOn));
    }

    /// <summary>
    /// <b>A node an intercity road runs through and only slip roads meet carries lanes on and is no junction</b> (TER-5k):
    /// its slips fork off the kerb lane and merge back onto it, crossing nothing.
    /// </summary>
    [Fact]
    public void ANodeOnlySlipRoadsMeetCarriesLanesOn()
    {
        var plan = Plan.Value;
        var lanes = plan.Paving(Config).Lanes;
        var (through, other) = Junctions(plan.Roads, plan.Junctions.CentreM.Length);
        var met = 0;
        for (var junction = 0; junction < through.Length; junction++)
        {
            if (through[junction] < 2 || other[junction] || plan.Ground.ArmsPerJunction()[junction] < 3) continue;

            met++;
            Assert.True(lanes.IsSeamless(junction), $"junction {junction} at {plan.Junctions.CentreM[junction]} only slips meet is a box");
        }

        Assert.True(met > 0, "no slip road met an intercity road");
    }

    /// <summary><b>No bar stands on a lane arriving at a node that carries lanes on</b> (TER-5k): nothing there is held.</summary>
    [Fact]
    public void NoBarStandsBeforeANodeThatCarriesLanesOn()
    {
        var plan = Plan.Value;
        var paving = plan.Paving(Config);
        var ends = paving.RoadEnds(Config);
        var crossed = Crossings.Lay(plan, Config, ends.CrossedM);
        var bars = StopBars.Lay(paving.Lanes, Crossings.Lay(plan, Config, ends.HeldM), Config, crossed);
        foreach (var lane in bars.Lane)
        {
            Assert.False(paving.Lanes.IsSeamless(paving.Lanes.LaneToJunction[lane]), $"lane {lane} holds at a bar before a node that carries lanes on");
        }
    }

    /// <summary>
    /// <b>Every movement over a node that carries lanes on is straight on</b> (TER-5k), so a slip merging onto the kerb lane
    /// is granted its ground at the rung of the lane it merges with (TER-5e).
    /// </summary>
    [Fact]
    public void EveryMovementOverANodeThatCarriesLanesOnIsStraightOn()
    {
        var plan = Plan.Value;
        var roads = RoadGraph.Build(plan, Config);
        var carried = 0;
        for (var connector = 0; connector < roads.ConnectorCount; connector++)
        {
            if (!roads.CarriesOn(connector)) continue;

            carried++;
            Assert.Equal(LaneTurn.Straight, roads.KindOf(connector));
        }

        Assert.True(carried > 0, "no movement was over a node that carries lanes on");
    }

    /// <summary>
    /// <b>The paint down an intercity road runs on through a node that carries lanes on</b> (TER-5k, TER-6): one run holds
    /// the intercity road either side of it.
    /// </summary>
    [Fact]
    public void TheLinesDownAnIntercityRoadRunOnThroughANodeThatCarriesLanesOn()
    {
        var plan = Plan.Value;
        var lanes = plan.Paving(Config).Lanes;
        var runs = CentrelineRuns.Lay(plan, Config);
        var carriedThrough = new HashSet<int>();
        for (var run = 0; run < runs.Count; run++)
        {
            var roadsOf = runs.RoadsOf(run);
            for (var at = 0; at + 1 < roadsOf.Length; at++)
            {
                var (one, next) = (roadsOf[at], roadsOf[at + 1]);
                foreach (var junction in (ReadOnlySpan<int>)[plan.Roads.FromJunction[one], plan.Roads.ToJunction[one]])
                {
                    if (junction == plan.Roads.FromJunction[next] || junction == plan.Roads.ToJunction[next]) carriedThrough.Add(junction);
                }
            }
        }

        var (through, _) = Junctions(plan.Roads, plan.Junctions.CentreM.Length);
        var seamless = 0;
        for (var junction = 0; junction < through.Length; junction++)
        {
            if (through[junction] < 2 || !lanes.IsSeamless(junction)) continue;

            seamless++;
            Assert.True(carriedThrough.Contains(junction), $"the paint stops at junction {junction} at {plan.Junctions.CentreM[junction]}");
        }

        Assert.True(seamless > 0, "no intercity road ran through a node that carries lanes on");
    }
}
