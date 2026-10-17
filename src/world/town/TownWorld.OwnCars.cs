using System.Numerics;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A car of somebody's own</b> (PER-29): driven by its owner's trip to a bay near the door they are going to,
/// and standing in its bay the rest of the time. <b>The driving is the leg's</b> (<see cref="SendTo"/>), parking and
/// unparking are the bay's own ways (GEN-4f), and what is here is only why the car goes and when its owner gets out.
/// </summary>
/// <remarks>
/// <para>
/// <b>It replaces the round</b> (CAR-8, <see cref="RunTheRound"/>) for every car somebody owns: a car its owner may
/// come back to at any moment does not wander off on a round of its own.
/// </para>
/// <para>
/// <b>Nobody aboard, it moves only to be where its owner can walk to it</b>: stood down outside a bay it parks in
/// the free bay nearest where it stands, and left further off than anybody walks it is called to the free bay
/// nearest its owner (<see cref="CallTheCar"/>) — both of them a leg of its own, as the round's were (CAR-1).
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>How many times a car has driven itself to its owner, left further off than they walk.</summary>
    public long CarsCalled { get; private set; }

    /// <summary>
    /// One decision of a car somebody owns, taken before the leg's own. <b>With its owner at the wheel it is the
    /// trip's</b>; with nobody in it, it stands in its bay, or parks in one where it was left anywhere else.
    /// </summary>
    void RunTheOwnersCar(int car, float sinceLastDecisionS)
    {
        if (Cars.Broken[car] || WheelIsHeldOver(car)) return;

        var owner = Cars.Owner[car];
        if (IsAtTheWheel(owner))
        {
            DriveTheOwner(car, owner, sinceLastDecisionS);
            return;
        }

        if (Cars.Driven[car] || StandsInItsBay(car)) return;

        var bay = FreeBayOff(RoadPlaceOfCar(car), Cars.PositionM[car], _config.PersonWalkReachM, car);
        if (bay >= 0) SendTo(car, _parking.CentreM(bay), bay);
    }

    /// <summary>
    /// <b>The trip at the wheel</b>: a drive to a bay is left to the leg, a car parked at the end of one lets its
    /// owner out, and a leg that ended anywhere else — given up in the street, or an order's that is not the trip's
    /// — is a trip given up, drawn again from where the car stands and driven like any trip begun at a wheel.
    /// </summary>
    void DriveTheOwner(int car, int owner, float sinceLastDecisionS)
    {
        if (People.Stage[owner] == TripStage.StandingBy)
        {
            People.TimerS[owner] -= sinceLastDecisionS;
            if (People.TimerS[owner] <= 0f) DrawTrip(owner);
            return;
        }

        if (Cars.Driven[car] && BayAimedAt(car) >= 0) return;

        if (!Cars.Driven[car] && StandsInItsBay(car))
        {
            LetTheOwnerOut(car, owner);
            return;
        }

        if (Cars.Driven[car]) StandTheCarDown(car);
        GiveUpTheTrip(owner);
    }

    /// <summary>When each car's owner was last refused the ground beside its door (<see cref="LetTheOwnerOut"/>), on the town's clock.</summary>
    readonly float[] _exitRefusedAtS;

    /// <summary>
    /// <b>The owner out, beside the driver's door</b> (GEN-4e, PHY-7a) — refused, and asked again every
    /// <see cref="Core.Config.PersonFigures.ExitAskEveryS"/>, while there is no free ground there — and the rest of the trip walked: to the door it was driven for, where that is within a
    /// walk of where they got out, and otherwise a trip drawn again from there.
    /// </summary>
    void LetTheOwnerOut(int car, int owner)
    {
        if (_elapsedS - _exitRefusedAtS[car] < _config.Person.ExitAskEveryS) return;

        var (beside, free) = ExitBesideTheCar(car);
        if (!TryPutDownOff(beside, free, car, out var spotM, out var spot))
        {
            _exitRefusedAtS[car] = _elapsedS;
            return;
        }

        _containers.Alight(car, owner);
        Place(owner, spotM, MathF.Atan2(spotM.Y - Cars.PositionM[car].Y, spotM.X - Cars.PositionM[car].X), spot);

        if (People.Manual[owner])
        {
            People.Stage[owner] = TripStage.UnderOrders;
            return;
        }

        var building = People.DestinationBuilding[owner];
        if (building == PersonFleet.NoBuilding)
        {
            DrawTrip(owner);
            return;
        }

        var buildingDoorM = DoorOf(building, spotM);
        if ((buildingDoorM - spotM).Length() > _config.PersonWalkReachM)
        {
            GiveUpTheTrip(owner);
            return;
        }

        People.Stage[owner] = TripStage.WalkingFromTheCar;
        WalkTo(owner, buildingDoorM, DoorPlaceOf(building, spotM));
    }

    /// <summary>
    /// <b>The leg to a bay near a door</b>: the free one nearest it on its own block (<see cref="Core.Config.SimConfig.PersonWalkWorthM"/>),
    /// or within a walk of it where the block's are taken. A door with neither is a trip given up, and drawn again.
    /// </summary>
    void DriveToABayNear(int car, int building, Vector2 doorM)
    {
        var street = _roadPlaceOfBuilding[building];
        var bay = FreeBayOff(street, doorM, _config.PersonWalkWorthM, car);
        if (bay < 0) bay = FreeBayOff(street, doorM, _config.PersonWalkReachM, car);
        if (bay < 0)
        {
            GiveUpTheTrip(Cars.Owner[car]);
            return;
        }

        GiveUpTheBay(car);
        TakeTheBay(car, bay);
        SetOff(car);
    }

    /// <summary>
    /// <b>A car further off its owner than anybody walks is called to them</b> (PER-29): it drives itself to the free
    /// bay nearest them, so whatever left them apart — an order, a hospital, a wreck mended in a depot's yard — the
    /// two are a walk apart again by the time the car has parked.
    /// </summary>
    void CallTheCar(int person)
    {
        var car = People.Car[person];
        if (car == PersonFleet.NoCar || !IsFreeToDrive(car)) return;

        if (Vector2.Distance(Cars.PositionM[car], People.PositionM[person]) <= _config.PersonWalkReachM) return;

        var bay = FreeBayOff(RoadPlaceOfWalker(person), People.PositionM[person], _config.PersonWalkReachM, car);
        if (bay < 0) return;

        CarsCalled++;
        SendTo(car, _parking.CentreM(bay), bay);
    }

    /// <summary>Whether this person's own car is free to drive and within a walk of them — which is what a driven trip needs.</summary>
    bool HasTheCarToHand(int person)
    {
        var car = People.Car[person];
        return car != PersonFleet.NoCar
               && IsFreeToDrive(car)
               && Vector2.Distance(Cars.PositionM[car], People.PositionM[person]) <= _config.PersonWalkReachM;
    }

    /// <summary>
    /// <b>Whether a car is free to be driven off</b>: whole, off every hook, out from under the player's orders and hand,
    /// with nobody in it and no leg of its own under way.
    /// </summary>
    bool IsFreeToDrive(int car) =>
        !Cars.Broken[car]
        && !Cars.Driven[car]
        && _recovery.OnTheHookOf[car] < 0
        && !IsUnderOrders(car)
        && !WheelIsHeldOver(car)
        && !_containers.AnybodyAboard(car);

    /// <summary>Whether this person is at the wheel of their own car.</summary>
    internal bool IsAtTheWheel(int person) =>
        People.Car[person] is var car and not PersonFleet.NoCar && _containers.DriverOf(car) == person;

    /// <summary>Whether a car is standing in the bay registered as its own, which is what a parked car is.</summary>
    bool StandsInItsBay(int car) => _parking.BayOf(car) is var bay and >= 0 && _parking.HoldsTheBody(bay, Cars.PositionM[car]);
}
