using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The planned layer, for the drivers</b> (TER-4c.1, TER-4c.8): each car's claim for this rebuild, laid by its
/// action — a plan down its line, its manoeuvre's hold, or what a hand or a tow cannot stop short of — and what each
/// came to.
/// </summary>
internal sealed partial class TownWorld
{
    /// <summary>The hold each car laid this rebuild, or <see cref="LaneOccupancy.NoHold"/> (<see cref="DrivingGround.PlanHold"/>).</summary>
    readonly int[] _carHold;

    readonly Following _following;

    /// <summary>
    /// <b>This car's claim, laid by its action</b>: what a hand or a truck cannot stop short of, a manoeuvre's hold,
    /// or a plan down the route's line — and nothing at all for a car whose action drives nothing.
    /// </summary>
    void PlanTheDrive(int car, Span<LineWay> ways)
    {
        var keptOffM = _ground.ClearTheClaim(car);
        switch (Cars.Action[car])
        {
            case CarAction.Hand:
                _ground.HoldWhatCannotBeStoppedShortOf(car, car);
                return;

            // EVA-5: a car on a bar is the truck's to move, and what it cannot stop short of is held under the truck.
            case CarAction.Towed:
                _ground.HoldWhatCannotBeStoppedShortOf(car, LaidAs(car));
                return;
        }

        // <b>A car manoeuvring at a bay plans nothing</b> (GEN-4f): the ground it drives is its manoeuvre's, laid as
        // a body, and all it is held by is a body standing in it — or, before it has that ground, where it stands.
        if (IsManoeuvring(car))
        {
            _bays.Hold(car);
            return;
        }

        if (IsUnderWay(car)) _following.Plan(car, keptOffM, ways);
    }

    /// <summary>The hold a car laid this rebuild, for an instrument asking what held it.</summary>
    public int DriveHold(int car) => _carHold[car];
}
