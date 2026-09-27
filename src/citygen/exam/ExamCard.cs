namespace TrafficSimulation.CityGen.Exam;

/// <summary>
/// Which arm of a junction something comes from or leaves by, on a map whose every road runs one of the
/// four ways. <b>The lattice's bearings and not the compass's</b>: <see cref="North"/> is the way the rows
/// count up, which is up the screen.
/// </summary>
internal enum ExamArm : byte
{
    North,
    East,
    South,
    West,
}

/// <summary>
/// <b>What shape of junction a card is asked at</b>, in the card's own frame. A card is written once, in
/// that frame, and turned onto whichever cell of the lattice has the shape (<see cref="ExamGround"/>), so the
/// frame fixes which arm is missing and nothing else.
/// </summary>
internal enum ExamShape : byte
{
    /// <summary>Four arms, one on every bearing.</summary>
    Crossroads,

    /// <summary>Three arms, and the one missing is <see cref="ExamArm.North"/>: the stem is the south arm.</summary>
    Tee,

    /// <summary>The head of a dead end (TER-5a), whose one arm is <see cref="ExamArm.South"/>.</summary>
    DeadEnd,

    /// <summary>Four arms, each meeting a junction of its own on a ring driven one way round (GEN-19).</summary>
    Roundabout,
}

/// <summary>What a card is about, which is how the report groups it — whatever shape of junction it is asked at.</summary>
internal enum ExamFamily : byte
{
    /// <summary>One car and nothing else: every movement every shape offers, driven through without a stop.</summary>
    Alone,

    /// <summary>Cars whose ways share no ground — passing each other, turning on opposite corners — and none waits.</summary>
    Apart,

    /// <summary>Cars whose ways cross: the right of way decides who waits (TER-5e), and everybody gets through.</summary>
    Across,

    Queues,
    Signals,
    Walkers,
    Emergency,
    OneWay,
}

/// <summary>When a staged car is sent: at once, or on the tick its own approach turns red or green.</summary>
/// <remarks>
/// <b>A card about a light is a card about one moment of its cycle</b>, and which axis of a lit junction is
/// showing what is the town's to say (TLT-3, <c>SignalService</c>) rather than the plan's — so the car is
/// held where it stands until its own approach shows the colour the card is about, and sent then.
/// </remarks>
internal enum ExamStart : byte
{
    AtOnce,
    OnRed,
    OnGreen,
}

/// <summary>Which side of an arm something stands on, read off the bearing that runs out of the junction along it.</summary>
internal enum ExamSide : byte
{
    Left,
    Right,
}

/// <summary>
/// One car a card stages: the arm it stands on, the arm it is sent out by, how far back from the box it
/// starts and how far past it it is sent. <b>The movement is the pair of arms and never a name</b> — which of
/// straight, near side and across it is falls out of the two bearings, exactly as the road graph works it
/// out.
/// </summary>
/// <param name="DelayS">How long after its start condition is met the order is given.</param>
/// <param name="Emergency">A car on a call (AMB-4): the call's rung on its plan and no red to keep.</param>
/// <param name="Parked">
/// A car stood on its own lane and held there for the whole card — the obstruction a card is about, never
/// sent anywhere. <see cref="To"/> means nothing for it.
/// </param>
/// <param name="Outbound">
/// <b>A car standing in the lane leaving the junction</b> on <see cref="From"/> rather than the one arriving
/// at it, and sent on down that same lane to <see cref="RunOnM"/> — the queue on the far side of a box a
/// card is about. It never crosses the box, so nothing is asked of it but that it gets there.
/// </param>
internal readonly record struct ExamDriver(
    ExamArm From,
    ExamArm To,
    float StandBackM,
    float RunOnM,
    ExamStart Start,
    float DelayS,
    bool Emergency,
    bool Parked,
    bool Outbound = false);

/// <summary>
/// One body on foot a card stages: at the kerb of the zebra on one arm, on one side of it, sent over to the
/// other side — a while after the card begins, or a while after one of the card's cars has come within a
/// distance of the paint. <b>Or, where it is sent <see cref="RoundTo"/> another arm, along the pavement</b>
/// round the corner between the two, crossing nothing.
/// </summary>
/// <param name="Crossing">The arm whose zebra it crosses, or whose pavement it sets off along.</param>
/// <param name="DelayS">
/// How long it waits: from the start of the card, or — where it waits for a car — from the moment that car
/// came within <see cref="WithinM"/>, which is how a group steps out one after another.
/// </param>
/// <param name="WaitsFor">The driver whose approach sends it, or <see cref="ExamClaim.Nobody"/> to go on its delay alone.</param>
/// <param name="PauseS">
/// How long it stands in the middle of the lane it steps into before going on over — nought to cross in one.
/// </param>
/// <param name="WithinM">
/// How near that driver's nose comes to the middle of the paint before it steps out — or, for a walker on the
/// pavement, to the place it sets off from.
/// </param>
/// <param name="RoundTo">
/// The arm whose pavement it walks to round the corner it shares with <see cref="Crossing"/> — or nothing, for
/// a walker sent over the zebra. <see cref="From"/> means nothing for it.
/// </param>
/// <param name="OutM">How far out along each of the two arms a walk round a corner sets off and ends.</param>
internal readonly record struct ExamWalker(
    ExamArm Crossing, ExamSide From, float DelayS, int WaitsFor = ExamClaim.Nobody, float WithinM = 0f,
    float PauseS = 0f, ExamArm? RoundTo = null, float OutM = 0f)
{
    /// <summary>Whether it walks the pavement rather than the paint.</summary>
    public bool Strolls => RoundTo is not null;
}

/// <summary>What a claim asks of the car or cars it names.</summary>
internal enum ExamRule : byte
{
    /// <summary>
    /// <b>The subject gives way to the other</b>, which is never brought to rest short of the ground their two
    /// paths share (TER-5e): a right of way orders who waits, so which of them is over that ground first is
    /// the engine's to settle, and only the one with the right of way made to wait is wrong.
    /// </summary>
    YieldsTo,

    /// <summary>The subject is on the box before the other is.</summary>
    Before,

    /// <summary>The subject is never brought to rest on its way up to the box, but by a red.</summary>
    Unhindered,

    /// <summary>The subject never passes its own stop bar while its approach is showing red.</summary>
    NeverOnRed,

    /// <summary>The subject is brought to rest short of the box at least once, and never passes its bar on a red.</summary>
    Waits,

    /// <summary>
    /// <b>The subject gives way to somebody on foot</b> (the other is a walker): it is never on the paint while
    /// they are, and they are on it before it is.
    /// </summary>
    ForWalker,

    /// <summary>The subject never comes to rest on a zebra.</summary>
    NotOnThePaint,
}

/// <summary>
/// One claim of a card: a rule, the driver it is about, and — for a rule between two — the other driver, or
/// the walker for <see cref="ExamRule.ForWalker"/>.
/// </summary>
internal readonly record struct ExamClaim(ExamRule Rule, int Subject, int Other = ExamClaim.Nobody)
{
    public const int Nobody = -1;
}

/// <summary>
/// <b>One scenario of the map</b>: the junction it is asked at, who is there, and what the engine is to make
/// of it — in a sentence, with the engine's own rules it exercises.
/// </summary>
/// <remarks>
/// <para>
/// <b>A card is data and the map is derived from it</b> (<see cref="ExamPlan"/>): the cell a card is given,
/// the lights over its junction and the one-way arms it asks for are all read off the table, so a card that
/// asked for a shape the lattice cannot carry fails when the map is laid rather than coming out different.
/// </para>
/// <para>
/// <b>What every card owes besides its own claims</b> is asked of all of them alike: every car sent gets
/// where it was sent through the box it was staged at, everybody on foot gets where they were sent, and
/// nothing the card stood touches anything.
/// </para>
/// </remarks>
/// <param name="Expects">What the town is to do with it, in a sentence.</param>
/// <param name="Rules">The engine's own rules it exercises, by ID.</param>
internal sealed record ExamCard(string Name, ExamFamily Family, ExamShape Shape, string Expects, string Rules)
{
    /// <summary>Whether the junction carries lights (TLT-3). A light is about nothing under three arms.</summary>
    public bool Lit { get; init; }

    /// <summary>The arms, in the card's frame, whose road runs one way <em>into</em> the junction.</summary>
    public ExamArm[] OneWayIn { get; init; } = [];

    /// <summary>And the ones whose road runs one way <em>out of</em> it.</summary>
    public ExamArm[] OneWayOut { get; init; } = [];

    public ExamDriver[] Drivers { get; init; } = [];

    public ExamWalker[] Walkers { get; init; } = [];

    public ExamClaim[] Claims { get; init; } = [];
}
