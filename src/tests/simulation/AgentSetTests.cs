using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Simulation;

/// <summary>
/// <b>A set of agents walks the agents of either set once each, in order, and none at or past the count</b> — what a tick
/// walks in place of every agent, so one left out is an agent the host never lays again.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class AgentSetTests
{
    [Fact]
    public void AUnionWalksEachAgentOfEitherOnceInOrder()
    {
        var held = new AgentSet(200);
        var touched = new AgentSet(200);
        foreach (var agent in (ReadOnlySpan<int>)[130, 3, 64]) held.Add(agent);
        foreach (var agent in (ReadOnlySpan<int>)[64, 199, 0]) touched.Add(agent);

        Span<int> walked = stackalloc int[200];
        var count = held.Union(touched, 200, walked);

        Assert.Equal([0, 3, 64, 130, 199], walked[..count].ToArray());
    }

    [Fact]
    public void NoAgentAtOrPastTheCountIsWalked()
    {
        var set = new AgentSet(200);
        set.AddFirst(200);

        Span<int> walked = stackalloc int[200];

        Assert.Equal(70, set.CopyTo(70, walked));
    }

    [Fact]
    public void AddingTheFirstAddsNoneAtOrPastThem()
    {
        var set = new AgentSet(200);
        set.AddFirst(70);

        Assert.Equal(70, set.CountBelow(200));
    }
}
