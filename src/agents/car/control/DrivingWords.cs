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
    /// <b>What a car is, and then what is limiting it</b> — in that order, because a car that is not
    /// being driven has no hold, and reading its <see cref="DrivingHold.None"/> off as a name called
    /// every driverless car in every bay in the town <c>driving</c>.
    /// </summary>
    /// <remarks>
    /// <b>The hold is the whole of what a car is doing</b> (CAR-15). There is no name beside it to drift
    /// from it: what a driver is at is a line and a term of the speed profile, and which line it is on is
    /// the one thing this adds — a bay's own way is the last dozen metres of a leg either way round.
    /// </remarks>
    public static string CarName(CarFleet cars, int car)
    {
        if (cars.Broken[car]) return "wrecked";
        if (!cars.Driven[car]) return "parked";
        if (cars.Line[car].ArcCount == 0) return "no line";

        if (cars.LineWayOf(car) != CarFleet.NoWay)
        {
            return cars.LineIsReverse[car] ? "backing at a bay" : "driving at a bay";
        }

        // The lane beside is a line of its own to a watcher, whatever bound the speed along it (CAR-46).
        if (cars.Pass[car].Begun) return "overtaking";

        // Neither past what stands in front of it nor back for the room to, which the traffic behind reads as a body
        // going nowhere (CAR-50).
        if (cars.Context[car].Blocked) return "blocked";

        return HoldName(cars.Hold[car]);
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
