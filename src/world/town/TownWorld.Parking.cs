using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// The town's side of a drive leg: where it begins, where the line it is driven on comes from, which bay
/// it is aimed at, and what stands the car down at the end of it. <b>The manoeuvres themselves are the
/// catalogue's</b> — what is here is only what needs the whole composition.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// A drive leg begins — the car acts because there is somebody in it. <b>The plan is drawn first and
    /// the first step of it taken</b>: a car standing in a bay backs out of it, one standing anywhere else
    /// takes the lane it is on and drives.
    /// </summary>
    void SetOff(int car)
    {
        // Somebody has got in and is driving it somewhere of their own, which is the one thing that takes a
        // car back off the player without the reset (CTL-4): a trip and an order cannot both say where it goes.
        _carOrders.Release(car);

        Cars.Driven[car] = true;
        Cars.ClearRoute(car);
        GiveUpTheTurn(car);
        RestTheLadder(car);
        Cars.Suspended[car] = Maneuver.None;

        var bay = BayAimedAt(car);
        Cars.HasDestination[car] = bay >= 0;
        Cars.DestinationM[car] = bay >= 0 ? _parking.CentreM(bay) : Cars.PositionM[car];

        PlanTheLeg(car);
        TakeTheNextStep(car);
    }

    /// <summary>
    /// The lane the car is standing on and pointing along, taken as the front of a line. The nearest
    /// centreline is as likely to be the oncoming lane as its own, so <b>direction decides, not
    /// distance</b>.
    /// </summary>
    /// <remarks>
    /// Refused where the car is standing on no lane at all, which is what makes `P-4`'s <c>Sa</c> fail
    /// from a verge and sends the ladder to `E-8` rather than driving a car with no line.
    /// </remarks>
    bool TakeTheLaneUnderIt(int car)
    {
        var forward = ForwardOf(car);
        var rearAxleM = CarFollower.RearAxleM(Cars.BuildOf(car), Cars.PositionM[car], forward);
        var lane = _roads.NearestLane(rearAxleM, out var alongM);
        if (lane < 0) return false;

        if (Vector2.Dot(Spline.SampleAt(_roads.ArcsOf(lane), alongM).Direction, forward) <= 0f)
        {
            var back = _roads.LaneReverse[lane];
            if (back >= 0)
            {
                lane = back;
                alongM = Spline.ProjectM(
                    _roads.ArcsOf(lane), rearAxleM, _roads.LaneLengthM[lane] * 0.5f, _roads.LaneLengthM[lane]);
            }
        }

        Cars.ChainOf(car)[0] = lane;
        LayLine(car, 1);
        Cars.ProgressM[car] = alongM;
        Cars.ClimbedFromM[car] = Cars.PositionM[car];
        return Cars.Line[car].ArcCount > 0;
    }

    /// <summary>
    /// A car with nothing left to do: no line, no route, no claim, handbrake on — and whoever is sitting
    /// in it is asked to get out wherever the leg ended, because a driver in a car that has stopped
    /// driving is a body nothing is running for.
    /// </summary>
    void StandTheCarDown(int car)
    {
        LeaveTheCatalogue(car);
        RestTheLadder(car);
        Cars.Line[car] = default;
        Cars.ClearRoute(car);
        GiveUpTheTurn(car);
        Cars.HasDestination[car] = false;
        Cars.Driven[car] = false;
        Cars.Command[car] = DriveCommand.Parked;
        Cars.Hold[car] = DrivingHold.None;
        Cars.Context[car] = DriveContext.Clear;
        DropTheMovement(car);

        var driver = _containers.DriverOf(car);
        // A service vehicle's crew stays aboard between errands (AMB-3, SRV-3): a leg ending is the
        // errand's own business, and a crew that got out would leave a car CAR-1 says is no longer an agent
        // — with no way of ever standing it down again. <b>So does the driver of a car under orders</b>
        // (CTL-8b), for the same reason said of a hand: they are waiting at the wheel for the next one.
        if (driver >= 0 && !IsAServiceVehicle(car) && LetsItsDriverOut(car))
        {
            People.Stage[driver] = TripStage.Alighting;
        }
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

            // The place changed, so the chain that was aiming at the old one is not this leg's any more.
            PlanTheLeg(car);
            return true;
        }

        return false;
    }

    /// <summary>
    /// <b>The bay this leg is on its way to</b>, or <see cref="ParkingRegistry.NoBay"/> — the booking, which
    /// is a register and the one hold in the town that is not a piece of road.
    /// </summary>
    int BayAimedAt(int car) => _desk.BookingOf(car);

    /// <summary>
    /// <b>The bay the line in hand finishes in</b>: the one this leg is turning in while it has one to turn
    /// in (GEN-4l), and the place it is going to otherwise. One question, so that the line, the plan and the
    /// route cannot disagree about which bay the last dozen metres of the line belong to.
    /// </summary>
    int BayTheLineEndsIn(int car) => _parking.TurnOf(car) is var turn and >= 0 ? turn : BayAimedAt(car);

    /// <summary>
    /// <b>A bay of this frontage to turn in</b> (GEN-4l): free, still in front of the car, driven into off
    /// the lane it is coming down and back out onto the lane it means to leave by, in the standing that
    /// pair of ways is laid for (GEN-4j). The first one that answers all of that, since a bay of a section
    /// is a bay of it.
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

        foreach (var bay in _bayWays.BaysOffLane(fromLane))
        {
            if (!_parking.IsFreeFor(car, bay)) continue;

            var way = _bayWays.TheWayToTurnIn(bay, fromLane, backLane);
            if (way == BayWays.NoWay || !StandsShortOfTheWay(car, fromLane, way)) continue;
            if (!_parking.TakeTheTurn(car, bay)) continue;

            // The chain is re-derived and never patched (MAN-3): what this leg does now is park in this
            // bay, leave it the other way, and go on driving the route from there.
            PlanTheLeg(car);
            TurnsAtALotBegun++;
            return true;
        }

        MarkTheFrontageFull(car, fromLane);
        return false;
    }

    /// <summary>
    /// <b>A car park with nothing free to turn in is a way priced up</b>, on the same mark `E-7` lays on a
    /// road somebody gave up entering (<see cref="MarkTheWayBlocked"/>) and for the same reason: a route
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
    /// <b>Whether the mouth of a way is still in front of this car</b>, its own nose included. A line ends
    /// where the way it finishes at begins, so a way whose mouth is behind the car is a line of no length
    /// — a leg with nothing to drive, standing in a lane it has already passed the turn-in for.
    /// </summary>
    /// <remarks>
    /// <b>Asked of the body and not of the line</b>, because it is asked while the line is being laid: what
    /// it compares is the metre of the lane the way leaves against the metre this car's nose stands at,
    /// projected onto the same lane. A lane the car is nowhere near answers yes, which is right — every
    /// lane further down the chain is ahead of it by construction.
    /// </remarks>
    bool StandsShortOfTheWay(int car, int lane, int way)
    {
        var arcs = _roads.ArcsOf(lane);
        var atM = _bayWays.AtLaneM(way);
        var noseM = Cars.PositionM[car] + (ForwardOf(car) * Cars.BuildOf(car).HalfLengthM);

        return atM > Spline.ProjectM(arcs, noseM, atM, _roads.LaneLengthM[lane]);
    }

    /// <summary>The turn given up: the bay back to the town, and the leg no longer coming back the other way.</summary>
    void GiveUpTheTurn(int car)
    {
        _parking.LeaveTheTurn(car);
        Cars.TurnsBackOn[car] = CarFleet.NoLane;
    }

    /// <summary>A bay booked for a leg — <b>the desk's, because a place is what a driver has to hand</b>.</summary>
    bool TakeTheBay(int car, int bay) => _desk.BookTheBay(car, bay);

    /// <summary>And given back.</summary>
    void GiveUpTheBay(int car) => _desk.GiveUpTheBooking(car);

    /// <summary>
    /// The nearest free bay a car can be got into within <paramref name="withinM"/> of a place, or −1 —
    /// where a service vehicle is stood before the first tick and where it is sent home to (AMB-2, SRV-2).
    /// </summary>
    int FreeBayNear(Vector2 ofM, float withinM) => FreeBayNear(ofM, withinM, AnySide);

    /// <summary>
    /// <b>And the same refused any bay across the carriageway from a place</b> (GEN-4k) — how an apron is
    /// kept to one kerb. A crew walking out of a station to a car parked over the road crosses the road on
    /// every call, and a station whose vehicles stand on both sides of the street does not read as a
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
        var lane = _roads.NearestLane(bayM, out var alongM);
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
    /// Whether the line in hand finishes at the bay this leg is aimed at — which is what says the car is
    /// past driving the road and into the last dozen metres of the leg.
    /// </summary>
    bool IsOnTheFinalApproach(int car) => Cars.TailWayOf(car) != CarFleet.NoWay;

    /// <summary>
    /// How far ahead of the rear axle the line leaves the road for the bay, which is where the last lane's
    /// own stretch of the line ends. Infinity where the line finishes on the road.
    /// </summary>
    float ToTheWayIntoTheBayM(int car)
    {
        var lanes = Cars.Line[car].LaneCount;
        if (lanes == 0 || Cars.TailWayOf(car) == CarFleet.NoWay) return float.PositiveInfinity;

        return Cars.LaneEndsOf(car)[lanes - 1] - Cars.ProgressM[car];
    }

    /// <summary>
    /// <b>The way into the bay this line ends in, where the line being laid actually reaches it</b>: the
    /// bay is reached from this lane and the route has run out on it. Anything else is a leg still driving
    /// the road, and its line ends where the lane does. <b>Which bay that is is the leg's own question</b>
    /// (<see cref="BayTheLineEndsIn"/>) — the one it is turning in comes first, and it is the same answer
    /// the plan's next step was drawn from.
    /// </summary>
    /// <remarks>
    /// It is asked once, where the line is assembled, and the answer is carried on the car
    /// (<see cref="CarFleet.TailWay"/>) rather than re-derived — a leg that gave its bay up between the two
    /// askings would otherwise be a line whose geometry and whose book disagreed about where it ends.
    /// <b>The driver's habit is what picks between the two standings</b> (GEN-4j), and it is a habit rather
    /// than a draw precisely so that the two askings agree — <b>except at a turn, where the way out is what
    /// picks</b> (GEN-4l): a car parking here to come back the other way has one standing that gets it out
    /// onto that lane, and the habit is what settles a bay that lays both.
    /// <para>
    /// <b>And never a way whose mouth is already behind the body</b> (<see cref="StandsShortOfTheWay"/>). A
    /// way into a bay leaves its lane part-way along it, so a leg that has driven past that point has
    /// overshot its own turn-in; threaded on regardless, the last dozen metres of the line are laid behind
    /// the car and the follower calls the line lost the moment it is handed one — with the same answer
    /// coming back every time the line is re-laid, which is a car standing in a clear lane for the rest of
    /// the run. What a driver who has overshot does is drive on and ask for the route again, and leaving
    /// the way off is what lets it (<see cref="NextLaneOnRoute"/>).
    /// </para>
    /// </remarks>
    int TheWayIntoTheBay(int car, int lastLane)
    {
        var bay = BayTheLineEndsIn(car);
        if (bay < 0 || Cars.RouteTaken[car] < Cars.RouteCount[car]) return CarFleet.NoWay;

        var backLane = _roads.LaneReverse[lastLane];
        var way = bay == _parking.TurnOf(car) && backLane >= 0
            ? _bayWays.TheWayToTurnIn(bay, lastLane, backLane)
            : _bayWays.WayInOffLane(bay, lastLane, !Cars.BacksIntoBays[car]);

        return way != BayWays.NoWay && StandsShortOfTheWay(car, lastLane, way) ? way : CarFleet.NoWay;
    }
}
