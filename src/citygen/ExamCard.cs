namespace TrafficSimulation.CityGen;

/// <summary>
/// Which arm of a junction a car comes from or leaves by, on a map whose every road runs one of the four
/// ways. <b>The lattice's bearings and not the compass's</b>: <see cref="ExamArm.North"/> is the way the
/// rows count up, which is up the screen.
/// </summary>
internal enum ExamArm : byte
{
    North,
    East,
    South,
    West,
}

/// <summary>
/// Where a card's junction stands relative to the cell that holds it. A cell's own node is the ordinary
/// answer; the other two are for the shapes a lattice of crossings cannot itself be
/// (<see cref="ExamPlan"/>).
/// </summary>
internal enum ExamStage : byte
{
    /// <summary>The cell's own node — a crossroads or a T, depending on how many arms it has.</summary>
    Cell,

    /// <summary>The head of the cell's spur, which is a dead end (TER-5a).</summary>
    Head,

    /// <summary>
    /// The cell's own node again, with the paint this card is about <b>struck in the middle of its south
    /// arm</b> rather than on an arm of the junction — a crossing belonging to no junction (TER-6), which
    /// is the one place the traffic has nothing but the body on the paint to go on.
    /// </summary>
    MidBlock,
}

/// <summary>
/// One car a card stages: the arm it stands on, the arm it is sent out by, and how far back from the
/// junction it starts. <b>The movement is the pair of arms and never a name</b> — which of straight, near
/// side and across it is falls out of the two bearings, exactly as the road graph works it out.
/// </summary>
/// <remarks>
/// <see cref="From"/> equal to <see cref="To"/> is coming back the way you came, which no box admits
/// (TER-5f) — so it is a card about `P-19` and is staged at a dead end.
/// </remarks>
internal readonly record struct ExamDriver(ExamArm From, ExamArm To, float StandBackM, float RunOnM);

/// <summary>
/// What a card claims about the car it is written for. <b>One claim a card, and it is about the subject
/// — <see cref="ExamCard.Drivers"/>[0]</b>; that every car staged clears its junction is asked of every
/// card and is not one of these.
/// </summary>
internal enum ExamAsks : byte
{
    /// <summary>Nothing beyond the standing claim: it gets through, however it has to.</summary>
    Clears,

    /// <summary>It never comes to rest short of the box. Its movement takes ground off nothing that is there.</summary>
    Unhindered,

    /// <summary>It is on the box only once the car it gives way to has left it (TER-5e).</summary>
    GivesWay,

    /// <summary>It is on the box after the car in front, having been held by it rather than by the junction.</summary>
    InTurn,

    /// <summary>
    /// It is never on the box while its own approach is showing red. <b>Not "it waits at the bar"</b>,
    /// which is a claim about the timetable: a car that meets a green on the way up has nothing to wait
    /// for, and what the rule actually says is what it may not do.
    /// </summary>
    EntersOnGreen,

    /// <summary>Its own body never crosses the paint faster than a crossing is driven over (CAR-7b).</summary>
    AtCrossingPace,

    /// <summary>It is never on the paint while somebody on foot is (TER-5e, TER-4c.1).</summary>
    StopsForThePaint,

    /// <summary>It ends up driving back the way it came, which at a dead end is `P-19`.</summary>
    TurnsRound,
}

/// <summary>
/// One junction crossing, staged: the shape it is asked of, the cars that meet at it, and the one claim
/// it makes about the first of them.
/// </summary>
/// <remarks>
/// <b>A card is data and the map is derived from it</b> — <see cref="ExamPlan"/> reads the thirty-six and
/// lays whatever they need: the spur that gives a cell's junction its fourth arm, and the lights over the
/// junctions whose cards are about lights. A card that asked for a shape the lattice cannot carry is a card
/// the plan's own tests refuse, not a map that quietly comes out different.
/// </remarks>
/// <param name="Stage">Which junction of the card's own cell it is staged at.</param>
/// <param name="Spur">Whether the cell is given a short road out of the lattice, which is what makes an edge cell a crossroads.</param>
/// <param name="Lit">Whether that junction carries lights (TLT-3).</param>
/// <param name="Watched">
/// The arm whose crossing this card is about — where somebody stands at the kerb. <b>Not where the paint
/// is</b>: every arm of every junction carries a crossing (TER-6), and what a card chooses is which one it
/// puts a body on.
/// </param>
/// <param name="Drivers">The cars it stages, the first of which is the subject its claim is about.</param>
internal readonly record struct ExamCard(
    string Name,
    ExamStage Stage,
    bool Spur,
    bool Lit,
    ExamArm? Watched,
    ExamAsks Asks,
    ExamDriver[] Drivers)
{
    /// <summary>
    /// <b>What this build does instead</b>, on the cards it does not pass — empty on every card that does.
    /// A card carrying one is <em>asserted to still fail</em>, so the day the engine passes it the suite
    /// says so and this line is deleted rather than left standing as a note nobody re-reads.
    /// </summary>
    public string Finding { get; init; } = string.Empty;
}

/// <summary>
/// <b>The driving exam</b>: thirty-six junction crossings, one to a cell of a six by six lattice, in
/// the order a learner meets them — the clear box, the box somebody else is in, the box a light governs
/// and the box somebody is walking over.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every card is one movement through one junction and one claim about it.</b> What varies between two
/// cards is what else is at the junction, which is the whole of what a crossing exam asks: the same turn
/// across is free at an empty box, waits for the oncoming stream at a busy one, and waits for its own
/// green at a lit one. So the movements repeat on purpose and the company does not.
/// </para>
/// <para>
/// <b>The cards are the map's reason to exist and therefore live with the plan</b> rather than with the
/// harness that runs them: what a spur is for, why a junction is lit and where the paint is are decided
/// here, and a rig that staged any of it would be measuring the rig.
/// </para>
/// </remarks>
internal static class ExamCards
{
    /// <summary>How far back a car starts by default: room to pull away, reach a road speed and stop again.</summary>
    const float BackM = 35f;

    /// <summary>And how far back the one that is meant to arrive first starts — behind the paint on its own arm, and no further.</summary>
    const float AheadM = 20f;

    /// <summary>The follower in a pair on one lane, far enough back to be a second car rather than the same one.</summary>
    const float BehindM = 60f;

    /// <summary>
    /// Far enough back to be at a road speed by the time the paint is under the wheels, since a card about
    /// how fast a car crosses a zebra needs a car that had a speed to give up.
    /// </summary>
    const float RunUpM = 45f;

    /// <summary>Behind the paint struck in the middle of the block, so the card's own crossing is on the way to the box.</summary>
    const float PastTheMidBlockM = 80f;

    /// <summary>
    /// A car pointed at a dead end from near enough to it that the head is the only place left to go, and
    /// the place it is then sent back to — far enough down the spur to be a drive back rather than a
    /// reversing manoeuvre, and short of the paint at the junction end of it.
    /// </summary>
    const float ShortOfTheHeadM = 15f;

    const float BackDownTheSpurM = 45f;

    public static ReadOnlySpan<ExamCard> All => Table;

    /// <summary>How many cards there are, which is also the lattice: six cells by six.</summary>
    public const int Count = 36;

    public const int Rows = 6;

    public const int Columns = 6;

    static readonly ExamCard[] Table =
    [
        // The empty box: what each of the movements is when nothing else is there.
        Card("Emerging near side out of a stem", ExamStage.Cell, spur: true, ExamAsks.Unhindered,
            Drives(ExamArm.East, ExamArm.North), Drives(ExamArm.North, ExamArm.South)),
        Card("Straight through a clear crossroads", ExamStage.Cell, spur: true, ExamAsks.Unhindered,
            Drives(ExamArm.West, ExamArm.East)),
        Card("The near-side turn at a clear crossroads", ExamStage.Cell, spur: true, ExamAsks.Unhindered,
            Drives(ExamArm.East, ExamArm.North)),
        Card("The turn across at a clear crossroads", ExamStage.Cell, spur: true, ExamAsks.Clears,
            Drives(ExamArm.East, ExamArm.South)),
        Card("Two near-side turns from opposite arms", ExamStage.Cell, spur: true, ExamAsks.Unhindered,
            Drives(ExamArm.East, ExamArm.North), Drives(ExamArm.West, ExamArm.South)),
        Card("Emerging across the stream out of a stem", ExamStage.Cell, spur: true, ExamAsks.GivesWay,
            Drives(ExamArm.North, ExamArm.East), Drives(ExamArm.East, ExamArm.West)),

        // The box somebody else is coming through, one pairing of movements at a time.
        Card("The near-side turn into a stem", ExamStage.Cell, spur: false, ExamAsks.Unhindered,
            Drives(ExamArm.South, ExamArm.East), Drives(ExamArm.North, ExamArm.South)),
        Card("The turn across gives way to the oncoming straight", ExamStage.Cell, spur: false, ExamAsks.GivesWay,
            Drives(ExamArm.South, ExamArm.West), Drives(ExamArm.North, ExamArm.South)),
        Card("The turn across gives way to the oncoming near-side turn", ExamStage.Cell, spur: false,
            ExamAsks.GivesWay,
            Drives(ExamArm.South, ExamArm.West), Drives(ExamArm.North, ExamArm.West)),
        Found(
            Card("Two opposing turns across each other", ExamStage.Cell, spur: false, ExamAsks.Clears,
                Drives(ExamArm.South, ExamArm.West), Drives(ExamArm.North, ExamArm.East)),
            "Neither goes. Two turns across are the same rank (TER-5e), so each is cut at the other's "
            + "ground, both stop on the box and both stay there until the ladder stands them down."),
        Card("The near-side turn joins the stream it turns into", ExamStage.Cell, spur: false, ExamAsks.Clears,
            Drives(ExamArm.South, ExamArm.East), Drives(ExamArm.West, ExamArm.East)),
        Card("The turn across into a stem", ExamStage.Cell, spur: false, ExamAsks.GivesWay,
            Drives(ExamArm.South, ExamArm.West), Drives(ExamArm.North, ExamArm.South)),
        Card("Straight past a stem somebody is emerging from", ExamStage.Cell, spur: false, ExamAsks.Unhindered,
            Drives(ExamArm.North, ExamArm.South), Drives(ExamArm.East, ExamArm.South)),
        Card("Two straights across one another", ExamStage.Cell, spur: false, ExamAsks.Clears,
            Drives(ExamArm.South, ExamArm.North), Drives(ExamArm.West, ExamArm.East)),
        Card("The car already committed keeps the ground", ExamStage.Cell, spur: false, ExamAsks.GivesWay,
            Drives(ExamArm.West, ExamArm.East), Drives(ExamArm.South, ExamArm.North, AheadM)),
        Card("Following the car in front through the box", ExamStage.Cell, spur: true, ExamAsks.InTurn,
            Drives(ExamArm.South, ExamArm.North, BehindM), Drives(ExamArm.South, ExamArm.North)),
        Card("Queueing behind a car waiting to turn across", ExamStage.Cell, spur: true, ExamAsks.InTurn,
            Drives(ExamArm.South, ExamArm.North, BehindM), Drives(ExamArm.South, ExamArm.West, AheadM),
            Drives(ExamArm.North, ExamArm.South)),
        Card("Emerging across a road running both ways", ExamStage.Cell, spur: false, ExamAsks.GivesWay,
            Drives(ExamArm.West, ExamArm.North), Drives(ExamArm.North, ExamArm.South),
            Drives(ExamArm.South, ExamArm.North)),
        Card("The near-side turn takes its ground off the turn across", ExamStage.Cell, spur: false,
            ExamAsks.Unhindered,
            Drives(ExamArm.East, ExamArm.North), Drives(ExamArm.North, ExamArm.East)),
        Card("Somebody on every arm of an unmarked crossroads", ExamStage.Cell, spur: false, ExamAsks.Clears,
            Drives(ExamArm.South, ExamArm.North), Drives(ExamArm.West, ExamArm.East),
            Drives(ExamArm.North, ExamArm.South), Drives(ExamArm.East, ExamArm.West)),
        Card("The straight keeps the ground a near-side turn merges over", ExamStage.Cell, spur: false,
            ExamAsks.Unhindered,
            Drives(ExamArm.West, ExamArm.East), Drives(ExamArm.South, ExamArm.East)),
        Card("The turn across gives way to the straight it merges in front of", ExamStage.Cell, spur: false,
            ExamAsks.GivesWay,
            Drives(ExamArm.North, ExamArm.East), Drives(ExamArm.West, ExamArm.East)),
        Card("The straight outranks both the near side and the across", ExamStage.Cell, spur: false,
            ExamAsks.Unhindered,
            Drives(ExamArm.South, ExamArm.North), Drives(ExamArm.East, ExamArm.South),
            Drives(ExamArm.West, ExamArm.South)),
        Card("The turn across waits out a pair in the oncoming stream", ExamStage.Cell, spur: false,
            ExamAsks.GivesWay,
            Drives(ExamArm.South, ExamArm.West), Drives(ExamArm.North, ExamArm.South, AheadM),
            Drives(ExamArm.North, ExamArm.South, BehindM)),
        Card("The near-side turn and the turn across meet in the arm they both join", ExamStage.Cell, spur: true,
            ExamAsks.Unhindered,
            Drives(ExamArm.East, ExamArm.North), Drives(ExamArm.West, ExamArm.North)),
        Card("Turning near side behind a car turning across", ExamStage.Cell, spur: false, ExamAsks.InTurn,
            Drives(ExamArm.South, ExamArm.East, BehindM), Drives(ExamArm.South, ExamArm.West, AheadM),
            Drives(ExamArm.North, ExamArm.South)),
        Card("Two turns across from arms beside one another", ExamStage.Cell, spur: false, ExamAsks.Clears,
            Drives(ExamArm.South, ExamArm.West), Drives(ExamArm.West, ExamArm.North)),

        // The box a light governs.
        Lit("Straight through on the green", ExamAsks.Clears, spur: false,
            Drives(ExamArm.West, ExamArm.East)),
        Lit("The cross traffic waits out its own red", ExamAsks.Unhindered, spur: false,
            Drives(ExamArm.West, ExamArm.East), Drives(ExamArm.South, ExamArm.North)),
        Lit("The turn across gives way inside its own green", ExamAsks.GivesWay, spur: true,
            Drives(ExamArm.South, ExamArm.West), Drives(ExamArm.North, ExamArm.South)),
        Lit("Never onto the box on a red", ExamAsks.EntersOnGreen, spur: false,
            Drives(ExamArm.South, ExamArm.North)),

        // The box somebody is walking over.
        MidBlock("Giving way at a mid-block crossing", ExamAsks.StopsForThePaint,
            Drives(ExamArm.South, ExamArm.West, PastTheMidBlockM)),
        Paint("Crossing pace over the paint", ExamArm.South, ExamAsks.AtCrossingPace,
            Drives(ExamArm.South, ExamArm.North, RunUpM)),
        Paint("Stopping short of somebody on the paint", ExamArm.South, ExamAsks.StopsForThePaint,
            Drives(ExamArm.South, ExamArm.North, RunUpM)),
        Found(
            Paint("Turning across a crossing somebody is on", ExamArm.North, ExamAsks.StopsForThePaint,
                Drives(ExamArm.West, ExamArm.North, RunUpM)),
            "It takes the turn and clears the box, then comes back down the arm and is settled beside it "
            + "rather than driven on to where it was ordered. Nothing about the paint is wrong — it is never "
            + "on it, and the walker is gone before it arrives; what it does not do is hold a route it has "
            + "already turned onto. It appeared when the pedal stopped being authored in m/s² (CAR-45): the "
            + "figure the ground reservation projects against fell to what the tyres actually deliver, and "
            + "this movement was the one standing on the difference."),
        Card("Shunting round at a dead end", ExamStage.Head, spur: true, ExamAsks.TurnsRound,
            Drives(ExamArm.South, ExamArm.South, ShortOfTheHeadM, BackDownTheSpurM)),
    ];

    static ExamDriver Drives(ExamArm from, ExamArm to, float standBackM = BackM, float runOnM = ExamLattice.RunOnM) =>
        new(from, to, standBackM, runOnM);

    /// <summary>A card this build does not pass, with what it does instead.</summary>
    static ExamCard Found(ExamCard card, string finding) => card with { Finding = finding };

    static ExamCard Card(
        string name, ExamStage stage, bool spur, ExamAsks asks, params ExamDriver[] drivers) =>
        new(name, stage, spur, Lit: false, Watched: null, asks, drivers);

    /// <summary>A card at a lit junction. A light is about nothing under three arms (TLT-3), so every one of these is a crossroads.</summary>
    static ExamCard Lit(string name, ExamAsks asks, bool spur, params ExamDriver[] drivers) =>
        new(name, ExamStage.Cell, spur, Lit: true, Watched: null, asks, drivers);

    /// <summary>A card with paint on one of its arms, and somebody standing at the kerb of it.</summary>
    static ExamCard Paint(string name, ExamArm watched, ExamAsks asks, params ExamDriver[] drivers) =>
        new(name, ExamStage.Cell, Spur: true, Lit: false, watched, asks, drivers);

    /// <summary>
    /// A card whose paint is struck in the middle of its cell's south arm, so the crossing is met on the
    /// open road rather than on the approach to a box.
    /// </summary>
    static ExamCard MidBlock(string name, ExamAsks asks, params ExamDriver[] drivers) =>
        new(name, ExamStage.MidBlock, Spur: false, Lit: false, Watched: null, asks, drivers);
}
