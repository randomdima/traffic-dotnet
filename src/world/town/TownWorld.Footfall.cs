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

        // Where the body stands on the way it is walking, worked out here because this is the first thing
        // in the tick that needs it and everything after reads what it wrote (SIM-7). <see cref="OnWayM"/>
        // is that projection and is not written again: a second opinion about one body's place on one way
        // is exactly what this used to be.
        People.OnCrossing[person] = PersonFleet.NoCrossing;
        People.OnCrossingWay[person] = PersonFleet.NoWay;

        // <b>Where the walk has got to, worked out once and written once</b> (SIM-7): the body's place on
        // the way it is walking, the next way off the chain where it has walked this one out, and the
        // point along it to aim at. Split between here and the agent's own tick, the way and the metre
        // along it were written at two different moments and disagreed at the end of every one.
        WalkTheWay(person);

        var on = People.CurrentRouteWay(person);
        if (on == PersonFleet.NoWay) return;

        // <b>Which paint the walk is on is a fact about the way being walked</b> and not about whether the
        // body has reached that way's own line yet: a body crossing to the far lane of a pavement is
        // walking onto the same zebra it was routed over. Written under the claim's own bar instead, it
        // read "off the network" for the stretch at the start of every leg.
        //
        // A corner carries the paint of the stretch it leads onto, so a walker knows it is stepping onto a
        // crossing from the near side of the kerb rather than once it is on it.
        //
        // <b>The stretch of paint and not only the crossing</b> (PER-27): a zebra is two lanes over one
        // carriageway and what a walker takes is the one of them its route names, so the direction is read
        // off the chain here with everything else rather than recovered from the crossing later.
        var edge = WalkingNetwork.IsACorner(on) ? Walking.TurnToEdge(WalkingNetwork.CornerOf(on)) : on;
        if (_bands.CrossingOfEdge[edge] == PersonFleet.NoCrossing) edge = TheCrossingArrivedAt(person);

        if (edge != PersonFleet.NoWay)
        {
            People.OnCrossingWay[person] = edge;
            People.OnCrossing[person] = _bands.CrossingOf(edge);
        }

        if (IsAfoot(person, out var way)) People.OnWay[person] = way;
    }

    /// <summary>
    /// <b>The stretch of paint the next way of this chain is, once the body is near enough the end of the
    /// way it is walking to be stopping for it</b> — or <see cref="PersonFleet.NoWay"/>, which is every walk
    /// that is not about to step onto a zebra.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Wanting a crossing has to begin off the paint or it begins too late</b> (PER-27). A zebra of this
    /// town runs kerb to kerb — its first metre is carriageway — and a pavement that meets one at the very
    /// point it sets off from is joined to it without a mitre (<see cref="WalkingNetwork.JoinArcs"/>), so
    /// there is no way of the network a body stands on between the pavement and the road. Asked only of the
    /// way being walked, a walker was told what it could have of a crossing in the first tick it was
    /// already on.
    /// </para>
    /// <para>
    /// <b>From a stop away, which is the figure the walker already plans with</b>
    /// (<see cref="PlansAheadM"/>): what it takes to come to rest at the pace it walks. A walker held here
    /// comes to rest at the kerb rather than in the road, and no distance of this rule's own is authored.
    /// </para>
    /// </remarks>
    int TheCrossingArrivedAt(int person)
    {
        var next = People.PeekNextRouteWay(person);
        if (next == PersonFleet.NoWay) return PersonFleet.NoWay;

        var edge = WalkingNetwork.IsACorner(next) ? Walking.TurnToEdge(WalkingNetwork.CornerOf(next)) : next;
        if (_bands.CrossingOfEdge[edge] == PersonFleet.NoCrossing) return PersonFleet.NoWay;

        var leftM = EndOfTheWayM(person, Walking) - People.OnWayM[person];
        return leftM <= PlansAheadM(person) ? edge : PersonFleet.NoWay;
    }


    /// <summary>
    /// <b>The walk in front of this body, planned</b> (PER-26): from the front of it down the ways of its
    /// route, for as far as it would take to come to rest — and <b>a crossing to the far kerb or none</b>
    /// (PER-27).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A body on no way of the network plans nothing</b>: it is walking straight at the network (PER-25)
    /// over ground the town does not number, and what it holds while it does is its body.
    /// </para>
    /// <para>
    /// <b>The paint is planned whole</b> (<see cref="ClaimPriority.Crossing"/>): what a walker wants of a
    /// zebra is the other side, so a plan that reaches the paint runs to its far end, and through the marks
    /// the lanes it is painted across. Refused anywhere on it, a walker not yet on the paint waits at the
    /// kerb; one already on it has the lanes under it at p0 and walks on as far as it was granted.
    /// </para>
    /// <para>
    /// <b>Answered before it is laid</b>, as a driver's plan is, so nothing is taken off another plan for a
    /// walk that then stops short of it.
    /// </para>
    /// </remarks>
    void PlanTheWalk(int person, Span<LineWay> ways)
    {
        _walkerHold[person] = LaneOccupancy.NoHold;

        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;
        if (!People.Walking[person] || People.OnWay[person] == PersonFleet.NoWay) return;

        var frontM = People.RadiusM[person];
        var count = WaysAlongTheWalk(person, frontM + PlansAheadM(person), ways, toTheFarKerb: true);

        // From the front of the body: what is behind that is the body's own, at p0.
        var first = 0;
        while (first < count && ways[first].LineFromM + (ways[first].ToM - ways[first].FromM) <= frontM) first++;
        if (first == count) return;

        if (ways[first].LineFromM < frontM)
        {
            ways[first] = ways[first] with
            {
                FromM = ways[first].FromM + (frontM - ways[first].LineFromM), LineFromM = frontM,
            };
        }

        var hold = _occupancy.BeginHold(_config.PersonStandstillGapM);
        _walkerHold[person] = hold;
        var alongMps = AlongItsWalkMps(person);
        Span<ClaimPriority> rungs = stackalloc ClaimPriority[count];
        LevelTheWalk(ways[..count], rungs);

        var cutLineM = float.PositiveInfinity;
        var cutBy = LaneClaim.Nothing;
        var cutOn = -1;
        for (var index = first; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            var reachM = _occupancy.Reach(
                WalkAsk(person, hold, way, rungs[index], frontM, alongMps), way.Way, way.ToM, way.FromM, out var by);
            if (reachM >= way.ToM) continue;

            cutLineM = OnTheLineM(way, reachM);
            cutBy = by;
            cutOn = index;
            break;
        }

        var marginM = cutBy.Found
            ? new LaneCredit(_config.PersonStandstillGapM, _config.PersonStandstillGapM, LaneRoster.Walking).Of(cutBy)
            : 0f;

        // To the far kerb or none: a walker still off the paint, refused anywhere on it, waits at the kerb.
        if (cutOn >= 0 && IsTheCrossing(ways[cutOn].Way) && ways[cutOn].Way != People.OnWay[person])
        {
            cutLineM = ways[cutOn].LineFromM;
            cutBy = LaneClaim.Nothing;
            marginM = 0f;
        }

        for (var index = first; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            if (way.LineFromM >= cutLineM) break;

            _occupancy.Take(WalkAsk(person, hold, way, rungs[index], frontM, alongMps), way.Way, OnTheWayM(way, cutLineM));
        }

        _occupancy.EndHold(hold, cutLineM, marginM, cutBy, cutOn >= 0 ? ways[cutOn].Way : LaneOccupancy.NoHold);
    }

    /// <summary>The hold a walker laid this rebuild, for an instrument asking what held it.</summary>
    public int WalkHold(int person) => _walkerHold[person];

    /// <summary>One piece of a walker's plan as the terms it is asked on.</summary>
    PlannedAsk WalkAsk(int person, int hold, in LineWay way, ClaimPriority rung, float frontM, float alongMps) =>
        new(hold, person, LaneRoster.Walking, rung, way.FromM, way.LineFromM, way.LineFromM - frontM, float.NegativeInfinity, alongMps);

    /// <summary>
    /// <b>The rung each piece of a walk is held at</b> (TER-5g.1): the paint's on a crossing and on the pavement
    /// leading to it, and the pavement's everywhere else.
    /// </summary>
    /// <remarks>
    /// <b>The kerb is worth what the zebra is.</b> Held at the pavement's rung, the last metres before the
    /// paint — which lie over the kerbside lane at a corner — went to any car going straight on, and a walker
    /// that would have been given the crossing was cut short of it by the traffic the crossing gives way to.
    /// </remarks>
    void LevelTheWalk(ReadOnlySpan<LineWay> ways, Span<ClaimPriority> rungs)
    {
        var next = ClaimPriority.Firm;
        for (var index = ways.Length - 1; index >= 0; index--)
        {
            if (IsTheCrossing(ways[index].Way)) next = ClaimPriority.Crossing;
            rungs[index] = next;
        }
    }

    /// <summary>Whether one of the town's ways is the paint of a zebra, walked from one kerb to the other.</summary>
    bool IsTheCrossing(int way) =>
        _ways.KindOf(way) == WayKind.Footway && _bands.CrossingOfEdge[_ways.FootwayOf(way)] != PersonFleet.NoCrossing;

    /// <summary>
    /// <b>What the walker got</b>: how far past the front of its body its plan survived, less the gap it keeps
    /// off what cut it — infinity where nothing did, and where it planned nothing.
    /// </summary>
    void ReadTheWalkersGrant(int person)
    {
        var endsAtM = _occupancy.HoldEndsAtM(_walkerHold[person], out var marginM, out _);
        People.GrantM[person] = float.IsPositiveInfinity(endsAtM)
            ? float.PositiveInfinity
            : endsAtM - marginM - People.RadiusM[person];
    }

    /// <summary>
    /// <b>How far in front of itself a walker plans</b>: what it needs to come to rest at the pace it walks,
    /// plus the gap it keeps.
    /// </summary>
    /// <remarks>
    /// <b>Sized by the pace it walks at and not by what it is doing</b>: a walker stopped behind something
    /// plans the ground it would set off into, or nothing would ever say it meant to move.
    /// </remarks>
    float PlansAheadM(int person) =>
        StoppingM(_config.PersonWalkSpeedMps, FootGripMps2(person)) + _config.PersonStandstillGapM;

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

        if (!People.Walking[person] || !People.IsOnItsFeet(person)) return false;

        // A hand at the keys aims a walker wherever it likes and the route under it is whatever was last
        // laid, so what would be claimed is where that walker was going before the hand took it.
        if (_hands.Held && _selected.Holds(SelectionKind.Person, person)) return false;

        var on = People.CurrentRouteWay(person);
        if (on == PersonFleet.NoWay) return false;

        // <b>On this way's own ground, and not merely near it</b> (<see cref="IsOnItsWay"/>). Held to the
        // looser bar, this and the ground walk disagreed about which of a pavement's two lanes somebody
        // was standing on — and disagreeing about that is two answers about one body.
        //
        // <b>Losing the route is a different question and a much looser bar</b>
        // (<see cref="HasLostItsLine"/>): being shoved off your half of a pavement is not a reason to
        // search the town again.
        if (!IsOnItsWay(person)) return false;

        way = WayOf(on);
        return way != PersonFleet.NoWay;
    }

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
    /// <param name="toTheFarKerb">Whether a stretch that runs out on the paint of a zebra is carried on to its far end (PER-27).</param>
    int WaysAlongTheWalk(int person, float aheadM, Span<LineWay> into, bool toTheFarKerb = false)
    {
        var route = People.RouteOf(person);
        var count = People.RouteCount[person];
        var at = People.RouteAt(person);
        if (at < 0 || at >= count) return 0;

        var walking = Walking;
        var written = 0;
        var walkedM = 0f;

        for (var slot = at; slot < count && walkedM < aheadM && written < into.Length; slot++)
        {
            var townWay = WayOf(route[slot]);
            if (townWay == PersonFleet.NoWay) break;

            walking.SpanOfWay(
                slot > 0 ? route[slot - 1] : WalkingNetwork.NoLane, route[slot],
                slot + 1 < count ? route[slot + 1] : WalkingNetwork.NoLane, out var fromM, out var endM);

            if (slot == count - 1) endM = MathF.Min(endM, People.RouteToM[person]);

            // The body's own place is where the statement begins, and every way after it is stated from
            // wherever the walk joins it.
            if (slot == at) fromM = MathF.Max(fromM, People.OnWayM[person]);
            if (endM <= fromM) continue;

            // The stretch may end part-way along a way — except on the paint, where it is the far kerb or none.
            var runM = toTheFarKerb && IsTheCrossing(townWay) ? endM - fromM : MathF.Min(endM - fromM, aheadM - walkedM);
            into[written++] = new LineWay(townWay, fromM, fromM + runM, walkedM);
            walkedM += endM - fromM;
        }

        return written;
    }

    /// <summary>How fast this walker is going the way it is facing, which is the only direction it walks in.</summary>
    float AlongItsWalkMps(int person) =>
        Vector2.Dot(People.VelocityMps[person], Heading.Unit(People.HeadingRad[person]));

    /// <summary>What the feet can put down on the ground this walker is standing on (TER-2, PER-3).</summary>
    float FootGripMps2(int person) => _config.PersonFootGripMps2 * People.GroundCoefficient[person];
}
