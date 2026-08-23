namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// The five phases of one tick, in the fixed order, plus the roster phase 3 walks. The order is what
/// makes every decision in a tick see the same instant of the world: nothing in the proximity index
/// survives a tick, and no body moves before <see cref="StepBodies"/>.
/// </summary>
/// <remarks>
/// <see cref="SimLoop{TWorld}"/> takes this as a type parameter, never as a field of interface type,
/// so a struct world is called directly and a sealed class world is devirtualised — no per-tick
/// interface dispatch and no allocation.
/// </remarks>
internal interface ISimWorld
{
    /// <summary>Stable within a tick. The index is the agent's identity for the decision clock's stagger.</summary>
    int AgentCount { get; }

    /// <summary>A terminal agent is not asked to think. Its body is still stepped, struck and pushed.</summary>
    bool IsTerminal(int agent);

    /// <summary>Manoeuvres negotiating with something moving, or steering to a pose, declare this and run every tick.</summary>
    bool DecidesEveryTick(int agent);

    /// <summary>Phase 1 — the player's input, so keys land before the decisions they feed.</summary>
    void ReadPlayerInput();

    /// <summary>Phase 2 — the proximity index rebuilt from the body roster. It survives nothing.</summary>
    void RebuildProximityIndex();

    /// <summary>Phase 3a — hard rules and junction reservation, asked every tick regardless of the clock.</summary>
    void TickAgent(int agent);

    /// <summary>Phase 3b — the manoeuvre catalogue, run on the decision clock.</summary>
    void DecideAgent(int agent, float sinceLastDecisionS);

    /// <summary>Phase 4 — every body stepped, where phase 3's impulses are applied.</summary>
    void StepBodies(float dtS);

    /// <summary>Phase 5 — contact arbitration, and the only place damage is decided.</summary>
    void ResolveContacts();
}
