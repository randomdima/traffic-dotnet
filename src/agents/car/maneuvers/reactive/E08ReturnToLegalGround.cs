namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`E-8` — return to legal ground.</b> The car is somewhere it should not be, so it drives the one
/// straight that gets the whole body back onto ground a car may drive on (CAR-9). See
/// <c>docs/e08-return-to-legal-ground.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stop first.</b> A car correcting a violation while still moving acquires a second one, so the
/// recovery begins from rest and drives a straight it has already walked.
/// </para>
/// <para>
/// <b>Lane legality and the no-idling rule are suspended for this path; no-collision and red lights
/// still bind</b> (S-6), which is exactly what keeps it a recovery rather than a licence.
/// </para>
/// <para>
/// <b>The straight is along the car's own axis</b> and not toward the nearest legal point. The path this
/// manoeuvre can issue is a single straight, so "the nearest lane point" — which is generally off to one
/// side — would have the car drive the right distance in the wrong direction.
/// </para>
/// </remarks>
internal static class E08ReturnToLegalGround
{
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary><c>Sa</c>: the body is not on drivable ground, and a pose along its own axis exists where all of it would be.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        if (scene.OnDrivableGround) return ManeuverStart.No;
        if (!desk.StraightToLegalGround(scene.Car, out var reachM, out var backwards)) return ManeuverStart.No;
        if (!desk.LayTheStraight(scene.Car, reachM, backwards)) return ManeuverStart.No;

        desk.SpendARecovery(scene.Car);
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
