using TrafficSimulation.Agents.Car.Control;

namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-4` — run the line.</b> The default, and the state every other manoeuvre comes back to: hold the
/// route's line at whatever speed the standing rules allow, and hand over to whichever entry the road
/// produces next. See <c>docs/p04-run-the-line.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>It lays no geometry of its own.</b> The line it holds is the route's, drawn over the lanes the plan
/// says to take — so the one thing this entry does on the way in is make sure there is one, which after
/// anything that drove a template of its own there is not.
/// </para>
/// <para>
/// <b>Its exits are named off the term that bound the speed profile</b>, because a car's speed is the
/// minimum of everything that limits it and the term that won is the least ambiguous reading there is of
/// what the car is actually doing. Each of the entries it names then exits on the <em>fact</em> it is
/// about rather than on that term — the difference is what stops a queue at rest, which sits exactly on
/// the threshold between two of them, being handed back and forth several hundred times in one spot.
/// </para>
/// <para>
/// <b>Queueing is not one of them.</b> A car held by the ground it was granted is running its line at the
/// speed that ground affords, which is this entry and needs no other: the grant is a term of the profile
/// like the corners and the stop points, and a car behind another is doing exactly what a car on an open
/// road is doing with a shorter road to do it on. The one thing left for another entry to decide is that
/// what is in front is not a queue at all, and the way past it is round it.
/// </para>
/// <para>
/// <b>And neither is a crossing.</b> The pace over paint (CAR-7b) and the stop short of a body on it
/// (TER-4c.1, TER-5e) are the standing rules', folded into that same minimum, so a car slowing for a zebra
/// is running its line on the road the zebra left it. An entry named off the term that won there would
/// impose nothing the profile was not already imposing.
/// </para>
/// </remarks>
internal static class P04RunTheLine
{
    /// <summary>Deliberation against distances of tens of metres, which is what the decision clock is for.</summary>
    public const bool ThinksEveryTick = false;

    public const bool Watched = true;

    /// <summary>
    /// <c>Sa</c>: on ground a car may drive on, with the route's line under it. A car holding a template
    /// asks the town for the lane it is standing on first — the pose a manoeuvre ends in is not the pose
    /// the plan expected, so the line is rebuilt from the actual one (MAN-3).
    /// </summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject) =>
        scene.OnARoute ? ManeuverStart.Yes : ManeuverStart.Ask(DriveOrder.TakeTheLaneUnderIt);

    /// <summary>
    /// The exits, in the order they are asked. Nothing here is a state machine running beside the
    /// driving; it is the driving, named.
    /// </summary>
    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        // A car that has lost its line is not running one. Taking the lane under it is the cheap half of
        // the recovery and the standing rules do it; everything past that is the ladder's, which reaches
        // this car because a stopped car off its line spends the blocked clock.
        if (scene.Hold == DrivingHold.LostLine) return ManeuverOutcome.Running;

        // A place on the line this car was sent to — a casualty in the road (`P-18`) — <b>once it is near
        // enough to be stopped for</b>, exactly as the box below is taken once it is within reserve
        // distance. Handed over any earlier, the last hundred metres of a rescue would be driven by an
        // entry that has no way past an obstruction: getting past what is in the way is this entry's
        // (`E-4`), and it has to still be the entry in charge while there is road left to do it in.
        if (scene.SceneIsNearEnoughToStopFor)
        {
            return ManeuverOutcome.To(Maneuver.AttendTheScene, ManeuverReason.RouteRanOut);
        }

        // The line leaves the road for the bay's own way, and past that point the leg is not driving any
        // more: it is steering to a pose inside a four-metre box, which is the plan's next step and is
        // asked on every tick for exactly that reason. The line itself runs on unbroken — what changes at
        // the hand-over is who is holding it and how often they are asked.
        //
        // <b>Or it stops where that way begins</b>, which is a bay the car reverses into (GEN-4j): a route
        // is driven forwards, so the line ends at the mouth of such a way and the hand-over is the car
        // having come to rest at it rather than having reached a place along a line that runs on.
        if (scene.OnTheFinalApproach && (scene.ToTheBayM <= 0f || scene.LineIsSpent))
        {
            return ManeuverOutcome.Done(ManeuverReason.RouteRanOut);
        }

        // The line ends where the leg has to come back the other way and no bay was there to turn in
        // (GEN-4l): what is past it is the car working itself round on the spot, and nothing else the road
        // offers gets this leg any further.
        if (scene.TurnsBackHere && scene.StoppedAtTheEnd)
        {
            return ManeuverOutcome.To(Maneuver.ShuntRound, ManeuverReason.RouteRanOut);
        }

        // A stop point is a place and `P-6` owns it — a red, a bar, a box that could not be claimed, or
        // the end of the route. A body in front is not one of those: the ground it was granted already
        // holds this car off it, so a car behind a queue keeps running this entry.
        if (scene.Hold is DrivingHold.Waiting or DrivingHold.LineEnd)
        {
            return ManeuverOutcome.To(Maneuver.HoldAtALine, ManeuverReason.None);
        }

        // <b>What is in front is asked what it is, not how long it has stood.</b> An obstruction is a
        // wreck, a car nobody is in, a body off its own line, somebody in the carriageway — the lane
        // index's answer — and §1.4 row 5 says the way past one is `E-4`. A car queueing is none of those,
        // whatever the clock says: the one at the head of the queue is held by something, and a driver who
        // swings out past a stopped queue is a head-on. The whole of the question is one reading, so the
        // entry this hands to cannot disagree with the hand-over about whether it was wanted.
        if (scene.WorthGoingRound) return ManeuverOutcome.To(Maneuver.GoRound, ManeuverReason.Bounded);

        // The junction ahead, once it is near enough to have been asked for. A claim it could not get is
        // a stop at the boundary, and that is `P-6` above: it is what the profile is stopping for.
        if (scene.ToTheBoxM <= scene.Config.CarJunctionReserveM && scene.BoxIsOurs)
        {
            return ManeuverOutcome.To(Maneuver.TakeTheJunction, ManeuverReason.BoxIsOurs);
        }

        return ManeuverOutcome.Running;
    }
}
