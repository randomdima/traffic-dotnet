using System.Numerics;
using System.Text.Json.Serialization;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Terrain;

/// <summary>Which of the town's two kerbs a kerb's art is laid along (TER-3d).</summary>
internal enum KerbLookKind : byte
{
    /// <summary>The town's kerb, where the carriageway hands over to the walk: its low side the road's.</summary>
    Kerb,

    /// <summary>The walk's own kerb, where the walk hands over to the grass: its low side the grass's.</summary>
    Edging,
}

/// <summary>One kerb's file (<c>assets/world/terrain/ground/kerbs/&lt;id&gt;/&lt;id&gt;.json</c>).</summary>
/// <remarks>Unknown members are skipped: its <c>describe</c> is the generator's and no figure of the town's.</remarks>
internal sealed class KerbLookFile
{
    public required string Id { get; init; }

    public required KerbLookKind Kind { get; init; }

    /// <summary>The strip's picture, its low side at the bottom; none until its art is kept.</summary>
    public string? Sprite { get; init; }

    /// <summary>
    /// How far along the kerb one picture runs before it repeats, which is whole stones: the drawing was asked longer
    /// (<see cref="FootprintM"/>) and cut to its stones' pitch to repeat (<c>qq sprites use</c>).
    /// </summary>
    public float? PictureM { get; init; }

    /// <summary>What the strip was drawn as: its length, and the kerb's width across it.</summary>
    public required Vector2 FootprintM { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true, UseStringEnumConverter = true, Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(KerbLookFile))]
internal sealed partial class KerbLookJson : JsonSerializerContext;

/// <summary>A kerb's art as the catalogue reads it: where its strip's picture is, and how far along the kerb one picture runs.</summary>
internal readonly record struct KerbLook(string Id, string SpritePath, float PictureM);

/// <summary>
/// <b>The kerbs' art</b>, read from <c>assets/world/terrain/ground/kerbs/common/Kerbs.json</c>: the strip the town's kerb
/// is laid as and the walk's (TER-3d) — one of each kind, the first listed, the rest candidates kept to choose from.
/// </summary>
internal sealed class KerbLooks
{
    KerbLooks(KerbLook kerb, KerbLook edging) => (Kerb, Edging) = (kerb, edging);

    public KerbLook Kerb { get; }

    public KerbLook Edging { get; }

    /// <summary>The kerbs' art, read once: data, the same for every town.</summary>
    public static KerbLooks Shared { get; } = Load();

    static KerbLooks Load()
    {
        var list = Path.Combine(ProjectPaths.Assets, "world", "terrain", "ground", "kerbs", "common", "Kerbs.json");
        var first = new KerbLook?[Enum.GetValues<KerbLookKind>().Length];
        foreach (var path in AssetJson.Catalog(list))
        {
            var file = AssetJson.Read(path, KerbLookJson.Default.KerbLookFile);
            if (file.Sprite is null || file.PictureM is not > 0f)
            {
                throw new InvalidDataException($"{path}: {file.Id} is listed among the kerbs' art and has no picture cut to repeat.");
            }

            first[(int)file.Kind] ??= new KerbLook(file.Id, AssetJson.Beside(path, file.Sprite), file.PictureM.Value);
        }

        return new KerbLooks(Listed(KerbLookKind.Kerb), Listed(KerbLookKind.Edging));

        KerbLook Listed(KerbLookKind kind) => first[(int)kind] ?? throw new InvalidDataException($"{list} lists no {kind}.");
    }
}
