namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`E-7` — reroute.</b> The road, not the destination, is the problem: price the stretch this car is
/// blocked entering up so every other driver benefits, drop the route in hand, and let the search find
/// another way to the same place. See <c>docs/e07-reroute.md</c>.
/// </summary>
/// <remarks>
/// <b>Expensive, never impassable.</b> In a town this small the only road to a place may be the marked
/// one, so a blocked stretch is priced rather than banned (SIM-6) — and <b>the mark expires and is never
/// swept</b>: nothing unmarks a road by inspection, so a stretch that is still blocked is marked again
/// by whoever finds it so. The count of reroutes a leg may spend is what stops a car pricing the whole
/// town up one street at a time.
/// </remarks>
internal static class E07Reroute
{
    public const bool ThinksEveryTick = false;

    public const bool Watched = true;

    /// <summary><c>Sa</c>: on a route, with a reroute left on this leg and a stretch ahead worth marking.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject) =>
        scene.OnARoute && scene.ReroutesLeft > 0
            ? ManeuverStart.Ask(DriveOrder.MarkTheWayBlocked)
            : ManeuverStart.No;

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits) =>
        ManeuverOutcome.To(Maneuver.RunTheLine, ManeuverReason.Bounded);
}
