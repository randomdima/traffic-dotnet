namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-14` — park in the bay.</b> From the staging place the route stopped at, drive the forward-in
/// template into the bay this leg holds. See <c>docs/p14-park-in-the-bay.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The line is the town's, where the town laid one.</b> The way into a bay is settled with the ground
/// it was painted on and threaded onto the end of the leg's own line, so this entry lays nothing and
/// drives on down the chain it was handed — which is what puts the last dozen metres of a leg in the
/// road's book like every metre before them.
/// </para>
/// <para>
/// <b>And a template from the pose the car actually stands in, where it has been shuffled off that
/// line</b>: after `P-16`, after a recovery, after anything that left the car somewhere the route did not
/// send it. A bay that cannot be driven into from there is a retarget rather than a squeeze — the
/// geometry either admits the shape or it does not, and no amount of shuffling on the same axis changes
/// the answer.
/// </para>
/// <para>
/// <b>A bigger turning radius does not park a car better; run-in does</b>, and every metre of radius is
/// a metre off the run-in. The line ends on a straight for the same reason: aiming on down the final
/// tangent converges the car onto it, and at manoeuvring pace with the rack still unwinding that takes
/// ground.
/// </para>
/// </remarks>
internal static class P14ParkInTheBay
{
    /// <summary>Steering to a pose inside a four-metre bay: the one place in the town where a tenth of a second of lag is metres.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary>
    /// <c>Sa</c>: the leg's line already finishes at the bay, or — where it does not — a bay this leg
    /// holds and a template into it from the pose the car is in.
    /// </summary>
    /// <remarks>
    /// <b>A bay the car reverses into is the second of those</b> (GEN-4j): a route is driven forwards, so
    /// the leg's line stops where the way in begins and the car comes to rest there. The shape is the
    /// town's own all the same — the same standing, off the same lane — laid again from the pose the car
    /// actually stopped in and driven in the gear it is drawn for.
    /// </remarks>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        if (scene.OnTheFinalApproach && !desk.ReversesIntoTheBay(scene.Car)) return ManeuverStart.Yes;

        var bay = subject >= 0 ? subject : desk.BayInHand(scene.Car);
        return bay >= 0 && desk.LayTheEntryLine(scene.Car, bay) ? ManeuverStart.Yes : ManeuverStart.No;
    }

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (!scene.OnATemplate && !scene.OnTheFinalApproach)
        {
            return ManeuverOutcome.Fail(Maneuver.RunTheLine, ManeuverReason.LostTheLine);
        }

        return scene.LineIsSpent
            ? ManeuverOutcome.Done(ManeuverReason.LineSpent)
            : ManeuverOutcome.Running;
    }
}
