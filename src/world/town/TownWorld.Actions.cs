using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>What each car is doing, one action at a time</b> (CAR-15b): the one place a car's action changes, and what
/// the action it leaves owned — a pass, a manoeuvre — dropped with it.
/// </summary>
/// <remarks>
/// <b>Nothing reads a car's doing off its line, its pass or its manoeuvre</b>: those are what its action holds, and
/// the action is what says which of them is in force. Two readings of one car were how a car waiting for its
/// manoeuvre was once answered as a plan down its line and drove it.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>A car handed over to another action</b>, and what the one it leaves owned let go: a pass is its overtake's,
    /// and a manoeuvre its bay's — kept from a manoeuvre into a bay to the one straight back out of it, where a leg
    /// turns there (GEN-4l).
    /// </summary>
    void Enter(int car, CarAction action)
    {
        var was = Cars.Action[car];
        if (was == action) return;

        if (was == CarAction.Overtake) Cars.Pass[car] = Overtake.None;
        if (IsAtABay(was) && !IsAtABay(action)) _manoeuvres.Clear(car);

        // <b>A grant is its action's claim</b>, and goes with it: the one claim two actions share is a plan down the
        // route's line, handed on between the actions that drive it. Anything else stands until its own is laid.
        if (!(PlansDownTheRoute(was) && PlansDownTheRoute(action) && _carHold[car] != LaneOccupancy.NoHold))
        {
            Cars.AuthorityM[car] = 0f;
        }

        Cars.Action[car] = action;
    }

    static bool IsAtABay(CarAction action) => action is CarAction.Park or CarAction.Unpark;

    /// <summary>Whether an action plans down the route's own line — a car getting into a bay does, up to where it waits.</summary>
    static bool PlansDownTheRoute(CarAction action) =>
        action is CarAction.Follow or CarAction.Overtake or CarAction.BackUp or CarAction.Park;

    /// <summary>
    /// <b>Whether a car's action drives the route's own line</b> — and so plans down it (TER-4c.1): following it,
    /// getting past something on it, backing down it for room, or coming up it to where it waits for its bay.
    /// </summary>
    bool DrivesTheRoute(int car) =>
        Cars.Action[car] switch
        {
            CarAction.Follow or CarAction.Overtake or CarAction.BackUp => true,
            CarAction.Park => !_manoeuvres.IsBegun(car),
            _ => false,
        };

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
