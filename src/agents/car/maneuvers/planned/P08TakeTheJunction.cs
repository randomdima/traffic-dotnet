namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-8` — take the junction.</b> One entry for all three movements: ahead, the near-side turn and the
/// turn across the oncoming stream. Which one is being made is a fact about the route, and the line for
/// it was drawn with the rest of the leg. See <c>docs/p08-take-the-junction.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The claim is the manoeuvre and the geometry is not.</b> The turn itself is lanes and a join
/// between them, which the assembler laid; what makes crossing a junction a manoeuvre rather than more
/// road is that the box is exclusive, that a car which cannot hold it stops at the boundary, and that
/// one already inside it holds it as a fact.
/// </para>
/// <para>
/// <b>Nothing is added on top of a green.</b> Where a signal has already decided whose turn it is, a
/// second gate does not make the junction safer — it makes the phase useless, and the queue with the
/// green crosses in single file. What is left standing is everything that is not a duplicate of the
/// signal: the headway, the stranded-in-the-box refusal, the bar, and the yield to anybody on the paint.
/// </para>
/// </remarks>
internal static class P08TakeTheJunction
{
    /// <summary>Negotiating with traffic doing eleven metres a second: a tenth of a second of staleness is a gap that had already closed.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary><c>Sa</c>: a box within the reserve distance ahead that this car has been given.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject) =>
        scene.ToTheBoxM <= scene.Config.CarJunctionReserveM && scene.BoxIsOurs
            ? ManeuverStart.Yes
            : ManeuverStart.No;

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits)
    {
        if (scene.InsideTheBox) return ManeuverOutcome.Running;

        // <b>Through is asked before refused</b>, and the order is the whole of the test: both states read
        // "not in a box that is mine", and only the distance to the next one tells them apart. Asked the
        // other way round, every car that had just crossed a junction reported that it had been turned
        // back from one.
        if (scene.ToTheBoxM > scene.Config.CarJunctionReserveM)
        {
            return ManeuverOutcome.To(Maneuver.RunTheLine, ManeuverReason.LineSpent);
        }

        // Refused after it was given, and not yet committed: the boundary is where that is answered, and
        // stopping short of it is `P-6`.
        return scene.BoxIsOurs
            ? ManeuverOutcome.Running
            : ManeuverOutcome.Fail(Maneuver.HoldAtALine, ManeuverReason.PriorityGone);
    }
}
