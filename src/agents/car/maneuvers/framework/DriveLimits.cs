namespace TrafficSimulation.Agents.Car.Maneuvers;

/// <summary>
/// <b>The whole of what a manoeuvre does to the car.</b> An entry's procedure sets these and nothing
/// else; the standing rules (§1.7) turn them, the line and what the book found into a steering angle
/// and a pedal. A manoeuvre that could write a command directly would be a manoeuvre that could put a
/// car somewhere the tyres could not have taken it.
/// </summary>
/// <remarks>
/// <para>
/// <b>They are re-asked on every decision and never latched.</b> A manoeuvre that stops setting a limit
/// has stopped wanting it, so the director clears these before each tick of a procedure; between
/// decisions they stand, which is a staleness of at most one decision interval and a stated figure.
/// </para>
/// <para>
/// <see cref="StopWithinM"/> is a distance from the rear axle and <em>not</em> a place, so between
/// decisions it is walked in by the ground the car covers — a stop point left unchanged while driving
/// toward it recedes at exactly the car's own speed, which is a stop line it can never reach.
/// </para>
/// </remarks>
/// <param name="CapMps">A ceiling on the speed profile's answer — manoeuvring pace, a crawl. Infinite where the manoeuvre imposes none.</param>
/// <param name="StopWithinM">How far ahead along the line the manoeuvre wants the car stopped. Infinite where it wants no stop of its own.</param>
/// <param name="HoldStill">Stand where you are: the profile's answer is zero and the handbrake holds it once the body has settled (S-5).</param>
/// <param name="SpendTheTyre">
/// `E-2` only: the margin the profile plans grip against is gone and what is left of the tyre is spent
/// at once. Braking that ramps up wastes the most valuable distance there is.
/// </param>
internal readonly record struct DriveLimits(float CapMps, float StopWithinM, bool HoldStill, bool SpendTheTyre)
{
    public static DriveLimits None => new(float.PositiveInfinity, float.PositiveInfinity, false, false);

    /// <summary>Stand still here, which is what every waiting entry asks for.</summary>
    public static DriveLimits Hold => new(0f, 0f, true, false);

    public bool HasStopPoint => !float.IsPositiveInfinity(StopWithinM);

    /// <summary>
    /// The stop point walked in by the ground covered since it was set. At zero it has been arrived at,
    /// and the honest reading of "stop within nothing" is a hold — leaving it at zero as a distance
    /// would release the car at the very line it was stopping for.
    /// </summary>
    public DriveLimits Carried(float alongM)
    {
        if (!HasStopPoint || alongM <= 0f) return this;

        var remainingM = StopWithinM - alongM;
        return remainingM > 0f
            ? this with { StopWithinM = remainingM }
            : this with { StopWithinM = 0f, HoldStill = true };
    }
}
