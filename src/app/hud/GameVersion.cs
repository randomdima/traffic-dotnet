using System.Reflection;

namespace TrafficSimulation.App.Hud;

/// <summary>
/// <b>Which build this is, as the interface writes it</b> — <c>v0.1.436 (8e5de894)</c>: the game's version and the
/// commit it was built from, read once off the assembly's informational version, which the build stamps
/// (Directory.Build.props, Directory.Build.targets) and the SDK appends the commit to.
/// </summary>
/// <remarks>
/// <b>Read and never written here</b>, so the start menu, the menu in a town and the card a map opens behind all say
/// what the page's own opening says (src/app/web/wwwroot/build.js) without a second copy of the number.
/// </remarks>
internal static class GameVersion
{
    /// <summary>How much of the commit is written: git's own short form, enough to find it again.</summary>
    const int CommitShown = 8;

    public static readonly string Text = Read();

    static string Read()
    {
        var stamped = typeof(GameVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(stamped)) return "v?";

        var plus = stamped.IndexOf('+');
        if (plus < 0) return $"v{stamped}";

        var commit = stamped[(plus + 1)..];
        return $"v{stamped[..plus]} ({commit[..Math.Min(CommitShown, commit.Length)]})";
    }
}
