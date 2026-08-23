namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`E-3` — back off.</b> The cheapest change of state there is: make room along the car's own axis,
/// and re-decide from the new distance. See <c>docs/e03-back-off.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Room made is room kept.</b> What the car does at the end of the straight is run the decision that
/// failed again from a different place, which is the plan re-derived rather than the same choice made
/// twice — and it is why the ladder offers this rung twice with something else between: a jam that has
/// had another watchdog's worth of time to change is a different jam.
/// </para>
/// <para>
/// <b>There must be something to back away from.</b> Four states count — something in the way, a
/// boundary the car may not cross, a template it can no longer follow, and a line it has lost. With an
/// open road in front and none of those the entry refuses; the fault that guard fixes was cars reversing
/// away from empty intersections while yielding perfectly correctly.
/// </para>
/// </remarks>
internal static class E03BackOff
{
    /// <summary>Steering a straight at manoeuvring pace with a metre of room is a control loop, not a decision.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary>
    /// <c>Sa</c>: an attempt left, something to back away from, and ground behind the car to back into —
    /// <b>walked rather than assumed</b>, in the gear it will be driven in.
    /// </summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        if (scene.BackOffsLeft <= 0 || !scene.SomethingToBackAwayFrom) return ManeuverStart.No;

        // The other way from whichever way the jammed manoeuvre was going: a reversing template that
        // jams is got out of forwards.
        var backwards = !scene.Reverse;
        var roomM = desk.RoomAlongTheAxisM(scene.Car, backwards);
        if (roomM <= 0f || !desk.LayTheStraight(scene.Car, roomM, backwards)) return ManeuverStart.No;

        desk.SpendABackOff(scene.Car);
        return ManeuverStart.Yes;
    }

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (!scene.OnATemplate) return ManeuverOutcome.Fail(Maneuver.RunTheLine, ManeuverReason.LostTheLine);

        return scene.LineIsSpent
            ? ManeuverOutcome.To(Maneuver.RunTheLine, ManeuverReason.LineSpent)
            : ManeuverOutcome.Running;
    }
}
