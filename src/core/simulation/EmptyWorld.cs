namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// A world with a roster and nothing in it: the loop's stand-in until there is a town to open, and the
/// fixture the allocation gate is taken on. A struct, so the phase calls are direct and a tick over it
/// allocates nothing.
/// </summary>
internal struct EmptyWorld(int agentCount) : ISimWorld
{
    public int AgentCount { get; } = agentCount;

    public long Decisions { get; private set; }

    public long AgentTicks { get; private set; }

    public readonly bool IsTerminal(int agent) => false;

    public readonly bool DecidesEveryTick(int agent) => false;

    public readonly void ReadPlayerInput() { }

    public readonly void RebuildProximityIndex() { }

    public void TickAgent(int agent) => AgentTicks++;

    public void DecideAgent(int agent, float sinceLastDecisionS) => Decisions++;

    public readonly void StepBodies(float dtS) { }

    public readonly void ResolveContacts() { }
}
