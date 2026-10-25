using System.Numerics;
using System.Text.Json.Serialization;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Terrain;

/// <summary>
/// <b>What a piece of a bridge's art is</b> (TER-3b.2, RAIL-8), in the order the ground's bridge sheet files them as
/// layers. The first five are strips laid along the span, each picture's bottom edge on the side its line names; the rest
/// are fittings laid one at a place, their bottom edge toward the deck.
/// </summary>
internal enum BridgeLookKind : byte
{
    /// <summary>The footway across the deck, between the kerb and the parapet: its bottom edge the kerb's.</summary>
    Walkway,

    /// <summary>The carriageway across the deck, kerb to kerb.</summary>
    DeckRoad,

    /// <summary>The expansion joint across the deck at each end of the span, where it meets the road it lands on.</summary>
    Joint,

    /// <summary>The parapet along each edge of the deck: its bottom edge the footway's.</summary>
    Parapet,

    /// <summary>
    /// A rail bridge's side, from its ballast's shoulder out to the deck's edge (RAIL-8): its walkway and its parapet as
    /// one strip, its bottom edge the ballast's.
    /// </summary>
    RailParapet,

    /// <summary>A lamp standing on the parapet, its arm reaching toward the bottom of the picture over the footway.</summary>
    Lamp,

    /// <summary>The nose of a pier standing in the river, poking out from under the deck: its square edge at the bottom.</summary>
    PierNose,

    /// <summary>The pillar a parapet ends on at each corner of the deck.</summary>
    Newel,

    /// <summary>The nose of a rail bridge's pier, as <see cref="PierNose"/> is a road bridge's.</summary>
    RailPierNose,

    /// <summary>The post a rail bridge's side ends on at each corner where it lands, as <see cref="Newel"/> is a road bridge's.</summary>
    RailEndPost,
}

/// <summary>One piece of a bridge's art (<c>assets/world/terrain/ground/bridge/&lt;id&gt;/&lt;id&gt;.json</c>).</summary>
/// <remarks>Unknown members are skipped: its <c>describe</c> is the generator's and no figure of the town's.</remarks>
internal sealed class BridgeLookFile
{
    public required string Id { get; init; }

    public required BridgeLookKind Kind { get; init; }

    /// <summary>The picture; none until its art is kept.</summary>
    public string? Sprite { get; init; }

    /// <summary>How far along a strip one picture runs before it repeats, cut joint to joint (<c>qq sprites use</c>).</summary>
    public float? PictureM { get; init; }

    /// <summary>What the drawing is: its width across the picture and its depth down it.</summary>
    public required Vector2 FootprintM { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true, UseStringEnumConverter = true, Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(BridgeLookFile))]
internal sealed partial class BridgeLookJson : JsonSerializerContext;

/// <summary>A piece of a bridge's art as the catalogue reads it.</summary>
/// <param name="PictureM">How far along its line one picture of a strip runs; nought for a fitting.</param>
/// <param name="FootprintM">The drawing's width across the picture and depth down it, in metres.</param>
internal readonly record struct BridgeLook(string Id, string SpritePath, float PictureM, Vector2 FootprintM);

/// <summary>
/// <b>A bridge's art</b>, read from <c>assets/world/terrain/ground/bridge/common/Bridge.json</c> (TER-3b.2): one piece of
/// each kind, the first listed, the rest candidates kept to choose from.
/// </summary>
internal sealed class BridgeLooks
{
    readonly BridgeLook[] _looks;

    BridgeLooks(BridgeLook[] looks) => _looks = looks;

    public BridgeLook this[BridgeLookKind kind] => _looks[(int)kind];

    /// <summary>How many kinds there are, which is how many layers the ground's bridge sheet has.</summary>
    public static int Kinds => Enum.GetValues<BridgeLookKind>().Length;

    /// <summary>Whether a kind is a strip laid along a line rather than a fitting laid at a place.</summary>
    public static bool IsStrip(BridgeLookKind kind) => kind <= BridgeLookKind.RailParapet;

    /// <summary>A bridge's art, read once: data, the same for every town.</summary>
    public static BridgeLooks Shared { get; } = Load();

    static BridgeLooks Load()
    {
        var list = Path.Combine(ProjectPaths.Assets, "world", "terrain", "ground", "bridge", "common", "Bridge.json");
        var first = new BridgeLook?[Kinds];
        foreach (var path in AssetJson.Catalog(list))
        {
            var file = AssetJson.Read(path, BridgeLookJson.Default.BridgeLookFile);
            if (file.Sprite is null || (IsStrip(file.Kind) && file.PictureM is not > 0f))
            {
                throw new InvalidDataException($"{path}: {file.Id} is listed among a bridge's art and has no picture to lay.");
            }

            first[(int)file.Kind] ??= new BridgeLook(
                file.Id, AssetJson.Beside(path, file.Sprite), file.PictureM ?? 0f, file.FootprintM);
        }

        var looks = new BridgeLook[Kinds];
        for (var kind = 0; kind < looks.Length; kind++)
        {
            looks[kind] = first[kind] ?? throw new InvalidDataException($"{list} lists no {(BridgeLookKind)kind}.");
        }

        return new BridgeLooks(looks);
    }
}
