namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-19` — shunt round.</b> The leg has to come back the way it came and there is no bay to turn in:
/// the car works itself round on the spot, a leg of the turn at a time, forwards and back at full lock.
/// See <c>docs/p19-shunt-round.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a sequence of lines and not one shape.</b> Each leg is a single arc as long as the ground and
/// the book will hold, and the next is laid from the pose the last one ended in — so a wide dead end comes
/// round in two legs and a narrow one in six, without anything here knowing which kind it is standing in.
/// </para>
/// <para>
/// <b>The gear alternates and the wheel does not</b>, which is the whole of what makes this a turn rather
/// than a car rocking on one spot (<see cref="ManeuverDesk.LayTheShunt"/>).
/// </para>
/// <para>
/// <b>It is the last way round there is</b> (GEN-4l): a leg turns at a car park where one is laid for it,
/// and shunts only where the road offers nothing else — which is a dead end, the one place a town promises
/// the room for it (TER-5a).
/// </para>
/// </remarks>
internal static class P19ShuntRound
{
    /// <summary>Steering an arc at full lock across a street, which is a control loop and not a decision.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary>
    /// <c>Sa</c>: at rest, with this leg coming back the other way from here, not round yet, and a leg of
    /// the turn that the ground and the book both admit.
    /// </summary>
    /// <remarks>
    /// <b>It is entered part-way round as readily as at the start</b>, which is what a reflex interrupting
    /// a turn leaves behind (§1.6): the entry state is the pose, so a car already half round asks for the
    /// leg that suits <em>that</em> pose rather than for the first one again.
    /// </remarks>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject)
    {
        if (!scene.TurnsBackHere || !scene.AtRest) return ManeuverStart.No;
        if (desk.IsOnTheWayBack(scene.Car)) return ManeuverStart.No;

        // <b>A car still holding a leg of this turn is already in it</b>, and takes up again on the line it
        // has rather than on one laid over it: what stops a leg being laid is the ground being somebody
        // else's for a moment, and a reflex that fires in the middle of a turn would otherwise end the leg.
        return scene.OnATemplate || TheNextLeg(scene, desk, backwards: false)
            ? ManeuverStart.Yes
            : ManeuverStart.No;
    }

    /// <summary>
    /// Drive this leg out, then either the body is round — and `P-4` takes the lane it is now pointing
    /// along — or the next leg is laid in the other gear. <b>Bounded by a clock</b> (MAN-4), because a car
    /// shunting is a car moving and no watchdog measuring stillness will ever find it.
    /// </summary>
    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (!scene.OnATemplate) return ManeuverOutcome.Fail(Maneuver.RunTheLine, ManeuverReason.LostTheLine);
        if (!scene.LineIsSpent) return ManeuverOutcome.Running;

        // Round, and standing on the line it is about to be handed: `P-4` takes the lane under it from here.
        if (desk.IsOnTheWayBack(scene.Car)) return ManeuverOutcome.To(Maneuver.RunTheLine, ManeuverReason.LineSpent);
        if (sinceS >= scene.Config.CarShuntRoundS) return ManeuverOutcome.Escalate(ManeuverReason.Bounded);

        // <b>A leg that will not lay is waited on and never escalated</b>: what refuses one is the ground
        // being somebody else's, which is a fact about this moment and not about this dead end. The car
        // stands where the last leg left it and asks again, and the clock above is what makes that finite.
        TheNextLeg(scene, desk, backwards: !scene.Reverse);
        return ManeuverOutcome.Running;
    }

    /// <summary>
    /// The leg this pose asks for: <b>the line-up where the body is round already</b>, and another sweep of
    /// the turn where it is not — in the gear the last leg was not, because that is what turns a car round
    /// rather than driving it in a circle.
    /// </summary>
    static bool TheNextLeg(in DriveScene scene, ManeuverDesk desk, bool backwards)
    {
        if (desk.PointsTheWayBack(scene.Car)) return desk.LayTheLineUpOntoTheWayBack(scene.Car);

        return desk.TheWayBack(scene.Car, out var backRad)
               && desk.LayTheShunt(scene.Car, backRad, backwards);
    }
}
