namespace TrafficSimulation.World.Road;


/// <summary>
/// <b>How strong a claim on ground its holder has</b> (TER-5e) — carried by every stretch, and the whole
/// of what says which of two bodies coming to one piece of the world gives it up.
/// </summary>
/// <remarks>
/// <para>
/// <b>It settles claims of one strength and never bodies</b> (<see cref="ClaimPriority"/>). What a greater
/// right of way takes is ground nobody has reached, and never a body nor the road a body is committed to
/// being able to stop in. A right of way is a rule about who waits.
/// </para>
/// <para>
/// <b>It is a fact about the way and not about the body on it.</b> A junction's every movement is a way of
/// its own, so the claim on the left-turn connector is a left turn by the ground it is on; two cars on one
/// lane are ordinary traffic alike and are held apart by the road each was granted, neither giving way to
/// the other.
/// </para>
/// <para>
/// <b>The zero of it is the weakest movement a box admits and not the middle</b>, because the order has to
/// run one way and a byte starts at nothing. <b>Asking at the zero is asking with no rank at all</b>, which
/// is what a walker and a template do.
/// </para>
/// </remarks>
internal enum RightOfWay : byte
{
    /// <summary>
    /// The turn across the oncoming stream (TER-4a) — the weakest there is, no box admitting a movement
    /// that reverses the direction of travel (TER-5f).
    /// </summary>
    TurningAcross,

    /// <summary>Ordinary traffic: the near-side turn, and every stretch of way that is not a movement through a box.</summary>
    Traffic,

    /// <summary>Straight through, which turns out of nobody's way.</summary>
    StraightOn,

    /// <summary>A body on a crossing's paint, which is what the paint is for.</summary>
    OnThePaint,

    /// <summary>
    /// <b>A road an officer has closed</b> (SRV-6). It outranks every ordinary movement and the paint, so
    /// traffic is held short of the ground it is laid on — and it is <em>below</em>
    /// <see cref="Emergency"/>, which is the whole of what "the other services are let through" means: a
    /// vehicle answering a call is not refused by it and needs to know nothing about why.
    /// </summary>
    /// <remarks>
    /// <b>It is a rank and not a kind of claim.</b> What an officer holds is ground it has been granted and
    /// not reached (<see cref="ClaimPriority.Firm"/>) like any other, so nothing reading the claims learns a
    /// new word — and a closure cannot take a body or the road a body is committed to stopping in.
    /// </remarks>
    Closed,

    /// <summary>
    /// <b>An ambulance answering a call</b> (AMB-4). It outranks every ordinary movement and the paint
    /// alike, which is the whole of what "every other agent gives way" means here: ground an ambulance
    /// asks for stops being anybody else's to claim.
    /// </summary>
    /// <remarks>
    /// <b>It is still only a rank and takes only what a rank may take</b> — a claim, which its holder has
    /// not reached and can give back. A body, and the road a body is committed to being able to stop in,
    /// are no more an ambulance's than anybody's: a blue light buys the road and never the tyres, and a
    /// rule that took those would be a licence to drive into somebody.
    /// </remarks>
    Emergency,

    /// <summary>
    /// <b>Ground its holder can no longer give back</b>: a body past the point it could stop short of it.
    /// Nothing outranks it, because a right of way is a rule about who waits and not about who is driven
    /// into.
    /// </summary>
    Committed,
}

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
/// <b>How strong the claim is</b> (TER-5g): whether it can be taken, and by whom. The one property besides
/// who and where, and the whole of what the ladder decides — which of two claims of one strength wins is
/// the right of way of the movement each is on.
/// </param>
/// <param name="Of">Which of the town's two rosters <paramref name="Occupant"/> is an index into.</param>
/// <param name="Right">
/// <b>The right of way its holder has to it</b> (TER-5e), which is what decides between two claims of one
/// strength. It belongs to the claim and not to the body: one car is straight through on the lane it is
/// leaving and a turn across the oncoming stream on the join it is entering, and those are two claims on two
/// ways.
/// </param>
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
/// <b>And what a walker may step round</b> (PER-24): a body going down the walk is one to wait behind, and
/// a body merely lying across it is one to walk round.
/// </para>
/// <para>
/// It is meaningless where nothing is standing in the claim, and readers ask it only together with the body
/// edge.
/// </para>
/// </param>
internal readonly record struct LaneClaim(
    float FromM, float ToM, float StandsToM, float AlongMps, int Occupant, ClaimPriority Priority,
    LaneRoster Of = LaneRoster.Driving, RightOfWay Right = RightOfWay.Traffic, float AcrossFromM = 0f,
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
    /// one shoved off its line, one under a hand. <b>What a walker steps round</b> (PER-24), and what the
    /// town's own furniture is not — a prop is nobody's body and is walked round by its own geometry.
    /// </summary>
    public bool IsLoose => HasBody && !OnItsLine && !IsFurniture;

    /// <summary>
    /// <b>Ground its holder has been granted and not reached</b> (<see cref="ClaimPriority.Firm"/> and
    /// above): the far end of a box, a bay being backed out of, a swerve about to cross, a road an officer
    /// is holding. <b>The one hold a caller can both lay and take back inside a tick</b>, which is what the
    /// count of them is for.
    /// </summary>
    public bool IsGranted => !HasBody && Priority <= ClaimPriority.Firm;

    /// <summary>
    /// <b>Road its holder has stated it means to use and has not reached</b>
    /// (<see cref="ClaimPriority.Soft"/>, TER-5g) — nothing is standing in it and nothing has been granted
    /// it; it is a statement of where a body is going.
    /// </summary>
    public bool IsStated => Priority == ClaimPriority.Soft;

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
/// <b>Ground of two kinds laid over one another is where a car is the exception</b> (TER-5c.1). A zebra is carriageway a
/// walk runs over, so a car on the paint writes the stretch of its own lane and nothing at all on the walk,
/// and what holds a walker off it is that same stretch, looked up where the crossing runs over the lane.
/// Written in both, one car would hold one piece of ground twice over, in two records free to disagree about
/// who has it.
/// </para>
/// <para>
/// <b>A body on foot on the paint writes both ways, because the look-up above is not asked about it.</b> What
/// a walker asks the road is what traffic is <em>coming</em> (<see cref="AnyTrafficOver"/>), and it asks it of
/// the one band it is about to step into — so a person standing on a crossing that was written on the lane
/// alone was a person no other walker could see. It is the same two writes a walker crossing under its own
/// power has always made: the bands of the lanes it is on, and its stretch of the crossing way it is walking.
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
    public LaneOccupancy(TownWays ways, int mostSlots)
    {
        _ways = ways;

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
    /// its callers want: a movement is laid as one claim per run of its join
    /// (<c>TownWorld.LayTheMovement</c>) and is given up whole. So <b>an occupant may not hold two
    /// independent claims of one priority on one way</b> — the town's two granted claims are told apart by
    /// the ways they are laid on, a manoeuvre's on a lane (<c>ManeuverDesk.TakeTheSwervesGround</c>) and a
    /// movement's on a junction's join or a bay's way out, and nothing may put both on one number without
    /// giving this an interval to take.
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
        LaneRoster of = LaneRoster.Driving)
    {
        var previous = NoSlot;
        for (var at = _head[way]; at != NoSlot;)
        {
            var next = _next[at];
            ref var slot = ref _slots[at];
            if (slot.Occupant != occupant || slot.Of != of || !Counts(slot, asked))
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
    /// <b>Ground its holder has been granted or has stated and is not on</b>: the far end of a box a car
    /// has committed to crossing, a bay being backed out of, a swerve about to cross, a road an officer is
    /// holding, the stretch a driver means to use beyond its own, the band a walker at a kerb was refused.
    /// <b>Nothing is standing in it</b>, which is exactly why a reading taken off the bodies alone lets two
    /// bodies take it at once.
    /// </summary>
    /// <remarks>
    /// <b>It is on a way its holder is going to be on</b>, like every other claim, and never a mark
    /// left on somebody else's road (TER-5c.1).
    /// </remarks>
    public bool ClaimAhead(
        int way, float fromM, float toM, float alongMps, int occupant, ClaimPriority priority,
        LaneRoster of = LaneRoster.Driving, RightOfWay right = RightOfWay.Traffic, float acrossFromM = 0f,
        float acrossToM = 0f) =>
        Lay(way, fromM, fromM, toM, alongMps, occupant, priority, of, right, acrossFromM, acrossToM, onItsLine: true);

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
    /// holder is not driving down <em>this</em> way — which is what a walker may step round (PER-24).
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
        RightOfWay right = RightOfWay.Traffic, float acrossFromM = 0f, float acrossToM = 0f) =>
        Lay(way, fromM, standsToM, toM, alongMps, occupant, priority, of, right, acrossFromM, acrossToM, onItsLine: false);

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
        RightOfWay right = RightOfWay.Traffic, float acrossFromM = 0f, float acrossToM = 0f) =>
        Lay(way, fromM, standsToM, toM, alongMps, occupant, priority, of, right, acrossFromM, acrossToM, onItsLine: true);

    /// <summary>
    /// The insertion all three of the above are, <b>taking the three edges in the order they lie on the
    /// way</b> — near, body, far. They are three floats and nothing but the order tells them apart, so there
    /// is one order and every caller writes it. <b>Returns whether any of it was laid</b>: past the bound, off
    /// the end of the way, or wholly over ground somebody else already holds, it is not.
    /// </summary>
    /// <remarks>
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
        ClaimPriority priority, LaneRoster of, RightOfWay right, float acrossFromM, float acrossToM,
        bool onItsLine)
    {
        if (toM < fromM) return false;
        if (toM <= 0f || fromM >= _ways.LengthM(way)) return false;

        var laying = new LaneClaim(
            fromM, toM, Math.Clamp(standsToM, fromM, toM), alongMps, occupant, priority, of, right,
            acrossFromM, acrossToM, onItsLine);

        return MakeRoomFor(way, ref laying) && Insert(way, in laying, laying.FromM, laying.ToM);
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
    /// <para>
    /// <b>The incoming one is cut at the near edge of the first thing that beats it</b> and never split in
    /// two around it: one body is one stretch of one way (TER-5c.2), and every claim that reaches past
    /// something is a claim whose far end the grant was going to be cut at anyway. What that gives up is
    /// metres on the far side of ground its holder was refused, which it could not have reached without
    /// crossing them.
    /// </para>
    /// <para>
    /// <b>An existing claim is cut and never dropped</b>, so the pair still covers between them everything
    /// either of them covered — a metre that changes hands is still a metre the town can see somebody on.
    /// One left with no length goes out of the way's list, since an interval no query can tell from a point
    /// is not a hold.
    /// </para>
    /// </remarks>
    bool MakeRoomFor(int way, ref LaneClaim laying)
    {
        // <b>A refused ask is a mark and not a hold</b> (TER-5g): it is nobody's ground, it binds nobody and
        // it cuts nothing, so it neither takes metres from a claim nor gives any up to one. It is laid on
        // the very ground the traffic holds — that is the whole of what it is for, since what it says is
        // that somebody is waiting for exactly those metres.
        if (laying.IsRejected) return true;

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
                // Behind it, the incoming one starts where it ends; in front of it, it stops where it starts.
                if (taken.FromM <= laying.FromM) laying = laying with { FromM = taken.ToM };
                else laying = laying with { ToM = taken.FromM };

                if (laying.ToM <= laying.FromM) return false;

                laying = laying with { StandsToM = Math.Clamp(laying.StandsToM, laying.FromM, laying.ToM) };
                previous = at;
                at = next;
                continue;
            }

            if (laying.FromM <= taken.FromM) taken = taken with { FromM = laying.ToM };
            else taken = taken with { ToM = laying.FromM };

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
    /// front keeps every metre of its own. Read off the priorities instead, two bodies of one rank were
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
