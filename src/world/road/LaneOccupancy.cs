namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>Which roster a reservation's occupant is named in.</b> The road's reservations are not one roster's — a
/// person in a lane is on the road like anything else — so an occupant is an integer into one of two fleets,
/// and <b>which one is carried rather than inferred</b>.
/// </summary>
internal enum LaneRoster : byte
{
    Driving,

    Walking,

    /// <summary>
    /// <b>The lights</b> (TLT-1): an occupant is one stretch a light holds, and never a body — a light has no
    /// collider and stands on no way.
    /// </summary>
    Signal,
}

/// <summary>
/// <b>One reservation on one stretch of one way</b>, in the way's own metres — a body standing there
/// (<see cref="ClaimPriority.Hard"/>) or ground somebody plans to use (every other rung): a main claim on the
/// holder's own line, or a secondary claim on a way that one crosses (TER-5c.1).
/// </summary>
/// <remarks>
/// <b>Both edges are distances along the bending ground</b>: a way is a chain of arcs, its metres are that
/// chain's own arclength, and a reservation on it is an interval of that. A reservation holds the whole width
/// of its way over that interval — there is no across in it, because the ribbon is the unit of ground.
/// </remarks>
/// <param name="FromM">The near edge, measured the way the way is travelled.</param>
/// <param name="ToM">The far edge. Never less than <paramref name="FromM"/>.</param>
/// <param name="AlongMps">How fast the occupant is going <em>along this way</em> — negative where it faces the other way.</param>
/// <param name="Occupant">The body's own index in <paramref name="Of"/>.</param>
/// <param name="Priority">
/// <b>How strong it is</b> (TER-5g): <see cref="ClaimPriority.Hard"/> for a body, and the rung its holder
/// plans at for everything else.
/// </param>
/// <param name="Of">Which of the town's two rosters <paramref name="Occupant"/> is an index into.</param>
/// <param name="OnItsLine">
/// <b>A body only: whether it is travelling down this very way</b> — a driver or a walker holding this way
/// on the line it is following, as against a body merely standing on it. It is what tells a queue from an
/// obstruction, and the holder says it of itself.
/// </param>
/// <param name="Hold">Planned only: the hold it is a piece of (<see cref="LaneOccupancy.BeginHold"/>).</param>
/// <param name="LineFromM">
/// Planned only: where <paramref name="FromM"/> falls on its holder's own line — and, for a secondary claim,
/// where on that line its main claim comes to the mark, which is the metre the hold is cut at if the secondary
/// claim is taken.
/// </param>
/// <param name="AheadM">
/// Planned only: <b>how far its holder has to travel to reach <paramref name="FromM"/></b> — or, for a
/// secondary claim, to reach the mark on its own way. The tie nothing else breaks goes to whoever gets there
/// first.
/// </param>
/// <param name="CommittedToM">
/// Planned only: where the ground its holder can no longer stop short of ends on this way
/// (<see cref="ClaimPriority.Committed"/>); everything short of it outranks every rung.
/// </param>
/// <param name="Secondary">
/// Planned only: <b>a secondary claim</b> (TER-5c.1) — the whole section of another way that a mark links a
/// stretch of the holder's main claim to, placed with that main claim rather than laid on the holder's own
/// line. It is weighed against main claims and never against another secondary claim.
/// </param>
/// <param name="Onward">
/// A body travelling this way only: <b>the way its line takes next</b>; <see cref="LaneOccupancy.RunsOn"/> where
/// its line runs on past it down this way and is not yet laid past it; or <see cref="LaneOccupancy.NoWay"/>
/// where its line ends where it stands — what says whether it is making the reader's own movement (TER-4c.6).
/// </param>
/// <param name="Still">A body only: <b>whether its holder is at rest</b>, which the holder says of itself.</param>
/// <param name="Passing">
/// A body only: <b>ground an overtake will cover</b> (TER-4c.6) rather than ground a collider stands on —
/// laid at p0 all the same, and never itself something to get past.
/// </param>
internal readonly record struct LaneClaim(
    float FromM, float ToM, float AlongMps, int Occupant, ClaimPriority Priority,
    LaneRoster Of = LaneRoster.Driving, bool OnItsLine = false, int Hold = LaneOccupancy.NoHold,
    float LineFromM = 0f, float AheadM = 0f, float CommittedToM = float.NegativeInfinity, bool Secondary = false,
    int Onward = LaneOccupancy.NoWay, bool Still = false, bool Passing = false)
{
    public static LaneClaim Nothing => new(
        float.PositiveInfinity, float.PositiveInfinity, 0f, LaneOccupancy.Nobody, ClaimPriority.Hard);

    public bool Found => float.IsFinite(FromM);

    /// <summary><b>Whether this is a body</b> — the physical layer — rather than ground somebody plans to use.</summary>
    public bool HasBody => Priority == ClaimPriority.Hard;

    /// <summary>
    /// <b>Whether a holder whose own line takes <paramref name="onward"/> next may get past this</b> (TER-4c.6):
    /// a body at rest that is not a pass itself, and not making the reader's own movement — somebody making it is
    /// waiting for what the reader would wait for. <b>A holder on a call gets past that too, and past traffic
    /// that is moving</b> (<paramref name="onACall"/>): what the queue waits for is ground its rung takes, and a
    /// car it passes is held short of where it steps back in. Never somebody on foot who is moving, who is
    /// crossing.
    /// </summary>
    public bool MayBePassedBy(int onward, bool onACall = false) =>
        HasBody && !Passing && (onACall ? Still || IsTraffic : Still && !MakesTheMovementOf(onward));

    /// <summary>
    /// <b>Whether this is a body going nowhere down this way</b>: at rest, not a pass, and not travelling the way
    /// on to anywhere — a wreck, a car stood down or off its line, a car whose line ends where it stands, somebody
    /// standing in the road. It is the one body a driver keeps room to step out round (CAR-46).
    /// </summary>
    public bool GoesNowhere => HasBody && Still && !Passing && !(OnItsLine && Onward != LaneOccupancy.NoWay);

    /// <summary>
    /// Whether this body is travelling this way on to where a holder whose own line takes
    /// <paramref name="onward"/> next is going.
    /// </summary>
    /// <remarks>
    /// <b>A line not yet laid past this way makes every movement</b> (<see cref="LaneOccupancy.RunsOn"/>): its
    /// holder is further than it can see from the way's end, so what holds it here is on this way, and the reader
    /// would wait for that too.
    /// </remarks>
    bool MakesTheMovementOf(int onward) =>
        OnItsLine && Onward != LaneOccupancy.NoWay
        && (Onward == onward || Onward == LaneOccupancy.RunsOn || onward == LaneOccupancy.RunsOn);

    /// <summary><b>Whether this is wheeled traffic standing here</b>: a body of the driving roster.</summary>
    public bool IsTraffic => HasBody && Of == LaneRoster.Driving;

    /// <summary>Whether the metre <paramref name="atM"/> of it is ground its holder will still cover before it can be at rest.</summary>
    public bool CommittedAt(float atM) => !HasBody && atM < CommittedToM;

    /// <summary>
    /// <b>Whether its holder can no longer stop short of the metre <paramref name="atM"/></b>, which is what the
    /// ladder is settled on (<see cref="LaneOccupancy.Beats"/>): the ground it will still cover and the metre at its
    /// own front, as <see cref="PlannedAsk.CannotStopShortOf"/> has it.
    /// </summary>
    public bool CannotStopShortOf(float atM) => !HasBody && atM <= CommittedToM;

    /// <summary>How far its holder has to travel to reach the metre <paramref name="atM"/> of it.</summary>
    public float ArrivalAt(float atM) => AheadM + (Secondary ? 0f : MathF.Max(0f, atM - FromM));
}

/// <summary>
/// <b>One stretch of one way a hold is asking for</b>, and what it asks with — the terms every comparison of
/// the planned layer is made on (<see cref="LaneOccupancy.Beats"/>).
/// </summary>
/// <param name="FromM">Where on the way the stretch begins.</param>
/// <param name="LineFromM">And where that falls on the holder's own line.</param>
/// <param name="AheadM">How far the holder has to travel to reach <paramref name="FromM"/>.</param>
/// <param name="CommittedToM">Where on this way the ground it can no longer stop short of ends.</param>
internal readonly record struct PlannedAsk(
    int Hold, int Occupant, LaneRoster Of, ClaimPriority Rung, float FromM, float LineFromM, float AheadM,
    float CommittedToM, float AlongMps)
{
    /// <summary>Whether the metre <paramref name="atM"/> of it is ground its holder can no longer stop short of.</summary>
    /// <remarks>
    /// <b>The end metre included, so a holder at rest still cannot stop short of its own front</b> (TER-5e): ground
    /// two ways share that its front is already inside is ground it keeps, and a secondary claim placed from there
    /// is committed whole. Open at the end, the answer turns on whether a car settling at a zebra comes back at
    /// exactly nought on a tick or a hair above it — the paint is the car's one rebuild and the walker's the next,
    /// and the walker steps a stride further out each time it has it. <b>The ladder's question and no other</b>: what a
    /// pass takes and where a body is put down ask what the holder will still cover
    /// (<see cref="LaneClaim.CommittedAt"/>), and a body at rest covers nothing (TER-4c.6).
    /// </remarks>
    public bool CannotStopShortOf(float atM) => atM <= CommittedToM;

    /// <summary>How far its holder has to travel to reach the metre <paramref name="atM"/> of this way.</summary>
    public float ArrivalAt(float atM) => AheadM + MathF.Max(0f, atM - FromM);

    /// <summary>Where the metre <paramref name="atM"/> of this way falls on the holder's own line.</summary>
    public float LineAt(float atM) => LineFromM + MathF.Max(0f, atM - FromM);

    /// <summary>The same terms as the piece of ground they lay.</summary>
    public LaneClaim Laid(float toM) =>
        new(FromM, toM, AlongMps, Occupant, Rung, Of, Hold: Hold, LineFromM: LineFromM, AheadM: AheadM,
            CommittedToM: CommittedToM);
}

/// <summary>
/// <b>Who is on each way and who means to be, as intervals of the way's own arclength</b> — and, by TER-4c,
/// <b>the whole of what an agent looks at</b>. Two layers over one numbering (<see cref="TownWays"/>), and the
/// only two ways ordinary agents meet (TER-4c.5).
/// </summary>
/// <remarks>
/// <para>
/// <b>The physical layer is where the bodies are</b> (TER-4c.2). Every body with a collider reserves, at p0,
/// every way whose ribbon its collider overlaps, over the stretch it overlaps (<see cref="RibbonAtlas"/>). It
/// is never compared, never cut and never taken, and two bodies' reservations may lie over one metre: it is a
/// record of real things and not of anybody's plan.
/// </para>
/// <para>
/// <b>The planned layer is where they mean to be</b> (TER-4c.1). A hold is one stretch of its holder's own
/// line, laid way by way from the nose forward as <b>main claims</b> — and, wherever a mark says a main claim's
/// ground is shared with another way, a <b>secondary claim</b> over the whole of that way's section, placed
/// with it (TER-5c.1). <b>A main claim is answered off its own way alone</b>: cut at the first body in front of
/// it, and weighed by one comparison (<see cref="Beats"/>) against every other hold's claim there, main or
/// secondary — the stronger keeps the ground and the weaker is cut back to where the two met. <b>Two secondary
/// claims never meet.</b> <b>A hold is one stretch</b> (TER-5c.2): cut anywhere, it gives up everything past
/// the cut, secondary claims included.
/// </para>
/// <para>
/// <b>Nothing on another way is read, because nothing there could answer differently.</b> A mark is filed
/// under both its ways (<see cref="WayCrossings"/>), so whatever holds the far side of it has placed its own
/// secondary claim over the near side, where the main claim asking meets it. Main meets main on one way and
/// main meets secondary on the main claim's own way, and those are the only two meetings there are.
/// </para>
/// <para>
/// <b>No two holds share a metre</b> (TER-4c.3), and the comparison is total and symmetric, so which of two
/// holds keeps a piece of ground does not turn on which was laid first. <b>What a cut frees is handed back by
/// the holder that was refused it</b>: a hold answered against ground a later cut took away is taken up and
/// laid again, whole, over a new answer (<see cref="ReopenHold"/>) — which is the holder's to ask for, since
/// only it knows what it was asking.
/// </para>
/// <para>
/// <b>Nothing here computes any geometry.</b> Which ways a body is on comes from the atlas, where a secondary
/// claim goes comes from its marks, and everything else is an interval of one way's own metres.
/// </para>
/// <para>
/// <b>It is rebuilt from the bodies every tick and never written to during a decision</b>, which is what
/// makes it an index rather than a register: nothing is released and nothing can leak.
/// </para>
/// </remarks>
internal sealed partial class LaneOccupancy
{
    public const int Nobody = -1;

    public const int NoHold = -1;

    /// <summary>No way: a body's line ending where it stands (<see cref="LaneClaim.Onward"/>).</summary>
    public const int NoWay = -1;

    /// <summary>
    /// A body's line running on past it down the way it is on, and not yet laid past that way
    /// (<see cref="LaneClaim.Onward"/>).
    /// </summary>
    public const int RunsOn = -2;

    const int NoSlot = -1;

    readonly TownWays _ways;

    /// <summary><b>Where a main claim's secondary claims go</b> (<see cref="RibbonAtlas.Marks"/>).</summary>
    readonly WayCrossings _marks;

    /// <summary>The first body on each way, or <see cref="NoSlot"/>, ascending by near edge.</summary>
    readonly int[] _bodies;

    /// <summary>The first planned piece on each way, or <see cref="NoSlot"/>, ascending by near edge.</summary>
    readonly int[] _planned;

    readonly int[] _next;
    readonly int[] _prev;
    readonly LaneClaim[] _slots;
    readonly int[] _slotWay;

    /// <summary>Whether a slot has been taken out of its way's list since it was laid.</summary>
    readonly bool[] _gone;

    readonly int[] _touched;
    readonly bool[] _laidOn;

    readonly int[] _holdFirst;
    readonly int[] _holdEnd;
    readonly float[] _holdCutLineM;
    readonly float[] _holdCutMarginM;
    readonly float[] _holdStandingMarginM;
    readonly LaneClaim[] _holdCutBy;
    readonly int[] _holdCutOn;

    int _slotCount;
    int _touchedCount;
    int _holdCount;
    int _cuts;

    /// <param name="ways">Every way in the town, in one numbering.</param>
    /// <param name="mostSlots">
    /// How many reservations the town may hold at once. <b>A bound on the work and not a figure behaviour
    /// reads</b>, and one that must never be reached: a dropped reservation is a body or a plan nobody can
    /// see, which the gates count (<see cref="Dropped"/>).
    /// </param>
    /// <param name="mostHolds">How many holds may be laid in one rebuild: every driver's, every walker's, every light's.</param>
    /// <param name="marks">
    /// <b>Which ways share ground</b> (<see cref="RibbonAtlas.Marks"/>); <see cref="WayCrossings.None"/> for a
    /// fixture whose ways touch nothing.
    /// </param>
    public LaneOccupancy(TownWays ways, int mostSlots, int mostHolds, WayCrossings marks)
    {
        _ways = ways;
        _marks = marks;

        _bodies = new int[ways.Count];
        _planned = new int[ways.Count];
        Array.Fill(_bodies, NoSlot);
        Array.Fill(_planned, NoSlot);
        _next = new int[mostSlots];
        _prev = new int[mostSlots];
        _slots = new LaneClaim[mostSlots];
        _slotWay = new int[mostSlots];
        _gone = new bool[mostSlots];
        _touched = new int[ways.Count];
        _laidOn = new bool[ways.Count];

        _holdFirst = new int[mostHolds];
        _holdEnd = new int[mostHolds];
        _holdCutLineM = new float[mostHolds];
        _holdCutMarginM = new float[mostHolds];
        _holdStandingMarginM = new float[mostHolds];
        _holdCutBy = new LaneClaim[mostHolds];
        _holdCutOn = new int[mostHolds];
    }

    /// <summary>The numbering these reservations are laid in.</summary>
    public TownWays Ways => _ways;

    public int WayCount => _ways.Count;

    /// <summary>The marks secondary claims are placed through.</summary>
    public WayCrossings Marks => _marks;

    /// <summary>How many reservations the last rebuild laid, taken ones included.</summary>
    public int SlotCount => _slotCount;

    public int Capacity => _slots.Length;

    /// <summary>How many holds the last rebuild laid.</summary>
    public int HoldCount => _holdCount;

    /// <summary>
    /// <b>How many times one hold has been cut by another's taking</b> since the rebuild began — none, and every
    /// hold still ends where it was answered.
    /// </summary>
    public int Cuts => _cuts;

    /// <summary>
    /// <b>How many reservations have been dropped for want of room</b> since the town was laid — a body or a
    /// plan nobody could see, which is a gate's failure and never an outcome.
    /// </summary>
    public long Dropped { get; private set; }

    /// <summary>
    /// The ways somebody is on, in no order and each once. <b>A reader that wants every reservation walks
    /// this and not the town.</b>
    /// </summary>
    public ReadOnlySpan<int> OccupiedWays => _touched.AsSpan(0, _touchedCount);

    public float WayLengthM(int way) => _ways.LengthM(way);

    /// <summary>Everything laid last tick is dropped. Nothing survives a rebuild, which is the whole guarantee.</summary>
    public void Begin()
    {
        for (var index = 0; index < _touchedCount; index++)
        {
            var way = _touched[index];
            _bodies[way] = NoSlot;
            _planned[way] = NoSlot;
            _laidOn[way] = false;
        }

        _touchedCount = 0;
        _slotCount = 0;
        _holdCount = 0;
        _cuts = 0;
    }

    /// <summary>
    /// <b>A body on one way</b> (TER-4c.2), over the stretch its collider covers of it. A body already on
    /// the way under the same name grows to cover both — one body is one stretch of one way.
    /// </summary>
    /// <param name="onward">Where a body travelling this way goes next (<see cref="LaneClaim.Onward"/>).</param>
    public void LayBody(
        int way, float fromM, float toM, float alongMps, int occupant, LaneRoster of, bool onItsLine,
        int onward = NoWay, bool still = false)
    {
        if (toM < fromM) return;

        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.Occupant != occupant || body.Of != of || body.Passing) continue;

            var grown = body with
            {
                FromM = MathF.Min(body.FromM, fromM), ToM = MathF.Max(body.ToM, toM),
                OnItsLine = body.OnItsLine || onItsLine, Onward = onItsLine ? onward : body.Onward,
            };
            Unlink(at);
            Link(_bodies, at, way, grown);
            return;
        }

        Append(
            _bodies, way,
            new LaneClaim(fromM, toM, alongMps, occupant, ClaimPriority.Hard, of, onItsLine, Onward: onward, Still: still));
    }

    /// <summary>
    /// <b>Ground an overtake will cover</b> (TER-4c.6), laid at p0 over one stretch of one way: a body in
    /// everything but having a collider, so every plan is cut short of it and nothing compares or takes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its own stretch and never grown into its holder's body</b>: the two lie over one way at once — a car
    /// half out of its lane is on both — and a body grown over the pass would stand over whatever is being
    /// passed.
    /// </para>
    /// <para>
    /// <b>Grown where it meets its own stretch of the same pass, and only there</b>: a pass is laid as the body
    /// swept down it, a station at a time, and one way it leaves and comes back to — its own lane either side of
    /// what it passes — is two stretches with that between them.
    /// </para>
    /// </remarks>
    public void LayPass(int way, float fromM, float toM, float alongMps, int occupant, LaneRoster of)
    {
        if (toM <= fromM) return;

        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var pass = ref _slots[at];
            if (!pass.Passing || pass.Occupant != occupant || pass.Of != of || pass.ToM < fromM || pass.FromM > toM)
            {
                continue;
            }

            var grown = pass with { FromM = MathF.Min(pass.FromM, fromM), ToM = MathF.Max(pass.ToM, toM) };
            Unlink(at);
            Link(_bodies, at, way, grown);
            return;
        }

        Append(
            _bodies, way, new LaneClaim(fromM, toM, alongMps, occupant, ClaimPriority.Hard, of, Passing: true));
    }

    /// <summary>
    /// <b>A hold begun</b>: the stretch one holder plans to use, laid a piece at a time
    /// (<see cref="Reach"/>, <see cref="Take"/>) and finished with what it came to (<see cref="EndHold"/>).
    /// </summary>
    /// <param name="standingMarginM">
    /// The ground its holder keeps off whatever it is cut at — what the grant is taken short of if another hold
    /// takes this one's ground later in the rebuild.
    /// </param>
    /// <returns>The hold, or <see cref="NoHold"/> where there is no room for another.</returns>
    public int BeginHold(float standingMarginM)
    {
        if (_holdCount == _holdFirst.Length)
        {
            Dropped++;
            return NoHold;
        }

        var hold = _holdCount++;
        _holdFirst[hold] = _slotCount;
        _holdEnd[hold] = _slotCount;
        _holdCutLineM[hold] = float.PositiveInfinity;
        _holdCutMarginM[hold] = 0f;
        _holdStandingMarginM[hold] = standingMarginM;
        _holdCutBy[hold] = LaneClaim.Nothing;
        _holdCutOn[hold] = NoSlot;
        return hold;
    }

    /// <summary>
    /// <b>How far along one way a hold's main claim can be had</b> — read and never written, and read off this
    /// way alone: the lesser of the near edge of the first body in front of the holder and the first metre of
    /// another hold's claim, main or secondary, that <see cref="Beats"/> this one there. <paramref name="toM"/>
    /// where nothing stops it.
    /// </summary>
    /// <remarks>
    /// <b>The secondary claims the piece would place are not asked about</b>: whatever they would meet on the
    /// ways they lie over has placed its own secondary claim on this one (<see cref="WayCrossings"/>).
    /// </remarks>
    /// <param name="standsToM">
    /// How far along this way the holder's own body already reaches — its nose on the way it is on, the
    /// piece's own start beyond that. <b>A body that does not reach past it cuts nothing</b>: it is beside or
    /// behind the holder, and a body the holder is already inside is what <paramref name="cutBy"/> says.
    /// </param>
    /// <param name="cutBy">What stopped it, or <see cref="LaneClaim.Nothing"/>.</param>
    public float Reach(in PlannedAsk ask, int way, float toM, float standsToM, out LaneClaim cutBy)
    {
        var fromM = ask.FromM;
        var limitM = toM;
        cutBy = LaneClaim.Nothing;

        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= limitM) break;
            if (Owns(ask, body) || body.ToM <= standsToM) continue;

            // A pass holds nobody already standing on its ground: its own holder is held off them (TER-4c.6), and
            // held against them too, the two would each wait for the other.
            if (body.Passing && StandsOn(way, ask.Occupant, ask.Of, body.FromM, body.ToM)) continue;

            limitM = MathF.Max(fromM, body.FromM);
            cutBy = body;
            break;
        }

        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var other = ref _slots[at];
            var atM = MathF.Max(fromM, other.FromM);
            if (atM >= limitM) break;
            if (other.ToM <= fromM || Owns(ask, other)) continue;

            var overToM = MathF.Min(toM, other.ToM);
            if (Beats(
                    ask, atM, StandsOn(way, ask.Occupant, ask.Of, atM, overToM), other,
                    StandsOn(way, other.Occupant, other.Of, atM, overToM)))
            {
                continue;
            }

            limitM = atM;
            cutBy = other;
            break;
        }

        return limitM;
    }

    /// <summary>
    /// <b>A hold's main claim laid</b> over <c>[ask.FromM, toM)</c> of one way, and its secondary claims with
    /// it: every other hold's claim over these metres of this way, main or secondary, is cut back to where the
    /// two met; the main claim is laid; and every mark over these metres places the whole of its section of the
    /// other way as a secondary claim of this hold.
    /// </summary>
    /// <remarks>
    /// <b>Laid only over what <see cref="Reach"/> said could be had</b>, so everything met here is something
    /// this hold beats. <b>A secondary claim cuts nothing where it is placed</b>: a main claim over that ground
    /// has a secondary claim of its own over this way, and was cut when that was. A piece of no length lays
    /// nothing and takes nothing.
    /// </remarks>
    public void Take(in PlannedAsk ask, int way, float toM)
    {
        var fromM = ask.FromM;
        if (toM <= fromM || ask.Hold == NoHold) return;

        var main = ask.Laid(toM);
        TakeFrom(way, fromM, toM, ask, main);

        foreach (ref readonly var mark in _marks.Of(way))
        {
            if (mark.MineFromM >= toM) break;
            if (mark.MineToM <= fromM) continue;

            var atM = MathF.Max(fromM, mark.MineFromM);
            Append(
                _planned, mark.OnWay,
                new LaneClaim(
                    mark.FromM, mark.ToM, 0f, ask.Occupant, ask.Rung, ask.Of, Hold: ask.Hold,
                    LineFromM: ask.LineAt(atM), AheadM: ask.ArrivalAt(atM),
                    CommittedToM: ask.CannotStopShortOf(atM) ? float.PositiveInfinity : float.NegativeInfinity,
                    Secondary: true));
        }

        Append(_planned, way, main);
    }

    /// <summary>
    /// <b>A secondary claim placed where it stands</b> over <c>[ask.FromM, toM)</c> of one way, and nothing
    /// else: no main claim, no mark followed and nothing cut (TER-5c.1). It is a light's hold (TLT-1) — ground held
    /// on the way it governs and on no other — so it meets the main claims asked of that way and no secondary claim
    /// placed there.
    /// </summary>
    /// <remarks>
    /// <b>Placed before any plan is laid</b>: a secondary claim cuts nothing where it is placed, so a main claim
    /// already over these metres would share them with it (TER-4c.3). Whether it may lie over a body is the
    /// placer's to say — a light's stops at the first body travelling its way (<see cref="AheadTraveller"/>) and
    /// lies over any other.
    /// </remarks>
    public void Place(in PlannedAsk ask, int way, float toM)
    {
        if (toM <= ask.FromM || ask.Hold == NoHold) return;

        Append(_planned, way, ask.Laid(toM) with { Secondary = true });
    }

    /// <summary>
    /// <b>A hold finished</b>: where on its holder's line it ended, what it was cut at and on which way, and
    /// the ground its holder keeps off that — or none of them, where it laid everything it asked for.
    /// </summary>
    public void EndHold(int hold, float cutLineM, float cutMarginM, in LaneClaim cutBy, int cutOn = NoSlot)
    {
        if (hold == NoHold) return;

        _holdEnd[hold] = _slotCount;
        if (cutLineM >= _holdCutLineM[hold]) return;

        _holdCutLineM[hold] = cutLineM;
        _holdCutMarginM[hold] = cutMarginM;
        _holdCutBy[hold] = cutBy;
        _holdCutOn[hold] = cutOn;
    }

    /// <summary>
    /// <b>A hold taken up to be laid again</b>: every main and secondary claim of it comes off the ways, and what it was answered is forgotten — laid again with <see cref="Reach"/>,
    /// <see cref="Take"/> and <see cref="EndHold"/> as though begun.
    /// </summary>
    /// <remarks>
    /// <b>An answer read before it was taken up still stands after</b>: a hold's own ground is never held
    /// against it, so its pieces change nothing <see cref="Reach"/> says. <b>Its pieces are laid in new
    /// slots</b>, and the ones it held are spent until the next rebuild.
    /// </remarks>
    public void ReopenHold(int hold)
    {
        if (hold == NoHold) return;

        for (var at = _holdFirst[hold]; at < _holdEnd[hold]; at++)
        {
            if (!_gone[at]) Unlink(at);
        }

        _holdFirst[hold] = _slotCount;
        _holdEnd[hold] = _slotCount;
        _holdCutLineM[hold] = float.PositiveInfinity;
        _holdCutMarginM[hold] = 0f;
        _holdCutBy[hold] = LaneClaim.Nothing;
        _holdCutOn[hold] = NoSlot;
    }

    /// <summary>Which way a hold was cut on, for an instrument saying what held somebody — or −1.</summary>
    public int HoldCutOn(int hold) => hold == NoHold ? NoSlot : _holdCutOn[hold];

    /// <summary>
    /// <b>What a hold came to</b> once every hold of the rebuild was laid: where on its holder's line it ends
    /// (infinity where it laid all it asked for), the margin its holder keeps off that, and what it was cut
    /// at.
    /// </summary>
    public float HoldEndsAtM(int hold, out float marginM, out LaneClaim cutBy)
    {
        if (hold == NoHold)
        {
            marginM = 0f;
            cutBy = LaneClaim.Nothing;
            return float.PositiveInfinity;
        }

        marginM = _holdCutMarginM[hold];
        cutBy = _holdCutBy[hold];
        return _holdCutLineM[hold];
    }

    /// <summary>
    /// <b>Which of two planned reservations meeting on one piece of ground keeps it</b> (TER-5e, TER-5g) — the
    /// one comparison there is, total and symmetric, so the answer is the same whichever of the two was laid
    /// first. <b>True where the asker keeps it.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ground its holder can no longer stop short of is taken by nothing</b>: a right of way orders who
    /// waits and never who is driven into, so it beats every rung.
    /// </para>
    /// <para>
    /// <b>Then ground its holder is already standing in</b> (<paramref name="askStands"/>): a plan over metres
    /// somebody's body is on is a plan that cannot be driven until that body has left them, and held against
    /// that body it is two holders each waiting for the other.
    /// </para>
    /// <para>
    /// <b>Below that it is the ladder</b> — a call, a light, somebody on foot, and the three
    /// movements a box admits, straightest first.
    /// </para>
    /// <para>
    /// <b>And last, whoever gets there first</b> — the holder with less of its own line to cover before the
    /// ground in question — and two exactly as near by roster and occupant. <b>The ladder is not asked between
    /// two holders that can no longer stop</b>: both are going in, and what is left to settle is who is there
    /// first — the one further off is the one with road left to brake on.
    /// </para>
    /// <para>
    /// <b>Nothing is remembered from the rebuild before</b>: a box does not stay with a car because it won it
    /// last time. A car that can no longer stop short of a box's mouth, or is already in it, holds it as ground
    /// it cannot give back, which is the first tier and not a tier of its own.
    /// </para>
    /// </remarks>
    /// <param name="atM">
    /// Where on the asker's way the two meet — the one way they are ever weighed on, since the other is a main
    /// claim of that way or a secondary claim placed on it.
    /// </param>
    /// <param name="askStands">Whether the asker's own body is on the ground the two want.</param>
    /// <param name="otherStands">And whether the other's holder's body is.</param>
    public static bool Beats(in PlannedAsk ask, float atM, bool askStands, in LaneClaim other, bool otherStands)
    {
        var askCommitted = ask.CannotStopShortOf(atM);
        var otherCommitted = other.CannotStopShortOf(atM);
        if (askCommitted != otherCommitted) return askCommitted;
        if (askStands != otherStands) return askStands;
        if (!askCommitted && ask.Rung != other.Priority) return ask.Rung < other.Priority;

        var askArrivalM = ask.ArrivalAt(atM);
        var otherArrivalM = other.ArrivalAt(atM);
        if (askArrivalM != otherArrivalM) return askArrivalM < otherArrivalM;

        return ask.Of != other.Of ? ask.Of < other.Of : ask.Occupant < other.Occupant;
    }

    /// <summary>
    /// Whether one holder's body is on a stretch of one way — its collider, and never the ground a pass of its
    /// will cover, which it is not standing on yet.
    /// </summary>
    bool StandsOn(int way, int occupant, LaneRoster of, float fromM, float toM)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= toM) break;
            if (body.Occupant == occupant && body.Of == of && !body.Passing && body.ToM > fromM) return true;
        }

        return false;
    }

    /// <summary>
    /// Every other hold's claim over <c>[fromM, toM)</c> of one way, main or secondary, cut back to where the
    /// taker met it — the whole of that hold beyond the metre, its secondary claims included.
    /// </summary>
    void TakeFrom(int way, float fromM, float toM, in PlannedAsk ask, in LaneClaim taker)
    {
        // Cutting a hold may take other pieces of it off this same list, so the walk starts again after each.
        var at = _planned[way];
        while (at != NoSlot)
        {
            ref readonly var other = ref _slots[at];
            if (other.FromM >= toM) break;
            if (other.ToM <= fromM || Owns(ask, other))
            {
                at = _next[at];
                continue;
            }

            var atM = MathF.Max(fromM, other.FromM);
            CutHold(other.Hold, other.Secondary ? other.LineFromM : other.LineFromM + (atM - other.FromM), taker, way);

            // The line metre and the way metre are one figure carried two ways, so the piece met here is
            // held to the exact metre it was met at: a hair of overlap left by the round trip is ground two
            // holds share, and a walk that met it again would never end.
            if (!_gone[at] && _slots[at].ToM > atM)
            {
                if (atM <= _slots[at].FromM) Unlink(at);
                else _slots[at] = _slots[at] with { ToM = atM };
            }

            at = _planned[way];
        }
    }

    /// <summary>
    /// <b>A hold cut at one metre of its holder's line</b>: every piece of it beyond is taken out, the main
    /// claim running over the metre ends there, and a secondary claim placed at or beyond it goes whole.
    /// </summary>
    void CutHold(int hold, float lineM, in LaneClaim cutBy, int cutOn)
    {
        _cuts++;
        for (var at = _holdFirst[hold]; at < _holdEnd[hold]; at++)
        {
            if (_gone[at]) continue;

            ref readonly var piece = ref _slots[at];
            if (piece.LineFromM >= lineM)
            {
                Unlink(at);
                continue;
            }

            if (piece.Secondary) continue;

            var endsAtLineM = piece.LineFromM + (piece.ToM - piece.FromM);
            if (endsAtLineM <= lineM) continue;

            var toM = piece.FromM + (lineM - piece.LineFromM);
            if (toM <= piece.FromM) Unlink(at);
            else _slots[at] = piece with { ToM = toM };
        }

        if (lineM >= _holdCutLineM[hold]) return;

        _holdCutLineM[hold] = lineM;
        _holdCutMarginM[hold] = _holdStandingMarginM[hold];
        _holdCutBy[hold] = cutBy;
        _holdCutOn[hold] = cutOn;
    }

    /// <summary>Whether a reservation is the asker's own, which is never held against it.</summary>
    static bool Owns(in PlannedAsk ask, in LaneClaim claim) =>
        claim.Occupant == ask.Occupant && claim.Of == ask.Of;

    /// <summary>A reservation put into one of a way's two lists, at the place its near edge says.</summary>
    void Append(int[] heads, int way, in LaneClaim claim)
    {
        if (_slotCount == _slots.Length)
        {
            Dropped++;
            return;
        }

        var slot = _slotCount++;
        Link(heads, slot, way, claim);

        if (!_laidOn[way])
        {
            _laidOn[way] = true;
            _touched[_touchedCount++] = way;
        }
    }

    void Link(int[] heads, int slot, int way, in LaneClaim claim)
    {
        _slots[slot] = claim;
        _slotWay[slot] = way;
        _gone[slot] = false;

        var after = NoSlot;
        for (var at = heads[way]; at != NoSlot && _slots[at].FromM < claim.FromM; at = _next[at]) after = at;

        var before = after == NoSlot ? heads[way] : _next[after];
        _prev[slot] = after;
        _next[slot] = before;
        if (before != NoSlot) _prev[before] = slot;
        if (after == NoSlot) heads[way] = slot;
        else _next[after] = slot;
    }

    void Unlink(int slot)
    {
        var way = _slotWay[slot];
        var heads = _slots[slot].HasBody ? _bodies : _planned;
        var before = _prev[slot];
        var after = _next[slot];
        if (before == NoSlot) heads[way] = after;
        else _next[before] = after;
        if (after != NoSlot) _prev[after] = before;

        _gone[slot] = true;
    }
}
