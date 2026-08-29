namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-16` — square up in the bay.</b> MAN-6's retry from a different pose: out of the bay on the
/// template that turns the car, then in again on the one that turns it back. See
/// <c>docs/p16-square-up-in-the-bay.md</c>.
/// </summary>
/// <remarks>
/// <b>Two poses on one line cannot rotate a car.</b> Any "back up and try again" that leaves the car on
/// the axis it failed on reproduces the failure to the degree, because a straight has no curvature and a
/// car driven along one arrives at exactly the angle it left at. That is the only reason this entry
/// exists: everywhere else the retry needs no manoeuvre of its own, since `P-4` draws its line from the
/// pose the car actually ended up in.
/// </remarks>
internal static class P16SquareUpInTheBay
{
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary>
    /// <c>Sa</c>: a bay this car is in or holds, that can be left, and a way out of it — which is the
    /// same template `P-2` drives, and is laid here for the same reason.
    /// </summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        var bay = desk.BayInHand(scene.Car);
        return bay >= 0 && desk.LayTheExitLine(scene.Car, bay) ? ManeuverStart.Yes : ManeuverStart.No;
    }

    /// <summary>
    /// Out, and then straight back in: the second half is `P-14` on the same bay, which lays its own
    /// template from the pose this one ends in. <b>One attempt</b> — a second square-up from a pose the
    /// first one chose is the failure the rule above forbids, and the ladder is what carries on from
    /// there.
    /// </summary>
    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (!scene.OnATemplate) return ManeuverOutcome.Fail(Maneuver.RunTheLine, ManeuverReason.LostTheLine);

        return scene.LineIsSpent
            ? ManeuverOutcome.To(Maneuver.ParkInTheBay, ManeuverReason.LineSpent)
            : ManeuverOutcome.Running;
    }
}
