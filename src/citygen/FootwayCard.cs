namespace TrafficSimulation.CityGen;

/// <summary>Which of the two kinds of ground a staged body stands on: the pavement, or the paint over a carriageway.</summary>
internal enum WalkGround : byte
{
    /// <summary>The pavement beside an arm, on the walking network's own line down it.</summary>
    Pavement,

    /// <summary>
    /// The paint itself, in the middle of the lane on the named side. <b>The one place a body may
    /// lawfully stand in a carriageway</b> (PER-15) and therefore the one thing another walker has to get
    /// past there.
    /// </summary>
    Paint,
}

/// <summary>
/// A place on one junction's ground, as a card names it: which arm, which side of it, and how far out
/// from the junction's own middle. <b>Nothing here is a distance the map was measured with</b> — the
/// pavement's line, the paint's setback and the kerb are the lattice's arithmetic
/// (<see cref="ExamGround.BesideTheArmM"/>), and a card only says where along them somebody stands.
/// </summary>
/// <remarks>
/// <see cref="OutM"/> of <see cref="FootwayCards.AtThePaint"/> is the kerb of that arm's own crossing,
/// which is where a card about crossing stands its bodies. Zero is the middle of the junction and is not a
/// place on the pavement, so it is free to mean the one place on an arm every card has to be able to name.
/// </remarks>
internal readonly record struct WalkStand(ExamArm Arm, ExamSide Side, float OutM, WalkGround Ground);

/// <summary>
/// One body a card stages: where it is put down, and where it is sent. <b>Sent to where it stands is a
/// body standing still</b> — a walker nobody is telling anything wanders the whole lattice
/// (<c>TownWorld.WanderInstead</c>), so every body on this map is under an order and the ones that are
/// meant to be furniture are ordered to stay where they are.
/// </summary>
internal readonly record struct WalkWalker(WalkStand From, WalkStand To);

/// <summary>
/// What a card claims about the body it is written for. <b>One claim a card, and it is about the subject
/// — <see cref="FootwayCard.Walkers"/>[0]</b>; that every body staged got where it was sent, without
/// giving up and without setting foot on a carriageway off the paint, is asked of every card and is not
/// one of these.
/// </summary>
internal enum WalkAsks : byte
{
    /// <summary>Nothing beyond the standing claims: it gets there, however it has to.</summary>
    Arrives,

    /// <summary>
    /// It is never held on the way — not by the claims of another body (PER-13) and not at a kerb
    /// (PER-15). <b>A card that asks this stages no traffic and no lights</b>, so anything that stopped it
    /// is the engine and not the town.
    /// </summary>
    Unhindered,

    /// <summary>
    /// It gets across a carriageway, and does it on the paint. <b>The claim is the crossing and not the
    /// arrival</b>: the standing claim already has it arriving, and a walk that got there without ever
    /// standing on a crossing went somewhere else.
    /// </summary>
    TakesThePaint,

    /// <summary>
    /// It never steps onto the paint while its own crossing is showing red (PER-7.3, TLT-2a). <b>Not "it
    /// waits at the kerb"</b>, which is a claim about the timetable: a body that meets a green has nothing
    /// to wait for, and what the rule says is what it may not do.
    /// </summary>
    EntersOnGreen,

    /// <summary>It gets past the body standing in its way rather than waiting behind it (PER-24).</summary>
    StepsRound,

    /// <summary>
    /// It follows the body under way in front of it and never steps round one (PER-24). The pair is a
    /// queue on one lane of the pavement, which is the one thing a step round is never taken past.
    /// </summary>
    Follows,
}

/// <summary>
/// One walk, staged: the shape of junction it is asked of, the bodies that meet on it, and the one claim
/// it makes about the first of them.
/// </summary>
/// <remarks>
/// <b>A card is data and the map is derived from it</b> — <see cref="FootwayPlan"/> reads the twenty and
/// lays whatever they need: the spur that gives a cell's junction its fourth arm, the lights over the
/// junctions whose cards are about lights, and the paint struck in the middle of a block. A card that
/// asked for a shape the lattice cannot carry is a card the plan's own tests refuse, not a map that
/// quietly comes out different.
/// </remarks>
/// <param name="Stage">Which junction of the card's own cell it is staged at.</param>
/// <param name="Spur">Whether the cell is given a short road out of the lattice, which is what makes an edge cell a crossroads.</param>
/// <param name="Lit">Whether that junction carries lights (TLT-3), which is what gives its crossings a signal to obey.</param>
/// <param name="Walkers">The bodies it stands up, the first of which is the subject its claim is about.</param>
internal readonly record struct FootwayCard(
    string Name,
    ExamStage Stage,
    bool Spur,
    bool Lit,
    WalkAsks Asks,
    WalkWalker[] Walkers)
{
    /// <summary>
    /// <b>What this build does instead</b>, on the cards it does not pass — empty on every card that does.
    /// A card carrying one is <em>asserted to still fail</em>, so the day the engine passes it the suite
    /// says so and this line is deleted rather than left standing as a note nobody re-reads.
    /// </summary>
    public string Finding { get; init; } = string.Empty;
}

/// <summary>
/// <b>The walking exam</b>: twenty walks, one to a cell of the lattice, in the order somebody on foot
/// meets them — the pavement, the corner, the paint, the light, and the body in the way.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing on this map drives.</b> What a car owes a crossing is the driving exam's question
/// (<see cref="ExamCards"/>) and it is asked there with the traffic staged; what is asked here is whether
/// somebody on foot can get about a town at all — round a corner, over a junction, past somebody standing
/// about — and a card that failed with a car on the map would leave the reader unable to say which of the
/// two agents was wrong.
/// </para>
/// <para>
/// <b>Every card is one walk between two places and one claim about it.</b> What varies between two cards
/// is the shape of ground between the ends: the same walk across a street is one crossing at a T, two at
/// the far corner of a crossroads, a wait at a lit one, and a walk round the head where there is no paint
/// at all. So the walks repeat on purpose and the ground does not.
/// </para>
/// <para>
/// <b>Which cell a card is written for is part of the card.</b> A cell in the middle of the lattice is a
/// crossroads and one on the edge is a T (<see cref="ExamGround"/>), so a card about a T is written at an
/// edge cell and a card that wants the fourth arm asks for the spur — exactly as the driving exam does.
/// </para>
/// </remarks>
internal static class FootwayCards
{
    /// <summary>The kerb of the crossing on an arm, which is where every card about crossing stands its bodies.</summary>
    public const float AtThePaint = 0f;

    /// <summary>A stand clear of the junction's own paint, which is where a walk down a pavement starts.</summary>
    const float NearM = 25f;

    /// <summary>Where one is sent: the far end of the same block, short of the next junction's paint.</summary>
    const float FarM = 90f;

    /// <summary>Halfway between the two, which is where a body standing about is in the way of the walk.</summary>
    const float MidwayM = 55f;

    /// <summary>And where the body being followed starts: far enough ahead to be a body in front rather than the same one.</summary>
    const float AheadM = 45f;

    /// <summary>The paint struck in the middle of a block, which is half a block from either junction.</summary>
    const float HalfABlockM = ExamGround.BlockM * 0.5f;

    /// <summary>The far end of the long walk: two junctions up the same street, which is three sets of paint on the way.</summary>
    const float TwoBlocksOnM = NearM + (2f * ExamGround.BlockM);

    /// <summary>Near enough to a dead end's head that walking round it beats going back for the junction's paint.</summary>
    const float ShortOfTheHeadM = 20f;

    public static ReadOnlySpan<FootwayCard> All => Table;

    /// <summary>How many cards there are, which is also the lattice: five rows of four cells.</summary>
    public const int Count = 20;

    public const int Rows = 5;

    public const int Columns = 4;

    static readonly FootwayCard[] Table =
    [
        // The pavement itself, where nothing is in the way and no road is crossed.
        Card("Along a pavement with nothing on it", WalkAsks.Unhindered,
            Walks(Along(ExamArm.East, ExamSide.Left, NearM), Along(ExamArm.East, ExamSide.Left, FarM))),
        Card("Round the corner at a T", WalkAsks.Unhindered,
            Walks(Along(ExamArm.East, ExamSide.Left, NearM), Along(ExamArm.North, ExamSide.Right, NearM))),

        // The paint: one arm, then the shapes that take more than one.
        Crossroads("Straight over the paint on one arm", WalkAsks.TakesThePaint,
            Walks(Kerb(ExamArm.North, ExamSide.Right), Kerb(ExamArm.North, ExamSide.Left))),
        Card("Four blocks up one street", WalkAsks.TakesThePaint,
            Walks(
                Along(ExamArm.North, ExamSide.Right, NearM),
                Along(ExamArm.North, ExamSide.Right, TwoBlocksOnM))),
        Crossroads("The opposite corner of a crossroads", WalkAsks.TakesThePaint,
            Walks(Kerb(ExamArm.North, ExamSide.Left), Kerb(ExamArm.South, ExamSide.Left))),
        Card("Along one street, past a crossroads", WalkAsks.TakesThePaint,
            Walks(
                Along(ExamArm.South, ExamSide.Right, NearM),
                Along(ExamArm.North, ExamSide.Left, NearM))),

        // Somebody else on the same pavement.
        Card("Round somebody standing on the pavement", WalkAsks.StepsRound,
            Walks(Along(ExamArm.West, ExamSide.Right, NearM), Along(ExamArm.West, ExamSide.Right, FarM)),
            Stands(Along(ExamArm.West, ExamSide.Right, MidwayM))),
        Card("Over the stem of a T", WalkAsks.TakesThePaint,
            Walks(Kerb(ExamArm.West, ExamSide.Right), Kerb(ExamArm.West, ExamSide.Left))),

        // The paint a light governs.
        Lit("Over a lit crossing", WalkAsks.EntersOnGreen,
            Walks(Kerb(ExamArm.East, ExamSide.Right), Kerb(ExamArm.East, ExamSide.Left))),
        Lit("Over a lit crossing on the other phase", WalkAsks.EntersOnGreen,
            Walks(Kerb(ExamArm.North, ExamSide.Right), Kerb(ExamArm.North, ExamSide.Left))),

        // The paint that belongs to no junction, and the head that carries none at all.
        MidBlock("Over a crossing in the middle of a block", WalkAsks.TakesThePaint,
            Walks(
                Along(ExamArm.South, ExamSide.Right, HalfABlockM),
                Along(ExamArm.South, ExamSide.Left, HalfABlockM))),
        Head("Round the head of a dead end", WalkAsks.Unhindered,
            Walks(
                Along(ExamArm.West, ExamSide.Right, ShortOfTheHeadM),
                Along(ExamArm.West, ExamSide.Left, ShortOfTheHeadM))),

        // Two bodies on one pavement, the two ways there are to meet one.
        Card("Passing somebody coming the other way", WalkAsks.Unhindered,
            Walks(Along(ExamArm.East, ExamSide.Right, NearM), Along(ExamArm.East, ExamSide.Right, FarM)),
            Walks(Along(ExamArm.East, ExamSide.Right, FarM), Along(ExamArm.East, ExamSide.Right, NearM))),
        Card("Following somebody down the same pavement", WalkAsks.Follows,
            Walks(Along(ExamArm.East, ExamSide.Right, NearM), Along(ExamArm.East, ExamSide.Right, FarM)),
            Walks(Along(ExamArm.East, ExamSide.Right, AheadM), Along(ExamArm.East, ExamSide.Right, FarM))),

        // And the two together: a body in the way, standing where a walk has to go.
        Crossroads("Past somebody standing on the paint", WalkAsks.TakesThePaint,
            Walks(Kerb(ExamArm.South, ExamSide.Right), Kerb(ExamArm.South, ExamSide.Left)),
            Stands(OnThePaint(ExamArm.South, ExamSide.Right))),
        Card("Over the paint and away down the next street", WalkAsks.TakesThePaint,
            Walks(Kerb(ExamArm.South, ExamSide.Left), Along(ExamArm.West, ExamSide.Left, MidwayM))),

        // More than one body on the move at one junction, which is what a town's pavements are made of.
        // <b>Nobody is sent to a kerb another body is walking through</b>: a stretch of the walking network
        // carries no width, so a body standing at one is the card about standing in the way (card 6) told
        // over again rather than a question about a crowd.
        Card("Four bodies over one junction at once", WalkAsks.Arrives,
            Walks(Kerb(ExamArm.South, ExamSide.Right), Along(ExamArm.South, ExamSide.Left, NearM)),
            Walks(Kerb(ExamArm.South, ExamSide.Left), Along(ExamArm.South, ExamSide.Right, NearM)),
            Walks(Kerb(ExamArm.East, ExamSide.Right), Along(ExamArm.East, ExamSide.Left, NearM)),
            Walks(Kerb(ExamArm.East, ExamSide.Left), Along(ExamArm.East, ExamSide.Right, NearM))),
        Lit("The far corner of a lit crossroads", WalkAsks.EntersOnGreen,
            Walks(Kerb(ExamArm.West, ExamSide.Right), Kerb(ExamArm.South, ExamSide.Left))),
        Crossroads("Round one corner, both ways at once", WalkAsks.Arrives,
            Walks(
                Along(ExamArm.South, ExamSide.Right, NearM),
                Along(ExamArm.West, ExamSide.Left, NearM)),
            Walks(
                Along(ExamArm.West, ExamSide.Left, NearM),
                Along(ExamArm.South, ExamSide.Right, NearM))),
        Card("Off a carriageway a body was left standing in", WalkAsks.Arrives,
            Walks(
                OnThePaint(ExamArm.South, ExamSide.Right),
                Along(ExamArm.South, ExamSide.Left, NearM))),
    ];

    /// <summary>A card this build does not pass, with what it does instead.</summary>
    static FootwayCard Found(FootwayCard card, string finding) => card with { Finding = finding };

    static WalkStand Along(ExamArm arm, ExamSide side, float outM) =>
        new(arm, side, outM, WalkGround.Pavement);

    /// <summary>The pavement at the kerb of that arm's own crossing, which is where a body about to cross stands.</summary>
    static WalkStand Kerb(ExamArm arm, ExamSide side) => new(arm, side, AtThePaint, WalkGround.Pavement);

    /// <summary>And the paint itself, in the lane on that side of it.</summary>
    static WalkStand OnThePaint(ExamArm arm, ExamSide side) => new(arm, side, AtThePaint, WalkGround.Paint);

    static WalkWalker Walks(WalkStand from, WalkStand to) => new(from, to);

    /// <summary>A body that is furniture: put down somewhere and ordered to stay there.</summary>
    static WalkWalker Stands(WalkStand at) => new(at, at);

    static FootwayCard Card(string name, WalkAsks asks, params WalkWalker[] walkers) =>
        new(name, ExamStage.Cell, Spur: false, Lit: false, asks, walkers);

    /// <summary>A card whose cell is given the arm that makes it a crossroads, wherever in the lattice it stands.</summary>
    static FootwayCard Crossroads(string name, WalkAsks asks, params WalkWalker[] walkers) =>
        new(name, ExamStage.Cell, Spur: true, Lit: false, asks, walkers);

    /// <summary>A card at a lit junction. A light is about nothing under three arms (TLT-3), so every one of these is a crossroads.</summary>
    static FootwayCard Lit(string name, WalkAsks asks, params WalkWalker[] walkers) =>
        new(name, ExamStage.Cell, Spur: true, Lit: true, asks, walkers);

    /// <summary>A card whose paint is struck in the middle of its cell's south arm, away from any junction.</summary>
    static FootwayCard MidBlock(string name, WalkAsks asks, params WalkWalker[] walkers) =>
        new(name, ExamStage.MidBlock, Spur: false, Lit: false, asks, walkers);

    /// <summary>A card staged at the head of its cell's spur, which is a dead end and carries no paint (TER-5a).</summary>
    static FootwayCard Head(string name, WalkAsks asks, params WalkWalker[] walkers) =>
        new(name, ExamStage.Head, Spur: true, Lit: false, asks, walkers);
}
