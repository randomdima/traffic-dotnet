using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Tests;

/// <summary>
/// Where a test's throwaway file goes: the project's own <c>.tmp/</c> and never the system
/// temp directory, so a file a failing test left behind can still be opened. Nothing here is a
/// record — the drawer is wiped without asking.
/// </summary>
internal static class Scratch
{
    static readonly string Directory = CreateDirectory();

    public static string Write(string name, string contents)
    {
        var path = Path.Combine(Directory, name);
        File.WriteAllText(path, contents);
        return path;
    }

    static string CreateDirectory()
    {
        var path = Path.Combine(ProjectPaths.Root, ".tmp", "dotnet-tests");
        System.IO.Directory.CreateDirectory(path);
        return path;
    }
}
