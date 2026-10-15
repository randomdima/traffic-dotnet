using System.Collections.Concurrent;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>An input read once a process</b>, however many steps of the run ask for it: <c>meta</c> lays its layers and then
/// audits them in one process, and both read the timetable and the machine-traced footprints.
/// </summary>
/// <remarks>
/// Only for what the process never writes — a kept source, never a layer, which <c>meta</c> rewrites before auditing.
/// What is held is shared, so a reader never changes it. The key names the file and anything else the reading depends on.
/// </remarks>
internal static class Held
{
    static readonly ConcurrentDictionary<string, Lazy<object>> Read = new();

    public static T Once<T>(string key, Func<T> read) where T : class =>
        (T)Read.GetOrAdd($"{typeof(T).FullName}\n{key}", _ => new Lazy<object>(read)).Value;

    /// <summary>The key of a set of files: each one's full path, in order.</summary>
    public static string Files(IEnumerable<string> paths) => string.Join('\n', paths.Select(Path.GetFullPath));
}
