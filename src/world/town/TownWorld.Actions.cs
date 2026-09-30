using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>Who is doing what</b>: each car's action and each walker's, changed in one place each (CAR-15b, PER-25b), and a
/// hand at a wheel or on the keys taken up and let go of before anything is laid.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>What every car is doing, and the one place that changes (<see cref="CarActions"/>).</summary>
    readonly CarActions _carActions;

    readonly Overtaking _overtaking;

    readonly BackingUp _backingUp;

    readonly Manoeuvres _manoeuvres;

    readonly BayManoeuvring _bays;

    readonly ParkingIn _parkingIn;

    readonly PullingOut _pullingOut;

    readonly Rejoining _rejoining;

    /// <summary>Getting past what stands in a lane (CAR-46), and its instruments.</summary>
    public Overtaking Overtaking => _overtaking;

    /// <summary>Backing up for the room to (CAR-50), and its instruments.</summary>
    public BackingUp BackingUp => _backingUp;

    /// <summary>Every car's manoeuvre at a bay (GEN-4f), and its instruments.</summary>
    public BayManoeuvring Bays => _bays;

    /// <summary>The manoeuvres, for an instrument or the debug layer; the driver's own and read by nothing else.</summary>
    public Manoeuvres Manoeuvres => _manoeuvres;

    bool IsManoeuvring(int car) => _carActions.IsManoeuvring(car);

    /// <summary>
    /// <b>A piece of a manoeuvre driven to its end</b>: the next piece, or — at the end of the last — whatever the
    /// action it is ends in: a car parked, or out on its lane.
    /// </summary>
    bool TheManoeuvreIsDriven(int car)
    {
        if (_bays.TakeTheNextPiece(car)) return true;

        var town = new CarTown(this);
        return Cars.Action[car] == CarAction.Park ? _parkingIn.Arrive(ref town, car) : _pullingOut.Arrive(ref town, car);
    }

    void Enter(int car, CarAction action) => _carActions.Enter(car, action);

    static bool IsAtABay(CarAction action) => CarActions.IsAtABay(action);

    bool DrivesTheRoute(int car) => _carActions.DrivesTheRoute(car);

    /// <summary>
    /// <b>A walker handed over to another action</b> (PER-25b), and what the one it leaves owned let go: a pass is its
    /// sidestep's.
    /// </summary>
    void Enter(int person, PersonAction action)
    {
        var was = People.Action[person];
        if (was == action) return;

        if (was == PersonAction.Sidestep) People.Pass[person] = Sidestep.None;
        People.Action[person] = action;
    }

    /// <summary>Whether a walker's action walks the ways of its route — down them, round somebody on them, or back onto them.</summary>
    static bool WalksItsRoute(PersonAction action) =>
        action is PersonAction.Walk or PersonAction.Sidestep or PersonAction.Rejoin;

    /// <summary>
    /// <b>Whether a walker has somewhere to walk</b>, as its trip says — and with it the action that goes with that:
    /// down its route, or standing.
    /// </summary>
    void SetWalking(int person, bool walking)
    {
        People.Walking[person] = walking;
        Enter(person, walking ? PersonAction.Walk : PersonAction.Stand);
    }

    /// <summary>
    /// <b>A hand taken to a car's wheel or a walker's keys, or taken off</b> (CTL-5, CTL-5d, CTL-6), before anything
    /// is laid this tick: under a hand the agent's own action is over, and let go a car takes the road again from
    /// wherever the hand left it, and a walker walks on or stands as its trip had it.
    /// </summary>
    void TakeUpTheHands()
    {
        for (var person = 0; person < People.Count; person++)
        {
            var action = People.Action[person];
            if (action is PersonAction.Inside or PersonAction.Down) continue;

            var held = _hands.Held && _selected.Holds(SelectionKind.Person, person);
            if (held == (action == PersonAction.Hand)) continue;

            if (held) Enter(person, PersonAction.Hand);
            else Enter(person, People.Walking[person] ? PersonAction.Walk : PersonAction.Stand);
        }

        for (var car = 0; car < Cars.Count; car++)
        {
            // What is on a bar goes where the truck takes it, whoever is holding its wheel (EVA-5).
            if (Cars.Action[car] == CarAction.Towed) continue;

            var held = HandAtTheWheel(car);
            if (held == (Cars.Action[car] == CarAction.Hand)) continue;

            if (held) Enter(car, CarAction.Hand);
            else LetGoOfTheWheel(car);
        }
    }

    /// <summary>
    /// <b>A car the hand has let go of</b>: back on its line where the hand left it on one, and otherwise off it —
    /// at rest, then onto the lane under it (CAR-9). A car nothing drives stands.
    /// </summary>
    void LetGoOfTheWheel(int car)
    {
        if (!Cars.Driven[car] || Cars.Broken[car]) Enter(car, CarAction.Stand);
        else Enter(car, Cars.Line[car].LaneCount > 0 ? CarAction.Follow : CarAction.Rejoin);
    }
}
