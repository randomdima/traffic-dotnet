using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Statics;

/// <summary>One rectangle of a roof's walls, in the picture's own axes.</summary>
internal readonly record struct BuildingPart(Vector2 AtM, Vector2 SizeM);

/// <summary>One roof: the image, the footprint it was drawn at, and the rectangles it is built of.</summary>
/// <remarks>
/// The art is drawn <b>door at +y</b> — the porch at the bottom of the image, since this world's y grows
/// downwards — which is the one fact about a building's picture the town has to know: everything else a
/// building does is its plan's box, its plan's ways in, and <see cref="PartsM"/>.
/// </remarks>
/// <param name="SpritePath">The picture; null for a prefab not yet drawn, which is drawn as a plain block of its look.</param>
/// <param name="PartsM">
/// <b>What this roof is collided as</b> (OBJ-5a), measured off the picture in the picture's own axes and
/// scaled with it. Empty for a roof that is the whole of its footprint.
/// </param>
/// <param name="CornerRadiusM">The radius the footprint's corners are rounded at, which it is drawn and collided with (OBJ-2).</param>
/// <param name="Look">What a prefab is drawn as (GEN-57); null for a roof that is not a prefab.</param>
internal readonly record struct BuildingVariant(
    string Id, string? SpritePath, Vector2 FootprintM, BuildingPart[] PartsM, float CornerRadiusM, BuildingLook? Look);

/// <summary>
/// The roofs a building can wear, read from <c>assets/…/Catalog.json</c>, the civic ones beside them
/// from <c>Civic.json</c>, and the prefabs a traced town's footprints are fitted from (GEN-57) from
/// <c>assets/world/building/prefabs/Prefabs.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// An ordinary roof is picked by a rule and never by a draw: the nearest authored footprint in the art's
/// own axes, turned so its door lands on the wall the plan's ways in sit off. The generator sizes a
/// building off this same catalogue, so "nearest" is all but exact on a shipped map.
/// </para>
/// <para>
/// <b>Three lists and one array</b>, on the terms <see cref="Car.Body.CarCatalog"/> keeps its fleet and its
/// service vehicles on. A civic roof names the use its building was drawn for (AMB-1a, SRV-1a) and is
/// found by id (<see cref="Hospital"/>, <see cref="PoliceStation"/>, <see cref="RepairShop"/>);
/// <see cref="Match"/> cannot reach it,
/// because a roof lettered HOSPITAL over somebody's front door is a building the town says is a hospital
/// and is not one. <b>A prefab is chosen by the plan and never matched</b>: which one a footprint wears is
/// its zone's look and size, settled where the building is stood (<c>ZoneBuildings</c>).
/// </para>
/// </remarks>
internal sealed class BuildingCatalog
{
    BuildingCatalog(BuildingVariant[] variants, int ordinary, int civic)
    {
        Variants = variants;
        Ordinary = ordinary;
        FirstPrefab = ordinary + civic;
        Hospital = IndexOf("hospital");
        PoliceStation = IndexOf("police_station");
        RepairShop = IndexOf("repair_shop");

        int IndexOf(string id)
        {
            for (var entry = ordinary; entry < FirstPrefab; entry++)
            {
                if (variants[entry].Id == id) return entry;
            }

            throw new InvalidDataException($"Civic.json names no variant with the id '{id}'.");
        }
    }

    /// <summary>The ordinary roofs first, then the civic ones, then the prefabs — one array, because a sheet slot is a sheet slot.</summary>
    public BuildingVariant[] Variants { get; }

    /// <summary>How many of them <see cref="Match"/> draws from, which is the ordinary roofs and not everything here.</summary>
    public int Ordinary { get; }

    /// <summary>Where the prefabs start, which is where a plan's prefab number is counted from (<see cref="CityPlan.BuildingArrays.Prefab"/>).</summary>
    public int FirstPrefab { get; }

    /// <summary>And how many there are altogether, which is what the sheet list is laid for.</summary>
    public int Count => Variants.Length;

    /// <summary>Which roof a hospital wears (AMB-1a), and which ones a police station and a depot wear (SRV-1a).</summary>
    public int Hospital { get; }

    public int PoliceStation { get; }

    public int RepairShop { get; }

    /// <summary>
    /// The roofs, read once, on the terms <see cref="Car.Body.CarCatalog.Shared"/> is: immutable, on
    /// disk, and the same for every town. A town's walls are stood from it as well as its pictures
    /// (OBJ-5a), so it is read where a town is stood up and not only where one is drawn.
    /// </summary>
    public static BuildingCatalog Shared { get; } = Load();

    /// <summary>
    /// <see cref="Shared"/>'s footprints, measured once: the seam a town is laid across (GEN-54). Every
    /// caller that opens a map hands the generator this same reading, so what a town is built of is the art
    /// this build ships and the plan below never learns that a catalogue exists.
    /// </summary>
    public static BuildingSizes Roofs { get; } = Shared.Footprints();

    public static BuildingCatalog Load()
    {
        var ordinary = AssetJson.Catalog(VariantList("Catalog.json"));
        var civic = AssetJson.Catalog(VariantList("Civic.json"));
        var prefabs = AssetJson.Catalog(Path.Combine(ProjectPaths.Assets, "world", "building", "prefabs", "Prefabs.json"));

        BuildingVariant[] variants = [.. ordinary.Select(ReadVariant), .. civic.Select(ReadVariant), .. prefabs.Select(ReadVariant)];
        for (var prefab = ordinary.Length + civic.Length; prefab < variants.Length; prefab++)
        {
            if (variants[prefab].Look is null) throw new InvalidDataException($"Prefabs.json names {variants[prefab].Id}, which says no look.");
        }

        return new BuildingCatalog(variants, ordinary.Length, civic.Length);
    }

    /// <summary>
    /// Which roof a building of this size wears, and whether the art's width axis runs along the
    /// building's own <c>x</c> or across it.
    /// </summary>
    /// <remarks>
    /// The two axis assignments are both offered because a roof is a picture of a rectangle and a
    /// rectangle laid the other way round is the same rectangle: matching without the swap would put a
    /// wide roof on a deep building whenever the plan happened to author it end-on.
    /// </remarks>
    /// <summary>The roof a use names (AMB-1a, SRV-1a), or −1 for the buildings that are only buildings.</summary>
    /// <remarks>
    /// <b>One mapping, read by both of its readers</b>: the generator sizes a service building off the roof
    /// it will wear (GEN-55) and the town draws it in that roof, and a second copy of which id serves which
    /// use is two answers about one hospital.
    /// </remarks>
    public int RoofFor(BuildingUse use) => use switch
    {
        BuildingUse.Hospital => Hospital,
        BuildingUse.PoliceStation => PoliceStation,
        BuildingUse.Depot => RepairShop,
        _ => NoRoof,
    };

    /// <summary>What <see cref="RoofFor"/> answers for a use no roof was drawn for.</summary>
    public const int NoRoof = -1;

    /// <summary>
    /// The footprints a town sizes its buildings at (GEN-54). <b>Handed to whoever lays a town</b>, because
    /// a building is sized by the picture it will wear and the plan may not read a catalogue that sits above
    /// it — the data crosses the seam and the type does not.
    /// </summary>
    public BuildingSizes Footprints()
    {
        var byUseM = new Vector2[BuildingSizes.Uses];
        for (var use = 0; use < byUseM.Length; use++)
        {
            var roof = RoofFor((BuildingUse)use);
            byUseM[use] = roof >= 0 ? Variants[roof].FootprintM : Vector2.Zero;
        }

        var prefabM = new Vector2[Count - FirstPrefab];
        var prefabCornerM = new float[prefabM.Length];
        var prefabLook = new BuildingLook[prefabM.Length];
        for (var prefab = 0; prefab < prefabM.Length; prefab++)
        {
            prefabM[prefab] = Variants[FirstPrefab + prefab].FootprintM;
            prefabCornerM[prefab] = Variants[FirstPrefab + prefab].CornerRadiusM;
            prefabLook[prefab] = Variants[FirstPrefab + prefab].Look!.Value;
        }

        return new BuildingSizes(byUseM, prefabM, prefabCornerM, prefabLook);
    }

    public (int Variant, bool Swapped) Match(Vector2 sizeM)
    {
        var best = 0;
        var bestSwapped = false;
        var bestError = float.MaxValue;

        for (var variant = 0; variant < Ordinary; variant++)
        {
            var authored = Variants[variant].FootprintM;
            var straight = MathF.Abs(authored.X - sizeM.X) + MathF.Abs(authored.Y - sizeM.Y);
            var swapped = MathF.Abs(authored.X - sizeM.Y) + MathF.Abs(authored.Y - sizeM.X);

            if (straight < bestError) (best, bestSwapped, bestError) = (variant, false, straight);
            if (swapped < bestError) (best, bestSwapped, bestError) = (variant, true, swapped);
        }

        return (best, bestSwapped);
    }

    static string VariantList(string file) =>
        Path.Combine(ProjectPaths.Assets, "world", "building", "variants", "common", file);

    static BuildingVariant ReadVariant(string path)
    {
        var variant = AssetJson.Read(path, BuildingVariantJson.Default.BuildingVariantFile);

        var parts = new BuildingPart[variant.PartsM.Length];
        for (var part = 0; part < parts.Length; part++)
        {
            parts[part] = new BuildingPart(variant.PartsM[part].AtM, variant.PartsM[part].SizeM);
        }

        return new BuildingVariant(
            variant.Id, variant.Sprite is { } sprite ? AssetJson.Beside(path, sprite) : null, variant.FootprintM, parts,
            Math.Clamp(variant.CornerRadiusM, 0f, MathF.Min(variant.FootprintM.X, variant.FootprintM.Y) * 0.5f),
            variant.Look is { } look ? Enum.Parse<BuildingLook>(look, ignoreCase: true) : null);
    }
}
