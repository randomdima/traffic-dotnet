namespace TrafficSimulation.Agents.Person.Body;

/// <summary>
/// Whether a walker is getting anywhere: the closest it has ever come to the end of what it is walking,
/// and how long since it last did better than that. What decides a leg has to be given up.
/// </summary>
/// <remarks>
/// <para>
/// Measured against the closest ever reached and never against the last decision — a body shoved
/// backwards and walking the same ground again has not made progress, and a clock that forgave it
/// would never run out.
/// </para>
/// <para>
/// <b>What is measured has to shrink as the walk goes well.</b> A distance to something that moves
/// forward with the body is not progress and never runs the clock down, so what this is fed is what is
/// left of the ground in front of it and never the point the follower is aiming at.
/// </para>
/// <para>
/// <b>And it has to carry no jitter</b>, because the mark is a record low: a figure that wanders by a
/// hand's breadth while a body is leaned on sets a new record every few decisions, and the clock then
/// never runs out for the one case it exists to end. Whatever is fed in is a distance the walk closes
/// and nothing the solver moves.
/// </para>
/// </remarks>
internal sealed class WalkProgress(int people)
{
    readonly float[] _sinceS = new float[people];
    readonly float[] _closestM = new float[people];

    /// <summary>A fresh leg: nothing reached yet, and no time run up against it.</summary>
    public void Restart(int person)
    {
        _sinceS[person] = 0f;
        _closestM[person] = float.MaxValue;
    }

    /// <summary>How far is left to go this decision, and how much world has passed since the last one.</summary>
    /// <param name="byM">
    /// How much nearer counts as nearer — <b>a body's own width</b>. A walker leaned on rocks where it
    /// stands, and its place on the line it is walking rocks with it; read to the millimetre, a body going
    /// nowhere sets a new closest every few decisions and the clock never runs out for the one case it
    /// exists to end.
    /// </param>
    public void Note(int person, float remainingM, float byM, float sinceLastDecisionS)
    {
        if (remainingM < _closestM[person] - byM)
        {
            _closestM[person] = remainingM;
            _sinceS[person] = 0f;
            return;
        }

        // Nearer, without being near enough to say it is getting anywhere: the mark still moves, so that
        // what counts as progress is always measured from the best the body has ever managed.
        _closestM[person] = MathF.Min(_closestM[person], remainingM);
        _sinceS[person] += sinceLastDecisionS;
    }

    public bool IsStuck(int person, float patienceS) => _sinceS[person] >= patienceS;
}
