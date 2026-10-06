namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>The scanner</b>: takes a city off OpenStreetMap into its traced map's survey (<see cref="Scan"/>), imports the
/// map the engine reads off it (<see cref="Meta.Import"/>), edits that map (<see cref="Crop"/>), and draws OSM's own
/// lanes over a shot of it to hold the engine's against (<see cref="Draw"/>). Run by <c>qq osm</c>.
/// </summary>
/// <remarks>
/// It is a tool of its own so that what OSM says is read by something that knows nothing of the engine's rules:
/// the survey it writes is OSM and OSM's conventions, and the engine lays what it is given.
/// </remarks>
internal static class Program
{
    const string Usage = """
        usage: osm-scan scan [--city odesa] [--refetch]
               osm-scan meta [--city odesa] [--refetch] [--fetch-only] [--skip SOURCE,...]
               osm-scan meta-check [--map OdesaOsm]
               osm-scan import [--map OdesaOsm] [--force]
               osm-scan crop --box S,W,N,E [--map OdesaOsm]
               osm-scan stumps [--map OdesaOsm] [--dry]
               osm-scan zones [--map OdesaOsm]
               osm-scan upgrade [--map OdesaOsm]
               osm-scan population [--people N] [--cars N] [--map OdesaOsm]
               osm-scan meta-draw OUT.png --at LAT,LON [--span 400] [--map OdesaOsm]
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
                "meta" => Meta.Enrichment.Run(root, Option(args, "--city") ?? "odesa", args.Contains("--refetch"), args.Contains("--fetch-only"),
                    (Option(args, "--skip") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)),
                "meta-check" => Meta.Audit.Run(root, Option(args, "--map") ?? "OdesaOsm"),
                "import" => Meta.Import.Run(root, Option(args, "--map") ?? "OdesaOsm", args.Contains("--force")),
                "crop" when Option(args, "--box") is { } box => Crop.Run(root, Option(args, "--map") ?? "OdesaOsm", box),
                "stumps" => Stumps.Run(root, Option(args, "--map") ?? "OdesaOsm", args.Contains("--dry")),
                "zones" => Zoning.Run(root, Option(args, "--map") ?? "OdesaOsm"),
                "upgrade" => Upgrade.Run(root, Option(args, "--map") ?? "OdesaOsm"),
                "population" => Population.Run(root, Option(args, "--map") ?? "OdesaOsm", Count(args, "--people"), Count(args, "--cars")),
                "meta-draw" when args.Length > 1 && Option(args, "--at") is { } at => Meta.Picture.Run(root, Option(args, "--map") ?? "OdesaOsm",
                    double.Parse(at.Split(',')[0], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(at.Split(',')[1], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(Option(args, "--span") ?? "400", System.Globalization.CultureInfo.InvariantCulture), Path.GetFullPath(args[1])),
                "draw" when args.Length > 1 => Draw.Run(root, Path.GetFullPath(args[1]), Option(args, "--map") ?? "OdesaOsm"),
                _ => Refuse(Usage),
            };
        }
        catch (Exception failure) when (failure is InvalidOperationException or InvalidDataException or ArgumentException or IOException or FormatException)
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

    static int? Count(string[] args, string name) =>
        Option(args, name) is { } count ? int.Parse(count, System.Globalization.CultureInfo.InvariantCulture) : null;

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
