namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>How a manoeuvre stood at the end of a tick of its own procedure (§1.2's exits).</summary>
internal enum ManeuverOutcomeKind : byte
{
    /// <summary>Still running its own procedure.</summary>
    Running,

    /// <summary>Delivered its <c>Sb</c>. The successor it names takes over, or the plan's next step where it names none.</summary>
    Succeeded,

    /// <summary>Did not deliver its <c>Sb</c>. <b>Every failure names a successor</b> (MAN-5) — "stop and think" is not an exit.</summary>
    Failed,

    /// <summary>
    /// A reactive entry's obligation is discharged: hand back to the planned manoeuvre it suspended,
    /// <b>through that manoeuvre's own <c>Sa</c></b> and never mid-procedure (§1.6).
    /// </summary>
    Resume,

    /// <summary>Nothing of this manoeuvre's own is left to try, so the ladder takes the next rung (§5).</summary>
    Escalate,

    /// <summary>The drive leg is over — parked, settled or abandoned.</summary>
    Finished,
}

/// <summary>
/// Why an outcome came out the way it did, as a code rather than a sentence. <b>The trace and the
/// read-outs are the only readers</b>, and a string built per hand-over is an allocation on the town's
/// hottest path for a line nobody is looking at.
/// </summary>
internal enum ManeuverReason : byte
{
    None,

    /// <summary>The line or the template the manoeuvre was driving has been driven to its end.</summary>
    LineSpent,

    /// <summary>The gap the manoeuvre was waiting for came, or the patience for it ran out.</summary>
    GapTaken,

    /// <summary>The thing that was in the way is not in the way any more.</summary>
    WayIsClear,

    /// <summary>Somebody with priority is no longer exercising it.</summary>
    PriorityGone,

    /// <summary>The box ahead is this car's to enter.</summary>
    BoxIsOurs,

    /// <summary>The route has nothing left in it and the bay's own template takes over.</summary>
    RouteRanOut,

    /// <summary>The car is not on the line it was given.</summary>
    LostTheLine,

    /// <summary>The car is not on ground a car may drive on.</summary>
    OffLegalGround,

    /// <summary>A bound was reached — a time, a distance, or an attempt count (MAN-4).</summary>
    Bounded,

    /// <summary>The geometry this manoeuvre needs does not exist from where the car stands.</summary>
    NoGeometry,

    /// <summary>There is no place left to aim at, or none that can be reached.</summary>
    NoPlace,

    /// <summary>Nothing is timing this car and nothing it can do will change that.</summary>
    NothingLeft,
}

/// <summary>
/// What a manoeuvre hands back on a tick of its procedure: how it stands, the successor it names, and
/// the code that says why. <b>A success naming <see cref="Maneuver.None"/> means "the plan's next
/// step"</b> — which is how a chain the planner laid is walked without any entry knowing what follows it.
/// </summary>
internal readonly record struct ManeuverOutcome(ManeuverOutcomeKind Kind, Maneuver Next, ManeuverReason Why)
{
    public static ManeuverOutcome Running => new(ManeuverOutcomeKind.Running, Maneuver.None, ManeuverReason.None);

    /// <summary>Done, and the plan says what is next.</summary>
    public static ManeuverOutcome Done(ManeuverReason why) =>
        new(ManeuverOutcomeKind.Succeeded, Maneuver.None, why);

    /// <summary>Done, and this entry names its own successor rather than deferring to the plan.</summary>
    public static ManeuverOutcome To(Maneuver next, ManeuverReason why) =>
        new(ManeuverOutcomeKind.Succeeded, next, why);

    public static ManeuverOutcome Fail(Maneuver next, ManeuverReason why) =>
        new(ManeuverOutcomeKind.Failed, next, why);

    public static ManeuverOutcome Resume(ManeuverReason why) =>
        new(ManeuverOutcomeKind.Resume, Maneuver.None, why);

    public static ManeuverOutcome Escalate(ManeuverReason why) =>
        new(ManeuverOutcomeKind.Escalate, Maneuver.None, why);

    public static ManeuverOutcome Finished(ManeuverReason why) =>
        new(ManeuverOutcomeKind.Finished, Maneuver.None, why);
}
