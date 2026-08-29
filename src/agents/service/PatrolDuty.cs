namespace TrafficSimulation.Agents.Service;

/// <summary>
/// <b>What a police car is doing about its beat</b> (SRV-5). The life cycle as observable states, on the
/// terms <see cref="Ambulance.RescueStage"/> names a rescue's: there is no state a patrol car can be in
/// that this does not name, and every transition between two of them is one line of
/// <c>TownWorld.Patrol.cs</c>.
/// </summary>
/// <remarks>
/// <b>These are not manoeuvres and do not pretend to be.</b> A police car drives the same closed catalogue
/// every other car drives (AGT-7) and gained no entry for this; what is here is the errand those
/// manoeuvres are being run for.
/// </remarks>
internal enum PatrolStage : byte
{
    /// <summary>Standing on its station's apron, waiting out the interval before the next beat.</summary>
    Standing,

    /// <summary>Under way to somewhere in the town it was sent, with no priority over anybody.</summary>
    Patrolling,

    /// <summary>Under way to a scene it has been called to, and carrying the priority for that leg (SRV-6).</summary>
    Attending,

    /// <summary>Stopped short of the scene with the officer out beside the road, holding it closed (SRV-6).</summary>
    Closing,

    /// <summary>The scene done with and the officer walking back to his own seat (SRV-3).</summary>
    BoardingAtTheScene,

    /// <summary>The beat driven out, on its way back to its own bay.</summary>
    ReturningToStation,
}

/// <summary>
/// <b>Every police car's beat, as one array per field</b> — keyed by the car, because a police car is a
/// car and a car is an index. Nothing here is a decision; it is what the town wrote down about the beat
/// each one is on.
/// </summary>
/// <remarks>
/// <b>It is laid over the whole fleet rather than over the police cars alone</b>, for the reason
/// <see cref="Ambulance.RescueDuty"/> is: a car's index means the same thing here as it does everywhere
/// else, so nothing has to hold a second numbering.
/// </remarks>
internal sealed class PatrolDuty
{
    public PatrolDuty(int cars)
    {
        Stage = new PatrolStage[cars];
        Station = new int[cars];
        Array.Fill(Station, NoBuilding);
        HomeBay = new int[cars];
        Array.Fill(HomeBay, NoBay);
        SinceS = new float[cars];
        RestS = new float[cars];
        LegsLeft = new int[cars];
        Casualty = new int[cars];
        Array.Fill(Casualty, Nobody);
        Wreck = new int[cars];
        Array.Fill(Wreck, Nobody);
        ClosedForS = new float[cars];
    }

    /// <summary>What this police car is doing. <see cref="PatrolStage.Standing"/> for every car that is not one.</summary>
    public PatrolStage[] Stage { get; }

    /// <summary>The police station it belongs to, which is what its beat starts and finishes at.</summary>
    public int[] Station { get; }

    /// <summary>The bay on that station's apron held for it for the whole run (SRV-2, GEN-4k).</summary>
    public int[] HomeBay { get; }

    /// <summary>
    /// How long this stage has been running: the wait before a beat while it stands, and the bound on a leg
    /// while it drives (SRV-5).
    /// </summary>
    public float[] SinceS { get; }

    /// <summary>How long this stand is to last, drawn when the car came home so no two of a station's cars share it.</summary>
    public float[] RestS { get; }

    /// <summary>How many more places this beat visits before the car is due back at its station.</summary>
    public int[] LegsLeft { get; }

    /// <summary>
    /// <b>The scene this car has been called to</b> (SRV-6): a casualty lying in the road, or a wreck
    /// standing in it. <b>Exactly one of the two, and <see cref="Nobody"/> in both for a car on an ordinary
    /// beat</b> — a scene is one thing to be closed round, and two fields is how the town says which roster
    /// the index is into without carrying a kind beside it.
    /// </summary>
    public int[] Casualty { get; }

    public int[] Wreck { get; }

    /// <summary>
    /// How long the closure has stood, which is the bound that ends one whose scene has outlived it
    /// (SRV-6). It is a clock of its own beside <see cref="SinceS"/>, because a closure is not a leg: what
    /// bounds a drive is the traffic and what bounds a closure is the town it is holding a lane out of.
    /// </summary>
    public float[] ClosedForS { get; }

    public const int NoBuilding = -1;

    public const int NoBay = -1;

    public const int Nobody = -1;

    /// <summary>Whether this car has a scene to be at, which is what a beat gives way to (SRV-6).</summary>
    public bool IsOnACall(int car) => Casualty[car] != Nobody || Wreck[car] != Nobody;

    /// <summary>
    /// Whether it is carrying the priority (SRV-6): <b>the leg out to a scene and nothing else</b>. A patrol
    /// is ordinary traffic (SRV-5), and what is urgent about a closure is getting the road shut before
    /// anybody else drives into it — never the drive home afterwards.
    /// </summary>
    public bool IsHurrying(int car) => Stage[car] == PatrolStage.Attending;

    /// <summary>The call given up or discharged: everything it held, dropped in one place.</summary>
    public void ClearTheCall(int car)
    {
        Casualty[car] = Nobody;
        Wreck[car] = Nobody;
        ClosedForS[car] = 0f;
    }
}
