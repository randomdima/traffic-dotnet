namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`E-1` — yield.</b> Another agent is entitled to be where this car wants to go, and waiting for it
/// is the correct answer. See <c>docs/e01-yield.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The wait is already what the speed profile is doing</b>, so what this entry adds is the name and a
/// bound. Yielding to another agent that blocks the path is legitimate idling and is the normal way cars
/// resolve conflicts (CAR-7) — but waiting for a junction somebody else is in is correct right up until
/// it has been correct for half a minute, and after that it is a jam rather than traffic.
/// </para>
/// <para>
/// <b>It keeps the name until the car moves off again</b>, not until the profile stops naming the term.
/// A yield discharged the moment another constraint won the minimum would be a yield nobody could see.
/// </para>
/// </remarks>
internal static class E01Yield
{
    /// <summary>Priority is not something to be approximately right about.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary><c>Sa</c>: something with priority is in the way — a red, a box somebody holds, or a body ahead that is itself moving.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject) =>
        scene.ObstructionHasPriority ? ManeuverStart.Yes : ManeuverStart.No;

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        // Bounded by construction (MAN-4). A yield that has resolved nothing by the blocked clock is not
        // a yield any more, and the ladder goes on from where it stands.
        if (scene.InManeuverS >= scene.Config.CarBlockedRoadS)
        {
            return ManeuverOutcome.Escalate(ManeuverReason.Bounded);
        }

        // Held until the car is moving again, and not until the obstruction stops being named: a car at
        // rest at a junction is still yielding whichever of its constraints happens to bind this tick.
        return scene.ObstructionHasPriority || scene.AtRest
            ? ManeuverOutcome.Running
            : ManeuverOutcome.Resume(ManeuverReason.PriorityGone);
    }
}
