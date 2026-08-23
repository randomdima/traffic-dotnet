using System.Collections.Concurrent;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using Xunit;

namespace TrafficSimulation.Tests.CityGen;

/// <summary>
/// Every town the suite asks a question of is read once and handed out. Reading Odesa is a tenth of
/// a second and there are a dozen questions to ask of it; the reference suite spent forty-six of its
/// fifty-four seconds laying the same handful of towns over and over.
/// </summary>
/// <remarks>
/// <b>A shared plan must not be written to.</b> A test that wants to break a town on purpose reads
/// its own copy through <see cref="Fresh"/>, and the day this engine has a generator, its two
/// determinism tests take a fresh town twice on purpose — handed the shared one they would compare a
/// town to itself and pass whatever it did.
/// </remarks>
internal static class Towns
{
    /// <summary>The fixture map: one screen, one of every kind of ground, and what detailed checks are staged on.</summary>
    public const string Fixture = "Test";

    static readonly ConcurrentDictionary<string, CityPlan> Shared = new();

    public static IEnumerable<string> Shipped => ProjectPaths.ShippedMaps();

    public static CityPlan Of(string map) => Shared.GetOrAdd(map, Fresh);

    public static CityPlan Fresh(string map) => TownReader.ReadFile(ProjectPaths.TownFile(map));

    /// <summary>Every shipped map, as xUnit wants its cases: one row per map, so a failure names it.</summary>
    public static TheoryData<string> EveryShippedMap()
    {
        var maps = new TheoryData<string>();
        foreach (var map in Shipped) maps.Add(map);
        return maps;
    }

    /// <summary>
    /// Every shipped map that was laid with a pavement — <b>which is what the walking network's own
    /// questions are asked of</b>. A map laid without one (<see cref="CityPlan.PavementWidthM"/>) has no
    /// footway, no kerb and nobody on it, and every claim about corners, mitres and crossings there is
    /// vacuously true rather than checked.
    /// </summary>
    public static TheoryData<string> EveryMapWithAFootway()
    {
        var maps = new TheoryData<string>();
        foreach (var map in Shipped)
        {
            if (Of(map).PavementWidthM > 0f) maps.Add(map);
        }

        return maps;
    }
}
