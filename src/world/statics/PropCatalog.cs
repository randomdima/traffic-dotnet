using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Statics;

/// <summary>
/// One prop look: the image, the ground its kind belongs on (GEN-6b), the size it is drawn at, and whether it has a
/// front to be turned by the bearing the plan laid it on.
/// </summary>
/// <param name="SizeM">
/// <b>What the picture covers seen from above, width by depth</b> (GEN-6e): a box's footprint, or a round look's
/// picture with its longest side the look's diameter and the other its aspect's.
/// </param>
/// <param name="Round">Whether the look was authored as a disc (<c>diameterM</c>) rather than as a box.</param>
/// <param name="ColliderM">The diameter of the disc it stands as a body, where the art states one apart from its girth.</param>
/// <param name="Share">How often it is drawn against the others of its kind (<see cref="PropVariantFile.Share"/>).</param>
internal readonly record struct PropVariant(
    string Id, int Kind, string SpritePath, Vector2 SizeM, bool Round, float? ColliderM, bool Turns, float Share = 1f)
{
    /// <summary>
    /// <b>The diameter of the disc the town clears ground for</b> (GEN-6e): a round look's diameter, the longest side of
    /// its picture, because a crown fills its disc and the picture's corners are bare; and a box's diagonal, so the
    /// picture stays inside the disc whichever way the plan's bearing turns it. Derived, and never authored.
    /// </summary>
    public float GirthM => Round ? MathF.Max(SizeM.X, SizeM.Y) : SizeM.Length();

    /// <summary>
    /// <b>The diameter of the disc the prop stands as a body</b> (GEN-6e): the art's collider, or the girth where it
    /// states none.
    /// </summary>
    public float BodyM => ColliderM ?? GirthM;
}

/// <summary>
/// The looks a prop can wear, read from <c>assets/…/Catalog.json</c>, in the order a plan names them by
/// (<see cref="Sizes"/>).
/// </summary>
/// <remarks>
/// <b>The plan chooses the look and the catalogue only draws it</b> (GEN-6e): a prop is laid at its look's own girth,
/// so the look is fixed before the prop is placed, and the plan carries it by its place in this catalogue.
/// </remarks>
internal sealed class PropCatalog
{
    PropCatalog(PropVariant[] variants) => Variants = variants;

    /// <summary>
    /// The looks, read once and the same for every town, on the terms <see cref="BuildingCatalog.Shared"/> is: what a
    /// town is laid with (<see cref="ShippedArt"/>).
    /// </summary>
    public static PropCatalog Shared { get; } = Load();

    public PropVariant[] Variants { get; }

    public int Count => Variants.Length;

    public static PropCatalog Load()
    {
        var catalogPath = Path.Combine(ProjectPaths.Assets, "world", "prop", "variants", "common", "Catalog.json");
        return new PropCatalog([.. AssetJson.Catalog(catalogPath).Select(Read)]);
    }

    /// <summary>
    /// <b>The looks a town lays its props as</b> (GEN-6e): each one's kind, girth, body and share, in this catalogue's
    /// order. Handed to whoever lays a town, because the plan may not read a catalogue that sits above it — the data
    /// crosses the seam and the type does not.
    /// </summary>
    public PropSizes Sizes() => new(
        [.. Variants.Select(variant => (PropKind)variant.Kind)], [.. Variants.Select(variant => variant.GirthM)],
        [.. Variants.Select(variant => variant.BodyM)], [.. Variants.Select(variant => variant.Share)]);

    static PropVariant Read(string path)
    {
        var variant = AssetJson.Read(path, PropVariantJson.Default.PropVariantFile);
        var spritePath = AssetJson.Beside(path, variant.Sprite);
        var (widthPx, heightPx) = ImageHeader.Measure(spritePath);
        return Of(variant, path, spritePath, (float)widthPx / heightPx);
    }

    /// <summary>
    /// One look as the file at <paramref name="path"/> states it, refused where it states its size as both a disc and a
    /// box, or as neither, a body wider than its girth, or a share of nothing (GEN-6e).
    /// </summary>
    /// <param name="aspect">
    /// The picture's width over its height, which a round look's size follows. <b>A box's is its footprint</b>, which
    /// <c>qq sprites use</c> writes off the same picture: its width authored and its depth measured.
    /// </param>
    internal static PropVariant Of(PropVariantFile variant, string path, string spritePath, float aspect)
    {
        var sizeM = (variant.DiameterM, variant.FootprintM) switch
        {
            ({ } diameterM, null) => new Vector2(aspect, 1f) * (diameterM / MathF.Max(aspect, 1f)),
            (null, [var widthM, var depthM]) => new Vector2(widthM, depthM),
            (null, { }) => throw new InvalidDataException($"{path}: footprintM is a width and a depth, two figures."),
            (null, null) => throw new InvalidDataException($"{path} states neither diameterM nor footprintM: the look has no size."),
            _ => throw new InvalidDataException($"{path} states both diameterM and footprintM: a look is round or a box."),
        };

        if (!(sizeM.X > 0f && sizeM.Y > 0f)) throw new InvalidDataException($"{path}: a look's size is more than nothing, and it is {sizeM} m.");

        // A look drawn never is a look the art should not have shipped (GEN-6b), and a negative weight is no weight at all.
        if (!(variant.Share > 0f)) throw new InvalidDataException($"{path}: share {variant.Share} is not more than nothing.");

        var look = new PropVariant(
            variant.Id, variant.Kind, spritePath, sizeM, variant.DiameterM is not null, variant.ColliderM, variant.Turns, variant.Share);

        // A body past the girth would stand in ground the town never cleared for it: in the next prop, or on the walk.
        if (!(look.BodyM > 0f && look.BodyM <= look.GirthM))
        {
            throw new InvalidDataException($"{path}: colliderM {look.BodyM} m is not within the look's girth of {look.GirthM:F2} m.");
        }

        return look;
    }
}
