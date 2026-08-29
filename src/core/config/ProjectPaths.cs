namespace TrafficSimulation.Core.Config;

/// <summary>
/// Where <c>assets/</c> and <c>towns/</c> are. Found by walking up from wherever the binary landed
/// rather than by assuming a working directory — a run from an IDE, from <c>dotnet run</c> and from
/// <c>bin/Debug/net10.0/</c> all start somewhere different.
/// </summary>
internal static class ProjectPaths
{
    public static string Root { get; } = FindRoot();

    public static string Assets => Path.Combine(Root, "assets");

    public static string Towns => Path.Combine(Root, "towns");

    /// <summary>The tunable figures, read once at startup and injected from the composition root.</summary>
    public static string SharedFiguresFile => Path.Combine(Assets, "shared", "config", "SimConfig.json");

    public static string TownFile(string map) => Path.Combine(Towns, map + ".town");

    /// <summary>
    /// The five ground surfaces in the order <c>Surface</c> indexes them: grass, tarmac, pavement,
    /// deck, water. Two file names mean the opposite of what they look like — <c>pavement.png</c> is
    /// the dark asphalt of the carriageway and <c>sidewalk.png</c> is the light paved walk.
    /// </summary>
    public static string[] GroundSurfaceFiles()
    {
        var surfaces = Path.Combine(Assets, "world", "terrain", "ground", "surfaces");
        return
        [
            Path.Combine(surfaces, "grass.png"),
            Path.Combine(surfaces, "pavement.png"),
            Path.Combine(surfaces, "sidewalk.png"),
            Path.Combine(surfaces, "bridge.png"),
            Path.Combine(surfaces, "water.png"),
        ];
    }

    /// <summary>The two signal head strips, car then pedestrian — one frame dark and one per lit lens.</summary>
    public static string[] SignalHeadFiles()
    {
        var heads = Path.Combine(Assets, "agents", "trafficlight", "heads");
        return [Path.Combine(heads, "car_light.png"), Path.Combine(heads, "pedestrian_light.png")];
    }

    /// <summary>
    /// One pitch of tread, shared by every car: a wheel is this laid several times along its own roll.
    /// There is no second tyre for a driven wheel — the drivetrain shows in behaviour, not in rubber.
    /// </summary>
    public static string TreadFile() =>
        Path.Combine(Assets, "agents", "car", "variants", "common", "tire_tread.png");

    /// <summary>
    /// Every lit lamp in the town, in one sheet: a row a variant, two columns a lens, each cell that
    /// variant's own bodywork cut out and driven emissive (CAR-14a). Cut by <c>--lamps</c> and committed
    /// beside the sprites it came from; an unlit lamp is not here, because it is the sprite itself.
    /// </summary>
    public static string LampAtlasFile() =>
        Path.Combine(Assets, "agents", "car", "variants", "common", "lamp_atlas.png");

    /// <summary>The map list is the <c>towns/</c> folder itself, so a map cannot be shipped and unlisted.</summary>
    public static string[] ShippedMaps()
    {
        var maps = Directory.GetFiles(Towns, "*.town");
        for (var i = 0; i < maps.Length; i++) maps[i] = Path.GetFileNameWithoutExtension(maps[i]);
        Array.Sort(maps, StringComparer.Ordinal);
        return maps;
    }

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
