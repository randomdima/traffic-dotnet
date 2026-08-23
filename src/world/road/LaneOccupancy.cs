namespace TrafficSimulation.World.Road;

/// <summary>
/// What one occupant of a way is doing on it, which is the whole reason the index exists: <b>a driver
/// follows what is going where it is going and gets past everything else</b>, and the two are told apart
/// here rather than guessed at from a speed.
/// </summary>
internal enum LaneUse : byte
{
    /// <summary>
    /// <b>A live body under way and the road it has taken, which are one stretch</b>: from the margin it
    /// keeps behind its own tail (TER-5c.2) to where it plans to be able to stop. Every driver under way
    /// lays one and so does every walker, and what holds one off the next is that nobody is granted ground
    /// somebody else will still be standing on once they have stopped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the one use that carries a margin</b>, because it is the one that belongs to a body that is
    /// still moving: a stretch is one interval of a way's arclength and the width of the body and the slack
    /// in its line were thrown away to make it. So a grant cut at one of these is cut where the ground
    /// begins, and a grant cut at anything else — a wreck, a claim, somebody on foot, the town's furniture —
    /// has the asker's own margin taken off it instead.
    /// </para>
    /// <para>
    /// <b>It is anchored at the body, so the occupants of a way come out in the order they are actually
    /// in</b>, which is what a grant has to be taken against: a stretch measured from where a car will
    /// have <em>stopped</em> can reach past a slower car in front, and a driver cut at one of those would
    /// be a driver held up by the car behind it. What the ground beyond a car's tail is worth to whoever
    /// is following is that car's own stopping distance, read off <see cref="LaneSlot.AlongMps"/>.
    /// </para>
    /// <para>
    /// The body inside it is <see cref="LaneSlot.StandsToM"/>: <b>the only use whose two far edges differ</b>.
    /// </para>
    /// </remarks>
    Reserved,

    /// <summary>
    /// Anything else standing on the way — a wreck, a car with nobody in it, a body shoved off its line,
    /// a manoeuvre laid across the lane. <b>Not a queue</b>, whatever it is doing.
    /// </summary>
    /// <remarks>
    /// <b>A body on a template of its own is laid over the whole sweep that template has still to make</b>
    /// and not over the pose it is passing through: the ground a manoeuvre is about to be on is ground it is
    /// holding, and a straight walked clear at the moment it was drawn is a straight somebody else may come
    /// to rest in while it is being driven.
    /// </remarks>
    Obstruction,

    /// <summary>
    /// Ground somebody has claimed and is about to be on: a bay being backed out of, a swerve about to
    /// cross, the far end of the box a car has committed to crossing. It is empty <em>now</em>, which is
    /// exactly why a reading taken off the bodies alone lets two cars take it at once.
    /// </summary>
    /// <remarks>
    /// <b>It is on a way its holder is going to be on</b>, like everything else in the book. A claim is what
    /// a body writes where its own reservation has not reached yet, and never a mark left on somebody else's
    /// road (TER-5c.1).
    /// </remarks>
    Claimed,

    /// <summary>
    /// <b>Ground on the carriageway a person is on or has been granted</b> — the band of a lane a crossing
    /// is painted across while somebody is walking over it or has been granted the next one (`PER-15`), and
    /// the stretch under a body standing in a lane where nothing is painted at all. The one use whose
    /// occupant is a walker and not a car.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is in every query a driver asks and it is not an <see cref="Obstruction"/>, and both halves of
    /// that matter.</b> A walker in no query is a walker no grant is cut at, which is a car granted the road
    /// through a body standing in it; a walker read as an obstruction is a walker nothing could tell from a
    /// wreck, and the two are not the same thing to a person deciding what to do about them. So it cuts road
    /// like anything else on the lane and reports itself as <c>HeadwayKind.Walker</c> — its own name, which
    /// is waited behind while it is moving and gone round once it has stopped.
    /// </para>
    /// <para>
    /// <b>What it is not is traffic.</b> A walker asking what is coming down a lane (`P-3`) and a driver
    /// asking what has come to rest in the mouth of its bay are both asking about cars, and neither is
    /// answered by another person on the road — which is why the queries about traffic name
    /// <see cref="Traffic"/> rather than <see cref="Bodies"/>.
    /// </para>
    /// </remarks>
    OnFoot,
}

/// <summary>
/// <b>Which roster a stretch's occupant is named in.</b> The road's book is not one roster's — a person in
/// a lane is on the road like anything else — so an occupant is an integer into one of two fleets, and
/// <b>which one is carried rather than inferred</b>.
/// </summary>
/// <remarks>
/// It was inferred once, from the book the stretch was in, and the two rosters were told apart by an
/// integer: a walker's index read out of the car fleet is whichever car happens to hold that number.
/// </remarks>
internal enum LaneRoster : byte
{
    Driving,

    Walking,
}

/// <summary>
/// One occupant's stretch of one way, in the way's own metres. <b>Two numbers, and both of them are
/// distances along the bending ground</b>: a way is a chain of arcs, its metres are that chain's own
/// arclength, and a stretch of it is an interval of that. Nothing here is a chord and no shape is lost —
/// what the pair leaves out is the width of the way and the pose of the body on it, never the curve.
/// </summary>
/// <param name="FromM">The near edge, measured the way the way is driven.</param>
/// <param name="ToM">The far edge. Never less than <paramref name="FromM"/>.</param>
/// <param name="StandsToM">
/// <b>Where the body itself ends</b>, as against how far the ground it has taken reaches — the two far
/// edges of one stretch (<see cref="LaneUse.Reserved"/>). It is <paramref name="ToM"/> for everything that
/// is only a body, so a question about where somebody <em>is</em> reads this edge on every slot alike and
/// a question about what ground is <em>spoken for</em> reads <paramref name="ToM"/>.
/// <para>
/// <b>It is also what tells in front from behind.</b> A stretch begins a margin behind its owner and is
/// clipped at the start of every way it runs onto, so near edges are not the bodies' order; this is.
/// </para>
/// </param>
/// <param name="AlongMps">How fast the occupant is going <em>along this way</em> — negative where it faces the other way.</param>
/// <param name="Occupant">Whatever the caller names an occupant by, which for this town is the body's own index in <paramref name="Of"/>.</param>
/// <param name="Of">Which of the town's two rosters <paramref name="Occupant"/> is an index into.</param>
internal readonly record struct LaneSlot(
    float FromM, float ToM, float StandsToM, float AlongMps, int Occupant, LaneUse Use,
    LaneRoster Of = LaneRoster.Driving)
{
    public static LaneSlot Nothing => new(
        float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, 0f, LaneOccupancy.Nobody,
        LaneUse.Obstruction);

    public bool Found => float.IsFinite(FromM);
}

/// <summary>
/// <b>Who is on each way, as intervals of the way's own arclength</b> — and, by TER-4c, <b>the whole of
/// what an agent looks at</b>. A ray finds a shape; what a driver needs to know is whether that shape is
/// somebody going where it is going, and the book is what knows.
/// </summary>
/// <remarks>
/// <para>
/// <b>A way is a lane or the join across a junction</b>, because the ground inside a disc belongs to no
/// lane (<see cref="RoadGraph"/>) and a car crossing it is exactly the car the one behind must not lose
/// sight of. The joins are indexed as ways of their own, numbered after the lanes, so the walk from one
/// lane to the next passes through the box rather than over it.
/// </para>
/// <para>
/// <b>The pavement is the same book over its own ways</b>, and the town keeps a second one of these for it:
/// a stretch of footway has a lane each way down it and a mitre between each pair, which are a lane and a
/// join in every sense this file has of the words. The two books are separate because the two networks
/// are — and <b>a body writes into the book of the network it is on and never into the other</b>
/// (TER-5c.1). A person standing on the carriageway is a stretch of the lane it stands in
/// (<see cref="LaneUse.OnFoot"/>) and cuts the road a driver is granted exactly as a car would, because a
/// body in a lane is on the road however it got there; a car driving over a zebra writes nothing on the
/// walk, and what holds a walker off it is that same car's stretch of the lane, looked up where the
/// crossing runs over it. <b>Which network the ground belongs to is what tells the two books apart, and
/// never which kind of body is standing on it.</b>
/// </para>
/// <para>
/// <b>It is rebuilt from the bodies every tick and never written to during a decision</b>, which is what
/// makes it an index rather than a register. Nothing has to be released, nothing can leak, a wreck drops
/// out of it the tick it stops being driven, and two cars deciding on different ticks read the same book.
/// The forward-looking things in it — a <see cref="LaneUse.Claimed"/> stretch, a
/// <see cref="LaneUse.Reserved"/> one — are re-laid from the car's own state every tick for exactly the
/// same reason.
/// </para>
/// <para>
/// <b>One body is one stretch of one way, and never two of them.</b> Where a body is and how much road it
/// has taken are the same fact read to two different edges (<see cref="LaneSlot.StandsToM"/>), so a body
/// cannot appear twice on a way, cannot overlap itself, and cannot be cut at its own other half.
/// </para>
/// <para>
/// <b>A reservation is granted by where it starts and not by who asks first</b> (TER-4c.1). The near edge
/// of one is a body's own tail, less the margin it keeps there, so every ask is laid before anybody is
/// granted anything and what a car is then granted is its own stretch cut at the nearest one in front of it. Two cars therefore need no order
/// to be resolved in, and the answer is the same whichever of them is asked first. <b>Ground nothing of the
/// asker's own reaches is a <see cref="LaneUse.Claimed"/> stretch instead</b>, and that one is checked
/// against the book before it is laid, because there is no tail to anchor the answer to.
/// </para>
/// <para>
/// <b>What comes back is the asker's to move into.</b> The book is not a queue of intentions: a stretch
/// granted is a stretch nobody else can be granted, so the holder needs no second permission from anything
/// and asks for none — and whoever comes to that ground later is the one that gives way.
/// </para>
/// <para>
/// <b>It answers and never decides</b> (SIM-7). Nothing here is a permission the road withholds: what
/// holds a car off the traffic in front of it is the speed profile, working on a grant that is a distance
/// and not a verdict, and where a caller does turn an answer into a refusal — a claim not taken, a
/// crossing not begun — the refusal is that caller's one gate and the book is only what it read.
/// </para>
/// <para>
/// <b>Occupants are held as bucket lists and not as sorted arrays.</b> A town has ten thousand ways and a
/// few hundred cars, so a rebuild that touched every way would cost two orders of magnitude more than the
/// bodies it is describing; only the ways somebody is on are touched at all.
/// </para>
/// </remarks>
internal sealed class LaneOccupancy
{
    public const int Nobody = -1;

    const int NoSlot = -1;

    readonly int _laneCount;
    readonly float[] _lengthM;

    /// <summary>The first slot on each way, or <see cref="NoSlot"/>. Only the ways in <see cref="_touched"/> are ever stale.</summary>
    readonly int[] _head;

    /// <summary>The next slot on the same way, ascending by <see cref="LaneSlot.FromM"/>.</summary>
    readonly int[] _next;

    readonly LaneSlot[] _slots;

    /// <summary>Which ways got a slot this tick, so a rebuild resets those heads and no others.</summary>
    readonly int[] _touched;

    int _slotCount;
    int _touchedCount;
    int _claimCount;

    /// <param name="mostSlots">
    /// How many stretches the town may hold at once. <b>A bound on the work and not a figure behaviour
    /// reads</b> — and, since the book is the whole of what a driver looks at, a bound that must never
    /// actually be reached: a dropped stretch is a body nobody's grant is cut at. It is sized from the two
    /// rosters and the town's own furniture, and the gates hold it clear of its own ceiling.
    /// </param>
    public LaneOccupancy(RoadGraph roads, int mostSlots)
        : this(roads.LaneCount, roads.LaneCount + roads.TurnCount, mostSlots)
    {
        for (var lane = 0; lane < _laneCount; lane++) _lengthM[lane] = roads.LaneLengthM[lane];
        for (var turn = 0; turn < roads.TurnCount; turn++) _lengthM[_laneCount + turn] = roads.JoinLengthM(turn);
    }

    /// <summary>
    /// A book over ways the caller measures for itself — <b>the pavement's</b>, whose lanes are the two
    /// sides of every stretch and whose joins are the mitres between them. The numbering is the same one
    /// <see cref="WayOfLane"/> and <see cref="WayOfTurn"/> hand out, so nothing downstream can tell the two
    /// books apart.
    /// </summary>
    public LaneOccupancy(ReadOnlySpan<float> laneLengthM, ReadOnlySpan<float> joinLengthM, int mostSlots)
        : this(laneLengthM.Length, laneLengthM.Length + joinLengthM.Length, mostSlots)
    {
        laneLengthM.CopyTo(_lengthM);
        joinLengthM.CopyTo(_lengthM.AsSpan(laneLengthM.Length));
    }

    LaneOccupancy(int laneCount, int ways, int mostSlots)
    {
        _laneCount = laneCount;
        _lengthM = new float[ways];

        _head = new int[ways];
        Array.Fill(_head, NoSlot);
        _next = new int[mostSlots];
        _slots = new LaneSlot[mostSlots];
        _touched = new int[mostSlots];
    }

    public int WayCount => _lengthM.Length;

    /// <summary>How many stretches the last rebuild laid, which is what says whether the bound was reached.</summary>
    public int SlotCount => _slotCount;

    public int Capacity => _slots.Length;

    /// <summary>
    /// How many stretches anybody has claimed — a figure for the tests and the instruments, and read by no
    /// decision. Claims are the one use a caller can both lay and take back inside a tick
    /// (<see cref="Withdraw"/>), so a count that does not come back to nothing when the holders let go is
    /// how a leak shows.
    /// </summary>
    public int ClaimCount => _claimCount;

    /// <summary>A lane's own way number. The lanes are numbered first so that a lane and its way are the same integer.</summary>
    public int WayOfLane(int lane) => lane;

    /// <summary>The way a junction's join is, named by the turn slot the graph gave it.</summary>
    public int WayOfTurn(int turnSlot) => _laneCount + turnSlot;

    /// <summary>Whether a way is one of the town's lanes rather than a junction's join.</summary>
    public bool WayIsLane(int way) => way < _laneCount;

    /// <summary>The lane or the turn slot a way stands for, told apart by <see cref="WayIsLane"/>.</summary>
    /// <remarks>The trip back from a way number, so that the numbering is known here and nowhere else.</remarks>
    public int WayIndex(int way) => way < _laneCount ? way : way - _laneCount;

    /// <summary>
    /// The ways somebody is on, in no order. <b>A reader that wants the whole book walks this and not the
    /// town</b>: a town has ten thousand ways and a few hundred occupants.
    /// </summary>
    public ReadOnlySpan<int> OccupiedWays => _touched.AsSpan(0, _touchedCount);

    public float WayLengthM(int way) => _lengthM[way];

    /// <summary>Everything laid last tick is dropped. Nothing survives a rebuild, which is the whole guarantee.</summary>
    public void Begin()
    {
        for (var index = 0; index < _touchedCount; index++) _head[_touched[index]] = NoSlot;

        _touchedCount = 0;
        _slotCount = 0;
        _claimCount = 0;
    }

    /// <summary>
    /// <b>One occupant's stretches of one way, taken back inside the tick that laid them</b> — what a car
    /// that has stopped wanting ground it reserved does, so that nothing later in the same walk is refused
    /// road whose holder has already let go of it.
    /// </summary>
    /// <remarks>
    /// The slot itself is left where it is rather than compacted out: the book is rebuilt from nothing
    /// every tick, so the only cost of a hole is the room it takes until then, and moving one would
    /// invalidate every index a walk in progress is holding.
    /// </remarks>
    public void Withdraw(int way, int occupant, LaneUse use, LaneRoster of = LaneRoster.Driving)
    {
        var previous = NoSlot;
        for (var at = _head[way]; at != NoSlot;)
        {
            var next = _next[at];
            if (_slots[at].Occupant == occupant && _slots[at].Use == use && _slots[at].Of == of)
            {
                if (previous == NoSlot) _head[way] = next;
                else _next[previous] = next;

                if (use == LaneUse.Claimed) _claimCount--;
            }
            else
            {
                previous = at;
            }

            at = next;
        }
    }

    /// <summary>
    /// One occupant's stretch of one way, inserted in its place. <b>Returns whether it was laid</b>: past
    /// the bound it is not, and the caller's own geometry is what covers the gap.
    /// </summary>
    /// <remarks>Body and ground are the same edge here — a thing that has taken road beyond itself is <see cref="AddUnderWay"/>.</remarks>
    public bool Add(
        int way, float fromM, float toM, float alongMps, int occupant, LaneUse use,
        LaneRoster of = LaneRoster.Driving) =>
        Add(way, fromM, toM, toM, alongMps, occupant, use, of);

    /// <summary>
    /// <b>A body under way, as the one stretch it is</b>: from its own tail, through where the body itself
    /// ends (<paramref name="standsToM"/>), to the far end of the ground it has taken.
    /// </summary>
    public bool AddUnderWay(
        int way, float fromM, float standsToM, float toM, float alongMps, int occupant,
        LaneRoster of = LaneRoster.Driving) =>
        Add(way, fromM, toM, standsToM, alongMps, occupant, LaneUse.Reserved, of);

    bool Add(
        int way, float fromM, float toM, float standsToM, float alongMps, int occupant, LaneUse use,
        LaneRoster of)
    {
        if (_slotCount == _slots.Length) return false;
        if (toM < fromM) return false;
        if (toM <= 0f || fromM >= _lengthM[way]) return false;

        var slot = _slotCount++;
        _slots[slot] = new LaneSlot(fromM, toM, Math.Clamp(standsToM, fromM, toM), alongMps, occupant, use, of);
        if (use == LaneUse.Claimed) _claimCount++;

        if (_head[way] == NoSlot)
        {
            _touched[_touchedCount++] = way;
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
    /// <b>Whether this occupant already holds ground of this way that these metres run over</b> — the one
    /// question a body laid from more than one place has to ask before it lays again (TER-5c.2).
    /// </summary>
    /// <remarks>
    /// One body is one stretch of one way, and a body read from both ends of the sweep it is committed to is
    /// one stretch read twice: laid regardless, every walk of the way counts it as two occupants and the
    /// overlay draws two washes over one piece of ground.
    /// </remarks>
    public bool AlreadyHolds(
        int way, float fromM, float toM, int occupant, LaneRoster of = LaneRoster.Driving)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.Occupant == occupant && slot.Of == of && slot.ToM > fromM && slot.FromM < toM) return true;
        }

        return false;
    }

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
    /// <b>Whether any traffic has this stretch of the way</b>: a road a driver has taken, ground somebody
    /// has claimed, a body with no reservation to its name. The all-or-nothing question — <em>is this piece
    /// of ground anybody's</em> — as against <see cref="NextSpokenFor"/>'s "how much of it is mine".
    /// </summary>
    /// <remarks>
    /// <b>Traffic and not everything spoken for</b>: the asker is somebody about to step into the road, and
    /// another person already standing in it is neither a reason to stay on the pavement nor something a
    /// grant is taken against.
    /// </remarks>
    public bool AnyTrafficOver(int way, float fromM, float toM) =>
        AnythingOver(way, fromM, toM, Nobody, LaneRoster.Driving, TrafficSpoken, out _);

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
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf, int uses, out LaneSlot found)
    {
        var at = FromTheStart;
        return NextOver(way, fromM, toM, excluding, excludingOf, uses, ref at, out found);
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
        NextOver(way, fromM, toM, excluding, excludingOf, Spoken, ref at, out found);

    bool NextOver(
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf, int uses, ref int at,
        out LaneSlot found)
    {
        for (at = at == FromTheStart ? _head[way] : _next[at]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > toM) break;
            if (Is(slot, excluding, excludingOf) || (Only(slot.Use) & uses) == 0 || slot.ToM < fromM) continue;

            found = slot;
            return true;
        }

        found = LaneSlot.Nothing;
        return false;
    }

    /// <summary>
    /// <b>Whether anybody is on foot over this stretch of the way</b> — the one question the road's book is
    /// asked about walkers, and the whole of `P-12`'s "is there somebody on the paint".
    /// </summary>
    /// <remarks>
    /// It excludes nobody, because the asker is a car and the occupants of these stretches are walkers:
    /// the two are indexed separately and no query but this one ever reads a
    /// <see cref="LaneUse.OnFoot"/> slot, which is what keeps the two rosters from being told apart by
    /// an integer.
    /// </remarks>
    public bool AnybodyOnFoot(int way, float fromM, float toM)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > toM) break;
            if (slot.Use != LaneUse.OnFoot || slot.ToM < fromM) continue;

            return true;
        }

        return false;
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
        LaneRoster excludingOf = LaneRoster.Driving)
    {
        for (at = at == FromTheStart ? _head[way] : _next[at]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > untilM) break;
            if (Is(slot, excluding, excludingOf) || slot.StandsToM < fromM) continue;
            if ((Only(slot.Use) & Spoken) == 0) continue;

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
    /// Where a body actually is, of whichever roster: traffic, and anybody on foot in the lane. <b>Read to
    /// <see cref="LaneSlot.StandsToM"/></b>, which is what tells a body apart from the road it has taken now
    /// that the two are one stretch.
    /// </summary>
    const int Bodies = Traffic | (1 << (int)LaneUse.OnFoot);

    /// <summary>
    /// Ground traffic has spoken for: reserved, claimed, or held by something with no reservation to its
    /// name. The same set as <see cref="Traffic"/> but for a claim, and read to <see cref="LaneSlot.ToM"/>.
    /// </summary>
    const int TrafficSpoken = Traffic | (1 << (int)LaneUse.Claimed);

    /// <summary>Ground anybody has spoken for, which is that and everybody on foot in the lane.</summary>
    const int Spoken = TrafficSpoken | (1 << (int)LaneUse.OnFoot);

    /// <summary>
    /// Whether a stretch is the asker's own. <b>Both halves, always</b>: an occupant is an index into one of
    /// two fleets, so a car excluding itself by number would otherwise also exclude the walker that happens
    /// to hold the same number.
    /// </summary>
    static bool Is(in LaneSlot slot, int occupant, LaneRoster of) => slot.Occupant == occupant && slot.Of == of;

    bool Nearest(int way, float fromM, float untilM, int excluding, LaneRoster excludingOf, int uses, out LaneSlot found)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > untilM) break;
            if (Is(slot, excluding, excludingOf) || slot.StandsToM <= fromM) continue;
            if ((Only(slot.Use) & uses) == 0) continue;

            found = slot;
            return true;
        }

        found = LaneSlot.Nothing;
        return false;
    }

    /// <summary>
    /// <b>The nearest traffic behind</b>, which is what anything about to occupy ground it is not on yet has
    /// to ask — the mouth of a bay, the lane a swerve crosses, the paint a walker is about to step onto.
    /// </summary>
    /// <remarks>
    /// <b>Traffic and not bodies</b> (<see cref="Traffic"/>): every asker here is asking what is coming down
    /// the lane at it, and another person standing on the carriageway is not that. <b>And where the body
    /// itself has got to</b> (<see cref="LaneSlot.StandsToM"/>) — a car whose reservation reaches the mouth
    /// of a bay is a car still coming, and one whose bonnet is already past it has gone.
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
        int way, float fromM, float toM, int excluding, LaneRoster excludingOf = LaneRoster.Driving)
    {
        for (var at = _head[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var slot = ref _slots[at];
            if (slot.FromM > toM) break;
            if (Is(slot, excluding, excludingOf) || slot.Use != LaneUse.Claimed || slot.ToM < fromM) continue;

            return true;
        }

        return false;
    }

    /// <summary>Everything on one way, nearest first — for a test, an overlay, and nothing on the hot path.</summary>
    public int CopyTo(int way, Span<LaneSlot> into)
    {
        var written = 0;
        for (var at = _head[way]; at != NoSlot && written < into.Length; at = _next[at]) into[written++] = _slots[at];

        return written;
    }
}
