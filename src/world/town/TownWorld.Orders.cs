using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// The three things a manoeuvre may ask of the town, carried. <b>Each needs the whole composition at
/// once</b> — the route search, the price table, the bay register and the road graph — which is exactly
/// why they are not the catalogue's to do: an entry that could reach all of that could reach anything.
/// </summary>
/// <remarks>
/// <b>An order that cannot be carried is a refusal</b>, and the entry that asked for it never begins. It
/// is the same contract as an <c>Sa</c> that does not hold, and it is what lets `E-6` mean "retarget,
/// and if there is nowhere to retarget to, take the next rung of the ladder".
/// </remarks>
internal sealed partial class TownWorld
{
    bool Carry(int car, DriveOrder order, int subject) => order switch
    {
        DriveOrder.TakeTheLaneUnderIt => TakeTheLaneUnderIt(car),
        DriveOrder.RetargetTheBay => RetargetTheBay(car, Cars.PositionM[car], subject),
        DriveOrder.MarkTheWayBlocked => MarkTheWayBlocked(car),
        _ => true,
    };

    /// <summary>
    /// `E-7`'s half of the town: price the stretch this car is blocked entering up so other drivers
    /// route around it, and drop the route in hand so the next line laid is drawn over the new answer.
    /// </summary>
    /// <remarks>
    /// <b>The mark expires and is never swept</b> — nothing unmarks a road by inspection, so a stretch
    /// that is still blocked is marked again by whoever finds it so.
    /// </remarks>
    bool MarkTheWayBlocked(int car)
    {
        var lane = LaneAheadOnTheLine(car);
        if (lane < 0) return false;

        var link = _driving.LinkOfLane(lane);
        if (link == TravelGraph.NoLink) return false;

        _surcharges.Mark(link, _config.CarBlockedWayPriceM, _config.CarBlockedWayLifeS);
        _desk.SpendAReroute(car);
        Cars.ClearRoute(car);
        return true;
    }

    /// <summary>
    /// The stretch the car is blocked <em>entering</em>, which is the one worth marking: the lane after
    /// the one under it, or the one under it where the line goes no further.
    /// </summary>
    int LaneAheadOnTheLine(int car)
    {
        var lanes = Cars.Line[car].LaneCount;
        if (lanes == 0) return CarFleet.NoLane;

        var chain = Cars.ChainOf(car);
        var starts = Cars.LaneStartsOf(car);
        var ahead = 0;
        while (ahead < lanes - 1 && Cars.ProgressM[car] >= starts[ahead + 1]) ahead++;

        return ahead + 1 < lanes ? chain[ahead + 1] : chain[ahead];
    }

    /// <summary>
    /// A leg's last tick. <b>Which of the three terminal entries ended it is what says what happened</b>
    /// — parked in the bay it was aiming at, stopped somewhere legal that is not it, or left where it
    /// stands — and the car is stood down the same way in all three.
    /// </summary>
    void EndTheLeg(int car)
    {
        if (Cars.Doing[car] == Maneuver.StandParked) BaysParkedIn++;

        StandTheCarDown(car);
    }
}
