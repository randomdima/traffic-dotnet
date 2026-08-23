using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Simulation;

[Trait(Tier.Key, Tier.Unit)]
public class TickGroupsTests
{
    [Fact]
    public void TheIntervalIsStatedInSecondsAndCountedInTicks()
    {
        var groups = new TickGroups(0.1f, 60);

        Assert.Equal(6, groups.Groups);
        Assert.Equal(0.1f, groups.IntervalS, tolerance: 1e-6f);
        Assert.False(groups.EveryTick);
    }

    [Fact]
    public void AtZeroEveryMemberTakesEveryTick()
    {
        var groups = new TickGroups(0f, 60);

        Assert.True(groups.EveryTick);
        for (var tick = 0; tick < 12; tick++)
        {
            for (var member = 0; member < 5; member++) Assert.True(groups.Turn(tick, member));
        }
    }

    /// <summary>
    /// The stagger is what stops the town's work spiking on one tick: over any window of a whole
    /// interval, each member takes exactly one turn and the load is spread evenly across the ticks.
    /// </summary>
    [Fact]
    public void TheTownsWorkIsSpreadAcrossTheTicksRatherThanSpikingOnOne()
    {
        const int Members = 600;
        var groups = new TickGroups(0.1f, 60);

        var perTick = new int[groups.Groups];
        for (var member = 0; member < Members; member++)
        {
            var turns = 0;
            for (var tick = 0; tick < groups.Groups; tick++)
            {
                if (!groups.Turn(tick, member)) continue;
                turns++;
                perTick[tick]++;
            }

            Assert.Equal(1, turns);
        }

        Assert.All(perTick, count => Assert.Equal(Members / groups.Groups, count));
    }

    [Fact]
    public void ARateBelowOneTickIsRoundedUpToEveryTick()
    {
        Assert.True(new TickGroups(0.001f, 60).EveryTick);
    }

    [Fact]
    public void TheShippedIntervalIsAMetreOfWorldAtTownSpeed()
    {
        var config = SimConfig.Shipped();
        var thinking = new TickGroups(config.Sim.AgentDecisionIntervalS, config.Sim.TickRateHz);

        // The whole argument for the interval: a stale answer is worth about a metre, against a 13.8 m
        // mean following distance.
        Assert.InRange(thinking.IntervalS * 10f, 0.5f, 2f);
    }
}
