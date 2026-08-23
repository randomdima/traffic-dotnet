using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.E2E.Scenarios;

namespace TrafficSimulation.Tests.E2E;

/// <summary>
/// The scenario table, assembled from one list per feature, and the two folders the tier works in.
/// </summary>
/// <remarks>
/// <para>
/// <b>A few scenarios per feature and no more.</b> The set is meant to be read in one sitting by a
/// reviewer who answers every claim on every frame; a second scenario that asks what the first
/// already asked costs that review and buys nothing.
/// </para>
/// <para>
/// The <c>core</c> group is the fixture map's own detail — every subject on it was drawn on purpose
/// and can be pointed at by name, and it opens in a fraction of a city's time. The <c>wider</c> group
/// is what the fixture cannot answer: a whole city, the skewed crossing, the debug layers and the
/// interface.
/// </para>
/// </remarks>
internal static class VisualScenarios
{
    public static readonly VisualScenario[] All =
    [
        .. GroundScenarios.All,
        .. RoadScenarios.All,
        .. JunctionScenarios.All,
        .. AgentScenarios.All,
        .. InterfaceScenarios.All,
    ];

    /// <summary>Where the frames are written. Scratch, wiped without asking — what is kept is this
    /// table and the reference frames beside it.</summary>
    public static string Frames { get; } = Path.Combine(ProjectPaths.Root, ".tmp", "e2e");

    /// <summary>
    /// The reference frames: the same scenarios as photographed by the godot-dotnet build, which is
    /// the reference implementation this engine is held to. Read from the source tree rather than
    /// copied into the test output — they are 29 MB, and nothing about them changes per build.
    /// </summary>
    public static string Expected { get; } = Path.Combine(ProjectPaths.Root, "src", "tests", "e2e", "expected");

    /// <summary>The names of one group, as the theories take them. Names rather than the scenarios
    /// themselves, so a test case is called after its scenario and can be run by that name.</summary>
    public static IEnumerable<string> Named(string group)
    {
        foreach (var scenario in All)
            if (scenario.Group == group)
                yield return scenario.Name;
    }

    /// <summary>One scenario by name. Throws rather than returning a default: a test staged on a
    /// scenario the table does not have is a broken test, and photographing the origin instead is how
    /// it would go unnoticed.</summary>
    public static VisualScenario ByName(string name) =>
        Array.Find(All, scenario => scenario.Name == name)
        ?? throw new ArgumentException($"no visual scenario called '{name}'", nameof(name));
}
