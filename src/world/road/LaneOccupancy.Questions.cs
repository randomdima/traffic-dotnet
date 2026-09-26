namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>Which claims an answer may count</b> — the scope of the question being asked, and <b>never a property
/// of the claim</b>. Every one of these is worked out from the claim's own edges, occupant and priority
/// (<see cref="LaneOccupancy.Counts"/>), so a question names what it is about rather than a claim carrying
/// a second tag that can disagree with the first.
/// </summary>
internal enum ClaimsAsked : byte
{
    /// <summary>Everything anybody is standing on or has been granted — what a grant is cut by.</summary>
    Held,

    /// <summary>And the road its holders have <em>stated</em> they mean to use beyond that (TER-5g).</summary>
    HeldOrStated,

    /// <summary>Only what a holder has stated it means to use, which is nothing anybody has been granted.</summary>
    Stated,

    /// <summary>Where a body actually is, of whichever roster — read to the body edge and not to the far one.</summary>
    Bodies,

    /// <summary>
    /// A body driving or walking down <em>this</em> way, and the road it has taken. <b>The one claim a
    /// holder lays from the line it is following</b>, so it is the one its own grant cuts back — a body it
    /// is merely lying across another way with is a second claim and is not this one.
    /// </summary>
    UnderWay,

    /// <summary>
    /// Wheeled bodies. <b>What is asked about by whoever wants to know what is <em>coming</em></b> — a
    /// walker at a kerb, a car about to back out of a bay — and a person on the carriageway is not an answer
    /// to that question, nor is a bollard that has stood there since the town was laid.
    /// </summary>
    Traffic,

    /// <summary>Wheeled bodies, and the ground anybody has been granted: the all-or-nothing <em>is this piece of the road anybody's</em>.</summary>
    TrafficHeld,

    /// <summary>Ground granted and not reached, which is ground about to stop being empty.</summary>
    Granted,

    /// <summary>A body lying where it is rather than driving down this way: a wreck, a body shoved off its line, one under a hand.</summary>
    Loose,

    /// <summary>Anybody on foot, which is the one question the road's claims are asked about walkers.</summary>
    OnFoot,
}

/// <summary>
/// <b>The questions the claims are asked</b>: what is in front, what is behind, how much of a named piece of
/// ground is already somebody's — every one of them a walk of one way's claims in the order they lie,
/// and none of them a decision (SIM-7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Which claims an answer is allowed to count is a scope and not a second loop</b>
/// (<see cref="ClaimsAsked"/>): a walker at a kerb asks about traffic and a driver's grant is cut by
/// everything held — questions of one set of claims, each naming what it is about in one place rather than
/// missing it in one of them.
/// </para>
/// <para>
/// <b>And how strong a claim counts as is the same question read off the rung</b> (TER-5e): whoever is asking
/// after a rescue or after a body on the paint is asking one walk with a floor under it, not a walk of its
/// own that filters what came back. <b>The grant itself is one of these questions</b>
/// (<see cref="GrantedOn"/>) and not a loop each asker writes for itself, in the asker's own figures
/// (<see cref="LaneCredit"/>).
/// </para>
/// </remarks>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// <b>The nearest body in front</b>: the claim with the least near edge that still reaches past
    /// <paramref name="fromM"/> and begins before <paramref name="untilM"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A claim the asking body is already overlapping answers at its own near edge rather than being
    /// skipped — a car inside somebody else is a contact and not a gap, and reading it as an empty road is
    /// the one wrong answer here.
    /// </para>
    /// <para>
    /// <b>Reaching past means the body does</b> (<see cref="LaneClaim.StandsToM"/>) and not the road it has
    /// taken: a driver behind this one has a claim running through it, and that is a car behind and not a
    /// body in front.
    /// </para>
    /// </remarks>
    public bool AheadBody(
        int way, float fromM, float untilM, int excluding, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        Nearest(way, fromM, untilM, excluding, excludingOf, ClaimsAsked.Bodies, out found);

    /// <summary>The nearest granted claim in front, which is ground about to stop being empty.</summary>
    public bool AheadClaim(
        int way, float fromM, float untilM, int excluding, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        Nearest(way, fromM, untilM, excluding, excludingOf, ClaimsAsked.Granted, out found);

    /// <summary>
    /// <b>Every body in front lying where it is rather than driving down this way</b>, near edge first: a
    /// wreck, somebody knocked down, a walker shoved off its own line, a car that has mounted a kerb.
    /// <b>What to do about each is the asker's</b>; all this hands back is which body it was.
    /// </summary>
    /// <remarks>
    /// <b>Walked and not answered with the nearest one</b>, because the two things an asker does with these
    /// are not decided by the same body: every one of them cuts the grant (TER-4c.3), and which one a
    /// manoeuvre is about is that manoeuvre's own question.
    /// </remarks>
    public bool NextLying(
        int way, float fromM, float untilM, int excluding, ref int at, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving, float acrossM = 0f) =>
        NextHeld(way, fromM, untilM, excluding, ref at, out found, excludingOf, ClaimsAsked.Loose, acrossM);

    /// <summary>
    /// <b>Whether any traffic has this stretch of the way</b>: a road a driver has taken, ground somebody
    /// has been granted, a body with no road to its name. The all-or-nothing question — <em>is this piece
    /// of ground anybody's</em> — as against <see cref="NextHeld"/>'s "how much of it is mine".
    /// </summary>
    /// <remarks>
    /// <b>Traffic and not everything held</b>: the asker is somebody about to step into the road, and
    /// neither another person already standing in it nor a bollard that has stood there since the town was
    /// laid is a reason to stay on the pavement. What it waits for is what is coming.
    /// </remarks>
    public bool AnyTrafficOver(int way, float fromM, float toM) =>
        AnythingOver(way, fromM, toM, Nobody, LaneRoster.Driving, ClaimsAsked.TrafficHeld, out _);

    /// <summary>
    /// <b>Whether an ambulance answering a call is coming through this stretch</b> (AMB-4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the rung and not the vehicle.</b> Whoever holds ground at a call's own rung
    /// (<see cref="ClaimPriority.Special"/>) is the thing being got out of the way of; an ambulance driving
    /// home holds its road at the rung of the movement it is making like anybody else.
    /// </para>
    /// <para>
    /// <b>And it is the ones actually coming through</b> (<paramref name="comingThroughMps"/>), because the
    /// claim of a rescue that has stopped is not a rescue to be got out of the way of — it is a stopped
    /// car, which is what anybody taking the ground would have made of it in any case. <b>Stopped is a pace
    /// and never zero</b>: a car held in a queue creeps at fractions of a millimetre a second, and read
    /// against zero that is a rescue coming through for as long as it sits there. Every claim over the
    /// ground is walked rather than the first one taken, so a rescue standing on a piece of road cannot
    /// hide one that is coming through it.
    /// </para>
    /// </remarks>
    /// <param name="comingThroughMps">How fast a rescue has to be going to be worth standing aside for.</param>
    public bool AnyRescueOver(int way, float fromM, float toM, float comingThroughMps)
    {
        var at = FromTheStart;
        while (NextOver(
                   way, fromM, toM, Nobody, LaneRoster.Driving, ClaimsAsked.TrafficHeld,
                   ClaimPriority.Rejected, ref at, out var rescue))
        {
            // <b>The call's own two rungs and not everything above them</b>: a rescue's body is a body like
            // any other (<see cref="ClaimPriority.Hard"/>), so a band read as "this rung or stronger" is
            // every car in the town. What says a rescue is coming is the road it has taken or stated.
            if (rescue.Priority is not (ClaimPriority.Special or ClaimPriority.SoftSpecial)) continue;
            if (rescue.AlongMps >= comingThroughMps) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>Whether a wheeled body is standing over this stretch</b> — going nowhere on it rather than coming
    /// through it. <b>The one thing on a road no rung takes</b>: a driver's road is handed back by driving
    /// on, and a body is not handed back at all (TER-5e).
    /// </summary>
    /// <remarks>
    /// <b>The bar is a pace and never zero</b>, exactly as <see cref="AnyRescueOver"/>'s is and for the same
    /// reason: a car held in a queue creeps at fractions of a millimetre a second, and read against zero that
    /// is a car coming through for as long as it sits there. Every claim over the ground is walked rather
    /// than the first one taken, so a car standing on a piece of road cannot hide one moving over it.
    /// </remarks>
    /// <param name="comingThroughMps">How fast a body has to be going to be worth waiting for rather than standing at.</param>
    public bool AnyTrafficStandingOver(
        int way, float fromM, float toM, float comingThroughMps, out LaneClaim found)
    {
        var at = FromTheStart;
        while (NextOver(
                   way, fromM, toM, Nobody, LaneRoster.Driving, ClaimsAsked.Traffic,
                   ClaimPriority.Rejected, ref at, out found))
        {
            if (MathF.Abs(found.AlongMps) < comingThroughMps) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>Whether this stretch of ground is somebody else's</b>, whoever they are and whatever they are
    /// doing on it — what a manoeuvre asks of every place its own geometry would put a body, since a
    /// template runs over ground no way owns and the ways under it are all there is to ask; and what a
    /// driver asks of the metres another way of a junction crosses its own (TER-5c.1).
    /// </summary>
    /// <remarks>
    /// The first answer of <see cref="NextHeldOver"/> and not a second copy of its loop, so <em>is any
    /// of this anybody's</em> and <em>which of them are</em> cannot come apart.
    /// </remarks>
    public bool SpokenForByAnother(
        int way, float fromM, float toM, int excluding, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        AnythingOver(way, fromM, toM, excluding, excludingOf, ClaimsAsked.Held, out found);

    /// <summary>
    /// <b>Whether anybody's body is standing over this stretch of ground</b>, whoever they are and whichever
    /// roster they are in — what a body with <em>no</em> way to be cut on asks of the ground it would step
    /// into (TER-4c.3), the ways under that ground being all there is to ask.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Bodies and not everything held</b> (<see cref="ClaimsAsked.Bodies"/>, and the difference from
    /// <see cref="SpokenForByAnother"/>). A body over the shared metres beats one merely reaching across them
    /// (TER-4c.3), so a step onto ground somebody has been granted takes that ground rather than sharing it.
    /// Asked of everything held instead, every walker in a crowd would hold the metres in front of it and
    /// nobody in it could take a step.
    /// </para>
    /// <para>
    /// <b>And read the way a body on the way reads it</b> (<see cref="Nearest"/>, TER-4c.2): to the body edge
    /// rather than the far one, and past whatever stands aside of the line that travels here. Asked without
    /// that, a body with no line would be held off ground a body <em>with</em> one is driven straight past,
    /// which is the same piece of the town answering two ways depending on who is asking.
    /// </para>
    /// </remarks>
    public bool AnybodyStandingOver(
        int way, float fromM, float toM, int excluding, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving, float acrossM = 0f) =>
        Nearest(way, fromM, toM, excluding, excludingOf, ClaimsAsked.Bodies, out found, acrossM);

    bool AnythingOver(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf, ClaimsAsked asked,
        out LaneClaim found) =>
        AnythingOver(way, fromM, toM, excluding, excludingOf, asked, ClaimPriority.Rejected, out found);

    bool AnythingOver(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf, ClaimsAsked asked,
        ClaimPriority atLeast, out LaneClaim found)
    {
        var at = FromTheStart;
        return NextOver(way, fromM, toM, excluding, excludingOf, asked, atLeast, ref at, out found);
    }

    /// <summary>
    /// <b>Every claim on this piece of the way that is somebody else's</b>, near edge first — <b>including
    /// one that began behind it and runs through</b>, which is the whole of what tells this apart from
    /// <see cref="NextHeld"/>.
    /// </summary>
    /// <remarks>
    /// The two are asking different questions of the same claims. A driver on a way wants the occupants of it
    /// in the order they are actually in, and a claim reaching back past its own tail is a car
    /// <em>behind</em> it; whoever is asking about a named piece of ground — a section two ways meet on, the
    /// mouth of a bay — wants everything lying over it, and where the near edge of that began is nothing to
    /// them.
    /// </remarks>
    public bool NextHeldOver(
        int way, float fromM, float toM, int excluding, ref int at, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving, ClaimsAsked asked = ClaimsAsked.Held) =>
        NextOver(way, fromM, toM, excluding, excludingOf, asked, ClaimPriority.Rejected, ref at, out found);

    /// <param name="atLeast">
    /// The weakest rung an answer may be held at. <b>A rung is a filter and not a second loop</b>: whoever
    /// is asking after a rescue or after a body on the paint is asking one question of one walk, exactly as
    /// the scope is one reading rather than a walk apiece.
    /// </param>
    bool NextOver(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf, ClaimsAsked asked,
        ClaimPriority atLeast, ref int at, out LaneClaim found)
    {
        for (at = at == FromTheStart ? _head[way] : _next[at]; at != NoSlot; at = _next[at])
        {
            ref readonly var claim = ref _slots[at];
            if (claim.FromM > toM) break;
            if (Is(claim, excluding, excludingOf) || !Counts(claim, asked) || claim.ToM < fromM) continue;
            if (claim.Priority > atLeast) continue;

            found = claim;
            return true;
        }

        found = LaneClaim.Nothing;
        return false;
    }

    /// <summary>
    /// <b>Whether anybody is on foot over this stretch of the way</b> — the one question the road's claims
    /// are asked about walkers, and the whole of a driver's "is there somebody on the paint".
    /// </summary>
    /// <remarks>
    /// It excludes nobody, because the asker is a car and the occupants of these claims are walkers:
    /// the two are indexed separately and no query but this one ever reads one, which is what keeps the two
    /// rosters from being told apart by an integer.
    /// </remarks>
    public bool AnybodyOnFoot(int way, float fromM, float toM) =>
        AnythingOver(way, fromM, toM, Nobody, LaneRoster.Driving, ClaimsAsked.OnFoot, out _);

    /// <summary>
    /// <b>Whether a claim somebody else holds refuses an asker claiming at this rung</b> (TER-5e, TER-5g) —
    /// <b>the one comparison there is</b>, so that what a stronger movement takes cannot be answered two
    /// ways.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What a stronger movement takes is ground nobody has reached and nothing else</b>: a claim granted
    /// across a box and a road a driver merely stated can both be given back, and a body — or the road a
    /// body is committed to being able to stop in (<see cref="ClaimPriority.Hard"/>) — cannot. A rule that
    /// took those would not be a rule about who waits, it would be a licence to drive into somebody.
    /// </para>
    /// <para>
    /// <b>A tie refuses a granted claim and does not refuse a stated one</b> (TER-5g), and the difference
    /// is what each of the two is for. A granted claim is one movement's ground and is settled by whoever was
    /// granted it; stated claims are laid by everybody at once, so two movements of one rung that each
    /// refused the other's would each be waiting for ground the other had merely stated, and neither would
    /// ever ask for it. What a tie is settled by is the granted claim, exactly as it was before either of them
    /// stated anything.
    /// </para>
    /// <para>
    /// <b>A statement is read at the rung its holder would have been granted</b>
    /// (<see cref="SaidAhead"/>), which is the whole of how one ladder carries both: the stated band mirrors
    /// the granted one a fixed distance below it, so a street's statement refuses the turn across it and
    /// the turn's refuses nobody but itself.
    /// </para>
    /// <para>
    /// <b>And a rescue's granted ground is taken only by a body</b> (<see cref="ClaimPriority.Special"/>),
    /// which falls out of the ladder rather than being said twice: nothing a road carries of itself is
    /// stronger than a call.
    /// </para>
    /// <para>
    /// <b>A crossing somebody has reserved refuses nobody at all</b> (<see cref="ClaimPriority.Reserved"/>,
    /// PER-27), which is the one arm here that compares nothing: what a walker wants of a zebra is ground
    /// the traffic drives over and takes, and this line is the whole of what would ever make it wait for
    /// one.
    /// </para>
    /// </remarks>
    /// <param name="mine">
    /// The rung the asker would hold this ground at — its movement's own (<c>TownWorld.FirmOn</c>), a call's
    /// or a closure's where it is answering one, and <see cref="ClaimPriority.Hard"/> where it is past the
    /// point it could stop.
    /// </param>
    public static bool Binds(in LaneClaim taken, ClaimPriority mine) => taken.Priority switch
    {
        ClaimPriority.Hard => true,
        ClaimPriority.Rejected => false,
        ClaimPriority.Reserved => false,
        >= ClaimPriority.SoftSpecial => SaidAhead(taken.Priority) < mine,
        _ => taken.Priority <= mine,
    };

    /// <summary>
    /// <b>The granted rung a stated one mirrors</b> (<see cref="ClaimPriority"/>): the two bands are one
    /// order a fixed distance apart, so a statement is compared as the grant its holder would have asked
    /// for. <b>The one place the distance is named</b>, and what makes the movements a claim can be on a
    /// property of the ladder rather than a second field beside it.
    /// </summary>
    public static ClaimPriority SaidAhead(ClaimPriority stated) =>
        (ClaimPriority)(stated - (ClaimPriority.SoftSpecial - ClaimPriority.Special));

    /// <summary>
    /// <b>Whether a claim somebody else holds takes a granted claim away from an asker holding it at this
    /// rung</b> (TER-5e) — the other side of <see cref="Binds"/>, and the one place that comparison is made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A rung above the claim's own takes it, and nothing else does.</b> That is the whole of what a right
    /// of way is entitled to: a granted claim can be handed back because its holder has not reached it, and a
    /// body — or the road a body is committed to being able to stop in — cannot.
    /// </para>
    /// <para>
    /// <b>It is the granted band and nothing else that takes</b>: a rescue holds its road at
    /// <see cref="ClaimPriority.Special"/> and is entitled to ground nobody has reached, while a statement
    /// takes nothing off anybody however straight the movement that made it — what a statement buys is that
    /// it is not <em>ignored</em> (<see cref="Binds"/>), never that somebody must hand ground back for it.
    /// </para>
    /// <para>
    /// <b>And a body takes nothing at all</b> (SIM-7) — <b>a walker included</b>, on the paint as anywhere
    /// else. Traffic, a person, a wreck shoved onto the ground and the town's own furniture all stand on it
    /// rather than claiming it, and they cut the claimant's own grant already on the way it is driving, so a
    /// second refusal would make the first useless. It would also be wrong: the stretch a swerve claims is
    /// the stretch containing the very body it is swinging round, so a claim given back for a body over it
    /// is a claim the movement could never keep for one tick.
    /// </para>
    /// </remarks>
    public static bool TakesAClaim(in LaneClaim taken, ClaimPriority mine) =>
        taken.Priority is > ClaimPriority.Hard and <= ClaimPriority.FirmAcross && taken.Priority < mine;

    /// <summary>
    /// <b>How far up one way the asker is granted</b>, in that way's own metres: of everything held in
    /// front of it that <see cref="Binds"/> says it must give way to, the least near edge plus what the
    /// ground beyond that edge is worth (<see cref="LaneCredit.Of"/>). Infinity where nothing cuts it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One of the bounds a driver's grant is cut at</b> (<c>TownWorld.GrantTheGround</c>), beside what
    /// the laying left of its own ask (<see cref="HeldToM"/>) and the first crossing ahead that binds it:
    /// how much of the road in front of the nose is still the driver's, with what each claim there is worth.
    /// </para>
    /// <para>
    /// <b>The least and never the nearest</b> (<see cref="NextHeld"/>). What a claim is worth is not the same
    /// for every one of them — one whose holder has not reached it is one the asker keeps its own margin
    /// clear of, and a body under way is one it stops at the edge of (<see cref="LaneCredit.Of"/>) — so the
    /// claim whose near edge comes first is not always the one that binds, and cutting at it grants the asker
    /// the ground through whatever is beyond.
    /// </para>
    /// <para>
    /// <b>What cut it comes back too</b> (<paramref name="heldBy"/>), because the reason a body is being
    /// held is a fact about the body in front and not about the distance: a queue is waited behind and a
    /// wreck is driven round, and the two are the same number of metres.
    /// </para>
    /// </remarks>
    /// <param name="heldBy">The claim the answer was cut at, or <see cref="LaneClaim.Nothing"/> where none was.</param>
    /// <param name="asked">
    /// Which claims may cut it. <b>A scope and not a second walk</b>, and never a way past anything
    /// (TER-4c.3): what a scope decides is which claims are in the walk, never whether one that is may be
    /// driven through.
    /// </param>
    public float GrantedOn(
        int way, float fromM, float untilM, int occupant, in LaneCredit asker, out LaneClaim heldBy,
        ClaimsAsked asked = ClaimsAsked.Held)
    {
        heldBy = LaneClaim.Nothing;
        var leastM = float.PositiveInfinity;

        var at = FromTheStart;
        while (NextHeld(way, fromM, untilM, occupant, ref at, out var taken, asker.Under, asked, asker.AcrossM))
        {
            // <b>A claim a stronger movement outranks is not a cut</b> (AMB-4.1, TER-5e). Ground its holder
            // has not reached can be given back; a body, and the road a body is committed to stopping in,
            // are nobody's to take.
            if (!Binds(taken, asker.Asking)) continue;

            // <b>And ground the asker is already standing on is not a cut either</b> (TER-5e). A claim with
            // nothing in it is ground its holder has <em>not reached</em>, so one whose near edge is behind
            // this asker is ground this asker has — never a body to be held off, and never something the
            // asker could get out from under by stopping. Answered at it, the grant stops being a distance in
            // front of the nose and comes back as a body's length of negative road, which is a car frozen on
            // the junction it is halfway across by the car queueing behind it for the same movement.
            //
            // A body's own claim is already left out by the body edge, so this is only ever about the ground
            // in front of one.
            if (taken.StandsToM <= fromM && taken.FromM < fromM) continue;

            var cutM = taken.FromM + asker.Of(taken);
            if (cutM >= leastM) continue;

            leastM = cutM;
            heldBy = taken;
        }

        return leastM;
    }

    /// <summary>
    /// <b>How far up the way the ground is already anybody's</b>: of everything behind
    /// <paramref name="beforeM"/> that is held or standing on the way, the claim whose far edge
    /// reaches furthest. <b>Its far edge is where whoever it belongs to is committed to being able to stop,
    /// and everything past that is ground nothing has taken</b> — which is what a body about to step into a
    /// lane has to ask, since a place inside somebody's own road is a place nothing could have stopped short
    /// of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The furthest and not the nearest.</b> The nearest claim behind may belong to a car that has
    /// already stopped, and the binding one is whoever is still coming through it.
    /// </para>
    /// <para>
    /// It excludes nobody, because the asker is not on the way at all, and its own presence is not yet part
    /// of the answer.
    /// </para>
    /// </remarks>
    public bool TakenUpTo(int way, float beforeM, out LaneClaim found)
    {
        found = LaneClaim.Nothing;
        var any = false;
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var claim = ref _slots[at];
            if (claim.FromM >= beforeM) break;
            if (!Counts(claim, ClaimsAsked.Held) || StandsAside(way, claim, askerAcrossM: 0f)) continue;
            if (any && claim.ToM <= found.ToM) continue;

            found = claim;
            any = true;
        }

        return any;
    }

    /// <summary>
    /// <b>How far up one way this occupant's own hold actually reaches</b>, in that way's own metres — the
    /// answer it was left holding once every claim in the town had been laid against it (TER-4c.1), and
    /// <paramref name="fromM"/> where it holds none of the ground it asked for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the answer the laying gave.</b> A stretch is cut back where it gives way, on its own way
    /// and on every way that way is driven over (<see cref="MakeRoomFor"/>), so what is left of it is the road
    /// its holder was not refused — the ground where two lines meet included — without a second question
    /// about a way the holder will not be on. A driver's grant is cut here and at <see cref="GrantedOn"/>.
    /// </para>
    /// <para>
    /// <b>The furthest edge of the occupant's own stretches and not the first of them.</b> One hold may be
    /// several stretches of one way with the seam wherever the answer fell (TER-5c.2) — a car's road and the
    /// box beyond it — and what the holder has is the far end of the run, the metres between being its own.
    /// </para>
    /// </remarks>
    public float HeldToM(
        int way, float fromM, float toM, int occupant, LaneRoster of = LaneRoster.Driving,
        ClaimsAsked asked = ClaimsAsked.HeldOrStated)
    {
        var heldToM = fromM;
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var claim = ref _slots[at];
            if (claim.FromM >= toM) break;
            if (claim.Occupant != occupant || claim.Of != of || !Counts(claim, asked)) continue;
            if (claim.ToM <= fromM) continue;

            heldToM = MathF.Max(heldToM, claim.ToM);
        }

        return heldToM;
    }

    /// <summary>
    /// Where a walk of one way's claims begins. Handed to <see cref="NextHeld"/> or
    /// <see cref="NextHeldOver"/> and carried by it after that.
    /// </summary>
    public const int FromTheStart = -2;

    /// <summary>
    /// <b>Every claim in front that is already somebody's</b>, near edge first: a road another driver has
    /// taken, ground somebody has been granted, a body that is not going anywhere.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>In front means the body is</b> (<see cref="LaneClaim.StandsToM"/>), and never where the ground it
    /// holds begins: a claim begins a margin behind its owner's tail (TER-4c.1), so near edges put the
    /// occupants of a way in an order that is one margin out of step with the bodies in it. What the asker
    /// wants is whoever is actually ahead of it — a car whose claim runs past the one in front is still
    /// behind it, and one whose ground reaches back past the asker's nose is still in front of it and is
    /// exactly what the asker has to be cut at.
    /// </para>
    /// <para>
    /// <b>Walked rather than answered with the nearest one</b>, because the nearest is not the binding one:
    /// a car at speed rests further up the road than a slower car in front of it, and a driver cut at the
    /// nearer of those two would be granted road straight through the further.
    /// </para>
    /// </remarks>
    public bool NextHeld(
        int way, float fromM, float untilM, int excluding, ref int at, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving, ClaimsAsked asked = ClaimsAsked.Held,
        float acrossM = 0f)
    {
        for (at = at == FromTheStart ? _head[way] : _next[at]; at != NoSlot; at = _next[at])
        {
            ref readonly var claim = ref _slots[at];
            if (claim.FromM > untilM) break;
            if (Is(claim, excluding, excludingOf) || claim.StandsToM < fromM) continue;
            if (!Counts(claim, asked) || StandsAside(way, claim, acrossM)) continue;

            found = claim;
            return true;
        }

        found = LaneClaim.Nothing;
        return false;
    }

    /// <summary>
    /// <b>Whether one claim is in the scope of one question</b> — the whole of what used to be a tag on the
    /// row, worked out here from the claim's own edges, occupant and priority so that it cannot disagree
    /// with them.
    /// </summary>
    /// <remarks>
    /// <b>Nothing here is the claim's own verdict.</b> A claim records who is claiming what, where and how
    /// strongly; what to make of that is the asker's, and the same body is a queue to the lane it is driving
    /// and an obstruction to the lane it is merely lying across.
    /// </remarks>
    public static bool Counts(in LaneClaim claim, ClaimsAsked asked) => asked switch
    {
        ClaimsAsked.Held => claim.HasBody || claim.IsGranted,
        ClaimsAsked.HeldOrStated => claim.HasBody || claim.IsGranted || claim.IsStated,
        ClaimsAsked.Stated => claim.IsStated,
        ClaimsAsked.Bodies => claim.HasBody,
        ClaimsAsked.UnderWay => claim.HasBody && claim.OnItsLine,
        ClaimsAsked.Traffic => claim.IsTraffic,
        ClaimsAsked.TrafficHeld => claim.IsTraffic || claim.IsGranted,
        ClaimsAsked.Granted => claim.IsGranted,
        ClaimsAsked.Loose => claim.IsLoose,
        _ => claim.HasBody && claim.Of == LaneRoster.Walking,
    };

    /// <summary>
    /// Whether a claim is the asker's own. <b>Both halves, always</b>: an occupant is an index into one of
    /// two fleets, so a car excluding itself by number would otherwise also exclude the walker that happens
    /// to hold the same number.
    /// </summary>
    /// <remarks>
    /// <b>And <see cref="Nobody"/> is nobody's, which is not the same as everybody's.</b> The town's own
    /// furniture stands under that number (<see cref="StandingGround"/>), so a question asked by nobody in
    /// particular — a walker at a kerb, an overlay — would exclude every bollard in the town from its own
    /// answer: one question's argument deciding another question's answer.
    /// </remarks>
    static bool Is(in LaneClaim claim, int occupant, LaneRoster of) =>
        occupant != Nobody && claim.Occupant == occupant && claim.Of == of;

    /// <summary>
    /// <b>Whether a claim stands far enough aside of the asker to be got past</b>
    /// (<see cref="LaneClaim.AsideM"/>) — the reader's half of TER-4c.2, and the reason a body may be written
    /// onto every way it touches without shutting every one of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the gap between the two of them across the way and not either one's distance from the
    /// line.</b> Both stand somewhere across the band, and what decides whether one is in the other's way is
    /// how far apart they are — so an asker on the line asks exactly what it always asked, and one that has
    /// stepped aside asks about the ground it has stepped to. Read off the holder alone, a way was
    /// two-dimensional for the body written into it and one-dimensional for every body reading it, and the
    /// asker could never move out from under an answer it had been given.
    /// </para>
    /// <para>
    /// <b>It is asked of the line and not of the band</b>, because the line is what the traffic here drives.
    /// A body a metre inside the edge of a three-metre lane has left two metres of that lane clear, and none
    /// of them is any use to a car whose own line runs through the body; a body that clips the kerbside edge
    /// has left the line alone and is nothing to anybody driving it.
    /// </para>
    /// <para>
    /// <b>Only what a body of this way's own width would get past asks it</b> — what is in front of a driver
    /// (<see cref="Nearest"/>), what its grant is cut at (<see cref="NextHeld"/>), what is coming up behind
    /// (<see cref="BehindBody"/>), and the ground a body off every line would step into
    /// (<see cref="AnybodyStandingOver"/>), which is that same question asked without a line to ask it along.
    /// <b>A template's own corridor never does</b> (<see cref="NextOver"/>): it runs over ground no line goes
    /// down and sweeps the whole band as it goes, so a body it merely reaches past is a body it is about to
    /// be inside of. Neither does an overlay, nor
    /// <see cref="AlreadyHolds"/> — the claims hold where the bodies are (TER-4c) and this is the only
    /// question that is about getting past one.
    /// </para>
    /// </remarks>
    /// <param name="askerAcrossM">
    /// <b>Where across this way's own line the asker is</b>, signed to the way's right and nought for
    /// everybody travelling the line — which is every driver and every walker that has not stepped off it.
    /// At nought this is exactly the question it has always been: a claim clears when its own near edge is
    /// further from the line than half the width of what travels there.
    /// </param>
    bool StandsAside(int way, in LaneClaim claim, float askerAcrossM)
    {
        var reachM = _ways.ClearsAsideM(way);
        return claim.AcrossFromM > askerAcrossM + reachM || claim.AcrossToM < askerAcrossM - reachM;
    }

    bool Nearest(
        int way, float fromM, float untilM, int excluding, LaneRoster excludingOf, ClaimsAsked asked,
        out LaneClaim found, float acrossM = 0f)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var claim = ref _slots[at];
            if (claim.FromM > untilM) break;
            // Reaching as far as the asker's own near edge and no further is a contact, which is why this
            // is the same bar <see cref="NextHeld"/> holds a claim to and not a tighter one.
            if (Is(claim, excluding, excludingOf) || claim.StandsToM < fromM) continue;
            if (!Counts(claim, asked) || StandsAside(way, claim, acrossM)) continue;

            found = claim;
            return true;
        }

        found = LaneClaim.Nothing;
        return false;
    }

    /// <summary>
    /// <b>The nearest traffic behind</b>, which is what anything about to occupy ground it is not on yet has
    /// to ask — the paint a walker is about to step onto.
    /// </summary>
    /// <remarks>
    /// <b>Traffic and not bodies</b> (<see cref="ClaimsAsked.Traffic"/>): every asker here is asking what is
    /// coming down the lane at it, and another person standing on the carriageway is not that. <b>And where
    /// the body itself has got to</b> (<see cref="LaneClaim.StandsToM"/>) — a car whose road reaches the kerb
    /// is a car still coming, and one whose bonnet is already past it has gone.
    /// </remarks>
    public bool BehindBody(
        int way, float beforeM, float sinceM, int excluding, out LaneClaim found,
        LaneRoster excludingOf = LaneRoster.Driving)
    {
        found = LaneClaim.Nothing;
        var any = false;
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var claim = ref _slots[at];
            if (claim.FromM >= beforeM) break;
            if (Is(claim, excluding, excludingOf) || claim.StandsToM < sinceM) continue;
            if (!Counts(claim, ClaimsAsked.Traffic) || StandsAside(way, claim, askerAcrossM: 0f)) continue;

            found = claim;
            any = true;
        }

        return any;
    }

    /// <summary>
    /// Whether anybody else has already been granted ground this stretch runs over. <b>What makes a claim
    /// binding rather than a note</b>: two cars in neighbouring bays each looked at an empty lane and
    /// each backed onto it, and the second of them is what this refuses.
    /// </summary>
    /// <param name="asked">
    /// How much of what anybody else has counts. <b>A scope and not a second walk</b>: ground somebody has
    /// been granted is what a claimant on the lane it is already on has to be held off, and a claimant on
    /// the lane running the other way is asking what is <em>coming</em> — for which the road anybody has
    /// stated (TER-5g) is the whole of the warning there is.
    /// </param>
    public bool ClaimedByAnother(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf = LaneRoster.Driving,
        ClaimsAsked asked = ClaimsAsked.Granted) =>
        AnythingOver(way, fromM, toM, excluding, excludingOf, asked, out _);

    /// <summary>Everything on one way, nearest first — for a test, an overlay, and nothing on the hot path.</summary>
    public int CopyTo(int way, Span<LaneClaim> into)
    {
        var written = 0;
        for (var at = _head[way]; at != NoSlot && written < into.Length; at = _next[at]) into[written++] = _slots[at];

        return written;
    }
}
