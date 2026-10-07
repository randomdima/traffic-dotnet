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

/// <summary>
/// <b>The town's claims rebuild laid on a device in step</b> (<see cref="TownWorld.ClaimsDevice"/>): the town lays only what
/// it alone lays, the device every body, light, plan and walk and every answer, and the claims are written back before
/// the town lays the backing and reads a single grant — so every reader reads what the town's own rebuild would have laid.
/// </summary>
/// <remarks>
/// Declared here and implemented above (<c>app/devicetick</c>), as <see cref="ITownDevice"/> is.
/// </remarks>
internal interface ITownClaims
{
    /// <summary>
    /// <b>Whether the device lays this rebuild's claims</b> — asked once the claims clock comes round, before anything is
    /// laid. Where it does not, the town lays them itself.
    /// </summary>
    bool LaysTheClaims(TownWorld town);

    /// <summary>
    /// <b>A claims rebuild the device lays</b>, handed the town at every stage as the town lays what only it lays
    /// (<see cref="ClaimsStage"/>): at <see cref="ClaimsStage.PlansLaid"/> the device lays every body, light, plan and walk,
    /// answers every plan and writes the claims back into the town, which then lays the backing and reads every grant.
    /// </summary>
    void ClaimsAt(TownWorld town, ClaimsStage stage);
}
