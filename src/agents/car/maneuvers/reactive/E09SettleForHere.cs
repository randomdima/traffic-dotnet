namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`E-9` — settle for here.</b> Stop somewhere that is not itself an obstruction, hold with the
/// handbrake, release every claim — and hand the rest of the trip to the driver's own feet. See
/// <c>docs/e09-settle-for-here.md</c>.
/// </summary>
/// <remarks>
/// <b>An agent uses the actions it has to get as close to its goal as it can; it does not drop the
/// goal.</b> That is the whole difference between this entry and `E-10`: the car is parked badly rather
/// than abandoned, and the person walks. <b>Terminal means it may not hand a leg back to something that
/// has already failed</b> — past the first recovery, nowhere legal to stop is `E-10` rather than a
/// second settle.
/// </remarks>
internal static class E09SettleForHere
{
    public const bool ThinksEveryTick = false;

    /// <summary>The leg ends on the tick it is taken up, so there is nothing for a fuse to watch.</summary>
    public const bool Watched = false;

    /// <summary>
    /// <c>Sa</c>: where the car stands is not itself an obstruction — on drivable ground, not across a
    /// lane, not in a box — and this leg has not already settled once.
    /// </summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        if (scene.RecoveriesUsed > 1) return ManeuverStart.No;
        if (!scene.OnDrivableGround || scene.AcrossALane) return ManeuverStart.No;

        desk.GiveUpTheBooking(scene.Car);
        return ManeuverStart.Yes;
    }

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        limits = DriveLimits.Hold;
        return ManeuverOutcome.Finished(ManeuverReason.NothingLeft);
    }
}
