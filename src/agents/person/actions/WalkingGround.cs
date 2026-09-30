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
    PersonFleet people, LaneOccupancy occupancy, RibbonAtlas atlas, TownWays ways, WayLines lines,
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

    public LaneOccupancy Occupancy { get; } = occupancy;

    public RibbonAtlas Atlas { get; } = atlas;

    public TownWays Ways { get; } = ways;

    public WayLines Lines { get; } = lines;

    public WalkingNetwork Walking { get; } = walking;

    public SimConfig Config { get; } = config;

    /// <summary>
    /// How many runs one walker's straight is kept in (<see cref="SweptGround"/>). A bound on the table and not a
    /// figure behaviour reads: a straight is a hop off a route, a walk back onto one, or an officer's across a street.
    /// </summary>
    const int MostRunsOfAStraight = 128;

    /// <summary>The straight each walker walking straight at a place committed to, and the ground under it swept once.</summary>
    readonly SweptGround _straight = new(people.Capacity, MostRunsOfAStraight);

    readonly Vector2[] _straightFromM = new Vector2[people.Capacity];

    readonly Vector2[] _straightToM = Unset(people.Capacity);

    /// <summary>The hold each walker laid this rebuild, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    public int[] WalkHold { get; } = new int[people.Capacity];

    static Vector2[] Unset(int count)
    {
        var places = new Vector2[count];
        Array.Fill(places, new Vector2(float.NaN));
        return places;
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

    /// <summary>How fast this walker is going the way it is facing, which is the only direction it walks in.</summary>
    public float AlongItsWalkMps(int person) =>
        Vector2.Dot(People.VelocityMps[person], Heading.Unit(People.HeadingRad[person]));

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

        Walking.SpanOfWay(
            People.RouteWayBefore(person), People.CurrentRouteWay(person), People.PeekNextRouteWay(person),
            out _, out var endM);
        return endM;
    }

    /// <summary>
    /// <b>Whether the body is on the ground of the way it is walking</b>, rather than merely near it: a
    /// pavement is walked a lane each way a lane's width apart (WLK-8), so past the half-band the body is
    /// nearer its sibling and the ground walk would name that one instead.
    /// </summary>
    /// <remarks>
    /// <b>It is regularly false for a stretch at the start of a walk</b>, and that is the state and not a
    /// fault: a walker standing on one lane of a pavement may be routed down the other (WLK-8,
    /// <see cref="WalkingNetwork.EntriesNear"/>), and until it has crossed to it, it is between the two —
    /// holding the ground its own disc is over like any body, and not yet on the way it walks. What it plans
    /// is that way all the same (<see cref="HasAWalkToPlan"/>): where it is going does not wait on its feet.
    /// </remarks>
    public bool IsOnItsWay(int person)
    {
        var way = People.CurrentRouteWay(person);
        return way != PersonFleet.NoWay && People.OffWayM[person] <= Walking.WayWidthM(way) * 0.5f;
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
        var arcs = Walking.WayArcs(People.CurrentRouteWay(person));
        if (arcs.Length == 0)
        {
            People.OffWayM[person] = 0f;
            return;
        }

        var atM = People.PositionM[person];
        var alongM = Spline.ProjectM(arcs, atM, People.OnWayM[person], windowM);

        People.OnWayM[person] = alongM;
        People.OffWayM[person] = (Spline.SampleAt(arcs, alongM).PositionM - atM).Length();
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
        Walking.SpanOfWay(
            slot > 0 ? route[slot - 1] : WalkingNetwork.NoLane, route[slot],
            slot + 1 < count ? route[slot + 1] : WalkingNetwork.NoLane, out fromM, out endM);

        if (slot == count - 1) endM = MathF.Min(endM, People.RouteToM[person]);
        if (slot == People.RouteAt(person)) fromM = MathF.Max(fromM, People.OnWayM[person]);
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
    /// officer's to their post: every way under the straight from the front of the body towards
    /// <paramref name="toM"/>, as far as it would take to come to rest — nearest first, each from where the straight
    /// comes onto it — read off the ground the straight was swept over when the walker set off down it
    /// (<see cref="CommitTheStraight"/>).
    /// </summary>
    /// <remarks>
    /// <b>The ground the network does not walk is still ground somebody may have</b>: the pavement round a door,
    /// or the carriageway an ordered walk strikes out over. A way the straight crosses rather than runs along is
    /// held over the stretch the body's width takes of it, which is what the atlas reads a body over too.
    /// </remarks>
    public int TheStraightAhead(int person, Vector2 toM, float frontM, float aheadM, Span<LineWay> into)
    {
        if (!IsOnTheStraightTo(person, toM)) CommitTheStraight(person, toM);

        var fromM = _straightFromM[person];
        var line = toM - fromM;
        var lengthM = line.Length();
        var alongM = lengthM > 0f ? Vector2.Dot(People.PositionM[person] - fromM, line / lengthM) : 0f;
        var nearM = alongM + frontM;
        var farM = MathF.Min(lengthM, nearM + aheadM);
        if (farM <= nearM) return 0;

        var strideM = People.RadiusM[person] * 2f;
        var written = 0;
        foreach (ref readonly var run in _straight.Of(person))
        {
            if (run.FirstAtM > farM || written == into.Length) break;
            if (run.LastAtM + strideM < nearM) continue;

            into[written++] = new LineWay(run.Way, run.FromM, run.ToM, MathF.Max(frontM, run.FirstAtM - alongM));
        }

        return written;
    }

    /// <summary>
    /// Whether the walker walks the straight it committed to towards <paramref name="toM"/> — the same place, and its
    /// body within a stride of the line, since a walker shoved off it is somewhere the straight was not drawn from.
    /// </summary>
    bool IsOnTheStraightTo(int person, Vector2 toM)
    {
        if (_straightToM[person] != toM) return false;

        var fromM = _straightFromM[person];
        var line = toM - fromM;
        var lengthSquaredM2 = line.LengthSquared();
        var shareOf = lengthSquaredM2 > 0f
            ? Math.Clamp(Vector2.Dot(People.PositionM[person] - fromM, line) / lengthSquaredM2, 0f, 1f)
            : 0f;
        return (People.PositionM[person] - (fromM + (line * shareOf))).Length() <= AStrideM;
    }

    /// <summary>
    /// <b>A straight committed to</b> (TER-4c.8): from where the walker stands to <paramref name="toM"/>, the ground
    /// under it swept once — a body's width at a time, kept as runs of two (<see cref="SweptGround"/>) — for its plan
    /// to be read off as it walks. What does not fit is planned no further, which is a walk planning short and never
    /// one planning through somebody.
    /// </summary>
    [SkipLocalsInit]
    void CommitTheStraight(int person, Vector2 toM)
    {
        var fromM = People.PositionM[person];
        _straightFromM[person] = fromM;
        _straightToM[person] = toM;
        _straight.Clear(person);

        var line = toM - fromM;
        var lengthM = line.Length();
        var forward = lengthM > 0f ? line / lengthM : Vector2.Zero;
        var radiusM = People.RadiusM[person];
        var strideM = radiusM * 2f;
        Span<WayCover> under = stackalloc WayCover[RibbonAtlas.MostWaysUnderABody];
        for (var atM = 0f; ; atM += strideM)
        {
            var toStationM = MathF.Min(lengthM, atM + strideM);
            var count = UnderTheStretch(fromM + (forward * atM), fromM + (forward * toStationM), radiusM, under);
            if (!_straight.Station(person, atM, strideM * 2f, under[..count]) || toStationM >= lengthM) return;
        }
    }

    /// <summary>
    /// The ground a body walking straight from one place to another covers, as the ways the atlas finds under it —
    /// <paramref name="halfWidthM"/> either side of the straight and past both ends of it.
    /// </summary>
    public int UnderTheStretch(Vector2 fromM, Vector2 toM, float halfWidthM, Span<WayCover> under)
    {
        var along = toM - fromM;
        var lengthM = along.Length();
        return lengthM <= 0f
            ? Atlas.UnderDisc(fromM, halfWidthM, under)
            : Atlas.UnderBox((fromM + toM) * 0.5f, along / lengthM, (lengthM * 0.5f) + halfWidthM, halfWidthM, under);
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

    /// <summary>
    /// <b>A walker's claim laid</b> (PER-26, TER-4c.8) over the ways its action walks, as one hold held at a walker's
    /// rung (<see cref="ClaimPriority.Afoot"/>, PER-27) — answered before it is laid, as a driver's plan is, so nothing
    /// is taken off another plan for a walk that then stops short of it.
    /// </summary>
    /// <remarks>
    /// <b>A hold with nothing to lay is still laid</b>: a walk whose ground lies on no way — a lawn, a yard, what is
    /// left of it inside the body's own reach — has claimed all there is of it, where a walker laying none would be
    /// granted nothing.
    /// </remarks>
    /// <param name="occupant">Whose ground it is held as, as the town names it: the walker's own, or an officer's car's.</param>
    public void Claim(int person, int occupant, LaneRoster roster, ReadOnlySpan<LineWay> walk)
    {
        var hold = Occupancy.BeginHold(Config.PersonStandstillGapM);
        WalkHold[person] = hold;
        LayTheWalk(person, occupant, roster, hold, walk, AnswerTheWalk(person, occupant, roster, hold, walk));
    }

    /// <summary>
    /// <b>A walker held where it stands by a body inside the ground its pass covers</b> (TER-4c.6): the ground up to
    /// there is the pass's own, and all it is held by is what stands in it.
    /// </summary>
    public void HoldShortOf(int person, in LaneClaim body, int on)
    {
        var held = Occupancy.BeginHold(Config.PersonStandstillGapM);
        WalkHold[person] = held;
        Occupancy.EndHold(held, People.RadiusM[person], Config.PersonStandstillGapM, body, on);
    }

    /// <summary>
    /// <b>Whether this walker's claim may move when answered again</b>: laid, and cut short by another plan rather
    /// than by a body — and where it ends now.
    /// </summary>
    public bool MayMove(int person, out float endsAtM)
    {
        endsAtM = float.PositiveInfinity;
        var hold = WalkHold[person];
        if (hold == LaneOccupancy.NoHold) return false;

        endsAtM = Occupancy.HoldEndsAtM(hold, out _, out var cutBy);
        return !float.IsPositiveInfinity(endsAtM) && !(cutBy.Found && cutBy.HasBody);
    }

    /// <summary>
    /// <b>This walker's claim answered again against what every other came to</b>, and laid again where the answer
    /// has moved off <paramref name="endsAtM"/> (<see cref="MayMove"/>) — true where it did.
    /// </summary>
    public bool Settle(int person, float endsAtM, int occupant, LaneRoster roster, ReadOnlySpan<LineWay> walk)
    {
        var hold = WalkHold[person];
        var answer = AnswerTheWalk(person, occupant, roster, hold, walk);
        if (answer.CutLineM == endsAtM) return false;

        Occupancy.ReopenHold(hold);
        LayTheWalk(person, occupant, roster, hold, walk, answer);
        return true;
    }

    /// <summary><b>How far this walker's plan can be had</b>, read and never written.</summary>
    /// <remarks>
    /// <b>A body its own is level with cuts nothing</b> (TER-4c.1): it is beside the walker or behind it. A straight
    /// walk crosses a way along the width of the walker's own body as well as ahead of it, so what it is level with
    /// there is read off where that body lies on the way, and not off where the straight comes onto it.
    /// </remarks>
    PlanAnswer AnswerTheWalk(int person, int occupant, LaneRoster roster, int hold, ReadOnlySpan<LineWay> walk)
    {
        for (var index = 0; index < walk.Length; index++)
        {
            ref readonly var way = ref walk[index];
            var levelWithM = MathF.Max(way.FromM, Occupancy.BodyReachesToM(way.Way, occupant, roster));
            var reachM = Occupancy.Reach(
                WalkAsk(person, occupant, roster, hold, way), way.Way, way.ToM, levelWithM, out var cutBy);
            if (reachM >= way.ToM) continue;

            return new PlanAnswer(OnTheLineM(way, reachM), Config.PersonStandstillGapM, cutBy, way.Way, index, reachM);
        }

        return PlanAnswer.Whole;
    }

    /// <summary><b>A walker's plan laid</b> over what its answer left, and finished with what it came to.</summary>
    void LayTheWalk(
        int person, int occupant, LaneRoster roster, int hold, ReadOnlySpan<LineWay> walk, in PlanAnswer answer)
    {
        for (var index = 0; index < walk.Length; index++)
        {
            ref readonly var way = ref walk[index];
            if (way.LineFromM >= answer.CutLineM) break;

            Occupancy.Take(WalkAsk(person, occupant, roster, hold, way), way.Way, LaidToM(answer, index, way));
            if (index == answer.CutAt) break;
        }

        Occupancy.EndHold(hold, answer.CutLineM, answer.MarginM, answer.CutBy, answer.CutOn);
    }

    /// <summary>One piece of a walker's plan as the terms it is asked on.</summary>
    PlannedAsk WalkAsk(int person, int occupant, LaneRoster roster, int hold, in LineWay way) =>
        new(
            hold, occupant, roster, ClaimPriority.Afoot, way.FromM, way.LineFromM,
            way.LineFromM - People.RadiusM[person], float.NegativeInfinity, AlongItsWalkMps(person));

    /// <summary>
    /// <b>What the walker got</b>: how far past the front of its body its plan survived, less the gap it keeps
    /// off what cut it — infinity where nothing did, and nothing where it planned nothing (PER-25b).
    /// </summary>
    public void ReadTheGrant(int person)
    {
        var hold = WalkHold[person];
        var endsAtM = Occupancy.HoldEndsAtM(hold, out var marginM, out _);
        People.GrantM[person] = hold == LaneOccupancy.NoHold ? 0f
            : float.IsPositiveInfinity(endsAtM) ? float.PositiveInfinity
            : endsAtM - marginM - People.RadiusM[person];
        People.OnCrossing[person] = TheZebraOf(person, hold);
    }

    /// <summary>Whether this walker stands where a light's hold refused its plan — the wait that spends no clock (TLT-2a).</summary>
    public bool HeldByALight(int person)
    {
        if (People.GrantM[person] > 0f) return false;

        Occupancy.HoldEndsAtM(WalkHold[person], out _, out var cutBy);
        return cutBy.Found && !cutBy.HasBody && cutBy.Of == LaneRoster.Signal;
    }

    /// <summary>
    /// <b>The zebra this walker is walking, or is held off</b>: the paint its route is on, or the paint its plan
    /// was refused on while it stands — read off its own route and hold, so what it is said to be doing is
    /// what the reservations did to it.
    /// </summary>
    int TheZebraOf(int person, int hold)
    {
        if (People.Inside[person].Any) return PersonFleet.NoCrossing;

        var on = WayOf(People.CurrentRouteWay(person));
        if (on != PersonFleet.NoWay && Lines.IsTheCrossing(on)) return Lines.ZebraOf(on);

        var cutOn = People.GrantM[person] <= 0f ? Occupancy.HoldCutOn(hold) : PersonFleet.NoWay;
        return cutOn >= 0 && Lines.IsTheCrossing(cutOn) ? Lines.ZebraOf(cutOn) : PersonFleet.NoCrossing;
    }
}
