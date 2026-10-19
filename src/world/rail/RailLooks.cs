using System.Numerics;
using System.Text.Json.Serialization;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Rail;

/// <summary>What a piece of the railway's art is.</summary>
/// <remarks>The crossing's gear has files of its own kinds beside the track's, waiting on art (<c>docs/tech-debt.md</c>); none is read until it is listed.</remarks>
internal enum RailLookKind : byte
{
    /// <summary>A strip of track — ballast, sleepers and rails — the ground repeats along every track (RAIL-3).</summary>
    Track,
}

/// <summary>One piece's file (<c>assets/world/rail/looks/&lt;id&gt;/&lt;id&gt;.json</c>).</summary>
/// <remarks>Unknown members are skipped: a track's file also carries the sleepers' length its picture was laid with, which is <c>qq sprites</c>' and no figure of the town's.</remarks>
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
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true, UseStringEnumConverter = true, Converters = [typeof(Vector2Json)])]
[JsonSerializable(typeof(RailLookFile))]
internal sealed partial class RailLookJson : JsonSerializerContext;

/// <summary>A piece of the railway's art as the catalogue reads it: where its picture is, and what it covers.</summary>
internal readonly record struct RailLook(string Id, RailLookKind Kind, string SpritePath, Vector2 FootprintM);

/// <summary>
/// <b>The railway's art</b>, read from <c>assets/world/rail/looks/common/Railway.json</c>: the track every line is laid as
/// (RAIL-3).
/// </summary>
internal sealed class RailLooks
{
    RailLooks(RailLook track) => Track = track;

    /// <summary>The strip the ground lays along every track, end to end.</summary>
    public RailLook Track { get; }

    /// <summary>The railway's art, read once: data, the same for every town.</summary>
    public static RailLooks Shared { get; } = Load();

    static RailLooks Load()
    {
        var list = Path.Combine(ProjectPaths.Assets, "world", "rail", "looks", "common", "Railway.json");
        RailLook? track = null;
        foreach (var path in AssetJson.Catalog(list))
        {
            var file = AssetJson.Read(path, RailLookJson.Default.RailLookFile);
            if (file.Sprite is null) throw new InvalidDataException($"{path}: {file.Id} is listed in the railway's art and has no picture.");

            var look = new RailLook(file.Id, file.Kind, AssetJson.Beside(path, file.Sprite), file.FootprintM);
            if (file.Kind == RailLookKind.Track) track ??= look;
        }

        return new RailLooks(track ?? throw new InvalidDataException($"{list} lists no track."));
    }
}
