using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Statics;

/// <summary>One roof: the image, and the footprint it was drawn at.</summary>
/// <remarks>
/// The art is drawn <b>door at +y</b> — the porch at the bottom of the image, since this world's y grows
/// downwards — which is the one fact about a building's picture the town has to know: everything else a
/// building does is its plan's box and its plan's ways in.
/// </remarks>
internal readonly record struct BuildingVariant(string Id, string SpritePath, Vector2 FootprintM);

/// <summary>
/// The roofs a building can wear, read from <c>assets/…/Catalog.json</c>.
/// </summary>
/// <remarks>
/// A roof is picked by a rule and never by a draw: the nearest authored footprint in the art's own axes,
/// turned so its door lands on the wall the plan's ways in sit off. The generator sizes a building off
/// this same catalogue, so "nearest" is all but exact on a shipped map.
/// </remarks>
internal sealed class BuildingCatalog
{
    BuildingCatalog(BuildingVariant[] variants) => Variants = variants;

    public BuildingVariant[] Variants { get; }

    public int Count => Variants.Length;

    public static BuildingCatalog Load()
    {
        var catalogPath = Path.Combine(ProjectPaths.Assets, "world", "building", "variants", "common", "Catalog.json");
        var entries = AssetJson.Catalog(catalogPath);

        var variants = new BuildingVariant[entries.Length];
        for (var entry = 0; entry < entries.Length; entry++) variants[entry] = ReadVariant(entries[entry]);

        return new BuildingCatalog(variants);
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
    public (int Variant, bool Swapped) Match(Vector2 sizeM)
    {
        var best = 0;
        var bestSwapped = false;
        var bestError = float.MaxValue;

        for (var variant = 0; variant < Variants.Length; variant++)
        {
            var authored = Variants[variant].FootprintM;
            var straight = MathF.Abs(authored.X - sizeM.X) + MathF.Abs(authored.Y - sizeM.Y);
            var swapped = MathF.Abs(authored.X - sizeM.Y) + MathF.Abs(authored.Y - sizeM.X);

            if (straight < bestError) (best, bestSwapped, bestError) = (variant, false, straight);
            if (swapped < bestError) (best, bestSwapped, bestError) = (variant, true, swapped);
        }

        return (best, bestSwapped);
    }

    static BuildingVariant ReadVariant(string path)
    {
        var variant = AssetJson.Read(path, BuildingVariantJson.Default.BuildingVariantFile);
        return new BuildingVariant(variant.Id, AssetJson.Beside(path, variant.Sprite), variant.FootprintM);
    }
}
