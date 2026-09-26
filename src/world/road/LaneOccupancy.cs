namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>Which roster a stretch's occupant is named in.</b> The road's claims are not one roster's — a person
/// in a lane is on the road like anything else — so an occupant is an integer into one of two fleets, and
/// <b>which one is carried rather than inferred</b>.
/// </summary>
/// <remarks>
/// It was inferred once, from which network's claims the stretch was in, and the two rosters were told
/// apart by an integer: a walker's index read out of the car fleet is whichever car happens to hold that
/// number.
/// </remarks>
internal enum LaneRoster : byte
{
    Driving,

    Walking,
}

/// <summary>
/// <b>One occupant's claim on one stretch of one way</b>, in the way's own metres — <b>the whole of what is
/// in the road's claims, and the only property a way has</b>. A way is used when there is a claim on it and
/// not otherwise; who is claiming, where, and how strongly is all there is to say.
/// </summary>
/// <remarks>
/// <b>Both edges are distances along the bending ground</b>: a way is a chain of arcs, its metres are that
/// chain's own arclength, and a claim on it is an interval of that. Nothing here is a chord and no shape is
/// lost — what the pair leaves out is the pose of the body on the way, never the curve. Of the way's width
/// it keeps the span the body covers across the line (<see cref="AcrossFromM"/>), because without it a body
/// that touches a way is a body that shuts it.
/// </remarks>
/// <param name="FromM">The near edge, measured the way the way is driven.</param>
/// <param name="ToM">The far edge. Never less than <paramref name="FromM"/>.</param>
/// <param name="StandsToM">
/// <b>Where the body in it ends</b>, as against how far the ground it has taken reaches — the two far edges
/// of one claim. <b>It is <paramref name="FromM"/> where nothing is standing in it at all</b>, which is what
/// tells ground somebody has been granted or has stated from ground somebody is on: a question about
/// where a body <em>is</em> reads this edge, and a question about what ground is <em>claimed</em> reads
/// <paramref name="ToM"/>.
/// <para>
/// <b>It is also what tells in front from behind.</b> A claim begins a margin behind its owner and is
/// clipped at the start of every way it runs onto, so near edges are not the bodies' order; this is.
/// </para>
/// </param>
/// <param name="AlongMps">How fast the occupant is going <em>along this way</em> — negative where it faces the other way.</param>
/// <param name="Occupant">
/// Whatever the caller names an occupant by, which for this town is the body's own index in
/// <paramref name="Of"/> — and <see cref="LaneOccupancy.Nobody"/> for the town's own furniture, which is in
/// neither roster.
/// </param>
/// <param name="Priority">
/// <b>How strong the claim is</b> (TER-5g): whether it can be taken, and by whom — <b>the one property
/// besides who and where</b>. What movement it is on is in it and not beside it (TER-5e): the same car is
/// granted the lane it is leaving at one rung and the turn across the oncoming stream at another, and those
/// are two claims on two ways.
/// </param>
/// <param name="Of">Which of the town's two rosters <paramref name="Occupant"/> is an index into.</param>
/// <param name="AcrossFromM">
/// <b>Where across this way's own line the holder's body begins</b>, signed to the way's right — the one
/// thing a claim says about the third dimension, and the reason a body may be written onto a way it barely
/// touches without shutting it (TER-4c.2, <see cref="LaneOccupancy.StandsAside"/>).
/// <para>
/// <b>A span and not a clearance, because getting past is a fact about two bodies.</b> A distance from the
/// line says how much of a nuisance a body is to whatever travels that line and cannot say whether it is a
/// nuisance to a body that has stepped aside of it — so a way was two-dimensional for whoever was written
/// into it and one-dimensional for everybody reading it, and nobody could ever move out from under an
/// answer.
/// </para>
/// <para>
/// <b>An empty span on the line is the answer for everything laid from a line rather than from a pose</b>:
/// a body under way, a claim ahead of one and a walker's band are all measured along the way's own line and
/// stand on it by construction. It is also the safe default, since a claim that says nothing about where
/// across it stands is one nobody may drive through.
/// </para>
/// </param>
/// <param name="AcrossToM">And where it ends, on the same terms. Never less than <paramref name="AcrossFromM"/>.</param>
/// <param name="OnItsLine">
/// <b>Whether the body in it is following this way's line rather than merely standing on it</b> — the one
/// thing left that neither the edges nor the priority say, and it turns exactly two answers.
/// <para>
/// <b>The margin</b> (<see cref="LaneCredit.Of"/>): such a claim begins a gap behind its owner's tail
/// (TER-5c.2), so whoever is cut at it stops at the near edge; everything else is laid at its true extent
/// and the asker keeps its own margin off it.
/// </para>
/// <para>
/// It is meaningless where nothing is standing in the claim, and readers ask it only together with the body
/// edge.
/// </para>
/// </param>
internal readonly record struct LaneClaim(
    float FromM, float ToM, float StandsToM, float AlongMps, int Occupant, ClaimPriority Priority,
    LaneRoster Of = LaneRoster.Driving, float AcrossFromM = 0f,
    float AcrossToM = 0f, bool OnItsLine = false)
{
    public static LaneClaim Nothing => new(
        float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, 0f, LaneOccupancy.Nobody,
        ClaimPriority.Hard);

    public bool Found => float.IsFinite(FromM);

    /// <summary>
    /// <b>How far clear of this way's own line the holder stands</b>, and nought or less where it covers the
    /// line — the span said as the one figure whoever is travelling the line cares about.
    /// </summary>
    /// <remarks>
    /// For a reader, not for a decision: <see cref="LaneOccupancy.StandsAside"/> asks the span, because the
    /// asker is not always on the line.
    /// </remarks>
    public float AsideM => MathF.Max(AcrossFromM, -AcrossToM);

    /// <summary>
    /// <b>Whether a body is standing in this claim at all</b>, as against ground its holder has been granted
    /// or has stated and not reached. Read off the edges and stored nowhere.
    /// </summary>
    public bool HasBody => StandsToM > FromM;

    /// <summary>
    /// <b>Whether this is one of the town's own props</b> (<see cref="StandingGround"/>) rather than
    /// anybody's body — the immovable third of TER-4c, which is in neither roster and is going nowhere by
    /// construction.
    /// </summary>
    public bool IsFurniture => Occupant == LaneOccupancy.Nobody;

    /// <summary>
    /// <b>Whether this is wheeled traffic</b>: a body of the driving roster. What is asked about by whoever
    /// wants to know what is <em>coming</em> — a walker at a kerb, a car about to back out of a bay — and a
    /// person on the carriageway is not an answer to that question, nor is a bollard.
    /// </summary>
    public bool IsTraffic => HasBody && Of == LaneRoster.Driving && !IsFurniture;

    /// <summary>
    /// <b>A body lying where it is rather than driving down this way</b>: a wreck, a car with nobody in it,
    /// one shoved off its line, one under a hand. <b>What a driver is held off rather than following</b>, and what
    /// the town's own furniture is not — a prop is nobody's body and is driven round by its own geometry.
    /// </summary>
    public bool IsLoose => HasBody && !OnItsLine && !IsFurniture;

    /// <summary>
    /// <b>Ground its holder has been granted and not reached</b> (the granted band,
    /// <see cref="ClaimPriority.FirmAcross"/> and above): the far end of a box, a bay being backed out of, a
    /// swerve about to cross, a road a police car at a scene is holding shut. <b>The one hold a caller can both lay and take
    /// back inside a tick</b>, which is what the count of them is for.
    /// </summary>
    public bool IsGranted => !HasBody && Priority <= ClaimPriority.FirmAcross;

    /// <summary>
    /// <b>Road its holder has stated it means to use and has not reached</b> (the stated band,
    /// <see cref="ClaimPriority.Soft"/> and its movements, TER-5g) — nothing is standing in it and nothing
    /// has been granted it; it is a statement of where a body is going.
    /// </summary>
    public bool IsStated => Priority is >= ClaimPriority.SoftSpecial and <= ClaimPriority.SoftAcross;

    /// <summary>
    /// <b>An ask that was refused, left among the claims so the traffic can see it</b>
    /// (<see cref="ClaimPriority.Rejected"/>) — nobody's ground, in no walk that cuts a grant.
    /// </summary>
    public bool IsRejected => Priority == ClaimPriority.Rejected;
}

/// <summary>
/// <b>Who is on each way, as intervals of the way's own arclength</b> — and, by TER-4c, <b>the whole of
/// what an agent looks at</b>. A ray finds a shape; what a driver needs to know is whether that shape is
/// somebody going where it is going, and the claims are what know.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every way in the town is in here</b> (<see cref="TownWays"/>, TER-4c.2) — the carriageway's lanes, the
/// joins across its junctions, the ways its bays are worked off, the two sides of every pavement and the
/// mitres between them. <b>One table and one numbering</b>: the ground inside a junction belongs to no lane
/// (<see cref="RoadGraph"/>) and a footway belongs to no carriageway, and both are ways here, so a claim on
/// any of them is comparable with a claim on any other and no metre of the world has two records that can
/// disagree about who has it (TER-4c.3).
/// </para>
/// <para>
/// <b>A body goes into the claims of the ground it is standing on, whatever kind of body it is.</b> A person
/// standing on the carriageway is a claim on the lane it stands in and cuts the road a driver is granted
/// exactly as a car would; a car that has mounted a kerb is a stretch of the footway under it and is walked
/// round exactly as a wreck in a lane is driven round. <b>What kind of ground the way is is what tells those
/// apart, and never which kind of body is standing on it.</b>
/// </para>
/// <para>
/// <b>Ground of two kinds laid over one another is two ways and no exception</b> (TER-4c.2). A zebra is
/// carriageway a walk runs over, so whoever stands on the paint writes the band of the lane beneath and the
/// stretch of the crossing way alike, whatever kind of body it is. One piece of ground carries one claim per
/// way and a body on two ways is on two ways: the pair cannot disagree about who has the ground, because
/// neither of them is the other's record of it.
/// </para>
/// <para>
/// <b>What each of those two writes is for is a different question, and neither is the write's.</b> A
/// walker asks the road what traffic is <em>coming</em> (<see cref="AnyTrafficOver"/>), of the one band it is
/// about to step into; a driver is held off the paint by the bodies standing in its own lane. Written on the
/// lane alone, a car or a person on a crossing was a body no reader of that crossing could see.
/// </para>
/// <para>
/// <b>It is rebuilt from the bodies every tick and never written to during a decision</b>, which is what
/// makes it an index rather than a register. Nothing has to be released, nothing can leak, a wreck drops
/// out of it the tick it stops being driven, and two cars deciding on different ticks read the same claims.
/// The forward-looking claims in it — ground granted across a box, the road a body under way has taken —
/// are re-laid from the car's own state every tick for exactly the same reason.
/// </para>
/// <para>
/// <b>One body is one claim on one way, and never two of them.</b> Where a body is and how much road it
/// has taken are the same fact read to two different edges (<see cref="LaneClaim.StandsToM"/>), so a body
/// cannot appear twice on a way, cannot overlap itself, and cannot be cut at its own other half.
/// </para>
/// <para>
/// <b>A claim is granted by where it starts and not by who asks first</b> (TER-4c.1). The near edge of a
/// body's own is its tail, less the margin it keeps there, so every request is laid before anybody is
/// granted anything and what a car is then granted is its own claim cut at the nearest one in front of it.
/// Two cars therefore need no order to be resolved in, and the answer is the same whichever of them is
/// asked first. <b>Ground nothing of the asker's own reaches is claimed ahead instead</b>
/// (<see cref="ClaimAhead"/>), and that one is checked against the claims before it is laid, because there
/// is no tail to anchor the answer to.
/// </para>
/// <para>
/// <b>What comes back is the asker's to move into.</b> Nothing here is a queue of asks waiting: a stretch
/// granted is a stretch nobody else can be granted, so the holder needs no second permission from anything
/// and asks for none — and whoever comes to that ground later is the one that gives way.
/// </para>
/// <para>
/// <b>It answers and never decides</b> (SIM-7). Nothing here is a permission the road withholds: what
/// holds a car off the traffic in front of it is the speed profile, working on a grant that is a distance
/// and not a verdict, and where a caller does turn an answer into a refusal — a claim not taken, a
/// crossing not begun — the refusal is that caller's one gate and the claims are only what it read.
/// </para>
/// <para>
/// <b>Occupants are held as bucket lists and not as sorted arrays.</b> A town has ten thousand ways and a
/// few hundred cars, so a rebuild that touched every way would cost two orders of magnitude more than the
/// bodies it is describing; only the ways somebody is on are touched at all.
/// </para>
/// <para>
/// <b>This file is the claims themselves</b> — how a way is numbered, what a rebuild drops, and how a
/// stretch is laid. <b>The questions asked of them are LaneOccupancy.Questions.cs</b>, which is every walk
/// of them a caller can make and nothing that writes.
/// </para>
/// </remarks>
internal sealed partial class LaneOccupancy
{
    public const int Nobody = -1;

    const int NoSlot = -1;

    /// <summary>
    /// <b>The town's ways, and the whole of what this knows about the ground</b> — how many there are, how
    /// long each is, and what has to be clear of its line (<see cref="TownWays"/>). The numbering is one
    /// table's, so a claim on a footway and a claim on a lane are the same kind of thing said about
    /// different ground.
    /// </summary>
    readonly TownWays _ways;

    /// <summary>
    /// <b>Which ways share ground with which</b> (<see cref="WayCrossings"/>, TER-5c) — the whole of what the
    /// claims know about the world outside one way's own arclength, and what makes a stretch of a lane mean
    /// something about the town rather than about the lane.
    /// </summary>
    /// <remarks>
    /// <b>It is read when a claim is laid and never written to</b> (TER-5c.1). A body takes ground on the ways
    /// it is on; where one of those is driven over another, the arbitration reaches across into that way's own
    /// claims and takes the ground from whichever of the two gives it up — so no stretch is ever laid on a way
    /// its holder will not be on, and no reader has to ask a second question to find out who else has the
    /// metres it was granted.
    /// </remarks>
    readonly WayCrossings _crossings;

    /// <summary>The first slot on each way, or <see cref="NoSlot"/>. Only the ways in <see cref="_touched"/> are ever stale.</summary>
    readonly int[] _head;

    /// <summary>The next slot on the same way, ascending by <see cref="LaneClaim.FromM"/>.</summary>
    readonly int[] _next;

    readonly LaneClaim[] _slots;

    /// <summary>Which ways got a slot this tick, so a rebuild resets those heads and no others.</summary>
    readonly int[] _touched;

    /// <summary>
    /// Whether a way is already in <see cref="_touched"/>. <b>Asked of this and never of
    /// <see cref="_head"/></b>: a way <see cref="Withdraw"/> empties has no head and has still been laid on,
    /// and keyed on the head it would go into the list a second time the next time anybody laid on it.
    /// </summary>
    readonly bool[] _laidOn;

    int _slotCount;
    int _touchedCount;
    int _claimCount;

    /// <param name="ways">
    /// <b>Every way in the town, in one numbering</b> (<see cref="TownWays"/>) — the carriageway, the bays
    /// and the pavement together. There is one of these and one set of claims over it, so that two bodies
    /// on one piece of the world are two stretches of one way rather than two records nothing compares.
    /// </param>
    /// <param name="mostSlots">
    /// How many stretches the town may hold at once. <b>A bound on the work and not a figure behaviour
    /// reads</b> — and, since the claims are the whole of what a driver looks at, a bound that must never
    /// actually be reached: a dropped stretch is a body nobody's grant is cut at. It is sized from the two
    /// rosters and the town's own furniture, and the gates hold it clear of its own ceiling.
    /// </param>
    /// <param name="crossings">
    /// <b>Which of those ways share ground with which</b> (TER-5c), so that a claim laid on one is arbitrated
    /// against the claims on every way its own stretch is driven over. <see cref="WayCrossings.None"/> for a
    /// fixture whose ways touch nothing.
    /// </param>
    public LaneOccupancy(TownWays ways, int mostSlots, WayCrossings crossings)
    {
        _ways = ways;
        _crossings = crossings;

        _head = new int[ways.Count];
        Array.Fill(_head, NoSlot);
        _next = new int[mostSlots];
        _slots = new LaneClaim[mostSlots];
        _touched = new int[mostSlots];
        _laidOn = new bool[ways.Count];
    }

    /// <summary>
    /// <b>The numbering these claims are laid in</b>, for a caller that holds a lane, a turn slot or a
    /// footway edge and wants the way it is. Asked of the claims so that the table and the numbering cannot
    /// come apart.
    /// </summary>
    public TownWays Ways => _ways;

    public int WayCount => _ways.Count;

    /// <summary>How many stretches the last rebuild laid, which is what says whether the bound was reached.</summary>
    public int SlotCount => _slotCount;

    public int Capacity => _slots.Length;

    /// <summary>
    /// How many stretches anybody has claimed — a figure for the tests and the instruments, and read by no
    /// decision. A granted claim is the one thing a caller can both lay and take back inside a tick
    /// (<see cref="Withdraw"/>), so a count that does not come back to nothing when the holders let go is
    /// how a leak shows.
    /// </summary>
    public int ClaimCount => _claimCount;

    /// <summary>
    /// The ways somebody is on, in no order and each of them once. <b>A reader that wants every claim
    /// walks this and not the town</b>: a town has ten thousand ways and a few hundred occupants.
    /// </summary>
    /// <remarks>
    /// A way everything laid on has since been withdrawn from is still named here and holds nothing, which
    /// is what every reader of it does with a way anyway: it walks the stretches, and there are none.
    /// </remarks>
    public ReadOnlySpan<int> OccupiedWays => _touched.AsSpan(0, _touchedCount);

    public float WayLengthM(int way) => _ways.LengthM(way);

    /// <summary>Everything laid last tick is dropped. Nothing survives a rebuild, which is the whole guarantee.</summary>
    public void Begin()
    {
        for (var index = 0; index < _touchedCount; index++)
        {
            _head[_touched[index]] = NoSlot;
            _laidOn[_touched[index]] = false;
        }

        _touchedCount = 0;
        _slotCount = 0;
        _claimCount = 0;
    }

    /// <summary>
    /// <b>One occupant's stretches of one way, taken back inside the tick that laid them</b> — what a car
    /// that has stopped wanting ground it claimed does, so that nothing later in the same walk is refused
    /// road whose holder has already let go of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every claim of that occupant at that priority on that way and not one of them</b>, which is what
    /// its callers want: a movement given back is given back whole, wherever the seam between the car's road
    /// and the ground beyond it happened to fall that tick (<c>TownWorld.ClaimWhatTheAnswerTook</c>). The
    /// town's two granted claims are told apart by the ways they are laid on — a movement's on a junction's
    /// join or on a bay's own way — so there is nothing here for a caller to name more finely.
    /// </para>
    /// <para>
    /// The row itself is left where it is rather than compacted out: the claims are rebuilt from nothing
    /// every tick, so the only cost of a hole is the room it takes until then, and moving one would
    /// invalidate every index a walk in progress is holding.
    /// </para>
    /// </remarks>
    public void Withdraw(int way, int occupant, ClaimsAsked asked, LaneRoster of = LaneRoster.Driving)
    {
        var previous = NoSlot;
        for (var at = _head[way]; at != NoSlot;)
        {
            var next = _next[at];
            if (_slots[at].Occupant == occupant && _slots[at].Of == of && Counts(_slots[at], asked))
            {
                if (previous == NoSlot) _head[way] = next;
                else _next[previous] = next;

                if (!_slots[at].HasBody) _claimCount--;
            }
            else
            {
                previous = at;
            }

            at = next;
        }
    }

    /// <summary>
    /// <b>The far end of one occupant's stretch of one way brought back to where it was answered</b>
    /// (TER-4c.1) — the ask laid, the answer taken off it, and the claim left holding the second.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never behind the body</b> (<see cref="LaneClaim.StandsToM"/>). A grant is how far a nose may go and
    /// goes to nought where a car is held at a bumper; the ground under the body itself is not a grant and is
    /// not the asker's to give back, and cut away it is a car no reader of the claims can see.
    /// </para>
    /// <para>
    /// <b>The near edge does not move, so the order does not change</b> — the list is kept ascending by it
    /// (<see cref="Lay"/>), and a claim whose far end has come in is still where it was. What is left with
    /// no length at all is a way the answer never reached, and it goes out rather than staying as an interval
    /// no query can tell from a point.
    /// </para>
    /// </remarks>
    public void CutTo(
        int way, int occupant, float toM, ClaimsAsked asked = ClaimsAsked.UnderWay,
        LaneRoster of = LaneRoster.Driving) =>
        Cut(way, occupant, of, asked, float.NegativeInfinity, float.PositiveInfinity, toM);

    /// <summary>
    /// <b>One ask brought back to its answer, over every stretch that ask laid</b> (TER-4c.1) — the piece
    /// with the body in it and <b>the pieces ahead of the body alike</b>, which is what keeps one hold to one
    /// answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stretches of one ask are told from an occupant's others by the ground they are on</b>
    /// (<paramref name="askedFromM"/>, <paramref name="askedToM"/>) and not by what they look like. A body's
    /// road ahead of its own nose carries no body, so it is ground granted and not reached like the box a car
    /// holds beyond it and like the road a police car at a scene holds shut — and scoped by the look of the
    /// stretch, the answer either missed the road it was about or cut a hold that was never asked here.
    /// </para>
    /// <para>
    /// <b>Cut to nothing, a piece goes out</b>, which is the ordinary answer for a way the grant never
    /// reached: a hold is one run of ways, and what the ways past the answer keep is nothing.
    /// </para>
    /// </remarks>
    /// <param name="askedFromM">The near edge of what was asked for on this way, in the way's own metres.</param>
    /// <param name="askedToM">And its far edge, which is where the ask ran out or the way did.</param>
    public void CutTheAskTo(
        int way, int occupant, float askedFromM, float askedToM, float toM,
        LaneRoster of = LaneRoster.Driving) =>
        Cut(way, occupant, of, ClaimsAsked.Held, askedFromM, askedToM, toM);

    /// <summary>
    /// The walk both of those are, <b>over the stretches of one way that are one occupant's, in one scope and
    /// over one piece of ground</b>.
    /// </summary>
    void Cut(
        int way, int occupant, LaneRoster of, ClaimsAsked asked, float overFromM, float overToM, float toM)
    {
        var previous = NoSlot;
        for (var at = _head[way]; at != NoSlot;)
        {
            var next = _next[at];
            ref var slot = ref _slots[at];
            if (slot.Occupant != occupant || slot.Of != of || !Counts(slot, asked)
                || slot.ToM <= overFromM || slot.FromM >= overToM)
            {
                previous = at;
                at = next;
                continue;
            }

            var cutToM = MathF.Max(slot.StandsToM, MathF.Min(slot.ToM, toM));
            if (cutToM <= slot.FromM)
            {
                if (previous == NoSlot) _head[way] = next;
                else _next[previous] = next;
            }
            else
            {
                slot = slot with { ToM = cutToM };
                previous = at;
            }

            at = next;
        }
    }

    /// <summary>
    /// <b>The near edge of one occupant's stretch of one way brought back to where its own answer left
    /// off</b> (TER-5c.2), so that the metres an answer took off the stretch before it are still somebody's.
    /// <b>False where the occupant holds no such stretch here</b>, which is the caller's cue to lay one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Back over ground nobody holds and never through anybody</b> (TER-4c.3): a near edge moves to the
    /// metre asked for or to the far edge of the nearest stretch behind it, whichever of the two is the
    /// further on. The list stays ascending by near edge, because nothing is ever moved behind the stretch it
    /// follows.
    /// </para>
    /// <para>
    /// <b>The body edge goes with it.</b> What this is asked of is ground its holder has stated and not
    /// reached, so the two edges are one metre; left where it was, the metres gained would read as a body
    /// standing in them (<see cref="LaneClaim.StandsToM"/>).
    /// </para>
    /// </remarks>
    public bool ReachBackTo(
        int way, int occupant, float fromM, ClaimsAsked asked, LaneRoster of = LaneRoster.Driving)
    {
        var found = false;
        var behindM = 0f;
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref var slot = ref _slots[at];

            // A refused ask is nobody's ground and is laid over the very stretch the traffic holds (TER-5g),
            // so it is no barrier to anything reaching back over it.
            if (slot.IsRejected) continue;

            if (slot.Occupant == occupant && slot.Of == of && Counts(slot, asked))
            {
                found = true;
                var toFromM = MathF.Max(fromM, behindM);
                if (toFromM < slot.FromM)
                {
                    slot = slot with
                    {
                        FromM = toFromM, StandsToM = slot.HasBody ? slot.StandsToM : toFromM,
                    };
                }
            }

            behindM = MathF.Max(behindM, slot.ToM);
        }

        return found;
    }

    /// <summary>
    /// <b>The body inside one occupant's stretch of one way, grown to the ground that body's box actually
    /// covers</b> (TER-4c.2) — the three edges every other body is laid on, said of a stretch that is
    /// already there. <b>False where the occupant has no body over these metres</b>, which is the caller's
    /// cue to lay one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One body is one stretch of one way</b> (TER-5c.2), so a body read a second way — from its pose,
    /// where the stretch already there was measured from its line
    /// (<c>TownWorld.PlaceTheBody</c>, <c>TownWorld.AskForTheGround</c>) — may not be laid beside itself.
    /// <b>Dropped instead of merged, the metres the second reading had and the first did not belonged to
    /// nobody</b>: a claim's body edge is the nose of the line the car is driving, and the leading corner of
    /// a car standing at an angle to that line reaches past it. Those metres stayed inside the claim as
    /// ground its holder had been <em>granted</em> — which a stronger movement is entitled to take
    /// (TER-5e), with the body standing on it.
    /// </para>
    /// <para>
    /// <b>Out over ground nobody holds and never through anybody</b> (TER-4c.3), at both ends and on the
    /// same terms as <see cref="ReachBackTo"/>: the near edge moves back to the metre asked for or to the far
    /// edge of the nearest stretch behind it, and the body edge out to the metre asked for or to the near
    /// edge of the nearest stretch in front. What it cannot reach is ground somebody else is already on, and
    /// the claims stay disjoint.
    /// </para>
    /// <para>
    /// <b>The body edge takes the far edge with it where it has to.</b> A stretch is near, body and far in
    /// that order, so a body grown past the end of the road its holder asked for is a body that has reached
    /// the end of it — which is what a car standing at a bar with its nose over the line is.
    /// </para>
    /// <para>
    /// <b>How far across the way its holder stands is left where the stretch has it.</b> That figure is
    /// whether the traffic on this way can get past (<see cref="LaneClaim.AsideM"/>), a car on the line it
    /// is driving is recorded as being on that line, and a body on its line is the thing nothing gets past.
    /// </para>
    /// </remarks>
    /// <param name="fromM">Where the box's ground on this way begins, which is the near edge it wants.</param>
    /// <param name="standsToM">And where the box itself ends.</param>
    /// <param name="toM">
    /// And where the ground it is holding ends, which is the whole of what it is matched against a stretch
    /// by: a row whose own stopping ground runs over a stretch of the same body is that body twice over as
    /// much as one standing on it is (TER-5c.2).
    /// </param>
    public bool StandOutTo(
        int way, int occupant, float fromM, float standsToM, float toM, LaneRoster of = LaneRoster.Driving)
    {
        var found = false;
        var behindM = 0f;
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref var slot = ref _slots[at];

            // A refused ask is nobody's ground and is laid over the very stretch the traffic holds (TER-5g),
            // so it is neither something to grow into nor something to be stopped by.
            if (slot.IsRejected) continue;

            if (slot.Occupant != occupant || slot.Of != of || !slot.HasBody
                || slot.ToM <= fromM || slot.FromM >= toM)
            {
                behindM = MathF.Max(behindM, slot.ToM);
                continue;
            }

            found = true;
            var outToM = MathF.Max(slot.StandsToM, MathF.Min(standsToM, AheadOfTheSlot(at, way)));
            slot = slot with
            {
                FromM = MathF.Min(slot.FromM, MathF.Max(fromM, behindM)),
                StandsToM = outToM,
                ToM = MathF.Max(slot.ToM, outToM),
            };

            behindM = MathF.Max(behindM, slot.ToM);
        }

        return found;
    }

    /// <summary>
    /// <b>The first metre past one stretch that is not its own to take</b> — the near edge of the next
    /// stretch on the way, or the end of the way where there is none.
    /// </summary>
    /// <remarks>
    /// The list is kept ascending by near edge and the stretches on a way are disjoint (TER-4c.3), so the
    /// next one along is the nearest thing in front of this one. <b>A refused ask is not one of them</b>: it
    /// is nobody's ground and lies over whatever the traffic holds.
    /// </remarks>
    float AheadOfTheSlot(int slot, int way)
    {
        for (var at = _next[slot]; at != NoSlot; at = _next[at])
        {
            if (!_slots[at].IsRejected) return _slots[at].FromM;
        }

        return _ways.LengthM(way);
    }

    /// <summary>
    /// <b>One occupant's own stretches of one way, held at a stronger rung</b> (TER-5g.1) — the road to
    /// ground it has been granted, worth what the ground at the end of it is worth. What is already held at
    /// that rung or above it is left where it is.
    /// </summary>
    /// <remarks>
    /// <b>The rung and nothing else.</b> Whose the ground is, where it runs and how fast its holder is
    /// coming are facts the stretch already carries and none of them change; what moves is the one thing the
    /// ladder is read for, which is whether somebody else may take these metres.
    /// </remarks>
    public void RaiseTo(
        int way, int occupant, float fromM, float toM, ClaimPriority rung,
        LaneRoster of = LaneRoster.Driving) =>
        Rung(way, occupant, of, fromM, toM, rung, stronger: true);

    /// <summary>
    /// <b>And at a weaker one</b> (TER-5g.1): ground its holder cannot reach, held at what the road to it is
    /// held at.
    /// </summary>
    /// <remarks>
    /// <b>A body's ground is never let down</b> (TER-5e). The stretch a body stands in and the road it can no
    /// longer give back are p0 because nothing takes them, and a rung that could be walked down would be a
    /// licence to drive into somebody — so both are passed over here whatever the caller asked for.
    /// </remarks>
    public void LowerTo(
        int way, int occupant, float fromM, float toM, ClaimPriority rung,
        LaneRoster of = LaneRoster.Driving) =>
        Rung(way, occupant, of, fromM, toM, rung, stronger: false);

    /// <summary>
    /// The walk both of those are, over the stretches of one way that are one occupant's and run over one
    /// piece of ground. <b>A refused ask is not one of them</b> (TER-5g): it is nobody's ground and it has
    /// no rung to move.
    /// </summary>
    void Rung(
        int way, int occupant, LaneRoster of, float fromM, float toM, ClaimPriority rung, bool stronger)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref var slot = ref _slots[at];
            if (slot.Occupant != occupant || slot.Of != of || slot.IsRejected || slot.HasBody) continue;
            if (slot.ToM <= fromM || slot.FromM >= toM) continue;
            if (stronger ? slot.Priority <= rung : slot.Priority >= rung) continue;
            if (!stronger && slot.Priority == ClaimPriority.Hard) continue;

            var held = slot.IsGranted;
            slot = slot with { Priority = rung };
            if (slot.IsGranted == held) continue;

            if (slot.IsGranted) _claimCount++;
            else _claimCount--;
        }
    }

    /// <summary>
    /// <b>Ground its holder has been granted or has stated and is not on</b>: the far end of a box a car
    /// has committed to crossing, a bay being backed out of, a swerve about to cross, a road a police car at
    /// a scene is holding shut, the stretch a driver or a walker means to use beyond its own, and the crossing
    /// a walker has reserved.
    /// <b>Nothing is standing in it</b>, which is exactly why a reading taken off the bodies alone lets two
    /// bodies take it at once.
    /// </summary>
    /// <remarks>
    /// <b>It is on a way its holder is going to be on</b>, like every other claim, and never a mark
    /// left on somebody else's road (TER-5c.1).
    /// </remarks>
    /// <param name="takingUpAgain">
    /// <b>Whether this stretch may begin past ground somebody else holds</b> instead of being given up whole
    /// (TER-4c.3). It is for the one shape that is not a hold reaching into ground its holder was refused:
    /// <b>the span between two stretches of one hold</b> — a car's road and the box it has already been
    /// granted, with somebody standing on the lane in between. Laid whole or not at all, those metres are
    /// nobody's and the hold has a hole in it (TER-5c.2); everything else asks with this false, since a claim
    /// that took up past a body would be holding road it could not reach.
    /// </param>
    public bool ClaimAhead(
        int way, float fromM, float toM, float alongMps, int occupant, ClaimPriority priority,
        LaneRoster of = LaneRoster.Driving, float acrossFromM = 0f,
        float acrossToM = 0f, bool takingUpAgain = false) =>
        Lay(
            way, fromM, fromM, toM, alongMps, occupant, priority, of, acrossFromM, acrossToM,
            onItsLine: true, takingUpAgain);

    /// <summary>
    /// <b>A body laid from its own pose, at the true extent of the box it stands in</b>
    /// (<see cref="BodyFootprint"/>) — no margin, and no more of the way than the box actually covers: a
    /// wreck, a car with nobody in it, a body shoved off its line, a car under a hand, and the town's own
    /// furniture.
    /// </summary>
    /// <remarks>
    /// <b>It is not a judgement that the body is an obstruction.</b> A car halfway across the oncoming lane
    /// lays one there while it is driving perfectly well, and what the traffic in that lane does about it is
    /// that traffic's own business. What this says is only that the interval is the bare box and that its
    /// holder is not driving down <em>this</em> way.
    /// </remarks>
    /// <param name="standsToM">
    /// Where the box itself ends, which is <paramref name="toM"/> for anything standing still. <b>A body on a
    /// template of its own is laid over the whole sweep that template has still to make</b> and not over the
    /// pose it is passing through: the ground a manoeuvre is about to be on is ground it is holding, and a
    /// straight walked clear at the moment it was drawn is a straight somebody else may come to rest in while
    /// it is being driven.
    /// </param>
    public bool ClaimWhereItStands(
        int way, float fromM, float standsToM, float toM, float alongMps, int occupant,
        ClaimPriority priority = ClaimPriority.Hard, LaneRoster of = LaneRoster.Driving,
        float acrossFromM = 0f, float acrossToM = 0f) =>
        Lay(way, fromM, standsToM, toM, alongMps, occupant, priority, of, acrossFromM, acrossToM, onItsLine: false);

    /// <summary>
    /// <b>A body under way down this very way, as the one claim it is</b>: from the margin it keeps behind
    /// its own tail (TER-5c.2), through where the body itself ends (<paramref name="standsToM"/>), to the far
    /// end of the road it has taken. Every driver under way lays one on the ways of its own line and so does
    /// every walker, and what holds one off the next is that nobody is granted ground somebody else will
    /// still be standing on once they have stopped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the one claim that carries a margin</b> (<see cref="LaneCredit.Of"/>): a claim is one
    /// interval of a way's arclength, and the width of the body and the slack in its line were thrown away to
    /// make it. So a grant cut at one of these is cut where the ground begins, and a grant cut at anything
    /// else has the asker's own margin taken off it instead.
    /// </para>
    /// <para>
    /// <b>It is anchored at the body, so the occupants of a way come out in the order they are actually
    /// in</b>, which is what a grant has to be taken against: a claim measured from where a car will have
    /// <em>stopped</em> can reach past a slower car in front, and a driver cut at one of those would be a
    /// driver held up by the car behind it.
    /// </para>
    /// </remarks>
    public bool ClaimUnderWay(
        int way, float fromM, float standsToM, float toM, float alongMps, int occupant,
        ClaimPriority priority = ClaimPriority.Hard, LaneRoster of = LaneRoster.Driving,
        float acrossFromM = 0f, float acrossToM = 0f) =>
        Lay(way, fromM, standsToM, toM, alongMps, occupant, priority, of, acrossFromM, acrossToM, onItsLine: true);

    /// <summary>
    /// The insertion all three of the above are, <b>taking the three edges in the order they lie on the
    /// way</b> — near, body, far. They are three floats and nothing but the order tells them apart, so there
    /// is one order and every caller writes it. <b>Returns whether the stretch went in whole</b>: false past
    /// the bound, off the end of the way, wholly over ground somebody else holds, and <b>false where either
    /// edge had to give way</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Whole is what a caller laying one stretch over several ways needs</b> (TER-5c.2). Such a run is a
    /// hold on the ways of one line, and a way in the middle of it that gave ground up is where the hold
    /// ends: laid on regardless, the ways past it carry stretches beginning at their own first metre, and
    /// between them and the metres this one gave up is road belonging to nobody.
    /// </para>
    /// <para>
    /// <b>Ground given up to its own holder is a seam and not an end</b> (TER-4c.3). A near edge pushed onto
    /// the far edge of a stretch the same body already holds leaves no metre unheld — the two are one hold —
    /// so such a run carries on over the ways past it. Counted as an end, a car's statement stopped at the
    /// first metre of its own road it met and never reached the box beyond, which is the hole this whole
    /// arrangement is against.
    /// </para>
    /// <para>
    /// <b>What comes out of here is a way whose claims are disjoint</b> (TER-4c.3). The metres a claim would
    /// have shared with one already on the way are taken off it before it goes in, so the two abut on an
    /// exact metre and no metre of any way is ever in two claims at once. What decides which of the pair
    /// gives the ground up is <see cref="Yields"/>.
    /// </para>
    /// <para>
    /// <b>One stretch goes in and never two</b> (TER-5c.2). A claim cut at something in front of it gives up
    /// the metres beyond that thing rather than resuming on the far side, because a body is one stretch of
    /// one way and every reader of the claims is built on it — <see cref="Withdraw"/>, <see cref="CutTo"/>
    /// and <see cref="AlreadyHolds"/> all take an occupant's hold on a way to be one interval.
    /// </para>
    /// </remarks>
    bool Lay(
        int way, float fromM, float standsToM, float toM, float alongMps, int occupant,
        ClaimPriority priority, LaneRoster of, float acrossFromM, float acrossToM,
        bool onItsLine, bool takingUpAgain = false)
    {
        if (toM < fromM) return false;
        if (toM <= 0f || fromM >= _ways.LengthM(way)) return false;

        var laying = new LaneClaim(
            fromM, toM, Math.Clamp(standsToM, fromM, toM), alongMps, occupant, priority, of,
            acrossFromM, acrossToM, onItsLine);

        if (!MakeRoomFor(way, ref laying, takingUpAgain, out var pastAnother)) return false;

        var whole = laying.ToM == toM && (laying.FromM == fromM || !pastAnother);
        return Insert(way, in laying, laying.FromM, laying.ToM) && whole;
    }

    /// <summary>
    /// The stretch put into the way's list at the place the near edges are ordered by, once
    /// <see cref="MakeRoomFor"/> has made the ground nobody else's. <b>False past the bound</b>, and the
    /// caller's own geometry is what covers the gap.
    /// </summary>
    bool Insert(int way, in LaneClaim laying, float fromM, float toM)
    {
        if (_slotCount == _slots.Length) return false;

        var run = laying with
        {
            FromM = fromM, ToM = toM, StandsToM = Math.Clamp(laying.StandsToM, fromM, toM),
        };

        var slot = _slotCount++;
        _slots[slot] = run;
        if (run.IsGranted) _claimCount++;

        if (!_laidOn[way])
        {
            _laidOn[way] = true;
            _touched[_touchedCount++] = way;
        }

        if (_head[way] == NoSlot)
        {
            _head[way] = slot;
            _next[slot] = NoSlot;
            return true;
        }

        // Ascending by the near edge, which is the order both queries walk in.
        if (_slots[_head[way]].FromM >= fromM)
        {
            _next[slot] = _head[way];
            _head[way] = slot;
            return true;
        }

        var at = _head[way];
        while (_next[at] != NoSlot && _slots[_next[at]].FromM < fromM) at = _next[at];

        _next[slot] = _next[at];
        _next[at] = slot;
        return true;
    }

    /// <summary>
    /// <b>The ground this claim is about to take, made nobody else's first</b> (TER-4c.3) — the incoming
    /// stretch cut back where it must give way, and whatever it outranks cut back where it must. <b>False
    /// where nothing of it is left</b>, and then it is not laid at all.
    /// </summary>
    /// <remarks>
    /// <b>Two walks and one arbitration</b> (TER-5c.1). A claim shares ground with the claims on its own way
    /// and with the claims on every way that way is driven over, and those are the same fact about the same
    /// world: the ground is made nobody else's along the one (<see cref="AlongTheWay"/>) and across the
    /// others (<see cref="AcrossTheWays"/>), by one comparison (<see cref="Yields"/>). <b>The order is
    /// load-bearing</b> — the stretch is settled on its own way before it is carried over any crossing, so
    /// what a crossed way is arbitrated against is the ground this claim is actually going to hold.
    /// </remarks>
    bool MakeRoomFor(int way, ref LaneClaim laying, bool takingUpAgain, out bool pastAnother)
    {
        pastAnother = false;

        // <b>A refused ask is a mark and not a hold</b> (TER-5g): it is nobody's ground, it binds nobody and
        // it cuts nothing, so it neither takes metres from a claim nor gives any up to one. It is laid on
        // the very ground the traffic holds — that is the whole of what it is for, since what it says is
        // that somebody is waiting for exactly those metres.
        if (laying.IsRejected) return true;

        return AlongTheWay(way, ref laying, takingUpAgain, ref pastAnother)
               && AcrossTheWays(way, ref laying, takingUpAgain, ref pastAnother);
    }

    /// <summary>
    /// <b>And the same ground made nobody else's on every way this one is driven over</b> (TER-5c, TER-4c.3)
    /// — the claims of the crossed ways, read where they lie and cut where they give way. <b>Nothing is laid
    /// on any of them</b>: what happens there is that somebody else's stretch gets shorter, or this one does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is what makes one claim mean one piece of the world.</b> A stretch is stated in one way's
    /// metres and the ground it stands for is the town's, so two ways that meet inside a junction are one
    /// piece of the world under two names — and a claim that only ever looked at its own name for it would be
    /// granted the metre two lines meet on at the same moment as the claim on the other line (TER-5c.1).
    /// </para>
    /// <para>
    /// <b>Both sides see one crossing, so neither has to be asked first.</b> The table records what each of a
    /// pair takes of the other and files it under both (<see cref="WayCrossings"/>), and the comparison is a
    /// fact about the pair rather than about the walk (<see cref="YieldsAcross"/>) — so whichever of two
    /// claims is laid first, the same one of them ends up holding the ground.
    /// </para>
    /// <para>
    /// <b>A section is the whole of what is known about where</b>. Within it there is no correspondence
    /// between one way's metres and the other's, so a claim reaching into a section is cut at that section's
    /// own near edge and a claim on the crossed way gives up the whole of the section's own metres. Read
    /// finer than the table was measured, the cut would be a number this has no right to.
    /// </para>
    /// </remarks>
    bool AcrossTheWays(int way, ref LaneClaim laying, bool takingUpAgain, ref bool pastAnother)
    {
        foreach (ref readonly var section in _crossings.Of(way))
        {
            if (section.MineToM <= laying.FromM || section.MineFromM >= laying.ToM) continue;

            if (!OverTheCrossing(in section, ref laying, takingUpAgain, ref pastAnother)) return false;
        }

        return true;
    }

    /// <summary>
    /// One crossing settled: <b>the claims on the crossed way over the metres this one shares with it</b>,
    /// each either cut back off that ground or left holding it with this stretch cut short of it.
    /// <b>False where nothing of the incoming stretch is left.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A stretch of this way's own holder is no answer</b> (<see cref="SameHolder"/>, TER-5c.2). One body's
    /// ground is one hold however many ways it is numbered in — a car's road and the way it is turning onto,
    /// a car working into a bay and the lane it is sweeping — and a hold read as its own obstruction is a body
    /// that refuses itself.
    /// </para>
    /// <para>
    /// <b>The loser gives up the section and never half of it.</b> On the crossed way the shared ground is
    /// the section's own metres, so a claim beaten there ends where the section begins, or — beaten at its own
    /// near edge — gives up the whole of itself, which is the same rule the claims of one way are settled by
    /// (TER-4c.3).
    /// </para>
    /// </remarks>
    bool OverTheCrossing(
        in CrossedSection section, ref LaneClaim laying, bool takingUpAgain, ref bool pastAnother)
    {
        var way = section.OnWay;
        var previous = NoSlot;
        for (var at = _head[way]; at != NoSlot;)
        {
            var next = _next[at];
            ref var taken = ref _slots[at];
            if (taken.IsRejected || SameHolder(in laying, in taken)
                || taken.ToM <= section.FromM || taken.FromM >= section.ToM)
            {
                previous = at;
                at = next;
                continue;
            }

            var layingStands = laying.StandsToM > section.MineFromM;
            var takenStands = taken.StandsToM > section.FromM;

            // <b>Neither gives way, and that is an outcome</b> (TER-4c.2, PHY-1). Two holds that cannot be
            // given back over one piece of the world are two bodies in one box, which is the collision
            // layer's question and not this one's — and read as "the one already there gives way" it is a
            // body cut out from under itself, which is the one shape the claims may never have.
            if (Firm(in laying, layingStands) && Firm(in taken, takenStands))
            {
                previous = at;
                at = next;
                continue;
            }

            if (YieldsAcross(in laying, layingStands, in taken, takenStands))
            {
                // In front of the crossing this stretch stops where it begins; over it, this stretch is
                // beaten at its own near edge and gives up the whole of itself — ground it could not have
                // reached without crossing ground it was refused (TER-4c.3).
                //
                // <b>Except the span between two stretches of one hold</b> (<paramref name="takingUpAgain"/>,
                // TER-5c.2), which takes up again past the crossing: those metres are not ground their holder
                // was refused, they are the middle of a hold whose two ends it has, and given up whole they
                // are a hole in it — road belonging to nobody with the same body on both sides.
                if (section.MineFromM > laying.FromM)
                {
                    laying = laying with { ToM = section.MineFromM };
                }
                else if (!layingStands && !takingUpAgain)
                {
                    return false;
                }
                else
                {
                    pastAnother = true;
                    laying = laying with { FromM = section.MineToM };
                }

                if (laying.ToM <= laying.FromM) return false;

                laying = laying with { StandsToM = Math.Clamp(laying.StandsToM, laying.FromM, laying.ToM) };
                previous = at;
                at = next;
                continue;
            }

            // And the same again for the one already there, on the crossed way's own metres: what it gives up
            // is the section, and a stretch left with no length at all goes out of that way's list.
            if (section.FromM > taken.FromM) taken = taken with { ToM = section.FromM };
            else if (!takenStands) taken = taken with { FromM = taken.ToM };
            else taken = taken with { FromM = section.ToM };

            if (taken.ToM > taken.FromM)
            {
                taken = taken with { StandsToM = Math.Clamp(taken.StandsToM, taken.FromM, taken.ToM) };
                previous = at;
                at = next;
                continue;
            }

            if (taken.IsGranted) _claimCount--;
            if (previous == NoSlot) _head[way] = next;
            else _next[previous] = next;

            at = next;
        }

        return true;
    }

    /// <summary>
    /// <b>Which of two claims over one crossing gives the ground up</b> — <see cref="Yields"/> asked of a pair
    /// on two ways, where the shared ground is the section and neither claim's metres mean anything in the
    /// other's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What cannot be given back is not taken</b> (TER-5e, <see cref="Binds"/>): a body standing on the
    /// crossing, and the road a body can no longer stop short of, which is the same p0 by the same fact. The
    /// ladder orders who waits and never who is driven into, so a claim at that rung beats one merely
    /// reaching over the ground whatever the other is claiming with — and <b>a claim ahead of a committed car
    /// carries no body at all</b>, which is exactly the hold this would otherwise hand to whoever crossed it.
    /// </para>
    /// <para>
    /// <b>Two of those over one piece of the world are two, and neither gives way.</b> That is a collision
    /// and not a question the claims have an opinion about (PHY-1): a hold cut out from under a body standing
    /// in it is a body the town cannot see, which is worse than the two the town can.
    /// </para>
    /// <para>
    /// <b>Below that it is the ladder</b> (TER-5g), and <b>a tie is settled by the holders and never by the
    /// walk</b>. On one way a tie can go to whoever is already there, because the pair is one list and one of
    /// them demonstrably is; across two there is no such fact, so an order that read the table's state would
    /// hand the ground to whichever claim happened to be laid first — which is the order dependency the whole
    /// arrangement is against (TER-4c.1). The roster and the occupant are arbitrary and they are the same
    /// arbitrary answer every tick, which is the whole of what is wanted: exactly one of the two holds it, and
    /// the other is cut and goes round again.
    /// </para>
    /// </remarks>
    static bool YieldsAcross(in LaneClaim laying, bool layingStands, in LaneClaim taken, bool takenStands)
    {
        var layingFirm = Firm(in laying, layingStands);
        var takenFirm = Firm(in taken, takenStands);
        if (layingFirm != takenFirm) return takenFirm;
        if (taken.Priority != laying.Priority) return taken.Priority < laying.Priority;

        return taken.Of != laying.Of ? taken.Of < laying.Of : taken.Occupant < laying.Occupant;
    }

    /// <summary>
    /// <b>Whether a claim's hold on a crossing is one nothing takes</b> (TER-5e): a body standing over it, or
    /// ground its holder can no longer give back, which is the same p0 by the same fact.
    /// </summary>
    static bool Firm(in LaneClaim claim, bool standsOnIt) =>
        standsOnIt || claim.Priority == ClaimPriority.Hard;

    /// <summary>
    /// <b>The ground made nobody else's on the way the stretch is laid on</b> — the claims of one way, which
    /// are one list and one interval apiece.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The incoming one is cut at the near edge of the first thing that beats it</b> and never split in
    /// two around it: one body is one stretch of one way (TER-5c.2), and every claim that reaches past
    /// something is a claim whose far end the grant was going to be cut at anyway. What that gives up is
    /// metres on the far side of ground its holder was refused, which it could not have reached without
    /// crossing them.
    /// </para>
    /// <para>
    /// <b>And one that is beaten from behind begins where that ground ends</b>, which is what makes a body's
    /// road and the stretch beyond it one piece (TER-5c.2): the claim a car lays over the box it is crossing
    /// is laid over its own road as well, and the seam is the metre that road actually reached rather than
    /// the same figure worked out a second time. <b>No road belonging to nobody is ever left between the
    /// two</b>: what the incoming one gives up here is metres somebody is already holding.
    /// </para>
    /// <para>
    /// <b>An existing claim is cut and never dropped</b>, so the pair still covers between them everything
    /// either of them covered — a metre that changes hands is still a metre the town can see somebody on.
    /// One left with no length goes out of the way's list, since an interval no query can tell from a point
    /// is not a hold.
    /// </para>
    /// </remarks>
    /// <param name="pastAnother">
    /// Whether the near edge was moved onto the far edge of somebody <em>else's</em> ground — which is what
    /// tells a run that ended from a run that met its own other stretch and carries on (<see cref="Lay"/>).
    /// </param>
    bool AlongTheWay(int way, ref LaneClaim laying, bool takingUpAgain, ref bool pastAnother)
    {
        var previous = NoSlot;
        for (var at = _head[way]; at != NoSlot;)
        {
            var next = _next[at];
            ref var taken = ref _slots[at];
            if (taken.IsRejected || taken.ToM <= laying.FromM || taken.FromM >= laying.ToM)
            {
                previous = at;
                at = next;
                continue;
            }

            if (Yields(in laying, in taken))
            {
                // In front of it, the incoming one stops where it starts; behind it, it begins where that
                // ground ends — and where that ground is somebody else's and nothing is standing in the one
                // being laid, it is not laid at all (TER-5c.2). A hold that begins past another body has the
                // metres between belonging to that body until it moves, and then to nobody.
                if (taken.FromM > laying.FromM)
                {
                    laying = laying with { ToM = taken.FromM };
                }
                else if (!laying.HasBody && !SameHolder(in laying, in taken) && !takingUpAgain)
                {
                    return false;
                }
                else
                {
                    pastAnother |= !SameHolder(in laying, in taken);
                    laying = laying with { FromM = taken.ToM };
                }

                if (laying.ToM <= laying.FromM) return false;

                laying = laying with { StandsToM = Math.Clamp(laying.StandsToM, laying.FromM, laying.ToM) };
                previous = at;
                at = next;
                continue;
            }

            // And the same again for the one already there, on the same terms: a bodiless stretch taken from
            // its own near edge by somebody else's ground is given up whole rather than pushed past it.
            if (laying.FromM > taken.FromM) taken = taken with { ToM = laying.FromM };
            else if (!taken.HasBody && !SameHolder(in taken, in laying)) taken = taken with { FromM = taken.ToM };
            else taken = taken with { FromM = laying.ToM };

            if (taken.ToM > taken.FromM)
            {
                taken = taken with { StandsToM = Math.Clamp(taken.StandsToM, taken.FromM, taken.ToM) };
                previous = at;
                at = next;
                continue;
            }

            if (taken.IsGranted) _claimCount--;
            if (previous == NoSlot) _head[way] = next;
            else _next[previous] = next;

            at = next;
        }

        return laying.ToM > laying.FromM;
    }

    /// <summary>
    /// <b>Whether two stretches are one body's</b> — the same occupant of the same roster, and never the
    /// town's own furniture, which is a claim apiece under one name and no body at all (TER-4c).
    /// </summary>
    /// <remarks>
    /// <b>It is what tells a seam from a hole</b> (TER-5c.2). A stretch that begins where its own holder's
    /// other one ends is one piece of road said in two claims — a car's road and the box beyond it. One that
    /// begins where somebody else's ends is a hold with that body's ground inside it, and there is nothing in
    /// those metres for a reader to be cut at once that body has moved on.
    /// </remarks>
    static bool SameHolder(in LaneClaim one, in LaneClaim other) =>
        !one.IsFurniture && one.Occupant == other.Occupant && one.Of == other.Of;

    /// <summary>
    /// <b>Which of two claims over one piece of a way gives it up</b> — asked of the ground they share and
    /// not of either claim as a whole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A body standing on the metres beats one merely reaching over them</b>, whatever either is claiming
    /// with. That is what makes the answer the same whichever of a queue is laid first: a follower's road
    /// reaches over the body in front, the body in front reaches over nothing of the follower's, and the
    /// road is cut at the body either way round. Decided on the claims as wholes instead, whichever car went
    /// into the table first kept the ground and the other's own body was cut out from under it.
    /// </para>
    /// <para>
    /// <b>Between two bodies it is whichever is further back that gives way</b>, which is the same answer
    /// the grant itself arrives at — a follower is cut at the near edge of the body in front, and the body in
    /// front keeps every metre of its own. Read off the rungs instead, two bodies of one rung were
    /// settled by which went into the table first, and a car close enough behind for its nose to reach into
    /// the margin the leader keeps could take the leader's own ground out from under it.
    /// </para>
    /// <para>
    /// <b>Below that it is the ladder</b> (TER-5g), and <b>a tie goes to whoever is already there</b>: two
    /// bodies genuinely abreast of one another on one way are one stretch and one seam, and which of them
    /// holds the seam is not a fact the town has an opinion about — only that exactly one of them does.
    /// </para>
    /// </remarks>
    static bool Yields(in LaneClaim laying, in LaneClaim taken)
    {
        var fromM = MathF.Max(laying.FromM, taken.FromM);
        var toM = MathF.Min(laying.ToM, taken.ToM);

        var layingStands = laying.StandsToM > fromM && laying.FromM < toM;
        var takenStands = taken.StandsToM > fromM && taken.FromM < toM;
        if (layingStands != takenStands) return takenStands;
        if (layingStands && laying.FromM != taken.FromM) return laying.FromM < taken.FromM;

        return taken.Priority <= laying.Priority;
    }

    /// <summary>
    /// <b>Whether this occupant's own body already covers ground of this way that these metres run over</b> —
    /// the one question a body laid from more than one place has to ask before it lays again (TER-5c.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One body is one stretch of one way, and a body read from both ends of the sweep it is committed to is
    /// one stretch read twice: laid regardless, every walk of the way counts it as two occupants and the
    /// overlay draws two washes over one piece of ground. It is the same question a driver's own footprint
    /// asks of the claim it has already laid on the ways of its line.
    /// </para>
    /// <para>
    /// <b>Bodies and never ground nobody has reached.</b> Ground a car has been granted across a box is not
    /// ground its body is standing on — and answered off one of those, a car sitting in the box it was
    /// granted would decline to write the one row that says it is there.
    /// </para>
    /// </remarks>
    public bool AlreadyHolds(
        int way, float fromM, float toM, int occupant, LaneRoster of = LaneRoster.Driving)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var claim = ref _slots[at];
            if (claim.Occupant != occupant || claim.Of != of || !claim.HasBody) continue;
            if (claim.ToM > fromM && claim.FromM < toM) return true;
        }

        return false;
    }
}
