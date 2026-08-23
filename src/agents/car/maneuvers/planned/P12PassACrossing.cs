namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>`P-12` — pass a crossing.</b> The paint on the arm being approached: arrive at it at the crossing
/// pace, stop short of it while anybody is on it or stepping onto it, and clear it before speeding up.
/// See <c>docs/p12-pass-a-crossing.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The yield is discharged here and never handed to `E-1`.</b> Handing it to a manoeuvre whose entry
/// conditions a person on foot cannot satisfy gets it refused, and a refused reactive manoeuvre goes to
/// the ladder — which answers a pedestrian by reversing away from them.
/// </para>
/// <para>
/// <b>One manoeuvre, one crossing.</b> Asked as "is there paint ahead" it is never false for long, since
/// a junction paints its far arm too; only the crossing on the arm being approached counts, and it is
/// the standing rules that pick which that is.
/// </para>
/// </remarks>
internal static class P12PassACrossing
{
    /// <summary>A body on the paint is a body that moves, and the answer about it is not one to be approximately right about.</summary>
    public const bool ThinksEveryTick = true;

    public const bool Watched = true;

    /// <summary><c>Sa</c>: a crossing within reach on the arm the car is on.</summary>
    public static ManeuverStart Begin(in DriveScene scene, ManeuverDesk desk, int subject) =>
        float.IsPositiveInfinity(scene.Context.CrossingAtM) ? ManeuverStart.No : ManeuverStart.Yes;

    public static ManeuverOutcome Tick(in DriveScene scene, ManeuverDesk desk, float sinceS, ref DriveLimits limits) =>
        float.IsPositiveInfinity(scene.Context.CrossingAtM)
            ? ManeuverOutcome.To(Maneuver.RunTheLine, ManeuverReason.PaintIsBehind)
            : ManeuverOutcome.Running;
}
