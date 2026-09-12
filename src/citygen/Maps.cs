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
/// <b>The last town laid is kept, and only the last</b> (<see cref="Plan"/>). A town is laid when it is
/// opened and lives as long as the world built from it; asked for the same map again under the same figures,
/// it is handed back rather than laid a second time.
/// </para>
/// </remarks>
internal static class Maps
{
    /// <summary>
    /// The map every detailed check is staged on: one screen, one of every kind of ground, furnished
    /// thinly and with a crowd on it. <b>Named here because this is where a name becomes a town</b> —
    /// the suite, the warm-up and the command line's own default all mean this one map, and three
    /// spellings of it is two chances to mean a different one.
    /// </summary>
    public const string Fixture = "Test";

    /// <summary>The maps laid in code, each against the name it carries.</summary>
    static readonly (string Name, Func<SimConfig, CityPlan> Lay)[] Laid =
    [
        (TrackPlan.NameOf(TrackLap.Pacing), config => TrackPlan.Lay(config, TrackLap.Pacing)),
        (TrackPlan.NameOf(TrackLap.Drunk), config => TrackPlan.Lay(config, TrackLap.Drunk)),
        (TrackPlan.NameOf(TrackLap.Fleet), config => TrackPlan.Lay(config, TrackLap.Fleet)),
        (ExamPlan.Name, ExamPlan.Lay),
        (FootwayPlan.Name, FootwayPlan.Lay),
        (SkidpadPlan.Name, SkidpadPlan.Lay),
        (IdlePlan.Name, IdlePlan.Lay),
    ];

    /// <summary>
    /// The two towns still carried as files rather than laid in code or generated from a brief: the
    /// fixture, and the crossings map. Neither may move when the generator does, which is why they are
    /// still files — and both are on their way to being laid in code.
    /// </summary>
    /// <remarks>
    /// <b>Named and not found.</b> Which maps exist is this class and never the <c>towns/</c> folder, so a
    /// map cannot appear because somebody left a file there and cannot vanish because one was moved: the
    /// day a fixture is laid in code, the name comes off this list and everything above carries on.
    /// </remarks>
    static readonly string[] Filed = [Fixture, "Zebras"];

    /// <summary>Every map there is, in name order: the briefs on disk, the maps laid in code, and the fixtures still carried as files.</summary>
    public static string[] Shipped()
    {
        var names = new List<string>(ProjectPaths.TownBriefs());
        foreach (var (name, _) in Laid) names.Add(name);
        names.AddRange(Filed);
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
    public static CityPlan Plan(string name, SimConfig config, ReadOnlySpan<Vector2> roofsM)
    {
        if (_kept is { } kept && kept.Is(name, config)) return kept.Plan;

        var plan = Lay(name, config, roofsM);

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

    static CityPlan Lay(string name, SimConfig config, ReadOnlySpan<Vector2> roofsM)
    {
        foreach (var (laid, lay) in Laid)
        {
            if (string.Equals(laid, name, StringComparison.Ordinal)) return lay(config);
        }

        if (IsGenerated(name)) return TownGenerator.Lay(Brief(name), config, roofsM);

        // And the two fixtures, off the list above rather than off whatever is in the folder: a name that
        // is not on it is not a map, even where a file of that name happens to be lying there.
        if (Array.IndexOf(Filed, name) >= 0) return TownReader.ReadFile(ProjectPaths.TownFile(name));

        throw new FileNotFoundException(
            $"No map called {name}: this build knows {string.Join(", ", Shipped())}.");
    }
}
