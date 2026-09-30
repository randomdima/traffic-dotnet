using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.Agents.Person.Actions;

/// <summary>
/// <b>Walk</b> (PER-25, PER-26): the ways of the walker's route, one at a time on each way's own line, and the one short
/// hop off the end of them onto its goal — on ground planned in front of it down those ways, had in part, and walked
/// to where it was cut. Done where the hop is walked, or the chain runs out and is laid again from there.
/// </summary>
/// <remarks>
/// <b>It is the driver's line-taking in the walking side's words</b> — a car takes the next lane off its route when
/// the line over the one it is on runs out, and this is that with the line being the way's own arc and no assembly in
/// between. A walker steers at nothing and avoids nothing: the ways of the route are where it goes, and what it is
/// held short of is what its reservations are answered with.
/// </remarks>
internal sealed class WalkingItsRoute(WalkingGround ground, PersonActions actions, Sidestepping sidestepping, LegProgress progress)
{
    PersonFleet People => ground.People;

    /// <summary>
    /// <b>One tick of walking the chain</b>: where the body now stands on the way it is on, and the next way
    /// off the chain where it has walked this one out. Where along it the body aims is the grant's, and is
    /// read once the grant is (<see cref="Aim"/>).
    /// </summary>
    /// <remarks>
    /// <b>The goal is walked to off the network and not along it.</b> The chain ends where the pavement
    /// does; what is left is the one short hop, and the body aims straight at it.
    /// </remarks>
    public void WalkTheWay(int person)
    {
        // Only a walk down its route has a route to walk: an officer on duty walks straight at their post (SRV-11),
        // and a hand walks a walker wherever it likes (CTL-6).
        if (!People.Walking[person] || !PersonActions.WalksItsRoute(People.Action[person])) return;

        // Walking with no way of the network under it: a body stands until the next decision lays it a
        // route, whose first leg is the straight back onto the pavement (PER-25). Aimed at the goal
        // instead it struck out for a door across town over whatever lay between — which is the one thing
        // the pavement graph exists to stop, and nothing here searched to find out it was doing it.
        if (People.CurrentRouteWay(person) == PersonFleet.NoWay)
        {
            People.DestinationM[person] = People.PositionM[person];
            return;
        }

        var walking = ground.Walking;
        ground.PlaceItOnItsWay(person, ground.AStrideM);

        while (ground.HasWalkedOut(person, ground.EndOfTheWayM(person)) && !People.OnTheLastWay(person))
        {
            var before = People.CurrentRouteWay(person);
            if (!People.TakeNextRouteWay(person, out var next)) break;

            walking.SpanOfWay(before, next, People.PeekNextRouteWay(person), out var fromM, out _);
            People.OnWayM[person] = fromM;
            ground.PlaceItOnItsWay(person, ground.AStrideM);

            // Taking a way *is* progress, and the clock that decides a walker has given up is measured
            // against where it is aiming. Left standing it would run up over a long way and call a walker
            // that had just been handed a fresh one stuck.
            progress.Restart(person);
        }

        var stopsAtM = ground.EndOfTheWayM(person);
        if (People.OnTheLastWay(person) && ground.HasWalkedOut(person, stopsAtM))
        {
            // The chain is walked out. What is left is the hop onto the goal, over ground the network does
            // not number — or, where the chain stopped for want of room rather than because it arrived,
            // nothing at all: the walk ends on the network and the rest of it is laid again from there.
            var endsAtM = People.GoalM[person];
            if (People.RouteRunsOut[person]
                && !walking.EndOfTheWalk(People.RouteOf(person)[..People.RouteCount[person]], stopsAtM, out endsAtM))
            {
                endsAtM = People.PositionM[person];
            }

            People.DestinationM[person] = endsAtM;

            // And the hop walked too, the leg is over: what happens next is the trip's to say — arriving,
            // waiting for a place, or laying the next leg. Left walking, a body stands on its own doorstep aiming
            // at it until the give-up clock takes the trip away.
            if ((endsAtM - People.PositionM[person]).Length() <= People.RadiusM[person])
            {
                actions.SetWalking(person, false);
            }
        }
    }

    /// <summary>
    /// <b>The ways a walker's plan is laid down</b>, from the front of its body — what is behind that is the
    /// body's own, at p0 — for as far as it would take to come to rest: down its route, or over the ground of the
    /// hop off the end of it. Empty where it plans nothing.
    /// </summary>
    public Span<LineWay> WaysOf(int person, Span<LineWay> into)
    {
        // PHY-7: inside a container there is no body in the world and nothing in anybody's way.
        if (People.Inside[person].Any || !ground.HasAWalkToPlan(person)) return default;

        var frontM = sidestepping.WalkPlannedFromM(person);
        if (ground.IsHopping(person))
        {
            return into[..ground.TheStraightAhead(person, People.DestinationM[person], frontM, ground.PlansAheadM, into)];
        }

        var count = ground.WaysAlongTheWalk(person, frontM + ground.PlansAheadM, into);

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
    /// <b>Where a walker on its way aims, on the grant this rebuild gave it</b> (PER-26): a stride down the
    /// way, or along the hop off the end of it towards where that is walked (<see cref="WalkTheWay"/>), and never
    /// past the road it was granted — so a walker with none granted stands where it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read after the grant and not before it</b>, as a driver's is: aimed while the chain was walked, a
    /// walker stepped on the grant the tick before had left it.
    /// </para>
    /// <para>
    /// <b>The aim is where the middle of the body gets to</b>, and the follower takes it there and no further. The
    /// grant is how far the front of the body may go, which is how far the middle may, so it is the grant that is
    /// added to the body's own place and not the grant and a radius.
    /// </para>
    /// </remarks>
    public void Aim(int person)
    {
        var way = People.CurrentRouteWay(person);
        if (way == PersonFleet.NoWay) return;

        if (People.GrantM[person] <= 0f)
        {
            People.DestinationM[person] = People.PositionM[person];
            return;
        }

        // The hop is aimed at where it was walked to, and no further along the straight than it was granted.
        if (ground.IsHopping(person))
        {
            var towards = People.DestinationM[person] - People.PositionM[person];
            var reachM = People.GrantM[person];
            if (towards.LengthSquared() > reachM * reachM)
            {
                People.DestinationM[person] = People.PositionM[person] + (Vector2.Normalize(towards) * reachM);
            }

            return;
        }

        if (sidestepping.Aim(person)) return;

        var strideM = MathF.Min(ground.Config.PersonWalkAheadM, People.GrantM[person]);
        var aheadM = MathF.Min(People.OnWayM[person] + strideM, ground.EndOfTheWayM(person));
        People.DestinationM[person] = Spline.SampleAt(ground.Walking.WayArcs(way), aheadM).PositionM;
    }
}
