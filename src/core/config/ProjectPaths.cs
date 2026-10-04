namespace TrafficSimulation.Core.Config;

/// <summary>
/// Where <c>assets/</c> and <c>towns/</c> are. Found by walking up from wherever the binary landed
/// rather than by assuming a working directory — a run from an IDE, from <c>dotnet run</c> and from
/// <c>bin/Debug/net10.0/</c> all start somewhere different.
/// </summary>
internal static class ProjectPaths
{
    static string? _root;

    public static string Root => _root ??= FindRoot();

    /// <summary>
    /// Where the town's files are, <b>told rather than found</b>. The handset's, and nothing else's: an
    /// APK is a zip and not a tree, so that head unpacks <c>assets/</c> and <c>towns/</c> into the app's
    /// own folder and says where it put them (AND-3). Called once, before anything reads a path — which
    /// is what keeps there being one asset story rather than a provider threaded through fifteen call
    /// sites.
    /// </summary>
    public static void FoundAt(string root) => _root = root;

    public static string Assets => Path.Combine(Root, "assets");

    public static string Towns => Path.Combine(Root, "towns");

    /// <summary>The tunable figures, read once at startup and injected from the composition root.</summary>
    public static string SharedFiguresFile => Path.Combine(Assets, "shared", "config", "SimConfig.json");

    /// <summary>What a city is authored as: a seed and the intent, from which the town itself is generated.</summary>
    public static string TownBriefFile(string map) => Path.Combine(Towns, map + ".json");

    /// <summary>Every brief in <c>towns/</c>, by name — the cities this build can lay.</summary>
    public static string[] TownBriefs()
    {
        var briefs = Directory.GetFiles(Towns, "*.json");
        for (var at = 0; at < briefs.Length; at++) briefs[at] = Path.GetFileNameWithoutExtension(briefs[at]);
        Array.Sort(briefs, StringComparer.Ordinal);
        return briefs;
    }

    /// <summary>
    /// What a traced map is authored as: a real place's roads, sea and what else is known of it, in one file of its
    /// own (GEN-57) — the only file of the place the engine reads. What it was imported from sits beside it, in a
    /// folder of the map's name, and is never read when a map is opened.
    /// </summary>
    public static string TracedMapFile(string map) => Path.Combine(Towns, "traced", map + TracedMapKind);

    const string TracedMapKind = ".map";

    /// <summary>
    /// Every traced map in <c>towns/traced/</c>, by name. <b>None where the folder is not there</b>, which is the
    /// browser head's runtime when it is handed the briefs alone (WEB-4).
    /// </summary>
    public static string[] TracedMaps()
    {
        var folder = Path.Combine(Towns, "traced");
        if (!Directory.Exists(folder)) return [];

        var maps = Directory.GetFiles(folder, "*" + TracedMapKind);
        for (var at = 0; at < maps.Length; at++) maps[at] = Path.GetFileNameWithoutExtension(maps[at]);
        Array.Sort(maps, StringComparer.Ordinal);
        return maps;
    }

    /// <summary>
    /// What a sheet is stored as. WebP, and lossy wherever a sheet could take it without moving an
    /// opaque pixel more than a little — the art is continuous-tone and PNG stores it at four times
    /// the size, which is thirty megabytes a browser waits on before the first frame.
    /// </summary>
    /// <remarks>
    /// <b>Nothing reads this to decide how to decode.</b> ImageSharp takes a file by its header, so a
    /// sheet stored either way loads without being told which; this is here so the names spelled out
    /// below are spelled once. The sheets a variant file names carry their own extension in that file,
    /// and <c>qq art</c> is what changes both together.
    /// </remarks>
    public const string Sheet = ".webp";

    /// <summary>
    /// The five ground surfaces in the order <c>Surface</c> indexes them: grass, tarmac, pavement,
    /// deck, water. Two file names mean the opposite of what they look like — <c>pavement</c> is
    /// the dark asphalt of the carriageway and <c>sidewalk</c> is the light paved walk.
    /// </summary>
    public static string[] GroundSurfaceFiles()
    {
        var surfaces = Path.Combine(Assets, "world", "terrain", "ground", "surfaces");
        return
        [
            Path.Combine(surfaces, "grass" + Sheet),
            Path.Combine(surfaces, "pavement" + Sheet),
            Path.Combine(surfaces, "sidewalk" + Sheet),
            Path.Combine(surfaces, "bridge" + Sheet),
            Path.Combine(surfaces, "water" + Sheet),
        ];
    }

    /// <summary>The two signal head strips, car then pedestrian — one frame dark and one per lit lens.</summary>
    public static string[] SignalHeadFiles()
    {
        var heads = Path.Combine(Assets, "agents", "trafficlight", "heads");
        return [Path.Combine(heads, "car_light" + Sheet), Path.Combine(heads, "pedestrian_light" + Sheet)];
    }

    /// <summary>
    /// One pitch of tread, shared by every car: a wheel is this laid several times along its own roll.
    /// There is no second tyre for a driven wheel — the drivetrain shows in behaviour, not in rubber.
    /// </summary>
    public static string TreadFile() =>
        Path.Combine(Assets, "agents", "car", "variants", "common", "tire_tread" + Sheet);

    /// <summary>
    /// Every lit lamp in the town, in one sheet: a row a variant, two columns a lens, each cell that
    /// variant's own bodywork cut out and driven emissive (CAR-14a). Cut by <c>--lamps</c> and committed
    /// beside the sprites it came from; an unlit lamp is not here, because it is the sprite itself.
    /// </summary>
    public static string LampAtlasFile() =>
        Path.Combine(Assets, "agents", "car", "variants", "common", "lamp_atlas" + Sheet);

    static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "towns")) &&
                Directory.Exists(Path.Combine(dir.FullName, "assets")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"No project root above {AppContext.BaseDirectory}: expected a folder holding both towns/ and assets/.");
    }
}
