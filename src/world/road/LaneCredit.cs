namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The terms one asker's grant is cut on</b>: the ground it keeps off whatever is not going anywhere,
/// which roster's reservations it reads as traffic, and the right of way it asks with. <b>One statement of
/// where somebody else's ground stops an asker</b>, so the road and the pavement cannot come to two answers
/// about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing beyond a near edge is ever worth anything</b> (TER-4c.1). Ground is asked for, answered, and
/// then it is the asker's, so what one body holds ends where the next body's begins: a grant that reached
/// past a near edge would be the same metre granted twice, and a mechanism that could do that would be no
/// mechanism at all. <b>The whole of what this decides is how far <em>short</em> of the edge the answer
/// falls.</b>
/// </para>
/// <para>
/// <b>The two ends of it are one figure read twice.</b> <see cref="Of"/> is what a stretch in front is worth,
/// and <see cref="AtAPlaceM"/> is what a place that is nobody's stretch is worth — a junction's crossing
/// point, the kerb line of a lane a walker was refused. Neither has a margin of its own, so the asker's is
/// taken off both, and it is the same margin.
/// </para>
/// </remarks>
/// <param name="StandingMarginM">
/// The ground the asker keeps off a body that is going nowhere — <see cref="Agents.Car.Body.CarBuild.BodyMarginM"/>
/// for a driver, the standstill gap for a walker.
/// </param>
/// <param name="Under">
/// Which roster's <see cref="LaneUse.Reserved"/> stretches are a body under way rather than something else's
/// book bleeding through. A reservation is written by a body into the book of the network it is on
/// (TER-5c.1), so this is the asker's own roster and the check is total rather than defensive.
/// </param>
/// <param name="Right">
/// The right of way the asker holds the ground it is asking for with (TER-5e). <b>The weakest rank is an
/// asker that outranks nothing</b>, which is every walker on the pavement: no claim there is anybody's to
/// take, so every stretch binds.
/// </param>
internal readonly record struct LaneCredit(float StandingMarginM, LaneRoster Under, RightOfWay Right)
{
    /// <summary>
    /// <b>Where this stretch stops the asker, measured from its near edge</b>: at the edge itself where the
    /// stretch carries a margin of its own, and a margin short of it where it does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it turns on is the margin and nothing else.</b> A reservation is the one stretch laid behind
    /// its owner's tail (TER-5c.2), so its near edge already stands a gap clear of the body and the asker
    /// stops there; a wreck, a claim, somebody on foot and the town's own furniture are laid at their true
    /// extent, so the asker keeps its own margin off them.
    /// </para>
    /// <para>
    /// <b>It was once the holder's own stopping distance</b>, on the ground that a body under way will have
    /// left the metres behind it by the time anybody arrives there. That is a true thing about traffic and
    /// the wrong place to say it: the answer is written back into the book
    /// (<c>TownWorld.CutTheGroundToTheGrant</c>), so a credited answer is two bodies holding one metre —
    /// which is what a junction jams on. What it cost to take out is the proving ground's fifteen cars
    /// keeping station at seventy metres a second (<c>world/road/docs/decision-log.md</c>).
    /// </para>
    /// </remarks>
    public float Of(in LaneSlot taken) =>
        taken.Use == LaneUse.Reserved && taken.Of == Under ? 0f : AtAPlaceM;

    /// <summary>What a place that is nobody's stretch is worth, which is the asker's own margin off it.</summary>
    public float AtAPlaceM => -StandingMarginM;

    /// <summary>How much road a body doing this speed needs before it can be at rest on the ground it is on.</summary>
    public static float StoppingM(float alongMps, float brakingMps2) =>
        alongMps <= 0f ? 0f : alongMps * alongMps / (2f * brakingMps2);
}
