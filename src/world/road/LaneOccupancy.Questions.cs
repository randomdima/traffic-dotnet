namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The questions the book is asked</b>: what is in front, what is behind, how much of a named piece of
/// ground is already somebody's — every one of them a walk of one way's stretches in the order they lie,
/// and none of them a decision (SIM-7).
/// </summary>
/// <remarks>
/// <para>
/// <b>Which stretches an answer is allowed to count is a mask and not a second loop</b>
/// (<see cref="Only"/>): a walker at a kerb asks about traffic, a driver's grant is cut by everything
/// spoken for, a crossing asks only about bodies on foot, and a driver approaching one asks who is
/// waiting at it — four questions of one book, so that a use added to <see cref="LaneUse"/> is named in
/// one place per question rather than missed in one of them.
/// </para>
/// <para>
/// <b>And how strong a hold on it counts is the same thing said of the rank</b> (TER-5e): whoever is asking
/// after a rescue or after a body on the paint is asking one walk with a floor under it, not a walk of its
/// own that filters what came back. <b>The grant itself is one of these questions</b>
/// (<see cref="GrantedOn"/>) and not a loop each asker writes for itself — the road and the pavement ask it
/// in the same words, in their own figures (<see cref="LaneCredit"/>).
/// </para>
/// </remarks>
internal sealed partial class LaneOccupancy
{
    /// <summary>
    /// <b>The nearest body in front</b>: the stretch with the least near edge that still reaches past
    /// <paramref name="fromM"/> and begins before <paramref name="untilM"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stretch the asking body is already overlapping answers at its own near edge rather than being
    /// skipped — a car inside somebody else is a contact and not a gap, and reading it as an empty road is
    /// the one wrong answer here.
    /// </para>
    /// <para>
    /// <b>Reaching past means the body does</b> (<see cref="LaneSlot.StandsToM"/>) and not the road it has
    /// taken: a driver behind this one has a reservation running through it, and that is a car behind and
    /// not a body in front.
    /// </para>
    /// </remarks>
    public bool AheadBody(
        int way, float fromM, float untilM, int excluding, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        Nearest(way, fromM, untilM, excluding, excludingOf, Bodies, out found);

    /// <summary>The nearest <see cref="LaneUse.Claimed"/> stretch in front, which is ground about to stop being empty.</summary>
    public bool AheadClaim(
        int way, float fromM, float untilM, int excluding, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        Nearest(way, fromM, untilM, excluding, excludingOf, Only(LaneUse.Claimed), out found);

    /// <summary>
    /// <b>The nearest body in front that is going nowhere</b>: a wreck, somebody knocked down, a walker
    /// shoved off its own line. <b>It is the other half of the walker's own grant</b> (PER-24) — what
    /// <see cref="UnderWay"/> leaves out of the cut is asked for here instead, because a body that is not
    /// going anywhere is something to step round rather than to queue behind.
    /// </summary>
    public bool AheadObstruction(
        int way, float fromM, float untilM, int excluding, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        Nearest(way, fromM, untilM, excluding, excludingOf, Only(LaneUse.Obstruction), out found);

    /// <summary>
    /// <b>Whether any traffic has this stretch of the way</b>: a road a driver has taken, ground somebody
    /// has claimed, a body with no reservation to its name. The all-or-nothing question — <em>is this piece
    /// of ground anybody's</em> — as against <see cref="NextSpokenFor"/>'s "how much of it is mine".
    /// </summary>
    /// <remarks>
    /// <b>Traffic and not everything spoken for</b>: the asker is somebody about to step into the road, and
    /// neither another person already standing in it nor a bollard that has stood there since the town was
    /// laid is a reason to stay on the pavement. What it waits for is what is coming.
    /// </remarks>
    public bool AnyTrafficOver(int way, float fromM, float toM) =>
        AnythingOver(way, fromM, toM, Nobody, LaneRoster.Driving, TrafficSpoken, out _);

    /// <summary>
    /// <b>Whether an ambulance answering a call is coming through this stretch</b> (AMB-4) — the one
    /// question about the road a walker's patience does not get to override.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the rank and not the vehicle.</b> Whoever holds ground with
    /// <see cref="RightOfWay.Emergency"/> is the thing being got out of the way of; an ambulance driving
    /// home holds its road at <see cref="RightOfWay.Traffic"/> like anybody else, and a walker's escape from
    /// a crossing that never clears applies to it exactly as it applies to a bus.
    /// </para>
    /// <para>
    /// <b>And it is the ones actually coming through</b> (<paramref name="comingThroughMps"/>), because the
    /// stretch of a rescue that has stopped is not a rescue to be got out of the way of — it is a stopped
    /// car, which is what anybody taking the ground would have made of it in any case. <b>Stopped is a pace
    /// and never zero</b>: a car held in a queue creeps at fractions of a millimetre a second, and read
    /// against zero that is a rescue coming through for as long as it sits there. Every stretch over the
    /// ground is walked rather than the first one taken, so a rescue standing on a piece of road cannot
    /// hide one that is coming through it.
    /// </para>
    /// </remarks>
    /// <param name="comingThroughMps">How fast a rescue has to be going to be worth standing aside for.</param>
    public bool AnyRescueOver(int way, float fromM, float toM, float comingThroughMps)
    {
        var at = FromTheStart;
        while (NextOver(
                   way, fromM, toM, Nobody, LaneRoster.Driving, TrafficSpoken, RightOfWay.Emergency, ref at,
                   out var rescue))
        {
            if (rescue.AlongMps >= comingThroughMps) return true;
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
    /// The first answer of <see cref="NextSpokenForOver"/> and not a second copy of its loop, so <em>is any
    /// of this anybody's</em> and <em>which of them are</em> cannot come apart.
    /// </remarks>
    public bool SpokenForByAnother(
        int way, float fromM, float toM, int excluding, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        AnythingOver(way, fromM, toM, excluding, excludingOf, Spoken, out found);

    bool AnythingOver(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf, int uses, out LaneSlot found) =>
        AnythingOver(way, fromM, toM, excluding, excludingOf, uses, RightOfWay.TurningAcross, out found);

    bool AnythingOver(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf, int uses, RightOfWay atLeast,
        out LaneSlot found)
    {
        var at = FromTheStart;
        return NextOver(way, fromM, toM, excluding, excludingOf, uses, atLeast, ref at, out found);
    }

    /// <summary>
    /// <b>Every stretch of this piece of the way that is somebody else's</b>, near edge first — <b>including
    /// one that began behind it and runs through</b>, which is the whole of what tells this apart from
    /// <see cref="NextSpokenFor"/>.
    /// </summary>
    /// <remarks>
    /// The two are asking different questions of the same book. A driver on a way wants the occupants of it
    /// in the order they are actually in, and a stretch reaching back past its own tail is a car
    /// <em>behind</em> it; whoever is asking about a named piece of ground — a section two ways meet on, the
    /// mouth of a bay — wants everything lying over it, and where the near edge of that began is nothing to
    /// them.
    /// </remarks>
    public bool NextSpokenForOver(
        int way, float fromM, float toM, int excluding, ref int at, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving) =>
        NextOver(way, fromM, toM, excluding, excludingOf, Spoken, RightOfWay.TurningAcross, ref at, out found);

    /// <param name="atLeast">
    /// The weakest right of way an answer may be held at. <b>A rank is a filter and not a second loop</b>:
    /// whoever is asking after a rescue or after a body on the paint is asking one question of one walk,
    /// exactly as the uses are a mask rather than a walk apiece.
    /// </param>
    bool NextOver(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf, int uses, RightOfWay atLeast,
        ref int at, out LaneSlot found)
    {
        for (at = at == FromTheStart ? _head[way] : _next[at]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > toM) break;
            if (Is(slot, excluding, excludingOf) || (Only(slot.Use) & uses) == 0 || slot.ToM < fromM) continue;
            if (slot.Right < atLeast) continue;

            found = slot;
            return true;
        }

        found = LaneSlot.Nothing;
        return false;
    }

    /// <summary>
    /// <b>Whether anybody is on foot over this stretch of the way</b> — the one question the road's book is
    /// asked about walkers, and the whole of a driver's "is there somebody on the paint".
    /// </summary>
    /// <remarks>
    /// It excludes nobody, because the asker is a car and the occupants of these stretches are walkers:
    /// the two are indexed separately and no query but this one ever reads a
    /// <see cref="LaneUse.OnFoot"/> slot, which is what keeps the two rosters from being told apart by
    /// an integer.
    /// </remarks>
    public bool AnybodyOnFoot(int way, float fromM, float toM) =>
        AnythingOver(way, fromM, toM, Nobody, LaneRoster.Driving, Only(LaneUse.OnFoot), out _);

    /// <summary>
    /// <b>Whether anybody is <em>using</em> this stretch of paint</b> — on foot over it and holding it with
    /// the crossing's own right of way, which is what a driver owes a stop to (TER-5e).
    /// </summary>
    /// <remarks>
    /// <b>It is narrower than <see cref="AnybodyOnFoot"/> and the difference is a body that is not going
    /// anywhere</b>: somebody knocked down (PER-18) lies where they fell and holds the ground under them at
    /// <see cref="RightOfWay.Traffic"/> like any other obstruction. A driver is still held off them — their
    /// stretch cuts every grant that runs over it — but they are not somebody to be stopped short of the
    /// paint for, and read as one they hold a street shut until an ambulance that is itself being stopped
    /// short of the same paint comes to fetch them.
    /// </remarks>
    public bool AnybodyCrossing(int way, float fromM, float toM) =>
        AnythingOver(
            way, fromM, toM, Nobody, LaneRoster.Driving, Only(LaneUse.OnFoot), RightOfWay.OnThePaint, out _);

    /// <summary>
    /// <b>Whether anybody with the right of way is waiting for this stretch of the way</b> — a walker at a
    /// kerb that asked for the band and was refused it (TER-5e), and the whole of what a car owes somebody
    /// standing at an uncontrolled crossing.
    /// </summary>
    /// <remarks>
    /// <b>It is a question of its own because the answer is not a cut</b>. Ground somebody is waiting for
    /// is in no mask — it is neither a body nor road anybody has taken — so no grant is cut at it; what it
    /// does is stop the traffic short of the paint, which is a place and is asked about here.
    /// </remarks>
    public bool AnybodyWaitingFor(int way, float fromM, float toM) =>
        AnythingOver(way, fromM, toM, Nobody, LaneRoster.Driving, Only(LaneUse.Awaited), out _);

    /// <summary>
    /// <b>Whether ground somebody else holds refuses an asker with this right of way</b> (TER-5e) — the
    /// one place the ranks are compared, so that what a right of way takes cannot be answered two ways.
    /// </summary>
    /// <remarks>
    /// <b>What a greater right of way takes is a claim and nothing else.</b> A claim is ground its holder
    /// has not reached and is not committed to, so it can be given back; a body, and the road a body is
    /// committed to being able to stop in, cannot be — and a rule that took those would not be a right of
    /// way, it would be a licence to drive into somebody.
    /// </remarks>
    public static bool Binds(in LaneSlot taken, RightOfWay mine) =>
        taken.Use != LaneUse.Claimed || taken.Right >= mine;

    /// <summary>
    /// <b>Whether ground somebody else holds takes a claim away from an asker holding it at this rank</b>
    /// (TER-5e) — the other side of <see cref="Binds"/>, and the one place that comparison is made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A rank above the claim's own takes it, and nothing else does.</b> That is the whole of what a right
    /// of way is entitled to: a claim can be handed back because its holder has not reached it, and a body —
    /// or the road a body is committed to being able to stop in — cannot.
    /// </para>
    /// <para>
    /// <b>A body standing on the ground is deliberately not here</b> (SIM-7). Ordinary traffic, a wreck
    /// shoved onto the ground and somebody on foot over it all cut the claimant's own grant already, on the
    /// way it is driving, and a second refusal would make the first useless. It would also be wrong: the
    /// stretch a swerve claims is the stretch containing the very body it is swinging round
    /// (<c>ManeuverDesk.ClaimTheSwerve</c>), so a claim given back for a body over it is a claim `E-4` could
    /// never keep for one tick.
    /// </para>
    /// </remarks>
    public static bool TakesAClaim(in LaneSlot taken, RightOfWay mine) => taken.Right > mine;

    /// <summary>
    /// <b>How far up one way the asker is granted</b>, in that way's own metres: of everything spoken for in
    /// front of it that <see cref="Binds"/> says it must give way to, the least near edge plus what the
    /// ground beyond that edge is worth (<see cref="LaneCredit.Of"/>). Infinity where nothing cuts it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the grant, and it is one walk rather than one per asker.</b> A driver on a lane and a
    /// walker on the pavement are asking the same question of two books — how much of the road in front of
    /// me is still mine — and answered apart they answered it differently: the same switch on
    /// <see cref="LaneUse"/>, the same margin, written twice and free to drift.
    /// </para>
    /// <para>
    /// <b>The least and never the nearest</b> (<see cref="NextSpokenFor"/>). What a stretch is worth is not
    /// the same for every use — a claim is one the asker keeps its own margin clear of, a reservation is one
    /// it stops at the edge of (<see cref="LaneCredit.Of"/>) — so the stretch whose near edge comes first is
    /// not always the one that binds, and cutting at it grants the asker the ground through whatever is
    /// beyond.
    /// </para>
    /// <para>
    /// <b>What cut it comes back too</b> (<paramref name="heldBy"/>), because the reason a body is being
    /// held is a fact about the body in front and not about the distance: a queue is waited behind and a
    /// wreck is driven round, and the two are the same number of metres.
    /// </para>
    /// </remarks>
    /// <param name="heldBy">The stretch the answer was cut at, or <see cref="LaneSlot.Nothing"/> where none was.</param>
    /// <param name="uses">
    /// Which uses may cut it. <b>A mask and not a second walk</b>: the one asker that is not cut by
    /// everything on its way is the walker, which steps round a body going nowhere (PER-24) and is
    /// answered here by leaving that use out (<see cref="UnderWay"/>).
    /// </param>
    public float GrantedOn(
        int way, float fromM, float untilM, int occupant, in LaneCredit asker, out LaneSlot heldBy,
        int uses = Spoken)
    {
        heldBy = LaneSlot.Nothing;
        var leastM = float.PositiveInfinity;

        var at = FromTheStart;
        while (NextSpokenFor(way, fromM, untilM, occupant, ref at, out var taken, asker.Under, uses))
        {
            // <b>A claim a stronger movement outranks is not a cut</b> (AMB-4.1, TER-5e). A claim is ground
            // its holder has not reached and can give back; a body, and the road a body is committed to
            // stopping in, are nobody's to take.
            if (!Binds(taken, asker.Right)) continue;

            // <b>And a claim the asker is already standing on is not a cut either</b> (TER-5e). A claim is
            // ground its holder has <em>not reached</em>, so one whose near edge is behind this asker is
            // ground this asker has — never a body to be held off, and never something the asker could get
            // out from under by stopping. Answered at it, the grant stops being a distance in front of the
            // nose and comes back as a body's length of negative road, which is a car frozen on the junction
            // it is halfway across by the car queueing behind it for the same movement.
            if (taken.Use == LaneUse.Claimed && taken.FromM < fromM) continue;

            // <b>And neither is a body level with the asker rather than in front of it</b>
            // (<see cref="NextSpokenFor"/>: in front means the body is, so anything nearer than this is
            // already left out). What reaches here is the body whose front is <em>exactly</em> the asker's,
            // which is what the end of a way makes of everything clamped onto it: two bodies at one metre,
            // each cut at ground the other is standing on, each holding the other where it stands for the
            // rest of the run. Neither of them is in front of anybody, and the grant that says so is a
            // body's length of negative road rather than a distance to walk.
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
    /// <paramref name="beforeM"/> that is spoken for or standing on the way, the stretch whose far edge
    /// reaches furthest. <b>Its far edge is where whoever it belongs to is committed to being able to stop,
    /// and everything past that is ground nothing has taken</b> — which is what a body about to step into a
    /// lane has to ask, since a place inside somebody's own road is a place nothing could have stopped short
    /// of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The furthest and not the nearest.</b> The nearest stretch behind may belong to a car that has
    /// already stopped, and the binding one is whoever is still coming through it.
    /// </para>
    /// <para>
    /// It excludes nobody, because the asker is not on the way at all: what a walker beside a road asks is
    /// whether the road is anybody's, and its own presence is not yet part of the answer.
    /// </para>
    /// </remarks>
    public bool TakenUpTo(int way, float beforeM, out LaneSlot found)
    {
        found = LaneSlot.Nothing;
        var any = false;
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM >= beforeM) break;
            if ((Only(slot.Use) & Spoken) == 0) continue;
            if (any && slot.ToM <= found.ToM) continue;

            found = slot;
            any = true;
        }

        return any;
    }

    /// <summary>
    /// Where a walk of one way's stretches begins. Handed to <see cref="NextSpokenFor"/> or
    /// <see cref="NextSpokenForOver"/> and carried by it after that.
    /// </summary>
    public const int FromTheStart = -2;

    /// <summary>
    /// <b>Every stretch in front that is already spoken for</b>, near edge first: a road another driver has
    /// taken, ground somebody has claimed, a body that is not going anywhere.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>In front means the body is</b> (<see cref="LaneSlot.StandsToM"/>), and never where the ground it
    /// holds begins: a stretch begins a margin behind its owner's tail (TER-4c.1), so near edges put the
    /// occupants of a way in an order that is one margin out of step with the bodies in it. What the asker
    /// wants is whoever is actually ahead of it — a car whose stretch runs past the one in front is still
    /// behind it, and one whose ground reaches back past the asker's nose is still in front of it and is
    /// exactly what the asker has to be cut at.
    /// </para>
    /// <para>
    /// <b>Walked rather than answered with the nearest one</b>, because the nearest is not the binding one:
    /// a car at speed rests further up the road than a slower car in front of it, and a driver cut at the
    /// nearer of those two would be granted road straight through the further.
    /// </para>
    /// </remarks>
    public bool NextSpokenFor(
        int way, float fromM, float untilM, int excluding, ref int at, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving, int uses = Spoken)
    {
        for (at = at == FromTheStart ? _head[way] : _next[at]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > untilM) break;
            if (Is(slot, excluding, excludingOf) || slot.StandsToM < fromM) continue;
            if ((Only(slot.Use) & uses) == 0) continue;

            found = slot;
            return true;
        }

        found = LaneSlot.Nothing;
        return false;
    }

    /// <summary>The stretch a slot of this use occupies in a mask, so a query names the uses it is about.</summary>
    static int Only(LaneUse use) => 1 << (int)use;

    /// <summary>
    /// <b>Traffic</b>: a driver on its route, and anything with wheels standing on the road. What is asked
    /// about by whoever wants to know what is <em>coming</em> — a walker at a kerb, a car about to back out
    /// of a bay — and a person on the carriageway is not an answer to that question.
    /// </summary>
    const int Traffic = (1 << (int)LaneUse.Reserved) | (1 << (int)LaneUse.Obstruction);

    /// <summary>
    /// Where a body actually is, of whichever roster: traffic, anybody on foot in the lane, and the town's
    /// own furniture. <b>Read to <see cref="LaneSlot.StandsToM"/></b>, which is what tells a body apart from
    /// the road it has taken now that the two are one stretch.
    /// </summary>
    const int Bodies = Traffic | (1 << (int)LaneUse.OnFoot) | (1 << (int)LaneUse.Furniture);

    /// <summary>
    /// Ground traffic has spoken for: reserved, claimed, or held by something with no reservation to its
    /// name. The same set as <see cref="Traffic"/> but for a claim, and read to <see cref="LaneSlot.ToM"/>.
    /// </summary>
    const int TrafficSpoken = Traffic | (1 << (int)LaneUse.Claimed);

    /// <summary>
    /// Ground anybody has spoken for, which is everything a body is on and everything claimed. <b>And not
    /// what anybody is merely waiting for</b> (<see cref="LaneUse.Awaited"/>): an ask that was refused is
    /// nobody's ground, so no grant is cut at it and the one question about it is its own.
    /// </summary>
    const int Spoken = Bodies | (1 << (int)LaneUse.Claimed);

    /// <summary>
    /// Ground spoken for by something that is <em>going somewhere</em>: everything <see cref="Spoken"/> but
    /// a body with no reservation to its name. <b>The walker's own mask</b> (PER-24): a body going nowhere
    /// cuts no walk, because what a walker does with one is step round it (<see cref="AheadObstruction"/>)
    /// rather than stand behind it and wait for it to move.
    /// </summary>
    public const int UnderWay = Spoken & ~(1 << (int)LaneUse.Obstruction);

    /// <summary>
    /// Whether a stretch is the asker's own. <b>Both halves, always</b>: an occupant is an index into one of
    /// two fleets, so a car excluding itself by number would otherwise also exclude the walker that happens
    /// to hold the same number.
    /// </summary>
    /// <remarks>
    /// <b>And <see cref="Nobody"/> is nobody's, which is not the same as everybody's.</b> The town's own
    /// furniture stands under that number (<see cref="StandingGround"/>), so a question asked by nobody in
    /// particular — a walker at a kerb, an overlay — would exclude every bollard in the town from its own
    /// answer. It is the trap the furniture was given a use of its own to escape, sprung from the other end:
    /// one question's argument deciding another question's answer.
    /// </remarks>
    static bool Is(in LaneSlot slot, int occupant, LaneRoster of) =>
        occupant != Nobody && slot.Occupant == occupant && slot.Of == of;

    bool Nearest(int way, float fromM, float untilM, int excluding, LaneRoster excludingOf, int uses, out LaneSlot found)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > untilM) break;
            // Reaching as far as the asker's own near edge and no further is a contact, which is why this
            // is the same bar <see cref="NextSpokenFor"/> holds a stretch to and not a tighter one.
            if (Is(slot, excluding, excludingOf) || slot.StandsToM < fromM) continue;
            if ((Only(slot.Use) & uses) == 0) continue;

            found = slot;
            return true;
        }

        found = LaneSlot.Nothing;
        return false;
    }

    /// <summary>
    /// <b>The nearest traffic behind</b>, which is what anything about to occupy ground it is not on yet has
    /// to ask — the paint a walker is about to step onto.
    /// </summary>
    /// <remarks>
    /// <b>Traffic and not bodies</b> (<see cref="Traffic"/>): every asker here is asking what is coming down
    /// the lane at it, and another person standing on the carriageway is not that. <b>And where the body
    /// itself has got to</b> (<see cref="LaneSlot.StandsToM"/>) — a car whose reservation reaches the kerb
    /// is a car still coming, and one whose bonnet is already past it has gone.
    /// </remarks>
    public bool BehindBody(
        int way, float beforeM, float sinceM, int excluding, out LaneSlot found,
        LaneRoster excludingOf = LaneRoster.Driving)
    {
        found = LaneSlot.Nothing;
        var any = false;
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM >= beforeM) break;
            if (Is(slot, excluding, excludingOf) || slot.StandsToM < sinceM || (Only(slot.Use) & Traffic) == 0) continue;

            found = slot;
            any = true;
        }

        return any;
    }

    /// <summary>
    /// Whether anybody else has already claimed ground this stretch runs over. <b>What makes a claim a
    /// reservation rather than a note</b>: two cars in neighbouring bays each looked at an empty lane and
    /// each backed onto it, and the second of them is what this refuses.
    /// </summary>
    public bool ClaimedByAnother(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf = LaneRoster.Driving) =>
        AnythingOver(way, fromM, toM, excluding, excludingOf, Only(LaneUse.Claimed), out _);

    /// <summary>Everything on one way, nearest first — for a test, an overlay, and nothing on the hot path.</summary>
    public int CopyTo(int way, Span<LaneSlot> into)
    {
        var written = 0;
        for (var at = _head[way]; at != NoSlot && written < into.Length; at = _next[at]) into[written++] = _slots[at];

        return written;
    }
}
