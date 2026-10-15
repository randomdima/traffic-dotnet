using System.Numerics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>The runs every body keeps for a watch over every body</b> (<see cref="TownWatch"/>), stepped off one step's sweeps.
/// Bodies are numbered walkers first, then cars.
/// </summary>
internal sealed class WatchRuns(int walkers, int cars, TownWatchTerms terms)
{
    readonly float[] _overlapM = new float[walkers + cars];
    readonly int[] _stuckTicks = new int[walkers + cars];
    readonly float[] _pastM = new float[walkers + cars];
    readonly int[] _pastTicks = new int[walkers + cars];
    readonly Vector2[] _stillFromM = new Vector2[walkers];
    readonly int[] _stillTicks = new int[walkers];

    /// <summary>How many bodies the runs cover.</summary>
    public int Bodies => _overlapM.Length;

    /// <summary>
    /// <b>One step</b>: every body's run inside something and run going deeper past its grant carried on or broken, every
    /// walker's run afoot getting nowhere, and the step's deepest, longest and furthest, each with the first body that had it.
    /// </summary>
    /// <param name="overlapM">How deep every body is into anything.</param>
    /// <param name="pastM">How far past its grant every body is — nought for a walker.</param>
    /// <param name="walkerAtM">Where every walker stands.</param>
    /// <param name="afoot">Whether every walker is out and walking: up, set walking and indoors nowhere.</param>
    /// <param name="drivenM">The metres the driven cars covered in the step, carried into what it reads.</param>
    public TownWatchedTick Step(
        ReadOnlySpan<float> overlapM, ReadOnlySpan<float> pastM, ReadOnlySpan<Vector2> walkerAtM, ReadOnlySpan<bool> afoot, float drivenM)
    {
        var (deepestM, deepestBody, stuck, stuckBody) = (0f, -1, 0, -1);
        var (furthestM, furthestBody, pastTicks, pastBody) = (0f, -1, 0, -1);
        for (var body = 0; body < _overlapM.Length; body++)
        {
            _overlapM[body] = overlapM[body];
            if (overlapM[body] > deepestM) (deepestM, deepestBody) = (overlapM[body], body);

            _stuckTicks[body] = overlapM[body] > terms.OverlapAllowanceM ? _stuckTicks[body] + 1 : 0;
            if (_stuckTicks[body] > stuck) (stuck, stuckBody) = (_stuckTicks[body], body);

            // Going deeper and never being past: a car stopped a stride over where it was told to latches a run otherwise.
            if (pastM[body] > furthestM) (furthestM, furthestBody) = (pastM[body], body);

            var deeper = pastM[body] > terms.PastTheGrantAllowanceM && pastM[body] > _pastM[body] + terms.PastTheGrantAllowanceM;
            _pastTicks[body] = deeper ? _pastTicks[body] + 1 : 0;
            _pastM[body] = pastM[body];
            if (_pastTicks[body] > pastTicks) (pastTicks, pastBody) = (_pastTicks[body], body);
        }

        var still = 0;
        for (var walker = 0; walker < _stillTicks.Length; walker++)
        {
            if (!afoot[walker] || (walkerAtM[walker] - _stillFromM[walker]).Length() > terms.MovedM)
            {
                _stillFromM[walker] = walkerAtM[walker];
                _stillTicks[walker] = 0;
                continue;
            }

            _stillTicks[walker]++;
            still = Math.Max(still, _stillTicks[walker]);
        }

        return new TownWatchedTick(deepestM, deepestBody, stuck, stuckBody, furthestM, furthestBody, pastTicks, pastBody, still, drivenM);
    }

    /// <summary>What the runs have against one body, as the last step left it.</summary>
    public TownWatchNote NoteOf(int body) => new(body, _overlapM[body], _stuckTicks[body], _pastM[body], _pastTicks[body]);
}
