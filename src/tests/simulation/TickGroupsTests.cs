using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Simulation;

[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P0)]
public class TickGroupsTests
{
    [Fact]
    public void TheIntervalIsStatedInSecondsAndCountedInTicks()
    {
        var groups = new TickGroups(0.1f, 60);

        Assert.Equal(6, groups.Groups);
        Assert.Equal(0.1f, groups.IntervalS, tolerance: 1e-6f);
    }

    [Fact]
    public void AtZeroEveryMemberTakesEveryTick()
    {
        var groups = new TickGroups(0f, 60);

        Assert.Equal(1, groups.Groups);
        for (var tick = 0; tick < 12; tick++)
        {
            for (var member = 0; member < 5; member++) Assert.True(groups.Turn(tick, member));
        }
    }

    /// <summary>
    /// The stagger is what stops the town's work spiking on one tick: over any window of a whole
    /// interval, each member takes exactly one turn and the load is spread evenly across the ticks —
    /// to the member where the roster is whole runs of every group, and to a run where it is not.
    /// </summary>
    [Fact]
    public void TheTownsWorkIsSpreadAcrossTheTicksRatherThanSpikingOnOne()
    {
        const int Members = 960;
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
    public void ARosterOfPartRunsIsSpreadToWithinARun()
    {
        const int Members = 1000;
        var groups = new TickGroups(0.1f, 60);
        var perTick = new int[groups.Groups];
        for (var tick = 0; tick < groups.Groups; tick++)
        {
            foreach (var _ in groups.InTurn(tick, Members)) perTick[tick]++;
        }

        Assert.InRange(perTick.Max() - perTick.Min(), 0, TickGroups.RunOfMembers);
    }

    /// <summary>The decide loop walks <see cref="TickGroups.InTurn"/>, so that is every member whose turn it is.</summary>
    [Theory]
    [InlineData(0.1f, 40)]
    [InlineData(0.1f, 250)]
    [InlineData(0f, 40)]
    public void WalkingTheTurnFindsExactlyTheMembersWhoseTurnItIs(float intervalS, int members)
    {
        var groups = new TickGroups(intervalS, 60);
        for (var tick = 0L; tick < 3 * groups.Groups; tick++)
        {
            var stepped = new List<int>();
            foreach (var member in groups.InTurn(tick, members)) stepped.Add(member);

            Assert.Equal(Enumerable.Range(0, members).Where(member => groups.Turn(tick, member)), stepped);
        }
    }

    [Fact]
    public void ARateBelowOneTickIsRoundedUpToEveryTick()
    {
        Assert.Equal(1, new TickGroups(0.001f, 60).Groups);
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
