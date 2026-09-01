using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Every map this build can open, and the one place a name becomes a town.</b> A city comes from its
/// brief (<see cref="TownBrief"/>) and is generated when it is asked for; a map laid to measure one thing
/// comes from the code that lays it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One list, read by everything</b> — the start menu, the command line, every probe and every sweep — so
/// a map that can be opened one way can be opened the other and no caller has to know which kind it is
/// looking at. That is the whole reason this exists: the two kinds of map differ in how they are made and in
/// nothing else, and a <see cref="CityPlan"/> is where the difference ends.
/// </para>
/// <para>
/// <b>Nothing is cached.</b> A town is laid when it is opened and lives as long as the world built from it,
/// so opening the same map twice lays it twice — deterministically, from the same seed, into the same town.
/// </para>
/// </remarks>
internal static class Maps
{
    /// <summary>The maps laid in code, each against the name it carries.</summary>
    static readonly (string Name, Func<SimConfig, CityPlan> Lay)[] Laid =
    [
        (TrackPlan.NameOf(TrackLap.Pacing), config => TrackPlan.Lay(config, TrackLap.Pacing)),
        (TrackPlan.NameOf(TrackLap.Drunk), config => TrackPlan.Lay(config, TrackLap.Drunk)),
        (TrackPlan.NameOf(TrackLap.Fleet), config => TrackPlan.Lay(config, TrackLap.Fleet)),
        (ExamPlan.Name, ExamPlan.Lay),
        (SkidpadPlan.Name, SkidpadPlan.Lay),
        (IdlePlan.Name, IdlePlan.Lay),
    ];

    /// <summary>Every map there is, in name order: the briefs on disk, the maps laid in code, and the fixtures still carried as files.</summary>
    public static string[] Shipped()
    {
        var names = new List<string>(ProjectPaths.TownBriefs());
        foreach (var (name, _) in Laid) names.Add(name);
        names.AddRange(ProjectPaths.ShippedMaps());
        names.Sort(StringComparer.Ordinal);
        return [.. names];
    }

    /// <summary>Whether a map is generated from a brief rather than laid in code.</summary>
    public static bool IsGenerated(string name) => File.Exists(ProjectPaths.TownBriefFile(name));

    /// <summary>
    /// The brief a generated map is laid from, for whoever wants to say what the map is. <b>Read once a
    /// name</b>: the menu asks every map what it is every time it draws a row, and a brief is the same file
    /// however often it is read.
    /// </summary>
    public static TownBrief Brief(string name) => Briefs.GetOrAdd(name, static map =>
    {
        var path = ProjectPaths.TownBriefFile(map);
        var brief = AssetJson.Read(path, TownBriefJson.Default.TownBrief);
        brief.Check(path);
        return brief;
    });

    static readonly ConcurrentDictionary<string, TownBrief> Briefs = new();

    /// <summary>
    /// The town itself. <b>A name that is neither a brief nor a laid map is a failure here</b> rather than an
    /// empty town somewhere downstream — the list above is the whole of what exists.
    /// </summary>
    public static CityPlan Plan(string name, SimConfig config, ReadOnlySpan<Vector2> roofsM)
    {
        foreach (var (laid, lay) in Laid)
        {
            if (string.Equals(laid, name, StringComparison.Ordinal)) return lay(config);
        }

        if (IsGenerated(name)) return TownGenerator.Lay(Brief(name), config, roofsM);

        // The two fixtures still arrive as files — the ground every detailed check is staged on, and the
        // crossings map. Neither may move when the generator does, so both are on their way to being laid in
        // code rather than generated.
        var file = ProjectPaths.TownFile(name);
        if (File.Exists(file)) return TownReader.ReadFile(file);

        throw new FileNotFoundException(
            $"No map called {name}: this build knows {string.Join(", ", Shipped())}.");
    }
}
