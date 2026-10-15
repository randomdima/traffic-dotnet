namespace TrafficSimulation.Core.Simulation;

/// <summary>
/// <b>Each stage of opening a map, said as it ends</b> to whoever is watching — a page, which has no profiler that reads
/// its memory and keeps the most it was ever given, so the stage it peaked in is the one figure worth having.
/// </summary>
/// <remarks>Build-time only, and nobody watches unless set: a stage passed unwatched is one null test.</remarks>
internal static class OpenWatch
{
    /// <summary>Called with each stage's name as it ends, on the thread laying the town.</summary>
    public static Action<string>? Watcher { get; set; }

    public static void Passed(string stage) => Watcher?.Invoke(stage);
}
