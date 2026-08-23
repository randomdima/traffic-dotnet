using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.Tests.Simulation;

internal enum Phase
{
    Input,
    Index,
    TickAgent,
    Decide,
    Step,
    Contacts,
}

/// <summary>
/// A world that does nothing and writes down what it was asked, in the order it was asked. The
/// fixture for the one thing the loop owns: that the five phases happen in the order that makes
/// every decision in a tick see one instant of the world.
/// </summary>
internal sealed class RecordingWorld(int agentCount) : ISimWorld
{
    public List<(Phase Phase, int Agent)> Log { get; } = [];

    public HashSet<int> Terminal { get; } = [];

    public HashSet<int> EveryTick { get; } = [];

    public int AgentCount { get; } = agentCount;

    public int Decisions => Log.Count(entry => entry.Phase == Phase.Decide);

    public bool IsTerminal(int agent) => Terminal.Contains(agent);

    public bool DecidesEveryTick(int agent) => EveryTick.Contains(agent);

    public void ReadPlayerInput() => Log.Add((Phase.Input, -1));

    public void RebuildProximityIndex() => Log.Add((Phase.Index, -1));

    public void TickAgent(int agent) => Log.Add((Phase.TickAgent, agent));

    public void DecideAgent(int agent, float sinceLastDecisionS) => Log.Add((Phase.Decide, agent));

    public void StepBodies(float dtS) => Log.Add((Phase.Step, -1));

    public void ResolveContacts() => Log.Add((Phase.Contacts, -1));
}
