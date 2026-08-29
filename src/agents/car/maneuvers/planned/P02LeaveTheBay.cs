namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-2` — leave the bay.</b> The first step of nearly every leg: drive the town's own way out of the
/// space, backwards, onto the lane it lands on.
/// See <c>docs/p02-leave-the-bay.md</c> for the scenario, the states either side of it and the bounds.
/// </summary>
/// <remarks>
/// <b>There is no wait here, and its absence is the entry</b> (GEN-4f). The way out is a way of the road's
/// book, so what holds the car in the bay is the road it is granted — cut at the first metre of it the traffic
/// on the street is driven over, by the same table walk that cuts a car at a junction — and what stops two
/// neighbouring bays taking the same gap is that the first of them takes the ground before it moves onto
/// it. A gap looked at, a patience spent and a beat to break the row apart were all one mechanism standing
/// in for the town's own.
/// </remarks>
internal static class P02LeaveTheBay
{
    /// <summary>Steering to a pose on a line a few metres long: a control loop run at a sixth of the rate converges at a sixth of the rate.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary>
    /// <c>Sa</c>: standing in a bay this car holds, with a way out of it the town laid. The way itself
    /// where the car is standing on the start of it, and the recovery from wherever else it has ended up.
    /// </summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        var bay = subject >= 0 ? subject : desk.BayOf(scene.Car);
        if (bay < 0) return ManeuverStart.No;

        return desk.TakeTheWayOutOfTheBay(scene.Car, bay) || desk.LayTheExitLine(scene.Car, bay)
            ? ManeuverStart.Yes
            : ManeuverStart.No;
    }

    /// <summary>
    /// Drive it out. <c>Sb</c> is a car on the lane with the bay given back, and the plan's next step takes
    /// it from there.
    /// </summary>
    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (!scene.OnATemplate) return ManeuverOutcome.Fail(Maneuver.RunTheLine, ManeuverReason.LostTheLine);
        if (!scene.LineIsSpent) return ManeuverOutcome.Running;

        desk.VacateTheBay(scene.Car);
        return ManeuverOutcome.Done(ManeuverReason.LineSpent);
    }
}
