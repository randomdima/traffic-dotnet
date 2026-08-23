using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;

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
        Cars.Driven[car] = true;
        Cars.ClearRoute(car);
        RestTheLadder(car);
        Cars.Suspended[car] = Maneuver.None;

        var bay = _parking.ReservationOf(car);
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
        var rearAxleM = CarFollower.RearAxleM(_config, Cars.PositionM[car], forward);
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
        Cars.HasDestination[car] = false;
        Cars.Driven[car] = false;
        Cars.Command[car] = DriveCommand.Parked;
        Cars.Hold[car] = DrivingHold.None;
        Cars.Context[car] = DriveContext.Clear;
        DropTheMovement(car);

        var driver = _containers.DriverOf(car);
        if (driver >= 0) People.Stage[driver] = TripStage.Alighting;
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
        _parking.GiveUpReservation(car);

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

            if (!_parking.TryReserve(bay, car)) continue;

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
    /// Whether the car is on the last lane of its route with the bay it reserved ahead of it — the one
    /// place a line is cut short of the lane it is on, because past the staging point the car is
    /// manoeuvring rather than driving.
    /// </summary>
    bool IsOnTheFinalApproach(int car)
    {
        var lanes = Cars.Line[car].LaneCount;
        return lanes > 0 && IsTheApproachLane(car, Cars.ChainOf(car)[lanes - 1]);
    }

    /// <summary>Whether this is the lane the car's own bay is entered from, with the route already run out on it.</summary>
    bool IsTheApproachLane(int car, int lane)
    {
        var bay = _parking.ReservationOf(car);
        return bay >= 0 && _parking.CanBeEntered(bay) && _parking.EnterLane(bay) == lane
               && Cars.RouteTaken[car] >= Cars.RouteCount[car];
    }

    /// <summary>Where a line stops on its last lane: the place the template is staged from, and not the lane's own end.</summary>
    float LastLaneToM(int car, int lastLane) =>
        IsTheApproachLane(car, lastLane) ? _parking.EnterAlongM(_parking.ReservationOf(car)) : float.PositiveInfinity;
}
