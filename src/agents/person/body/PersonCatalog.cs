using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Person.Body;

/// <summary>One look: a sheet, the one frame it lies down as, and the height it is drawn at. Only the look is data.</summary>
/// <remarks>
/// Walk speed, turn rate and the collision circle come from <see cref="Shared.Config.SimConfig"/> for
/// everyone — a variant that could carry its own would be a second place a walker's pace is decided.
/// </remarks>
/// <param name="DownSheetPath">
/// <b>A body in the road is one picture and not a grid</b> (PER-18): the walk sheet draws all eight
/// facings because a standing body is drawn upright whichever way it faces, and a body lying along the
/// ground is a shape with a direction — so it is drawn the way a car is, one frame turned to its heading.
/// </param>
internal readonly record struct PersonVariant(string Id, string SheetPath, string DownSheetPath, float HeightM);

/// <summary>
/// The looks a walker can be drawn as, read from <c>assets/…/Catalog.json</c>, and the uniforms beside
/// them from <c>Service.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every sheet is 8 facing rows × 8 walk-cycle columns, anticlockwise from up, and every facing is drawn.
/// It is the same fact for every variant, so it is a constant here rather than a field per look. <b>Every
/// look has a second sheet</b>, which is the one frame it is drawn as once it is down
/// (<see cref="PersonVariant.DownSheetPath"/>).
/// </para>
/// <para>
/// <b>Two lists and one array</b>, on <see cref="Car.Body.CarCatalog"/>'s terms and for its reason: a
/// town's walkers are drawn by wrapping an index over <see cref="Count"/>, and a uniform is worn only by
/// somebody named to wear it (SRV-3a), so its picture has to be somewhere that wrap cannot reach.
/// </para>
/// </remarks>
internal sealed class PersonCatalog
{
    public const int FacingRows = 8;

    public const int WalkColumns = 8;

    PersonCatalog(PersonVariant[] variants, int walkers)
    {
        Variants = variants;
        Count = walkers;
        Paramedic = IndexOf("paramedic_red");
        Police = IndexOf("police_navy");
        Recovery = IndexOf("recovery_yellow");

        int IndexOf(string id)
        {
            for (var entry = walkers; entry < variants.Length; entry++)
            {
                if (variants[entry].Id == id) return entry;
            }

            throw new InvalidDataException($"Service.json names no variant with the id '{id}'.");
        }
    }

    /// <summary>The walkers first, then the uniforms — one array, because a sheet slot is a sheet slot.</summary>
    public PersonVariant[] Variants { get; }

    /// <summary>
    /// <b>How many looks a town's walkers are drawn from</b>, which is the ordinary ones and not
    /// everything here: handing the seventeenth pedestrian a paramedic's jacket would put a crew on the
    /// pavement (SRV-3a).
    /// </summary>
    public int Count { get; }

    /// <summary>And how many looks there are altogether, which is what the sheet list is laid for.</summary>
    public int SheetCount => Variants.Length;

    /// <summary>Which uniform an ambulance's crew wears, a police car's, and an evacuator's (SRV-3a).</summary>
    public int Paramedic { get; }

    public int Police { get; }

    public int Recovery { get; }

    /// <summary>
    /// The looks, read once. <b>It is data and not a service</b> — immutable, on disk, and the same for
    /// every town — on <see cref="Car.Body.CarCatalog.Shared"/>'s terms.
    /// </summary>
    public static PersonCatalog Shared { get; } = Load();

    public static PersonCatalog Load()
    {
        var walkers = AssetJson.Catalog(VariantList("Catalog.json"));
        var service = AssetJson.Catalog(VariantList("Service.json"));

        var variants = new PersonVariant[walkers.Length + service.Length];
        for (var entry = 0; entry < walkers.Length; entry++) variants[entry] = ReadVariant(walkers[entry]);
        for (var entry = 0; entry < service.Length; entry++) variants[walkers.Length + entry] = ReadVariant(service[entry]);

        return new PersonCatalog(variants, walkers.Length);
    }

    static string VariantList(string file) =>
        Path.Combine(ProjectPaths.Assets, "agents", "person", "variants", "common", file);

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
        return new PersonVariant(
            variant.Id, AssetJson.Beside(path, variant.Sheet), AssetJson.Beside(path, variant.Down), variant.HeightM);
    }
}
