using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A walker's own tick</b> (PER-25): the way of the route it is on, and the straight back onto the
/// network for a body that is on none of it.
/// </summary>
/// <remarks>
/// <b>There is one walk and it has two halves.</b> On the network a body walks the chain of ways the
/// search was expanded into (<see cref="RouteChain"/>), one way at a time, aiming along the line that way
/// already has — the pavement is contracted once when the town is stood up and a walk is a search over it,
/// so nothing here steers, avoids or plans. Off the network the route is laid again from wherever the body
/// has got to, and what it does until then is walk straight at the nearest way — a doorway, a body shoved
/// off its route, somebody put down at the roadside and somebody knocked over are one state and get one
/// answer.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>Whether this body has lost the way it was walking</b> — further off that way's own line than the
    /// pavement has ground either side of it. It is the walking side's own off-line, and what it answers is
    /// PER-25's second half.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the way and not of a line laid under the body</b>, because the way is where the body
    /// actually is: a walker's place on the network is written when it is handed a way and carried forward
    /// as it walks, so being off it is a distance from ground the town already numbers.
    /// </remarks>
    bool HasLostItsLine(int person) =>
        People.Walking[person]
        && People.CurrentRouteWay(person) != PersonFleet.NoWay
        && People.OffWayM[person] > _config.WalkerOffLaneM * OffLineTolerance;

    /// <summary>
    /// <b>Whether the body is on the ground of the way it is walking</b>, rather than merely near it: a
    /// pavement is walked a lane each way a lane's width apart (WLK-8), so past the half-band the body is
    /// nearer its sibling and the ground walk would name that one instead.
    /// </summary>
    /// <remarks>
    /// <b>It is regularly false for a stretch at the start of a walk</b>, and that is the state and not a
    /// fault: a walker standing on one lane of a pavement may be routed down the other (WLK-8,
    /// <see cref="WalkingNetwork.EntriesNear"/>), and until it has crossed to it, it is between the two —
    /// holding the ground its own box is over like any body, and stating nothing, there being no one way
    /// it is on to state from.
    /// </remarks>
    bool IsOnItsWay(int person)
    {
        var way = People.CurrentRouteWay(person);
        return way != PersonFleet.NoWay && People.OffWayM[person] <= Walking.WayWidthM(way) * 0.5f;
    }

    /// <summary>
    /// A body whose controller is paused — the same thing done for a body nobody is deciding for: a
    /// walker holds its stance under the ground's friction, and a car is left with no pedals and no
    /// steering, so what happens to it is the tyres' and the solver's.
    /// </summary>
    void Paused(int agent)
    {
        if (Roster.IsCar(agent))
        {
            var car = Roster.CarIndex(agent);
            Cars.Command[car] = DriveCommand.Idle;
            Tyres(car, PoseOf(car));
            return;
        }

        var positionM = People.PositionM[agent];
        _impulseNs[agent] = WalkerFollower.Step(
            _config, People.HeadingRad[agent], positionM, People.VelocityMps[agent], positionM, moving: false,
            _terrain.At(positionM).Coefficient, People.IsOnItsFeet(agent), People.MassKg[agent], _config.TickSeconds).ImpulseNs;
    }

    /// <summary>
    /// <b>The route this walker walks to where it is going</b>: a search over the pavement's contracted
    /// network, expanded into the chain of ways it is travelled as, with the body seated on the first of
    /// them.
    /// </summary>
    /// <remarks>
    /// <b>It is the drive's own steps</b> (<see cref="TryPlan"/>): one search, one expansion
    /// (<see cref="RouteChain"/>), one body put on the first way of what came back. What differs is only
    /// that a driver is handed lanes and assembles a line over them, and a walker is handed the pavement's
    /// own ways and is held on each in turn.
    /// <para>
    /// The goal is where the walk ends and the chain is how the body gets near it: a destination is any
    /// walkable place and the network is the pavement, so the last piece of every walk is the one short
    /// straight hop off it.
    /// </para>
    /// </remarks>
    /// <param name="reachTheGoal">
    /// Whether the goal itself is walked to. <b>A trip has somewhere it must actually get to</b> — a
    /// doorway — so the last piece of such a walk is that hop, and no more.
    /// </param>
    void LayWalk(int person, bool reachTheGoal = false)
    {
        var people = People;
        people.ClearRoute(person);

        var walking = Walking;
        var entries = _walkSearch.Entries;
        var entryCount = walking.EntriesNear(people.PositionM[person], entries);
        var goalCount = walking.GoalsAt(people.GoalM[person], _walkSearch.Goals);
        if (entryCount == 0 || goalCount == 0) return;

        var linkCount = _walkSearch.Plan(entryCount, goalCount, _surcharges, out var goalSlot);
        if (linkCount == 0 || goalSlot < 0) return;

        var links = _walkSearch.Links(linkCount);
        var entry = WalkingNetwork.SetOffFrom(entries[..entryCount], links[0]);

        var runs = walking.Runs;
        var joins = new WalkingNetwork.FootJoins(walking);
        var into = people.RouteOf(person);
        var written = RouteChain.LayInto(
            ref joins, runs, links, fromWay: NothingBehind, firstSlot: runs.PieceAt(links[0], entry.AlongM, out var enteredAtM),
            _walkSearch.Goals[goalSlot], into, out var ranOut);

        if (written == 0) return;

        people.RouteCount[person] = written;
        people.RouteRunsOut[person] = ranOut;
        people.RouteToM[person] = StopsAtM(walking, into[..written], _walkSearch.Goals[goalSlot], ranOut);

        // Seated on the first way, as far along it as the body actually stands — found over the whole of
        // that way, the entry's own metre being the fine graph's and this the lane's.
        people.RouteTaken[person] = 1;
        people.OnWayM[person] = walking.LaneMOf(into[0], enteredAtM);
        PlaceItOnItsWay(person, walking, float.PositiveInfinity);

        MoveTheGoalOntoTheWalk(person, reachTheGoal, walking, into[..written]);
    }

    /// <summary>What a route chain is expanded from when there is no way behind the body: a walk begins on its first way.</summary>
    const int NothingBehind = -1;

    /// <summary>
    /// How far along the chain's last way the walk stops. <b>Where the destination stands</b> when the
    /// chain reached it, and the end of that way where the chain ran out of room first — the rest is then
    /// laid again from there.
    /// </summary>
    static float StopsAtM(WalkingNetwork walking, ReadOnlySpan<int> ways, RouteGoal goal, bool ranOut)
    {
        var last = ways[^1];
        walking.SpanOfWay(ways.Length > 1 ? ways[^2] : WalkingNetwork.NoLane, last, WalkingNetwork.NoLane,
            out _, out var endM);
        if (ranOut || WalkingNetwork.IsACorner(last)) return endM;

        walking.Runs.PieceAt(goal.Link, goal.AlongM, out var arrivedAtM);
        return MathF.Min(endM, walking.LaneMOf(last, arrivedAtM));
    }

    /// <summary>
    /// <b>The goal moves onto the network rather than the walk coming off it</b>: striking out for a drawn
    /// point would cross whatever lay between, a carriageway included, which is the one thing the pavement
    /// graph exists to stop. <b>A trip's own goal is a place that has to be arrived at</b>, so the hop off
    /// the network is kept where it is short enough to be one.
    /// </summary>
    /// <remarks>
    /// <b>A chain that ran out of room moves nothing</b> (<see cref="PersonFleet.RouteRunsOut"/>). Where the
    /// walk goes is still the door; what is bounded is how much of the way there fits in one chain, and the
    /// rest is laid again from where the body has got to (PER-25). Moved, the door was lost — a walker then
    /// reached the end of a chain, was found to have arrived at the goal it was carrying, and walked into a
    /// building it was nowhere near.
    /// </remarks>
    void MoveTheGoalOntoTheWalk(int person, bool reachTheGoal, WalkingNetwork walking, ReadOnlySpan<int> ways)
    {
        var endsAtM = Spline.SampleAt(walking.WayArcs(ways[^1]), People.RouteToM[person]).PositionM;
        if (!reachTheGoal)
        {
            People.GoalM[person] = endsAtM;
            return;
        }

        if (People.RouteRunsOut[person]) return;

        // A player's order is exempt from the cap on what a trip may hand somebody: that cap is a rule
        // about the routes this town draws for itself.
        var capM = People.Manual[person] ? float.PositiveInfinity : _config.PersonOffNetworkHopM;
        if ((People.GoalM[person] - endsAtM).Length() > capM) People.GoalM[person] = endsAtM;
    }

    /// <summary>
    /// <b>One tick of walking the chain</b>: where the body now stands on the way it is on, the next way
    /// off the chain where it has walked this one out, and the point along it to aim at.
    /// </summary>
    /// <remarks>
    /// <b>It is the driver's line-taking in the walking side's words</b> — a car takes the next lane off
    /// its route when the line over the one it is on runs out (<see cref="NextLaneOnRoute"/>), and this is
    /// that with the line being the way's own arc and no assembly in between.
    /// <para>
    /// <b>The goal is walked to off the network and not along it.</b> The chain ends where the pavement
    /// does; what is left is the one short hop, and the body aims straight at it.
    /// </para>
    /// </remarks>
    void WalkTheWay(int person)
    {
        if (!People.Walking[person]) return;

        // Walking with no way of the network under it: a body stands until the next decision lays it a
        // route, whose first leg is the straight back onto the pavement (PER-25). Aimed at the goal
        // instead it struck out for a door across town over whatever lay between — which is the one thing
        // the pavement graph exists to stop, and nothing here searched to find out it was doing it.
        if (People.CurrentRouteWay(person) == PersonFleet.NoWay)
        {
            People.DestinationM[person] = People.PositionM[person];
            return;
        }

        var walking = Walking;
        PlaceItOnItsWay(person, walking, AStrideM);

        while (People.OnWayM[person] >= EndOfTheWayM(person, walking) && !People.OnTheLastWay(person))
        {
            var before = People.CurrentRouteWay(person);
            if (!People.TakeNextRouteWay(person, out var next)) break;

            walking.SpanOfWay(before, next, People.PeekNextRouteWay(person), out var fromM, out _);
            People.OnWayM[person] = fromM;
            PlaceItOnItsWay(person, walking, AStrideM);

            // Taking a way *is* progress, and the clock that decides a walker has given up is measured
            // against where it is aiming. Left standing it would run up over a long way and call a walker
            // that had just been handed a fresh one stuck.
            _progress.Restart(person);
        }

        var way = People.CurrentRouteWay(person);
        var stopsAtM = EndOfTheWayM(person, walking);
        if (People.OnTheLastWay(person) && People.OnWayM[person] >= stopsAtM)
        {
            // The chain is walked out. What is left is the hop onto the goal, over ground the network does
            // not number — or, where the chain stopped for want of room rather than because it arrived,
            // nothing at all: the walk ends on the network and the rest of it is laid again from there.
            var endsAtM = People.RouteRunsOut[person]
                ? Spline.SampleAt(walking.WayArcs(way), stopsAtM).PositionM
                : People.GoalM[person];

            People.DestinationM[person] = endsAtM;

            // And the hop walked too, the leg is over: what happens next is the trip's to say — arriving,
            // waiting for a place, or laying the next leg (<see cref="StandingStill"/>). Left walking, a
            // body stands on its own doorstep aiming at it until the give-up clock takes the trip away.
            if ((endsAtM - People.PositionM[person]).Length() <= People.RadiusM[person])
            {
                People.Walking[person] = false;
            }

            return;
        }

        var aheadM = MathF.Min(People.OnWayM[person] + _config.PersonWalkAheadM, stopsAtM);
        People.DestinationM[person] = Spline.SampleAt(walking.WayArcs(way), aheadM).PositionM;
    }

    /// <summary>
    /// How far along the way it is on this walk goes: the end of that way's own stretch of the chain, or
    /// where the destination stands on it where it is the last of them.
    /// </summary>
    float EndOfTheWayM(int person, WalkingNetwork walking)
    {
        if (People.OnTheLastWay(person)) return People.RouteToM[person];

        walking.SpanOfWay(
            People.RouteWayBefore(person), People.CurrentRouteWay(person), People.PeekNextRouteWay(person),
            out _, out var endM);
        return endM;
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
    void PlaceItOnItsWay(int person, WalkingNetwork walking, float windowM)
    {
        var arcs = walking.WayArcs(People.CurrentRouteWay(person));
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

    /// <summary>A stride and the ground either side of the line, which is as far as a body moves between two ticks.</summary>
    float AStrideM => _config.PersonWalkAheadM + _config.WalkerOffLaneM;

    /// <summary>
    /// <b>PER-8: a body that cannot walk back to the network is set down on it.</b> The second walk is the
    /// recovery for everything that puts a walker off its line, and it is the whole of it right up to the
    /// case where the straight back runs through a wall — so this is what that case falls to, and only
    /// that case: a body still on a way of the pavement is held up by something and moving it would be
    /// answering the solver with a placement.
    /// </summary>
    /// <remarks>
    /// <b>Bounded by the clock and counted where it happens</b> (<see cref="WalkersSetDown"/>). A body
    /// lifted over what was holding it is the town papering over a failure, so it may happen only once a
    /// leg has run out its patience, and it is a figure the instruments print rather than a quiet
    /// correction: a town setting hundreds of walkers down is a town whose doors open into places nothing
    /// can walk out of, and that is the thing to go and look at.
    /// </remarks>
    /// <returns>Whether the body was moved, which it is not for a walker that is on its way.</returns>
    bool PutItBackOnThePavement(int person)
    {
        if (IsOnItsWay(person)) return false;

        var goals = _walkSearch.Goals;
        if (_walking.GoalsAt(People.PositionM[person], goals) == 0) return false;

        Place(person, NetworkPointM(goals[0]), People.HeadingRad[person]);
        WalkersSetDown++;
        return true;
    }

    /// <summary>
    /// How many walkers the town has had to lift back onto the pavement (PER-8), which is a count of the
    /// times its own ground beat a body rather than a thing the town does.
    /// </summary>
    public long WalkersSetDown { get; private set; }

    /// <summary>
    /// <b>How much of this leg is left to walk</b>, which is what decides whether a walker is getting
    /// anywhere (PER-25, <see cref="WalkProgress"/>): what remains of the way it is on, or the straight to
    /// the goal for a body on none of the network.
    /// </summary>
    /// <remarks>
    /// <b>The way and not the point the follower is aiming at.</b> That point stands a fixed stride in
    /// front of the body and moves forward with it, so the distance to it is the stride itself whether the
    /// walk is going well or not at all — a clock measured against it read a walker at full pace down a
    /// long stretch as a walker that had not moved. <b>What is left of one way is enough</b>, because
    /// taking the next one restarts the clock (<see cref="WalkTheWay"/>): progress down the chain is the
    /// handover, and progress along a way is this.
    /// <para>
    /// <b>And the ground back onto the line counts as ground still to be walked</b>, which is PER-25's
    /// second walk measured like the first: a body shoved off the pavement has further to go rather than
    /// nothing left to do, and it closes that distance by walking back.
    /// </para>
    /// </remarks>
    float RemainingOnTheWalkM(int person)
    {
        if (People.CurrentRouteWay(person) == PersonFleet.NoWay)
        {
            return (People.GoalM[person] - People.PositionM[person]).Length();
        }

        // Past the end of the chain what is left is the hop onto a place that stands still, so the
        // straight to it is a distance that shrinks as the body covers it.
        if (People.OnTheLastWay(person) && People.OnWayM[person] >= People.RouteToM[person])
        {
            return (People.DestinationM[person] - People.PositionM[person]).Length();
        }

        // <b>Off the way's own ground and not merely off its line.</b> A walker wanders about its lane and
        // is shoved across it, and that wander is a record low every few decisions — so the lane itself
        // counts for nothing here and only the ground beyond it is a distance the walk has to close.
        var backOntoItM = MathF.Max(0f, People.OffWayM[person] - _config.WalkerOffLaneM);
        return backOntoItM + MathF.Max(0f, EndOfTheWayM(person, Walking) - People.OnWayM[person]);
    }
}
