using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The walkers' half of the claims, which is two claims and no arithmetic</b> (PER-26): the ground a
/// body is standing on, held at <see cref="ClaimPriority.Hard"/>, and the ground it is walking at, stated
/// at <see cref="ClaimPriority.Soft"/>. Both are laid from the body every tick and neither is answered.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no grant on this side of the town.</b> A driver asks for road and is handed a distance
/// because a car has a speed profile to spend it on; a walker's pace is a cap and never a curve (PER-3),
/// so a distance in front of one buys nothing that the ground it is standing on does not already say. What
/// the two claims are for is that the rest of the town can see a walker — the traffic is held off the body
/// by the first and off the paint it is stepping onto by the second — and <b>what a walker does about
/// another walker is the solver's</b> (PHY-1), not a queue's.
/// </para>
/// <para>
/// <b>It is the same table and the same ladder as the road's</b> (<see cref="LaneOccupancy"/>,
/// <see cref="TownWays"/>, TER-5g), and deliberately not a second mechanism. A body holds the ground it
/// occupies whatever kind of body it is (TER-4c.2), and states the ground it means to use like any driver
/// (TER-5g) — so nothing reading the claims learns that a walker exists.
/// </para>
/// <para>
/// <b>A way is one side of one stretch</b>, or the mitre between two of them. The two directions of a
/// pavement are two lines half a band apart (<see cref="WalkingNetwork.LaneOffsetM"/>), so somebody coming
/// the other way is on other ground — which falls out of the ways rather than being tested for.
/// </para>
/// <para>
/// <b>Where a body stands is read off the line it is walking and never searched for.</b> Every point of a
/// walked line carries the way it was stationed on and how far along that way it stands
/// (<see cref="WalkedLine"/>), so a walker's place on the network costs a subtraction. Only a body that is
/// on no line at all — standing about, knocked over, under a hand — is looked up, and that answer is what
/// says it is off the network and walking straight back onto it (PER-25).
/// </para>
/// <para>
/// <b>What is never written onto the pavement is a car on the paint</b> (TER-5c.1). A zebra is a walk laid
/// over a carriageway, so the ground under it has two names and one owner: the car's stretch of it is a
/// stretch of the <em>lane</em>. A body on foot standing there is written onto both, because the look-up
/// that answers for a car is about the traffic that is coming and a person in a lane is not an answer to
/// it.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How many ways one walker's statement may run over: the way it is on and the ones its walk crosses
    /// onto before it gets where it is aiming. A bound on a stack span and not a figure behaviour reads.
    /// </summary>
    /// <remarks>
    /// <b>Reached, the ways at the far end go unstated</b>, which costs a walker nothing it was holding — a
    /// statement is not ground anybody was given. A corner is two short ways within a stride, so the count
    /// is what a body on a corner can cover rather than what a stretch of pavement suggests.
    /// </remarks>
    const int MostWaysAlongAWalk = 5;

    /// <summary>
    /// How many claims one walker may lay at once: every way its own box is over (PER-26's first), and the
    /// ways along the walk in front of it (PER-26's second). <b>A body lays both</b>, which is the whole of
    /// what it tells the town.
    /// </summary>
    static int MostSlotsPerWalker(in PavementWays pavement) =>
        MostWaysAlongAWalk + pavement.MostWaysUnderAPlace;

    /// <summary>
    /// <b>The pavement's two blocks of the town's numbering, as the runs of metres they are</b> — a lane
    /// each way down every stretch, and the mitre at every corner. Handed to <see cref="TownWays"/>, which
    /// is what makes them ways of the same table the carriageway's are.
    /// </summary>
    /// <remarks>
    /// <b>What stands far enough aside of one of these lines to be walked past is half a body</b>
    /// (<see cref="SimConfig.WalkPassableAsideM"/>, <see cref="TownWays.ClearsAsideM"/>), which is the
    /// road's own bar in the walking side's figures. At nought a stretch stopped being in the way the moment
    /// it was a hair clear of the line — so a car parked across a footway was walked straight through, the
    /// walker's own width being the whole of what it had left over.
    /// </remarks>
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
        var edge = WalkingNetwork.IsACorner(on) ? Walking.TurnToEdge(WalkingNetwork.CornerOf(on)) : on;
        People.OnCrossing[person] = _bands.CrossingOfEdge[edge];

        if (IsAfoot(person, out var way)) People.OnWay[person] = way;
    }

    /// <summary>
    /// <b>PER-26's first claim: the ground under this body, at p0.</b> Every way of the pavement its own
    /// box is over — the lane it is on, the lane running back the other way where it reaches into it, the
    /// mitres of a corner it is standing across, and the paint of a crossing it is standing on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same walk a car standing on the footway is written onto</b> (TER-4c.2,
    /// <see cref="LieOnThePavement"/>, <see cref="GroundUnder"/>), <b>and its own box rather than a radius at
    /// a projection</b> (<see cref="BodyFootprint.CoversOn"/>) — the reading every other body in this town is
    /// laid by.
    /// </para>
    /// <para>
    /// <b>It is laid for every walker and not only for the ones standing about.</b> A body is a body whether
    /// it is walking a line or lying where it was knocked down, and nothing takes the ground somebody is
    /// already on (TER-5g, p0) — so there is one row per way per walker and no state that says which kind of
    /// walker this is.
    /// </para>
    /// <para>
    /// <b>And each row says how far aside of that way's line the body stands</b>
    /// (<see cref="LaneClaim.AsideM"/>), which is what lets a body be written onto every way it touches
    /// without shutting every one of them. Written without it, one person standing at a corner held both
    /// lanes of every stretch meeting there and the mitres between them — a corner nobody could walk through.
    /// </para>
    /// </remarks>
    [SkipLocalsInit]
    void HoldThePavementUnderIt(int person)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;

        var radiusM = People.RadiusM[person];

        Span<WayUnder> under = stackalloc WayUnder[_pavement.MostWaysUnderAPlace];
        var count = GroundUnder.At(
            _pavement, People.PositionM[person], BodyFootprint.Round(radiusM), _config.CrossesOntoAWayM,
            under);

        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref under[index];
            _occupancy.ClaimWhereItStands(
                way.Way, way.AlongM + way.BackM, way.AlongM + way.AheadM, way.AlongM + way.AheadM,
                Vector2.Dot(People.VelocityMps[person], way.AlongUnit), person, of: LaneRoster.Walking,
                acrossFromM: way.AcrossFromM, acrossToM: way.AcrossToM);
        }
    }

    /// <summary>
    /// <b>PER-26's second claim: the pavement this body is walking at, at p9.</b> From its own front to
    /// where it is aiming, on each way that stretch runs over — a statement of where it is going and never
    /// ground anybody handed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A body on no way of the network states nothing</b>, because there is no way to state it on: it is
    /// walking straight at the network (PER-25) over ground the town does not number, and what it holds
    /// while it does is the box it is standing in.
    /// </para>
    /// <para>
    /// <b>It is the weakest hold there is and it is meant to be</b> (TER-5g). Everything stronger takes it
    /// and nothing on the pavement is refused by it, so two walkers walking at the same doorway both get
    /// there; what the statement is worth is that the town can see where somebody is going, which is what a
    /// debug layer draws.
    /// </para>
    /// </remarks>
    void StateThePavementAhead(int person, Span<LineWay> ways)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any) return;
        if (!People.Walking[person] || People.OnWay[person] == PersonFleet.NoWay) return;

        var alongMps = AlongItsWalkMps(person);
        var count = WaysAlongTheWalk(person, StatesAheadM(person), ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var over = ref ways[index];
            _occupancy.ClaimAhead(
                over.Way, over.FromM, over.ToM, alongMps, person, ClaimPriority.Soft, LaneRoster.Walking);
        }
    }

    /// <summary>
    /// <b>How far in front of itself a body states ground</b>: what it needs to come to rest at the pace it
    /// walks, plus the gap it keeps.
    /// </summary>
    /// <remarks>
    /// <b>Sized by the pace it walks at and not by what it is doing</b>, exactly as a driver's statement is:
    /// a walker stopped behind something states the ground it would set off into, or nothing would ever say
    /// it meant to move.
    /// </remarks>
    float StatesAheadM(int person) =>
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
    /// The ways of the pavement under a stretch of one walk — from <paramref name="backM"/> behind the body
    /// to <paramref name="aheadM"/> in front of it — each with the metres of its own that the stretch
    /// covers and where its near edge falls back on the walk.
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

            // The statement may end part-way along a way, and what is left of it is spent as ground there.
            var runM = MathF.Min(endM - fromM, aheadM - walkedM);
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
