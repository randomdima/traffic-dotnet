using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Person.Body;

/// <summary>One look: a sheet and the height it is drawn at. Only the look is data.</summary>
/// <remarks>
/// Walk speed, turn rate and the collision circle come from <see cref="SimConfig"/> for
/// everyone — a variant that could carry its own would be a second place a walker's pace is decided.
/// </remarks>
internal readonly record struct PersonVariant(string Id, string SheetPath, float HeightM);

/// <summary>
/// The looks a walker can be drawn as, read from <c>assets/…/Catalog.json</c>, and the uniforms beside
/// them from <c>Service.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every sheet is 8 facing rows × 8 walk-cycle columns, anticlockwise from up, and every facing is drawn.
/// It is the same fact for every variant, so it is a constant here rather than a field per look. <b>A
/// body down in the road is drawn off the same sheet</b> (PER-18): its stance, reddened.
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
        _paramedicLooks = UniformsNamed(ParamedicIdPrefix);
        _policeLooks = UniformsNamed(PoliceIdPrefix);
        _recoveryLooks = UniformsNamed(RecoveryIdPrefix);

        int[] UniformsNamed(string prefix)
        {
            var looks = new List<int>();
            for (var entry = walkers; entry < variants.Length; entry++)
            {
                if (variants[entry].Id.StartsWith(prefix, StringComparison.Ordinal)) looks.Add(entry);
            }

            return looks.Count > 0
                ? [.. looks]
                : throw new InvalidDataException($"Service.json names no variant whose id starts '{prefix}'.");
        }
    }

    /// <summary>The walkers first, then the uniforms — one array, because a sheet slot is a sheet slot.</summary>
    public PersonVariant[] Variants { get; }

    /// <summary>
    /// <b>How many looks a town's walkers are drawn from</b>, which is the ordinary ones and not
    /// everything here: handing an ordinary pedestrian a paramedic's jacket would put a crew on the
    /// pavement (SRV-3a).
    /// </summary>
    public int Count { get; }

    /// <summary>And how many looks there are altogether, which is what the sheet list is laid for.</summary>
    public int SheetCount => Variants.Length;

    /// <summary>
    /// <b>The paramedic look the <paramref name="nth"/> member of an ambulance crew wears</b> (SRV-3a): every
    /// service-list look whose id starts <see cref="ParamedicIdPrefix"/>, in the list's order, in turn — as
    /// <see cref="PoliceLook"/> and <see cref="RecoveryLook"/> are a police car's and an evacuator's.
    /// </summary>
    public int ParamedicLook(int nth) => _paramedicLooks[nth % _paramedicLooks.Length];

    public int PoliceLook(int nth) => _policeLooks[nth % _policeLooks.Length];

    public int RecoveryLook(int nth) => _recoveryLooks[nth % _recoveryLooks.Length];

    readonly int[] _paramedicLooks;
    readonly int[] _policeLooks;
    readonly int[] _recoveryLooks;

    /// <summary>What a paramedic look's id starts with: the service list says which looks a crew has, by naming them.</summary>
    const string ParamedicIdPrefix = "paramedic";

    const string PoliceIdPrefix = "police";

    const string RecoveryIdPrefix = "recovery";

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
    /// Which column, stepped by <b>distance walked</b> and never by time, so whatever slows a walker — a
    /// queue, a shove — slows its stride with it.
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
