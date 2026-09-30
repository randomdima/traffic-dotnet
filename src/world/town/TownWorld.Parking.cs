using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// The town's side of a drive leg: where it begins, where the line it is driven on comes from, which bay
/// it is aimed at, and what stands the car down at the end of it. <b>Which line a leg is on next is
/// <see cref="TakeTheNextStepOfTheLeg"/>'s</b>, and how a car gets into a bay and out of one is its own
/// manoeuvre (<see cref="ShapeTheWayIn"/>, <see cref="ShapeTheWayOut"/>) — what is here is only what needs the
/// whole composition.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// A drive leg begun for whoever has got in, aimed at the bay that leg claimed. <b>A car standing in a
    /// bay manoeuvres out of it; one standing anywhere else takes the lane it is on and is routed from
    /// there.</b> <b>Nothing calls it</b>: nobody boards a car (CAR-1), and every leg a car is sent on is
    /// begun by <see cref="SendTo"/>.
    /// </summary>
    void SetOff(int car)
    {
        // Somebody has got in and is driving it somewhere of their own, which is the one thing that takes a
        // car back off the player without the reset (CTL-4): a trip and an order cannot both say where it goes.
        _carOrders.Release(car);

        Cars.Driven[car] = true;
        Cars.ClearRoute(car);
        GiveUpTheTurn(car);
        _driveProgress.Restart(car);

        var bay = BayAimedAt(car);
        Cars.HasDestination[car] = bay >= 0;
        Cars.DestinationM[car] = bay >= 0 ? _parking.CentreM(bay) : Cars.PositionM[car];

        LayTheFirstLine(car);
    }

    /// <summary>
    /// <b>The line a leg begins on</b>: the first piece of the manoeuvre out of the bay the car is standing in,
    /// or the lane it is standing on and the route from there. A car that can be given neither has nothing to
    /// drive, and the clock is what ends such a leg.
    /// </summary>
    void LayTheFirstLine(int car)
    {
        _manoeuvres.Clear(car);

        // <b>The bay the body is in, whether or not it was registered there</b>: a leg given up on the way
        // into a bay stands the car down in the space it never reached the end of, and the bay under it is not
        // a lane it may take (GEN-53).
        var standingIn = _parking.BayOf(car);
        if (standingIn < 0) standingIn = _parking.BayHolding(Cars.PositionM[car]);
        if (standingIn >= 0 && ShapeTheWayOut(car, standingIn)) return;

        TakeTheLaneUnderIt(car);
    }

    /// <summary>The direction the body is pointing, which every line is read back through.</summary>
    Vector2 ForwardOf(int car) => Heading.Unit(Cars.HeadingRad[car]);

    /// <summary>
    /// The lane the car is standing on and pointing along, taken as the front of a line — or the join it is
    /// standing in and the lanes either side of it (<see cref="TheCarriagewayUnder"/>).
    /// </summary>
    /// <remarks>
    /// Refused where the car is standing on no lane at all, which is a leg with nothing to drive: the
    /// clock is what ends one (<see cref="WatchTheProgress"/>). <b>A lane of the carriageway and never a
    /// bay</b> (GEN-53): a bay is joined to nothing, and taken as a lane it is a dead end the car drives to
    /// the back of.
    /// </remarks>
    bool TakeTheLaneUnderIt(int car)
    {
        var forward = ForwardOf(car);
        var rearAxleM = CarFollower.RearAxleM(Cars.BuildOf(car), Cars.PositionM[car], forward);
        var under = TheCarriagewayUnder(rearAxleM, forward);
        if (under.Lane < 0) return false;

        LayLine(car, TheChainFrom(car, under));
        Cars.ProgressM[car] = under.AlongM;
        return Cars.Line[car].ArcCount > 0;
    }

    /// <summary>A line's first lanes as a body standing on the carriageway has them, and how many that is.</summary>
    int TheChainFrom(int car, in StandingOn under)
    {
        var chain = Cars.ChainOf(car);
        chain[0] = under.Lane;
        if (under.Onward == CarFleet.NoLane) return 1;

        chain[1] = under.Onward;
        return 2;
    }

    /// <summary>
    /// <b>Where on the carriageway a body stands, as a line is laid from</b>: the lane it is on and pointing
    /// along — or, <b>standing in a junction's box, where no lane runs, the lane it came off and the one the
    /// join it stands in leads onto</b> — with how far along a line over them it stands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The nearest centreline is as likely to be the oncoming lane as its own, so direction decides, not
    /// distance.</b>
    /// </para>
    /// <para>
    /// <b>And in a box, the nearest lane end is as likely to be the one ahead of the body as the one behind
    /// it.</b> A line laid from there begins in front of the car, which the follower calls lost the tick it
    /// is handed one — and every recovery asked the same question again and got the same answer, so a car
    /// that came to rest in a box stood there for the rest of the run.
    /// </para>
    /// </remarks>
    StandingOn TheCarriagewayUnder(Vector2 rearAxleM, Vector2 forward)
    {
        var lane = _roads.NearestStreetLane(rearAxleM, out var alongM);
        if (lane < 0) return StandingOn.Nowhere;

        if (Vector2.Dot(Spline.SampleAt(_roads.ArcsOf(lane), alongM).Direction, forward) <= 0f
            && _roads.LaneReverse[lane] is var back and >= 0)
        {
            lane = back;
            alongM = Spline.ProjectM(
                _roads.ArcsOf(lane), rearAxleM, _roads.LaneLengthM[lane] * 0.5f, _roads.LaneLengthM[lane]);
        }

        var onLane = new StandingOn(lane, CarFleet.NoLane, alongM, Spline.SampleAt(_roads.ArcsOf(lane), alongM));
        if (alongM >= _roads.LaneLengthM[lane] - LineTolerance.RoundingM)
        {
            foreach (var onto in _roads.LanesFrom(lane)) OnTheJoin(lane, onto, rearAxleM, forward, ref onLane);
        }
        else if (alongM <= LineTolerance.RoundingM)
        {
            foreach (var from in _roads.LanesIntoJunction(_roads.LaneFromJunction[lane]))
            {
                OnTheJoin(from, lane, rearAxleM, forward, ref onLane);
            }
        }

        return onLane;
    }

    /// <summary>
    /// The join between two lanes, taken over <paramref name="best"/> where the body stands nearer its line
    /// than that and it runs the way the body points.
    /// </summary>
    void OnTheJoin(int from, int onto, Vector2 rearAxleM, Vector2 forward, ref StandingOn best)
    {
        var join = _roads.ConnectorBetween(from, onto);
        if (join == RoadGraph.NoConnector) return;

        var arcs = _roads.ConnectorArcs(join);
        if (arcs.Length == 0) return;

        var onM = Spline.ProjectM(arcs, rearAxleM, 0f, _roads.ConnectorLengthM(join), out var offSq);
        if (offSq >= (best.At.PositionM - rearAxleM).LengthSquared()) return;

        var at = Spline.SampleAt(arcs, onM);
        if (Vector2.Dot(at.Direction, forward) <= 0f) return;

        best = new StandingOn(from, onto, _roads.LaneLengthM[from] + onM, at);
    }

    /// <summary>
    /// What <see cref="TheCarriagewayUnder"/> found: the lane a line over it begins on, the lane after the join
    /// the body stands in (or <see cref="CarFleet.NoLane"/> on a lane), how far along that line the body
    /// stands, and the point of the line there.
    /// </summary>
    readonly record struct StandingOn(int Lane, int Onward, float AlongM, SplineSample At)
    {
        public static StandingOn Nowhere => new(CarFleet.NoLane, CarFleet.NoLane, 0f, default);
    }

    /// <summary>
    /// A car with nothing left to do: no line, no route, no turn, no manoeuvre, no destination and no movement,
    /// handbrake on. <b>Nobody is let out</b>: nobody is ever in a car to let out (CAR-1).
    /// </summary>
    void StandTheCarDown(int car)
    {
        Cars.Reroutes[car] = 0;
        _driveProgress.Restart(car);
        Cars.Line[car] = default;
        Cars.ClearRoute(car);
        GiveUpTheTurn(car);
        _manoeuvres.Clear(car);
        Cars.HasDestination[car] = false;
        Cars.Driven[car] = false;
        Cars.Command[car] = DriveCommand.Parked;
        Cars.Hold[car] = DrivingHold.None;
        Cars.Context[car] = DriveContext.Clear;
    }

    /// <summary>
    /// From the car's side: a bay that cannot be reached or cannot be driven into is given up for the
    /// nearest one that can, near where the car has actually got to — and a car with nowhere to put itself
    /// keeps driving rather than standing in a lane. <b>Release before taking</b>: a place held by a car
    /// that has gone elsewhere is a place removed from the town.
    /// </summary>
    /// <remarks>
    /// <b>The same handful of searches a leg is drawn with</b> (<see cref="BaysRoutedPerLeg"/>), and for
    /// the same reason: the screening search is what a bay costs to consider, and a retarget that walked
    /// every candidate would spend a leg's whole routing budget on one refusal.
    /// </remarks>
    bool RetargetTheBay(int car, Vector2 nearM, int avoidBay)
    {
        GiveUpTheBay(car);

        // The place changed, so a turn the old route asked for is a turn nothing is asking for. The one the
        // new route needs is claimed where that route reaches its frontage, like any other.
        GiveUpTheTurn(car);

        var bays = _parking.BaysNear(nearM, _config.PersonWalkWorthM, _bayCandidates);
        var fromLane = Cars.LaneOf(car);
        var searched = 0;
        for (var slot = 0; slot < bays && searched < BaysRoutedPerLeg; slot++)
        {
            var bay = _bayCandidates[slot];
            if (bay == avoidBay) continue;

            if (fromLane >= 0)
            {
                searched++;
                if (!RouteExistsToTheBay(fromLane, bay)) continue;
            }

            if (!TakeTheBay(car, bay)) continue;

            Cars.HasDestination[car] = true;
            Cars.DestinationM[car] = _parking.CentreM(bay);
            Cars.ClearRoute(car);

            // The place changed, so the route that was aiming at the old one is not this leg's any more:
            // the line is laid again from where the body is — the way out of a bay where it is still in one,
            // and the lane under it otherwise — which plans to the new place.
            LayTheFirstLine(car);
            return true;
        }

        return false;
    }

    /// <summary>
    /// <b>The bay this leg is on its way to</b>, or <see cref="ParkingRegistry.NoBay"/> — a claim in the
    /// register, and the one hold in the town that is not a piece of road.
    /// </summary>
    int BayAimedAt(int car) => _parking.ClaimedBayOf(car);

    /// <summary>
    /// <b>The bay the line in hand finishes at</b>: the one this leg is turning in while it has one to turn
    /// in (GEN-4l), and the place it is going to otherwise. One question, so that the line, the manoeuvre and
    /// the route cannot disagree about which bay the last metres of the line belong to.
    /// </summary>
    int BayTheLineEndsIn(int car) => _parking.TurnOf(car) is var turn and >= 0 ? turn : BayAimedAt(car);

    /// <summary>
    /// <b>A bay of this frontage to turn in</b> (GEN-4l): free, worked off the lane running back as well, near the
    /// lane's end (<see cref="Core.Config.SimConfig.TurnAtALotWithinM"/>) and still in front of the car — <b>the one
    /// furthest along</b>, since the route turns the leg at the lane's end and lands it at the start of the lane
    /// running back.
    /// </summary>
    /// <remarks>
    /// <b>A leg that finds none is not a leg that has failed.</b> The line then ends where the frontage
    /// does, the car comes to rest there like any other car at the end of its route, and the question is
    /// asked again every time the line is laid — a bay a moment away from being given back is the ordinary
    /// case at a full car park. What ends such a leg is the same watchdog that ends every other one that
    /// stands still too long.
    /// </remarks>
    bool TakeABayToTurnIn(int car, int fromLane, int backLane)
    {
        if (_parking.TurnOf(car) >= 0) return true;

        var best = BayStreets.NoBay;
        var bestM = _roads.LaneLengthM[fromLane] - _config.TurnAtALotWithinM;
        foreach (var bay in _bayStreets.BaysOffLane(fromLane))
        {
            var atM = _bayStreets.AtLaneM(bay, fromLane);
            if (atM < bestM || !_parking.IsFreeFor(car, bay) || float.IsNaN(_bayStreets.AtLaneM(bay, backLane))) continue;
            if (!StandsShortOf(car, fromLane, StopForTheBayM(car, bay, fromLane))) continue;

            best = bay;
            bestM = atM;
        }

        if (best != BayStreets.NoBay && _parking.TakeTheTurn(car, best))
        {
            TurnsAtALotBegun++;
            return true;
        }

        MarkTheFrontageFull(car, fromLane);
        return false;
    }

    /// <summary>
    /// <b>A car park with nothing free to turn in is a way priced up</b>, on the same mark a leg lays on a
    /// road it could not get into (<see cref="MarkTheWayBlocked"/>) and for the same reason: a route
    /// through a turn that cannot be made is a route nobody can drive, and a search asked again over an
    /// unmarked graph comes back with it every time. The mark expires, so a bay given back a minute later
    /// is a frontage the town uses again.
    /// </summary>
    /// <remarks>
    /// Laid only by a car actually at the frontage. Seen from a street away it is a car park somebody else
    /// may well have left by the time this one arrives, and pricing it up from there is a town routed on a
    /// reading nobody took.
    /// </remarks>
    void MarkTheFrontageFull(int car, int fromLane)
    {
        if (Cars.LaneOf(car) != fromLane) return;

        var link = _driving.LinkOfLane(fromLane);
        if (link != TravelGraph.NoLink) _surcharges.Mark(link, _config.CarBlockedWayPriceM, _config.CarBlockedWayLifeS);
    }

    /// <summary>
    /// <b>Whether a metre of a lane is still in front of this car</b>, its own nose included. A line that stops
    /// for a bay stops there, so one whose stop is behind the car is a line of no length — a leg standing in a
    /// lane it has already passed its turn-in on.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the body and not of the line</b>, because it is asked while the line is being laid: what
    /// it compares is the metre against the one this car's nose stands at, projected onto the same lane. A lane
    /// the car is nowhere near answers yes, which is right — every lane further down the chain is ahead of it by
    /// construction.
    /// </remarks>
    bool StandsShortOf(int car, int lane, float atM)
    {
        var arcs = _roads.ArcsOf(lane);
        var noseM = Cars.PositionM[car] + (ForwardOf(car) * Cars.BuildOf(car).HalfLengthM);

        return atM > Spline.ProjectM(arcs, noseM, atM, _roads.LaneLengthM[lane]);
    }

    /// <summary>The turn given up: the bay back to the town, and the leg no longer coming back the other way.</summary>
    void GiveUpTheTurn(int car)
    {
        _parking.LeaveTheTurn(car);
        Cars.TurnsBackOn[car] = CarFleet.NoLane;
    }

    /// <summary>
    /// <b>A bay claimed for a leg</b> — the register's, because such a claim is held over ground the car
    /// has no line to yet and is the one hold in the town that is not a piece of road.
    /// </summary>
    bool TakeTheBay(int car, int bay) => _parking.Claim(car, bay);

    /// <summary>And given back: a place held by a car that has stopped driving towards it is a place removed from the town.</summary>
    void GiveUpTheBay(int car) => _parking.Release(car);

    /// <summary>
    /// The nearest free bay a car can be got into within <paramref name="withinM"/> of a place, or −1 —
    /// where a service vehicle is stood before the first tick and where it is sent home to (AMB-2, SRV-2).
    /// </summary>
    int FreeBayNear(Vector2 ofM, float withinM) => FreeBayNear(ofM, withinM, AnySide);

    /// <summary>
    /// <b>And the same refused any bay across the carriageway from a place</b> (GEN-4k) — how an apron is
    /// kept to one kerb. A station whose vehicles stand on both sides of the street does not read as a
    /// station at all.
    /// </summary>
    int FreeBayNear(Vector2 ofM, float withinM, Vector2 sameSideAsM)
    {
        var bays = _parking.BaysNear(ofM, withinM, _bayCandidates);
        for (var slot = 0; slot < bays; slot++)
        {
            var bay = _bayCandidates[slot];
            if (!_parking.CanBeReached(bay)) continue;
            if (sameSideAsM != AnySide && !StandOnTheSameSideOfTheRoad(_parking.CentreM(bay), sameSideAsM)) continue;

            return bay;
        }

        return -1;
    }

    /// <summary>The place a search is not asked to match a side against, since no bay stands infinitely far off one.</summary>
    static readonly Vector2 AnySide = new(float.PositiveInfinity);

    /// <summary>
    /// <b>Whether a place stands on the same side of the carriageway as this bay does</b> — the bay's own
    /// road being the one asked about, since a bay hangs off a kerb (GEN-4b) and a building may stand back
    /// from several.
    /// </summary>
    /// <remarks>
    /// The two are measured against <em>one</em> lane, each at its own projection onto it, because the side
    /// a body is on is a fact about a ribbon and not about a point: a lane read at each end of a bend has
    /// two different normals, and comparing offsets taken against the wrong one calls the far kerb the near
    /// one on any road that curves. Read against each body's own nearest lane instead, both sides of a
    /// street come out positive, since each is to the same hand of the lane beside it.
    /// </remarks>
    public bool StandOnTheSameSideOfTheRoad(Vector2 bayM, Vector2 placeM)
    {
        var lane = _roads.NearestStreetLane(bayM, out var alongM);
        if (lane < 0) return false;

        var arcs = _roads.ArcsOf(lane);
        var lengthM = _roads.LaneLengthM[lane];
        return SideOf(arcs, bayM, alongM) * SideOf(arcs, placeM, Spline.ProjectM(arcs, placeM, alongM, lengthM)) > 0f;

        static float SideOf(ReadOnlySpan<ArcSeg> arcs, Vector2 pointM, float atM)
        {
            var on = Spline.SampleAt(arcs, atM);
            return ((pointM.X - on.PositionM.X) * -on.Direction.Y) + ((pointM.Y - on.PositionM.Y) * on.Direction.X);
        }
    }

    /// <summary>
    /// Whether the line in hand stops for the bay this leg is aimed at — which is what says the car is past
    /// driving the road and into the last metres of the leg.
    /// </summary>
    bool IsOnTheFinalApproach(int car) => Cars.StopsForBayOf(car) != CarFleet.NoBay;

    /// <summary>
    /// <b>The bay this line stops for, where the line being laid actually reaches it</b>: the bay is worked off
    /// this lane, the route has run out on it, and where the car waits for it is still in front of the car.
    /// Anything else is a leg still driving the road, and its line ends where the lane does. <b>Which bay that
    /// is is the leg's own question</b> (<see cref="BayTheLineEndsIn"/>) — the one it is turning in comes first.
    /// </summary>
    /// <remarks>
    /// It is asked once, where the line is assembled, and the answer is carried on the car
    /// (<see cref="CarFleet.StopsForBay"/>) rather than re-derived — a leg that gave its bay up between the two
    /// askings would otherwise be a line whose end and whose manoeuvre disagreed about where it goes.
    /// <para>
    /// <b>And never a bay whose place to wait is already behind the body</b> (<see cref="StandsShortOf"/>): a leg
    /// that has driven past it has overshot its own turn-in, and what a driver who has done that does is drive on
    /// and ask for the route again, which leaving the bay off is what lets it (<see cref="NextLaneOnRoute"/>).
    /// </para>
    /// </remarks>
    int TheBayTheLineStopsFor(int car, int lastLane)
    {
        var bay = BayTheLineEndsIn(car);
        if (bay < 0 || Cars.RouteTaken[car] < Cars.RouteCount[car]) return CarFleet.NoBay;
        if (float.IsNaN(_bayStreets.AtLaneM(bay, lastLane))) return CarFleet.NoBay;

        return StandsShortOf(car, lastLane, StopForTheBayM(car, bay, lastLane)) ? bay : CarFleet.NoBay;
    }
}
