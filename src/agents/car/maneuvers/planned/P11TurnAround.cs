namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-11` — turn around inside a junction.</b> The route reverses direction of travel, and a junction
/// is the only place on the network where that is allowed. See <c>docs/p11-turn-around.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two arcs, not one.</b> The car counter-swings <em>away</em> from the lane it is heading for and
/// then sweeps back through it. A plain half-circle needs a junction as wide as twice the turning
/// radius; the counter-swing lands the same lane separation on minimum-radius arcs, and pays for it by
/// reaching further along the arm — so <b>the reach is the constraint to check, not the lateral one</b>,
/// and it is checked against the ground the shape is actually drawn over.
/// </para>
/// <para>
/// <b>A junction that cannot hold the shape is not a road problem, so it is not a reroute</b>: the route
/// asked for a turn-around only because the goal is back the way the car came, and every replan comes
/// back with the same answer. What changes the problem is the destination, and where the leg has already
/// spent that, the honest end is to stop somewhere legal and walk the rest.
/// </para>
/// </remarks>
internal static class P11TurnAround
{
    /// <summary>The one junction movement that sweeps the whole box and crosses the other stream throughout.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary>
    /// <c>Sa</c>: the route's next lane is the reverse of the one under the car, the box is this car's,
    /// and the counter-swing fits the ground it would be driven over.
    /// </summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        if (!scene.RouteReversesHere || !scene.BoxIsOurs) return ManeuverStart.No;

        return desk.LayTheTurnAround(scene.Car, scene.LaneOn) ? ManeuverStart.Yes : ManeuverStart.No;
    }

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (!scene.OnATemplate) return ManeuverOutcome.Fail(Maneuver.RunTheLine, ManeuverReason.LostTheLine);

        return scene.LineIsSpent
            ? ManeuverOutcome.To(Maneuver.RunTheLine, ManeuverReason.LineSpent)
            : ManeuverOutcome.Running;
    }
}
