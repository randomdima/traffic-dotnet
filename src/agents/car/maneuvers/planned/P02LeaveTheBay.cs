namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-2` — leave the bay.</b> The first step of nearly every leg: back out of the space onto the lane
/// it is entered from, including the wait for a gap in the traffic on it.
/// See <c>docs/p02-leave-the-bay.md</c> for the scenario, the states either side of it and the bounds.
/// </summary>
/// <remarks>
/// <b>The bay is the one piece of road this car is entitled to occupy</b>, so the wait happens in it and
/// never in the lane: before the body has crossed the mouth the car holds where it is, and past that
/// point it is committed and finishes the template. Both halves are the same line, and which half the
/// car is in is read off its progress along it.
/// </remarks>
internal static class P02LeaveTheBay
{
    /// <summary>Steering to a pose on a two-metre template: a control loop run at a sixth of the rate converges at a sixth of the rate.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary>
    /// <c>Sa</c>: standing in a bay this car holds, with a way out of it the geometry admits. The
    /// template is laid here and the wait begins with the beat that keeps two neighbouring bays from
    /// taking the same gap.
    /// </summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        var bay = subject >= 0 ? subject : desk.BayOf(scene.Car);
        if (bay < 0 || !desk.LayTheExitLine(scene.Car, bay)) return ManeuverStart.No;

        desk.BeginTheWait(scene.Car);
        return ManeuverStart.Yes;
    }

    /// <summary>
    /// Wait in the mouth until the lane answers, then drive the template out. <c>Sb</c> is a car on the
    /// lane with the bay given back, and the plan's next step takes it from there.
    /// </summary>
    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (!scene.OnATemplate) return ManeuverOutcome.Fail(Maneuver.RunTheLine, ManeuverReason.LostTheLine);

        // Still inside the bay: the car is entitled to be exactly where it is, so it waits there for the
        // lane rather than in it. Past the give-way patience the gap is taken anyway — a car waiting out
        // a jam is one more car in it, and that bound is inside the answer this asks for.
        if (scene.ProgressM < scene.Config.Car.LengthM * 0.5f && !scene.GapIsClear)
        {
            desk.SpendTheWait(scene.Car, sinceS);
            limits = DriveLimits.Hold;
            return ManeuverOutcome.Running;
        }

        if (!scene.LineIsSpent) return ManeuverOutcome.Running;

        desk.VacateTheBay(scene.Car);
        return ManeuverOutcome.Done(ManeuverReason.LineSpent);
    }
}
