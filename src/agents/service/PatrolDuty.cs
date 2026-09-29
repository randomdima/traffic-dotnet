namespace TrafficSimulation.Agents.Service;

/// <summary>
/// <b>What a police car is doing about its beat</b> (SRV-5) and its calls (SRV-6). The life cycle as observable
/// states, on the terms <see cref="Ambulance.RescueStage"/> names a rescue's: there is no state a patrol car can
/// be in that this does not name, and every transition between two of them is one line of
/// <c>TownWorld.Patrol.cs</c>.
/// </summary>
/// <remarks>
/// <b>These are not actions and do not pretend to be.</b> A police car drives the legs every other car
/// drives (AGT-7, CAR-15), with the place it is sent to named as where it is stopped; what is here is the
/// errand those legs are being run for.
/// </remarks>
internal enum PatrolStage : byte
{
    /// <summary>Standing on its station's apron, waiting out the interval before the next beat.</summary>
    Standing,

    /// <summary>Under way to somewhere in the town it was sent, with no priority over anybody.</summary>
    Patrolling,

    /// <summary>Under way to the entrance of a lane it has been called to close, carrying the priority for that leg (SRV-6).</summary>
    Attending,

    /// <summary>Standing in the entrance of the lane it closes, its officer out on the road in front of it (SRV-9, SRV-11).</summary>
    Closing,

    /// <summary>The scene over: its officer walking back to the car, which waits for them before it goes (SRV-11).</summary>
    Reopening,

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
    /// <param name="mostLanesClosed">
    /// How many lanes one closure may hold at most — the room <see cref="RoadClosure.Stretch"/> is walked into.
    /// </param>
    public PatrolDuty(int cars, int mostLanesClosed)
    {
        MostLanesClosed = mostLanesClosed;
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
        ClosedLanes = new int[cars * mostLanesClosed];
        ClosedCount = new int[cars];
        SceneLane = new int[cars];
        Array.Fill(SceneLane, Nobody);
        Officer = new int[cars];
        Array.Fill(Officer, Nobody);
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

    /// <summary>The room one closure's lanes are written into: <see cref="MostLanesClosed"/> a car.</summary>
    public int MostLanesClosed { get; }

    /// <summary>
    /// <b>The lanes this car's closure holds</b> (SRV-9), entrance first (<see cref="RoadClosure.Stretch"/>), found
    /// once when the call is taken — the scene does not move, so neither does the road round it.
    /// </summary>
    public int[] ClosedLanes { get; }

    /// <summary>How many of them there are, and nought for a car closing nothing.</summary>
    public int[] ClosedCount { get; }

    /// <summary>
    /// <b>The lane of the scene this car closes</b>, or <see cref="Nobody"/> — which of a scene's lanes is this
    /// car's, where a scene across the road is closed by two (SRV-9).
    /// </summary>
    public int[] SceneLane { get; }

    /// <summary>
    /// <b>The officer this car carries</b> (SRV-11), or <see cref="Nobody"/> — who stands at the entrance and whose
    /// body is what blocks it (SRV-9). Laid with the car and never another, since the walker roster is not grown.
    /// </summary>
    public int[] Officer { get; }

    public const int NoBuilding = -1;

    public const int NoBay = -1;

    public const int Nobody = -1;

    /// <summary>The lanes one car's closure holds, entrance first.</summary>
    public ReadOnlySpan<int> ClosedLanesOf(int car) =>
        ClosedLanes.AsSpan(car * MostLanesClosed, ClosedCount[car]);

    /// <summary>Room for them, to be written when a call is taken.</summary>
    public Span<int> RoomForTheClosureOf(int car) => ClosedLanes.AsSpan(car * MostLanesClosed, MostLanesClosed);

    /// <summary>The lane whose mouth the car and its officer stand at, or <see cref="Nobody"/>.</summary>
    public int EntranceOf(int car) => ClosedCount[car] > 0 ? ClosedLanes[car * MostLanesClosed] : Nobody;

    /// <summary>Whether this car has a scene to be at, which is what a beat gives way to (SRV-6).</summary>
    public bool IsOnACall(int car) => Casualty[car] != Nobody || Wreck[car] != Nobody;

    /// <summary>
    /// <b>Whether its lanes are closed now</b> (SRV-9, SRV-10): standing at the entrance, and until its officer is
    /// back aboard — the lane is not given back while somebody is still standing in it.
    /// </summary>
    public bool Closes(int car) =>
        ClosedCount[car] > 0 && Stage[car] is PatrolStage.Closing or PatrolStage.Reopening;

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
        ClosedCount[car] = 0;
        SceneLane[car] = Nobody;
    }
}
