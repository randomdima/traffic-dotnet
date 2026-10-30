using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Physics;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A served town's players, as people of it</b> (MP-4, MP-13): each seat a body on the pavement that its keys walk,
/// that gets into the nearest vehicle and out again, and that a car can knock down like anybody else.
/// </summary>
/// <remarks>
/// <para>
/// <b>A body is a person of the town in every way but its will</b>: it is drawn, struck, carried to hospital and
/// treated as anybody is, and what it never does is decide — no trip, no walk, no claim (<see cref="DecideNow"/>). While
/// a seat is it, the keys are its whole behaviour; let go of, it is taken offstage.
/// </para>
/// <para>
/// <b>Getting in puts out whoever drives</b>, a town's driver or another player, and the crew and the stretcher stay
/// where they are: only the wheel changes hands. A vehicle's own errand is under the hand as any car's is (CTL-5d), and
/// takes up again where the hand leaves it.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    readonly PlayerBodies _players;

    /// <summary>
    /// <b>The players' bodies, laid offstage</b> after everybody else on the roster (MP-4), in the walkers' looks in turn,
    /// so the bodies a seat is one after another are different people.
    /// </summary>
    void StandThePlayersBodies()
    {
        var atM = _plan.WorldSizeM * 0.5f;
        for (var body = 0; body < _players.Count; body++)
        {
            var person = People.Add(
                atM, 0f, _config.Person.MassKg, _config.PersonRadiusM, (byte)(body % PersonCatalog.Shared.Count),
                new Rng(_agentSeed, PlayerStream + (ulong)body), reckless: false);
            if (body == 0) _players.First = person;

            _progress.Restart(person);
            People.Manual[person] = true;
            People.Stage[person] = TripStage.UnderOrders;
            _containers.PutIn(Offstage, person);
            Contain(person);
        }
    }

    static Contained Offstage => new(ContainerKind.Offstage, 0);

    /// <summary>The person a seat is now, or −1 before it is anybody.</summary>
    public int BodyOfSeat(int seat) => _players.BodyOf(seat);

    /// <summary>The seat a person is the body of now, or −1 (MP-12).</summary>
    public int SeatOfPerson(int person) => _players.SeatOf(person);

    /// <summary>The seat driving a rail vehicle's train, asked of its front vehicle alone — one mark a player (MP-12) — or −1.</summary>
    public int SeatOfWagon(int wagon)
    {
        var train = _trains.TrainOfWagon(wagon);
        return _trains.Running(train) && _trains.VehiclesOf(train)[0] == wagon ? _players.SeatOfTrain(train) : -1;
    }

    /// <summary>Whether a seat's body lies where it fell (PER-18): a player waiting to be somebody new.</summary>
    public bool IsDown(int seat) => _players.BodyOf(seat) is var body and >= 0 && People.Wounded[body];

    /// <summary>
    /// <b>What a player is drawn as</b>, which its area is round (MP-1): its body, the car it is in — at the wheel or on a
    /// stretcher — or the train it drives. Nothing before it is anybody.
    /// </summary>
    public Selection ShownAs(int seat)
    {
        var body = _players.BodyOf(seat);
        if (body < 0) return default;

        var inside = People.Inside[body];
        return inside.Kind switch
        {
            ContainerKind.Car => new Selection(SelectionKind.Car, inside.Index),
            ContainerKind.Train when _trains.Running(inside.Index) => new Selection(SelectionKind.Locomotive, inside.Index),
            _ => new Selection(SelectionKind.Person, body),
        };
    }

    /// <summary>
    /// <b>A seat stood up as somebody new</b> (MP-4): a body of its own put down on the pavement nearest a place — off a
    /// building's way in where the place is inside one — and the body it was before left to the town. False while every
    /// body it has is in the town's care, or there is nowhere free to put one down; asked again on a later tick.
    /// </summary>
    /// <remarks>
    /// <b>A player's own act, so it may look the ground up</b> (the root decision log, 2026-10-13), as an order does.
    /// </remarks>
    public bool SpawnPlayer(int seat, Vector2 nearM)
    {
        var body = SpareBodyOf(seat);
        if (body < 0) return false;

        var building = BuildingAt(nearM);
        var (place, free) = building >= 0 ? DoorExitOf(building, nearM) : PavementNearest(nearM);
        if (!TryPutDownOff(place, free, beside: -1, out var spotM, out var spot)) return false;

        LetThePlayerGo(seat);
        if (People.Wounded[body]) ClearAway(body);

        _containers.TakeOut(body);
        Place(body, spotM, People.HeadingRad[body], spot);
        People.Stage[body] = TripStage.UnderOrders;
        People.Manual[body] = true;
        _players.Become(seat, body);
        return true;
    }

    /// <summary>
    /// A body a seat may be next: one offstage, or else one it left lying in the road that no ambulance is coming for —
    /// which is a town with no hospital, or one whose call was given up (AMB-9). −1 while there is neither.
    /// </summary>
    int SpareBodyOf(int seat)
    {
        var (from, to) = _players.BodiesOf(seat);
        for (var body = from; body < to; body++)
        {
            if (People.Inside[body].Kind == ContainerKind.Offstage) return body;
        }

        for (var body = from; body < to; body++)
        {
            if (People.Wounded[body] && !People.Inside[body].Any && !IsSpokenFor(body)) return body;
        }

        return -1;
    }

    /// <summary>A body lying in the road taken up out of it, as if it had never been knocked down (<see cref="RaiseTheCall"/>).</summary>
    void ClearAway(int person)
    {
        PutTheWalkerOnLayer(person, CollisionLayer.Person);
        People.Wounded[person] = false;
        _wounded.Remove(person);
        _woundedCount--;
    }

    /// <summary>The pavement nearest a point, with the spots off it clear of the furniture (<see cref="FreeSpots"/>).</summary>
    (FootPlace Place, uint Free) PavementNearest(Vector2 pointM)
    {
        var place = _walking.PlaceNearest(pointM);
        return (place, FreeSpotsOf(place));
    }

    /// <summary>
    /// <b>A seat's keys this tick</b> (MP-3, CTL-5d), on whatever its body is in: walked on foot, at a car's wheel, at a
    /// train's controls — and on nothing while it lies in the road or is anywhere else. A train driven off the map takes
    /// nobody with it: its driver is the seat's no longer, and is stood up afresh.
    /// </summary>
    public void HandOnPlayer(int seat, HandInput hand)
    {
        var body = _players.BodyOf(seat);
        if (body < 0)
        {
            _seats.Hold(seat, -1, HandInput.None);
            _players.Drive(seat, -1);
            _players.Hold(seat, HandInput.None);
            return;
        }

        var inside = People.Inside[body];
        if (inside.Kind == ContainerKind.Train && !_trains.Running(inside.Index))
        {
            TakeOffstage(body);
            _players.Become(seat, -1);
            HandOnPlayer(seat, hand);
            return;
        }

        var acts = People.Acts(body);
        var car = acts && inside.Kind == ContainerKind.Car && _containers.DriverOf(inside.Index) == body ? inside.Index : -1;
        _seats.Hold(seat, car, car >= 0 ? hand : HandInput.None);
        _players.Drive(seat, acts && inside.Kind == ContainerKind.Train ? inside.Index : -1);
        _players.Hold(seat, acts ? hand : HandInput.None);
    }

    /// <summary>The hand a seat walks its body on foot with this tick, if a seat's body this is and it is on its feet in the town.</summary>
    bool PlayerWalks(int person, out HandInput hand)
    {
        var seat = _players.SeatOf(person);
        hand = seat >= 0 ? _players.HandOf(seat) : default;
        return hand.Held && !People.Inside[person].Any && People.Acts(person);
    }

    /// <summary>
    /// <b>A player's way in and out</b> (MP-13): on foot, into the nearest vehicle within <c>BoardReachM</c> of the body — at
    /// a car's wheel or a train's controls, whoever drove it put out — and in one, out at once beside it. Whether it got in
    /// or out.
    /// </summary>
    public bool PlayerActs(int seat)
    {
        var body = _players.BodyOf(seat);
        if (body < 0 || !People.Acts(body)) return false;

        var inside = People.Inside[body];
        switch (inside.Kind)
        {
            case ContainerKind.None:
                return GetIn(seat, body);
            case ContainerKind.Car:
                StepOut(inside.Index, body);
                return true;
            case ContainerKind.Train:
                StepOffTheTrain(inside.Index, body);
                return true;
            default:
                return false;
        }
    }

    bool GetIn(int seat, int body)
    {
        var atM = People.PositionM[body];
        var reachM = _config.Multiplayer.BoardReachM + People.RadiusM[body];
        var car = NearestCarWithin(atM, reachM, out var carM);
        var wagon = NearestRailVehicleWithin(atM, reachM, out var wagonM);
        if (car >= 0 && (wagon < 0 || carM <= wagonM))
        {
            TakeTheCar(seat, body, car);
            return true;
        }

        if (wagon < 0) return false;

        TakeTheTrain(seat, body, _trains.TrainOfWagon(wagon));
        return true;
    }

    /// <summary>
    /// <b>A car taken</b>: whoever is at the wheel put out beside it — a town's driver with the trip it was on given up and
    /// drawn again on foot, a player still that player on foot — and the body in its place.
    /// </summary>
    void TakeTheCar(int seat, int body, int car)
    {
        var driver = _containers.DriverOf(car);
        if (driver >= 0)
        {
            var other = _players.SeatOf(driver);
            StepOut(car, driver);
            if (other < 0)
            {
                if (Cars.Driven[car]) StandTheCarDown(car);
                GiveUpTheTrip(driver);
            }
        }

        _containers.TryBoard(car, body);
        Contain(body);
        _seats.Hold(seat, car, _players.HandOf(seat));
    }

    /// <summary>A train's controls taken: a player at them put down beside its cab, and the body in the cab.</summary>
    void TakeTheTrain(int seat, int body, int train)
    {
        var other = _players.SeatOfTrain(train);
        if (other >= 0) StepOffTheTrain(train, _players.BodyOf(other));

        _containers.PutIn(new Contained(ContainerKind.Train, train), body);
        Contain(body);
        _players.Drive(seat, train);
    }

    /// <summary>
    /// <b>Out of a car at once</b>: onto the pavement beside it where a spot there is free (PHY-7a), and where none is
    /// within reach of the driver's door, on the ground off that door, as a crash puts somebody (PHY-6) — a player is
    /// never kept in. <b>Off a deck's channel there is no pavement</b> (PHY-1b): the walker is put off the door onto the
    /// deck, on the car's channels, and not on the ground under it.
    /// </summary>
    void StepOut(int car, int person)
    {
        var (beside, free) = ExitBesideTheCar(car);
        var doorM = DriverDoorM(car);
        var channels = Cars.Channels[car];
        if ((channels & CityPlan.RoadArrays.GroundChannel) == 0 || !TryPutDownOff(beside, free, car, out var spotM, out _) ||
            Vector2.Distance(spotM, doorM) > _config.PersonExitSearchRadiusM)
        {
            spotM = doorM;
        }

        _containers.Alight(car, person);
        if (_players.SeatOf(person) is var seat and >= 0) _seats.Hold(seat, -1, HandInput.None);
        Place(person, spotM, Trig.Atan2(spotM.Y - Cars.PositionM[car].Y, spotM.X - Cars.PositionM[car].X), beside, channels);
    }

    /// <summary>Off a train at once, the same way: onto the pavement nearest the cab's door, or the ground off it.</summary>
    void StepOffTheTrain(int train, int person)
    {
        var cab = _trains.VehiclesOf(train)[0];
        ref readonly var view = ref _world.View.Wagons[cab];
        var forward = Heading.Unit(view.HeadingRad);
        var doorM = view.PositionM + (Heading.RightOf(forward) * -_config.RoadSideSign *
                                      ((_trains.VehicleOf(cab).FootprintM.Y * 0.5f) + _config.PersonDiameterM));
        var (place, free) = PavementNearest(doorM);
        if (!TryPutDownOff(place, free, beside: -1, out var spotM, out _) ||
            Vector2.Distance(spotM, doorM) > _config.PersonExitSearchRadiusM)
        {
            spotM = doorM;
        }

        _containers.TakeOut(person);
        if (_players.SeatOf(person) is var seat and >= 0) _players.Drive(seat, -1);
        Place(person, spotM, Trig.Atan2(spotM.Y - view.PositionM.Y, spotM.X - view.PositionM.X), place);
    }

    /// <summary>
    /// <b>A seat given up</b> (MP-4): whatever it drives let go of, and its body taken offstage — out of a vehicle, or off
    /// the pavement it stood on. A body lying in the road is the town's to come for, and comes back offstage from the
    /// hospital (<see cref="DecideContained"/>).
    /// </summary>
    public void LetThePlayerGo(int seat)
    {
        var body = _players.BodyOf(seat);
        _seats.Hold(seat, -1, HandInput.None);
        _players.Drive(seat, -1);
        _players.Hold(seat, HandInput.None);
        _players.Become(seat, -1);
        if (body >= 0 && People.Acts(body)) TakeOffstage(body);
    }

    /// <summary>A player's body out of the town: out of whatever it was in, and offstage.</summary>
    void TakeOffstage(int person)
    {
        var inside = People.Inside[person];
        switch (inside.Kind)
        {
            case ContainerKind.Car:
                _containers.Alight(inside.Index, person);
                break;
            case ContainerKind.Building:
                _containers.LeaveBuilding(inside.Index, person);
                break;
            case ContainerKind.Train:
                _containers.TakeOut(person);
                break;
        }

        if (!inside.Any) Contain(person);
        _containers.PutIn(Offstage, person);
        People.Stage[person] = TripStage.UnderOrders;
        People.Manual[person] = true;
    }

    /// <summary>
    /// The nearest car whose body is within a reach of a point, and how far its body is; or −1. A wreck and a car on a
    /// tractor's arm have no wheel to take.
    /// </summary>
    int NearestCarWithin(Vector2 pointM, float reachM, out float nearestM)
    {
        var best = -1;
        nearestM = reachM;
        for (var car = 0; car < Cars.Count; car++)
        {
            if (Cars.Broken[car] || _hitches.IsHeld(car) || Cars.Action[car] == CarAction.Towed) continue;

            ref readonly var build = ref Cars.BuildOf(car);
            var offM = OffABox(pointM - Cars.PositionM[car], Cars.Forward[car], new Vector2(build.HalfLengthM, build.FlankM));
            if (offM > nearestM) continue;

            best = car;
            nearestM = offM;
        }

        return best;
    }

    /// <summary>The nearest running rail vehicle whose body is within a reach of a point, and how far its body is; or −1.</summary>
    int NearestRailVehicleWithin(Vector2 pointM, float reachM, out float nearestM)
    {
        var best = -1;
        nearestM = reachM;
        for (var train = 0; train < _trains.Count; train++)
        {
            if (!_trains.Running(train)) continue;

            foreach (var wagon in _trains.VehiclesOf(train))
            {
                ref readonly var view = ref _world.View.Wagons[wagon];
                if (!view.IsInWorld) continue;

                var offM = OffABox(pointM - view.PositionM, Heading.Unit(view.HeadingRad), _trains.VehicleOf(wagon).FootprintM * 0.5f);
                if (offM > nearestM) continue;

                best = wagon;
                nearestM = offM;
            }
        }

        return best;
    }

    /// <summary>How far a point is from a box round a body's middle — nought inside it — in the body's own frame.</summary>
    static float OffABox(Vector2 offsetM, Vector2 forward, Vector2 halfM)
    {
        var alongM = MathF.Max(0f, MathF.Abs(Vector2.Dot(offsetM, forward)) - halfM.X);
        var acrossM = MathF.Max(0f, MathF.Abs(Vector2.Dot(offsetM, Heading.RightOf(forward))) - halfM.Y);
        return MathF.Sqrt((alongM * alongM) + (acrossM * acrossM));
    }

    const ulong PlayerStream = 0x504C4159;
}
