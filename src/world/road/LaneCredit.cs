namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>How far short of what cut it a holder's grant ends</b> — the ground it keeps off whatever its hold was
/// cut at. <b>One statement</b>, which the grant of every hold is read through.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing beyond a cut is ever worth anything</b> (TER-4c.1): a hold is cut where somebody else's ground
/// begins, so what this decides is only how far <em>short</em> of that the answer falls.
/// </para>
/// <para>
/// <b>A body reserves its collider and not a metre more</b> (TER-4c.2), so the gap between two bodies is kept
/// by whoever is coming up behind: a queue at the gap a follower keeps behind the body it follows
/// (<paramref name="QueueGapM"/>), and anything else — a body going nowhere, ground another hold has, a place
/// the holder has to stop short of — at the ground it keeps off something standing still
/// (<paramref name="StandingMarginM"/>).
/// </para>
/// </remarks>
/// <param name="StandingMarginM">The ground the holder keeps off anything but a queue — a driver's body margin.</param>
/// <param name="QueueGapM">The gap it keeps behind a body going the way it is going — a driver's tail margin.</param>
/// <param name="Under">Which roster's bodies under way are a queue to it rather than something in its way.</param>
internal readonly record struct LaneCredit(float StandingMarginM, float QueueGapM, LaneRoster Under)
{
    /// <summary>The ground the holder keeps off what its hold was cut at.</summary>
    public float Of(in LaneClaim cutBy) =>
        cutBy.HasBody && cutBy.OnItsLine && cutBy.Of == Under ? QueueGapM : StandingMarginM;

    /// <summary>How much road a body doing this speed needs before it can be at rest on the ground it is on.</summary>
    public static float StoppingM(float alongMps, float brakingMps2) =>
        alongMps <= 0f ? 0f : alongMps * alongMps / (2f * brakingMps2);
}
