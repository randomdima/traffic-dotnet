using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Foot;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A walker's own tick</b> (PER-25): the line the network laid it, and the straight back onto the
/// network for a body that is not on it.
/// </summary>
/// <remarks>
/// <b>There is one walk and it has two halves.</b> On the network a body follows the points the route
/// search laid, which is the whole of the pathfinding: the network is contracted once when the town is
/// stood up and a walk is a search over it, so nothing here steers, avoids or plans. Off the network the
/// line is laid again from wherever the body has got to, and its first leg is the straight to the nearest
/// point of the network — a doorway, a body shoved off its line, somebody put down at the roadside and
/// somebody knocked over are one state and get one answer.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>Whether this body has lost the line it was laid</b> — further off the stretch of walk it is on
    /// than that stretch has pavement either side of it. It is the walking side's own off-line, and what it
    /// answers is PER-25's second half: the line is laid again and the body walks straight back onto the
    /// network.
    /// </summary>
    /// <remarks>
    /// <b>The first leg of a walk is never one of these.</b> There is no point behind it to measure across,
    /// and by construction it <em>is</em> the straight onto the network — so a walk that has just been laid
    /// is not immediately laid again, which would be a body standing still while its line was rewritten
    /// under it every decision.
    /// </remarks>
    bool HasLostItsLine(int person)
    {
        if (!People.Walking[person]) return false;

        var at = People.WalkedAt(person);
        if (at <= 0 || at >= People.WalkedCount[person]) return false;

        var points = People.WalkedLineOf(person);

        return OffTheWalkM(points[at - 1], points[at], People.PositionM[person])
               > _config.WalkerOffLaneM * OffLineTolerance;
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
    /// The line this walker walks to where it is going: a route over the pavement's own network, laid as
    /// the points of the lane each stretch's own side asks for, with the goal itself on the end of it.
    /// </summary>
    /// <remarks>
    /// The goal is the last point and the route is how the body gets near it: a destination is any
    /// walkable place and the network is the pavement, so the last stretch of every walk is off the
    /// network and is walked straight. A line that would not fit is laid again from where the body has
    /// got to — safe only because the goal is added when the whole route fitted, since a truncated line
    /// ending at the goal would be a walker sent across country.
    /// </remarks>
    /// <param name="reachTheGoal">
    /// Whether the goal itself goes on the end of the line. <b>A trip has somewhere it must actually
    /// get to</b> — a doorway — and the network is the pavement, so the last piece of such a walk is the
    /// one short straight hop off it, and no more.
    /// </param>
    void LayWalk(int person, bool reachTheGoal = false)
    {
        var people = People;
        people.ClearWalkedLine(person);

        var walking = Walking;
        var entries = _walkSearch.Entries;
        var entryCount = walking.EntriesNear(people.PositionM[person], entries);
        var goalCount = walking.GoalsAt(people.GoalM[person], _walkSearch.Goals);

        var into = people.WalkedLineOf(person);
        var intoCrossing = people.WalkedCrossingOf(person);
        intoCrossing.Fill(CityPlan.NoRecord);
        var intoWay = people.WalkedWayOf(person);
        intoWay.Fill(WalkedLine.NoWay);
        var intoAlongM = people.WalkedAlongOf(person);
        var written = 0;
        var complete = false;

        if (entryCount > 0 && goalCount > 0)
        {
            var linkCount = _walkSearch.Plan(
                entryCount, goalCount, people.GoalM[person], _surcharges, out var goalSlot);

            if (linkCount > 0 && goalSlot >= 0)
            {
                var links = _walkSearch.Links(linkCount);
                // Which of the two ways along its own stretch the search set off down is the first link
                // it returned; laying the line from the other one starts the walk facing backwards.
                var entry = entries[0];
                for (var slot = 0; slot < entryCount; slot++)
                {
                    if (entries[slot].Link == links[0]) entry = entries[slot];
                }

                written = WalkedLine.Lay(
                    walking, links, entry, _walkSearch.Goals[goalSlot], _config.Network.SplineToleranceWalkedM,
                    _bands.CrossingOfEdge, into, intoCrossing, intoWay, intoAlongM, out complete);
            }
        }

        // The goal moves onto the network rather than the line coming off it: striking out for a drawn
        // point would cross whatever lay between, a carriageway included, which is the one thing the
        // pavement graph exists to stop.
        if (written > 0 && complete && !reachTheGoal) people.GoalM[person] = into[written - 1];

        // A trip's own goal is a place that has to be arrived at rather than got near, so the hop off
        // the network goes on the end of the line — and only where it is short enough to be one, since
        // the shortness is the whole safeguard against a walk that steers round the town.
        if (reachTheGoal && complete && written < into.Length)
        {
            // A player's order is exempt from the cap on what a trip may hand somebody: that cap is a
            // rule about the routes this town draws for itself.
            var capM = people.Manual[person] ? float.PositiveInfinity : _config.PersonOffNetworkHopM;
            var fromM = written > 0 ? into[written - 1] : people.PositionM[person];
            if ((people.GoalM[person] - fromM).Length() <= capM)
            {
                intoCrossing[written] = CityPlan.NoRecord;
                intoWay[written] = WalkedLine.NoWay;
                into[written++] = people.GoalM[person];
            }
            else if (written > 0)
            {
                people.GoalM[person] = into[written - 1];
            }
        }

        people.WalkedCount[person] = written;
        people.WalkedTaken[person] = 0;

        // Either the goal is on the end of the line or the line stopped for want of room, since a walk the
        // network could not reach at all wrote nothing at all.
        people.WalkedRunsOut[person] = written > 0 && !complete;
        people.DestinationM[person] = people.TakeNextWalkedPoint(person, out var firstM) ? firstM : people.GoalM[person];
    }
}
