using TrafficSimulation.Core.Config;

namespace TrafficSimulation.App.Hud;

/// <summary>What a shipped map is for, which is what decides the page of the menu it appears on.</summary>
internal enum MapKind
{
    /// <summary>A town somebody plays: the first page of the menu.</summary>
    Place,

    /// <summary>
    /// A town laid to put one behaviour under a microscope, and the fixture every detailed check is
    /// staged on. Still an ordinary map in every way that matters — the ordinary game, camera and
    /// agents — and behind its own submenu so that a menu of two cities does not read as a menu of
    /// two cities and a laboratory.
    /// </summary>
    Scenario,
}

/// <summary>One row of the map list.</summary>
internal readonly record struct MapEntry(string Name, MapKind Kind, string Description);

/// <summary>
/// The list of maps is one list: the start menu reads it and so does the command line, so a map that
/// can be opened one way can be opened the other.
/// </summary>
/// <remarks>
/// What is on disk is the authority for <em>which</em> maps exist —
/// <see cref="ProjectPaths.ShippedMaps"/> reads the folder, so a map cannot be shipped and unlisted —
/// and this catalogue is the authority for what each one <em>is</em>. The unit suite guards the pair in
/// both directions. The generated town has no row because the generator is not written, and a menu
/// entry that opened nothing would be worse than its absence.
/// </remarks>
internal static class MapCatalogue
{
    /// <summary>
    /// Every map this engine knows what to say about. A shipped file with no entry here is a failure
    /// of the suite rather than a row the menu quietly invents a description for.
    /// </summary>
    static readonly MapEntry[] Known =
    [
        new("Odesa", MapKind.Place, "A coastal city: two grids on their own bearings inside an orbital ring"),
        new("River", MapKind.Place, "A river city: five bridges, a street down each bank, an orbital through every way out"),
        new("Test", MapKind.Scenario, "The fixture map: one screen, one of every kind of ground, furnished thinly"),
        new("Zebras", MapKind.Scenario, "Five isolated streets with a crossing on each, one walker apiece"),
        new("Track", MapKind.Scenario, "The proving ground: one lap of five shapes, six cars, fifteen people pacing the road"),
        new("Drunk", MapKind.Scenario, "The same lap with the fifteen reeling down the road instead of pacing across it"),
    ];

    /// <summary>The shipped maps in menu order, places first — read off the folder and described from the catalogue.</summary>
    public static MapEntry[] Shipped()
    {
        var names = ProjectPaths.ShippedMaps();
        var entries = new MapEntry[names.Length];
        for (var map = 0; map < names.Length; map++) entries[map] = Describe(names[map]);

        Array.Sort(entries, (a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : string.CompareOrdinal(a.Name, b.Name));
        return entries;
    }

    public static MapEntry[] On(MapKind kind) => Array.FindAll(Shipped(), entry => entry.Kind == kind);

    /// <summary>What the catalogue says about a map, or a row that names the gap rather than hiding it.</summary>
    public static MapEntry Describe(string name)
    {
        foreach (var entry in Known)
        {
            if (string.Equals(entry.Name, name, StringComparison.Ordinal)) return entry;
        }

        return new MapEntry(name, MapKind.Scenario, "Shipped but undescribed: add it to MapCatalogue");
    }

    /// <summary>The catalogue's own rows, for the test that guards it against the folder in both directions.</summary>
    public static ReadOnlySpan<MapEntry> Catalogued => Known;
}
