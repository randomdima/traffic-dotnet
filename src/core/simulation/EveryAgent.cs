namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// <b>Phase 3's two passes over the whole roster</b> (<see cref="ISimWorld.TickTheAgents"/>,
/// <see cref="ISimWorld.DecideTheAgents"/>), for a world that leaves no agent to anything else.
/// </summary>
/// <remarks>Generic over the world, so a struct world is called directly and nothing boxes.</remarks>
internal static class EveryAgent
{
    /// <summary><see cref="ISimWorld.TickAgent"/> of every agent not terminal, in roster order.</summary>
    public static void Tick<TWorld>(ref TWorld world) where TWorld : ISimWorld
    {
        var agents = world.AgentCount;
        for (var agent = 0; agent < agents; agent++)
        {
            if (!world.IsTerminal(agent)) world.TickAgent(agent);
        }
    }

    /// <summary>
    /// <see cref="ISimWorld.DecideAgent"/> of every agent not terminal whose turn <paramref name="tick"/> is, or that decides
    /// every tick, in roster order.
    /// </summary>
    public static void Decide<TWorld>(ref TWorld world, long tick, TickGroups decisions) where TWorld : ISimWorld
    {
        var agents = world.AgentCount;
        for (var agent = 0; agent < agents; agent++)
        {
            if (world.IsTerminal(agent)) continue;

            if (decisions.Turn(tick, agent) || world.DecidesEveryTick(agent)) world.DecideAgent(agent, decisions.IntervalS);
        }
    }

    /// <summary>
    /// <see cref="Decide"/> for a world none of whose agents decides every tick: the same agents in the same order, found
    /// by walking the turn's runs (<see cref="TickGroups.InTurn"/>) rather than by asking every agent.
    /// </summary>
    public static void DecideTheTurn<TWorld>(ref TWorld world, long tick, TickGroups decisions) where TWorld : ISimWorld
    {
        foreach (var agent in decisions.InTurn(tick, world.AgentCount))
        {
            if (!world.IsTerminal(agent)) world.DecideAgent(agent, decisions.IntervalS);
        }
    }
}
