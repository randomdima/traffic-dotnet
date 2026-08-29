namespace TrafficSimulation.Agents.Evacuator;

/// <summary>
/// <b>What an evacuator is doing about the wreck it is on</b> (EVA-3). The life cycle as observable
/// states, on the terms <see cref="Ambulance.RescueStage"/> names a rescue's: there is no state an
/// evacuator can be in that this does not name, and every transition between two of them is one line of
/// <c>TownWorld.Recovery.cs</c>.
/// </summary>
/// <remarks>
/// <b>These are not manoeuvres and do not pretend to be.</b> An evacuator drives the same closed catalogue
/// every other car drives (AGT-7) and the recovery gained no entry for it; what is here is the errand those
/// manoeuvres are being run for.
/// </remarks>
internal enum RecoveryStage : byte
{
    /// <summary>Standing in its own bay at its depot with nothing to clear. Where an evacuator spends most of a run.</summary>
    Waiting,

    /// <summary>Under way to the wreck, carrying the priority: the one leg of a recovery that is urgent (EVA-4).</summary>
    Running,

    /// <summary>Stopped beside the wreck with the recovery man out at the arm, working it onto the hook (EVA-5).</summary>
    Hitching,

    /// <summary>The wreck on the bar and the recovery man walking back to his own seat (SRV-3).</summary>
    BoardingAtTheScene,

    /// <summary>Under way to the depot's yard with the wreck on the bar, and no priority at all — a loaded evacuator is ordinary traffic.</summary>
    Hauling,

    /// <summary>Standing at the yard with the man out at the slot, asking it for a place. A full yard refuses, which is a wait and not a failure.</summary>
    Unhitching,

    /// <summary>The wreck in its slot and the man walking back to his seat (SRV-3).</summary>
    BoardingAtTheYard,

    /// <summary>Driving back to its own bay with nothing on the hook.</summary>
    GoingHome,
}

/// <summary>
/// <b>Every evacuator's recovery, as one array per field</b> — keyed by the car, because an evacuator is a
/// car and a car is an index. Nothing here is a decision; it is what the town wrote down about the errand
/// each one is on.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is laid over the whole fleet rather than over the evacuators alone</b>, for the reason
/// <see cref="Ambulance.RescueDuty"/> is: a car's index means the same thing here as it does everywhere
/// else, so nothing has to hold a second numbering and nothing can disagree about which car a tow belongs
/// to. <see cref="Towing"/> and <see cref="OnTheHookOf"/> are the same fact from its two ends, and both are
/// on this array so a wreck can be asked what has it as cheaply as an evacuator can be asked what it has.
/// </para>
/// <para>
/// <b>A recovery is bounded</b> (<see cref="SinceS"/>, EVA-8), exactly as a call is: a wreck the traffic
/// never lets an evacuator reach must not hold the town's only evacuator off every later one.
/// </para>
/// </remarks>
internal sealed class RecoveryDuty
{
    public RecoveryDuty(int cars)
    {
        Stage = new RecoveryStage[cars];
        Wreck = new int[cars];
        Array.Fill(Wreck, Nothing);
        Towing = new int[cars];
        Array.Fill(Towing, Nothing);
        OnTheHookOf = new int[cars];
        Array.Fill(OnTheHookOf, Nothing);
        HeldByTheTail = new bool[cars];
        Depot = new int[cars];
        Array.Fill(Depot, NoBuilding);
        Yard = new int[cars];
        Array.Fill(Yard, NoYard);
        HomeBay = new int[cars];
        Array.Fill(HomeBay, NoBay);
        HaulsLeft = new int[cars];
        InTheYard = new bool[cars];
        SinceS = new float[cars];
        HitchedForS = new float[cars];
        RepairedForS = new float[cars];
    }

    /// <summary>What this evacuator is doing about its wreck. <see cref="RecoveryStage.Waiting"/> for every car that is not one.</summary>
    public RecoveryStage[] Stage { get; }

    /// <summary>The wreck this recovery is for, or <see cref="Nothing"/>. <b>One wreck to a recovery and one recovery to a wreck.</b></summary>
    public int[] Wreck { get; }

    /// <summary>
    /// The car actually on this one's bar, or <see cref="Nothing"/>. It is not the same as
    /// <see cref="Wreck"/>: a recovery has a wreck from the moment it is taken and something on the hook
    /// only once the crew has got it there.
    /// </summary>
    public int[] Towing { get; }

    /// <summary>And the evacuator whose bar this car is on — the same coupling read from the wreck's end.</summary>
    public int[] OnTheHookOf { get; }

    /// <summary>
    /// <b>Which end of <em>this car</em> the arm caught</b> (EVA-5) — its tail, or its nose. On the towed
    /// car's own index rather than the truck's, because it is a fact about how that body is sitting: which
    /// two of its wheels are in the air, and which way the arm reaches to it.
    /// </summary>
    public bool[] HeldByTheTail { get; }

    /// <summary>The depot this evacuator belongs to: whose yard a wreck is taken to, and what its own bay is near.</summary>
    public int[] Depot { get; }

    /// <summary>Which of the town's yards that depot's is, which is how its slots are found (EVA-2).</summary>
    public int[] Yard { get; }

    /// <summary>The bay it waits in between recoveries, held for it for the whole run (GEN-4k).</summary>
    public int[] HomeBay { get; }

    /// <summary>
    /// How many more times this haul may run out of clock and be laid again before the wreck is set down
    /// where the evacuator stands and the recovery given up (EVA-8).
    /// </summary>
    public int[] HaulsLeft { get; }

    /// <summary>
    /// Whether <em>this car</em> is standing in a yard slot rather than lying in the town — the state that
    /// makes a wreck stop being a recovery and start being a repair (EVA-6, EVA-7).
    /// </summary>
    public bool[] InTheYard { get; }

    /// <summary>How long this leg has been running, which is the bound that ends an unreachable recovery.</summary>
    public float[] SinceS { get; }

    /// <summary>How long the crew has been working on the hook — the clock both ends of a tow are timed on.</summary>
    public float[] HitchedForS { get; }

    /// <summary>
    /// And how long <em>this car</em> has been standing in a yard slot being put back together (EVA-7).
    /// It is on the wreck's own index rather than the evacuator's, because by then the evacuator has gone.
    /// </summary>
    public float[] RepairedForS { get; }

    public const int Nothing = -1;

    public const int NoBuilding = -1;

    public const int NoYard = -1;

    public const int NoBay = -1;

    /// <summary>Whether this evacuator is on a recovery at all, which is every stage but the one it stands in.</summary>
    public bool IsOnARecovery(int car) => Stage[car] != RecoveryStage.Waiting;

    /// <summary>
    /// Whether it is carrying the priority (EVA-4): the one leg it is hurrying on, and none of the four it
    /// stands still for or hauls or drives home. <b>The way out is a rescue and the way back is traffic.</b>
    /// </summary>
    public bool IsHurrying(int car) => Stage[car] == RecoveryStage.Running;

    /// <summary>The recovery given up or discharged: everything it held, dropped in one place.</summary>
    public void Clear(int car)
    {
        Stage[car] = RecoveryStage.Waiting;
        Wreck[car] = Nothing;
        SinceS[car] = 0f;
        HitchedForS[car] = 0f;
    }
}
