using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Person.Body;

/// <summary>One look: a sheet and the height it is drawn at. Only the look is data.</summary>
/// <remarks>
/// Walk speed, turn rate and the collision circle come from <see cref="Shared.Config.SimConfig"/> for
/// everyone — a variant that could carry its own would be a second place a walker's pace is decided.
/// </remarks>
internal readonly record struct PersonVariant(string Id, string SheetPath, float HeightM);

/// <summary>
/// The looks a walker can be drawn as, read from <c>assets/…/Catalog.json</c>.
/// </summary>
/// <remarks>
/// Every sheet is 8 facing rows × 8 walk-cycle columns, anticlockwise from up, every facing drawn and
/// nothing mirrored. It is the same fact for every variant, so it is a constant here rather than a
/// field per look.
/// </remarks>
internal sealed class PersonCatalog
{
    public const int FacingRows = 8;

    public const int WalkColumns = 8;

    PersonCatalog(PersonVariant[] variants) => Variants = variants;

    public PersonVariant[] Variants { get; }

    public int Count => Variants.Length;

    public static PersonCatalog Load()
    {
        var catalogPath = Path.Combine(ProjectPaths.Assets, "agents", "person", "variants", "common", "Catalog.json");
        var entries = AssetJson.Catalog(catalogPath);

        var variants = new PersonVariant[entries.Length];
        for (var entry = 0; entry < entries.Length; entry++) variants[entry] = ReadVariant(entries[entry]);

        return new PersonCatalog(variants);
    }

    /// <summary>
    /// Which row of the sheet a heading is drawn in. The rows run <b>anticlockwise from up</b>, and
    /// "up" is negative y because this world's y grows downwards.
    /// </summary>
    public static int FacingRow(float headingRad)
    {
        var octant = (int)MathF.Round((-MathF.PI * 0.5f - headingRad) / (MathF.PI * 0.25f));
        return ((octant % FacingRows) + FacingRows) % FacingRows;
    }

    /// <summary>
    /// Which column, stepped by <b>distance walked</b> and never by time, so ground that slows a
    /// walker (TER-2) slows its stride with it.
    /// </summary>
    /// <param name="strideCycleM">How far one whole eight-frame cycle covers.</param>
    public static int WalkColumn(float distanceWalkedM, float strideCycleM)
    {
        if (strideCycleM <= 0f) return 0;

        var cycles = distanceWalkedM / strideCycleM;
        var column = (int)MathF.Floor((cycles - MathF.Floor(cycles)) * WalkColumns);
        return Math.Clamp(column, 0, WalkColumns - 1);
    }

    static PersonVariant ReadVariant(string path)
    {
        var variant = AssetJson.Read(path, PersonVariantJson.Default.PersonVariantFile);
        return new PersonVariant(variant.Id, AssetJson.Beside(path, variant.Sheet), variant.HeightM);
    }
}
