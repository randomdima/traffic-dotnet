using System.Collections.Concurrent;
using TrafficSimulation.CityGen.Exam;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Every map this build can open, and the one place a name becomes a town.</b> A town comes from its map's own file
/// (<see cref="TownMap"/>, GEN-58) and is laid off it when it is asked for (<see cref="TownPlan"/>); a map laid to measure
/// one thing comes from the code that lays it.
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
    /// The map every detailed check is staged on: a small town of its own — water, districts and a dozen cars, and no
    /// building, so no car park, crossing or walker (the known gaps in <c>docs/index.md</c>). <b>Named here because this
    /// is where a name becomes a town</b> — the suite, the warm-up and the command line's own default all mean this one
    /// map, and three spellings of it is two chances to mean a different one.
    /// </summary>
    public const string Fixture = "Test";

    /// <summary>
    /// The maps laid in code: the ring the start menu stands over (GEN-1b), the same ring showing the fleet's
    /// looks (<see cref="ShowroomPlan"/>), and the scenario map — a lattice with one traffic scenario staged at
    /// each of its junctions (<see cref="ExamPlan"/>).
    /// </summary>
    static readonly (string Name, Func<SimConfig, ArtSizes, CityPlan> Lay)[] Laid =
    [
        (IdlePlan.Name, static (config, _) => IdlePlan.Lay(config)),
        (ShowroomPlan.Name, static (config, art) => ShowroomPlan.Lay(config, art.Buildings)),
        (ExamPlan.Name, static (config, _) => ExamPlan.Lay(config)),
    ];

    /// <summary>
    /// Every map the probes and sweeps ask questions of, in name order — the map files in <c>towns/</c> and the
    /// exam laid in code. <b>A map is a file or code</b> (GEN-58): every town is its map's file, whatever
    /// authored it.
    /// </summary>
    /// <remarks>
    /// <b>The idle ring and the showroom are laid but not shipped</b>: each is a picture rather than a town to ask
    /// questions of, so it is swept by nobody. The showroom is still offered (<see cref="Offered"/>).
    /// </remarks>
    public static string[] Shipped()
    {
        var names = new List<string>(ProjectPaths.MapFiles()) { ExamPlan.Name };
        names.Sort(StringComparer.Ordinal);
        return [.. names];
    }

    /// <summary>
    /// Every map a person is offered to open — the start menu's list and the command line's — which is the shipped
    /// ones and the showroom: a picture to look at, so it is offered, and swept by nobody.
    /// </summary>
    public static string[] Offered()
    {
        var names = new List<string>(Shipped()) { ShowroomPlan.Name };
        names.Sort(StringComparer.Ordinal);
        return [.. names];
    }

    /// <summary>
    /// <b>Whether a map is content rather than code</b> — a file, which a build may ship any number of without that being
    /// a change to the engine — which is what decides that only <c>Tier.Maps</c> asks questions of it.
    /// </summary>
    public static bool IsCity(string name) => File.Exists(ProjectPaths.MapFile(name));

    /// <summary>
    /// A map's own file (<see cref="TownMap"/>), read whole each time it is asked for: it is milliseconds, and a town is
    /// laid off it once (<see cref="Plan"/>), so a copy kept would only be one more city in memory.
    /// </summary>
    public static TownMap Read(string name) => TownMap.Read(ProjectPaths.MapFile(name));

    /// <summary>
    /// What a map says it is, off the head of its file (<see cref="MapHead"/>): the menu's question is one line of a
    /// file whose whole is hundreds of kilobytes for a city. Read once a name.
    /// </summary>
    public static string Description(string name) =>
        Descriptions.GetOrAdd(name, static map => MapHead.Description(ProjectPaths.MapFile(map)));

    static readonly ConcurrentDictionary<string, string> Descriptions = new();

    /// <summary>
    /// <b>The town itself, laid once for as long as it is the town being asked about.</b> A name that is
    /// neither a map file nor a laid map is a failure here rather than an empty town somewhere downstream —
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
    /// The prefabs a town stands its buildings as, the roofs its services wear and the looks its props are laid as, read
    /// off the art by the catalogues above this slice and handed down as data (<see cref="ArtSizes"/>, GEN-54, GEN-58,
    /// GEN-6e). A map laid in code stands what its own code stands, which only the showroom's does off these.
    /// </param>
    public static CityPlan Plan(string name, SimConfig config, ArtSizes sizes) => Planning(name, config, sizes).Through();

    /// <summary><see cref="Plan"/> a stage at a time (<see cref="TownPlan.Reading"/>): a map laid in code is one block.</summary>
    public static Staged<CityPlan> Planning(string name, SimConfig config, ArtSizes sizes) =>
        new(made => Planning(name, config, sizes, made));

    static IEnumerable<string> Planning(string name, SimConfig config, ArtSizes sizes, Action<CityPlan> made)
    {
        if (_kept is { } kept && kept.Is(name, config))
        {
            made(kept.Plan);
            yield break;
        }

        var laying = Lay(name, config, sizes);
        foreach (var stage in laying.Stages) yield return stage;

        // A reference is written whole, so a reader takes the town before or the town after and never half
        // of either. Two callers laying the same map at once lay it twice, which is what they did anyway.
        _kept = new Kept(name, config, laying.Made);
        made(laying.Made);
    }

    /// <summary>The town most recently laid, against the name and the figures it was laid from.</summary>
    sealed record Kept(string Name, SimConfig Config, CityPlan Plan)
    {
        public bool Is(string name, SimConfig config) =>
            ReferenceEquals(Config, config) && string.Equals(Name, name, StringComparison.Ordinal);
    }

    static Kept? _kept;

    static Staged<CityPlan> Lay(string name, SimConfig config, ArtSizes sizes)
    {
        foreach (var (laid, lay) in Laid)
        {
            if (string.Equals(laid, name, StringComparison.Ordinal)) return Staged<CityPlan>.Whole(() => lay(config, sizes));
        }

        if (IsCity(name)) return TownPlan.Reading(ProjectPaths.MapFile(name), config, sizes);

        throw new FileNotFoundException(
            $"No map called {name}: this build knows {string.Join(", ", Shipped())}.");
    }
}
