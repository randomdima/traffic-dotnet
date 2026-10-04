using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>
/// What a car is doing, in words, for whatever puts it on screen. It lives with the car because it is a
/// reading of the car's own state and not a fact about any panel: the interface and the debug layer both
/// ask for it, and a second copy of this switch would drift from the controller the day a term is added.
/// </summary>
internal static class DrivingWords
{
    /// <summary>
    /// <b>What a car is doing, and — following its route — what is limiting it</b>: the action first (CAR-15b), since
    /// a car that is not being driven has no hold, and reading its <see cref="DrivingHold.None"/> off as a name called
    /// every driverless car in every bay in the town <c>driving</c>.
    /// </summary>
    public static string CarName(CarFleet cars, int car)
    {
        if (cars.Broken[car]) return "wrecked";

        var reverse = cars.LineIsReverse[car];
        return cars.Action[car] switch
        {
            CarAction.Towed => "on a bar",
            CarAction.Hand => "under a hand",
            CarAction.Stand => cars.Driven[car] ? "no line" : "parked",
            CarAction.Rejoin => HoldName(DrivingHold.LostLine),
            CarAction.Overtake => cars.Pass[car].Begun ? "overtaking" : "waiting to overtake",
            CarAction.Switch => cars.Pass[car].Begun ? "changing lanes" : "looking to change lanes",

            // Neither past what stands in front of it nor back for the room to, which the traffic behind reads as a
            // body going nowhere (CAR-50).
            CarAction.BackUp => cars.Context[car].Blocked ? "blocked" : HoldName(DrivingHold.BackingUp),
            CarAction.Park when cars.Line[car].LaneCount > 0 => "coming up to its bay",
            CarAction.Park => reverse ? "backing into a bay" : "driving into a bay",
            CarAction.Unpark => reverse ? "backing out of a bay" : "driving out of a bay",
            _ => HoldName(cars.Hold[car]),
        };
    }

    /// <summary>
    /// What cut a car's grant, in words. <b>It names what the thing is and never what the car will do about
    /// it</b>: what the car does is stop within the road it was given, whatever ended it.
    /// </summary>
    public static string AheadName(HeadwayKind ahead) => ahead switch
    {
        HeadwayKind.Queue => "a queue",
        HeadwayKind.Obstruction => "an obstruction",
        HeadwayKind.Claimed => "road somebody means to use",
        HeadwayKind.Light => "a light",
        HeadwayKind.Walker => "a walker",
        HeadwayKind.Passing => "somebody overtaking",
        _ => "nothing",
    };

    public static string HoldName(DrivingHold hold) => hold switch
    {
        DrivingHold.Corner => "slowing for a corner",
        DrivingHold.Wheel => "turning harder than its line",
        DrivingHold.LineEnd => "stopping at the end of its line",
        DrivingHold.Claimed => "queueing",
        DrivingHold.Waiting => "waiting for the junction",
        DrivingHold.Reach => "keeping within its plan",
        DrivingHold.LostLine => "off its line",
        DrivingHold.BackingUp => "backing up to pass",
        _ => "driving",
    };
}
