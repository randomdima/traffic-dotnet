using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Foot;

namespace TrafficSimulation.World.Town;

/// <summary>A walker's own tick: the kerb it is held at, where it stands while it waits, and the line it is given to walk.</summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// `P-3`, asked every tick and not on the clock: a walker whose line steps onto a crossing next
    /// stands at the kerb until the road answers. Being held is not being stuck, so the clock that gives
    /// a leg up does not run while this is true. Every tick because the answer is about traffic — a gap
    /// re-checked into oblivion is a gap given away.
    /// </summary>
    /// <remarks>
    /// <b>The wait can also happen in the road</b>, now that a body holds the lane it is standing in and no
    /// more (PER-15): one stopped at a lane's edge by a car committed to the next band is waiting for the
    /// same thing this is, so the clock runs there too and its patience gets it the rest of the way over.
    /// What it is not is <em>at a kerb</em> — nothing stands it back off the paint and no new wait is
    /// counted, because the body is already in the road.
    /// </remarks>
    bool AtTheKerb(int agent, Vector2 positionM)
    {
        var crossing = People.CrossingAhead(agent);
        if (!People.Walking[agent] || crossing < 0)
        {
            People.HeldAtTheKerb[agent] = false;
            People.WaitingToCrossS[agent] = 0f;
            return false;
        }

        if (_terrain.At(positionM).Drivable)
        {
            People.HeldAtTheKerb[agent] = false;

            // <b>Spent on the lane it is standing there for and given back when it is standing in it</b>
            // (<see cref="PersonFleet.WaitingForLane"/>), which the book does. Handed back the tick the
            // traffic gave way instead, the patience buys one tick of ground and the wait begins again —
            // a body stuttering at a lane's edge for as long as the street is busy.
            if (People.AuthorityM[agent] <= 0f) People.WaitingToCrossS[agent] += _config.TickSeconds;

            return false;
        }

        var clear = Kerb.MayBegin(
            _config, _signals, _elapsedS, crossing, PaintClaimM(crossing), _occupancy,
            TheWayItStepsOnto(agent, crossing, out var onto) ? _bands.On(onto) : default,
            People.WaitingToCrossS[agent]);

        if (clear)
        {
            People.HeldAtTheKerb[agent] = false;
            return false;
        }

        if (!People.HeldAtTheKerb[agent])
        {
            KerbWaitsBegun++;
            People.KerbM[agent] = positionM;
        }

        People.HeldAtTheKerb[agent] = true;
        People.WaitingToCrossS[agent] += _config.TickSeconds;
        return true;
    }

    /// <summary>
    /// Where a walker held at a kerb stands. The two waits stand in different places on purpose: a wait
    /// for a gap belongs at the kerb, where the view is; a wait for a red belongs a stand-off back from
    /// the paint, because it lasts a phase with a crowd building behind it.
    /// </summary>
    /// <remarks>
    /// The stand-off is measured from the kerb the wait began at and never from where the body has got
    /// to — measured from the body it is two metres further back every tick, and the walker retreats up
    /// the street for as long as the light is red.
    /// </remarks>
    Vector2 WaitAimM(int agent)
    {
        var kerbM = People.KerbM[agent];
        var crossing = People.CrossingAhead(agent);
        if (crossing < 0 || !_signals.CrossingIsLit(crossing)) return kerbM;
        if (_signals.ForCrossing(crossing, _elapsedS) == SignalColour.Green) return kerbM;

        var backM = kerbM - People.DestinationM[agent];
        var lengthM = backM.Length();
        return lengthM > 1e-3f ? kerbM + (backM / lengthM * _config.Person.RedWaitSetbackM) : kerbM;
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
    /// get to</b> — a doorway, the ground beside a car — and the network is the pavement, so the last
    /// piece of such a walk is the one short straight hop off it, and no more. A
    /// drawn point is not one of those: the place the line ends <em>is</em> where that walker was going.
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
        people.DestinationM[person] = people.TakeNextWalkedPoint(person, out var firstM) ? firstM : people.GoalM[person];
    }
}
