using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// One car in the fleet: what it looks like, what it measures, and which wheels it drives through.
/// </summary>
/// <remarks>
/// Unlike a walker's look, a car's variant carries figures the model reads — the footprint it is drawn
/// and collided at, where its axles are, how wide its track is, and which end the drive is placed on.
/// The nominal car the junctions are sized against is nobody's actual car; these are the actual ones.
/// </remarks>
internal readonly record struct CarVariant(
    string Id, string SpritePath, string WreckSpritePath, Vector2 WreckScale, Vector2 FootprintM, float FrontAxleM,
    float RearAxleM, float HalfTrackM, int Drivetrain)
{
    public float WheelbaseM => FrontAxleM - RearAxleM;

    /// <summary>How far the drive is placed on the front axle, in the terms the tyre model spends it in.</summary>
    /// <remarks>The shipped fleet writes 0 for rear, 1 for front and 2 for all four; nothing else appears in it.</remarks>
    public float DrivenFrontShare => Drivetrain switch { 0 => 0f, 2 => 0.5f, _ => 1f };
}

/// <summary>
/// The fleet, read from <c>assets/…/Fleet.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// A car's sheet is one frame, nose along +x, which is why nothing here has the rows and columns
/// <see cref="Person.Body.PersonCatalog"/> needs: a car's heading is shown by turning the quad, because
/// the body itself turns and the art is drawn once.
/// </para>
/// <para>
/// A variant names a <see cref="WreckFile"/> being the wreck it becomes, cut from the same tile, so
/// breaking a car swaps one texture and moves nothing.
/// </para>
/// </remarks>
internal sealed class CarCatalog
{
    CarCatalog(CarVariant[] variants) => Variants = variants;

    public CarVariant[] Variants { get; }

    public int Count => Variants.Length;

    /// <summary>
    /// The fleet, read once. <b>It is data and not a service</b> — immutable, on disk, and the same for
    /// every town — and a town is stood up often enough that reading seventeen files each time would be a
    /// cost paid by every unit test that builds one.
    /// </summary>
    public static CarCatalog Shared { get; } = Load();

    /// <summary>
    /// Which end the variant at this index drives through. <b>The index wraps the way the sheets wrap</b>
    /// (see <c>CarSprites.Fill</c>): a town with more cars than the fleet has entries hands the seventeenth
    /// car the first variant, and it must be the first variant's drivetrain as well as its picture.
    /// </summary>
    public float DrivenFrontShareOf(int variant) => Variants[(variant % Count) + (variant < 0 ? Count : 0)].DrivenFrontShare;

    public static CarCatalog Load()
    {
        var fleetPath = Path.Combine(ProjectPaths.Assets, "agents", "car", "variants", "common", "Fleet.json");
        var entries = AssetJson.Catalog(fleetPath);

        var variants = new CarVariant[entries.Length];
        for (var entry = 0; entry < entries.Length; entry++) variants[entry] = ReadVariant(entries[entry]);

        return new CarCatalog(variants);
    }

    static CarVariant ReadVariant(string path)
    {
        var variant = AssetJson.Read(path, CarVariantJson.Default.CarVariantFile);
        var sprite = AssetJson.Beside(path, variant.Sprite);

        return new CarVariant(
            variant.Id, sprite,
            variant.Wreck is { } wreck ? AssetJson.Beside(path, wreck.Sprite) : sprite,
            variant.Wreck?.Scale ?? Vector2.One, variant.FootprintM, variant.FrontAxleM, variant.RearAxleM,
            variant.TrackM, variant.Drivetrain);
    }
}
