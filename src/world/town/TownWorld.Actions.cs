using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Control;

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

        Cars.Action[car] = action;
    }

    static bool IsAtABay(CarAction action) => action is CarAction.Park or CarAction.Unpark;

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
    /// <b>A hand taken to a car's wheel, or taken off it</b> (CTL-5, CTL-5d), before anything is laid this tick:
    /// under a hand the car's own action is over, and let go it takes the road again from wherever the hand left it.
    /// </summary>
    void TakeUpTheHands()
    {
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
