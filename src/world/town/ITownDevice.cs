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
    /// hands back what they left — and what a tick of theirs asks of the town (a line lost, a lane stepped onto) — and,
    /// where it <see cref="StepsWithTheTick"/>, steps every body after them in the same submit.
    /// </summary>
    void TickTheRest(TownWorld town);

    /// <summary>
    /// <b>Phase 4</b>: every body stepped on the device and handed back as the step left it — the cars, the walkers, the
    /// solver's moving bodies, and the pairs that began touching for phase 5 to judge. Never asked of a device that
    /// <see cref="StepsWithTheTick"/>.
    /// </summary>
    void StepBodies(TownWorld town, float dtS);

    /// <summary>
    /// <b>Whether the bodies are stepped with the agents' tick</b>, in <see cref="TickTheRest"/>, rather than after the
    /// decisions: the town readies the step before handing the tick over (<c>TownWorld.ReadyTheStep</c>), the decisions
    /// read the town the step left, and what a decision does to a body is stepped a tick later.
    /// </summary>
    bool StepsWithTheTick { get; }
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

    /// <summary>
    /// <b>Whether the rebuild under way read every asked car's grant and every walker's on the device</b>, with the
    /// answers, and wrote them into the town by <see cref="ClaimsStage.PlansAnswered"/>
    /// (<see cref="TownWorld.GrantedOnTheDevice"/>, <see cref="TownWorld.WalkerGrantedOnTheDevice"/>) — so the town reads
    /// none off the claims.
    /// </summary>
    bool ReadTheGrants { get; }

    /// <summary>
    /// <b>Whether the rebuild under way writes its answers into the layer every reader reads later</b> than its own stages —
    /// asked once <see cref="ClaimsStage.PlansLaid"/> is done: the readers go on reading the last laying's layer, with the
    /// numbers each agent was laid with for it, until the device lands this one (<see cref="TownWorld.LandTheLaying"/>).
    /// </summary>
    bool LandsLater { get; }

    /// <summary>The last rebuild's answers written into the layer every reader reads now, if they wait to be; nothing otherwise.</summary>
    void LandTheLastLaying(TownWorld town);
}
