namespace TrafficSimulation.World.Routing;

/// <summary>
/// Whether an actor is getting anywhere on the leg it is travelling: the closest it has ever come to the
/// end of what it is covering, and how long since it last did better than that. What decides a leg has to
/// be given up.
/// </summary>
/// <remarks>
/// <para>
/// <b>One clock for both agent kinds</b>, because at this tier they are the same question — a walker and
/// a driver each travel a chain of the network's own ways, and neither can tell "held up" from "going
/// nowhere" without one. What differs is the distance fed in and the patience it is asked against, and
/// both of those are the caller's.
/// </para>
/// <para>
/// Measured against the closest ever reached and never against the last decision — a body shoved
/// backwards and covering the same ground again has not made progress, and a clock that forgave it
/// would never run out.
/// </para>
/// <para>
/// <b>What is measured has to shrink as the leg goes well.</b> A distance to something that moves
/// forward with the body is not progress and never runs the clock down, so what this is fed is what is
/// left of the ground in front of it and never the point the follower is aiming at.
/// </para>
/// <para>
/// <b>And it has to carry no jitter</b>, because the mark is a record low: a figure that wanders by a
/// hand's breadth while a body is leaned on sets a new record every few decisions, and the clock then
/// never runs out for the one case it exists to end. Whatever is fed in is a distance the leg closes
/// and nothing the solver moves.
/// </para>
/// </remarks>
internal sealed class LegProgress(int actors)
{
    readonly float[] _sinceS = new float[actors];
    readonly float[] _closestM = new float[actors];

    /// <summary>A fresh leg: nothing reached yet, and no time run up against it.</summary>
    public void Restart(int actor)
    {
        _sinceS[actor] = 0f;
        _closestM[actor] = float.MaxValue;
    }

    /// <summary>How far is left to go this decision, and how much world has passed since the last one.</summary>
    /// <param name="byM">
    /// How much nearer counts as nearer — <b>a body's own width</b>. A body leaned on rocks where it
    /// stands, and its place on the way it is travelling rocks with it; read to the millimetre, one going
    /// nowhere sets a new closest every few decisions and the clock never runs out for the one case it
    /// exists to end.
    /// </param>
    public void Note(int actor, float remainingM, float byM, float sinceLastDecisionS)
    {
        if (remainingM < _closestM[actor] - byM)
        {
            _closestM[actor] = remainingM;
            _sinceS[actor] = 0f;
            return;
        }

        // Nearer, without being near enough to say it is getting anywhere: the mark still moves, so that
        // what counts as progress is always measured from the best the body has ever managed.
        _closestM[actor] = MathF.Min(_closestM[actor], remainingM);
        _sinceS[actor] += sinceLastDecisionS;
    }

    /// <summary>
    /// The clock held where the leg is stopped for something that will end on its own — a light. <b>It
    /// buys the wait and not the standing</b>: the clock stands still rather than being given back, so a
    /// body still where it was when the light went green is not waiting for it.
    /// </summary>
    public void Hold(int actor, float remainingM) => _closestM[actor] = MathF.Min(_closestM[actor], remainingM);

    public bool IsStuck(int actor, float patienceS) => _sinceS[actor] >= patienceS;

    /// <summary>How long this actor has been getting nowhere, which is a reading and never a decision.</summary>
    public float StuckForS(int actor) => _sinceS[actor];
}
