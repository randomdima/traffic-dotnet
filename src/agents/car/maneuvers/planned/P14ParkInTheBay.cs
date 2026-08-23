namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-14` — park in the bay.</b> From the staging place the route stopped at, drive the forward-in
/// template into the bay this leg holds. See <c>docs/p14-park-in-the-bay.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The template is laid from where the car actually stands</b>, never from where the route said it
/// would be — and a bay that cannot be driven into from here is a retarget rather than a squeeze. That
/// refusal is the whole of the manoeuvre's <c>Sa</c>: the geometry either admits the shape or it does
/// not, and no amount of shuffling on the same axis changes the answer.
/// </para>
/// <para>
/// <b>A bigger turning radius does not park a car better; run-in does</b>, and every metre of radius is
/// a metre off the run-in. The template ends on a straight for the same reason: aiming on down the final
/// tangent converges the car onto the line, and at manoeuvring pace with the rack still unwinding that
/// takes ground.
/// </para>
/// </remarks>
internal static class P14ParkInTheBay
{
    /// <summary>Steering to a pose inside a four-metre bay: the one place in the town where a tenth of a second of lag is metres.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary><c>Sa</c>: a bay this leg holds, and a template into it from the pose the car is in.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        var bay = subject >= 0 ? subject : desk.ReservationOf(scene.Car);
        return bay >= 0 && desk.LayTheEntryLine(scene.Car, bay) ? ManeuverStart.Yes : ManeuverStart.No;
    }

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (!scene.OnATemplate) return ManeuverOutcome.Fail(Maneuver.RunTheLine, ManeuverReason.LostTheLine);

        return scene.LineIsSpent
            ? ManeuverOutcome.Done(ManeuverReason.LineSpent)
            : ManeuverOutcome.Running;
    }
}
