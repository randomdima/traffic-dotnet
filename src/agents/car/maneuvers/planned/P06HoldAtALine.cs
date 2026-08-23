namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-6` — hold at a line.</b> There is a place ahead the car may not pass — a red, a painted bar, a
/// junction it was refused, or the end of the line it was given — and the manoeuvre is to come to rest
/// short of it, wait there, and pull away again when it is gone. See <c>docs/p06-hold-at-a-line.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A place, not a thing</b>: what `P-4` waits for may move off on its own, and what this waits for
/// will not. The distinction is what makes them two entries rather than one, and it is why the clock a
/// car spends here is the signal's rather than the watchdog's — a car queueing through two phases at a
/// busy junction is doing exactly what the light asked.
/// </para>
/// <para>
/// <b>It must not be scheduled</b>, and that was measured rather than reasoned. It looks perfectly safe
/// to run on the decision clock, because the car is stopping and the reservation pass holds it at the
/// bar every tick regardless — and scheduling it still put the front of the queue nearly twice as far
/// back from the paint.
/// </para>
/// </remarks>
internal static class P06HoldAtALine
{
    /// <summary>Braking to a line is a closed loop on an error, and a control loop is not a decision.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary><c>Sa</c>: a stop point somewhere ahead on the line, or the end of the line itself.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject) =>
        HasSomethingToHoldAt(scene) ? ManeuverStart.Yes : ManeuverStart.No;

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        // The end of the line on the leg's own last lane is not a line to wait at — it is the place the
        // bay's template is staged from, and the plan's next step is what happens there.
        if (scene.OnTheFinalApproach && scene.ToTheEndM <= scene.Config.Car.LengthM * 0.5f && scene.AtRest)
        {
            return ManeuverOutcome.Done(ManeuverReason.RouteRanOut);
        }

        // <b>The car gives itself back by moving off, not by the line disappearing.</b> A car at rest at a
        // junction is bound by the box one tick and by the queue in front the next, and neither means the
        // thing it stopped for has gone — an exit on the bare test hands `P-4` a stationary car and takes
        // it straight back, a hundred times in one spot. This entry imposes nothing, so the profile pulls
        // the car away the moment it may and the exit follows the body.
        return HasSomethingToHoldAt(scene) || scene.AtRest
            ? ManeuverOutcome.Running
            : ManeuverOutcome.To(Maneuver.RunTheLine, ManeuverReason.WayIsClear);
    }

    /// <summary>
    /// Whether anything is still asking this car to stop at a place. <b>Asked of the stop point and the
    /// end of the line, never of which term happened to bind the speed</b>: a car creeping up to a bar
    /// is bound by the bar one tick and by the car in front the next, and neither means the bar has gone.
    /// </summary>
    static bool HasSomethingToHoldAt(in DriveScene scene) =>
        !float.IsPositiveInfinity(scene.Context.StopAtM)
        || !float.IsPositiveInfinity(scene.LightAheadM)
        || scene.ToTheEndM <= scene.Config.Car.LengthM;
}
