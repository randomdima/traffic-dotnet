using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// <b>Which variant files the fleet is read from</b>: <c>Fleet.json</c>'s, or the looks a run was told to
/// draw its traffic from instead (<c>--fleet</c>) — art to be looked at before it is put in the shipped list.
/// </summary>
/// <remarks>
/// <para>
/// <b>Told once, before anything reads it.</b> The catalogue is read once a process (<see cref="CarCatalog.Shared"/>)
/// and the renderer reads its own copy, so a fleet told after either has read one is two fleets in one run; the
/// second telling throws rather than leaving that behind. This is its own type and not a member of the catalogue
/// because touching any static of <see cref="CarCatalog"/> reads the shipped fleet.
/// </para>
/// <para>
/// <b>A told fleet is named by id</b>, each the variant file at <c>variants/&lt;id&gt;/&lt;id&gt;.json</c> as every
/// shipped one is, and it replaces the fleet and nothing else: the service vehicles are still
/// <c>Service.json</c>'s.
/// </para>
/// </remarks>
internal static class FleetList
{
    static string[]? _told;
    static bool _read;

    /// <summary>Whether this run's fleet is a told one rather than the shipped list.</summary>
    public static bool IsTold => _told is not null;

    /// <summary>
    /// The lamp sheet a told fleet is drawn with, cut from its own sprites by whoever told it
    /// (<c>LampAtlasBake</c>, which a page and a handset do not carry): the committed sheet's rows are the
    /// shipped catalogue's looks.
    /// </summary>
    public static string? LampAtlasFile { get; private set; }

    public static void LampsCutTo(string file)
    {
        if (!IsTold) throw new InvalidOperationException("The shipped fleet's lamp sheet is the committed one.");

        LampAtlasFile = file;
    }

    public static void Tell(string[] ids)
    {
        if (_read) throw new InvalidOperationException("The fleet was told after it had been read: tell it first.");

        _told = FilesOf(ids);
    }

    /// <summary>The variant files the fleet is read from, in the order its looks are handed out.</summary>
    public static string[] Files()
    {
        _read = true;
        return _told ?? AssetJson.Catalog(Path.Combine(Variants, "common", "Fleet.json"));
    }

    /// <summary>
    /// Each id's own variant file, refused when there is none: a look misspelt on a command line is a run that
    /// fails at once and not a fleet one car short.
    /// </summary>
    public static string[] FilesOf(string[] ids)
    {
        if (ids.Length == 0) throw new ArgumentException("A told fleet names at least one look.");

        var files = new string[ids.Length];
        for (var at = 0; at < ids.Length; at++)
        {
            files[at] = Path.Combine(Variants, ids[at], ids[at] + ".json");
            if (!File.Exists(files[at]))
                throw new FileNotFoundException($"No car variant {ids[at]}: expected {files[at]}.", files[at]);
        }

        return files;
    }

    static string Variants => Path.Combine(ProjectPaths.Assets, "agents", "car", "variants");
}
