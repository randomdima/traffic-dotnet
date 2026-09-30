using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The walkers' half of the reservations</b> (PER-26, PER-27): the ground a body stands on, which the
/// physical layer holds (<see cref="LayTheWalkersBody"/>), and <b>the walk in front of it, planned and
/// answered like a driver's road</b> — the grant it comes back as is what the walker walks to
/// (<see cref="PersonFleet.GrantM"/>).
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
/// <para>
/// <b>Where a body stands is read off the way it is walking and never searched for across the town</b>:
/// it is the body's projection onto that way's own line, sought a stride either side of the metre it held a
/// tick ago (<see cref="PlaceItOnItsWay"/>). Only a body that is on no way at all plans nothing, and walks
/// straight back onto the network (PER-25).
/// </para>
/// </remarks>
internal sealed partial class TownWorld
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
    const int MostWaysAlongAWalk = 5;

    /// <summary>The hold each walker laid this rebuild, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    readonly int[] _walkerHold;

    /// <summary>
    /// <b>The pavement's two blocks of the town's numbering, as the runs of metres they are</b> — a lane
    /// each way down every stretch, and the mitre at every corner. Handed to <see cref="TownWays"/>, which
    /// is what makes them ways of the same table the carriageway's are.
    /// </summary>
    static float[] PavementLengthsM(WalkingNetwork walking, out float[] mitreLengthM)
    {
        var lanesM = new float[walking.Foot.EdgeCount];
        for (var edge = 0; edge < lanesM.Length; edge++) lanesM[edge] = walking.LaneLengthM(edge);

        mitreLengthM = new float[walking.TurnCount];
        for (var turn = 0; turn < mitreLengthM.Length; turn++) mitreLengthM[turn] = walking.JoinLengthM(turn);

        return lanesM;
    }

    /// <summary>
    /// <b>Where this walker stands on the pavement's own network</b>, or <see cref="PersonFleet.NoWay"/>
    /// where it is on none of it — <b>which is also what decides how it walks</b> (PER-25): a body on a way
    /// follows the line the network laid it, and a body on none of them walks straight at the nearest of
    /// them.
    /// </summary>
    /// <remarks>
    /// <b>Worked out before either network is laid, because both read it</b> (<see cref="RebuildLaneOccupancy"/>):
    /// the road needs the way a body on a crossing is walking to know which lane it stands in, and the
    /// statement in front of the body begins from the same place. Asked twice it was the same walk of the
    /// same line, and the two answers were a tick apart.
    /// </remarks>
    void StationTheWalker(int person)
    {
        People.OnWay[person] = PersonFleet.NoWay;

        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;

        // <b>Where the walk has got to, worked out once and written once</b> (SIM-7): the body's place on
        // the way it is walking, and the next way off the chain where it has walked this one out. Split
        // between here and the agent's own tick, the way and the metre along it were written at two different
        // moments and disagreed at the end of every one.
        WalkTheWay(person);

        if (IsAfoot(person, out var way)) People.OnWay[person] = way;
    }

    /// <summary>
    /// <b>The walk in front of this body, planned</b> (PER-26): from the front of it down the ways of its
    /// route, for as far as it would take to come to rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A body on no way of the network plans nothing</b>: it is walking straight at the network (PER-25)
    /// over ground the town does not number, and what it holds while it does is its body.
    /// </para>
    /// <para>
    /// <b>Every way of it is held at one rung</b> (<see cref="ClaimPriority.Afoot"/>, PER-27), a zebra's paint
    /// and the pavement to it included. What makes a zebra the whole zebra is the marks and not the walk
    /// (TER-5c.3): a metre of the paint places the lanes under all of it, and the traffic's plan over any of
    /// those holds all of the paint.
    /// </para>
    /// <para>
    /// <b>Answered before it is laid</b>, as a driver's plan is, so nothing is taken off another plan for a
    /// walk that then stops short of it.
    /// </para>
    /// </remarks>
    void PlanTheWalk(int person, Span<LineWay> walk)
    {
        _walkerHold[person] = LaneOccupancy.NoHold;

        // <b>A walker on a pass is held only by a body inside it</b> (TER-4c.6): the ground up to its end is the
        // pass's, and what it plans begins past there.
        if (People.Pass[person].Begun && TheBodyInTheSidestep(person, out var inTheWay, out var on))
        {
            var held = _occupancy.BeginHold(_config.PersonStandstillGapM);
            _walkerHold[person] = held;
            _occupancy.EndHold(held, People.RadiusM[person], _config.PersonStandstillGapM, inTheWay, on);
            return;
        }

        var ways = TheWalkAhead(person, walk);
        if (ways.IsEmpty) return;

        var hold = _occupancy.BeginHold(_config.PersonStandstillGapM);
        _walkerHold[person] = hold;
        LayTheWalk(person, hold, ways, AnswerTheWalk(person, hold, ways));
    }

    /// <summary>
    /// <b>The ways a walker's plan is laid down</b>, from the front of its body — what is behind that is the
    /// body's own, at p0 — for as far as it would take to come to rest: down its route, or over the ground of the
    /// hop off the end of it. Empty where it plans nothing.
    /// </summary>
    Span<LineWay> TheWalkAhead(int person, Span<LineWay> into)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any || !HasAWalkToPlan(person)) return default;

        var frontM = WalkPlannedFromM(person);
        if (IsHopping(person)) return into[..TheHopAhead(person, frontM, PlansAheadM, into)];

        var count = WaysAlongTheWalk(person, frontM + PlansAheadM, into);

        var first = 0;
        while (first < count && into[first].LineFromM + (into[first].ToM - into[first].FromM) <= frontM) first++;
        if (first == count) return default;

        if (into[first].LineFromM < frontM)
        {
            into[first] = into[first] with
            {
                FromM = into[first].FromM + (frontM - into[first].LineFromM), LineFromM = frontM,
            };
        }

        return into[first..count];
    }

    /// <summary>
    /// <b>The hop off the end of the route, planned</b> (PER-25, PER-26): every way the atlas finds under the
    /// straight from the front of the body towards where it is walking, as far as it would take to come to rest
    /// — nearest first, each from where the straight comes onto it.
    /// </summary>
    /// <remarks>
    /// <b>The ground the network does not walk is still ground somebody may have</b>: the pavement round a door,
    /// or the carriageway an ordered walk strikes out over. A way the straight crosses rather than runs along is
    /// held over the stretch the body's width takes of it, which is what the atlas reads a body over too.
    /// </remarks>
    [SkipLocalsInit]
    int TheHopAhead(int person, float frontM, float aheadM, Span<LineWay> into)
    {
        var bodyM = People.PositionM[person];
        var along = People.DestinationM[person] - bodyM;
        var lengthM = along.Length();
        var runM = MathF.Min(lengthM - frontM, aheadM);
        if (runM <= 0f) return 0;

        var forward = along / lengthM;
        Span<WayCover> under = stackalloc WayCover[MostWaysUnderABody];
        var count = _atlas.UnderBox(
            bodyM + (forward * (frontM + (runM * 0.5f))), forward, runM * 0.5f, People.RadiusM[person], under);

        var written = 0;
        for (var at = 0; at < count && written < into.Length; at++)
        {
            ref readonly var cover = ref under[at];
            var line = LineOfWay(cover.Way, out _);
            var enteredM = MathF.Min(
                Vector2.Dot(Spline.SampleAt(line, cover.FromM).PositionM - bodyM, forward),
                Vector2.Dot(Spline.SampleAt(line, cover.ToM).PositionM - bodyM, forward));
            var piece = new LineWay(cover.Way, cover.FromM, cover.ToM, MathF.Max(frontM, enteredM));

            // Nearest first, which a handful of ways under a stride is sorted by walking it once.
            var slot = written++;
            for (; slot > 0 && into[slot - 1].LineFromM > piece.LineFromM; slot--) into[slot] = into[slot - 1];
            into[slot] = piece;
        }

        return written;
    }

    /// <summary><b>How far this walker's plan can be had</b>, read and never written.</summary>
    PlanAnswer AnswerTheWalk(int person, int hold, ReadOnlySpan<LineWay> ways)
    {
        for (var index = 0; index < ways.Length; index++)
        {
            ref readonly var way = ref ways[index];
            var reachM = _occupancy.Reach(WalkAsk(person, hold, way), way.Way, way.ToM, way.FromM, out var cutBy);
            if (reachM >= way.ToM) continue;

            return new PlanAnswer(OnTheLineM(way, reachM), _config.PersonStandstillGapM, cutBy, way.Way, index, reachM);
        }

        return PlanAnswer.Whole;
    }

    /// <summary><b>A walker's plan laid</b> over what its answer left, and finished with what it came to.</summary>
    void LayTheWalk(int person, int hold, ReadOnlySpan<LineWay> ways, in PlanAnswer answer)
    {
        for (var index = 0; index < ways.Length; index++)
        {
            ref readonly var way = ref ways[index];
            if (way.LineFromM >= answer.CutLineM) break;

            _occupancy.Take(WalkAsk(person, hold, way), way.Way, LaidToM(answer, index, way));
            if (index == answer.CutAt) break;
        }

        _occupancy.EndHold(hold, answer.CutLineM, answer.MarginM, answer.CutBy, answer.CutOn);
    }

    /// <summary>
    /// <b>This walker's plan answered again against what every other came to</b>, and laid again where the
    /// answer has moved (<see cref="SettleThePlans"/>) — true where it did.
    /// </summary>
    bool SettleTheWalk(int person, Span<LineWay> walk)
    {
        var hold = _walkerHold[person];
        if (hold == LaneOccupancy.NoHold) return false;

        var endsAtM = _occupancy.HoldEndsAtM(hold, out _, out var cutBy);
        if (float.IsPositiveInfinity(endsAtM) || (cutBy.Found && cutBy.HasBody)) return false;

        var ways = TheWalkAhead(person, walk);
        var answer = AnswerTheWalk(person, hold, ways);
        if (answer.CutLineM == endsAtM) return false;

        _occupancy.ReopenHold(hold);
        LayTheWalk(person, hold, ways, answer);
        return true;
    }

    /// <summary>The hold a walker laid this rebuild, for an instrument asking what held it.</summary>
    public int WalkHold(int person) => _walkerHold[person];

    /// <summary>One piece of a walker's plan as the terms it is asked on.</summary>
    PlannedAsk WalkAsk(int person, int hold, in LineWay way) =>
        new(
            hold, person, LaneRoster.Walking, ClaimPriority.Afoot, way.FromM, way.LineFromM,
            way.LineFromM - People.RadiusM[person], float.NegativeInfinity, AlongItsWalkMps(person));

    /// <summary>Whether one of the town's ways is the paint of a zebra, walked from one kerb to the other.</summary>
    public bool IsTheCrossing(int way) => ZebraOf(way) != RibbonMarks.NoZebra;

    /// <summary>
    /// <b>What the walker got</b>: how far past the front of its body its plan survived, less the gap it keeps
    /// off what cut it — infinity where nothing did, and where it planned nothing.
    /// </summary>
    void ReadTheWalkersGrant(int person)
    {
        var hold = _walkerHold[person];
        var endsAtM = _occupancy.HoldEndsAtM(hold, out var marginM, out _);
        People.GrantM[person] = float.IsPositiveInfinity(endsAtM)
            ? float.PositiveInfinity
            : endsAtM - marginM - People.RadiusM[person];
        People.OnCrossing[person] = TheZebraOf(person, hold);
    }

    /// <summary>Whether this walker stands where a light's hold refused its plan — the wait that spends no clock (TLT-2a).</summary>
    bool HeldByALight(int person)
    {
        if (People.GrantM[person] > 0f) return false;

        _occupancy.HoldEndsAtM(_walkerHold[person], out _, out var cutBy);
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
        if (on != PersonFleet.NoWay && IsTheCrossing(on)) return ZebraOf(on);

        var cutOn = People.GrantM[person] <= 0f ? _occupancy.HoldCutOn(hold) : PersonFleet.NoWay;
        return cutOn >= 0 && IsTheCrossing(cutOn) ? ZebraOf(cutOn) : PersonFleet.NoCrossing;
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
    float PlansAheadM => _config.PersonStepM + _config.PersonStandstillGapM;

    /// <summary>
    /// Whether this walker is a body on a line of its own rather than a shape on the pavement — <b>the
    /// whole of what says which of PER-25's two walks it is taking</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Its place is read and never searched for</b>: the way is the one its route handed it and the
    /// metre along it is where the body projects onto that way's own line, worked out once a tick with
    /// the rest of the walk's state (<see cref="StationTheWalker"/>).
    /// </para>
    /// <para>
    /// <b>Being on a way and holding a route are two questions</b> (<see cref="IsOnItsWay"/>). A body
    /// routed down the far lane of the pavement it is standing on is between the two until it crosses,
    /// and a body shoved aside is off its way long before it has lost it.
    /// </para>
    /// </remarks>
    bool IsAfoot(int person, out int way)
    {
        way = PersonFleet.NoWay;

        // <b>On this way's own ground, and not merely near it</b> (<see cref="IsOnItsWay"/>). Held to the
        // looser bar, this and the ground walk disagreed about which of a pavement's two lanes somebody
        // was standing on — and disagreeing about that is two answers about one body.
        //
        // <b>Losing the route is a different question and a much looser bar</b>
        // (<see cref="HasLostItsLine"/>): being shoved off your half of a pavement is not a reason to
        // search the town again.
        if (!HasAWalkToPlan(person) || !IsOnItsWay(person)) return false;

        way = WayOf(People.CurrentRouteWay(person));
        return true;
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
    bool HasAWalkToPlan(int person) =>
        People.Walking[person] && People.IsOnItsFeet(person)
        && !(_hands.Held && _selected.Holds(SelectionKind.Person, person))
        && WayOf(People.CurrentRouteWay(person)) != PersonFleet.NoWay;

    /// <summary>
    /// The town's way number for a way of a route chain, or <see cref="PersonFleet.NoWay"/> for the hop
    /// off the network. <b>The one place the chain's own encoding is spent</b>
    /// (<see cref="WalkingNetwork.IsACorner"/>): a stretch's own lane, or the complement of a corner's
    /// turn slot.
    /// </summary>
    int WayOf(int code) =>
        code == RouteChain.NoWay || code == WalkingNetwork.NoLane ? PersonFleet.NoWay
        : WalkingNetwork.IsACorner(code) ? _ways.OfMitre(WalkingNetwork.CornerOf(code))
        : _ways.OfFootway(code);

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
    int WaysAlongTheWalk(int person, float aheadM, Span<LineWay> into)
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
    void WalkedOfSlot(int person, int slot, out float fromM, out float endM)
    {
        var route = People.RouteOf(person);
        var count = People.RouteCount[person];
        Walking.SpanOfWay(
            slot > 0 ? route[slot - 1] : WalkingNetwork.NoLane, route[slot],
            slot + 1 < count ? route[slot + 1] : WalkingNetwork.NoLane, out fromM, out endM);

        if (slot == count - 1) endM = MathF.Min(endM, People.RouteToM[person]);
        if (slot == People.RouteAt(person)) fromM = MathF.Max(fromM, People.OnWayM[person]);
    }

    /// <summary>How fast this walker is going the way it is facing, which is the only direction it walks in.</summary>
    float AlongItsWalkMps(int person) =>
        Vector2.Dot(People.VelocityMps[person], Heading.Unit(People.HeadingRad[person]));
}
