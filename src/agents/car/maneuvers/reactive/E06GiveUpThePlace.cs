namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`E-6` — give up the target place.</b> The destination, not the manoeuvre, is the problem: the bay
/// this leg holds cannot be reached or cannot be driven into, so it goes back on the market and another
/// is claimed near where the car has actually got to. See <c>docs/e06-give-up-the-place.md</c>.
/// </summary>
/// <remarks>
/// <b>Release before taking</b> — a place held by a car that has gone elsewhere is a place removed from
/// the town — and claim a replacement <b>only once a route to its own approach lane exists</b>. A bay
/// claimed without that is the same jam again with a different postcode. A car that can claim none keeps
/// driving rather than standing in a lane, and that is the refusal this entry returns.
/// </remarks>
internal static class E06GiveUpThePlace
{
    public const bool ThinksEveryTick = false;

    public const bool Watched = true;

    /// <summary><c>Sa</c>: there is a place to give up, and the town can find another to take.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject) =>
        scene.BayBooked >= 0
            ? ManeuverStart.Ask(DriveOrder.RetargetTheBay, scene.BayBooked)
            : ManeuverStart.No;

    /// <summary>The place is changed and the leg goes on. The whole manoeuvre is the claim, so it is over as soon as it is taken up.</summary>
    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits) =>
        ManeuverOutcome.To(Maneuver.RunTheLine, ManeuverReason.NoPlace);
}
