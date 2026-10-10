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
    /// <summary>
    /// <b>Whether the town leaves this agent's tick to the device this tick</b>, asked before the agent's own tick: the device
    /// ticks it, or puts off a car the town ticks itself until what that tick reads is written back, and has the town tick
    /// it then (<see cref="TownWorld.TickTheCar"/>).
    /// </summary>
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

    /// <summary>
    /// <b>Whether the town decides a tick behind the device</b> (root decision log, 2026-10-08): the decisions due on a tick
    /// are put off and taken while the next tick runs on the device (<see cref="TownWorld.TakeTheDecisionsPutOff"/>), on the
    /// town as the tick before left it, so what one does is ticked a tick later. A tick handed back after them leaves the
    /// town's own to what a decision changed meanwhile.
    /// </summary>
    bool DecidesATickBehind { get; }

    /// <summary>
    /// <b>Every car the device ticked handed back as its last tick left it, and every walker as its last claims rebuild
    /// left it</b>, where the town is about to read every agent — a closure sending the town round it, the town laying the
    /// claims itself — rather than only those it reads (<see cref="TownWorld.ReadsTheTickOf"/>,
    /// <see cref="TownWorld.ReadsTheWalker"/>), every pose with them (<see cref="TakeEveryPoseBack"/>). Nothing where the
    /// device hands every agent back.
    /// </summary>
    void TakeEveryCarBack(TownWorld town)
    {
    }

    /// <summary>
    /// <b>This agent's pose as the device's last step left it</b>, written into the town's fleets and its solver's table
    /// where they hold an older one — before the town reads a body it does not read every tick: a partner, the nearest of a
    /// kind, one picked out, one a crash wrecked. Nothing where the device hands every pose back.
    /// </summary>
    void TakeThePoseBack(TownWorld town, int agent)
    {
    }

    /// <summary><b>Every agent's pose as the device's last step left it</b>, where the town is about to read every one.</summary>
    void TakeEveryPoseBack(TownWorld town)
    {
    }

    /// <summary>
    /// <b>The town let go of</b> as it stops ticking on the device: every pose handed back (<see cref="TakeEveryPoseBack"/>),
    /// and whatever the device was to take of the town's handed to it.
    /// </summary>
    void LetGo(TownWorld town) => TakeEveryPoseBack(town);

    /// <summary>
    /// <b>One walker handed back as the device's last claims rebuild left it</b>, where the town is about to change it out of
    /// its own turn — struck down, or taken in hand. Nothing for one the town holds as the device left it.
    /// </summary>
    void TakeTheWalkerBack(TownWorld town, int person)
    {
    }

    /// <summary>
    /// <b>One car handed back as the device's last tick left it</b>, where the interface has just picked it out and may
    /// read or change it before the town's next tick reads it (<see cref="TownWorld.ReadsTheTickOf"/>) — an order, a hand,
    /// letting it go. Nothing for one the town holds as the device left it.
    /// </summary>
    void TakeTheCarBack(TownWorld town, int car)
    {
    }

    /// <summary>
    /// <b>How far this car may yet go on its grant</b> (<see cref="Agents.Car.Body.CarFleet.GrantLeftM"/>) as the town would
    /// hold it were the car handed back now: off the device where it keeps a car the town has not read, handed back with
    /// every tick while the town watches every agent (<see cref="TownWorld.WatchesEveryAgent"/>) — the town's own otherwise.
    /// </summary>
    float GrantLeftM(TownWorld town, int car) => town.Cars.GrantLeftM(car);

    /// <summary>
    /// <b>Whether this car's decision, put off a tick, was its clock alone and run on the device</b>
    /// (<see cref="TownWorld.TakesOnlyTheClock"/>) — or, where it was more, what the town decides it on handed back first.
    /// False for a car the device did not decide, which the town decides with what it holds.
    /// </summary>
    bool DecidedOnTheDevice(int car) => false;

    /// <summary>
    /// <b>Whether the device names the cars the town ticks and decides itself</b> (<see cref="CarsTheTownTicks"/>,
    /// <see cref="CarsTheTownDecides"/>): it takes every other car and every walker not under a hand
    /// (<see cref="TownWorld.WalksUnderAHand"/>), while the agents are not held — so the town's two passes walk those and no
    /// other agent. False, the town asks every agent (<see cref="Takes"/>).
    /// </summary>
    bool NamesTheCars => false;

    /// <summary>
    /// <b>The cars the town ticks itself this tick, and those it may</b>, in order, where the device names them
    /// (<see cref="NamesTheCars"/>): every car any other answer of <see cref="Takes"/> could be other than the device's own
    /// tick taking it. Asked before any agent is ticked.
    /// </summary>
    ReadOnlySpan<int> CarsTheTownTicks(TownWorld town) => default;

    /// <summary>
    /// <b>The cars whose decisions the town takes on their turn</b>, in order, where the device names them
    /// (<see cref="NamesTheCars"/>): every car the device does not find the decision of (<see cref="DecidesItself"/>).
    /// </summary>
    ReadOnlySpan<int> CarsTheTownDecides(TownWorld town) => default;

    /// <summary>
    /// <b>Whether the device finds this car's decision itself</b>, a tick behind (<see cref="DecidesATickBehind"/>): the town
    /// puts nothing off for it, and is handed it to decide only where the device finds it more than its clock
    /// (<see cref="TownWorld.PutOffTheDecisions"/>).
    /// </summary>
    bool DecidesItself(int car) => false;

    /// <summary>
    /// <b>Whether this walker's decision, put off a tick, was its clock alone and run on the device</b>
    /// (<see cref="TownWorld.WalkerTakesOnlyTheClock"/>) — or, where it was more, its clock handed back first. False for a
    /// walker the device did not decide.
    /// </summary>
    bool WalkerDecidedOnTheDevice(int person) => false;
}

/// <summary>
/// <b>The town's claims rebuild laid on a device in step</b> (<see cref="TownWorld.ClaimsDevice"/>): the town lays only what
/// it alone lays, the device every body, light, plan and walk, every answer and the backing after them, and the claims are
/// written back before the town reads a single grant — so every reader reads what the town's own rebuild would have laid.
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
    /// answers every plan, lays the backing every car asked for by then (<see cref="TownWorld.TakeTheBackingAsk"/>) and
    /// writes the claims back into the town, which then reads every grant it did not.
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
    /// <b>Whether the device keeps every walker's side of the claims as its own</b>: stations each, lays its body and its
    /// pass, plans its walk and — where it <see cref="ReadTheGrants"/> — reads its grant, its pass's word and its aim, and
    /// writes back what it made of each by <see cref="ClaimsStage.PlansLaid"/> (<see cref="TownWorld.KeptOnTheDevice"/>). The
    /// town then stations no walker, lays no walker's pass, and reads only the grants the device did not.
    /// </summary>
    bool KeepsTheWalkers { get; }

    /// <summary>
    /// <b>Whether the rebuild under way writes its answers into the layer every reader reads later</b> than its own stages —
    /// asked once <see cref="ClaimsStage.PlansLaid"/> is done: the readers go on reading the last laying's layer, with the
    /// numbers each agent was laid with for it, until the device lands this one (<see cref="TownWorld.LandTheLaying"/>).
    /// </summary>
    bool LandsLater { get; }

    /// <summary>The last rebuild's answers written into the layer every reader reads now, if they wait to be; nothing otherwise.</summary>
    void LandTheLastLaying(TownWorld town);
}
