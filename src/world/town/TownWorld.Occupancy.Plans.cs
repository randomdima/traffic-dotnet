using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The planned layer, for the drivers</b> (TER-4c.1, TER-4c.8): each car's claim, laid by its action at its turn — a
/// plan down its line, its manoeuvre's hold, or what a hand or a tow cannot stop short of — and what each came to.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>The hold each car's claim is in, or <see cref="LaneOccupancy.NoHold"/> (<see cref="DrivingGround.PlanHold"/>).</summary>
    readonly int[] _carHold;

    readonly Following _following;

    /// <summary>
    /// <b>This car's claim, laid by its action</b>: what a hand or a truck cannot stop short of, a manoeuvre's hold,
    /// or a plan down the route's line — and nothing at all for a car whose action drives nothing. Whatever it held for
    /// another action is let go of.
    /// </summary>
    void PlanTheDrive(int car, Span<LineWay> ways)
    {
        _ground.ClearTheClaim(car);
        _ground.LetGoOfTheHand(car);
        switch (Cars.Action[car])
        {
            case CarAction.Hand:
                _ground.LetGoOfThePlan(car);
                _ground.HoldWhatCannotBeStoppedShortOf(car, car, _world.View.CoversOfCar(car));
                return;

            // EVA-5: a car on a bar is the truck's to move, and what it cannot stop short of is held under the truck.
            case CarAction.Towed:
                _ground.LetGoOfThePlan(car);
                _ground.HoldWhatCannotBeStoppedShortOf(car, LaidAs(car), _world.View.CoversOfCar(car));
                return;
        }

        // <b>A car manoeuvring at a bay plans nothing</b> (GEN-4f): the ground it drives is its manoeuvre's, and all it
        // is held by is whatever refused that — or, before it has that ground, where it stands.
        if (IsManoeuvring(car))
        {
            _ground.LetGoOfThePlan(car);
            _bays.Hold(car);
            return;
        }

        if (IsUnderWay(car)) _following.Plan(car, ways);
        else _ground.LetGoOfThePlan(car);
    }

    /// <summary>The hold a car's claim is in, for an instrument asking what held it.</summary>
    public int DriveHold(int car) => _carHold[car];
}
