using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>CTL-8: a car under the player's orders.</b> One right-click is one goal, and which goal it is, is
/// read off what the pointer was over — so there is no mode to be in and no key to hold.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing below goal selection is touched</b> (CTL-2). Every order here ends in a destination and a
/// chain, handed to the same <see cref="SendTo"/> a rescue's leg is handed to; from there it is the
/// catalogue, the routing, the road and the tyres, exactly as for a car nobody has ever clicked on.
/// </para>
/// <para>
/// <b>An order needs no driver</b> (CTL-8d). CAR-1 makes a driverless car furniture because nothing is
/// choosing for it; a hand giving it goals is exactly that choice, and it is the same substitution
/// CTL-5 already makes at the wheel.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    readonly PlayerOrders _carOrders;

    /// <summary>Whether this car answers to the player rather than to a trip or an errand of its own (CTL-4).</summary>
    public bool IsUnderOrders(int car) => _carOrders.Manual[car];

    /// <summary>What it was last told to do, for a read-out or a goal mark. <see cref="PlayerOrder.None"/> while it idles.</summary>
    public PlayerOrder OrderOf(int car) => _carOrders.Kind[car];

    /// <summary>And the car a follow order is following, or <see cref="PlayerOrders.NoCar"/>.</summary>
    public int OrderedAfter(int car) => _carOrders.Lead[car];

    /// <summary>
    /// <b>An order to a car</b> (CTL-8): the goal its driver would otherwise have picked, pinned by a
    /// right-click, and the leg to it begun at once.
    /// </summary>
    /// <remarks>
    /// <b>A wreck and a car on somebody's arm take no orders</b>, which is CTL-4's terminal rule and
    /// EVA-5's: neither is choosing anything, and neither would be able to carry one out.
    /// </remarks>
    /// <returns>Whether the order was taken. A refusal leaves the car exactly as it was.</returns>
    public bool OrderCar(int car, Vector2 toM)
    {
        if (car < 0 || car >= Cars.Count || Cars.Broken[car] || _recovery.OnTheHookOf[car] >= 0) return false;

        // An order gives up the wheel: both say where the car goes, and a hand still on the keys would
        // overwrite the order on the very next tick.
        if (_selected.Holds(SelectionKind.Car, car)) GaveUpTheWheel();

        var kind = TheOrderAt(car, toM, out var lead);
        _carOrders.Take(car, kind, toM, lead);
        if (BeginTheOrder(car)) return true;

        // Nowhere to carry it out: the car keeps whatever it was doing, and manual mode still stands so
        // that it idles awaiting the next order rather than driving off on one of its own (CTL-4).
        _carOrders.Done(car);
        return false;
    }

    /// <summary>The reset: the car goes back to choosing for itself (CTL-4).</summary>
    /// <remarks>
    /// <b>And a car with nobody in it is stood down rather than left driving</b> (CAR-1). The hand was the
    /// whole of what was choosing for it, so a leg still in hand once the hand is gone is a car driving
    /// itself somewhere for no reason at all. A service vehicle is not one of those — its crew is out
    /// working and its errand is still choosing (AMB-10, SRV-3).
    /// </remarks>
    public void ReleaseOrderOfCar(int car)
    {
        if (car < 0 || car >= Cars.Count) return;

        _carOrders.Release(car);
        if (Cars.Driven[car] && _containers.IsFree(car) && !IsAServiceVehicle(car)) StandTheCarDown(car);
    }

    /// <summary>
    /// <b>Which of the four orders a click is</b> (CTL-8), decided by what the pointer was over and by
    /// nothing else — a car, a car park, ground a car may drive on, or none of those.
    /// </summary>
    /// <remarks>
    /// The ground is the plan's own classifier and not a shape test, so the answer agrees with what the
    /// reader can see under the pointer to within half a cell. <b>A click on the ordered car itself is
    /// not a car it can follow</b>, and falls through to the ground beneath it.
    /// </remarks>
    PlayerOrder TheOrderAt(int car, Vector2 pointM, out int lead)
    {
        // A wreck is not a car to follow (CAR-1), and neither is the car being ordered: both fall through
        // to the ground under them, so clicking one is an order to drive to where it stands.
        lead = CarAt(pointM);
        if (lead == car || (lead >= 0 && Cars.Broken[lead])) lead = PlayerOrders.NoCar;
        if (lead >= 0) return PlayerOrder.FollowThatCar;

        var ground = _terrain.GroundAt(pointM);
        if (ground == Ground.Parking) return PlayerOrder.ParkThere;

        return GroundCatalog.Drivable(ground) ? PlayerOrder.DriveThere : PlayerOrder.ParkAndWalkThere;
    }

    /// <summary>The order in hand, begun. A leg that cannot be arranged at all is a refusal and never a car left half-sent.</summary>
    bool BeginTheOrder(int car) => _carOrders.Kind[car] switch
    {
        PlayerOrder.DriveThere => SendToTheOrderedPlace(car),
        PlayerOrder.ParkThere or PlayerOrder.ParkAndWalkThere => SendToABayNearTheOrder(car),
        PlayerOrder.FollowThatCar => SendAfterTheOrderedCar(car),
        _ => false,
    };

    /// <summary>
    /// <b>One decision of a car under orders</b>, taken before the driver's own and in place of whatever
    /// errand the vehicle would otherwise be running — the same seam a rescue's crew decides at.
    /// </summary>
    /// <remarks>
    /// <b>A hand at the wheel suspends it</b> (S-7): the wheel substitutes the whole behaviour concern and
    /// an order is part of what it substitutes, so the order is left standing and picked up again from the
    /// pose the player leaves the car in.
    /// </remarks>
    void RunTheOrder(int car)
    {
        if (HandAtTheWheel(car)) return;

        switch (_carOrders.Kind[car])
        {
            case PlayerOrder.DriveThere:
                RunToTheOrderedPlace(car);
                return;

            case PlayerOrder.ParkThere:
                if (!Cars.Driven[car]) _carOrders.Done(car);
                return;

            case PlayerOrder.ParkAndWalkThere:
                // The driving is over; what is left of the order is a walk, and it is taken up where the
                // car puts its driver down (<see cref="WalkTheRestOfTheOrder"/>). A car nobody is in has
                // nobody to walk it, so the order ends with the leg.
                if (!Cars.Driven[car] && _containers.DriverOf(car) < 0) _carOrders.Done(car);
                return;

            case PlayerOrder.FollowThatCar:
                RunAfterTheOrderedCar(car);
                return;
        }
    }

    /// <summary>
    /// <b>A place on the carriageway</b> (CTL-8a): the leg is aimed at the point itself rather than at a
    /// bay, so the route search picks whichever direction of the stretch reaches it first and the car
    /// comes to rest along that lane. Aligning to the lane is the line and not a correction applied after.
    /// </summary>
    bool SendToTheOrderedPlace(int car)
    {
        SendTo(car, _carOrders.PointM[car], ParkingRegistry.NoBay);
        return true;
    }

    /// <summary>
    /// And the arrival: at rest, near enough to the place to be said to have got there. <b>Every other way
    /// a leg can end finishes the order too</b> (CTL-4) — settled, abandoned or given up, an order that has
    /// run its recovery idles awaiting the next one instead of being tried again.
    /// </summary>
    void RunToTheOrderedPlace(int car)
    {
        if (!Cars.Driven[car])
        {
            _carOrders.Done(car);
            return;
        }

        if (Cars.VelocityMps[car].Length() > _config.Driving.StopSpeedMps) return;
        if ((Cars.PositionM[car] - _carOrders.PointM[car]).Length() > _config.OrderedPlaceReachM) return;

        StandTheCarDown(car);
        _carOrders.Done(car);
    }

    /// <summary>
    /// <b>A bay</b> (CTL-8b): the free one nearest the point, which is the bay under the pointer wherever
    /// that one can be had and the one a driver sent there would settle for wherever it cannot.
    /// </summary>
    /// <remarks>
    /// <b>The search is the whole town and not a walk of the place</b>, which is the one way an order's
    /// choice of bay differs from a trip's. A trip is bounded because nobody parks a mile from the door
    /// they are going to (PER-10a); a player who clicked on a full car park asked for the nearest free bay
    /// to it, and answering with nothing is a click that looks like it did not land.
    /// </remarks>
    bool SendToABayNearTheOrder(int car)
    {
        var bay = FreeBayNear(_carOrders.PointM[car], TheWholeTownM);
        if (bay < 0) return false;

        SendTo(car, _parking.CentreM(bay), bay);
        return true;
    }

    /// <summary>Far enough to reach every bay in the town from any point in it, which is its own diagonal.</summary>
    float TheWholeTownM => _plan.WorldSizeM.Length();

    /// <summary>
    /// <b>The rest of a park-and-walk order</b> (CTL-8b), taken up at the moment the car puts its driver
    /// down — which is the only moment there is a body in the town to give a walk to.
    /// </summary>
    /// <remarks>
    /// It is the walker's own order and not a second kind of one (CTL-3): the point is handed to
    /// <see cref="TakeTheOrder(int, Vector2)"/>, so a building at the end of it is walked to and entered
    /// exactly as it would be had the player clicked it with the walker selected.
    /// </remarks>
    bool WalkTheRestOfTheOrder(int car, int person)
    {
        if (_carOrders.Kind[car] != PlayerOrder.ParkAndWalkThere) return false;

        var toM = _carOrders.PointM[car];
        _carOrders.Done(car);
        TakeTheOrder(person, toM);
        return true;
    }

    /// <summary>
    /// <b>Another car</b> (CTL-8c): the leg is aimed at a place back along the road from it, which is the
    /// same shape as a rescue's standoff and is there for the same reason — a vehicle can only be made to
    /// stand somewhere it can arrive.
    /// </summary>
    bool SendAfterTheOrderedCar(int car)
    {
        var lead = _carOrders.Lead[car];
        if (lead < 0 || lead >= Cars.Count || lead == car) return false;

        var behindM = TheFollowingPlaceM(lead);
        _carOrders.AimedAtM[car] = behindM;
        SendTo(car, behindM, ParkingRegistry.NoBay);
        return true;
    }

    /// <summary>
    /// And the station kept: the leg is drawn again once the car in front has moved far enough to be worth
    /// a fresh route, and once more wherever the leg it was on ended without it.
    /// </summary>
    /// <remarks>
    /// <b>What holds the gap is the road, not this</b> (S-2a): the follower is granted what is left of the
    /// stretch in front of the car already on it and holds the speed that road affords, every tick. Drawing
    /// the route again is only how the goal keeps up, and it is bounded so that a leader creeping forward in
    /// a queue is not a route search a second.
    /// </remarks>
    void RunAfterTheOrderedCar(int car)
    {
        var lead = _carOrders.Lead[car];

        // The car it was following has stopped being one — wrecked, or on somebody's arm. A follower with
        // nothing to follow stands where it is and idles awaiting the next order (CTL-4).
        if (lead < 0 || lead >= Cars.Count || Cars.Broken[lead] || _recovery.OnTheHookOf[lead] >= 0)
        {
            if (Cars.Driven[car]) StandTheCarDown(car);
            _carOrders.Done(car);
            return;
        }

        var behindM = TheFollowingPlaceM(lead);
        if (Cars.Driven[car] && (behindM - _carOrders.AimedAtM[car]).Length() <= _config.OrderedFollowRedrawM) return;

        _carOrders.AimedAtM[car] = behindM;
        SendTo(car, behindM, ParkingRegistry.NoBay);
    }

    /// <summary>Where a follower is sent: a gap back along the road the car in front is pointing down.</summary>
    /// <remarks>
    /// The leader's own heading and not the lane's, unlike a casualty's standoff (AMB-10): a car has a
    /// direction of its own and a body in the road has not, and reading the leader's tells the follower
    /// which end of a car it is meant to be at even where the two are off the lane entirely.
    /// </remarks>
    Vector2 TheFollowingPlaceM(int lead) =>
        Cars.PositionM[lead] - (ForwardOf(lead) * _config.OrderedFollowGapM);

    /// <summary>
    /// <b>Where an order stops the car</b> — `P-18`'s place, asked of a hand's order the same way it is
    /// asked of a rescue's casualty and a recovery's wreck.
    /// </summary>
    bool TheOrderStopsAt(int car, out Vector2 placeM)
    {
        switch (_carOrders.Kind[car])
        {
            case PlayerOrder.DriveThere:
                placeM = _carOrders.PointM[car];
                return true;

            // Read live rather than off the leg, so the gap is held against where the car in front
            // actually is and not against where it was when the route was last drawn.
            case PlayerOrder.FollowThatCar:
                var lead = _carOrders.Lead[car];
                placeM = lead >= 0 && lead < Cars.Count ? TheFollowingPlaceM(lead) : default;
                return lead >= 0 && lead < Cars.Count;

            default:
                placeM = default;
                return false;
        }
    }

    /// <summary>
    /// Whether the leg in hand finishes at a place on a lane rather than in a bay — the one thing about an
    /// order the ordinary drive-leg machinery has to know (<see cref="IsAimedAtAPlaceInTheRoad"/>).
    /// </summary>
    bool IsOrderedToAPlaceInTheRoad(int car) =>
        _carOrders.Kind[car] is PlayerOrder.DriveThere or PlayerOrder.FollowThatCar;

    /// <summary>
    /// <b>Whether a leg ending puts this car's driver out of it</b>. A trip's does, because a drive that has
    /// finished is a walk to the door; and so does the one order that ends in a walk (CTL-8b). Under every
    /// other order the driver keeps their seat and idles awaiting the next one (CTL-4).
    /// </summary>
    bool LetsItsDriverOut(int car) =>
        !_carOrders.Manual[car] || _carOrders.Kind[car] == PlayerOrder.ParkAndWalkThere;

    /// <summary>
    /// A person sitting in a car that is under orders is at the wheel of it and not a passenger waiting to
    /// be let out — which is what stops <see cref="TripStage.Driving"/> putting them on the pavement the
    /// moment an order is carried out.
    /// </summary>
    bool SitsAtTheWheelOfAnOrderedCar(int person)
    {
        var inside = _containers.WhereIs(person);
        return inside.Kind == ContainerKind.Car && _carOrders.Manual[inside.Index];
    }
}
