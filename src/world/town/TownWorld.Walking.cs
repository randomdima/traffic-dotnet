using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>A walker's own tick: the kerb it is held at, where it stands while it waits, and the line it is given to walk.</summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// PER-15, asked every tick and not on the clock: a walker whose line steps onto a crossing next
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

        // <b>Whether the patience runs is decided by where the body is standing and never by what is left of
        // its line.</b> A walk laid onto a crossing is spent as the body walks it, so a body part way over
        // has no crossing point left <em>ahead</em> of it — and cleared on that, the one clock that gets it
        // the rest of the way over (<see cref="MayStepOnto"/>) was reset every tick it stood there. What
        // it was refused is the band in front, which is traffic and is exactly what the clock is for; a
        // pavement is where a body that has stopped crossing stands, and there the clock is nobody's.
        var inTheRoad = _terrain.At(positionM).Drivable;
        if (!People.Walking[agent] || crossing < 0 || inTheRoad)
        {
            People.HeldAtTheKerb[agent] = false;

            // <b>Spent on the lane it is standing there for and given back when it is standing in it</b>
            // (<see cref="PersonFleet.WaitingForLane"/>), which the book does. Handed back the tick the
            // traffic gave way instead, the patience buys one tick of ground and the wait begins again —
            // a body stuttering at a lane's edge for as long as the street is busy.
            if (!inTheRoad) People.WaitingToCrossS[agent] = 0f;
            else if (People.AuthorityM[agent] <= 0f) People.WaitingToCrossS[agent] += _config.TickSeconds;

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
    /// <b>Where a walker aims to get past the body in its way</b> (PER-24), which is the aim it already had
    /// wherever nothing is. The book found the obstruction when it took the grant
    /// (<see cref="GrantThePavement"/>); what is decided here is the side and whether the step is one this
    /// body may take.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The right, and the pavement is not the bound on it.</b> A walker's own lane line runs a body's
    /// width from the edge of its band, so nearly every step round somebody standing on that line ends up
    /// off the walk — on the verge, the frontage or the channel — and a step held to the band would be a
    /// walker turning left at almost every body it met. What answers is the ground and not the network.
    /// </para>
    /// <para>
    /// <b>Neither side is a body that stands where it is</b> (<paramref name="walledIn"/>), which is the
    /// answer a walker had before there was a step at all: it stops short of what is in front of it and the
    /// clock that gives up on a leg draws it a line round. Walking on instead would be a walker shoving a
    /// casualty down the street, which is a body the ambulance then has to catch.
    /// </para>
    /// </remarks>
    /// <param name="walledIn">Whether a body is in the way and there is nowhere to step to get past it.</param>
    Vector2 StepRoundAim(int agent, Vector2 positionM, Vector2 aimM, out bool walledIn)
    {
        walledIn = false;

        var body = People.StepsRound[agent];
        if (body == PersonFleet.NoBody) return aimM;

        var bodyM = People.PositionM[body];
        var clearanceM = People.RadiusM[agent] + People.RadiusM[body] + _config.PersonShoulderRoomM;
        if (!StepAround.IsInTheWay(positionM, aimM, bodyM, clearanceM)) return aimM;

        var fromTheCarriageway = _terrain.At(positionM).Drivable;

        var rightM = StepAround.PassM(positionM, aimM, bodyM, clearanceM, onTheRight: true);
        if (IsGroundToStepOnto(rightM, fromTheCarriageway))
        {
            StepsRound++;
            return rightM;
        }

        var leftM = StepAround.PassM(positionM, aimM, bodyM, clearanceM, onTheRight: false);
        if (IsGroundToStepOnto(leftM, fromTheCarriageway))
        {
            StepsRound++;
            StepsRoundToTheLeft++;
            return leftM;
        }

        walledIn = true;
        return aimM;
    }

    /// <summary>
    /// Whether a step (PER-24) may land here. <b>Ground the traffic is not on is a walker's to step onto</b>,
    /// walk or no walk — grass, a verge, a frontage, a bay, the far side of the pavement — because what
    /// PER-7.2 is about is the traffic and not the network, and a step held inside a pavement band is a step
    /// almost never taken.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A carriageway is grazed and never entered.</b> The bound is how far past the kerb line the middle
    /// of the body may be (<see cref="SimConfig.PersonRoadGrazeM"/>): at the channel, with the body over the
    /// kerb, which is what a person does to get round something on a narrow pavement — and never far enough
    /// to be standing in a lane, which is a walker in the traffic rather than beside it.
    /// </para>
    /// <para>
    /// <b>The terrain says what nobody can stand on and the lane's band says where the traffic is</b>
    /// (<see cref="StepAround.IsClearOfTheTraffic"/>): water and its like are refused here, and how far a
    /// step reaches past a kerb is geometry, because the ground grid is a metre to the cell and a kerb line
    /// is not on it.
    /// </para>
    /// <para>
    /// <b>Already on the carriageway, it is where the walk is</b>: a body half way over a crossing is on the
    /// road by the whole design of a zebra (PER-15), and the graze would refuse it every step it takes on
    /// the paint.
    /// </para>
    /// </remarks>
    bool IsGroundToStepOnto(Vector2 atM, bool fromTheCarriageway)
    {
        var ground = _terrain.At(atM);
        if (!ground.Walkable && !ground.Drivable) return false;

        return fromTheCarriageway || StepAround.IsClearOfTheTraffic(_roads, atM, _config.PersonRoadGrazeM);
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
            // rule about the routes this town draws for itself. <b>So is a crew out working</b> (SRV-3):
            // what a paramedic is walking at is a body lying in a carriageway, which the pavement's own
            // network has no point anywhere near, and a walk that stopped at the kerb would be a rescue
            // that never reaches anybody knocked into the middle of a road.
            var capM = people.Manual[person] || people.Stage[person] == TripStage.Attending
                ? float.PositiveInfinity
                : _config.PersonOffNetworkHopM;
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
