using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Simulation;

[Trait(Tier.Key, Tier.Unit)]
public class SimLoopTests
{
    static SimConfig Clocked(float intervalS) => new() { Sim = new SimFigures { AgentDecisionIntervalS = intervalS } };

    [Fact]
    public void TheFivePhasesRunInTheOrderTheBriefFixes()
    {
        var world = new RecordingWorld(agentCount: 2);
        new SimLoop<RecordingWorld>(world, Clocked(0f)).Advance();

        Assert.Equal(
            [
                (Phase.Input, -1),
                (Phase.Index, -1),
                (Phase.TickAgent, 0),
                (Phase.Decide, 0),
                (Phase.TickAgent, 1),
                (Phase.Decide, 1),
                (Phase.Step, -1),
                (Phase.Contacts, -1),
            ],
            world.Log);
    }

    /// <summary>
    /// Nothing in the index survives a tick and no body moves before the step, so every decision in
    /// a tick is taken against the same instant of the world.
    /// </summary>
    [Fact]
    public void NothingIsDecidedAfterABodyHasMoved()
    {
        var world = new RecordingWorld(agentCount: 8);
        new SimLoop<RecordingWorld>(world, Clocked(0f)).Advance(4);

        var stepped = false;
        foreach (var (phase, _) in world.Log)
        {
            switch (phase)
            {
                case Phase.Step:
                    stepped = true;
                    break;
                case Phase.Index:
                    stepped = false;
                    break;
                case Phase.TickAgent or Phase.Decide:
                    Assert.False(stepped, "an agent thought after a body had already moved this tick");
                    break;
            }
        }
    }

    [Fact]
    public void ATerminalAgentIsNotAskedToThink()
    {
        var world = new RecordingWorld(agentCount: 3);
        world.Terminal.Add(1);

        new SimLoop<RecordingWorld>(world, Clocked(0f)).Advance(6);

        Assert.DoesNotContain(world.Log, entry => entry.Agent == 1);
        Assert.Contains(world.Log, entry => entry.Agent == 0);
        Assert.Contains(world.Log, entry => entry.Agent == 2);
    }

    /// <summary>
    /// The hard rules and the junction reservation are asked every tick regardless of the clock; the
    /// catalogue is what the clock holds back.
    /// </summary>
    [Fact]
    public void HardRulesAreAskedEveryTickAndTheCatalogueIsNot()
    {
        const int Ticks = 60;
        var world = new RecordingWorld(agentCount: 1);
        var config = Clocked(0.1f);

        new SimLoop<RecordingWorld>(world, config).Advance(Ticks);

        Assert.Equal(Ticks, world.Log.Count(entry => entry.Phase == Phase.TickAgent));
        Assert.Equal(Ticks / 6, world.Decisions);
    }

    /// <summary>A manoeuvre negotiating with something that is itself moving declares for itself that it runs every tick.</summary>
    [Fact]
    public void AManoeuvreMayDeclareThatItMustRunEveryTick()
    {
        const int Ticks = 60;
        var world = new RecordingWorld(agentCount: 2);
        world.EveryTick.Add(1);

        new SimLoop<RecordingWorld>(world, Clocked(0.1f)).Advance(Ticks);

        Assert.Equal(Ticks / 6, world.Log.Count(entry => entry is { Phase: Phase.Decide, Agent: 0 }));
        Assert.Equal(Ticks, world.Log.Count(entry => entry is { Phase: Phase.Decide, Agent: 1 }));
    }

    /// <summary>
    /// The equivalence that proves the clock changed no behaviour it should not have: at an interval
    /// of 0 every agent thinks every tick, which is the un-clocked town. Once there are agents with
    /// state, the two runs are compared tick for tick; at M0 what can be compared is who was asked.
    /// </summary>
    [Fact]
    public void AtIntervalZeroTheTownIsTheUnClockedTown()
    {
        const int Ticks = 30;
        const int Agents = 12;

        var unclocked = new RecordingWorld(Agents);
        new SimLoop<RecordingWorld>(unclocked, Clocked(0f)).Advance(Ticks);

        var clocked = new RecordingWorld(Agents);
        new SimLoop<RecordingWorld>(clocked, Clocked(0.1f)).Advance(Ticks);

        Assert.Equal(Ticks * Agents, unclocked.Decisions);
        Assert.Equal(Ticks * Agents / 6, clocked.Decisions);
    }

    [Fact]
    public void TheRosterIsWalkedInAStableOrder()
    {
        var world = new RecordingWorld(agentCount: 4);
        new SimLoop<RecordingWorld>(world, Clocked(0f)).Advance(3);

        var ticked = world.Log.Where(entry => entry.Phase == Phase.TickAgent).Select(entry => entry.Agent).ToArray();

        Assert.Equal([0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3], ticked);
    }
}
