using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Maneuvers;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>
/// What a car is doing, in words, for whatever puts it on screen. It lives with the car because it is a
/// reading of the car's own state and not a fact about any panel: the interface and the debug layer both
/// ask for it, and a second copy of this switch would drift from the catalogue the day an entry is added.
/// </summary>
internal static class DrivingWords
{
    /// <summary>
    /// What a car is, and then what is limiting it — <b>in that order</b>, because a car that took no
    /// decision has no hold, and reading its <see cref="DrivingHold.None"/> off as a name called every
    /// driverless car in every bay in the town <c>driving</c>.
    /// </summary>
    public static string CarName(CarFleet cars, int car)
    {
        if (cars.Broken[car]) return "wrecked";
        if (!cars.Driven[car]) return "parked";
        if (cars.Line[car].ArcCount == 0) return "no line";

        // The manoeuvre first and the hold second, because they answer different questions: the entry
        // is what the car is doing and the hold is what is limiting it, and a car driving a template
        // is limited by things the route's own vocabulary cannot name.
        var doing = cars.Doing[car];
        return doing switch
        {
            Maneuver.LeaveTheBay =>
                cars.Limits[car].HoldStill
                    ? "P-2 waiting for a gap"
                    : "P-2 backing out of a bay",
            Maneuver.TurnAround => "P-11 turning around",
            Maneuver.ParkInTheBay => "P-14 parking",
            Maneuver.SquareUpInTheBay => "P-16 squaring up",
            Maneuver.StandParked => "P-17 parked",
            Maneuver.Yield => "E-1 yielding",
            Maneuver.EmergencyStop => "E-2 stopping hard",
            Maneuver.BackOff => "E-3 backing off",
            Maneuver.GoRound => "E-4 going round",
            Maneuver.GiveUpThePlace => "E-6 taking another place",
            Maneuver.Reroute => "E-7 rerouting",
            Maneuver.ReturnToLegalGround => "E-8 back to legal ground",
            Maneuver.SettleForHere => "E-9 settled for here",
            Maneuver.AbandonTheCar => "E-10 abandoned",
            Maneuver.None => HoldName(cars.Hold[car]),
            _ => $"{Maneuvers.Maneuvers.Code(doing)} {HoldName(cars.Hold[car])}",
        };
    }

    /// <summary>
    /// What is limiting a car, in the words its own controller uses. <b>A hold is not a manoeuvre</b>:
    /// the entry the car is in says what it is doing, and this says which of the things that limit a
    /// car is the one limiting it.
    /// </summary>
    public static string HoldName(DrivingHold hold) => hold switch
    {
        DrivingHold.Corner => "slowing for a corner",
        DrivingHold.LineEnd => "stopping at the end of its line",
        DrivingHold.Headway => "holding off something in the way",
        DrivingHold.Reserved => "queueing",
        DrivingHold.Waiting => "waiting for the junction",
        DrivingHold.Crossing => "yielding at a crossing",
        DrivingHold.LostLine => "off its line",
        DrivingHold.Procedure => "holding for its manoeuvre",
        _ => "driving",
    };
}
