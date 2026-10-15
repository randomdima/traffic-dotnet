namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// <b>Work done a stage at a time</b>: an iterator yielding each stage as it passes (<see cref="OpenWatch.Passed"/>), and
/// handing what it made to the end it is given. A head that can wait runs it <see cref="Through"/>; a page, which has one
/// thread, hands the browser a frame between two stages, which is what lets its opening card move (WEB-8).
/// </summary>
/// <remarks>A stage is a place the work may pause and never a promise that it is short: what runs between two is one block.</remarks>
internal sealed class Staged<T>(Func<Action<T>, IEnumerable<string>> stages) where T : class
{
    T? _made;

    /// <summary>Each stage as it passes. Enumerated once.</summary>
    /// <remarks>
    /// <b>The lay let go of once its last stage has passed</b>: an iterator keeps its locals as fields, and one held by
    /// whoever enumerates this — a foreach in an iterator above it is a field there too — keeps a finished plan's working
    /// set through the town laid after it, tens of megabytes on a traced city.
    /// </remarks>
    public IEnumerable<string> Stages
    {
        get
        {
            IEnumerator<string>? lay = stages(made => _made = made).GetEnumerator();
            while (lay.MoveNext()) yield return lay.Current;

            lay.Dispose();
            lay = null;
        }
    }

    /// <summary>What the stages made, once the last has passed.</summary>
    public T Made => _made ?? throw new InvalidOperationException("What a staged lay made was asked for before its last stage passed.");

    /// <summary>Every stage run at once, and what they made.</summary>
    public T Through()
    {
        foreach (var _ in Stages)
        {
        }

        return Made;
    }

    /// <summary>Work with no stage of its own, done whole once it is run.</summary>
    public static Staged<T> Whole(Func<T> make) => new(made => Once(make, made));

    static IEnumerable<string> Once(Func<T> make, Action<T> made)
    {
        made(make());
        yield break;
    }
}
