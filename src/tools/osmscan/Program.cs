namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>The scanner</b>: takes a city off OpenStreetMap into its traced map's survey (<see cref="Scan"/>), and
/// draws OSM's own lanes over a shot of it to hold the engine's against (<see cref="Draw"/>). Run by
/// <c>qq osm</c>.
/// </summary>
/// <remarks>
/// It is a tool of its own so that what OSM says is read by something that knows nothing of the engine's rules:
/// the survey it writes is OSM and OSM's conventions, and the engine lays what it is given.
/// </remarks>
internal static class Program
{
    const string Usage = """
        usage: osm-scan scan [--city odesa] [--refetch]
               osm-scan draw SHOT.png [--map OdesaOsm]
        """;

    static int Main(string[] args)
    {
        var root = Root();
        try
        {
            return args.FirstOrDefault() switch
            {
                "scan" => Scan.Run(root, Option(args, "--city") ?? "odesa", args.Contains("--refetch")),
                "draw" when args.Length > 1 => Draw.Run(root, Path.GetFullPath(args[1]), Option(args, "--map") ?? "OdesaOsm"),
                _ => Refuse(Usage),
            };
        }
        catch (Exception failure) when (failure is InvalidOperationException or InvalidDataException or ArgumentException or IOException)
        {
            return Refuse(failure.Message);
        }
    }

    static int Refuse(string why)
    {
        Console.Error.WriteLine(why);
        return 2;
    }

    static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    /// <summary>The project's root: the nearest directory up from here holding the engine's project.</summary>
    static string Root()
    {
        for (var at = new DirectoryInfo(Directory.GetCurrentDirectory()); at is not null; at = at.Parent)
        {
            if (File.Exists(Path.Combine(at.FullName, "traffic-dotnet.csproj"))) return at.FullName;
        }

        throw new InvalidOperationException("not inside the traffic-dotnet project");
    }
}
