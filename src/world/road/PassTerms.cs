namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>What a pass is asked on besides its ground</b> (TER-4c.6): the rung it takes the plans there at, and the
/// stretch of the way a zebra's paint covers, where somebody on foot is waited for rather than refusing it.
/// </summary>
/// <remarks>
/// <b>Only a call's pass is asked at a rung</b>: every other is had only over ground nobody plans
/// (<see cref="Plain"/>) and never over a zebra. A call's takes whatever its rung beats short of ground a holder
/// can no longer stop short of, the paint of a zebra among it, and its holder waits short of the paint while
/// anybody is on it rather than asking again. <b>And it minds what it passes</b>: a call gets past traffic that
/// is moving, which may be unable to stop short of the pass where it steps back in.
/// </remarks>
/// <param name="Rung">
/// The rung the pass takes a weaker plan at. <see cref="ClaimPriority.Backing"/>, the weakest there is, takes none.
/// </param>
/// <param name="PaintFromM">Where on the way the paint of a zebra under the pass begins.</param>
/// <param name="PaintToM">And where it ends — no further than it begins where there is none.</param>
internal readonly record struct PassTerms(ClaimPriority Rung, float PaintFromM, float PaintToM)
{
    /// <summary>Asked at no rung, over no paint: every pass but a call's.</summary>
    public static PassTerms Plain => new(ClaimPriority.Backing, 0f, 0f);

    /// <summary>
    /// Whether the pass may be laid over a plan it meets at <paramref name="atM"/>: one of what it passes, the pass
    /// laid over it cutting it (TER-4c.6) — or one weaker than its rung. <b>Either only where its holder can still
    /// stop short of it</b> (TER-5e), for a call's; every other pass passes only bodies at rest, and takes nothing
    /// else.
    /// </summary>
    public bool Takes(in LaneClaim plan, float atM, bool ofWhatItPasses)
    {
        var stops = !plan.CommittedAt(atM);
        return ofWhatItPasses ? stops || !IsACalls : stops && plan.Priority > Rung;
    }

    /// <summary>Whether it is asked at a rung, which only a call's is.</summary>
    bool IsACalls => Rung != Plain.Rung;

    /// <summary>Whether a body over the pass's ground is waited for rather than refusing it: somebody on foot on the paint.</summary>
    public bool WaitsFor(in LaneClaim body) =>
        body.Of == LaneRoster.Walking && body.ToM > PaintFromM && body.FromM < PaintToM;
}
