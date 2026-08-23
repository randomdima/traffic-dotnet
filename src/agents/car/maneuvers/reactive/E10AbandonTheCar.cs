namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`E-10` — abandon the car.</b> Nothing else is available: stop and hold, release every claim, and
/// put the driver out where the car stands. See <c>docs/e10-abandon-the-car.md</c>.
/// </summary>
/// <remarks>
/// <b>The last rung of the ladder, and the one that never refuses.</b> Every escalation ends here or
/// above it, which is what makes the ladder finite — a car that every other rung has refused still gets
/// an answer, and anything else is a stuck agent for the rest of the run. An abandoned car is no longer
/// an agent (CAR-1) and is town furniture until somebody else drives it away.
/// </remarks>
internal static class E10AbandonTheCar
{
    public const bool ThinksEveryTick = false;

    public const bool Watched = false;

    /// <summary><c>Sa</c>: none, and there may never be one. This is the exit the whole catalogue is bounded by.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        desk.GiveUpTheReservation(scene.Car);
        return ManeuverStart.Yes;
    }

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        limits = DriveLimits.Hold;
        return ManeuverOutcome.Finished(ManeuverReason.NothingLeft);
    }
}
