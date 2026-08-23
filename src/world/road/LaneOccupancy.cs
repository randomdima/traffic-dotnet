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
    /// <b>What it is not is traffic.</b> A walker asking what is coming down a lane (PER-15) and a driver
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
/// <para>
/// <b>This file is the book itself</b> — how a way is numbered, what a rebuild drops, and how a stretch is
/// laid. <b>The questions asked of it are LaneOccupancy.Questions.cs</b>, which is every walk of it a
/// caller can make and nothing that writes.
/// </para>
/// </remarks>
internal sealed partial class LaneOccupancy
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
}
