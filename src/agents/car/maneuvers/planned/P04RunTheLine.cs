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
/// road is doing with a shorter road to do it on. What the entry that used to be here decided is the one
/// thing left over — that what is in front is not a queue at all, and the way past it is round it.
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

        // The line stops where the bay's own template is staged from, so a car at rest on the end of it
        // has finished driving and what is left of the leg is the plan's next step.
        if (scene.OnTheFinalApproach
            && scene.ToTheEndM <= scene.Config.Car.LengthM * 0.5f
            && scene.AtRest)
        {
            return ManeuverOutcome.Done(ManeuverReason.RouteRanOut);
        }

        if (scene.Hold == DrivingHold.Crossing)
        {
            return ManeuverOutcome.To(Maneuver.PassACrossing, ManeuverReason.None);
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
            return scene.RouteReversesHere
                ? ManeuverOutcome.To(Maneuver.TurnAround, ManeuverReason.None)
                : ManeuverOutcome.To(Maneuver.TakeTheJunction, ManeuverReason.BoxIsOurs);
        }

        return ManeuverOutcome.Running;
    }
}
