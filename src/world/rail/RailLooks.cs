using System.Numerics;
using System.Text.Json.Serialization;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Rail;

/// <summary>What a piece of the railway's art is.</summary>
/// <remarks>A file of a kind not named here (a relay cabinet's) is a candidate nothing lists yet.</remarks>
internal enum RailLookKind : byte
{
    /// <summary>
    /// A strip of track — its sleepers and two rails, clear between the sleepers — the ground repeats along every track
    /// over its ballast at grade and its deck on a bridge (RAIL-3).
    /// </summary>
    Track,

    /// <summary>A level crossing's arm, its tip at +x, laid at its body's length across whatever lanes it closes (RAIL-7).</summary>
    Barrier,

    /// <summary>The housing an arm pivots at, its arm's end at +x.</summary>
    Drive,

    /// <summary>An upright crossing's head with two lamps, drawn from the front: a candidate nothing lists.</summary>
    Head,

    /// <summary>A crossing's X light over a lane coming in, drawn flat as a junction's head is, and its lit twin (<see cref="RailLookFile.Lit"/>).</summary>
    Light,

    /// <summary>
    /// The bed of ballast under a track at grade (RAIL-3, RAIL-13): a strip the band the town keeps clear deep (RAIL-4),
    /// packed across its middle and fraying into the grass along both long edges.
    /// </summary>
    Ballast,
}

/// <summary>One piece's file (<c>assets/world/rail/looks/&lt;id&gt;/&lt;id&gt;.json</c>).</summary>
/// <remarks>
/// Unknown members are skipped: a track's file also carries the sleepers' length its picture was laid with, and a lamp's
/// file the glass the template frames for the generator, which are <c>qq sprites</c>' and no figure of the town's.
/// </remarks>
internal sealed class RailLookFile
{
    public required string Id { get; init; }

    public required RailLookKind Kind { get; init; }

    /// <summary>The picture, keyed off magenta and cut to the footprint; none until its art is kept.</summary>
    public string? Sprite { get; init; }

    /// <summary>
    /// What its picture covers: a track's length is how far along the track the picture runs before it repeats, and its
    /// width the band it is laid across.
    /// </summary>
    public required Vector2 FootprintM { get; init; }

    /// <summary>A light's file switched on, beside this one: the same picture with its lamps lit.</summary>
    public string? Lit { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true, UseStringEnumConverter = true, Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(RailLookFile))]
internal sealed partial class RailLookJson : JsonSerializerContext;

/// <summary>A piece of the railway's art as the catalogue reads it: where its picture is, and its lit one if it has one, and what it covers.</summary>
internal readonly record struct RailLook(string Id, RailLookKind Kind, string SpritePath, string? LitSpritePath, Vector2 FootprintM);

/// <summary>
/// <b>The railway's art</b>, read from <c>assets/world/rail/looks/common/Railway.json</c>: the track every line is laid as
/// (RAIL-3), and every level crossing's gear (RAIL-7) — one of each kind, the first listed.
/// </summary>
internal sealed class RailLooks
{
    RailLooks(RailLook track, RailLook ballast, RailLook barrier, RailLook drive, RailLook light) =>
        (Track, Ballast, Barrier, Drive, Light) = (track, ballast, barrier, drive, light);

    /// <summary>The sleepers and rails the ground lays along every track, end to end.</summary>
    public RailLook Track { get; }

    /// <summary>The bed of ballast it lays under them where the track stands at grade.</summary>
    public RailLook Ballast { get; }

    public RailLook Barrier { get; }

    public RailLook Drive { get; }

    /// <summary>An X light, dark and lit (<see cref="RailLook.LitSpritePath"/>).</summary>
    public RailLook Light { get; }

    /// <summary>The railway's art, read once: data, the same for every town.</summary>
    public static RailLooks Shared { get; } = Load();

    static RailLooks Load()
    {
        var list = Path.Combine(ProjectPaths.Assets, "world", "rail", "looks", "common", "Railway.json");
        var first = new RailLook?[Enum.GetValues<RailLookKind>().Length];
        foreach (var path in AssetJson.Catalog(list))
        {
            var file = AssetJson.Read(path, RailLookJson.Default.RailLookFile);
            if (file.Sprite is null) throw new InvalidDataException($"{path}: {file.Id} is listed in the railway's art and has no picture.");

            first[(int)file.Kind] ??= new RailLook(file.Id, file.Kind, AssetJson.Beside(path, file.Sprite), LitOf(path, file), file.FootprintM);
        }

        var light = Listed(RailLookKind.Light);
        if (light.LitSpritePath is null) throw new InvalidDataException($"{light.Id} is a crossing's light and has no lit twin.");

        return new RailLooks(
            Listed(RailLookKind.Track), Listed(RailLookKind.Ballast), Listed(RailLookKind.Barrier), Listed(RailLookKind.Drive), light);

        RailLook Listed(RailLookKind kind) => first[(int)kind] ?? throw new InvalidDataException($"{list} lists no {kind}.");
    }

    /// <summary>A piece's lit picture, held to its own footprint: the one is drawn in the other's place.</summary>
    static string? LitOf(string path, RailLookFile file)
    {
        if (file.Lit is null) return null;

        var litPath = AssetJson.Beside(path, file.Lit);
        var lit = AssetJson.Read(litPath, RailLookJson.Default.RailLookFile);
        if (lit.Sprite is null || lit.FootprintM != file.FootprintM)
        {
            throw new InvalidDataException($"{litPath}: {file.Id}'s lit twin {lit.Id} has no picture, or covers other than {file.FootprintM} m.");
        }

        return AssetJson.Beside(litPath, lit.Sprite);
    }
}
