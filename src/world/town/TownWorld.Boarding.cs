using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Parking;

namespace TrafficSimulation.World.Town;

/// <summary>The legs of a trip that are not walking: a door gone through, a car got into and out of, and the containment that hides a body while it is inside one.</summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// The three columns <see cref="ExitSpots"/> reads, handed over as spans: the containment slice
    /// places a body without ever learning what an agent is (PHY-7a).
    /// </summary>
    ExitSpots.Standing StandingPeople => new(People.PositionM, People.RadiusM, People.Inside);

    /// <summary>
    /// `P-9`: the door. Capacity is checked here, atomically — the claim held during the walk was
    /// advisory and only kept the crowd down.
    /// </summary>
    void EnterTheBuilding(int person)
    {
        var building = People.DestinationBuilding[person];
        if (building < 0)
        {
            // A wander, an order to a point, or one end of a paced road: arriving is the end of it. Only
            // the lane end of a pace is stood in — back on the pavement there is nothing to wait for, so
            // the road is asked again at once and the body steps out as soon as it is clear.
            if (People.Manual[person]) People.Stage[person] = TripStage.UnderOrders;
            else if (PacesARoad(person, out _, out var inTheLane) && inTheLane) StandABeat(person);
            else DrawTrip(person);
            return;
        }

        if (!_containers.TryAdmit(building, person))
        {
            if (People.Stage[person] != TripStage.WaitingForAPlace)
            {
                DoorsFoundFull++;
                People.Stage[person] = TripStage.WaitingForAPlace;
                People.TimerS[person] = _config.Building.DwellMaxS * PlacePatienceInDwells;
            }

            return;
        }

        BuildingsEntered++;
        _containers.GiveUpClaim(building);
        People.DestinationBuilding[person] = PersonFleet.NoBuilding;
        Contain(person);
        People.Stage[person] = TripStage.Dwelling;
        People.TimerS[person] = People.Draw[person].NextFloat(_config.Building.DwellMinS, _config.Building.DwellMaxS);
    }

    /// <summary>
    /// `P-6`: the car's eligibility is asked at this moment and not when the trip was drawn. On refusal
    /// this is not a retry — something else took the car, and a walker who has already walked to it is
    /// closer to walking the whole way than to walking to a second one.
    /// </summary>
    void BoardTheCar(int person)
    {
        var car = People.TripCar[person];
        if (car < 0 || !CanBeBoarded(car) || !_containers.TryBoard(car, person))
        {
            GiveUpTheCar(person);
            WalkTheTripInstead(person);
            return;
        }

        Boardings++;
        People.TripCar[person] = car;
        People.Stage[person] = TripStage.Driving;
        Contain(person);
        SetOff(car);
    }

    /// <summary>The drive fell through: the destination stands, so what is left of the trip is a walk to it.</summary>
    void WalkTheTripInstead(int person)
    {
        var building = People.DestinationBuilding[person];
        if (building < 0)
        {
            DrawTrip(person);
            return;
        }

        People.Stage[person] = TripStage.WalkingToTheDoor;
        WalkTo(person, DoorOf(building, People.PositionM[person]));
    }

    /// <summary>
    /// `P-7`: a spot beside the car, preferring the side the pavement is on — the difference between the
    /// next leg being a formality and being a road crossing. Refused means every position round the car
    /// is taken, so the person stays in it and asks again.
    /// </summary>
    void TryAlight(int person)
    {
        var where = _containers.WhereIs(person);
        if (where.Kind != ContainerKind.Car)
        {
            People.Stage[person] = TripStage.StandingBy;
            return;
        }

        var car = where.Index;
        var wayOutM = WayInOf(car);

        // `E-10`: out of a wrecked car at once, and onto whatever the car offers — road or not. Getting
        // off the road afterwards is the walker's own rule and is the next leg's problem.
        if (!ExitSpots.TryFind(
                _config, _terrain, _physics, _nearby, StandingPeople, wayOutM, wayOutM, _spotNearby, out var spotM,
                anyGround: Cars.Broken[car]))
        {
            return;
        }

        Alightings++;
        _containers.Alight(car, person);
        People.TripCar[person] = PersonFleet.NoCar;
        Place(person, spotM, MathF.Atan2(spotM.Y - Cars.PositionM[car].Y, spotM.X - Cars.PositionM[car].X));

        // A leg that has landed the person further from the door than they would ever have walked drops
        // the destination rather than handing over the walk; standing by draws a whole fresh trip.
        var building = People.DestinationBuilding[person];
        var doorM = building >= 0 ? DoorOf(building, spotM) : spotM;
        if (building < 0 || Trip.IsTooFarToWalk(_config, (doorM - spotM).Length()))
        {
            DrawTrip(person);
            return;
        }

        People.Stage[person] = TripStage.WalkingToTheDoor;
        WalkTo(person, doorM);
    }

    /// <summary>`P-1`: the building places its occupant outside, and refuses while there is nowhere to put them.</summary>
    bool TryLeaveTheBuilding(int person)
    {
        var where = _containers.WhereIs(person);
        if (where.Kind != ContainerKind.Building) return true;

        var building = where.Index;
        var centreM = _plan.Buildings.CentreM[building];
        var doorM = DoorOf(building, centreM);
        if (!ExitSpots.TryFind(
                _config, _terrain, _physics, _nearby, StandingPeople, doorM, doorM + (doorM - centreM), _spotNearby,
                out var spotM))
        {
            return false;
        }

        _containers.LeaveBuilding(building, person);
        Place(person, spotM, MathF.Atan2(spotM.Y - centreM.Y, spotM.X - centreM.X));
        return true;
    }

    /// <summary>
    /// Which of a building's ways in this trip uses. Settled when the trip is drawn and never
    /// afterwards: a door chosen again from where the body has got to re-plans at every swing.
    /// </summary>
    Vector2 DoorOf(int building, Vector2 fromM)
    {
        var buildings = _plan.Buildings;
        var first = buildings.EntryOffsets[building];
        var last = buildings.EntryOffsets[building + 1];
        if (last <= first) return buildings.CentreM[building];

        var best = first;
        var bestDistanceSq = float.MaxValue;
        for (var entry = first; entry < last; entry++)
        {
            var distanceSq = (buildings.EntryPointM[entry] - fromM).LengthSquared();
            if (distanceSq >= bestDistanceSq) continue;

            best = entry;
            bestDistanceSq = distanceSq;
        }

        return buildings.EntryPointM[best];
    }

    /// <summary>Into a container: the body leaves the world and everything the follower held for it is dropped.</summary>
    void Contain(int person)
    {
        People.Walking[person] = false;
        People.ClearWalkedLine(person);
        People.HeldAtTheKerb[person] = false;
        People.WaitingToCrossS[person] = 0f;
        _impulseNs[person] = Vector2.Zero;
        _physics.Contain(People.Body[person]);
    }

    /// <summary>And back out, where the container put them down — the one place a walker's pose is written by anything but the solver.</summary>
    void Place(int person, Vector2 atM, float headingRad)
    {
        People.PositionM[person] = atM;
        People.VelocityMps[person] = Vector2.Zero;
        People.HeadingRad[person] = headingRad;
        People.DestinationM[person] = atM;
        People.GoalM[person] = atM;
        People.Walking[person] = false;
        People.ClearWalkedLine(person);
        People.HeldAtTheKerb[person] = false;
        People.WaitingToCrossS[person] = 0f;
        _impulseNs[person] = Vector2.Zero;
        _progress.Restart(person);
        _physics.Release(People.Body[person], atM, headingRad);
    }

    /// <summary>
    /// The order pins the goal the behaviour would otherwise have picked, and nothing under it changes.
    /// What the pointer was over decides which goal that is — a building or a car is walked to <em>and
    /// entered</em>, and ground is walked to and then stood on.
    /// </summary>
    /// <remarks>
    /// All containment checks bind unchanged: the door is still asked at the door and the car at the
    /// car, so an ordered walker can find a building full or a car taken.
    /// </remarks>
    void TakeTheOrder(int person, Vector2 toM)
    {
        GiveUpTheClaims(person);
        People.Manual[person] = true;

        // Contained when the order arrives: it is taken up the moment the container puts the body down,
        // which is what the dwell's own exit already does.
        if (People.Inside[person].Any) return;

        var building = BuildingAt(toM);
        if (building >= 0)
        {
            People.DestinationBuilding[person] = building;
            _containers.Claim(building);
            People.Stage[person] = TripStage.WalkingToTheDoor;
            WalkTo(person, DoorOf(building, People.PositionM[person]));
            return;
        }

        var car = CarAt(toM);
        if (car >= 0 && CanBeBoarded(car))
        {
            People.TripCar[person] = car;
            People.Stage[person] = TripStage.WalkingToTheCar;
            WalkTo(person, WayInOf(car));
            return;
        }

        People.Stage[person] = TripStage.UnderOrders;
        WalkTo(person, toM);
    }

    /// <summary>The building a point falls inside, or −1 — what a right-click on one reads as.</summary>
    public int BuildingAt(Vector2 pointM)
    {
        var buildings = _plan.Buildings;
        for (var building = 0; building < buildings.Count; building++)
        {
            var headingRad = buildings.HeadingRad[building];
            var forward = Heading.Unit(headingRad);
            var offset = pointM - buildings.CentreM[building];
            var halfM = buildings.SizeM[building] * 0.5f;
            var along = Vector2.Dot(offset, forward);
            var across = Vector2.Dot(offset, new Vector2(-forward.Y, forward.X));
            if (MathF.Abs(along) <= halfM.X && MathF.Abs(across) <= halfM.Y) return building;
        }

        return -1;
    }

    /// <summary>
    /// An order to a car: the goal a driver would have picked is a bay, so this pins which bay — the
    /// nearest free one to the point, re-planned from where the car is.
    /// </summary>
    /// <remarks>
    /// A car with nobody in it takes no order: that would be an order to something that takes no actions
    /// at all. Taking its wheel is how one of those is moved.
    /// </remarks>
    public bool OrderCar(int car, Vector2 toM)
    {
        if (car < 0 || car >= Cars.Count || Cars.Broken[car] || _containers.IsFree(car)) return false;
        if (!RetargetTheBay(car, toM, ParkingRegistry.NoBay)) return false;

        Cars.ClearRoute(car);
        return true;
    }

    /// <summary>`E-9`: the trip failed. Every claim is released and a fresh one is drawn from where the body actually is.</summary>
    void GiveUpTheTrip(int person)
    {
        TripsGivenUp++;
        DrawTrip(person);
    }

    /// <summary>What a trip holds on the town's behalf: a building's claim and a car with a bay reserved for it.</summary>
    void GiveUpTheClaims(int person)
    {
        var building = People.DestinationBuilding[person];
        if (building >= 0) _containers.GiveUpClaim(building);

        People.DestinationBuilding[person] = PersonFleet.NoBuilding;
        GiveUpTheCar(person);
    }

    void GiveUpTheCar(int person)
    {
        var car = People.TripCar[person];
        if (car < 0) return;

        _parking.GiveUpReservation(car);
        People.TripCar[person] = PersonFleet.NoCar;
    }
}
