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
}

/// <summary>
/// <b>One reservation on one stretch of one way</b>, in the way's own metres — a body standing there
/// (<see cref="ClaimPriority.Hard"/>) or ground somebody plans to use (every other rung).
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
/// Planned only: where <paramref name="FromM"/> falls on its holder's own line — and, for a piece written
/// through a mark, where on that line the mark begins, which is the metre the hold is cut at if the piece is
/// taken.
/// </param>
/// <param name="AheadM">
/// Planned only: <b>how far its holder has to travel to reach <paramref name="FromM"/></b> — or, through a
/// mark, the start of that mark on its own way. The tie nothing else breaks goes to whoever gets there first.
/// </param>
/// <param name="Held">
/// Planned only: <b>ground on the way to and through a box its holder has already been given</b> — a movement
/// it won the last time the plans were laid (<c>CarFleet.MovementWay</c>). It is what keeps a box with the car
/// that has it, where the ladder does not say otherwise, rather than handing it to whoever came nearer since.
/// </param>
/// <param name="CommittedToM">
/// Planned only: where the ground its holder can no longer stop short of ends on this way
/// (<see cref="ClaimPriority.Committed"/>); everything short of it outranks every rung.
/// </param>
/// <param name="Linked">
/// Planned only: <b>written through a mark rather than laid on the holder's own line</b> (TER-5c.1) — the
/// section of another way that a stretch of the holder's own needs, held whole or not at all.
/// </param>
internal readonly record struct LaneClaim(
    float FromM, float ToM, float AlongMps, int Occupant, ClaimPriority Priority,
    LaneRoster Of = LaneRoster.Driving, bool OnItsLine = false, int Hold = LaneOccupancy.NoHold,
    float LineFromM = 0f, float AheadM = 0f, float CommittedToM = float.NegativeInfinity, bool Linked = false,
    bool Held = false)
{
    public static LaneClaim Nothing => new(
        float.PositiveInfinity, float.PositiveInfinity, 0f, LaneOccupancy.Nobody, ClaimPriority.Hard);

    public bool Found => float.IsFinite(FromM);

    /// <summary><b>Whether this is a body</b> — the physical layer — rather than ground somebody plans to use.</summary>
    public bool HasBody => Priority == ClaimPriority.Hard;

    /// <summary><b>Whether this is wheeled traffic standing here</b>: a body of the driving roster.</summary>
    public bool IsTraffic => HasBody && Of == LaneRoster.Driving;

    /// <summary>Whether the metre <paramref name="atM"/> of it is ground its holder can no longer stop short of.</summary>
    public bool CommittedAt(float atM) => !HasBody && atM < CommittedToM;

    /// <summary>How far its holder has to travel to reach the metre <paramref name="atM"/> of it.</summary>
    public float ArrivalAt(float atM) => AheadM + (Linked ? 0f : MathF.Max(0f, atM - FromM));
}

/// <summary>
/// <b>One stretch of one way a hold is asking for</b>, and what it asks with — the terms every comparison of
/// the planned layer is made on (<see cref="LaneOccupancy.Beats"/>).
/// </summary>
/// <param name="FromM">Where on the way the stretch begins.</param>
/// <param name="LineFromM">And where that falls on the holder's own line.</param>
/// <param name="AheadM">How far the holder has to travel to reach <paramref name="FromM"/>.</param>
/// <param name="CommittedToM">Where on this way the ground it can no longer stop short of ends.</param>
/// <param name="Held">Whether this is ground on the way to or through a box the holder has already been given (<see cref="LaneClaim.Held"/>).</param>
internal readonly record struct PlannedAsk(
    int Hold, int Occupant, LaneRoster Of, ClaimPriority Rung, float FromM, float LineFromM, float AheadM,
    float CommittedToM, float AlongMps, bool Held = false)
{
    /// <summary>Whether the metre <paramref name="atM"/> of it is ground its holder can no longer stop short of.</summary>
    public bool CommittedAt(float atM) => atM < CommittedToM;

    /// <summary>How far its holder has to travel to reach the metre <paramref name="atM"/> of this way.</summary>
    public float ArrivalAt(float atM) => AheadM + MathF.Max(0f, atM - FromM);

    /// <summary>Where the metre <paramref name="atM"/> of this way falls on the holder's own line.</summary>
    public float LineAt(float atM) => LineFromM + MathF.Max(0f, atM - FromM);

    /// <summary>The same terms as the piece of ground they lay.</summary>
    public LaneClaim Laid(float toM) =>
        new(FromM, toM, AlongMps, Occupant, Rung, Of, Hold: Hold, LineFromM: LineFromM, AheadM: AheadM,
            CommittedToM: CommittedToM, Held: Held);
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
/// line, laid way by way from the nose forward. On each way it is <b>cut at the first body in front of it</b>,
/// and weighed against every other hold it meets — on its own way, and on every way a mark says its ground is
/// shared with (TER-5c.1) — by one comparison (<see cref="Beats"/>): the stronger keeps the ground and the
/// weaker is cut back to where the two met. <b>A marked section is held whole or not at all</b>: to hold the
/// stretch of its own way a mark names, a hold must hold the section of the other way it links to, and one
/// that cannot is cut at the start of its own side of the mark. <b>A hold is one stretch</b> (TER-5c.2):
/// cut anywhere, it gives up everything past the cut, marked sections included.
/// </para>
/// <para>
/// <b>No two holds share a metre</b> (TER-4c.3), and the comparison is total and symmetric, so which of two
/// holds keeps a piece of ground does not turn on which was laid first. What a cut frees is not handed back
/// inside the tick: a hold cut earlier by ground a later one took away is laid again, whole, the tick after.
/// </para>
/// <para>
/// <b>Nothing here computes any geometry.</b> Which ways a body is on comes from the atlas, which ground two
/// ways share comes from its marks, and everything else is an interval of one way's own metres.
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

    const int NoSlot = -1;

    readonly TownWays _ways;

    /// <summary><b>Which ways share ground, and where</b> (<see cref="RibbonAtlas.Marks"/>).</summary>
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

    /// <param name="ways">Every way in the town, in one numbering.</param>
    /// <param name="mostSlots">
    /// How many reservations the town may hold at once. <b>A bound on the work and not a figure behaviour
    /// reads</b>, and one that must never be reached: a dropped reservation is a body or a plan nobody can
    /// see, which the gates count (<see cref="Dropped"/>).
    /// </param>
    /// <param name="mostHolds">How many holds may be laid in one rebuild: every driver, every walker, every closure.</param>
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

    /// <summary>The marks the planned layer is settled over.</summary>
    public WayCrossings Marks => _marks;

    /// <summary>How many reservations the last rebuild laid, taken ones included.</summary>
    public int SlotCount => _slotCount;

    public int Capacity => _slots.Length;

    /// <summary>How many holds the last rebuild laid.</summary>
    public int HoldCount => _holdCount;

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
    }

    /// <summary>
    /// <b>A body on one way</b> (TER-4c.2), over the stretch its collider covers of it. A body already on
    /// the way under the same name grows to cover both — one body is one stretch of one way.
    /// </summary>
    public void LayBody(
        int way, float fromM, float toM, float alongMps, int occupant, LaneRoster of, bool onItsLine)
    {
        if (toM < fromM) return;

        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.Occupant != occupant || body.Of != of) continue;

            var grown = body with
            {
                FromM = MathF.Min(body.FromM, fromM), ToM = MathF.Max(body.ToM, toM),
                OnItsLine = body.OnItsLine || onItsLine,
            };
            Unlink(at);
            Link(_bodies, at, way, grown);
            return;
        }

        Append(_bodies, way, new LaneClaim(fromM, toM, alongMps, occupant, ClaimPriority.Hard, of, onItsLine));
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
    /// <b>How far along one way a hold's piece can be had</b> — read and never written: the least of the
    /// near edge of the first body in front of the holder, the first metre of another hold that
    /// <see cref="Beats"/> this one there, and the first mark whose linked section another hold that beats
    /// this one is on. <paramref name="toM"/> where nothing stops it.
    /// </summary>
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

            limitM = MathF.Max(fromM, body.FromM);
            cutBy = body;
            break;
        }

        // The planned pieces on this way and the marks along it, in the order the holder comes to them.
        var piece = _planned[way];
        var marks = _marks.Of(way);
        var mark = 0;
        while (true)
        {
            while (piece != NoSlot && (_slots[piece].ToM <= fromM || Owns(ask, _slots[piece]))) piece = _next[piece];
            while (mark < marks.Length && marks[mark].MineToM <= fromM) mark++;

            var pieceAtM = piece == NoSlot ? float.PositiveInfinity : MathF.Max(fromM, _slots[piece].FromM);
            var markAtM = mark == marks.Length ? float.PositiveInfinity : MathF.Max(fromM, marks[mark].MineFromM);
            var atM = MathF.Min(pieceAtM, markAtM);
            if (atM >= limitM) break;

            if (pieceAtM <= markAtM)
            {
                ref readonly var other = ref _slots[piece];
                var overToM = MathF.Min(toM, other.ToM);
                if (!Beats(
                        ask, atM, StandsOn(way, ask.Occupant, ask.Of, atM, overToM), other, atM,
                        StandsOn(way, other.Occupant, other.Of, atM, overToM)))
                {
                    limitM = atM;
                    cutBy = other;
                    break;
                }

                piece = _next[piece];
                continue;
            }

            if (LosesTheSection(ask, atM, marks[mark], out var winner))
            {
                limitM = atM;
                cutBy = winner;
                break;
            }

            mark++;
        }

        return limitM;
    }

    /// <summary>
    /// <b>A hold's piece laid</b> over <c>[ask.FromM, toM)</c> of one way: every other hold it beats there,
    /// on this way and on every section a mark over these metres links it to, is cut back to where the two
    /// met; each such section is written as this hold's own; and then the piece itself.
    /// </summary>
    /// <remarks>
    /// <b>Laid only over what <see cref="Reach"/> said could be had</b>, so everything met here is something
    /// this hold beats. A piece of no length lays nothing and takes nothing.
    /// </remarks>
    public void Take(in PlannedAsk ask, int way, float toM)
    {
        var fromM = ask.FromM;
        if (toM <= fromM || ask.Hold == NoHold) return;

        var taker = ask.Laid(toM);
        TakeFrom(way, fromM, toM, ask, taker);

        foreach (ref readonly var mark in _marks.Of(way))
        {
            if (mark.MineFromM >= toM) break;
            if (mark.MineToM <= fromM) continue;

            if (mark.ToM <= mark.FromM) continue;

            TakeFrom(mark.OnWay, mark.FromM, mark.ToM, ask, taker, piecesOnly: true);

            var atM = MathF.Max(fromM, mark.MineFromM);
            Append(
                _planned, mark.OnWay,
                new LaneClaim(
                    mark.FromM, mark.ToM, 0f, ask.Occupant, ask.Rung, ask.Of, Hold: ask.Hold,
                    LineFromM: ask.LineAt(atM), AheadM: ask.ArrivalAt(atM),
                    CommittedToM: ask.CommittedAt(atM) ? float.PositiveInfinity : float.NegativeInfinity,
                    Linked: true, Held: ask.Held));
        }

        Append(_planned, way, taker);
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
    /// <b>Below that it is the ladder</b> — a call, a closure, a crossing somebody is walking, and the three
    /// movements a box admits, straightest first.
    /// </para>
    /// <para>
    /// <b>Then a box already given</b> (<see cref="LaneClaim.Held"/>): of two equal movements, the one that
    /// won the box last time keeps it, so a box does not change hands under a car that is on its way into it
    /// because another came nearer since. <b>Neither the ladder nor the box is asked between two holders that
    /// can no longer stop</b>: both are going in, and what is left to settle is who is there first — the one
    /// further off is the one with road left to brake on.
    /// </para>
    /// <para>
    /// <b>And last, whoever gets there first</b> — the holder with less of its own line to cover before the
    /// ground in question — and two exactly as near by roster and occupant.
    /// </para>
    /// </remarks>
    /// <param name="atM">Where on its own way the asker meets the other.</param>
    /// <param name="askStands">Whether the asker's own body is on the ground the two want.</param>
    /// <param name="otherAtM">And where on its own way the other is met.</param>
    /// <param name="otherStands">And whether the other's holder's body is.</param>
    public static bool Beats(
        in PlannedAsk ask, float atM, bool askStands, in LaneClaim other, float otherAtM, bool otherStands)
    {
        var askCommitted = ask.CommittedAt(atM);
        var otherCommitted = other.CommittedAt(otherAtM);
        if (askCommitted != otherCommitted) return askCommitted;
        if (askStands != otherStands) return askStands;
        if (!askCommitted && ask.Rung != other.Priority) return ask.Rung < other.Priority;
        if (!askCommitted && ask.Held != other.Held) return ask.Held;

        var askArrivalM = ask.ArrivalAt(atM);
        var otherArrivalM = other.ArrivalAt(otherAtM);
        if (askArrivalM != otherArrivalM) return askArrivalM < otherArrivalM;

        return ask.Of != other.Of ? ask.Of < other.Of : ask.Occupant < other.Occupant;
    }

    /// <summary>Whether one holder's body is on a stretch of one way.</summary>
    bool StandsOn(int way, int occupant, LaneRoster of, float fromM, float toM)
    {
        for (var at = _bodies[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var body = ref _slots[at];
            if (body.FromM >= toM) break;
            if (body.Occupant == occupant && body.Of == of && body.ToM > fromM) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether some other hold on the section a mark links to beats the asker there — and which, where one
    /// does. The first found is enough: the section is held whole or not at all.
    /// </summary>
    /// <remarks>
    /// <b>Another hold's marked section on the same way is no answer.</b> Two marked sections on one way are
    /// two holders whose ground each overlaps that way's ribbon, and where the two grounds overlap one
    /// another the two holders' own ways are marked against each other and meet there — so held against
    /// each other here, they were two cars refused a corner of pavement neither of them drives.
    /// </remarks>
    bool LosesTheSection(in PlannedAsk ask, float atM, in CrossedSection mark, out LaneClaim winner)
    {
        var way = mark.OnWay;
        for (var at = _planned[way]; at != NoSlot; at = _next[at])
        {
            ref readonly var other = ref _slots[at];
            if (other.FromM >= mark.ToM) break;
            if (other.ToM <= mark.FromM || other.Linked || Owns(ask, other)) continue;

            var overFromM = MathF.Max(mark.FromM, other.FromM);
            var overToM = MathF.Min(mark.ToM, other.ToM);
            if (Beats(
                    ask, atM, StandsOn(way, ask.Occupant, ask.Of, overFromM, overToM), other, overFromM,
                    StandsOn(way, other.Occupant, other.Of, overFromM, overToM)))
            {
                continue;
            }

            winner = other;
            return true;
        }

        winner = LaneClaim.Nothing;
        return false;
    }

    /// <summary>
    /// Every other hold's piece over <c>[fromM, toM)</c> of one way, cut back to where the taker met it — the
    /// whole of that hold beyond the metre, marked sections included.
    /// </summary>
    /// <param name="piecesOnly">
    /// Whether what is being laid is itself a marked section, which the other holds' marked sections on the
    /// same way do not meet (<see cref="LosesTheSection"/>).
    /// </param>
    void TakeFrom(int way, float fromM, float toM, in PlannedAsk ask, in LaneClaim taker, bool piecesOnly = false)
    {
        // Cutting a hold may take other pieces of it off this same list, so the walk starts again after each.
        var at = _planned[way];
        while (at != NoSlot)
        {
            ref readonly var other = ref _slots[at];
            if (other.FromM >= toM) break;
            if (other.ToM <= fromM || (piecesOnly && other.Linked) || Owns(ask, other))
            {
                at = _next[at];
                continue;
            }

            var atM = MathF.Max(fromM, other.FromM);
            CutHold(other.Hold, other.Linked ? other.LineFromM : other.LineFromM + (atM - other.FromM), taker, way);

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
    /// <b>A hold cut at one metre of its holder's line</b>: every piece of it beyond is taken out, the piece
    /// running over the metre ends there, and a marked section begun at or beyond it goes whole.
    /// </summary>
    void CutHold(int hold, float lineM, in LaneClaim cutBy, int cutOn)
    {
        for (var at = _holdFirst[hold]; at < _holdEnd[hold]; at++)
        {
            if (_gone[at]) continue;

            ref readonly var piece = ref _slots[at];
            if (piece.LineFromM >= lineM)
            {
                Unlink(at);
                continue;
            }

            if (piece.Linked) continue;

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
