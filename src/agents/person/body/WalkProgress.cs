namespace TrafficSimulation.Agents.Person.Body;

/// <summary>
/// Whether a walker is getting anywhere: the closest it has ever come to where it is going, and how
/// long since it last did better than that. What decides a leg has to be given up.
/// </summary>
/// <remarks>
/// Measured against the closest ever reached and never against the last decision — a body shoved
/// backwards and walking the same ground again has not made progress, and a clock that forgave it
/// would never run out.
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
    public void Note(int person, float remainingM, float sinceLastDecisionS)
    {
        if (remainingM < _closestM[person])
        {
            _closestM[person] = remainingM;
            _sinceS[person] = 0f;
            return;
        }

        _sinceS[person] += sinceLastDecisionS;
    }

    public bool IsStuck(int person, float patienceS) => _sinceS[person] >= patienceS;
}
