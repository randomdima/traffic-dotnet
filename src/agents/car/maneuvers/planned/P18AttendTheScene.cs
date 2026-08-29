namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-18` — attend the scene.</b> The line the car is driving passes a place it has been sent to:
/// come to rest beside it and stand there while the crew works. It is the last step of an ambulance's
/// run to a casualty, and the only entry in the catalogue whose end condition is something happening off
/// the car. See <c>docs/p18-attend-the-scene.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A place, like `P-6`, and not a bay.</b> A rescue stops in the road beside the body — there is no
/// template, no reversing and no pose to steer to, because the ambulance is not being put anywhere; it is
/// being stopped somewhere. What it asks of the car is one stop point, which the speed profile takes into
/// the same minimum as everything else, so nothing here can drive the car past a crossing, a wreck or a
/// queue that the standing rules were already holding it off.
/// </para>
/// <para>
/// <b>Its end condition is the place going away</b> (<see cref="DriveScene.ToTheSceneM"/>). The crew's
/// work is containment and belongs to the town, so what this entry knows about it is exactly what a
/// driver would: the errand is over when there is no longer anywhere to be. That is one fact written in
/// one place — the town clears it when the casualty is aboard and when the call is given up alike — so
/// the entry cannot disagree with the errand about whether it is finished.
/// </para>
/// <para>
/// <b>It owns the last few metres and not the approach</b> (MAN-4). `P-4` hands over once the place is
/// near enough to be stopped for, and until then the entry in charge is the one that knows how to get past
/// what is in the way — an ambulance held behind an obstruction forty metres short of its casualty is a
/// rescue that never arrives, and this entry has no swerve of its own to offer.
/// </para>
/// <para>
/// <b>And it is watched, although standing still is what it does.</b> The crew's work is seconds and the
/// fuse is half a minute, so the watchdog cannot fire on an ambulance doing its job — and what it does
/// catch is the one thing the call's own clock is too slow for: a car brought to rest at the place with
/// something in the way of the last car length of it.
/// </para>
/// </remarks>
internal static class P18AttendTheScene
{
    /// <summary>Braking to a place is a closed loop on an error, and a control loop is not a decision.</summary>
    public const bool ThinksEveryTick = true;

    /// <summary>The crew's work is seconds and the fuse is half a minute, so being watched costs a rescue nothing.</summary>
    public const bool Watched = true;

    /// <summary><c>Sa</c>: there is a place on the line ahead this car has been sent to.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject) =>
        float.IsFinite(scene.ToTheSceneM) ? ManeuverStart.Yes : ManeuverStart.No;

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (float.IsPositiveInfinity(scene.ToTheSceneM)) return ManeuverOutcome.Done(ManeuverReason.WayIsClear);

        // Past the place is still at the place: a body in the road is a couple of metres wide and the crew
        // reaches it from either side, so a car that overshot by half its own length holds where it is
        // rather than driving on to find somewhere better to have stopped.
        limits = scene.ToTheSceneM > 0f
            ? limits with { StopWithinM = scene.ToTheSceneM }
            : DriveLimits.Hold;

        return ManeuverOutcome.Running;
    }
}
