namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The connectors leaving one lane's end, as the run of ids they are.</b> A connector is numbered inside
/// its own lane's run, so the ways out of a lane are contiguous and the run is two integers rather than a
/// list.
/// </summary>
/// <remarks>
/// It exists so that a caller walks <em>connectors</em> and never a lane paired with an index into it. The
/// pair was two numbers that had to be carried together to mean anything, and every reader of one held the
/// other; the id alone answers which lane it leaves, which it arrives on, what turn it is and what ground it
/// takes off the rest.
/// </remarks>
internal readonly record struct ConnectorRun(int FirstId, int Count)
{
    public int this[int index] => FirstId + index;

    public Enumerator GetEnumerator() => new(FirstId, FirstId + Count);

    /// <summary>A struct enumerator, because the ways out of a lane are walked on the tick (rule 2).</summary>
    public struct Enumerator(int next, int end)
    {
        public int Current { get; private set; } = -1;

        public bool MoveNext()
        {
            if (next >= end) return false;

            Current = next++;
            return true;
        }
    }
}
