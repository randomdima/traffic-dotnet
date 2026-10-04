using System.Collections.Concurrent;
using System.Numerics;
using TrafficSimulation.CityGen.Exam;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Every map this build can open, and the one place a name becomes a town.</b> A city comes from its
/// brief (<see cref="TownBrief"/>) and is generated when it is asked for, or from the map of a real place
/// (<see cref="TracedMap"/>) and is traced; a map laid to measure one thing comes from the code that lays it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One list, read by everything</b> — the start menu, the command line, every probe and every sweep — so
/// a map that can be opened one way can be opened the other and no caller has to know which kind it is
/// looking at. That is the whole reason this exists: the kinds of map differ in how they are made and in
/// nothing else, and a <see cref="CityPlan"/> is where the difference ends.
/// </para>
/// <para>
/// <b>The last town laid is kept, and only the last</b> (<see cref="Plan"/>). A town is laid when it is
/// opened and lives as long as the world built from it; asked for the same map again under the same figures,
/// it is handed back rather than laid a second time.
/// </para>
/// </remarks>
internal static class Maps
{
    /// <summary>
    /// The map every detailed check is staged on: a small town from a brief of its own — water, districts
    /// and a dozen cars, and no building, so no car park, crossing or walker (the known gaps in
    /// <c>docs/index.md</c>). <b>Named here because this is where a name becomes a town</b> —
    /// the suite, the warm-up and the command line's own default all mean this one map, and three
    /// spellings of it is two chances to mean a different one.
    /// </summary>
    public const string Fixture = "Test";

    /// <summary>
    /// The maps laid in code: the ring the start menu stands over (GEN-1b), and the scenario map — a lattice
    /// with one traffic scenario staged at each of its junctions (<see cref="ExamPlan"/>).
    /// </summary>
    static readonly (string Name, Func<SimConfig, CityPlan> Lay)[] Laid =
    [
        (IdlePlan.Name, IdlePlan.Lay),
        (ExamPlan.Name, ExamPlan.Lay),
    ];

    /// <summary>
    /// Every map there is to open, in name order — the briefs and the traced maps on disk and the laboratories laid
    /// in code. <b>A map is a brief, a survey or code</b>: no town is carried as a file, the fixture having been
    /// the last of them, and a traced map carries a place's ways rather than a town laid off them (GEN-57).
    /// </summary>
    /// <remarks>
    /// <b>The idle ring is laid but not shipped.</b> Every probe and every sweep reads this list, and the
    /// ring is a frame for the menu rather than a town to ask questions of — so it is opened by name
    /// (<see cref="Lay"/>) and swept by nobody.
    /// </remarks>
    public static string[] Shipped()
    {
        var names = new List<string>(ProjectPaths.TownBriefs()) { ExamPlan.Name };
        names.AddRange(ProjectPaths.TracedMaps());
        names.Sort(StringComparer.Ordinal);
        return [.. names];
    }

    /// <summary>Whether a map is generated from a brief rather than laid in code.</summary>
    public static bool IsGenerated(string name) => File.Exists(ProjectPaths.TownBriefFile(name));

    /// <summary>Whether a map is traced off a survey of a real place (GEN-57).</summary>
    public static bool IsTraced(string name) => File.Exists(ProjectPaths.TracedMapFile(name));

    /// <summary>
    /// <b>Whether a map is content rather than code</b> — a brief or a survey, either of which a build may ship
    /// any number of without that being a change to the engine — which is what decides that only
    /// <c>Tier.Maps</c> asks questions of it.
    /// </summary>
    public static bool IsCity(string name) => IsGenerated(name) || IsTraced(name);

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
    /// A traced map's own file (<see cref="TracedMap"/>), read whole each time it is asked for: it is milliseconds,
    /// and a town is laid off it once (<see cref="Plan"/>), so a copy kept would only be one more city in memory.
    /// </summary>
    public static TracedMap Traced(string name) => TracedMap.Read(ProjectPaths.TracedMapFile(name));

    /// <summary>
    /// What a traced map says it is, off the head of its file (<see cref="SurveyHead"/>): the menu's question
    /// is one line of a file whose whole is megabytes. Read once a name, as a brief is.
    /// </summary>
    public static string SurveyDescription(string name) =>
        SurveyDescriptions.GetOrAdd(name, static map => SurveyHead.Description(ProjectPaths.TracedMapFile(map)));

    static readonly ConcurrentDictionary<string, string> SurveyDescriptions = new();

    /// <summary>
    /// <b>The town itself, laid once for as long as it is the town being asked about.</b> A name that is
    /// neither a brief nor a laid map is a failure here rather than an empty town somewhere downstream —
    /// the list above is the whole of what exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What costs is asking for one map over and over</b>: a review sheet stages every cell through the
    /// one shot path and a city is most of a minute to lay, so nine cells of one town were nine towns and
    /// the drawing was the rounding error in it. A town is laid deterministically from its own seed, so the
    /// second lay is the first town again and the only thing it buys is the wait.
    /// </para>
    /// <para>
    /// <b>One town and never a collection of them.</b> Kept by name, every city this build ships would be
    /// alive at once the first time a sweep asked for them all, and a city is tens of megabytes of arrays.
    /// Kept as the last, it is never more than the town whoever asked is about to use anyway — and the
    /// pattern that costs is repetition rather than revisiting.
    /// </para>
    /// <para>
    /// <b>The figures are part of the name.</b> A plan lays its pavement against the configuration it was
    /// first asked with (<see cref="CityPlan.Paving"/>), so a town laid under other figures is a different
    /// town and is laid again.
    /// </para>
    /// </remarks>
    /// <param name="sizes">
    /// The footprints a generated town sizes its buildings at, read off the art by the catalogue above this slice and
    /// handed down as data (<see cref="BuildingSizes"/>, GEN-54). A map laid in code stands what its own code stands,
    /// and a traced one its survey's footprints, and neither reads this.
    /// </param>
    public static CityPlan Plan(string name, SimConfig config, BuildingSizes sizes)
    {
        if (_kept is { } kept && kept.Is(name, config)) return kept.Plan;

        var plan = Lay(name, config, sizes);

        // A reference is written whole, so a reader takes the town before or the town after and never half
        // of either. Two callers laying the same map at once lay it twice, which is what they did anyway.
        _kept = new Kept(name, config, plan);
        return plan;
    }

    /// <summary>The town most recently laid, against the name and the figures it was laid from.</summary>
    sealed record Kept(string Name, SimConfig Config, CityPlan Plan)
    {
        public bool Is(string name, SimConfig config) =>
            ReferenceEquals(Config, config) && string.Equals(Name, name, StringComparison.Ordinal);
    }

    static Kept? _kept;

    static CityPlan Lay(string name, SimConfig config, BuildingSizes sizes)
    {
        foreach (var (laid, lay) in Laid)
        {
            if (string.Equals(laid, name, StringComparison.Ordinal)) return lay(config);
        }

        if (IsGenerated(name)) return TownGenerator.Lay(Brief(name), config, sizes);
        if (IsTraced(name)) return TracedPlan.Lay(Survey.Of(Traced(name), config), config);

        throw new FileNotFoundException(
            $"No map called {name}: this build knows {string.Join(", ", Shipped())}.");
    }
}
