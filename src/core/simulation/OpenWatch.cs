namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// <b>Each stage of opening a map, said as it ends</b> to whoever is watching — a page, which has no profiler that reads
/// its memory and keeps the most it was ever given, so the stage it peaked in is the one figure worth having, and whose
/// opening card says how far the open has got (WEB-8).
/// </summary>
/// <remarks>Build-time only, and nobody watches unless set: a stage passed unwatched is one null test.</remarks>
internal static class OpenWatch
{
    /// <summary>Called with each stage's name as it ends, on the thread laying the town.</summary>
    public static Action<string>? Watcher { get; set; }

    /// <summary>The stage, said — and handed back, for a lay done a stage at a time to yield where it may pause (<see cref="Staged{T}"/>).</summary>
    public static string Passed(string stage)
    {
        Watcher?.Invoke(stage);
        return stage;
    }
}
