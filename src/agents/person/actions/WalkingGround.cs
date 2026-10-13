using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;
using static TrafficSimulation.World.Road.LineWays;

namespace TrafficSimulation.Agents.Person.Actions;

/// <summary>
/// <b>The town as a walker's action reads it</b>: the fleet, the pavement's network, the ways and the ground each
/// covers, what is laid on them this rebuild — the walker's place on its route and the walk ahead of it, and <b>the
/// claim every walking action lays</b> (PER-26, TER-4c.8): its walk ahead, answered and laid like a driver's road,
/// and the grant it comes back as.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the same table and the same ladder as the road's</b> (<see cref="LaneOccupancy"/>, TER-5g), and
/// deliberately not a second mechanism: a walker is held off another walker the way a driver is held off
/// another car, by the ground the one in front stands on and by nothing else (TER-4c.5).
/// </para>
/// <para>
/// <b>A way is one lane of the pavement</b>, or the mitre between two of them. The two directions of a
/// pavement are two lanes a walking lane's width apart (WLK-8), so somebody coming the other way is on other
/// ground — which falls out of the ways rather than being tested for.
/// </para>
/// </remarks>
internal sealed class WalkingGround(
    PersonFleet people, LaneOccupancy occupancy, TownWays ways, WayLines lines,
    WalkingNetwork walking, SimConfig config)
{
    /// <summary>
    /// How many ways one walker's plan may run over: the way it is on and the ones its walk crosses onto
    /// before it gets where it is aiming. A bound on a stack span and not a figure behaviour reads.
    /// </summary>
    /// <remarks>
    /// <b>Reached, the ways at the far end go unplanned</b>, which is a walker planning less far than it
    /// might and never one planning through somebody. A corner is two short ways within a stride, so the
    /// count is what a body on a corner can cover rather than what a stretch of pavement suggests.
    /// </remarks>
    public const int MostWaysAlongAWalk = 5;

    public PersonFleet People { get; } = people;

    /// <summary>What is laid on the ways: the layer every reader reads, or a laying of the town's own while one lasts.</summary>
    public LaneOccupancy Occupancy { get; internal set; } = occupancy;

    public TownWays Ways { get; } = ways;

    public WayLines Lines { get; } = lines;

    public WalkingNetwork Walking { get; } = walking;

    public SimConfig Config { get; } = config;

    /// <summary>The hold each walker laid this rebuild, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    public int[] WalkHold { get; } = LaneOccupancy.NoHolds(people.Capacity);

    /// <summary>
    /// <b>Whose ground each walker's is</b>, as the town laid its body this rebuild (<see cref="HoldTheGroundAs"/>): its
    /// own, or that of the car it is one occupant with. Its pass is laid, kept and asked for as that same ground, so
    /// the walker's own body is never what refuses it (PER-28).
    /// </summary>
    readonly int[] _heldAs = Own(people.Capacity);
    readonly LaneRoster[] _heldAsRoster = Afoot(people.Capacity);

    static int[] Own(int count)
    {
        var occupants = new int[count];
        for (var person = 0; person < count; person++) occupants[person] = person;
        return occupants;
    }

    static LaneRoster[] Afoot(int count)
    {
        var rosters = new LaneRoster[count];
        Array.Fill(rosters, LaneRoster.Walking);
        return rosters;
    }

    /// <summary>Whose ground this walker's is laid as from here on — the town's to say (<see cref="HeldAs"/>).</summary>
    public void HoldTheGroundAs(int person, int occupant, LaneRoster roster)
    {
        _heldAs[person] = occupant;
        _heldAsRoster[person] = roster;
    }

    /// <summary>Whose ground this walker's was when its body was last laid, and in which roster.</summary>
    public int HeldAs(int person, out LaneRoster roster)
    {
        roster = _heldAsRoster[person];
        return _heldAs[person];
    }

    /// <summary>
    /// <b>How far in front of itself a walker plans</b>: what it needs to come to rest at the pace it walks —
    /// the one tick's walk, a walker having no acceleration of its own (PER-3) — plus the gap it keeps.
    /// </summary>
    /// <remarks>
    /// <b>Sized by the pace it walks at and not by what it is doing</b>: a walker stopped behind something
    /// plans the ground it would set off into, or nothing would ever say it meant to move. <b>And one figure
    /// for every walker</b>, since a walker's pace is its own and not the ground's (TER-2).
    /// </remarks>
    public float PlansAheadM => Config.PersonStepM + Config.PersonStandstillGapM;

    /// <summary>A stride and the ground either side of the line, which is as far as a body moves between two ticks.</summary>
    public float AStrideM => Config.PersonWalkAheadM + Config.WalkerOffLaneM;

    /// <summary>
    /// The town's way number for a way of a route chain, or <see cref="PersonFleet.NoWay"/> for the hop
    /// off the network. <b>The one place the chain's own encoding is spent</b>
    /// (<see cref="WalkingNetwork.IsACorner"/>): a stretch's own lane, or the complement of a corner's
    /// turn slot.
    /// </summary>
    public int WayOf(int code) =>
        code == RouteChain.NoWay || code == WalkingNetwork.NoLane ? PersonFleet.NoWay
        : WalkingNetwork.IsACorner(code) ? Ways.OfMitre(WalkingNetwork.CornerOf(code))
        : Ways.OfFootway(code);

    /// <summary>
    /// <b>Whether the body has walked a way out as far as <paramref name="endM"/></b> — standing within a
    /// tick's walk of it, which is the tick that would carry it there.
    /// </summary>
    /// <remarks>
    /// <b>Never "at or past" it.</b> The body's metre is a projection onto the way's arcs, and on a way of
    /// several arcs the most it can come back as is the sum of their lengths in float — an ulp short of the
    /// way's own length on some. Asked exactly, a walker stood on the end of such a way aiming at the end of
    /// it and was never handed the next.
    /// </remarks>
    public bool HasWalkedOut(int person, float endM) => People.OnWayM[person] >= endM - Config.PersonStepM;

    /// <summary>
    /// <b>Whether this walker has walked its chain out</b> and what is left is the hop off the end of it — onto the
    /// goal, over ground the network does not number.
    /// </summary>
    public bool IsHopping(int person) => People.OnTheLastWay(person) && HasWalkedOut(person, EndOfTheWayM(person));

    /// <summary>
    /// How far along the way it is on this walk goes: the end of that way's own stretch of the chain, or
    /// where the destination stands on it where it is the last of them.
    /// </summary>
    public float EndOfTheWayM(int person)
    {
        if (People.OnTheLastWay(person)) return People.RouteToM[person];

        SpanOf(
            person, People.RouteWayBefore(person), People.CurrentRouteWay(person), People.PeekNextRouteWay(person),
            out _, out var endM);
        return endM;
    }

    /// <summary>One way's span between the ways either side of it (<see cref="WalkingNetwork.SpanOfWay"/>), and which three.</summary>
    readonly record struct KeptSpan(int Before, int Way, int After, float FromM, float EndM);

    /// <summary>
    /// <b>The span of the way each walker is walking</b>, kept beside it: asked of it several times a laying, and the
    /// same until it takes the next way — where reading it off the network is a lookup into whatever corner it is.
    /// </summary>
    readonly KeptSpan[] _spans = Enumerable.Repeat(
        new KeptSpan(WalkingNetwork.NoLane, int.MinValue, WalkingNetwork.NoLane, 0f, 0f), people.Capacity).ToArray();

    /// <summary>The span of the way a walker is walking, off what it keeps where that is of the same three ways.</summary>
    void SpanOf(int person, int before, int way, int after, out float fromM, out float endM)
    {
        ref var kept = ref _spans[person];
        if (kept.Before != before || kept.Way != way || kept.After != after)
        {
            Walking.SpanOfWay(before, way, after, out fromM, out endM);
            kept = new KeptSpan(before, way, after, fromM, endM);
        }

        (fromM, endM) = (kept.FromM, kept.EndM);
    }

    /// <summary>
    /// <b>Whether the body is on the ground of the way it is walking</b>, rather than merely near it: a
    /// pavement is walked a lane each way a lane's width apart (WLK-8), so past the half-band the body is
    /// nearer its sibling and the ground walk would name that one instead.
    /// </summary>
    /// <remarks>
    /// <b>It is regularly false for a stretch at the start of a walk</b>, and that is the state and not a
    /// fault: a walker standing on one lane of a pavement may be routed down the other (WLK-8,
    /// <see cref="WalkingNetwork.EntriesAt"/>), and until it has crossed to it, it is between the two —
    /// holding the ground its centre stands on like any body, and not yet on the way it walks. What it plans
    /// is that way all the same (<see cref="HasAWalkToPlan"/>): where it is going does not wait on its feet.
    /// </remarks>
    public bool IsOnItsWay(int person)
    {
        var way = People.CurrentRouteWay(person);
        return way != PersonFleet.NoWay && People.OffWayM[person] <= FactsOf(person, way).WidthM * 0.5f;
    }

    /// <summary>What a walker reads of the way it is walking every laying: how wide it is and the zebra it is the paint of.</summary>
    readonly record struct WayFacts(int Way, float WidthM, int Zebra);

    /// <summary>
    /// <b>The way each walker is walking, as it reads it</b>, kept beside it until it takes the next — where reading it off
    /// the network and the town's lines is a lookup into wherever that way is.
    /// </summary>
    readonly WayFacts[] _facts = Enumerable.Repeat(new WayFacts(int.MinValue, 0f, RibbonMarks.NoZebra), people.Capacity).ToArray();

    /// <summary>What a walker reads of one way of its walk, off what it keeps where that is the same way.</summary>
    WayFacts FactsOf(int person, int way)
    {
        ref var kept = ref _facts[person];
        if (kept.Way != way)
        {
            var town = WayOf(way);
            kept = new WayFacts(way, Walking.WayWidthM(way), town == PersonFleet.NoWay ? RibbonMarks.NoZebra : Lines.ZebraOf(town));
        }

        return kept;
    }

    /// <summary>
    /// <b>Whether this walker has a walk in front of it to plan</b> (PER-26): on its feet, walking a way of its
    /// route, and under nobody's hand — on that way's own ground or still crossing to it, since what it plans
    /// is where it is going and not where it stands.
    /// </summary>
    /// <remarks>
    /// A hand at the keys aims a walker wherever it likes and the route under it is whatever was last laid, so
    /// what would be planned is where that walker was going before the hand took it.
    /// </remarks>
    public bool HasAWalkToPlan(int person) =>
        People.Walking[person] && People.IsOnItsFeet(person) && People.Action[person] != PersonAction.Hand
        && WayOf(People.CurrentRouteWay(person)) != PersonFleet.NoWay;

    /// <summary>
    /// Whether this walker is a body on a line of its own rather than a shape on the pavement — <b>the
    /// whole of what says which of PER-25's two walks it is taking</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its place is read and never searched for</b>: the way is the one its route handed it and the
    /// metre along it is where the body projects onto that way's own line, worked out once a tick with
    /// the rest of the walk's state.
    /// </para>
    /// <para>
    /// <b>Being on a way and holding a route are two questions</b> (<see cref="IsOnItsWay"/>). A body
    /// routed down the far lane of the pavement it is standing on is between the two until it crosses,
    /// and a body shoved aside is off its way long before it has lost it.
    /// </para>
    /// </remarks>
    public bool IsAfoot(int person, out int way)
    {
        way = PersonFleet.NoWay;

        // <b>On this way's own ground, and not merely near it</b> (<see cref="IsOnItsWay"/>). Held to the
        // looser bar, this and the ground walk disagreed about which of a pavement's two lanes somebody
        // was standing on — and disagreeing about that is two answers about one body.
        //
        // <b>Losing the route is a different question and a much looser bar</b>: being shoved off your half of a
        // pavement is not a reason to search the town again.
        if (!HasAWalkToPlan(person) || !IsOnItsWay(person)) return false;

        way = WayOf(People.CurrentRouteWay(person));
        return true;
    }

    /// <summary>
    /// <b>Where on the way it is walking this body now stands, and how far off it</b> — one projection,
    /// answering both (SIM-7).
    /// </summary>
    /// <remarks>
    /// <b>Found and not carried.</b> A body is pushed about by the solver, so its place on a way is where
    /// it actually stands rather than where a step of arithmetic said it should be — and seeded at the
    /// metre it held a tick ago, the search is a stride of one way rather than a walk of the town.
    /// </remarks>
    /// <param name="windowM">
    /// How far either side of the metre it last held the way is searched. <b>A stride is enough while a
    /// walk is under way</b> and the whole way is what a body newly handed one needs, the metre it is
    /// carrying then being the previous way's or none at all.
    /// </param>
    public void PlaceItOnItsWay(int person, float windowM)
    {
        var aroundM = People.OnWayM[person];
        var arcs = ArcsAbout(
            person, People.CurrentRouteWay(person), MathF.Max(0f, aroundM - windowM), aroundM + windowM, out var cursor);
        if (arcs.Length == 0)
        {
            People.OffWayM[person] = 0f;
            return;
        }

        People.OnWayM[person] = Spline.ProjectM(arcs, People.PositionM[person], aroundM, windowM, ref cursor, out var offSq);
        People.OffWayM[person] = MathF.Sqrt(offSq);
    }

    /// <summary>How many of its way's arcs each walker keeps beside it (<see cref="ArcsAbout"/>).</summary>
    const int ArcsKept = 4;

    /// <summary>
    /// <b>The arcs of each walker's way about where it stands, kept beside it</b>: a run of <see cref="ArcsKept"/> of
    /// them, where on the way the first begins and the last ends, and whether they reach the way's two ends — so placing
    /// a walker reads its own few arcs, in the order the walkers are taken, rather than the network's, in the order of
    /// whatever ways they happen to walk.
    /// </summary>
    readonly ArcSeg[] _arcs = new ArcSeg[people.Capacity * ArcsKept];

    readonly int[] _arcsWay = Enumerable.Repeat(PersonFleet.NoWay, people.Capacity).ToArray();
    readonly int[] _arcsCount = new int[people.Capacity];
    readonly float[] _arcsFromM = new float[people.Capacity];
    readonly float[] _arcsToM = new float[people.Capacity];
    readonly bool[] _arcsFromTheHead = new bool[people.Capacity];
    readonly bool[] _arcsToTheEnd = new bool[people.Capacity];

    /// <summary>
    /// <b>The arcs of one walker's way that a walk over <c>[fromM, toM]</c> of it reads</b>, and where on the way the first
    /// of them begins: its own where they are every arc of the way reaching over that stretch, kept again from the way's
    /// where they are not, and the way's own where that stretch is more than <see cref="ArcsKept"/> of them.
    /// </summary>
    /// <remarks>
    /// <b>The same answer to the bit</b>: where a kept run begins is added up from the way's head, arc by arc, exactly as a
    /// walk from the head adds it up (<see cref="SplineCursor"/>).
    /// </remarks>
    ReadOnlySpan<ArcSeg> ArcsAbout(int person, int way, float fromM, float toM, out SplineCursor cursor)
    {
        if (!Holds(person, way, fromM, toM)) KeepTheArcsAbout(person, way, fromM);

        if (!Holds(person, way, fromM, toM))
        {
            cursor = default;
            return Walking.WayArcs(way);
        }

        cursor = new SplineCursor { PieceStartM = _arcsFromM[person] };
        return _arcs.AsSpan(person * ArcsKept, _arcsCount[person]);
    }

    /// <summary>
    /// <b>Where on its way a walker's walk is <paramref name="atM"/> along it</b> — off the arcs it keeps where they begin
    /// at the way's head and reach past there, which samples them as the way's own would be, and off the way's otherwise.
    /// </summary>
    public SplineSample OnItsWayAt(int person, int way, float atM)
    {
        var kept = _arcsWay[person] == way && _arcsFromTheHead[person]
                   && (_arcsToTheEnd[person] || atM + SampledWithinM < _arcsToM[person]);
        return Spline.SampleAt(kept ? _arcs.AsSpan(person * ArcsKept, _arcsCount[person]) : Walking.WayArcs(way), atM);
    }

    /// <summary>
    /// How far short of the end of a walker's kept arcs a place has to be to be sampled off them: what a way's metres
    /// counted down arc by arc and added up arc by arc can differ by is a float's rounding, and this is a millimetre.
    /// </summary>
    const float SampledWithinM = 1e-3f;

    /// <summary>
    /// Whether a walker's kept arcs are every one of its way's that reaches over <c>[fromM, toM]</c>: none before them
    /// ends at or past <paramref name="fromM"/>, and none after them begins at or short of <paramref name="toM"/>.
    /// </summary>
    bool Holds(int person, int way, float fromM, float toM) =>
        _arcsWay[person] == way && (_arcsFromTheHead[person] || _arcsFromM[person] < fromM)
        && (_arcsToTheEnd[person] || _arcsToM[person] > toM);

    /// <summary>
    /// <b>A walker's way's arcs kept</b> from the first that reaches <paramref name="fromM"/> — or from further back where
    /// the way has no more than that left to keep, so a short way is kept whole and is read from its head.
    /// </summary>
    void KeepTheArcsAbout(int person, int way, float fromM)
    {
        var arcs = Walking.WayArcs(way);
        var reaching = 0;
        var reachingM = 0f;
        while (reaching < arcs.Length - 1 && reachingM + arcs[reaching].LengthM < fromM)
        {
            reachingM += arcs[reaching].LengthM;
            reaching++;
        }

        var first = Math.Min(reaching, Math.Max(0, arcs.Length - ArcsKept));
        var startM = 0f;
        for (var at = 0; at < first; at++) startM += arcs[at].LengthM;

        var count = Math.Min(ArcsKept, arcs.Length - first);
        arcs.Slice(first, count).CopyTo(_arcs.AsSpan(person * ArcsKept, ArcsKept));
        var toM = startM;
        foreach (ref readonly var arc in arcs.Slice(first, count)) toM += arc.LengthM;

        _arcsWay[person] = way;
        _arcsCount[person] = count;
        _arcsFromM[person] = startM;
        _arcsToM[person] = toM;
        _arcsFromTheHead[person] = first == 0;
        _arcsToTheEnd[person] = first + count == arcs.Length;
    }

    /// <summary>
    /// The ways of the pavement under a stretch of one walk — from the body's own place on its way to
    /// <paramref name="aheadM"/> in front of it — each with the metres of its own that the stretch covers and
    /// where its near edge falls back on the walk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The walk is its own measure here</b>, and <see cref="LineWay.LineFromM"/> is a distance from the
    /// body rather than from a line's origin: a walked line is re-laid from wherever the body has got to,
    /// so it has no origin two walkers could be compared against.
    /// </para>
    /// <para>
    /// <b>A station's way is the ground walked to reach it</b>, so the stretch between two stations belongs
    /// to the later of the two — which is what puts the mitre before a corner on the corner rather than on
    /// the pavement leading up to it.
    /// </para>
    /// </remarks>
    public int WaysAlongTheWalk(int person, float aheadM, Span<LineWay> into)
    {
        var route = People.RouteOf(person);
        var count = People.RouteCount[person];
        var at = People.RouteAt(person);
        if (at < 0 || at >= count) return 0;

        var written = 0;
        var walkedM = 0f;

        for (var slot = at; slot < count && walkedM < aheadM && written < into.Length; slot++)
        {
            var townWay = WayOf(route[slot]);
            if (townWay == PersonFleet.NoWay) break;

            WalkedOfSlot(person, slot, out var fromM, out var endM);
            if (endM <= fromM) continue;

            into[written++] = new LineWay(townWay, fromM, fromM + MathF.Min(endM - fromM, aheadM - walkedM), walkedM);
            walkedM += endM - fromM;
        }

        return written;
    }

    /// <summary>
    /// <b>The stretch of one way of this walker's chain that the walk still covers</b>: the way's span between
    /// the ways either side of it, from the body's own place on the way it is on — every way after that being
    /// stated from wherever the walk joins it — and to where the walk stops on the last.
    /// </summary>
    public void WalkedOfSlot(int person, int slot, out float fromM, out float endM)
    {
        var route = People.RouteOf(person);
        var count = People.RouteCount[person];
        var before = slot > 0 ? route[slot - 1] : WalkingNetwork.NoLane;
        var after = slot + 1 < count ? route[slot + 1] : WalkingNetwork.NoLane;
        var walking = slot == People.RouteAt(person);
        if (walking) SpanOf(person, before, route[slot], after, out fromM, out endM);
        else Walking.SpanOfWay(before, route[slot], after, out fromM, out endM);

        if (slot == count - 1) endM = MathF.Min(endM, People.RouteToM[person]);
        if (walking) fromM = MathF.Max(fromM, People.OnWayM[person]);
    }

    /// <summary>
    /// <b>How far down its route a walker has come from a place on it</b> — every way between taken over the stretch
    /// of it the route covers, which is the walk's own measure (<see cref="WaysAlongTheWalk"/>).
    /// </summary>
    public float WalkedSince(int person, int slot, float fromM)
    {
        var at = People.RouteAt(person);
        if (at == slot) return People.OnWayM[person] - fromM;

        var route = People.RouteOf(person);
        var count = People.RouteCount[person];
        var walkedM = -fromM;
        for (var way = slot; way < at; way++)
        {
            Walking.SpanOfWay(
                way > 0 ? route[way - 1] : WalkingNetwork.NoLane, route[way],
                way + 1 < count ? route[way + 1] : WalkingNetwork.NoLane, out var spanFromM, out var spanToM);
            walkedM += spanToM - (way == slot ? 0f : spanFromM);
        }

        Walking.SpanOfWay(
            route[at - 1], route[at], at + 1 < count ? route[at + 1] : WalkingNetwork.NoLane, out var onFromM, out _);
        return walkedM + People.OnWayM[person] - onFromM;
    }

    /// <summary>
    /// <b>Where the walk is a distance down it</b> from the body's own place, over the pavement's lanes and the
    /// mitres at its corners alike. False where the route does not reach that far.
    /// </summary>
    [SkipLocalsInit]
    public bool OnTheWalkAt(int person, float aheadM, out SplineSample at)
    {
        at = default;
        Span<LineWay> pieces = stackalloc LineWay[MostWaysAlongAWalk];

        // A tick's walk past it, so the place the body stands on is on a piece of the walk too.
        var count = WaysAlongTheWalk(person, aheadM + Config.PersonStepM, pieces);
        for (var index = 0; index < count; index++)
        {
            ref readonly var piece = ref pieces[index];
            if (aheadM > piece.LineFromM + (piece.ToM - piece.FromM)) continue;

            at = Spline.SampleAt(Lines.LineOf(piece.Way, out _), piece.FromM + MathF.Max(0f, aheadM - piece.LineFromM));
            return true;
        }

        return false;
    }

    /// <summary>
    /// <b>A straight walk, planned</b> (PER-25, PER-26) — the hop off the end of the route, the walk back onto it, an
    /// officer's to their post: <b>the ways the world reads its body on, and the way it lands on</b> — the ground either
    /// side of the body it would set off into (<see cref="PlansAheadM"/>) on each way under it
    /// (<paramref name="under"/>), and the stretch of <paramref name="ontoWay"/> from where the body stands abeam of it to
    /// <paramref name="ontoM"/>, where the straight to <paramref name="toM"/> lands, and as far past both, once the edge
    /// of the way is inside the reach of its plan.
    /// </summary>
    /// <remarks>
    /// <b>An actor off its ways goes straight back to one</b> (the root decision log, 2026-10-13): what it crosses on the
    /// way is ground the world reads its body on as it goes (TER-4c.2), held as it stands on it, and a walk planning
    /// nothing is granted the whole of the straight (<see cref="Claim"/>). A straight run along the way it lands on — a
    /// hop over a zebra's paint to a place on it — holds the paint it walks.
    /// </remarks>
    public int TheStraightAhead(
        int person, Vector2 toM, int ontoWay, float ontoM, float frontM, float aheadM, ReadOnlySpan<WayCover> under,
        Span<LineWay> into)
    {
        var written = 0;
        foreach (ref readonly var cover in under)
        {
            if (written == into.Length - 1) break;
            if (cover.Way == ontoWay) continue;

            into[written++] = new LineWay(
                cover.Way, MathF.Max(0f, cover.FromM - PlansAheadM),
                MathF.Min(Ways.LengthM(cover.Way), cover.ToM + PlansAheadM), frontM);
        }

        return written + TheLanding(person, toM, ontoWay, ontoM, frontM, aheadM, into[written..]);
    }

    /// <summary>The stretch of the way a straight lands on (<see cref="TheStraightAhead"/>), or nothing.</summary>
    int TheLanding(int person, Vector2 toM, int ontoWay, float ontoM, float frontM, float aheadM, Span<LineWay> into)
    {
        if (ontoWay == PersonFleet.NoWay || into.IsEmpty) return 0;

        var radiusM = People.RadiusM[person];
        var atM = People.PositionM[person];
        var line = Lines.LineOf(ontoWay, out var widthM);
        var abeamM = Spline.ProjectM(line, atM, ontoM, Vector2.Distance(atM, toM) + widthM, out var offSq);
        var landsAtM = MathF.Max(frontM, MathF.Sqrt(offSq) - radiusM - (widthM * 0.5f));        if (landsAtM > frontM + aheadM) return 0;

        // A body is never level with ground its own plan holds (TER-4c.1), so one standing where it lands holds the ground
        // either side of it that it would set off into.
        var pastM = radiusM + PlansAheadM;
        into[0] = new LineWay(
            ontoWay, MathF.Max(0f, MathF.Min(abeamM, ontoM) - pastM),
            MathF.Min(Ways.LengthM(ontoWay), MathF.Max(abeamM, ontoM) + pastM), landsAtM);
        return 1;
    }

    /// <summary>
    /// <b>The way a hop off the end of a walk lands on</b>, and where on it: the last way of the walk, or a way joined at
    /// its end — a zebra's paint the place stands on — whichever has <paramref name="toM"/> inside its band. Read off the
    /// walk's own ways and nothing else; <see cref="PersonFleet.NoWay"/> where the place is off all of them.
    /// </summary>
    public int WhereTheHopLands(int person, Vector2 toM, out float ontoM)
    {
        ontoM = 0f;
        var code = People.CurrentRouteWay(person);
        if (code == RouteChain.NoWay || code == WalkingNetwork.NoLane || WalkingNetwork.IsACorner(code)) return PersonFleet.NoWay;

        if (Lands(Ways.OfFootway(code), toM, out ontoM)) return Ways.OfFootway(code);

        foreach (var onto in Walking.TurnsFrom(code))
        {
            var way = Ways.OfFootway(onto);
            if (Lands(way, toM, out ontoM)) return way;
        }

        return PersonFleet.NoWay;
    }

    /// <summary>Whether a place stands inside a way's band, and where along it.</summary>
    bool Lands(int way, Vector2 toM, out float alongM)
    {
        var line = Lines.LineOf(way, out var widthM);
        alongM = Spline.ProjectM(line, toM, 0f, Ways.LengthM(way), out var offSq);
        return offSq <= widthM * widthM * 0.25f;
    }

    /// <summary>
    /// <b>A walker walking straight at a place aims at it</b>, and no further along the straight than it was granted
    /// (PER-26) — so one granted nothing stands where it is.
    /// </summary>
    public void AimAlongTheStraight(int person, Vector2 toM)
    {
        var towards = toM - People.PositionM[person];
        var reachM = MathF.Max(0f, People.GrantM[person]);
        People.DestinationM[person] = towards.LengthSquared() > reachM * reachM
            ? People.PositionM[person] + (Vector2.Normalize(towards) * reachM)
            : toM;
    }

    /// <summary>How many pieces one walker's body may be laid in: every way the atlas finds under it, and its own.</summary>
    public const int MostPiecesOfABody = RibbonAtlas.MostWaysUnderABody + 1;

    /// <summary>
    /// <b>The ground a walker's body stands on</b> (TER-4c.2), as the stretches the town lays for it in the order it lays
    /// them: every way the world read under it as a single wheel at its centre, held its radius either side
    /// (<paramref name="under"/>) — and the way it is walking, over the stretch its own body takes of it. Whose ground it
    /// is is the town's to say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A body in a lane holds the lane</b>, on the paint or off it, walking, knocked down or under a hand:
    /// which ground it stands on is a fact about the ground (TER-4c.2).
    /// </para>
    /// <para>
    /// <b>Its own way is read off its place on it</b> (<see cref="PersonFleet.OnWayM"/>), because the lattice point
    /// nearest its centre may stand across the edge of that way's band — and the way it walks is the one way it must
    /// never be missing from.
    /// </para>
    /// </remarks>
    /// <param name="into">Room for <see cref="MostPiecesOfABody"/>.</param>
    /// <returns>How many were written.</returns>
    public int BodyCover(int person, ReadOnlySpan<WayCover> under, Span<WayCover> into)
    {
        var written = 0;
        foreach (ref readonly var cover in under) into[written++] = cover;

        var walking = People.OnWay[person];
        if (walking == PersonFleet.NoWay) return written;

        var radiusM = People.RadiusM[person];
        var atM = People.OnWayM[person];
        into[written++] = new WayCover(
            walking, MathF.Max(0f, atM - radiusM), MathF.Min(Ways.LengthM(walking), atM + radiusM));
        return written;
    }

    /// <summary>
    /// <b>A walker's claim laid</b> (PER-26, TER-4c.8) over the ways its action walks, as one hold held at a walker's
    /// rung (<see cref="ClaimPriority.Afoot"/>, PER-27) and answered with every other plan, as a driver's is
    /// (<see cref="LaneOccupancy.Answer"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A hold with nothing to lay is still laid</b>: a walk whose ground lies on no way — a lawn, a yard, what is
    /// left of it inside the body's own reach — has claimed all there is of it, where a walker laying none would be
    /// granted nothing.
    /// </para>
    /// <para>
    /// <b>A body its own is level with cuts nothing</b> (TER-4c.1): a straight walk crosses a way along the width of the
    /// walker's own body as well as ahead of it, and each piece is asked from past that body (<see cref="LaneOccupancy.Ask"/>).
    /// </para>
    /// </remarks>
    /// <param name="occupant">Whose ground it is held as, as the town names it: the walker's own, or an officer's car's.</param>
    /// <param name="begunOn">
    /// <b>The hold of a pass the walker has begun</b> (TER-4c.6), which the walk carries on from — so whatever refuses the
    /// pass holds the walker where it stands — or <see cref="LaneOccupancy.NoHold"/>.
    /// </param>
    public void Claim(
        int person, int occupant, LaneRoster roster, ReadOnlySpan<LineWay> walk, int begunOn = LaneOccupancy.NoHold)
    {
        var hold = begunOn != LaneOccupancy.NoHold ? begunOn : Occupancy.BeginHold(occupant, roster);
        WalkHold[person] = hold;
        foreach (ref readonly var way in walk)
        {
            Occupancy.Ask(hold, way.Way, way.FromM, way.ToM, way.LineFromM, ClaimPriority.Afoot);
        }
    }

    /// <summary>
    /// <b>What the walker got</b>: how far past the front of its body its plan survived, less the gap it keeps
    /// off what cut it — infinity where nothing did, and nothing where it planned nothing (PER-25b).
    /// </summary>
    public void ReadTheGrant(int person)
    {
        var hold = WalkHold[person];
        var cut = Occupancy.CutOf(hold);
        People.GrantM[person] = hold == LaneOccupancy.NoHold ? 0f
            : !cut.Refused ? float.PositiveInfinity
            : cut.CutLineM - Config.PersonStandstillGapM - People.RadiusM[person];
        People.OnCrossing[person] = TheZebraOf(person, cut.Way);
    }

    /// <summary>Whether this walker stands where a light's hold refused its plan — the wait that spends no clock (TLT-2a).</summary>
    public bool HeldByALight(int person) => People.GrantM[person] <= 0f && Occupancy.CutOf(WalkHold[person]).ByALight;

    /// <summary>
    /// <b>The zebra this walker is walking, or is held off</b>: the paint its route is on, or the paint its plan
    /// was refused on while it stands — read off its own route and hold, so what it is said to be doing is
    /// what the reservations did to it.
    /// </summary>
    int TheZebraOf(int person, int holdCutOn)
    {
        if (People.Inside[person].Any) return PersonFleet.NoCrossing;

        var walking = People.CurrentRouteWay(person);
        if (walking != PersonFleet.NoWay && FactsOf(person, walking).Zebra is var zebra && zebra != RibbonMarks.NoZebra)
        {
            return zebra;
        }

        var cutOn = People.GrantM[person] <= 0f ? holdCutOn : PersonFleet.NoWay;
        return cutOn >= 0 && Lines.IsTheCrossing(cutOn) ? Lines.ZebraOf(cutOn) : PersonFleet.NoCrossing;
    }
}
