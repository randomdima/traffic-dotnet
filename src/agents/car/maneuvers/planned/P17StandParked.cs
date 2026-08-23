namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-17` — stand parked.</b> The car is in the bay: the handbrake holds it, the bay is marked
/// occupied, the leg is over and whoever was driving gets out. See <c>docs/p17-stand-parked.md</c>.
/// </summary>
/// <remarks>
/// <b>It is a manoeuvre and not the absence of one.</b> Standing still is what a car does most of the
/// time in a town, and a state nothing names is a state no instrument can count and no watchdog can
/// exempt — which is how a parked car ends up being escalated for not making progress. The squareness
/// this leg achieved is read at the moment this entry is taken up and never at the end of a run: a
/// parked car later shoved by traffic is not a parking result.
/// </remarks>
internal static class P17StandParked
{
    public const bool ThinksEveryTick = false;

    /// <summary>Standing still <em>is</em> the procedure, so there is nothing for a stuck fuse to find.</summary>
    public const bool Watched = false;

    /// <summary><c>Sa</c>: a bay to be in. The occupancy is taken here, which is what makes the bay no longer free.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        var bay = subject >= 0 ? subject : desk.ReservationOf(scene.Car);
        if (bay < 0) return ManeuverStart.No;

        desk.OccupyTheBay(scene.Car, bay);
        return ManeuverStart.Yes;
    }

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        limits = DriveLimits.Hold;
        return ManeuverOutcome.Finished(ManeuverReason.None);
    }
}
