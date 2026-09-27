using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Exam;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>How the movements through a junction are classified where the lanes meeting there bend (TER-5e, GEN-19).</summary>
[Trait(Tier.Key, Tier.Town)]
[Trait(Priority.Key, Priority.P4)]
public class LaneLinesTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// <b>Going round a roundabout is straight on, and coming onto it is a turn to the kerb side</b> (GEN-19):
    /// the ring bends through every node by the arc the node takes out of it, which read end to end would be a
    /// turn towards the island — weaker than every entry it meets, where circulating traffic holds for none.
    /// </summary>
    [Fact]
    public void GoingRoundARingIsStraightOnAndComingOntoItIsANearSideTurn()
    {
        var plan = ExamPlan.Lay(Config);
        var roads = RoadGraph.Build(plan, Config);
        var ring = new HashSet<int>(plan.Roundabouts.Road);
        var round = 0;
        for (var connector = 0; connector < roads.ConnectorCount; connector++)
        {
            if (!ring.Contains(roads.LaneRoad[roads.ConnectorTo(connector)])) continue;

            var goesRound = ring.Contains(roads.LaneRoad[roads.ConnectorFrom(connector)]);
            if (goesRound) round++;
            Assert.Equal(goesRound ? LaneTurn.Straight : LaneTurn.NearSide, roads.KindOf(connector));
        }

        Assert.True(round > 0, "the scenario map laid no ring to go round");
    }
}
