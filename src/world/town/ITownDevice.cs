namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The town's tick run on a device rather than in the town's own loops</b> — in step: the agents it takes ticked there,
/// and the bodies stepped there, each phase's answer handed back into the town before anything reads it, so every
/// decision, instrument and agent the town still ticks itself reads what it reads today.
/// </summary>
/// <remarks>
/// Declared here and implemented above (<c>app/devicetick</c>), so the town names what it hands over and nothing of how.
/// </remarks>
internal interface ITownDevice
{
    /// <summary>Whether the device ticks this agent this tick, so that the town does not — asked before the agent's own tick.</summary>
    bool Takes(int agent);

    /// <summary>
    /// <b>The end of phase 3a</b>: the town has ticked every agent the device does not take. The device ticks the rest and
    /// hands back what they left — and what a tick of theirs asks of the town (a line lost, a lane stepped onto).
    /// </summary>
    void TickTheRest(TownWorld town);

    /// <summary>
    /// <b>Phase 4</b>: every body stepped on the device and handed back as the step left it — the cars, the walkers, the
    /// solver's moving bodies, and the pairs that began touching for phase 5 to judge.
    /// </summary>
    void StepBodies(TownWorld town, float dtS);
}
